using NUnit.Framework;
using Smalltalk80.Compiler.Analysis;

namespace Smalltalk80.Compiler.Tests;

/// <summary>
/// Unit tests for the TempFrameAllocator class.
/// Tests unified temp frame allocation to ensure refactoring doesn't break slot assignment.
/// </summary>
[TestFixture]
public class TempFrameAllocatorTests
{
    private TempFrameAllocator _allocator = null!;

    [SetUp]
    public void Setup()
    {
        _allocator = new TempFrameAllocator();
    }

    #region Method Scope Allocation

    [Test]
    public void EnterMethodScope_AllocatesArgsStartingAtZero()
    {
        _allocator.EnterMethodScope(["arg1", "arg2", "arg3"], []);

        Assert.That(_allocator.GetSlotIndex("arg1"), Is.EqualTo(0));
        Assert.That(_allocator.GetSlotIndex("arg2"), Is.EqualTo(1));
        Assert.That(_allocator.GetSlotIndex("arg3"), Is.EqualTo(2));
    }

    [Test]
    public void EnterMethodScope_AllocatesTempsAfterArgs()
    {
        _allocator.EnterMethodScope(["arg1", "arg2"], ["temp1", "temp2"]);

        Assert.That(_allocator.GetSlotIndex("arg1"), Is.EqualTo(0));
        Assert.That(_allocator.GetSlotIndex("arg2"), Is.EqualTo(1));
        Assert.That(_allocator.GetSlotIndex("temp1"), Is.EqualTo(2));
        Assert.That(_allocator.GetSlotIndex("temp2"), Is.EqualTo(3));
    }

    [Test]
    public void EnterMethodScope_NoArgsNoTemps_MaxFrameSizeIsZero()
    {
        _allocator.EnterMethodScope([], []);

        Assert.That(_allocator.GetMaxFrameSize(), Is.EqualTo(0));
    }

    [Test]
    public void EnterMethodScope_SetsCorrectMaxFrameSize()
    {
        _allocator.EnterMethodScope(["arg1", "arg2"], ["temp1", "temp2", "temp3"]);

        Assert.That(_allocator.GetMaxFrameSize(), Is.EqualTo(5)); // 2 args + 3 temps
    }

    [Test]
    public void EnterMethodScope_SetsScopeDepthToOne()
    {
        _allocator.EnterMethodScope([], []);

        Assert.That(_allocator.ScopeDepth, Is.EqualTo(1));
    }

    #endregion

    #region Block Scope Allocation

    [Test]
    public void EnterBlockScope_AllocatesBlockArgsInUnifiedFrame()
    {
        _allocator.EnterMethodScope(["mArg"], ["mTemp"]);
        _allocator.EnterBlockScope(["bArg1", "bArg2"], [], false);

        // Block args should be allocated after method args and temps
        Assert.That(_allocator.GetSlotIndex("bArg1"), Is.EqualTo(2));
        Assert.That(_allocator.GetSlotIndex("bArg2"), Is.EqualTo(3));
    }

    [Test]
    public void EnterBlockScope_AllocatesBlockTempsAfterBlockArgs()
    {
        _allocator.EnterMethodScope([], []);
        _allocator.EnterBlockScope(["bArg"], ["bTemp"], false);

        Assert.That(_allocator.GetSlotIndex("bArg"), Is.EqualTo(0));
        Assert.That(_allocator.GetSlotIndex("bTemp"), Is.EqualTo(1));
    }

    [Test]
    public void EnterBlockScope_ReusesSlotsForMatchingNames()
    {
        // Blue Book semantics: block arg with same name as outer temp shares slot
        _allocator.EnterMethodScope([], ["shared"]);
        _allocator.EnterBlockScope(["shared"], [], false);

        // Block arg "shared" should reuse existing slot
        Assert.That(_allocator.GetSlotIndex("shared"), Is.EqualTo(0));
        Assert.That(_allocator.GetMaxFrameSize(), Is.EqualTo(1)); // Only 1 slot used
    }

    [Test]
    public void EnterBlockScope_NestedBlock_IncreasesScopeDepth()
    {
        _allocator.EnterMethodScope([], []);
        Assert.That(_allocator.ScopeDepth, Is.EqualTo(1));

        _allocator.EnterBlockScope([], [], false);
        Assert.That(_allocator.ScopeDepth, Is.EqualTo(2));

        _allocator.EnterBlockScope([], [], true);
        Assert.That(_allocator.ScopeDepth, Is.EqualTo(3));
    }

