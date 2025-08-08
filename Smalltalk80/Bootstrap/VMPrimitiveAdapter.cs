using System;
using System.Collections.Generic;
using System.Linq;

namespace Smalltalk80.Bootstrap;

/// <summary>
/// Adapter that implements ISmalltalkVM by delegating to VirtualMachine and ObjectMemory.
/// Provides primitive operations for bootstrap to create real Smalltalk objects.
/// </summary>
public class VMPrimitiveAdapter : ISmalltalkVM
{
    private readonly VirtualMachine vm;
    private readonly Dictionary<string, int> globals = new();
    private readonly Dictionary<string, int> classes = new();
    private readonly Dictionary<string, int> internedSymbols = new();
    private int? nilOop = null;
    
    public VMPrimitiveAdapter(VirtualMachine vm)
    {
        this.vm = vm;
    }
    
    /// <summary>
    /// Create a new class object (8 slots for full class).
    /// </summary>
    public int PrimitiveNewClass()
    {
        // Classes have 8 slots:
        // [0] superclass, [1] methodDict, [2] format
        // [3] instanceVariables, [4] organization, [5] name
        // [6] classPool, [7] sharedPools
        var classObj = new SmalltalkObject(null, 8);
        return classObj.Oop;
    }
    
    /// <summary>
    /// Create a new metaclass object (6 slots).
    /// </summary>
    public int PrimitiveNewMetaclass()
    {
        // Metaclasses have 6 slots:
        // [0] superclass, [1] methodDict, [2] format
        // [3] instanceVariables, [4] organization, [5] thisClass
        var metaclassObj = new SmalltalkObject(null, 6);
        return metaclassObj.Oop;
    }
    
    /// <summary>
    /// Create a new instance of a class.
    /// </summary>
    public int PrimitiveNewInstance(int classId)
    {
        var classObj = ObjectMemory.FromOop(classId);
        if (classObj == null)
            throw new InvalidOperationException($"Class with OOP {classId} not found");
            
        // Determine instance size from the class's instance variables
        // Look at slot 3 (instanceVariables) to count fields
        int instanceSize = 0;
        var instVarsField = classObj.GetField(3);
        if (instVarsField is SmalltalkObject instVarsArray)
        {
            instanceSize = instVarsArray.Fields.Length;
        }
        
        // For CompiledMethod, we know it has 4 fields: selector, bytecode, literals, header
        // This is a simplified approach - a full implementation would calculate from class hierarchy
        if (classes.ContainsValue(classId))
        {
            var className = classes.FirstOrDefault(x => x.Value == classId).Key;
            if (className == "CompiledMethod")
            {
                instanceSize = 4; // selector, bytecode, literals, header
            }
            else if (className == "MethodContext")
            {
                instanceSize = 6; // sender, receiver, method, arguments, pc, stackPointer
            }
            else if (className == "Message")
            {
                instanceSize = 2; // selector, arguments
            }
            else if (className == "MethodDictionary")
            {
                // Start with minimal size to test capacity limits
                instanceSize = 2; // Keys and values arrays only
                Console.WriteLine($"[VM] Creating MethodDictionary with size {instanceSize} to test capacity");
            }
        }
        
        var instance = new SmalltalkObject(classObj, instanceSize);
        return instance.Oop;
    }
    
    /// <summary>
    /// Get slot value at a specific index.
    /// </summary>
    public int GetSlot(int objectId, int slotIndex)
    {
        var obj = ObjectMemory.FromOop(objectId);
        if (obj == null)
            return -1;
            
        if (slotIndex < 0 || slotIndex >= obj.Fields.Length)
            return -1;
            
        var value = obj.Fields[slotIndex];
        if (value is SmalltalkObject smalltalkObj)
            return smalltalkObj.Oop;
            
        return -1;
    }
    
