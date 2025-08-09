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
    private readonly BootstrapContext? bootstrapContext;
    private readonly Dictionary<string, int> symbolCache = new(); // Keep a symbol cache even after bootstrap
    private int? nilOop = null;
    private int smalltalkDictOop = -1;
    #pragma warning disable CS0414 // Field is assigned but its value is never used
    private bool usesSmalltalkDictionary = false; // Track when we switch to Smalltalk dictionary (for debugging)
    #pragma warning restore CS0414
    private int methodDictionaryClassOop = -1;  // Cache the MethodDictionary class OOP
    
    /// <summary>
    /// Controls whether detailed VM debug output is printed to console.
    /// Set to false to reduce output noise during normal operation.
    /// </summary>
    public static bool EnableVMDebugOutput { get; set; } = false;
    
    public VMPrimitiveAdapter(VirtualMachine vm)
    {
        this.vm = vm;
        // Bootstrap context is only created when needed during Phase 1
        // and is deactivated after Phase 1 completes
        this.bootstrapContext = new BootstrapContext();
    }
    
    /// <summary>
    /// Helper method to conditionally output VM debug messages.
    /// </summary>
    private static void DebugLog(string message)
    {
        if (EnableVMDebugOutput)
        {
            Console.WriteLine(message);
        }
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
        // Try to get the class name by checking known classes
        string className = GetClassName(classId);
        if (!string.IsNullOrEmpty(className) && !className.StartsWith("<"))
        {
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
                DebugLog($"[VM] Creating MethodDictionary with size {instanceSize} to test capacity");
            }
            else if (className == "Dictionary" || className == "SystemDictionary" || className == "IdentityDictionary")
            {
                // Dictionaries need space for tally + key-value pairs
                // Need enough space for all bootstrap classes and globals
                // Phase 1: ~28 entries, Phase 2: ~30 entries, total ~55 each for globals and classes
                // Total: ~110 entries, need at least 110 pairs + room for growth
                instanceSize = 257; // tally + 128 pairs (plenty for bootstrap + growth)
                DebugLog($"[VM] Creating {className} with size {instanceSize} for initial capacity");
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
        {
            DebugLog($"[VM] GetSlot: Object OOP {objectId} not found");
            return -1;
        }
            
        if (slotIndex < 0 || slotIndex >= obj.Fields.Length)
        {
            DebugLog($"[VM] GetSlot: Slot {slotIndex} out of range for object OOP {objectId} with {obj.Fields.Length} slots");
            return -1;
        }
            
        var value = obj.Fields[slotIndex];
        if (value is SmalltalkObject smalltalkObj)
        {
            DebugLog($"[VM] GetSlot: Object OOP {objectId} slot {slotIndex} contains SmalltalkObject OOP {smalltalkObj.Oop}");
            return smalltalkObj.Oop;
        }
        
        DebugLog($"[VM] GetSlot: Object OOP {objectId} slot {slotIndex} contains {value?.GetType().Name ?? "null"}, returning -1");
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
            
        // Handle SmallIntegers as tagged pointers
        object? value;
        if (IsSmallInteger(valueId))
        {
            // Store the tagged pointer directly
            value = valueId;
        }
        else
        {
            // OOP 0 is a valid object (ProtoObject), so we need to handle it
            // Only use null if we explicitly want to set nil (which would be represented differently)
            value = ObjectMemory.FromOop(valueId);
        }
        obj.SetField(slotIndex, value);
        
        // Debug: log what was set
        DebugLog($"[VM] SetSlot: Object OOP {objectId} slot {slotIndex} <- value OOP {valueId} (stored as {value?.GetType().Name ?? "null"})");
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
    /// Intern a symbol and return its OOP using message send.
    /// </summary>
    public int PrimitiveInternSymbol(string name)
    {
        // Always check the symbol cache first to ensure uniqueness
        if (symbolCache.TryGetValue(name, out var cachedOop))
        {
            DebugLog($"[VM] Symbol '{name}' already interned as OOP {cachedOop}");
            return cachedOop;
        }
        
        // Phase 1: Also register in bootstrap context
        if (bootstrapContext != null && bootstrapContext.IsActive)
        {
            if (bootstrapContext.TryGetTemporarySymbol(name, out var existingOop))
            {
                symbolCache[name] = existingOop;
                DebugLog($"[VM] Symbol '{name}' already interned as OOP {existingOop} (bootstrap)");
                return existingOop;
            }
        }
        
        // After Phase 1, try to use message send: Symbol new: name
        var symbolClassOop = PrimitiveLookupClass("Symbol");
        if (symbolClassOop > 0)
        {
            // Check if Symbol class has new: method
            var methodDictOop = GetSlot(symbolClassOop, 1);
            if (methodDictOop >= 0)
            {
                var newSelector = PrimitiveInternSymbol("new:"); // Recursive but terminates via cache
                var methodOop = PrimitiveDictionaryAt(methodDictOop, newSelector);
                if (methodOop >= 0)
                {
                    // Method exists, use message send
                    var nameStringOop = PrimitiveInternString(name);
                    var resultOop = PrimitiveSendMessage(symbolClassOop, newSelector, new[] { nameStringOop });
                    // Register in caches
                    symbolCache[name] = resultOop;
                    if (bootstrapContext != null && bootstrapContext.IsActive)
                    {
                        bootstrapContext.RegisterTemporarySymbol(name, resultOop);
                    }
                    DebugLog($"[VM] Interned symbol '{name}' via message send as OOP {resultOop}");
                    return resultOop;
                }
            }
        }
        
        // Fallback for bootstrap: direct creation
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
        
        // Store in symbol cache and bootstrap context if active
        symbolCache[name] = symbol.Oop;
        if (bootstrapContext != null && bootstrapContext.IsActive)
        {
            bootstrapContext.RegisterTemporarySymbol(name, symbol.Oop);
        }
        DebugLog($"[VM] Interned new symbol '{name}' as OOP {symbol.Oop} (direct creation)");
        
        return symbol.Oop;
    }
    
    /// <summary>
    /// Gets the string value of a Symbol object.
    /// </summary>
    private string GetSymbolString(int symbolOop)
    {
        if (symbolOop < 0) return "<invalid>";
        
        var symbol = ObjectMemory.FromOop(symbolOop);
        if (symbol == null) return $"<unknown OOP {symbolOop}>";
        
        // Symbol stores its name in field 0
        if (symbol.Fields.Length > 0 && symbol.Fields[0] is string name)
        {
            return name;
        }
        
        return $"<symbol OOP {symbolOop}>";
    }
    
    /// <summary>
    /// Gets the name of a class object.
    /// </summary>
    private string GetClassName(int classOop)
    {
        if (classOop < 0) return "<invalid>";
        
        // First check if this is registered as a global
        // During bootstrap, check temporary context
        if (bootstrapContext != null && bootstrapContext.IsActive)
        {
            // Can't iterate during bootstrap phase
            return $"<class OOP {classOop}>";
        }
        
        // After bootstrap, would need to iterate Smalltalk dictionary
        // For now, return a placeholder
        return $"<class OOP {classOop}>";
        
        /* TODO: Once we have proper dictionary iteration:
        foreach (var kvp in SmalltalkDictionary)
        {
            if (kvp.Value == classOop)
            {
                // Convert FooClass to "Foo class" for metaclasses
                var name = kvp.Key;
                if (name.EndsWith("Class") && name != "Class" && name != "Metaclass")
                {
                    return name.Substring(0, name.Length - 5) + " class";
                }
                return name;
            }
        }
        
        // Try to get the name from the class object's name field (slot 5 for Class objects)
        var classObj = ObjectMemory.FromOop(classOop);
        if (classObj != null && classObj.Fields.Length > 5)
        {
            var nameField = classObj.Fields[5];
            if (nameField is SmalltalkObject nameSymbol && nameSymbol.Fields.Length > 0)
            {
                if (nameSymbol.Fields[0] is string name)
                {
                    return name;
                }
            }
        }
        
        return $"<class OOP {classOop}>";
        */
    }
    
    /// <summary>
    /// Gets the Smalltalk class name of an object.
    /// </summary>
    private string GetObjectClassName(SmalltalkObject? obj)
    {
        if (obj == null) return "nil";
        
        if (obj.ClassPointer != null)
        {
            return GetClassName(obj.ClassPointer.Oop);
        }
        
        // Note: SmallIntegers are now tagged pointers, not SmalltalkObjects
        // This method only handles actual objects
        
        return "<unknown class>";
    }
    
    /// <summary>
    /// Gets a description of an object in Smalltalk terms.
    /// </summary>
    private string DescribeObject(SmalltalkObject? obj)
    {
        if (obj == null) return "nil";
        
        var className = GetObjectClassName(obj);
        
        // For SmallIntegers, show the value
        if (className == "SmallInteger" && obj.Fields.Length > 0 && obj.Fields[0] is int value)
        {
            return $"{value} (SmallInteger)";
        }
        
        // For Symbols, show the symbol name
        if (className == "Symbol" && obj.Fields.Length > 0 && obj.Fields[0] is string symbolName)
        {
            return $"#{symbolName}";
        }
        
        // For Strings, show the string value
        if (className == "String" && obj.Fields.Length > 0 && obj.Fields[0] is string stringValue)
        {
            return $"'{stringValue}'";
        }
        
        // For Arrays, show size
        if (className == "Array")
        {
            return $"Array[{obj.Fields.Length}]";
        }
        
        // For ByteArrays, show size
        if (className == "ByteArray")
        {
            return $"ByteArray[{obj.Fields.Length}]";
        }
        
        return $"a {className}";
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
        
        var parts = new System.Collections.Generic.List<string>();
        
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
    /// Create a SmallInteger object using message send.
    /// </summary>
    public int PrimitiveNewSmallInteger(int value)
    {
        // Canonical SmallInteger representation: tagged pointer
        // Bit 0 = 1 indicates SmallInteger
        // Value is shifted left by 1
        // This gives us a 31-bit signed integer range
        return (value << 1) | 1;
    }
    
    /// <summary>
    /// Check if an OOP is a SmallInteger (tagged pointer).
    /// </summary>
    public static bool IsSmallInteger(int oop)
    {
        return (oop & 1) == 1;
    }
    
    /// <summary>
    /// Extract the integer value from a SmallInteger OOP.
    /// </summary>
    public static int SmallIntegerValue(int oop)
    {
        if (!IsSmallInteger(oop))
            throw new InvalidOperationException($"OOP {oop} is not a SmallInteger");
        return oop >> 1;
    }
    
    /// <summary>
    /// Create a new ByteArray.
    /// </summary>
    public int PrimitiveNewByteArray(int size)
    {
        var byteArrayClassOop = PrimitiveLookupClass("ByteArray");
        SmalltalkObject? byteArrayClass = byteArrayClassOop > 0 ? ObjectMemory.FromOop(byteArrayClassOop) : null;
        
        var byteArray = new SmalltalkObject(byteArrayClass, size);
        // Initialize with zeros
        for (int i = 0; i < size; i++)
        {
            byteArray.SetField(i, (byte)0);
        }
        return byteArray.Oop;
    }
    
    /// <summary>
    /// Set a byte value in a ByteArray.
    /// </summary>
    public void PrimitiveByteArrayAtPut(int byteArrayOop, int index, byte value)
    {
        var byteArray = ObjectMemory.FromOop(byteArrayOop);
        if (byteArray == null)
            throw new InvalidOperationException($"ByteArray with OOP {byteArrayOop} not found");
            
        if (index < 0 || index >= byteArray.Fields.Length)
            throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} out of range for ByteArray with {byteArray.Fields.Length} bytes");
            
        // Store the byte directly
        byteArray.SetField(index, value);
    }
    
    /// <summary>
    /// Create a new array using message send.
    /// </summary>
    public int PrimitiveNewArray(int size)
    {
        // After Phase 1 bootstrap, use message send to create Array
        var arrayClassOop = PrimitiveLookupClass("Array");
        if (arrayClassOop > 0)
        {
            // Try to use message send: Array new: size
            var newSelector = PrimitiveInternSymbol("new:");
            var sizeOop = PrimitiveNewSmallInteger(size);
            
            // Check if we can use message sends (Phase 1 complete)
            var methodDictOop = GetSlot(arrayClassOop, 1);
            if (methodDictOop >= 0)
            {
                var methodOop = PrimitiveDictionaryAt(methodDictOop, newSelector);
                if (methodOop >= 0)
                {
                    // Method exists, use message send
                    return PrimitiveSendMessage(arrayClassOop, newSelector, new[] { sizeOop });
                }
            }
            
            // Fallback for bootstrap: direct creation
            var arrayClass = ObjectMemory.FromOop(arrayClassOop);
            var array = new SmalltalkObject(arrayClass, size);
            return array.Oop;
        }
        
        // Fallback: create without class pointer
        var arrayNoClass = new SmalltalkObject(null, size);
        return arrayNoClass.Oop;
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
            
        // Handle SmallIntegers as tagged pointers
        object? key;
        object? value;
        
        if (IsSmallInteger(keyId))
        {
            key = keyId;
        }
        else
        {
            key = ObjectMemory.FromOop(keyId);
            if (key == null)
                throw new InvalidOperationException($"Key with OOP {keyId} not found");
        }
        
        if (IsSmallInteger(valueId))
        {
            value = valueId;
        }
        else
        {
            value = ObjectMemory.FromOop(valueId);
            if (value == null)
                throw new InvalidOperationException($"Value with OOP {valueId} not found");
        }
            
        // Get nil object for comparison
        var nilOopValue = PrimitiveNil();
        var nilObject = ObjectMemory.FromOop(nilOopValue);
            
        // Find an empty slot or replace existing key
        // Dictionary structure: [tally, key1, value1, key2, value2, ...]
        // Start at index 1 (skip tally at index 0)
        bool installed = false;
        
        for (int i = 1; i < dict.Fields.Length - 1; i += 2)
        {
            var existingKey = dict.Fields[i];
            
            // Check if this slot is empty (either C# null or nil object)
            // Note: nil is an object, not a SmallInteger
            if (existingKey == null || 
                (existingKey is SmalltalkObject existingObj && existingObj.Oop == nilOopValue))
            {
                dict.SetField(i, key);
                dict.SetField(i + 1, value);
                installed = true;
                
                // Update tally (slot 0) - increment count of entries
                var tallyField = dict.Fields[0];
                if (tallyField is SmalltalkObject tallyObj)
                {
                    // Assuming it's a SmallInteger, we need to increment it
                    // For now, we'll just leave tally management to Smalltalk code
                    // once proper methods are installed
                }
                
                DebugLog($"[VM] Dictionary at:put: installed new entry at slot {i}: key OOP {keyId} -> value OOP {valueId}");
                break;
            }
            
            // Check if this key already exists (need to compare OOPs or SmallIntegers)
            bool keyExists = false;
            if (IsSmallInteger(keyId) && existingKey is int existingKeyInt)
            {
                keyExists = existingKeyInt == keyId;
            }
            else if (!IsSmallInteger(keyId) && existingKey is SmalltalkObject existingKeyObj)
            {
                keyExists = existingKeyObj.Oop == keyId;
            }
            
            if (keyExists)
            {
                // Replace existing value
                dict.SetField(i + 1, value);
                installed = true;
                DebugLog($"[VM] Dictionary at:put: replaced existing entry at slot {i}: key OOP {keyId} -> value OOP {valueId}");
                break;
            }
        }
        
        if (!installed)
        {
            DebugLog($"[VM] Warning: Dictionary full, could not install key OOP {keyId}");
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
            DebugLog($"[VM] DictionaryAt: Dictionary OOP {dictId} not found");
            return -1;
        }
        
        // Check the class of the dictionary to determine its type
        bool isMethodDictionary = false;
        
        // Use the cached MethodDictionary class OOP if available
        if (methodDictionaryClassOop > 0 && dict.ClassPointer != null)
        {
            isMethodDictionary = dict.ClassPointer.Oop == methodDictionaryClassOop;
        }
        else if (dict.Fields.Length >= 2)
        {
            // Fallback: check if slot 0 is a SmallInteger (flat format) or object (MethodDictionary format)
            var field0 = dict.Fields[0];
            // If field0 is an integer (tagged SmallInteger), it's a flat dictionary
            // If field0 is a SmalltalkObject, it's probably a MethodDictionary
            isMethodDictionary = field0 is SmalltalkObject;
        }
        
        if (isMethodDictionary && 
            dict.Fields[0] is SmalltalkObject keysArray &&
            dict.Fields[1] is SmalltalkObject valuesArray &&
            keysArray.Fields != null && valuesArray.Fields != null)
        {
            // This is a proper MethodDictionary with separate keys and values arrays
            DebugLog($"[VM] DictionaryAt: Using MethodDictionary format with keys array OOP {keysArray.Oop} and values array OOP {valuesArray.Oop}");
            
            // Search through the keys array
            for (int i = 0; i < keysArray.Fields.Length; i++)
            {
                var existingKey = keysArray.Fields[i];
                
                if (existingKey == null)
                    continue; // Empty slot
                    
                // Check if this key matches (compare OOPs)
                if (existingKey is SmalltalkObject existingKeyObj)
                {
                    var keyName = GetSymbolString(existingKeyObj.Oop);
                    var lookingForName = GetSymbolString(keyId);
                    DebugLog($"[VM] DictionaryAt: Checking key at index {i}: #{keyName} (OOP {existingKeyObj.Oop}) vs looking for #{lookingForName} (OOP {keyId})");
                    
                    if (existingKeyObj.Oop == keyId)
                    {
                        // Found the key, get corresponding value from values array
                        if (i < valuesArray.Fields.Length && valuesArray.Fields[i] is SmalltalkObject valueObj)
                        {
                            DebugLog($"[VM] DictionaryAt: Found value OOP {valueObj.Oop} for key OOP {keyId}");
                            return valueObj.Oop;
                        }
                        DebugLog($"[VM] DictionaryAt: Value at index {i} not found or not a SmalltalkObject");
                        return -1;
                    }
                }
            }
        }
        else
        {
            // This is a flat format dictionary (legacy)
            DebugLog($"[VM] DictionaryAt: Using flat format dictionary");
            
            // Search for the key in the dictionary (flat format: tally, key, value, key, value...)
            // Start at index 1 to skip the tally
            for (int i = 1; i < dict.Fields.Length - 1; i += 2)
            {
                var existingKey = dict.Fields[i];
                
                if (existingKey == null)
                    continue; // Empty slot
                    
                // Check if this key matches (compare OOPs or SmallInteger values)
                bool keyMatches = false;
                
                if (IsSmallInteger(keyId) && existingKey is int existingKeyInt)
                {
                    // Both are SmallIntegers
                    keyMatches = existingKeyInt == keyId;
                }
                else if (!IsSmallInteger(keyId) && existingKey is SmalltalkObject existingKeyObj)
                {
                    // Both are object pointers
                    keyMatches = existingKeyObj.Oop == keyId;
                    
                    var keyName2 = GetSymbolString(existingKeyObj.Oop);
                    var lookingForName2 = GetSymbolString(keyId);
                    DebugLog($"[VM] DictionaryAt: Checking key at slot {i}: #{keyName2} (OOP {existingKeyObj.Oop}) vs looking for #{lookingForName2} (OOP {keyId})");
                }
                
                if (keyMatches)
                {
                    // Found the key, return the value
                    var value = dict.Fields[i + 1];
                    if (value is int valueInt)
                    {
                        // SmallInteger value
                        DebugLog($"[VM] DictionaryAt: Found SmallInteger value {SmallIntegerValue(valueInt)} (tagged {valueInt}) for key OOP {keyId}");
                        return valueInt;
                    }
                    else if (value is SmalltalkObject valueObj)
                    {
                        DebugLog($"[VM] DictionaryAt: Found value OOP {valueObj.Oop} for key OOP {keyId}");
                        return valueObj.Oop;
                    }
                    DebugLog($"[VM] DictionaryAt: Value at slot {i+1} is not recognized");
                    return -1;
                }
            }
        }
        
        // Key not found
        DebugLog($"[VM] DictionaryAt: Key OOP {keyId} not found in dictionary");
        return -1;
    }
    
    /// <summary>
    /// Compile and install a method in a class.
    /// </summary>
    public void PrimitiveInstallMethod(int classId, string selector, string sourceCode)
    {
        // For bootstrap, we'll skip actual compilation
        // Methods will be compiled later when the compiler is available
        JsonBootstrapExecutor.BootstrapLog($"[BOOTSTRAP] Method {selector} queued for installation in class {classId}");
    }
    
    /// <summary>
    /// Get the nil object OOP.
    /// </summary>
    public int PrimitiveNil()
    {
        // Create nil object if it doesn't exist yet
        if (nilOop == null)
        {
            // Get the UndefinedObject class if it exists
            var undefinedObjectClassOop = PrimitiveLookupClass("UndefinedObject");
            SmalltalkObject? undefinedObjectClass = null;
            if (undefinedObjectClassOop > 0)
            {
                undefinedObjectClass = ObjectMemory.FromOop(undefinedObjectClassOop);
            }
            
            // nil is a special singleton instance of UndefinedObject with no fields
            var nil = new SmalltalkObject(undefinedObjectClass, 0);
            nilOop = nil.Oop;
        }
        return nilOop.Value;
    }
    
    /// <summary>
    /// Register a global variable.
    /// During Phase 1, uses temporary bootstrap context.
    /// After Phase 1, registers directly in Smalltalk SystemDictionary.
    /// </summary>
    public void PrimitiveRegisterGlobal(string name, int valueId)
    {
        // Special case: track Smalltalk dictionary
        if (name == "Smalltalk")
        {
            smalltalkDictOop = valueId;
        }
        // Special case: track MethodDictionary class
        else if (name == "MethodDictionary")
        {
            methodDictionaryClassOop = valueId;
            JsonBootstrapExecutor.BootstrapLog($"[BOOTSTRAP] Cached MethodDictionary class OOP: {valueId}");
        }
        
        // Phase 1: Use bootstrap context
        if (bootstrapContext != null && bootstrapContext.IsActive)
        {
            bootstrapContext.RegisterTemporaryGlobal(name, valueId);
            
            // Also track classes separately for easy lookup
            if (name.Length > 0 && char.IsUpper(name[0]))
            {
                bootstrapContext.RegisterTemporaryClass(name, valueId);
            }
            
            JsonBootstrapExecutor.BootstrapLog($"[BOOTSTRAP] Registered temporary global '{name}' -> OOP {valueId}");
        }
        // After Phase 1: Register directly in Smalltalk dictionary
        else if (smalltalkDictOop > 0)
        {
            var keySymbol = PrimitiveInternSymbol(name);
            PrimitiveDictionaryAtPut(smalltalkDictOop, keySymbol, valueId);
            JsonBootstrapExecutor.BootstrapLog($"[BOOTSTRAP] Registered '{name}' in Smalltalk dictionary -> OOP {valueId}");
        }
        else
        {
            throw new InvalidOperationException($"Cannot register global '{name}': No bootstrap context or Smalltalk dictionary available");
        }
    }
    
    /// <summary>
    /// Get a registered class by name.
    /// During Phase 1, uses temporary bootstrap context.
    /// After Phase 1, looks up in Smalltalk SystemDictionary.
    /// </summary>
    public int PrimitiveLookupClass(string className)
    {
        // Phase 1: Use bootstrap context
        if (bootstrapContext != null && bootstrapContext.IsActive)
        {
            return bootstrapContext.LookupTemporaryClass(className);
        }
        
        // After Phase 1: Look up in Smalltalk dictionary
        if (smalltalkDictOop > 0)
        {
            return LookupInSmalltalkDictionary(className);
        }
        
        return -1;
    }
    
    /// <summary>
    /// Look up a global variable by name.
    /// During Phase 1, uses temporary bootstrap context.
    /// After Phase 1, looks up in Smalltalk SystemDictionary.
    /// </summary>
    public int PrimitiveLookupGlobal(string name)
    {
        // Phase 1: Use bootstrap context
        if (bootstrapContext != null && bootstrapContext.IsActive)
        {
            var result = bootstrapContext.LookupTemporaryGlobal(name);
            DebugLog($"[VM] PrimitiveLookupGlobal('{name}'): Found in bootstrap context -> OOP {result}");
            return result;
        }
        
        // After Phase 1: Look up in Smalltalk dictionary
        if (smalltalkDictOop > 0)
        {
            var result = LookupInSmalltalkDictionary(name);
            DebugLog($"[VM] PrimitiveLookupGlobal('{name}'): Found in Smalltalk dict -> OOP {result}");
            return result;
        }
        
        DebugLog($"[VM] PrimitiveLookupGlobal('{name}'): Not found (no bootstrap context and smalltalkDictOop={smalltalkDictOop})");
        return -1;
    }
    
    /// <summary>
    /// Intern a string and return its OOP using message send.
    /// </summary>
    public int PrimitiveInternString(string value)
    {
        // After Phase 1 bootstrap, use message send to create String
        var stringClassOop = PrimitiveLookupClass("String");
        if (stringClassOop < 0)
        {
            // String class doesn't exist yet, try the metaclass
            stringClassOop = PrimitiveLookupClass("StringClass");
        }
        
        if (stringClassOop > 0)
        {
            // For now, use direct creation during bootstrap
            // TODO: Once String class has proper new: method, use:
            // var newSelector = PrimitiveInternSymbol("new:");
            // return PrimitiveSendMessage(stringClassOop, newSelector, new[] { ... });
            
            var stringClass = ObjectMemory.FromOop(stringClassOop);
            var str = new SmalltalkObject(stringClass, 1);
            str.SetField(0, value);
            return str.Oop;
        }
        
        // Fallback: create without class pointer
        var strNoClass = new SmalltalkObject(null, 1);
        strNoClass.SetField(0, value);
        return strNoClass.Oop;
    }
    
    /// <summary>
    /// Send a message to an object.
    /// </summary>
    public int PrimitiveSendMessage(int receiverId, int selectorId, int[] argumentIds)
    {
        var selectorName = GetSymbolString(selectorId);
        DebugLog($"[VM] SendMessage: receiver={receiverId}, selector=#{selectorName} (OOP {selectorId}), args=[{string.Join(",", argumentIds)}]");
        
        var receiver = ObjectMemory.FromOop(receiverId);
        var selector = ObjectMemory.FromOop(selectorId);
        
        if (receiver == null || selector == null)
        {
            DebugLog($"[VM] SendMessage: Invalid receiver or selector");
            return PrimitiveNil();
        }
        
        // Get the receiver's class
        var receiverClass = receiver.ClassPointer;
        if (receiverClass == null)
        {
            DebugLog($"[VM] SendMessage: Receiver has no class");
            return PrimitiveNil();
        }
        
        var className = GetClassName(receiverClass.Oop);
        DebugLog($"[VM] SendMessage: Receiver class = {className} (OOP {receiverClass.Oop})");
        
        // Look up the method in the class hierarchy (method inheritance)
        var currentClass = receiverClass;
        int methodOop = -1;
        int methodDictOop = -1;
        
        while (currentClass != null && methodOop < 0)
        {
            // Look up the method in the current class's method dictionary
            methodDictOop = GetSlot(currentClass.Oop, 1); // Method dictionary is at slot 1
            var currentClassName = GetClassName(currentClass.Oop);
            DebugLog($"[VM] SendMessage: Checking class {currentClassName} (OOP {currentClass.Oop}), method dictionary OOP = {methodDictOop}");
            
            if (methodDictOop >= 0)
            {
                // Look up the method by selector
                DebugLog($"[VM] SendMessage: Looking up selector #{selectorName} (OOP {selectorId}) in dictionary OOP {methodDictOop}");
                methodOop = PrimitiveDictionaryAt(methodDictOop, selectorId);
                
                if (methodOop >= 0)
                {
                    DebugLog($"[VM] SendMessage: Found method OOP {methodOop} for #{selectorName} in class {currentClassName}");
                    break;
                }
            }
            
            // Method not found in current class, try superclass
            var superclassOop = GetSlot(currentClass.Oop, 0); // Superclass is at slot 0
            if (superclassOop >= 0)
            {
                currentClass = ObjectMemory.FromOop(superclassOop);
                var superclassName = GetClassName(superclassOop);
                DebugLog($"[VM] SendMessage: Method #{selectorName} not found in {currentClassName}, trying superclass {superclassName} (OOP {superclassOop})");
            }
            else
            {
                DebugLog($"[VM] SendMessage: No superclass found for class {currentClassName} (OOP {currentClass.Oop})");
                currentClass = null;
            }
        }
        
        if (methodOop < 0)
        {
            DebugLog($"[VM] SendMessage: Method not found for selector #{selectorName} (OOP {selectorId}) in entire class hierarchy");
            return PrimitiveNil();
        }
        
        var methodObj = ObjectMemory.FromOop(methodOop);
        if (methodObj == null)
        {
            DebugLog($"[VM] SendMessage: Method object not found");
            return PrimitiveNil();
        }
        
        DebugLog($"[VM] SendMessage: Found method OOP {methodOop} for selector #{selectorName}");
        
        // Now execute the method by constructing a CompiledMethod and running it
        var compiledMethod = ExtractCompiledMethod(methodObj);
        if (compiledMethod == null)
        {
            DebugLog($"[VM] SendMessage: Could not extract CompiledMethod from OOP {methodOop}");
            return PrimitiveNil();
        }
        
        // Convert argument OOPs to objects
        var args = new object?[argumentIds.Length];
        for (int i = 0; i < argumentIds.Length; i++)
        {
            args[i] = ObjectMemory.FromOop(argumentIds[i]);
        }
        
        // Check if this is a primitive method
        var primitiveIndex = compiledMethod.Header.PrimitiveIndex;
        if (primitiveIndex > 0)
        {
            DebugLog($"[VM] SendMessage: Executing primitive {primitiveIndex}");
            
            // Create a context for primitive execution
            var receiverObj = ObjectMemory.FromOop(receiverId);
            if (receiverObj == null)
            {
                DebugLog($"[VM] SendMessage: Receiver OOP {receiverId} not found");
                return PrimitiveNil();
            }
            
            var context = new Context
            {
                Stack = new Stack<object?>(),
                Receiver = receiverObj,
                Temporaries = new object?[0],
                Arguments = args,
                Method = compiledMethod
            };
            
            // Push receiver
            context.Stack.Push(context.Receiver);
            
            // Push arguments
            foreach (var argId in argumentIds)
            {
                context.Stack.Push(ObjectMemory.FromOop(argId));
            }
            
            // Execute the primitive through the dispatch mechanism
            if (global::Primitives.Execute(primitiveIndex, context))
            {
                // Primitive succeeded, get the result from the stack
                if (context.Stack.Count > 0)
                {
                    var result = context.Stack.Pop();
                    
                    // Convert result to OOP if it's a SmalltalkObject
                    if (result is SmalltalkObject resultObj)
                    {
                        DebugLog($"[VM] SendMessage: Primitive {primitiveIndex} executed successfully, result OOP {resultObj.Oop}");
                        return resultObj.Oop;
                    }
                    else if (result is int intResult)
                    {
                        // Might already be an OOP
                        DebugLog($"[VM] SendMessage: Primitive {primitiveIndex} executed successfully, result {intResult}");
                        return intResult;
                    }
                    else
                    {
                        DebugLog($"[VM] SendMessage: Primitive {primitiveIndex} executed but returned unexpected type: {result?.GetType().Name}");
                        return PrimitiveNil();
                    }
                }
                else
                {
                    DebugLog($"[VM] SendMessage: Primitive {primitiveIndex} executed but stack is empty");
                    return PrimitiveNil();
                }
            }
            else
            {
                DebugLog($"[VM] SendMessage: Primitive {primitiveIndex} failed, falling back to bytecode execution");
            }
        }
        
        // Execute the method using the interpreter
        DebugLog($"[VM] SendMessage: Executing method with {compiledMethod.Bytecodes.Length} bytecodes");
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
                DebugLog($"[VM] SendMessage: Temp[{i}] = argument OOP {argumentIds[i]}");
            }
            
            DebugLog($"[VM] SendMessage: Executing with {tempCount} temporaries, receiver OOP {receiverId}");
            var result = interpreter.Evaluate(compiledMethod, receiver, temps);
            
            // Debug: log what was returned
            if (result is SmalltalkObject debugObj)
            {
                DebugLog($"[VM] SendMessage: Method returned SmalltalkObject with OOP {debugObj.Oop}");
            }
            else
            {
                DebugLog($"[VM] SendMessage: Method returned {result?.GetType().Name ?? "null"}: {result}");
            }
            
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
                return PrimitiveNewSmallInteger(intResult);
            }
            else if (result is string strResult)
            {
                return PrimitiveInternString(strResult);
            }
            else
            {
                DebugLog($"[VM] SendMessage: Unknown result type {result.GetType()}");
                return PrimitiveNil();
            }
        }
        catch (Exception ex)
        {
            DebugLog($"[VM] SendMessage: Execution failed: {ex.Message}");
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
            DebugLog($"[VM] ExtractCompiledMethod: Method object has {methodObj.Fields.Length} fields, expected at least 4");
            return null;
        }
        
        // Extract bytecodes from slot 1
        var bytecodesField = methodObj.Fields[1];
        byte[] bytecodes;
        
        if (bytecodesField is SmalltalkObject bytecodeFieldObj)
        {
            var fieldClassName = GetObjectClassName(bytecodeFieldObj);
            DebugLog($"[VM] ExtractCompiledMethod: Bytecodes field is a {fieldClassName} with {bytecodeFieldObj.Fields.Length} elements");
        }
        else
        {
            DebugLog($"[VM] ExtractCompiledMethod: Bytecodes field is {bytecodesField?.GetType().Name ?? "null"}");
        }
        
        if (bytecodesField is SmalltalkObject bytecodeArray)
        {
            // BytecodeArray logging is now handled above
            // Extract bytes from the array
            bytecodes = new byte[bytecodeArray.Fields.Length];
            for (int i = 0; i < bytecodeArray.Fields.Length; i++)
            {
                var field = bytecodeArray.Fields[i];
                if (field is SmalltalkObject fieldObj)
                {
                    var desc = DescribeObject(fieldObj);
                    DebugLog($"[VM] ExtractCompiledMethod: Bytecode[{i}] = {desc}");
                }
                else
                {
                    DebugLog($"[VM] ExtractCompiledMethod: Bytecode[{i}] = {field} (raw)");
                }
                
                if (field is SmalltalkObject byteObj && byteObj.Fields.Length > 0)
                {
                    // Already logged above
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
                    // Direct int already logged above
                    bytecodes[i] = (byte)directIntVal;
                }
                else if (field is byte directByte)
                {
                    // Direct byte value stored in ByteArray
                    bytecodes[i] = directByte;
                }
                else
                {
                    // Default case already logged above
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
        
        if (headerField is SmalltalkObject headerFieldObj)
        {
            var headerDesc = DescribeObject(headerFieldObj);
            DebugLog($"[VM] ExtractCompiledMethod: Header field is {headerDesc}");
        }
        else
        {
            DebugLog($"[VM] ExtractCompiledMethod: Header field is {headerField} (raw)");
        }
        
        if (headerField is SmalltalkObject headerObj && headerObj.Fields.Length > 0)
        {
            var firstField = headerObj.Fields[0];
            
            int headerInt = 0;
            if (headerObj.Fields[0] is int directInt)
            {
                headerInt = directInt;
            }
            else if (headerObj.Fields[0] is string headerStr && int.TryParse(headerStr, out int parsedVal))
            {
                headerInt = parsedVal;
            }
            
            DebugLog($"[VM] ExtractCompiledMethod: Header = {FormatBlueBookHeader(headerInt)}");
            
            // Decode Blue Book header format (16-bit word):
            // Bits 0-8: Primitive number (0 if no primitive)
            // Bit 9: Large context flag
            // Bits 10-15: Number of temporaries
            
            primitiveNumber = headerInt & 0x1FF;  // Lower 9 bits
            bool largeContext = (headerInt & 0x200) != 0;  // Bit 9
            tempCount = (headerInt >> 10) & 0x3F;  // Bits 10-15
            
            // For methods without explicit temp count, use arg count
            if (tempCount == 0 && primitiveNumber == 0)
            {
                // This might be an old-style header with just arg count
                // (for backwards compatibility with existing Phase3 files)
                if (headerInt <= 10)
                {
                    argCount = headerInt;
                    tempCount = headerInt;
                }
            }
            else
            {
                // Proper Blue Book header
                argCount = tempCount; // Will be refined when we have proper arg tracking
            }
        }
        
        // Single concise summary line
        DebugLog($"[VM] ExtractCompiledMethod: CompiledMethod with bytecodes=[{string.Join(", ", bytecodes)}], {literals.Length} literals, primitive={primitiveNumber}, args={argCount}");
        
        // Create a proper method header
        // The header encoding: bits 0-2: flag/argCount, bits 3-7: tempCount, bit 8: large context, bits 9-14: literal count
        // If there's a primitive, set flag to 7 to indicate extension is used
        int flagValue = primitiveNumber > 0 ? 7 : Math.Min(argCount, 7);
        int headerRaw = (flagValue & 0x7) | ((tempCount & 0x1F) << 3) | ((literals.Length & 0x3F) << 9);
        
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
    /// DEPRECATED: This method exists only for backward compatibility.
    /// New code should use PrimitiveLookupGlobal instead.
    /// </summary>
    [Obsolete("Use PrimitiveLookupGlobal instead. This method will be removed.")]
    public Dictionary<string, int> GetGlobals()
    {
        // Return empty dictionary after Phase 1
        if (bootstrapContext == null || !bootstrapContext.IsActive)
        {
            return new Dictionary<string, int>();
        }
        
        // During Phase 1, return a snapshot for compatibility
        var result = new Dictionary<string, int>();
        // This is a hack for backward compatibility only
        return result;
    }
    
    /// <summary>
    /// Get the registered classes dictionary.
    /// DEPRECATED: This method exists only for backward compatibility.
    /// New code should use PrimitiveLookupClass instead.
    /// </summary>
    [Obsolete("Use PrimitiveLookupClass instead. This method will be removed.")]
    public Dictionary<string, int> GetClasses()
    {
        // Return empty dictionary after Phase 1
        if (bootstrapContext == null || !bootstrapContext.IsActive)
        {
            return new Dictionary<string, int>();
        }
        
        // During Phase 1, return a snapshot for compatibility
        var result = new Dictionary<string, int>();
        // This is a hack for backward compatibility only
        return result;
    }
    
    /// <summary>
    /// Complete Phase 1 bootstrap and migrate to Smalltalk dictionary.
    /// This should be called after Phase 1 creates the Smalltalk SystemDictionary.
    /// NOTE: This uses primitive operations which don't handle dictionary expansion.
    /// Use CompleteBootstrapWithMessageSends after Phase 3 instead.
    /// </summary>
    public void CompletePhase1Bootstrap()
    {
        if (bootstrapContext == null || !bootstrapContext.IsActive)
        {
            JsonBootstrapExecutor.BootstrapLog("[BOOTSTRAP] Phase 1 already completed or not started");
            return;
        }
        
        // Get the Smalltalk dictionary OOP
        smalltalkDictOop = bootstrapContext.GetSmalltalkDictionaryOop();
        if (smalltalkDictOop < 0)
        {
            JsonBootstrapExecutor.BootstrapLog("[BOOTSTRAP] Warning: Smalltalk SystemDictionary not found, continuing with bootstrap context");
            return;
        }
        
        // Migrate all temporary entries to Smalltalk dictionary
        bootstrapContext.MigrateToSmalltalkDictionary((name, oop) =>
        {
            var keySymbol = PrimitiveInternSymbol(name);
            PrimitiveDictionaryAtPut(smalltalkDictOop, keySymbol, oop);
        });
        
        // Switch to using Smalltalk dictionary
        usesSmalltalkDictionary = true;
        JsonBootstrapExecutor.BootstrapLog("[BOOTSTRAP] Phase 1 complete. All lookups now use Smalltalk SystemDictionary.");
    }
    
    /// <summary>
    /// Complete bootstrap using message sends (after Phase 2/3 when at:put: might be available).
    /// Falls back to primitive operations if at:put: is not available.
    /// </summary>
    public void CompleteBootstrapWithMessageSends()
    {
        if (bootstrapContext == null || !bootstrapContext.IsActive)
        {
            JsonBootstrapExecutor.BootstrapLog("[BOOTSTRAP] Bootstrap already completed or not started");
            return;
        }
        
        // Get the Smalltalk dictionary OOP
        smalltalkDictOop = bootstrapContext.GetSmalltalkDictionaryOop();
        if (smalltalkDictOop < 0)
        {
            JsonBootstrapExecutor.BootstrapLog("[BOOTSTRAP] Warning: Smalltalk SystemDictionary not found, continuing with bootstrap context");
            return;
        }
        
        JsonBootstrapExecutor.BootstrapLog("[BOOTSTRAP] Migrating from C# dictionaries to Smalltalk SystemDictionary...");
        JsonBootstrapExecutor.BootstrapLog("[BOOTSTRAP] Note: Dictionary at:put: is not a primitive - using VM operations");
        
        // Use primitive operations to populate the dictionary
        // Dictionary's at:put: method is implemented in Smalltalk bytecode, not as a primitive
        bootstrapContext.MigrateToSmalltalkDictionary((name, oop) =>
        {
            var keySymbol = PrimitiveInternSymbol(name);
            PrimitiveDictionaryAtPut(smalltalkDictOop, keySymbol, oop);
            JsonBootstrapExecutor.BootstrapLog($"[BOOTSTRAP] Registered '{name}' in Smalltalk dictionary");
        });
        
        // Switch to using Smalltalk dictionary
        usesSmalltalkDictionary = true;
        JsonBootstrapExecutor.BootstrapLog("[BOOTSTRAP] Bootstrap complete. All lookups now use Smalltalk SystemDictionary.");
    }
    
    /// <summary>
    /// Look up an entry in the Smalltalk SystemDictionary.
    /// </summary>
    private int LookupInSmalltalkDictionary(string name)
    {
        if (smalltalkDictOop < 0)
        {
            DebugLog($"[VM] LookupInSmalltalkDictionary('{name}'): smalltalkDictOop not set");
            return -1;
        }
            
        // During migration or when symbol doesn't exist yet, create it directly
        // to avoid circular dependency
        if (!symbolCache.TryGetValue(name, out var keySymbol))
        {
            DebugLog($"[VM] LookupInSmalltalkDictionary('{name}'): Symbol not in cache, creating new");
            // Create symbol directly without going through PrimitiveInternSymbol
            var symbolClassOop = -1;
            
            // Try to find Symbol class in Smalltalk dictionary
            // We can't use PrimitiveLookupClass here to avoid recursion
            var symbolKeyForClass = symbolCache.GetValueOrDefault("Symbol", -1);
            if (symbolKeyForClass > 0)
            {
                symbolClassOop = PrimitiveDictionaryAt(smalltalkDictOop, symbolKeyForClass);
                DebugLog($"[VM] LookupInSmalltalkDictionary: Found Symbol class OOP {symbolClassOop}");
            }
            else
            {
                DebugLog($"[VM] LookupInSmalltalkDictionary: 'Symbol' key not in cache");
            }
            
            SmalltalkObject? symbolClass = symbolClassOop > 0 ? ObjectMemory.FromOop(symbolClassOop) : null;
            var symbol = new SmalltalkObject(symbolClass, 1);
            symbol.SetField(0, name);
            keySymbol = symbol.Oop;
            symbolCache[name] = keySymbol;
            DebugLog($"[VM] LookupInSmalltalkDictionary: Created symbol '{name}' with OOP {keySymbol}");
        }
        else
        {
            DebugLog($"[VM] LookupInSmalltalkDictionary('{name}'): Using cached symbol OOP {keySymbol}");
        }
        
        var result = PrimitiveDictionaryAt(smalltalkDictOop, keySymbol);
        DebugLog($"[VM] LookupInSmalltalkDictionary('{name}'): Dictionary lookup returned OOP {result}");
        return result;
    }
}