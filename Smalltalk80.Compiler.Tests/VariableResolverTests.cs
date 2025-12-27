using NUnit.Framework;
using Smalltalk80.Compiler;
using Smalltalk80.Compiler.Analysis;
using Smalltalk80.Compiler.BytecodeGen;
using Smalltalk80.Compiler.Model;

namespace Smalltalk80.Compiler.Tests;

/// <summary>
/// Unit tests for the VariableResolver class.
/// Tests all variable resolution paths to ensure refactoring doesn't break resolution.
/// </summary>
[TestFixture]
public class VariableResolverTests
{
    private VariableResolver _resolver = null!;

    [SetUp]
    public void Setup()
    {
        _resolver = new VariableResolver();
    }

    #region Pseudo Variables

    [Test]
    [TestCase("self")]
    [TestCase("super")]
    [TestCase("true")]
    [TestCase("false")]
    [TestCase("nil")]
    [TestCase("thisContext")]
    public void Resolve_PseudoVariable_ReturnsPseudoVariableType(string name)
    {
        var context = CreateMethodContext();
        var result = _resolver.ResolveVariable(name, context);

        Assert.That(result.Type, Is.EqualTo(VariableResolver.VariableType.PseudoVariable));
        Assert.That(result.IsAssignable, Is.False, "Pseudo-variables should not be assignable");
        Assert.That(result.Name, Is.EqualTo(name));
    }

    [Test]
    public void Resolve_PseudoVariable_CannotBeShadowed()
    {
        var context = CreateMethodContext();
        context.LocalTemporaryIndices["self"] = 0; // Try to shadow

        var result = _resolver.ResolveVariable("self", context);

        // Pseudo-variable takes precedence over temp
        Assert.That(result.Type, Is.EqualTo(VariableResolver.VariableType.PseudoVariable));
    }

    #endregion

    #region Method Arguments

    [Test]
    public void Resolve_MethodArgument_ReturnsMethodArgumentType()
    {
        var context = CreateMethodContext();
        context.LocalArgumentIndices["arg1"] = 0;
        context.ArgumentCount = 1;

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg1"], []);

        var result = _resolver.ResolveVariable("arg1", context, allocator);

        Assert.That(result.Type, Is.EqualTo(VariableResolver.VariableType.MethodArgument));
        Assert.That(result.IsAssignable, Is.False, "Method arguments should not be assignable");
        Assert.That(result.Index, Is.EqualTo(0));
    }

    [Test]
    public void Resolve_MultipleMethodArguments_ReturnsCorrectIndices()
    {
        var context = CreateMethodContext();
        context.LocalArgumentIndices["arg1"] = 0;
        context.LocalArgumentIndices["arg2"] = 1;
        context.LocalArgumentIndices["arg3"] = 2;
        context.ArgumentCount = 3;

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg1", "arg2", "arg3"], []);

        var result1 = _resolver.ResolveVariable("arg1", context, allocator);
        var result2 = _resolver.ResolveVariable("arg2", context, allocator);
        var result3 = _resolver.ResolveVariable("arg3", context, allocator);

        Assert.That(result1.Index, Is.EqualTo(0));
        Assert.That(result2.Index, Is.EqualTo(1));
        Assert.That(result3.Index, Is.EqualTo(2));
    }

    #endregion

    #region Method Temporaries

    [Test]
    public void Resolve_MethodTemporary_ReturnsMethodTemporaryType()
    {
        var context = CreateMethodContext();
        context.LocalTemporaryIndices["temp1"] = 0;
        context.TemporariesAllocatedInFrame = 1;

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope([], ["temp1"]);

        var result = _resolver.ResolveVariable("temp1", context, allocator);

        Assert.That(result.Type, Is.EqualTo(VariableResolver.VariableType.MethodTemporary));
        Assert.That(result.IsAssignable, Is.True, "Method temporaries should be assignable");
        Assert.That(result.Index, Is.EqualTo(0));
    }

    [Test]
    public void Resolve_MethodTempsAfterArgs_CorrectIndex()
    {
        var context = CreateMethodContext();
        context.LocalArgumentIndices["arg1"] = 0;
        context.LocalArgumentIndices["arg2"] = 1;
        context.ArgumentCount = 2;
        context.LocalTemporaryIndices["temp1"] = 0;
        context.TemporariesAllocatedInFrame = 1;

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["arg1", "arg2"], ["temp1"]);

        var result = _resolver.ResolveVariable("temp1", context, allocator);

