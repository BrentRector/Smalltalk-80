using System;

/// <summary>
///     Provides simple tracing facilities used for debugging the interpreter.
/// </summary>
public static class Tracer
{
    /// <summary>
    ///     Gets or sets a value indicating whether tracing is enabled.
    /// </summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>
    ///     Temporarily disables tracing until the returned object is disposed.
    /// </summary>
    /// <returns>An <see cref="IDisposable"/> that restores the previous tracing
    /// state on disposal.</returns>
    public static IDisposable ScopedDisable()
    {
        var previous = Enabled;
        Enabled = false;
        return new Restore(previous);
    }

    private sealed class Restore(bool previous) : IDisposable
    {
        public void Dispose() => Enabled = previous;
    }

    /// <summary>
    ///     Writes a trace message to the console if tracing is enabled.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Log(string message)
    {
        if (Enabled) Console.WriteLine("[TRACE] " + message);
    }
}