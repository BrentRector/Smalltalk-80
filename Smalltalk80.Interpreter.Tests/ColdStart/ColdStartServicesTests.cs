// ColdStartServicesTests.cs
// Comprehensive unit tests for Cold Start shared services

using Moq;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Engine.Services;
using System.Text.Json;
using Xunit;

namespace Smalltalk80.Interpreter.Tests.ColdStart;

/// <summary>
/// Unit tests for cold start shared services: SymbolService, DictionaryService, and FieldValueService.
/// </summary>
public class ColdStartServicesTests
{
    #region Test Fixtures

    private static ColdStartBundle CreateMinimalValidBundle()
    {
        return new ColdStartBundle
        {
            Metadata = new BundleMetadata
            {
                Version = "1.0.6",
                CreatedAt = DateTime.UtcNow.ToString("o"),
                SourceFile = "test.st"
            },
            Symbols = ["Object", "test", "foo", "bar", "Association", "Dictionary", "tally"],
            Strings = [],
            Classes =
            [
                CreateClassDescriptor("class:Object", 0, null, false, [], 0),
                CreateClassDescriptor("class:Association", 4, 0, false, [], 2),
                CreateClassDescriptor("class:Dictionary", 5, 0, false, [6], 1),
            ],
            Objects = [],
            Methods = [],
            InitExpressions = [],
            Patches = []
        };
    }

    private static ClassDescriptor CreateClassDescriptor(
        string localId,
        int nameIndex,
        int? superNameIndex,
        bool isMetaclass,
        int[] instVarNames,
        int totalInstVarCount)
    {
        return new ClassDescriptor
        {
            LocalId = localId,
            NameIndex = nameIndex,
            SuperNameIndex = superNameIndex,
            IsMetaclass = isMetaclass,
            InstVarNames = instVarNames,
            TotalInstanceVarCount = totalInstVarCount,
            Format = 0,
            ClassVarNames = [],
            FieldLayout = Enumerable.Range(0, totalInstVarCount)
                .Select(i => new FieldLayoutEntry { NameIndex = instVarNames.Length > i ? instVarNames[i] : 0, SlotIndex = i })
                .ToArray()
        };
    }

    private static void SetupSymbolRegistry(ColdStartBundle bundle)
    {
        SymbolRegistry.Reset();
        for (int i = 0; i < bundle.Symbols.Length; i++)
        {
            var symbolOOP = new SmalltalkOOP(1000 + i);
            SymbolRegistry.Register(i, symbolOOP);
        }
        SymbolRegistry.Freeze();
    }

    #endregion

    #region SymbolService Tests

    [Fact]
    public void SymbolService_GetSymbolOOP_ReturnsCorrectOOP()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var service = new SymbolService(bundle);

        // Act
        var result = service.GetSymbolOOP("Object");

