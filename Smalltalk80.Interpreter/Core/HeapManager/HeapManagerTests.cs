using Smalltalk80.Interpreter.Core.Objects;

namespace Smalltalk80.Interpreter.Core.HeapManager;

/// <summary>
/// Simple test program to verify the new heap manager implementation.
/// This will be replaced with proper unit tests later.
/// </summary>
public static class HeapManagerTests
{
    public static void RunBasicTests()
    {
        Console.WriteLine("=== Heap Manager Basic Tests ===");
        
        var heapManager = new SmalltalkHeapManager();
        
        // Test 1: SmallInteger OOPs
        TestSmallIntegers();
        
        // Test 2: Basic object allocation
        TestBasicObjectAllocation(heapManager);
        
        // Test 3: Format-specific access
        TestFormatSpecificAccess(heapManager);
        
        // Test 4: Garbage collection
        TestGarbageCollection(heapManager);
        
        // Test 5: Heap statistics
        TestHeapStatistics(heapManager);
        
        Console.WriteLine("=== All Tests Completed ===");
    }

    private static void TestSmallIntegers()
    {
        Console.WriteLine("\n--- Testing SmallInteger OOPs ---");
        
        var smallInt42 = SmalltalkOOP.FromSmallInteger(42);
        var smallIntNeg5 = SmalltalkOOP.FromSmallInteger(-5);
        var smallInt0 = SmalltalkOOP.FromSmallInteger(0);
        
        Console.WriteLine($"SmallInteger 42: {smallInt42} (IsSmallInteger: {smallInt42.IsSmallInteger}, Value: {smallInt42.AsSmallInteger()})");
        Console.WriteLine($"SmallInteger -5: {smallIntNeg5} (IsSmallInteger: {smallIntNeg5.IsSmallInteger}, Value: {smallIntNeg5.AsSmallInteger()})");
        Console.WriteLine($"SmallInteger 0: {smallInt0} (IsSmallInteger: {smallInt0.IsSmallInteger}, Value: {smallInt0.AsSmallInteger()})");
        
        var objectOOP = SmalltalkOOP.FromObjectTableIndex(123);
        Console.WriteLine($"Object pointer: {objectOOP} (IsSmallInteger: {objectOOP.IsSmallInteger}, Index: {objectOOP.ToObjectTableIndex()})");
    }

    private static void TestBasicObjectAllocation(ISmalltalkHeapManager heapManager)
    {
        Console.WriteLine("\n--- Testing Basic Object Allocation ---");
        
        var classOOP = SmalltalkOOP.FromObjectTableIndex(1000); // Mock class
        
        // Allocate a simple object with 2 instance variables
        var pointOOP = heapManager.AllocateObject(classOOP, ObjectFormat.Pointer, 2, 0);
        Console.WriteLine($"Allocated Point object: {pointOOP}");
        Console.WriteLine($"  Class OOP: {heapManager.GetClassOOP(pointOOP)}");
        Console.WriteLine($"  Format: {heapManager.GetObjectFormat(pointOOP)}");
        Console.WriteLine($"  Size: {heapManager.GetObjectSizeInWords(pointOOP)} words");
        Console.WriteLine($"  Instance variables: {heapManager.GetInstanceVariableCount(pointOOP)}");
        
        // Set instance variables
        var xCoord = SmalltalkOOP.FromSmallInteger(10);
        var yCoord = SmalltalkOOP.FromSmallInteger(20);
        heapManager.SetInstanceVariable(pointOOP, 0, xCoord);
        heapManager.SetInstanceVariable(pointOOP, 1, yCoord);
        
        // Read them back
        var readX = heapManager.GetInstanceVariable(pointOOP, 0);
        var readY = heapManager.GetInstanceVariable(pointOOP, 1);
        Console.WriteLine($"  Set and read X: {readX} (value: {readX.AsSmallInteger()})");
        Console.WriteLine($"  Set and read Y: {readY} (value: {readY.AsSmallInteger()})");
    }

