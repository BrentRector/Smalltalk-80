using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Smalltalk80.Bootstrap;
using Smalltalk80.Exceptions;

/// <summary>
/// Orchestrates the complete Smalltalk-80 bootstrap process with proper phase management,
/// validation, and error recovery capabilities.
/// </summary>
public class BootstrapOrchestrator
{
    /// <summary>
    /// Represents the different phases of the bootstrap process.
    /// </summary>
    public enum BootstrapPhase
    {
        /// <summary>No bootstrap has been performed.</summary>
        None,
        /// <summary>Phase 1: Core classes without methods.</summary>
        Phase1_CoreClasses,
        /// <summary>Phase 2: Methods installed on core classes.</summary>
        Phase2_CoreMethods,
        /// <summary>Phase 3: Extended system classes and full functionality.</summary>
        Phase3_ExtendedSystem,
        /// <summary>Bootstrap completed successfully.</summary>
        Complete
    }
    
    private readonly VirtualMachine virtualMachine;
    private readonly Dictionary<BootstrapPhase, BootstrapCheckpoint> checkpoints = new();
    private BootstrapPhase currentPhase = BootstrapPhase.None;
    private readonly List<string> bootstrapLog = new();
    private VMPrimitiveAdapter? bootstrapAdapter;
    
    /// <summary>
    /// Gets the current bootstrap phase.
    /// </summary>
    public BootstrapPhase CurrentPhase => currentPhase;
    
    /// <summary>
    /// Gets the bootstrap log messages.
    /// </summary>
    public IReadOnlyList<string> BootstrapLog => bootstrapLog;
    
    /// <summary>
    /// Gets whether the bootstrap process is complete.
    /// </summary>
    public bool IsComplete => currentPhase == BootstrapPhase.Complete;
    
    /// <summary>
    /// Gets the bootstrap adapter for VM operations.
    /// </summary>
    public VMPrimitiveAdapter? Adapter => bootstrapAdapter;
    
    /// <summary>
    /// Initializes a new BootstrapOrchestrator for the specified virtual machine.
    /// </summary>
    /// <param name="vm">The virtual machine to bootstrap.</param>
    public BootstrapOrchestrator(VirtualMachine vm)
    {
        virtualMachine = vm ?? throw new ArgumentNullException(nameof(vm));
    }
    
    /// <summary>
    /// Performs the complete bootstrap process with all phases.
    /// </summary>
    /// <param name="bootstrapDirectory">Directory containing bootstrap files.</param>
    /// <param name="skipExtendedSystem">Whether to skip Phase 3 extended system loading.</param>
    /// <returns>Bootstrap statistics and performance information.</returns>
    public BootstrapResult ExecuteCompleteBootstrap(string? bootstrapDirectory = null, bool skipExtendedSystem = false)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new BootstrapResult();
        