        Assert.That(result.Index, Is.EqualTo(2), "Temp should be at index 2 (after 2 args)");
    }

    #endregion

    #region Block Arguments

    [Test]
    public void Resolve_BlockArgument_ReturnsBlockTemporaryType()
    {
        var methodContext = CreateMethodContext();
        var blockContext = CreateBlockContext(methodContext);
        blockContext.LocalArgumentIndices["x"] = 0;
        blockContext.ArgumentCount = 1;

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope([], []);
        allocator.EnterBlockScope(["x"], [], false);

        var result = _resolver.ResolveVariable("x", blockContext, allocator);

        Assert.That(result.Type, Is.EqualTo(VariableResolver.VariableType.BlockTemporary));
        Assert.That(result.IsAssignable, Is.True, "Block arguments should be assignable");
    }

    #endregion

    #region Instance Variables

    [Test]
    public void Resolve_InstanceVariable_ReturnsInstanceVariableType()
    {
        var context = CreateMethodContext();
        context.InstanceVariables["x"] = 0;
        context.InstanceVariables["y"] = 1;
        context.InstanceVariables["z"] = 2;

        var result = _resolver.ResolveVariable("y", context);

        Assert.That(result.Type, Is.EqualTo(VariableResolver.VariableType.InstanceVariable));
        Assert.That(result.IsAssignable, Is.True);
        Assert.That(result.Index, Is.EqualTo(1));
    }

    [Test]
    public void Resolve_InstanceVariable_FromBlock()
    {
        var methodContext = CreateMethodContext();
        methodContext.InstanceVariables["ivar"] = 3;

        var blockContext = CreateBlockContext(methodContext);
        // Block inherits instance variables from method

        var result = _resolver.ResolveVariable("ivar", blockContext);

        Assert.That(result.Type, Is.EqualTo(VariableResolver.VariableType.InstanceVariable));
        Assert.That(result.Index, Is.EqualTo(3));
    }

    #endregion

    #region Class Variables

    [Test]
    public void Resolve_ClassVariable_ReturnsClassVariableType()
    {
        var context = CreateMethodContext();
        context.ClassVariables["ClassVar"] = true; // Just mark as present

        var result = _resolver.ResolveVariable("ClassVar", context);

        Assert.That(result.Type, Is.EqualTo(VariableResolver.VariableType.ClassVariable));
        Assert.That(result.IsAssignable, Is.True);
        Assert.That(result.Index, Is.EqualTo(-1), "Class var index is assigned by caller");
    }

    #endregion

    #region Pool Variables

    [Test]
    public void Resolve_PoolVariable_ReturnsPoolVariableType()
    {
        var context = CreateMethodContext();
        context.PoolVariables["CR"] = "TextConstants";

        var result = _resolver.ResolveVariable("CR", context);

        Assert.That(result.Type, Is.EqualTo(VariableResolver.VariableType.PoolVariable));
        Assert.That(result.IsAssignable, Is.True);
        Assert.That(result.PoolDictionaryName, Is.EqualTo("TextConstants"));
    }

    #endregion

    #region Global Variables

    [Test]
    public void Resolve_UnknownVariable_AssumedGlobal()
    {
        var context = CreateMethodContext();

        var result = _resolver.ResolveVariable("UnknownGlobal", context);

        Assert.That(result.Type, Is.EqualTo(VariableResolver.VariableType.Global));
        Assert.That(result.IsAssignable, Is.True);
    }

    [Test]
    [TestCase("Smalltalk")]
    [TestCase("Processor")]
    [TestCase("Display")]
    [TestCase("Transcript")]
    [TestCase("Sensor")]
    [TestCase("ScheduledControllers")]
    [TestCase("Undeclared")]
    public void Resolve_WellKnownGlobal_ReturnsGlobalType(string name)
    {
        var context = CreateMethodContext();

        var result = _resolver.ResolveVariable(name, context);

        Assert.That(result.Type, Is.EqualTo(VariableResolver.VariableType.Global));
    }

    [Test]
    public void Resolve_KnownClassName_ReturnsGlobalType()
    {
        var context = CreateMethodContext();
        context.KnownClassNames = new HashSet<string> { "Array", "Dictionary", "OrderedCollection" };

        var result = _resolver.ResolveVariable("Array", context);

        Assert.That(result.Type, Is.EqualTo(VariableResolver.VariableType.Global));
        Assert.That(result.IsAssignable, Is.False, "Class names should not be assignable");
    }

    [Test]
    public void Resolve_PoolDictionaryName_ReturnsGlobalType()
    {
        var context = CreateMethodContext();
        context.KnownPoolDictionaryNames = new HashSet<string> { "TextConstants", "GraphicsConstants" };

        var result = _resolver.ResolveVariable("TextConstants", context);

        Assert.That(result.Type, Is.EqualTo(VariableResolver.VariableType.Global));
    }

    #endregion

    #region Resolution Priority

    [Test]
    public void Resolve_TempShadowsInstanceVar()
    {
        var context = CreateMethodContext();
        context.InstanceVariables["x"] = 0;
        context.LocalTemporaryIndices["x"] = 0;
        context.TemporariesAllocatedInFrame = 1;

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope([], ["x"]);

        var result = _resolver.ResolveVariable("x", context, allocator);

        // Temp should shadow instance variable
        Assert.That(result.Type, Is.EqualTo(VariableResolver.VariableType.MethodTemporary));
    }

    [Test]
    public void Resolve_BlockArgShadowsMethodTemp()
    {
        var methodContext = CreateMethodContext();
        methodContext.LocalTemporaryIndices["x"] = 0;
        methodContext.TemporariesAllocatedInFrame = 1;

        var blockContext = CreateBlockContext(methodContext);
        blockContext.LocalArgumentIndices["x"] = 0;
        blockContext.ArgumentCount = 1;

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope([], ["x"]);
        allocator.EnterBlockScope(["x"], [], false);

        var result = _resolver.ResolveVariable("x", blockContext, allocator);

        // Block argument should shadow method temp
        Assert.That(result.Type, Is.EqualTo(VariableResolver.VariableType.BlockTemporary));
    }

    #endregion

    #region Parent Variable Access

    [Test]
    public void Resolve_OuterTempFromBlock_ReturnsParentVariable()
    {
        var methodContext = CreateMethodContext();
        methodContext.LocalTemporaryIndices["outer"] = 0;
        methodContext.TemporariesAllocatedInFrame = 1;

        var blockContext = CreateBlockContext(methodContext);
        blockContext.LocalArgumentIndices["x"] = 0;
        blockContext.ArgumentCount = 1;

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope([], ["outer"]);
        allocator.EnterBlockScope(["x"], [], false);

        var result = _resolver.ResolveVariable("outer", blockContext, allocator);

        Assert.That(result.Type, Is.EqualTo(VariableResolver.VariableType.ParentVariable));
        Assert.That(result.IsAssignable, Is.True);
    }

    [Test]
    public void Resolve_MethodArgFromNestedBlock_ReturnsMethodArgument()
    {
        var methodContext = CreateMethodContext();
        methodContext.LocalArgumentIndices["mArg"] = 0;
        methodContext.ArgumentCount = 1;

        var block1 = CreateBlockContext(methodContext);
        var block2 = CreateBlockContext(block1);

        var allocator = new TempFrameAllocator();
        allocator.EnterMethodScope(["mArg"], []);
        allocator.EnterBlockScope([], [], false);
        allocator.EnterBlockScope([], [], true);

        var result = _resolver.ResolveVariable("mArg", block2, allocator);

        // For Blue Book inline blocks, method args accessed from nested blocks
        // are resolved as parent variables (accessed via home context chain)
        Assert.That(result.Type,
            Is.EqualTo(VariableResolver.VariableType.MethodArgument)
                .Or.EqualTo(VariableResolver.VariableType.ParentVariable),
            "Method arg from nested block should be MethodArgument or ParentVariable");
    }

    #endregion

    #region Pool Dictionary Assumption

    [Test]
    public void Resolve_UnknownVarWithPools_AssumesPoolVariable()
    {
        var context = CreateMethodContext();
        context.PoolVariables["__HAS_POOLS__"] = "TextConstants,GraphicsConstants";

        var result = _resolver.ResolveVariable("SomePoolVar", context);

        Assert.That(result.Type, Is.EqualTo(VariableResolver.VariableType.PoolVariable));
        Assert.That(result.PoolDictionaryName, Is.EqualTo("TextConstants")); // First pool
    }

    [Test]
    public void Resolve_PoolDictionaryNameItself_IsGlobalNotPoolVar()
    {
        var context = CreateMethodContext();
        context.PoolVariables["__HAS_POOLS__"] = "TextConstants,GraphicsConstants";

        var result = _resolver.ResolveVariable("TextConstants", context);

        Assert.That(result.Type, Is.EqualTo(VariableResolver.VariableType.Global));
    }

    #endregion

    #region Helper Methods

    private CompilationEnvironment CreateMethodContext()
    {
        return new CompilationEnvironment
        {
            Type = ScopeType.Method,
            ScopeDepth = 0,
            Parent = null
        };
    }

    private CompilationEnvironment CreateBlockContext(CompilationEnvironment parent)
    {
        var context = new CompilationEnvironment
        {
            Type = ScopeType.Block,
            ScopeDepth = parent.ScopeDepth + 1,
            Parent = parent
        };
        // Blocks inherit instance, class, and pool variables from parent
        foreach (var kvp in parent.InstanceVariables)
            context.InstanceVariables[kvp.Key] = kvp.Value;
        foreach (var kvp in parent.ClassVariables)
            context.ClassVariables[kvp.Key] = kvp.Value;
        foreach (var kvp in parent.PoolVariables)
            context.PoolVariables[kvp.Key] = kvp.Value;
        return context;
    }

    #endregion
}