        // Assert
        Assert.Equal(new SmalltalkOOP(1000), result);
    }

    [Fact]
    public void SymbolService_GetSymbolOOP_ThrowsForUnknownSymbol()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var service = new SymbolService(bundle);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => service.GetSymbolOOP("unknown"));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void SymbolService_GetSymbolOOPByIndex_ReturnsCorrectOOP()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var service = new SymbolService(bundle);

        // Act
        var result = service.GetSymbolOOPByIndex(1);

        // Assert
        Assert.Equal(new SmalltalkOOP(1001), result);
    }

    [Fact]
    public void SymbolService_TryGetSymbolOOP_ReturnsTrueForExisting()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var service = new SymbolService(bundle);

        // Act
        var found = service.TryGetSymbolOOP("test", out var oop);

        // Assert
        Assert.True(found);
        Assert.Equal(new SmalltalkOOP(1001), oop);
    }

    [Fact]
    public void SymbolService_TryGetSymbolOOP_ReturnsFalseForMissing()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var service = new SymbolService(bundle);

        // Act
        var found = service.TryGetSymbolOOP("nonexistent", out var oop);

        // Assert
        Assert.False(found);
    }

    [Fact]
    public void SymbolService_SymbolCount_ReturnsCorrectCount()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var service = new SymbolService(bundle);

        // Act & Assert
        Assert.Equal(bundle.Symbols.Length, service.SymbolCount);
    }

    [Fact]
    public void SymbolService_NullBundle_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SymbolService(null!));
    }

    [Fact]
    public void SymbolService_GetSymbolOOP_MultipleSymbols_ReturnsCorrectOOPs()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var service = new SymbolService(bundle);

        // Act & Assert
        Assert.Equal(new SmalltalkOOP(1000), service.GetSymbolOOP("Object"));
        Assert.Equal(new SmalltalkOOP(1001), service.GetSymbolOOP("test"));
        Assert.Equal(new SmalltalkOOP(1002), service.GetSymbolOOP("foo"));
        Assert.Equal(new SmalltalkOOP(1003), service.GetSymbolOOP("bar"));
    }

    #endregion

    #region DictionaryService Tests

    [Fact]
    public void DictionaryService_GetSymbolHash_ReturnsConsistentHash()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var mockHeapManager = new Mock<ISmalltalkHeapManager>();
        var allocator = new Mock<ObjectGraphAllocator>(mockHeapManager.Object, bundle);
        var service = new DictionaryService(allocator.Object, bundle);

        var symbolOOP = new SmalltalkOOP(1000);

        // Act
        var hash1 = service.GetSymbolHash(symbolOOP);
        var hash2 = service.GetSymbolHash(symbolOOP);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void DictionaryService_AllocateAssociation_CreatesAssociation()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var mockHeapManager = new Mock<ISmalltalkHeapManager>();
        var allocator = new Mock<ObjectGraphAllocator>(mockHeapManager.Object, bundle);

        GlobalRegistry.Reset();
        var associationClassOOP = new SmalltalkOOP(500);
        GlobalRegistry.RegisterClass("Association", associationClassOOP);

        var associationOOP = new SmalltalkOOP(600);
        allocator.Setup(a => a.AllocateFixedObject("Association")).Returns(associationOOP);

        var service = new DictionaryService(allocator.Object, bundle);

        var keyOOP = new SmalltalkOOP(100);
        var valueOOP = new SmalltalkOOP(200);

        // Act
        var result = service.AllocateAssociation(keyOOP, valueOOP);

        // Assert
        Assert.Equal(associationOOP, result);
        allocator.Verify(a => a.SetInstanceVariable(associationOOP, 0, keyOOP), Times.Once());
        allocator.Verify(a => a.SetInstanceVariable(associationOOP, 1, valueOOP), Times.Once());
    }

    [Fact]
    public void DictionaryService_InstallInSystemDictionary_InstallsAssociation()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var mockHeapManager = new Mock<ISmalltalkHeapManager>();
        var allocator = new Mock<ObjectGraphAllocator>(mockHeapManager.Object, bundle);

        GlobalRegistry.Reset();
        var systemDictOOP = new SmalltalkOOP(50);
        GlobalRegistry.Register("Smalltalk", systemDictOOP);

        var nilOOP = new SmalltalkOOP(10);
        SpecialObjectsArray.Initialize(nilOOP, new SmalltalkOOP(11), new SmalltalkOOP(12));

        allocator.Setup(a => a.GetIndexedSize(systemDictOOP)).Returns(100);
        allocator.Setup(a => a.GetIndexedOOP(systemDictOOP, It.IsAny<int>())).Returns(nilOOP);
        allocator.Setup(a => a.GetInstanceVariable(It.IsAny<SmalltalkOOP>(), 0)).Returns(new SmalltalkOOP(1000));

        var service = new DictionaryService(allocator.Object, bundle);

        var associationOOP = new SmalltalkOOP(600);

        // Act
        service.InstallInSystemDictionary(associationOOP, "test");

        // Assert
        allocator.Verify(a => a.SetIndexedOOP(systemDictOOP, It.IsAny<int>(), associationOOP), Times.Once());
    }

    [Fact]
    public void DictionaryService_InstallInDictionary_IncrementssTally()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var mockHeapManager = new Mock<ISmalltalkHeapManager>();
        var allocator = new Mock<ObjectGraphAllocator>(mockHeapManager.Object, bundle);

        var nilOOP = new SmalltalkOOP(10);
        SpecialObjectsArray.Initialize(nilOOP, new SmalltalkOOP(11), new SmalltalkOOP(12));

        var dictionaryOOP = new SmalltalkOOP(50);
        allocator.Setup(a => a.GetIndexedSize(dictionaryOOP)).Returns(100);
        allocator.Setup(a => a.GetIndexedOOP(dictionaryOOP, It.IsAny<int>())).Returns(nilOOP);
        allocator.Setup(a => a.GetInstanceVariable(dictionaryOOP, It.IsAny<int>()))
            .Returns(SmalltalkOOP.FromSmallInteger(0));
        allocator.Setup(a => a.GetInstanceVariable(It.IsAny<SmalltalkOOP>(), 0)).Returns(new SmalltalkOOP(1000));

        var service = new DictionaryService(allocator.Object, bundle);

        var associationOOP = new SmalltalkOOP(600);

        // Act
        service.InstallInDictionary(dictionaryOOP, associationOOP, "test");

        // Assert - tally should be incremented
        allocator.Verify(a => a.SetInstanceVariable(dictionaryOOP, It.IsAny<int>(), It.Is<SmalltalkOOP>(o => o.AsSmallInteger() == 1)),
            Times.Once());
    }

    [Fact]
    public void DictionaryService_InstallInDictionary_FindsEmptySlot()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var mockHeapManager = new Mock<ISmalltalkHeapManager>();
        var allocator = new Mock<ObjectGraphAllocator>(mockHeapManager.Object, bundle);

        var nilOOP = new SmalltalkOOP(10);
        SpecialObjectsArray.Initialize(nilOOP, new SmalltalkOOP(11), new SmalltalkOOP(12));

        var dictionaryOOP = new SmalltalkOOP(50);
        allocator.Setup(a => a.GetIndexedSize(dictionaryOOP)).Returns(10);

        // First few slots occupied, then nil
        int callCount = 0;
        allocator.Setup(a => a.GetIndexedOOP(dictionaryOOP, It.IsAny<int>()))
            .Returns(() =>
            {
                callCount++;
                return callCount <= 3 ? new SmalltalkOOP(100) : nilOOP;
            });

        allocator.Setup(a => a.GetInstanceVariable(dictionaryOOP, It.IsAny<int>()))
            .Returns(SmalltalkOOP.FromSmallInteger(0));
        allocator.Setup(a => a.GetInstanceVariable(It.IsAny<SmalltalkOOP>(), 0)).Returns(new SmalltalkOOP(999));

        var service = new DictionaryService(allocator.Object, bundle);
        var associationOOP = new SmalltalkOOP(600);

        // Act
        service.InstallInDictionary(dictionaryOOP, associationOOP, "test");

        // Assert
        allocator.Verify(a => a.SetIndexedOOP(dictionaryOOP, It.IsAny<int>(), associationOOP), Times.Once());
    }

    [Fact]
    public void DictionaryService_NullAllocator_ThrowsArgumentNullException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DictionaryService(null!, bundle));
    }

    [Fact]
    public void DictionaryService_NullBundle_ThrowsArgumentNullException()
    {
        // Arrange
        var mockHeapManager = new Mock<ISmalltalkHeapManager>();
        var bundle = CreateMinimalValidBundle();
        var allocator = new Mock<ObjectGraphAllocator>(mockHeapManager.Object, bundle);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DictionaryService(allocator.Object, null!));
    }

    #endregion

    #region FieldValueService Tests

    [Fact]
    public void FieldValueService_ResolveSimpleValue_Nil()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var symbolService = new SymbolService(bundle);
        var service = new FieldValueService(bundle, symbolService);

        var nilOOP = new SmalltalkOOP(10);
        SpecialObjectsArray.Initialize(nilOOP, new SmalltalkOOP(11), new SmalltalkOOP(12));

        var fieldValue = new FieldValue { Type = "nil" };
        var localIdToObjectId = new Dictionary<string, SmalltalkOOP>();

        // Act
        var result = service.ResolveSimpleValue(fieldValue, localIdToObjectId);

        // Assert
        Assert.Equal(nilOOP, result);
    }

    [Fact]
    public void FieldValueService_ResolveSimpleValue_True()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var symbolService = new SymbolService(bundle);
        var service = new FieldValueService(bundle, symbolService);

        var trueOOP = new SmalltalkOOP(11);
        SpecialObjectsArray.Initialize(new SmalltalkOOP(10), new SmalltalkOOP(12), trueOOP);

        var fieldValue = new FieldValue { Type = "true" };
        var localIdToObjectId = new Dictionary<string, SmalltalkOOP>();

        // Act
        var result = service.ResolveSimpleValue(fieldValue, localIdToObjectId);

        // Assert
        Assert.Equal(trueOOP, result);
    }

    [Fact]
    public void FieldValueService_ResolveSimpleValue_False()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var symbolService = new SymbolService(bundle);
        var service = new FieldValueService(bundle, symbolService);

        var falseOOP = new SmalltalkOOP(12);
        SpecialObjectsArray.Initialize(new SmalltalkOOP(10), falseOOP, new SmalltalkOOP(11));

        var fieldValue = new FieldValue { Type = "false" };
        var localIdToObjectId = new Dictionary<string, SmalltalkOOP>();

        // Act
        var result = service.ResolveSimpleValue(fieldValue, localIdToObjectId);

        // Assert
        Assert.Equal(falseOOP, result);
    }

    [Fact]
    public void FieldValueService_ResolveSimpleValue_SmallInt()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var symbolService = new SymbolService(bundle);
        var service = new FieldValueService(bundle, symbolService);

        var fieldValue = new FieldValue { Type = "smallInt", Value = 42 };
        var localIdToObjectId = new Dictionary<string, SmalltalkOOP>();

        // Act
        var result = service.ResolveSimpleValue(fieldValue, localIdToObjectId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Value.IsSmallInteger);
        Assert.Equal(42, result.Value.AsSmallInteger());
    }

    [Fact]
    public void FieldValueService_ResolveSimpleValue_SymbolRef()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var symbolService = new SymbolService(bundle);
        var service = new FieldValueService(bundle, symbolService);

        var fieldValue = new FieldValue { Type = "symbolRef", Index = 0 };
        var localIdToObjectId = new Dictionary<string, SmalltalkOOP>();

        // Act
        var result = service.ResolveSimpleValue(fieldValue, localIdToObjectId);

        // Assert
        Assert.Equal(new SmalltalkOOP(1000), result);
    }

    [Fact]
    public void FieldValueService_ResolveSimpleValue_ObjectRef()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var symbolService = new SymbolService(bundle);
        var service = new FieldValueService(bundle, symbolService);

        var objOOP = new SmalltalkOOP(500);
        var fieldValue = new FieldValue { Type = "objectRef", LocalId = "class:Object" };
        var localIdToObjectId = new Dictionary<string, SmalltalkOOP>
        {
            ["class:Object"] = objOOP
        };

        // Act
        var result = service.ResolveSimpleValue(fieldValue, localIdToObjectId);

        // Assert
        Assert.Equal(objOOP, result);
    }

    [Fact]
    public void FieldValueService_ResolveSimpleValue_UnknownType_ReturnsNull()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var symbolService = new SymbolService(bundle);
        var service = new FieldValueService(bundle, symbolService);

        var fieldValue = new FieldValue { Type = "float", Value = 3.14 }; // Requires allocation
        var localIdToObjectId = new Dictionary<string, SmalltalkOOP>();

        // Act
        var result = service.ResolveSimpleValue(fieldValue, localIdToObjectId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void FieldValueService_GetIntValue_FromInt()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var symbolService = new SymbolService(bundle);
        var service = new FieldValueService(bundle, symbolService);

        // Act
        var result = service.GetIntValue(42);

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public void FieldValueService_GetIntValue_FromLong()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var symbolService = new SymbolService(bundle);
        var service = new FieldValueService(bundle, symbolService);

        // Act
        var result = service.GetIntValue(42L);

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public void FieldValueService_GetIntValue_FromJsonElement()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var symbolService = new SymbolService(bundle);
        var service = new FieldValueService(bundle, symbolService);

        var json = JsonDocument.Parse("42");
        var element = json.RootElement;

        // Act
        var result = service.GetIntValue(element);

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public void FieldValueService_GetFloatValue_FromDouble()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var symbolService = new SymbolService(bundle);
        var service = new FieldValueService(bundle, symbolService);

        // Act
        var result = service.GetFloatValue(3.14);

        // Assert
        Assert.Equal(3.14, result);
    }

    [Fact]
    public void FieldValueService_GetFloatValue_FromFloat()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var symbolService = new SymbolService(bundle);
        var service = new FieldValueService(bundle, symbolService);

        // Act
        var result = service.GetFloatValue(3.14f);

        // Assert
        Assert.Equal(3.14f, result, 5);
    }

    [Fact]
    public void FieldValueService_ConvertHexStringToBytes_ValidHex()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var symbolService = new SymbolService(bundle);
        var service = new FieldValueService(bundle, symbolService);

        // Act
        var result = service.ConvertHexStringToBytes("48656C6C6F"); // "Hello"

        // Assert
        Assert.Equal([0x48, 0x65, 0x6C, 0x6C, 0x6F], result);
    }

    [Fact]
    public void FieldValueService_ConvertHexStringToBytes_WithSpaces()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var symbolService = new SymbolService(bundle);
        var service = new FieldValueService(bundle, symbolService);

        // Act
        var result = service.ConvertHexStringToBytes("48 65 6C 6C 6F");

        // Assert
        Assert.Equal([0x48, 0x65, 0x6C, 0x6C, 0x6F], result);
    }

    [Fact]
    public void FieldValueService_ConvertHexStringToBytes_OddLength_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var symbolService = new SymbolService(bundle);
        var service = new FieldValueService(bundle, symbolService);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => service.ConvertHexStringToBytes("48656C6C6"));
    }

    [Fact]
    public void FieldValueService_NullBundle_ThrowsArgumentNullException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var symbolService = new SymbolService(bundle);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new FieldValueService(null!, symbolService));
    }

    [Fact]
    public void FieldValueService_NullSymbolService_ThrowsArgumentNullException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new FieldValueService(bundle, null!));
    }

    [Fact]
    public void FieldValueService_GetSymbolOOP_DelegatesToSymbolService()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var symbolService = new SymbolService(bundle);
        var service = new FieldValueService(bundle, symbolService);

        // Act
        var result = service.GetSymbolOOP("test");

        // Assert
        Assert.Equal(new SmalltalkOOP(1001), result);
    }

    [Fact]
    public void FieldValueService_GetSymbolOOPByIndex_DelegatesToSymbolService()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        SetupSymbolRegistry(bundle);
        var symbolService = new SymbolService(bundle);
        var service = new FieldValueService(bundle, symbolService);

        // Act
        var result = service.GetSymbolOOPByIndex(2);

        // Assert
        Assert.Equal(new SmalltalkOOP(1002), result);
    }

    #endregion
}
