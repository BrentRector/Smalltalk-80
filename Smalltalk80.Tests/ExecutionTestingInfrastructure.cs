using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Smalltalk80.Tests;

/// <summary>
/// Comprehensive testing infrastructure for Smalltalk-80 class and method execution.
/// Bridges the gap between low-level bytecode tests and real-world class execution scenarios.
/// </summary>
public class ExecutionTestingInfrastructure
{
    private readonly ITestOutputHelper output;

    public ExecutionTestingInfrastructure(ITestOutputHelper output)
    {
        this.output = output;
    }

    /// <summary>
    /// Test infrastructure for compiling and executing simple Smalltalk classes.
    /// This demonstrates the complete source-to-execution pipeline.
    /// </summary>
    [Fact]
    public void BasicClassExecution_CounterClass_CompilesAndExecutes()
    {
        // Step 1: Reset system to clean state
        ObjectMemory.Reset();

        // Step 2: Create test class source
        var classSource = @"
Object subclass: #Counter
    instanceVariableNames: 'count'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Examples'!

!Counter methodsFor: 'initialization'!
initialize
    count _ 0.
    ^self
! !

!Counter methodsFor: 'accessing'!
count
    ^count
! !

!Counter methodsFor: 'incrementing'!
increment
    count _ count + 1.
    ^self
! !

!Counter methodsFor: 'incrementing'!
incrementBy: amount
    count _ count + amount.
    ^self
! !
";

        // Step 3: Compile and install the class
        var vm = new VirtualMachine();
        var tempFile = CreateTempFile(classSource);
        vm.LoadSystemFile(tempFile);

        // Step 4: Verify class was installed correctly
        var counterClass = SystemDictionary.GetClass("Counter");
        Assert.NotNull(counterClass);
        Assert.Contains("count", counterClass.InstanceVariables);

        // Debug: Print what methods were actually loaded
        output.WriteLine("=== DEBUG: Methods actually loaded in Counter class ===");
        foreach (var kvp in counterClass.MethodDictionary)
        {
            output.WriteLine($"  {kvp.Key.Name}");
        }
        output.WriteLine("=== END DEBUG ===");

        // Verify methods were compiled and installed
        AssertMethodExists(counterClass, "initialize");
        AssertMethodExists(counterClass, "count");
        AssertMethodExists(counterClass, "increment");
        AssertMethodExists(counterClass, "incrementBy:");

        // Step 5: Create instance and execute methods
        var counterInstance = new SmalltalkObject(counterClass, counterClass.InstanceVariables.Count);

        // Execute initialize method
        var initializeMethod = counterClass.Lookup(new Symbol("initialize"));
        Assert.NotNull(initializeMethod);

        // Create interpreter and execute
        var interpreter = new Interpreter();
        interpreter.SetActiveContext(counterInstance, initializeMethod);
        interpreter.ExecuteMethod(); // Actually execute the initialize method

        // Execute and verify results
        ExecuteMethodAndVerifyResult(interpreter, counterInstance, "count", 0);
        ExecuteMethodAndVerifyResult(interpreter, counterInstance, "increment", counterInstance);
        ExecuteMethodAndVerifyResult(interpreter, counterInstance, "count", 1);

        output.WriteLine("✅ Basic class execution test completed successfully");

        File.Delete(tempFile);
    }

    /// <summary>
    /// Test infrastructure for executing methods with primitive operations.
    /// </summary>
    [Fact]
    public void PrimitiveMethodExecution_ArithmeticOperations_ExecutesCorrectly()
    {
        ObjectMemory.Reset();

        var calculatorSource = @"
Object subclass: #Calculator
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Examples'!

!Calculator methodsFor: 'arithmetic'!
add: a with: b
    ^a + b
! !

!Calculator methodsFor: 'arithmetic'!
multiply: a with: b
    ^a * b
! !

!Calculator methodsFor: 'comparisons'!
isEqual: a to: b
    ^a = b
! !
";

        var vm = new VirtualMachine();
        var tempFile = CreateTempFile(calculatorSource);
        vm.LoadSystemFile(tempFile);

        var calculatorClass = SystemDictionary.GetClass("Calculator");
        var calculatorInstance = new SmalltalkObject(calculatorClass, calculatorClass.InstanceVariables.Count);

        var interpreter = new Interpreter();

        // Test arithmetic operations
        var result = ExecuteMethodWithArguments(interpreter, calculatorInstance, "add:with:", 5, 3);
        Assert.Equal(8, result);

        result = ExecuteMethodWithArguments(interpreter, calculatorInstance, "multiply:with:", 4, 7);
        Assert.Equal(28, result);

        // Test comparison operations  
        var comparisonResult = ExecuteMethodWithArguments(interpreter, calculatorInstance, "isEqual:to:", 5, 5);
        Assert.NotNull(comparisonResult);
        Assert.True((bool)comparisonResult!);

        output.WriteLine("✅ Primitive method execution test completed successfully");

        File.Delete(tempFile);
    }

