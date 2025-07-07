using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Validates that sample Smalltalk source files compile without errors.
/// </summary>
public class ParserSampleFilesTests
{
    /// <summary>
    /// Provides test data mapping each sample file to the class and method
    /// expected to be defined.
    /// </summary>
    public static IEnumerable<object[]> SampleFiles => new[]
    {
        new object[] { "BasicClassNoMembers.st", "BasicNoMembers", null! },
        new object[] { "BasicMetaclassNoMembers.st", "MetaNoMembers", null! },
        new object[] { "ClassWithInstanceVar.st", "WithInstanceVar", null! },
        new object[] { "ClassWithMethod.st", "WithMethod", "foo" },
        new object[] { "MethodWithTemporary.st", "TempExample", "compute" },
        new object[] { "MethodWithInstanceVar.st", "LocalExample", "setAndAdd:" },
        new object[] { "MetaclassWithMethod.st", "MetaWithMethod", "answer" },
        new object[] { "ExpressionConstructs.st", "ExpressionSamples", "characterExample" }
    };

    /// <summary>
    /// Loads each sample file and verifies that the expected class and method
    /// definitions are present after compilation.
    /// </summary>
    /// <param name="file">The file to load.</param>
    /// <param name="className">The class defined by the file.</param>
    /// <param name="methodName">An optional method expected on the class.</param>
    [Theory]
    [MemberData(nameof(SampleFiles))]
    public void LoadSystemFile_SampleFile_Compiles(string file, string className, string? methodName)
    {
        ObjectMemory.Reset();
        var vm = new VirtualMachine();
        var baseDir = AppContext.BaseDirectory;
        var path = Path.GetFullPath(Path.Combine(
            baseDir,
            "..", "..", "..", "..",
            "Smalltalk80", "Smalltalk-80 Source", file));

        vm.LoadSystemFile(path);

        var cls = SystemDictionary.GetClass(className);
        Assert.NotNull(cls);

        if (methodName is not null)
        {
            Assert.True(cls.MethodDictionary.ContainsKey(new Symbol(methodName)));
        }
    }
}
