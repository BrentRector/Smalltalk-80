namespace Smalltalk80.Interpreter.ColdStart;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Config;

/// <summary>
/// Creates a TestClass with precompiled methods for validating bytecode execution.
/// </summary>
public static class TestClassBuilder
{
    /// <summary>
    /// Creates and registers the TestClass with precompiled test methods.
    /// </summary>
    public static SObjectRef CreateTestClass()
    {
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Creating TestClass with precompiled methods...");
        }

        // Create TestClass as subclass of Object with one instance variable "value"
        var objectClassRef = SystemDictionary.ObjectClassRef;
        if (objectClassRef.IsNil)
        {
            throw new InvalidOperationException("Object class not found");
        }

        // Create the class using ObjectMemory.CreateClassObject
        var testClassRef = ObjectMemory.CreateClassObject(
            "TestClass",
            objectClassRef,
            new[] { "value" },
            ObjectFormat.FixedPointer);

        // Add precompiled test methods
        AddSimpleReturnMethod(testClassRef);
        AddSimpleArithmeticMethod(testClassRef);
        AddInstanceVariableMethod(testClassRef);
        AddMessageSendMethod(testClassRef);

        // Add enhanced test methods
        AddTemporaryVariableMethod(testClassRef);
        AddNestedLevel3Method(testClassRef);
        AddNestedLevel2Method(testClassRef);
        AddNestedLevel1Method(testClassRef);
        AddNestedLevel0WithGCMethod(testClassRef);
        AddForceGCMethod(testClassRef);

