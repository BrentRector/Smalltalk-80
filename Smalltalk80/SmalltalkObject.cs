using System;

/// <summary>
///     Represents a generic Smalltalk object instance.
/// </summary>
public class SmalltalkObject
{
    /// <summary>
    /// Debug flag for Phase 3 lookup tracing.
    /// </summary>
    public static bool EnablePhase3LookupDebug { get; set; } = false;
    /// <summary>
    ///     Gets or sets the object's class pointer. This may be <c>null</c>
    ///     during deserialization until the class table is resolved.
    /// </summary>
    public SmalltalkObject? ClassPointer { get; set; }

    /// <summary>
    ///     Gets the encoded object pointer assigned by <see cref="ObjectMemory"/>.
    /// </summary>
    public int Oop { get; internal set; }

    /// <summary>
    ///     Gets the raw field values stored in the object.
    /// </summary>
    public object?[] Fields { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SmalltalkObject"/> class.
    /// </summary>
    /// <param name="cls">The object's class pointer.</param>
    /// <param name="fieldCount">The number of fields to allocate.</param>
    public SmalltalkObject(SmalltalkObject? cls, int fieldCount)
    {
        ClassPointer = cls;
        Fields = new object?[fieldCount];
        ObjectMemory.Register(this);
    }

    /// <summary>
    ///     Reads the value of the specified field.
    /// </summary>
    /// <param name="index">The zero-based field index.</param>
    /// <returns>The field value.</returns>
    public object? GetField(int index) => Fields[index];

    /// <summary>
    ///     Sets the value of the specified field.
    /// </summary>
    /// <param name="index">The zero-based field index.</param>
    /// <param name="value">The value to store.</param>
    public void SetField(int index, object? value) => Fields[index] = value;

    /// <summary>
    ///     Looks up a method by searching through the class hierarchy's method dictionaries.
    ///     Implements the Smalltalk-80 lookup including the Object->Object class sidestep.
    /// </summary>
    public CompiledMethod? Lookup(Symbol selector)
    {
        // Start with this class and walk up the superclass chain
        var currentClass = this;
        int depth = 0;
        bool hasSidestepped = false;
        
        // Debug output for Phase 3
        if (EnablePhase3LookupDebug)
        {
            Console.WriteLine($"[LOOKUP] Looking for #{selector.Value} starting from class OOP {this.Oop}");
            // Try to identify what class this is
            var className = TryGetClassName(this);
            if (!string.IsNullOrEmpty(className))
            {
                Console.WriteLine($"[LOOKUP]   Starting class appears to be: {className}");
            }
        }
        
        while (currentClass != null)
        {
            if (EnablePhase3LookupDebug)
            {
                var className = TryGetClassName(currentClass);
                var classDesc = !string.IsNullOrEmpty(className) ? $" ({className})" : "";
                // Also try to get the superclass for debugging
                var superclass = GetSuperclassOf(currentClass);
                var superclassName = superclass != null ? TryGetClassName(superclass) : "nil";
                var superclassDesc = !string.IsNullOrEmpty(superclassName) ? $", superclass: {superclassName}" : "";
                Console.WriteLine($"[LOOKUP]   Checking class at OOP {currentClass.Oop}{classDesc} (depth {depth}{superclassDesc})");
            }
            
            // Look in the current class's method dictionary
            var method = LookupInMethodDictionary(currentClass, selector);
            if (method != null)
            {
                if (EnablePhase3LookupDebug)
                {
                    Console.WriteLine($"[LOOKUP]   ✓ Found #{selector.Value} at depth {depth}");
                }
                return method;
            }
            
            // Get the superclass
            var nextClass = GetSuperclassOf(currentClass);
            
            // SPECIAL CASE: If superclass is nil and we haven't sidestepped yet,
            // sidestep to the class side (Object -> Object class)
            if (nextClass == null && !hasSidestepped)
            {
                if (EnablePhase3LookupDebug)
                {
                    Console.WriteLine($"[LOOKUP]   Reached top of hierarchy (superclass=nil), sidestepping to class side");
                    Console.WriteLine($"[LOOKUP]   Current class OOP {currentClass.Oop}, ClassPointer: {currentClass.ClassPointer?.Oop.ToString() ?? "null"}");
                }
                
                // The current class should be Object (or a root class)
                // Get its class (metaclass) and continue from there
                var metaclass = currentClass.ClassPointer;
                if (metaclass != null)
                {
                    if (EnablePhase3LookupDebug)
                    {
                        var metaclassName = TryGetClassName(metaclass);
                        Console.WriteLine($"[LOOKUP]   Sidestepping to {metaclassName ?? $"OOP {metaclass.Oop}"}");
                    }
                    nextClass = metaclass;
                    hasSidestepped = true;
                }
                else
                {
                    if (EnablePhase3LookupDebug)
                    {
                        Console.WriteLine($"[LOOKUP]   ERROR: Cannot sidestep - current class has no ClassPointer!");
                    }
                }
            }
            
            currentClass = nextClass;
            depth++;
        }
        
        if (EnablePhase3LookupDebug)
        {
            Console.WriteLine($"[LOOKUP]   ✗ #{selector.Value} not found in hierarchy");
        }
        
        return null;
    }
    
