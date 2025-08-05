using System;
using System.Collections.Generic;
using System.Linq;
using Smalltalk80.Parser;

/// <summary>
/// Specialized loader for complete Smalltalk-80 system images that handles
/// forward references and circular dependencies between classes.
/// Uses a two-pass loading strategy to resolve dependencies correctly.
/// </summary>
public static class SystemImageLoader
{
    /// <summary>
    /// Load a complete Smalltalk-80 system image with proper dependency resolution.
    /// Uses a two-pass strategy:
    /// 1. First pass: Create all class stubs without superclass relationships
    /// 2. Second pass: Resolve superclass relationships and install methods
    /// </summary>
    /// <param name="path">Path to the system image file.</param>
    /// <returns>Result containing success status and any errors encountered.</returns>
    public static SystemImageLoadResult LoadSystemImage(string path)
    {
        var result = new SystemImageLoadResult { SourcePath = path };
        var errors = new List<ParseError>();
        var warnings = new List<LoadWarning>();

        try
        {
            // Parse the entire file first
            FileNode fileAst = FileInReader.Parse(path);

            // Collect all class definitions and method chunks
            var classDefinitions = new List<ClassDefinitionNode>();
            var methodChunks = new List<(MethodCategoryNode? Category, MethodDefinitionNode? Method, MethodHeaderNode? Header)>();
            var otherChunks = new List<AstNode>();

            foreach (var chunk in fileAst.Chunks)
            {
                switch (chunk)
                {
                    case ClassDefinitionNode classDef:
                        classDefinitions.Add(classDef);
                        break;
                    case MethodCategoryNode methodCategory:
                        methodChunks.Add((methodCategory, null, methodCategory.Header));
                        break;
                    case MethodDefinitionNode methodDef:
                        methodChunks.Add((null, methodDef, null));
                        break;
                    case MethodHeaderNode header:
                        methodChunks.Add((null, null, header));
                        break;
                    default:
                        otherChunks.Add(chunk);
                        break;
                }
            }

            Console.WriteLine($"[SYSTEM_IMAGE] Found {classDefinitions.Count} class definitions");
            Console.WriteLine($"[SYSTEM_IMAGE] Found {methodChunks.Count} method chunks");

            // PASS 1: Create all class stubs without superclass relationships
            var classStubs = new Dictionary<string, SmalltalkClass>();
            foreach (var classDef in classDefinitions)
            {
                try
                {
                    // Create class with nil superclass initially
                    var classStub = SystemDictionary.DefineClass(
                        classDef.ClassName,
                        null, // No superclass yet
                        classDef.InstanceVariables,
                        classDef.ClassVariables,
                        classDef.PoolDictionaries,
                        classDef.Category);

                    classStubs[classDef.ClassName] = classStub;
                    Console.WriteLine($"[SYSTEM_IMAGE] Created class stub: {classDef.ClassName}");
                }
                catch (Exception ex)
                {
                    errors.Add(new ParseError(0, 0, $"Failed to create class stub for {classDef.ClassName}: {ex.Message}"));
                }
            }

            // PASS 2: Resolve superclass relationships
            foreach (var classDef in classDefinitions)
            {
                try
                {
                    if (classStubs.TryGetValue(classDef.ClassName, out var cls))
                    {
                        // Resolve superclass
                        SmalltalkClass? superclass = null;
                        if (!string.IsNullOrEmpty(classDef.Superclass) && classDef.Superclass != "nil")
                        {
                            if (classStubs.TryGetValue(classDef.Superclass, out superclass))
                            {
                                Console.WriteLine($"[SYSTEM_IMAGE] Resolved {classDef.ClassName} -> {classDef.Superclass}");
                            }
                            else if (SystemDictionary.TryGetClass(classDef.Superclass, out superclass))
                            {
                                Console.WriteLine($"[SYSTEM_IMAGE] Found existing superclass: {classDef.Superclass}");
                            }
                            else
                            {
                                warnings.Add(new LoadWarning(0, 0,
                                    $"Superclass '{classDef.Superclass}' not found for class '{classDef.ClassName}'"));
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[SYSTEM_IMAGE] Class {classDef.ClassName} has no superclass (root class)");
                        }

                        // Update the class with resolved superclass
                        cls.Superclass = superclass;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(new ParseError(0, 0, $"Failed to resolve superclass for {classDef.ClassName}: {ex.Message}"));
                }
            }

            // PASS 3: Install methods
            MethodHeaderNode? currentHeader = null;
            SmalltalkClass? currentClass = null;

            foreach (var (category, method, header) in methodChunks)
            {
                try
                {
                    if (header != null)
                    {
                        currentHeader = header;
                        if (classStubs.TryGetValue(header.ClassName, out currentClass))
                        {
                            Console.WriteLine($"[SYSTEM_IMAGE] Processing methods for class: {header.ClassName}");
                        }
                        else
                        {
                            warnings.Add(new LoadWarning(0, 0, $"Class not found for methods: {header.ClassName}"));
                        }
                    }

                    if (category != null)
                    {
                        // Process all methods in the category
                        foreach (var categoryMethod in category.Methods)
                        {
                            InstallMethod(categoryMethod, currentClass, currentHeader, warnings);
                        }
                    }
                    else if (method != null && currentClass != null && currentHeader != null)
                    {
                        InstallMethod(method, currentClass, currentHeader, warnings);
                    }
                }
                catch (Exception ex)
                {
                    var methodName = method?.Selector ?? category?.Methods.FirstOrDefault()?.Selector ?? "unknown";
                    errors.Add(new ParseError(0, 0, $"Failed to install method {methodName}: {ex.Message}"));
                }
            }

            result.Success = errors.Count == 0;
            result.Errors = errors;
            result.Warnings = warnings;
            result.ClassesLoaded = classStubs.Count;
            result.MethodsLoaded = methodChunks.Count;

            if (result.Success)
            {
                Console.WriteLine($"[SYSTEM_IMAGE] Successfully loaded {result.ClassesLoaded} classes and {result.MethodsLoaded} method chunks");
            }
            else
            {
                Console.WriteLine($"[SYSTEM_IMAGE] Failed to load system image: {errors.Count} errors");
                foreach (var error in errors)
                {
                    Console.WriteLine($"[SYSTEM_IMAGE] ERROR: {error.Message} (Line {error.Line}, Col {error.Column})");
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            errors.Add(new ParseError(0, 0, $"Unexpected error loading system image: {ex.Message}"));
            result.Success = false;
            result.Errors = errors;
            return result;
        }
    }

    private static void InstallMethod(
        MethodDefinitionNode methodDef,
        SmalltalkClass? targetClass,
        MethodHeaderNode? header,
        List<LoadWarning> warnings)
    {
        if (targetClass == null || header == null)
        {
            warnings.Add(new LoadWarning(0, 0, $"Cannot install method {methodDef.Selector}: no target class"));
            return;
        }

        var variables = header.IsClassSide ? targetClass.ClassVariables : targetClass.InstanceVariables;

        CompiledMethod compiled;
        if (string.IsNullOrWhiteSpace(methodDef.Source))
        {
            var emptyBody = new SequenceNode(new List<string>(), new List<StatementNode>());
            compiled = AstMethodCompiler.CompileMethod(
                emptyBody,
                methodDef.Arguments,
                variables,
                methodDef.HasPrimitive,
                methodDef.PrimitiveIndex);
        }
        else
        {
            // Use AstMethodCompiler directly with the pre-parsed body and primitive information
            compiled = AstMethodCompiler.CompileMethod(
                methodDef.Body,
                methodDef.Arguments,
                variables,
                methodDef.HasPrimitive,
                methodDef.PrimitiveIndex);
        }

        compiled = new CompiledMethod
        {
            Header = compiled.Header,
            Bytecodes = compiled.Bytecodes,
            Literals = compiled.Literals,
            TemporariesCount = compiled.TemporariesCount,
            HasPrimitive = methodDef.HasPrimitive,
            PrimitiveIndex = methodDef.PrimitiveIndex,
            Comment = compiled.Comment
        };

        // Install in the appropriate method dictionary
        var methodDictionary = header.IsClassSide ? targetClass.ClassMethodDictionary : targetClass.MethodDictionary;
        methodDictionary[new Symbol(methodDef.Selector)] = compiled;
    }
}

/// <summary>
/// Result of loading a complete Smalltalk-80 system image.
/// </summary>
public class SystemImageLoadResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the system image was loaded successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the path of the system image file that was loaded.
    /// </summary>
    public string SourcePath { get; set; } = "";

    /// <summary>
    /// Gets or sets the collection of errors encountered during loading.
    /// </summary>
    public List<ParseError> Errors { get; set; } = new();

    /// <summary>
    /// Gets or sets the collection of warnings generated during loading.
    /// </summary>
    public List<LoadWarning> Warnings { get; set; } = new();

    /// <summary>
    /// Gets or sets the number of classes successfully loaded.
    /// </summary>
    public int ClassesLoaded { get; set; }

    /// <summary>
    /// Gets or sets the number of methods successfully loaded.
    /// </summary>
    public int MethodsLoaded { get; set; }
}

/// <summary>
/// Represents a parse error encountered during source loading.
/// </summary>
public class ParseError
{
    /// <summary>
    /// Gets the line number where the error occurred.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the column number where the error occurred.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParseError"/> class.
    /// </summary>
    /// <param name="line">The line number where the error occurred.</param>
    /// <param name="column">The column number where the error occurred.</param>
    /// <param name="message">The error message.</param>
    public ParseError(int line, int column, string message)
    {
        Line = line;
        Column = column;
        Message = message;
    }
}

/// <summary>
/// Represents a warning generated during source loading.
/// </summary>
public class LoadWarning
{
    /// <summary>
    /// Gets the line number where the warning occurred.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the column number where the warning occurred.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Gets the warning message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LoadWarning"/> class.
    /// </summary>
    /// <param name="line">The line number where the warning occurred.</param>
    /// <param name="column">The column number where the warning occurred.</param>
    /// <param name="message">The warning message.</param>
    public LoadWarning(int line, int column, string message)
    {
        Line = line;
        Column = column;
        Message = message;
    }
}
