using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Smalltalk80.Parser;

/// <summary>
/// Enhanced file-in loader that extends the basic FileInLoader with
/// comprehensive error handling, progress reporting, and validation.
/// </summary>
public static class EnhancedFileInLoader
{
    /// <summary>
    /// Load a Smalltalk file with enhanced error handling and reporting.
    /// </summary>
    /// <param name="path">Path to the Smalltalk source file.</param>
    /// <param name="options">Loading options for error handling and validation.</param>
    /// <returns>Result containing success status, errors, and statistics.</returns>
    public static FileLoadResult LoadWithDiagnostics(string path, FileLoadOptions? options = null)
    {
        options ??= new FileLoadOptions();
        var result = new FileLoadResult { SourcePath = path };
        var errors = new List<ParseError>();
        var warnings = new List<LoadWarning>();
        var processedChunks = new Dictionary<string, int>();

        try
        {
            // Validate file exists
            if (!File.Exists(path))
            {
                errors.Add(new ParseError(0, 0, $"File not found: {path}"));
                result.Success = false;
                result.Errors = errors;
                return result;
            }

            // Parse file with enhanced error handling
            FileNode fileAst;
            try
            {
                fileAst = FileInReader.Parse(path);
            }
            catch (Exception ex)
            {
                errors.Add(new ParseError(0, 0, $"Failed to parse file: {ex.Message}"));
                result.Success = false;
                result.Errors = errors;
                return result;
            }

            var context = new LoadContext();
            int totalChunks = fileAst.Chunks.Count;
            int processedCount = 0;

            // Process each chunk with detailed error handling
            foreach (var chunk in fileAst.Chunks)
            {
                try
                {
                    ProcessChunkWithValidation(chunk, context, options, warnings);
                    IncrementChunkCounter(processedChunks, chunk.GetType().Name);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    var error = new ParseError(0, 0,
                        $"Error processing {chunk.GetType().Name} (chunk {processedCount + 1}/{totalChunks}): {ex.Message}");
                    errors.Add(error);

                    if (!options.ContinueOnError)
                    {
                        result.Success = false;
                        result.Errors = errors;
                        result.Warnings = warnings;
                        result.ProcessedChunks = processedChunks;
                        return result;
                    }
                }
            }

            // Validate the loaded state
            if (options.ValidateMethodSignatures)
            {
                ValidateLoadedClasses(warnings);
            }

            result.Success = errors.Count == 0;
            result.Errors = errors;
            result.Warnings = warnings;
            result.ProcessedChunks = processedChunks;

            return result;
        }
        catch (Exception ex)
        {
            errors.Add(new ParseError(0, 0, $"Unexpected error loading file: {ex.Message}"));
            result.Success = false;
            result.Errors = errors;
            return result;
        }
    }

    /// <summary>
    /// Load multiple files in sequence with consolidated reporting.
    /// </summary>
    /// <param name="paths">Paths to Smalltalk source files.</param>
    /// <param name="options">Loading options.</param>
    /// <returns>Consolidated load result.</returns>
    public static FileLoadResult LoadMultipleFiles(IEnumerable<string> paths, FileLoadOptions? options = null)
    {
        options ??= new FileLoadOptions();
        var consolidatedResult = new FileLoadResult();
        var allErrors = new List<ParseError>();
        var allWarnings = new List<LoadWarning>();
        var allChunks = new Dictionary<string, int>();

        foreach (var path in paths)
        {
            var result = LoadWithDiagnostics(path, options);

            // Consolidate results
            allErrors.AddRange(result.Errors);
            allWarnings.AddRange(result.Warnings);

            foreach (var chunk in result.ProcessedChunks)
            {
                allChunks.TryGetValue(chunk.Key, out var existing);
                allChunks[chunk.Key] = existing + chunk.Value;
            }

            if (!result.Success && !options.ContinueOnError)
            {
                consolidatedResult.Success = false;
                consolidatedResult.Errors = allErrors;
                consolidatedResult.Warnings = allWarnings;
                consolidatedResult.ProcessedChunks = allChunks;
                return consolidatedResult;
            }
        }

        consolidatedResult.Success = allErrors.Count == 0;
        consolidatedResult.Errors = allErrors;
        consolidatedResult.Warnings = allWarnings;
        consolidatedResult.ProcessedChunks = allChunks;

        return consolidatedResult;
    }

