using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Smalltalk80.Tests;

/// <summary>
/// Helper class for testing individual method compilation and execution.
/// Provides utilities for quick method testing without full class infrastructure.
/// </summary>
public class MethodExecutionHelper
{
    private readonly ITestOutputHelper output;

    public MethodExecutionHelper(ITestOutputHelper output)
    {
        this.output = output;
    }

    /// <summary>
    /// Compiles and executes a single method source string.
    /// </summary>
    /// <param name="methodSource">The Smalltalk method source code.</param>
    /// <param name="receiver">The receiver object for the method.</param>
    /// <param name="arguments">Arguments to pass to the method.</param>
    /// <returns>The result of method execution.</returns>
    public object? CompileAndExecute(string methodSource, SmalltalkObject? receiver = null, params object[] arguments)
    {
        // Create a minimal receiver if none provided
        if (receiver == null)
        {
            var objectClass = SystemDictionary.DefineClass("Object");
            receiver = new SmalltalkObject(objectClass, 0);
        }

        // Generate argument names for the method
        var argumentNames = new List<string>();
        for (int i = 0; i < arguments.Length; i++)
        {
            argumentNames.Add($"arg{i}");
        }

        // Parse and compile the method
        var parser = new SmalltalkParser(methodSource);
        var instanceVars = receiver.ClassPointer?.InstanceVariables ?? new List<string>();
        var method = parser.ParseMethod(argumentNames, instanceVars);

        // Execute the method
        var interpreter = new Interpreter();
        interpreter.SetActiveContext(receiver, method, arguments);
        var result = interpreter.ExecuteMethod();

        output.WriteLine($"Method executed successfully. Result: {result}");
        return result;
    }

    /// <summary>
    /// Creates a test class with instance variables and compiles a method for it.
    /// </summary>
    /// <param name="className">Name of the test class.</param>
    /// <param name="instanceVariables">Instance variable names.</param>
    /// <param name="methodSource">The method source code.</param>
    /// <returns>A tuple containing the compiled method and a test instance.</returns>
    public (CompiledMethod method, SmalltalkObject instance) CreateTestClassAndMethod(
        string className,
        string[] instanceVariables,
        string methodSource)
    {
        // Define the test class
        var testClass = SystemDictionary.DefineClass(className, instanceVariables: instanceVariables);

        // Create an instance
        var instance = new SmalltalkObject(testClass, instanceVariables.Length);

        // Parse the method to see if it needs arguments (simple heuristic: check for 'arg0', 'arg1', etc.)
        var argumentNames = new List<string>();
        for (int i = 0; i < 10; i++) // Support up to 10 arguments
        {
            if (methodSource.Contains($"arg{i}"))
            {
                argumentNames.Add($"arg{i}");
            }
        }

        // Compile the method
        var parser = new SmalltalkParser(methodSource);
        var method = parser.ParseMethod(argumentNames, instanceVariables);

        output.WriteLine($"Created test class '{className}' with method.");

        return (method, instance);
    }

    /// <summary>
    /// Executes a method and verifies its bytecode generation.
    /// </summary>
    /// <param name="methodSource">The method source code.</param>
    /// <param name="expectedBytecodes">Expected bytecode sequence.</param>
    /// <param name="expectedResult">Expected execution result.</param>
    public void VerifyMethodBytecodeAndExecution(string methodSource, byte[] expectedBytecodes, object? expectedResult)
    {
        var (method, instance) = CreateTestClassAndMethod("TestClass", Array.Empty<string>(), methodSource);

        // Verify bytecode generation
        Assert.Equal(expectedBytecodes, method.Bytecodes);
        output.WriteLine($"Bytecode verification passed: {string.Join(", ", method.Bytecodes)}");

        // Verify execution result
        var interpreter = new Interpreter();
        interpreter.SetActiveContext(instance, method);
        var result = interpreter.ExecuteMethod();

        Assert.Equal(expectedResult, result);
        output.WriteLine($"Execution verification passed. Result: {result}");
    }

    /// <summary>
    /// Tests method execution with specific instance variable values.
    /// </summary>
    /// <param name="methodSource">The method source code.</param>
    /// <param name="instanceVarNames">Names of instance variables.</param>
    /// <param name="instanceVarValues">Initial values for instance variables.</param>
    /// <param name="expectedResult">Expected execution result.</param>
    public void TestMethodWithInstanceVariables(
        string methodSource,
        string[] instanceVarNames,
        object[] instanceVarValues,
        object? expectedResult)
    {
        var (method, instance) = CreateTestClassAndMethod("TestClass", instanceVarNames, methodSource);

        // Initialize instance variables
        for (int i = 0; i < Math.Min(instanceVarValues.Length, instance.Fields.Length); i++)
        {
            instance.Fields[i] = instanceVarValues[i];
        }

        // Execute method
        var interpreter = new Interpreter();
        interpreter.SetActiveContext(instance, method);
        var result = interpreter.ExecuteMethod();

        Assert.Equal(expectedResult, result);
        output.WriteLine($"Instance variable test passed. Result: {result}");
    }
}