    /// <summary>
    /// Set slot value at a specific index.
    /// </summary>
    public void PrimitiveSetSlot(int objectId, int slotIndex, int valueId)
    {
        var obj = ObjectMemory.FromOop(objectId);
        if (obj == null)
            throw new InvalidOperationException($"Object with OOP {objectId} not found");
            
        if (slotIndex < 0 || slotIndex >= obj.Fields.Length)
            throw new ArgumentOutOfRangeException(nameof(slotIndex), $"Slot index {slotIndex} out of range for object with {obj.Fields.Length} slots");
            
        // OOP 0 is a valid object (ProtoObject), so we need to handle it
        // Only use null if we explicitly want to set nil (which would be represented differently)
        var value = ObjectMemory.FromOop(valueId);
        obj.SetField(slotIndex, value);
    }
    
    /// <summary>
    /// Set the class pointer of an object.
    /// </summary>
    public void PrimitiveSetClass(int objectId, int classId)
    {
        var obj = ObjectMemory.FromOop(objectId);
        if (obj == null)
            throw new InvalidOperationException($"Object with OOP {objectId} not found");
            
        var classObj = ObjectMemory.FromOop(classId);
        obj.ClassPointer = classObj;
        
        // Debug: verify the class was set
        if (obj.ClassPointer != classObj)
        {
            Console.WriteLine($"[WARNING] Failed to set class pointer for OOP {objectId} to OOP {classId}");
        }
    }
    
    /// <summary>
    /// Intern a symbol and return its OOP.
    /// </summary>
    public int PrimitiveInternSymbol(string name)
    {
        // Check if this symbol was already interned
        if (internedSymbols.TryGetValue(name, out var existingOop))
        {
            Console.WriteLine($"[VM] Symbol '{name}' already interned as OOP {existingOop}");
            return existingOop;
        }
        
        // Create a new symbol
        // Get the Symbol class if it exists
        var symbolClassOop = PrimitiveLookupClass("Symbol");
        SmalltalkObject? symbolClass = null;
        if (symbolClassOop < 0)
        {
            // Symbol class doesn't exist yet, try the metaclass
            symbolClassOop = PrimitiveLookupClass("SymbolClass");
        }
        if (symbolClassOop > 0)
        {
            symbolClass = ObjectMemory.FromOop(symbolClassOop);
        }
        
        var symbol = new SmalltalkObject(symbolClass, 1);
        symbol.SetField(0, name);
        
        // Store in the interned symbols table
        internedSymbols[name] = symbol.Oop;
        Console.WriteLine($"[VM] Interned new symbol '{name}' as OOP {symbol.Oop}");
        
        return symbol.Oop;
    }
    
    /// <summary>
    /// Create a SmallInteger object.
    /// </summary>
    public int PrimitiveNewSmallInteger(int value)
    {
        // For now, create a simple object with the integer value
        // In a full implementation, SmallIntegers would be encoded directly
        var intObj = new SmalltalkObject(null, 1);
        intObj.SetField(0, value);
        return intObj.Oop;
    }
    
    /// <summary>
    /// Create a new array.
    /// </summary>
    public int PrimitiveNewArray(int size)
    {
        // Get the Array class if it exists
        var arrayClassOop = PrimitiveLookupClass("Array");
        SmalltalkObject? arrayClass = arrayClassOop > 0 ? ObjectMemory.FromOop(arrayClassOop) : null;
        
        var array = new SmalltalkObject(arrayClass, size);
        return array.Oop;
    }
    
    /// <summary>
    /// Set array element at index.
    /// </summary>
    public void PrimitiveArrayAtPut(int arrayId, int index, int valueId)
    {
        PrimitiveSetSlot(arrayId, index, valueId);
    }
    
    /// <summary>
    /// Create a new dictionary.
    /// </summary>
    public int PrimitiveNewDictionary()
    {
        // Get the MethodDictionary or Dictionary class if it exists
        var dictClassOop = PrimitiveLookupClass("MethodDictionary");
        if (dictClassOop < 0)
        {
            dictClassOop = PrimitiveLookupClass("Dictionary");
        }
        
        SmalltalkObject? dictClass = dictClassOop > 0 ? ObjectMemory.FromOop(dictClassOop) : null;
        
        // A dictionary needs at least 2 slots for keys and values arrays
        // We'll allocate a simple structure with parallel arrays
        // Increased size to 64 slots (32 key-value pairs)
        var dict = new SmalltalkObject(dictClass, 64);
        
        // Initialize with empty state - all slots null
        for (int i = 0; i < dict.Fields.Length; i++)
        {
            dict.SetField(i, null);
        }
        
        return dict.Oop;
    }
    