        // Register as global
        SystemDictionary.RegisterGlobalRef("TestClass", testClassRef);

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] TestClass created with 10 test methods");
        }

        return testClassRef;
    }

    /// <summary>
    /// Adds testReturnConstant method - returns 42.
    /// Bytecode: pushConstant 42, returnTop
    /// Note: Selector is added as LAST literal by CreateCompiledMethod
    /// </summary>
    private static void AddSimpleReturnMethod(SObjectRef classRef)
    {
        // Method: testReturnConstant
        // ^42
        var bytecodes = new byte[]
        {
            0x20,  // push literal constant #0 (42) - selector will be #1
            0x7C   // return top
        };

        var literals = new SObjectRef[]
        {
            SObjectRef.FromSmallInteger(42)
            // Selector "testReturnConstant" will be added as literal #1 by CreateCompiledMethod
        };

        var method = ObjectMemory.CreateCompiledMethod(
            "testReturnConstant",
            bytecodes,
            literals,
            0, // no arguments
            0, // no temporaries
            0); // no primitive

        ObjectMemory.AddMethod(classRef.AsObjectPointer(), "testReturnConstant", method);
    }

    /// <summary>
    /// Adds testAddition method - returns 3 + 4 = 7.
    /// Bytecode: push 3, push 4, send +, returnTop
    /// Note: Selector is added as LAST literal by CreateCompiledMethod
    /// </summary>
    private static void AddSimpleArithmeticMethod(SObjectRef classRef)
    {
        // Method: testAddition
        // ^3 + 4
        var bytecodes = new byte[]
        {
            0x20,  // push literal constant #0 (3)
            0x21,  // push literal constant #1 (4) - selector will be #2
            0xB0,  // send + (arithmetic selector #0)
            0x7C   // return top
        };

        var literals = new SObjectRef[]
        {
            SObjectRef.FromSmallInteger(3),
            SObjectRef.FromSmallInteger(4)
            // Selector "testAddition" will be added as literal #2 by CreateCompiledMethod
        };

        var method = ObjectMemory.CreateCompiledMethod(
            "testAddition",
            bytecodes,
            literals,
            0, // no arguments
            0, // no temporaries
            0); // no primitive

        ObjectMemory.AddMethod(classRef.AsObjectPointer(), "testAddition", method);
    }

    /// <summary>
    /// Adds testGetValue method - returns the value instance variable.
    /// Bytecode: pushReceiverVariable 0, returnTop
    /// </summary>
    private static void AddInstanceVariableMethod(SObjectRef classRef)
    {
        // Method: testGetValue
        // ^value
        var bytecodes = new byte[]
        {
            0x00,  // push receiver variable #0 (value)
            0x7C   // return top
        };

        var method = ObjectMemory.CreateCompiledMethod(
            "testGetValue",
            bytecodes,
            Array.Empty<SObjectRef>(),
            0, // no arguments
            0, // no temporaries
            0); // no primitive

        ObjectMemory.AddMethod(classRef.AsObjectPointer(), "testGetValue", method);
    }

    /// <summary>
    /// Adds testMultiplication method - returns 6 * 7 = 42.
    /// Bytecode: push 6, push 7, send *, returnTop
    /// Note: Selector is added as LAST literal by CreateCompiledMethod
    /// </summary>
    private static void AddMessageSendMethod(SObjectRef classRef)
    {
        // Method: testMultiplication
        // ^6 * 7
        var bytecodes = new byte[]
        {
            0x20,  // push literal constant #0 (6)
            0x21,  // push literal constant #1 (7) - selector will be #2
            0xB8,  // send * (arithmetic selector #8)
            0x7C   // return top
        };

        var literals = new SObjectRef[]
        {
            SObjectRef.FromSmallInteger(6),
            SObjectRef.FromSmallInteger(7)
            // Selector "testMultiplication" will be added as literal #2 by CreateCompiledMethod
        };

        var method = ObjectMemory.CreateCompiledMethod(
            "testMultiplication",
            bytecodes,
            literals,
            0, // no arguments
            0, // no temporaries
            0); // no primitive

        ObjectMemory.AddMethod(classRef.AsObjectPointer(), "testMultiplication", method);
    }

    /// <summary>
    /// Adds testTemporary method - stores 99 in a temporary, then returns it.
    /// Tests temporary variable store and load.
    /// Bytecode: push 99, store temp 0, push temp 0, returnTop
    /// </summary>
    private static void AddTemporaryVariableMethod(SObjectRef classRef)
    {
        // Method: testTemporary
        // | temp |
        // temp := 99.
        // ^temp
        var bytecodes = new byte[]
        {
            0x20,  // push literal constant #0 (99)
            0x68,  // store into temp 0
            0x10,  // push temp 0
            0x7C   // return top
        };

        var literals = new SObjectRef[]
        {
            SObjectRef.FromSmallInteger(99)
            // Selector "testTemporary" will be added by CreateCompiledMethod
        };

        var method = ObjectMemory.CreateCompiledMethod(
            "testTemporary",
            bytecodes,
            literals,
            0, // no arguments
            1, // 1 temporary
            0); // no primitive

        ObjectMemory.AddMethod(classRef.AsObjectPointer(), "testTemporary", method);
    }

    /// <summary>
    /// Adds testNestedLevel3 - top level of nested calls.
    /// Sends testNestedLevel2 to self.
    /// Bytecode: push self, send testNestedLevel2, returnTop
    /// </summary>
    private static void AddNestedLevel3Method(SObjectRef classRef)
    {
        // Method: testNestedLevel3
        // ^self testNestedLevel2
        var bytecodes = new byte[]
        {
            0x70,  // push self (receiver)
            0xD0,  // send literal selector #0 with 0 args
            0x7C   // return top
        };

        var literals = new SObjectRef[]
        {
            ObjectMemory.InternSymbol("testNestedLevel2")
            // Selector "testNestedLevel3" will be added by CreateCompiledMethod
        };

        var method = ObjectMemory.CreateCompiledMethod(
            "testNestedLevel3",
            bytecodes,
            literals,
            0, // no arguments
            0, // no temporaries
            0); // no primitive

        ObjectMemory.AddMethod(classRef.AsObjectPointer(), "testNestedLevel3", method);
    }

    /// <summary>
    /// Adds testNestedLevel2 - second level of nested calls.
    /// Sends testNestedLevel1 to self.
    /// </summary>
    private static void AddNestedLevel2Method(SObjectRef classRef)
    {
        // Method: testNestedLevel2
        // ^self testNestedLevel1
        var bytecodes = new byte[]
        {
            0x70,  // push self (receiver)
            0xD0,  // send literal selector #0 with 0 args
            0x7C   // return top
        };

        var literals = new SObjectRef[]
        {
            ObjectMemory.InternSymbol("testNestedLevel1")
            // Selector "testNestedLevel2" will be added by CreateCompiledMethod
        };

        var method = ObjectMemory.CreateCompiledMethod(
            "testNestedLevel2",
            bytecodes,
            literals,
            0, // no arguments
            0, // no temporaries
            0); // no primitive

        ObjectMemory.AddMethod(classRef.AsObjectPointer(), "testNestedLevel2", method);
    }

    /// <summary>
    /// Adds testNestedLevel1 - third level of nested calls.
    /// Sends testNestedLevel0 to self.
    /// </summary>
    private static void AddNestedLevel1Method(SObjectRef classRef)
    {
        // Method: testNestedLevel1
        // ^self testNestedLevel0
        var bytecodes = new byte[]
        {
            0x70,  // push self (receiver)
            0xD0,  // send literal selector #0 with 0 args
            0x7C   // return top
        };

        var literals = new SObjectRef[]
        {
            ObjectMemory.InternSymbol("testNestedLevel0")
            // Selector "testNestedLevel1" will be added by CreateCompiledMethod
        };

        var method = ObjectMemory.CreateCompiledMethod(
            "testNestedLevel1",
            bytecodes,
            literals,
            0, // no arguments
            0, // no temporaries
            0); // no primitive

        ObjectMemory.AddMethod(classRef.AsObjectPointer(), "testNestedLevel1", method);
    }

    /// <summary>
    /// Adds testNestedLevel0 - deepest level that triggers GC and returns 88.
    /// Calls forceGC to trigger garbage collection, then returns 88.
    /// </summary>
    private static void AddNestedLevel0WithGCMethod(SObjectRef classRef)
    {
        // Method: testNestedLevel0
        // self forceGC.
        // ^88
        var bytecodes = new byte[]
        {
            0x70,  // push self (receiver)
            0xD0,  // send literal selector #0 (forceGC) with 0 args
            0x87,  // pop (discard result)
            0x21,  // push literal constant #1 (88)
            0x7C   // return top
        };

        var literals = new SObjectRef[]
        {
            ObjectMemory.InternSymbol("forceGC"),  // Selector for GC call
            SObjectRef.FromSmallInteger(88)
            // Selector "testNestedLevel0" will be added by CreateCompiledMethod
        };

        var method = ObjectMemory.CreateCompiledMethod(
            "testNestedLevel0",
            bytecodes,
            literals,
            0, // no arguments
            0, // no temporaries
            0); // no primitive

        ObjectMemory.AddMethod(classRef.AsObjectPointer(), "testNestedLevel0", method);
    }

    /// <summary>
    /// Adds forceGC method - primitive 133 that forces garbage collection.
    /// This method has ONLY the primitive, no bytecodes (will return receiver when primitive succeeds).
    /// </summary>
    private static void AddForceGCMethod(SObjectRef classRef)
    {
        // Method: forceGC
        // <primitive: 133>
        // self primitiveFailed
        var bytecodes = new byte[]
        {
            0x70,  // push self (receiver) - only executes if primitive fails
            0x7C   // return top
        };

        var method = ObjectMemory.CreateCompiledMethod(
            "forceGC",
            bytecodes,
            Array.Empty<SObjectRef>(),
            0, // no arguments
            0, // no temporaries
            133); // primitive 133 = force GC

        ObjectMemory.AddMethod(classRef.AsObjectPointer(), "forceGC", method);
    }

    /// <summary>
    /// Creates a test instance of TestClass with value set to 100.
    /// </summary>
    public static SObjectRef CreateTestInstance(SObjectRef testClassRef)
    {
        var instance = ObjectMemory.CreateInstance(
            testClassRef.AsObjectPointer(),
            0); // No indexed slots, just instance variables

        // Set value to 100
        ObjectMemory.SetInstanceVariable(instance.AsObjectPointer(), 0, SObjectRef.FromSmallInteger(100));

        return instance;
    }
}