        try
        {
            LogMessage("Starting complete Smalltalk-80 bootstrap process");
            
            // Resolve bootstrap directory
            var bootstrapDir = ResolveBootstrapDirectory(bootstrapDirectory);
            result.BootstrapDirectory = bootstrapDir;
            
            // Execute bootstrap phases in sequence
            ExecutePhase1_CoreClasses(bootstrapDir);
            result.Phase1Stats = CapturePhaseStats("Phase 1");
            
            ExecutePhase2_CoreMethods(bootstrapDir);
            result.Phase2Stats = CapturePhaseStats("Phase 2");
            
            if (!skipExtendedSystem)
            {
                ExecutePhase3_ExtendedSystem(bootstrapDir);
                result.Phase3Stats = CapturePhaseStats("Phase 3");
            }
            
            CompleteBootstrap();
            result.Success = true;
            
            stopwatch.Stop();
            result.TotalTime = stopwatch.Elapsed;
            result.ObjectCount = ObjectMemory.AllocatedObjectCount;
            result.GCStats = ObjectMemory.GetAdaptiveGCStats();
            
            LogMessage($"Bootstrap completed successfully in {stopwatch.ElapsedMilliseconds}ms");
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.Error = ex;
            result.TotalTime = stopwatch.Elapsed;
            
            LogMessage($"Bootstrap failed: {ex.Message}");
            throw new BootstrapException($"Bootstrap failed in {currentPhase}", currentPhase.ToString(), null, ex);
        }
    }
    
    /// <summary>
    /// Executes Phase 1: Core class structures without methods.
    /// </summary>
    /// <param name="bootstrapDir">Bootstrap directory path.</param>
    public void ExecutePhase1_CoreClasses(string bootstrapDir)
    {
        if (currentPhase != BootstrapPhase.None)
            throw new InvalidOperationException($"Cannot execute Phase 1 from current phase: {currentPhase}");
            
        LogMessage("Phase 1: Creating core class structures");
        
        try
        {
            // Save checkpoint before starting
            SaveCheckpoint(BootstrapPhase.None);
            
            // Clear object memory for fresh start
            ObjectMemory.Reset();
            LogMessage("Cleared VM object memory");
            
            // Load bootstrap file
            var bootstrapPath = FindBootstrapFile(bootstrapDir, "bootstrap.json", "bootstrap_phase1.json");
            var jsonContent = File.ReadAllText(bootstrapPath);
            
            // Initialize bootstrap adapter
            bootstrapAdapter = new VMPrimitiveAdapter(virtualMachine);
            
            // Execute core class creation
            var executor = new JsonBootstrapExecutor(bootstrapAdapter);
            executor.ExecuteBootstrapClasses(jsonContent);
            
            currentPhase = BootstrapPhase.Phase1_CoreClasses;
            SaveCheckpoint(currentPhase);
            
            LogMessage($"Phase 1 complete: Created {ObjectMemory.AllocatedObjectCount} objects");
        }
        catch (Exception ex)
        {
            throw new BootstrapException("Phase 1 failed", "Phase1_CoreClasses", bootstrapDir, ex);
        }
    }
    
    /// <summary>
    /// Executes Phase 2: Install methods on core classes.
    /// </summary>
    /// <param name="bootstrapDir">Bootstrap directory path.</param>
    public void ExecutePhase2_CoreMethods(string bootstrapDir)
    {
        if (currentPhase != BootstrapPhase.Phase1_CoreClasses)
            throw new InvalidOperationException($"Cannot execute Phase 2 from current phase: {currentPhase}");
            
        LogMessage("Phase 2: Installing methods on core classes");
        
        try
        {
            if (bootstrapAdapter == null)
                throw new InvalidOperationException("Bootstrap adapter not initialized");
                
            // Load bootstrap file with methods
            var bootstrapPath = FindBootstrapFile(bootstrapDir, "bootstrap.json", "bootstrap_phase2.json");
            var jsonContent = File.ReadAllText(bootstrapPath);
            
            // Execute method installation
            var executor = new JsonBootstrapExecutor(bootstrapAdapter);
            executor.ExecuteUnifiedBootstrap(jsonContent);
            
            // Migrate to Smalltalk dictionary now that at:put: method is available
            bootstrapAdapter.CompleteBootstrapWithMessageSends();
            
            currentPhase = BootstrapPhase.Phase2_CoreMethods;
            SaveCheckpoint(currentPhase);
            
            LogMessage($"Phase 2 complete: Core system operational with {ObjectMemory.AllocatedObjectCount} objects");
        }
        catch (Exception ex)
        {
            throw new BootstrapException("Phase 2 failed", "Phase2_CoreMethods", bootstrapDir, ex);
        }
    }
    
    /// <summary>
    /// Executes Phase 3: Load extended system classes.
    /// </summary>
    /// <param name="bootstrapDir">Bootstrap directory path.</param>
    public void ExecutePhase3_ExtendedSystem(string bootstrapDir)
    {
        if (currentPhase != BootstrapPhase.Phase2_CoreMethods)
            throw new InvalidOperationException($"Cannot execute Phase 3 from current phase: {currentPhase}");
            
        LogMessage("Phase 3: Loading extended system classes");
        
        try
        {
            if (bootstrapAdapter == null)
                throw new InvalidOperationException("Bootstrap adapter not initialized");
                
            // Look for extended system image
            var systemImagePath = FindExtendedSystemImage(bootstrapDir);
            if (systemImagePath == null)
            {
                LogMessage("Warning: Extended system image not found, skipping Phase 3");
                currentPhase = BootstrapPhase.Phase3_ExtendedSystem;
                return;
            }
            
            LogMessage($"Loading extended system from: {systemImagePath}");
            
            // Define essential classes to load
            var essentialClasses = GetEssentialExtendedClasses();
            
            // Load extended classes
            var executor = new JsonBootstrapExecutor(bootstrapAdapter);
            executor.ExecuteLowLevelClasses(systemImagePath, essentialClasses);
            
            currentPhase = BootstrapPhase.Phase3_ExtendedSystem;
            SaveCheckpoint(currentPhase);
            
            LogMessage($"Phase 3 complete: Extended system loaded with {ObjectMemory.AllocatedObjectCount} objects");
        }
        catch (Exception ex)
        {
            throw new BootstrapException("Phase 3 failed", "Phase3_ExtendedSystem", bootstrapDir, ex);
        }
    }
    
    /// <summary>
    /// Completes the bootstrap process and performs final validation.
    /// </summary>
    public void CompleteBootstrap()
    {
        if (currentPhase != BootstrapPhase.Phase3_ExtendedSystem && currentPhase != BootstrapPhase.Phase2_CoreMethods)
            throw new InvalidOperationException($"Cannot complete bootstrap from current phase: {currentPhase}");
            
        LogMessage("Completing bootstrap and performing final validation");
        
        try
        {
            // Perform final validation
            ValidateBootstrapCompleteness();
            
            // Optimize memory after bootstrap
            ObjectMemory.ForceGC();
            
            currentPhase = BootstrapPhase.Complete;
            LogMessage("Bootstrap completed successfully");
        }
        catch (Exception ex)
        {
            throw new BootstrapException("Bootstrap completion failed", "Complete", null, ex);
        }
    }
    
    /// <summary>
    /// Validates that the bootstrap completed successfully.
    /// </summary>
    private void ValidateBootstrapCompleteness()
    {
        var requiredClasses = new[] { "Object", "Behavior", "Class", "Metaclass", "CompiledMethod", "Symbol", "String" };
        var missingClasses = new List<string>();
        
        if (bootstrapAdapter == null)
            throw new InvalidOperationException("Bootstrap adapter not available for validation");
            
        foreach (var className in requiredClasses)
        {
            var classOop = bootstrapAdapter.PrimitiveLookupClass(className);
            if (classOop < 0)
            {
                missingClasses.Add(className);
            }
        }
        
        if (missingClasses.Count > 0)
        {
            throw new BootstrapException($"Missing required classes: {string.Join(", ", missingClasses)}", "Validation");
        }
        
        LogMessage($"Bootstrap validation passed: All {requiredClasses.Length} required classes present");
    }
    
    /// <summary>
    /// Rolls back to a previous bootstrap phase.
    /// </summary>
    /// <param name="targetPhase">The phase to roll back to.</param>
    public void RollbackToPhase(BootstrapPhase targetPhase)
    {
        if (targetPhase > currentPhase)
            throw new ArgumentException($"Cannot roll back from {currentPhase} to future phase {targetPhase}");
            
        if (!checkpoints.TryGetValue(targetPhase, out var checkpoint))
            throw new InvalidOperationException($"No checkpoint available for phase {targetPhase}");
            
        LogMessage($"Rolling back from {currentPhase} to {targetPhase}");
        
        try
        {
            RestoreCheckpoint(checkpoint);
            currentPhase = targetPhase;
            LogMessage($"Rollback completed to phase {targetPhase}");
        }
        catch (Exception ex)
        {
            throw new BootstrapException($"Rollback to {targetPhase} failed", targetPhase.ToString(), null, ex);
        }
    }
    
    /// <summary>
    /// Saves a checkpoint of the current system state.
    /// </summary>
    /// <param name="phase">The phase to checkpoint.</param>
    private void SaveCheckpoint(BootstrapPhase phase)
    {
        var checkpoint = new BootstrapCheckpoint
        {
            Phase = phase,
            ObjectCount = ObjectMemory.AllocatedObjectCount,
            GCStats = ObjectMemory.GetAdaptiveGCStats(),
            Timestamp = DateTime.UtcNow
        };
        
        checkpoints[phase] = checkpoint;
        LogMessage($"Saved checkpoint for phase {phase} with {checkpoint.ObjectCount} objects");
    }
    
    /// <summary>
    /// Restores a checkpoint (simplified implementation).
    /// </summary>
    /// <param name="checkpoint">The checkpoint to restore.</param>
    private void RestoreCheckpoint(BootstrapCheckpoint checkpoint)
    {
        // For now, this is a simplified implementation that resets memory
        // A full implementation would save/restore actual object state
        ObjectMemory.Reset();
        LogMessage($"Restored checkpoint for phase {checkpoint.Phase}");
    }
    
    /// <summary>
    /// Resolves the bootstrap directory path.
    /// </summary>
    private string ResolveBootstrapDirectory(string? providedPath)
    {
        if (!string.IsNullOrEmpty(providedPath) && Directory.Exists(providedPath))
            return providedPath;
            
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var searchPaths = new[]
        {
            Path.Combine(Path.GetDirectoryName(assemblyLocation) ?? ".", "Bootstrap"),
            Path.Combine(Path.GetDirectoryName(assemblyLocation) ?? ".", "..", "..", "..", "..", "Bootstrap"),
            Path.Combine(Directory.GetCurrentDirectory(), "Bootstrap")
        };
        
        foreach (var path in searchPaths)
        {
            if (Directory.Exists(path))
                return path;
        }
        
        throw new BootstrapException("Bootstrap directory not found", "DirectoryResolution");
    }
    
    /// <summary>
    /// Finds a bootstrap file, trying multiple possible names.
    /// </summary>
    private string FindBootstrapFile(string bootstrapDir, params string[] fileNames)
    {
        foreach (var fileName in fileNames)
        {
            var path = Path.Combine(bootstrapDir, fileName);
            if (File.Exists(path))
                return path;
        }
        
        throw BootstrapException.FileNotFound(fileNames[0], bootstrapDir);
    }
    
    /// <summary>
    /// Finds the extended system image file.
    /// </summary>
    private string? FindExtendedSystemImage(string bootstrapDir)
    {
        var searchPaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "complete_system_final.json"),
            Path.Combine(bootstrapDir, "..", "complete_system_final.json"),
            Path.Combine(bootstrapDir, "..", "..", "complete_system_final.json"),
            "complete_system_final.json"
        };
        
        return searchPaths.FirstOrDefault(File.Exists);
    }
    
    /// <summary>
    /// Gets the list of essential extended classes to load in Phase 3.
    /// </summary>
    private List<string> GetEssentialExtendedClasses()
    {
        return new List<string>
        {
            // Core system classes
            "BlockContext", "MethodContext", "ContextPart",
            "SmallInteger", "LargePositiveInteger", "LargeNegativeInteger", "Integer", "Number",
            "Float", "Fraction",
            
            // Collections
            "Set", "IdentitySet", "Bag", "OrderedCollection", "SortedCollection",
            "LinkedList", "Interval",
            
            // Streams
            "Stream", "PositionableStream", "ReadStream", "WriteStream", "ReadWriteStream",
            
            // Characters and Strings
            "Character", "Text", "Paragraph", "TextStyle",
            
            // Processes and Scheduling
            "Process", "ProcessorScheduler", "Semaphore", "Delay",
            
            // Exceptions
            "Exception", "Error", "Halt", "MessageNotUnderstood",
            
            // Compiler and Parser
            "Parser", "Scanner", "Compiler", "CompilationContext",
            
            // File System
            "File", "FileDirectory", "FileStream",
            
            // Graphics (basic)
            "Point", "Rectangle", "Form", "BitBlt"
        };
    }
    
    /// <summary>
    /// Captures statistics for a completed phase.
    /// </summary>
    private BootstrapPhaseStats CapturePhaseStats(string phaseName)
    {
        return new BootstrapPhaseStats
        {
            Phase = phaseName,
            ObjectCount = ObjectMemory.AllocatedObjectCount,
            MemoryStats = ObjectMemory.GetMemoryStats(),
            TypeIndexStats = ObjectMemory.GetTypeIndexStats(),
            GCStats = ObjectMemory.GetAdaptiveGCStats(),
            Timestamp = DateTime.UtcNow
        };
    }
    
    /// <summary>
    /// Logs a message to the bootstrap log.
    /// </summary>
    private void LogMessage(string message)
    {
        var logEntry = $"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}";
        bootstrapLog.Add(logEntry);
        Console.WriteLine(logEntry);
    }
}