    /// <summary>
    /// Test infrastructure for method execution with control flow (blocks, conditionals).
    /// </summary>
    [Fact]
    public void ControlFlowExecution_ConditionalMethods_ExecutesCorrectly()
    {
        ObjectMemory.Reset();

        var conditionalSource = @"
Object subclass: #ConditionalTester
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Examples'!

!ConditionalTester methodsFor: 'conditionals'!
absoluteValue: number
    number < 0 
        ifTrue: [ ^number negated ]
        ifFalse: [ ^number ]
! !

!ConditionalTester methodsFor: 'conditionals'!
max: a with: b
    ^a > b 
        ifTrue: [ a ]
        ifFalse: [ b ]
! !
";

        var vm = new VirtualMachine();
        var tempFile = CreateTempFile(conditionalSource);
        vm.LoadSystemFile(tempFile);

        var testClass = SystemDictionary.GetClass("ConditionalTester");
        var testInstance = new SmalltalkObject(testClass, testClass.InstanceVariables.Count);

        var interpreter = new Interpreter();

        // Test conditional execution with negative number
        var result = ExecuteMethodWithArguments(interpreter, testInstance, "absoluteValue:", -5);
        Assert.Equal(5, result);

        // Test conditional execution with positive number
        result = ExecuteMethodWithArguments(interpreter, testInstance, "absoluteValue:", 3);
        Assert.Equal(3, result);

        // Test max method
        result = ExecuteMethodWithArguments(interpreter, testInstance, "max:with:", 10, 7);
        Assert.Equal(10, result);

        result = ExecuteMethodWithArguments(interpreter, testInstance, "max:with:", 3, 8);
        Assert.Equal(8, result);

        output.WriteLine("✅ Control flow execution test completed successfully");

        File.Delete(tempFile);
    }

    /// <summary>
    /// Test infrastructure for class inheritance and method lookup.
    /// </summary>
    [Fact]
    public void InheritanceExecution_SuperclassMethodLookup_WorksCorrectly()
    {
        ObjectMemory.Reset();

        var inheritanceSource = @"
Object subclass: #Shape
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Examples'!

!Shape methodsFor: 'displaying'!
displayName
    ^'Generic Shape'
! !

!Shape methodsFor: 'calculating'!
area
    ^0
! !

Shape subclass: #Rectangle
    instanceVariableNames: 'width height'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Examples'!

!Rectangle methodsFor: 'initialization'!
width: w height: h
    width := w.
    height := h.
    ^self
! !

!Rectangle methodsFor: 'calculating'!
area
    ^width * height
! !
";

        var vm = new VirtualMachine();
        var tempFile = CreateTempFile(inheritanceSource);
        vm.LoadSystemFile(tempFile);

        // Verify inheritance structure
        var shapeClass = SystemDictionary.GetClass("Shape");
        var rectangleClass = SystemDictionary.GetClass("Rectangle");
        Assert.Equal(shapeClass, rectangleClass.Superclass);

        // Create rectangle instance and test inherited method
        var rectangleInstance = new SmalltalkObject(rectangleClass, rectangleClass.InstanceVariables.Count);
        var interpreter = new Interpreter();

        // Test inherited method from superclass
        var displayName = ExecuteMethodAndGetResult(interpreter, rectangleInstance, "displayName");
        Assert.Equal("Generic Shape", displayName);

        // Test overridden method in subclass
        ExecuteMethodWithArguments(interpreter, rectangleInstance, "width:height:", 5, 3);
        var area = ExecuteMethodAndGetResult(interpreter, rectangleInstance, "area");
        Assert.Equal(15, area);

        output.WriteLine("✅ Inheritance execution test completed successfully");

        File.Delete(tempFile);
    }

