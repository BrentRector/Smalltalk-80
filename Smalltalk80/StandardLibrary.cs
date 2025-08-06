using System;
using System.Collections.Generic;

/// <summary>
///     Provides a minimal set of global Smalltalk objects used by the interpreter.
/// </summary>
public static class StandardLibrary
{
    private static readonly Dictionary<string, SmalltalkObject> globals = new();

    /// <summary>
    ///     Initializes the standard library with core classes.
    /// </summary>
    public static void Initialize()
    {
        var objectClass = SystemDictionary.DefineClass("Object");
        globals["Object"] = new SmalltalkObject(objectClass, 0);

        // Define core number classes with primitive arithmetic methods
        InitializeNumberClasses();

        // Define core boolean classes with control flow methods
        InitializeBooleanClasses();
    }

    /// <summary>
    /// Initializes the core number classes and their primitive arithmetic methods.
    /// </summary>
    private static void InitializeNumberClasses()
    {
        // Get Object class (already defined)
        var objectClass = SystemDictionary.GetClass("Object");

        // Define Integer as a subclass of Object
        var integerClass = SystemDictionary.DefineClass("Integer", objectClass);

        // Define SmallInteger as a subclass of Integer  
        var smallIntegerClass = SystemDictionary.DefineClass("SmallInteger", integerClass);

        // Add primitive arithmetic methods to SmallInteger
        AddPrimitiveMethod(smallIntegerClass, "+", 1, 1);       // Add
        AddPrimitiveMethod(smallIntegerClass, "-", 1, 2);       // Subtract  
        AddPrimitiveMethod(smallIntegerClass, "<", 1, 3);       // LessThan
        AddPrimitiveMethod(smallIntegerClass, ">", 1, 4);       // GreaterThan
        AddPrimitiveMethod(smallIntegerClass, "<=", 1, 5);      // LessOrEqual
        AddPrimitiveMethod(smallIntegerClass, ">=", 1, 6);      // GreaterOrEqual
        AddPrimitiveMethod(smallIntegerClass, "=", 1, 7);       // Equals
        AddPrimitiveMethod(smallIntegerClass, "~=", 1, 8);      // NotEqual
        AddPrimitiveMethod(smallIntegerClass, "*", 1, 9);       // Multiply
        AddPrimitiveMethod(smallIntegerClass, "/", 1, 10);      // Divide
        AddPrimitiveMethod(smallIntegerClass, "\\\\", 1, 11);   // Mod
        AddPrimitiveMethod(smallIntegerClass, "//", 1, 12);     // Div

        // Define LargeInteger as a subclass of Integer
        var largeIntegerClass = SystemDictionary.DefineClass("LargeInteger", integerClass);

        globals["Integer"] = new SmalltalkObject(integerClass, 0);
        globals["SmallInteger"] = new SmalltalkObject(smallIntegerClass, 0);
        globals["LargeInteger"] = new SmalltalkObject(largeIntegerClass, 0);
    }

    /// <summary>
    /// Initializes the core boolean classes and their control flow methods.
    /// </summary>
    private static void InitializeBooleanClasses()
    {
        // Get Object class (already defined)
        var objectClass = SystemDictionary.GetClass("Object");

        // Define Boolean as a subclass of Object
        var booleanClass = SystemDictionary.DefineClass("Boolean", objectClass);

        // Define True and False as subclasses of Boolean
        var trueClass = SystemDictionary.DefineClass("True", booleanClass);
        var falseClass = SystemDictionary.DefineClass("False", booleanClass);

        // Add essential control flow methods to Boolean hierarchy
        AddControlFlowMethods(booleanClass, trueClass, falseClass);

        globals["Boolean"] = new SmalltalkObject(booleanClass, 0);
        globals["True"] = new SmalltalkObject(trueClass, 0);
        globals["False"] = new SmalltalkObject(falseClass, 0);
    }

    /// <summary>
    /// Adds essential control flow methods (ifTrue:, ifFalse:, and:, or:) to Boolean classes.
    /// These methods are critical for conditional execution in Smalltalk.
    /// </summary>
    private static void AddControlFlowMethods(SmalltalkClass booleanClass, SmalltalkClass trueClass, SmalltalkClass falseClass)
    {
        // Add ifFalse: to False class (returns the block value)
        AddSimpleMethod(falseClass, "ifFalse:", 1, new byte[] {
            16,   // PushTemporaryVariable0 (the block argument)
            131, 0, 192,  // send: value with: 0 arguments
            124   // ReturnTopFromMethod
        });

        // Add ifFalse: to True class (returns nil)
        AddSimpleMethod(trueClass, "ifFalse:", 1, new byte[] {
            115,  // PushNil  
            124   // ReturnTopFromMethod
        });

        // Add ifTrue: to True class (returns the block value)
        AddSimpleMethod(trueClass, "ifTrue:", 1, new byte[] {
            16,   // PushTemporaryVariable0 (the block argument)
            131, 0, 192,  // send: value with: 0 arguments
            124   // ReturnTopFromMethod
        });

        // Add ifTrue: to False class (returns nil)
        AddSimpleMethod(falseClass, "ifTrue:", 1, new byte[] {
            115,  // PushNil  
            124   // ReturnTopFromMethod
        });

        // Add and: to True class (evaluates the block)
        AddSimpleMethod(trueClass, "and:", 1, new byte[] {
            16,   // PushTemporaryVariable0 (the block argument)
            131, 0, 192,  // send: value with: 0 arguments
            124   // ReturnTopFromMethod
        });

        // Add and: to False class (returns self)
        AddSimpleMethod(falseClass, "and:", 1, new byte[] {
            112,  // PushSelf
            124   // ReturnTopFromMethod
        });
    }

    /// <summary>
    /// Adds a simple bytecode method to the specified class.
    /// </summary>
    private static void AddSimpleMethod(SmalltalkClass targetClass, string selector, int argCount, byte[] bytecodes)
    {
        var header = new MethodHeader(argCount, 0); // No primitives, just bytecode
        var compiledMethod = new CompiledMethod
        {
            Header = header,
            Literals = Array.Empty<object>(),
            Bytecodes = bytecodes,
            HasPrimitive = false,
            PrimitiveIndex = 0
        };

        targetClass.MethodDictionary[new Symbol(selector)] = compiledMethod;
    }

    /// <summary>
    /// Adds a primitive method to the specified class.
    /// </summary>
    private static void AddPrimitiveMethod(SmalltalkClass targetClass, string selector, int argCount, int primitiveIndex)
    {
        // Create method header with primitive index
        // Extension encodes: (argCount << 10) | primitiveIndex  
        var extension = (argCount << 10) | primitiveIndex;
        var header = new MethodHeader(7, extension); // FlagValue = 7 for primitive methods

        // Create simple bytecode that returns top of stack (primitive will handle the work)
        var bytecodes = new[] { (byte)124 }; // ReturnTopFromMethod

        var compiledMethod = new CompiledMethod
        {
            Header = header,
            Literals = Array.Empty<object>(),
            Bytecodes = bytecodes,
            HasPrimitive = true,
            PrimitiveIndex = primitiveIndex
        };

        targetClass.MethodDictionary[new Symbol(selector)] = compiledMethod;
    }

    /// <summary>
    ///     Retrieves a global object by name.
    /// </summary>
    /// <param name="name">The name of the global to fetch.</param>
    /// <returns>The requested global object.</returns>
    public static SmalltalkObject Get(string name) => globals[name];
}