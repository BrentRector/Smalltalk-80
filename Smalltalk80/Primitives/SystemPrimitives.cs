using System;

namespace Smalltalk80;

/// <summary>
/// Implements system-level primitives for the interpreter.
/// </summary>
public static class SystemPrimitives
{
    /// <summary>
    /// Returns an approximation of free object slots in the object memory.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool CoreLeft(Context ctx)
    {
        _ = ctx.Stack.Pop();
        int free = int.MaxValue - ObjectMemory.Objects.Count;
        ctx.Stack.Push(free);
        return true;
    }

    /// <summary>
    /// Requests termination of the process. This implementation does not exit during testing.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool Quit(Context ctx)
    {
        _ = ctx.Stack.Pop();
        ctx.Stack.Push(null);
        return true;
    }

    /// <summary>
    /// Breaks into the debugger if attached.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool ExitToDebugger(Context ctx)
    {
        _ = ctx.Stack.Pop();
        System.Diagnostics.Debugger.Launch();
        ctx.Stack.Push(null);
        return true;
    }

    /// <summary>
    /// Returns a placeholder value representing remaining oops.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool OopsLeft(Context ctx)
    {
        _ = ctx.Stack.Pop();
        ctx.Stack.Push(int.MaxValue - ObjectMemory.Objects.Count);
        return true;
    }

    /// <summary>
    /// Signals a semaphore when the number of allocated objects reaches a threshold.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool SignalAtOopsLeftWordsLeft(Context ctx)
    {
        if (ctx.Stack.Pop() is not int threshold || ctx.Stack.Pop() is not Semaphore sem)
            return false;
        _ = ctx.Stack.Pop();
        if (ObjectMemory.Objects.Count >= threshold)
        {
            ProcessorScheduler.Instance.Resume(new Process(ctx));
        }
        ctx.Stack.Push(sem);
        return true;
    }
}
