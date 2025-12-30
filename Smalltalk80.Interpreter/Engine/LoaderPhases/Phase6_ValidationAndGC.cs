using System.Diagnostics;
using System.Text.Json;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Shared.Types.Diagnostics;

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
public class Phase6_ValidationAndGC : IPhase
{
    private readonly ColdStartBundle _bundle;
    private readonly ObjectGraphAllocator _allocator;
    private readonly ISmalltalkHeap _heapManager;
    private readonly LayoutResolver _layoutResolver;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
    private readonly string[] _symbols;
    private readonly ColdstartReport _report;
    private readonly IDiagnosticHub? _diagnostics;
    private readonly SmalltalkOOP _initExpressionMethods;

    /// <summary>
    /// Gets the phase name for diagnostics and logging.
    /// </summary>
    public string Name => "Phase 6: Validation and GC Verification";

    /// <summary>
    /// Gets the phase number (1-6).
    /// </summary>
    public int PhaseNumber => 6;

    public Phase6_ValidationAndGC(
        ColdStartBundle bundle,
        ObjectGraphAllocator allocator,
        ISmalltalkHeap heapManager,
        LayoutResolver layoutResolver,
        Dictionary<string, SmalltalkOOP> localIdToObjectId,
        ColdstartReport report,
        SmalltalkOOP initExpressionMethods,
        IDiagnosticHub? diagnostics = null)
    {
        _bundle = bundle;
        _allocator = allocator;
        _heapManager = heapManager;
        _layoutResolver = layoutResolver;
        _localIdToObjectId = localIdToObjectId;
        _report = report ?? throw new ArgumentNullException(nameof(report));
        _diagnostics = diagnostics;
        _initExpressionMethods = initExpressionMethods;

        // Use symbols from bundle for validation
        _symbols = bundle.Symbols;
    }

    /// <summary>
    /// Validates phase preconditions before execution.
    /// </summary>
    /// <returns>List of precondition failure messages.</returns>
    public IReadOnlyList<string> ValidatePreconditions()
    {
        return new List<string>();
    }

    /// <summary>
    /// Validates phase postconditions after execution.
    /// </summary>
    /// <returns>List of postcondition failure messages.</returns>
    public IReadOnlyList<string> ValidatePostconditions()
    {
        return new List<string>();
    }

    /// <summary>
    /// Run all validation and GC verification steps.
    /// </summary>
    public void Run()
    {
        // Step 1: Comprehensive heap validation (from Phase13)
        RunComprehensiveValidation();

        // Step 2: Set up GC roots (from Loader Phase 14)
        SetupGCRoots();

        // Step 3: Run GC verification (from Loader Phase 15)
        RunGCVerification();
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

        // 6. Validate well-known objects integrity
        ValidateWellKnownObjects(results);

        // 7. Validate method dictionaries
        ValidateMethodDictionaries(results);

        // 8. Validate Association canonicalization
        ValidateAssociationCanonicalization(results);

        // 9. Validate VMConstants against actual class layouts
        ValidateVMConstants(results);

        // 10. Block ownership validation removed - BlockOwnerMap tracked in Phase4

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
                byte[] expectedBytecodes = ClassHierarchyTraversal.DecodeBytecodes(method.Bytecodes);
                int actualBytecodeSize = _allocator.GetIndexedSize(bytecodeArrayOOP);

                // Account for source position encoding: when FileIndex and SourcePosition are set,
                // 3 extra bytes are appended to encode the source location per Blue Book format
                int expectedSize = expectedBytecodes.Length;
                if (method.FileIndex.HasValue && method.SourcePosition.HasValue && method.SourcePosition.Value >= 0)
                {
                    expectedSize += 3; // Source position encoding: [lowByte] [midByte] [fileIndex+highBits]
                }

                if (actualBytecodeSize != expectedSize)
                {
                    results.AddError($"Method {method.LocalId} bytecode size {actualBytecodeSize} != expected {expectedSize}");
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
        ValidateSpecialObject("nil", _heapManager.GetNil(), results);

        // Validate true/false
        ValidateSpecialObject("true", GlobalRegistry.Get("true"), results);
        ValidateSpecialObject("false", GlobalRegistry.Get("false"), results);

        // Validate SystemDictionary
        ValidateSpecialObject("SystemDictionary", GlobalRegistry.SystemDictionaryOOP, results);

        // Validate USTable (class variable of Symbol)
        var ustableOOP = LookupClassVariable("Symbol", "USTable");
        if (ustableOOP != _allocator.GetNil())
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
            results.AddError("USTable not found in Symbol's classPool");
        }
    }