    /// <summary>
    /// Try to get the class name for debugging purposes.
    /// </summary>
    private string? TryGetClassName(SmalltalkObject classObj)
    {
        // Try to get the name from slot 5 (for Class objects)
        // But ClassDescription and Behavior don't have name at slot 5
        // For those, we'll use hardcoded OOP mappings or other heuristics
        
        // Known OOP mappings (based on our bootstrap)
        if (classObj.Oop == 0) return "Object";
        if (classObj.Oop == 2) return "Behavior";  
        if (classObj.Oop == 4) return "ClassDescription";
        if (classObj.Oop == 6) return "Class";
        if (classObj.Oop == 8) return "Metaclass";
        
        // Try slot 5 for Class-like objects
        if (classObj.Fields != null && classObj.Fields.Length > 5)
        {
            var nameField = classObj.Fields[5];
            if (nameField is Symbol sym) return sym.Name ?? sym.Value;
            if (nameField is string str) return str;
            if (nameField is SmalltalkObject nameObj)
            {
                if (nameObj.TryGetStringValue(out var name))
                    return name;
            }
        }
        
        // For Behavior and ClassDescription, they don't have a name field
        // Try to identify them by their position in hierarchy
        if (classObj.Fields != null && classObj.Fields.Length >= 3)
        {
            // Behavior has [superclass, methodDict, format]
            // ClassDescription has [superclass, methodDict, format, instanceVariables, organization]
            if (classObj.Fields.Length == 3) return "Behavior?";
            if (classObj.Fields.Length == 5) return "ClassDescription?";
        }
        
        return null;
    }
    
