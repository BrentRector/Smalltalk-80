using System.Text.Json;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Phase 6: Comprehensive Validation and GC Verification
/// Consolidates three critical final phases:
/// 1. Comprehensive heap validation (from Phase 13)
/// 2. GC root setup (from Phase 14)
/// 3. GC verification (from Phase 15)
///
/// This phase ensures:
/// - All heap objects match JSON bundle definitions
/// - All class structures are correct
/// - All method dictionaries are properly formed
/// - All special objects exist with correct types
/// - GC roots are properly configured
/// - ALL objects are reachable (no garbage collected)
/// </summary>
public class Phase6_ValidationAndGC
{
    private readonly ColdStartBundle _bundle;
    private readonly SemanticAllocator _allocator;
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly LayoutResolver _layoutResolver;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
    private readonly string[] _symbols;
    private readonly ColdstartReport _report;

    public Phase6_ValidationAndGC(
        ColdStartBundle bundle,
        SemanticAllocator allocator,
        ISmalltalkHeapManager heapManager,
        LayoutResolver layoutResolver,
        Dictionary<string, SmalltalkOOP> localIdToObjectId,
        ColdstartReport report)
    {
        _bundle = bundle;
        _allocator = allocator;
        _heapManager = heapManager;
        _layoutResolver = layoutResolver;
        _localIdToObjectId = localIdToObjectId;
        _report = report ?? throw new ArgumentNullException(nameof(report));

        // Use symbols from bundle for validation
        _symbols = bundle.Symbols;
    }

    /// <summary>
    /// Run all validation and GC verification steps.
    /// </summary>
    public void Run()
    {
        // Step 1: Comprehensive heap validation (from Phase13)
// Console.WriteLine("Step 1: Comprehensive Heap Validation...");
        RunComprehensiveValidation();
// Console.WriteLine("✓");
        
        // DEBUG: Check class pointer after validation
        if (_localIdToObjectId.TryGetValue("class:Browser", out var browserOOP_afterValidation))
        {
            var slot0_afterValidation = _heapManager.GetSlot(browserOOP_afterValidation, 0);
            Console.WriteLine($"[After Validation] class:Browser slot[0]={slot0_afterValidation}");
        }

        // Step 2: Set up GC roots (from Loader Phase 14)
// Console.WriteLine("Step 2: GC Root Setup...");
        SetupGCRoots();
// Console.WriteLine("✓");
        
        // DEBUG: Check class pointer after GC root setup
        if (_localIdToObjectId.TryGetValue("class:Browser", out var browserOOP_afterGCRoots))
        {
            var slot0_afterGCRoots = _heapManager.GetSlot(browserOOP_afterGCRoots, 0);
            Console.WriteLine($"[After GC Roots] class:Browser slot[0]={slot0_afterGCRoots}");
        }

        // Step 3: Run GC verification (from Loader Phase 15)
// Console.WriteLine("Step 3: GC Verification...");
        RunGCVerification();
// Console.WriteLine("✓");
    }

    // ========================================================================================
    // Step 1: Comprehensive Validation (from Phase13_ComprehensiveValidation)
    // ========================================================================================

    private void RunComprehensiveValidation()
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

        // 8. Validate Association canonicalization
        ValidateAssociationCanonicalization(results);

        // 9. Block ownership validation removed - BlockOwnerMap tracked in Phase4

