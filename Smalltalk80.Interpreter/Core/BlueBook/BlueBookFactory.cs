namespace Smalltalk80.Interpreter.Core.BlueBook;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Generated.Layouts;

/// <summary>
/// Factory methods for creating Blue Book compliant Smalltalk objects.
/// These methods ensure all objects are created with correct field counts and formats
/// as specified in Smalltalk-80.st.
///
/// CRITICAL: These are the ONLY correct way to create Blue Book objects during cold start.
/// Do not use specialized C# classes directly - they may have incorrect layouts.
///
/// NEW ARCHITECTURE: All methods now return SObjectRef and use ObjectMemoryNew.AllocateObject.
/// </summary>
public static class BlueBookFactory
{
    /// <summary>
    /// Creates a MethodContext with correct Blue Book layout.
    /// Blue Book: sender, pc, stackp, method, receiverMap, receiver + variable (temps + stack)
    /// </summary>
    /// <param name="methodContextClass">The MethodContext class (as SObjectRef).</param>
    /// <param name="method">The CompiledMethod for this context.</param>
    /// <param name="receiver">The receiver of the message.</param>
    /// <param name="tempCount">Number of temporary variables.</param>
    /// <param name="stackSize">Size of the evaluation stack.</param>
    /// <returns>A new MethodContext as SObjectRef.</returns>
    public static SObjectRef CreateMethodContext(
        SObjectRef methodContextClass,
        SObjectRef method,
        SObjectRef receiver,
        int tempCount,
        int stackSize)
    {
        // Calculate total size: 6 fixed fields + temps + stack
        const int FixedFieldCount = 6;
        var totalSize = FixedFieldCount + tempCount + stackSize;

        var oop = ObjectMemoryNew.AllocateObject(
            methodContextClass,
            ObjectFormat.FixedPointer,  // MethodContext is variableSubclass but stores in inst vars
            instanceVarCount: totalSize,
            indexedSize: 0
        );

        STMethodContextCorrect context = SObjectRef.FromObjectPointer(oop);

        // Initialize fixed fields using type-safe layout struct
        context.Sender = SObjectRef.Nil;
        context.InstructionPointer = SObjectRef.FromSmallInteger(0);
        context.StackPointer = SObjectRef.FromSmallInteger(FixedFieldCount + tempCount);
        context.Method = method;
        context.ReceiverMap = SObjectRef.Nil;
        context.Receiver = receiver;

        // Initialize temps and stack to nil
        var contextRef = context.AsRef;
        for (var i = FixedFieldCount; i < totalSize; i++)
        {
            contextRef.SetInstanceVariable(i, SObjectRef.Nil);
        }

        return contextRef;
    }

    /// <summary>
    /// Creates a BlockContext with correct Blue Book layout.
    /// Blue Book: sender, pc, stackp, nargs, startpc, home + variable (copied values + stack)
    /// </summary>
    /// <param name="blockContextClass">The BlockContext class (as SObjectRef).</param>
    /// <param name="home">The home MethodContext.</param>
    /// <param name="nargs">Number of block arguments.</param>
    /// <param name="startpc">Starting program counter.</param>
    /// <param name="stackSize">Size of the evaluation stack.</param>
    /// <returns>A new BlockContext as SObjectRef.</returns>
    public static SObjectRef CreateBlockContext(
        SObjectRef blockContextClass,
        SObjectRef home,
        int nargs,
        int startpc,
        int stackSize)
    {
        // Calculate total size: 6 fixed fields + stack
        const int FixedFieldCount = 6;
        var totalSize = FixedFieldCount + stackSize;

        var oop = ObjectMemoryNew.AllocateObject(
            blockContextClass,
            ObjectFormat.FixedPointer,
            instanceVarCount: totalSize,
            indexedSize: 0
        );

        STBlockContextCorrect context = SObjectRef.FromObjectPointer(oop);

        // Initialize fixed fields using type-safe layout struct
        context.Sender = SObjectRef.Nil;
        context.InstructionPointer = SObjectRef.FromSmallInteger(startpc);
        context.StackPointer = SObjectRef.FromSmallInteger(FixedFieldCount);
        context.Nargs = SObjectRef.FromSmallInteger(nargs);
        context.StartPC = SObjectRef.FromSmallInteger(startpc);
        context.Home = home;

        // Initialize stack to nil
        var contextRef = context.AsRef;
        for (var i = FixedFieldCount; i < totalSize; i++)
        {
            contextRef.SetInstanceVariable(i, SObjectRef.Nil);
        }

        return contextRef;
    }