    /// <summary>
    /// Looks up a selector in a specific class's method dictionary.
    /// </summary>
    private CompiledMethod? LookupInMethodDictionary(SmalltalkObject classObj, Symbol selector)
    {
        // Method dictionary is typically at index 1 for Behavior-like objects
        // Format: [superclass, methodDict, format, ...]
        const int METHOD_DICT_INDEX = 1;
        
        if (classObj.Fields == null || classObj.Fields.Length <= METHOD_DICT_INDEX)
        {
            if (EnablePhase3LookupDebug)
            {
                Console.WriteLine($"[LOOKUP]     No fields or not enough fields in class OOP {classObj.Oop}");
            }
            return null;
        }
            
        var methodDictObj = classObj.Fields[METHOD_DICT_INDEX];
        if (methodDictObj is not SmalltalkObject methodDict)
        {
            if (EnablePhase3LookupDebug)
            {
                Console.WriteLine($"[LOOKUP]     Method dict slot is not a SmalltalkObject, it's: {methodDictObj?.GetType()?.Name ?? "null"}");
            }
            return null;
        }
        
        // MethodDictionary format:
        // Field 0: tally (number of entries)
        // Fields 1..n/2: keys (selectors)
        // Fields n/2+1..n: values (compiled methods)
        
        if (methodDict.Fields == null || methodDict.Fields.Length < 2)
        {
            if (EnablePhase3LookupDebug)
            {
                Console.WriteLine($"[LOOKUP]     Method dict has no fields or too few fields: {methodDict.Fields?.Length ?? 0}");
            }
            return null;
        }
            
        // Tally might be stored as a SmalltalkObject (SmallInteger) or as int
        int tally = 0;
        if (methodDict.Fields[0] is int tallyInt)
        {
            // If it's an int, it should be a SmallInteger (tagged with bit 0 = 1)
            if ((tallyInt & 1) == 1)
            {
                // It's a tagged SmallInteger, extract the value
                tally = tallyInt >> 1;
            }
            else
            {
                // ERROR: Tally should ALWAYS be a SmallInteger!
                // This indicates a bug in the bootstrap
                if (EnablePhase3LookupDebug)
                {
                    Console.WriteLine($"[LOOKUP]     ERROR: Tally is not a SmallInteger! Value: {tallyInt}");
                }
                tally = tallyInt; // Use it anyway for debugging
            }
        }
        else if (methodDict.Fields[0] is SmalltalkObject tallyObj)
        {
            // SmallIntegers are stored with OOP as (value << 1) | 1
            // So to extract the value, we shift right by 1
            if (tallyObj.Oop % 2 == 1)  // SmallInteger tagged with lowest bit = 1
            {
                tally = tallyObj.Oop >> 1;
            }
            else
            {
                // Not a SmallInteger - might be stored differently
                // Try to extract value from fields
                if (tallyObj.Fields != null && tallyObj.Fields.Length > 0 && tallyObj.Fields[0] is int val)
                {
                    tally = val;
                }
                else
                {
                    // As a last resort, assume the OOP itself might be the value
                    // (this shouldn't happen for SmallIntegers but helps debugging)
                    if (EnablePhase3LookupDebug)
                    {
                        Console.WriteLine($"[LOOKUP]       Warning: Tally object OOP {tallyObj.Oop} is not a SmallInteger");
                    }
                }
            }
        }
        
        if (EnablePhase3LookupDebug && methodDict.Fields[0] != null)
        {
            var tallyDebugStr = methodDict.Fields[0]?.ToString() ?? "null";
            if (methodDict.Fields[0] is SmalltalkObject to)
            {
                tallyDebugStr = $"SmalltalkObject OOP={to.Oop}";
            }
            Console.WriteLine($"[LOOKUP]       Tally field type: {methodDict.Fields[0]?.GetType().Name ?? "null"}, value: {tallyDebugStr}, extracted tally: {tally}");
        }
        
        // Handle negative tally (shouldn't happen but helps debugging)
        if (tally < 0)
        {
            if (EnablePhase3LookupDebug)
            {
                Console.WriteLine($"[LOOKUP]     WARNING: Method dict has negative tally {tally}, treating as positive");
            }
            tally = Math.Abs(tally);
        }
        
        if (tally == 0)
        {
            if (EnablePhase3LookupDebug)
            {
                Console.WriteLine($"[LOOKUP]     Method dict tally is 0 (empty dictionary)");
            }
            return null;
        }
        
        if (EnablePhase3LookupDebug)
        {
            Console.WriteLine($"[LOOKUP]     Method dict has {tally} entries, searching for #{selector.Value}");
        }
            
        // Use flat format: [tally, key1, value1, key2, value2, ...] (like the pretty-printer)
        // Start at index 1 to skip tally, increment by 2
        int entryIndex = 0;
        for (int i = 1; i < methodDict.Fields.Length - 1; i += 2)
        {
            var key = methodDict.Fields[i];
            var value = methodDict.Fields[i + 1];
            
            if (EnablePhase3LookupDebug && entryIndex < 3) // Only show first few keys to avoid spam
            {
                if (key is Symbol keySymbol)
                {
                    Console.WriteLine($"[LOOKUP]       Entry {entryIndex}: #{keySymbol.Value}");
                }
                else if (key is SmalltalkObject keyObj)
                {
                    Console.WriteLine($"[LOOKUP]       Entry {entryIndex}: SmalltalkObject OOP={keyObj.Oop}");
                }
                else if (key != null)
                {
                    Console.WriteLine($"[LOOKUP]       Entry {entryIndex}: {key.GetType().Name} = {key}");
                }
            }
            
            // Skip nil entries
            if (key == null || value == null)
            {
                entryIndex++;
                continue;
            }
            
            // Check if this key matches our selector
            bool isMatch = false;
            
            // Case 1: C# Symbol object (from Phase 1/2 bootstrap)
            if (key is Symbol keySymbol2 && keySymbol2.Equals(selector))
            {
                isMatch = true;
            }
            // Case 2: SmalltalkObject representing a Symbol (from Phase 3 bytecode execution)
            else if (key is SmalltalkObject keyObj)
            {
                // Try to extract the selector name from the SmalltalkObject Symbol
                if (keyObj.TryGetStringValue(out var keyString))
                {
                    if (EnablePhase3LookupDebug && entryIndex < 3)
                    {
                        Console.WriteLine($"[LOOKUP]         Extracted string from Symbol OOP {keyObj.Oop}: '{keyString}' (looking for '{selector.Value}')");
                    }
                    
                    if (keyString == selector.Value)
                    {
                        isMatch = true;
                    }
                }
                else
                {
                    if (EnablePhase3LookupDebug && entryIndex < 3)
                    {
                        Console.WriteLine($"[LOOKUP]         Could not extract string from Symbol OOP {keyObj.Oop}, trying field inspection...");
                        // Let's inspect the fields directly
                        for (int f = 0; f < Math.Min(keyObj.Fields.Length, 5); f++)
                        {
                            var field = keyObj.Fields[f];
                            Console.WriteLine($"[LOOKUP]           Field {f}: {field?.GetType()?.Name ?? "null"} = {field}");
                        }
                    }
                }
            }
            // Case 3: OOP reference that needs to be resolved
            else if (key is int keyOop && keyOop > 0)
            {
                var keyObject = ObjectMemory.FromOop(keyOop);
                if (keyObject != null && keyObject.TryGetStringValue(out var keyString) && keyString == selector.Value)
                {
                    isMatch = true;
                }
            }
            
            if (isMatch)
            {
                // Found it! Get the corresponding value
                if (value is CompiledMethod compiledMethod)
                    return compiledMethod;
                    
                // CompiledMethod might be stored as a SmalltalkObject
                if (value is SmalltalkObject methodObj)
                {
                    // Convert SmalltalkObject to CompiledMethod
                    return ConvertToCompiledMethod(methodObj);
                }
                    
                // Value might be stored as OOP reference - need to resolve it
                if (value is int oop && oop >= 0)
                {
                    var obj = ObjectMemory.FromOop(oop);
                    if (obj != null)
                        return ConvertToCompiledMethod(obj);
                }
                
                return null;
            }
            
            entryIndex++;
        }
        
        return null;
    }
    
