using System;
using System.IO;
using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Tests for loading classic Smalltalk file-in sources.
/// </summary>
public class FileInLoaderTests
{
    [Fact]
    public void LoadSystemFile_DefinesClassesAndMethods()
    {
        ObjectMemory.Reset();
        var vm = new VirtualMachine();
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempPath, new[]
            {
                "Object subclass: #Sample!",
                "!Sample methodsFor: 'testing'!",
                "foo",
                "    ^1",
                "!",
                "bar",
                "    ^2",
                "!",
                "!!"
            });

            vm.LoadSystemFile(tempPath);

            var cls = SystemDictionary.GetClass("Sample");
            Assert.True(cls.MethodDictionary.ContainsKey(new Symbol("foo")));
            Assert.True(cls.MethodDictionary.ContainsKey(new Symbol("bar")));
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
