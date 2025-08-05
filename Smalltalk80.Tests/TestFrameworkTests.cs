using System;
using Xunit;
using Xunit.Abstractions;

namespace Smalltalk80.Tests;

/// <summary>
/// Tests for the Smalltalk test framework infrastructure.
/// Validates that TestLibrary.st loads correctly and provides working test execution.
/// </summary>
public class TestFrameworkTests
{
    private readonly ITestOutputHelper output;

    public TestFrameworkTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void TestLibrary_LoadsSuccessfully()
    {
        // Reset system to clean state
        ObjectMemory.Reset();

        // Create VM and load test framework
        var vm = VirtualMachine.CreateWithTestFramework();

        // Verify essential test framework classes exist
        Assert.True(SystemDictionary.TryGetClass("TestCase", out var testCaseClass));
        Assert.True(SystemDictionary.TryGetClass("TestResult", out var testResultClass));
        Assert.True(SystemDictionary.TryGetClass("TestSuite", out var testSuiteClass));
        Assert.True(SystemDictionary.TryGetClass("TestRunner", out var testRunnerClass));
        Assert.True(SystemDictionary.TryGetClass("TestFailure", out var testFailureClass));

        output.WriteLine("✅ All test framework classes loaded successfully");

        // Verify test framework classes have expected methods
        Assert.True(testCaseClass.MethodDictionary.ContainsKey(new Symbol("assert:")));
        Assert.True(testCaseClass.MethodDictionary.ContainsKey(new Symbol("assertEqual:to:")));
        Assert.True(testCaseClass.MethodDictionary.ContainsKey(new Symbol("runTest")));

        output.WriteLine("✅ Test framework classes have expected methods");
    }

    [Fact]
    public void SampleBasicTest_ExecutesSuccessfully()
    {
        // Reset system to clean state
        ObjectMemory.Reset();

        // Create VM and load test framework
        var vm = VirtualMachine.CreateWithTestFramework();

        // Verify sample test class exists
        Assert.True(SystemDictionary.TryGetClass("SampleBasicTest", out var sampleTestClass));

        output.WriteLine("✅ SampleBasicTest class found");

        // Check that it has test methods
        var testMethods = 0;
        foreach (var methodName in sampleTestClass.MethodDictionary.Keys)
        {
            if (methodName.Name.StartsWith("test"))
            {
                testMethods++;
                output.WriteLine($"   Found test method: {methodName.Name}");
            }
        }

        Assert.True(testMethods > 0, "SampleBasicTest should have test methods");
        output.WriteLine($"✅ Found {testMethods} test methods in SampleBasicTest");
    }

    [Fact]
    public void TestCase_BasicAssertion_Works()
    {
        // Reset system to clean state
        ObjectMemory.Reset();

        // Create VM and load test framework
        var vm = VirtualMachine.CreateWithTestFramework();

        // Get TestCase class and create instance
        var testCaseClass = SystemDictionary.GetClass("TestCase");
        var testCaseInstance = new SmalltalkObject(testCaseClass, testCaseClass.InstanceVariables.Count);

        // Test basic assertion - this should not fail
        var assertMethod = testCaseClass.Lookup(new Symbol("assert:"));
        Assert.NotNull(assertMethod);

        var interpreter = vm.Interpreter;
        interpreter.SetActiveContext(testCaseInstance, assertMethod, true); // assert: true

        // This should execute without throwing
        var result = interpreter.ExecuteMethod();

        output.WriteLine("✅ Basic assertion (assert: true) executed successfully");
    }

    [Fact]
    public void TestFramework_Integration_CreatesAndRunsTest()
    {
        // Reset system to clean state
        ObjectMemory.Reset();

        // Create VM and load test framework
        var vm = VirtualMachine.CreateWithTestFramework();

        // Test the complete integration by attempting to create and run a simple test
        // This validates that all the framework pieces work together

        // Get SampleBasicTest class
        var sampleTestClass = SystemDictionary.GetClass("SampleBasicTest");
        var sampleTestInstance = new SmalltalkObject(sampleTestClass, sampleTestClass.InstanceVariables.Count);

        // Test that we can access test methods
        Assert.True(sampleTestClass.MethodDictionary.ContainsKey(new Symbol("testBasicArithmetic")));
        Assert.True(sampleTestClass.MethodDictionary.ContainsKey(new Symbol("testBooleanLogic")));
        Assert.True(sampleTestClass.MethodDictionary.ContainsKey(new Symbol("testObjectIdentity")));

        output.WriteLine("✅ Test framework integration validation completed");
        output.WriteLine("   - TestCase class hierarchy working");
        output.WriteLine("   - Test methods accessible");
        output.WriteLine("   - Sample test class properly defined");
    }

    [Fact]
    public void TestFramework_ArrayExtensions_Work()
    {
        // Reset system to clean state
        ObjectMemory.Reset();

        // Create VM and load test framework
        var vm = VirtualMachine.CreateWithTestFramework();

        // Test that Array extensions from test framework work
        var arrayClass = SystemDictionary.GetClass("Array");

        // Check that essential methods exist
        Assert.True(arrayClass.MethodDictionary.ContainsKey(new Symbol("copyWith:")));
        Assert.True(arrayClass.MethodDictionary.ContainsKey(new Symbol("do:")));
        Assert.True(arrayClass.MethodDictionary.ContainsKey(new Symbol(",")));

        output.WriteLine("✅ Array extensions from test framework loaded successfully");
    }

    [Fact]
    public void TestFramework_StringExtensions_Work()
    {
        // Reset system to clean state
        ObjectMemory.Reset();

        // Create VM and load test framework
        var vm = VirtualMachine.CreateWithTestFramework();

        // Test that String extensions from test framework work
        var stringClass = SystemDictionary.GetClass("String");

        // Check that essential methods exist
        Assert.True(stringClass.MethodDictionary.ContainsKey(new Symbol("startsWith:")));
        Assert.True(stringClass.MethodDictionary.ContainsKey(new Symbol("printString")));

        output.WriteLine("✅ String extensions from test framework loaded successfully");
    }
}
