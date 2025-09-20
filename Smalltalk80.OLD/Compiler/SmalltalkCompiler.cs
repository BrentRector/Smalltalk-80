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
    /// Supports both standalone methods and file-out format (with bang terminators).
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
        
        // Determine format: file-out (with !) or standalone
        bool isFileOutFormat = source.Contains('!');
        
        Antlr4.Runtime.Tree.IParseTree tree;
        object? result;
        
        if (isFileOutFormat)
        {
            // Parse as file-out format
            tree = parser.fileIn();
            
            if (parser.NumberOfSyntaxErrors > 0)
            {
                throw new CompilationException($"Syntax error in method: {parser.NumberOfSyntaxErrors} errors found");
            }
            
            // Build AST - for file-out, we need to extract the method from chunks
            var visitor = new FileInMethodExtractor();
            result = visitor.Visit(tree);
        }
        else
        {
            // Parse as standalone method
            tree = parser.methodEntry();
            
            if (parser.NumberOfSyntaxErrors > 0)
            {
                throw new CompilationException($"Syntax error in method: {parser.NumberOfSyntaxErrors} errors found");
            }
            
            // Build AST
            var visitor = new SimpleAstBuildingVisitor();
            result = visitor.Visit(tree);
        }
        
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
    public SmalltalkObject? ClassObject { get; private set; }
    
    // These should be loaded from the actual class object, not maintained separately
    public Dictionary<string, int> InstanceVariables { get; } = new();
    public Dictionary<string, int> ClassVariables { get; } = new();
    public Dictionary<string, object> PoolVariables { get; } = new();
    
    public CompilationContext(string className, bool isClassMethod, Interpreter interpreter)
    {
        ClassName = className;
        IsClassMethod = isClassMethod;
        Interpreter = interpreter;
        
        // Try to find the class object in the VM
        LoadClassObject();
        
        // Load instance variables from the actual class object
        LoadInstanceVariables();
        
        // Load class variables from the actual class object
        LoadClassVariables();
    }
    
    private void LoadClassObject()
    {
        // Look up the class in the Smalltalk global dictionary
        // We need to find the Smalltalk global dictionary first
        var smalltalkDict = LookupGlobalDictionary();
        if (smalltalkDict == null)
        {
            // During bootstrap or testing, the global dictionary might not be available
            return;
        }
        
        // Look up the class by name
        var classSymbol = new Symbol(ClassName);
        ClassObject = LookupInDictionary(smalltalkDict, classSymbol);
    }
    
    private void LoadInstanceVariables()
    {
        if (ClassObject == null)
            return;
            
        // Instance variables are stored in slot 3 of ClassDescription
        // The class hierarchy is: Object -> Behavior -> ClassDescription -> Class
        // ClassDescription adds slot 3 for instance variables
        
        if (ClassObject.Fields.Length > 3)
        {
            var instVarsField = ClassObject.Fields[3];
            
            // Instance variables can be stored as:
            // 1. An Array of Symbols
            // 2. A String with space-separated names
            // 3. A SmalltalkObject containing symbols
            
            if (instVarsField is SmalltalkObject instVarsArray)
            {
                // Array of symbols
                for (int i = 0; i < instVarsArray.Fields.Length; i++)
                {
                    if (instVarsArray.Fields[i] is Symbol symbol)
                    {
                        InstanceVariables[symbol.Value] = i;
                    }
                    else if (instVarsArray.Fields[i] is SmalltalkObject symObj && symObj.Fields.Length > 0)
                    {
                        // Symbol objects have the string in field 0
                        if (symObj.Fields[0] is string varName)
                        {
                            InstanceVariables[varName] = i;
                        }
                    }
                }
            }
            else if (instVarsField is string instVarsString)
            {
                // Space-separated string
                var varNames = instVarsString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < varNames.Length; i++)
                {
                    InstanceVariables[varNames[i]] = i;
                }
            }
        }
    }
    
    private void LoadClassVariables()
    {
        if (ClassObject == null)
            return;
            
        // Class variables are typically stored in slot 6 (classVariables)
        // This is a Dictionary or similar structure
        // For now, we'll look for them but this needs the full class structure
        
        if (ClassObject.Fields.Length > 6)
        {
            var classVarsField = ClassObject.Fields[6];
            
            if (classVarsField is SmalltalkObject classVarsDict)
            {
                // This would be a Dictionary object
                // We'd need to iterate through it to get the variable names
                // For now, leaving empty as class variables are less commonly used
                // and require dictionary iteration support
            }
        }
    }
    
    private SmalltalkObject? LookupGlobalDictionary()
    {
        // The global dictionary is named "Smalltalk" and should be registered
        // We need to find it in the object memory
        
        // Look through all objects for the SystemDictionary
        foreach (var obj in ObjectMemory.Objects)
        {
            if (obj == null) continue;
            
            // Check if this is the Smalltalk dictionary
            // It would be a SystemDictionary instance with many entries
            if (obj.ClassPointer != null)
            {
                var className = GetClassNameFromObject(obj.ClassPointer);
                if (className == "SystemDictionary")
                {
                    // This should be the Smalltalk global dictionary
                    return obj;
                }
            }
        }
        
        return null;
    }
    
    private string? GetClassNameFromObject(SmalltalkObject classObj)
    {
        // Class name is typically in slot 5
        if (classObj.Fields.Length > 5)
        {
            if (classObj.Fields[5] is Symbol symbol)
                return symbol.Value;
            if (classObj.Fields[5] is SmalltalkObject symObj && symObj.Fields.Length > 0)
            {
                if (symObj.Fields[0] is string name)
                    return name;
            }
        }
        return null;
    }
    
    private SmalltalkObject? LookupInDictionary(SmalltalkObject dict, Symbol key)
    {
        // Dictionary structure in Smalltalk-80:
        // Field 0: tally (number of entries)
        // Then pairs of key-value entries
        
        if (dict.Fields.Length < 1)
            return null;
            
        var tally = dict.Fields[0] as int? ?? 0;
        if (tally == 0)
            return null;
            
        // Search for the key in the dictionary
        for (int i = 1; i < dict.Fields.Length - 1; i += 2)
        {
            var dictKey = dict.Fields[i];
            
            if (dictKey is Symbol sym && sym.Value == key.Value)
            {
                // Found it!
                return dict.Fields[i + 1] as SmalltalkObject;
            }
            else if (dictKey is SmalltalkObject keyObj && keyObj.Fields.Length > 0)
            {
                // Check if it's a symbol object
                if (keyObj.Fields[0] is string keyName && keyName == key.Value)
                {
                    return dict.Fields[i + 1] as SmalltalkObject;
                }
            }
        }
        
        return null;
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