    #endregion

    #region Scope Exit and Slot Reuse

    [Test]
    public void ExitScope_SequentialBlock_ReusesSlotsForNextBlock()
    {
        _allocator.EnterMethodScope([], []);

        _allocator.EnterBlockScope(["a"], ["t1"], false);
        Assert.That(_allocator.GetSlotIndex("a"), Is.EqualTo(0));
        Assert.That(_allocator.GetSlotIndex("t1"), Is.EqualTo(1));
        _allocator.ExitScope();

        // Next sequential block should reuse slots 0, 1
        _allocator.EnterBlockScope(["b"], ["t2"], false);
        Assert.That(_allocator.GetSlotIndex("b"), Is.EqualTo(0));
        Assert.That(_allocator.GetSlotIndex("t2"), Is.EqualTo(1));
    }

    [Test]
    public void ExitScope_NestedBlock_DoesNotReuseSlots()
    {
        _allocator.EnterMethodScope([], []);

        _allocator.EnterBlockScope(["a"], [], false);
        _allocator.EnterBlockScope(["b"], [], true); // Nested
        Assert.That(_allocator.GetSlotIndex("b"), Is.EqualTo(1));
        _allocator.ExitScope(); // Exit nested

        // Non-nested should NOT have reused slots because nested doesn't trigger reuse
        Assert.That(_allocator.GetSlotIndex("a"), Is.EqualTo(0));
    }

    [Test]
    public void ExitScope_DecreasesScopeDepth()
    {
        _allocator.EnterMethodScope([], []);
        _allocator.EnterBlockScope([], [], false);
        Assert.That(_allocator.ScopeDepth, Is.EqualTo(2));

        _allocator.ExitScope();
        Assert.That(_allocator.ScopeDepth, Is.EqualTo(1));
    }

