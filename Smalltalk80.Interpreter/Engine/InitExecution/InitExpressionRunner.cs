using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Engine.InitExecution;

/// <summary>
/// Manages the execution of init expressions during cold start.
/// Encapsulates the logic for building expression lists, executing phases,
/// and reporting results.
/// </summary>
public class InitExpressionRunner
{
    private readonly SmalltalkHeapManager _heapManager;
    private readonly VirtualMachine _vm;
    private readonly VMMetadata _metadata;
    private readonly IDiagnosticHub? _diagnostics;
    private readonly InitExpression[] _initExpressions;
    private readonly SmalltalkOOP _initMethodsOOP;
    private readonly InitExpressionExecutor _executor;

    private int _succeeded;
    private int _failed;

    /// <summary>
    /// Essential init expressions that always run.
    /// </summary>
    private static readonly HashSet<string> EssentialExpressions =
    [
        "Character initialize",
        "SmallInteger initialize",
        "Float initialize",
        "InputSensor initialize",
        "InputSensor install",
        "InputSensor initSensor",
        "BlockContextTest validate",
    ];

    /// <summary>
    /// Expressions to skip (require features not yet implemented).
    /// </summary>
    private static readonly HashSet<string> SkipExpressions =
    [
        // FormMenuView initialize now enabled - file I/O primitives implemented
    ];

    public InitExpressionRunner(
        SmalltalkHeapManager heapManager,
        VirtualMachine vm,
        VMMetadata metadata,
        IDiagnosticHub? diagnostics,
        InitExpression[] initExpressions)
    {
        _heapManager = heapManager;
        _vm = vm;
        _metadata = metadata;
        _diagnostics = diagnostics;
        _initExpressions = initExpressions;

        // Note: 5000ms default timeout - see InterpreterConstants.DefaultInitExpressionTimeoutMs
        _executor = new InitExpressionExecutor(
            heapManager,
            vm,
            metadata,
            diagnostics,
            InitPolicy.ContinueAndCollect,
            defaultTimeoutMs: 5000
        );

        _initMethodsOOP = metadata.InitExpressionMethods;
        if (_initMethodsOOP == heapManager.GetNil())
        {
            throw new InvalidOperationException("InitExpressionMethods array not found - Phase 4 must complete first");
        }
    }

    /// <summary>
    /// Executes all init expressions and reports results.
    /// </summary>
    public void Execute()
    {
        if (_initExpressions.Length == 0)
        {
            Console.WriteLine("  No init expressions found");
            return;
        }

        Console.WriteLine("\n=== Init Expression Execution ===");

        // Build expression lists
        var (phase1Indices, skippedIndices, commentIndices, phase2Indices) = BuildExpressionLists();

        // Execute phases
        ExecutePhase1(phase1Indices);
        ExecuteCommentExpressions(commentIndices);
        RunGarbageCollection();
        ExecutePhase2Tests(phase2Indices);

        // Report results
        ReportSkippedExpressions(skippedIndices);
        ReportSummary(phase1Indices.Count, phase2Indices.Count, skippedIndices.Count);
    }

    private (List<int> phase1, List<(int index, string reason)> skipped, List<int> comments, List<int> phase2) BuildExpressionLists()
    {
        var phase1Indices = new List<int>();
        var skippedIndices = new List<(int index, string reason)>();
        var commentIndices = new List<int>();
        var phase2Indices = new List<int>();

        // Build source-to-index map
        var sourceToIndex = new Dictionary<string, int>();
        for (int i = 0; i < _initExpressions.Length; i++)
        {
            var source = _initExpressions[i].Source;
            if (source != null)
            {
                sourceToIndex[source] = i;
            }
        }

        // Execute all init expressions in explicit order
        foreach (var source in InitExpressionOrdering.GetExecutionOrder())
        {
            if (sourceToIndex.TryGetValue(source, out int idx))
            {
                if (SkipExpressions.Contains(source) && !EssentialExpressions.Contains(source))
                {
                    skippedIndices.Add((idx, "Requires features not yet available"));
                }
                else
                {
                    phase1Indices.Add(idx);
                }
            }
        }

        // Note: Comment expressions and Phase 2 tests are disabled
        // Uncomment and update logic here if needed in future

        return (phase1Indices, skippedIndices, commentIndices, phase2Indices);
    }

    private void ExecutePhase1(List<int> indices)
    {
        Console.WriteLine("\n--- Phase 1: Initialize expressions ---");
        foreach (int index in indices)
        {
            ExecuteSingleExpression(index);
        }
    }

    private void ExecuteSingleExpression(int index)
    {
        var expr = _initExpressions[index];
        var methodOOP = _heapManager.GetIndexedOOP(_initMethodsOOP, index);

        Console.Write($"  {expr.Source}... ");

        var initId = expr.Source ?? $"init[{index}]";
        var result = _executor.ExecuteInitExpression(initId, methodOOP);

        Console.WriteLine($"✓ {result.ElapsedMilliseconds}ms");

        if (result.Status == InitStatus.Success)
        {
            _succeeded++;
            DisplayReturnValue(expr, result);
        }
        else
        {
            _failed++;
            ReportFailure(expr, result);
        }
    }

