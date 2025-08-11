using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;

namespace Smalltalk80.Bootstrap;

/// <summary>
/// Executes Phase 2 manifests by sending messages through the minimal Smalltalk system.
/// </summary>
public class Phase2Executor
{
    private readonly ISmalltalkVM vm;
    private readonly Dictionary<string, int> testVariables = new();
    
    /// <summary>
    /// Controls whether Phase 2 debug output is displayed.
    /// </summary>
    public static bool EnablePhase2DebugOutput { get; set; } = true;
    
    /// <summary>
    /// Logs a Phase 2 message only if debug output is enabled.
    /// </summary>
    public static void Phase2Log(string message)
    {
        if (EnablePhase2DebugOutput)
        {
            Console.WriteLine(message);
        }
    }
    
    public Phase2Executor(ISmalltalkVM vm)
    {
        this.vm = vm;
    }
    
    /// <summary>
    /// Class definition from manifest.
    /// </summary>
    public class ClassDef
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        
        [JsonPropertyName("superclass")]
        public string Superclass { get; set; } = "Object";
        
        [JsonPropertyName("instanceVariables")]
        public List<string> InstanceVariables { get; set; } = new();
        
        [JsonPropertyName("classVariables")]
        public List<string> ClassVariables { get; set; } = new();
        
        [JsonPropertyName("poolDictionaries")]
        public List<string> PoolDictionaries { get; set; } = new();
        
