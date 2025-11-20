using Xunit;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Tests.VM;

/// <summary>
/// Comprehensive unit tests for the GlobalRegistry component.
/// Tests global variable registration, lookup, and freeze behavior.
/// </summary>
public class GlobalRegistryTests : IDisposable
{
    public GlobalRegistryTests()
    {
        // Clear registry before each test
        GlobalRegistry.Clear();
    }

    public void Dispose()
    {
        // Clear registry after each test to avoid interference
        GlobalRegistry.Clear();
    }

    #region Register Tests

    [Fact]
    public void Register_NilOOP_SetsNilProperty()
    {
        // Arrange
        var nilOOP = new SmalltalkOOP(2);

        // Act
        GlobalRegistry.Register("nil", nilOOP);

        // Assert
        Assert.Equal(nilOOP, GlobalRegistry.NilOOP);
        Assert.Equal(nilOOP, SpecialObjectsArray.Instance.Nil);
    }

    [Fact]
    public void Register_TrueOOP_SetsTrueProperty()
    {
        // Arrange
        var trueOOP = new SmalltalkOOP(4);

        // Act
        GlobalRegistry.Register("true", trueOOP);

        // Assert
        Assert.Equal(trueOOP, GlobalRegistry.TrueOOP);
        Assert.Equal(trueOOP, SpecialObjectsArray.Instance.True);
    }

    [Fact]
    public void Register_FalseOOP_SetsFalseProperty()
    {
        // Arrange
        var falseOOP = new SmalltalkOOP(6);

        // Act
        GlobalRegistry.Register("false", falseOOP);

        // Assert
        Assert.Equal(falseOOP, GlobalRegistry.FalseOOP);
        Assert.Equal(falseOOP, SpecialObjectsArray.Instance.False);
    }

    [Fact]
    public void Register_SystemDictionary_SetsSystemDictionaryProperty()
    {
        // Arrange
        var systemDictOOP = new SmalltalkOOP(100);

        // Act
        GlobalRegistry.Register("SystemDictionary", systemDictOOP);

        // Assert
        Assert.Equal(systemDictOOP, GlobalRegistry.SystemDictionaryOOP);
        Assert.Equal(systemDictOOP, SpecialObjectsArray.Instance.Smalltalk);
    }

    [Fact]
    public void Register_Smalltalk_SetsSystemDictionaryProperty()
    {
        // Arrange
        var smalltalkOOP = new SmalltalkOOP(100);

        // Act
        GlobalRegistry.Register("Smalltalk", smalltalkOOP);

        // Assert
        Assert.Equal(smalltalkOOP, GlobalRegistry.SystemDictionaryOOP);
        Assert.Equal(smalltalkOOP, SpecialObjectsArray.Instance.Smalltalk);
    }

    [Fact]
    public void Register_CustomGlobal_CanBeRetrieved()
    {
        // Arrange
        var customOOP = new SmalltalkOOP(200);

        // Act
        GlobalRegistry.Register("MyGlobal", customOOP);

        // Assert
        Assert.Equal(customOOP, GlobalRegistry.Get("MyGlobal"));
    }

    #endregion

    #region RegisterClass Tests

    [Fact]
    public void RegisterClass_ValidClass_CanBeLookedUp()
    {
        // Arrange
        var classOOP = new SmalltalkOOP(300);

        // Act
        GlobalRegistry.RegisterClass("Object", classOOP);

        // Assert
        Assert.Equal(classOOP, GlobalRegistry.LookupClassOOP("Object"));
    }

    [Fact]
    public void RegisterClass_MultipleClasses_AllCanBeLookedUp()
    {
        // Arrange
        var objectOOP = new SmalltalkOOP(300);
        var numberOOP = new SmalltalkOOP(302);
        var stringOOP = new SmalltalkOOP(304);

        // Act
        GlobalRegistry.RegisterClass("Object", objectOOP);
        GlobalRegistry.RegisterClass("Number", numberOOP);
        GlobalRegistry.RegisterClass("String", stringOOP);

        // Assert
        Assert.Equal(objectOOP, GlobalRegistry.LookupClassOOP("Object"));
        Assert.Equal(numberOOP, GlobalRegistry.LookupClassOOP("Number"));
        Assert.Equal(stringOOP, GlobalRegistry.LookupClassOOP("String"));
    }

