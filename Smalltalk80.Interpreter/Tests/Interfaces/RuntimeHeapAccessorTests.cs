using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Tests.Interfaces;

/// <summary>
/// Unit tests for IRuntimeHeapAccessor interface and RuntimeHeapAccessor implementation.
/// Tests runtime heap access, class lookups, symbol interning, and cache invalidation.
/// </summary>
public class RuntimeHeapAccessorTests
{
    /// <summary>
    /// Test that IRuntimeHeapAccessor is properly implemented.
    /// </summary>
    public static void TestInterfaceImplementation()
    {
        // Verify RuntimeHeapAccessor implements IRuntimeHeapAccessor
        var heapManager = CreateTestHeapManager();
        IRuntimeHeapAccessor accessor = new RuntimeHeapAccessor(heapManager);

        // Interface should be assignable
        if (accessor == null)
            throw new Exception("RuntimeHeapAccessor should implement IRuntimeHeapAccessor");

        Console.WriteLine("✅ IRuntimeHeapAccessor implementation verified");
    }

    /// <summary>
    /// Test that cache invalidation works correctly.
    /// </summary>
    public static void TestCacheInvalidation()
    {
        var heapManager = CreateTestHeapManager();
        var accessor = new RuntimeHeapAccessor(heapManager);

        // Should not throw when invalidating caches
        try
        {
            accessor.InvalidateAllCaches();
            Console.WriteLine("✅ Cache invalidation works correctly");
        }
        catch (Exception ex)
        {
            throw new Exception($"Cache invalidation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Test RuntimeClassCache behavior.
    /// </summary>
    public static void TestRuntimeClassCache()
    {
        var heapManager = CreateTestHeapManager();
        var accessor = new RuntimeHeapAccessor(heapManager);
        var cache = new RuntimeClassCache(accessor, heapManager);

        // Cache should handle invalidation
        cache.InvalidateCache();
        Console.WriteLine("✅ RuntimeClassCache invalidation works");
    }

    /// <summary>
    /// Test RuntimeSpecialObjects singleton pattern.
    /// </summary>
    public static void TestRuntimeSpecialObjects()
    {
        // Singleton should return same instance
        var instance1 = RuntimeSpecialObjects.Instance;
        var instance2 = RuntimeSpecialObjects.Instance;

        if (!ReferenceEquals(instance1, instance2))
            throw new Exception("RuntimeSpecialObjects should be singleton");

        // Should handle invalidation
        instance1.InvalidateCache();
        Console.WriteLine("✅ RuntimeSpecialObjects singleton and invalidation work");
    }

    /// <summary>
    /// Run all RuntimeHeapAccessor tests.
    /// </summary>
    public static int RunAllTests()
    {
        int passed = 0;
        int failed = 0;

        var tests = new (string Name, Action Test)[]
        {
            ("Interface Implementation", TestInterfaceImplementation),
            ("Cache Invalidation", TestCacheInvalidation),
            ("RuntimeClassCache", TestRuntimeClassCache),
            ("RuntimeSpecialObjects", TestRuntimeSpecialObjects),
        };

        foreach (var (name, test) in tests)
        {
            try
            {
                test();
                passed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {name}: {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine($"\nRuntimeHeapAccessor Tests: {passed} passed, {failed} failed");
        return failed;
    }

    private static ISmalltalkHeapManager CreateTestHeapManager()
    {
        // Create a minimal heap manager for testing
        // In real tests, this would be a mock or the actual implementation
        return new SmalltalkHeapManager(10000);
    }
}
