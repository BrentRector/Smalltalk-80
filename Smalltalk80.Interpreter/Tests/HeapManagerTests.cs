using Xunit;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Tests;

/// <summary>
/// Unit tests for managed storage implementation in SmalltalkHeapManager.
/// Tests verify allocation, GC, bounds checking, and format validation.
/// </summary>
public class HeapManagerTests
{
    [Fact]
    public void ManagedStorage_AllocationFreeReuse_Success()
    {
        var heap = new SmalltalkHeapManager();

        // Allocate 10 PointerIndexed objects
        var oops = new SmalltalkOOP[10];
        for (int i = 0; i < 10; i++)
            oops[i] = heap.AllocateObject(ObjectFormat.PointerIndexed, 0, 5);

        Assert.Equal(10, heap.GetActiveObjectCount());

        // GC with no roots - should free all objects
        heap.SetGCRoots(Array.Empty<SmalltalkOOP>());
        var stats = heap.GarbageCollect();

        Assert.Equal(0, heap.GetActiveObjectCount());
        Assert.Equal(10, stats.CollectedObjects);

        // Allocate 10 more objects - should reuse freed indices
        for (int i = 0; i < 10; i++)
            heap.AllocateObject(ObjectFormat.ByteIndexed, 0, 100);

        Assert.Equal(10, heap.GetActiveObjectCount());
    }

    [Fact]
    public void ManagedStorage_IndexedOOP_BoundsCheck_ThrowsException()
    {
        var heap = new SmalltalkHeapManager();
        var oop = heap.AllocateObject(ObjectFormat.PointerIndexed, 0, 3);

        // Valid indices: 0, 1, 2
        heap.SetIndexedOOP(oop, 0, SmalltalkOOP.FromSmallInteger(10));
        heap.SetIndexedOOP(oop, 2, SmalltalkOOP.FromSmallInteger(20));

        // Invalid index: 3 (out of range) - should throw
        Assert.Throws<ArgumentOutOfRangeException>(() => heap.GetIndexedOOP(oop, 3));
    }

    [Fact]
    public void ManagedStorage_ByteArray_ContentPersistence()
    {
        var heap = new SmalltalkHeapManager();
        var oop = heap.AllocateObject(ObjectFormat.ByteIndexed, 0, 5);

        // Write bytes
        heap.SetIndexedByte(oop, 0, 0x42);
        heap.SetIndexedByte(oop, 1, 0x43);
        heap.SetIndexedByte(oop, 4, 0xFF);

        // Read back and verify
        Assert.Equal((byte)0x42, heap.GetIndexedByte(oop, 0));
        Assert.Equal((byte)0x43, heap.GetIndexedByte(oop, 1));
        Assert.Equal((byte)0x00, heap.GetIndexedByte(oop, 2)); // Zero-initialized
        Assert.Equal((byte)0xFF, heap.GetIndexedByte(oop, 4));
    }

    [Fact]
    public void ManagedStorage_GC_ReachabilityGraph()
    {
        var heap = new SmalltalkHeapManager();

        // Create object chain: A -> B -> C
        var oopC = heap.AllocateObject(ObjectFormat.Pointer, 1, 0);
        var oopB = heap.AllocateObject(ObjectFormat.Pointer, 1, 0);
        var oopA = heap.AllocateObject(ObjectFormat.Pointer, 1, 0);

        // Verify allocation
        Assert.Equal(3, heap.GetActiveObjectCount());

        // Set up references: A[0] = B, B[0] = C
        heap.SetInstanceVariable(oopA, 0, oopB);
        heap.SetInstanceVariable(oopB, 0, oopC);

        // Test 1: Root = [A] => all objects retained
        heap.SetGCRoots(new[] { oopA });
        var stats1 = heap.GarbageCollect();
        Assert.Equal(3, heap.GetActiveObjectCount());
        Assert.Equal(0, stats1.CollectedObjects);

        // Test 2: Root = [] => all objects collected
        heap.SetGCRoots(Array.Empty<SmalltalkOOP>());
        var stats2 = heap.GarbageCollect();
        Assert.Equal(0, heap.GetActiveObjectCount());
        Assert.Equal(3, stats2.CollectedObjects);
    }

    [Fact]
    public void ManagedStorage_FormatValidation_ThrowsOnMismatch()
    {
        var heap = new SmalltalkHeapManager();

        // Allocate ByteIndexed object
        var oop = heap.AllocateObject(ObjectFormat.ByteIndexed, 0, 100);

        // Try to access as PointerIndexed - should throw
        Assert.Throws<InvalidOperationException>(() => heap.GetIndexedOOP(oop, 0));
    }

