using System;
using Xunit;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Execution;

namespace Tests;

/// <summary>
/// Tests for the inline caching system.
/// </summary>
public sealed class TestInlineCache
{
    private readonly Kernel _kernel;
    
    public TestInlineCache()
    {
        _kernel = new Kernel();
        _kernel.Initialize();
    }
    
    /// <summary>
    /// Tests monomorphic inline cache basic operations.
    /// </summary>
    [Fact]
    public void TestMonomorphicCache()
    {
        var cache = new InlineCache();
        
        // Create test class and method
        var testClass = new SClass(_kernel.ObjectClass, "TestClass");
        var method = new SCompiledMethod(_kernel.CompiledMethodClass)
        {
            Header = SCompiledMethod.MakeHeader(0, 0, 0, false, 0),
            Bytecode = [124], // Return stack top
            Literals = []
        };
        
        // Initially empty
        Assert.False(cache.TryGetMethod(testClass, out var cached));
        Assert.Null(cached);
        Assert.Equal(0, cache.Hits);
        Assert.Equal(1, cache.Misses);
        
        // Cache the method
        cache.CacheMethod(testClass, method);
        
        // Should now hit
        Assert.True(cache.TryGetMethod(testClass, out cached));
        Assert.Same(method, cached);
        Assert.Equal(1, cache.Hits);
        Assert.Equal(1, cache.Misses);
        
        // Multiple hits
        Assert.True(cache.TryGetMethod(testClass, out _));
        Assert.True(cache.TryGetMethod(testClass, out _));
        Assert.Equal(3, cache.Hits);
        Assert.Equal(1, cache.Misses);
        
        // Hit ratio
        Assert.Equal(0.75, cache.HitRatio, 2);
    }
    
    /// <summary>
    /// Tests monomorphic cache invalidation.
    /// </summary>
    [Fact]
    public void TestMonomorphicInvalidation()
    {
        var cache = new InlineCache();
        
        var testClass = new SClass(_kernel.ObjectClass, "TestClass");
        var method = new SCompiledMethod(_kernel.CompiledMethodClass);
        
        cache.CacheMethod(testClass, method);
        Assert.True(cache.TryGetMethod(testClass, out _));
        
        // Invalidate
        cache.Invalidate();
        
        // Should now miss
        Assert.False(cache.TryGetMethod(testClass, out var cached));
        Assert.Null(cached);
    }
    
    /// <summary>
    /// Tests monomorphic cache with different classes.
    /// </summary>
    [Fact]
    public void TestMonomorphicDifferentClasses()
    {
        var cache = new InlineCache();
        
        var class1 = new SClass(_kernel.ObjectClass, "Class1");
        var class2 = new SClass(_kernel.ObjectClass, "Class2");
        var method1 = new SCompiledMethod(_kernel.CompiledMethodClass);
        var method2 = new SCompiledMethod(_kernel.CompiledMethodClass);
        
        // Cache for class1
        cache.CacheMethod(class1, method1);
        Assert.True(cache.TryGetMethod(class1, out var cached));
        Assert.Same(method1, cached);
        
        // Miss for class2
        Assert.False(cache.TryGetMethod(class2, out cached));
        Assert.Null(cached);
        
        // Replace with class2
        cache.CacheMethod(class2, method2);
        Assert.True(cache.TryGetMethod(class2, out cached));
        Assert.Same(method2, cached);
        
        // Now class1 misses
        Assert.False(cache.TryGetMethod(class1, out cached));
        Assert.Null(cached);
    }
    
    /// <summary>
    /// Tests version-based invalidation.
    /// </summary>
    [Fact]
    public void TestVersionInvalidation()
    {
        var cache = new InlineCache();
        
        var testClass = new SClass(_kernel.ObjectClass, "TestClass");
        testClass.MethodDictionary = new VersionedMethodDictionary();
        
        var method = new SCompiledMethod(_kernel.CompiledMethodClass);
        
        // Cache with version 0
        cache.CacheMethod(testClass, method);
        Assert.True(cache.TryGetMethod(testClass, out _));
        
        // Change version
        testClass.MethodDictionary.IncrementVersion();
        
        // Should now miss due to version mismatch
        Assert.False(cache.TryGetMethod(testClass, out var cached));
        Assert.Null(cached);
    }
    