    /// <summary>
    /// Converts a SmalltalkObject representing a CompiledMethod to a CompiledMethod instance.
    /// </summary>
    private CompiledMethod? ConvertToCompiledMethod(SmalltalkObject methodObj)
    {
        // CompiledMethod object format:
        // Field 0: header (contains temp count, literal count, etc.)
        // Field 1: bytecodes (ByteArray)
        // Field 2: literals (Array)
        // Field 3: selector (optional)
        
        if (methodObj.Fields == null || methodObj.Fields.Length < 3)
            return null;
            
        // Extract header from field[3] (CompiledMethod structure: [selector, bytecode, literals, header])
        int headerValue = 0;
        int primitiveIndex = 0;
        int tempCount = 0;
        
        // Debug for instVarAt:put:
        if (methodObj.Fields.Length > 0 && methodObj.Fields[0] is SmalltalkObject selObj)
        {
            if (selObj.TryGetStringValue(out var selName) && selName == "instVarAt:put:")
            {
                Console.WriteLine($"[DEBUG] ConvertToCompiledMethod for instVarAt:put:");
                Console.WriteLine($"[DEBUG]   Fields.Length: {methodObj.Fields.Length}");
                for (int i = 0; i < methodObj.Fields.Length; i++)
                {
                    var field = methodObj.Fields[i];
                    Console.WriteLine($"[DEBUG]   Field[{i}]: {field?.GetType()?.Name ?? "null"} = {field}");
                }
            }
        }
        
        if (methodObj.Fields.Length > 3)
        {
            var header = methodObj.Fields[3];
            if (header is int h)
            {
                // If it's an int, it might be a SmallInteger OOP (tagged with bit 0 = 1)
                if ((h & 1) == 1)
                {
                    // It's a tagged SmallInteger, extract the value
                    headerValue = h >> 1;
                }
                else
                {
                    // It's a raw int value
                    headerValue = h;
                }
            }
            else if (header is SmalltalkObject headerObj && headerObj.IsSmallInteger)
            {
                headerValue = headerObj.SmallIntegerValue;
            }
            
            // Blue Book header format:
            // Bits 0-8: Primitive number (0-511)
            // Bit 9: Large context flag
            // Bits 10-15: Number of temporaries
            primitiveIndex = headerValue & 0x1FF;  // Lower 9 bits
            bool largeContext = (headerValue & 0x200) != 0;  // Bit 9
            tempCount = (headerValue >> 10) & 0x3F;  // Bits 10-15
        }
        
        // Extract bytecodes
        byte[]? bytecodes = null;
        if (methodObj.Fields[1] is SmalltalkObject bytecodeArray && bytecodeArray.Fields != null)
        {
            bytecodes = new byte[bytecodeArray.Fields.Length];
            for (int i = 0; i < bytecodeArray.Fields.Length; i++)
            {
                if (bytecodeArray.Fields[i] is int b)
                    bytecodes[i] = (byte)b;
                else if (bytecodeArray.Fields[i] is byte bb)
                    bytecodes[i] = bb;
            }
        }
        
        // Extract literals
        object[]? literals = null;
        if (methodObj.Fields[2] is SmalltalkObject literalArray && literalArray.Fields != null)
        {
            literals = new object[literalArray.Fields.Length];
            Array.Copy(literalArray.Fields, literals, literalArray.Fields.Length);
        }
        
        // Extract selector from field[0]
        string? selectorName = null;
        if (methodObj.Fields.Length > 0)
        {
            if (methodObj.Fields[0] is Symbol sel)
            {
                selectorName = sel.Value;
            }
            else if (methodObj.Fields[0] is SmalltalkObject selectorObj)
            {
                // Try to extract string from Symbol SmalltalkObject
                if (selectorObj.TryGetStringValue(out var name))
                {
                    selectorName = name;
                }
            }
        }
        
        if (bytecodes == null || literals == null)
            return null;
            
        return new CompiledMethod
        {
            Bytecodes = bytecodes,
            Literals = literals,
            TemporariesCount = tempCount,
            SelectorName = selectorName,
            HasPrimitive = primitiveIndex > 0,
            PrimitiveIndex = primitiveIndex
        };
    }
    