    [Fact]
    public void ManagedStorage_InstanceVariable_ReadWriteCorrect()
    {
        var heap = new SmalltalkHeapManager();

        // Allocate object with 3 instance variables
        var oop = heap.AllocateObject(ObjectFormat.Pointer, 3, 0);

        // Write instance variables
        heap.SetInstanceVariable(oop, 0, SmalltalkOOP.FromSmallInteger(100));
        heap.SetInstanceVariable(oop, 1, SmalltalkOOP.FromSmallInteger(200));
        heap.SetInstanceVariable(oop, 2, SmalltalkOOP.FromSmallInteger(300));

        // Read back and verify
        Assert.Equal(100, heap.GetInstanceVariable(oop, 0).AsSmallInteger());
        Assert.Equal(200, heap.GetInstanceVariable(oop, 1).AsSmallInteger());
        Assert.Equal(300, heap.GetInstanceVariable(oop, 2).AsSmallInteger());
    }

    [Fact]
    public void ManagedStorage_GetUnreachableObjects_ReturnsOOPs()
    {
        var heap = new SmalltalkHeapManager();

        // Allocate 5 objects
        var oop1 = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var oop2 = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var oop3 = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var oop4 = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var oop5 = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);

        // Root only oop1 and oop3
        heap.SetGCRoots(new[] { oop1, oop3 });

        // Get unreachable objects
        var unreachable = heap.GetUnreachableObjects();

        // Should return OOPs, not ints
        Assert.All(unreachable, oop => Assert.False(oop.IsSmallInteger));

