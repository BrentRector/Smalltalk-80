using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Interpreter.Tests.Core;

/// <summary>
/// Comprehensive test suite for the MethodLookupCache implementation.
/// </summary>
[TestFixture]
public class TestMethodLookupCache
{
    private MethodLookupCache _cache = null!;
    private SClass _objectClass = null!;
    private SClass _stringClass = null!;
    private SClass _arrayClass = null!;
    private SSymbol _testSelector = null!;
    private SSymbol _anotherSelector = null!;
    private SCompiledMethod _testMethod = null!;
    private SCompiledMethod _anotherMethod = null!;
    
    [SetUp]
    public void SetUp()
    {
        // Clear symbol pool for clean test state
        SSymbol.ClearPool();
        
        // Create test cache with small size for easier testing
        _cache = new MethodLookupCache(maxEntries: 10);
        
        // Create test class hierarchy
        _objectClass = new SClass("Object", 0);
        _stringClass = new SClass("String", 1) { Superclass = _objectClass };
        _arrayClass = new SClass("Array", 0) { Superclass = _objectClass };
        
        // Set up metaclasses (simplified)
        var objectMetaclass = new SClass("Object class", 0);
        _objectClass.Metaclass = objectMetaclass;
        
        // Create test selectors
        _testSelector = SSymbol.Intern("test");
        _anotherSelector = SSymbol.Intern("another");
        
        // Create test methods
        var methodClass = new SClass("CompiledMethod", 4);
        _testMethod = new SCompiledMethod(methodClass) { Selector = _testSelector };
        _anotherMethod = new SCompiledMethod(methodClass) { Selector = _anotherSelector };
    }
    
    [TearDown]
    public void TearDown()
    {
        _cache?.Clear();
        SSymbol.ClearPool();
    }
    
    #region Basic Cache Operations
    
    [Test]
    public void TestCacheHit()
    {
        // Arrange
        _objectClass.InstallMethod(_testSelector, _testMethod);
        
        // Act - First lookup (miss)
        var method1 = _cache.Lookup(_objectClass, _testSelector);
        var stats1 = _cache.Statistics;
        
        // Act - Second lookup (hit)
        var method2 = _cache.Lookup(_objectClass, _testSelector);
        var stats2 = _cache.Statistics;
        
        // Assert
        Assert.That(method1, Is.EqualTo(_testMethod));
        Assert.That(method2, Is.EqualTo(_testMethod));
        Assert.That(stats1.Misses, Is.EqualTo(1));
        Assert.That(stats2.Hits, Is.EqualTo(1));
        Assert.That(stats2.Misses, Is.EqualTo(1)); // Total still 1
    }
    
    [Test]
    public void TestCacheMiss()
    {
        // Act
        var method = _cache.Lookup(_objectClass, _testSelector);
        
        // Assert
        Assert.That(method, Is.Null);
        Assert.That(_cache.Statistics.Misses, Is.EqualTo(1));
        Assert.That(_cache.Statistics.Hits, Is.EqualTo(0));
    }
    
    [Test]
    public void TestInheritedMethodLookup()
    {
        // Arrange
        _objectClass.InstallMethod(_testSelector, _testMethod);
        
        // Act - Lookup from subclass
        var method = _cache.Lookup(_stringClass, _testSelector);
        
        // Assert
        Assert.That(method, Is.EqualTo(_testMethod));
        
        // Act - Second lookup should hit cache
        var method2 = _cache.Lookup(_stringClass, _testSelector);
        Assert.That(method2, Is.EqualTo(_testMethod));
        Assert.That(_cache.Statistics.Hits, Is.EqualTo(1));
    }
    
    #endregion
    
    #region Version-based Invalidation
    
