using Smalltalk80.Compiler;
using Smalltalk80.Compiler.Analysis;
using Xunit;

namespace Smalltalk80.Interpreter.Tests.Compiler;

/// <summary>
/// Comprehensive unit tests for VariableResolver.
/// Tests the 9-step variable resolution priority system for Smalltalk-80.
/// </summary>
public class VariableResolverTests
{
    private readonly VariableResolver _resolver = new();

    #region Helper Methods

    /// <summary>
    /// Creates a basic method-level compilation environment.
    /// </summary>
    private static CompilationEnvironment CreateMethodEnvironment()
    {
        return new CompilationEnvironment
        {
            Type = ScopeType.Method,
            ScopeDepth = 0,
            Parent = null
        };
    }

    /// <summary>
    /// Creates a block-level compilation environment with a parent.
    /// </summary>
    private static CompilationEnvironment CreateBlockEnvironment(CompilationEnvironment parent)
    {
        return new CompilationEnvironment
        {
            Type = ScopeType.Block,
            ScopeDepth = parent.ScopeDepth + 1,
            Parent = parent
        };
    }

    #endregion

    #region Pseudo-Variable Resolution Tests (Step 1)

    [Theory]
    [InlineData("self")]
    [InlineData("super")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("nil")]
    [InlineData("thisContext")]
    public void ResolveVariable_PseudoVariables_ReturnsCorrectType(string varName)
    {
        // Arrange
        var env = CreateMethodEnvironment();

        // Act
        var result = _resolver.ResolveVariable(varName, env);

        // Assert
        Assert.Equal(VariableResolver.VariableType.PseudoVariable, result.Type);
        Assert.Equal(varName, result.Name);
        Assert.False(result.IsAssignable);
    }

    [Fact]
    public void ResolveVariable_Self_CannotBeAssigned()
    {
        // Arrange
        var env = CreateMethodEnvironment();

        // Act
        var result = _resolver.ResolveVariable("self", env);

        // Assert
        Assert.False(result.IsAssignable);
    }

    [Fact]
    public void ResolveVariable_Nil_CannotBeAssigned()
    {
        // Arrange
        var env = CreateMethodEnvironment();

        // Act
        var result = _resolver.ResolveVariable("nil", env);

        // Assert
        Assert.False(result.IsAssignable);
    }

    [Fact]
    public void ResolveVariable_PseudoVariables_CannotBeShadowed()
    {
        // Arrange - Try to shadow 'self' with a method argument
        var env = CreateMethodEnvironment();
        env.LocalArgumentIndices["self"] = 0;
        env.ArgumentCount = 1;

        // Act - Pseudo-variable takes priority
        var result = _resolver.ResolveVariable("self", env);

        // Assert
        Assert.Equal(VariableResolver.VariableType.PseudoVariable, result.Type);
    }

    #endregion

    #region Method Argument Resolution Tests (Step 5)

    [Fact]
    public void ResolveVariable_MethodArgument_ReturnsMethodArgumentType()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.LocalArgumentIndices["arg1"] = 0;
        env.ArgumentCount = 1;

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg1"], []);

        // Act
        var result = _resolver.ResolveVariable("arg1", env, allocator);