    /// <summary>
    /// Gets the superclass of a class object.
    /// </summary>
    private SmalltalkObject? GetSuperclassOf(SmalltalkObject classObj)
    {
        // Superclass is typically at index 0 for Behavior-like objects
        const int SUPERCLASS_INDEX = 0;
        
        if (classObj.Fields == null || classObj.Fields.Length <= SUPERCLASS_INDEX)
            return null;
            
        var superclass = classObj.Fields[SUPERCLASS_INDEX];
        
        if (superclass is SmalltalkObject superclassObj)
            return superclassObj;
            
        // Might be stored as OOP
        if (superclass is int oop && oop >= 0)
            return ObjectMemory.FromOop(oop);
            
        return null;
    }

    /// <summary>
    ///     STUB: Gets superclass - simplified for shadow class removal.  
    /// </summary>
    public SmalltalkObject? Superclass => ClassPointer; // Simplified stub

    /// <summary>
    ///     STUB: Instance variables list - simplified for shadow class removal.
    /// </summary>
    public List<string> InstanceVariables { get; } = new();

    /// <summary>
    ///     STUB: Class variables list - simplified for shadow class removal.
    /// </summary>
    public List<string> ClassVariables { get; } = new();

    /// <summary>
    ///     STUB: Method dictionary - simplified for shadow class removal.
    /// </summary>
    public Dictionary<Symbol, CompiledMethod> MethodDictionary { get; } = new();

