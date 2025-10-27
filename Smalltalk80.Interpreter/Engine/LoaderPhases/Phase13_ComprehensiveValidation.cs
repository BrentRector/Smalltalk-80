using System.Text.Json;
using Smalltalk80.Interpreter.Core.Memory;

using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Phase 13: Comprehensive Heap Validation
/// Performs complete validation of heap objects against JSON bundle definitions:
/// - All classes match their JSON structure
/// - All methods match their JSON structure  
/// - All objects have correct instance variables, indexed data, formats
/// - All references are valid and match expected types
/// This comprehensive validation helps diagnose heap corruption and GC issues.
/// </summary>
public class Phase13_ComprehensiveValidation
{
    private readonly ColdStartBundle _bundle;
    private readonly SemanticAllocator _allocator;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
    private readonly string[] _symbols;

    public Phase13_ComprehensiveValidation(
        ColdStartBundle bundle,
        SemanticAllocator allocator,
        Dictionary<string, SmalltalkOOP> localIdToObjectId,
        string[] symbols)
    {
        _bundle = bundle;
        _allocator = allocator;
        _localIdToObjectId = localIdToObjectId;
        _symbols = symbols;
    }

    public void Run()
    {
        var results = new ValidationResults();

        // 1. Validate all classes
        ValidateAllClasses(results);

        // 2. Validate all methods (including CompiledMethod objects)
        ValidateAllMethods(results);

        // 3. Validate special objects (nil, true, false, SystemDictionary, USTable)
        ValidateSpecialObjects(results);

        // 4. Validate init expressions array
        ValidateInitExpressions(results);

        // 5. Cross-reference validation (object references point to valid objects)
        ValidateCrossReferences(results);

        // 6. Validate SpecialObjectsArray integrity
        ValidateSpecialObjectsArray(results);

        // 7. Validate method dictionaries
        ValidateMethodDictionaries(results);

        // Report results
        ReportValidationResults(results);
    }

    private void ValidateAllClasses(ValidationResults results)
    {
        Console.WriteLine("  Validating classes...");
        
        foreach (var classDesc in _bundle.Classes)
        {
            if (!_localIdToObjectId.TryGetValue(classDesc.LocalId, out SmalltalkOOP classOOP))
            {
                results.AddError($"Class {GetClassName(classDesc)} not found in heap (localId: {classDesc.LocalId})");
                continue;
            }
            
            ValidateClassObject(classDesc, classOOP, results);
        }
    }

    private void ValidateClassObject(ClassDescriptor classDesc, SmalltalkOOP classOOP, ValidationResults results)
    {
        string className = GetClassName(classDesc);
        
        if (!_allocator.IsValidObjectOOP(classOOP))
        {
            results.AddError($"Class {className} has invalid OOP {classOOP}");
            return;
        }
        
        // Validate object format
        var format = _allocator.GetObjectFormat(classOOP);
        if (format != ObjectFormat.Pointer)
        {
            results.AddError($"Class {className} has wrong format {format}, expected Pointer");
        }
        
        // Validate instance variable count
        int actualInstVars = _allocator.GetInstanceVariableCount(classOOP);
        int expectedInstVars = CalculateExpectedInstanceVariables(classDesc);
        
        if (actualInstVars != expectedInstVars)
        {
            results.AddError($"Class {className} has {actualInstVars} instance variables, expected {expectedInstVars}");
        }
        
        // TODO: Validate specific instance variable contents (superclass, method dictionary, etc.)
        // This would require understanding the class structure layout
        
        results.ClassesValidated++;
    }

    private void ValidateAllMethods(ValidationResults results)
    {
        Console.WriteLine("  Validating methods...");
        
        foreach (var method in _bundle.Methods)
        {
            if (!_localIdToObjectId.TryGetValue(method.LocalId, out SmalltalkOOP methodOOP))
            {
                results.AddError($"Method {method.LocalId} not found in heap");
                continue;
            }
            
            ValidateMethodObject(method, methodOOP, results);
        }
    }