    /// <summary>
    /// Add a key-value pair to a dictionary.
    /// </summary>
    public void PrimitiveDictionaryAtPut(int dictId, int keyId, int valueId)
    {
        var dict = ObjectMemory.FromOop(dictId);
        if (dict == null)
            throw new InvalidOperationException($"Dictionary with OOP {dictId} not found");
            
        var key = ObjectMemory.FromOop(keyId);
        var value = ObjectMemory.FromOop(valueId);
        
        if (key == null)
            throw new InvalidOperationException($"Key with OOP {keyId} not found");
        if (value == null)
            throw new InvalidOperationException($"Value with OOP {valueId} not found");
            
        // Find an empty slot or replace existing key
        // Store key-value pairs in adjacent slots (even index = key, odd index = value)
        bool installed = false;
        
        for (int i = 0; i < dict.Fields.Length; i += 2)
        {
            var existingKey = dict.Fields[i];
            
            // Check if this slot is empty
            if (existingKey == null)
            {
                dict.SetField(i, key);
                dict.SetField(i + 1, value);
                installed = true;
                Console.WriteLine($"[VM] Dictionary at:put: installed new entry at slot {i}: key OOP {keyId} -> value OOP {valueId}");
                break;
            }
            
            // Check if this key already exists (need to compare OOPs)
            if (existingKey is SmalltalkObject existingKeyObj && existingKeyObj.Oop == keyId)
            {
                // Replace existing value
                dict.SetField(i + 1, value);
                installed = true;
                Console.WriteLine($"[VM] Dictionary at:put: replaced existing entry at slot {i}: key OOP {keyId} -> value OOP {valueId}");
                break;
            }
        }
        
        if (!installed)
        {
            Console.WriteLine($"[VM] Warning: Dictionary full, could not install key OOP {keyId}");
        }
    }
    
    /// <summary>
    /// Look up a value in a dictionary by key.
    /// </summary>
    public int PrimitiveDictionaryAt(int dictId, int keyId)
    {
        var dict = ObjectMemory.FromOop(dictId);
        if (dict == null)
        {
            Console.WriteLine($"[VM] DictionaryAt: Dictionary OOP {dictId} not found");
            return -1;
        }
        
        // Check if this is a proper MethodDictionary with keys/values arrays (fields 0 and 1)
        // or a flat format dictionary
        if (dict.Fields.Length >= 2 && 
            dict.Fields[0] is SmalltalkObject keysArray &&
            dict.Fields[1] is SmalltalkObject valuesArray)
        {
            // This is a proper MethodDictionary with separate keys and values arrays
            Console.WriteLine($"[VM] DictionaryAt: Using MethodDictionary format with keys array OOP {keysArray.Oop} and values array OOP {valuesArray.Oop}");
            
            // Search through the keys array
            for (int i = 0; i < keysArray.Fields.Length; i++)
            {
                var existingKey = keysArray.Fields[i];
                
                if (existingKey == null)
                    continue; // Empty slot
                    
                // Check if this key matches (compare OOPs)
                if (existingKey is SmalltalkObject existingKeyObj)
                {
                    Console.WriteLine($"[VM] DictionaryAt: Checking key at index {i}: OOP {existingKeyObj.Oop} vs looking for {keyId}");
                    
                    if (existingKeyObj.Oop == keyId)
                    {
                        // Found the key, get corresponding value from values array
                        if (i < valuesArray.Fields.Length && valuesArray.Fields[i] is SmalltalkObject valueObj)
                        {
                            Console.WriteLine($"[VM] DictionaryAt: Found value OOP {valueObj.Oop} for key OOP {keyId}");
                            return valueObj.Oop;
                        }
                        Console.WriteLine($"[VM] DictionaryAt: Value at index {i} not found or not a SmalltalkObject");
                        return -1;
                    }
                }
            }
        }
        else
        {
            // This is a flat format dictionary (legacy)
            Console.WriteLine($"[VM] DictionaryAt: Using flat format dictionary");
            
            // Search for the key in the dictionary (flat format: key, value, key, value...)
            for (int i = 0; i < dict.Fields.Length; i += 2)
            {
                var existingKey = dict.Fields[i];
                
                if (existingKey == null)
                    continue; // Empty slot
                    
                // Check if this key matches (compare OOPs)
                if (existingKey is SmalltalkObject existingKeyObj)
                {
                    Console.WriteLine($"[VM] DictionaryAt: Checking key at slot {i}: OOP {existingKeyObj.Oop} vs looking for {keyId}");
                    
                    if (existingKeyObj.Oop == keyId)
                    {
                        // Found the key, return the value
                        var value = dict.Fields[i + 1];
                        if (value is SmalltalkObject valueObj)
                        {
                            Console.WriteLine($"[VM] DictionaryAt: Found value OOP {valueObj.Oop} for key OOP {keyId}");
                            return valueObj.Oop;
                        }
                        Console.WriteLine($"[VM] DictionaryAt: Value at slot {i+1} is not a SmalltalkObject");
                        return -1;
                    }
                }
            }
        }
        
        // Key not found
        Console.WriteLine($"[VM] DictionaryAt: Key OOP {keyId} not found in dictionary");
        return -1;
    }
    
