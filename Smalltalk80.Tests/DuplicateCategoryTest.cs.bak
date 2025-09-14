using System;
using Xunit;
using Xunit.Abstractions;

namespace Smalltalk80.Tests;

public class DuplicateCategoryTest(ITestOutputHelper output)
{
    [Fact]
    public void FileOutFormat_DuplicateCategoryHeaders_LoadsAllMethods()
    {
        ObjectMemory.Reset();

        var fileOutSource = @"
Object subclass: #TestClass
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Debug'!

!TestClass methodsFor: 'samecategory'!
method1
    ^1
! !

!TestClass methodsFor: 'samecategory'!
method2
    ^2
! !
";

        var tempFile = CreateTempFile(fileOutSource);

        try
        {
            var vm = new VirtualMachine();
            vm.LoadSystemFile(tempFile);

            var testClass = SystemDictionary.GetClass("TestClass");
            Assert.NotNull(testClass);

            var method1 = testClass.Lookup(new Symbol("method1"));
            var method2 = testClass.Lookup(new Symbol("method2"));

            output.WriteLine($"Method1 found: {method1 != null}");
            output.WriteLine($"Method2 found: {method2 != null}");
            output.WriteLine("=== Methods actually loaded ===");
            foreach (var kvp in testClass.MethodDictionary)
            {
                output.WriteLine($"  {kvp.Key.Name}");
            }

            // This will probably fail, proving the issue
            Assert.NotNull(method1); // Will likely fail
            Assert.NotNull(method2);

            output.WriteLine("✅ Duplicate category headers work");
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
