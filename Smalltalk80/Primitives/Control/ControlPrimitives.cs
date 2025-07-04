using System;
using System.Collections.Generic;

namespace Smalltalk80;

/// <summary>
/// Provides primitive implementations for block handling, process
/// management and semaphore signaling.
/// </summary>
public static class ControlPrimitives
{
    /// <summary>
    /// Creates a new <see cref="BlockContext"/> for the given context.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    /// <returns><c>true</c> if the primitive succeeded.</returns>
    public static bool BlockCopy(Context ctx)
    {
        if (ctx.Stack.Pop() is not int argCount || ctx.Stack.Pop() is not Context source)
            return false;

        var home = source is BlockContext blk ? blk.HomeContext : source;
        int startPc = source.PC;
        var block = BlockContext.Create(home, startPc, argCount, Array.Empty<object?>(), ctx);
        ctx.Stack.Push(block);
        return true;
    }

    /// <summary>
    /// Executes a block with arguments supplied on the stack.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool Value(Context ctx)
    {
        var popped = new List<object?>();
        while (ctx.Stack.Count > 0)
        {
            var obj = ctx.Stack.Pop();
            if (obj is BlockContext block)
            {
                var args = popped.ToArray();
                args = args.Length > 0 ? args : Array.Empty<object?>();
                Array.Reverse(args);
                if (args.Length != block.ArgumentCount)
                {
                    ctx.Stack.Push(block);
                    foreach (var item in popped.AsEnumerable().Reverse())
                        ctx.Stack.Push(item);
                    return false;
                }

                var exec = BlockContext.Create(block.HomeContext, block.StartPC, block.ArgumentCount, args, ctx);
                var interpreter = new Interpreter { ExitOnReturn = false };
                var result = interpreter.Evaluate(exec);
                ctx.Stack.Push(result);
                return true;
            }
            popped.Add(obj);
        }

        foreach (var item in popped.AsEnumerable().Reverse())
            ctx.Stack.Push(item);
        return false;
    }

    /// <summary>
    /// Executes a block with arguments provided in an array.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool ValueWithArgs(Context ctx)
    {
        if (ctx.Stack.Pop() is not object[] args || ctx.Stack.Pop() is not BlockContext block)
            return false;
        if (args.Length != block.ArgumentCount)
            return false;

        var exec = BlockContext.Create(block.HomeContext, block.StartPC, block.ArgumentCount, args, ctx);
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(exec);
        ctx.Stack.Push(result);
        return true;
    }

