using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.LoaderPhases;

/// <summary>
/// Phase 11: Execute Init Expressions
/// Executes the prepared initialization expressions (DoIts) to complete image setup.
/// Init expressions set up global variables, class variables, pool dictionaries, and system state.
/// </summary>
public class Phase11_InitExpressionExecution
{
    private readonly ISmalltalkHeap _heap;
    private readonly LayoutResolver _layoutResolver;
    private readonly int _initArrayId;

    public Phase11_InitExpressionExecution(
        ISmalltalkHeap heap,
        LayoutResolver layoutResolver,
        int initArrayId)
    {
        _heap = heap;
        _layoutResolver = layoutResolver;
        _initArrayId = initArrayId;
    }

    /// <summary>
    /// Executes all init expressions using the VirtualMachine.
    /// Returns a report with execution statistics and any failures.
    /// </summary>
    public InitExecutionReport Run()
    {
        // If no init expressions, return empty report
        if (_initArrayId == 0)
        {
            return new InitExecutionReport(0, 0, 0, new List<InitFailure>());
        }

        // Cast to SmalltalkHeap to access methods not in interface
        var heap = _heap as SmalltalkHeap;
        if (heap == null)
        {
            throw new InvalidOperationException("Heap must be a SmalltalkHeap instance");
        }

        // Get count of init expressions
        int count = heap.GetIndexedSize(_initArrayId);
        if (count == 0)
        {
            return new InitExecutionReport(0, 0, 0, new List<InitFailure>());
        }

        // Create VirtualMachine for execution
        var vm = new VirtualMachine(heap, _layoutResolver);

        // Use nil as receiver (standard DoIt semantics)
        int receiverId = GlobalRegistry.NilObjectId;

        int succeeded = 0;
        int failed = 0;
        var failures = new List<InitFailure>();

        // Execute each init expression
        for (int i = 0; i < count; i++)
        {
            int methodId = heap.GetIndexedField(_initArrayId, i);

            try
            {
                // Execute the init expression with nil receiver and no arguments
                // Init expressions are DoIts that run for side effects (setting globals, etc.)
                int result = vm.Execute(methodId, receiverId, Array.Empty<int>());
                succeeded++;

                // Optional: Show progress every 50 expressions
                if ((i + 1) % 50 == 0)
                {
                    Console.Write($"{i + 1}/{count}...");
                }
            }
            catch (Exception ex)
            {
                failed++;

                // Record failure details
                // Try to get localId if available (for debugging)
                string localId = $"init:{i}"; // Fallback ID

                failures.Add(new InitFailure(
                    LocalId: localId,
                    MethodId: methodId,
                    ErrorMessage: ex.Message,
                    StackTrace: ex.StackTrace
                ));

                // Log detailed error information
                Console.WriteLine($"\n✗ Init expression {i + 1}/{count} FAILED");
                Console.WriteLine($"  Method ID: {methodId}");
                Console.WriteLine($"  Error: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }
                Console.WriteLine($"  Stack: {ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");

                // Abort on first error for now (Phase 1a - simple debugging)
                // Can be changed to continue-on-error policy later (Phase 1b)
                throw new InvalidOperationException(
                    $"Init expression {i + 1}/{count} failed (method ID {methodId})", ex);
            }
        }

        return new InitExecutionReport(count, succeeded, failed, failures);
    }
}

/// <summary>
/// Report of init expression execution results.
/// </summary>
public record InitExecutionReport(
    int Total,
    int Succeeded,
    int Failed,
    List<InitFailure> Failures
);

/// <summary>
/// Details of a failed init expression.
/// </summary>
public record InitFailure(
    string LocalId,
    int MethodId,
    string ErrorMessage,
    string? StackTrace
);