        // Should have 3 unreachable objects (oop2, oop4, oop5)
        Assert.Equal(3, unreachable.Length);
    }

    [Fact]
    public void ManagedStorage_WordArray_ReadWriteCorrect()
    {
        var heap = new SmalltalkHeapManager();
        var oop = heap.AllocateObject(ObjectFormat.WordIndexed, 0, 4);

        // Write words
        heap.SetIndexedWord(oop, 0, 0x1234);
        heap.SetIndexedWord(oop, 1, 0x5678);
        heap.SetIndexedWord(oop, 3, 0xABCD);

        // Read back and verify
        Assert.Equal((ushort)0x1234, heap.GetIndexedWord(oop, 0));
        Assert.Equal((ushort)0x5678, heap.GetIndexedWord(oop, 1));
        Assert.Equal((ushort)0x0000, heap.GetIndexedWord(oop, 2)); // Zero-initialized
        Assert.Equal((ushort)0xABCD, heap.GetIndexedWord(oop, 3));
    }

    [Fact]
    public void ManagedStorage_MultipleGCCycles_Success()
    {
        var heap = new SmalltalkHeapManager();

        for (int cycle = 0; cycle < 5; cycle++)
        {
            // Allocate objects
            var root = heap.AllocateObject(ObjectFormat.Pointer, 1, 0);
            for (int i = 0; i < 10; i++)
                heap.AllocateObject(ObjectFormat.ByteIndexed, 0, 100);

            // Keep only root
            heap.SetGCRoots(new[] { root });
            var stats = heap.GarbageCollect();

            // Verify 10 objects collected, 1 retained
            Assert.Equal(10, stats.CollectedObjects);
            Assert.Equal(1, heap.GetActiveObjectCount());

            // Clear root for next cycle
            heap.SetGCRoots(Array.Empty<SmalltalkOOP>());
            heap.GarbageCollect();
        }

        Assert.Equal(0, heap.GetActiveObjectCount());
    }

    [Fact]
    public void ManagedStorage_PointerIndexed_InstanceVarsAndIndexed()
    {
        var heap = new SmalltalkHeapManager();

        // Allocate PointerIndexed with 2 instance vars and 3 indexed slots
        var oop = heap.AllocateObject(ObjectFormat.PointerIndexed, 2, 3);

        // Set instance variables
        heap.SetInstanceVariable(oop, 0, SmalltalkOOP.FromSmallInteger(100));
        heap.SetInstanceVariable(oop, 1, SmalltalkOOP.FromSmallInteger(200));

        // Set indexed slots
        heap.SetIndexedOOP(oop, 0, SmalltalkOOP.FromSmallInteger(10));
        heap.SetIndexedOOP(oop, 1, SmalltalkOOP.FromSmallInteger(20));
        heap.SetIndexedOOP(oop, 2, SmalltalkOOP.FromSmallInteger(30));

        // Verify instance variables
        Assert.Equal(100, heap.GetInstanceVariable(oop, 0).AsSmallInteger());
        Assert.Equal(200, heap.GetInstanceVariable(oop, 1).AsSmallInteger());

        // Verify indexed slots
        Assert.Equal(10, heap.GetIndexedOOP(oop, 0).AsSmallInteger());
        Assert.Equal(20, heap.GetIndexedOOP(oop, 1).AsSmallInteger());
        Assert.Equal(30, heap.GetIndexedOOP(oop, 2).AsSmallInteger());
    }

    [Fact]
    public void FreeList_ReuseAfterGC_MaintainsActiveCount()
    {
        var heap = new SmalltalkHeapManager();

        // Allocate 5 objects
        var oops = new SmalltalkOOP[5];
        for (int i = 0; i < 5; i++)
            oops[i] = heap.AllocateObject(ObjectFormat.Pointer, 1, 0);

        Assert.Equal(5, heap.GetActiveObjectCount());

        // Keep only first 2 objects as roots
        heap.SetGCRoots(new[] { oops[0], oops[1] });
        var stats1 = heap.GarbageCollect();

        // Verify 3 collected, 2 remain
        Assert.Equal(3, stats1.CollectedObjects);
        Assert.Equal(2, heap.GetActiveObjectCount());

        // Allocate 3 more objects - should reuse freed indices
        var newOops = new SmalltalkOOP[3];
        for (int i = 0; i < 3; i++)
            newOops[i] = heap.AllocateObject(ObjectFormat.ByteIndexed, 0, 100);

        // Verify count is now 5 again (2 old + 3 new)
        Assert.Equal(5, heap.GetActiveObjectCount());

        // Verify new objects are valid and accessible
        for (int i = 0; i < 3; i++)
        {
            heap.SetIndexedByte(newOops[i], 0, (byte)(i + 1));
            Assert.Equal((byte)(i + 1), heap.GetIndexedByte(newOops[i], 0));
        }
    }

    [Fact]
    public void FreeList_RepeatedAllocateFreeCycles_Stable()
    {
        var heap = new SmalltalkHeapManager();

        for (int cycle = 0; cycle < 10; cycle++)
        {
            // Allocate 20 objects
            var oops = new SmalltalkOOP[20];
            for (int i = 0; i < 20; i++)
                oops[i] = heap.AllocateObject(ObjectFormat.Pointer, 2, 0);

            Assert.Equal(20, heap.GetActiveObjectCount());

            // Free all objects
            heap.SetGCRoots(Array.Empty<SmalltalkOOP>());
            heap.GarbageCollect();

            Assert.Equal(0, heap.GetActiveObjectCount());
        }

        // After 10 cycles, system should be stable
        Assert.Equal(0, heap.GetActiveObjectCount());
    }

    [Fact]
    public void EnumerateObjectTable_ReturnsAllValidObjects()
    {
        var heap = new SmalltalkHeapManager();

        // Allocate 5 objects
        var oops = new SmalltalkOOP[5];
        for (int i = 0; i < 5; i++)
            oops[i] = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);

        // Enumerate and verify we get all 5
        var enumerated = heap.EnumerateObjectTable().ToArray();
        Assert.Equal(5, enumerated.Length);

        // Verify all allocated OOPs are in the enumeration
        foreach (var oop in oops)
        {
            Assert.Contains(oop, enumerated);
        }
    }

    [Fact]
    public void EnumerateObjectTable_SkipsFreedSlots()
    {
        var heap = new SmalltalkHeapManager();

        // Allocate 10 objects
        var oops = new SmalltalkOOP[10];
        for (int i = 0; i < 10; i++)
            oops[i] = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);

        // Keep only first 3 as roots
        heap.SetGCRoots(new[] { oops[0], oops[1], oops[2] });
        heap.GarbageCollect();

        // Enumerate - should only get the 3 surviving objects
        var enumerated = heap.EnumerateObjectTable().ToArray();
        Assert.Equal(3, enumerated.Length);
        Assert.Contains(oops[0], enumerated);
        Assert.Contains(oops[1], enumerated);
        Assert.Contains(oops[2], enumerated);
    }

    [Fact]
    public void EnumerateObjectTable_CanonicalOrder()
    {
        var heap = new SmalltalkHeapManager();

        // Allocate objects in order
        var oop1 = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var oop2 = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);
        var oop3 = heap.AllocateObject(ObjectFormat.Pointer, 0, 0);

        // Enumerate - should be in allocation order
        var enumerated = heap.EnumerateObjectTable().ToArray();
        Assert.Equal(3, enumerated.Length);
        Assert.Equal(oop1, enumerated[0]);
        Assert.Equal(oop2, enumerated[1]);
        Assert.Equal(oop3, enumerated[2]);
    }
}
