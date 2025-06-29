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
    ///     Writes a trace message to the console if tracing is enabled.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Log(string message)
    {
        if (Enabled) Console.WriteLine("[TRACE] " + message);
    }
}