    /// <summary>
    /// Compile and install a method in a class.
    /// </summary>
    public void PrimitiveInstallMethod(int classId, string selector, string sourceCode)
    {
        // For bootstrap, we'll skip actual compilation
        // Methods will be compiled later when the compiler is available
        Console.WriteLine($"[BOOTSTRAP] Method {selector} queued for installation in class {classId}");
    }
    
    /// <summary>
    /// Get the nil object OOP.
    /// </summary>
    public int PrimitiveNil()
    {
        // Create nil object if it doesn't exist yet
        if (nilOop == null)
        {
            // nil is a special singleton instance with no fields
            var nil = new SmalltalkObject(null, 0);
            nilOop = nil.Oop;
        }
        return nilOop.Value;
    }
    
    /// <summary>
    /// Register a global variable.
    /// </summary>
    public void PrimitiveRegisterGlobal(string name, int valueId)
    {
        globals[name] = valueId;
        
        // Also track classes separately for easy lookup
        if (name.Length > 0 && char.IsUpper(name[0]))
        {
            classes[name] = valueId;
        }
        
        Console.WriteLine($"[BOOTSTRAP] Registered global '{name}' -> OOP {valueId}");
    }
    
    /// <summary>
    /// Get a registered class by name.
    /// </summary>
    public int PrimitiveLookupClass(string className)
    {
        return classes.TryGetValue(className, out var oop) ? oop : -1;
    }
    
    /// <summary>
    /// Look up a global variable by name.
    /// </summary>
    public int PrimitiveLookupGlobal(string name)
    {
        return globals.TryGetValue(name, out var oop) ? oop : -1;
    }
    
    /// <summary>
    /// Intern a string and return its OOP.
    /// </summary>
    public int PrimitiveInternString(string value)
    {
        // Create a string object
        // Get the String class if it exists
        var stringClassOop = PrimitiveLookupClass("String");
        SmalltalkObject? stringClass = null;
        if (stringClassOop < 0)
        {
            // String class doesn't exist yet, try the metaclass
            stringClassOop = PrimitiveLookupClass("StringClass");
        }
        if (stringClassOop > 0)
        {
            stringClass = ObjectMemory.FromOop(stringClassOop);
        }
        
        var str = new SmalltalkObject(stringClass, 1);
        str.SetField(0, value);
        return str.Oop;
    }
    