    private void DisplayReturnValue(InitExpression expr, InitResult result)
    {
        if (expr.Source == null || result.ReturnValueOop?.IsSmallInteger != true)
            return;

        var returnOOP = result.ReturnValueOop.Value;
        int testResult = returnOOP.AsSmallInteger();

        // Show results for Diag* tests
        if (expr.Source.StartsWith("Diag"))
        {
            Console.WriteLine($"    Returned: {testResult}");
        }
        // Show results for test runners
        else if (expr.Source.Contains("TestRunner"))
        {
            Console.WriteLine($"\n  ✓✓✓ TEST RESULT: {testResult} ✓✓✓");

            // BlockContextTestRunner: 120 tests × 9 + 111 = 1191
            if (expr.Source.Contains("BlockContextTestRunner") && testResult == 1191)
            {
                Console.WriteLine($"  ✓✓✓ ALL 120 BLOCK CONTEXT TESTS PASSED ✓✓✓\n");
            }
        }
    }

    private static void ReportFailure(InitExpression expr, InitResult result)
    {
        Console.WriteLine($"    ❌ Failed: {expr.Source}");
        Console.WriteLine($"       Status: {result.Status}");
        if (result.Diagnostic?.Message != null)
        {
            Console.WriteLine($"       Error: {result.Diagnostic.Message}");
        }
    }

    private void ExecuteCommentExpressions(List<int> indices)
    {
        if (indices.Count == 0)
            return;

        Console.WriteLine($"\n--- Comment expressions ({indices.Count} total) ---");
        int commentSucceeded = 0;
        int commentFailed = 0;
        long totalCommentTimeMs = 0;

        foreach (int index in indices)
        {
            var expr = _initExpressions[index];
            var className = expr.Source?.Split(' ')[0] ?? "Unknown";
            Console.Write($"  {className} comment: ... ");

            var methodOOP = _heapManager.GetIndexedOOP(_initMethodsOOP, index);
            var initId = expr.Source ?? $"init[{index}]";
            var result = _executor.ExecuteInitExpression(initId, methodOOP);
            totalCommentTimeMs += result.ElapsedMilliseconds;

            if (result.Status == InitStatus.Success)
            {
                Console.WriteLine($"✓ {result.ElapsedMilliseconds}ms");
                commentSucceeded++;
                _succeeded++;
            }
            else
            {
                Console.WriteLine($"❌ {result.ElapsedMilliseconds}ms");
                commentFailed++;
                _failed++;
            }
        }

        Console.WriteLine($"  Comment expressions: {commentSucceeded}/{indices.Count} succeeded in {totalCommentTimeMs / 1000.0:F1}s total");
    }

    private void RunGarbageCollection()
    {
        Console.WriteLine($"\n=== Running GC after all initialization ===");
        var gcRoots = _heapManager.GC.GetGCRoots();
        Console.WriteLine($"  GC Roots: {gcRoots.Length}");
        var gcStats = _heapManager.GC.GarbageCollect();
        Console.WriteLine($"  {gcStats.GetSummary()}");

        if (gcStats.CollectedObjects > 0 && gcStats.CollectedObjects <= 20)
        {
            Console.WriteLine($"  Collected objects by class:");
            foreach (var (oop, format, size, className) in gcStats.CollectedObjectDetails.Take(20))
            {
                Console.WriteLine($"    OOP({oop}): {className} ({format}, {size} words)");
            }
        }

        var cumulativeStats = _heapManager.GC.GetAndResetCumulativeStatistics();
        Console.WriteLine($"  {cumulativeStats.GetSummary()}");
    }

    private void ExecutePhase2Tests(List<int> indices)
    {
        if (indices.Count == 0)
            return;

        Console.WriteLine($"\n--- Phase 2: Test expressions ---");
        bool isFirstTest = true;

        foreach (int index in indices)
        {
            var expr = _initExpressions[index];

            // Add delay between graphics tests
            if (!isFirstTest)
            {
                Console.WriteLine("  [Waiting 5 seconds before next test...]");
                Thread.Sleep(5000);
            }
            isFirstTest = false;

            var methodOOP = _heapManager.GetIndexedOOP(_initMethodsOOP, index);
            var initId = expr.Source ?? $"init[{index}]";
            var result = _executor.ExecuteInitExpression(initId, methodOOP);

            if (result.Status == InitStatus.Success)
            {
                _succeeded++;
            }
            else
            {
                _failed++;
                ReportFailure(expr, result);
            }
        }
    }

    private void ReportSkippedExpressions(List<(int index, string reason)> skippedIndices)
    {
        if (skippedIndices.Count == 0)
            return;

        Console.WriteLine($"\n  Skipped {skippedIndices.Count} expressions:");
        foreach (var (index, reason) in skippedIndices.Take(5))
        {
            var expr = _initExpressions[index];
            Console.WriteLine($"    - {expr.Source}: {reason}");
        }
        if (skippedIndices.Count > 5)
        {
            Console.WriteLine($"    ... and {skippedIndices.Count - 5} more");
        }
    }

    private void ReportSummary(int phase1Count, int phase2Count, int skippedCount)
    {
        Console.WriteLine($"\n=== Init Expression Execution Complete ===");
        Console.WriteLine($"  {_succeeded} succeeded, {_failed} failed, {skippedCount} skipped");
        if (phase2Count > 0)
        {
            Console.WriteLine($"  Phase 1: {phase1Count} init expressions, Phase 2: {phase2Count} test expressions");
        }
    }
}
