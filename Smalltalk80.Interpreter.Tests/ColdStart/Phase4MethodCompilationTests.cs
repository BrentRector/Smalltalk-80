// Phase4MethodCompilationTests.cs
// Comprehensive unit tests for Phase 4: Method Compilation

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
/// Unit tests for Phase4_MethodCompilation covering method compilation from descriptors,
/// literal resolution, bytecode installation, and method header encoding.
/// </summary>
public class Phase4MethodCompilationTests
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
            Symbols = ["Object", "test", "CompiledMethod", "ByteArray", "Association", "Symbol",
                       "Array", "Dictionary", "tally"],
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
            CreateClassDescriptor("class:CompiledMethod", 2, 0, false, [], 1),
            CreateClassDescriptor("class:ByteArray", 3, 0, false, [], 0),
            CreateClassDescriptor("class:Association", 4, 0, false, [], 2),
            CreateClassDescriptor("class:Symbol", 5, 0, false, [], 0),
            CreateClassDescriptor("class:Array", 6, 0, false, [], 0),
            CreateClassDescriptor("class:Dictionary", 7, 0, false, [8], 1),
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
            Format = 0,
            ClassVarNames = [],
            FieldLayout = Enumerable.Range(0, totalInstVarCount)
                .Select(i => new FieldLayoutEntry { NameIndex = instVarNames.Length > i ? instVarNames[i] : 0, SlotIndex = i })
                .ToArray(),
            MetaclassOfLocalId = metaclassOfLocalId
        };
    }

    private static void SetupPhasePrerequisites(
        ColdStartBundle bundle,
        Mock<ISmalltalkHeapManager> heapManager,
        Dictionary<string, SmalltalkOOP> localIdToObjectId)
    {
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
            if (!cls.IsMetaclass)
            {
                GlobalRegistry.RegisterClass(bundle.Symbols[cls.NameIndex], classOOP);
            }
        }

        // Set up special objects
        var nilOOP = new SmalltalkOOP(10);
        var trueOOP = new SmalltalkOOP(11);
        var falseOOP = new SmalltalkOOP(12);
        SpecialObjectsArray.Initialize(nilOOP, falseOOP, trueOOP);

        // Set up SystemDictionary
        var systemDictOOP = new SmalltalkOOP(50);
        GlobalRegistry.Register("Smalltalk", systemDictOOP);
    }

    #endregion

    #region Phase Properties Tests

    [Fact]
    public void Phase4_Name_ReturnsCorrectName()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var heapManager = new Mock<ISmalltalkHeapManager>();
        var localIdToObjectId = new Dictionary<string, SmalltalkOOP>();
        SetupPhasePrerequisites(bundle, heapManager, localIdToObjectId);

        var allocator = new Mock<ObjectGraphAllocator>(heapManager.Object, bundle);
        var layoutResolver = new LayoutResolver();
        var initializedFields = new HashSet<(SmalltalkOOP oop, int slot)>();
        var report = new ColdstartReport();

        var phase = new Phase4_MethodCompilation(
            bundle, allocator.Object, heapManager.Object, layoutResolver,
            localIdToObjectId, initializedFields, report, null);

        // Act & Assert
        Assert.Equal("Phase 4: Method Compilation", phase.Name);
    }

    [Fact]
    public void Phase4_PhaseNumber_Returns4()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var heapManager = new Mock<ISmalltalkHeapManager>();
        var localIdToObjectId = new Dictionary<string, SmalltalkOOP>();
        SetupPhasePrerequisites(bundle, heapManager, localIdToObjectId);

        var allocator = new Mock<ObjectGraphAllocator>(heapManager.Object, bundle);
        var layoutResolver = new LayoutResolver();
        var initializedFields = new HashSet<(SmalltalkOOP oop, int slot)>();
        var report = new ColdstartReport();

        var phase = new Phase4_MethodCompilation(
            bundle, allocator.Object, heapManager.Object, layoutResolver,
            localIdToObjectId, initializedFields, report, null);

        // Act & Assert
        Assert.Equal(4, phase.PhaseNumber);
    }

    [Fact]
    public void Phase4_ValidatePreconditions_ReturnsEmptyList()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var heapManager = new Mock<ISmalltalkHeapManager>();
        var localIdToObjectId = new Dictionary<string, SmalltalkOOP>();
        SetupPhasePrerequisites(bundle, heapManager, localIdToObjectId);

        var allocator = new Mock<ObjectGraphAllocator>(heapManager.Object, bundle);
        var layoutResolver = new LayoutResolver();
        var initializedFields = new HashSet<(SmalltalkOOP oop, int slot)>();
        var report = new ColdstartReport();

        var phase = new Phase4_MethodCompilation(
            bundle, allocator.Object, heapManager.Object, layoutResolver,
            localIdToObjectId, initializedFields, report, null);

        // Act
        var result = phase.ValidatePreconditions();

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region Method Compilation Tests

    [Fact]
    public void Phase4_CompilesMethodFromDescriptor()
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
                Header = new MethodHeader { Word = 0 },
                Literals = [],
                Bytecodes = "00" // Single NOP bytecode
            }
        ];

        var heapManager = new Mock<ISmalltalkHeapManager>();
        heapManager.Setup(h => h.AllocateObject(It.IsAny<ObjectFormat>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new SmalltalkOOP(200));
        heapManager.Setup(h => h.GetIndexedSize(It.IsAny<SmalltalkOOP>())).Returns(100);

        var localIdToObjectId = new Dictionary<string, SmalltalkOOP>();
        SetupPhasePrerequisites(bundle, heapManager, localIdToObjectId);

        var allocator = new Mock<ObjectGraphAllocator>(heapManager.Object, bundle);
        allocator.Setup(a => a.AllocateCompiledMethod(
            It.IsAny<SmalltalkOOP>(), It.IsAny<SmalltalkOOP>(), It.IsAny<SmalltalkOOP>(),
            It.IsAny<int>(), It.IsAny<SmalltalkOOP[]>(), It.IsAny<byte[]>()))
            .Returns(new SmalltalkOOP(300));
        allocator.Setup(a => a.GetIndexedSize(It.IsAny<SmalltalkOOP>())).Returns(100);

        var layoutResolver = new LayoutResolver();
        var initializedFields = new HashSet<(SmalltalkOOP oop, int slot)>();
        var report = new ColdstartReport();

        var phase = new Phase4_MethodCompilation(
            bundle, allocator.Object, heapManager.Object, layoutResolver,
            localIdToObjectId, initializedFields, report, null);

        // Act
        phase.Run();

        // Assert - method should be compiled
        Assert.True(localIdToObjectId.ContainsKey("method:Object>>test"));
    }

    [Fact]
    public void Phase4_CompilesMultipleMethods()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Symbols = bundle.Symbols.Concat(["foo", "bar"]).ToArray();
        bundle.Methods =
        [
            new MethodDescriptor
            {
                LocalId = "method:Object>>foo",
                HomeClassLocalId = "class:Object",
                SelectorIndex = bundle.Symbols.Length - 2,
                Header = new MethodHeader { Word = 0 },
                Literals = [],
                Bytecodes = "00"
            },
            new MethodDescriptor
            {
                LocalId = "method:Object>>bar",
                HomeClassLocalId = "class:Object",
                SelectorIndex = bundle.Symbols.Length - 1,
                Header = new MethodHeader { Word = 0 },
                Literals = [],
                Bytecodes = "00"
            }
        ];

        var heapManager = new Mock<ISmalltalkHeapManager>();
        int oopCounter = 200;
        heapManager.Setup(h => h.AllocateObject(It.IsAny<ObjectFormat>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(() => new SmalltalkOOP(oopCounter++));
        heapManager.Setup(h => h.GetIndexedSize(It.IsAny<SmalltalkOOP>())).Returns(100);

        var localIdToObjectId = new Dictionary<string, SmalltalkOOP>();
        SetupPhasePrerequisites(bundle, heapManager, localIdToObjectId);

        var allocator = new Mock<ObjectGraphAllocator>(heapManager.Object, bundle);
        allocator.Setup(a => a.AllocateCompiledMethod(
            It.IsAny<SmalltalkOOP>(), It.IsAny<SmalltalkOOP>(), It.IsAny<SmalltalkOOP>(),
            It.IsAny<int>(), It.IsAny<SmalltalkOOP[]>(), It.IsAny<byte[]>()))
            .Returns(() => new SmalltalkOOP(oopCounter++));
        allocator.Setup(a => a.GetIndexedSize(It.IsAny<SmalltalkOOP>())).Returns(100);

        var layoutResolver = new LayoutResolver();
        var initializedFields = new HashSet<(SmalltalkOOP oop, int slot)>();
        var report = new ColdstartReport();

        var phase = new Phase4_MethodCompilation(
            bundle, allocator.Object, heapManager.Object, layoutResolver,
            localIdToObjectId, initializedFields, report, null);

        // Act
        phase.Run();

        // Assert
        Assert.True(localIdToObjectId.ContainsKey("method:Object>>foo"));
        Assert.True(localIdToObjectId.ContainsKey("method:Object>>bar"));
    }

    [Fact]
    public void Phase4_MissingCompiledMethodClass_ThrowsException()
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
                Header = new MethodHeader { Word = 0 },
                Literals = [],
                Bytecodes = "00"
            }
        ];

        var heapManager = new Mock<ISmalltalkHeapManager>();
        var localIdToObjectId = new Dictionary<string, SmalltalkOOP>();

        // Don't set up CompiledMethod class
        GlobalRegistry.Reset();
        SymbolRegistry.Reset();
        for (int i = 0; i < bundle.Symbols.Length; i++)
        {
            SymbolRegistry.Register(i, new SmalltalkOOP(1000 + i));
        }
        SymbolRegistry.Freeze();

        // Only add Object class
        localIdToObjectId["class:Object"] = new SmalltalkOOP(100);

        var nilOOP = new SmalltalkOOP(10);
        SpecialObjectsArray.Initialize(nilOOP, new SmalltalkOOP(11), new SmalltalkOOP(12));

        var allocator = new Mock<ObjectGraphAllocator>(heapManager.Object, bundle);
        var layoutResolver = new LayoutResolver();
        var initializedFields = new HashSet<(SmalltalkOOP oop, int slot)>();
        var report = new ColdstartReport();

        var phase = new Phase4_MethodCompilation(
            bundle, allocator.Object, heapManager.Object, layoutResolver,
            localIdToObjectId, initializedFields, report, null);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => phase.Run());
        Assert.Contains("CompiledMethod class not found", ex.Message);
    }

    #endregion

    #region Literal Resolution Tests

    [Fact]
    public void Phase4_ResolvesSymbolLiteral()
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
                Header = new MethodHeader { Word = 0 },
                Literals =
                [
                    new LiteralDescriptor { Index = 0, Type = "symbol", SymbolIndex = 0 }
                ],
                Bytecodes = "00"
            }
        ];

        var heapManager = new Mock<ISmalltalkHeapManager>();
        heapManager.Setup(h => h.AllocateObject(It.IsAny<ObjectFormat>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new SmalltalkOOP(200));
        heapManager.Setup(h => h.GetIndexedSize(It.IsAny<SmalltalkOOP>())).Returns(100);

        var localIdToObjectId = new Dictionary<string, SmalltalkOOP>();
        SetupPhasePrerequisites(bundle, heapManager, localIdToObjectId);

        var allocator = new Mock<ObjectGraphAllocator>(heapManager.Object, bundle);
        allocator.Setup(a => a.AllocateCompiledMethod(
            It.IsAny<SmalltalkOOP>(), It.IsAny<SmalltalkOOP>(), It.IsAny<SmalltalkOOP>(),
            It.IsAny<int>(), It.IsAny<SmalltalkOOP[]>(), It.IsAny<byte[]>()))
            .Returns(new SmalltalkOOP(300));
        allocator.Setup(a => a.GetIndexedSize(It.IsAny<SmalltalkOOP>())).Returns(100);

        var layoutResolver = new LayoutResolver();
        var initializedFields = new HashSet<(SmalltalkOOP oop, int slot)>();
        var report = new ColdstartReport();

        var phase = new Phase4_MethodCompilation(
            bundle, allocator.Object, heapManager.Object, layoutResolver,
            localIdToObjectId, initializedFields, report, null);

        // Act
        phase.Run();

        // Assert - SetIndexedOOP should be called to patch literals
        allocator.Verify(a => a.SetIndexedOOP(It.IsAny<SmalltalkOOP>(), It.IsAny<int>(), It.IsAny<SmalltalkOOP>()),
            Times.AtLeastOnce());
    }

    [Fact]
    public void Phase4_ResolvesSmallIntegerLiteral()
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
                Header = new MethodHeader { Word = 0 },
                Literals =
                [
                    new LiteralDescriptor { Index = 0, Type = "smallInteger", SmallInteger = 42 }
                ],
                Bytecodes = "00"
            }
        ];

        var heapManager = new Mock<ISmalltalkHeapManager>();
        heapManager.Setup(h => h.AllocateObject(It.IsAny<ObjectFormat>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new SmalltalkOOP(200));
        heapManager.Setup(h => h.GetIndexedSize(It.IsAny<SmalltalkOOP>())).Returns(100);

        var localIdToObjectId = new Dictionary<string, SmalltalkOOP>();
        SetupPhasePrerequisites(bundle, heapManager, localIdToObjectId);

        var allocator = new Mock<ObjectGraphAllocator>(heapManager.Object, bundle);
        allocator.Setup(a => a.AllocateCompiledMethod(
            It.IsAny<SmalltalkOOP>(), It.IsAny<SmalltalkOOP>(), It.IsAny<SmalltalkOOP>(),
            It.IsAny<int>(), It.IsAny<SmalltalkOOP[]>(), It.IsAny<byte[]>()))
            .Returns(new SmalltalkOOP(300));
        allocator.Setup(a => a.GetIndexedSize(It.IsAny<SmalltalkOOP>())).Returns(100);

        var layoutResolver = new LayoutResolver();
        var initializedFields = new HashSet<(SmalltalkOOP oop, int slot)>();
        var report = new ColdstartReport();

        var phase = new Phase4_MethodCompilation(
            bundle, allocator.Object, heapManager.Object, layoutResolver,
            localIdToObjectId, initializedFields, report, null);

        // Act
        phase.Run();

        // Assert
        allocator.Verify(a => a.SetIndexedOOP(It.IsAny<SmalltalkOOP>(), It.IsAny<int>(), It.IsAny<SmalltalkOOP>()),
            Times.AtLeastOnce());
    }

    #endregion

    #region Block Ownership Tests

    [Fact]
    public void Phase4_BuildsBlockOwnerMap()
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
                Header = new MethodHeader { Word = 0 },
                Literals =
                [
                    new LiteralDescriptor { Index = 0, Type = "compiledMethod", LocalId = "block:1" }
                ],
                Bytecodes = "00"
            },
            new MethodDescriptor
            {
                LocalId = "block:1",
                HomeClassLocalId = "",
                SelectorIndex = 0,
                Header = new MethodHeader { Word = 0 },
                Literals = [],
                Bytecodes = "00"
            }
        ];

        var heapManager = new Mock<ISmalltalkHeapManager>();
        int oopCounter = 200;
        heapManager.Setup(h => h.AllocateObject(It.IsAny<ObjectFormat>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(() => new SmalltalkOOP(oopCounter++));
        heapManager.Setup(h => h.GetIndexedSize(It.IsAny<SmalltalkOOP>())).Returns(100);

        var localIdToObjectId = new Dictionary<string, SmalltalkOOP>();
        SetupPhasePrerequisites(bundle, heapManager, localIdToObjectId);

        var allocator = new Mock<ObjectGraphAllocator>(heapManager.Object, bundle);
        allocator.Setup(a => a.AllocateCompiledMethod(
            It.IsAny<SmalltalkOOP>(), It.IsAny<SmalltalkOOP>(), It.IsAny<SmalltalkOOP>(),
            It.IsAny<int>(), It.IsAny<SmalltalkOOP[]>(), It.IsAny<byte[]>()))
            .Returns(() => new SmalltalkOOP(oopCounter++));
        allocator.Setup(a => a.GetIndexedSize(It.IsAny<SmalltalkOOP>())).Returns(100);

        var layoutResolver = new LayoutResolver();
        var initializedFields = new HashSet<(SmalltalkOOP oop, int slot)>();
        var report = new ColdstartReport();

        var phase = new Phase4_MethodCompilation(
            bundle, allocator.Object, heapManager.Object, layoutResolver,
            localIdToObjectId, initializedFields, report, null);

        // Act
        phase.Run();

        // Assert - blockOwnerMap should be stored in GlobalRegistry
        var blockOwnerMap = GlobalRegistry.GetObject<Dictionary<string, string>>("blockOwnerMap");
        Assert.NotNull(blockOwnerMap);
        Assert.True(blockOwnerMap.ContainsKey("block:1"));
        Assert.Equal("method:Object>>test", blockOwnerMap["block:1"]);
    }

    #endregion

    #region Init Expression Tests

    [Fact]
    public void Phase4_CreatesInitExpressionMethods()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.InitExpressions =
        [
            new InitExpression
            {
                LocalId = "init:0",
                Source = "Object test",
                Header = new MethodHeader { Word = 0 },
                Literals = [],
                Bytecodes = "00"
            }
        ];

        var heapManager = new Mock<ISmalltalkHeapManager>();
        int oopCounter = 200;
        heapManager.Setup(h => h.AllocateObject(It.IsAny<ObjectFormat>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(() => new SmalltalkOOP(oopCounter++));
        heapManager.Setup(h => h.GetIndexedSize(It.IsAny<SmalltalkOOP>())).Returns(100);

        var localIdToObjectId = new Dictionary<string, SmalltalkOOP>();
        SetupPhasePrerequisites(bundle, heapManager, localIdToObjectId);

        var allocator = new Mock<ObjectGraphAllocator>(heapManager.Object, bundle);
        allocator.Setup(a => a.AllocateCompiledMethod(
            It.IsAny<SmalltalkOOP>(), It.IsAny<SmalltalkOOP>(), It.IsAny<SmalltalkOOP>(),
            It.IsAny<int>(), It.IsAny<SmalltalkOOP[]>(), It.IsAny<byte[]>()))
            .Returns(() => new SmalltalkOOP(oopCounter++));
        allocator.Setup(a => a.AllocateArray(It.IsAny<SmalltalkOOP>(), It.IsAny<int>()))
            .Returns(new SmalltalkOOP(500));
        allocator.Setup(a => a.GetIndexedSize(It.IsAny<SmalltalkOOP>())).Returns(100);

        var layoutResolver = new LayoutResolver();
        var initializedFields = new HashSet<(SmalltalkOOP oop, int slot)>();
        var report = new ColdstartReport();

        var phase = new Phase4_MethodCompilation(
            bundle, allocator.Object, heapManager.Object, layoutResolver,
            localIdToObjectId, initializedFields, report, null);

        // Act
        phase.Run();

        // Assert
        Assert.True(localIdToObjectId.ContainsKey("init:0"));
    }

    [Fact]
    public void Phase4_EmptyInitExpressions_SetsNil()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.InitExpressions = [];

        var heapManager = new Mock<ISmalltalkHeapManager>();
        heapManager.Setup(h => h.AllocateObject(It.IsAny<ObjectFormat>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new SmalltalkOOP(200));
        heapManager.Setup(h => h.GetIndexedSize(It.IsAny<SmalltalkOOP>())).Returns(100);

        var localIdToObjectId = new Dictionary<string, SmalltalkOOP>();
        SetupPhasePrerequisites(bundle, heapManager, localIdToObjectId);

        var allocator = new Mock<ObjectGraphAllocator>(heapManager.Object, bundle);
        allocator.Setup(a => a.GetIndexedSize(It.IsAny<SmalltalkOOP>())).Returns(100);

        var layoutResolver = new LayoutResolver();
        var initializedFields = new HashSet<(SmalltalkOOP oop, int slot)>();
        var report = new ColdstartReport();

        var phase = new Phase4_MethodCompilation(
            bundle, allocator.Object, heapManager.Object, layoutResolver,
            localIdToObjectId, initializedFields, report, null);

        // Act
        phase.Run();

        // Assert - InitExpressionMethods should be set to nil
        Assert.Equal(SpecialObjectsArray.Instance.Nil, SpecialObjectsArray.Instance.InitExpressionMethods);
    }

    #endregion

    #region Report Metrics Tests

    [Fact]
    public void Phase4_UpdatesReportMetrics()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.InitExpressions =
        [
            new InitExpression
            {
                LocalId = "init:0",
                Source = "Object test",
                Header = new MethodHeader { Word = 0 },
                Literals = [],
                Bytecodes = "00"
            }
        ];

        var heapManager = new Mock<ISmalltalkHeapManager>();
        int oopCounter = 200;
        heapManager.Setup(h => h.AllocateObject(It.IsAny<ObjectFormat>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(() => new SmalltalkOOP(oopCounter++));
        heapManager.Setup(h => h.GetIndexedSize(It.IsAny<SmalltalkOOP>())).Returns(100);

        var localIdToObjectId = new Dictionary<string, SmalltalkOOP>();
        SetupPhasePrerequisites(bundle, heapManager, localIdToObjectId);

        var allocator = new Mock<ObjectGraphAllocator>(heapManager.Object, bundle);
        allocator.Setup(a => a.AllocateCompiledMethod(
            It.IsAny<SmalltalkOOP>(), It.IsAny<SmalltalkOOP>(), It.IsAny<SmalltalkOOP>(),
            It.IsAny<int>(), It.IsAny<SmalltalkOOP[]>(), It.IsAny<byte[]>()))
            .Returns(() => new SmalltalkOOP(oopCounter++));
        allocator.Setup(a => a.AllocateArray(It.IsAny<SmalltalkOOP>(), It.IsAny<int>()))
            .Returns(new SmalltalkOOP(500));
        allocator.Setup(a => a.GetIndexedSize(It.IsAny<SmalltalkOOP>())).Returns(100);

        var layoutResolver = new LayoutResolver();
        var initializedFields = new HashSet<(SmalltalkOOP oop, int slot)>();
        var report = new ColdstartReport();

        var phase = new Phase4_MethodCompilation(
            bundle, allocator.Object, heapManager.Object, layoutResolver,
            localIdToObjectId, initializedFields, report, null);

        // Act
        phase.Run();

        // Assert
        Assert.Equal(1, report.Metrics.InitMethodCount);
    }

    #endregion
}