    /// <summary>
    ///     STUB: Metaclass reference - simplified for shadow class removal.
    /// </summary>
    public SmalltalkObject? Metaclass { get; set; }
    
    /// <summary>
    /// Checks if this object appears to be a SmallInteger.
    /// </summary>
    public bool IsSmallInteger => false; // TODO: Implement proper SmallInteger check
    
    /// <summary>
    /// Gets the SmallInteger value if this is a SmallInteger.
    /// </summary>
    public int SmallIntegerValue => 0; // TODO: Implement proper SmallInteger value extraction
    
    /// <summary>
    /// Tries to get a field value by name.
    /// </summary>
    public bool TryGetField(string fieldName, out object? value)
    {
        // For now, handle known field indices based on class structure
        // This is a simplified implementation - a full one would use the class's instance variable names
        
        // Common field mappings based on Smalltalk-80 class structures:
        switch (fieldName)
        {
            case "name" when Fields.Length > 5:
                // Class objects store name at field 5
                value = Fields[5];
                return value != null;
                
            case "superclass" when Fields.Length > 0:
                // Behavior stores superclass at field 0
                value = Fields[0];
                return value != null;
                
            case "methodDict" when Fields.Length > 1:
                // Behavior stores method dictionary at field 1
                value = Fields[1];
                return value != null;
                
            case "format" when Fields.Length > 2:
                // Behavior stores format at field 2
                value = Fields[2];
                return value != null;
                
            default:
                value = null;
                return false;
        }
    }
    