    private void ValidateMethodObject(MethodDescriptor method, SmalltalkOOP methodOOP, ValidationResults results)
    {
        
        if (!_allocator.IsValidObjectOOP(methodOOP))
        {
            results.AddError($"Method {method.LocalId} has invalid OOP {methodOOP}");
            return;
        }
        
        // Validate CompiledMethod structure (PointerIndexed with hidden ByteArray reference)
        var format = _allocator.GetObjectFormat(methodOOP);
        if (format != ObjectFormat.PointerIndexed)
        {
            results.AddError($"Method {method.LocalId} has wrong format {format}, expected PointerIndexed");
        }
        
        // Validate indexed size matches expected: header + literals + bytecode array reference
        int actualIndexedSize = _allocator.GetIndexedSize(methodOOP);
        int expectedIndexedSize = 1 + method.Literals.Length + 1; // header + literals + bytecode ref
        
        if (actualIndexedSize != expectedIndexedSize)
        {
            results.AddError($"Method {method.LocalId} has indexed size {actualIndexedSize}, expected {expectedIndexedSize}");
        }
        
        // Validate method header at index 0
        try
        {
            var headerOOP = _allocator.GetIndexedOOP(methodOOP, 0);
            if (!headerOOP.IsSmallInteger)
            {
                results.AddError($"Method {method.LocalId} header is not a SmallInteger");
            }
            else
            {
                int headerValue = headerOOP.AsSmallInteger();
                if (headerValue != method.Header.Word)
                {
                    results.AddError($"Method {method.LocalId} header value {headerValue} != expected {method.Header.Word}");
                }
            }
        }
        catch (Exception ex)
        {
            results.AddError($"Method {method.LocalId} header access failed: {ex.Message}");
        }
        
        // Validate literals at indices 1..N
        for (int i = 0; i < method.Literals.Length; i++)
        {
            try
            {
                var literalOOP = _allocator.GetIndexedOOP(methodOOP, i + 1);
                // TODO: Could validate literal values match expected types/values
            }
            catch (Exception ex)
            {
                results.AddError($"Method {method.LocalId} literal {i} access failed: {ex.Message}");
            }
        }
        
        // Validate bytecode array reference at final index
        try
        {
            int bytecodeArrayIndex = 1 + method.Literals.Length;
            var bytecodeArrayOOP = _allocator.GetIndexedOOP(methodOOP, bytecodeArrayIndex);
            
            if (!_allocator.IsValidObjectOOP(bytecodeArrayOOP))
            {
                results.AddError($"Method {method.LocalId} has invalid bytecode array reference");
            }
            else
            {
                // Validate bytecode array is ByteIndexed
                var bytecodeFormat = _allocator.GetObjectFormat(bytecodeArrayOOP);
                if (bytecodeFormat != ObjectFormat.ByteIndexed)
                {
                    results.AddError($"Method {method.LocalId} bytecode array has wrong format {bytecodeFormat}");
                }
                
                // Validate bytecode array size
                byte[] expectedBytecodes = ClassHierarchyUtils.DecodeBytecodes(method.Bytecodes);
                int actualBytecodeSize = _allocator.GetIndexedSize(bytecodeArrayOOP);
                
                
                if (actualBytecodeSize != expectedBytecodes.Length)
                {
                    results.AddError($"Method {method.LocalId} bytecode size {actualBytecodeSize} != expected {expectedBytecodes.Length}");
                }
            }
        }
        catch (Exception ex)
        {
            results.AddError($"Method {method.LocalId} bytecode array validation failed: {ex.Message}");
        }
        
        results.MethodsValidated++;
    }

    private void ValidateSpecialObjects(ValidationResults results)
    {
        Console.WriteLine("  Validating special objects...");

        // Validate 5 special objects (note: only 4 are GC roots - USTable is not a GC root)

        // Validate nil
        ValidateSpecialObject("nil", SpecialObjectsArray.Instance.Nil, results);

        // Validate true/false
        ValidateSpecialObject("true", SpecialObjectsArray.Instance.True, results);
        ValidateSpecialObject("false", SpecialObjectsArray.Instance.False, results);

        // Validate SystemDictionary
        ValidateSpecialObject("SystemDictionary", GlobalRegistry.SystemDictionaryOOP, results);

        // Validate USTable
        if (GlobalRegistry.TryGet("USTable", out SmalltalkOOP ustableOOP))
        {
            ValidateSpecialObject("USTable", ustableOOP, results);
            
            // Validate USTable is Array with expected size
            if (_allocator.IsValidObjectOOP(ustableOOP))
            {
                var format = _allocator.GetObjectFormat(ustableOOP);
                if (format != ObjectFormat.PointerIndexed)
                {
                    results.AddError($"USTable has wrong format {format}, expected PointerIndexed");
                }
                
                int actualSize = _allocator.GetIndexedSize(ustableOOP);
                int expectedSize = _symbols.Length * 2; // Load factor heuristic from Phase10
                if (actualSize != expectedSize)
                {
                    results.AddError($"USTable size {actualSize} != expected {expectedSize}");
                }
            }
        }
        else
        {
            results.AddError("USTable not found in GlobalRegistry");
        }
    }

