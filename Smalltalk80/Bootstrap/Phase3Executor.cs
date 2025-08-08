using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;

namespace Smalltalk80.Bootstrap;

/// <summary>
/// Executes Phase 3 manifests by sending messages through the minimal Smalltalk system.
/// </summary>
public class Phase3Executor
{
    private readonly ISmalltalkVM vm;
    private readonly Dictionary<string, int> testVariables = new();
    
    /// <summary>
    /// Controls whether Phase 3 debug output is displayed.
    /// </summary>
    public static bool EnablePhase3DebugOutput { get; set; } = false;
    
    /// <summary>
    /// Logs a Phase 3 message only if debug output is enabled.
    /// </summary>
    public static void Phase3Log(string message)
    {
        if (EnablePhase3DebugOutput)
        {
            Console.WriteLine(message);
        }
    }
    
    public Phase3Executor(ISmalltalkVM vm)
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
    /// Phase 3 manifest structure.
    /// </summary>
    public class Phase3Manifest
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
    /// Execute a Phase 3 manifest.
    /// </summary>
    public void ExecuteManifest(string jsonContent)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        
        var manifest = JsonSerializer.Deserialize<Phase3Manifest>(jsonContent, options);
        if (manifest == null)
        {
            throw new InvalidOperationException("Failed to parse Phase 3 manifest");
        }
        
        Phase3Log($"[PHASE3] Executing: {manifest.Description}");
        
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
        
