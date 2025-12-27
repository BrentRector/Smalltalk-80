namespace Smalltalk80.Compiler.Model;

/// <summary>
/// Information about a parsed method.
/// </summary>
public class MethodInfo
{
    /// <summary>
    /// The selector (name) of the method.
    /// </summary>
    public string Selector { get; set; } = "";

    /// <summary>
    /// The source code of the method.
    /// </summary>
    public string Source { get; set; } = "";

    /// <summary>
    /// Whether this is a class method (vs instance method).
    /// </summary>
    public bool IsClassMethod { get; set; }

    /// <summary>
    /// The category this method belongs to.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Whether this method has a primitive declaration.
    /// </summary>
    public bool IsPrimitive { get; set; }

    /// <summary>
    /// The primitive number if this is a primitive method.
    /// </summary>
    public int PrimitiveNumber { get; set; }

    /// <summary>
    /// The compiled bytecode data for this method.
    /// </summary>
    public CompiledMethodData? CompiledData { get; set; }
}