    [Test]
    public void ExitScope_NoActiveScope_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _allocator.ExitScope());
    }

    #endregion

    #region GetSlotIndex and TryGetSlotIndex

    [Test]
    public void GetSlotIndex_ValidVariable_ReturnsIndex()
    {
        _allocator.EnterMethodScope(["arg"], []);

        Assert.That(_allocator.GetSlotIndex("arg"), Is.EqualTo(0));
    }

    [Test]
    public void GetSlotIndex_InvalidVariable_Throws()
    {
        _allocator.EnterMethodScope([], []);

        Assert.Throws<InvalidOperationException>(() => _allocator.GetSlotIndex("nonexistent"));
    }

    [Test]
    public void TryGetSlotIndex_ValidVariable_ReturnsTrue()
    {
        _allocator.EnterMethodScope(["arg"], []);

        bool found = _allocator.TryGetSlotIndex("arg", out int index);

        Assert.That(found, Is.True);
        Assert.That(index, Is.EqualTo(0));
    }

    [Test]
    public void TryGetSlotIndex_InvalidVariable_ReturnsFalse()
    {
        _allocator.EnterMethodScope([], []);

        bool found = _allocator.TryGetSlotIndex("nonexistent", out int index);

        Assert.That(found, Is.False);
    }

    #endregion

    #region HasVariable

    [Test]
    public void HasVariable_AllocatedVariable_ReturnsTrue()
    {
        _allocator.EnterMethodScope(["arg"], ["temp"]);

        Assert.That(_allocator.HasVariable("arg"), Is.True);
        Assert.That(_allocator.HasVariable("temp"), Is.True);
    }

    [Test]
    public void HasVariable_NotAllocated_ReturnsFalse()
    {
        _allocator.EnterMethodScope([], []);

        Assert.That(_allocator.HasVariable("nonexistent"), Is.False);
    }

    [Test]
    public void HasVariable_AfterExitScope_ReturnsFalseForFreedVars()
    {
        _allocator.EnterMethodScope([], []);
        _allocator.EnterBlockScope(["blockArg"], [], false);
        Assert.That(_allocator.HasVariable("blockArg"), Is.True);

        _allocator.ExitScope();
        Assert.That(_allocator.HasVariable("blockArg"), Is.False);
    }

    #endregion

    #region GetMaxFrameSize

    [Test]
    public void GetMaxFrameSize_TracksHighestSlotEverUsed()
    {
        _allocator.EnterMethodScope([], []);

        // First block uses slots 0, 1
        _allocator.EnterBlockScope(["a", "b"], [], false);
        _allocator.ExitScope();

        // Second block reuses slots 0, 1 but highest is still 1
        _allocator.EnterBlockScope(["c", "d"], [], false);
        _allocator.ExitScope();

        Assert.That(_allocator.GetMaxFrameSize(), Is.EqualTo(2)); // Slots 0-1 = 2 slots
    }

    [Test]
    public void GetMaxFrameSize_GrowsWithNestedBlocks()
    {
        _allocator.EnterMethodScope(["mArg"], ["mTemp"]); // Slots 0, 1

        _allocator.EnterBlockScope(["b1Arg"], [], false); // Slot 2
        _allocator.EnterBlockScope(["b2Arg"], [], true);  // Slot 3 (nested)

        Assert.That(_allocator.GetMaxFrameSize(), Is.EqualTo(4)); // Slots 0-3
    }

    #endregion

    #region GetAllAllocatedVariables

    [Test]
    public void GetAllAllocatedVariables_ReturnsSnapshot()
    {
        _allocator.EnterMethodScope(["arg1", "arg2"], ["temp"]);

        var snapshot = _allocator.GetAllAllocatedVariables();

        Assert.That(snapshot.Count, Is.EqualTo(3));
        Assert.That(snapshot["arg1"], Is.EqualTo(0));
        Assert.That(snapshot["arg2"], Is.EqualTo(1));
        Assert.That(snapshot["temp"], Is.EqualTo(2));
    }

    [Test]
    public void GetAllAllocatedVariables_ModifyingSnapshotDoesNotAffectAllocator()
    {
        _allocator.EnterMethodScope(["arg"], []);
        var snapshot = _allocator.GetAllAllocatedVariables();

        snapshot["newVar"] = 99;

        Assert.That(_allocator.HasVariable("newVar"), Is.False);
    }

    #endregion

    #region Complex Scenarios

    [Test]
    public void ComplexScenario_MultipleNestedAndSequentialBlocks()
    {
        _allocator.EnterMethodScope(["mArg"], ["mTemp"]); // Slots 0, 1

        // First block with nested block
        _allocator.EnterBlockScope(["b1"], [], false);    // Slot 2
        Assert.That(_allocator.GetSlotIndex("b1"), Is.EqualTo(2));

        _allocator.EnterBlockScope(["b2"], [], true);     // Slot 3 (nested)
        Assert.That(_allocator.GetSlotIndex("b2"), Is.EqualTo(3));
        _allocator.ExitScope(); // Exit nested

        _allocator.ExitScope(); // Exit first block (sequential - can reuse)

        // Second sequential block
        _allocator.EnterBlockScope(["b3"], [], false);    // Reuses slot 2
        Assert.That(_allocator.GetSlotIndex("b3"), Is.EqualTo(2));
        _allocator.ExitScope();

        // Max frame size should reflect highest ever allocated (0-3 = 4)
        Assert.That(_allocator.GetMaxFrameSize(), Is.EqualTo(4));
    }

    [Test]
    public void Scenario_RunArrayCopyFromTo()
    {
        // Simulates RunArray>>copyFrom:to: pattern where block params share slots with method temps
        _allocator.EnterMethodScope(["start", "stop"], ["run", "offset", "value"]);
        // run=2, offset=3, value=4

        // [:r :o :v | ...] - block params share names with method temps
        _allocator.EnterBlockScope(["r", "o", "v"], [], false);

        // These are new variables, not sharing (different names)
        Assert.That(_allocator.GetSlotIndex("r"), Is.EqualTo(5));
        Assert.That(_allocator.GetSlotIndex("o"), Is.EqualTo(6));
        Assert.That(_allocator.GetSlotIndex("v"), Is.EqualTo(7));
    }

    [Test]
    public void Scenario_BlockParamSharesWithOuterTemp()
    {
        // Blue Book semantics: [:value | value := 42] shares slot with outer "value"
        _allocator.EnterMethodScope([], ["value"]);  // value at slot 0

        _allocator.EnterBlockScope(["value"], [], false); // Should reuse slot 0

        Assert.That(_allocator.GetSlotIndex("value"), Is.EqualTo(0));
        Assert.That(_allocator.GetMaxFrameSize(), Is.EqualTo(1)); // Only 1 slot used
    }

    #endregion
}
