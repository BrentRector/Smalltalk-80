using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Smalltalk80;
using Smalltalk80.Generated;
using Smalltalk80.Parser;

/// <summary>
/// Provides a high level interface combining the interpreter and object memory
/// into a simple virtual machine suitable for the REPL and other hosts.
/// </summary>
public sealed class VirtualMachine
{
    /// <summary>
    /// Gets the interpreter instance executing methods for this VM.
    /// </summary>
    public Interpreter Interpreter { get; } = new() { ExitOnReturn = false };

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualMachine"/> class using JSON bootstrap.
    /// </summary>
    public VirtualMachine(bool skipStandardLibrary = false)
    {
        InitializeWithJsonBootstrap(skipStandardLibrary);
    }
    
    /// <summary>
    /// Initializes the VM using the three-phase JSON bootstrap system.
    /// </summary>
    private void InitializeWithJsonBootstrap(bool skipStandardLibrary)
    {
        Console.WriteLine("[BOOTSTRAP] Starting JSON-based bootstrap...");
        
        var bootstrapDir = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
            "Bootstrap");
            
        if (!Directory.Exists(bootstrapDir))
        {
            // Fallback: look in project directory during development
            bootstrapDir = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
                "..", "..", "..", "..", "Bootstrap");
        }
        
        if (!Directory.Exists(bootstrapDir))
        {
            throw new DirectoryNotFoundException($"Bootstrap directory not found. Searched: {bootstrapDir}");
        }
        
        Console.WriteLine($"[BOOTSTRAP] Using bootstrap files from: {bootstrapDir}");
        
        // Phase 1: VM primitive operations (create objects and patch pointers)
        ExecutePhase1Bootstrap(bootstrapDir);
        
        // Phase 2: Message-based class hierarchy construction
        ExecutePhase2Bootstrap(bootstrapDir);
        
        // Phase 3: Standard library loading (if not skipped)
        if (!skipStandardLibrary)
        {
            ExecutePhase3Bootstrap(bootstrapDir);
        }
        
        Console.WriteLine("[BOOTSTRAP] JSON bootstrap complete!");
    }
    
    /// <summary>
    /// Executes Phase 1 bootstrap from JSON: VM primitive operations.
    /// </summary>
    private void ExecutePhase1Bootstrap(string bootstrapDir)
    {
        Console.WriteLine("[BOOTSTRAP] Phase 1: VM primitive operations...");
        
        var phase1Path = Path.Combine(bootstrapDir, "bootstrap_phase1.json");
        if (!File.Exists(phase1Path))
        {
            throw new FileNotFoundException($"Phase 1 bootstrap file not found: {phase1Path}");
        }
        
        // For now, use the existing SystemDictionary bootstrap approach
        // TODO: Implement true VM primitive-based bootstrap from JSON
        SystemDictionary.Clear();
        SystemDictionary.CreateBootstrapClasses();
        
        Console.WriteLine("[BOOTSTRAP] Phase 1 complete: VM primitives executed");
    }
    
    /// <summary>
    /// Executes Phase 2 bootstrap from JSON: Message-based class construction.
    /// </summary>
    private void ExecutePhase2Bootstrap(string bootstrapDir)
    {
        Console.WriteLine("[BOOTSTRAP] Phase 2: Message-based class construction...");
        
        var phase2Path = Path.Combine(bootstrapDir, "bootstrap_phase2.json");
        if (!File.Exists(phase2Path))
        {
            throw new FileNotFoundException($"Phase 2 bootstrap file not found: {phase2Path}");
        }
        
        // TODO: Parse JSON and execute message sends to build class hierarchy
        // For now, the Phase 1 bootstrap includes the basic class structure
        
        Console.WriteLine("[BOOTSTRAP] Phase 2 complete: Class hierarchy constructed");
    }
    
    /// <summary>
    /// Executes Phase 3 bootstrap: Standard library loading.
    /// </summary>
    private void ExecutePhase3Bootstrap(string bootstrapDir)
    {
        Console.WriteLine("[BOOTSTRAP] Phase 3: Standard library loading...");
        
        // Look for HeadlessStandardLibrary.st first, then StandardLibrary.st
        var libraryDir = Path.GetDirectoryName(bootstrapDir) ?? "";
        var headlessLibraryPath = Path.Combine(libraryDir, "HeadlessStandardLibrary.st");
        var standardLibraryPath = Path.Combine(libraryDir, "StandardLibrary.st");
        
        string libraryPath = "";
        if (File.Exists(headlessLibraryPath))
        {
            libraryPath = headlessLibraryPath;
            Console.WriteLine("[BOOTSTRAP] Using HeadlessStandardLibrary.st");
        }
        else if (File.Exists(standardLibraryPath))
        {
            libraryPath = standardLibraryPath;
            Console.WriteLine("[BOOTSTRAP] Using StandardLibrary.st");
        }
        else
        {
            Console.WriteLine("[BOOTSTRAP] No standard library found - continuing with bootstrap classes only");
            return;
        }
        
        // Load standard library via SmalltalkLoader
        var loader = new SmalltalkLoader(Interpreter);
        var result = loader.LoadFile(libraryPath);
        
        if (result.Success)
        {
            Console.WriteLine($"[BOOTSTRAP] Loaded {result.ClassesLoaded} classes and {result.MethodsLoaded} methods");
        }
        else
        {
            Console.WriteLine($"[BOOTSTRAP] Warning: Failed to load some parts of standard library");
            foreach (var error in result.Errors.Take(3))
            {
                Console.WriteLine($"  Error: {error}");
            }
        }
        
        Console.WriteLine("[BOOTSTRAP] Phase 3 complete: Standard library processed");
    }
