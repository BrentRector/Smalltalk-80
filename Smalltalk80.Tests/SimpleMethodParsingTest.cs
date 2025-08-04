using System;
using Xunit;
using Xunit.Abstractions;

namespace Smalltalk80.Tests;

public class SimpleMethodParsingTest(ITestOutputHelper output)
{
    [Fact]
    public void ComplexKeywordMethod_ParsesCorrectly()
    {
        ObjectMemory.Reset();

        var fileOutSource = @"
Object subclass: #Counter
    instanceVariableNames: 'count'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Debug'!

!Counter methodsFor: 'incrementing'!
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

            var testClass = SystemDictionary.GetClass("Counter");
            Assert.NotNull(testClass);

            output.WriteLine("=== Methods loaded ===");
            foreach (var kvp in testClass.MethodDictionary)
            {
                output.WriteLine($"  {kvp.Key.Name}");
            }

            // This should be 'incrementBy:' not 'incrementBy:count:'
            var method = testClass.Lookup(new Symbol("incrementBy:"));
            output.WriteLine($"Method 'incrementBy:' found: {method != null}");

            var wrongMethod = testClass.Lookup(new Symbol("incrementBy:count:"));
            output.WriteLine($"Method 'incrementBy:count:' found: {wrongMethod != null}");

            Assert.NotNull(method);
            Assert.Null(wrongMethod);

            output.WriteLine("✅ Complex keyword method parsed correctly");
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
