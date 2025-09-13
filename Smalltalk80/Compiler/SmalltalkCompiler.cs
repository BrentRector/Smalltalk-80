using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antlr4.Runtime;
using Smalltalk80.Generated;
using Smalltalk80.Parser;

namespace Smalltalk80.Compiler;

/// <summary>
/// Main Smalltalk compiler that integrates the ANTLR parser with the bytecode compiler.
/// Provides the interface between the VM and the compilation pipeline.
/// </summary>
public class SmalltalkCompiler
{
    private readonly Interpreter interpreter;
    
    public SmalltalkCompiler(Interpreter interpreter)
    {
        this.interpreter = interpreter;
    }
    
    /// <summary>
    /// Compile a method source string into a CompiledMethod object.
    /// </summary>
    /// <param name="source">The Smalltalk method source code.</param>
    /// <param name="className">The class this method belongs to.</param>
    /// <param name="isClassMethod">Whether this is a class-side method.</param>
    /// <returns>A CompiledMethod object ready to be installed.</returns>
    public CompiledMethod CompileMethod(string source, string className, bool isClassMethod = false)
    {
        // Parse the source code
        var methodNode = ParseMethod(source);
        
        // Create compilation context
        var context = new CompilationContext(className, isClassMethod, interpreter);
        
        // Compile to bytecode
        var compiler = new AstToBytecodeCompiler(context);
        return compiler.CompileMethod(methodNode);
    }
    
    /// <summary>
    /// Parse a method source string into an AST.
    /// </summary>
    private MethodNode ParseMethod(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new Smalltalk80Lexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);
        
        // Set up error handling
        parser.RemoveErrorListeners();
        var errorListener = new ThrowingErrorListener();
        parser.AddErrorListener(errorListener);
        
        // Parse as a standalone method
        var tree = parser.methodEntry();
        
        if (parser.NumberOfSyntaxErrors > 0)
        {
            throw new CompilationException($"Syntax error in method: {parser.NumberOfSyntaxErrors} errors found");
        }
        
        // Build AST
        var visitor = new SimpleAstBuildingVisitor();
        var result = visitor.Visit(tree);
        
        if (result is not MethodNode methodNode)
        {
            throw new CompilationException("Failed to parse method - result was not a MethodNode");
        }
        
        return methodNode;
    }
    
    /// <summary>
    /// Compile a doIt expression (workspace evaluation).
    /// </summary>
    public CompiledMethod CompileDoIt(string expression)
    {
        // Wrap expression in a method format
        var methodSource = $"doIt\n\t^{expression}";
        return CompileMethod(methodSource, "UndefinedObject");
    }
    
    /// <summary>
    /// Install a compiled method into a class.
    /// </summary>
    public void InstallMethod(CompiledMethod method, SmalltalkObject classObject, bool isClassMethod)
    {
        // Get the method dictionary
        var methodDictSlot = 1; // Method dictionary is in slot 1
        var targetClass = isClassMethod ? GetMetaclass(classObject) : classObject;
        
        var methodDictOop = targetClass.Fields[methodDictSlot];
        if (methodDictOop is not SmalltalkObject methodDict)
        {
            throw new InvalidOperationException("Method dictionary not found or invalid");
        }
        
        // Install the method
        var selector = method.SelectorName ?? "unknown";
        var selectorSymbol = new Symbol(selector);
        
        // Add to method dictionary
        // Method dictionaries store selector->method mappings
        // We need to find the right place in the dictionary
        InstallMethodInDictionary(methodDict, selectorSymbol, method);
        
        // Flush method cache
        interpreter.MethodCache.FlushCache();
    }
    
    private void InstallMethodInDictionary(SmalltalkObject methodDict, Symbol selector, CompiledMethod method)
    {
        // Method dictionary structure depends on implementation
        // Typically it's either:
        // 1. Parallel arrays: [tally, keys..., values...]
        // 2. Or: [tally, key1, value1, key2, value2, ...]
        
        if (methodDict.Fields.Length < 2)
        {
            throw new InvalidOperationException("Invalid method dictionary structure");
        }
        
        // For now, use a simple linear search and replace/add
        // This is a simplified implementation
        var tally = methodDict.Fields[0] as int? ?? 0;
        
        // Try to find existing selector
        for (int i = 1; i < methodDict.Fields.Length - 1; i += 2)
        {
            if (methodDict.Fields[i] is Symbol sym && sym.Value == selector.Value)
            {
                // Replace existing method
                methodDict.Fields[i + 1] = method;
                return;
            }
        }
        
        // Add new method
        // Find first nil slot
        for (int i = 1; i < methodDict.Fields.Length - 1; i += 2)
        {
            if (methodDict.Fields[i] == null)
            {
                methodDict.Fields[i] = selector;
                methodDict.Fields[i + 1] = method;
                methodDict.Fields[0] = (tally >> 1) + 1; // Increment tally (as SmallInteger)
                return;
            }
        }
        
        throw new InvalidOperationException("Method dictionary is full");
    }
    
    private SmalltalkObject GetMetaclass(SmalltalkObject classObject)
    {
        // In Smalltalk-80, the class of a class is its metaclass
        // The metaclass is typically the ClassPointer of the class object
        var metaclass = classObject.ClassPointer;
        if (metaclass == null)
        {
            throw new InvalidOperationException("Class has no metaclass");
        }
        return metaclass;
    }
    
    private string GetClassName(SmalltalkObject classObject)
    {
        // Class name is typically in slot 5
        if (classObject.Fields.Length > 5 && classObject.Fields[5] is Symbol symbol)
        {
            return symbol.Value;
        }
        
        // Fallback - search for it
        foreach (var field in classObject.Fields)
        {
            if (field is Symbol sym && !sym.Value.Contains(':'))
            {
                return sym.Value;
            }
        }
        
        return "UnknownClass";
    }
}

/// <summary>
/// Compilation context that tracks the current class and compilation state.
/// </summary>
public class CompilationContext
{
    public string ClassName { get; }
    public bool IsClassMethod { get; }
    public Interpreter Interpreter { get; }
    public Dictionary<string, int> InstanceVariables { get; } = new();
    public Dictionary<string, int> ClassVariables { get; } = new();
    public Dictionary<string, object> PoolVariables { get; } = new();
    
    public CompilationContext(string className, bool isClassMethod, Interpreter interpreter)
    {
        ClassName = className;
        IsClassMethod = isClassMethod;
        Interpreter = interpreter;
        
        // Load instance variables for the class
        LoadInstanceVariables();
        
        // Load class variables
        LoadClassVariables();
    }
    
    private void LoadInstanceVariables()
    {
        // For now, we'll need to pass the class object directly
        // or find another way to look it up
        // This is a simplified implementation
    }
    
    private void LoadClassVariables()
    {
        // For now, we'll need to pass the class object directly
        // or find another way to look it up
        // This is a simplified implementation
    }
}

/// <summary>
/// Exception thrown during compilation.
/// </summary>
public class CompilationException : Exception
{
    public CompilationException(string message) : base(message) { }
    public CompilationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Error listener that throws exceptions on parse errors.
/// </summary>
public class ThrowingErrorListener : Antlr4.Runtime.BaseErrorListener
{
    public override void SyntaxError(
        TextWriter output,
        Antlr4.Runtime.IRecognizer recognizer,
        Antlr4.Runtime.IToken offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        Antlr4.Runtime.RecognitionException e)
    {
        throw new CompilationException($"Syntax error at line {line}:{charPositionInLine} - {msg}", e);
    }
}