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

    /// <summary>
    /// Ensures that temporary variables within loaded methods are
    /// compiled correctly by the ANTLR-based loader.
    /// </summary>
    [Fact]
    public void LoadSystemFile_MethodWithTemporaries_ExecutesCorrectly()
    {
        ObjectMemory.Reset();
        var vm = new VirtualMachine();
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempPath, new[]
            {
                "Object subclass: #TempExample!",
                "!TempExample methodsFor: 'testing'!",
                "foo",
                "| x y |",
                "    x := 2.",
                "    y := 3.",
                "    ^x + y",
                "!",
                "!!"
            });

            vm.LoadSystemFile(tempPath);

            var cls = SystemDictionary.GetClass("TempExample");
            Assert.True(cls.MethodDictionary.ContainsKey(new Symbol("foo")));

            var instance = new SmalltalkObject(cls, 0);
            var callMethod = new CompiledMethod
            {
                Bytecodes = new byte[]
                {
                    (byte)Bytecode.PushSelf,
                    (byte)Bytecode.SingleExtendedSend, 0,
                    (byte)Bytecode.ReturnTopFromMethod
                },
                Literals = new object[] { new Symbol("foo") },
                TemporariesCount = 0
            };

            var result = vm.Interpreter.Evaluate(callMethod, instance);
            Assert.Equal(5, result);

            var loaded = cls.MethodDictionary[new Symbol("foo")];
            Assert.Contains((byte)Bytecode.StoreAndPopTemporaryVariable0, loaded.Bytecodes);
            Assert.Contains((byte)Bytecode.StoreAndPopTemporaryVariable1, loaded.Bytecodes);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Confirms that keyword message sends are parsed and executed correctly.
    /// </summary>
    [Fact]
    public void LoadSystemFile_KeywordSendMethod_ExecutesCorrectly()
    {
        ObjectMemory.Reset();
        var vm = new VirtualMachine();
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempPath, new[]
            {
                "Object subclass: #KeywordExample instanceVariableNames: ''!",
                "!KeywordExample methodsFor: 'testing'!",
                "add: x to: y",
                "    ^x + y",
                "!",
                "call",
                "    ^self add: 3 to: 4",
                "!",
                "!!"
            });

            vm.LoadSystemFile(tempPath);

            var cls = SystemDictionary.GetClass("KeywordExample");
            var instance = new SmalltalkObject(cls, 0);
            var call = cls.MethodDictionary[new Symbol("call")];

            var result = vm.Interpreter.Evaluate(call, instance);

            Assert.Equal(7, result);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Verifies that method category headers with a trailing exclamation mark on
    /// the same line are parsed correctly.
    /// </summary>
    [Fact]
    public void LoadSystemFile_HeaderWithTrailingBang_ParsesCorrectly()
    {
        ObjectMemory.Reset();
        var vm = new VirtualMachine();
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempPath, new[]
            {
                "Object subclass: #Example!",
                "!Example methodsFor: 'empty'! !",
                "!!"
            });

            vm.LoadSystemFile(tempPath);

            Assert.NotNull(SystemDictionary.GetClass("Example"));
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Ensures that class definition options are parsed without entering a new scope
    /// and that instance variables are recorded correctly.
    /// </summary>
    [Fact]
    public void LoadSystemFile_ClassDefinitionWithOptions_DefinesVariables()
    {
        ObjectMemory.Reset();
        var vm = new VirtualMachine();
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempPath, new[]
            {
                "Object subclass: #OptionTest",
                "    instanceVariableNames: 'selectors'",
                "    classVariableNames: ''",
                "    poolDictionaries: ''",
                "    category: 'Menus'!",
                "!!"
            });

            vm.LoadSystemFile(tempPath);

            var cls = SystemDictionary.GetClass("OptionTest");
            Assert.Contains("selectors", cls.InstanceVariables);
            Assert.Empty(cls.PoolDictionaries);
            Assert.Equal("Menus", cls.Category);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Ensures that class variables declared in a class definition are recorded.
    /// </summary>
    [Fact]
    public void LoadSystemFile_ClassDefinitionWithClassVars_DefinesClassVariables()
    {
        ObjectMemory.Reset();
        var vm = new VirtualMachine();
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempPath, new[]
            {
                "Object subclass: #ClassVarTest",
                "    instanceVariableNames: ''",
                "    classVariableNames: 'Foo Bar'",
                "    poolDictionaries: ''",
                "    category: 'Tests'!",
                "!!"
            });

            vm.LoadSystemFile(tempPath);

            var cls = SystemDictionary.GetClass("ClassVarTest");
            Assert.Contains("Foo", cls.ClassVariables);
            Assert.Contains("Bar", cls.ClassVariables);
            Assert.Empty(cls.PoolDictionaries);
            Assert.Equal("Tests", cls.Category);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Verifies that pool dictionary names are parsed and stored on the class.
    /// </summary>
    [Fact]
    public void LoadSystemFile_ClassDefinitionWithPools_RecordsPools()
    {
        ObjectMemory.Reset();
        var vm = new VirtualMachine();
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempPath, new[]
            {
                "Object subclass: #PoolTest",
                "    instanceVariableNames: ''",
                "    classVariableNames: ''",
                "    poolDictionaries: 'TextConstants OtherPool'",
                "    category: 'Tests'!",
                "!!"
            });

            vm.LoadSystemFile(tempPath);

            var cls = SystemDictionary.GetClass("PoolTest");
            Assert.Contains("TextConstants", cls.PoolDictionaries);
            Assert.Contains("OtherPool", cls.PoolDictionaries);
            Assert.Equal("Tests", cls.Category);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Ensures that class-side methods are compiled and installed on the class.
    /// </summary>
    [Fact]
    public void LoadSystemFile_ClassMethod_CompilesAndExecutes()
    {
        ObjectMemory.Reset();
        var vm = new VirtualMachine();
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempPath, new[]
            {
                "Object subclass: #Counter!",
                "!Counter class methodsFor: 'testing'!",
                "fortyTwo",
                "    ^42!",
                "!!"
            });

            vm.LoadSystemFile(tempPath);

            var cls = SystemDictionary.GetClass("Counter");
            Assert.True(cls.MethodDictionary.ContainsKey(new Symbol("fortyTwo")));

            var method = cls.MethodDictionary[new Symbol("fortyTwo")];
            var result = vm.Interpreter.Evaluate(method, cls);
            Assert.Equal(42, result);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
