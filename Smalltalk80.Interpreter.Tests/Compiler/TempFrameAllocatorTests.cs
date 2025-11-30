using Smalltalk80.Compiler.Analysis;
using Xunit;

namespace Smalltalk80.Interpreter.Tests.Compiler;

/// <summary>
/// Comprehensive unit tests for TempFrameAllocator.
/// Tests the unified temp frame allocation system for Smalltalk-80 methods and blocks.
/// </summary>
[Trait("Category", "Compiler")]
public class TempFrameAllocatorTests
{
    #region EnterMethodScope Tests

    [Fact]
    public void EnterMethodScope_WithNoArgsNoTemps_AllocatesZeroSlots()
    {
        // Arrange
        var allocator = new TempFrameAllocator();

        // Act
        allocator.EnterMethodScope([], []);

        // Assert
        Assert.Equal(1, allocator.ScopeDepth);
        Assert.Equal(0, allocator.GetMaxFrameSize());
    }

    [Fact]
    public void EnterMethodScope_WithOneArg_AllocatesOneSlot()
    {
        // Arrange
        var allocator = new TempFrameAllocator();

        // Act
        allocator.EnterMethodScope(["arg1"], []);

        // Assert
        Assert.Equal(0, allocator.GetSlotIndex("arg1"));
        Assert.Equal(1, allocator.GetMaxFrameSize());
    }

    [Fact]
    public void EnterMethodScope_WithMultipleArgs_AllocatesSequentialSlots()
    {
        // Arrange
        var allocator = new TempFrameAllocator();

        // Act
        allocator.EnterMethodScope(["a", "b", "c"], []);

        // Assert
        Assert.Equal(0, allocator.GetSlotIndex("a"));
        Assert.Equal(1, allocator.GetSlotIndex("b"));
        Assert.Equal(2, allocator.GetSlotIndex("c"));
        Assert.Equal(3, allocator.GetMaxFrameSize());
    }

    [Fact]
    public void EnterMethodScope_WithOneTemp_AllocatesOneSlot()
    {
        // Arrange
        var allocator = new TempFrameAllocator();

        // Act
        allocator.EnterMethodScope([], ["temp1"]);

        // Assert
        Assert.Equal(0, allocator.GetSlotIndex("temp1"));
        Assert.Equal(1, allocator.GetMaxFrameSize());
    }

    [Fact]
    public void EnterMethodScope_WithMultipleTemps_AllocatesSequentialSlots()
    {
        // Arrange
        var allocator = new TempFrameAllocator();

        // Act
        allocator.EnterMethodScope([], ["x", "y", "z"]);

        // Assert
        Assert.Equal(0, allocator.GetSlotIndex("x"));
        Assert.Equal(1, allocator.GetSlotIndex("y"));
        Assert.Equal(2, allocator.GetSlotIndex("z"));
        Assert.Equal(3, allocator.GetMaxFrameSize());
    }

    [Fact]
    public void EnterMethodScope_ArgsBeforeTemps_AllocatesArgsFirst()
    {
        // Arrange
        var allocator = new TempFrameAllocator();

        // Act
        allocator.EnterMethodScope(["arg1", "arg2"], ["temp1", "temp2"]);

        // Assert
        Assert.Equal(0, allocator.GetSlotIndex("arg1"));
        Assert.Equal(1, allocator.GetSlotIndex("arg2"));
        Assert.Equal(2, allocator.GetSlotIndex("temp1"));
        Assert.Equal(3, allocator.GetSlotIndex("temp2"));
        Assert.Equal(4, allocator.GetMaxFrameSize());
    }

    [Fact]
    public void EnterMethodScope_ManyVariables_AllocatesCorrectly()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        var args = new List<string> { "a1", "a2", "a3", "a4", "a5" };
        var temps = new List<string> { "t1", "t2", "t3", "t4", "t5" };

        // Act
        allocator.EnterMethodScope(args, temps);

