namespace Smalltalk80.Interpreter.Core.Objects;

using Smalltalk80.Interpreter.Execution;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

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
    /// Factory method to create a CompiledMethod as an SObjectRef for use with the new object memory system.
    /// </summary>
    /// <param name="selector">The method selector.</param>
    /// <param name="bytecodes">The bytecode instructions.</param>
    /// <param name="literals">Array of literal SObjectRefs (symbols, numbers, etc.).</param>
    /// <param name="numArgs">Number of arguments this method expects.</param>
    /// <param name="numTemps">Number of temporary variables this method uses.</param>
    /// <param name="primitiveIndex">Primitive index if this method uses a primitive (0 for none).</param>
    /// <returns>An SObjectRef pointing to the new CompiledMethod object.</returns>
    public static SObjectRef CreateCompiledMethodRef(
        string selector,
        byte[] bytecodes,
        SObjectRef[] literals,
        int numArgs,
        int numTemps,
        int primitiveIndex = 0)
    {
        // Use ObjectMemoryNew to create the CompiledMethod directly
        // This bypasses the need for SClass during cold start
        return ObjectMemoryNew.CreateCompiledMethod(
            selector,
            bytecodes,
            literals,
            numArgs,
            numTemps,
            primitiveIndex);
    }

    /// <summary>
    /// Creates an array of literals as an SObjectRef.
    /// </summary>
    private static SObjectRef CreateLiteralsArray(SObjectRef[] literals)
    {
        if (literals.Length == 0)
        {
            // Return an empty array
            return SObjectRef.FromObjectPointer(
                ObjectMemoryNew.AllocateObject(
                    ObjectMemoryNew.GetArrayClass(),
                    ObjectFormat.PointerIndexed,
                    0,
                    0));
        }

        // Allocate array in object memory
        var arrayRef = SObjectRef.FromObjectPointer(
            ObjectMemoryNew.AllocateObject(
                ObjectMemoryNew.GetArrayClass(),
                ObjectFormat.PointerIndexed,
                0,
                literals.Length));

        // Set each literal in the array
        for (int i = 0; i < literals.Length; i++)
        {
            arrayRef.SetIndexedPointer(i, literals[i]);
        }

        return arrayRef;
    }

    /// <summary>
    /// Helper method to convert SObject literals to SObjectRef literals.
    /// Used during migration from old to new architecture.
    /// </summary>
    public static SObjectRef[] ConvertLiteralsToRefs(params SObject[] literals)
    {
        var refs = new SObjectRef[literals.Length];
        for (int i = 0; i < literals.Length; i++)
        {
            var literal = literals[i];
            if (literal == null)
            {
                refs[i] = SObjectRef.Nil;
            }
            else if (literal is SmallInteger si)
            {
                refs[i] = SObjectRef.FromSmallInteger(si.Value);
            }
            else if (literal is SSymbol symbol)
            {
                refs[i] = ObjectMemoryNew.InternSymbol(symbol.Value);
            }
            else if (literal is SString str)
            {
                // Create string in object memory
                var stringOop = ObjectMemoryNew.AllocateObject(
                    ObjectMemoryNew.GetStringClass(),
                    ObjectFormat.ByteIndexed,
                    str.Value.Length,
                    0);
                var stringRef = SObjectRef.FromObjectPointer(stringOop);
                // Copy string data
                for (int j = 0; j < str.Value.Length; j++)
                {
                    stringRef.SetIndexedByte(j, (byte)str.Value[j]);
                }
                refs[i] = stringRef;
            }
            else
            {
                // For other objects, register them in the bootstrap object table
                var oop = ObjectMemoryNew.RegisterBootstrapObject(literal);
                refs[i] = SObjectRef.FromObjectPointer(oop);
            }
        }
        return refs;
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