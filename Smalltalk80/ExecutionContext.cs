namespace Smalltalk80;

/// <summary>
/// Represents an execution frame for a Smalltalk method.
/// Holds the program counter, stack and stack pointer.
/// </summary>
public class ExecutionContext
{
    public int ProgramCounter { get; set; }

    public int StackPointer { get; set; }

    public object[] Stack { get; }

    public ExecutionContext? Sender { get; }

    public ExecutionContext(int stackSize = 1024, ExecutionContext? sender = null)
    {
        Stack = new object[stackSize];
        Sender = sender;
    }
}

