using System;

/// <summary>
///     Represents a generic Smalltalk object instance.
/// </summary>
public class SmalltalkObject
{
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
    /// </summary>
    public CompiledMethod? Lookup(Symbol selector)
    {
        // Start with this class and walk up the superclass chain
        var currentClass = this;
        
        while (currentClass != null)
        {
            // Look in the current class's method dictionary
            var method = LookupInMethodDictionary(currentClass, selector);
            if (method != null)
                return method;
            
            // Move to superclass
            currentClass = GetSuperclassOf(currentClass);
        }
        
        // Method not found in the entire hierarchy
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
            return null;
            
        var methodDictObj = classObj.Fields[METHOD_DICT_INDEX];
        if (methodDictObj is not SmalltalkObject methodDict)
            return null;
        
        // MethodDictionary format:
        // Field 0: tally (number of entries)
        // Fields 1..n/2: keys (selectors)
        // Fields n/2+1..n: values (compiled methods)
        
        if (methodDict.Fields == null || methodDict.Fields.Length < 2)
            return null;
            
        var tally = methodDict.Fields[0] as int? ?? 0;
        if (tally == 0)
            return null;
            
        // The dictionary is split in half - first half is keys, second half is values
        var halfSize = (methodDict.Fields.Length - 1) / 2;
        var keysStart = 1;
        var valuesStart = keysStart + halfSize;
        
        // Search for the selector in the keys
        for (int i = 0; i < halfSize; i++)
        {
            var key = methodDict.Fields[keysStart + i];
            
            // Check if this key matches our selector
            if (key is Symbol keySymbol && keySymbol.Equals(selector))
            {
                // Found it! Get the corresponding value
                var value = methodDict.Fields[valuesStart + i];
                
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
            
        // Extract header
        var header = methodObj.Fields[0];
        int headerValue = 0;
        if (header is int h)
            headerValue = h;
        else if (header is SmalltalkObject headerObj && headerObj.IsSmallInteger)
            headerValue = headerObj.SmallIntegerValue;
            
        // Parse header to get counts
        var literalCount = (headerValue >> 9) & 0x3F;
        var tempCount = (headerValue >> 3) & 0x1F;
        var flagValue = headerValue & 0x7;
        
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
        
        // Extract selector if present
        string? selectorName = null;
        if (methodObj.Fields.Length > 3 && methodObj.Fields[3] is Symbol sel)
        {
            selectorName = sel.Value;
        }
        
        if (bytecodes == null || literals == null)
            return null;
            
        return new CompiledMethod
        {
            Bytecodes = bytecodes,
            Literals = literals,
            TemporariesCount = tempCount,
            SelectorName = selectorName
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
                
                value = chars.ToString();
                return value.Length > 0;
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