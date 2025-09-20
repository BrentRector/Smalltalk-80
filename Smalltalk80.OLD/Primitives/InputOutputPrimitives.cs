using System;
using System.Collections.Generic;
using System.Linq;

namespace Smalltalk80;

/// <summary>
/// Implements basic input/output primitives used by the interpreter.
/// </summary>
public static class InputOutputPrimitives
{
    private static Semaphore? inputSemaphore;
    private static readonly Queue<int> inputBuffer = new();
    private static int sampleInterval;
    private static bool cursorLinked;
    private static (int X, int Y) cursorLocation;
    private static SmalltalkObject? displayScreen;
    private static SmalltalkObject? cursor;

    /// <summary>
    /// Returns a point describing the current cursor location.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    /// <returns><c>true</c> if successful.</returns>
    public static bool MousePoint(Context ctx)
    {
        _ = ctx.Stack.Pop();
        var point = new SmalltalkObject(null, 2);
        point.Fields[0] = cursorLocation.X;
        point.Fields[1] = cursorLocation.Y;
        ctx.Stack.Push(point);
        return true;
    }

    /// <summary>
    /// Sets the cursor location.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool CursorLocPut(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject point)
            return false;
        _ = ctx.Stack.Pop();
        if (point.Fields.Length < 2)
            return false;
        cursorLocation = (Convert.ToInt32(point.Fields[0]), Convert.ToInt32(point.Fields[1]));
        ctx.Stack.Push(point);
        return true;
    }

    /// <summary>
    /// Links or unlinks the cursor from the pointing device.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool CursorLink(Context ctx)
    {
        if (ctx.Stack.Pop() is not bool link)
            return false;
        _ = ctx.Stack.Pop();
        cursorLinked = link;
        ctx.Stack.Push(link);
        return true;
    }

    /// <summary>
    /// Sets the <see cref="Semaphore"/> used to signal input events.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool InputSemaphore(Context ctx)
    {
        if (ctx.Stack.Pop() is not Semaphore sem)
            return false;
        _ = ctx.Stack.Pop();
        inputSemaphore = sem;
        ctx.Stack.Push(sem);
        return true;
    }

    /// <summary>
    /// Sets the sample interval for pointing device movement.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool SampleInterval(Context ctx)
    {
        if (ctx.Stack.Pop() is not int interval)
            return false;
        _ = ctx.Stack.Pop();
        sampleInterval = interval;
        ctx.Stack.Push(interval);
        return true;
    }

    /// <summary>
    /// Retrieves the next input word from the buffer.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool InputWord(Context ctx)
    {
        _ = ctx.Stack.Pop();
        if (inputBuffer.Count == 0)
            return false;
        int word = inputBuffer.Dequeue();
        ctx.Stack.Push(word);
        return true;
    }

    /// <summary>
    /// Performs a bit block transfer operation. This implementation is a stub.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool CopyBits(Context ctx)
    {
        _ = ctx.Stack.Pop();
        return false;
    }

    /// <summary>
    /// Writes a snapshot of the object memory to disk.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool Snapshot(Context ctx)
    {
        _ = ctx.Stack.Pop();
        try
        {
            ObjectImage.Save("snapshot.json", new List<SmalltalkObject>(ObjectMemory.Objects.Where(o => o != null)!));
            ctx.Stack.Push(true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Stores the current time in seconds since 1901 into a byte array.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool TimeWordsInto(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject bytes || bytes.Fields.Length < 4)
            return false;
        _ = ctx.Stack.Pop();
        var seconds = (int)(DateTime.UtcNow - new DateTime(1901, 1, 1)).TotalSeconds;
        for (int i = 0; i < 4; i++)
            bytes.Fields[i] = (byte)((seconds >> (8 * i)) & 0xFF);
        ctx.Stack.Push(bytes);
        return true;
    }

    /// <summary>
    /// Stores the current tick count in a byte array.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool TickWordsInto(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject bytes || bytes.Fields.Length < 4)
            return false;
        _ = ctx.Stack.Pop();
        int ticks = Environment.TickCount;
        for (int i = 0; i < 4; i++)
            bytes.Fields[i] = (byte)((ticks >> (8 * i)) & 0xFF);
        ctx.Stack.Push(bytes);
        return true;
    }

    /// <summary>
    /// Schedules a semaphore to be signaled at the specified tick count. This implementation uses a timer.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool SignalAtTick(Context ctx)
    {
        if (ctx.Stack.Pop() is not int tick || ctx.Stack.Pop() is not Semaphore sem)
            return false;
        _ = ctx.Stack.Pop();
        var delay = Math.Max(0, tick - Environment.TickCount);
        var timer = new System.Threading.Timer(_ => inputSemaphore?.Waiting.Enqueue(new Process(ctx)), null, delay, System.Threading.Timeout.Infinite);
        ctx.Stack.Push(sem);
        return true;
    }

    /// <summary>
    /// Sets the active cursor object.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool BeCursor(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject c)
            return false;
        _ = ctx.Stack.Pop();
        cursor = c;
        ctx.Stack.Push(c);
        return true;
    }

    /// <summary>
    /// Sets the active display screen object.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool BeDisplay(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject disp)
            return false;
        _ = ctx.Stack.Pop();
        displayScreen = disp;
        ctx.Stack.Push(disp);
        return true;
    }

    /// <summary>
    /// Optional primitive for scanning characters. Always fails in this implementation.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool ScanCharacters(Context ctx)
    {
        _ = ctx.Stack.Pop();
        return false;
    }

    /// <summary>
    /// Optional primitive for drawing loops. Always fails in this implementation.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool DrawLoop(Context ctx)
    {
        _ = ctx.Stack.Pop();
        return false;
    }

    /// <summary>
    /// Replaces bytes in a <see cref="SmalltalkObject"/> array with bytes from another array.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool StringReplace(Context ctx)
    {
        if (ctx.Stack.Pop() is not int start ||
            ctx.Stack.Pop() is not SmalltalkObject source ||
            ctx.Stack.Pop() is not int stop ||
            ctx.Stack.Pop() is not SmalltalkObject target)
            return false;
        _ = ctx.Stack.Pop();
        int count = Math.Min(stop - 1, target.Fields.Length) - Math.Min(start - 1, target.Fields.Length) + 1;
        if (count <= 0)
            return false;
        for (int i = 0; i < count && start - 1 + i < target.Fields.Length && i < source.Fields.Length; i++)
            target.Fields[start - 1 + i] = source.Fields[i];
        ctx.Stack.Push(target);
        return true;
    }
}
