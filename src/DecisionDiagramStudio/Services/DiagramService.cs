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
    private Zdd? _previousZdd;
    private Zdd? _currentZdd;
    private Mtbdd? _currentMtbdd;
    private Zmtbdd? _currentZmtbdd;
    private string[] _currentZddVariableNames = [];

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
            ValidateIntegerValueTableShape(variableNames, intValueTable);
            ValidateIntegerValueTableFamily(family);
            if (family == DiagramFamily.BDD)
            {
                ValidateBddValueTableValues(intValueTable);
            }

            await _semaphore.WaitAsync(ct).ConfigureAwait(false);
            hasSemaphore = true;

            if (CriticalSectionProbeAsync is { } probe)
            {
                await probe(ct).ConfigureAwait(false);
            }

            var session = await Task.Run(() => family switch
            {
                DiagramFamily.BDD => BuildBddSession(variableNames, intValueTable),
                DiagramFamily.MTBDD => BuildMtbddSession(variableNames, intValueTable),
                DiagramFamily.ZMTBDD => BuildZmtbddSession(variableNames, intValueTable),
                _ => throw new NotSupportedException("Use the set-family overload for ZDD builds."),
            }, ct).ConfigureAwait(false);
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
    public async Task<DiagramSession> BuildAsync(
        string[] variableNames,
        IReadOnlyList<IReadOnlyList<string>> setInput,
        DiagramFamily family,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(variableNames);
        ArgumentNullException.ThrowIfNull(setInput);

        var stopwatch = Stopwatch.StartNew();
        var variableCount = variableNames.Length;
        var setCount = setInput.Count;
        var hasSemaphore = false;

        _logger.LogDebug(
            "ZDD build requested. Family={Family} VariableCount={VariableCount} SetInputCount={SetInputCount}",
            family,
            variableCount,
            setCount);

        try
        {
            ValidateVariableNames(variableNames);
            ValidateSetInput(variableNames, setInput);

            if (family != DiagramFamily.ZDD)
            {
                throw new NotSupportedException("Only ZDD set-family builds are supported by this overload.");
            }

            await _semaphore.WaitAsync(ct).ConfigureAwait(false);
            hasSemaphore = true;

            var session = await Task.Run(() => BuildZddSession(variableNames, setInput, updateHistory: true), ct).ConfigureAwait(false);
            _logger.LogDebug(
                "ZDD build completed. VariableCount={VariableCount} SetCount={SetCount} ReachableNodeCount={ReachableNodeCount} ElapsedMs={ElapsedMs}",
                session.Statistics.VariableCount,
                session.Statistics.SetCount,
                session.Statistics.ReachableNodeCount,
                stopwatch.ElapsedMilliseconds);
            return session;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            _logger.LogWarning(
                "ZDD build request failed validation. Family={Family} VariableCount={VariableCount} SetInputCount={SetInputCount} ExceptionType={ExceptionType}",
                family,
                variableCount,
                setCount,
                ex.GetType().Name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "ZDD build failed. Family={Family} VariableCount={VariableCount} SetInputCount={SetInputCount} ExceptionType={ExceptionType}",
                family,
                variableCount,
                setCount,
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
    public async Task<DiagramSession> ApplyZddOperationAsync(ZddOperation operation, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var hasSemaphore = false;

        _logger.LogDebug("ZDD operation requested. Operation={Operation}", operation);

        try
        {
            await _semaphore.WaitAsync(ct).ConfigureAwait(false);
            hasSemaphore = true;

            if (_previousZdd is null || _currentZdd is null)
            {
                throw new InvalidOperationException("Two ZDD operands must be built before applying a set-family operation.");
            }

            var session = await Task.Run(() => ApplyZddOperation(operation), ct).ConfigureAwait(false);
            _logger.LogDebug(
                "ZDD operation completed. Operation={Operation} SetCount={SetCount} ElapsedMs={ElapsedMs}",
                operation,
                session.Statistics.SetCount,
                stopwatch.ElapsedMilliseconds);
            return session;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "ZDD operation failed. Operation={Operation} ExceptionType={ExceptionType}",
                operation,
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
            ValidateIntegerValueTableShape(session.VariableNames, session.IntValueTable);
            ValidateBddValueTableValues(session.IntValueTable!);

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
        ValidateIntegerValueTableShape(variableNames, values);
        ValidateBddValueTableValues(values);
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

    private static void ValidateIntegerValueTableFamily(DiagramFamily family)
    {
        if (family is DiagramFamily.BDD or DiagramFamily.MTBDD or DiagramFamily.ZMTBDD)
        {
            return;
        }

        throw new NotSupportedException("Integer value tables are supported for BDD, MTBDD, and ZMTBDD. Use the set-family overload for ZDD.");
    }

    private static void ValidateIntegerValueTableShape(string[] variableNames, int[]? values)
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
    }

    private static void ValidateBddValueTableValues(int[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] is not (0 or 1))
            {
                throw new ArgumentException("BDD value tables may contain only 0 or 1.", nameof(values));
            }
        }
    }

    private static void ValidateSetInput(string[] variableNames, IReadOnlyList<IReadOnlyList<string>> setInput)
    {
        ArgumentNullException.ThrowIfNull(variableNames);
        ArgumentNullException.ThrowIfNull(setInput);

        var allowed = variableNames.ToHashSet(StringComparer.Ordinal);
        for (var setIndex = 0; setIndex < setInput.Count; setIndex++)
        {
            var set = setInput[setIndex] ?? throw new ArgumentException("ZDD set input may not contain null sets.", nameof(setInput));
            for (var memberIndex = 0; memberIndex < set.Count; memberIndex++)
            {
                var member = set[memberIndex];
                if (member is null || !VariableNameRegex.IsMatch(member))
                {
                    throw new ArgumentException("ZDD set members must match ^[a-zA-Z_][a-zA-Z0-9_]*$.", nameof(setInput));
                }

                if (!allowed.Contains(member))
                {
                    throw new ArgumentException("ZDD set members must be declared in variableNames: " + member, nameof(setInput));
                }
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

    private DiagramSession BuildMtbddSession(string[] variableNames, int[] intValueTable)
    {
        EnsureMtbddVariableSchema(variableNames);
        var mtbddManager = _manager.Mtbdd;
        for (var i = 0; i < variableNames.Length; i++)
        {
            _ = mtbddManager.GetOrAddVariable(variableNames[i]);
        }

        var mtbdd = mtbddManager.Create(intValueTable);
        _currentMtbdd = mtbdd;

        var statistics = AppDiagramStatistics.ForMtbdd(mtbddManager.GetStatistics(mtbdd));
        var dotText = MtbddDiagnostics.ToDot(mtbddManager, mtbdd);
        _logger.LogTrace(
            "MTBDD session materialized. VariableCount={VariableCount} DotLength={DotLength} ReachableNodeCount={ReachableNodeCount} ReachableTerminalCount={ReachableTerminalCount}",
            variableNames.Length,
            dotText.Length,
            statistics.ReachableNodeCount,
            statistics.ReachableTerminalCount);

        return new DiagramSession
        {
            Family = DiagramFamily.MTBDD,
            VariableNames = (string[])variableNames.Clone(),
            VariableOrder = Enumerable.Range(0, variableNames.Length).ToArray(),
            IntValueTable = (int[])intValueTable.Clone(),
            SetInput = null,
            DotText = dotText,
            Statistics = statistics,
            LastModified = DateTime.UtcNow,
        };
    }

    private DiagramSession BuildZmtbddSession(string[] variableNames, int[] intValueTable)
    {
        EnsureZmtbddVariableSchema(variableNames);
        var zmtbddManager = _manager.Zmtbdd;
        for (var i = 0; i < variableNames.Length; i++)
        {
            _ = zmtbddManager.GetOrAddVariable(variableNames[i]);
        }

        var zmtbdd = zmtbddManager.Create(intValueTable);
        _currentZmtbdd = zmtbdd;

        var statistics = AppDiagramStatistics.ForZmtbdd(zmtbddManager.GetStatistics(zmtbdd));
        var dotText = ZmtbddDiagnostics.ToDot(zmtbddManager, zmtbdd);
        _logger.LogTrace(
            "ZMTBDD session materialized. VariableCount={VariableCount} DotLength={DotLength} ReachableNodeCount={ReachableNodeCount} ReachableTerminalCount={ReachableTerminalCount}",
            variableNames.Length,
            dotText.Length,
            statistics.ReachableNodeCount,
            statistics.ReachableTerminalCount);

        return new DiagramSession
        {
            Family = DiagramFamily.ZMTBDD,
            VariableNames = (string[])variableNames.Clone(),
            VariableOrder = Enumerable.Range(0, variableNames.Length).ToArray(),
            IntValueTable = (int[])intValueTable.Clone(),
            SetInput = null,
            DotText = dotText,
            Statistics = statistics,
            LastModified = DateTime.UtcNow,
        };
    }

    private DiagramSession BuildZddSession(
        string[] variableNames,
        IReadOnlyList<IReadOnlyList<string>> setInput,
        bool updateHistory)
    {
        EnsureZddVariableSchema(variableNames);
        var zddManager = _manager.Zdd;
        for (var i = 0; i < variableNames.Length; i++)
        {
            _ = zddManager.GetOrAddVariable(variableNames[i]);
        }

        var normalizedInput = CloneSetInput(setInput);
        var zdd = zddManager.MakeFamily(normalizedInput);
        if (updateHistory)
        {
            _previousZdd = _currentZdd;
            _currentZdd = zdd;
            _currentZddVariableNames = (string[])variableNames.Clone();
        }

        return MaterializeZddSession(zdd, variableNames, normalizedInput);
    }

    private DiagramSession ApplyZddOperation(ZddOperation operation)
    {
        var zddManager = _manager.Zdd;
        var result = operation switch
        {
            ZddOperation.Union => zddManager.Union(_previousZdd!.Value, _currentZdd!.Value),
            ZddOperation.Intersection => zddManager.Intersect(_previousZdd!.Value, _currentZdd!.Value),
            ZddOperation.Difference => zddManager.Difference(_previousZdd!.Value, _currentZdd!.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unsupported ZDD operation."),
        };

        _previousZdd = _currentZdd;
        _currentZdd = result;
        var setInput = EnumerateZddSetInput(result);
        return MaterializeZddSession(result, _currentZddVariableNames, setInput);
    }

    private DiagramSession MaterializeZddSession(
        Zdd zdd,
        string[] variableNames,
        IReadOnlyList<IReadOnlyList<string>> setInput)
    {
        var zddManager = _manager.Zdd;
        var setCount = zddManager.CountSets(zdd);
        var statistics = AppDiagramStatistics.ForZdd(zddManager.GetStatistics(zdd), setCount);
        var dotText = ZddDiagnostics.ToDot(zddManager, zdd);
        _logger.LogTrace(
            "ZDD session materialized. VariableCount={VariableCount} DotLength={DotLength} SetCount={SetCount}",
            variableNames.Length,
            dotText.Length,
            setCount);

        return new DiagramSession
        {
            Family = DiagramFamily.ZDD,
            VariableNames = (string[])variableNames.Clone(),
            VariableOrder = Enumerable.Range(0, variableNames.Length).ToArray(),
            IntValueTable = null,
            SetInput = CloneSetInput(setInput),
            DotText = dotText,
            Statistics = statistics,
            LastModified = DateTime.UtcNow,
        };
    }

    private IReadOnlyList<IReadOnlyList<string>> EnumerateZddSetInput(Zdd zdd)
    {
        var sets = _manager.Zdd.EnumerateSets(zdd);
        var result = new List<IReadOnlyList<string>>(sets.Count);
        foreach (var set in sets)
        {
            result.Add(set.Select(id => _manager.Zdd.GetVariableName(id)).ToArray());
        }

        return result;
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

        ResetManagerForVariableSchema();
        _logger.LogDebug("Decision diagram manager reset for a new BDD variable schema. VariableCount={VariableCount}", variableNames.Length);
    }

    private void EnsureZddVariableSchema(string[] variableNames)
    {
        var zddManager = _manager.Zdd;
        if (zddManager.VariableCount == variableNames.Length)
        {
            var sameSchema = true;
            for (var i = 0; i < variableNames.Length; i++)
            {
                if (!StringComparer.Ordinal.Equals(zddManager.GetVariableName(new VariableId(i)), variableNames[i]))
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

        ResetManagerForVariableSchema();
        _logger.LogDebug("Decision diagram manager reset for a new ZDD variable schema. VariableCount={VariableCount}", variableNames.Length);
    }

    private void EnsureMtbddVariableSchema(string[] variableNames)
    {
        var mtbddManager = _manager.Mtbdd;
        if (mtbddManager.VariableCount == variableNames.Length)
        {
            var sameSchema = true;
            for (var i = 0; i < variableNames.Length; i++)
            {
                if (!StringComparer.Ordinal.Equals(mtbddManager.GetVariableName(new VariableId(i)), variableNames[i]))
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

        ResetManagerForVariableSchema();
        _logger.LogDebug("Decision diagram manager reset for a new MTBDD variable schema. VariableCount={VariableCount}", variableNames.Length);
    }

    private void EnsureZmtbddVariableSchema(string[] variableNames)
    {
        var zmtbddManager = _manager.Zmtbdd;
        if (zmtbddManager.VariableCount == variableNames.Length)
        {
            var sameSchema = true;
            for (var i = 0; i < variableNames.Length; i++)
            {
                if (!StringComparer.Ordinal.Equals(zmtbddManager.GetVariableName(new VariableId(i)), variableNames[i]))
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

        ResetManagerForVariableSchema();
        _logger.LogDebug("Decision diagram manager reset for a new ZMTBDD variable schema. VariableCount={VariableCount}", variableNames.Length);
    }

    private void ResetManagerForVariableSchema()
    {
        _manager = new DecisionDiagramManager(_options);
        _currentBdd = null;
        _previousZdd = null;
        _currentZdd = null;
        _currentMtbdd = null;
        _currentZmtbdd = null;
        _currentZddVariableNames = [];
    }

    private static IReadOnlyList<IReadOnlyList<string>> CloneSetInput(IReadOnlyList<IReadOnlyList<string>> setInput)
    {
        var clone = new IReadOnlyList<string>[setInput.Count];
        for (var i = 0; i < setInput.Count; i++)
        {
            clone[i] = setInput[i].ToArray();
        }

        return clone;
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
