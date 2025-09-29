using Xunit;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Tests.Core;

/// <summary>
/// Comprehensive tests for the VersionedMethodDictionary implementation.
/// </summary>
public class TestVersionedMethodDictionary
{
    /// <summary>
    /// Tests basic dictionary creation and initialization.
    /// </summary>
    [Fact]
    public void TestCreation()
    {
        // Arrange & Act
        var dict = new VersionedMethodDictionary();

        // Assert
        Assert.Equal(0, dict.Count);
        Assert.Equal(0, dict.Version);
        Assert.Empty(dict.Selectors);
        Assert.Empty(dict.Methods);
    }

    /// <summary>
    /// Tests adding methods and version incrementation.
    /// </summary>
    [Fact]
    public void TestAtPut()
    {
        // Arrange
        var dict = new VersionedMethodDictionary();
        var selector = SSymbol.Intern("test");
        var method = new SCompiledMethod(null);
        var initialVersion = dict.Version;

        // Act
        dict.AtPut(selector, method);

        // Assert
        Assert.Equal(1, dict.Count);
        Assert.NotEqual(initialVersion, dict.Version);
        Assert.Equal(method, dict.Lookup(selector));
    }

    /// <summary>
    /// Tests method lookup for existing and non-existing selectors.
    /// </summary>
    [Fact]
    public void TestLookup()
    {
        // Arrange
        var dict = new VersionedMethodDictionary();
        var selector1 = SSymbol.Intern("existing");
        var selector2 = SSymbol.Intern("nonExisting");
        var method = new SCompiledMethod(null);
        dict.AtPut(selector1, method);

        // Act
        var found = dict.Lookup(selector1);
        var notFound = dict.Lookup(selector2);

        // Assert
        Assert.Equal(method, found);
        Assert.Null(notFound);
    }

    /// <summary>
    /// Tests updating an existing method increments version.
    /// </summary>
    [Fact]
    public void TestUpdateIncrementsVersion()
    {
        // Arrange
        var dict = new VersionedMethodDictionary();
        var selector = SSymbol.Intern("test");
        var method1 = new SCompiledMethod(null);
        var method2 = new SCompiledMethod(null);
        
        dict.AtPut(selector, method1);
        var versionAfterAdd = dict.Version;

        // Act
        dict.AtPut(selector, method2);
        var versionAfterUpdate = dict.Version;

        // Assert
        Assert.NotEqual(versionAfterAdd, versionAfterUpdate);
        Assert.Equal(method2, dict.Lookup(selector));
        Assert.Equal(1, dict.Count); // Still only one method
    }

    /// <summary>
    /// Tests removing methods.
    /// </summary>
    [Fact]
    public void TestRemove()
    {
        // Arrange
        var dict = new VersionedMethodDictionary();
        var selector = SSymbol.Intern("test");
        var method = new SCompiledMethod(null);
        dict.AtPut(selector, method);
        var versionAfterAdd = dict.Version;

        // Act
        var removed = dict.Remove(selector);
        var versionAfterRemove = dict.Version;
        var removedAgain = dict.Remove(selector);
        var versionAfterSecondRemove = dict.Version;

        // Assert
        Assert.True(removed);
        Assert.False(removedAgain);
        Assert.NotEqual(versionAfterAdd, versionAfterRemove);
        Assert.Equal(versionAfterRemove, versionAfterSecondRemove); // No change on failed remove
        Assert.Null(dict.Lookup(selector));
        Assert.Equal(0, dict.Count);
    }

    /// <summary>
    /// Tests the Contains method.
    /// </summary>
    [Fact]
    public void TestContains()
    {
        // Arrange
        var dict = new VersionedMethodDictionary();
        var selector1 = SSymbol.Intern("existing");
        var selector2 = SSymbol.Intern("nonExisting");
        var method = new SCompiledMethod(null);
        dict.AtPut(selector1, method);

        // Act & Assert
        Assert.True(dict.Contains(selector1));
        Assert.False(dict.Contains(selector2));
    }

    /// <summary>
    /// Tests clearing the dictionary.
    /// </summary>
    [Fact]
    public void TestClear()
    {
        // Arrange
        var dict = new VersionedMethodDictionary();
        var selector1 = SSymbol.Intern("method1");
        var selector2 = SSymbol.Intern("method2");
        dict.AtPut(selector1, new SCompiledMethod(null));
        dict.AtPut(selector2, new SCompiledMethod(null));
        var versionBeforeClear = dict.Version;

        // Act
        dict.Clear();
        var versionAfterClear = dict.Version;

        // Assert
        Assert.Equal(0, dict.Count);
        Assert.NotEqual(versionBeforeClear, versionAfterClear);
        Assert.Null(dict.Lookup(selector1));
        Assert.Null(dict.Lookup(selector2));
    }

    /// <summary>
    /// Tests clearing an empty dictionary doesn't change version.
    /// </summary>
    [Fact]
    public void TestClearEmptyDoesNotChangeVersion()
    {
        // Arrange
        var dict = new VersionedMethodDictionary();
        var initialVersion = dict.Version;

        // Act
        dict.Clear();

        // Assert
        Assert.Equal(initialVersion, dict.Version);
    }

