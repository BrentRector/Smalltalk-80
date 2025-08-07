using System;
using System.IO;
using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Tests verifying loading of Smalltalk source files into the virtual machine.
/// </summary>
public class SourceFileLoadingTests
{
    [Fact]
    public void LoadSourceFile_DefinesMethodOnClass()
    {
        ObjectMemory.Reset();
        var vm = new VirtualMachine();
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempPath, new[]
            {
                "TestClass>>foo",
                "1 + 2"
            });

            vm.LoadSourceFile(tempPath);

            var cls = SystemDictionary.GetClass("TestClass");
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
            Assert.Equal(3, result);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
