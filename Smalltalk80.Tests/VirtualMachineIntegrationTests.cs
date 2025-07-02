using System;
using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Provides higher level integration tests for the virtual machine.
/// </summary>
public class VirtualMachineIntegrationTests
{
    /// <summary>
    /// Ensures that sending the <c>class</c> message to the global <c>Object</c>
    /// returns its class object.
    /// </summary>
    [Fact]
    public void GlobalObject_RespondsToClassMessage()
    {
        ObjectMemory.Reset();
        var vm = new VirtualMachine();
        var receiver = StandardLibrary.Get("Object");
        var objectClass = receiver.ClassPointer!;

        objectClass.MethodDictionary[new Symbol("class")] = new CompiledMethod
        {
            Bytecodes = new[] { (byte)Bytecode.ReturnSelf },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0,
            HasPrimitive = true,
            PrimitiveIndex = 111
        };

        var method = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushSelf,
                (byte)Bytecode.SendSpecialSelector7,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = Array.Empty<object>(),
            TemporariesCount = 0
        };

        var result = vm.Interpreter.Evaluate(method, receiver);

        Assert.Same(objectClass, result);
    }

    /// <summary>
    /// Confirms that a newly created class is registered in the object memory.
    /// </summary>
    [Fact]
    public void NewClass_IsStoredInObjectMemory()
    {
        ObjectMemory.Reset();
        _ = new VirtualMachine();
        var newClass = new SmalltalkClass();

        Assert.Contains(newClass, ObjectMemory.Objects);
    }

    /// <summary>
    /// Validates that instances of a dynamically defined class respond to
    /// their installed methods.
    /// </summary>
    [Fact]
    public void NewClass_MethodInvocation_Works()
    {
        ObjectMemory.Reset();
        var vm = new VirtualMachine();
        var newClass = new SmalltalkClass();
        var instance = new SmalltalkObject(newClass, 0);

        var fooMethod = new CompiledMethod
        {
            Bytecodes = new byte[]
            {
                (byte)Bytecode.PushLiteralConstant0,
                (byte)Bytecode.ReturnTopFromMethod
            },
            Literals = new object[] { 99 },
            TemporariesCount = 0
        };
        newClass.MethodDictionary[new Symbol("foo")] = fooMethod;

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

        Assert.Equal(99, result);
    }
}