    /// <summary>
    /// Send a message to an object.
    /// </summary>
    public int PrimitiveSendMessage(int receiverId, int selectorId, int[] argumentIds)
    {
        Console.WriteLine($"[VM] SendMessage: receiver={receiverId}, selector={selectorId}, args=[{string.Join(",", argumentIds)}]");
        
        var receiver = ObjectMemory.FromOop(receiverId);
        var selector = ObjectMemory.FromOop(selectorId);
        
        if (receiver == null || selector == null)
        {
            Console.WriteLine($"[VM] SendMessage: Invalid receiver or selector");
            return PrimitiveNil();
        }
        
        // Get the receiver's class
        var receiverClass = receiver.ClassPointer;
        if (receiverClass == null)
        {
            Console.WriteLine($"[VM] SendMessage: Receiver has no class");
            return PrimitiveNil();
        }
        
        Console.WriteLine($"[VM] SendMessage: Receiver class OOP = {receiverClass.Oop}");
        
        // Look up the method in the class hierarchy (method inheritance)
        var currentClass = receiverClass;
        int methodOop = -1;
        int methodDictOop = -1;
        
        while (currentClass != null && methodOop < 0)
        {
            // Look up the method in the current class's method dictionary
            methodDictOop = GetSlot(currentClass.Oop, 1); // Method dictionary is at slot 1
            Console.WriteLine($"[VM] SendMessage: Checking class OOP {currentClass.Oop}, method dictionary OOP = {methodDictOop}");
            
            if (methodDictOop >= 0)
            {
                // Look up the method by selector
                Console.WriteLine($"[VM] SendMessage: Looking up selector OOP {selectorId} in dictionary OOP {methodDictOop}");
                methodOop = PrimitiveDictionaryAt(methodDictOop, selectorId);
                
                if (methodOop >= 0)
                {
                    Console.WriteLine($"[VM] SendMessage: Found method OOP {methodOop} in class OOP {currentClass.Oop}");
                    break;
                }
            }
            
            // Method not found in current class, try superclass
            var superclassOop = GetSlot(currentClass.Oop, 0); // Superclass is at slot 0
            if (superclassOop >= 0)
            {
                currentClass = ObjectMemory.FromOop(superclassOop);
                Console.WriteLine($"[VM] SendMessage: Method not found, trying superclass OOP {superclassOop}");
            }
            else
            {
                Console.WriteLine($"[VM] SendMessage: No superclass found for class OOP {currentClass.Oop}");
                currentClass = null;
            }
        }
        
        if (methodOop < 0)
        {
            Console.WriteLine($"[VM] SendMessage: Method not found for selector OOP {selectorId} in entire class hierarchy");
            return PrimitiveNil();
        }
        
        var methodObj = ObjectMemory.FromOop(methodOop);
        if (methodObj == null)
        {
            Console.WriteLine($"[VM] SendMessage: Method object not found");
            return PrimitiveNil();
        }
        
        Console.WriteLine($"[VM] SendMessage: Found method OOP {methodOop} for selector OOP {selectorId}");
        
        // Now execute the method by constructing a CompiledMethod and running it
        var compiledMethod = ExtractCompiledMethod(methodObj);
        if (compiledMethod == null)
        {
            Console.WriteLine($"[VM] SendMessage: Could not extract CompiledMethod from OOP {methodOop}");
            return PrimitiveNil();
        }
        
        // Convert argument OOPs to objects
        var args = new object?[argumentIds.Length];
        for (int i = 0; i < argumentIds.Length; i++)
        {
            args[i] = ObjectMemory.FromOop(argumentIds[i]);
        }
        
        // Check if this is a primitive method
        if (compiledMethod.Header.Extension > 0)
        {
            Console.WriteLine($"[VM] SendMessage: Executing primitive {compiledMethod.Header.Extension}");
            
            // Handle primitive 70 (basicNew)
            if (compiledMethod.Header.Extension == 70)
            {
                // Create a new instance of the receiver (which should be a class)
                // Use PrimitiveNewInstance to properly handle instance size
                var instanceOop = PrimitiveNewInstance(receiverId);
                Console.WriteLine($"[VM] SendMessage: Primitive 70 (basicNew) created instance OOP {instanceOop}");
                return instanceOop;
            }
            
            Console.WriteLine($"[VM] SendMessage: Unknown primitive {compiledMethod.Header.Extension}, falling back to bytecode execution");
        }
        
        // Execute the method using the interpreter
        Console.WriteLine($"[VM] SendMessage: Executing method with {compiledMethod.Bytecodes.Length} bytecodes");
        try
        {
            // Enable tracing for debugging
            Tracer.Enabled = true;
            var interpreter = new Interpreter { ExitOnReturn = false };
            
            // Create a method context and execute
            var tempCount = Math.Max(compiledMethod.TemporariesCount, args.Length);
            var temps = new object?[tempCount];
            
            // Copy arguments into temporaries
            for (int i = 0; i < args.Length; i++)
            {
                temps[i] = args[i];
                Console.WriteLine($"[VM] SendMessage: Temp[{i}] = argument OOP {argumentIds[i]}");
            }
            
            Console.WriteLine($"[VM] SendMessage: Executing with {tempCount} temporaries, receiver OOP {receiverId}");
            var result = interpreter.Evaluate(compiledMethod, receiver, temps);
            
            // Convert result to OOP
            if (result == null)
            {
                return PrimitiveNil();
            }
            else if (result is SmalltalkObject resultObj)
            {
                return resultObj.Oop;
            }
            else if (result is int intResult)
            {
                // Create a SmallInteger
                var intObj = new SmalltalkObject(null, 1);
                intObj.SetField(0, intResult);
                return intObj.Oop;
            }
            else if (result is string strResult)
            {
                return PrimitiveInternString(strResult);
            }
            else
            {
                Console.WriteLine($"[VM] SendMessage: Unknown result type {result.GetType()}");
                return PrimitiveNil();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VM] SendMessage: Execution failed: {ex.Message}");
            return PrimitiveNil();
        }
    }
    
