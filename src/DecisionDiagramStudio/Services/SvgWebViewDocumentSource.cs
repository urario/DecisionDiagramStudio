using System.Net;
using System.Security.Cryptography;
using DecisionDiagramStudio.Services.Interfaces;

namespace DecisionDiagramStudio.Services;

/// <summary>
/// Builds the nonce-protected WebView2 document used for diagram SVG previews.
/// </summary>
public sealed class SvgWebViewDocumentSource : ISvgWebViewDocumentSource
{
    private const string Style = "html,body{height:100%;margin:0;background:#f8f8f8;color:#1f1f1f;font-family:Segoe UI,Arial,sans-serif;overflow:hidden;}" +
        ".surface{position:relative;height:100%;overflow:hidden;box-sizing:border-box;}" +
        ".viewport{position:absolute;inset:0;overflow:hidden;cursor:grab;touch-action:none;}" +
        ".viewport.is-panning{cursor:grabbing;}" +
        ".diagram-layer{position:absolute;left:0;top:0;transform-origin:0 0;will-change:transform;}" +
        ".diagram-layer svg{display:block;max-width:none;height:auto;}" +
        ".controls{position:absolute;right:12px;top:12px;z-index:2;display:flex;gap:8px;}" +
        ".controls button{border:1px solid #c7c7c7;border-radius:4px;background:#ffffff;color:#1f1f1f;font:12px Segoe UI,Arial,sans-serif;padding:5px 10px;box-shadow:0 1px 3px rgba(0,0,0,0.12);}" +
        ".controls button:hover{background:#f0f0f0;}" +
        ".placeholder{height:100%;display:flex;align-items:center;justify-content:center;color:#666;font-size:14px;}";

