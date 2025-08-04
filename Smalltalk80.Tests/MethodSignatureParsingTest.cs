using System;
using Xunit;
using Xunit.Abstractions;

namespace Smalltalk80.Tests;

public class MethodSignatureParsingTest(ITestOutputHelper output)
{
    [Fact]
    public void MethodWithArgument_ParsesSignatureCorrectly()
    {
        ObjectMemory.Reset();

        var fileOutSource = @"
Object subclass: #TestClass
    instanceVariableNames: 'instanceVar'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Debug'!

!TestClass methodsFor: 'testing'!
methodWith: argument
    instanceVar := argument.
    ^self
! !
";

        var tempFile = CreateTempFile(fileOutSource);

        try
        {
            var vm = new VirtualMachine();
            vm.LoadSystemFile(tempFile);

            var testClass = SystemDictionary.GetClass("TestClass");
            Assert.NotNull(testClass);

            output.WriteLine("=== Methods loaded with their signatures ===");
            foreach (var kvp in testClass.MethodDictionary)
            {
                output.WriteLine($"  {kvp.Key.Name}");
            }

            // Check if the method signature is correct
            var method = testClass.Lookup(new Symbol("methodWith:"));
            Assert.NotNull(method);

            output.WriteLine("✅ Method signature parsed correctly");
        }
        finally
        {
            System.IO.File.Delete(tempFile);
        }
    }

    [Fact]
    public void MethodWithArgumentAndInstanceVariable_ParsesSignatureCorrectly()
    {
        ObjectMemory.Reset();

        var fileOutSource = @"
Object subclass: #TestClass
    instanceVariableNames: 'count'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Debug'!

!TestClass methodsFor: 'testing'!
incrementBy: amount
    count _ count + amount.
    ^self
! !
";

        var tempFile = CreateTempFile(fileOutSource);

        try
        {
            var vm = new VirtualMachine();
            vm.LoadSystemFile(tempFile);

            var testClass = SystemDictionary.GetClass("TestClass");
            Assert.NotNull(testClass);

            output.WriteLine("=== Methods loaded with their signatures ===");
            foreach (var kvp in testClass.MethodDictionary)
            {
                output.WriteLine($"  {kvp.Key.Name}");
            }

            // This should find incrementBy: not incrementBy:count:
            var method = testClass.Lookup(new Symbol("incrementBy:"));
            output.WriteLine($"Method 'incrementBy:' found: {method != null}");

            var wrongMethod = testClass.Lookup(new Symbol("incrementBy:count:"));
            output.WriteLine($"Method 'incrementBy:count:' found: {wrongMethod != null}");

            Assert.NotNull(method);

            output.WriteLine("✅ Method signature with instance variable parsed correctly");
        }
        finally
        {
            System.IO.File.Delete(tempFile);
        }
    }

    private static string CreateTempFile(string content)
    {
        var tempFile = System.IO.Path.GetTempFileName();
        System.IO.File.WriteAllText(tempFile, content);
        return tempFile;
    }
}
