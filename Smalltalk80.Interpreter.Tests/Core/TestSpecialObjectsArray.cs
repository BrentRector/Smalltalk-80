using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Smalltalk80.Interpreter.Core;
using Xunit;

namespace Smalltalk80.Tests.Core;

/// <summary>
/// Unit tests for the SpecialObjectsArray implementation.
/// </summary>
public class TestSpecialObjectsArray
{
    /// <summary>
    /// Tests that the array initializes with the correct default size.
    /// </summary>
    [Fact]
    public void TestDefaultInitialization()
    {
        using var array = new SpecialObjectsArray();
        
        // Verify standard size
        Assert.Equal(SNil.Instance, array[0]);
        Assert.Equal(SFalse.Instance, array[1]);
        Assert.Equal(STrue.Instance, array[2]);
        
        // Verify uninitialized slots return nil
        for (int i = 3; i < SpecialObjectsArray.StandardSize; i++)
        {
            var obj = array[i];
            Assert.Equal(SNil.Instance, obj);
        }
    }
    
    /// <summary>
    /// Tests that the array can be created with a custom size.
    /// </summary>
    [Fact]
    public void TestCustomSize()
    {
        const int customSize = 64;
        using var array = new SpecialObjectsArray(customSize);
        
        // Should be able to access all indices up to custom size
        for (int i = 0; i < customSize; i++)
        {
            var obj = array[i];
            Assert.NotNull(obj); // Will be nil, but not null
        }
        
        // Verify we can set and get at extended indices
        var testObj = new SObject();
        array[60] = testObj;
        Assert.Equal(testObj, array[60]);
    }
    
    /// <summary>
    /// Tests that the array rejects sizes smaller than standard.
    /// </summary>
    [Fact]
    public void TestInvalidSize()
    {
        Assert.Throws<ArgumentException>(() => new SpecialObjectsArray(10));
    }
    
    /// <summary>
    /// Tests enum-based indexing.
    /// </summary>
    [Fact]
    public void TestEnumIndexing()
    {
        using var array = new SpecialObjectsArray();
        
        // Test getting default values
        Assert.Equal(SNil.Instance, array[SpecialObjectsIndex.NilObject]);
        Assert.Equal(SFalse.Instance, array[SpecialObjectsIndex.FalseObject]);
        Assert.Equal(STrue.Instance, array[SpecialObjectsIndex.TrueObject]);
        
        // Test setting and getting with enum
        var stringClass = new SClass { Name = "String" };
        array[SpecialObjectsIndex.StringClass] = stringClass;
        Assert.Equal(stringClass, array[SpecialObjectsIndex.StringClass]);
        
        var arrayClass = new SClass { Name = "Array" };
        array[SpecialObjectsIndex.ArrayClass] = arrayClass;
        Assert.Equal(arrayClass, array[SpecialObjectsIndex.ArrayClass]);
    }
    
    /// <summary>
    /// Tests integer-based indexing.
    /// </summary>
    [Fact]
    public void TestIntegerIndexing()
    {
        using var array = new SpecialObjectsArray();
        
        // Test all valid indices
        for (int i = 0; i < SpecialObjectsArray.StandardSize; i++)
        {
            var testObj = new SObject();
            array[i] = testObj;
            Assert.Equal(testObj, array[i]);
        }
    }
    
