using System;
using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;
using Smalltalk80.Generated;

namespace Smalltalk80;

/// <summary>
/// Simplified Smalltalk file loader that works without SystemDictionary.
/// </summary>
public class SmalltalkLoader
{
    private readonly Interpreter interpreter;
    
    public SmalltalkLoader(Interpreter interpreter)
    {
        this.interpreter = interpreter;
    }
    
    /// <summary>
    /// Load a Smalltalk file.
    /// </summary>
    public LoadResult LoadFile(string filePath)
    {
        var result = new LoadResult();
        
        try
        {
            var input = File.ReadAllText(filePath);
            var inputStream = new AntlrInputStream(input);
            var lexer = new Smalltalk80Lexer(inputStream);
            var tokens = new CommonTokenStream(lexer);
            var parser = new Smalltalk80Parser(tokens);
            
            // Parse the file
            var tree = parser.fileIn();
            
            if (parser.NumberOfSyntaxErrors > 0)
            {
                result.Success = false;
                result.Errors.Add($"Failed to parse {filePath}: {parser.NumberOfSyntaxErrors} syntax errors");
            }
            else
            {
                result.Success = true;
                // TODO: Process the parsed tree when VM supports it
                Console.WriteLine($"[LOADER] Parsed {filePath} successfully (processing not yet implemented)");
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Error loading {filePath}: {ex.Message}");
        }
        
        return result;
    }
}

/// <summary>
/// Result of loading a Smalltalk file.
/// </summary>
public class LoadResult
{
    public bool Success { get; set; }
    public int ClassesLoaded { get; set; }
    public int MethodsLoaded { get; set; }
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
}