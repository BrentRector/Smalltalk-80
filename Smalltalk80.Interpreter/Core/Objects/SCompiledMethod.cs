namespace Smalltalk80.Interpreter.Core.Objects;

using Smalltalk80.Interpreter.Execution;
using Smalltalk80.Interpreter.Core.Values;

/// <summary>
/// Represents a compiled Smalltalk method.
/// This is a proper Smalltalk object following the Blue Book specification.
/// </summary>
/// <remarks>
/// From the Blue Book:
/// CompiledMethod instances contain the bytecode instructions and literals
/// needed to execute a method. They are stored in method dictionaries.
/// 
/// Instance variables (conceptual - stored as properties):
/// - header: Contains flags, arg count, temp count, stack size
/// - literals: Array of literal objects and special bindings
/// - bytecodes: The bytecode instructions
/// - sourcePointer: Reference to source (optional)
/// </remarks>
public sealed class SCompiledMethod : SInstance
{
    // Properties to maintain the compiled method data
    private string selector = "";
    private byte[] bytecodes = [];
    private SArray? literals;
    private int numArgs;
    private int numTemps;
    private int primitiveIndex;
    
    /// <summary>
    /// Default constructor for creation through SClass.CreateInstance.
    /// </summary>
    public SCompiledMethod(SClass compiledMethodClass) : base(compiledMethodClass, 0)
    {
        // Initialize with defaults - will be set through Initialize method
    }
    
    /// <summary>
    /// Legacy constructor for backward compatibility - DEPRECATED.
    /// </summary>
    [Obsolete("Use CreateCompiledMethod factory method instead")]
    public SCompiledMethod(
        string selector,
        byte[] bytecodes,
        SArray literals,
        int numArgs,
        int numTemps,
        int primitiveIndex = 0) : base(SystemDictionary.CompiledMethodClass, 0)
    {
        Initialize(selector, bytecodes, literals, numArgs, numTemps, primitiveIndex);
    }
    
    /// <summary>
    /// Initializes this compiled method with the given data.
    /// </summary>
    public void Initialize(
        string selector,
        byte[] bytecodes,
        SArray literals,
        int numArgs,
        int numTemps,
        int primitiveIndex = 0)
    {
        this.selector = selector;
        this.bytecodes = bytecodes;
        this.literals = literals;
        this.numArgs = numArgs;
        this.numTemps = numTemps;
        this.primitiveIndex = primitiveIndex;
    }
    
    /// <summary>
    /// Factory method to create a CompiledMethod as a proper Smalltalk object.
    /// </summary>
    public static SCompiledMethod CreateCompiledMethod(
        string selector,
        byte[] bytecodes,
        SArray literals,
        int numArgs,
        int numTemps,
        int primitiveIndex = 0)
    {
        var methodClass = SystemDictionary.LookupClass("CompiledMethod")
            ?? SystemDictionary.CompiledMethodClass
            ?? throw new InvalidOperationException("CompiledMethod class not found");
        
        var method = methodClass.CreateInstance() as SCompiledMethod
            ?? new SCompiledMethod(methodClass);
        
        method.Initialize(selector, bytecodes, literals, numArgs, numTemps, primitiveIndex);
        
        // Note: GC registration should happen through SClass.CreateInstance
        // or by the caller who has access to the VM
        
        return method;
    }
    
    /// <summary>
    /// Gets the method selector.
    /// </summary>
    public string Selector => selector;
    
    /// <summary>
    /// Gets the bytecode array.
    /// </summary>
    public byte[] Bytecodes => bytecodes;
    
    /// <summary>
    /// Gets the literal array.
    /// </summary>
    public SArray Literals => literals ?? new SArray(0);
    
    /// <summary>
    /// Gets the number of arguments.
    /// </summary>
    public int NumArgs => numArgs;
    
    /// <summary>
    /// Gets the number of temporaries.
    /// </summary>
    public int NumTemps => numTemps;
    
    /// <summary>
    /// Gets the primitive index (0 if no primitive).
    /// </summary>
    public int PrimitiveIndex => primitiveIndex;
    
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
        // SArray uses 1-based indexing, so add 1 to the index
        return Literals.At(index + 1);
    }
    
    // Class is already handled by base SInstance class
    
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