    /// <summary>
    /// Tries to get the string value if this object represents a string.
    /// </summary>
    public bool TryGetStringValue(out string value)
    {
        // First, try the most direct approach - check if field[0] is a string
        // This is often the case for symbols in the method dictionaries
        if (Fields.Length > 0 && Fields[0] is string directString && !string.IsNullOrEmpty(directString))
        {
            value = directString;
            return true;
        }
        
        // Check if this is a String or Symbol object by examining the class
        if (ClassPointer != null)
        {
            // Try to get the class name to see if it's String or Symbol
            string? className = GetClassNameForDebugging();
            
            // For Phase 3 symbols created by the VM, we expect them to be String/Symbol class objects
            // In the real Smalltalk-80 system, Symbols and Strings store character data differently:
            // - They are variable-sized objects where the character data is stored in indexed fields
            // - Each character is typically stored as a SmallInteger
            
            if (className == "String" || className == "Symbol")
            {
                // Extract character data from the fields
                // In Smalltalk-80, variable-sized objects store their data after named instance variables
                var chars = new System.Text.StringBuilder();
                
                // For Symbols and Strings, character data starts from field index 0 for the string data
                // (since Symbol inherits from String and String has no instance variables)
                foreach (var field in Fields)
                {
                    if (field is int charValue)
                    {
                        // Check if it's a SmallInteger (tagged with lowest bit = 1)
                        if (IsSmallIntegerValue(charValue))
                        {
                            int actualValue = ExtractSmallIntegerValue(charValue);
                            if (actualValue >= 0 && actualValue <= 255)
                            {
                                chars.Append((char)actualValue);
                            }
                        }
                        else if (charValue >= 0 && charValue <= 255)
                        {
                            // Direct character code (untagged)
                            chars.Append((char)charValue);
                        }
                    }
                    else if (field is char c)
                    {
                        // Direct character
                        chars.Append(c);
                    }
                    else if (field is byte b)
                    {
                        // Byte value
                        chars.Append((char)b);
                    }
                }
                
                if (chars.Length > 0)
                {
                    value = chars.ToString();
                    return true;
                }
            }
            
            // Also check if the class name itself indicates this might be a string-like object
            if (className?.Contains("String") == true || className?.Contains("Symbol") == true)
            {
                // Try generic field extraction for debugging
                var chars = new System.Text.StringBuilder();
                foreach (var field in Fields)
                {
                    if (field is string s)
                    {
                        chars.Append(s);
                    }
                    else if (field is int i && i >= 32 && i <= 126)
                    {
                        chars.Append((char)i);
                    }
                }
                
                if (chars.Length > 0)
                {
                    value = chars.ToString();
                    return true;
                }
            }
        }
        
        // Fallback: try to extract directly from all fields without class info  
        // This handles cases where the symbol might not have proper class setup
        if (Fields.Length > 0)
        {
            var chars = new System.Text.StringBuilder();
            foreach (var field in Fields)
            {
                if (field is string s)
                {
                    chars.Append(s);
                }
                else if (field is char c)
                {
                    chars.Append(c);
                }
                else if (field is int i && i >= 32 && i <= 255)
                {
                    // Check for both tagged and untagged character values
                    if (IsSmallIntegerValue(i))
                    {
                        int actualValue = ExtractSmallIntegerValue(i);
                        if (actualValue >= 32 && actualValue <= 255)
                        {
                            chars.Append((char)actualValue);
                        }
                    }
                    else if (i >= 32 && i <= 255)
                    {
                        chars.Append((char)i);
                    }
                }
                else if (field is byte b && b >= 32)
                {
                    chars.Append((char)b);
                }
            }
            
            if (chars.Length > 0)
            {
                value = chars.ToString();
                return true;
            }
        }
        
        value = string.Empty;
        return false;
    }
    
    /// <summary>
    /// Gets a string representation of the class name for debugging.
    /// </summary>
    private string? GetClassNameForDebugging()
    {
        if (ClassPointer?.TryGetField("name", out var nameObj) == true)
        {
            if (nameObj is Symbol sym) return sym.Name;
            if (nameObj is string str) return str;
            // Avoid recursive call - just check if it's a SmalltalkObject with simple field access
            if (nameObj is SmalltalkObject nameStrObj)
            {
                // Try simple field extraction without recursion
                foreach (var field in nameStrObj.Fields)
                {
                    if (field is string s && !string.IsNullOrEmpty(s)) return s;
                    if (field is char c) return c.ToString();
                }
            }
        }
        return null;
    }
    
    /// <summary>
    /// Checks if a value represents a SmallInteger (tagged with lowest bit = 1).
    /// </summary>
    private static bool IsSmallIntegerValue(int value) => (value & 1) == 1;
    
    /// <summary>
    /// Extracts the actual integer value from a tagged SmallInteger.
    /// </summary>
    private static int ExtractSmallIntegerValue(int taggedValue) => taggedValue >> 1;
}