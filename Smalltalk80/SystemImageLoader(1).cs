using System;
using System.Collections.Generic;

/// <summary>
/// STUB IMPLEMENTATION - Simplified for shadow class removal.
/// Specialized loader for complete Smalltalk-80 system images.
/// </summary>
public static class SystemImageLoader
{
    /// <summary>
    /// Stub implementation for system image loading.
    /// </summary>
    public static SystemImageLoadResult LoadSystemImage(string path)
    {
        Console.WriteLine($"[SYSIMG] Stub system image loader - shadow classes removed. File: {path}");
        return new SystemImageLoadResult 
        { 
            SourcePath = path, 
            Success = false,
            Errors = new List<ParseError> { new ParseError { Message = "System image loading disabled - shadow classes removed" } }
        };
    }
}

/// <summary>
/// Result of system image loading operation.
/// </summary>
public class SystemImageLoadResult
{
    public string SourcePath { get; set; } = "";
    public bool Success { get; set; } = false;
    public List<ParseError> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new(); // Missing property added
    public int ClassesLoaded { get; set; } = 0;
    public int MethodsLoaded { get; set; } = 0;
}

/// <summary>
/// Parse error information.
/// </summary>
public class ParseError
{
    public string Message { get; set; } = "";
    public int Line { get; set; } = 0;
    public int Column { get; set; } = 0;
}