    [Test]
    public void TestVersionInvalidation()
    {
        // Arrange
        _objectClass.InstallMethod(_testSelector, _testMethod);
        var method1 = _cache.Lookup(_objectClass, _testSelector);
        
        // Act - Modify the method (changes version)
        var newMethod = new SCompiledMethod(_testMethod.Class!) { Selector = _testSelector };
        _objectClass.InstallMethod(_testSelector, newMethod);
        
        // Act - Lookup should detect version change
        var method2 = _cache.Lookup(_objectClass, _testSelector);
        
        // Assert
        Assert.That(method1, Is.EqualTo(_testMethod));
        Assert.That(method2, Is.EqualTo(newMethod));
        Assert.That(_cache.Statistics.Invalidations, Is.GreaterThan(0));
    }
    
    [Test]
    public void TestMethodRemovalInvalidation()
    {
        // Arrange
        _objectClass.InstallMethod(_testSelector, _testMethod);
        var method1 = _cache.Lookup(_objectClass, _testSelector);
        Assert.That(method1, Is.Not.Null);
        
        // Act - Remove the method
        _objectClass.MethodDict.Remove(_testSelector);
        
        // Act - Lookup should detect removal
        var method2 = _cache.Lookup(_objectClass, _testSelector);
        
        // Assert
        Assert.That(method2, Is.Null);
        Assert.That(_cache.Statistics.Invalidations, Is.GreaterThan(0));
    }
    
    #endregion
    
    #region LRU Eviction
    
    [Test]
    public void TestLRUEviction()
    {
        // Arrange - Create cache with small size
        var smallCache = new MethodLookupCache(maxEntries: 3);
        
        // Add methods to fill cache
        var selectors = new List<SSymbol>();
        for (int i = 0; i < 5; i++)
        {
            var selector = SSymbol.Intern($"method{i}");
            var method = new SCompiledMethod(_objectClass) { Selector = selector };
            _objectClass.InstallMethod(selector, method);
            selectors.Add(selector);
        }
        
        // Act - Fill cache beyond capacity
        foreach (var selector in selectors)
        {
            smallCache.Lookup(_objectClass, selector);
            Thread.Sleep(10); // Ensure different timestamps
        }
        
        // Assert
        Assert.That(smallCache.Count, Is.LessThanOrEqualTo(3));
        Assert.That(smallCache.Statistics.Evictions, Is.GreaterThan(0));
        
        // Verify LRU behavior - recent lookups should be cached
        Assert.That(smallCache.TryGetCached(_objectClass, selectors[3], out _), Is.True);
        Assert.That(smallCache.TryGetCached(_objectClass, selectors[4], out _), Is.True);
    }
    
    [Test]
    public void TestAccessTimeUpdate()
    {
        // Arrange
        var smallCache = new MethodLookupCache(maxEntries: 2);
        
        var selector1 = SSymbol.Intern("first");
        var selector2 = SSymbol.Intern("second");
        var selector3 = SSymbol.Intern("third");
        
        _objectClass.InstallMethod(selector1, _testMethod);
        _objectClass.InstallMethod(selector2, _anotherMethod);
        var method3 = new SCompiledMethod(_objectClass) { Selector = selector3 };
        _objectClass.InstallMethod(selector3, method3);
        
        // Act
        smallCache.Lookup(_objectClass, selector1); // Cache: [1]
        Thread.Sleep(10);
        smallCache.Lookup(_objectClass, selector2); // Cache: [1, 2]
        Thread.Sleep(10);
        smallCache.Lookup(_objectClass, selector1); // Access 1 again - updates timestamp
        Thread.Sleep(10);
        smallCache.Lookup(_objectClass, selector3); // Should evict 2, not 1
        
        // Assert
        Assert.That(smallCache.TryGetCached(_objectClass, selector1, out _), Is.True, "selector1 should be cached");
        Assert.That(smallCache.TryGetCached(_objectClass, selector2, out _), Is.False, "selector2 should be evicted");
        Assert.That(smallCache.TryGetCached(_objectClass, selector3, out _), Is.True, "selector3 should be cached");
    }
    
    #endregion
    
    #region Thread Safety
    
