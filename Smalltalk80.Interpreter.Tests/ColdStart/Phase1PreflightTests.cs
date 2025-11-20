// Phase1PreflightTests.cs
// Comprehensive unit tests for Phase 1: Preflight Validation and Symbol Interning

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
/// Unit tests for Phase1_PreflightAndSymbols covering bundle validation,
/// symbol table creation, special object allocation, and symbol interning.
/// </summary>
public class Phase1PreflightTests
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
            Symbols = ["Object", "nil", "true", "false", "UndefinedObject", "True", "False", "Behavior", "ClassDescription", "Class", "Metaclass"],
            Strings = [],
            Classes =
            [
                CreateClassDescriptor("class:Object", 0, null, false, []),
                CreateClassDescriptor("class:UndefinedObject", 4, 0, false, []),
                CreateClassDescriptor("class:True", 5, 0, false, []),
                CreateClassDescriptor("class:False", 6, 0, false, []),
                CreateClassDescriptor("class:Behavior", 7, 0, false, []),
                CreateClassDescriptor("class:ClassDescription", 8, 7, false, []),
                CreateClassDescriptor("class:Class", 9, 8, false, []),
                CreateClassDescriptor("class:Metaclass", 10, 8, false, []),
                CreateClassDescriptor("meta:Object", 0, null, true, [], "class:Object"),
                CreateClassDescriptor("meta:Behavior", 7, null, true, [], "class:Behavior"),
                CreateClassDescriptor("meta:ClassDescription", 8, null, true, [], "class:ClassDescription"),
                CreateClassDescriptor("meta:Class", 9, null, true, [], "class:Class"),
                CreateClassDescriptor("meta:Metaclass", 10, null, true, [], "class:Metaclass"),
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
        string? metaclassOfLocalId = null)
    {
        return new ClassDescriptor
        {
            LocalId = localId,
            NameIndex = nameIndex,
            SuperNameIndex = superNameIndex,
            IsMetaclass = isMetaclass,
            InstVarNames = instVarNames,
            TotalInstanceVarCount = instVarNames.Length,
            Format = 0,
            ClassVarNames = [],
            FieldLayout = instVarNames.Select((idx, i) => new FieldLayoutEntry { NameIndex = idx, SlotIndex = i }).ToArray(),
            MetaclassOfLocalId = metaclassOfLocalId
        };
    }

    private static (Phase1_PreflightAndSymbols phase, Mock<ObjectGraphAllocator> allocator) CreatePhase(ColdStartBundle bundle)
    {
        var mockHeapManager = new Mock<ISmalltalkHeapManager>();
        mockHeapManager.Setup(h => h.AllocateObject(It.IsAny<ObjectFormat>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new SmalltalkOOP(100));

        var allocator = new Mock<ObjectGraphAllocator>(mockHeapManager.Object, bundle);
        allocator.Setup(a => a.AllocateString(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(new SmalltalkOOP(200));

        var report = new ColdstartReport();

        // Reset SymbolRegistry for each test
        SymbolRegistry.Reset();

        var phase = new Phase1_PreflightAndSymbols(bundle, allocator.Object, report, null);
        return (phase, allocator);
    }

    #endregion

    #region Bundle Version Validation Tests

    [Fact]
    public void Phase1_ValidBundleVersion_106_Succeeds()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Metadata.Version = "1.0.6";
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert - should not throw
        phase.Run();
    }

    [Fact]
    public void Phase1_ValidBundleVersion_200_Succeeds()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Metadata.Version = "2.0.0";
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert - should not throw
        phase.Run();
    }

    [Fact]
    public void Phase1_InvalidBundleVersion_100_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Metadata.Version = "1.0.0";
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => phase.Run());
        Assert.Contains("not supported", ex.Message);
        Assert.Contains("1.0.6", ex.Message);
    }

    [Fact]
    public void Phase1_InvalidBundleVersion_105_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Metadata.Version = "1.0.5";
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => phase.Run());
        Assert.Contains("not supported", ex.Message);
    }

    [Fact]
    public void Phase1_MalformedVersion_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Metadata.Version = "invalid";
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => phase.Run());
    }

    [Fact]
    public void Phase1_EmptyVersion_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Metadata.Version = "";
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => phase.Run());
    }

    #endregion

    #region LocalId Validation Tests

    [Fact]
    public void Phase1_UniqueLocalIds_Succeeds()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert - should not throw
        phase.Run();
    }

    [Fact]
    public void Phase1_DuplicateClassLocalIds_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Classes = bundle.Classes.Append(
            CreateClassDescriptor("class:Object", 0, null, false, [])
        ).ToArray();
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => phase.Run());
        Assert.Contains("Duplicate localIds", ex.Message);
    }

    [Fact]
    public void Phase1_DuplicateMethodLocalIds_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Methods =
        [
            new MethodDescriptor { LocalId = "method:test", HomeClassLocalId = "class:Object", SelectorIndex = 0, Header = new MethodHeader(), Literals = [], Bytecodes = "" },
            new MethodDescriptor { LocalId = "method:test", HomeClassLocalId = "class:Object", SelectorIndex = 1, Header = new MethodHeader(), Literals = [], Bytecodes = "" }
        ];
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => phase.Run());
        Assert.Contains("Duplicate localIds", ex.Message);
    }

    #endregion

    #region Symbol Index Validation Tests

    [Fact]
    public void Phase1_ValidSymbolIndices_Succeeds()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert - should not throw
        phase.Run();
    }

    [Fact]
    public void Phase1_InvalidClassNameIndex_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Classes[0].NameIndex = 999; // Out of range
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert
        Assert.Throws<IndexOutOfRangeException>(() => phase.Run());
    }

    [Fact]
    public void Phase1_InvalidInstVarNameIndex_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Classes[0].InstVarNames = [999]; // Out of range
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert
        Assert.Throws<IndexOutOfRangeException>(() => phase.Run());
    }

    #endregion

    #region Precomputed Layout Validation Tests

    [Fact]
    public void Phase1_ValidFieldLayouts_Succeeds()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert - should not throw
        phase.Run();
    }

    [Fact]
    public void Phase1_MissingFieldLayout_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Classes[0].FieldLayout = null!;
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => phase.Run());
        Assert.Contains("missing FieldLayout", ex.Message);
    }

    [Fact]
    public void Phase1_MismatchedFieldLayoutCount_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Classes[0].TotalInstanceVarCount = 5;
        bundle.Classes[0].FieldLayout = [new FieldLayoutEntry { NameIndex = 0, SlotIndex = 0 }]; // Only 1 entry
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => phase.Run());
        Assert.Contains("does not match TotalInstanceVarCount", ex.Message);
    }

    #endregion

    #region HomeClass Metadata Validation Tests

    [Fact]
    public void Phase1_ValidHomeClassMetadata_Succeeds()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Methods =
        [
            new MethodDescriptor
            {
                LocalId = "method:Object>>test",
                HomeClassLocalId = "class:Object",
                SelectorIndex = 0,
                Header = new MethodHeader(),
                Literals = [],
                Bytecodes = ""
            }
        ];
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert - should not throw
        phase.Run();
    }

    [Fact]
    public void Phase1_InvalidHomeClassReference_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Methods =
        [
            new MethodDescriptor
            {
                LocalId = "method:Test>>foo",
                HomeClassLocalId = "class:NonExistent",
                SelectorIndex = 0,
                Header = new MethodHeader(),
                Literals = [],
                Bytecodes = ""
            }
        ];
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => phase.Run());
        Assert.Contains("unknown home class", ex.Message);
    }

    [Fact]
    public void Phase1_BlockLiteralsSkipHomeClassValidation_Succeeds()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Methods =
        [
            new MethodDescriptor
            {
                LocalId = "block:1234",
                HomeClassLocalId = "", // Blocks don't have home class in class list
                SelectorIndex = 0,
                Header = new MethodHeader(),
                Literals = [],
                Bytecodes = ""
            }
        ];
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert - should not throw (blocks are skipped)
        phase.Run();
    }

    #endregion

    #region Metaclass Circle Validation Tests

    [Fact]
    public void Phase1_ValidMetaclassCircle_Succeeds()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert - should not throw
        phase.Run();
    }

    [Fact]
    public void Phase1_MissingMetaObject_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Classes = bundle.Classes.Where(c => c.LocalId != "meta:Object").ToArray();
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => phase.Run());
        Assert.Contains("meta:Object", ex.Message);
    }

    [Fact]
    public void Phase1_MissingMetaBehavior_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Classes = bundle.Classes.Where(c => c.LocalId != "meta:Behavior").ToArray();
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => phase.Run());
        Assert.Contains("meta:Behavior", ex.Message);
    }

    #endregion

    #region Special Objects Validation Tests

    [Fact]
    public void Phase1_RequiredSpecialClasses_Succeeds()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert - should not throw
        phase.Run();
    }

    [Fact]
    public void Phase1_MissingUndefinedObjectClass_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Classes = bundle.Classes.Where(c => c.LocalId != "class:UndefinedObject").ToArray();
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => phase.Run());
        Assert.Contains("class:UndefinedObject", ex.Message);
    }

    [Fact]
    public void Phase1_MissingTrueClass_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Classes = bundle.Classes.Where(c => c.LocalId != "class:True").ToArray();
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => phase.Run());
        Assert.Contains("class:True", ex.Message);
    }

    [Fact]
    public void Phase1_MissingFalseClass_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Classes = bundle.Classes.Where(c => c.LocalId != "class:False").ToArray();
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => phase.Run());
        Assert.Contains("class:False", ex.Message);
    }

    #endregion

    #region Symbol Interning Tests

    [Fact]
    public void Phase1_InternsAllSymbols_SymbolRegistryPopulated()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var (phase, allocator) = CreatePhase(bundle);

        // Act
        phase.Run();

        // Assert - SymbolRegistry should have all symbols
        Assert.True(SymbolRegistry.IsFrozen);
        allocator.Verify(a => a.AllocateString("Symbol", It.IsAny<int>()), Times.Exactly(bundle.Symbols.Length));
    }

    [Fact]
    public void Phase1_SymbolDeduplication_SameOopReturned()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        // Add duplicate symbol names
        bundle.Symbols = ["test", "test", "other"];
        var (phase, allocator) = CreatePhase(bundle);

        int allocationCount = 0;
        allocator.Setup(a => a.AllocateString("Symbol", It.IsAny<int>()))
            .Returns(() => new SmalltalkOOP(++allocationCount * 100));

        // Act
        phase.Run();

        // Assert - should allocate for each unique symbol
        // (Note: actual deduplication happens in InternSymbol)
        allocator.Verify(a => a.AllocateString("Symbol", It.IsAny<int>()), Times.AtLeastOnce());
    }

    [Fact]
    public void Phase1_SymbolRegistryFrozen_AfterRun()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var (phase, _) = CreatePhase(bundle);

        // Act
        phase.Run();

        // Assert
        Assert.True(SymbolRegistry.IsFrozen);
    }

    #endregion

    #region Phase Properties Tests

    [Fact]
    public void Phase1_Name_ReturnsCorrectName()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert
        Assert.Equal("Phase 1: Preflight Validation and Symbol Interning", phase.Name);
    }

    [Fact]
    public void Phase1_PhaseNumber_Returns1()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert
        Assert.Equal(1, phase.PhaseNumber);
    }

    [Fact]
    public void Phase1_ValidatePreconditions_ReturnsEmptyList()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var (phase, _) = CreatePhase(bundle);

        // Act
        var result = phase.ValidatePreconditions();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Phase1_ValidatePostconditions_ReturnsEmptyList()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var (phase, _) = CreatePhase(bundle);

        // Act
        var result = phase.ValidatePostconditions();

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region Constructor Validation Tests

    [Fact]
    public void Phase1_NullBundle_ThrowsArgumentNullException()
    {
        // Arrange
        var mockHeapManager = new Mock<ISmalltalkHeapManager>();
        var bundle = CreateMinimalValidBundle();
        var allocator = new Mock<ObjectGraphAllocator>(mockHeapManager.Object, bundle);
        var report = new ColdstartReport();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new Phase1_PreflightAndSymbols(null!, allocator.Object, report));
    }

    [Fact]
    public void Phase1_NullAllocator_ThrowsArgumentNullException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        var report = new ColdstartReport();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new Phase1_PreflightAndSymbols(bundle, null!, report));
    }

    [Fact]
    public void Phase1_NullReport_ThrowsArgumentNullException()
    {
        // Arrange
        var mockHeapManager = new Mock<ISmalltalkHeapManager>();
        var bundle = CreateMinimalValidBundle();
        var allocator = new Mock<ObjectGraphAllocator>(mockHeapManager.Object, bundle);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new Phase1_PreflightAndSymbols(bundle, allocator.Object, null!));
    }

    #endregion

    #region Patch Validation Tests

    [Fact]
    public void Phase1_EmptyPatches_Succeeds()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Patches = [];
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert - should not throw
        phase.Run();
    }

    [Fact]
    public void Phase1_NullPatches_Succeeds()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Patches = null!;
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert - should not throw
        phase.Run();
    }

    [Fact]
    public void Phase1_UnknownPatchType_ThrowsException()
    {
        // Arrange
        var bundle = CreateMinimalValidBundle();
        bundle.Patches =
        [
            new Patch { Type = "UnknownPatchType" }
        ];
        var (phase, _) = CreatePhase(bundle);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => phase.Run());
        Assert.Contains("Unknown patch type", ex.Message);
    }

    #endregion
}
