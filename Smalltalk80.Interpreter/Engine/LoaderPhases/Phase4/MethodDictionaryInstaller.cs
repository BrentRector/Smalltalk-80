using System.Diagnostics;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Shared.Types.Diagnostics;
using Smalltalk80.Interpreter.Engine.Services;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase4;

/// <summary>
/// Handles allocation and population of method dictionaries for Smalltalk classes.
/// Manages the IdentityDictionary structure used for method lookup.
/// </summary>
/// <param name="bundle">The cold start bundle containing class and method data.</param>
/// <param name="allocator">The object graph allocator for creating objects.</param>
/// <param name="localIdToObjectId">Mapping from local IDs to allocated object OOPs.</param>
/// <param name="symbolService">The symbol service for symbol lookups.</param>
/// <param name="dictService">The runtime dictionary service for hash-based dictionary operations.</param>
/// <param name="diagnostics">Optional diagnostic hub for logging.</param>
internal class MethodDictionaryInstaller(
    ColdStartBundle bundle,
    ObjectGraphAllocator allocator,
    Dictionary<string, SmalltalkOOP> localIdToObjectId,
    SymbolService symbolService,
    RuntimeDictionaryService dictService,
    IDiagnosticHub? diagnostics = null)
{
    private readonly ColdStartBundle _bundle = bundle;
    private readonly ObjectGraphAllocator _allocator = allocator;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId = localIdToObjectId;
    private readonly SymbolService _symbolService = symbolService;
    private readonly RuntimeDictionaryService _dictService = dictService;
    private readonly IDiagnosticHub? _diagnostics = diagnostics;

    /// <summary>
    /// Allocates method dictionaries for all classes in the bundle.
    /// </summary>
    public void AllocateMethodDictionaries()
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
                continue;
            }

            // Count methods for this class
            int methodCount = methodCounts.GetValueOrDefault(cls.LocalId, 0);

            // Allocate method dictionary with room for growth (next power of 2, min 8)
            int dictionarySize = Math.Max(8, NextPowerOfTwo(methodCount * 2)); // 50% load factor

            SmalltalkOOP methodDictOOP = AllocateMethodDictionary(dictionarySize);

            // Set the method dictionary as instance variable 1 in the class
            _allocator.SetInstanceVariable(classOOP, 1, methodDictOOP);

            allocatedDictionaries++;
        }
    }

    /// <summary>
    /// Allocates a MethodDictionary object with the specified capacity.
    /// MethodDictionary (IdentityDictionary): [0] = tally (inherited from Set), [1] = valueArray, indexed fields = keys
    /// </summary>
    /// <param name="capacity">The number of slots for keys in the dictionary.</param>
    /// <returns>The allocated MethodDictionary OOP.</returns>
    /// <exception cref="InvalidOperationException">Thrown when MethodDictionary class not found.</exception>
    public SmalltalkOOP AllocateMethodDictionary(int capacity)
    {
        // Find MethodDictionary class definition
        var methodDictClassDefinition = FindClassDefinition("MethodDictionary");
        if (methodDictClassDefinition == null)
        {
            throw new InvalidOperationException("MethodDictionary class not found in bundle");
        }

        // Allocate the MethodDictionary object (PointerIndexed with capacity for keys)
        var methodDictClassOOP = GlobalRegistry.LookupClassOOP("MethodDictionary");
        var methodDictOOP = _allocator.AllocateArray(methodDictClassOOP, capacity);

        // Set tally to 0 (no methods installed yet)
        var tallyOOP = SmalltalkOOP.FromSmallInteger(0);
        _allocator.SetInstanceVariable(methodDictOOP, VMConstants.METHOD_DICTIONARY_TALLY_INDEX, tallyOOP);

        // Allocate valueArray (Array of CompiledMethod)
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        var valuesArrayOOP = _allocator.AllocateArray(arrayClassOOP, capacity);
        _allocator.SetInstanceVariable(methodDictOOP, VMConstants.METHOD_DICTIONARY_VALUE_ARRAY_INDEX, valuesArrayOOP);

        // Initialize all indexed fields (keys) to nil
        var nilOOP = _allocator.GetNil();
        for (int i = 0; i < capacity; i++)
        {
            _allocator.SetIndexedOOP(methodDictOOP, i, nilOOP);
        }

        return methodDictOOP;
    }

    /// <summary>
    /// Installs all compiled methods into their class method dictionaries.
    /// </summary>
    public void InstallMethodsIntoDictionaries()
    {
        int installedMethods = 0;
        int skippedBlocks = 0;
        int skippedMethods = 0;

        foreach (var method in _bundle.Methods)
        {
            // Skip blocks - blocks are compiled inline in method bytecode, not separate CompiledMethods
            if (string.IsNullOrEmpty(method.HomeClassLocalId))
            {
                skippedBlocks++;
                continue;
            }

            // Get the CompiledMethod object created earlier
            if (!_localIdToObjectId.TryGetValue(method.LocalId, out SmalltalkOOP methodOOP))
            {
                _diagnostics?.Emit(new DiagnosticMessageEvent
                {
                    Severity = MessageSeverity.Warning,
                    Category = "Phase4",
                    Message = $"Method not found in localIdToObjectId: {method.LocalId}",
                    Timestamp = Stopwatch.GetTimestamp(),
                    InstructionCount = 0
                });
                skippedMethods++;
                continue;
            }

            // Resolve the home class object
            if (!_localIdToObjectId.TryGetValue(method.HomeClassLocalId, out SmalltalkOOP classOOP))
            {
                _diagnostics?.Emit(new DiagnosticMessageEvent
                {
                    Severity = MessageSeverity.Warning,
                    Category = "Phase4",
                    Message = $"Home class not found: {method.HomeClassLocalId}",
                    Timestamp = Stopwatch.GetTimestamp(),
                    InstructionCount = 0
                });
                skippedMethods++;
                continue;
            }

            // Get the class's method dictionary (instance variable 1 in Behavior)
            // Behavior instance variables: [0]=superclass, [1]=methodDict, [2]=format, [3]=subclasses
            var methodDictOOP = _allocator.GetInstanceVariable(classOOP, 1); // methodDict is at instance var index 1

            if (methodDictOOP == _allocator.GetNil())
            {
                _diagnostics?.Emit(new DiagnosticMessageEvent
                {
                    Severity = MessageSeverity.Warning,
                    Category = "Phase4",
                    Message = $"Class {method.HomeClassLocalId} has nil methodDict",
                    Timestamp = Stopwatch.GetTimestamp(),
                    InstructionCount = 0
                });
                skippedMethods++;
                continue;
            }

            // Get the selector symbol
            string selectorName = _bundle.Symbols[method.SelectorIndex];
            SmalltalkOOP selectorSymbolOOP = GetSymbolOOP(selectorName);

            // Install the method in the method dictionary
            InstallMethodInDictionary(methodDictOOP, selectorSymbolOOP, methodOOP);
            installedMethods++;
        }
    }

    /// <summary>
    /// Gets the OOP for a symbol that was interned in Phase 1.
    /// </summary>
    /// <param name="symbolName">The name of the symbol to look up.</param>
    /// <returns>The symbol OOP.</returns>
    /// <exception cref="InvalidOperationException">Thrown when symbol not found in symbol table.</exception>
    public SmalltalkOOP GetSymbolOOP(string symbolName)
    {
        return _symbolService.GetSymbolOOP(symbolName);
    }

    /// <summary>
    /// Installs a method into a method dictionary using IdentityDictionary structure.
    /// Delegates to RuntimeDictionaryService for hash-based placement.
    /// </summary>
    /// <param name="methodDictOOP">The method dictionary to install into.</param>
    /// <param name="selectorSymbolOOP">The selector symbol OOP.</param>
    /// <param name="methodOOP">The compiled method OOP to install.</param>
    public void InstallMethodInDictionary(SmalltalkOOP methodDictOOP, SmalltalkOOP selectorSymbolOOP, SmalltalkOOP methodOOP)
    {
        _dictService.InstallInIdentityDictionary(methodDictOOP, selectorSymbolOOP, methodOOP);
    }

    /// <summary>
    /// Finds a class definition by name in the bundle.
    /// </summary>
    /// <param name="className">The name of the class to find.</param>
    /// <returns>The class descriptor, or null if not found.</returns>
    public ClassDescriptor? FindClassDefinition(string className)
    {
        return _bundle.Classes.FirstOrDefault(c =>
            !c.IsMetaclass && _bundle.Symbols[c.NameIndex] == className);
    }

    /// <summary>
    /// Returns the next power of 2 greater than or equal to the given value.
    /// </summary>
    /// <param name="value">The value to find the next power of 2 for.</param>
    /// <returns>The next power of 2.</returns>
    public static int NextPowerOfTwo(int value)
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