    [Test]
    public void TestConcurrentAccess()
    {
        // Arrange
        const int threadCount = 10;
        const int operationsPerThread = 100;
        var barrier = new Barrier(threadCount);
        var errors = new List<Exception>();
        
        // Install some methods
        for (int i = 0; i < 20; i++)
        {
            var selector = SSymbol.Intern($"concurrent{i}");
            var method = new SCompiledMethod(_objectClass) { Selector = selector };
            _objectClass.InstallMethod(selector, method);
        }
        
        // Act - Concurrent operations
        var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(() =>
        {
            try
            {
                barrier.SignalAndWait(); // Synchronize start
                
                var random = new Random(threadId);
                for (int i = 0; i < operationsPerThread; i++)
                {
                    var selectorIndex = random.Next(20);
                    var selector = SSymbol.Intern($"concurrent{selectorIndex}");
                    
                    // Mix of operations
                    switch (random.Next(4))
                    {
                        case 0:
                        case 1:
                            _cache.Lookup(_objectClass, selector);
                            break;
                        case 2:
                            _cache.TryGetCached(_objectClass, selector, out _);
                            break;
                        case 3:
                            if (random.Next(10) == 0) // Occasional invalidation
                                _cache.InvalidateSelector(selector);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                lock (errors)
                    errors.Add(ex);
            }
        })).ToArray();
        
        Task.WaitAll(tasks);
        
        // Assert
        Assert.That(errors, Is.Empty, $"Concurrent access caused errors: {string.Join(", ", errors)}");
        Assert.That(_cache.Statistics.Hits + _cache.Statistics.Misses, Is.GreaterThan(0));
    }
    
    [Test]
    public void TestConcurrentEviction()
    {
        // Arrange
        var smallCache = new MethodLookupCache(maxEntries: 5);
        const int threadCount = 10;
        var barrier = new Barrier(threadCount);
        
        // Act - Many threads trying to add entries simultaneously
        var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(() =>
        {
            barrier.SignalAndWait();
            
            for (int i = 0; i < 10; i++)
            {
                var selector = SSymbol.Intern($"thread{threadId}_method{i}");
                var method = new SCompiledMethod(_objectClass) { Selector = selector };
                _objectClass.InstallMethod(selector, method);
                smallCache.Lookup(_objectClass, selector);
            }
        })).ToArray();
        
        Task.WaitAll(tasks);
        
        // Assert
        Assert.That(smallCache.Count, Is.LessThanOrEqualTo(5));
        Assert.That(smallCache.Statistics.Evictions, Is.GreaterThan(0));
    }
    
    #endregion
    
    #region Special Selector Optimization
    
    [Test]
    public void TestSpecialSelectorOptimization()
    {
        // Arrange
        var specialSelectors = new[]
        {
            SSymbol.Intern("+"),
            SSymbol.Intern("-"),
            SSymbol.Intern("*"),
            SSymbol.Intern("/"),
            SSymbol.Intern("="),
            SSymbol.Intern("<"),
            SSymbol.Intern(">")
        };
        
        var optimizedCache = new MethodLookupCache(maxEntries: 100, specialSelectors: specialSelectors);
        
        // Install methods for special selectors
        foreach (var selector in specialSelectors)
        {
            var method = new SCompiledMethod(_objectClass) { Selector = selector };
            _objectClass.InstallMethod(selector, method);
        }
        
        // Act - Lookup special selectors multiple times
        foreach (var selector in specialSelectors)
        {
            optimizedCache.Lookup(_objectClass, selector);
            optimizedCache.Lookup(_stringClass, selector); // Inherited
        }
        
        // Assert - All lookups after first should be hits
        var stats = optimizedCache.Statistics;
        Assert.That(stats.HitRatio, Is.GreaterThan(0.4)); // At least some hits
    }
    
    #endregion
    
    #region Cache Management
    
