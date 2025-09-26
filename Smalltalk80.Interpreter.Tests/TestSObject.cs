using Smalltalk80.Interpreter.Core;
using Xunit;

namespace Smalltalk80.Interpreter.Tests;

/// <summary>
/// Unit tests for the SObject base class and special singleton objects.
/// </summary>
public class TestSObject
{
    /// <summary>
    /// Tests that identity hash remains stable across multiple accesses.
    /// </summary>
    [Fact]
    public void IdentityHash_ShouldBeStable()
    {
        // Arrange
        var obj = new SObject();

        // Act
        var hash1 = obj.IdentityHash;
        var hash2 = obj.IdentityHash;
        var hash3 = obj.IdentityHash;

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.Equal(hash2, hash3);
        Assert.NotEqual(0, hash1); // Should not be 0 (reserved for nil)
    }

    /// <summary>
    /// Tests that different objects have different identity hashes.
    /// </summary>
    [Fact]
    public void IdentityHash_ShouldBeUniqueForDifferentObjects()
    {
        // Arrange & Act
        var obj1 = new SObject();
        var obj2 = new SObject();
        var obj3 = new SObject();

        // Assert
        Assert.NotEqual(obj1.IdentityHash, obj2.IdentityHash);
        Assert.NotEqual(obj2.IdentityHash, obj3.IdentityHash);
        Assert.NotEqual(obj1.IdentityHash, obj3.IdentityHash);
    }

    /// <summary>
    /// Tests that identity hashes skip reserved values (0, 1, 2).
    /// </summary>
    [Fact]
    public void IdentityHash_ShouldSkipReservedValues()
    {
        // Arrange
        const int objectCount = 100;
        var objects = new List<SObject>();

        // Act
        for (int i = 0; i < objectCount; i++)
        {
            objects.Add(new SObject());
        }

        // Assert
        foreach (var obj in objects)
        {
            var hash = obj.IdentityHash;
            Assert.NotEqual(0, hash); // Reserved for nil
            Assert.NotEqual(1, hash); // Reserved for true
            Assert.NotEqual(2, hash); // Reserved for false
        }
    }

    /// <summary>
    /// Tests instance variable access with valid indices.
    /// </summary>
    [Fact]
    public void InstVar_AccessWithValidIndex_ShouldWork()
    {
        // Arrange
        var obj = new SObject(null, 3);
        var value1 = "test";
        var value2 = 42;
        var value3 = new SObject();

        // Act
        obj.SetInstVar(0, value1);
        obj.SetInstVar(1, value2);
        obj.SetInstVar(2, value3);

        // Assert
        Assert.Equal(value1, obj.GetInstVar(0));
        Assert.Equal(value2, obj.GetInstVar(1));
        Assert.Equal(value3, obj.GetInstVar(2));
        Assert.Equal(3, obj.InstVarCount);
    }