/// <summary>
/// Represents a checkpoint of the bootstrap state.
/// </summary>
public record BootstrapCheckpoint
{
    public BootstrapOrchestrator.BootstrapPhase Phase { get; init; }
    public int ObjectCount { get; init; }
    public AdaptiveGCStats GCStats { get; init; } = new();
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Contains the results of a bootstrap operation.
/// </summary>
public record BootstrapResult
{
    public bool Success { get; set; }
    public Exception? Error { get; set; }
    public TimeSpan TotalTime { get; set; }
    public string? BootstrapDirectory { get; set; }
    public int ObjectCount { get; set; }
    public AdaptiveGCStats GCStats { get; set; } = new();
    public BootstrapPhaseStats? Phase1Stats { get; set; }
    public BootstrapPhaseStats? Phase2Stats { get; set; }
    public BootstrapPhaseStats? Phase3Stats { get; set; }
}

/// <summary>
/// Statistics for a completed bootstrap phase.
/// </summary>
public record BootstrapPhaseStats
{
    public string Phase { get; init; } = string.Empty;
    public int ObjectCount { get; init; }
    public (int allocated, int free, int total, int collections) MemoryStats { get; init; }
    public (int classesIndexed, int totalInstances) TypeIndexStats { get; init; }
    public AdaptiveGCStats GCStats { get; init; } = new();
    public DateTime Timestamp { get; init; }
}