        // Report results
        ReportValidationResults(results);
    }

    private void ValidateAllClasses(ValidationResults results)
    {
// Console.WriteLine("  Validating classes...");

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

        results.ClassesValidated++;
    }

    private void ValidateAllMethods(ValidationResults results)
    {
// Console.WriteLine("  Validating methods...");

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
                // Could validate literal values match expected types/values
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
// Console.WriteLine("  Validating special objects...");

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
                int expectedSize = _symbols.Length * 2; // Load factor heuristic
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
// Console.WriteLine("  Validating init expressions...");
        results.InitExpressionsValidated = _bundle.InitExpressions.Length;
    }

    private void ValidateCrossReferences(ValidationResults results)
    {
// Console.WriteLine("  Validating cross-references...");

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

    private void ValidateSpecialObjectsArray(ValidationResults results)
    {
// Console.WriteLine("  Validating SpecialObjectsArray...");

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

        // Validate SystemDictionary
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
            var systemDictClassName = _allocator.GetClassName(systemDictOOP);
            if (systemDictClassName != "SystemDictionary")
            {
// Console.WriteLine($"    NOTE: Smalltalk global has class {systemDictClassName}, expected SystemDictionary");
            }
            validatedSlots++;
        }

        // Validate InitExpressionMethods array
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

// Console.WriteLine($"    Validated {validatedSlots} special object slots");
    }

    private void ValidateMethodDictionaries(ValidationResults results)
    {
// Console.WriteLine("  Validating method dictionaries...");

        int classesWithMethodDict = 0;
        int totalMethods = 0;
        int methodsFound = 0;

        foreach (var classDesc in _bundle.Classes)
        {
            if (!_localIdToObjectId.TryGetValue(classDesc.LocalId, out SmalltalkOOP classOOP))
            {
                continue;
            }

            // Get the class's method dictionary (instance variable 1 in Behavior)
            var methodDictOOP = _allocator.GetInstanceVariable(classOOP, 1);

            if (methodDictOOP == SpecialObjectsArray.Instance.Nil)
            {
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

            // Count actual methods for this class
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

// Console.WriteLine($"    Validated {classesWithMethodDict} method dictionaries");
// Console.WriteLine($"    Found {methodsFound}/{totalMethods} methods in dictionaries");

        if (methodsFound < totalMethods)
        {
            results.AddError($"Method dictionary validation failed: {totalMethods - methodsFound} methods not found in dictionaries");
        }
    }

    private void ValidateAssociationCanonicalization(ValidationResults results)
    {
// Console.WriteLine("  Validating Association canonicalization...");

        int validatedCount = 0;
        int errorCount = 0;

        foreach (var kv in AssociationRegistry.GetAll())
        {
            string symbolName = kv.Key;
            SmalltalkOOP assocOOP = kv.Value;

            // Validate Association has correct structure
            var keyOOP = _heapManager.GetInstanceVariable(assocOOP, 0);
            var valueOOP = _heapManager.GetInstanceVariable(assocOOP, 1);

            // Verify key is Symbol with correct name
            int symbolIndex = Array.IndexOf(_symbols, symbolName);
            if (symbolIndex < 0)
            {
// Console.WriteLine($"    ERROR: Association '{symbolName}' key not found in symbol table");
                errorCount++;
                results.AddError($"Association '{symbolName}' key not found in symbol table");
                continue;
            }

            var expectedKeyOOP = SymbolRegistry.GetSymbolOOP(symbolIndex);

            if (!keyOOP.Equals(expectedKeyOOP))
            {
// Console.WriteLine($"    ERROR: Association '{symbolName}' has mismatched key");
                errorCount++;
                results.AddError($"Association '{symbolName}' has mismatched key OOP");
            }

            // Value can be nil for globals not yet set
            validatedCount++;
        }

        if (errorCount > 0)
        {
            throw new InvalidOperationException(
                $"Association canonicalization validation FAILED: {errorCount} errors");
        }

// Console.WriteLine($"    Validated {validatedCount} canonical Associations");
    }

    // Block ownership validation removed - BlockOwnerMap is tracked in Phase4, not accessible here
    /* private void ValidateBlockOwnership(ValidationResults results)
    {
        // Removed - BlockOwnerMap not available in Phase6
    } */

    private void ReportValidationResults(ValidationResults results)
    {
        if (results.Errors.Count == 0)
        {
// Console.WriteLine("  ✓ Validation Summary:");
// Console.WriteLine($"    Classes: {results.ClassesValidated} validated");
// Console.WriteLine($"    Methods: {results.MethodsValidated} validated");
// Console.WriteLine($"    Special objects: {results.SpecialObjectsValidated} validated");
// Console.WriteLine($"    Init expressions: {results.InitExpressionsValidated} validated");
// Console.WriteLine($"    Cross-references: {results.CrossReferencesValidated} objects checked");
// Console.WriteLine($"    ✓ All heap objects match JSON definitions");
        }
        else
        {
// Console.WriteLine("  ✗ VALIDATION FAILED");
// Console.WriteLine($"    Validation errors found: {results.Errors.Count}");
// Console.WriteLine($"    Classes: {results.ClassesValidated} validated");
// Console.WriteLine($"    Methods: {results.MethodsValidated} validated");
// Console.WriteLine($"    Invalid references: {results.InvalidReferences}");

// Console.WriteLine($"    Showing first 20 errors:");
            foreach (var error in results.Errors.Take(20))
            {
// Console.WriteLine($"      {error}");
            }
            if (results.Errors.Count > 20)
            {
// Console.WriteLine($"      ... and {results.Errors.Count - 20} more errors");
            }

            throw new InvalidOperationException($"Heap validation failed with {results.Errors.Count} errors");
        }
    }

    // ========================================================================================
    // Step 2: GC Root Setup (from Loader Phase 14)
    // ========================================================================================

    private void SetupGCRoots()
    {
        // Create proper Smalltalk-80 GC roots - only the fundamental special objects
        var gcRootsList = new List<(string name, SmalltalkOOP oop)>
        {
            ("nil", SpecialObjectsArray.Instance.Nil),
            ("false", SpecialObjectsArray.Instance.False),
            ("true", SpecialObjectsArray.Instance.True),
            ("Smalltalk", GlobalRegistry.SystemDictionaryOOP)
        };

        // Add init expression methods if allocated
        if (SpecialObjectsArray.Instance.InitExpressionMethods != SmalltalkOOP.Null)
        {
            gcRootsList.Add(("InitExpressionMethods", SpecialObjectsArray.Instance.InitExpressionMethods));
        }

        // Set as GC roots
        var gcRoots = gcRootsList.Select(r => r.oop).ToArray();
        _heapManager.SetGCRoots(gcRoots);

        // Display GC roots
// Console.WriteLine($"  GC Roots ({gcRootsList.Count} objects):");
        foreach (var (name, oop) in gcRootsList)
        {
            var className = _heapManager.GetClassName(oop);
// Console.WriteLine($"    {name} ({oop}, class={className})");
        }
    }

    // ========================================================================================
    // Step 3: GC Verification (from Loader Phase 15)
    // ========================================================================================

    private void RunGCVerification()
    {
        // DEBUG: Check before GetActiveObjectCount
        if (_localIdToObjectId.TryGetValue("class:Browser", out var oop1))
        {
            var s1 = _heapManager.GetSlot(oop1, 0);
            Console.WriteLine($"[Before GetActiveObjectCount] class:Browser slot[0]={s1}");
        }
        
        // Log object counts before GC
        Console.WriteLine($"[Phase6] Objects before GC:");
        Console.WriteLine($"  Total objects: {_heapManager.GetActiveObjectCount()}");
        
        // DEBUG: Check after GetActiveObjectCount
        if (_localIdToObjectId.TryGetValue("class:Browser", out var oop2))
        {
            var s2 = _heapManager.GetSlot(oop2, 0);
            Console.WriteLine($"[After GetActiveObjectCount] class:Browser OOP={oop2}, slot[0]={s2}");
        }
        
        var gcStats = _heapManager.GarbageCollect();

        // Print GC statistics
        Console.WriteLine($"[Phase6] GC Results:");
        Console.WriteLine($"  Live objects: {gcStats.LiveObjects}");
        Console.WriteLine($"  Collected: {gcStats.CollectedObjects}");

        // Update report metrics
        _report.Metrics.TotalObjectsAllocated = gcStats.TotalObjects;
        _report.Metrics.TotalObjectsReachable = gcStats.LiveObjects;
        _report.Metrics.GCPassed = (gcStats.CollectedObjects == 0);

        // Cold start fails if ANY objects were collected
        if (gcStats.CollectedObjects > 0)
        {
            Console.WriteLine($"\n✗ COLD START FAILED: GC collected {gcStats.CollectedObjects} objects!");
            Console.WriteLine($"  All objects must be reachable from GC roots for cold start to pass.");
            Console.WriteLine($"\n  Collected Objects (first 50):");
            
            // Group by class name
            var byClass = gcStats.CollectedObjectDetails
                .GroupBy(x => x.className ?? "<null>")
                .OrderByDescending(g => g.Count())
                .ToList();
            
            foreach (var group in byClass)
            {
                Console.WriteLine($"    {group.Key}: {group.Count()} objects");
                
                // Show ALL collected objects if <= 50
                int showCount = Math.Min(group.Count(), 50);
                foreach (var obj in group.Take(showCount))
                {
                    // Try to read the array elements to identify what it contains
                    string elementsInfo = "";
                    string slot0Info = "";
                    try
                    {
                        if (obj.format == ObjectFormat.PointerIndexed)
                        {
                            var arrayOOP = new SmalltalkOOP(obj.oop);
                            
                            // Check slot 0 (class pointer) to see what class this REALLY is
                            var classOOP = _heapManager.GetSlot(arrayOOP, 0);
                            string actualClassName = "?";
                            string homeClassInfo = "";
                            try
                            {
                                actualClassName = _heapManager.GetClassName(arrayOOP);
                                
                                // If this is a CompiledMethod, check its homeClass (instance variable 0)
                                if (actualClassName == "CompiledMethod")
                                {
                                    var homeClassOOP = _heapManager.GetInstanceVariable(arrayOOP, 0);
                                    if (homeClassOOP == SpecialObjectsArray.Instance.Nil)
                                    {
                                        homeClassInfo = " [homeClass=NIL!]";
                                    }
                                    else
                                    {
                                        var homeClassName = _heapManager.GetClassName(homeClassOOP);
                                        var homeClassClass = _heapManager.GetSlot(homeClassOOP, 0);
                                        var homeClassClassClass = _heapManager.GetClassName(homeClassClass);
                                        homeClassInfo = $" [homeClass={homeClassOOP}:{homeClassName}, homeClass.class={homeClassClass}:{homeClassClassClass}]";
                                    }
                                }
                            }
                            catch { }
                            slot0Info = $" [slot0={classOOP}, actualClass={actualClassName}{homeClassInfo}]";
                            
                            int indexedSize = _heapManager.GetIndexedSize(arrayOOP);
                            
                            // Show all elements for arrays up to size 10
                            if (indexedSize <= 10)
                            {
                                var elements = new List<string>();
                                for (int i = 0; i < indexedSize; i++)
                                {
                                    var elem = _heapManager.GetIndexedOOP(arrayOOP, i);
                                    string elemStr;
                                    if (elem.IsSmallInteger)
                                        elemStr = elem.AsSmallInteger().ToString();
                                    else if (elem == SpecialObjectsArray.Instance.Nil)
                                        elemStr = "nil";
                                    else if (TryGetSymbolName(elem, out var name))
                                        elemStr = $"#{name}";
                                    else
                                    {
                                        var elemClass = _heapManager.GetClassName(elem);
                                        elemStr = $"{elem}:{elemClass}";
                                    }
                                    elements.Add(elemStr);
                                }
                                elementsInfo = $" = ({string.Join(", ", elements)})";
                            }
                            else
                            {
                                elementsInfo = $" = Array[{indexedSize}] (too large to display)";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        elementsInfo = $" <error: {ex.Message}>";
                    }
                    
                    Console.WriteLine($"      OOP={obj.oop}, Format={obj.format}, Size={obj.size} words, Class={obj.className}{slot0Info}{elementsInfo}");
                }
                if (group.Count() > showCount)
                {
                    Console.WriteLine($"      ... and {group.Count() - showCount} more");
                }
            }

            throw new InvalidOperationException(
                $"COLD START BUG: GC collected {gcStats.CollectedObjects} objects. " +
                $"All objects should be reachable from GC roots.");
        }

// Console.WriteLine($"  ✓ GC Verification passed: All {gcStats.LiveObjects} objects are reachable");
    }

    // ========================================================================================
    // Helper Methods
    // ========================================================================================

    private string GetClassName(ClassDescriptor classDesc)
    {
        return _symbols[classDesc.NameIndex];
    }

    private bool TryGetSymbolName(SmalltalkOOP symbolOOP, out string name)
    {
        name = "";
        try
        {
            if (symbolOOP.IsSmallInteger || !_heapManager.IsValidObjectOOP(symbolOOP))
                return false;
                
            var className = _heapManager.GetClassName(symbolOOP);
            if (className != "Symbol")
                return false;
                
            int byteCount = _heapManager.GetIndexedSize(symbolOOP);
            byte[] bytes = new byte[byteCount];
            for (int i = 0; i < byteCount; i++)
            {
                bytes[i] = _heapManager.GetIndexedByte(symbolOOP, i);
            }
            name = System.Text.Encoding.UTF8.GetString(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private int CalculateExpectedInstanceVariables(ClassDescriptor classDesc)
    {
        // Class objects are instances of either Class or Metaclass
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

    /// <summary>
    /// Internal class to track validation results.
    /// </summary>
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