    /// <summary>
    /// Creates a Process with correct Blue Book layout.
    /// Blue Book: nextLink (from Link), suspendedContext, priority, myList
    /// </summary>
    /// <param name="processClass">The Process class (as SObjectRef).</param>
    /// <param name="suspendedContext">The suspended context.</param>
    /// <param name="priority">Process priority level.</param>
    /// <returns>A new Process as SObjectRef.</returns>
    public static SObjectRef CreateProcess(
        SObjectRef processClass,
        SObjectRef suspendedContext,
        int priority)
    {
        var oop = ObjectMemoryNew.AllocateObject(
            processClass,
            ObjectFormat.FixedPointer,
            instanceVarCount: STProcess.InstanceVarCount,
            indexedSize: 0
        );

        // Use type-safe wrapper
        STProcess process = SObjectRef.FromObjectPointer(oop);

        // Initialize fields
        process.NextLink = SObjectRef.Nil;
        process.SuspendedContext = suspendedContext;
        process.Priority = SObjectRef.FromSmallInteger(priority);
        process.MyList = SObjectRef.Nil;

        return process;
    }

    /// <summary>
    /// Creates a Semaphore with correct Blue Book layout.
    /// Blue Book: firstLink, lastLink (from LinkedList), excessSignals
    /// </summary>
    /// <param name="semaphoreClass">The Semaphore class (as SObjectRef).</param>
    /// <param name="initialSignals">Initial signal count.</param>
    /// <returns>A new Semaphore as SObjectRef.</returns>
    public static SObjectRef CreateSemaphore(
        SObjectRef semaphoreClass,
        int initialSignals = 0)
    {
        var oop = ObjectMemoryNew.AllocateObject(
            semaphoreClass,
            ObjectFormat.FixedPointer,
            instanceVarCount: STSemaphore.InstanceVarCount,
            indexedSize: 0
        );

        STSemaphore semaphore = SObjectRef.FromObjectPointer(oop);

        // Initialize fields using type-safe layout struct
        semaphore.FirstLink = SObjectRef.Nil;
        semaphore.LastLink = SObjectRef.Nil;
        semaphore.ExcessSignals = SObjectRef.FromSmallInteger(initialSignals);

        return semaphore.AsRef;
    }

    /// <summary>
    /// Creates a Dictionary with correct Blue Book layout.
    /// Blue Book: tally (from Set) + variable part for Association objects
    /// </summary>
    /// <param name="dictionaryClass">The Dictionary class (as SObjectRef).</param>
    /// <param name="arrayClass">The Array class for internal storage (ignored - for compatibility).</param>
    /// <param name="initialCapacity">Initial capacity for associations.</param>
    /// <returns>A new Dictionary as SObjectRef.</returns>
    public static SObjectRef CreateDictionary(
        SObjectRef dictionaryClass,
        SObjectRef arrayClass,
        int initialCapacity)
    {
        // Array class parameter is ignored - included for test compatibility
        return CreateDictionary(dictionaryClass, initialCapacity);
    }

    /// <summary>
    /// Creates a Dictionary with correct Blue Book layout.
    /// Blue Book: tally (from Set) + variable part for Association objects
    /// </summary>
    /// <param name="dictionaryClass">The Dictionary class (as SObjectRef).</param>
    /// <param name="initialCapacity">Initial capacity for associations.</param>
    /// <returns>A new Dictionary as SObjectRef.</returns>
    public static SObjectRef CreateDictionary(
        SObjectRef dictionaryClass,
        int initialCapacity = 16)
    {
        var oop = ObjectMemoryNew.AllocateObject(
            dictionaryClass,
            ObjectFormat.PointerIndexed,
            instanceVarCount: STDictionary.InstanceVarCount,
            indexedSize: initialCapacity
        );

        // Use type-safe wrapper
        STDictionary dictionary = SObjectRef.FromObjectPointer(oop);

        // Initialize tally to 0
        dictionary.Tally = SObjectRef.FromSmallInteger(0);

        // Initialize all slots to nil (still need direct access for indexed parts)
        var dictRef = (SObjectRef)dictionary;
        for (var i = 0; i < initialCapacity; i++)
        {
            dictRef.SetIndexedPointer(i, SObjectRef.Nil);
        }

        return dictionary;
    }

    /// <summary>
    /// Creates an Association with correct Blue Book layout.
    /// Blue Book: key (from LookupKey), value
    /// </summary>
    /// <param name="associationClass">The Association class (as SObjectRef).</param>
    /// <param name="key">The association key.</param>
    /// <param name="value">The association value.</param>
    /// <returns>A new Association as SObjectRef.</returns>
    public static SObjectRef CreateAssociation(
        SObjectRef associationClass,
        SObjectRef key,
        SObjectRef value)
    {
        var oop = ObjectMemoryNew.AllocateObject(
            associationClass,
            ObjectFormat.FixedPointer,
            instanceVarCount: STAssociation.InstanceVarCount,
            indexedSize: 0
        );

        // Use type-safe wrapper
        STAssociation association = SObjectRef.FromObjectPointer(oop);
        association.Key = key;
        association.Value = value;

        return association;
    }

