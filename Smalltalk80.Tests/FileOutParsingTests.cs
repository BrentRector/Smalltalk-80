using System;
using Xunit;
using Xunit.Abstractions;

namespace Smalltalk80.Tests;

/// <summary>
/// Tests to debug and fix file-out format parsing issues.
/// </summary>
public class FileOutParsingTests(ITestOutputHelper output)
{
    [Fact]
    public void FileOutFormat_SingleMethod_ParsesCorrectly()
    {
        ObjectMemory.Reset();

        var fileOutSource = @"
Object subclass: #TestClass
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Debug'!

!TestClass methodsFor: 'testing'!
testMethod
    ^42
! !
";

        var tempFile = CreateTempFile(fileOutSource);

        try
        {
            var vm = new VirtualMachine();
            vm.LoadSystemFile(tempFile);

            var testClass = SystemDictionary.GetClass("TestClass");
            Assert.NotNull(testClass);

            var method = testClass.Lookup(new Symbol("testMethod"));
            Assert.NotNull(method);

            output.WriteLine("✅ Single method file-out parsing works");
        }
        finally
        {
            System.IO.File.Delete(tempFile);
        }
    }

    [Fact]
    public void FileOutFormat_MultipleMethodsInCategory_ParsesCorrectly()
    {
        ObjectMemory.Reset();

        var fileOutSource = @"
Object subclass: #TestClass
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Debug'!

!TestClass methodsFor: 'testing'!
method1
    ^1
! !

!TestClass methodsFor: 'testing'!
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

            Assert.NotNull(method1);
            Assert.NotNull(method2);

            output.WriteLine("✅ Multiple methods in same category parsing works");
        }
        finally
        {
            System.IO.File.Delete(tempFile);
        }
    }

    [Fact]
    public void FileOutFormat_MultipleCategories_ParsesCorrectly()
    {
        ObjectMemory.Reset();

        var fileOutSource = @"
Object subclass: #TestClass
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Debug'!

!TestClass methodsFor: 'category1'!
method1
    ^1
! !

!TestClass methodsFor: 'category2'!
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

            output.WriteLine($"Method1 (category1) found: {method1 != null}");
            output.WriteLine($"Method2 (category2) found: {method2 != null}");

            Assert.NotNull(method1);
            Assert.NotNull(method2);

            output.WriteLine("✅ Multiple categories parsing works");
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
