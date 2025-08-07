using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Smalltalk80.Parser;

namespace Smalltalk80;

/// <summary>
/// Loads Smalltalk source files by sending messages to the Smalltalk system.
/// This is used after bootstrap to load StandardLibrary and other code via Smalltalk's
/// own class creation and method compilation mechanisms.
/// </summary>
public class SmalltalkLoader
{
    private readonly Interpreter interpreter;
    private readonly Dictionary<string, ClassInfo> loadedClasses = new();
    private int classesLoaded = 0;
    private int methodsLoaded = 0;
    private readonly List<string> errors = new();
    private readonly List<string> warnings = new();
    
    private class ClassInfo
    {
        public string Name { get; set; } = "";
        public bool IsBootstrapClass { get; set; }
        public int MethodsAdded { get; set; }
    }
    
    /// <summary>
    /// Creates a new SmalltalkLoader that will use the given interpreter
    /// to execute Smalltalk expressions.
    /// </summary>
    public SmalltalkLoader(Interpreter interpreter)
    {
        this.interpreter = interpreter;
        
        // Mark bootstrap classes as already existing
        MarkBootstrapClasses();
    }
    
    /// <summary>
    /// Marks classes that were created during bootstrap.
    /// These may be enhanced but not recreated.
    /// </summary>
    private void MarkBootstrapClasses()
    {
        var bootstrapClassNames = new[]
        {
            "Object", "UndefinedObject", "Behavior", "ClassDescription", "Class", "Metaclass",
            "Boolean", "True", "False", "Magnitude", "Number", "Integer", "SmallInteger",
            "Symbol", "String", "Collection", "SequenceableCollection", "ArrayedCollection",
            "Array", "Set", "Dictionary", "SystemDictionary", "IdentityDictionary",
            "MethodDictionary", "LookupKey", "Association", "BlockContext", "CompiledMethod",
            "ClassOrganizer", "SystemOrganizer"
        };
        
        foreach (var name in bootstrapClassNames)
        {
            loadedClasses[name] = new ClassInfo 
            { 
                Name = name, 
                IsBootstrapClass = true 
            };
        }
    }
    
