using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.Memory;

using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Phase 9: Method Dictionary Installation
/// 
/// Installs CompiledMethod objects (created in Phase 8) into the appropriate class method dictionaries.
/// This critical phase was missing and caused GC traversal failures because method references
/// were not properly established in the object graph.
/// 
/// For each method:
/// 1. Resolve homeClassLocalId to class object ID
/// 2. Get the class's methodDict (instance variable index 1 in Behavior)
/// 3. Install method using selector as key
/// 4. Handle both instance-side and class-side methods
/// </summary>
public class Phase9_MethodDictionaryInstallation
{
    private readonly ColdStartBundle _bundle;
    private readonly SemanticAllocator _allocator;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
    private readonly IReadOnlyList<string> _symbols;

    public Phase9_MethodDictionaryInstallation(
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
        int installedMethods = 0;
        int installedBlocks = 0;
        int skippedMethods = 0;


        foreach (var method in _bundle.Methods)
        {
            // Skip blocks - they are not installed in method dictionaries
            if (string.IsNullOrEmpty(method.HomeClassLocalId))
            {
                installedBlocks++;
                continue;
            }

            // Get the CompiledMethod object created in Phase 7
            if (!_localIdToObjectId.TryGetValue(method.LocalId, out SmalltalkOOP methodOOP))
            {
                Console.WriteLine($"WARNING: Method not found in localIdToObjectId: {method.LocalId}");
                skippedMethods++;
                continue;
            }

            // Resolve the home class object
            if (!_localIdToObjectId.TryGetValue(method.HomeClassLocalId, out SmalltalkOOP classOOP))
            {
                Console.WriteLine($"WARNING: Home class not found: {method.HomeClassLocalId}");
                skippedMethods++;
                continue;
            }

            // Get the class's method dictionary (instance variable 1 in Behavior)
            var methodDictOOP = _allocator.GetInstanceVariable(classOOP, 1); // methodDict is at index 1

            if (methodDictOOP == SpecialObjectsArray.Instance.Nil)
            {
                Console.WriteLine($"WARNING: Class {method.HomeClassLocalId} has nil methodDict");
                skippedMethods++;
                continue;
            }

            // Get the selector symbol
            string selectorName = _symbols[method.SelectorIndex];
            SmalltalkOOP selectorSymbolOOP = GetSymbolOOP(selectorName);


            // Install the method in the method dictionary
            InstallMethodInDictionary(methodDictOOP, selectorSymbolOOP, methodOOP);
            installedMethods++;

        }

    }

    /// <summary>
    /// Gets the OOP for a symbol that was interned in Phase 1.
    /// </summary>
    private SmalltalkOOP GetSymbolOOP(string symbolName)
    {
        // Find the symbol in the symbol table from Phase 1
        for (int i = 0; i < _bundle.Symbols.Length; i++)
        {
            if (_bundle.Symbols[i] == symbolName)
            {
                return SymbolRegistry.GetSymbolOOP(i);
            }
        }
        
        throw new InvalidOperationException($"Symbol '{symbolName}' not found in symbol table. Ensure Phase 1 (Symbol Interning) completed successfully.");
    }

    /// <summary>
    /// Installs a method into a method dictionary using IdentityDictionary structure.
    /// MethodDictionary (IdentityDictionary): [0] = valueArray, [1] = tally, indexed fields = keys
    /// </summary>
    private void InstallMethodInDictionary(SmalltalkOOP methodDictOOP, SmalltalkOOP selectorSymbolOOP, SmalltalkOOP methodOOP)
    {
        
        // MethodDictionary (IdentityDictionary): [0] = valueArray, [1] = tally, indexed fields = keys
        var valuesArrayOOP = _allocator.GetInstanceVariable(methodDictOOP, 0);
        var tallyOOP = _allocator.GetInstanceVariable(methodDictOOP, 1);

        if (valuesArrayOOP == SpecialObjectsArray.Instance.Nil)
        {
            throw new InvalidOperationException($"MethodDictionary {methodDictOOP} has nil valueArray");
        }

        int dictionarySize = _allocator.GetIndexedSize(methodDictOOP);
        int valuesArraySize = _allocator.GetIndexedSize(valuesArrayOOP);

        if (dictionarySize != valuesArraySize)
        {
            throw new InvalidOperationException($"MethodDictionary {methodDictOOP} has mismatched sizes: keys={dictionarySize}, values={valuesArraySize}");
        }

        // Find an empty slot or existing entry for this selector
        int targetIndex = -1;
        for (int i = 0; i < dictionarySize; i++)
        {
            var keyOOP = _allocator.GetIndexedOOP(methodDictOOP, i);
            
            if (keyOOP == SpecialObjectsArray.Instance.Nil)
            {
                // Empty slot found
                targetIndex = i;
                break;
            }
            else if (keyOOP == selectorSymbolOOP)
            {
                // Existing entry for this selector - replace it
                targetIndex = i;
                break;
            }
        }

        if (targetIndex == -1)
        {
            throw new InvalidOperationException($"MethodDictionary {methodDictOOP} is full - no empty slots available");
        }

        // Check if this is a new entry (for tally update)
        var existingKeyOOP = _allocator.GetIndexedOOP(methodDictOOP, targetIndex);
        bool isNewEntry = existingKeyOOP == SpecialObjectsArray.Instance.Nil;

        // Install the method
        _allocator.SetIndexedOOP(methodDictOOP, targetIndex, selectorSymbolOOP);
        _allocator.SetIndexedOOP(valuesArrayOOP, targetIndex, methodOOP);

        // Update tally if we used a new slot
        if (isNewEntry)
        {
            int currentTally = tallyOOP.IsSmallInteger ? tallyOOP.AsSmallInteger() : 0;
            var newTallyOOP = SmalltalkOOP.FromSmallInteger(currentTally + 1);
            _allocator.SetInstanceVariable(methodDictOOP, 1, newTallyOOP);
        }
    }
}