        // Assert
        Assert.Equal(VariableResolver.VariableType.MethodArgument, result.Type);
        Assert.Equal(0, result.Index);
        Assert.False(result.IsAssignable);
    }

    [Fact]
    public void ResolveVariable_MultipleMethodArguments_ReturnsCorrectIndices()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.LocalArgumentIndices["a"] = 0;
        env.LocalArgumentIndices["b"] = 1;
        env.LocalArgumentIndices["c"] = 2;
        env.ArgumentCount = 3;

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["a", "b", "c"], []);

        // Act & Assert
        Assert.Equal(0, _resolver.ResolveVariable("a", env, allocator).Index);
        Assert.Equal(1, _resolver.ResolveVariable("b", env, allocator).Index);
        Assert.Equal(2, _resolver.ResolveVariable("c", env, allocator).Index);
    }

    [Fact]
    public void ResolveVariable_MethodArgument_IsNotAssignable()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.LocalArgumentIndices["arg"] = 0;
        env.ArgumentCount = 1;

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg"], []);

        // Act
        var result = _resolver.ResolveVariable("arg", env, allocator);

        // Assert
        Assert.False(result.IsAssignable);
    }

    #endregion

    #region Method Temporary Resolution Tests (Step 6)

    [Fact]
    public void ResolveVariable_MethodTemporary_ReturnsMethodTemporaryType()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.LocalTemporaryIndices["temp1"] = 0;
        env.TemporariesAllocatedInFrame = 1;

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope([], ["temp1"]);

        // Act
        var result = _resolver.ResolveVariable("temp1", env, allocator);

        // Assert
        Assert.Equal(VariableResolver.VariableType.MethodTemporary, result.Type);
        Assert.Equal(0, result.Index);
        Assert.True(result.IsAssignable);
    }

    [Fact]
    public void ResolveVariable_MethodTemporary_AfterArgs_HasCorrectIndex()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.LocalArgumentIndices["arg1"] = 0;
        env.LocalArgumentIndices["arg2"] = 1;
        env.ArgumentCount = 2;
        env.LocalTemporaryIndices["temp1"] = 0;
        env.TemporariesAllocatedInFrame = 1;

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg1", "arg2"], ["temp1"]);

        // Act
        var result = _resolver.ResolveVariable("temp1", env, allocator);

        // Assert - temp1 is at slot 2 (after 2 args)
        Assert.Equal(2, result.Index);
    }

    [Fact]
    public void ResolveVariable_MethodTemporary_IsAssignable()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.LocalTemporaryIndices["temp"] = 0;
        env.TemporariesAllocatedInFrame = 1;

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope([], ["temp"]);

        // Act
        var result = _resolver.ResolveVariable("temp", env, allocator);

        // Assert
        Assert.True(result.IsAssignable);
    }

    #endregion

    #region Block Argument/Temporary Resolution Tests (Steps 2-4)

    [Fact]
    public void ResolveVariable_BlockArgument_ReturnsBlockTemporaryType()
    {
        // Arrange - UNIFIED TEMP FRAME: block args are BlockTemporary
        var methodEnv = CreateMethodEnvironment();
        var blockEnv = CreateBlockEnvironment(methodEnv);
        blockEnv.LocalArgumentIndices["blockArg"] = 0;
        blockEnv.ArgumentCount = 1;

        // Act
        var result = _resolver.ResolveVariable("blockArg", blockEnv);

        // Assert
        Assert.Equal(VariableResolver.VariableType.BlockTemporary, result.Type);
    }

    [Fact]
    public void ResolveVariable_BlockArgument_IsNotAssignable()
    {
        // Arrange - Block arguments are read-only like method arguments
        var methodEnv = CreateMethodEnvironment();
        var blockEnv = CreateBlockEnvironment(methodEnv);
        blockEnv.LocalArgumentIndices["blockArg"] = 0;
        blockEnv.ArgumentCount = 1;

        // Act
        var result = _resolver.ResolveVariable("blockArg", blockEnv);

        // Assert
        Assert.False(result.IsAssignable);
    }

    [Fact]
    public void ResolveVariable_BlockTemporary_ReturnsBlockTemporaryType()
    {
        // Arrange
        var methodEnv = CreateMethodEnvironment();
        var blockEnv = CreateBlockEnvironment(methodEnv);
        blockEnv.LocalTemporaryIndices["blockTemp"] = 0;
        blockEnv.TemporariesAllocatedInFrame = 1;

        // Act
        var result = _resolver.ResolveVariable("blockTemp", blockEnv);

        // Assert
        Assert.Equal(VariableResolver.VariableType.BlockTemporary, result.Type);
        Assert.True(result.IsAssignable);
    }

    [Fact]
    public void ResolveVariable_BlockTemporary_HasCorrectIndex()
    {
        // Arrange
        var methodEnv = CreateMethodEnvironment();
        var blockEnv = CreateBlockEnvironment(methodEnv);
        blockEnv.LocalArgumentIndices["blockArg"] = 0;
        blockEnv.ArgumentCount = 1;
        blockEnv.LocalTemporaryIndices["blockTemp"] = 0;
        blockEnv.TemporariesAllocatedInFrame = 1;

        // Act
        var result = _resolver.ResolveVariable("blockTemp", blockEnv);

        // Assert - temp index = argCount + tempIndex = 1 + 0 = 1
        Assert.Equal(1, result.Index);
    }

    [Fact]
    public void ResolveVariable_BlockAccessMethodArg_WithAllocator_ReturnsCorrectIndex()
    {
        // Arrange
        var methodEnv = CreateMethodEnvironment();
        methodEnv.LocalArgumentIndices["methodArg"] = 0;
        methodEnv.ArgumentCount = 1;

        var blockEnv = CreateBlockEnvironment(methodEnv);
        blockEnv.LocalArgumentIndices["blockArg"] = 0;
        blockEnv.ArgumentCount = 1;

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["methodArg"], []);
        allocator.EnterBlockScope(["blockArg"], [], isNested: false);

        // Act - Block accessing method's argument
        var result = _resolver.ResolveVariable("methodArg", blockEnv, allocator);

        // Assert - Uses unified frame index
        Assert.Equal(VariableResolver.VariableType.MethodArgument, result.Type);
        Assert.Equal(0, result.Index);
    }

    #endregion

    #region Parent Variable Resolution Tests (Steps 3-4)

    [Fact]
    public void ResolveVariable_ParentBlockVariable_ReturnsParentVariableType()
    {
        // Arrange - Outer block has 'outerTemp', inner block accesses it
        var methodEnv = CreateMethodEnvironment();

        var outerBlock = CreateBlockEnvironment(methodEnv);
        outerBlock.LocalTemporaryIndices["outerTemp"] = 0;
        outerBlock.TemporariesAllocatedInFrame = 1;

        var innerBlock = CreateBlockEnvironment(outerBlock);

        // Act
        var result = _resolver.ResolveVariable("outerTemp", innerBlock);

        // Assert
        Assert.Equal(VariableResolver.VariableType.ParentVariable, result.Type);
        Assert.True(result.IsAssignable);
    }

    [Fact]
    public void ResolveVariable_DeeplyNestedAccess_ReturnsCorrectType()
    {
        // Arrange - Method -> Block1 -> Block2 -> Block3
        var methodEnv = CreateMethodEnvironment();
        methodEnv.LocalTemporaryIndices["methodTemp"] = 0;
        methodEnv.TemporariesAllocatedInFrame = 1;

        var block1 = CreateBlockEnvironment(methodEnv);
        var block2 = CreateBlockEnvironment(block1);
        var block3 = CreateBlockEnvironment(block2);

        // Act - Block3 accessing method temp
        var result = _resolver.ResolveVariable("methodTemp", block3);

        // Assert
        Assert.Equal(VariableResolver.VariableType.MethodTemporary, result.Type);
    }

    #endregion

    #region Instance Variable Resolution Tests (Step 7)

    [Fact]
    public void ResolveVariable_InstanceVariable_ReturnsInstanceVariableType()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.InstanceVariables["name"] = 0;
        env.InstanceVariables["age"] = 1;

        // Act
        var result = _resolver.ResolveVariable("name", env);

        // Assert
        Assert.Equal(VariableResolver.VariableType.InstanceVariable, result.Type);
        Assert.Equal(0, result.Index);
        Assert.True(result.IsAssignable);
    }

    [Fact]
    public void ResolveVariable_MultipleInstanceVariables_ReturnsCorrectIndices()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.InstanceVariables["first"] = 0;
        env.InstanceVariables["second"] = 1;
        env.InstanceVariables["third"] = 2;

        // Act & Assert
        Assert.Equal(0, _resolver.ResolveVariable("first", env).Index);
        Assert.Equal(1, _resolver.ResolveVariable("second", env).Index);
        Assert.Equal(2, _resolver.ResolveVariable("third", env).Index);
    }

    [Fact]
    public void ResolveVariable_InstanceVariable_IsAssignable()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.InstanceVariables["field"] = 0;

        // Act
        var result = _resolver.ResolveVariable("field", env);

        // Assert
        Assert.True(result.IsAssignable);
    }

    [Fact]
    public void ResolveVariable_InstanceVariableFromBlock_ReturnsInstanceVariableType()
    {
        // Arrange
        var methodEnv = CreateMethodEnvironment();
        methodEnv.InstanceVariables["field"] = 0;

        var blockEnv = CreateBlockEnvironment(methodEnv);
        blockEnv.InstanceVariables["field"] = 0;  // Inherited

        // Act
        var result = _resolver.ResolveVariable("field", blockEnv);

        // Assert
        Assert.Equal(VariableResolver.VariableType.InstanceVariable, result.Type);
    }

    #endregion

    #region Class Variable Resolution Tests (Step 8)

    [Fact]
    public void ResolveVariable_ClassVariable_ReturnsClassVariableType()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.ClassVariables["ClassVar"] = true;

        // Act
        var result = _resolver.ResolveVariable("ClassVar", env);

        // Assert
        Assert.Equal(VariableResolver.VariableType.ClassVariable, result.Type);
        Assert.Equal(-1, result.Index);  // Literal index assigned by caller
        Assert.True(result.IsAssignable);
    }

    [Fact]
    public void ResolveVariable_ClassVariable_IsAssignable()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.ClassVariables["Counter"] = 0;

        // Act
        var result = _resolver.ResolveVariable("Counter", env);

        // Assert
        Assert.True(result.IsAssignable);
    }

    #endregion

    #region Pool Variable Resolution Tests (Step 9)

    [Fact]
    public void ResolveVariable_PoolVariable_ReturnsPoolVariableType()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.PoolVariables["CR"] = "TextConstants";

        // Act
        var result = _resolver.ResolveVariable("CR", env);

        // Assert
        Assert.Equal(VariableResolver.VariableType.PoolVariable, result.Type);
        Assert.Equal("TextConstants", result.PoolDictionaryName);
        Assert.True(result.IsAssignable);
    }

    [Fact]
    public void ResolveVariable_PoolVariable_IncludesPoolDictionaryName()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.PoolVariables["Tab"] = "TextConstants";
        env.PoolVariables["ReadMode"] = "FilePool";

        // Act
        var textConstResult = _resolver.ResolveVariable("Tab", env);
        var filePoolResult = _resolver.ResolveVariable("ReadMode", env);

        // Assert
        Assert.Equal("TextConstants", textConstResult.PoolDictionaryName);
        Assert.Equal("FilePool", filePoolResult.PoolDictionaryName);
    }

    [Fact]
    public void ResolveVariable_UnknownVariableWithPools_AssumesPoolVariable()
    {
        // Arrange - Class has pool dictionaries declared
        var env = CreateMethodEnvironment();
        env.PoolVariables["__HAS_POOLS__"] = "TextConstants";

        // Act - Unknown variable in class with pools
        var result = _resolver.ResolveVariable("UnknownConstant", env);

        // Assert
        Assert.Equal(VariableResolver.VariableType.PoolVariable, result.Type);
        Assert.Equal("TextConstants", result.PoolDictionaryName);
    }

    #endregion

    #region Global Variable Resolution Tests (Step 10+)

    [Fact]
    public void ResolveVariable_KnownClassName_ReturnsGlobalType()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.KnownClassNames = ["Array", "Dictionary", "Object"];

        // Act
        var result = _resolver.ResolveVariable("Array", env);

        // Assert
        Assert.Equal(VariableResolver.VariableType.Global, result.Type);
        Assert.False(result.IsAssignable);  // Class names are read-only
    }

    [Fact]
    public void ResolveVariable_KnownPoolDictionaryName_ReturnsGlobalType()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.KnownPoolDictionaryNames = ["TextConstants", "FilePool"];

        // Act
        var result = _resolver.ResolveVariable("FilePool", env);

        // Assert
        Assert.Equal(VariableResolver.VariableType.Global, result.Type);
        Assert.True(result.IsAssignable);  // Pool dictionaries are assignable
    }

    [Theory]
    [InlineData("Smalltalk")]
    [InlineData("Processor")]
    [InlineData("Display")]
    [InlineData("Transcript")]
    [InlineData("Sensor")]
    [InlineData("ScheduledControllers")]
    [InlineData("Undeclared")]
    public void ResolveVariable_WellKnownGlobals_ReturnsGlobalType(string globalName)
    {
        // Arrange
        var env = CreateMethodEnvironment();

        // Act
        var result = _resolver.ResolveVariable(globalName, env);

        // Assert
        Assert.Equal(VariableResolver.VariableType.Global, result.Type);
        Assert.True(result.IsAssignable);
    }

    [Fact]
    public void ResolveVariable_UnknownVariable_ReturnsGlobalType()
    {
        // Arrange - Variable not found anywhere, assume global
        var env = CreateMethodEnvironment();

        // Act
        var result = _resolver.ResolveVariable("UnknownGlobal", env);

        // Assert
        Assert.Equal(VariableResolver.VariableType.Global, result.Type);
        Assert.Equal(-1, result.Index);  // Literal index assigned by caller
        Assert.True(result.IsAssignable);
    }

    #endregion

    #region Resolution Priority Tests

    [Fact]
    public void ResolutionPriority_LocalTempOverInstanceVar_LocalWins()
    {
        // Arrange - Same name for temp and instance var
        var env = CreateMethodEnvironment();
        env.LocalTemporaryIndices["x"] = 0;
        env.TemporariesAllocatedInFrame = 1;
        env.InstanceVariables["x"] = 0;

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope([], ["x"]);

        // Act
        var result = _resolver.ResolveVariable("x", env, allocator);

        // Assert - Local temp takes priority
        Assert.Equal(VariableResolver.VariableType.MethodTemporary, result.Type);
    }

    [Fact]
    public void ResolutionPriority_InstanceVarOverClassVar_InstanceWins()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.InstanceVariables["shared"] = 0;
        env.ClassVariables["shared"] = true;

        // Act
        var result = _resolver.ResolveVariable("shared", env);

        // Assert - Instance var takes priority
        Assert.Equal(VariableResolver.VariableType.InstanceVariable, result.Type);
    }

    [Fact]
    public void ResolutionPriority_ClassVarOverPoolVar_ClassWins()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.ClassVariables["Constant"] = true;
        env.PoolVariables["Constant"] = "SomePool";

        // Act
        var result = _resolver.ResolveVariable("Constant", env);

        // Assert - Class var takes priority
        Assert.Equal(VariableResolver.VariableType.ClassVariable, result.Type);
    }

    [Fact]
    public void ResolutionPriority_BlockLocalOverMethodTemp_BlockWins()
    {
        // Arrange
        var methodEnv = CreateMethodEnvironment();
        methodEnv.LocalTemporaryIndices["x"] = 0;
        methodEnv.TemporariesAllocatedInFrame = 1;

        var blockEnv = CreateBlockEnvironment(methodEnv);
        blockEnv.LocalTemporaryIndices["x"] = 0;
        blockEnv.TemporariesAllocatedInFrame = 1;

        // Act
        var result = _resolver.ResolveVariable("x", blockEnv);

        // Assert - Block local takes priority
        Assert.Equal(VariableResolver.VariableType.BlockTemporary, result.Type);
    }

    #endregion

    #region Assignability Tests

    [Fact]
    public void Assignability_PseudoVariables_NotAssignable()
    {
        // Arrange
        var env = CreateMethodEnvironment();

        // Act & Assert
        Assert.False(_resolver.ResolveVariable("self", env).IsAssignable);
        Assert.False(_resolver.ResolveVariable("super", env).IsAssignable);
        Assert.False(_resolver.ResolveVariable("true", env).IsAssignable);
        Assert.False(_resolver.ResolveVariable("false", env).IsAssignable);
        Assert.False(_resolver.ResolveVariable("nil", env).IsAssignable);
        Assert.False(_resolver.ResolveVariable("thisContext", env).IsAssignable);
    }

    [Fact]
    public void Assignability_Arguments_NotAssignable()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.LocalArgumentIndices["arg"] = 0;
        env.ArgumentCount = 1;

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg"], []);

        // Act
        var result = _resolver.ResolveVariable("arg", env, allocator);

        // Assert
        Assert.False(result.IsAssignable);
    }

    [Fact]
    public void Assignability_Temporaries_AreAssignable()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.LocalTemporaryIndices["temp"] = 0;
        env.TemporariesAllocatedInFrame = 1;

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope([], ["temp"]);

        // Act
        var result = _resolver.ResolveVariable("temp", env, allocator);

        // Assert
        Assert.True(result.IsAssignable);
    }

    [Fact]
    public void Assignability_ClassNames_NotAssignable()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.KnownClassNames = ["String"];

        // Act
        var result = _resolver.ResolveVariable("String", env);

        // Assert
        Assert.False(result.IsAssignable);
    }

    #endregion

    #region Edge Cases and Complex Scenarios

    [Fact]
    public void ResolveVariable_EmptyEnvironment_ResolvesAsGlobal()
    {
        // Arrange - No variables defined
        var env = CreateMethodEnvironment();

        // Act
        var result = _resolver.ResolveVariable("anything", env);

        // Assert
        Assert.Equal(VariableResolver.VariableType.Global, result.Type);
    }

    [Fact]
    public void ResolveVariable_PoolDictionaryNameInPools_TreatedAsGlobal()
    {
        // Arrange - 'TextConstants' is both a pool dict name and has __HAS_POOLS__
        var env = CreateMethodEnvironment();
        env.PoolVariables["__HAS_POOLS__"] = "TextConstants";

        // Act - The pool dictionary name itself should be a global
        var result = _resolver.ResolveVariable("TextConstants", env);

        // Assert
        Assert.Equal(VariableResolver.VariableType.Global, result.Type);
    }

    [Fact]
    public void ResolveVariable_WithAllocatorDesync_FallsBackCorrectly()
    {
        // Arrange - Allocator has variable but environment doesn't
        var env = CreateMethodEnvironment();

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg"], []);

        // Act - Environment doesn't know about 'arg', but allocator does
        var result = _resolver.ResolveVariable("arg", env, allocator);

        // Assert - Falls back to method temporary type
        Assert.Equal(VariableResolver.VariableType.MethodTemporary, result.Type);
        Assert.Equal(0, result.Index);
    }

    [Fact]
    public void ResolveVariable_NullAllocator_UsesEnvironmentResolution()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.LocalArgumentIndices["arg"] = 0;
        env.ArgumentCount = 1;

        // Act - No allocator provided
        var result = _resolver.ResolveVariable("arg", env, null);

        // Assert
        Assert.Equal(VariableResolver.VariableType.MethodArgument, result.Type);
        Assert.Equal(0, result.Index);
    }

    [Fact]
    public void ResolveVariable_BlockArgWithUnifiedFrame_ReturnsCorrectSlot()
    {
        // Arrange - Testing the UNIFIED TEMP FRAME architecture
        var methodEnv = CreateMethodEnvironment();
        methodEnv.LocalArgumentIndices["methodArg"] = 0;
        methodEnv.ArgumentCount = 1;
        methodEnv.LocalTemporaryIndices["methodTemp"] = 0;
        methodEnv.TemporariesAllocatedInFrame = 1;

        var blockEnv = CreateBlockEnvironment(methodEnv);
        blockEnv.LocalArgumentIndices["blockArg"] = 0;
        blockEnv.ArgumentCount = 1;

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["methodArg"], ["methodTemp"]);
        allocator.EnterBlockScope(["blockArg"], [], isNested: false);

        // Act
        var result = _resolver.ResolveVariable("blockArg", blockEnv, allocator);

        // Assert - Block arg at slot 2 (after methodArg=0, methodTemp=1)
        Assert.Equal(2, result.Index);
    }

    [Fact]
    public void ResolveVariable_ResolutionResultContainsCorrectName()
    {
        // Arrange
        var env = CreateMethodEnvironment();
        env.LocalTemporaryIndices["myVariable"] = 0;
        env.TemporariesAllocatedInFrame = 1;

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope([], ["myVariable"]);

        // Act
        var result = _resolver.ResolveVariable("myVariable", env, allocator);

        // Assert
        Assert.Equal("myVariable", result.Name);
    }

    #endregion
}