    /// <summary>
    /// Creates a Character with correct Blue Book layout.
    /// Blue Book: value (SmallInteger ASCII code)
    /// </summary>
    /// <param name="characterClass">The Character class (as SObjectRef).</param>
    /// <param name="asciiValue">ASCII value of the character.</param>
    /// <returns>A new Character as SObjectRef.</returns>
    public static SObjectRef CreateCharacter(
        SObjectRef characterClass,
        int asciiValue)
    {
        var oop = ObjectMemoryNew.AllocateObject(
            characterClass,
            ObjectFormat.FixedPointer,
            instanceVarCount: STCharacter.InstanceVarCount,
            indexedSize: 0
        );

        // Use type-safe wrapper
        STCharacter character = SObjectRef.FromObjectPointer(oop);
        character.Value = SObjectRef.FromSmallInteger(asciiValue);

        return character;
    }

    /// <summary>
    /// Creates an Array with correct Blue Book layout.
    /// Blue Book: no instance variables, only indexed pointer slots
    /// </summary>
    /// <param name="arrayClass">The Array class (as SObjectRef).</param>
    /// <param name="size">Number of indexed elements.</param>
    /// <returns>A new Array as SObjectRef.</returns>
    public static SObjectRef CreateArray(
        SObjectRef arrayClass,
        int size)
    {
        var oop = ObjectMemoryNew.AllocateObject(
            arrayClass,
            ObjectFormat.PointerIndexed,
            instanceVarCount: STArray.InstanceVarCount,  // 0
            indexedSize: size
        );

        var array = SObjectRef.FromObjectPointer(oop);

        // Initialize all elements to nil
        for (var i = 0; i < size; i++)
        {
            array.SetIndexedPointer(i, SObjectRef.Nil);
        }

        return array;
    }

    /// <summary>
    /// Creates a String with correct Blue Book layout.
    /// Blue Book: no instance variables, only indexed byte slots
    /// </summary>
    /// <param name="stringClass">The String class (as SObjectRef).</param>
    /// <param name="length">Length of the string in bytes.</param>
    /// <returns>A new String as SObjectRef.</returns>
    public static SObjectRef CreateString(
        SObjectRef stringClass,
        int length)
    {
        var oop = ObjectMemoryNew.AllocateObject(
            stringClass,
            ObjectFormat.ByteIndexed,
            instanceVarCount: STString.InstanceVarCount,  // 0
            indexedSize: length
        );

        return SObjectRef.FromObjectPointer(oop);
    }

    /// <summary>
    /// Creates a String from a C# string.
    /// </summary>
    /// <param name="stringClass">The String class (as SObjectRef).</param>
    /// <param name="text">The C# string to convert.</param>
    /// <returns>A new String as SObjectRef.</returns>
    public static SObjectRef CreateStringFromCSharp(
        SObjectRef stringClass,
        string text)
    {
        var str = CreateString(stringClass, text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            str.SetIndexedByte(i, (byte)text[i]);
        }

        return str;
    }

    /// <summary>
    /// Creates a Symbol with correct Blue Book layout.
    /// Blue Book: String variableByteSubclass, no instance variables
    /// </summary>
    /// <param name="symbolClass">The Symbol class (as SObjectRef).</param>
    /// <param name="symbolString">The symbol's string representation.</param>
    /// <returns>A new Symbol as SObjectRef.</returns>
    public static SObjectRef CreateSymbol(
        SObjectRef symbolClass,
        string symbolString)
    {
        var oop = ObjectMemoryNew.AllocateObject(
            symbolClass,
            ObjectFormat.ByteIndexed,
            instanceVarCount: STSymbol.InstanceVarCount,  // 0
            indexedSize: symbolString.Length
        );

        var symbol = SObjectRef.FromObjectPointer(oop);

        for (var i = 0; i < symbolString.Length; i++)
        {
            symbol.SetIndexedByte(i, (byte)symbolString[i]);
        }

        return symbol;
    }

    /// <summary>
    /// Creates a ByteArray with correct Blue Book layout.
    /// Blue Book: no instance variables, only indexed byte slots
    /// </summary>
    /// <param name="byteArrayClass">The ByteArray class (as SObjectRef).</param>
    /// <param name="size">Number of bytes.</param>
    /// <returns>A new ByteArray as SObjectRef.</returns>
    public static SObjectRef CreateByteArray(
        SObjectRef byteArrayClass,
        int size)
    {
        var oop = ObjectMemoryNew.AllocateObject(
            byteArrayClass,
            ObjectFormat.ByteIndexed,
            instanceVarCount: STByteArray.InstanceVarCount,  // 0
            indexedSize: size
        );

        return SObjectRef.FromObjectPointer(oop);
    }

