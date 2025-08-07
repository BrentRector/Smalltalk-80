using System;

/// <summary>
///     Represents a method that has been compiled to bytecode.
/// </summary>
public sealed class CompiledMethod
{
    /// <summary>
    ///     Gets or sets the raw header information describing this method.
    /// </summary>
    public MethodHeader Header { get; init; }

    /// <summary>
    ///     Gets the bytecodes that make up the body of the method.
    /// </summary>
    public required byte[] Bytecodes { get; init; } = Array.Empty<byte>();
    
    /// <summary>
    ///     Gets the bytecodes (alias for Bytecodes for compatibility).
    /// </summary>
    public byte[] Bytecode => Bytecodes;

    /// <summary>
    ///     Gets the literal objects referenced by the method.
    /// </summary>
    public required object[] Literals { get; init; } = Array.Empty<object>();

    /// <summary>
    ///     Gets the number of temporary variables used by the method.
    /// </summary>
    public int TemporariesCount { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the method calls a primitive.
    /// </summary>
    public bool HasPrimitive { get; init; }

    /// <summary>
    ///     If <see cref="HasPrimitive"/> is <c>true</c>, contains the index of
    ///     the primitive routine to invoke.
    /// </summary>
    public int PrimitiveIndex { get; init; }

    /// <summary>
    ///     Optional comment describing the method's purpose.
    /// </summary>
    public string? Comment { get; init; }
}