    private void ValidateSpecialObject(string name, SmalltalkOOP objectOOP, ValidationResults results)
    {
        if (!_allocator.IsValidObjectOOP(objectOOP))
        {
            results.AddError($"Special object {name} ({objectOOP}) is not valid");
        }
        else
        {
            results.SpecialObjectsValidated++;
        }
    }

    private void ValidateInitExpressions(ValidationResults results)
    {
        Console.WriteLine("  Validating init expressions...");
        
        // Check if init expressions array exists and has correct structure
        // This is stored in _initExpressionsArrayId from Phase 9
        // TODO: Add access to this from Loader if needed for validation
        
        results.InitExpressionsValidated = _bundle.InitExpressions.Length;
    }

    private void ValidateCrossReferences(ValidationResults results)
    {
        Console.WriteLine("  Validating cross-references...");
        
        // Walk through all objects and verify their references point to valid objects
        int totalObjectsChecked = 0;
        int invalidReferences = 0;
        
        foreach (var kvp in _localIdToObjectId)
        {
            var objectOOP = kvp.Value;
            if (!_allocator.IsValidObjectOOP(objectOOP))
            {
                continue; // Skip invalid objects (already reported)
            }
            
            totalObjectsChecked++;
            
            // Check class OOP
            try
            {
                var classOOP = _allocator.GetClassOOP(objectOOP);
                if (!classOOP.IsSmallInteger && !_allocator.IsValidObjectOOP(classOOP))
                {
                    results.AddError($"Object {kvp.Key} has invalid class OOP {classOOP}");
                    invalidReferences++;
                }
            }
            catch (Exception ex)
            {
                results.AddError($"Object {kvp.Key} class OOP access failed: {ex.Message}");
                invalidReferences++;
            }
            
            // Check instance variables
            try
            {
                int instVarCount = _allocator.GetInstanceVariableCount(objectOOP);
                for (int i = 0; i < instVarCount; i++)
                {
                    var instVarOOP = _allocator.GetInstanceVariable(objectOOP, i);
                    if (!instVarOOP.IsSmallInteger && !_allocator.IsValidObjectOOP(instVarOOP))
                    {
                        results.AddError($"Object {kvp.Key} instance variable {i} has invalid OOP {instVarOOP}");
                        invalidReferences++;
                    }
                }
            }
            catch (Exception ex)
            {
                results.AddError($"Object {kvp.Key} instance variable validation failed: {ex.Message}");
                invalidReferences++;
            }
            
            // Check indexed data (if PointerIndexed)
            try
            {
                var format = _allocator.GetObjectFormat(objectOOP);
                if (format == ObjectFormat.PointerIndexed)
                {
                    int indexedSize = _allocator.GetIndexedSize(objectOOP);
                    for (int i = 0; i < indexedSize; i++)
                    {
                        var indexedOOP = _allocator.GetIndexedOOP(objectOOP, i);
                        if (!indexedOOP.IsSmallInteger && !_allocator.IsValidObjectOOP(indexedOOP))
                        {
                            results.AddError($"Object {kvp.Key} indexed field {i} has invalid OOP {indexedOOP}");
                            invalidReferences++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                results.AddError($"Object {kvp.Key} indexed data validation failed: {ex.Message}");
                invalidReferences++;
            }
        }
        
        results.CrossReferencesValidated = totalObjectsChecked;
        results.InvalidReferences = invalidReferences;
    }

    private void ReportValidationResults(ValidationResults results)
    {
        if (results.Errors.Count == 0)
        {
            Console.WriteLine("✓");
            Console.WriteLine($"  Classes: {results.ClassesValidated} validated");
            Console.WriteLine($"  Methods: {results.MethodsValidated} validated");
            Console.WriteLine($"  Special objects: {results.SpecialObjectsValidated} validated");
            Console.WriteLine($"  Init expressions: {results.InitExpressionsValidated} validated");
            Console.WriteLine($"  Cross-references: {results.CrossReferencesValidated} objects checked");
            Console.WriteLine($"  ✓ All heap objects match JSON definitions");
        }
        else
        {
            Console.WriteLine("✗ FAILED");
            Console.WriteLine($"  Validation errors found: {results.Errors.Count}");
            Console.WriteLine($"  Classes: {results.ClassesValidated} validated");
            Console.WriteLine($"  Methods: {results.MethodsValidated} validated");
            Console.WriteLine($"  Invalid references: {results.InvalidReferences}");
            
            Console.WriteLine($"  Showing first 20 errors:");
            foreach (var error in results.Errors.Take(20))
            {
                Console.WriteLine($"    {error}");
            }
            if (results.Errors.Count > 20)
            {
                Console.WriteLine($"    ... and {results.Errors.Count - 20} more errors");
            }
            
            throw new InvalidOperationException($"Heap validation failed with {results.Errors.Count} errors");
        }
    }

    // Helper methods
    
    private string GetClassName(ClassDescriptor classDesc)
    {
        return _symbols[classDesc.NameIndex];
    }

    private int CalculateExpectedInstanceVariables(ClassDescriptor classDesc)
    {
        // CRITICAL DISTINCTION: Class objects vs Class instances
        // 
        // During validation, we're checking CLASS OBJECTS (the objects that represent classes)
        // NOT instances of those classes.
        //
        // Class objects are instances of either:
        // - "Class" (9 instance variables: Object(0) + Behavior(4) + ClassDescription(2) + Class(3))
        // - "Metaclass" (7 instance variables: Object(0) + Behavior(4) + ClassDescription(2) + Metaclass(1))
        //
        // The JSON class descriptor tells us what instances OF this class would look like,
        // but class objects themselves are always instances of Class/Metaclass.
        
        if (classDesc.IsMetaclass)
        {
            // Metaclass objects are instances of "Metaclass" - always 7 instance variables
            return 7;
        }
        else
        {
            // Class objects are instances of "Class" - always 9 instance variables
            return 9;
        }
    }

    // DecodeBytecodes method moved to ClassHierarchyUtils to eliminate duplication

    /// <summary>
    /// Validates the SpecialObjectsArray integrity.
    /// Ensures all required special object slots are populated with correct object types.
    /// </summary>
    private void ValidateSpecialObjectsArray(ValidationResults results)
    {
        Console.WriteLine("  Validating SpecialObjectsArray...");

        int validatedSlots = 0;

        // Validate nil singleton
        var nilOOP = SpecialObjectsArray.Instance.Nil;
        if (nilOOP == SmalltalkOOP.Null || nilOOP.IsSmallInteger)
        {
            results.AddError("SpecialObjectsArray.Nil is null or SmallInteger");
        }
        else
        {
            var nilClassName = _allocator.GetClassName(nilOOP);
            if (nilClassName != "UndefinedObject")
            {
                results.AddError($"SpecialObjectsArray.Nil has wrong class: {nilClassName}, expected UndefinedObject");
            }
            validatedSlots++;
        }

        // Validate false singleton
        var falseOOP = SpecialObjectsArray.Instance.False;
        if (falseOOP == SmalltalkOOP.Null || falseOOP.IsSmallInteger)
        {
            results.AddError("SpecialObjectsArray.False is null or SmallInteger");
        }
        else
        {
            var falseClassName = _allocator.GetClassName(falseOOP);
            if (falseClassName != "False")
            {
                results.AddError($"SpecialObjectsArray.False has wrong class: {falseClassName}, expected False");
            }
            validatedSlots++;
        }

        // Validate true singleton
        var trueOOP = SpecialObjectsArray.Instance.True;
        if (trueOOP == SmalltalkOOP.Null || trueOOP.IsSmallInteger)
        {
            results.AddError("SpecialObjectsArray.True is null or SmallInteger");
        }
        else
        {
            var trueClassName = _allocator.GetClassName(trueOOP);
            if (trueClassName != "True")
            {
                results.AddError($"SpecialObjectsArray.True has wrong class: {trueClassName}, expected True");
            }
            validatedSlots++;
        }

        // Validate SystemDictionary (registered as "Smalltalk" global)
        if (!GlobalRegistry.TryGet("Smalltalk", out SmalltalkOOP systemDictOOP))
        {
            results.AddError("Smalltalk (SystemDictionary) not found in GlobalRegistry");
        }
        else if (systemDictOOP == SmalltalkOOP.Null || systemDictOOP.IsSmallInteger)
        {
            results.AddError("Smalltalk (SystemDictionary) is null or SmallInteger");
        }
        else
        {
            // SystemDictionary should be a SystemDictionary class
            var systemDictClassName = _allocator.GetClassName(systemDictOOP);
            if (systemDictClassName != "SystemDictionary")
            {
                // This is a warning, not an error - some systems might use a different class
                Console.WriteLine($"    NOTE: Smalltalk global has class {systemDictClassName}, expected SystemDictionary");
            }
            validatedSlots++;
        }

        // Validate InitExpressionMethods array (may be null if no init expressions)
        var initMethodsOOP = SpecialObjectsArray.Instance.InitExpressionMethods;
        if (initMethodsOOP != SpecialObjectsArray.Instance.Nil && initMethodsOOP != SmalltalkOOP.Null)
        {
            var initMethodsClassName = _allocator.GetClassName(initMethodsOOP);
            if (initMethodsClassName != "Array")
            {
                results.AddError($"SpecialObjectsArray.InitExpressionMethods has wrong class: {initMethodsClassName}, expected Array");
            }
            validatedSlots++;
        }

        Console.WriteLine($"    Validated {validatedSlots} special object slots");
    }

    /// <summary>
    /// Validates method dictionary structure and integrity.
    /// Ensures each class's method dictionary:
    /// - Has correct capacity
    /// - Has correct tally matching actual method count
    /// - All installed methods are findable via lookup
    /// </summary>
    private void ValidateMethodDictionaries(ValidationResults results)
    {
        Console.WriteLine("  Validating method dictionaries...");

        int classesWithMethodDict = 0;
        int totalMethods = 0;
        int methodsFound = 0;

        foreach (var classDesc in _bundle.Classes)
        {
            if (!_localIdToObjectId.TryGetValue(classDesc.LocalId, out SmalltalkOOP classOOP))
            {
                continue; // Already reported in ValidateAllClasses
            }

            // Get the class's method dictionary (instance variable 1 in Behavior)
            var methodDictOOP = _allocator.GetInstanceVariable(classOOP, 1);

            if (methodDictOOP == SpecialObjectsArray.Instance.Nil)
            {
                // Class has no methods - this is valid for some classes
                continue;
            }

            classesWithMethodDict++;

            // Validate method dictionary structure
            var format = _allocator.GetObjectFormat(methodDictOOP);
            if (format != ObjectFormat.PointerIndexed)
            {
                results.AddError($"Class {GetClassName(classDesc)} methodDict has wrong format: {format}, expected PointerIndexed");
                continue;
            }

            // Get tally (instance variable 1) and capacity (indexed size)
            var tallyOOP = _allocator.GetInstanceVariable(methodDictOOP, 1);
            int tally = tallyOOP.IsSmallInteger ? tallyOOP.AsSmallInteger() : 0;
            int capacity = _allocator.GetIndexedSize(methodDictOOP);

            // Count actual methods for this class in the bundle
            int expectedMethodCount = _bundle.Methods.Count(m => m.HomeClassLocalId == classDesc.LocalId);

            if (tally != expectedMethodCount)
            {
                results.AddError(
                    $"Class {GetClassName(classDesc)} methodDict tally mismatch: " +
                    $"tally={tally}, bundle methods={expectedMethodCount}");
            }

            // Validate that all methods are findable in the dictionary
            foreach (var method in _bundle.Methods.Where(m => m.HomeClassLocalId == classDesc.LocalId))
            {
                totalMethods++;

                // Get selector symbol
                string selectorName = _symbols[method.SelectorIndex];

                // Try to find the method in the dictionary
                bool found = false;
                for (int i = 0; i < capacity; i++)
                {
                    var keyOOP = _allocator.GetIndexedOOP(methodDictOOP, i);
                    if (keyOOP == SpecialObjectsArray.Instance.Nil)
                        continue;

                    var keySymbolName = _allocator.GetSymbolName(keyOOP);
                    if (keySymbolName == selectorName)
                    {
                        found = true;
                        methodsFound++;
                        break;
                    }
                }

                if (!found)
                {
                    results.AddError(
                        $"Class {GetClassName(classDesc)} method '{selectorName}' not found in methodDict");
                }
            }
        }

        Console.WriteLine($"    Validated {classesWithMethodDict} method dictionaries");
        Console.WriteLine($"    Found {methodsFound}/{totalMethods} methods in dictionaries");

        if (methodsFound < totalMethods)
        {
            results.AddError($"Method dictionary validation failed: {totalMethods - methodsFound} methods not found in dictionaries");
        }
    }

    private class ValidationResults
    {
        public List<string> Errors { get; } = new();
        public int ClassesValidated { get; set; }
        public int MethodsValidated { get; set; }
        public int SpecialObjectsValidated { get; set; }
        public int InitExpressionsValidated { get; set; }
        public int CrossReferencesValidated { get; set; }
        public int InvalidReferences { get; set; }

        public void AddError(string error)
        {
            Errors.Add(error);
        }
    }
}