    /// <summary>
    /// Tests polymorphic inline cache basic operations.
    /// </summary>
    [Fact]
    public void TestPolymorphicCache()
    {
        var cache = new PolymorphicInlineCache();
        
        // Create multiple classes and methods
        var class1 = new SClass(_kernel.ObjectClass, "Class1");
        var class2 = new SClass(_kernel.ObjectClass, "Class2");
        var class3 = new SClass(_kernel.ObjectClass, "Class3");
        
        var method1 = new SCompiledMethod(_kernel.CompiledMethodClass);
        var method2 = new SCompiledMethod(_kernel.CompiledMethodClass);
        var method3 = new SCompiledMethod(_kernel.CompiledMethodClass);
        
        // Cache multiple entries
        cache.CacheMethod(class1, method1);
        cache.CacheMethod(class2, method2);
        cache.CacheMethod(class3, method3);
        
        // All should hit
        Assert.True(cache.TryGetMethod(class1, out var cached));
        Assert.Same(method1, cached);
        
        Assert.True(cache.TryGetMethod(class2, out cached));
        Assert.Same(method2, cached);
        
        Assert.True(cache.TryGetMethod(class3, out cached));
        Assert.Same(method3, cached);
        
        Assert.Equal(3, cache.Hits);
        Assert.Equal(0, cache.Misses);
    }
    
    /// <summary>
    /// Tests polymorphic cache overflow (more than 4 entries).
    /// </summary>
    [Fact]
    public void TestPolymorphicOverflow()
    {
        var cache = new PolymorphicInlineCache();
        
        // Create 5 classes (more than max entries)
        var classes = new SClass[5];
        var methods = new SCompiledMethod[5];
        
        for (int i = 0; i < 5; i++)
        {
            classes[i] = new SClass(_kernel.ObjectClass, $"Class{i}");
            methods[i] = new SCompiledMethod(_kernel.CompiledMethodClass);
            cache.CacheMethod(classes[i], methods[i]);
        }
        
        // First entry should be evicted (round-robin)
        Assert.False(cache.TryGetMethod(classes[0], out _));
        
        // Others should still be cached
        for (int i = 1; i < 5; i++)
        {
            Assert.True(cache.TryGetMethod(classes[i], out var cached));
            Assert.Same(methods[i], cached);
        }
    }
    
    /// <summary>
    /// Tests polymorphic cache update of existing entry.
    /// </summary>
    [Fact]
    public void TestPolymorphicUpdate()
    {
        var cache = new PolymorphicInlineCache();
        
        var testClass = new SClass(_kernel.ObjectClass, "TestClass");
        var method1 = new SCompiledMethod(_kernel.CompiledMethodClass);
        var method2 = new SCompiledMethod(_kernel.CompiledMethodClass);
        
        // Cache first method
        cache.CacheMethod(testClass, method1);
        Assert.True(cache.TryGetMethod(testClass, out var cached));
        Assert.Same(method1, cached);
        
        // Update with second method
        cache.CacheMethod(testClass, method2);
        Assert.True(cache.TryGetMethod(testClass, out cached));
        Assert.Same(method2, cached);
    }
    
    /// <summary>
    /// Tests polymorphic cache invalidation.
    /// </summary>
    [Fact]
    public void TestPolymorphicInvalidation()
    {
        var cache = new PolymorphicInlineCache();
        
        var class1 = new SClass(_kernel.ObjectClass, "Class1");
        var class2 = new SClass(_kernel.ObjectClass, "Class2");
        
        var method1 = new SCompiledMethod(_kernel.CompiledMethodClass);
        var method2 = new SCompiledMethod(_kernel.CompiledMethodClass);
        
        cache.CacheMethod(class1, method1);
        cache.CacheMethod(class2, method2);
        
        Assert.True(cache.TryGetMethod(class1, out _));
        Assert.True(cache.TryGetMethod(class2, out _));
        
        // Invalidate all
        cache.Invalidate();
        
        Assert.False(cache.TryGetMethod(class1, out _));
        Assert.False(cache.TryGetMethod(class2, out _));
    }
    