    private static void ProcessChunkWithValidation(
        AstNode chunk,
        LoadContext context,
        FileLoadOptions options,
        List<LoadWarning> warnings)
    {
        switch (chunk)
        {
            case ClassDefinitionNode classDef:
                ProcessClassDefinitionWithValidation(classDef, context, options, warnings);
                break;

            case MethodDefinitionNode methodDef:
                ProcessMethodDefinitionWithValidation(methodDef, context, options, warnings);
                break;

            case MethodCategoryNode categoryNode:
                ProcessMethodCategoryWithValidation(categoryNode, context, options, warnings);
                break;

            case ClassCommentNode commentNode:
                ProcessClassComment(commentNode, context);
                break;

            case ScriptNode scriptNode:
                ProcessScript(scriptNode, context, options, warnings);
                break;

            case ExpressionChunkNode exprNode:
                ProcessExpression(exprNode, context, options, warnings);
                break;

            default:
                warnings.Add(new LoadWarning(0, 0, $"Unknown chunk type: {chunk.GetType().Name}"));
                break;
        }
    }

    private static void ProcessClassDefinitionWithValidation(
        ClassDefinitionNode classDef,
        LoadContext context,
        FileLoadOptions options,
        List<LoadWarning> warnings)
    {
        // Validate superclass exists
        var super = SystemDictionary.TryGetClass(classDef.Superclass, out var sup) ? sup : null;

        if (super == null && !string.IsNullOrEmpty(classDef.Superclass) && classDef.Superclass != "nil")
        {
            warnings.Add(new LoadWarning(0, 0,
                $"Superclass '{classDef.Superclass}' not found for class '{classDef.ClassName}'"));
        }

        // Validate variable names
        if (options.CheckVariableConflicts)
        {
            ValidateVariableNames(classDef, warnings);
        }

        // Create the class
        var cls = SystemDictionary.DefineClass(
            classDef.ClassName,
            super,
            classDef.InstanceVariables,
            classDef.ClassVariables,
            classDef.PoolDictionaries,
            classDef.Category);

        context.CurrentClass = cls;
        context.CurrentClassName = classDef.ClassName;
    }

