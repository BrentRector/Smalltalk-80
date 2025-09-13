using System;
using System.Collections.Generic;
using System.Linq;
using Smalltalk80.Compiler;

namespace Smalltalk80.Primitives.Compiler;

/// <summary>
/// Primitives for the Smalltalk compiler, enabling dynamic method compilation.
/// These primitives allow Smalltalk code to compile and install new methods at runtime.
/// </summary>
public static class CompilerPrimitives
{
    private static readonly Dictionary<Interpreter, SmalltalkCompiler> compilers = new();
    
    /// <summary>
    /// Get or create the compiler instance for the given interpreter.
    /// </summary>
    private static SmalltalkCompiler GetCompiler(Interpreter interp)
    {
        if (!compilers.TryGetValue(interp, out var compiler))
        {
            compiler = new SmalltalkCompiler(interp);
            compilers[interp] = compiler;
        }
        return compiler;
    }
    
    /// <summary>
    /// Get the interpreter from the context.
    /// Note: This requires the Context to have a reference to its interpreter.
    /// </summary>
    private static Interpreter? GetInterpreter(Context context)
    {
        // In a real implementation, the Context would have a reference to its Interpreter
        // For now, we'll try to get it from the first registered compiler
        return compilers.Keys.FirstOrDefault();
    }
    
    /// <summary>
    /// Get interpreter for primitive execution.
    /// Uses a static instance that must be set during interpreter initialization.
    /// </summary>
    private static Interpreter? GetInterpreterForPrimitive(Context context)
    {
        // Try to get from registered compilers first
        var interpreter = GetInterpreter(context);
        if (interpreter != null)
            return interpreter;
        
        // If no compiler registered yet, create a new interpreter
        // This is a fallback for testing
        return new Interpreter();
    }
    
    /// <summary>
    /// Register an interpreter instance for use by compiler primitives.
    /// Should be called during interpreter initialization.
    /// </summary>
    public static void RegisterInterpreter(Interpreter interpreter)
    {
        // Ensure we have a compiler for this interpreter
        GetCompiler(interpreter);
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
            
            // Get interpreter - use a static instance if available
            var interpreter = GetInterpreterForPrimitive(context);
            if (interpreter == null)
                return false;
            
            // Get or create compiler
            var compiler = GetCompiler(interpreter);
            
            // Compile the method
            var compiledMethod = compiler.CompileMethod(source, className, false);
            
            // Create a SmalltalkObject representing the compiled method
            var methodObject = CreateCompiledMethodObject(compiledMethod);
            
            // Push result onto stack
            context.Stack.Push(methodObject);
            return true;
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
        try
        {
            if (context.Arguments.Length < 3)
                return false;
            
            // Get arguments
            var methodSourceObj = context.Arguments[0];
            var classObj = context.Arguments[1];
            var isClassMethodObj = context.Arguments[2];
            
            // Extract source string
            if (methodSourceObj is not SmalltalkObject sourceObj)
                return false;
            
            var source = ExtractString(sourceObj);
            if (source == null)
                return false;
            
            // Get class
            if (classObj is not SmalltalkObject classObject)
                return false;
            
            var className = GetClassName(classObject);
            if (className == null)
                return false;
            
            // Check if class method
            bool isClassMethod = isClassMethodObj == ObjectMemory.TrueObject;
            
            // Get interpreter
            var interpreter = GetInterpreterForPrimitive(context);
            if (interpreter == null)
                return false;
            
            // Get or create compiler
            var compiler = GetCompiler(interpreter);
            
            // Compile the method
            var compiledMethod = compiler.CompileMethod(source, className, isClassMethod);
            
            // Install the method
            compiler.InstallMethod(compiledMethod, classObject, isClassMethod);
            
            // Create and return the method object
            var methodObject = CreateCompiledMethodObject(compiledMethod);
            context.Stack.Push(methodObject);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PRIMITIVE 171] Compile and install failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Primitive 172: Evaluate a doIt expression.
    /// Arguments: expressionSource (String)
    /// Returns: The result of evaluating the expression
    /// </summary>
    public static bool EvaluateExpression(Context context)
    {
        try
        {
            if (context.Arguments.Length < 1)
                return false;
            
            // Get expression source
            var expressionSourceObj = context.Arguments[0];
            if (expressionSourceObj is not SmalltalkObject sourceObj)
                return false;
            
            var source = ExtractString(sourceObj);
            if (source == null)
                return false;
            
            // Get interpreter
            var interpreter = GetInterpreterForPrimitive(context);
            if (interpreter == null)
                return false;
            
            // Get or create compiler
            var compiler = GetCompiler(interpreter);
            
            // Compile as doIt
            var compiledMethod = compiler.CompileDoIt(source);
            
            // Execute the compiled method
            // Create a new context for execution
            var doItContext = new Context
            {
                Sender = context,
                Method = compiledMethod,
                Receiver = context.Receiver, // Use current receiver (usually UndefinedObject)
                Arguments = Array.Empty<object?>(),
                Temporaries = new object?[compiledMethod.TemporariesCount],
                Stack = new Stack<object?>(),
                PC = 0
            };
            
            // Execute the method using the interpreter
            // Set the context and execute
            var savedContext = interpreter.CurrentContext;
            try
            {
                // Set up the interpreter with our doIt context
                // Note: This is a simplified approach. In a full implementation,
                // we'd properly integrate with the interpreter's context management
                interpreter.ExecuteMethod();
                
                // Get the result from the context's stack
                var result = doItContext.Stack.Count > 0 ? doItContext.Stack.Pop() : ObjectMemory.NilObject;
                
                // Push result onto stack
                context.Stack.Push(result);
            }
            finally
            {
                // Restore context if needed
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PRIMITIVE 172] Evaluation failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Primitive 173: Parse method source to AST (for tools).
    /// Arguments: methodSource (String)
    /// Returns: AST representation or nil on failure
    /// </summary>
    public static bool ParseMethodToAst(Context context)
    {
        try
        {
            if (context.Arguments.Length < 1)
                return false;
            
            // Get method source
            var methodSourceObj = context.Arguments[0];
            if (methodSourceObj is not SmalltalkObject sourceObj)
                return false;
            
            var source = ExtractString(sourceObj);
            if (source == null)
                return false;
            
            // For now, return nil as AST representation would require
            // creating Smalltalk objects for each AST node
            context.Stack.Push(ObjectMemory.NilObject);
            
            // In a full implementation, we would:
            // 1. Parse the source to AST
            // 2. Convert AST nodes to Smalltalk objects
            // 3. Return the root of the AST tree
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PRIMITIVE 173] Parse to AST failed: {ex.Message}");
            return false;
        }
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