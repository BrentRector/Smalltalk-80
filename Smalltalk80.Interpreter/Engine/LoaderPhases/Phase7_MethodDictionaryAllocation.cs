using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Phase 7: Method Dictionary Allocation
/// 
/// Allocates MethodDictionary objects for all classes and sets them as instance variable 1 (methodDict).
/// This phase was missing and caused classes to have nil method dictionaries, breaking GC traversal.
/// 
/// For each class:
/// 1. Count methods that belong to this class
/// 2. Allocate MethodDictionary with appropriate size
/// 3. Set methodDict instance variable (index 1) in the class
/// </summary>
public class Phase7_MethodDictionaryAllocation
{
    private readonly ColdStartBundle _bundle;
    private readonly SemanticAllocator _allocator;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
    private readonly IReadOnlyList<string> _symbols;

    public Phase7_MethodDictionaryAllocation(
        ColdStartBundle bundle,
        SemanticAllocator allocator,
        Dictionary<string, SmalltalkOOP> localIdToObjectId,
        IReadOnlyList<string> symbols)
    {
        _bundle = bundle;
        _allocator = allocator;
        _localIdToObjectId = localIdToObjectId;
        _symbols = symbols;
    }

    public void Run()
    {

        // Group methods by home class to count them
        var methodCounts = new Dictionary<string, int>();
        foreach (var method in _bundle.Methods)
        {
            if (method.HomeClassLocalId != null)
            {
                methodCounts[method.HomeClassLocalId] = methodCounts.GetValueOrDefault(method.HomeClassLocalId, 0) + 1;
            }
        }

        int allocatedDictionaries = 0;

        foreach (var cls in _bundle.Classes)
        {
            if (!_localIdToObjectId.TryGetValue(cls.LocalId, out SmalltalkOOP classOOP))
            {
                Console.WriteLine($"WARNING: Class not found in localIdToObjectId: {cls.LocalId}");
                continue;
            }

            // Count methods for this class
            int methodCount = methodCounts.GetValueOrDefault(cls.LocalId, 0);
            
            // Allocate method dictionary with room for growth (next power of 2, min 8)
            int dictionarySize = Math.Max(8, NextPowerOfTwo(methodCount * 2)); // 50% load factor
            
            int methodDictId = AllocateMethodDictionary(dictionarySize);
            
            // Set the method dictionary as instance variable 1 in the class
            var methodDictOOP = SmalltalkOOP.FromObjectTableIndex(methodDictId);
            _allocator.SetInstanceVariable(classOOP, 1, methodDictOOP);
            
            allocatedDictionaries++;

        }

    }

    /// <summary>
    /// Allocates a MethodDictionary object with the specified capacity.
    /// MethodDictionary (IdentityDictionary): [0] = valueArray, [1] = tally, indexed fields = keys
    /// </summary>
    private int AllocateMethodDictionary(int capacity)
    {
        // Find MethodDictionary class definition
        var methodDictClassDefinition = FindClassDefinition("MethodDictionary");
        if (methodDictClassDefinition == null)
        {
            throw new InvalidOperationException("MethodDictionary class not found in bundle");
        }

        // Allocate the MethodDictionary object (PointerIndexed with capacity for keys)
        int methodDictClassId = GlobalRegistry.LookupClassObjectId("MethodDictionary");
        var methodDictClassOOP = SmalltalkOOP.FromObjectTableIndex(methodDictClassId);
        var methodDictOOP = _allocator.AllocateArray(methodDictClassOOP, capacity);

        // Allocate valueArray (Array of CompiledMethod) 
        int arrayClassId = GlobalRegistry.LookupClassObjectId("Array");
        var arrayClassOOP = SmalltalkOOP.FromObjectTableIndex(arrayClassId);
        var valuesArrayOOP = _allocator.AllocateArray(arrayClassOOP, capacity);
        _allocator.SetInstanceVariable(methodDictOOP, 0, valuesArrayOOP);

        // Set tally to 0 (no methods installed yet)
        var tallyOOP = SmalltalkOOP.FromSmallInteger(0);
        _allocator.SetInstanceVariable(methodDictOOP, 1, tallyOOP);

        // Initialize all indexed fields (keys) to nil
        var nilOOP = SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.NilObjectId);
        for (int i = 0; i < capacity; i++)
        {
            _allocator.SetIndexedOOP(methodDictOOP, i, nilOOP);
        }

        return methodDictOOP.ToObjectTableIndex();
    }

    /// <summary>
    /// Allocates an Array object with the specified size, initialized to nil.
    /// </summary>
    private int AllocateArray(int size)
    {
        // Find Array class definition
        var arrayClassDefinition = FindClassDefinition("Array");
        if (arrayClassDefinition == null)
        {
            throw new InvalidOperationException("Array class not found in bundle");
        }

        int arrayClassId = GlobalRegistry.LookupClassObjectId("Array");
        var arrayClassOOP = SmalltalkOOP.FromObjectTableIndex(arrayClassId);
        var arrayOOP = _allocator.AllocateArray(arrayClassOOP, size);

        // Initialize all elements to nil
        var nilOOP = SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.NilObjectId);
        for (int i = 0; i < size; i++)
        {
            _allocator.SetIndexedOOP(arrayOOP, i, nilOOP);
        }

        return arrayOOP.ToObjectTableIndex();
    }

    /// <summary>
    /// Finds a class definition by name in the bundle.
    /// </summary>
    private ClassDescriptor? FindClassDefinition(string className)
    {
        return _bundle.Classes.FirstOrDefault(c => 
            !c.IsMetaclass && _symbols[c.NameIndex] == className);
    }

    /// <summary>
    /// Calculates the total instance variable count for a class including inherited ones.
    /// </summary>
    private int CalculateTotalInstanceVariableCount(ClassDescriptor classDefinition)
    {
        int totalCount = 0;
        var currentClass = classDefinition;
        
        // Traverse superclass chain to count all instance variables
        while (currentClass != null)
        {
            totalCount += currentClass.InstVarNames.Length;
            
            if (currentClass.SuperNameIndex.HasValue)
            {
                string superName = _symbols[currentClass.SuperNameIndex.Value];
                currentClass = _bundle.Classes.FirstOrDefault(c => 
                    !c.IsMetaclass && _symbols[c.NameIndex] == superName);
            }
            else
            {
                currentClass = null;
            }
        }
        
        return totalCount;
    }

    /// <summary>
    /// Returns the next power of 2 greater than or equal to the given value.
    /// </summary>
    private int NextPowerOfTwo(int value)
    {
        if (value <= 1) return 1;
        
        int power = 1;
        while (power < value)
        {
            power *= 2;
        }
        return power;
    }
}