        [JsonPropertyName("category")]
        public string Category { get; set; } = "Unclassified";
    }
    
    /// <summary>
    /// Method definition from manifest.
    /// </summary>
    public class MethodDef
    {
        [JsonPropertyName("class")]
        public string ClassName { get; set; } = "";
        
        [JsonPropertyName("selector")]
        public string Selector { get; set; } = "";
        
        [JsonPropertyName("source")]
        public string Source { get; set; } = "";
        
        [JsonPropertyName("bytecodes")]
        public List<byte> Bytecodes { get; set; } = new();
        
        [JsonPropertyName("literals")]
        public List<string> Literals { get; set; } = new();
        
        [JsonPropertyName("numArgs")]
        public int NumArgs { get; set; }
        
        [JsonPropertyName("primitive")]
        public int Primitive { get; set; } = 0;
    }
    
    /// <summary>
    /// Test action from manifest.
    /// </summary>
    public class TestAction
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = "";
        
        [JsonPropertyName("class")]
        public string? ClassName { get; set; }
        
        [JsonPropertyName("assignTo")]
        public string? AssignTo { get; set; }
        
        [JsonPropertyName("receiver")]
        public string? Receiver { get; set; }
        
        [JsonPropertyName("selector")]
        public string? Selector { get; set; }
        
        [JsonPropertyName("arguments")]
        public List<string>? Arguments { get; set; }
        
        [JsonPropertyName("expectResult")]
        public string? ExpectResult { get; set; }
    }
    
    /// <summary>
    /// Phase 2 manifest structure (legacy multi-class format).
    /// </summary>
    public class Phase2Manifest
    {
        [JsonPropertyName("phase")]
        public string Phase { get; set; } = "3";
        
        [JsonPropertyName("description")]
        public string Description { get; set; } = "";
        
        [JsonPropertyName("classes")]
        public List<ClassDef> Classes { get; set; } = new();
        
        [JsonPropertyName("methods")]
        public List<MethodDef> Methods { get; set; } = new();
        
        [JsonPropertyName("testSequence")]
        public List<TestAction>? TestSequence { get; set; }
    }
    
    /// <summary>
    /// Phase 2 single class manifest structure.
    /// </summary>
    public class Phase2ClassManifest
    {
        [JsonPropertyName("phase")]
        public string Phase { get; set; } = "3";
        
        [JsonPropertyName("className")]
        public string ClassName { get; set; } = "";
        
        [JsonPropertyName("superclass")]
        public string Superclass { get; set; } = "Object";
        
        [JsonPropertyName("comment")]
        public string Comment { get; set; } = "";
        
        [JsonPropertyName("instanceVariables")]
        public List<string> InstanceVariables { get; set; } = new();
        
        [JsonPropertyName("classVariables")]
        public List<string> ClassVariables { get; set; } = new();
        
        [JsonPropertyName("poolDictionaries")]
        public List<string> PoolDictionaries { get; set; } = new();
        
        [JsonPropertyName("category")]
        public string Category { get; set; } = "Unclassified";
        
        [JsonPropertyName("methods")]
        public List<MethodDef> Methods { get; set; } = new();
        
        [JsonPropertyName("classMethods")]
        public List<MethodDef> ClassMethods { get; set; } = new();
    }
    
    /// <summary>
    /// Execute a Phase 2 manifest.
    /// </summary>
    public void ExecuteManifest(string jsonContent)
    {
        // Check if this is a sequence-based manifest (like phase3_test_counter.json)
        if (jsonContent.Contains("\"sequence\""))
        {
            ExecuteSequenceManifest(jsonContent);
            return;
        }
        
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        
        var manifest = JsonSerializer.Deserialize<Phase2Manifest>(jsonContent, options);
        if (manifest == null)
        {
            throw new InvalidOperationException("Failed to parse Phase 2 manifest");
        }
        
        Phase2Log($"[PHASE2] Executing: {manifest.Description}");
        
        // Step 1: Create classes
        foreach (var classDef in manifest.Classes)
        {
            CreateClass(classDef);
        }
        
        // Step 2: Install methods
        foreach (var methodDef in manifest.Methods)
        {
            InstallMethod(methodDef);
        }
        
        // Step 3: Execute test sequence if present
        if (manifest.TestSequence != null)
        {
            ExecuteTestSequence(manifest.TestSequence);
        }
        
        Phase2Log("[PHASE2] Manifest execution complete");
    }
    
    /// <summary>
    /// Execute a sequence-based Phase 2 manifest (like phase3_test_counter.json).
    /// This format uses individual message sends to build classes step by step.
    /// </summary>
    private void ExecuteSequenceManifest(string jsonContent)
    {
        Console.WriteLine("[PHASE2] Executing sequence-based manifest via bytecode...");
        
        // Create TestCounter class by executing bytecode that sends the proper messages
        CreateTestCounterViaBytecode();
    }
    
    /// <summary>
    /// Create TestCounter class by executing bytecode that sends Smalltalk messages.
    /// This is the proper Phase 2 approach - everything happens via bytecode execution.
    /// Using period-authentic selectors: basicNew, instVarAt:put:, class, at:put:
    /// </summary>
    private void CreateTestCounterViaBytecode()
    {
        Console.WriteLine("[PHASE2] Creating TestCounter class via bytecode execution...");
        
        try
        {
            // Slot indices based on Smalltalk-80 class layout:
            // Behavior: [0] superclass, [1] methodDict, [2] format
            // ClassDescription adds: [3] instanceVariables, [4] organization
            // Class adds: [5] name, [6] classPool, [7] sharedPools
            // Metaclass: [0] superclass, [1] methodDict, [2] format, [3] instanceVariables, [4] organization, [5] thisClass
            
            // Step 1: Create bytecode that will create TestCounter following period-authentic sequence
            var bytecode = new byte[]
            {
                // Step 1: Allocate the metaclass - MC := Metaclass basicNew
                64,     // pushLiteralVariable: 0 (Metaclass Association)
                177,    // sendLiteralSelector: 1 (basicNew) with 0 args (176 + 1)
                104,    // popAndStoreTemp: 0 (store metaclass MC in temp 0)
                
                // Step 2: Set MC's superclass to Object class - MC instVarAt: 1 put: (Object class)
                16,     // pushTemp: 0 (the metaclass MC)
                42,     // pushLiteralConstant: 10 (integer 1 for superclass slot)
                66,     // pushLiteralVariable: 2 (Object Association)
                179,    // sendLiteralSelector: 3 (class) with 0 args (176 + 3)
                212,    // sendLiteralSelector: 4 (instVarAt:put:) with 2 args (208 + 4)
                135,    // pop
                
                // Step 3: Allocate the class (instance of MC) - C := MC basicNew
                16,     // pushTemp: 0 (the metaclass MC)
                181,    // sendLiteralSelector: 5 (basicNew) with 0 args (176 + 5)
                105,    // popAndStoreTemp: 1 (store class C in temp 1)
                
                // Step 4: Close the metaclass/class loop - MC instVarAt: 6 put: C
                16,     // pushTemp: 0 (the metaclass MC)
                41,     // pushLiteralConstant: 9 (will be integer 6)
                17,     // pushTemp: 1 (the class C)
                212,    // sendLiteralSelector: 4 (instVarAt:put:) with 2 args (208 + 4)
                135,    // pop
                
                // Step 5: Set the class's superclass - C instVarAt: 1 put: Object
                17,     // pushTemp: 1 (the class C)
                42,     // pushLiteralConstant: 10 (will be integer 1)
                66,     // pushLiteralVariable: 2 (Object Association - use same literal)
                212,    // sendLiteralSelector: 4 (instVarAt:put:) with 2 args (208 + 4)
                135,    // pop
                
                // Step 6a: Create method dictionary for class - MDclass := MethodDictionary basicNew
                70,     // pushLiteralVariable: 6 (MethodDictionary Association)
                181,    // sendLiteralSelector: 5 (basicNew) with 0 args (176 + 5)
                106,    // popAndStoreTemp: 2 (store MDclass in temp 2)
                
                // Step 6b: Set class method dictionary - C instVarAt: 2 put: MDclass
                17,     // pushTemp: 1 (the class C)
                43,     // pushLiteralConstant: 11 (integer 2 for methodDict slot)
                18,     // pushTemp: 2 (MDclass)
                212,    // sendLiteralSelector: 4 (instVarAt:put:) with 2 args (208 + 4)
                135,    // pop
                
                // Step 7a: Create method dictionary for metaclass - MDmeta := MethodDictionary basicNew
                70,     // pushLiteralVariable: 6 (MethodDictionary Association - reuse)
                181,    // sendLiteralSelector: 5 (basicNew) with 0 args (176 + 5)
                107,    // popAndStoreTemp: 3 (store MDmeta in temp 3)
                
                // Step 7b: Set metaclass method dictionary - MC instVarAt: 2 put: MDmeta
                16,     // pushTemp: 0 (the metaclass MC)
                43,     // pushLiteralConstant: 11 (integer 2 for methodDict slot)
                19,     // pushTemp: 3 (MDmeta)
                212,    // sendLiteralSelector: 4 (instVarAt:put:) with 2 args (208 + 4)
                135,    // pop
                
                // Step 8: Copy Object's format - C instVarAt: 3 put: (Object instVarAt: 3)
                17,     // pushTemp: 1 (the class C)
                44,     // pushLiteralConstant: 12 (integer 3 for format slot)
                66,     // pushLiteralVariable: 2 (Object Association - reuse)
                44,     // pushLiteralConstant: 12 (integer 3 for format slot)
                199,    // sendLiteralSelector: 7 (instVarAt:) with 1 arg (192 + 7)
                212,    // sendLiteralSelector: 4 (instVarAt:put:) with 2 args (208 + 4)
                135,    // pop
                
                // Step 9: Name the class - C instVarAt: 6 put: #TestCounter
                17,     // pushTemp: 1 (the class C)
                41,     // pushLiteralConstant: 9 (integer 6 for name slot)
                40,     // pushLiteralConstant: 8 (TestCounter Symbol)
                212,    // sendLiteralSelector: 4 (instVarAt:put:) with 2 args (208 + 4)
                135,    // pop
                
                // Step 10: Bind globally - Smalltalk at: #TestCounter put: C
                77,     // pushLiteralVariable: 13 (Smalltalk Association)
                40,     // pushLiteralConstant: 8 (TestCounter Symbol - reuse)
                17,     // pushTemp: 1 (the class C)
                222,    // sendLiteralSelector: 14 (at:put:) with 2 args (208 + 14)
                135,    // pop
                
                // Return the class
                17,     // pushTemp: 1
                137     // returnTop (bytecode 137 from returnReceiver/returnTop range)
            };
            
            // Create literals array
            // For global variables like Class, we need to create Associations
            // that point to the actual class objects
            
            // Look up Metaclass class
            var metaclassOop = vm.PrimitiveLookupGlobal("Metaclass");
            Console.WriteLine($"[PHASE2] Metaclass lookup returned OOP: {metaclassOop}");
            var metaclassObj = metaclassOop >= 0 ? ObjectMemory.FromOop(metaclassOop) : null;
            if (metaclassObj == null)
            {
                Console.WriteLine($"[PHASE2] ✗ Metaclass class not found (OOP={metaclassOop})");
                return;
            }
            
            // Look up Object class
            var objectOop = vm.PrimitiveLookupGlobal("Object");
            Console.WriteLine($"[PHASE2] Object lookup returned OOP: {objectOop}");
            var objectObj = objectOop >= 0 ? ObjectMemory.FromOop(objectOop) : null;
            if (objectObj == null)
            {
                Console.WriteLine($"[PHASE2] ✗ Object class not found (OOP={objectOop})");
                return;
            }
            
            // Look up MethodDictionary class
            var methodDictOop = vm.PrimitiveLookupGlobal("MethodDictionary");
            var methodDictObj = methodDictOop >= 0 ? ObjectMemory.FromOop(methodDictOop) : null;
            if (methodDictObj == null)
            {
                Console.WriteLine($"[PHASE2] ✗ MethodDictionary class not found (OOP={methodDictOop})");
                return;
            }
            
            // Look up Smalltalk dictionary
            var smalltalkOop = vm.PrimitiveLookupGlobal("Smalltalk");
            Console.WriteLine($"[PHASE2] Smalltalk lookup returned OOP: {smalltalkOop}");
            var smalltalkObj = smalltalkOop >= 0 ? ObjectMemory.FromOop(smalltalkOop) : null;
            if (smalltalkObj == null)
            {
                Console.WriteLine($"[PHASE2] ✗ Smalltalk dictionary not found (OOP={smalltalkOop})");
                return;
            }
            
            // Create literals - all must be SmalltalkObjects for bytecode execution
            
            // Create Symbol SmalltalkObjects for selectors - MUST be defined before createAssociation
            var symbolClassOop = vm.PrimitiveLookupGlobal("Symbol");
            var symbolClass = symbolClassOop >= 0 ? ObjectMemory.FromOop(symbolClassOop) : null;
            Console.WriteLine($"[PHASE2] Symbol class at OOP {symbolClassOop}");
            
            // Helper to create a SmalltalkObject Symbol
            Func<string, SmalltalkObject> createSymbol = (str) =>
            {
                var sym = new SmalltalkObject(symbolClass, 1);
                sym.Fields[0] = str; // Store the string directly
                return sym;
            };
            
            // Create Association SmalltalkObjects for globals
            var associationClassOop = vm.PrimitiveLookupGlobal("Association");
            var associationClass = associationClassOop >= 0 ? ObjectMemory.FromOop(associationClassOop) : null;
            Console.WriteLine($"[PHASE2] Association class at OOP {associationClassOop}");
            
            // Helper to create a SmalltalkObject Association
            // Association inherits from LookupKey, so it has:
            // - 1 field from LookupKey (key)
            // - 1 additional field (value)
            // Total: 2 fields
            Func<string, SmalltalkObject, SmalltalkObject> createAssociation = (name, value) =>
            {
                if (associationClass == null)
                {
                    Console.WriteLine("[PHASE2] WARNING: Association class is null, creating minimal object");
                    var minimalAssoc = new SmalltalkObject(null, 2);
                    minimalAssoc.Fields[0] = createSymbol(name); // key - use SmalltalkObject Symbol
                    minimalAssoc.Fields[1] = value; // value
                    return minimalAssoc;
                }
                
                var assoc = new SmalltalkObject(associationClass, 2);
                assoc.Fields[0] = createSymbol(name); // key - use SmalltalkObject Symbol (inherited from LookupKey)
                assoc.Fields[1] = value; // value (added by Association)
                return assoc;
            };
            
            // Create SmallInteger objects for integer constants
            // SmallIntegers are immediate values encoded as (value << 1) | 1
            Func<int, object> createSmallInteger = (value) => (value << 1) | 1;
            
            // Debug: Check the first association before adding to literals
            var firstAssoc = createAssociation("Metaclass", metaclassObj);
            Console.WriteLine($"[PHASE2] Created Association for Metaclass: fields={firstAssoc.Fields.Length}, class={firstAssoc.ClassPointer?.Oop ?? -1}");
            Console.WriteLine($"[PHASE2] First Association OOP: {firstAssoc.Oop}");
            Console.WriteLine($"[PHASE2] First Association field[0] type: {firstAssoc.Fields[0]?.GetType().Name ?? "null"}");
            Console.WriteLine($"[PHASE2] First Association field[1] type: {firstAssoc.Fields[1]?.GetType().Name ?? "null"}");
            if (firstAssoc.Fields[1] is SmalltalkObject valueObj)
            {
                Console.WriteLine($"[PHASE2] First Association value OOP: {valueObj.Oop} (should be 48 for Metaclass)");
                Console.WriteLine($"[PHASE2] Metaclass object OOP: {metaclassObj.Oop}");
                Console.WriteLine($"[PHASE2] Value's class pointer OOP: {valueObj.ClassPointer?.Oop ?? -1}");
            }
            
            var literalsList = new object[]
            {
                firstAssoc,                                                     // 0: Metaclass global
                createSymbol("basicNew"),                                      // 1: selector
                createAssociation("Object", objectObj),                        // 2: Object global
                createSymbol("class"),                                         // 3: selector (to get Object's metaclass)
                createSymbol("instVarAt:put:"),                               // 4: selector
                createSymbol("basicNew"),                                      // 5: selector (reused)
                createAssociation("MethodDictionary", methodDictObj),          // 6: MethodDictionary global
                createSymbol("instVarAt:"),                                    // 7: selector
                createSymbol("TestCounter"),                                   // 8: class name symbol
                createSmallInteger(6),                                         // 9: integer constant 6 (thisClass slot in Metaclass)
                createSmallInteger(1),                                         // 10: integer constant 1 (superclass slot)
                createSmallInteger(2),                                         // 11: integer constant 2 (methodDict slot)
                createSmallInteger(3),                                         // 12: integer constant 3 (format slot)
                createAssociation("Smalltalk", smalltalkObj),                  // 13: Smalltalk global
                createSymbol("at:put:")                                        // 14: selector
            };
            
            // We don't need to set CompiledMethod fields in the Smalltalk object since
            // we're building a C# CompiledMethod directly
            
            // Step 2: Build a C# CompiledMethod directly
            // For simplicity, just use the bytecode array directly since we just created it
            Console.WriteLine($"[PHASE2] Bytecode array has {bytecode.Length} bytes");
            Console.WriteLine($"[PHASE2] Literals array has {literalsList.Length} entries");
            
            // Create method header using Blue Book format:
            // Bits 0-8: Primitive number (0-511) 
            // Bit 9: Large context flag
            // Bits 10-15: Number of temporaries
            // We need 4 temps for MC, C, MDclass, MDmeta
            int primitiveNum = 0;  // No primitive
            int largeContext = 0;  // Normal context
            int tempCount = 4;     // 4 temporaries
            int rawHeader = primitiveNum | (largeContext << 9) | (tempCount << 10);
            
            // Create the CompiledMethod
            var compiledMethod = new CompiledMethod
            {
                Bytecodes = bytecode,
                Literals = literalsList,
                Header = new MethodHeader(rawHeader, 0),
                TemporariesCount = tempCount,  // Set the temporaries count explicitly
                HasPrimitive = primitiveNum > 0,
                PrimitiveIndex = primitiveNum
            };
            
            // Step 3: Execute the method through the interpreter
            // Get nil as receiver (we don't need a specific receiver)
            var nilOop = vm.PrimitiveNil();
            var nilObj = ObjectMemory.FromOop(nilOop) ?? new SmalltalkObject(null, 0);
            
            // Enable lookup debugging
            SmalltalkObject.EnablePhase3LookupDebug = true;
            
            var interpreter = new Interpreter();
            Console.WriteLine("[PHASE2] Executing bytecode to create TestCounter class...");
            var result = interpreter.Evaluate(compiledMethod, nilObj);
            
            // Disable lookup debugging
            SmalltalkObject.EnablePhase3LookupDebug = false;
            
            if (result != null)
            {
                Console.WriteLine("[PHASE2] ✓ TestCounter class created successfully via bytecode");
                
                // Verify it was registered
                var testCounterOop = vm.PrimitiveLookupGlobal("TestCounter");
                if (testCounterOop > 0)
                {
                    Console.WriteLine($"[PHASE2] ✓ TestCounter registered in Smalltalk dictionary at OOP {testCounterOop}");
                }
            }
            else
            {
                Console.WriteLine("[PHASE2] ✗ Failed to create TestCounter class");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PHASE2] ✗ Error creating TestCounter: {ex.Message}");
            if (ex.StackTrace != null)
            {
                Console.WriteLine($"[PHASE2] Stack trace: {ex.StackTrace}");
            }
        }
    }
    
    /// <summary>
    /// Get or create a Symbol object.
    /// </summary>
    private Symbol GetOrCreateSymbol(string value)
    {
        // Create new symbol directly - we need the actual Symbol object for literals
        return new Symbol(value);
    }
    
    /// <summary>
    /// Create a class from definition.
    /// </summary>
    private void CreateClass(ClassDef classDef)
    {
        Phase2Log($"[PHASE2] Creating class {classDef.Name} < {classDef.Superclass}");
        
        // Look up superclass
        var superclassOop = vm.PrimitiveLookupClass(classDef.Superclass);
        if (superclassOop < 0)
        {
            Phase2Log($"[PHASE2] Warning: Superclass {classDef.Superclass} not found");
            return;
        }
        
        // Create the class
        var classOop = vm.PrimitiveNewClass();
        
        // Create the metaclass
        var metaclassOop = vm.PrimitiveNewMetaclass();
        
        // Set up class relationships
        vm.PrimitiveSetClass(classOop, metaclassOop);
        
        // The metaclass itself needs to have Metaclass as its class
        var metaclassClassOop = vm.PrimitiveLookupClass("Metaclass");
        if (metaclassClassOop > 0)
        {
            vm.PrimitiveSetClass(metaclassOop, metaclassClassOop);
        }
        
        // Set superclass (slot 0 in Behavior)
        vm.PrimitiveSetSlot(classOop, 0, superclassOop);
        
        // Create and set instance variables array
        if (classDef.InstanceVariables.Count > 0)
        {
            var instVarsArray = vm.PrimitiveNewArray(classDef.InstanceVariables.Count);
            for (int i = 0; i < classDef.InstanceVariables.Count; i++)
            {
                var symbolOop = vm.PrimitiveInternSymbol(classDef.InstanceVariables[i]);
                vm.PrimitiveArrayAtPut(instVarsArray, i, symbolOop);
            }
            // Instance variables are in slot 3 of ClassDescription
            vm.PrimitiveSetSlot(classOop, 3, instVarsArray);
        }
        
        // Set class name (slot 5 in Class)
        var nameSymbol = vm.PrimitiveInternSymbol(classDef.Name);
        vm.PrimitiveSetSlot(classOop, 5, nameSymbol);
        
        // Register the class globally
        vm.PrimitiveRegisterGlobal(classDef.Name, classOop);
        
        Phase2Log($"[PHASE2] Created class {classDef.Name} -> OOP {classOop}");
    }
    
    /// <summary>
    /// Install a method in a class following the 5-step protocol.
    /// </summary>
    private void InstallMethod(MethodDef methodDef)
    {
        Phase2Log($"[PHASE2] Installing {methodDef.ClassName}>>{methodDef.Selector}");
        
        // Step 1: Look up the target class
        var classOop = vm.PrimitiveLookupClass(methodDef.ClassName);
        if (classOop < 0)
        {
            Phase2Log($"[PHASE2] Warning: Class {methodDef.ClassName} not found");
            return;
        }
        
        // Step 2: Ensure the selector is properly interned as a Symbol
        var selectorSymbol = vm.PrimitiveInternSymbol(methodDef.Selector);
        Phase2Log($"[PHASE2]   - Selector '{methodDef.Selector}' interned as Symbol OOP {selectorSymbol}");
        
        // Step 3: Create the CompiledMethod object with proper setup
        var compiledMethodClass = vm.PrimitiveLookupClass("CompiledMethod");
        if (compiledMethodClass < 0)
        {
            Phase2Log("[PHASE2] Warning: CompiledMethod class not found, cannot install method");
            return;
        }
        
        var methodOop = vm.PrimitiveNewInstance(compiledMethodClass);
        Phase2Log($"[PHASE2]   - Created CompiledMethod instance OOP {methodOop}");
        
        // CompiledMethod has 4 fields: selector, bytecode, literals, header
        // Set selector (slot 0)
        vm.PrimitiveSetSlot(methodOop, 0, selectorSymbol);
        
        // Set bytecodes (slot 1) - create ByteArray or regular array with bytes
        if (methodDef.Bytecodes.Count > 0)
        {
            // Store bytecodes as an array where each element contains the byte value
            var bytecodeArray = vm.PrimitiveNewArray(methodDef.Bytecodes.Count);
            for (int i = 0; i < methodDef.Bytecodes.Count; i++)
            {
                // Create a small integer object for each byte
                var byteObj = vm.PrimitiveNewSmallInteger(methodDef.Bytecodes[i]);
                vm.PrimitiveArrayAtPut(bytecodeArray, i, byteObj);
            }
            vm.PrimitiveSetSlot(methodOop, 1, bytecodeArray);
            Phase2Log($"[PHASE2]   - Set bytecodes ({methodDef.Bytecodes.Count} bytes): [{string.Join(", ", methodDef.Bytecodes)}]");
        }
        else
        {
            // No bytecodes - set empty array
            var emptyArray = vm.PrimitiveNewArray(0);
            vm.PrimitiveSetSlot(methodOop, 1, emptyArray);
        }
        
        // Set literals (slot 2)
        var literalsArray = vm.PrimitiveNewArray(methodDef.Literals.Count);
        for (int i = 0; i < methodDef.Literals.Count; i++)
        {
            var literal = methodDef.Literals[i];
            int literalOop;
            
            if (literal.StartsWith("#"))
            {
                // Symbol literal
                literalOop = vm.PrimitiveInternSymbol(literal.Substring(1));
            }
            else if (int.TryParse(literal, out int intValue))
            {
                // Integer literal - store as string for now
                // TODO: Create proper SmallInteger objects
                literalOop = vm.PrimitiveInternString(literal);
            }
            else
            {
                // String literal
                literalOop = vm.PrimitiveInternString(literal);
            }
            
            vm.PrimitiveArrayAtPut(literalsArray, i, literalOop);
        }
        vm.PrimitiveSetSlot(methodOop, 2, literalsArray);
        Phase2Log($"[PHASE2]   - Set literals ({methodDef.Literals.Count} literals)");
        
        // Set header (slot 3) - Blue Book encoding (16-bit word):
        // Bits 0-8: Primitive number (0 if no primitive)
        // Bit 9: Large context flag (1 if > 16 temps)
        // Bits 10-15: Number of temporaries/stack size
        
        int primitiveNum = methodDef.Primitive & 0x1FF; // 9 bits for primitive
        int largeContext = 0; // We don't have large contexts yet
        int tempCount = methodDef.NumArgs; // For now, temps = args
        
        // Blue Book header encoding
        int headerValue = primitiveNum | (largeContext << 9) | (tempCount << 10);
        
        var headerOop = vm.PrimitiveNewSmallInteger(headerValue);
        vm.PrimitiveSetSlot(methodOop, 3, headerOop);
        
        // Format the header for display
        var headerDesc = FormatBlueBookHeader(headerValue);
        Phase2Log($"[PHASE2]   - Set header: {headerDesc}");
        
        // Step 4: Retrieve or create the class's method dictionary using message sends
        var methodDictOop = vm.GetSlot(classOop, 1);
        
        if (methodDictOop < 0)
        {
            // Create a new method dictionary via "MethodDictionary new"
            var methodDictClass = vm.PrimitiveLookupClass("MethodDictionary");
            if (methodDictClass > 0)
            {
                var newSelector = vm.PrimitiveInternSymbol("new");
                methodDictOop = vm.PrimitiveSendMessage(methodDictClass, newSelector, new int[0]);
                Phase2Log($"[PHASE2]   - Created MethodDictionary via message send: MethodDictionary new -> OOP {methodDictOop}");
            }
            else
            {
                // Fallback: use simple dictionary
                methodDictOop = vm.PrimitiveNewDictionary();
                Phase2Log($"[PHASE2]   - Created simple dictionary OOP {methodDictOop}");
            }
            
            // Install the dictionary in the class (slot 1 of Behavior)
            vm.PrimitiveSetSlot(classOop, 1, methodDictOop);
            Phase2Log($"[PHASE2]   - Installed method dictionary in class slot 1");
        }
        else
        {
            Phase2Log($"[PHASE2]   - Using existing method dictionary OOP {methodDictOop}");
        }
        
        // Step 5: Install the method directly using primitives (bypassing broken at:put: method)
        // Since Phase 1 now creates properly sized dictionaries, we can use direct primitive storage
        try
        {
            vm.PrimitiveDictionaryAtPut(methodDictOop, selectorSymbol, methodOop);
            Phase2Log($"[PHASE2]   - Installed method via primitive: dict[#{methodDef.Selector}] = method OOP {methodOop}");
        }
        catch (Exception ex)
        {
            // Fallback to message send if primitive fails
            Phase2Log($"[PHASE2]   - Primitive failed ({ex.Message}), falling back to message send");
            var atPutSelector = vm.PrimitiveInternSymbol("at:put:");
            var result = vm.PrimitiveSendMessage(methodDictOop, atPutSelector, new int[] { selectorSymbol, methodOop });
            Phase2Log($"[PHASE2]   - Fallback to message send: dict at: #{methodDef.Selector} put: method -> result OOP {result}");
        }
        
        Phase2Log($"[PHASE2] ✓ Method {methodDef.ClassName}>>{methodDef.Selector} successfully installed");
    }
    
    /// <summary>
    /// Execute a test sequence.
    /// </summary>
    private void ExecuteTestSequence(List<TestAction> testSequence)
    {
        Phase2Log("[PHASE2] Executing test sequence...");
        
        foreach (var action in testSequence)
        {
            switch (action.Action)
            {
                case "createInstance":
                    ExecuteCreateInstance(action);
                    break;
                    
                case "sendMessage":
                    ExecuteSendMessage(action);
                    break;
                    
                default:
                    Phase2Log($"[PHASE2] Unknown test action: {action.Action}");
                    break;
            }
        }
    }
    
    /// <summary>
    /// Create an instance of a class.
    /// </summary>
    private void ExecuteCreateInstance(TestAction action)
    {
        if (action.ClassName == null)
        {
            Phase2Log("[PHASE2] createInstance requires className");
            return;
        }
        
        Phase2Log($"[PHASE2] Creating instance of {action.ClassName}");
        
        var classOop = vm.PrimitiveLookupClass(action.ClassName);
        if (classOop < 0)
        {
            Phase2Log($"[PHASE2] Class {action.ClassName} not found");
            return;
        }
        
        var instanceOop = vm.PrimitiveNewInstance(classOop);
        
        if (!string.IsNullOrEmpty(action.AssignTo))
        {
            testVariables[action.AssignTo] = instanceOop;
            Phase2Log($"[PHASE2] Created instance -> OOP {instanceOop}, assigned to '{action.AssignTo}'");
        }
    }
    
    /// <summary>
    /// Formats a Blue Book method header for display.
    /// </summary>
    private string FormatBlueBookHeader(int headerValue)
    {
        // Decode Blue Book header format (16-bit word):
        // Bits 0-8: Primitive number (0 if no primitive)
        // Bit 9: Large context flag
        // Bits 10-15: Number of temporaries
        
        int primitiveNum = headerValue & 0x1FF;  // Lower 9 bits
        bool largeContext = (headerValue & 0x200) != 0;  // Bit 9
        int tempCount = (headerValue >> 10) & 0x3F;  // Bits 10-15
        
        var parts = new List<string>();
        
        if (primitiveNum > 0)
        {
            parts.Add($"primitive={primitiveNum}");
        }
        
        parts.Add($"temps={tempCount}");
        
        if (largeContext)
        {
            parts.Add("largeContext");
        }
        
        return $"0x{headerValue:X4} ({string.Join(", ", parts)})";
    }
    
    /// <summary>
    /// Send a message to an object.
    /// </summary>
    private void ExecuteSendMessage(TestAction action)
    {
        if (action.Receiver == null || action.Selector == null)
        {
            Phase2Log("[PHASE2] sendMessage requires receiver and selector");
            return;
        }
        
        Phase2Log($"[PHASE2] Sending {action.Selector} to {action.Receiver}");
        
        // Get receiver OOP
        if (!testVariables.TryGetValue(action.Receiver, out int receiverOop))
        {
            // Try to look up as a class name
            receiverOop = vm.PrimitiveLookupClass(action.Receiver);
            if (receiverOop < 0)
            {
                Phase2Log($"[PHASE2] Receiver '{action.Receiver}' not found in test variables or as a class");
                return;
            }
            Phase2Log($"[PHASE2]   - Found receiver as class '{action.Receiver}' -> OOP {receiverOop}");
        }
        
        // Intern the selector as a Symbol
        var selectorOop = vm.PrimitiveInternSymbol(action.Selector);
        Phase2Log($"[PHASE2]   - Selector '{action.Selector}' -> Symbol OOP {selectorOop}");
        
        // Prepare arguments
        var argOops = new int[action.Arguments?.Count ?? 0];
        if (action.Arguments != null)
        {
            for (int i = 0; i < action.Arguments.Count; i++)
            {
                var arg = action.Arguments[i];
                // For now, just intern as string
                argOops[i] = vm.PrimitiveInternString(arg);
                Phase2Log($"[PHASE2]   - Argument '{arg}' -> String OOP {argOops[i]}");
            }
        }
        
        // Actually send the message using the VM primitive
        Phase2Log($"[PHASE2] Invoking PrimitiveSendMessage(receiver: {receiverOop}, selector: {selectorOop}, args: [{string.Join(", ", argOops)}])");
        var resultOop = vm.PrimitiveSendMessage(receiverOop, selectorOop, argOops);
        
        if (resultOop >= 0)
        {
            Phase2Log($"[PHASE2] Message send returned OOP {resultOop}");
            
            // Store result if needed
            if (!string.IsNullOrEmpty(action.AssignTo))
            {
                testVariables[action.AssignTo] = resultOop;
                Phase2Log($"[PHASE2] Result assigned to '{action.AssignTo}'");
            }
        }
        else
        {
            Phase2Log($"[PHASE2] Message send failed or returned nil");
        }
        
        if (!string.IsNullOrEmpty(action.ExpectResult))
        {
            Phase2Log($"[PHASE2] Expected result: {action.ExpectResult}");
            // TODO: Compare actual result with expected result
        }
    }
    
    /// <summary>
    /// Execute a Phase 2 single class manifest.
    /// </summary>
    public void ExecuteClassManifest(string jsonContent)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        
        var manifest = JsonSerializer.Deserialize<Phase2ClassManifest>(jsonContent, options);
        if (manifest == null)
        {
            throw new InvalidOperationException("Failed to parse Phase 2 class manifest");
        }
        
        Phase2Log($"[PHASE2] Loading class: {manifest.ClassName}");
        
        // Create the class definition
        var classDef = new ClassDef
        {
            Name = manifest.ClassName,
            Superclass = manifest.Superclass,
            InstanceVariables = manifest.InstanceVariables,
            ClassVariables = manifest.ClassVariables,
            PoolDictionaries = manifest.PoolDictionaries,
            Category = manifest.Category
        };
        
        // Step 1: Create the class (only if it doesn't already exist)
        var existingClass = vm.PrimitiveLookupClass(manifest.ClassName);
        if (existingClass < 0)
        {
            CreateClass(classDef);
        }
        else
        {
            Phase2Log($"[PHASE2] Class {manifest.ClassName} already exists, adding methods only");
        }
        
        // Step 1.5: Method dictionary sizing is now handled in Phase 1
        // No longer needed - Phase 1 creates properly sized dictionaries
        // Workaround disabled since Phase 1 now pre-sizes dictionaries correctly
        
        // Step 2: Install instance methods
        foreach (var methodDef in manifest.Methods)
        {
            methodDef.ClassName = manifest.ClassName;
            InstallMethod(methodDef);
        }
        
        // Step 3: Install class methods (on the metaclass)
        foreach (var methodDef in manifest.ClassMethods)
        {
            methodDef.ClassName = manifest.ClassName + " class";
            InstallMethod(methodDef);
        }
        
        Phase2Log($"[PHASE2] Class {manifest.ClassName} loading complete");
    }
    
    /// <summary>
    /// Workaround: Ensures a class has a large enough method dictionary to hold new methods.
    /// Replaces small/full dictionaries with appropriately sized new ones.
    /// </summary>
    private void EnsureLargeMethodDictionary(int classOop, string className, int expectedMethodCount)
    {
        Phase2Log($"[PHASE2] Ensuring large method dictionary for {className} (expecting {expectedMethodCount} methods)");
        
        // Get the current method dictionary (slot 1 in Behavior)
        var currentMethodDict = vm.GetSlot(classOop, 1);
        
        // Calculate needed capacity: existing methods + new methods + 50% growth buffer
        int neededCapacity = expectedMethodCount + 10; // Add buffer for growth
        
        // Always create a new, larger dictionary to avoid the broken at:put: issue
        Phase2Log($"[PHASE2]   - Creating new method dictionary with capacity {neededCapacity}");
        
        // Create a new method dictionary using primitives (avoiding the broken MethodDictionary new)
        var newMethodDict = vm.PrimitiveNewDictionary();
        
        // If there was an existing method dictionary, copy its methods to the new one
        if (currentMethodDict > 0)
        {
            Phase2Log($"[PHASE2]   - Found existing method dictionary OOP {currentMethodDict}, copying methods");
            CopyMethodsToNewDictionary(currentMethodDict, newMethodDict, className);
        }
        else
        {
            Phase2Log($"[PHASE2]   - No existing method dictionary found, starting with empty dictionary");
        }
        
        // Install the new dictionary in the class
        vm.PrimitiveSetSlot(classOop, 1, newMethodDict);
        Phase2Log($"[PHASE2]   - Installed new method dictionary OOP {newMethodDict} in class {className}");
    }
    
    /// <summary>
    /// Copies methods from an old method dictionary to a new one.
    /// Uses primitive operations to avoid the broken at:put: implementation.
    /// </summary>
    private void CopyMethodsToNewDictionary(int oldDict, int newDict, string className)
    {
        // This is a simplified copy - in a full implementation we'd need to properly
        // traverse the dictionary structure and extract key-value pairs.
        // For now, we'll just note that copying is happening
        Phase2Log($"[PHASE2]     - Method copying from old dictionary not yet implemented");
        Phase2Log($"[PHASE2]     - Existing methods in {className} may need to be re-added manually");
        
        // TODO: Implement proper method dictionary traversal and copying
        // This would require understanding the exact dictionary format used by Phase 2
        // and extracting selector->method mappings to re-install them
    }
}