    private static void TestFormatSpecificAccess(ISmalltalkHeapManager heapManager)
    {
        Console.WriteLine("\n--- Testing Format-Specific Access ---");
        
        var classOOP = SmalltalkOOP.FromObjectTableIndex(2000); // Mock class
        
        // Test Array (PointerIndexed)
        var arrayOOP = heapManager.AllocateObject(classOOP, ObjectFormat.PointerIndexed, 0, 3);
        Console.WriteLine($"Allocated Array: {arrayOOP} with 3 indexed slots");
        
        var elem0 = SmalltalkOOP.FromSmallInteger(100);
        var elem1 = SmalltalkOOP.FromSmallInteger(200);
        var elem2 = SmalltalkOOP.FromSmallInteger(300);
        
        heapManager.SetIndexedOOP(arrayOOP, 0, elem0);
        heapManager.SetIndexedOOP(arrayOOP, 1, elem1);
        heapManager.SetIndexedOOP(arrayOOP, 2, elem2);
        
        Console.WriteLine($"  Array[0]: {heapManager.GetIndexedOOP(arrayOOP, 0)} (value: {heapManager.GetIndexedOOP(arrayOOP, 0).AsSmallInteger()})");
        Console.WriteLine($"  Array[1]: {heapManager.GetIndexedOOP(arrayOOP, 1)} (value: {heapManager.GetIndexedOOP(arrayOOP, 1).AsSmallInteger()})");
        Console.WriteLine($"  Array[2]: {heapManager.GetIndexedOOP(arrayOOP, 2)} (value: {heapManager.GetIndexedOOP(arrayOOP, 2).AsSmallInteger()})");
        
        // Test String (ByteIndexed)
        var stringOOP = heapManager.AllocateObject(classOOP, ObjectFormat.ByteIndexed, 0, 5);
        Console.WriteLine($"Allocated String: {stringOOP} with 5 byte slots");
        
        // Write "Hello"
        heapManager.SetIndexedByte(stringOOP, 0, (byte)'H');
        heapManager.SetIndexedByte(stringOOP, 1, (byte)'e');
        heapManager.SetIndexedByte(stringOOP, 2, (byte)'l');
        heapManager.SetIndexedByte(stringOOP, 3, (byte)'l');
        heapManager.SetIndexedByte(stringOOP, 4, (byte)'o');
        
        // Read it back
        var chars = new char[5];
        for (int i = 0; i < 5; i++)
        {
            chars[i] = (char)heapManager.GetIndexedByte(stringOOP, i);
        }
        Console.WriteLine($"  String content: '{new string(chars)}'");
        
        // Test WordArray (WordIndexed)
        var wordArrayOOP = heapManager.AllocateObject(classOOP, ObjectFormat.WordIndexed, 0, 3);
        Console.WriteLine($"Allocated WordArray: {wordArrayOOP} with 3 word slots");
        
        heapManager.SetIndexedWord(wordArrayOOP, 0, 0x1234);
        heapManager.SetIndexedWord(wordArrayOOP, 1, 0x5678);
        heapManager.SetIndexedWord(wordArrayOOP, 2, 0x9ABC);
        
        Console.WriteLine($"  WordArray[0]: 0x{heapManager.GetIndexedWord(wordArrayOOP, 0):X4}");
        Console.WriteLine($"  WordArray[1]: 0x{heapManager.GetIndexedWord(wordArrayOOP, 1):X4}");
        Console.WriteLine($"  WordArray[2]: 0x{heapManager.GetIndexedWord(wordArrayOOP, 2):X4}");
    }

    private static void TestGarbageCollection(ISmalltalkHeapManager heapManager)
    {
        Console.WriteLine("\n--- Testing Garbage Collection ---");
        
        var classOOP = SmalltalkOOP.FromObjectTableIndex(3000); // Mock class
        
        // Create some objects
        var obj1 = heapManager.AllocateObject(classOOP, ObjectFormat.Pointer, 1, 0);
        var obj2 = heapManager.AllocateObject(classOOP, ObjectFormat.Pointer, 1, 0);
        var obj3 = heapManager.AllocateObject(classOOP, ObjectFormat.PointerIndexed, 0, 2);
        
        // Make obj1 reference obj2
        heapManager.SetInstanceVariable(obj1, 0, obj2);
        
        // Make obj3 reference obj1 in its indexed portion
        heapManager.SetIndexedOOP(obj3, 0, obj1);
        
        Console.WriteLine($"Created objects: {obj1}, {obj2}, {obj3}");
        Console.WriteLine($"Reference chain: {obj3} -> {obj1} -> {obj2}");
        
        // Perform GC with obj3 as the only root
        var roots = new[] { obj3 };
        heapManager.SetGCRoots(roots);
        var gcResult = heapManager.GarbageCollect();
        
        Console.WriteLine($"GC Results:");
        Console.WriteLine($"  {gcResult.GetSummary()}");
        Console.WriteLine($"  {gcResult.GetFormatBreakdown()}");
        Console.WriteLine($"  Mark time: {gcResult.MarkTimeMs:F2}ms, Sweep time: {gcResult.SweepTimeMs:F2}ms");
        
        // All objects should be reachable (obj3 is root, references obj1, which references obj2)
        Console.WriteLine($"  Objects should all be reachable since they form a connected graph from root");
    }

    private static void TestHeapStatistics(ISmalltalkHeapManager heapManager)
    {
        Console.WriteLine("\n--- Testing Heap Statistics ---");
        
        // TODO: Add GetHeapStatistics method to ISmalltalkHeapManager interface if needed
        // For now, use GC statistics to show heap info
        var gcStats = heapManager.GarbageCollect();
        
        Console.WriteLine($"Heap Statistics (via GC):");
        Console.WriteLine($"  {gcStats.GetSummary()}");
        Console.WriteLine($"  {gcStats.GetFormatBreakdown()}");
        
        Console.WriteLine($"ObjectTable Details:");
        Console.WriteLine($"  Total entries: {heapManager.GetObjectTableSize()}");
        Console.WriteLine($"  Active objects: {heapManager.GetActiveObjectCount()}");
    }

    private static void TestBoundsChecking(ISmalltalkHeapManager heapManager)
    {
        Console.WriteLine("\n--- Testing Bounds Checking ---");
        
        var classOOP = SmalltalkOOP.FromObjectTableIndex(4000);
        var arrayOOP = heapManager.AllocateObject(classOOP, ObjectFormat.PointerIndexed, 0, 2);
        
        try
        {
            // This should throw - index out of bounds
            heapManager.GetIndexedOOP(arrayOOP, 5);
            Console.WriteLine("ERROR: Should have thrown bounds exception!");
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Console.WriteLine($"  ✓ Correctly caught bounds exception: {ex.Message}");
        }
        
        try
        {
            // This should throw - wrong format
            heapManager.GetIndexedByte(arrayOOP, 0);
            Console.WriteLine("ERROR: Should have thrown format exception!");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"  ✓ Correctly caught format exception: {ex.Message}");
        }
        
        var smallInt = SmalltalkOOP.FromSmallInteger(42);
        try
        {
            // This should throw - SmallInteger access
            heapManager.GetInstanceVariable(smallInt, 0);
            Console.WriteLine("ERROR: Should have thrown SmallInteger exception!");
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"  ✓ Correctly caught SmallInteger exception: {ex.Message}");
        }
    }
}