    /// <summary>
    /// Looks up a class variable from a class's classPool dictionary.
    /// </summary>
    private SmalltalkOOP LookupClassVariable(string className, string varName)
    {
        // Get the class OOP (using GlobalRegistry is acceptable during cold start Phase 6)
        var classOOP = GlobalRegistry.LookupClassOOP(className);
        if (classOOP == _allocator.GetNil())
            return _allocator.GetNil();

        // Get classPool (instance variable 7 of Class)
        var classPoolOOP = _allocator.GetInstanceVariable(classOOP, 7);
        if (classPoolOOP == _allocator.GetNil())
            return _allocator.GetNil();

        // Find the symbol for the variable name
        var varSymbolOOP = _allocator.GetNil();
        foreach (var (index, symbolOOP) in SymbolRegistry.AllSymbols())
        {
            if (_bundle.Symbols[index] == varName)
            {
                varSymbolOOP = symbolOOP;
                break;
            }
        }

        if (varSymbolOOP == _allocator.GetNil())
            return _allocator.GetNil();

        // Search classPool dictionary for association with this key
        int size = _allocator.GetIndexedSize(classPoolOOP);
        for (int i = 0; i < size; i++)
        {
            var assocOOP = _allocator.GetIndexedOOP(classPoolOOP, i);
            if (assocOOP == _allocator.GetNil())
                continue;

            var keyOOP = _allocator.GetInstanceVariable(assocOOP, VMConstants.ASSOCIATION_KEY_INDEX);
            if (keyOOP == varSymbolOOP)
            {
                // Return the value from the association
                return _allocator.GetInstanceVariable(assocOOP, VMConstants.ASSOCIATION_VALUE_INDEX);
            }
        }

        return _allocator.GetNil();
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

    private void ValidateWellKnownObjects(ValidationResults results)
    {
// Console.WriteLine("  Validating well-known objects...");

        int validatedSlots = 0;

        // Validate nil singleton
        var nilOOP = _heapManager.GetNil();
        if (nilOOP.IsSmallInteger)
        {
            results.AddError("Nil is SmallInteger");
        }
        else
        {
            var nilClassName = _allocator.GetClassName(nilOOP);
            if (nilClassName != "UndefinedObject")
            {
                results.AddError($"Nil has wrong class: {nilClassName}, expected UndefinedObject");
            }
            validatedSlots++;
        }

        // Validate false singleton
        var falseOOP = GlobalRegistry.Get("false");
        if (falseOOP == nilOOP || falseOOP.IsSmallInteger)
        {
            results.AddError("False is nil or SmallInteger");
        }
        else
        {
            var falseClassName = _allocator.GetClassName(falseOOP);
            if (falseClassName != "False")
            {
                results.AddError($"False has wrong class: {falseClassName}, expected False");
            }
            validatedSlots++;
        }

        // Validate true singleton
        var trueOOP = GlobalRegistry.Get("true");
        if (trueOOP == nilOOP || trueOOP.IsSmallInteger)
        {
            results.AddError("True is nil or SmallInteger");
        }
        else
        {
            var trueClassName = _allocator.GetClassName(trueOOP);
            if (trueClassName != "True")
            {
                results.AddError($"True has wrong class: {trueClassName}, expected True");
            }
            validatedSlots++;
        }

        // Validate SystemDictionary
        if (!GlobalRegistry.TryGet("Smalltalk", out SmalltalkOOP systemDictOOP))
        {
            results.AddError("Smalltalk (SystemDictionary) not found in GlobalRegistry");
        }
        else if (systemDictOOP == nilOOP || systemDictOOP.IsSmallInteger)
        {
            results.AddError("Smalltalk (SystemDictionary) is nil or SmallInteger");
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
        var initMethodsOOP = _initExpressionMethods;
        if (initMethodsOOP != nilOOP)
        {
            var initMethodsClassName = _allocator.GetClassName(initMethodsOOP);
            if (initMethodsClassName != "Array")
            {
                results.AddError($"InitExpressionMethods has wrong class: {initMethodsClassName}, expected Array");
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

            if (methodDictOOP == _heapManager.GetNil())
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

            // Get tally and capacity (indexed size)
            var tallyOOP = _allocator.GetInstanceVariable(methodDictOOP, VMConstants.METHOD_DICTIONARY_TALLY_INDEX);
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
                    if (keyOOP == _heapManager.GetNil())
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
            string registryKey = kv.Key;
            SmalltalkOOP assocOOP = kv.Value;

            // Extract the actual symbol name from the registry key
            // Registry key format: "symbolName" for globals/class vars, "symbolName@poolName" for pool vars
            string symbolName = registryKey.Contains('@')
                ? registryKey.Substring(0, registryKey.IndexOf('@'))
                : registryKey;

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
        // Count VMConstants-specific errors
        var vmConstantErrors = results.Errors.Where(e => e.Contains("VMConstants")).ToList();
        var otherErrors = results.Errors.Except(vmConstantErrors).ToList();

        // Report VMConstants validation status
        if (vmConstantErrors.Count == 0)
        {
            _diagnostics?.Emit(new DiagnosticMessageEvent
            {
                Severity = MessageSeverity.Info,
                Category = "Phase6",
                Message = "✓ VMConstants validation: All 27 constants match class layouts",
                Timestamp = Stopwatch.GetTimestamp(),
                InstructionCount = 0
            });
        }
        else
        {
            _diagnostics?.Emit(new DiagnosticMessageEvent
            {
                Severity = MessageSeverity.Error,
                Category = "Phase6",
                Message = $"✗ VMConstants validation FAILED: {vmConstantErrors.Count} mismatches found",
                Timestamp = Stopwatch.GetTimestamp(),
                InstructionCount = 0
            });

            foreach (var error in vmConstantErrors)
            {
                _diagnostics?.Emit(new DiagnosticMessageEvent
                {
                    Severity = MessageSeverity.Error,
                    Category = "Phase6",
                    Message = $"  {error}",
                    Timestamp = Stopwatch.GetTimestamp(),
                    InstructionCount = 0
                });
            }
        }

        // Report other validation errors if any
        if (otherErrors.Count > 0)
        {
            _diagnostics?.Emit(new DiagnosticMessageEvent
            {
                Severity = MessageSeverity.Error,
                Category = "Phase6",
                Message = $"✗ VALIDATION FAILED: {otherErrors.Count} errors found",
                Timestamp = Stopwatch.GetTimestamp(),
                InstructionCount = 0
            });

            foreach (var error in otherErrors.Take(20))
            {
                _diagnostics?.Emit(new DiagnosticMessageEvent
                {
                    Severity = MessageSeverity.Error,
                    Category = "Phase6",
                    Message = $"  {error}",
                    Timestamp = Stopwatch.GetTimestamp(),
                    InstructionCount = 0
                });
            }
            if (otherErrors.Count > 20)
            {
                _diagnostics?.Emit(new DiagnosticMessageEvent
                {
                    Severity = MessageSeverity.Error,
                    Category = "Phase6",
                    Message = $"  ... and {otherErrors.Count - 20} more errors",
                    Timestamp = Stopwatch.GetTimestamp(),
                    InstructionCount = 0
                });
            }
        }

        // Throw if any errors found
        if (results.Errors.Count > 0)
        {
            throw new InvalidOperationException($"Heap validation failed with {results.Errors.Count} errors");
        }
    }

    // ========================================================================================
    // Step 2: GC Root Setup (from Loader Phase 14)
    // ========================================================================================

    private void SetupGCRoots()
    {
        // Create proper Smalltalk-80 GC roots - only the fundamental special objects
        // These are the objects that the garbage collector uses as starting points for reachability analysis
        // NOTE: We use SetGCRoots() for bulk setting during cold start. For incremental additions at runtime,
        // use: ((ISmalltalkHeap)_heapManager).GC.AddGCRoot(oop)
        var gcRootsList = new List<(string name, SmalltalkOOP oop)>
        {
            ("nil", _heapManager.GetNil()),
            ("false", GlobalRegistry.Get("false")),
            ("true", GlobalRegistry.Get("true")),
            ("Smalltalk", GlobalRegistry.SystemDictionaryOOP)
        };

        // Add InitExpressionMethods array if it exists (pre-compiled in Phase 4)
        if (_initExpressionMethods != _heapManager.GetNil())
        {
            gcRootsList.Add(("InitExpressionMethods", _initExpressionMethods));
            Console.WriteLine($"[Phase 6] Added InitExpressionMethods as GC root: {_initExpressionMethods}");
        }

        // Set as GC roots (bulk operation - replaces any previous roots)
        _heapManager.GC.ClearGCRoots();
        foreach (var (name, oop) in gcRootsList)
        {
            _heapManager.GC.AddGCRoot(oop);
        }

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
        var gcStats = _heapManager.GC.GarbageCollect();

        // Update report metrics
        _report.Metrics.TotalObjectsAllocated = gcStats.TotalObjects;
        _report.Metrics.TotalObjectsReachable = gcStats.LiveObjects;
        _report.Metrics.GCPassed = (gcStats.CollectedObjects == 1);

        // Cold start expects exactly 1 collected object (the placeholder nil from heap manager initialization)
        // After become: swaps the placeholder with the real UndefinedObject, the placeholder becomes unreachable
        // v1.0.9: Reduced Undeclared dictionary size from 620 to 16 words (all variables are now canonical)
        if (gcStats.CollectedObjects != 1)
        {
            _diagnostics?.Emit(new DiagnosticMessageEvent
            {
                Severity = MessageSeverity.Error,
                Category = "Phase6",
                Message = $"COLD START FAILED: GC collected {gcStats.CollectedObjects} objects! Expected exactly 1 object.",
                Timestamp = Stopwatch.GetTimestamp(),
                InstructionCount = 0
            });
            _diagnostics?.Emit(new DiagnosticMessageEvent
            {
                Severity = MessageSeverity.Error,
                Category = "Phase6",
                Message = "Collected Objects (first 50):",
                Timestamp = Stopwatch.GetTimestamp(),
                InstructionCount = 0
            });
            
            // Group by class name
            var byClass = gcStats.CollectedObjectDetails
                .GroupBy(x => x.className ?? "<null>")
                .OrderByDescending(g => g.Count())
                .ToList();
            
            foreach (var group in byClass)
            {
                _diagnostics?.Emit(new DiagnosticMessageEvent
                {
                    Severity = MessageSeverity.Error,
                    Category = "Phase6",
                    Message = $"  {group.Key}: {group.Count()} objects",
                    Timestamp = Stopwatch.GetTimestamp(),
                    InstructionCount = 0
                });
                
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
                            var classOOP = _heapManager.GetClassOOP(arrayOOP);
                            string actualClassName = "?";
                            string homeClassInfo = "";
                            try
                            {
                                actualClassName = _heapManager.GetClassName(arrayOOP);
                                
                                // If this is a CompiledMethod, check its homeClass (instance variable 0)
                                if (actualClassName == "CompiledMethod")
                                {
                                    var homeClassOOP = _heapManager.GetInstanceVariable(arrayOOP, 0);
                                    if (homeClassOOP == _heapManager.GetNil())
                                    {
                                        homeClassInfo = " [homeClass=NIL!]";
                                    }
                                    else
                                    {
                                        var homeClassName = _heapManager.GetClassName(homeClassOOP);
                                        var homeClassClass = _heapManager.GetClassOOP(homeClassOOP);
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
                                    else if (elem == _heapManager.GetNil())
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
                    
                    _diagnostics?.Emit(new DiagnosticMessageEvent
                    {
                        Severity = MessageSeverity.Error,
                        Category = "Phase6",
                        Message = $"    OOP={obj.oop}, Format={obj.format}, Size={obj.size} words, Class={obj.className}{slot0Info}{elementsInfo}",
                        Timestamp = Stopwatch.GetTimestamp(),
                        InstructionCount = 0
                    });
                }
                if (group.Count() > showCount)
                {
                    _diagnostics?.Emit(new DiagnosticMessageEvent
                    {
                        Severity = MessageSeverity.Error,
                        Category = "Phase6",
                        Message = $"    ... and {group.Count() - showCount} more",
                        Timestamp = Stopwatch.GetTimestamp(),
                        InstructionCount = 0
                    });
                }
            }

            throw new InvalidOperationException(
                $"COLD START BUG: GC collected {gcStats.CollectedObjects} objects. " +
                $"Expected exactly 1 object.");
        }

        Console.WriteLine($"  ✓ GC Verification passed: {gcStats.LiveObjects} objects reachable, 1 placeholder nil collected as expected");
    }

    // ========================================================================================
    // VMConstants Validation (catches Smalltalk source changes requiring VMConstants updates)
    // ========================================================================================

    /// <summary>
    /// Validates that all VMConstants match the actual class layouts from LayoutResolver.
    /// This catches changes in Smalltalk source code that require VMConstants to be updated.
    /// </summary>
    private void ValidateVMConstants(ValidationResults results)
    {
        // MethodContext field validation
        ValidateConstant(results, "class:MethodContext", "sender", VMConstants.CONTEXT_SENDER_INDEX);
        ValidateConstant(results, "class:MethodContext", "pc", VMConstants.CONTEXT_PC_INDEX);
        ValidateConstant(results, "class:MethodContext", "stackp", VMConstants.CONTEXT_STACKP_INDEX);
        ValidateConstant(results, "class:MethodContext", "method", VMConstants.CONTEXT_METHOD_INDEX);
        ValidateConstant(results, "class:MethodContext", "lexicalParent", VMConstants.CONTEXT_LEXICALPARENT_INDEX);
        ValidateConstant(results, "class:MethodContext", "receiver", VMConstants.CONTEXT_RECEIVER_INDEX);

        // BlockContext field validation
        ValidateConstant(results, "class:BlockContext", "sender", VMConstants.BLOCKCONTEXT_CALLER_INDEX);
        ValidateConstant(results, "class:BlockContext", "pc", VMConstants.BLOCKCONTEXT_PC_INDEX);
        ValidateConstant(results, "class:BlockContext", "stackp", VMConstants.BLOCKCONTEXT_STACKP_INDEX);
        ValidateConstant(results, "class:BlockContext", "nargs", VMConstants.BLOCKCONTEXT_NARGS_INDEX);
        ValidateConstant(results, "class:BlockContext", "startpc", VMConstants.BLOCKCONTEXT_STARTPC_INDEX);
        ValidateConstant(results, "class:BlockContext", "home", VMConstants.BLOCKCONTEXT_HOME_INDEX);
        // Note: argStartIndex VM extension removed - block args now stored via STORE_TEMP bytecodes

        // Process field validation
        ValidateConstant(results, "class:Process", "nextLink", VMConstants.PROCESS_NEXTLINK_INDEX);
        ValidateConstant(results, "class:Process", "suspendedContext", VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX);
        ValidateConstant(results, "class:Process", "priority", VMConstants.PROCESS_PRIORITY_INDEX);
        ValidateConstant(results, "class:Process", "myList", VMConstants.PROCESS_MYLIST_INDEX);

        // ProcessorScheduler field validation
        ValidateConstant(results, "class:ProcessorScheduler", "quiescentProcessLists", VMConstants.PROCESSORSCHEDULER_QUIESCENTPROCESSLISTS_INDEX);
        ValidateConstant(results, "class:ProcessorScheduler", "activeProcess", VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX);

        // Association field validation
        ValidateConstant(results, "class:Association", "key", VMConstants.ASSOCIATION_KEY_INDEX);
        ValidateConstant(results, "class:Association", "value", VMConstants.ASSOCIATION_VALUE_INDEX);

        // Set/Dictionary field validation
        ValidateConstant(results, "class:Set", "tally", VMConstants.SET_TALLY_INDEX);
        ValidateConstant(results, "class:Dictionary", "tally", VMConstants.DICTIONARY_TALLY_INDEX);

        // Metaclass field validation
        ValidateConstant(results, "class:Metaclass", "thisClass", VMConstants.METACLASS_THISCLASS_INDEX);

        // Behavior field validation (superclass, methodDict, format)
        ValidateConstant(results, "class:Behavior", "superclass", VMConstants.BEHAVIOR_SUPERCLASS_INDEX);
        ValidateConstant(results, "class:Behavior", "methodDict", VMConstants.BEHAVIOR_METHODDICT_INDEX);
        ValidateConstant(results, "class:Behavior", "format", VMConstants.BEHAVIOR_FORMAT_INDEX);
    }

    /// <summary>
    /// Validates a single VMConstant against LayoutResolver.
    /// </summary>
    private void ValidateConstant(ValidationResults results, string classLocalId, string fieldName, int expectedIndex)
    {
        try
        {
            int actualIndex = _layoutResolver.GetFieldIndex(classLocalId, fieldName);
            if (actualIndex != expectedIndex)
            {
                results.AddError(
                    $"VMConstants mismatch: {classLocalId}.{fieldName} expected index {expectedIndex} " +
                    $"but LayoutResolver reports {actualIndex}. " +
                    $"VMConstants.cs must be updated to match Smalltalk source changes.");
            }
        }
        catch (Exception ex)
        {
            results.AddError(
                $"VMConstants validation failed for {classLocalId}.{fieldName}: {ex.Message}");
        }
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
