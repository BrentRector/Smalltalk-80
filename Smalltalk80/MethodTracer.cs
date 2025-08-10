using System;

/// <summary>
/// Provides method execution tracing for the Smalltalk interpreter.
/// </summary>
public static class MethodTracer
{
    /// <summary>
    /// Gets or sets whether method entry/exit tracing is enabled.
    /// </summary>
    public static bool Enabled { get; set; } = false;
    
    /// <summary>
    /// Gets or sets whether individual bytecode tracing is enabled.
    /// </summary>
    public static bool BytecodeTracingEnabled { get; set; } = false;
    
    private static int callDepth = 0;
    private static string GetIndent() => new string(' ', callDepth * 2);
    
    /// <summary>
    /// Logs method entry.
    /// </summary>
    public static void LogMethodEntry(string className, string selector, object? receiver)
    {
        if (!Enabled) return;
        
        Console.WriteLine($"[METHOD] {GetIndent()}>> Entering {className}#{selector} (receiver: {DescribeObject(receiver)})");
        callDepth++;
    }
    
    /// <summary>
    /// Logs method exit.
    /// </summary>
    public static void LogMethodExit(string className, string selector, object? returnValue)
    {
        if (!Enabled) return;
        
        callDepth = Math.Max(0, callDepth - 1);
        Console.WriteLine($"[METHOD] {GetIndent()}<< Exiting {className}#{selector} (returns: {DescribeObject(returnValue)})");
    }
    
    /// <summary>
    /// Logs bytecode execution.
    /// </summary>
    public static void LogBytecode(Bytecode bytecode, int pc, Context context)
    {
        if (!BytecodeTracingEnabled) return;
        
        var stackInfo = context.Stack.Count > 0 
            ? $"Stack top: {DescribeObject(context.Stack.Peek())}" 
            : "Stack empty";
            
        Console.WriteLine($"[BYTECODE] {GetIndent()}  PC:{pc:D3} {bytecode,-30} | {stackInfo}");
    }
    
    private static string DescribeObject(object? obj)
    {
        if (obj == null) return "nil";
        if (obj is int i) return $"SmallInteger({i})";
        if (obj is bool b) return b ? "true" : "false";
        if (obj is string s) return $"\"{s}\"";
        if (obj is Symbol sym) return $"#{sym.Name}";
        if (obj is SmalltalkObject so)
        {
            // Get the class name for this object
            string className = GetObjectClassName(so);
            
            // For class objects themselves, show as "Class" 
            if (IsClassObject(so))
            {
                return $"a {className}";
            }
            
            // For regular objects, show as "a ClassName"
            return $"a {className}";
        }
        return obj.GetType().Name;
    }
    
    /// <summary>
    /// Gets the class name for a Smalltalk object.
    /// </summary>
    private static string GetObjectClassName(SmalltalkObject obj)
    {
        if (obj.ClassPointer != null)
        {
            // Use the improved class name lookup from Interpreter
            string? className = GetClassNameFromOOP(obj.ClassPointer);
            if (!string.IsNullOrEmpty(className))
                return className;
            
            // Try to get the class name from the class's name field
            if (obj.ClassPointer.TryGetField("name", out var nameObj))
            {
                // Handle different ways the name might be stored
                if (nameObj is Symbol nameSym) 
                    return nameSym.Name;
                if (nameObj is string str) 
                    return str;
                if (nameObj is SmalltalkObject nameStrObj && nameStrObj.TryGetStringValue(out var nameStr))
                    return nameStr;
            }
        }
        
        // Fallback - try to infer from OOP patterns or return generic name
        return "Object";
    }
    
    /// <summary>
    /// Gets class name from OOP using similar logic to Interpreter.GetClassName
    /// </summary>
    private static string? GetClassNameFromOOP(SmalltalkObject classObj)
    {
        // Use same OOP mapping as the Interpreter
        var commonClasses = new Dictionary<string, int[]>
        {
            {"Array", new[] {16, 18}},
            {"String", new[] {14, 20}},
            {"Symbol", new[] {12, 22}},
            {"Object", new[] {2, 26}},
            {"Class", new[] {4, 28}},
            {"Metaclass", new[] {6, 30}},
            {"ByteArray", new[] {18, 24}},
            {"MethodDictionary", new[] {10, 32}}
        };
        
        foreach (var kvp in commonClasses)
        {
            foreach (var oop in kvp.Value)
            {
                if (classObj.Oop == oop)
                    return kvp.Key;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Determines if a SmalltalkObject represents a class object.
    /// </summary>
    private static bool IsClassObject(SmalltalkObject obj)
    {
        // Heuristic: class objects typically have specific field patterns
        // In Smalltalk-80, classes have fields like name, superclass, methodDict, etc.
        if (obj.Fields.Length >= 6)
        {
            // Check if this looks like a class by having typical class fields
            // Field 5 is typically the name field in class objects
            if (obj.Fields[5] != null)
                return true;
        }
        return false;
    }
}