    /// <summary>
    /// Tests special selector cache.
    /// </summary>
    [Fact]
    public void TestSpecialSelectorCache()
    {
        // Clear caches first
        SpecialSelectorCache.ClearCaches();
        
        // Initially empty
        Assert.Null(SpecialSelectorCache.GetSmallIntegerMethod(0));
        Assert.Null(SpecialSelectorCache.GetArrayMethod(0));
        
        // Cache methods
        var plusMethod = new SCompiledMethod(_kernel.CompiledMethodClass);
        var atMethod = new SCompiledMethod(_kernel.CompiledMethodClass);
        
        SpecialSelectorCache.CacheSmallIntegerMethod(0, plusMethod); // + selector
        SpecialSelectorCache.CacheArrayMethod(16, atMethod); // at: selector
        
        // Retrieve cached methods
        Assert.Same(plusMethod, SpecialSelectorCache.GetSmallIntegerMethod(0));
        Assert.Same(atMethod, SpecialSelectorCache.GetArrayMethod(16));
        
        // Out of bounds returns null
        Assert.Null(SpecialSelectorCache.GetSmallIntegerMethod(100));
        Assert.Null(SpecialSelectorCache.GetArrayMethod(100));
        
        // Clear and verify
        SpecialSelectorCache.ClearCaches();
        Assert.Null(SpecialSelectorCache.GetSmallIntegerMethod(0));
        Assert.Null(SpecialSelectorCache.GetArrayMethod(16));
    }
    
    /// <summary>
    /// Tests special selector names.
    /// </summary>
    [Fact]
    public void TestSpecialSelectorNames()
    {
        var selectors = SpecialSelectorCache.SpecialSelectors;
        
        // Verify some key selectors
        Assert.Equal("+", selectors[0]);
        Assert.Equal("-", selectors[1]);
        Assert.Equal("<", selectors[2]);
        Assert.Equal(">", selectors[3]);
        Assert.Equal("<=", selectors[4]);
        Assert.Equal(">=", selectors[5]);
        Assert.Equal("=", selectors[6]);
        Assert.Equal("~=", selectors[7]);
        Assert.Equal("*", selectors[8]);
        Assert.Equal("/", selectors[9]);
        Assert.Equal("\\\\", selectors[10]);
        Assert.Equal("@", selectors[11]);
        Assert.Equal("at:", selectors[16]);
        Assert.Equal("at:put:", selectors[17]);
        Assert.Equal("size", selectors[18]);
        Assert.Equal("==", selectors[22]);
        Assert.Equal("class", selectors[23]);
        Assert.Equal("value", selectors[25]);
        Assert.Equal("value:", selectors[26]);
        
        // Should have 32 special selectors
        Assert.Equal(32, selectors.Length);
    }
    
    /// <summary>
    /// Tests cache statistics reset.
    /// </summary>
    [Fact]
    public void TestStatisticsReset()
    {
        var cache = new InlineCache();
        var polyCache = new PolymorphicInlineCache();
        
        var testClass = new SClass(_kernel.ObjectClass, "TestClass");
        var method = new SCompiledMethod(_kernel.CompiledMethodClass);
        
        // Generate some statistics
        cache.CacheMethod(testClass, method);
        cache.TryGetMethod(testClass, out _); // Hit
        cache.TryGetMethod(new SClass(_kernel.ObjectClass, "Other"), out _); // Miss
        
        polyCache.CacheMethod(testClass, method);
        polyCache.TryGetMethod(testClass, out _); // Hit
        polyCache.TryGetMethod(new SClass(_kernel.ObjectClass, "Other"), out _); // Miss
        
        Assert.True(cache.Hits > 0);
        Assert.True(cache.Misses > 0);
        Assert.True(polyCache.Hits > 0);
        Assert.True(polyCache.Misses > 0);
        
        // Reset statistics
        cache.ResetStatistics();
        polyCache.ResetStatistics();
        
        Assert.Equal(0, cache.Hits);
        Assert.Equal(0, cache.Misses);
        Assert.Equal(0.0, cache.HitRatio);
        
        Assert.Equal(0, polyCache.Hits);
        Assert.Equal(0, polyCache.Misses);
        Assert.Equal(0.0, polyCache.HitRatio);
    }
    
    /// <summary>
    /// Tests concurrent access to cache statistics.
    /// </summary>
    [Fact]
    public void TestConcurrentStatistics()
    {
        var cache = new InlineCache();
        var testClass = new SClass(_kernel.ObjectClass, "TestClass");
        var method = new SCompiledMethod(_kernel.CompiledMethodClass);
        
        cache.CacheMethod(testClass, method);
        
        // Simulate concurrent access
        var tasks = new System.Threading.Tasks.Task[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = System.Threading.Tasks.Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    cache.TryGetMethod(testClass, out _);
                }
            });
        }
        
        System.Threading.Tasks.Task.WaitAll(tasks);
        
        // Should have 1000 hits total
        Assert.Equal(1000, cache.Hits);
        Assert.Equal(0, cache.Misses);
        Assert.Equal(1.0, cache.HitRatio);
    }
}