    [Test]
    public void TestInvalidateClass()
    {
        // Arrange
        _objectClass.InstallMethod(_testSelector, _testMethod);
        _stringClass.InstallMethod(_anotherSelector, _anotherMethod);
        
        _cache.Lookup(_objectClass, _testSelector);
        _cache.Lookup(_stringClass, _testSelector); // Inherited
        _cache.Lookup(_stringClass, _anotherSelector);
        
        // Act
        _cache.InvalidateClass(_stringClass);
        
        // Assert
        Assert.That(_cache.TryGetCached(_objectClass, _testSelector, out _), Is.True);
        Assert.That(_cache.TryGetCached(_stringClass, _testSelector, out _), Is.False);
        Assert.That(_cache.TryGetCached(_stringClass, _anotherSelector, out _), Is.False);
    }
    
    [Test]
    public void TestInvalidateSelector()
    {
        // Arrange
        _objectClass.InstallMethod(_testSelector, _testMethod);
        _stringClass.InstallMethod(_testSelector, _anotherMethod); // Override
        
        _cache.Lookup(_objectClass, _testSelector);
        _cache.Lookup(_stringClass, _testSelector);
        _cache.Lookup(_arrayClass, _testSelector); // Inherited from Object
        
        // Act
        _cache.InvalidateSelector(_testSelector);
        
        // Assert
        Assert.That(_cache.TryGetCached(_objectClass, _testSelector, out _), Is.False);
        Assert.That(_cache.TryGetCached(_stringClass, _testSelector, out _), Is.False);
        Assert.That(_cache.TryGetCached(_arrayClass, _testSelector, out _), Is.False);
    }
    
    [Test]
    public void TestClearCache()
    {
        // Arrange
        _objectClass.InstallMethod(_testSelector, _testMethod);
        _cache.Lookup(_objectClass, _testSelector);
        Assert.That(_cache.Count, Is.GreaterThan(0));
        
        // Act
        _cache.Clear();
        
        // Assert
        Assert.That(_cache.Count, Is.EqualTo(0));
        Assert.That(_cache.Statistics.Hits, Is.EqualTo(0));
        Assert.That(_cache.Statistics.Misses, Is.EqualTo(0));
    }
    
    #endregion
    
    #region Batch Operations
    
    [Test]
    public void TestBatchLookup()
    {
        // Arrange
        var selectors = new List<SSymbol>();
        for (int i = 0; i < 5; i++)
        {
            var selector = SSymbol.Intern($"batch{i}");
            var method = new SCompiledMethod(_objectClass) { Selector = selector };
            _objectClass.InstallMethod(selector, method);
            selectors.Add(selector);
        }
        
        // Act
        var results = _cache.BatchLookup(_objectClass, selectors);
        
        // Assert
        Assert.That(results.Count, Is.EqualTo(5));
        foreach (var selector in selectors)
        {
            Assert.That(results.ContainsKey(selector), Is.True);
            Assert.That(results[selector], Is.Not.Null);
        }
    }
    
    [Test]
    public void TestWarmUp()
    {
        // Arrange
        var classes = new[] { _objectClass, _stringClass, _arrayClass };
        var selectors = new[]
        {
            SSymbol.Intern("warmup1"),
            SSymbol.Intern("warmup2"),
            SSymbol.Intern("warmup3")
        };
        
        foreach (var selector in selectors)
        {
            var method = new SCompiledMethod(_objectClass) { Selector = selector };
            _objectClass.InstallMethod(selector, method);
        }
        
        // Act
        _cache.WarmUp(classes, selectors);
        
        // Assert - All combinations should be cached
        foreach (var klass in classes)
        {
            foreach (var selector in selectors)
            {
                Assert.That(_cache.TryGetCached(klass, selector, out var method), Is.True);
                Assert.That(method, Is.Not.Null);
            }
        }
    }
    
    #endregion
    
    #region Performance Metrics
    