        Phase3Log("[PHASE3] Manifest execution complete");
    }
    
    /// <summary>
    /// Create a class from definition.
    /// </summary>
    private void CreateClass(ClassDef classDef)
    {
        Phase3Log($"[PHASE3] Creating class {classDef.Name} < {classDef.Superclass}");
        
        // Look up superclass
        var superclassOop = vm.PrimitiveLookupClass(classDef.Superclass);
        if (superclassOop < 0)
        {
            Phase3Log($"[PHASE3] Warning: Superclass {classDef.Superclass} not found");
            return;
        }
        
        // Create the class
        var classOop = vm.PrimitiveNewClass();
        
        // Create the metaclass
        var metaclassOop = vm.PrimitiveNewMetaclass();
        
        // Set up class relationships
        vm.PrimitiveSetClass(classOop, metaclassOop);
        
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
        
        Phase3Log($"[PHASE3] Created class {classDef.Name} -> OOP {classOop}");
    }
    
    /// <summary>
    /// Install a method in a class following the 5-step protocol.
    /// </summary>
    private void InstallMethod(MethodDef methodDef)
    {
        Phase3Log($"[PHASE3] Installing {methodDef.ClassName}>>{methodDef.Selector}");
        
        // Step 1: Look up the target class
        var classOop = vm.PrimitiveLookupClass(methodDef.ClassName);
        if (classOop < 0)
        {
            Phase3Log($"[PHASE3] Warning: Class {methodDef.ClassName} not found");
            return;
        }
        
        // Step 2: Ensure the selector is properly interned as a Symbol
        var selectorSymbol = vm.PrimitiveInternSymbol(methodDef.Selector);
        Phase3Log($"[PHASE3]   - Selector '{methodDef.Selector}' interned as Symbol OOP {selectorSymbol}");
        
        // Step 3: Create the CompiledMethod object with proper setup
        var compiledMethodClass = vm.PrimitiveLookupClass("CompiledMethod");
        if (compiledMethodClass < 0)
        {
            Phase3Log("[PHASE3] Warning: CompiledMethod class not found, cannot install method");
            return;
        }
        
        var methodOop = vm.PrimitiveNewInstance(compiledMethodClass);
        Phase3Log($"[PHASE3]   - Created CompiledMethod instance OOP {methodOop}");
        
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
            Phase3Log($"[PHASE3]   - Set bytecodes ({methodDef.Bytecodes.Count} bytes): [{string.Join(", ", methodDef.Bytecodes)}]");
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
        Phase3Log($"[PHASE3]   - Set literals ({methodDef.Literals.Count} literals)");
        
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
        Phase3Log($"[PHASE3]   - Set header: {headerDesc}");
        
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
                Phase3Log($"[PHASE3]   - Created MethodDictionary via message send: MethodDictionary new -> OOP {methodDictOop}");
            }
            else
            {
                // Fallback: use simple dictionary
                methodDictOop = vm.PrimitiveNewDictionary();
                Phase3Log($"[PHASE3]   - Created simple dictionary OOP {methodDictOop}");
            }
            
            // Install the dictionary in the class (slot 1 of Behavior)
            vm.PrimitiveSetSlot(classOop, 1, methodDictOop);
            Phase3Log($"[PHASE3]   - Installed method dictionary in class slot 1");
        }
        else
        {
            Phase3Log($"[PHASE3]   - Using existing method dictionary OOP {methodDictOop}");
        }
        
        // Step 5: Install the method via "dict at: selector put: method" message send
        var atPutSelector = vm.PrimitiveInternSymbol("at:put:");
        var result = vm.PrimitiveSendMessage(methodDictOop, atPutSelector, new int[] { selectorSymbol, methodOop });
        Phase3Log($"[PHASE3]   - Installed method via message send: dict at: #{methodDef.Selector} put: method -> result OOP {result}");
        
        // The dictionary will automatically expand if needed via its inherited fullCheck mechanism
        
        Phase3Log($"[PHASE3] ✓ Method {methodDef.ClassName}>>{methodDef.Selector} successfully installed");
    }
    
    /// <summary>
    /// Execute a test sequence.
    /// </summary>
    private void ExecuteTestSequence(List<TestAction> testSequence)
    {
        Phase3Log("[PHASE3] Executing test sequence...");
        
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
                    Phase3Log($"[PHASE3] Unknown test action: {action.Action}");
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
            Phase3Log("[PHASE3] createInstance requires className");
            return;
        }
        
        Phase3Log($"[PHASE3] Creating instance of {action.ClassName}");
        
        var classOop = vm.PrimitiveLookupClass(action.ClassName);
        if (classOop < 0)
        {
            Phase3Log($"[PHASE3] Class {action.ClassName} not found");
            return;
        }
        
        var instanceOop = vm.PrimitiveNewInstance(classOop);
        
        if (!string.IsNullOrEmpty(action.AssignTo))
        {
            testVariables[action.AssignTo] = instanceOop;
            Phase3Log($"[PHASE3] Created instance -> OOP {instanceOop}, assigned to '{action.AssignTo}'");
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
            Phase3Log("[PHASE3] sendMessage requires receiver and selector");
            return;
        }
        
        Phase3Log($"[PHASE3] Sending {action.Selector} to {action.Receiver}");
        
        // Get receiver OOP
        if (!testVariables.TryGetValue(action.Receiver, out int receiverOop))
        {
            // Try to look up as a class name
            receiverOop = vm.PrimitiveLookupClass(action.Receiver);
            if (receiverOop < 0)
            {
                Phase3Log($"[PHASE3] Receiver '{action.Receiver}' not found in test variables or as a class");
                return;
            }
            Phase3Log($"[PHASE3]   - Found receiver as class '{action.Receiver}' -> OOP {receiverOop}");
        }
        
        // Intern the selector as a Symbol
        var selectorOop = vm.PrimitiveInternSymbol(action.Selector);
        Phase3Log($"[PHASE3]   - Selector '{action.Selector}' -> Symbol OOP {selectorOop}");
        
        // Prepare arguments
        var argOops = new int[action.Arguments?.Count ?? 0];
        if (action.Arguments != null)
        {
            for (int i = 0; i < action.Arguments.Count; i++)
            {
                var arg = action.Arguments[i];
                // For now, just intern as string
                argOops[i] = vm.PrimitiveInternString(arg);
                Phase3Log($"[PHASE3]   - Argument '{arg}' -> String OOP {argOops[i]}");
            }
        }
        
        // Actually send the message using the VM primitive
        Phase3Log($"[PHASE3] Invoking PrimitiveSendMessage(receiver: {receiverOop}, selector: {selectorOop}, args: [{string.Join(", ", argOops)}])");
        var resultOop = vm.PrimitiveSendMessage(receiverOop, selectorOop, argOops);
        
        if (resultOop >= 0)
        {
            Phase3Log($"[PHASE3] Message send returned OOP {resultOop}");
            
            // Store result if needed
            if (!string.IsNullOrEmpty(action.AssignTo))
            {
                testVariables[action.AssignTo] = resultOop;
                Phase3Log($"[PHASE3] Result assigned to '{action.AssignTo}'");
            }
        }
        else
        {
            Phase3Log($"[PHASE3] Message send failed or returned nil");
        }
        
        if (!string.IsNullOrEmpty(action.ExpectResult))
        {
            Phase3Log($"[PHASE3] Expected result: {action.ExpectResult}");
            // TODO: Compare actual result with expected result
        }
    }
}