        // Assert
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(i, allocator.GetSlotIndex($"a{i + 1}"));
            Assert.Equal(i + 5, allocator.GetSlotIndex($"t{i + 1}"));
        }
        Assert.Equal(10, allocator.GetMaxFrameSize());
    }

    #endregion

    #region EnterBlockScope Tests

    [Fact]
    public void EnterBlockScope_Sequential_AllocatesAfterMethod()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg1"], ["temp1"]);

        // Act
        allocator.EnterBlockScope(["blockArg"], ["blockTemp"], isNested: false);

        // Assert
        Assert.Equal(2, allocator.GetSlotIndex("blockArg"));
        Assert.Equal(3, allocator.GetSlotIndex("blockTemp"));
        Assert.Equal(4, allocator.GetMaxFrameSize());
        Assert.Equal(2, allocator.ScopeDepth);
    }

    [Fact]
    public void EnterBlockScope_Nested_DoesNotReuseSlots()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg"], []);
        allocator.EnterBlockScope(["outerBlockArg"], [], isNested: false);

        // Act
        allocator.EnterBlockScope(["innerBlockArg"], [], isNested: true);

        // Assert
        Assert.Equal(1, allocator.GetSlotIndex("outerBlockArg"));
        Assert.Equal(2, allocator.GetSlotIndex("innerBlockArg"));
        Assert.Equal(3, allocator.GetMaxFrameSize());
        Assert.Equal(3, allocator.ScopeDepth);
    }

    [Fact]
    public void EnterBlockScope_SequentialBlocks_ReuseSlots()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg"], []);

        // Act - First sequential block
        allocator.EnterBlockScope(["block1Arg"], ["block1Temp"], isNested: false);
        Assert.Equal(1, allocator.GetSlotIndex("block1Arg"));
        Assert.Equal(2, allocator.GetSlotIndex("block1Temp"));
        allocator.ExitScope();

        // Act - Second sequential block (should reuse slots)
        allocator.EnterBlockScope(["block2Arg"], ["block2Temp"], isNested: false);

        // Assert
        Assert.Equal(1, allocator.GetSlotIndex("block2Arg"));
        Assert.Equal(2, allocator.GetSlotIndex("block2Temp"));
        Assert.Equal(3, allocator.GetMaxFrameSize());
    }

    [Fact]
    public void EnterBlockScope_WithZeroArgsZeroTemps_NoSlotsAllocated()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg"], []);

        // Act
        allocator.EnterBlockScope([], [], isNested: false);

        // Assert
        Assert.Equal(2, allocator.ScopeDepth);
        Assert.Equal(1, allocator.GetMaxFrameSize());
    }

    [Fact]
    public void EnterBlockScope_BlockArgsAllocatedInUnifiedFrame()
    {
        // Arrange - This is the UNIFIED TEMP FRAME architecture
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["methodArg"], ["methodTemp"]);

        // Act
        allocator.EnterBlockScope(["blockArg1", "blockArg2"], [], isNested: false);

        // Assert - Block args are in unified frame after method vars
        Assert.Equal(0, allocator.GetSlotIndex("methodArg"));
        Assert.Equal(1, allocator.GetSlotIndex("methodTemp"));
        Assert.Equal(2, allocator.GetSlotIndex("blockArg1"));
        Assert.Equal(3, allocator.GetSlotIndex("blockArg2"));
    }

    #endregion

    // Note: GetCurrentBlockArgStartIndex tests removed - method no longer exists
    // Block arguments are now stored via STORE_TEMP bytecodes at block body start (Blue Book compliant)

    #region ExitScope Tests

    [Fact]
    public void ExitScope_SequentialBlock_ReusesSlots()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg"], []);
        allocator.EnterBlockScope(["blockArg"], [], isNested: false);

        // Act
        allocator.ExitScope();

        // Assert - Variable no longer accessible
        Assert.False(allocator.HasVariable("blockArg"));
        Assert.Equal(1, allocator.ScopeDepth);
    }

    [Fact]
    public void ExitScope_NestedBlock_DoesNotReuseSlots()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg"], []);
        allocator.EnterBlockScope(["outer"], [], isNested: false);
        allocator.EnterBlockScope(["inner"], [], isNested: true);

        // Act
        allocator.ExitScope();

        // Assert - Outer block variable still accessible
        Assert.True(allocator.HasVariable("outer"));
        Assert.False(allocator.HasVariable("inner"));
    }

    [Fact]
    public void ExitScope_NoActiveScope_ThrowsException()
    {
        // Arrange
        var allocator = new TempFrameAllocator();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => allocator.ExitScope());
    }

    [Fact]
    public void ExitScope_MethodScope_CanExit()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg"], []);

        // Act
        allocator.ExitScope();

        // Assert
        Assert.Equal(0, allocator.ScopeDepth);
    }

    #endregion

    #region GetSlotIndex and TryGetSlotIndex Tests

    [Fact]
    public void GetSlotIndex_ExistingVariable_ReturnsCorrectIndex()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["a", "b", "c"], []);

        // Act & Assert
        Assert.Equal(0, allocator.GetSlotIndex("a"));
        Assert.Equal(1, allocator.GetSlotIndex("b"));
        Assert.Equal(2, allocator.GetSlotIndex("c"));
    }

    [Fact]
    public void GetSlotIndex_NonExistingVariable_ThrowsException()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg"], []);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => allocator.GetSlotIndex("nonexistent"));
        Assert.Contains("nonexistent", ex.Message);
    }

    [Fact]
    public void TryGetSlotIndex_ExistingVariable_ReturnsTrueAndCorrectIndex()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg"], ["temp"]);

        // Act
        bool found = allocator.TryGetSlotIndex("temp", out int index);

        // Assert
        Assert.True(found);
        Assert.Equal(1, index);
    }

    [Fact]
    public void TryGetSlotIndex_NonExistingVariable_ReturnsFalse()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg"], []);

        // Act
        bool found = allocator.TryGetSlotIndex("nonexistent", out int index);

        // Assert
        Assert.False(found);
    }

    #endregion

    #region GetMaxFrameSize Tests

    [Fact]
    public void GetMaxFrameSize_EmptyAllocator_ReturnsZero()
    {
        // Arrange
        var allocator = new TempFrameAllocator();

        // Act
        int maxSize = allocator.GetMaxFrameSize();

        // Assert
        Assert.Equal(0, maxSize);
    }

    [Fact]
    public void GetMaxFrameSize_AfterSlotReuse_ReturnsHighestEverAllocated()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg"], []);

        // First block uses slots 1-3
        allocator.EnterBlockScope(["a", "b", "c"], [], isNested: false);
        allocator.ExitScope();

        // Second block reuses slot 1
        allocator.EnterBlockScope(["x"], [], isNested: false);

        // Act
        int maxSize = allocator.GetMaxFrameSize();

        // Assert - Should return 4 (highest was slot 3 = index 3, so count = 4)
        Assert.Equal(4, maxSize);
    }

    [Fact]
    public void GetMaxFrameSize_DeeplyNestedBlocks_TracksMaximum()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["a"], []);          // slot 0
        allocator.EnterBlockScope(["b"], [], false);    // slot 1
        allocator.EnterBlockScope(["c"], [], true);     // slot 2
        allocator.EnterBlockScope(["d"], [], true);     // slot 3

        // Act
        int maxSize = allocator.GetMaxFrameSize();

        // Assert
        Assert.Equal(4, maxSize);
    }

    #endregion

    #region HasVariable Tests

    [Fact]
    public void HasVariable_ExistingVariable_ReturnsTrue()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg"], ["temp"]);

        // Act & Assert
        Assert.True(allocator.HasVariable("arg"));
        Assert.True(allocator.HasVariable("temp"));
    }

    [Fact]
    public void HasVariable_NonExistingVariable_ReturnsFalse()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg"], []);

        // Act & Assert
        Assert.False(allocator.HasVariable("nonexistent"));
    }

    [Fact]
    public void HasVariable_AfterExitSequentialScope_ReturnsFalse()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope([], []);
        allocator.EnterBlockScope(["blockVar"], [], isNested: false);
        allocator.ExitScope();

        // Act & Assert
        Assert.False(allocator.HasVariable("blockVar"));
    }

    #endregion

    #region ScopeDepth Tests

    [Fact]
    public void ScopeDepth_Initial_IsZero()
    {
        // Arrange
        var allocator = new TempFrameAllocator();

        // Assert
        Assert.Equal(0, allocator.ScopeDepth);
    }

    [Fact]
    public void ScopeDepth_AfterMethodScope_IsOne()
    {
        // Arrange
        var allocator = new TempFrameAllocator();

        // Act
        allocator.EnterMethodScope([], []);

        // Assert
        Assert.Equal(1, allocator.ScopeDepth);
    }

    [Fact]
    public void ScopeDepth_AfterBlockScopes_IncreasesCorrectly()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope([], []);

        // Act
        allocator.EnterBlockScope([], [], false);
        Assert.Equal(2, allocator.ScopeDepth);

        allocator.EnterBlockScope([], [], true);
        Assert.Equal(3, allocator.ScopeDepth);
    }

    [Fact]
    public void ScopeDepth_AfterExitScope_DecreasesCorrectly()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope([], []);
        allocator.EnterBlockScope([], [], false);

        // Act
        allocator.ExitScope();

        // Assert
        Assert.Equal(1, allocator.ScopeDepth);
    }

    #endregion

    #region GetAllAllocatedVariables Tests

    [Fact]
    public void GetAllAllocatedVariables_ReturnsAllVariables()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg1", "arg2"], ["temp1"]);
        allocator.EnterBlockScope(["blockArg"], [], isNested: false);

        // Act
        var allVars = allocator.GetAllAllocatedVariables();

        // Assert
        Assert.Equal(4, allVars.Count);
        Assert.Equal(0, allVars["arg1"]);
        Assert.Equal(1, allVars["arg2"]);
        Assert.Equal(2, allVars["temp1"]);
        Assert.Equal(3, allVars["blockArg"]);
    }

    [Fact]
    public void GetAllAllocatedVariables_ReturnsCopy()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg"], []);

        // Act
        var allVars = allocator.GetAllAllocatedVariables();
        allVars["newVar"] = 100;  // Modify the copy

        // Assert - Original should not be affected
        Assert.False(allocator.HasVariable("newVar"));
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void DeeplyNestedBlocks_FiveLevels_AllocatesCorrectly()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["m"], []);      // slot 0

        // Act - Nest 5 blocks
        for (int i = 1; i <= 5; i++)
        {
            allocator.EnterBlockScope([$"b{i}"], [], isNested: true);
        }

        // Assert
        Assert.Equal(6, allocator.ScopeDepth);
        Assert.Equal(6, allocator.GetMaxFrameSize());
        for (int i = 1; i <= 5; i++)
        {
            Assert.Equal(i, allocator.GetSlotIndex($"b{i}"));
        }
    }

    [Fact]
    public void MixedSequentialAndNestedBlocks_AllocatesCorrectly()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg"], []);           // slot 0

        // Act - First sequential block with nested
        allocator.EnterBlockScope(["a"], [], false);       // slot 1
        allocator.EnterBlockScope(["b"], [], true);        // slot 2
        allocator.ExitScope();                             // exit b
        allocator.ExitScope();                             // exit a

        // Second sequential block (should reuse)
        allocator.EnterBlockScope(["c"], [], false);       // slot 1 (reused)

        // Assert
        Assert.Equal(1, allocator.GetSlotIndex("c"));
        Assert.Equal(3, allocator.GetMaxFrameSize());
    }

    [Fact]
    public void BlockWithManyVariables_TenArgsAndTemps_AllocatesCorrectly()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope([], []);

        var blockArgs = Enumerable.Range(1, 10).Select(i => $"arg{i}").ToList();
        var blockTemps = Enumerable.Range(1, 10).Select(i => $"temp{i}").ToList();

        // Act
        allocator.EnterBlockScope(blockArgs, blockTemps, isNested: false);

        // Assert
        Assert.Equal(20, allocator.GetMaxFrameSize());
        Assert.Equal(0, allocator.GetSlotIndex("arg1"));
        Assert.Equal(9, allocator.GetSlotIndex("arg10"));
        Assert.Equal(10, allocator.GetSlotIndex("temp1"));
        Assert.Equal(19, allocator.GetSlotIndex("temp10"));
    }

    [Fact]
    public void SlotReuse_MultipleSequentialBlocks_ReusesCorrectly()
    {
        // Arrange
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope([], []);

        // Act - Three sequential blocks
        allocator.EnterBlockScope(["a1", "a2", "a3"], [], false);
        allocator.ExitScope();

        allocator.EnterBlockScope(["b1", "b2"], [], false);
        allocator.ExitScope();

        allocator.EnterBlockScope(["c1"], [], false);

        // Assert - All blocks start at slot 0
        Assert.Equal(0, allocator.GetSlotIndex("c1"));
        Assert.Equal(3, allocator.GetMaxFrameSize()); // Highest was 3 slots
    }

    #endregion

    #region Blue Book Block Parameter Slot Reuse Tests

    [Fact]
    public void EnterBlockScope_BlockArgMatchesMethodTemp_ReusesSlot()
    {
        // Arrange - Blue Book scenario: block param shadows method temp
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope([], ["foo"]);  // method temp foo at slot 0

        // Act - block with same-named param
        allocator.EnterBlockScope(["foo"], [], isNested: false);

        // Assert - should reuse slot 0, not allocate new slot
        Assert.Equal(0, allocator.GetSlotIndex("foo"));
        Assert.Equal(1, allocator.GetMaxFrameSize());  // Only 1 slot used
    }

    [Fact]
    public void EnterBlockScope_GrandparentTempReuse_ReusesSlot()
    {
        // Arrange - User's scenario: block arg matches temp from grandparent scope
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope([], ["foo"]);          // slot 0: method temp foo
        allocator.EnterBlockScope(["bar"], [], false);    // slot 1: outer block param bar

        // Act - nested inner block with param matching method temp
        allocator.EnterBlockScope(["foo"], [], true);     // should reuse slot 0

        // Assert
        Assert.Equal(0, allocator.GetSlotIndex("foo"));   // reused grandparent slot
        Assert.Equal(1, allocator.GetSlotIndex("bar"));   // outer block param
        Assert.Equal(2, allocator.GetMaxFrameSize());     // slots 0 and 1
    }

    [Fact]
    public void EnterBlockScope_GreatGrandparentTempReuse_ReusesSlot()
    {
        // Arrange - deeply nested scenario
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope([], ["foo"]);          // slot 0
        allocator.EnterBlockScope(["a"], [], false);      // slot 1
        allocator.EnterBlockScope(["b"], [], true);       // slot 2

        // Act - 3 levels deep, param matches method temp
        allocator.EnterBlockScope(["foo"], [], true);     // should reuse slot 0

        // Assert
        Assert.Equal(0, allocator.GetSlotIndex("foo"));
    }

    [Fact]
    public void ExitScope_SequentialBlockReusedSlot_DoesNotRemoveOuterTemp()
    {
        // Arrange - potential bug: sequential block exit should not remove reused outer var
        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope([], ["foo"]);          // slot 0
        allocator.EnterBlockScope(["foo"], [], false);    // reuses slot 0

        // Act - exit the sequential block
        allocator.ExitScope();

        // Assert - method temp 'foo' should STILL be accessible
        Assert.True(allocator.HasVariable("foo"), "Method temp 'foo' should still be accessible after block exit");
        Assert.Equal(0, allocator.GetSlotIndex("foo"));
    }

    #endregion
}