    [Fact]
    public void LookupClassOOP_NonExistentClass_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() => GlobalRegistry.LookupClassOOP("NonExistent"));
    }

    #endregion

    #region Get and TryGet Tests

    [Fact]
    public void Get_ExistingGlobal_ReturnsValue()
    {
        // Arrange
        var globalOOP = new SmalltalkOOP(400);
        GlobalRegistry.Register("TestGlobal", globalOOP);

        // Act
        var result = GlobalRegistry.Get("TestGlobal");

        // Assert
        Assert.Equal(globalOOP, result);
    }

    [Fact]
    public void Get_NonExistentGlobal_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() => GlobalRegistry.Get("NonExistent"));
    }

    [Fact]
    public void TryGet_ExistingGlobal_ReturnsTrueAndValue()
    {
        // Arrange
        var globalOOP = new SmalltalkOOP(400);
        GlobalRegistry.Register("TestGlobal", globalOOP);

        // Act
        var found = GlobalRegistry.TryGet("TestGlobal", out var result);

        // Assert
        Assert.True(found);
        Assert.Equal(globalOOP, result);
    }

    [Fact]
    public void TryGet_NonExistentGlobal_ReturnsFalse()
    {
        // Act
        var found = GlobalRegistry.TryGet("NonExistent", out var result);

        // Assert
        Assert.False(found);
        Assert.Equal(default, result);
    }

    #endregion

    #region Set and GetObject Tests

    [Fact]
    public void Set_GenericObject_CanBeRetrieved()
    {
        // Arrange
        var dictionary = new Dictionary<string, int> { { "key", 42 } };

        // Act
        GlobalRegistry.Set("TestDict", dictionary);
        var result = GlobalRegistry.GetObject<Dictionary<string, int>>("TestDict");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(42, result["key"]);
    }

    [Fact]
    public void GetObject_NonExistent_ReturnsNull()
    {
        // Act
        var result = GlobalRegistry.GetObject<Dictionary<string, int>>("NonExistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetObject_WrongType_ReturnsNull()
    {
        // Arrange
        GlobalRegistry.Set("TestString", "hello");

        // Act
        var result = GlobalRegistry.GetObject<Dictionary<string, int>>("TestString");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryGetObject_ExistingObject_ReturnsTrueAndValue()
    {
        // Arrange
        var list = new List<int> { 1, 2, 3 };
        GlobalRegistry.Set("TestList", list);

        // Act
        var found = GlobalRegistry.TryGetObject<List<int>>("TestList", out var result);

        // Assert
        Assert.True(found);
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void TryGetObject_NonExistent_ReturnsFalse()
    {
        // Act
        var found = GlobalRegistry.TryGetObject<List<int>>("NonExistent", out var result);

        // Assert
        Assert.False(found);
        Assert.Null(result);
    }

    #endregion

    #region GetAll Tests

    [Fact]
    public void GetAll_ReturnsAllRegisteredGlobals()
    {
        // Arrange
        var oop1 = new SmalltalkOOP(100);
        var oop2 = new SmalltalkOOP(200);
        GlobalRegistry.Register("Global1", oop1);
        GlobalRegistry.Register("Global2", oop2);

        // Act
        var all = GlobalRegistry.GetAll().ToList();

        // Assert
        Assert.Equal(2, all.Count);
        Assert.Contains(all, kvp => kvp.Key == "Global1" && kvp.Value == oop1);
        Assert.Contains(all, kvp => kvp.Key == "Global2" && kvp.Value == oop2);
    }

    [Fact]
    public void GetAll_EmptyRegistry_ReturnsEmpty()
    {
        // Act
        var all = GlobalRegistry.GetAll().ToList();

        // Assert
        Assert.Empty(all);
    }

    #endregion

    #region Clear Tests

    [Fact]
    public void Clear_RemovesAllGlobals()
    {
        // Arrange
        GlobalRegistry.Register("Global1", new SmalltalkOOP(100));
        GlobalRegistry.RegisterClass("TestClass", new SmalltalkOOP(200));
        GlobalRegistry.Set("TestObject", new object());

        // Act
        GlobalRegistry.Clear();

        // Assert
        Assert.False(GlobalRegistry.TryGet("Global1", out _));
        Assert.Throws<KeyNotFoundException>(() => GlobalRegistry.LookupClassOOP("TestClass"));
        Assert.Null(GlobalRegistry.GetObject<object>("TestObject"));
    }

    [Fact]
    public void Clear_ResetsSpecialOOPs()
    {
        // Arrange
        GlobalRegistry.Register("nil", new SmalltalkOOP(2));
        GlobalRegistry.Register("true", new SmalltalkOOP(4));
        GlobalRegistry.Register("false", new SmalltalkOOP(6));

        // Act
        GlobalRegistry.Clear();

        // Assert
        Assert.Equal(SmalltalkOOP.Null, GlobalRegistry.NilOOP);
        Assert.Equal(SmalltalkOOP.Null, GlobalRegistry.TrueOOP);
        Assert.Equal(SmalltalkOOP.Null, GlobalRegistry.FalseOOP);
        Assert.Equal(SmalltalkOOP.Null, GlobalRegistry.SystemDictionaryOOP);
    }

    #endregion

    #region Freeze Tests

    [Fact]
    public void Freeze_PreventsRegister()
    {
        // Arrange
        GlobalRegistry.Freeze();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => GlobalRegistry.Register("Test", new SmalltalkOOP(100)));
    }

    [Fact]
    public void Freeze_PreventsRegisterClass()
    {
        // Arrange
        GlobalRegistry.Freeze();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => GlobalRegistry.RegisterClass("Test", new SmalltalkOOP(100)));
    }

    [Fact]
    public void Freeze_PreventsSet()
    {
        // Arrange
        GlobalRegistry.Freeze();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => GlobalRegistry.Set("Test", new object()));
    }

    [Fact]
    public void Freeze_AllowsGet()
    {
        // Arrange
        GlobalRegistry.Register("Test", new SmalltalkOOP(100));
        GlobalRegistry.Freeze();

        // Act - should not throw
        var result = GlobalRegistry.Get("Test");

        // Assert
        Assert.Equal(new SmalltalkOOP(100), result);
    }

    [Fact]
    public void Freeze_AllowsTryGet()
    {
        // Arrange
        GlobalRegistry.Register("Test", new SmalltalkOOP(100));
        GlobalRegistry.Freeze();

        // Act - should not throw
        var found = GlobalRegistry.TryGet("Test", out var result);

        // Assert
        Assert.True(found);
        Assert.Equal(new SmalltalkOOP(100), result);
    }

    [Fact]
    public void Freeze_AllowsLookupClassOOP()
    {
        // Arrange
        GlobalRegistry.RegisterClass("Test", new SmalltalkOOP(100));
        GlobalRegistry.Freeze();

        // Act - should not throw
        var result = GlobalRegistry.LookupClassOOP("Test");

        // Assert
        Assert.Equal(new SmalltalkOOP(100), result);
    }

    [Fact]
    public void Freeze_AllowsGetObject()
    {
        // Arrange
        GlobalRegistry.Set("Test", "hello");
        GlobalRegistry.Freeze();

        // Act - should not throw
        var result = GlobalRegistry.GetObject<string>("Test");

        // Assert
        Assert.Equal("hello", result);
    }

    [Fact]
    public void Freeze_AllowsGetAll()
    {
        // Arrange
        GlobalRegistry.Register("Test", new SmalltalkOOP(100));
        GlobalRegistry.Freeze();

        // Act - should not throw
        var all = GlobalRegistry.GetAll().ToList();

        // Assert
        Assert.Single(all);
    }

    #endregion
}