    /// <summary>
    /// Extracts a CompiledMethod from a SmalltalkObject representation.
    /// </summary>
    private CompiledMethod? ExtractCompiledMethod(SmalltalkObject methodObj)
    {
        // CompiledMethod has 4 fields: selector, bytecode, literals, header
        if (methodObj.Fields.Length < 4)
        {
            Console.WriteLine($"[VM] ExtractCompiledMethod: Method object has {methodObj.Fields.Length} fields, expected at least 4");
            return null;
        }
        
        // Extract bytecodes from slot 1
        var bytecodesField = methodObj.Fields[1];
        byte[] bytecodes;
        
        Console.WriteLine($"[VM] ExtractCompiledMethod: Bytecodes field type: {bytecodesField?.GetType().Name ?? "null"}");
        
        if (bytecodesField is SmalltalkObject bytecodeArray)
        {
            Console.WriteLine($"[VM] ExtractCompiledMethod: BytecodeArray has {bytecodeArray.Fields.Length} fields");
            // Extract bytes from the array
            bytecodes = new byte[bytecodeArray.Fields.Length];
            for (int i = 0; i < bytecodeArray.Fields.Length; i++)
            {
                var field = bytecodeArray.Fields[i];
                Console.WriteLine($"[VM] ExtractCompiledMethod: Bytecode[{i}] field type: {field?.GetType().Name ?? "null"}, value: {field}");
                
                if (field is SmalltalkObject byteObj && byteObj.Fields.Length > 0)
                {
                    Console.WriteLine($"[VM] ExtractCompiledMethod: Bytecode[{i}] is SmalltalkObject with {byteObj.Fields.Length} fields, field[0] = {byteObj.Fields[0]}");
                    // Try to get the byte value from the SmallInteger object
                    if (byteObj.Fields[0] is int intVal)
                    {
                        bytecodes[i] = (byte)intVal;
                    }
                    else if (byteObj.Fields[0] is string byteStr && byte.TryParse(byteStr, out byte byteVal))
                    {
                        bytecodes[i] = byteVal;
                    }
                    else
                    {
                        bytecodes[i] = 0;
                    }
                }
                else if (field is int directIntVal)
                {
                    Console.WriteLine($"[VM] ExtractCompiledMethod: Bytecode[{i}] is direct int: {directIntVal}");
                    bytecodes[i] = (byte)directIntVal;
                }
                else
                {
                    Console.WriteLine($"[VM] ExtractCompiledMethod: Bytecode[{i}] defaulting to 0");
                    bytecodes[i] = 0;
                }
            }
        }
        else
        {
            // No bytecodes
            bytecodes = Array.Empty<byte>();
        }
        
        // Extract literals from slot 2
        var literalsField = methodObj.Fields[2];
        object[] literals;
        
        if (literalsField is SmalltalkObject literalsArray)
        {
            literals = new object[literalsArray.Fields.Length];
            for (int i = 0; i < literalsArray.Fields.Length; i++)
            {
                literals[i] = literalsArray.Fields[i] ?? new object();
            }
        }
        else
        {
            literals = Array.Empty<object>();
        }
        
        // Extract header from slot 3
        var headerField = methodObj.Fields[3];
        int primitiveNumber = 0;
        int argCount = 0;
        int tempCount = 0;
        
        Console.WriteLine($"[VM] ExtractCompiledMethod: Header field type: {headerField?.GetType().Name ?? "null"}, value: {headerField}");
        
        if (headerField is SmalltalkObject headerObj && headerObj.Fields.Length > 0)
        {
            var firstField = headerObj.Fields[0];
            Console.WriteLine($"[VM] ExtractCompiledMethod: Header object has {headerObj.Fields.Length} fields, field[0] type = {firstField?.GetType().Name ?? "null"}, value = {firstField}");
            if (headerObj.Fields[0] is int headerInt)
            {
                // If header is > 0, it might be a primitive number
                primitiveNumber = headerInt;
                // For primitives, we need to determine arg count from the primitive
                // Primitive 70 (basicNew) takes 0 arguments
                if (primitiveNumber == 70)
                {
                    argCount = 0;
                    tempCount = 0;
                }
            }
            else if (headerObj.Fields[0] is string headerStr && int.TryParse(headerStr, out int headerVal))
            {
                // Could be primitive number or arg count
                if (headerVal > 0 && headerVal < 256)
                {
                    // Likely a primitive number
                    primitiveNumber = headerVal;
                    // For primitive 70 (basicNew), no arguments
                    if (primitiveNumber == 70)
                    {
                        argCount = 0;
                        tempCount = 0;
                    }
                }
                else
                {
                    argCount = headerVal;
                    tempCount = argCount;
                }
            }
        }
        
        Console.WriteLine($"[VM] ExtractCompiledMethod: Extracted method with {bytecodes.Length} bytecodes, {literals.Length} literals, primitive={primitiveNumber}, args={argCount}");
        if (bytecodes.Length > 0)
        {
            Console.WriteLine($"[VM] ExtractCompiledMethod: Bytecodes = [{string.Join(", ", bytecodes)}]");
        }
        
        // Create a proper method header
        // The header encoding: bits 0-2: flag/argCount, bits 3-7: tempCount, bit 8: large context, bits 9-14: literal count
        int headerRaw = (argCount & 0x7) | ((tempCount & 0x1F) << 3) | ((literals.Length & 0x3F) << 9);
        
        return new CompiledMethod
        {
            Bytecodes = bytecodes,
            Literals = literals,
            TemporariesCount = tempCount,
            Header = new MethodHeader(headerRaw, primitiveNumber)
        };
    }
    
    /// <summary>
    /// Execute bytecode (not used in Phase 1).
    /// </summary>
    public int ExecuteBytecode(int receiverId, byte[] bytecode, int[] argumentIds)
    {
        throw new NotImplementedException("ExecuteBytecode is not used in Phase 1 bootstrap");
    }
    
    /// <summary>
    /// Get the registered globals dictionary.
    /// </summary>
    public Dictionary<string, int> GetGlobals()
    {
        return new Dictionary<string, int>(globals);
    }
    
    /// <summary>
    /// Get the registered classes dictionary.
    /// </summary>
    public Dictionary<string, int> GetClasses()
    {
        return new Dictionary<string, int>(classes);
    }
}