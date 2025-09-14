using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Smalltalk80.Tests;

/// <summary>
/// Integration tests demonstrating the complete Smalltalk-80 execution testing infrastructure.
/// Shows source-to-execution pipeline with real class compilation and method execution.
/// </summary>
public class IntegrationExecutionTests
{
    private readonly ITestOutputHelper output;
    private readonly MethodExecutionHelper helper;

    public IntegrationExecutionTests(ITestOutputHelper output)
    {
        this.output = output;
        this.helper = new MethodExecutionHelper(output);
    }

    [Fact]
    public void SimpleMethodExecution_ReturnConstant_WorksCorrectly()
    {
        ObjectMemory.Reset();

        // Test simple method that returns a constant
        var methodSource = "^42!";
        var result = helper.CompileAndExecute(methodSource);

        Assert.Equal(42, result);
        output.WriteLine("✅ Simple constant return method executed successfully");
    }

    [Fact]
    public void ArithmeticMethodExecution_AddTwoNumbers_WorksCorrectly()
    {
        ObjectMemory.Reset();

        // Test method with arithmetic operations
        var methodSource = "arg0 + arg1!";
        var result = helper.CompileAndExecute(methodSource, arguments: new object[] { 15, 27 });

        Assert.Equal(42, result);
        output.WriteLine("✅ Arithmetic method executed successfully");
    }

    [Fact]
    public void InstanceVariableAccess_GetterSetter_WorksCorrectly()
    {
        ObjectMemory.Reset();

        // Test instance variable access
        var getterSource = "^value!";
        helper.TestMethodWithInstanceVariables(
            getterSource,
            new[] { "value" },
            new object[] { 100 },
            100);

        var setterSource = "value := arg0. ^self!";
        var (setterMethod, instance) = helper.CreateTestClassAndMethod("TestClass", new[] { "value" }, setterSource);

        var interpreter = new Interpreter();
        interpreter.SetActiveContext(instance, setterMethod, 200);
        var setterResult = interpreter.ExecuteMethod();

        Assert.Equal(instance, setterResult);
        Assert.Equal(200, instance.Fields[0]);

        output.WriteLine("✅ Instance variable access methods executed successfully");
    }

    [Fact]
    public void ConditionalExecution_IfTrueFalse_WorksCorrectly()
    {
        ObjectMemory.Reset();

        // Test conditional execution
        var conditionalSource = @"
arg0 ifTrue: [ ^'positive' ] ifFalse: [ ^'negative' ]!";

        // Test true branch
        var result1 = helper.CompileAndExecute(conditionalSource, arguments: new object[] { true });
        Assert.Equal("positive", result1);

        // Test false branch  
        var result2 = helper.CompileAndExecute(conditionalSource, arguments: new object[] { false });
        Assert.Equal("negative", result2);

        output.WriteLine("✅ Conditional execution methods executed successfully");
    }

    [Fact]
    public void BytecodeVerification_OptimizedReturns_GeneratesCorrectBytecodes()
    {
        ObjectMemory.Reset();

        // Test optimized return bytecode generation
        helper.VerifyMethodBytecodeAndExecution(
            "^self!",
            new byte[] { (byte)Bytecode.ReturnSelf },
            null); // Will return the receiver

        helper.VerifyMethodBytecodeAndExecution(
            "^true!",
            new byte[] { (byte)Bytecode.ReturnTrue },
            true);

        helper.VerifyMethodBytecodeAndExecution(
            "^nil!",
            new byte[] { (byte)Bytecode.ReturnNil },
            null);

        output.WriteLine("✅ Optimized return bytecode verification passed");
    }

    [Fact]
    public void FullClassWorkflow_CounterExample_ExecutesCorrectly()
    {
        ObjectMemory.Reset();

        // Test complete class compilation and execution workflow
        var classSource = @"
Object subclass: #SimpleCounter
    instanceVariableNames: 'count'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Integration'!

!SimpleCounter methodsFor: 'initialization'!
initialize
    count := 0.
    ^self
! !

!SimpleCounter methodsFor: 'accessing'!
getCount
    ^count
! !

!SimpleCounter methodsFor: 'incrementing'!
increment
    count := count + 1.
    ^count
! !
";

        // Load the complete class
        var vm = new VirtualMachine();
        var tempFile = CreateTempFile(classSource);
        vm.LoadSystemFile(tempFile);

        // Get the class and create an instance
        var counterClass = SystemDictionary.GetClass("SimpleCounter");
        var counterInstance = new SmalltalkObject(counterClass, counterClass.InstanceVariables.Count);

        var interpreter = new Interpreter();

        // Test initialization
        var initMethod = counterClass.Lookup(new Symbol("initialize"));
        Assert.NotNull(initMethod);
        interpreter.SetActiveContext(counterInstance, initMethod);
        var initResult = interpreter.ExecuteMethod();
        Assert.Equal(counterInstance, initResult);

        // Test getting initial count
        var getCountMethod = counterClass.Lookup(new Symbol("getCount"));
        Assert.NotNull(getCountMethod);
        interpreter.SetActiveContext(counterInstance, getCountMethod);
        var countResult = interpreter.ExecuteMethod();
        Assert.Equal(0, countResult);

        // Test increment
        var incrementMethod = counterClass.Lookup(new Symbol("increment"));
        Assert.NotNull(incrementMethod);
        interpreter.SetActiveContext(counterInstance, incrementMethod);
        var incrementResult = interpreter.ExecuteMethod();
        Assert.Equal(1, incrementResult);

        // Verify count was updated
        interpreter.SetActiveContext(counterInstance, getCountMethod);
        var finalCount = interpreter.ExecuteMethod();
        Assert.Equal(1, finalCount);

        output.WriteLine("✅ Full class workflow executed successfully");

        File.Delete(tempFile);
    }

    [Fact]
    public void MethodLookupAndInheritance_SuperclassMethod_WorksCorrectly()
    {
        ObjectMemory.Reset();

        var inheritanceSource = @"
Object subclass: #Parent
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Integration'!

!Parent methodsFor: 'testing'!
parentMethod
    ^'from parent'
! !

Parent subclass: #Child
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Integration'!

!Child methodsFor: 'testing'!
childMethod
    ^'from child'
! !
";

        var vm = new VirtualMachine();
        var tempFile = CreateTempFile(inheritanceSource);
        vm.LoadSystemFile(tempFile);

        var childClass = SystemDictionary.GetClass("Child");
        var childInstance = new SmalltalkObject(childClass, 0);

        var interpreter = new Interpreter();

        // Test child's own method
        var childMethod = childClass.Lookup(new Symbol("childMethod"));
        Assert.NotNull(childMethod);
        interpreter.SetActiveContext(childInstance, childMethod);
        var childResult = interpreter.ExecuteMethod();
        Assert.Equal("from child", childResult);

        // Test inherited method from parent
        var parentMethod = childClass.Lookup(new Symbol("parentMethod"));
        Assert.NotNull(parentMethod);
        interpreter.SetActiveContext(childInstance, parentMethod);
        var parentResult = interpreter.ExecuteMethod();
        Assert.Equal("from parent", parentResult);

        output.WriteLine("✅ Method lookup and inheritance executed successfully");

        File.Delete(tempFile);
    }

    #region Helper Methods

    private string CreateTempFile(string content)
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, content);
        return tempFile;
    }

    #endregion
}