    /// <summary>
    /// Performs the message indicated by the selector argument.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool Perform(Context ctx)
    {
        var args = new List<object?>();
        Symbol? selector = null;
        while (ctx.Stack.Count > 0 && selector == null)
        {
            var obj = ctx.Stack.Pop();
            if (obj is Symbol sym)
            {
                selector = sym;
            }
            else
            {
                args.Add(obj);
            }
        }

        if (selector == null || ctx.Stack.Pop() is not SmalltalkObject receiver)
        {
            foreach (var item in args.AsEnumerable().Reverse())
                ctx.Stack.Push(item);
            if (selector != null) ctx.Stack.Push(selector);
            return false;
        }

        args.Reverse();
        var cls = receiver.ClassPointer;
        var method = cls?.Lookup(selector);
        if (method == null)
            return false;

        var newCtx = new Context
        {
            Sender = ctx,
            Method = method,
            Receiver = receiver,
            Arguments = args.ToArray(),
            Temporaries = new object?[method.TemporariesCount],
            Stack = new Stack<object?>(),
            PC = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(newCtx);
        ctx.Stack.Push(result);
        return true;
    }

    /// <summary>
    /// Performs a message using arguments from an array.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool PerformWithArgs(Context ctx)
    {
        if (ctx.Stack.Pop() is not object[] argArray ||
            ctx.Stack.Pop() is not Symbol selector ||
            ctx.Stack.Pop() is not SmalltalkObject receiver)
            return false;

        var cls = receiver.ClassPointer;
        var method = cls?.Lookup(selector);
        if (method == null)
            return false;

        var newCtx = new Context
        {
            Sender = ctx,
            Method = method,
            Receiver = receiver,
            Arguments = argArray,
            Temporaries = new object?[method.TemporariesCount],
            Stack = new Stack<object?>(),
            PC = 0
        };
        var interpreter = new Interpreter { ExitOnReturn = false };
        var result = interpreter.Evaluate(newCtx);
        ctx.Stack.Push(result);
        return true;
    }

    /// <summary>
    /// Signals the specified semaphore.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool Signal(Context ctx)
    {
        if (ctx.Stack.Pop() is not Semaphore sem)
            return false;
        if (sem.Waiting.Count > 0)
        {
            var proc = sem.Waiting.Dequeue();
            ProcessorScheduler.Instance.Resume(proc);
        }
        else
        {
            sem.ExcessSignals++;
        }
        ctx.Stack.Push(sem);
        return true;
    }

    /// <summary>
    /// Waits on a semaphore, suspending the active process if necessary.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool Wait(Context ctx)
    {
        if (ctx.Stack.Peek() is not Semaphore sem)
            return false;
        _ = ctx.Stack.Pop();
        if (sem.ExcessSignals > 0)
        {
            sem.ExcessSignals--;
            ctx.Stack.Push(sem);
            return true;
        }

        var proc = ProcessorScheduler.Instance.ActiveProcess;
        if (proc == null)
            return false;
        sem.Waiting.Enqueue(proc);
        ProcessorScheduler.Instance.SuspendActive();
        ctx.Stack.Push(sem);
        return true;
    }

    /// <summary>
    /// Resumes the process passed on the stack.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool Resume(Context ctx)
    {
        if (ctx.Stack.Pop() is not Process proc)
            return false;
        ProcessorScheduler.Instance.Resume(proc);
        ctx.Stack.Push(proc);
        return true;
    }

    /// <summary>
    /// Suspends the active process if it matches the receiver.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool Suspend(Context ctx)
    {
        if (ctx.Stack.Pop() is not Process proc)
            return false;
        if (!ReferenceEquals(proc, ProcessorScheduler.Instance.ActiveProcess))
            return false;
        ProcessorScheduler.Instance.SuspendActive();
        ctx.Stack.Push(proc);
        return true;
    }

    /// <summary>
    /// Flushes the method cache. This implementation is a no-op.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool FlushCache(Context ctx)
    {
        return true;
    }
}

/// <summary>
/// Represents a lightweight process managed by the <see cref="ProcessorScheduler"/>.
/// </summary>
public sealed class Process : SmalltalkObject
{
    /// <summary>Initializes a new instance of the <see cref="Process"/> class.</summary>
    /// <param name="context">The initial suspended context.</param>
    public Process(Context context) : base(null, 0) => SuspendedContext = context;

    /// <summary>Gets or sets the context where this process is suspended.</summary>
    public Context SuspendedContext { get; set; }
}

/// <summary>
/// A counting semaphore used for process synchronization.
/// </summary>
public sealed class Semaphore : SmalltalkObject
{
    /// <summary>Initializes a new instance of the <see cref="Semaphore"/> class.</summary>
    public Semaphore() : base(null, 0) { }

    /// <summary>Gets the count of pending signals.</summary>
    public int ExcessSignals { get; set; }

    /// <summary>Queue of processes waiting on the semaphore.</summary>
    public Queue<Process> Waiting { get; } = new();
}

/// <summary>
/// Very small scheduler used by the control primitives.
/// </summary>
public sealed class ProcessorScheduler
{
    /// <summary>Gets the global scheduler instance.</summary>
    public static ProcessorScheduler Instance { get; } = new();

    private readonly Queue<Process> ready = new();

    /// <summary>Gets or sets the currently active process.</summary>
    public Process? ActiveProcess { get; private set; }

    private ProcessorScheduler() { }

    /// <summary>Adds a process to the ready queue.</summary>
    /// <param name="process">The process to queue.</param>
    public void Resume(Process process) => ready.Enqueue(process);

    /// <summary>Suspends the active process and schedules the next ready process.</summary>
    public void SuspendActive()
    {
        if (ActiveProcess != null)
            ready.Enqueue(ActiveProcess);
        ActiveProcess = ready.Count > 0 ? ready.Dequeue() : null;
    }
}