    /// <summary>
    /// Creates a Behavior with correct Blue Book layout.
    /// Blue Book: superclass, methodDict, format, subclasses
    /// </summary>
    /// <param name="behaviorClass">The Behavior class (as SObjectRef).</param>
    /// <param name="superclass">The superclass reference.</param>
    /// <param name="methodDict">The method dictionary.</param>
    /// <param name="format">The format specification.</param>
    /// <returns>A new Behavior as SObjectRef.</returns>
    public static SObjectRef CreateBehavior(
        SObjectRef behaviorClass,
        SObjectRef superclass,
        SObjectRef methodDict,
        SObjectRef format)
    {
        var oop = ObjectMemoryNew.AllocateObject(
            behaviorClass,
            ObjectFormat.FixedPointer,
            instanceVarCount: STBehavior.InstanceVarCount,
            indexedSize: 0
        );

        // Use type-safe wrapper
        STBehavior behavior = SObjectRef.FromObjectPointer(oop);

        behavior.Superclass = superclass;
        behavior.MethodDict = methodDict;
        behavior.Format = format;
        behavior.Subclasses = SObjectRef.Nil;

        return behavior;
    }

    /// <summary>
    /// Creates a Class with correct Blue Book layout.
    /// Blue Book: superclass, methodDict, format, subclasses, instanceVariables, organization, name, classPool, sharedPools
    /// </summary>
    /// <param name="classClass">The Class class (as SObjectRef).</param>
    /// <param name="superclass">The superclass reference.</param>
    /// <param name="name">The class name (Symbol).</param>
    /// <param name="instanceVariables">Instance variable names (String).</param>
    /// <returns>A new Class as SObjectRef.</returns>
    public static SObjectRef CreateClass(
        SObjectRef classClass,
        SObjectRef superclass,
        SObjectRef name,
        SObjectRef instanceVariables)
    {
        var oop = ObjectMemoryNew.AllocateObject(
            classClass,
            ObjectFormat.FixedPointer,
            instanceVarCount: STClass.InstanceVarCount,
            indexedSize: 0
        );

        // Use type-safe wrapper
        STClass cls = SObjectRef.FromObjectPointer(oop);

        // Inherited from Behavior
        cls.Superclass = superclass;
        cls.MethodDict = SObjectRef.Nil;  // Set later
        cls.Format = SObjectRef.FromSmallInteger(0);  // Set later
        cls.Subclasses = SObjectRef.Nil;

        // Inherited from ClassDescription
        cls.InstanceVariables = instanceVariables;
        cls.Organization = SObjectRef.Nil;

        // Class fields
        cls.Name = name;
        cls.ClassPool = SObjectRef.Nil;
        cls.SharedPools = SObjectRef.Nil;

        return cls;
    }

    /// <summary>
    /// Creates a CompiledMethod with correct Blue Book layout.
    /// Blue Book: CompiledMethod is variableByteSubclass with literals in instance variables
    /// and bytecodes in indexed portion.
    /// </summary>
    /// <param name="compiledMethodClass">The CompiledMethod class (as SObjectRef).</param>
    /// <param name="header">The method header (as SmallInteger).</param>
    /// <param name="literals">The literal array.</param>
    /// <param name="bytecodes">The bytecode array.</param>
    /// <returns>A new CompiledMethod as SObjectRef.</returns>
    public static SObjectRef CreateCompiledMethod(
        SObjectRef compiledMethodClass,
        SObjectRef header,
        SObjectRef[] literals,
        byte[] bytecodes)
    {
        // CompiledMethod stores literals as instance variables and bytecodes as indexed bytes
        var oop = ObjectMemoryNew.AllocateObject(
            compiledMethodClass,
            ObjectFormat.CompiledMethod,
            instanceVarCount: literals.Length,  // Literals are stored as instance variables
            indexedSize: bytecodes.Length + 2   // +2 for header bytes
        );

        var method = SObjectRef.FromObjectPointer(oop);

        // Set literals as instance variables
        for (var i = 0; i < literals.Length; i++)
        {
            method.SetInstanceVariable(i, literals[i]);
        }

        // Encode header as first two bytes (little-endian)
        var headerValue = header.AsSmallInteger();
        method.SetIndexedByte(0, (byte)(headerValue & 0xFF));
        method.SetIndexedByte(1, (byte)((headerValue >> 8) & 0xFF));

        // Copy bytecodes
        for (var i = 0; i < bytecodes.Length; i++)
        {
            method.SetIndexedByte(i + 2, bytecodes[i]);
        }

        return method;
    }
}