    /// <summary>
    /// Tests TryGetMethod.
    /// </summary>
    [Fact]
    public void TestTryGetMethod()
    {
        // Arrange
        var dict = new VersionedMethodDictionary();
        var selector1 = SSymbol.Intern("existing");
        var selector2 = SSymbol.Intern("nonExisting");
        var method = new SCompiledMethod(null);
        dict.AtPut(selector1, method);

        // Act & Assert
        Assert.True(dict.TryGetMethod(selector1, out var found1));
        Assert.Equal(method, found1);
        
        Assert.False(dict.TryGetMethod(selector2, out var found2));
        Assert.Null(found2);
    }

    /// <summary>
    /// Tests GetAll returns all key-value pairs.
    /// </summary>
    [Fact]
    public void TestGetAll()
    {
        // Arrange
        var dict = new VersionedMethodDictionary();
        var selector1 = SSymbol.Intern("method1");
        var selector2 = SSymbol.Intern("method2");
        var method1 = new SCompiledMethod(null);
        var method2 = new SCompiledMethod(null);
        dict.AtPut(selector1, method1);
        dict.AtPut(selector2, method2);

        // Act
        var all = dict.GetAll().ToList();

        // Assert
        Assert.Equal(2, all.Count);
        Assert.Contains(all, kvp => kvp.Key == selector1 && kvp.Value == method1);
        Assert.Contains(all, kvp => kvp.Key == selector2 && kvp.Value == method2);
    }

    /// <summary>
    /// Tests CopyFrom method.
    /// </summary>
    [Fact]
    public void TestCopyFrom()
    {
        // Arrange
        var source = new VersionedMethodDictionary();
        var selector1 = SSymbol.Intern("method1");
        var selector2 = SSymbol.Intern("method2");
        var method1 = new SCompiledMethod(null);
        var method2 = new SCompiledMethod(null);
        source.AtPut(selector1, method1);
        source.AtPut(selector2, method2);

        var target = new VersionedMethodDictionary();
        var initialVersion = target.Version;

        // Act
        target.CopyFrom(source);

        // Assert
        Assert.Equal(2, target.Count);
        Assert.Equal(method1, target.Lookup(selector1));
        Assert.Equal(method2, target.Lookup(selector2));
        Assert.NotEqual(initialVersion, target.Version);
    }

    /// <summary>
    /// Tests copying from empty dictionary doesn't change version.
    /// </summary>
    [Fact]
    public void TestCopyFromEmptyDoesNotChangeVersion()
    {
        // Arrange
        var source = new VersionedMethodDictionary();
        var target = new VersionedMethodDictionary();
        var initialVersion = target.Version;

        // Act
        target.CopyFrom(source);

        // Assert
        Assert.Equal(initialVersion, target.Version);
        Assert.Equal(0, target.Count);
    }

    /// <summary>
    /// Tests that version overflow is handled correctly.
    /// </summary>
    [Fact]
    public void TestVersionOverflow()
    {
        // Arrange
        var dict = new VersionedMethodDictionary();
        var selector = SSymbol.Intern("test");
        var method = new SCompiledMethod(null);

        // Use reflection to set version close to max
        var versionField = typeof(VersionedMethodDictionary)
            .GetField("_version", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        versionField?.SetValue(dict, int.MaxValue - 1001);

        // Act
        dict.AtPut(selector, method); // Should increment to MaxValue - 1000
        var versionBeforeOverflow = dict.Version;
        
        dict.Remove(selector); // Should trigger overflow handling
        var versionAfterOverflow = dict.Version;

        // Assert
        Assert.True(versionBeforeOverflow > versionAfterOverflow);
        Assert.Equal(1, versionAfterOverflow); // Should reset to 1
    }

    /// <summary>
    /// Tests null parameter validation.
    /// </summary>
    [Fact]
    public void TestNullValidation()
    {
        // Arrange
        var dict = new VersionedMethodDictionary();
        var selector = SSymbol.Intern("test");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => dict.AtPut(null, new SCompiledMethod(null)));
        Assert.Throws<ArgumentNullException>(() => dict.AtPut(selector, null));
        Assert.Throws<ArgumentNullException>(() => dict.Lookup(null));
        Assert.Throws<ArgumentNullException>(() => dict.Remove(null));
        Assert.Throws<ArgumentNullException>(() => dict.Contains(null));
        Assert.Throws<ArgumentNullException>(() => dict.TryGetMethod(null, out _));
        Assert.Throws<ArgumentNullException>(() => dict.CopyFrom(null));
    }

    /// <summary>
    /// Tests the Selectors and Methods properties.
    /// </summary>
    [Fact]
    public void TestSelectorsAndMethods()
    {
        // Arrange
        var dict = new VersionedMethodDictionary();
        var selector1 = SSymbol.Intern("method1");
        var selector2 = SSymbol.Intern("method2");
        var method1 = new SCompiledMethod(null) { Selector = selector1 };
        var method2 = new SCompiledMethod(null) { Selector = selector2 };
        dict.AtPut(selector1, method1);
        dict.AtPut(selector2, method2);

        // Act
        var selectors = dict.Selectors.ToList();
        var methods = dict.Methods.ToList();

        // Assert
        Assert.Equal(2, selectors.Count);
        Assert.Contains(selector1, selectors);
        Assert.Contains(selector2, selectors);
        
        Assert.Equal(2, methods.Count);
        Assert.Contains(method1, methods);
        Assert.Contains(method2, methods);
    }
}