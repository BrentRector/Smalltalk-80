using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Smalltalk80.Interpreter.Execution;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.VM;

namespace Tests;

/// <summary>
/// Tests the MethodContext pooling implementation for performance.
/// </summary>
public static class TestMethodContextPool
{
    /// <summary>
    /// Runs performance tests for the context pool.
    /// </summary>
    public static void RunPerformanceTests()
    {
        Console.WriteLine("=== MethodContext Pool Performance Test ===\n");
        
        // Test 1: Basic pooling functionality
        TestBasicPooling();
        
        // Test 2: Concurrent access
        TestConcurrentAccess();
        
        // Test 3: Memory pressure test
        TestMemoryPressure();
        
        Console.WriteLine("\n=== All Tests Complete ===");
    }
    
    /// <summary>
    /// Tests basic rent and return functionality.
    /// </summary>
    private static void TestBasicPooling()
    {
        Console.WriteLine("Test 1: Basic Pooling");
        var pool = MethodContextPool.Instance;
        
        // Rent and return multiple contexts
        var contexts = new MethodContext[100];
        for (int i = 0; i < 100; i++)
        {
            contexts[i] = pool.Rent(null, OOP.Nil, OOP.Nil, [], 0, 16);
        }
        
        // Return them all
        foreach (var ctx in contexts)
        {
            pool.Return(ctx);
        }
        
        // Get statistics
        var stats = pool.GetStatistics();
        Console.WriteLine($"  Rented: {stats.rented}");
        Console.WriteLine($"  Returned: {stats.returned}");
        Console.WriteLine($"  Created: {stats.created}");
        Console.WriteLine($"  Pool Size: {stats.currentSharedSize}");
        Console.WriteLine($"  Reuse Rate: {(1.0 - (double)stats.created / stats.rented) * 100:F2}%\n");
    }
    
    /// <summary>
    /// Tests concurrent access to the pool.
    /// </summary>
    private static void TestConcurrentAccess()
    {
        Console.WriteLine("Test 2: Concurrent Access");
        var pool = MethodContextPool.Instance;
        pool.Clear(); // Clear pool for fresh test
        
        const int threadCount = 4;
        const int operationsPerThread = 1000;
        
        var tasks = new Task[threadCount];
        for (int t = 0; t < threadCount; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < operationsPerThread; i++)
                {
                    var ctx = pool.Rent(null, OOP.Nil, OOP.Nil, [], 0, 16);
                    // Simulate some work
                    ctx.Stack.Push(OOP.FromSmallInteger(i));
                    ctx.PC = i % 100;
                    pool.Return(ctx);
                }
            });
        }
        
        Task.WaitAll(tasks);
        
        var stats = pool.GetStatistics();
        Console.WriteLine($"  Total Operations: {threadCount * operationsPerThread}");
        Console.WriteLine($"  Contexts Created: {stats.created}");
        Console.WriteLine($"  Reuse Rate: {(1.0 - (double)stats.created / stats.rented) * 100:F2}%\n");
    }
    
    /// <summary>
    /// Tests behavior under memory pressure with realistic VM usage.
    /// </summary>
    private static void TestMemoryPressure()
    {
        Console.WriteLine("Test 3: Memory Pressure with VM");
        
        // Create a minimal VM instance
        var vm = new VirtualMachine(NullLogger<VirtualMachine>.Instance);
        
        // Perform bootstrap to set up basic classes
        Console.WriteLine("  Bootstrapping VM...");
        vm.Bootstrap();
        
        // Get initial pool statistics
        var initialStats = vm.GetContextPoolStatistics();
        Console.WriteLine($"  Initial - Created: {initialStats.created}, Pool Size: {initialStats.currentSharedSize}");
        
        // Simulate method calls (which will use the pool)
        Console.WriteLine("  Simulating 1000 method calls...");
        
        // Note: In a real scenario, we would execute actual Smalltalk methods
        // For this test, we're directly testing the pool statistics
        
        var pool = MethodContextPool.Instance;
        
        // Simulate deep call stacks
        for (int i = 0; i < 100; i++)
        {
            MethodContext? root = null;
            MethodContext? current = null;
            
            // Create a call stack 10 levels deep
            for (int depth = 0; depth < 10; depth++)
            {
                var newContext = pool.Rent(current, OOP.Nil, OOP.Nil, [], 2, 32);
                if (root == null) root = newContext;
                current = newContext;
            }
            
            // Return all contexts in the stack
            current = root;
            while (current != null)
            {
                var next = current.Sender;
                pool.Return(current);
                current = next;
            }
        }
        
        // Get final statistics
        var finalStats = vm.GetContextPoolStatistics();
        Console.WriteLine($"  Final - Created: {finalStats.created}, Pool Size: {finalStats.currentSharedSize}");
        Console.WriteLine($"  Total Rented: {finalStats.rented}");
        Console.WriteLine($"  Total Returned: {finalStats.returned}");
        Console.WriteLine($"  Memory Reuse Rate: {(1.0 - (double)(finalStats.created - initialStats.created) / (finalStats.rented - initialStats.rented)) * 100:F2}%");
        
        // Calculate memory savings
        var contextsReused = finalStats.rented - finalStats.created;
        var estimatedSavings = contextsReused * 200; // Rough estimate: 200 bytes per context
        Console.WriteLine($"  Estimated Memory Saved: {estimatedSavings / 1024.0:F2} KB");
    }
    
    /// <summary>
    /// Main entry point for testing.
    /// </summary>
    public static void Main(string[] args)
    {
        try
        {
            RunPerformanceTests();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}