    /// <summary>
    /// Test infrastructure for comprehensive method execution scenarios.
    /// </summary>
    [Fact]
    public void ComprehensiveExecution_ComplexMethodInteractions_ExecutesCorrectly()
    {
        ObjectMemory.Reset();

        var complexSource = @"
Object subclass: #BankAccount
    instanceVariableNames: 'balance accountNumber'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Examples'!

!BankAccount methodsFor: 'initialization'!
accountNumber: number initialBalance: amount
    accountNumber := number.
    balance := amount.
    ^self
! !

!BankAccount methodsFor: 'accessing'!
balance
    ^balance
! !

!BankAccount methodsFor: 'transactions'!
deposit: amount
    amount > 0 ifTrue: [
        balance := balance + amount.
        ^true
    ].
    ^false
! !

!BankAccount methodsFor: 'transactions'!
withdraw: amount
    (amount > 0 and: [ amount <= balance ]) ifTrue: [
        balance := balance - amount.
        ^true
    ].
    ^false
! !

!BankAccount methodsFor: 'transactions'!
transferTo: otherAccount amount: amount
    (self withdraw: amount) ifTrue: [
        ^otherAccount deposit: amount
    ].
    ^false
! !
";

        var vm = new VirtualMachine();
        var tempFile = CreateTempFile(complexSource);
        vm.LoadSystemFile(tempFile);

        var accountClass = SystemDictionary.GetClass("BankAccount");
        var interpreter = new Interpreter();

        // Create two account instances
        var account1 = new SmalltalkObject(accountClass, accountClass.InstanceVariables.Count);
        var account2 = new SmalltalkObject(accountClass, accountClass.InstanceVariables.Count);

        // Initialize accounts
        ExecuteMethodWithArguments(interpreter, account1, "accountNumber:initialBalance:", 1001, 1000);
        ExecuteMethodWithArguments(interpreter, account2, "accountNumber:initialBalance:", 1002, 500);

        // Test deposit
        var depositResult = ExecuteMethodWithArguments(interpreter, account1, "deposit:", 200);
        Assert.NotNull(depositResult);
        Assert.True((bool)depositResult!);

        var balance1 = ExecuteMethodAndGetResult(interpreter, account1, "balance");
        Assert.Equal(1200, balance1);

        // Test withdrawal
        var withdrawResult = ExecuteMethodWithArguments(interpreter, account1, "withdraw:", 300);
        Assert.NotNull(withdrawResult);
        Assert.True((bool)withdrawResult!);

        balance1 = ExecuteMethodAndGetResult(interpreter, account1, "balance");
        Assert.Equal(900, balance1);

        // Test transfer between accounts
        var transferResult = ExecuteMethodWithArguments(interpreter, account1, "transferTo:amount:", account2, 100);
        Assert.NotNull(transferResult);
        Assert.True((bool)transferResult!);

        balance1 = ExecuteMethodAndGetResult(interpreter, account1, "balance");
        var balance2 = ExecuteMethodAndGetResult(interpreter, account2, "balance");

        Assert.Equal(800, balance1);
        Assert.Equal(600, balance2);

        output.WriteLine("✅ Comprehensive execution test completed successfully");

        File.Delete(tempFile);
    }

    #region Helper Methods

    private string CreateTempFile(string content)
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, content);
        return tempFile;
    }

    private void AssertMethodExists(SmalltalkClass cls, string selector)
    {
        Assert.True(cls.MethodDictionary.ContainsKey(new Symbol(selector)),
            $"Method '{selector}' should exist in class");
    }

    private void ExecuteMethodAndVerifyResult(Interpreter interpreter, SmalltalkObject receiver,
        string selector, object expectedResult)
    {
        var method = receiver.ClassPointer!.Lookup(new Symbol(selector));
        Assert.NotNull(method);

        interpreter.SetActiveContext(receiver, method);
        var result = interpreter.ExecuteMethod();

        Assert.Equal(expectedResult, result);
    }

    private object? ExecuteMethodWithArguments(Interpreter interpreter, SmalltalkObject receiver,
        string selector, params object[] arguments)
    {
        var method = receiver.ClassPointer!.Lookup(new Symbol(selector));
        Assert.NotNull(method);

        interpreter.SetActiveContext(receiver, method, arguments);
        return interpreter.ExecuteMethod();
    }

    private object? ExecuteMethodAndGetResult(Interpreter interpreter, SmalltalkObject receiver, string selector)
    {
        var method = receiver.ClassPointer!.Lookup(new Symbol(selector));
        Assert.NotNull(method);

        interpreter.SetActiveContext(receiver, method);
        return interpreter.ExecuteMethod();
    }

    #endregion
}