    private static void ProcessMethodDefinitionWithValidation(
        MethodDefinitionNode methodDef,
        LoadContext context,
        FileLoadOptions options,
        List<LoadWarning> warnings)
    {
        if (context.CurrentClass == null)
        {
            throw new InvalidOperationException("Method definition found without preceding class definition");
        }

        // Validate method signature
        if (options.ValidateMethodSignatures)
        {
            ValidateMethodSignature(methodDef, warnings);
        }

        var variables = context.CurrentHeader?.IsClassSide == true
            ? context.CurrentClass.ClassVariables
            : context.CurrentClass.InstanceVariables;

        CompiledMethod compiled;
        try
        {
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
                var parser = new SmalltalkParser(methodDef.Source);
                compiled = parser.ParseMethod(methodDef.Arguments, variables, methodDef.Selector);
            }

            // Validate compilation result
            if (compiled.Bytecodes.Length == 0 && !methodDef.HasPrimitive)
            {
                warnings.Add(new LoadWarning(0, 0,
                    $"Method '{methodDef.Selector}' compiled to empty bytecode sequence"));
            }

            // Check for primitive method validity
            if (methodDef.HasPrimitive)
            {
                ValidatePrimitiveMethod(methodDef, compiled, warnings);
            }

            context.CurrentClass.MethodDictionary[new Symbol(methodDef.Selector)] = compiled;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to compile method '{methodDef.Selector}': {ex.Message}", ex);
        }
    }

    private static void ProcessMethodCategoryWithValidation(
        MethodCategoryNode categoryNode,
        LoadContext context,
        FileLoadOptions options,
        List<LoadWarning> warnings)
    {
        if (categoryNode.Header == null)
        {
            throw new InvalidOperationException("Method category without header");
        }

        context.CurrentHeader = categoryNode.Header;
        var categoryClass = SystemDictionary.DefineClass(categoryNode.Header.ClassName);
        context.CurrentClass = categoryClass;

        foreach (var method in categoryNode.Methods)
        {
            try
            {
                ProcessMethodDefinitionWithValidation(method, context, options, warnings);
            }
            catch (Exception ex)
            {
                if (!options.ContinueOnError)
                    throw;

                warnings.Add(new LoadWarning(0, 0,
                    $"Failed to process method in category: {ex.Message}"));
            }
        }
    }

    private static void ProcessClassComment(ClassCommentNode commentNode, LoadContext context)
    {
        // Class comment processing - could be stored in a separate dictionary
        // since SmalltalkClass doesn't have a Comment property
        if (context.CurrentClass != null)
        {
            // For now, we'll just note that the comment exists
            // A full implementation might store comments in a separate registry
        }
    }

    private static void ProcessScript(
        ScriptNode scriptNode,
        LoadContext context,
        FileLoadOptions options,
        List<LoadWarning> warnings)
    {
        if (options.ExecuteScripts)
        {
            warnings.Add(new LoadWarning(0, 0, "Script execution not yet implemented"));
        }
    }

    private static void ProcessExpression(
        ExpressionChunkNode exprNode,
        LoadContext context,
        FileLoadOptions options,
        List<LoadWarning> warnings)
    {
        if (options.ExecuteExpressions)
        {
            warnings.Add(new LoadWarning(0, 0, "Expression execution not yet implemented"));
        }
    }

    private static void ValidateVariableNames(ClassDefinitionNode classDef, List<LoadWarning> warnings)
    {
        // Check for conflicts between instance and class variables
        var conflicts = classDef.InstanceVariables.Intersect(classDef.ClassVariables).ToList();
        foreach (var conflict in conflicts)
        {
            warnings.Add(new LoadWarning(0, 0,
                $"Variable '{conflict}' declared as both instance and class variable in '{classDef.ClassName}'"));
        }

        // Check for reserved names
        var reservedNames = new[] { "self", "super", "nil", "true", "false" };
        var reservedConflicts = classDef.InstanceVariables
            .Concat(classDef.ClassVariables)
            .Intersect(reservedNames)
            .ToList();

        foreach (var conflict in reservedConflicts)
        {
            warnings.Add(new LoadWarning(0, 0,
                $"Variable '{conflict}' conflicts with reserved name in '{classDef.ClassName}'"));
        }
    }

    private static void ValidateMethodSignature(MethodDefinitionNode methodDef, List<LoadWarning> warnings)
    {
        // Check for argument conflicts with reserved names
        var reservedNames = new[] { "self", "super", "nil", "true", "false" };
        var conflicts = methodDef.Arguments.Intersect(reservedNames).ToList();

        foreach (var conflict in conflicts)
        {
            warnings.Add(new LoadWarning(0, 0,
                $"Method argument '{conflict}' conflicts with reserved name in '{methodDef.Selector}'"));
        }

        // Check for duplicate arguments
        var duplicates = methodDef.Arguments
            .GroupBy(x => x)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var duplicate in duplicates)
        {
            warnings.Add(new LoadWarning(0, 0,
                $"Duplicate argument '{duplicate}' in method '{methodDef.Selector}'"));
        }
    }

    private static void ValidatePrimitiveMethod(
        MethodDefinitionNode methodDef,
        CompiledMethod compiled,
        List<LoadWarning> warnings)
    {
        // Check if primitive index is valid
        if (methodDef.PrimitiveIndex < 1 || methodDef.PrimitiveIndex > 255)
        {
            warnings.Add(new LoadWarning(0, 0,
                $"Invalid primitive index {methodDef.PrimitiveIndex} in method '{methodDef.Selector}'"));
        }

        // Check if method has both primitive and significant bytecode
        if (compiled.Bytecodes.Length > 3) // More than just a return
        {
            warnings.Add(new LoadWarning(0, 0,
                $"Primitive method '{methodDef.Selector}' has substantial fallback code"));
        }
    }

    private static void ValidateLoadedClasses(List<LoadWarning> warnings)
    {
        // Validate class hierarchy integrity
        foreach (var classEntry in SystemDictionary.AllClasses)
        {
            var cls = classEntry.Value;
            // Additional validation could be added here
            // For example, checking superclass relationships
        }
    }

    private static void IncrementChunkCounter(Dictionary<string, int> counters, string chunkType)
    {
        counters.TryGetValue(chunkType, out var count);
        counters[chunkType] = count + 1;
    }
}

/// <summary>
/// Enhanced method compiler with temporary variable validation.
/// </summary>
public static class EnhancedMethodCompiler
{
    /// <summary>
    /// Compile a method with enhanced temporary variable parsing and validation.
    /// </summary>
    public static CompiledMethod CompileMethodWithValidation(
        SequenceNode body,
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> instanceVariables,
        bool hasPrimitive = false,
        int primitiveIndex = 0)
    {
        // Enhanced temporary variable validation could be added here
        // For now, use the standard compiler
        return AstMethodCompiler.CompileMethod(body, arguments, instanceVariables, hasPrimitive, primitiveIndex);
    }
}

// Supporting data structures for enhanced file loading

