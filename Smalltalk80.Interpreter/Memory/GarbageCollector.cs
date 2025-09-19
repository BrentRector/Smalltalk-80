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
        bool firstReported = false;
        
        // Find all unmarked objects
        foreach (var (oop, layout) in objectMemory.EnumerateObjects())
        {
            if (!layout.Header.IsMarked && !layout.Header.IsPermanent)
            {
                // Report details of first object being collected
                if (!firstReported)
                {
                    Console.WriteLine($"\n[GC] FIRST UNREACHABLE OBJECT:");
                    Console.WriteLine($"  OOP: {oop}");
                    Console.WriteLine($"  Class OOP: {layout.Header.ClassOOP}");
                    Console.WriteLine($"  Field Count: {layout.Fields.Length}");
                    Console.WriteLine($"  HasPointers: {layout.HasPointers}");
                    Console.WriteLine($"  IsBytes: {layout.IsBytes}");
                    Console.WriteLine($"  Format: 0x{layout.Header.Format:X2}");
                    
                    // Show first few fields if any
                    if (layout.Fields.Length > 0)
                    {
                        Console.WriteLine($"  First fields:");
                        for (int i = 0; i < Math.Min(4, layout.Fields.Length); i++)
                        {
                            Console.WriteLine($"    Field[{i}]: {layout.Fields[i]}");
                        }
                    }
                    
                    // Try to identify what class this is
                    var classObj = objectMemory.GetObject(layout.Header.ClassOOP);
                    if (classObj != null && classObj.Fields.Length > 6)
                    {
                        var nameOop = classObj.Fields[6]; // name field is typically at index 6
                        var nameObj = objectMemory.GetObject(nameOop);
                        if (nameObj != null && nameObj.IsBytes)
                        {
                            var bytes = new System.Collections.Generic.List<byte>();
                            foreach (var field in nameObj.Fields)
                            {
                                if (field.IsSmallInteger)
                                    bytes.Add((byte)field.SmallIntegerValue);
                            }
                            var className = System.Text.Encoding.UTF8.GetString(bytes.ToArray());
                            Console.WriteLine($"  Class Name: {className}");
                        }
                    }
                    
                    // If it's a CompiledMethod (has exactly 4 pointer fields: selector, bytecode, literals, header)
                    if (layout.Fields.Length == 4 && layout.HasPointers && !layout.IsBytes)
                    {
                        var selectorOop = layout.Fields[0];
                        if (!selectorOop.IsNil && !selectorOop.IsSmallInteger)
                        {
                            var selectorObj = objectMemory.GetObject(selectorOop);
                            if (selectorObj != null && selectorObj.IsBytes)
                            {
                                var bytes = new System.Collections.Generic.List<byte>();
                                foreach (var field in selectorObj.Fields)
                                {
                                    if (field.IsSmallInteger)
                                        bytes.Add((byte)field.SmallIntegerValue);
                                }
                                var selectorName = System.Text.Encoding.UTF8.GetString(bytes.ToArray());
                                Console.WriteLine($"  Method Selector: {selectorName}");
                                
                                // Also show bytecode size
                                var bytecodeOop = layout.Fields[1];
                                if (!bytecodeOop.IsNil)
                                {
                                    var bytecodeObj = objectMemory.GetObject(bytecodeOop);
                                    if (bytecodeObj != null && bytecodeObj.IsBytes)
                                    {
                                        Console.WriteLine($"  Bytecode length: {bytecodeObj.Fields.Length} bytes");
                                    }
                                }
                            }
                        }
                    }
                    Console.WriteLine();
                    firstReported = true;
                }
                
                // Only show debug for unexpected cases (objects with fields)
                if (layout.Fields.Length > 0)
                {
                    Console.WriteLine($"[GC] Collecting unreachable object: OOP={oop}, Class={layout.Header.ClassOOP}, " +
                                    $"FieldCount={layout.Fields.Length}");
                }
                
                toRemove.Add(oop.ObjectIndex);
            }
        }
        
        // Remove unmarked objects
        lastCollectedCount = toRemove.Count;
        foreach (var index in toRemove)
        {
            objectMemory.RemoveObject(index);
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