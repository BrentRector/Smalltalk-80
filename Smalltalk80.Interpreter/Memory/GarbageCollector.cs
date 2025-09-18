using System;
using System.Collections.Generic;
using Smalltalk80.Interpreter.ObjectModel;

namespace Smalltalk80.Interpreter.Memory;
/// <summary>
/// Mark-and-sweep garbage collector.
/// Traces from Special Objects Array roots and collects unreachable objects.
/// </summary>
/// <param name="objectMemory">The object memory to manage.</param>
public sealed class GarbageCollector(ObjectMemory objectMemory)
{
    private readonly ObjectMemory objectMemory = objectMemory ?? throw new ArgumentNullException(nameof(objectMemory));
    private readonly HashSet<OOP> marked = [];
    private int lastCollectedCount;

    /// <summary>
    /// Performs a full garbage collection cycle.
    /// </summary>
    public GCStatistics Collect(OOP[] specialObjectsArray)
    {
        var startTime = DateTime.UtcNow;
        
        // Clear previous marks
        marked.Clear();
        ClearAllMarks();
        
        // Mark phase - trace from roots
        MarkRoots(specialObjectsArray);
        var markedCount = marked.Count;
        
        // Sweep phase - collect unmarked objects
        SweepUnmarked();
        
        // Calculate statistics
        return new GCStatistics 
        { 
            StartTime = startTime,
            EndTime = DateTime.UtcNow,
            MarkedCount = markedCount,
            CollectedCount = lastCollectedCount,
            SurvivingCount = marked.Count
        };
    }

    private void ClearAllMarks()
    {
        foreach (var (_, layout) in objectMemory.EnumerateObjects())
        {
            layout.Header.IsMarked = false;
        }
    }

    private void MarkRoots(OOP[] specialObjectsArray)
    {
        // Mark all objects reachable from Special Objects Array
        foreach (var root in specialObjectsArray)
        {
            if (!root.IsNil)
            {
                MarkReachable(root);
            }
        }
    }

    private void MarkReachable(OOP oop)
    {
        // Skip if already marked or not an object
        if (oop.IsSmallInteger || oop.IsNil || marked.Contains(oop))
            return;
        
        // Get the object layout
        var layout = objectMemory.GetObject(oop);
        if (layout == null)
            return;
        
        // Skip if permanent
        if (layout.Header.IsPermanent)
            return;
        
        // Mark this object
        marked.Add(oop);
        layout.Header.IsMarked = true;
        
        // Mark the class
        MarkReachable(layout.Header.ClassOOP);
        
        // If object has pointers, recursively mark all fields
        if (layout.HasPointers)
        {
            foreach (var field in layout.Fields)
            {
                if (!field.IsSmallInteger && !field.IsNil)
                {
                    MarkReachable(field);
                }
            }
        }
    }

    private void SweepUnmarked()
    {
        List<uint> toRemove = [];
        
        // Find all unmarked objects
        foreach (var (oop, layout) in objectMemory.EnumerateObjects())
        {
            if (!layout.Header.IsMarked && !layout.Header.IsPermanent)
            {
                toRemove.Add(oop.ObjectIndex);
            }
        }
        
        // Remove unmarked objects
        lastCollectedCount = toRemove.Count;
        foreach (var index in toRemove)
        {
            // Note: In a real implementation, we'd need to update the ObjectTable
            // to support removal. For now, this is a placeholder.
            // objectMemory.RemoveObject(index);
        }
    }

    /// <summary>
    /// Gets the number of objects collected in the last GC cycle.
    /// </summary>
    public int LastCollectedCount => lastCollectedCount;
}

/// <summary>
/// Statistics from a garbage collection cycle.
/// </summary>
public sealed record GCStatistics
{
    public required DateTime StartTime { get; init; }
    public required DateTime EndTime { get; init; }
    public required int MarkedCount { get; init; }
    public required int CollectedCount { get; init; }
    public required int SurvivingCount { get; init; }
    
    public TimeSpan Duration => EndTime - StartTime;
    
    public override string ToString() =>
        $"GC: Marked={MarkedCount}, Collected={CollectedCount}, Surviving={SurvivingCount}, Duration={Duration.TotalMilliseconds:F1}ms";
}