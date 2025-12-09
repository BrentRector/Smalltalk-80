using Smalltalk80.Interpreter.Shared.Types.Diagnostics;

namespace Smalltalk80.Interpreter.Core.Diagnostics.Listeners;

/// <summary>
/// Outputs method entry/exit events to console for runtime debugging.
/// Shows call depth with indentation for visual call stack representation.
/// </summary>
public class MethodTraceListener : IDiagnosticListener
{
    public string Name => "MethodTrace";
    public IDiagnosticFilter? Filter => null;

    private int _maxDepth = 100; // Configurable max depth to display

    public int MaxDepth
    {
        get => _maxDepth;
        set => _maxDepth = value;
    }

    public void OnEvent(DiagnosticEvent evt)
    {
        switch (evt)
        {
            case MethodEntryEvent e:
                if (e.CallDepth <= _maxDepth)
                {
                    var indent = new string(' ', e.CallDepth * 2);
                    var superFlag = e.IsSuper ? " [super]" : "";
                    var args = e.Arguments.Length > 0
                        ? $" ({e.Arguments.Length} args)"
                        : "";
                    Console.WriteLine($"{indent}-> {e.ReceiverClass}>>{e.Selector}{superFlag}{args}");
                }
                break;

            case MethodExitEvent e:
                if (e.CallDepth <= _maxDepth)
                {
                    var indent = new string(' ', e.CallDepth * 2);
                    Console.WriteLine($"{indent}<- {e.ReceiverClass}>>{e.Selector}");
                }
                break;

            case PrimitiveEvent e:
                var status = e.Success ? "OK" : $"FAIL: {e.FailureReason}";
                Console.WriteLine($"   [Primitive {e.PrimitiveIndex}] {e.PrimitiveName}: {status}");
                break;
        }
    }
}

/// <summary>
/// Outputs runtime memory events (GC, allocation) to console.
/// </summary>
public class RuntimeMemoryListener : IDiagnosticListener
{
    public string Name => "RuntimeMemory";
    public IDiagnosticFilter? Filter => null;

    private bool _showAllocations = false;

    public bool ShowAllocations
    {
        get => _showAllocations;
        set => _showAllocations = value;
    }

    public void OnEvent(DiagnosticEvent evt)
    {
        switch (evt)
        {
            case GCEvent e:
                var efficiency = e.ObjectsBefore > 0
                    ? (100.0 * e.ObjectsCollected / e.ObjectsBefore)
                    : 0;
                Console.WriteLine($"[GC] Collected {e.ObjectsCollected}/{e.ObjectsBefore} objects " +
                    $"({efficiency:F1}%) in {e.MarkTimeMs + e.SweepTimeMs:F2}ms, " +
                    $"{e.BytesFreed:N0} bytes freed");
                break;

            case AllocationEvent e:
                if (_showAllocations)
                {
                    Console.WriteLine($"[Alloc] {e.ClassName} OOP={e.ObjectOOP.RawValue} " +
                        $"({e.SizeInWords} words, {e.Format})");
                }
                break;
        }
    }
}

/// <summary>
/// Outputs bytecode execution events to console for low-level debugging.
/// Warning: Extremely verbose - use only for targeted debugging.
/// </summary>
public class BytecodeTraceListener : IDiagnosticListener
{
    public string Name => "BytecodeTrace";
    public IDiagnosticFilter? Filter => null;
    private int _eventCount = 0;

    public void OnEvent(DiagnosticEvent evt)
    {
        _eventCount++;

        // DIAGNOSTIC: Log first few events
        if (_eventCount <= 5)
        {
            Console.WriteLine($"[BytecodeTraceListener] Received event #{_eventCount}: {evt.GetType().Name}");
            Console.Out.Flush();
        }

        switch (evt)
        {
            case InstructionEvent e:
                var operand = e.Operand != null ? $" {e.Operand}" : "";
                Console.WriteLine($"  @{e.IP:D4} {e.Mnemonic}{operand}");
                Console.Out.Flush();
                break;
        }
    }
}