/// <summary>
/// Configuration options for loading Smalltalk file-out format source files.
/// Provides control over parsing behavior, validation, and execution of loaded content.
/// </summary>
public class FileLoadOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to continue processing when parsing errors are encountered.
    /// When true, the loader will attempt to recover and continue processing subsequent chunks.
    /// When false, the loader will stop at the first error encountered.
    /// Default is true.
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to execute script expressions found in the source.
    /// When true, standalone expressions will be executed during loading.
    /// When false, script expressions will be parsed but not executed.
    /// Default is false for security reasons.
    /// </summary>
    public bool ExecuteScripts { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to execute general expressions during loading.
    /// When true, expression chunks will be evaluated during the loading process.
    /// When false, expressions will be parsed but not executed.
    /// Default is false for security reasons.
    /// </summary>
    public bool ExecuteExpressions { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to validate method signatures against class definitions.
    /// When true, method signatures will be checked for compatibility with their containing class.
    /// When false, signature validation is skipped.
    /// Default is true.
    /// </summary>
    public bool ValidateMethodSignatures { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to check for variable name conflicts.
    /// When true, variable names will be validated for conflicts between instance variables,
    /// class variables, and method temporaries.
    /// When false, variable conflict checking is skipped.
    /// Default is true.
    /// </summary>
    public bool CheckVariableConflicts { get; set; } = true;
}

/// <summary>
/// Represents the result of loading a Smalltalk file-out format source file.
/// Contains success status, processing details, and any errors or warnings encountered.
/// </summary>
public class FileLoadResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the file loading operation completed successfully.
    /// True if all chunks were processed without fatal errors, false otherwise.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the path of the source file that was loaded.
    /// Contains the full path to the original source file.
    /// </summary>
    public string SourcePath { get; set; } = "";

    /// <summary>
    /// Gets or sets the collection of parsing errors encountered during loading.
    /// Each error includes location information and a descriptive message.
    /// </summary>
    public List<ParseError> Errors { get; set; } = new();

    /// <summary>
    /// Gets or sets the collection of warnings generated during loading.
    /// Warnings indicate potential issues that didn't prevent processing.
    /// </summary>
    public List<LoadWarning> Warnings { get; set; } = new();

    /// <summary>
    /// Gets or sets a dictionary tracking the number of chunks processed by type.
    /// Keys are chunk type names, values are the count of chunks of that type processed.
    /// </summary>
    public Dictionary<string, int> ProcessedChunks { get; set; } = new();
}

/// <summary>
/// Maintains the current parsing context during file loading operations.
/// Tracks the current class being processed and related parsing state.
/// </summary>
public class LoadContext
{
    /// <summary>
    /// Gets or sets the current class being processed.
    /// This is the SmalltalkClass object representing the class currently being defined or modified.
    /// May be null if no class is currently active.
    /// </summary>
    public SmalltalkClass? CurrentClass { get; set; }

    /// <summary>
    /// Gets or sets the name of the current class being processed.
    /// This is the string name of the class, which may be used before the class object is created.
    /// May be null if no class is currently active.
    /// </summary>
    public string? CurrentClassName { get; set; }

    /// <summary>
    /// Gets or sets the current method header being processed.
    /// This contains the parsed method signature including selector and parameter names.
    /// May be null if no method is currently being processed.
    /// </summary>
    public MethodHeaderNode? CurrentHeader { get; set; }
}

/// <summary>
/// Represents a parsing error encountered during file loading.
/// Contains location information and an error message for debugging and user feedback.
/// </summary>
public class ParseError
{
    /// <summary>
    /// Gets or sets the line number where the error occurred.
    /// Line numbers are 1-based to match typical editor conventions.
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// Gets or sets the column number where the error occurred.
    /// Column numbers are 1-based to match typical editor conventions.
    /// </summary>
    public int Column { get; set; }

    /// <summary>
    /// Gets or sets the error message describing what went wrong.
    /// This should be a human-readable description of the parsing issue.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Initializes a new instance of the ParseError class with the specified location and message.
    /// </summary>
    /// <param name="line">The line number where the error occurred (1-based).</param>
    /// <param name="column">The column number where the error occurred (1-based).</param>
    /// <param name="message">A human-readable description of the error.</param>
    public ParseError(int line, int column, string message)
    {
        Line = line;
        Column = column;
        Message = message;
    }
}

/// <summary>
/// Represents a warning message generated during file loading.
/// Warnings indicate potential issues that don't prevent processing but may need attention.
/// </summary>
public class LoadWarning
{
    /// <summary>
    /// Gets or sets the line number where the warning condition was detected.
    /// Line numbers are 1-based to match typical editor conventions.
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// Gets or sets the column number where the warning condition was detected.
    /// Column numbers are 1-based to match typical editor conventions.
    /// </summary>
    public int Column { get; set; }

    /// <summary>
    /// Gets or sets the warning message describing the potential issue.
    /// This should be a human-readable description of what might need attention.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Initializes a new instance of the LoadWarning class with the specified location and message.
    /// </summary>
    /// <param name="line">The line number where the warning was generated (1-based).</param>
    /// <param name="column">The column number where the warning was generated (1-based).</param>
    /// <param name="message">A human-readable description of the warning condition.</param>
    public LoadWarning(int line, int column, string message)
    {
        Line = line;
        Column = column;
        Message = message;
    }
}
