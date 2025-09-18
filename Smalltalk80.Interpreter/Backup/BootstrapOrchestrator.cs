using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Smalltalk80.Bootstrap;
using Smalltalk80.Exceptions;

/// <summary>
/// Orchestrates the complete Smalltalk-80 bootstrap process with proper state management,
/// validation, and error recovery capabilities.
/// </summary>
public class BootstrapOrchestrator
{
    /// <summary>
    /// Represents the different states of the bootstrap process.
    /// </summary>
    public enum BootstrapState
    {
        /// <summary>No bootstrap has been performed.</summary>
        None,
        /// <summary>Legacy: Core classes without methods (deprecated).</summary>
        LegacyCoreClasses,
        /// <summary>Legacy: Methods installed on core classes (deprecated).</summary>
        LegacyCoreMethods,
        /// <summary>Legacy: Extended system classes and full functionality (deprecated).</summary>
        LegacyExtendedSystem,
        /// <summary>Bootstrap completed successfully.</summary>
        Complete
    }
    
    private readonly VirtualMachine virtualMachine;
    private readonly Dictionary<BootstrapState, BootstrapCheckpoint> checkpoints = new();
    private BootstrapState currentState = BootstrapState.None;
    private readonly List<string> bootstrapLog = new();
    private VMPrimitiveAdapter? bootstrapAdapter;
    
    /// <summary>
    /// Gets the current bootstrap state.
    /// </summary>
    public BootstrapState CurrentState => currentState;
    
    /// <summary>
    /// Gets the bootstrap log messages.
    /// </summary>
    public IReadOnlyList<string> BootstrapLog => bootstrapLog;
    
    /// <summary>
    /// Gets whether the bootstrap process is complete.
    /// </summary>
    public bool IsComplete => currentState == BootstrapState.Complete;
    
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
    /// <returns>Bootstrap statistics and performance information.</returns>
    public BootstrapResult ExecuteCompleteBootstrap(string? bootstrapDirectory = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new BootstrapResult();
        
        try
        {
            LogMessage("Starting complete Smalltalk-80 bootstrap process");
            
            // Resolve bootstrap directory
            var bootstrapDir = ResolveBootstrapDirectory(bootstrapDirectory);
            result.BootstrapDirectory = bootstrapDir;
            
            // Execute consolidated bootstrap (now includes all classes)
            ExecuteConsolidatedBootstrap(bootstrapDir);
            result.BootstrapStats = CaptureStats("Bootstrap");
            
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
            throw new BootstrapException($"Bootstrap failed in {currentState}", currentState.ToString(), null, ex);
        }
    }
    
    /// <summary>
    /// Executes the unified bootstrap process.
    /// </summary>
    /// <param name="bootstrapDir">Bootstrap directory path.</param>
    public void ExecuteConsolidatedBootstrap(string bootstrapDir)
    {
        if (currentState != BootstrapState.None)
            throw new InvalidOperationException($"Cannot execute consolidated bootstrap from current state: {currentState}");
            
        LogMessage("Executing clean slate system image bootstrap");
        
        try
        {
            // Save checkpoint before starting
            SaveCheckpoint(BootstrapState.None);
            
            // Clear object memory for fresh start
            ObjectMemory.Reset();
            LogMessage("Cleared VM object memory");
            
            // Determine system image path (complete_system_final.json is the single source of truth)
            var systemImagePath = Path.Combine(
                Path.GetDirectoryName(bootstrapDir) ?? "", 
                "complete_system_final.json");
            
            if (!File.Exists(systemImagePath))
            {
                // Fallback to local system image
                systemImagePath = "complete_system_final.json";
                if (!File.Exists(systemImagePath))
                {
                    throw new FileNotFoundException($"System image not found. Tried: {systemImagePath}");
                }
            }
            
            // Create bootstrap adapter for VM operations
            bootstrapAdapter = new VMPrimitiveAdapter(virtualMachine);
            
            // Execute clean slate bootstrap directly from system image JSON
            var executor = new SystemImageBootstrapExecutor(virtualMachine, bootstrapAdapter, systemImagePath);
            executor.ExecuteBootstrap();
            
            currentState = BootstrapState.Complete; // Mark as complete
            SaveCheckpoint(currentState);
            
            LogMessage($"Clean slate bootstrap complete: Created {ObjectMemory.AllocatedObjectCount} objects");
        }
        catch (Exception ex)
        {
            throw new BootstrapException("Consolidated bootstrap failed", "ConsolidatedBootstrap", bootstrapDir, ex);
        }
    }
    