    /// <summary>
    /// Tests that accessing instance variables with invalid indices throws exceptions.
    /// </summary>
    [Fact]
    public void InstVar_AccessWithInvalidIndex_ShouldThrow()
    {
        // Arrange
        var obj = new SObject(null, 2);

        // Act & Assert - negative index
        Assert.Throws<ArgumentOutOfRangeException>(() => obj.GetInstVar(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => obj.SetInstVar(-1, "value"));

        // Act & Assert - index beyond bounds
        Assert.Throws<ArgumentOutOfRangeException>(() => obj.GetInstVar(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => obj.SetInstVar(2, "value"));
    }

    /// <summary>
    /// Tests that objects without instance variables handle access correctly.
    /// </summary>
    [Fact]
    public void InstVar_NoInstVars_ShouldHandleCorrectly()
    {
        // Arrange
        var obj1 = new SObject();
        var obj2 = new SObject(null, 0);

        // Assert
        Assert.Equal(0, obj1.InstVarCount);
        Assert.Equal(0, obj2.InstVarCount);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => obj1.GetInstVar(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => obj2.GetInstVar(0));
    }

    /// <summary>
    /// Tests that nil singleton has identity hash 0.
    /// </summary>
    [Fact]
    public void Nil_ShouldHaveIdentityHashZero()
    {
        // Arrange & Act
        var nil = SNil.Instance;

        // Assert
        Assert.Equal(0, nil.IdentityHash);
        Assert.Equal("nil", nil.ToString());
        Assert.Same(SNil.Instance, nil); // Verify singleton
    }

    /// <summary>
    /// Tests that true singleton has identity hash 1.
    /// </summary>
    [Fact]
    public void True_ShouldHaveIdentityHashOne()
    {
        // Arrange & Act
        var trueObj = STrue.Instance;

        // Assert
        Assert.Equal(1, trueObj.IdentityHash);
        Assert.Equal("true", trueObj.ToString());
        Assert.Same(STrue.Instance, trueObj); // Verify singleton
    }

    /// <summary>
    /// Tests that false singleton has identity hash 2.
    /// </summary>
    [Fact]
    public void False_ShouldHaveIdentityHashTwo()
    {
        // Arrange & Act
        var falseObj = SFalse.Instance;

        // Assert
        Assert.Equal(2, falseObj.IdentityHash);
        Assert.Equal("false", falseObj.ToString());
        Assert.Same(SFalse.Instance, falseObj); // Verify singleton
    }

    /// <summary>
    /// Tests singleton object identity.
    /// </summary>
    [Fact]
    public void Singletons_ShouldAlwaysReturnSameInstance()
    {
        // Act & Assert
        Assert.Same(SNil.Instance, SNil.Instance);
        Assert.Same(STrue.Instance, STrue.Instance);
        Assert.Same(SFalse.Instance, SFalse.Instance);
        
        // Verify they are different from each other
        Assert.NotSame(SNil.Instance as object, STrue.Instance as object);
        Assert.NotSame(SNil.Instance as object, SFalse.Instance as object);
        Assert.NotSame(STrue.Instance as object, SFalse.Instance as object);
    }

    /// <summary>
    /// Tests the SObject constructor with class parameter.
    /// </summary>
    [Fact]
    public void Constructor_WithClass_ShouldSetKlass()
    {
        // Arrange
        var testClass = new SClass("TestClass");

        // Act
        var obj = new SObject(testClass);

        // Assert
        Assert.Same(testClass, obj.Klass);
        Assert.Equal(0, obj.InstVarCount);
    }

    /// <summary>
    /// Tests the SObject constructor with class and instance variable count.
    /// </summary>
    [Fact]
    public void Constructor_WithClassAndInstVars_ShouldAllocateStorage()
    {
        // Arrange
        var testClass = new SClass("TestClass");

        // Act
        var obj = new SObject(testClass, 5);

        // Assert
        Assert.Same(testClass, obj.Klass);
        Assert.Equal(5, obj.InstVarCount);
        Assert.NotNull(obj.InstVars);
        Assert.Equal(5, obj.InstVars!.Length);
        
        // Verify all slots are initially null
        for (int i = 0; i < 5; i++)
        {
            Assert.Null(obj.GetInstVar(i));
        }
    }

    /// <summary>
    /// Tests the ToString method for debugging output.
    /// </summary>
    [Fact]
    public void ToString_ShouldProvideDebugInfo()
    {
        // Arrange
        var testClass = new SClass("MyTestClass");
        var obj = new SObject(testClass);

        // Act
        var str = obj.ToString();
        var hash = obj.IdentityHash;

        // Assert
        Assert.Contains("MyTestClass", str);
        Assert.Contains(hash.ToString("X8"), str);
    }

    /// <summary>
    /// Tests the ToString method for objects without a class.
    /// </summary>
    [Fact]
    public void ToString_WithoutClass_ShouldShowUnknown()
    {
        // Arrange
        var obj = new SObject();

        // Act
        var str = obj.ToString();

        // Assert
        Assert.Contains("UnknownClass", str);
    }

    /// <summary>
    /// Tests that the IsImmediate property returns false for regular objects.
    /// </summary>
    [Fact]
    public void IsImmediate_ForRegularObjects_ShouldBeFalse()
    {
        // Arrange & Act
        var obj = new SObject();
        var nil = SNil.Instance;
        var trueObj = STrue.Instance;
        var falseObj = SFalse.Instance;

        // Assert
        Assert.False(obj.IsImmediate);
        Assert.False(nil.IsImmediate);
        Assert.False(trueObj.IsImmediate);
        Assert.False(falseObj.IsImmediate);
    }

    /// <summary>
    /// Tests thread-safety of identity hash generation.
    /// </summary>
    [Fact]
    public void IdentityHash_ShouldBeThreadSafe()
    {
        // Arrange
        const int threadCount = 10;
        const int objectsPerThread = 100;
        var allHashes = new System.Collections.Concurrent.ConcurrentBag<int>();
        var tasks = new Task[threadCount];

        // Act
        for (int t = 0; t < threadCount; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < objectsPerThread; i++)
                {
                    var obj = new SObject();
                    allHashes.Add(obj.IdentityHash);
                }
            });
        }

        Task.WaitAll(tasks);

        // Assert
        var uniqueHashes = new HashSet<int>(allHashes);
        Assert.Equal(threadCount * objectsPerThread, uniqueHashes.Count); // All hashes should be unique
    }

    /// <summary>
    /// Tests the basic SClass functionality.
    /// </summary>
    [Fact]
    public void SClass_BasicFunctionality()
    {
        // Arrange & Act
        var sclass = new SClass("TestClass");

        // Assert
        Assert.Equal("TestClass", sclass.Name);
        Assert.Equal("Class(TestClass)", sclass.ToString());
        Assert.True(sclass.IdentityHash > 2); // Should have a regular hash, not reserved
    }
}