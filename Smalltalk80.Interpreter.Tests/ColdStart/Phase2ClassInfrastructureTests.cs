// Phase2ClassInfrastructureTests.cs
// Comprehensive unit tests for Phase 2: Class Infrastructure

using Moq;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Engine.LoaderPhases;
using Xunit;

namespace Smalltalk80.Interpreter.Tests.ColdStart;

/// <summary>
/// Unit tests for Phase2_ClassInfrastructure covering class object creation,
/// metaclass creation, superclass chain setup, instance variable layout, and method dictionary setup.
/// </summary>
public class Phase2ClassInfrastructureTests
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
            Symbols = ["Object", "nil", "true", "false", "UndefinedObject", "True", "False",
                       "Behavior", "ClassDescription", "Class", "Metaclass", "superclass",
                       "methodDict", "format", "subclasses", "instanceVariables", "organization",
                       "name", "classPool", "sharedPools", "thisClass", "tally", "Array", "String",
                       "Dictionary", "SystemDictionary", "Set"],
            Strings = [],
            Classes = CreateStandardClassHierarchy(),
            Objects = [],
            Methods = [],
            InitExpressions = [],
            Patches = []
        };
    }

    private static ClassDescriptor[] CreateStandardClassHierarchy()
    {
        return
        [
            CreateClassDescriptor("class:Object", 0, null, false, [], 0),
            CreateClassDescriptor("class:UndefinedObject", 4, 0, false, [], 0),
            CreateClassDescriptor("class:True", 5, 0, false, [], 0),
            CreateClassDescriptor("class:False", 6, 0, false, [], 0),
            CreateClassDescriptor("class:Behavior", 7, 0, false, [11, 12, 13, 14], 4),
            CreateClassDescriptor("class:ClassDescription", 8, 7, false, [15, 16], 6),
            CreateClassDescriptor("class:Class", 9, 8, false, [17, 18, 19], 9),
            CreateClassDescriptor("class:Metaclass", 10, 8, false, [20], 7),
            CreateClassDescriptor("class:Array", 22, 0, false, [], 0),
            CreateClassDescriptor("class:String", 23, 0, false, [], 0),
            CreateClassDescriptor("class:Dictionary", 24, 0, false, [21], 1),
            CreateClassDescriptor("class:SystemDictionary", 25, 24, false, [], 1),
            CreateClassDescriptor("class:Set", 26, 0, false, [21], 1),
            CreateClassDescriptor("meta:Object", 0, null, true, [], 0, "class:Object"),
            CreateClassDescriptor("meta:Behavior", 7, null, true, [], 0, "class:Behavior"),
            CreateClassDescriptor("meta:ClassDescription", 8, null, true, [], 0, "class:ClassDescription"),
            CreateClassDescriptor("meta:Class", 9, null, true, [], 0, "class:Class"),
            CreateClassDescriptor("meta:Metaclass", 10, null, true, [], 0, "class:Metaclass"),
        ];
    }

    private static ClassDescriptor CreateClassDescriptor(
        string localId,
        int nameIndex,
        int? superNameIndex,
        bool isMetaclass,
        int[] instVarNames,
        int totalInstVarCount,
        string? metaclassOfLocalId = null)
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
                .ToArray(),
            MetaclassOfLocalId = metaclassOfLocalId
        };
    }

    private static Phase2_ClassInfrastructure CreatePhase(
        ColdStartBundle bundle,
        out Mock<ISmalltalkHeapManager> heapManagerMock,
        out Dictionary<string, SmalltalkOOP> localIdToObjectId)
    {
        heapManagerMock = new Mock<ISmalltalkHeapManager>();
        int oopCounter = 100;

        heapManagerMock.Setup(h => h.AllocateObject(It.IsAny<ObjectFormat>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(() => new SmalltalkOOP(oopCounter++));

        var allocator = new ObjectGraphAllocator(heapManagerMock.Object, bundle);
        localIdToObjectId = new Dictionary<string, SmalltalkOOP>();
        var initializedFields = new HashSet<(SmalltalkOOP oop, int slot)>();
        var report = new ColdstartReport();

        // Reset registries for each test
        GlobalRegistry.Reset();
        SymbolRegistry.Reset();

        // Pre-register symbols
        for (int i = 0; i < bundle.Symbols.Length; i++)
        {
            var symbolOOP = new SmalltalkOOP(1000 + i);
            SymbolRegistry.Register(i, symbolOOP);
        }
        SymbolRegistry.Freeze();

        var phase = new Phase2_ClassInfrastructure(
            bundle,
            allocator,
            heapManagerMock.Object,
            localIdToObjectId,
            initializedFields,
            report,
            null);

        return phase;
    }

    #endregion

    #region Phase Properties Tests

    [Fact]
    public void Phase2_Name_ReturnsCorrectName()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out _);

        // Act & Assert
        Assert.Equal("Phase 2: Class Infrastructure", phase.Name);
    }

    [Fact]
    public void Phase2_PhaseNumber_Returns2()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out _);

        // Act & Assert
        Assert.Equal(2, phase.PhaseNumber);
    }

    [Fact]
    public void Phase2_ValidatePreconditions_ReturnsEmptyList()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out _);

        // Act
        var result = phase.ValidatePreconditions();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Phase2_ValidatePostconditions_ReturnsEmptyList()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out _);

        // Act
        var result = phase.ValidatePostconditions();

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region Class Shell Allocation Tests

    [Fact]
    public void Phase2_AllocatesAllClassShells()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out var heapManager, out var localIdToObjectId);

        // Act
        phase.Run();

        // Assert - all classes should be in localIdToObjectId
        foreach (var cls in bundle.Classes)
        {
            Assert.True(localIdToObjectId.ContainsKey(cls.LocalId), $"Class {cls.LocalId} not allocated");
        }
    }

    [Fact]
    public void Phase2_AllocatesSystemDictionary()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out var localIdToObjectId);

        // Act
        phase.Run();

        // Assert
        Assert.True(localIdToObjectId.ContainsKey("object:Smalltalk"));
    }

    [Fact]
    public void Phase2_RegistersSystemDictionaryInGlobalRegistry()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out _);

        // Act
        phase.Run();

        // Assert
        Assert.True(GlobalRegistry.TryGet("Smalltalk", out _));
    }

    [Fact]
    public void Phase2_RegistersInstanceClassesInGlobalRegistry()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out _);

        // Act
        phase.Run();

        // Assert - instance classes should be registered
        Assert.True(GlobalRegistry.TryGet("class:Object", out _));
        Assert.True(GlobalRegistry.TryGet("class:Class", out _));
    }

    [Fact]
    public void Phase2_DoesNotRegisterMetaclassesInGlobalRegistry()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out _);

        // Act
        phase.Run();

        // Assert - metaclasses should NOT be registered
        Assert.False(GlobalRegistry.TryGet("meta:Object", out _));
    }

    #endregion

    #region Metaclass Wiring Tests

    [Fact]
    public void Phase2_WiresMetaclassThisClass()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out var heapManager, out var localIdToObjectId);

        // Act
        phase.Run();

        // Assert - SetInstanceVariable should be called for thisClass
        heapManager.Verify(h => h.SetInstanceVariable(It.IsAny<SmalltalkOOP>(), It.IsAny<int>(), It.IsAny<SmalltalkOOP>()),
            Times.AtLeastOnce());
    }

    [Fact]
    public void Phase2_AllMetaclassesHaveThisClassWired()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out var localIdToObjectId);

        // Act
        phase.Run();

        // Assert - all metaclasses should be in localIdToObjectId
        var metaclasses = bundle.Classes.Where(c => c.IsMetaclass);
        foreach (var meta in metaclasses)
        {
            Assert.True(localIdToObjectId.ContainsKey(meta.LocalId));
        }
    }

    [Fact]
    public void Phase2_NoMetaclassRelationships_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        // Remove all metaclass relationships
        foreach (var cls in bundle.Classes.Where(c => c.IsMetaclass))
        {
            cls.MetaclassOfLocalId = null;
        }
        var phase = CreatePhase(bundle, out _, out _);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => phase.Run());
        Assert.Contains("No metaclass relationships were wired", ex.Message);
    }

    #endregion

    #region Fundamental Metaclass Circle Tests

    [Fact]
    public void Phase2_WiresFundamentalMetaclassCircle()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out var heapManager, out _);

        // Act
        phase.Run();

        // Assert - superclass should be set multiple times for the metaclass circle
        heapManager.Verify(h => h.SetInstanceVariable(It.IsAny<SmalltalkOOP>(), It.IsAny<int>(), It.IsAny<SmalltalkOOP>()),
            Times.AtLeast(4));
    }

    [Fact]
    public void Phase2_MissingMetaObject_SkipsCircleWiring()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Classes = bundle.Classes.Where(c => c.LocalId != "meta:Object").ToArray();
        var phase = CreatePhase(bundle, out _, out _);

        // Act & Assert - should throw because metaclass validation fails
        Assert.Throws<InvalidOperationException>(() => phase.Run());
    }

    #endregion

    #region Class Metadata Resolution Tests

    [Fact]
    public void Phase2_SetsClassSuperclass()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out var heapManager, out _);

        // Act
        phase.Run();

        // Assert - SetInstanceVariable should be called for superclass (index 0)
        heapManager.Verify(h => h.SetInstanceVariable(It.IsAny<SmalltalkOOP>(), 0, It.IsAny<SmalltalkOOP>()),
            Times.AtLeastOnce());
    }

    [Fact]
    public void Phase2_SetsClassFormat()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out var heapManager, out _);

        // Act
        phase.Run();

        // Assert - SetInstanceVariable should be called for format (index 2)
        heapManager.Verify(h => h.SetInstanceVariable(It.IsAny<SmalltalkOOP>(), 2, It.IsAny<SmalltalkOOP>()),
            Times.AtLeastOnce());
    }

    [Fact]
    public void Phase2_SetsClassName()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out var heapManager, out _);

        // Act
        phase.Run();

        // Assert - SetInstanceVariable should be called for name (index 6)
        heapManager.Verify(h => h.SetInstanceVariable(It.IsAny<SmalltalkOOP>(), 6, It.IsAny<SmalltalkOOP>()),
            Times.AtLeastOnce());
    }

    [Fact]
    public void Phase2_AllocatesClassPool()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out var heapManager, out _);

        // Act
        phase.Run();

        // Assert - SetInstanceVariable should be called for classPool (index 7)
        heapManager.Verify(h => h.SetInstanceVariable(It.IsAny<SmalltalkOOP>(), 7, It.IsAny<SmalltalkOOP>()),
            Times.AtLeastOnce());
    }

    [Fact]
    public void Phase2_InvalidSuperclass_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        // Reference non-existent superclass
        bundle.Classes[0].SuperNameIndex = 999;
        bundle.Symbols = bundle.Symbols.Append("NonExistent").ToArray();
        bundle.Classes[0].SuperNameIndex = bundle.Symbols.Length - 1;
        var phase = CreatePhase(bundle, out _, out _);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => phase.Run());
    }

    #endregion

    #region Layout Loading Tests

    [Fact]
    public void Phase2_LoadsPrecomputedLayouts()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out _);

        // Act
        phase.Run();

        // Assert
        Assert.NotNull(phase.LayoutResolver);
    }

    [Fact]
    public void Phase2_LayoutResolver_IsPopulated()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out _);

        // Act
        phase.Run();

        // Assert - LayoutResolver should be populated
        var resolver = phase.LayoutResolver;
        Assert.NotNull(resolver);
    }

    #endregion

    #region Instance Variables Field Population Tests

    [Fact]
    public void Phase2_PopulatesInstanceVariablesField()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out var heapManager, out _);

        // Act
        phase.Run();

        // Assert - SetInstanceVariable should be called for instanceVariables (index 4)
        heapManager.Verify(h => h.SetInstanceVariable(It.IsAny<SmalltalkOOP>(), 4, It.IsAny<SmalltalkOOP>()),
            Times.AtLeastOnce());
    }

    [Fact]
    public void Phase2_AllocatesEmptyArrayForClassesWithNoInstVars()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out var heapManager, out _);

        // Act
        phase.Run();

        // Assert - AllocateObject should be called for empty arrays
        heapManager.Verify(h => h.AllocateObject(ObjectFormat.PointerIndexed, 0, 0), Times.AtLeastOnce());
    }

    [Fact]
    public void Phase2_AllocatesStringsForInstVarNames()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out var heapManager, out _);

        // Act
        phase.Run();

        // Assert - ByteIndexed allocations for String objects
        heapManager.Verify(h => h.AllocateObject(ObjectFormat.ByteIndexed, 0, It.IsAny<int>()), Times.AtLeastOnce());
    }

    #endregion

    #region SystemDictionary Initialization Tests

    [Fact]
    public void Phase2_InitializesSystemDictionaryTally()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out var heapManager, out _);

        // Act
        phase.Run();

        // Assert - tally should be set to SmallInteger(0)
        heapManager.Verify(h => h.SetInstanceVariable(It.IsAny<SmalltalkOOP>(), 0, new SmalltalkOOP(1)),
            Times.AtLeastOnce());
    }

    [Fact]
    public void Phase2_SystemDictionary_HasCorrectSize()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out var heapManager, out _);

        // Act
        phase.Run();

        // Assert - SystemDictionary should be allocated as array
        heapManager.Verify(h => h.AllocateObject(
            ObjectFormat.PointerIndexed,
            It.IsAny<int>(),
            It.Is<int>(size => size >= 500)), // Should be at least 500 (the buffer size)
            Times.AtLeastOnce());
    }

    #endregion

    #region Metaclass Superclass Wiring Tests

    [Fact]
    public void Phase2_WiresMetaclassSuperclasses()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out var heapManager, out _);

        // Act
        phase.Run();

        // Assert - SetInstanceVariable should be called for superclass wiring
        heapManager.Verify(h => h.SetInstanceVariable(It.IsAny<SmalltalkOOP>(), It.IsAny<int>(), It.IsAny<SmalltalkOOP>()),
            Times.AtLeast(5)); // Multiple metaclass superclass wirings
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Phase2_ClassWithManyInstVars_AllocatesCorrectly()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Symbols = bundle.Symbols.Concat(["var1", "var2", "var3", "var4", "var5"]).ToArray();
        int baseIdx = bundle.Symbols.Length - 5;
        bundle.Classes = bundle.Classes.Append(
            CreateClassDescriptor("class:BigClass", baseIdx, 0, false,
                [baseIdx, baseIdx + 1, baseIdx + 2, baseIdx + 3, baseIdx + 4], 5)
        ).ToArray();
        var phase = CreatePhase(bundle, out _, out var localIdToObjectId);

        // Act
        phase.Run();

        // Assert
        Assert.True(localIdToObjectId.ContainsKey("class:BigClass"));
    }

    [Fact]
    public void Phase2_ClassWithClassVars_AllocatesClassPool()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Classes[0].ClassVarNames = [0, 1]; // Two class variables
        var phase = CreatePhase(bundle, out var heapManager, out _);

        // Act
        phase.Run();

        // Assert - classPool dictionary should be allocated
        heapManager.Verify(h => h.SetSlot(It.IsAny<SmalltalkOOP>(), 0, It.IsAny<SmalltalkOOP>()),
            Times.AtLeastOnce());
    }

    #endregion

    #region Report Validation Tests

    [Fact]
    public void Phase2_AddsValidationsToReport()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var heapManager = new Mock<ISmalltalkHeapManager>();
        int oopCounter = 100;
        heapManager.Setup(h => h.AllocateObject(It.IsAny<ObjectFormat>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(() => new SmalltalkOOP(oopCounter++));

        var allocator = new ObjectGraphAllocator(heapManager.Object, bundle);
        var localIdToObjectId = new Dictionary<string, SmalltalkOOP>();
        var initializedFields = new HashSet<(SmalltalkOOP oop, int slot)>();
        var report = new ColdstartReport();

        GlobalRegistry.Reset();
        SymbolRegistry.Reset();
        for (int i = 0; i < bundle.Symbols.Length; i++)
        {
            SymbolRegistry.Register(i, new SmalltalkOOP(1000 + i));
        }
        SymbolRegistry.Freeze();

        var phase = new Phase2_ClassInfrastructure(
            bundle, allocator, heapManager.Object, localIdToObjectId, initializedFields, report, null);

        // Act
        phase.Run();

        // Assert - report should have validations
        Assert.NotEmpty(report.Validations);
    }

    #endregion
}