    /// <summary>
    /// Legacy method - deprecated in favor of unified bootstrap.
    /// </summary>
    [Obsolete("Use ExecuteConsolidatedBootstrap instead")]
    public void ExecuteLegacyCoreClasses(string bootstrapDir)
    {
        throw new NotSupportedException("Legacy bootstrap is deprecated. Use ExecuteConsolidatedBootstrap for unified bootstrap.");
    }
    
    /// <summary>
    /// Legacy method - deprecated in favor of unified bootstrap.
    /// </summary>
    [Obsolete("Use ExecuteConsolidatedBootstrap instead")]
    public void ExecuteLegacyCoreMethods(string bootstrapDir)
    {
        throw new NotSupportedException("Legacy bootstrap is deprecated. Use ExecuteConsolidatedBootstrap for unified bootstrap.");
    }
    
    
    /// <summary>
    /// Completes the bootstrap process and performs final validation.
    /// </summary>
    public void CompleteBootstrap()
    {
        // If already complete, just return - don't re-validate
        if (currentState == BootstrapState.Complete)
        {
            LogMessage("Bootstrap already complete, skipping re-validation");
            return;
        }
            
        // Bootstrap can be completed from various states
        // (legacy states or after unified bootstrap)
        LogMessage("Completing bootstrap and performing validation");
        
        try
        {
            // Perform final validation
            ValidateBootstrapCompleteness();
            
            // Optimize memory after bootstrap
            ObjectMemory.ForceGC();
            
            currentState = BootstrapState.Complete;
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
    /// Rolls back to a previous bootstrap state.
    /// </summary>
    /// <param name="targetState">The state to roll back to.</param>
    public void RollbackToState(BootstrapState targetState)
    {
        if (targetState > currentState)
            throw new ArgumentException($"Cannot roll back from {currentState} to future state {targetState}");
            
        if (!checkpoints.TryGetValue(targetState, out var checkpoint))
            throw new InvalidOperationException($"No checkpoint available for state {targetState}");
            
        LogMessage($"Rolling back from {currentState} to {targetState}");
        
        try
        {
            RestoreCheckpoint(checkpoint);
            currentState = targetState;
            LogMessage($"Rollback completed to state {targetState}");
        }
        catch (Exception ex)
        {
            throw new BootstrapException($"Rollback to {targetState} failed", targetState.ToString(), null, ex);
        }
    }
    
    /// <summary>
    /// Saves a checkpoint of the current system state.
    /// </summary>
    /// <param name="state">The state to checkpoint.</param>
    private void SaveCheckpoint(BootstrapState state)
    {
        var checkpoint = new BootstrapCheckpoint
        {
            State = state,
            ObjectCount = ObjectMemory.AllocatedObjectCount,
            GCStats = ObjectMemory.GetAdaptiveGCStats(),
            Timestamp = DateTime.UtcNow
        };
        
        checkpoints[state] = checkpoint;
        
        // Only log meaningful checkpoints (skip empty "None" state)
        if (state != BootstrapState.None || checkpoint.ObjectCount > 0)
        {
            LogMessage($"Saved checkpoint for state {state} with {checkpoint.ObjectCount} objects");
        }
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
        LogMessage($"Restored checkpoint for state {checkpoint.State}");
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
    /// Captures statistics for completed operations.
    /// </summary>
    private BootstrapStats CaptureStats(string operationName)
    {
        return new BootstrapStats
        {
            Operation = operationName,
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
    public BootstrapOrchestrator.BootstrapState State { get; init; }
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
    public BootstrapStats? BootstrapStats { get; set; }
    public BootstrapStats? LegacyStats { get; set; }
    public BootstrapStats? ExtendedStats { get; set; }
}

/// <summary>
/// Statistics for completed bootstrap operations.
/// </summary>
public record BootstrapStats
{
    public string Operation { get; init; } = string.Empty;
    public int ObjectCount { get; init; }
    public (int allocated, int free, int total, int collections) MemoryStats { get; init; }
    public (int classesIndexed, int totalInstances) TypeIndexStats { get; init; }
    public AdaptiveGCStats GCStats { get; init; } = new();
    public DateTime Timestamp { get; init; }
}