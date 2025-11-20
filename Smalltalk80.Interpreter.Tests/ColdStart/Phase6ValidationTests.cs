// Phase6ValidationTests.cs
// Comprehensive unit tests for Phase 6: Validation and GC Verification

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
/// Unit tests for Phase6_ValidationAndGC covering object graph validation,
/// class reference validation, GC root setup, and statistics reporting.
/// </summary>
public class Phase6ValidationTests
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
            Symbols = ["Object", "test", "UndefinedObject", "True", "False"],
            Strings = [],
            Classes = CreateMinimalClasses(),
            Objects = [],
            Methods = [],
            InitExpressions = [],
            Patches = []
        };
    }

    private static ClassDescriptor[] CreateMinimalClasses()
    {
        return
        [
            CreateClassDescriptor("class:Object", 0, null, false, [], 0),
            CreateClassDescriptor("class:UndefinedObject", 2, 0, false, [], 0),
            CreateClassDescriptor("class:True", 3, 0, false, [], 0),
            CreateClassDescriptor("class:False", 4, 0, false, [], 0),
            CreateClassDescriptor("meta:Object", 0, null, true, [], 0, "class:Object"),
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
            Format = isMetaclass ? 7 : 9,
            ClassVarNames = [],
            FieldLayout = Enumerable.Range(0, totalInstVarCount)
                .Select(i => new FieldLayoutEntry { NameIndex = instVarNames.Length > i ? instVarNames[i] : 0, SlotIndex = i })
                .ToArray(),
            MetaclassOfLocalId = metaclassOfLocalId
        };
    }

    private static Phase6_ValidationAndGC CreatePhase(
        ColdStartBundle bundle,
        out Mock<ISmalltalkHeapManager> heapManagerMock,
        out Mock<ObjectGraphAllocator> allocatorMock,
        out Dictionary<string, SmalltalkOOP> localIdToObjectId)
    {
        heapManagerMock = new Mock<ISmalltalkHeapManager>();

        // Set up default heap manager behavior
        heapManagerMock.Setup(h => h.GetActiveObjectCount()).Returns(100);
        heapManagerMock.Setup(h => h.GarbageCollect()).Returns(new GCStatistics
        {
            TotalObjects = 100,
            LiveObjects = 100,
            CollectedObjects = 0,
            CollectedObjectDetails = []
        });
        heapManagerMock.Setup(h => h.GetClassName(It.IsAny<SmalltalkOOP>())).Returns("Object");

        allocatorMock = new Mock<ObjectGraphAllocator>(heapManagerMock.Object, bundle);
        allocatorMock.Setup(a => a.IsValidObjectOOP(It.IsAny<SmalltalkOOP>())).Returns(true);
        allocatorMock.Setup(a => a.GetObjectFormat(It.IsAny<SmalltalkOOP>())).Returns(ObjectFormat.Pointer);
        allocatorMock.Setup(a => a.GetInstanceVariableCount(It.IsAny<SmalltalkOOP>())).Returns(9);
        allocatorMock.Setup(a => a.GetClassOOP(It.IsAny<SmalltalkOOP>())).Returns(new SmalltalkOOP(100));
        allocatorMock.Setup(a => a.GetClassName(It.IsAny<SmalltalkOOP>())).Returns("Object");
        allocatorMock.Setup(a => a.GetIndexedSize(It.IsAny<SmalltalkOOP>())).Returns(0);
        allocatorMock.Setup(a => a.GetInstanceVariable(It.IsAny<SmalltalkOOP>(), It.IsAny<int>()))
            .Returns(new SmalltalkOOP(10));

        localIdToObjectId = new Dictionary<string, SmalltalkOOP>();
        var layoutResolver = new LayoutResolver();
        var report = new ColdstartReport();

        // Reset registries
        GlobalRegistry.Reset();
        SymbolRegistry.Reset();
        AssociationRegistry.Clear();

        // Pre-register symbols
        for (int i = 0; i < bundle.Symbols.Length; i++)
        {
            var symbolOOP = new SmalltalkOOP(1000 + i);
            SymbolRegistry.Register(i, symbolOOP);
        }
        SymbolRegistry.Freeze();

        // Pre-allocate class OOPs
        int oopCounter = 100;
        foreach (var cls in bundle.Classes)
        {
            var classOOP = new SmalltalkOOP(oopCounter++);
            localIdToObjectId[cls.LocalId] = classOOP;
        }

        // Set up special objects
        var nilOOP = new SmalltalkOOP(10);
        var trueOOP = new SmalltalkOOP(11);
        var falseOOP = new SmalltalkOOP(12);
        SpecialObjectsArray.Initialize(nilOOP, falseOOP, trueOOP);

        // Set up SystemDictionary
        var systemDictOOP = new SmalltalkOOP(50);
        GlobalRegistry.Register("Smalltalk", systemDictOOP);

        // Set up USTable
        var ustableOOP = new SmalltalkOOP(60);
        GlobalRegistry.Register("USTable", ustableOOP);
        allocatorMock.Setup(a => a.GetObjectFormat(ustableOOP)).Returns(ObjectFormat.PointerIndexed);
        allocatorMock.Setup(a => a.GetIndexedSize(ustableOOP)).Returns(bundle.Symbols.Length * 2);

        var phase = new Phase6_ValidationAndGC(
            bundle,
            allocatorMock.Object,
            heapManagerMock.Object,
            layoutResolver,
            localIdToObjectId,
            report,
            null);

        return phase;
    }

    #endregion

    #region Phase Properties Tests

    [Fact]
    public void Phase6_Name_ReturnsCorrectName()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out _, out _);

        // Act & Assert
        Assert.Equal("Phase 6: Validation and GC Verification", phase.Name);
    }

    [Fact]
    public void Phase6_PhaseNumber_Returns6()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out _, out _);

        // Act & Assert
        Assert.Equal(6, phase.PhaseNumber);
    }

    [Fact]
    public void Phase6_ValidatePreconditions_ReturnsEmptyList()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out _, out _);

        // Act
        var result = phase.ValidatePreconditions();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Phase6_ValidatePostconditions_ReturnsEmptyList()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out _, out _);

        // Act
        var result = phase.ValidatePostconditions();

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region Class Validation Tests

    [Fact]
    public void Phase6_ValidatesAllClasses()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out var allocator, out _);

        // Act
        phase.Run();

        // Assert - should verify all classes
        allocator.Verify(a => a.IsValidObjectOOP(It.IsAny<SmalltalkOOP>()), Times.AtLeast(bundle.Classes.Length));
    }

    [Fact]
    public void Phase6_InvalidClassOOP_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out var allocator, out _);

        // Make one class OOP invalid
        allocator.Setup(a => a.IsValidObjectOOP(new SmalltalkOOP(100))).Returns(false);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => phase.Run());
    }

    [Fact]
    public void Phase6_WrongClassFormat_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out var allocator, out _);

        // Make one class have wrong format
        allocator.Setup(a => a.GetObjectFormat(new SmalltalkOOP(100))).Returns(ObjectFormat.ByteIndexed);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => phase.Run());
    }

    [Fact]
    public void Phase6_WrongInstanceVariableCount_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out var allocator, out _);

        // Make one class have wrong instance variable count
        allocator.Setup(a => a.GetInstanceVariableCount(new SmalltalkOOP(100))).Returns(3);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => phase.Run());
    }

    #endregion

    #region Method Validation Tests

    [Fact]
    public void Phase6_ValidatesAllMethods()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Methods =
        [
            new MethodDescriptor
            {
                LocalId = "method:Object>>test",
                HomeClassLocalId = "class:Object",
                SelectorIndex = 1,
                Header = new MethodHeader { Word = 42 },
                Literals = [],
                Bytecodes = "00"
            }
        ];

        var phase = CreatePhase(bundle, out _, out var allocator, out var localIdToObjectId);
        localIdToObjectId["method:Object>>test"] = new SmalltalkOOP(200);

        allocator.Setup(a => a.GetObjectFormat(new SmalltalkOOP(200))).Returns(ObjectFormat.PointerIndexed);
        allocator.Setup(a => a.GetIndexedSize(new SmalltalkOOP(200))).Returns(3); // header + 0 literals + bytecode ref
        allocator.Setup(a => a.GetIndexedOOP(new SmalltalkOOP(200), 0)).Returns(SmalltalkOOP.FromSmallInteger(42));
        allocator.Setup(a => a.GetIndexedOOP(new SmalltalkOOP(200), 1)).Returns(new SmalltalkOOP(300));

        // Act
        phase.Run();

        // Assert
        allocator.Verify(a => a.IsValidObjectOOP(new SmalltalkOOP(200)), Times.AtLeastOnce());
    }

    [Fact]
    public void Phase6_InvalidMethodFormat_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Methods =
        [
            new MethodDescriptor
            {
                LocalId = "method:Object>>test",
                HomeClassLocalId = "class:Object",
                SelectorIndex = 1,
                Header = new MethodHeader { Word = 42 },
                Literals = [],
                Bytecodes = "00"
            }
        ];

        var phase = CreatePhase(bundle, out _, out var allocator, out var localIdToObjectId);
        localIdToObjectId["method:Object>>test"] = new SmalltalkOOP(200);

        // Wrong format for CompiledMethod
        allocator.Setup(a => a.GetObjectFormat(new SmalltalkOOP(200))).Returns(ObjectFormat.Pointer);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => phase.Run());
    }

    #endregion

    #region Special Objects Validation Tests

    [Fact]
    public void Phase6_ValidatesNilObject()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out var allocator, out _);

        allocator.Setup(a => a.GetClassName(SpecialObjectsArray.Instance.Nil)).Returns("UndefinedObject");

        // Act
        phase.Run();

        // Assert
        allocator.Verify(a => a.GetClassName(SpecialObjectsArray.Instance.Nil), Times.AtLeastOnce());
    }

    [Fact]
    public void Phase6_ValidatesTrueObject()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out var allocator, out _);

        allocator.Setup(a => a.GetClassName(SpecialObjectsArray.Instance.True)).Returns("True");

        // Act
        phase.Run();

        // Assert
        allocator.Verify(a => a.GetClassName(SpecialObjectsArray.Instance.True), Times.AtLeastOnce());
    }

    [Fact]
    public void Phase6_ValidatesFalseObject()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out var allocator, out _);

        allocator.Setup(a => a.GetClassName(SpecialObjectsArray.Instance.False)).Returns("False");

        // Act
        phase.Run();

        // Assert
        allocator.Verify(a => a.GetClassName(SpecialObjectsArray.Instance.False), Times.AtLeastOnce());
    }

    [Fact]
    public void Phase6_InvalidNilClass_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out var allocator, out _);

        // nil has wrong class
        allocator.Setup(a => a.GetClassName(SpecialObjectsArray.Instance.Nil)).Returns("Object");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => phase.Run());
    }

    #endregion

    #region GC Root Setup Tests

    [Fact]
    public void Phase6_SetsUpGCRoots()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out var heapManager, out _, out _);

        // Act
        phase.Run();

        // Assert
        heapManager.Verify(h => h.SetGCRoots(It.IsAny<SmalltalkOOP[]>()), Times.Once());
    }

    [Fact]
    public void Phase6_GCRootsIncludeNil()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out var heapManager, out _, out _);

        SmalltalkOOP[]? capturedRoots = null;
        heapManager.Setup(h => h.SetGCRoots(It.IsAny<SmalltalkOOP[]>()))
            .Callback<SmalltalkOOP[]>(roots => capturedRoots = roots);

        // Act
        phase.Run();

        // Assert
        Assert.NotNull(capturedRoots);
        Assert.Contains(SpecialObjectsArray.Instance.Nil, capturedRoots);
    }

    [Fact]
    public void Phase6_GCRootsIncludeSystemDictionary()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out var heapManager, out _, out _);

        SmalltalkOOP[]? capturedRoots = null;
        heapManager.Setup(h => h.SetGCRoots(It.IsAny<SmalltalkOOP[]>()))
            .Callback<SmalltalkOOP[]>(roots => capturedRoots = roots);

        // Act
        phase.Run();

        // Assert
        Assert.NotNull(capturedRoots);
        Assert.Contains(GlobalRegistry.SystemDictionaryOOP, capturedRoots);
    }

    #endregion

    #region GC Verification Tests

    [Fact]
    public void Phase6_RunsGarbageCollection()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out var heapManager, out _, out _);

        // Act
        phase.Run();

        // Assert
        heapManager.Verify(h => h.GarbageCollect(), Times.Once());
    }

    [Fact]
    public void Phase6_AllObjectsReachable_Succeeds()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out var heapManager, out _, out _);

        heapManager.Setup(h => h.GarbageCollect()).Returns(new GCStatistics
        {
            TotalObjects = 100,
            LiveObjects = 100,
            CollectedObjects = 0,
            CollectedObjectDetails = []
        });

        // Act & Assert - should not throw
        phase.Run();
    }

    [Fact]
    public void Phase6_ObjectsCollected_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out var heapManager, out _, out _);

        heapManager.Setup(h => h.GarbageCollect()).Returns(new GCStatistics
        {
            TotalObjects = 100,
            LiveObjects = 95,
            CollectedObjects = 5,
            CollectedObjectDetails =
            [
                (200, "Array", ObjectFormat.PointerIndexed, 10),
                (201, "String", ObjectFormat.ByteIndexed, 5)
            ]
        });

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => phase.Run());
        Assert.Contains("GC collected", ex.Message);
    }

    [Fact]
    public void Phase6_UpdatesReportMetrics()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();

        var heapManager = new Mock<ISmalltalkHeapManager>();
        heapManager.Setup(h => h.GetActiveObjectCount()).Returns(100);
        heapManager.Setup(h => h.GarbageCollect()).Returns(new GCStatistics
        {
            TotalObjects = 100,
            LiveObjects = 100,
            CollectedObjects = 0,
            CollectedObjectDetails = []
        });
        heapManager.Setup(h => h.GetClassName(It.IsAny<SmalltalkOOP>())).Returns("Object");

        var allocator = new Mock<ObjectGraphAllocator>(heapManager.Object, bundle);
        allocator.Setup(a => a.IsValidObjectOOP(It.IsAny<SmalltalkOOP>())).Returns(true);
        allocator.Setup(a => a.GetObjectFormat(It.IsAny<SmalltalkOOP>())).Returns(ObjectFormat.Pointer);
        allocator.Setup(a => a.GetInstanceVariableCount(It.IsAny<SmalltalkOOP>())).Returns(9);
        allocator.Setup(a => a.GetClassOOP(It.IsAny<SmalltalkOOP>())).Returns(new SmalltalkOOP(100));
        allocator.Setup(a => a.GetClassName(It.IsAny<SmalltalkOOP>())).Returns("UndefinedObject");
        allocator.Setup(a => a.GetIndexedSize(It.IsAny<SmalltalkOOP>())).Returns(bundle.Symbols.Length * 2);
        allocator.Setup(a => a.GetInstanceVariable(It.IsAny<SmalltalkOOP>(), It.IsAny<int>()))
            .Returns(new SmalltalkOOP(10));

        var localIdToObjectId = new Dictionary<string, SmalltalkOOP>();
        var layoutResolver = new LayoutResolver();
        var report = new ColdstartReport();

        GlobalRegistry.Reset();
        SymbolRegistry.Reset();
        AssociationRegistry.Clear();

        for (int i = 0; i < bundle.Symbols.Length; i++)
        {
            SymbolRegistry.Register(i, new SmalltalkOOP(1000 + i));
        }
        SymbolRegistry.Freeze();

        int oopCounter = 100;
        foreach (var cls in bundle.Classes)
        {
            localIdToObjectId[cls.LocalId] = new SmalltalkOOP(oopCounter++);
        }

        var nilOOP = new SmalltalkOOP(10);
        var trueOOP = new SmalltalkOOP(11);
        var falseOOP = new SmalltalkOOP(12);
        SpecialObjectsArray.Initialize(nilOOP, falseOOP, trueOOP);

        GlobalRegistry.Register("Smalltalk", new SmalltalkOOP(50));
        GlobalRegistry.Register("USTable", new SmalltalkOOP(60));

        var phase = new Phase6_ValidationAndGC(
            bundle, allocator.Object, heapManager.Object, layoutResolver,
            localIdToObjectId, report, null);

        // Act
        phase.Run();

        // Assert
        Assert.Equal(100, report.Metrics.TotalObjectsAllocated);
        Assert.Equal(100, report.Metrics.TotalObjectsReachable);
        Assert.True(report.Metrics.GCPassed);
    }

    #endregion

    #region Cross-Reference Validation Tests

    [Fact]
    public void Phase6_ValidatesCrossReferences()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out var allocator, out _);

        // Act
        phase.Run();

        // Assert - should check class OOP for each object
        allocator.Verify(a => a.GetClassOOP(It.IsAny<SmalltalkOOP>()), Times.AtLeastOnce());
    }

    [Fact]
    public void Phase6_InvalidClassReference_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var phase = CreatePhase(bundle, out _, out var allocator, out var localIdToObjectId);

        // Make one object have invalid class reference
        var testOOP = localIdToObjectId["class:Object"];
        allocator.Setup(a => a.GetClassOOP(testOOP)).Returns(new SmalltalkOOP(9999));
        allocator.Setup(a => a.IsValidObjectOOP(new SmalltalkOOP(9999))).Returns(false);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => phase.Run());
    }

    #endregion
}
