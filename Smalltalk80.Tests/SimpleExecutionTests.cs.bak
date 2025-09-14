using System;
using Xunit;
using Xunit.Abstractions;

namespace Smalltalk80.Tests;

/// <summary>
/// Simplified execution tests to validate basic method compilation and execution 
/// without complex file-out format dependencies.
/// </summary>
public class SimpleExecutionTests(ITestOutputHelper output)
{
    [Fact]
    public void SimpleMethod_ReturnConstant_ExecutesCorrectly()
    {
        // Reset system 
        ObjectMemory.Reset();

        // Create a simple method that returns a constant
        var vm = new VirtualMachine();
        var tempFile = CreateTempMethodFile("Object>>testMethod", "^42!");

        vm.LoadSourceFile(tempFile);

        // Verify method was loaded
        var objectClass = SystemDictionary.GetClass("Object");
        var method = objectClass.Lookup(new Symbol("testMethod"));
        Assert.NotNull(method);

        // Execute the method
        var objectInstance = new SmalltalkObject(objectClass, 0);
        var interpreter = new Interpreter();
        interpreter.SetActiveContext(objectInstance, method);
        var result = interpreter.ExecuteMethod();

        Assert.Equal(42, result);
        output.WriteLine("✅ Simple method execution test passed");

        CleanupTempFile(tempFile);
    }

    [Fact]
    public void SimpleMethod_WithArgument_ExecutesCorrectly()
    {
        // Reset system 
        ObjectMemory.Reset();

        // Create a method that adds 1 to its argument
        var vm = new VirtualMachine();
        var tempFile = CreateTempMethodFile("Object>>addOne: x", "^x + 1!");

        vm.LoadSourceFile(tempFile);

        // Verify method was loaded
        var objectClass = SystemDictionary.GetClass("Object");
        var method = objectClass.Lookup(new Symbol("addOne:"));
        Assert.NotNull(method);

        // Execute the method with argument
        var objectInstance = new SmalltalkObject(objectClass, 0);
        var interpreter = new Interpreter();
        interpreter.SetActiveContext(objectInstance, method, 5);
        var result = interpreter.ExecuteMethod();

        Assert.Equal(6, result);
        output.WriteLine("✅ Method with argument test passed");

        CleanupTempFile(tempFile);
    }

    [Fact]
    public void SimpleMethod_MultipleArithmetic_ExecutesCorrectly()
    {
        // Reset system
        ObjectMemory.Reset();

        // Create a method that does multiple arithmetic operations
        var vm = new VirtualMachine();
        var tempFile = CreateTempMethodFile("Object>>calculate: a with: b", "^(a + b) * 2!");

        vm.LoadSourceFile(tempFile);

        // Execute the method
        var objectClass = SystemDictionary.GetClass("Object");
        var method = objectClass.Lookup(new Symbol("calculate:with:"));
        Assert.NotNull(method);

        var objectInstance = new SmalltalkObject(objectClass, 0);
        var interpreter = new Interpreter();
        interpreter.SetActiveContext(objectInstance, method, 3, 7);
        var result = interpreter.ExecuteMethod();

        Assert.Equal(20, result); // (3 + 7) * 2 = 20
        output.WriteLine("✅ Multiple arithmetic test passed");

        CleanupTempFile(tempFile);
    }

    /// <summary>
    /// Creates a temporary file with the specified method definition.
    /// </summary>
    private static string CreateTempMethodFile(string header, string body)
    {
        var tempFile = System.IO.Path.GetTempFileName();
        var content = $"{header}\n{body}";
        System.IO.File.WriteAllText(tempFile, content);
        return tempFile;
    }

    /// <summary>
    /// Cleans up the temporary file.
    /// </summary>
    private static void CleanupTempFile(string tempFile)
    {
        if (System.IO.File.Exists(tempFile))
        {
            System.IO.File.Delete(tempFile);
        }
    }
}