    private const string Script = """
(() => {
    const viewport = document.getElementById('diagramViewport');
    const layer = document.getElementById('diagramLayer');
    const resetButton = document.getElementById('resetZoomButton');
    if (!viewport || !layer) {
        return;
    }

    const state = {
        scale: 1,
        x: 0,
        y: 0,
        pointerId: null,
        startClientX: 0,
        startClientY: 0,
        startX: 0,
        startY: 0
    };
    const minScale = 0.2;
    const maxScale = 8;
    const variableNamePattern = /^[a-zA-Z_][a-zA-Z0-9_]*$/;

    function clamp(value, min, max) {
        return Math.min(Math.max(value, min), max);
    }

    function render() {
        layer.style.transform = `translate(${state.x}px, ${state.y}px) scale(${state.scale})`;
    }

    function fitDiagram() {
        const svg = layer.querySelector('svg');
        const viewportRect = viewport.getBoundingClientRect();
        if (!svg || viewportRect.width <= 0 || viewportRect.height <= 0) {
            return;
        }

        layer.style.transform = 'none';
        const svgRect = svg.getBoundingClientRect();
        if (svgRect.width <= 0 || svgRect.height <= 0) {
            return;
        }

        const padding = 48;
        const scaleX = Math.max((viewportRect.width - padding) / svgRect.width, minScale);
        const scaleY = Math.max((viewportRect.height - padding) / svgRect.height, minScale);
        state.scale = clamp(Math.min(scaleX, scaleY, 1), minScale, maxScale);
        state.x = (viewportRect.width - (svgRect.width * state.scale)) / 2;
        state.y = (viewportRect.height - (svgRect.height * state.scale)) / 2;
        render();
    }

    function zoomAt(clientX, clientY, scaleFactor) {
        const viewportRect = viewport.getBoundingClientRect();
        const pointerX = clientX - viewportRect.left;
        const pointerY = clientY - viewportRect.top;
        const diagramX = (pointerX - state.x) / state.scale;
        const diagramY = (pointerY - state.y) / state.scale;
        const nextScale = clamp(state.scale * scaleFactor, minScale, maxScale);
        state.x = pointerX - (diagramX * nextScale);
        state.y = pointerY - (diagramY * nextScale);
        state.scale = nextScale;
        render();
    }

    function normalizeNodeId(value, fallbackIndex) {
        const match = String(value || '').match(/\d+/);
        return match ? `n${match[0]}` : `n${fallbackIndex}`;
    }

    function normalizeVariableName(value) {
        const candidate = String(value || '').trim();
        return variableNamePattern.test(candidate) ? candidate : '_terminal';
    }

    function inferNodeType(group) {
        const text = group.textContent?.trim() || '';
        return text === '0' || text === '1' || text === 'False' || text === 'True' ? 'terminal' : 'internal';
    }

    function wireNodeClickMessages() {
        const nodes = layer.querySelectorAll('[data-node-id], g.node');
        nodes.forEach((node, index) => {
            node.addEventListener('click', event => {
                event.stopPropagation();
                const title = node.querySelector('title')?.textContent || '';
                const nodeId = normalizeNodeId(node.dataset.nodeId || node.id || title, index + 1);
                const nodeType = node.dataset.nodeType === 'terminal' ? 'terminal' : inferNodeType(node);
                const variableName = normalizeVariableName(node.dataset.variable || title);
                window.chrome?.webview?.postMessage({
                    type: 'nodeClick',
                    nodeId,
                    variableName,
                    nodeType
                });
            });
        });
    }

    viewport.addEventListener('wheel', event => {
        event.preventDefault();
        const scaleFactor = Math.exp(-event.deltaY * 0.001);
        zoomAt(event.clientX, event.clientY, scaleFactor);
    }, { passive: false });

    viewport.addEventListener('pointerdown', event => {
        if (event.button !== 0) {
            return;
        }

        state.pointerId = event.pointerId;
        state.startClientX = event.clientX;
        state.startClientY = event.clientY;
        state.startX = state.x;
        state.startY = state.y;
        viewport.classList.add('is-panning');
        viewport.setPointerCapture(event.pointerId);
    });

    viewport.addEventListener('pointermove', event => {
        if (state.pointerId !== event.pointerId) {
            return;
        }

        state.x = state.startX + event.clientX - state.startClientX;
        state.y = state.startY + event.clientY - state.startClientY;
        render();
    });

    function endPan(event) {
        if (state.pointerId !== event.pointerId) {
            return;
        }

        state.pointerId = null;
        viewport.classList.remove('is-panning');
        if (viewport.hasPointerCapture(event.pointerId)) {
            viewport.releasePointerCapture(event.pointerId);
        }
    }

    viewport.addEventListener('pointerup', endPan);
    viewport.addEventListener('pointercancel', endPan);
    viewport.addEventListener('dblclick', fitDiagram);
    resetButton?.addEventListener('click', fitDiagram);
    window.addEventListener('resize', fitDiagram);
    wireNodeClickMessages();
    requestAnimationFrame(fitDiagram);
})();
""";

    /// <inheritdoc />
    public string CreateDocument(string svgContent)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var csp = "default-src 'none'; img-src data:; style-src 'unsafe-inline'; script-src 'nonce-" + nonce + "'; object-src 'none'; base-uri 'none'";
        var body = string.IsNullOrWhiteSpace(svgContent)
            ? "<div class=\"placeholder\">Build a diagram to preview SVG.</div>"
            : "<div class=\"controls\"><button id=\"resetZoomButton\" type=\"button\">Reset zoom</button></div>" +
                "<div id=\"diagramViewport\" class=\"viewport\"><div id=\"diagramLayer\" class=\"diagram-layer\">" +
                svgContent +
                "</div></div>";

        return "<!doctype html><html><head><meta http-equiv=\"Content-Security-Policy\" content=\"" +
            WebUtility.HtmlEncode(csp) +
            "\"><style>" +
            Style +
            "</style></head><body><div class=\"surface\">" +
            body +
            "</div><script nonce=\"" +
            nonce +
            "\">" +
            Script +
            "</script></body></html>";
    }
}
