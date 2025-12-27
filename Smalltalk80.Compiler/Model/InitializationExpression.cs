using System;
using System.Collections.Generic;

namespace Smalltalk80.Compiler.Model;

/// <summary>
/// Represents an initialization expression that should be executed after loading classes.
/// </summary>
public class InitializationExpression
{
    /// <summary>
    /// The bytecode for the initialization expression.
    /// </summary>
    public byte[] Bytecode { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// The literal objects referenced by the expression.
    /// </summary>
    public List<object> Literals { get; set; } = [];

    /// <summary>
    /// Number of temporary variables needed.
    /// </summary>
    public int NumTemps { get; set; } = 0;

    /// <summary>
    /// Maximum stack depth required by this expression.
    /// </summary>
    public int StackDepth { get; set; } = 0;

    /// <summary>
    /// The source line for debugging purposes.
    /// </summary>
    public string Source { get; set; } = "";

    /// <summary>
    /// Optional: The receiver of the initialization message (for documentation).
    /// </summary>
    public string? Receiver { get; set; }

    /// <summary>
    /// Optional: The selector to send (for documentation).
    /// </summary>
    public string? Selector { get; set; }

    /// <summary>
    /// Optional: Arguments if any (for documentation).
    /// </summary>
    public List<string>? Arguments { get; set; }
}