    /// <summary>
    /// Tests out-of-range access throws appropriate exceptions.
    /// </summary>
    [Fact]
    public void TestOutOfRangeAccess()
    {
        using var array = new SpecialObjectsArray();
        
        // Test negative index
        Assert.Throws<ArgumentOutOfRangeException>(() => array[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => array[-1] = new SObject());
        
        // Test index beyond array size
        Assert.Throws<ArgumentOutOfRangeException>(() => array[100]);
        Assert.Throws<ArgumentOutOfRangeException>(() => array[100] = new SObject());
    }
    
    /// <summary>
    /// Tests helper property accessors.
    /// </summary>
    [Fact]
    public void TestHelperProperties()
    {
        using var array = new SpecialObjectsArray();
        
        // Test singleton accessors
        Assert.Equal(SNil.Instance, array.Nil);
        Assert.Equal(STrue.Instance, array.True);
        Assert.Equal(SFalse.Instance, array.False);
        
        // Set up classes and test class accessors
        var smallIntClass = new SClass { Name = "SmallInteger" };
        var stringClass = new SClass { Name = "String" };
        var arrayClass = new SClass { Name = "Array" };
        var symbolClass = new SClass { Name = "Symbol" };
        var byteArrayClass = new SClass { Name = "ByteArray" };
        
        array[SpecialObjectsIndex.SmallIntegerClass] = smallIntClass;
        array[SpecialObjectsIndex.StringClass] = stringClass;
        array[SpecialObjectsIndex.ArrayClass] = arrayClass;
        array[SpecialObjectsIndex.SymbolClass] = symbolClass;
        array[SpecialObjectsIndex.ByteArrayClass] = byteArrayClass;
        
        Assert.Equal(smallIntClass, array.SmallIntegerClass);
        Assert.Equal(stringClass, array.StringClass);
        Assert.Equal(arrayClass, array.ArrayClass);
        Assert.Equal(symbolClass, array.SymbolClass);
        Assert.Equal(byteArrayClass, array.ByteArrayClass);
        
        // Test symbol accessors
        var dnu = SSymbol.Intern("doesNotUnderstand:");
        var cannotReturn = SSymbol.Intern("cannotReturn:");
        var mustBeBoolean = SSymbol.Intern("mustBeBoolean");
        
        array[SpecialObjectsIndex.DoesNotUnderstandSymbol] = dnu;
        array[SpecialObjectsIndex.CannotReturnSymbol] = cannotReturn;
        array[SpecialObjectsIndex.MustBeBooleanSymbol] = mustBeBoolean;
        
        Assert.Equal(dnu, array.DoesNotUnderstandSymbol);
        Assert.Equal(cannotReturn, array.CannotReturnSymbol);
        Assert.Equal(mustBeBoolean, array.MustBeBooleanSymbol);
    }
    
    /// <summary>
    /// Tests that nil is returned for unset values.
    /// </summary>
    [Fact]
    public void TestNilForUnsetValues()
    {
        using var array = new SpecialObjectsArray();
        
        // All uninitialized slots should return nil (except 0-2)
        for (int i = 3; i < SpecialObjectsArray.StandardSize; i++)
        {
            Assert.Equal(SNil.Instance, array[i]);
        }
        
        // Set a value then clear it (set to null)
        array[10] = new SObject();
        array[10] = null;
        Assert.Equal(SNil.Instance, array[10]);
    }
    
    /// <summary>
    /// Tests thread safety of concurrent reads and writes.
    /// </summary>
    [Fact]
    public async Task TestThreadSafety()
    {
        using var array = new SpecialObjectsArray();
        var testObjects = new SObject[20];
        for (int i = 0; i < testObjects.Length; i++)
        {
            testObjects[i] = new SObject();
        }
        
        const int threadCount = 10;
        const int operationsPerThread = 1000;
        var tasks = new Task[threadCount];
        
        // Create reader and writer tasks
        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                var random = new Random(threadId);
                for (int op = 0; op < operationsPerThread; op++)
                {
                    if (random.Next(2) == 0)
                    {
                        // Write operation
                        int index = random.Next(3, 20); // Don't overwrite core objects
                        array[index] = testObjects[random.Next(testObjects.Length)];
                    }
                    else
                    {
                        // Read operation
                        int index = random.Next(SpecialObjectsArray.StandardSize);
                        var value = array[index];
                        Assert.NotNull(value); // Should never be null (nil is not null)
                    }
                }
            });
        }
        
        await Task.WhenAll(tasks);
        
        // Verify core objects are still intact
        Assert.Equal(SNil.Instance, array[SpecialObjectsIndex.NilObject]);
        Assert.Equal(SFalse.Instance, array[SpecialObjectsIndex.FalseObject]);
        Assert.Equal(STrue.Instance, array[SpecialObjectsIndex.TrueObject]);
    }
    
    /// <summary>
    /// Tests cache functionality for performance.
    /// </summary>
    [Fact]
    public void TestCaching()
    {
        using var array = new SpecialObjectsArray();
        
        // Set frequently accessed objects
        var stringClass = new SClass { Name = "String" };
        array[SpecialObjectsIndex.StringClass] = stringClass;
        
        // Access multiple times - should use cache
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(stringClass, array[SpecialObjectsIndex.StringClass]);
        }
        
        // Clear cache and verify still works
        array.ClearCache();
        Assert.Equal(stringClass, array[SpecialObjectsIndex.StringClass]);
    }
    
    /// <summary>
    /// Tests kernel initialization.
    /// </summary>
    [Fact]
    public void TestKernelInitialization()
    {
        using var array = new SpecialObjectsArray();
        var logger = new LoggerFactory().CreateLogger<Kernel>();
        var kernel = new Kernel(logger)
        {
            SmallIntegerClass = new SClass { Name = "SmallInteger" },
            StringClass = new SClass { Name = "String" },
            ArrayClass = new SClass { Name = "Array" },
            SymbolClass = new SClass { Name = "Symbol" },
            ByteArrayClass = new SClass { Name = "ByteArray" },
            MethodContextClass = new SClass { Name = "MethodContext" },
            BlockContextClass = new SClass { Name = "BlockContext" },
            CompiledMethodClass = new SClass { Name = "CompiledMethod" },
            MethodDictionaryClass = new SClass { Name = "MethodDictionary" },
            UndefinedObjectClass = new SClass { Name = "UndefinedObject" },
            MetaclassClass = new SClass { Name = "Metaclass" }
        };
        
        array.InitializeFromKernel(kernel);
        
        Assert.Equal(kernel.SmallIntegerClass, array.SmallIntegerClass);
        Assert.Equal(kernel.StringClass, array.StringClass);
        Assert.Equal(kernel.ArrayClass, array.ArrayClass);
        Assert.Equal(kernel.SymbolClass, array.SymbolClass);
        Assert.Equal(kernel.ByteArrayClass, array.ByteArrayClass);
        Assert.Equal(kernel.MethodContextClass, array.MethodContextClass);
        Assert.Equal(kernel.BlockContextClass, array.BlockContextClass);
        Assert.Equal(kernel.CompiledMethodClass, array.CompiledMethodClass);
        Assert.Equal(kernel.MethodDictionaryClass, array[SpecialObjectsIndex.MethodDictionaryClass]);
        Assert.Equal(kernel.UndefinedObjectClass, array.UndefinedObjectClass);
        Assert.Equal(kernel.MetaclassClass, array.MetaclassClass);
    }
    
    /// <summary>
    /// Tests snapshot and restore functionality for image persistence.
    /// </summary>
    [Fact]
    public void TestSnapshotAndRestore()
    {
        using var array1 = new SpecialObjectsArray();
        
        // Set up some test data
        var stringClass = new SClass { Name = "String" };
        var arrayClass = new SClass { Name = "Array" };
        var testSymbol = SSymbol.Intern("test");
        
        array1[SpecialObjectsIndex.StringClass] = stringClass;
        array1[SpecialObjectsIndex.ArrayClass] = arrayClass;
        array1[SpecialObjectsIndex.DoesNotUnderstandSymbol] = testSymbol;
        
        // Create snapshot
        var snapshot = array1.CreateSnapshot();
        
        // Create new array and restore
        using var array2 = new SpecialObjectsArray();
        array2.RestoreFromSnapshot(snapshot);
        
        // Verify restoration
        Assert.Equal(stringClass, array2[SpecialObjectsIndex.StringClass]);
        Assert.Equal(arrayClass, array2[SpecialObjectsIndex.ArrayClass]);
        Assert.Equal(testSymbol, array2[SpecialObjectsIndex.DoesNotUnderstandSymbol]);
        
        // Verify core objects are preserved
        Assert.Equal(SNil.Instance, array2[SpecialObjectsIndex.NilObject]);
        Assert.Equal(SFalse.Instance, array2[SpecialObjectsIndex.FalseObject]);
        Assert.Equal(STrue.Instance, array2[SpecialObjectsIndex.TrueObject]);
    }
    
    /// <summary>
    /// Tests all special object indices are correctly defined.
    /// </summary>
    [Fact]
    public void TestAllIndicesAreDefined()
    {
        // Verify all Blue Book indices 0-47 are defined
        var definedIndices = new HashSet<int>();
        foreach (SpecialObjectsIndex index in Enum.GetValues<SpecialObjectsIndex>())
        {
            int value = (int)index;
            Assert.InRange(value, 0, 47);
            Assert.DoesNotContain(value, definedIndices); // No duplicates
            definedIndices.Add(value);
        }
        
        // Verify we have all 48 indices defined
        Assert.Equal(48, definedIndices.Count);
        for (int i = 0; i < 48; i++)
        {
            Assert.Contains(i, definedIndices);
        }
    }
    
    /// <summary>
    /// Tests that the special objects array integrates correctly with SArray.
    /// </summary>
    [Fact]
    public void TestSArrayIntegration()
    {
        using var specialObjects = new SpecialObjectsArray();
        
        // Set up array class
        var arrayClass = new SClass { Name = "Array" };
        specialObjects[SpecialObjectsIndex.ArrayClass] = arrayClass;
        
        // Create special selectors array
        var selectors = new SArray(arrayClass, 10);
        specialObjects[SpecialObjectsIndex.SpecialSelectors] = selectors;
        
        // Verify we can retrieve and use it
        var retrieved = specialObjects.SpecialSelectors;
        Assert.NotNull(retrieved);
        Assert.Equal(selectors, retrieved);
        Assert.Equal(10, retrieved.Size);
    }
    
    /// <summary>
    /// Tests that null values are properly handled as nil.
    /// </summary>
    [Fact]
    public void TestNullHandling()
    {
        using var array = new SpecialObjectsArray();
        
        // Setting null should store null but return nil
        array[10] = null;
        Assert.Equal(SNil.Instance, array[10]);
        
        // Setting nil explicitly
        array[11] = SNil.Instance;
        Assert.Equal(SNil.Instance, array[11]);
        
        // Never-set values should also return nil
        Assert.Equal(SNil.Instance, array[15]);
    }
}