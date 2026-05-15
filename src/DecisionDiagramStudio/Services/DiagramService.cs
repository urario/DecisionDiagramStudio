using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DecisionDiagramSharp;
using DecisionDiagramSharp.Diagnostics;
using DecisionDiagramStudio.Models;
using DecisionDiagramStudio.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DecisionDiagramStudio.Services;

/// <summary>
/// Wraps DecisionDiagramSharp managers and converts library results into application models.
/// </summary>
public sealed class DiagramService : IDiagramService
{
    /// <summary>
    /// The largest BDD variable count for direct BDT DOT generation.
    /// </summary>
    public const int MaxBdtVariableCount = 10;

    private static readonly Regex VariableNameRegex = new("^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.CultureInvariant);

    private readonly DecisionDiagramOptions _options;
    private readonly ILogger<DiagramService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private DecisionDiagramManager _manager;
    private Bdd? _currentBdd;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagramService"/> class with default library options.
    /// </summary>
    public DiagramService()
        : this(new DecisionDiagramOptions(), NullLogger<DiagramService>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagramService"/> class.
    /// </summary>
    /// <param name="options">Shared options for the wrapped decision diagram managers.</param>
    public DiagramService(DecisionDiagramOptions options)
        : this(options, NullLogger<DiagramService>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagramService"/> class.
    /// </summary>
    /// <param name="options">Shared options for the wrapped decision diagram managers.</param>
    /// <param name="logger">The logger used for service diagnostics.</param>
    public DiagramService(DecisionDiagramOptions options, ILogger<DiagramService> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _manager = new DecisionDiagramManager(_options);
    }

    internal Func<CancellationToken, Task>? CriticalSectionProbeAsync { get; set; }

    /// <inheritdoc />
    public async Task<DiagramSession> BuildAsync(string[] variableNames, int[] intValueTable, DiagramFamily family, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(variableNames);
        ArgumentNullException.ThrowIfNull(intValueTable);

        var stopwatch = Stopwatch.StartNew();
        var variableCount = variableNames.Length;
        var rowCount = intValueTable.Length;
        var hasSemaphore = false;

        _logger.LogDebug(
            "Diagram build requested. Family={Family} VariableCount={VariableCount} RowCount={RowCount}",
            family,
            variableCount,
            rowCount);

        try
        {
            ValidateVariableNames(variableNames);
            ValidateTruthTableShape(variableNames, intValueTable);

            if (family != DiagramFamily.BDD)
            {
                throw new NotSupportedException("Only BDD builds are supported in v0.1.");
            }

            await _semaphore.WaitAsync(ct).ConfigureAwait(false);
            hasSemaphore = true;

            if (CriticalSectionProbeAsync is { } probe)
            {
                await probe(ct).ConfigureAwait(false);
            }

            var session = await Task.Run(() => BuildBddSession(variableNames, intValueTable), ct).ConfigureAwait(false);
            _logger.LogDebug(
                "Diagram build completed. Family={Family} VariableCount={VariableCount} RowCount={RowCount} ReachableNodeCount={ReachableNodeCount} ElapsedMs={ElapsedMs}",
                session.Family,
                session.Statistics.VariableCount,
                session.IntValueTable?.Length ?? 0,
                session.Statistics.ReachableNodeCount,
                stopwatch.ElapsedMilliseconds);
            return session;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(
                "Diagram build request is not supported. Family={Family} VariableCount={VariableCount} RowCount={RowCount} ExceptionType={ExceptionType}",
                family,
                variableCount,
                rowCount,
                ex.GetType().Name);
            throw;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                "Diagram build input validation failed. Family={Family} VariableCount={VariableCount} RowCount={RowCount} ExceptionType={ExceptionType}",
                family,
                variableCount,
                rowCount,
                ex.GetType().Name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Diagram build failed. Family={Family} VariableCount={VariableCount} RowCount={RowCount} ExceptionType={ExceptionType}",
                family,
                variableCount,
                rowCount,
                ex.GetType().Name);
            throw;
        }
        finally
        {
            if (hasSemaphore)
            {
                _semaphore.Release();
            }
        }
    }

    /// <inheritdoc />
    public Task<string> GetBdtDotAsync(DiagramSession session, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);

        var stopwatch = Stopwatch.StartNew();
        var variableCount = session.VariableNames.Length;

        _logger.LogDebug("BDT DOT generation requested. VariableCount={VariableCount}", variableCount);

        try
        {
            ct.ThrowIfCancellationRequested();

            if (session.Family != DiagramFamily.BDD)
            {
                throw new NotSupportedException("BDT DOT generation is only available for BDD sessions.");
            }

            ValidateVariableNames(session.VariableNames);
            ValidateTruthTableShape(session.VariableNames, session.IntValueTable);

            variableCount = session.VariableNames.Length;
            if (variableCount > MaxBdtVariableCount)
            {
                throw new BdtVariableLimitException(variableCount, MaxBdtVariableCount);
            }

            var dotText = BuildBdtDot(session.VariableNames, session.IntValueTable!);
            _logger.LogDebug(
                "BDT DOT generation completed. VariableCount={VariableCount} DotLength={DotLength} ElapsedMs={ElapsedMs}",
                variableCount,
                dotText.Length,
                stopwatch.ElapsedMilliseconds);
            return Task.FromResult(dotText);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (BdtVariableLimitException ex)
        {
            _logger.LogWarning(
                "BDT DOT generation exceeded the variable limit. VariableCount={VariableCount} MaxVariableCount={MaxVariableCount}",
                ex.VariableCount,
                ex.MaxVariableCount);
            throw;
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(
                "BDT DOT generation request is not supported. VariableCount={VariableCount} ExceptionType={ExceptionType}",
                variableCount,
                ex.GetType().Name);
            throw;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                "BDT DOT input validation failed. VariableCount={VariableCount} ExceptionType={ExceptionType}",
                variableCount,
                ex.GetType().Name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "BDT DOT generation failed. VariableCount={VariableCount} ExceptionType={ExceptionType}",
                variableCount,
                ex.GetType().Name);
            throw;
        }
    }

    internal Bdd BuildBddFromTruthTable(int[] values, string[] variableNames)
    {
        ValidateVariableNames(variableNames);
        ValidateTruthTableShape(variableNames, values);
        EnsureBddVariableSchema(variableNames);

        var bddManager = _manager.Bdd;
        var variableIds = new VariableId[variableNames.Length];
        for (var i = 0; i < variableNames.Length; i++)
        {
            variableIds[i] = bddManager.GetOrAddVariable(variableNames[i]);
        }

        var root = bddManager.False;
        for (var mask = 0; mask < values.Length; mask++)
        {
            var value = values[mask];
            if (value == 0)
            {
                continue;
            }

            var term = bddManager.True;
            for (var variable = 0; variable < variableIds.Length; variable++)
            {
                var variableNode = bddManager.Var(variableIds[variable]);
                var literal = IsBitSet(mask, variable) ? variableNode : bddManager.Not(variableNode);
                term = bddManager.And(term, literal);
            }

            root = bddManager.Or(root, term);
        }

        return root;
    }

    private static void ValidateVariableNames(string[] variableNames)
    {
        ArgumentNullException.ThrowIfNull(variableNames);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < variableNames.Length; i++)
        {
            var variableName = variableNames[i];
            if (variableName is null || !VariableNameRegex.IsMatch(variableName))
            {
                throw new ArgumentException("Variable names must match ^[a-zA-Z_][a-zA-Z0-9_]*$.", nameof(variableNames));
            }

            if (!seen.Add(variableName))
            {
                throw new ArgumentException("Variable names must be unique.", nameof(variableNames));
            }
        }
    }

