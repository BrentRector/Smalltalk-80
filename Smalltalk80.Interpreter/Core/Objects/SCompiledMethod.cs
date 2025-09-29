namespace Smalltalk80.Interpreter.Core.Objects;

using Smalltalk80.Interpreter.Execution;

/// <summary>
/// Represents a compiled Smalltalk method.
/// </summary>
public sealed class SCompiledMethod : SObject
{
    /// <summary>
    /// Initializes a new SCompiledMethod.
    /// </summary>
    public SCompiledMethod(
        string selector,
        byte[] bytecodes,
        SArray literals,
        int numArgs,
        int numTemps,
        int primitiveIndex = 0)
    {
        Selector = selector;
        Bytecodes = bytecodes;
        Literals = literals;
        NumArgs = numArgs;
        NumTemps = numTemps;
        PrimitiveIndex = primitiveIndex;
    }
    
    /// <summary>
    /// Gets the method selector.
    /// </summary>
    public string Selector { get; }
    
    /// <summary>
    /// Gets the bytecode array.
    /// </summary>
    public byte[] Bytecodes { get; }
    
    /// <summary>
    /// Gets the literal array.
    /// </summary>
    public SArray Literals { get; }
    
    /// <summary>
    /// Gets the number of arguments.
    /// </summary>
    public int NumArgs { get; }
    
    /// <summary>
    /// Gets the number of temporaries.
    /// </summary>
    public int NumTemps { get; }
    
    /// <summary>
    /// Gets the primitive index (0 if no primitive).
    /// </summary>
    public int PrimitiveIndex { get; }
    
    /// <summary>
    /// Gets or sets the class that owns this method.
    /// </summary>
    public SClass? MethodClass { get; set; }
    
    /// <summary>
    /// Gets the maximum stack size needed by this method.
    /// </summary>
    public int StackSize => 20; // Default stack size, should be computed from bytecodes
    
    /// <summary>
    /// Gets a literal from the literals array.
    /// </summary>
    public SObject? GetLiteral(int index)
    {
        if (index < 0 || index >= Literals.Size)
        {
            return null;
        }
        return Literals.At(index);
    }
    
    /// <summary>
    /// Gets the class of CompiledMethod.
    /// </summary>
    public override SClass Class => SystemDictionary.CompiledMethodClass;
    
    /// <summary>
    /// Executes this method with the given receiver and arguments.
    /// </summary>
    public SObject Execute(SObject receiver, SObject[] arguments)
    {
        // Check argument count
        if (arguments.Length != NumArgs)
        {
            throw new ArgumentException($"Expected {NumArgs} arguments, got {arguments.Length}");
        }
        
        // Try primitive first if present
        if (PrimitiveIndex > 0)
        {
            var primitiveResult = PrimitiveDispatcher.ExecutePrimitive(PrimitiveIndex, receiver, arguments);
            if (primitiveResult != null)
            {
                return primitiveResult;
            }
            // Primitive failed, execute bytecodes
        }
        
        // Execute bytecodes
        var interpreter = new BytecodeInterpreter();
        return interpreter.Execute(this, receiver, arguments);
    }
    
    /// <summary>
    /// Converts this CompiledMethod to a display string.
    /// </summary>
    public override string ToDisplayString() => $"CompiledMethod({Selector})";
    
    /// <summary>
    /// Gets the hash code for this CompiledMethod.
    /// </summary>
    public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    
    /// <summary>
    /// Determines whether this CompiledMethod equals another object.
    /// </summary>
    public override bool Equals(object? obj) => ReferenceEquals(this, obj);
}