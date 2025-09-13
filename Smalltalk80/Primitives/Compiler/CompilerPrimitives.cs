using System;
using Smalltalk80.Compiler;

namespace Smalltalk80.Primitives.Compiler;

/// <summary>
/// Primitives for the Smalltalk compiler, enabling dynamic method compilation.
/// These primitives allow Smalltalk code to compile and install new methods at runtime.
/// </summary>
public static class CompilerPrimitives
{
    private static SmalltalkCompiler? compiler;
    private static Interpreter? interpreter;
    
    /// <summary>
    /// Get or create the compiler instance.
    /// </summary>
    private static SmalltalkCompiler GetCompiler(Interpreter interp)
    {
        interpreter = interp;
        compiler ??= new SmalltalkCompiler(interp);
        return compiler;
    }
    
    /// <summary>
    /// Primitive 170: Compile a method source string.
    /// Arguments: methodSource (String), class (Class)
    /// Returns: CompiledMethod or nil on failure
    /// </summary>
    public static bool CompileMethod(Context context)
    {
        try
        {
            if (context.Arguments.Length < 2)
                return false;
            
            // Get arguments
            var methodSourceObj = context.Arguments[0];
            var classObj = context.Arguments[1];
            
            // Extract source string
            if (methodSourceObj is not SmalltalkObject sourceObj)
                return false;
            
            var source = ExtractString(sourceObj);
            if (source == null)
                return false;
            
            // Get class name
            var className = GetClassName(classObj as SmalltalkObject);
            if (className == null)
                return false;
            
            // We need an interpreter instance - for now return false
            // In a real implementation, we'd get this from somewhere
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PRIMITIVE 170] Compilation failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Primitive 171: Compile and install a method.
    /// Arguments: methodSource (String), class (Class), isClassMethod (Boolean)
    /// Returns: CompiledMethod or nil on failure
    /// </summary>
    public static bool CompileAndInstallMethod(Context context)
    {
        // Simplified implementation - needs interpreter access
        return false;
    }
    
    /// <summary>
    /// Primitive 172: Evaluate a doIt expression.
    /// Arguments: expressionSource (String)
    /// Returns: The result of evaluating the expression
    /// </summary>
    public static bool EvaluateExpression(Context context)
    {
        // Simplified implementation - needs interpreter access
        return false;
    }
    
    /// <summary>
    /// Primitive 173: Parse method source to AST (for tools).
    /// Arguments: methodSource (String)
    /// Returns: AST representation or nil on failure
    /// </summary>
    public static bool ParseMethodToAst(Context context)
    {
        // This would return an AST representation for browser tools
        // For now, not implemented
        return false;
    }
    
    /// <summary>
    /// Extract a string from a Smalltalk String object.
    /// </summary>
    private static string? ExtractString(SmalltalkObject stringObj)
    {
        // Strings are typically ByteArrays or have character data
        if (stringObj.Fields.Length == 0)
            return "";
        
        // Try to extract as byte array
        if (stringObj.Fields[0] is byte[] bytes)
        {
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        
        // Try to extract as character array
        if (stringObj.Fields[0] is char[] chars)
        {
            return new string(chars);
        }
        
        // Try field by field
        var result = "";
        foreach (var field in stringObj.Fields)
        {
            if (field is char c)
                result += c;
            else if (field is byte b)
                result += (char)b;
            else if (field is int i && i >= 0 && i <= 127)
                result += (char)i;
        }
        
        return result.Length > 0 ? result : null;
    }
    
    /// <summary>
    /// Get the class name from a class object.
    /// </summary>
    private static string? GetClassName(SmalltalkObject? classObj)
    {
        if (classObj == null)
            return null;
        
        // Class name is typically in slot 5
        if (classObj.Fields.Length > 5)
        {
            if (classObj.Fields[5] is Symbol symbol)
                return symbol.Value;
            
            if (classObj.Fields[5] is SmalltalkObject nameObj)
            {
                var name = ExtractString(nameObj);
                if (name != null)
                    return name;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Create a SmalltalkObject representing a CompiledMethod.
    /// </summary>
    private static SmalltalkObject CreateCompiledMethodObject(CompiledMethod method)
    {
        // Create method object with 4 fields
        var methodObj = new SmalltalkObject(null, 4);
        
        // Field 0: selector (Symbol)
        methodObj.Fields[0] = new Symbol(method.SelectorName ?? "unknown");
        
        // Field 1: bytecodes (ByteArray)
        var bytecodeArray = new SmalltalkObject(null, method.Bytecodes.Length);
        for (int i = 0; i < method.Bytecodes.Length; i++)
        {
            bytecodeArray.Fields[i] = (int)method.Bytecodes[i];
        }
        methodObj.Fields[1] = bytecodeArray;
        
        // Field 2: literals (Array)
        var literalArray = new SmalltalkObject(null, method.Literals.Length);
        for (int i = 0; i < method.Literals.Length; i++)
        {
            literalArray.Fields[i] = method.Literals[i];
        }
        methodObj.Fields[2] = literalArray;
        
        // Field 3: header (SmallInteger)
        // Use the header from the CompiledMethod
        methodObj.Fields[3] = (method.Header.Raw << 1) | 1; // SmallInteger encoding
        
        // Register in ObjectMemory - get next available OOP
        ObjectMemory.Register(methodObj);
        
        return methodObj;
    }
}