    /// <summary>
    /// Loads a Smalltalk source file by parsing it and sending appropriate
    /// messages to the Smalltalk system.
    /// </summary>
    public LoadResult LoadFile(string path)
    {
        Console.WriteLine($"[LOADER] Loading {path} via Smalltalk messages...");
        
        try
        {
            // Parse the file
            var fileAst = FileInReader.Parse(path);
            
            // Process class definitions first
            ProcessClassDefinitions(fileAst);
            
            // Then process methods
            ProcessMethods(fileAst);
            
            // Finally process any initialization code
            ProcessInitializations(fileAst);
            
            return new LoadResult
            {
                Success = errors.Count == 0,
                ClassesLoaded = classesLoaded,
                MethodsLoaded = methodsLoaded,
                Errors = errors,
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to load file: {ex.Message}");
            return new LoadResult
            {
                Success = false,
                Errors = errors,
                Warnings = warnings
            };
        }
    }
    
    /// <summary>
    /// Processes all class definitions in the file.
    /// </summary>
    private void ProcessClassDefinitions(FileNode fileAst)
    {
        foreach (var chunk in fileAst.Chunks)
        {
            if (chunk is ClassDefinitionNode classDef)
            {
                ProcessClassDefinition(classDef);
            }
        }
    }
    
    /// <summary>
    /// Processes a single class definition by sending a subclass: message.
    /// </summary>
    private void ProcessClassDefinition(ClassDefinitionNode classDef)
    {
        try
        {
            // Check if this is a bootstrap class being enhanced
            if (loadedClasses.TryGetValue(classDef.ClassName, out var existingClass))
            {
                if (existingClass.IsBootstrapClass)
                {
                    Console.WriteLine($"[LOADER] Enhancing bootstrap class: {classDef.ClassName}");
                    warnings.Add($"Class {classDef.ClassName} already exists from bootstrap, will add new methods only");
                    return; // Don't recreate, just mark for method additions
                }
            }
            
            // Generate the class creation expression
            var expression = GenerateClassCreationExpression(classDef);
            Console.WriteLine($"[LOADER] Creating class {classDef.ClassName} via: {expression.Substring(0, Math.Min(60, expression.Length))}...");
            
            // Execute it
            var result = ExecuteExpression(expression);
            
            if (result != null)
            {
                loadedClasses[classDef.ClassName] = new ClassInfo 
                { 
                    Name = classDef.ClassName,
                    IsBootstrapClass = false
                };
                classesLoaded++;
                Console.WriteLine($"[LOADER] Created class: {classDef.ClassName}");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to create class {classDef.ClassName}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Generates a Smalltalk expression to create a class.
    /// </summary>
    private string GenerateClassCreationExpression(ClassDefinitionNode classDef)
    {
        var superclass = string.IsNullOrEmpty(classDef.Superclass) || classDef.Superclass == "nil" 
            ? "nil" 
            : classDef.Superclass;
            
        var instVars = string.Join(" ", classDef.InstanceVariables);
        var classVars = string.Join(" ", classDef.ClassVariables);
        var pools = string.Join(" ", classDef.PoolDictionaries);
        var category = classDef.Category ?? "Unclassified";
        
        // For now, we'll use the standard subclass: selector
        // In a full implementation, we'd need to detect variable subclass types from the parse tree
        // This would require either:
        // 1. Extending ClassDefinitionNode to include the subclass type
        // 2. Analyzing the parse tree context to determine the selector used
        string selector = "subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:";
        
        // Build the properly formatted Smalltalk expression
        return $@"{superclass} {selector}
    #{classDef.ClassName}
    instanceVariableNames: '{instVars}'
    classVariableNames: '{classVars}'
    poolDictionaries: '{pools}'
    category: '{category}'";
    }
    
    /// <summary>
    /// Processes all method definitions in the file.
    /// </summary>
    private void ProcessMethods(FileNode fileAst)
    {
        MethodHeaderNode? currentHeader = null;
        
        foreach (var chunk in fileAst.Chunks)
        {
            switch (chunk)
            {
                case MethodHeaderNode header:
                    currentHeader = header;
                    break;
                    
                case MethodCategoryNode category:
                    ProcessMethodCategory(category);
                    break;
                    
                case MethodDefinitionNode method when currentHeader != null:
                    ProcessMethodDefinition(currentHeader, method);
                    break;
            }
        }
    }
    
    /// <summary>
    /// Processes a method category containing multiple methods.
    /// </summary>
    private void ProcessMethodCategory(MethodCategoryNode category)
    {
        foreach (var method in category.Methods)
        {
            ProcessMethodDefinition(category.Header, method);
        }
    }
    
    /// <summary>
    /// Processes a single method definition by sending a compile: message.
    /// </summary>
    private void ProcessMethodDefinition(MethodHeaderNode header, MethodDefinitionNode method)
    {
        try
        {
            // Check if the class exists
            if (!loadedClasses.ContainsKey(header.ClassName) && 
                !SystemDictionary.TryGetClass(header.ClassName, out _))
            {
                warnings.Add($"Class {header.ClassName} not found for method {method.Selector}");
                return;
            }
            
            // Generate the method compilation expression
            var expression = GenerateMethodCompilationExpression(header, method);
            
            // Execute it
            var result = ExecuteExpression(expression);
            
            if (result != null)
            {
                methodsLoaded++;
                if (loadedClasses.TryGetValue(header.ClassName, out var classInfo))
                {
                    classInfo.MethodsAdded++;
                }
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Failed to compile method {method.Selector} in {header.ClassName}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Generates a Smalltalk expression to compile a method.
    /// </summary>
    private string GenerateMethodCompilationExpression(MethodHeaderNode header, MethodDefinitionNode method)
    {
        // Get the class reference
        var classRef = header.IsClassSide 
            ? $"{header.ClassName} class" 
            : header.ClassName;
        
        // Escape the method source for inclusion in a string
        var escapedSource = method.Source
            .Replace("'", "''")  // Escape single quotes
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");
        
        // Use the Smalltalk compile:classified: method
        var category = header.Category ?? "as yet unclassified";
        
        // Build the compile expression
        // We'll use compile:classified: which is the standard Smalltalk way
        return $@"{classRef} compile: '{escapedSource}' classified: '{category}'";
    }
    
    /// <summary>
    /// Processes initialization code (do-its).
    /// </summary>
    private void ProcessInitializations(FileNode fileAst)
    {
        foreach (var chunk in fileAst.Chunks)
        {
            if (chunk is ExpressionChunkNode exprChunk)
            {
                ProcessExpressionChunk(exprChunk);
            }
        }
    }
    
    /// <summary>
    /// Processes an expression chunk (executable code).
    /// </summary>
    private void ProcessExpressionChunk(ExpressionChunkNode exprChunk)
    {
        try
        {
            Console.WriteLine("[LOADER] Executing initialization code...");
            // Convert the expression to source and execute it
            // For now, we'll need to reconstruct the source from the AST
            // This is a limitation - we may need to preserve source in the AST
            var source = "Object new"; // Placeholder - need proper AST to source conversion
            var result = ExecuteExpression(source);
        }
        catch (Exception ex)
        {
            warnings.Add($"Failed to execute initialization: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Executes a Smalltalk expression using the interpreter.
    /// </summary>
    private object? ExecuteExpression(string expression)
    {
        try
        {
            // Parse the expression to determine the receiver
            // For class creation messages, we need to use the correct receiver
            object receiver;
            
            // Check if this is a class creation expression (starts with a class name or nil)
            var firstToken = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            
            if (firstToken == "nil")
            {
                // Use nil as receiver
                if (!SystemDictionary.TryGetGlobal("nil", out receiver))
                {
                    // Create a nil object if not found
                    if (!SystemDictionary.TryGetClass("UndefinedObject", out var nilClass))
                    {
                        throw new InvalidOperationException("UndefinedObject class not found");
                    }
                    receiver = new SmalltalkObject(nilClass, 0);
                }
            }
            else if (firstToken != null && SystemDictionary.TryGetClass(firstToken, out var cls))
            {
                // Use the class as receiver for subclass: messages
                receiver = cls;
            }
            else
            {
                // Check if it's a class-side message (e.g., "Object class")
                var parts = expression.Split(new[] { " class " }, StringSplitOptions.None);
                if (parts.Length > 1 && SystemDictionary.TryGetClass(parts[0], out var targetClass))
                {
                    // Use the metaclass as receiver
                    receiver = targetClass.Metaclass ?? targetClass;
                }
                else
                {
                    // Default to Object instance
                    if (!SystemDictionary.TryGetClass("Object", out var objectClass))
                    {
                        throw new InvalidOperationException("Object class not found");
                    }
                    receiver = new SmalltalkObject(objectClass, 0);
                }
            }
            
            // Parse and execute the expression
            var method = ParserService.CompileExpression(expression);
            
            // Ensure receiver is a SmalltalkObject (SmalltalkClass inherits from SmalltalkObject)
            if (!(receiver is SmalltalkObject stObj))
            {
                throw new InvalidOperationException($"Invalid receiver type: {receiver?.GetType().Name}");
            }
            
            return interpreter.Evaluate(method, stObj);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to execute: {expression}", ex);
        }
    }
    
    /// <summary>
    /// Result of loading a file.
    /// </summary>
    public class LoadResult
    {
        public bool Success { get; set; }
        public int ClassesLoaded { get; set; }
        public int MethodsLoaded { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}