    private static void ValidateTruthTableShape(string[] variableNames, int[]? values)
    {
        ArgumentNullException.ThrowIfNull(variableNames);
        ArgumentNullException.ThrowIfNull(values);

        if (variableNames.Length > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(variableNames), "Truth tables above 30 variables are not supported by the Int32 row-count contract.");
        }

        var expectedLength = 1 << variableNames.Length;
        if (values.Length != expectedLength)
        {
            throw new ArgumentException(
                "The value table length must be 2^variableCount.",
                nameof(values));
        }

        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] is not (0 or 1))
            {
                throw new ArgumentException("BDD value tables may contain only 0 or 1.", nameof(values));
            }
        }
    }

    private static string BuildBdtDot(string[] variableNames, int[] values)
    {
        var variableCount = variableNames.Length;
        var totalNodeCount = (1 << (variableCount + 1)) - 1;
        var firstLeafIndex = (1 << variableCount) - 1;
        var sb = new StringBuilder();

        sb.AppendLine("digraph BDT {");
        sb.AppendLine("  rankdir=TB;");
        for (var nodeIndex = 0; nodeIndex < totalNodeCount; nodeIndex++)
        {
            if (nodeIndex < firstLeafIndex)
            {
                var level = GetHeapLevel(nodeIndex);
                sb.AppendLine(
                    "  bdt" + nodeIndex.ToString(CultureInfo.InvariantCulture) +
                    " [label=\"" + EscapeDotLabel(variableNames[level]) + "\", shape=circle];");
            }
            else
            {
                var leafOffset = nodeIndex - firstLeafIndex;
                var mask = ConvertLeafOffsetToTruthTableMask(leafOffset, variableCount);
                sb.AppendLine(
                    "  bdt" + nodeIndex.ToString(CultureInfo.InvariantCulture) +
                    " [label=\"" + values[mask].ToString(CultureInfo.InvariantCulture) + "\", shape=box];");
            }
        }

        for (var nodeIndex = 0; nodeIndex < firstLeafIndex; nodeIndex++)
        {
            var lowChild = (nodeIndex * 2) + 1;
            var highChild = lowChild + 1;
            sb.AppendLine(
                "  bdt" + nodeIndex.ToString(CultureInfo.InvariantCulture) +
                " -> bdt" + lowChild.ToString(CultureInfo.InvariantCulture) +
                " [style=dashed,label=\"0\"];");
            sb.AppendLine(
                "  bdt" + nodeIndex.ToString(CultureInfo.InvariantCulture) +
                " -> bdt" + highChild.ToString(CultureInfo.InvariantCulture) +
                " [style=solid,label=\"1\"];");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private DiagramSession BuildBddSession(string[] variableNames, int[] intValueTable)
    {
        var bdd = BuildBddFromTruthTable(intValueTable, variableNames);
        _currentBdd = bdd;

        var statistics = AppDiagramStatistics.ForBdd(_manager.Bdd.GetStatistics(bdd));
        var dotText = BddDiagnostics.ToDot(_manager.Bdd, bdd);
        _logger.LogTrace(
            "BDD session materialized. VariableCount={VariableCount} DotLength={DotLength} ReachableNodeCount={ReachableNodeCount}",
            variableNames.Length,
            dotText.Length,
            statistics.ReachableNodeCount);

        return new DiagramSession
        {
            Family = DiagramFamily.BDD,
            VariableNames = (string[])variableNames.Clone(),
            VariableOrder = Enumerable.Range(0, variableNames.Length).ToArray(),
            IntValueTable = (int[])intValueTable.Clone(),
            SetInput = null,
            DotText = dotText,
            Statistics = statistics,
            LastModified = DateTime.UtcNow,
        };
    }

    private void EnsureBddVariableSchema(string[] variableNames)
    {
        var bddManager = _manager.Bdd;
        if (bddManager.VariableCount == variableNames.Length)
        {
            var sameSchema = true;
            for (var i = 0; i < variableNames.Length; i++)
            {
                if (!StringComparer.Ordinal.Equals(bddManager.GetVariableName(new VariableId(i)), variableNames[i]))
                {
                    sameSchema = false;
                    break;
                }
            }

            if (sameSchema)
            {
                return;
            }
        }

        _manager = new DecisionDiagramManager(_options);
        _currentBdd = null;
        _logger.LogDebug("Decision diagram manager reset for a new BDD variable schema. VariableCount={VariableCount}", variableNames.Length);
    }

    private static int ConvertLeafOffsetToTruthTableMask(int leafOffset, int variableCount)
    {
        var mask = 0;
        for (var variable = 0; variable < variableCount; variable++)
        {
            var pathBit = (leafOffset >> (variableCount - variable - 1)) & 1;
            if (pathBit == 1)
            {
                mask |= 1 << variable;
            }
        }

        return mask;
    }

    private static int GetHeapLevel(int nodeIndex)
    {
        var level = 0;
        var firstIndexAtLevel = 0;
        var nodesAtLevel = 1;

        while (nodeIndex >= firstIndexAtLevel + nodesAtLevel)
        {
            firstIndexAtLevel += nodesAtLevel;
            nodesAtLevel *= 2;
            level++;
        }

        return level;
    }

    private static bool IsBitSet(int mask, int variable)
    {
        return (mask & (1 << variable)) != 0;
    }

    private static string EscapeDotLabel(string label)
    {
        return label.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