    [Test]
    public void TestStatistics()
    {
        // Arrange
        _objectClass.InstallMethod(_testSelector, _testMethod);
        
        // Act
        _cache.Lookup(_objectClass, _testSelector); // Miss
        _cache.Lookup(_objectClass, _testSelector); // Hit
        _cache.Lookup(_objectClass, _anotherSelector); // Miss (not found)
        
        // Assert
        var stats = _cache.Statistics;
        Assert.That(stats.Hits, Is.EqualTo(1));
        Assert.That(stats.Misses, Is.EqualTo(2));
        Assert.That(stats.HitRatio, Is.EqualTo(1.0 / 3.0).Within(0.01));
    }
    
    [Test]
    public void TestDiagnostics()
    {
        // Arrange
        _objectClass.InstallMethod(_testSelector, _testMethod);
        _stringClass.InstallMethod(_anotherSelector, _anotherMethod);
        
        _cache.Lookup(_objectClass, _testSelector);
        _cache.Lookup(_stringClass, _anotherSelector);
        
        // Act
        var diagnostics = _cache.GetDiagnostics();
        
        // Assert
        Assert.That(diagnostics, Does.Contain("MethodLookupCache Diagnostics"));
        Assert.That(diagnostics, Does.Contain("Entries: 2/10"));
        Assert.That(diagnostics, Does.Contain("Statistics:"));
    }
    
    #endregion
    
    #region Edge Cases
    
    [Test]
    public void TestNullArguments()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => _cache.Lookup(null!, _testSelector));
        Assert.Throws<ArgumentNullException>(() => _cache.Lookup(_objectClass, null!));
        Assert.Throws<ArgumentNullException>(() => _cache.TryGetCached(null!, _testSelector, out _));
        Assert.Throws<ArgumentNullException>(() => _cache.TryGetCached(_objectClass, null!, out _));
        Assert.Throws<ArgumentNullException>(() => _cache.InvalidateClass(null!));
        Assert.Throws<ArgumentNullException>(() => _cache.InvalidateSelector(null!));
    }
    
    [Test]
    public void TestInvalidCacheSize()
    {
        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new MethodLookupCache(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MethodLookupCache(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MethodLookupCache(100_000)); // Too large
    }
    
    [Test]
    public void TestEmptyHierarchy()
    {
        // Arrange
        var orphanClass = new SClass("Orphan", 0); // No superclass
        
        // Act
        var method = _cache.Lookup(orphanClass, _testSelector);
        
        // Assert
        Assert.That(method, Is.Null);
        Assert.That(_cache.Statistics.Misses, Is.EqualTo(1));
    }
    
    [Test]
    public void TestDeepHierarchy()
    {
        // Arrange - Create deep class hierarchy
        var rootClass = _objectClass;
        SClass currentClass = rootClass;
        
        for (int i = 0; i < 10; i++)
        {
            var subclass = new SClass($"Level{i}", 0) { Superclass = currentClass };
            currentClass = subclass;
        }
        
        rootClass.InstallMethod(_testSelector, _testMethod);
        
        // Act - Lookup from deepest class
        var method = _cache.Lookup(currentClass, _testSelector);
        
        // Assert
        Assert.That(method, Is.EqualTo(_testMethod));
        
        // Second lookup should hit cache
        _cache.Lookup(currentClass, _testSelector);
        Assert.That(_cache.Statistics.Hits, Is.EqualTo(1));
    }
    
    [Test]
    public void TestMethodOverrideInvalidation()
    {
        // Arrange
        _objectClass.InstallMethod(_testSelector, _testMethod);
        _cache.Lookup(_stringClass, _testSelector); // Cache inherited method
        
        // Act - Override in subclass
        var overrideMethod = new SCompiledMethod(_stringClass) { Selector = _testSelector };
        _stringClass.InstallMethod(_testSelector, overrideMethod);
        
        // Lookup again
        var method = _cache.Lookup(_stringClass, _testSelector);
        
        // Assert
        Assert.That(method, Is.EqualTo(overrideMethod));
        Assert.That(_cache.Statistics.Invalidations, Is.GreaterThan(0));
    }
    
    #endregion
}