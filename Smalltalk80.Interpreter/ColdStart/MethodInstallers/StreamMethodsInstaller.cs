namespace Smalltalk80.Interpreter.ColdStart.MethodInstallers;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Generated.Layouts;

/// <summary>
/// Installs kernel methods for stream-related classes including Stream, 
/// PositionableStream, ReadStream, and WriteStream.
/// </summary>
public static class StreamMethodsInstaller
{
    /// <summary>
    /// Installs all stream-related methods.
    /// </summary>
    public static void Install()
    {
        InstallStreamMethods();
        InstallPositionableStreamMethods();
        InstallWriteStreamMethods();
        InstallReadStreamMethods();
    }
    
    /// <summary>
    /// Installs Stream methods.
    /// </summary>
    private static void InstallStreamMethods()
    {
        var streamClassRef = SystemDictionary.StreamClassRef;
        if (streamClassRef.IsNil) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Stream instance methods: #next, #next:, #nextPut:, #nextPutAll:, #atEnd");
        }
        
        // Abstract methods - subclasses must implement
        // next
        var method1 = ObjectMemoryNew.CreateCompiledMethod(
            "next",
            [0x70, 0x20, 0xD3, 0x7C], // self subclassResponsibility
            [ObjectMemoryNew.InternSymbol("subclassResponsibility")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(streamClassRef.AsObjectPointer(), "next", method1);
        instanceMethodCount++;

        // nextPut:
        var method2 = ObjectMemoryNew.CreateCompiledMethod(
            "nextPut:",
            [0x70, 0x20, 0xD3, 0x7C], // self subclassResponsibility
            [ObjectMemoryNew.InternSymbol("subclassResponsibility")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(streamClassRef.AsObjectPointer(), "nextPut:", method2);
        instanceMethodCount++;

        // atEnd
        var method3 = ObjectMemoryNew.CreateCompiledMethod(
            "atEnd",
            [0x70, 0x20, 0xD3, 0x7C], // self subclassResponsibility
            [ObjectMemoryNew.InternSymbol("subclassResponsibility")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(streamClassRef.AsObjectPointer(), "atEnd", method3);
        instanceMethodCount++;

        // nextPutAll:
        var method4 = ObjectMemoryNew.CreateCompiledMethod(
            "nextPutAll:",
            [0x10, 0x8F, 0x00, 0x02, 0x70, 0x11, 0xE1, 0xF7, 0x10, 0x7C], // aCollection do: [:each | self nextPut: each]. ^aCollection
            [ObjectMemoryNew.InternSymbol("do:"), ObjectMemoryNew.InternSymbol("nextPut:")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(streamClassRef.AsObjectPointer(), "nextPutAll:", method4);
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Stream: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs PositionableStream methods.
    /// </summary>
    private static void InstallPositionableStreamMethods()
    {
        var positionableStreamClassRef = SystemDictionary.PositionableStreamClassRef;
        if (positionableStreamClassRef.IsNil) return;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] PositionableStream: 0 instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs WriteStream methods.
    /// </summary>
    private static void InstallWriteStreamMethods()
    {
        var writeStreamClassRef = SystemDictionary.WriteStreamClassRef;
        if (writeStreamClassRef.IsNil) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing WriteStream instance methods: #nextPut:, #nextPutAll:, #contents, #position, #space, #cr, #tab");
        }
        
        // nextPut:
        var method5 = ObjectMemoryNew.CreateCompiledMethod(
            "nextPut:",
            [0x00, 0x01, 0x76, 0xB0, 0x60, 0x01, 0x10, 0xC1, 0x60, 0x76, 0xB0, 0x60, 0x10, 0x7C],
            [ObjectMemoryNew.InternSymbol("collection"), ObjectMemoryNew.InternSymbol("position"), ObjectMemoryNew.InternSymbol("at:put:")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(writeStreamClassRef.AsObjectPointer(), "nextPut:", method5);
        instanceMethodCount++;

        // position - return current position
        var method6 = ObjectMemoryNew.CreateCompiledMethod(
            "position",
            [0x01, 0x7C], // ^position
            [ObjectMemoryNew.InternSymbol("position")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(writeStreamClassRef.AsObjectPointer(), "position", method6);
        instanceMethodCount++;

        // space - write a space character
        var method7 = ObjectMemoryNew.CreateCompiledMethod(
            "space",
            [0x70, 0x42, 0x20, 0xCC, 0xE1, 0x7C], // self nextPut: (Character value: 32)
            [ObjectMemoryNew.InternSymbol("Character"),
             SObjectRef.FromSmallInteger(32),
             ObjectMemoryNew.InternSymbol("value:"),
             ObjectMemoryNew.InternSymbol("nextPut:")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(writeStreamClassRef.AsObjectPointer(), "space", method7);
        instanceMethodCount++;

        // cr - write a carriage return
        var method8 = ObjectMemoryNew.CreateCompiledMethod(
            "cr",
            [0x70, 0x42, 0x20, 0xCC, 0xE1, 0x7C], // self nextPut: (Character value: 13)
            [ObjectMemoryNew.InternSymbol("Character"),
             SObjectRef.FromSmallInteger(13),
             ObjectMemoryNew.InternSymbol("value:"),
             ObjectMemoryNew.InternSymbol("nextPut:")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(writeStreamClassRef.AsObjectPointer(), "cr", method8);
        instanceMethodCount++;

        // tab - write a tab character
        var method9 = ObjectMemoryNew.CreateCompiledMethod(
            "tab",
            [0x70, 0x42, 0x20, 0xCC, 0xE1, 0x7C], // self nextPut: (Character value: 9)
            [ObjectMemoryNew.InternSymbol("Character"),
             SObjectRef.FromSmallInteger(9),
             ObjectMemoryNew.InternSymbol("value:"),
             ObjectMemoryNew.InternSymbol("nextPut:")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(writeStreamClassRef.AsObjectPointer(), "tab", method9);
        instanceMethodCount++;

        // contents
        var method10 = ObjectMemoryNew.CreateCompiledMethod(
            "contents",
            [0x00, 0x76, 0x01, 0xF0, 0x7C], // collection copyFrom: 1 to: position
            [ObjectMemoryNew.InternSymbol("collection"), ObjectMemoryNew.InternSymbol("position"), ObjectMemoryNew.InternSymbol("copyFrom:to:")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(writeStreamClassRef.AsObjectPointer(), "contents", method10);
        instanceMethodCount++;
        
        // Note: Class methods would be installed on WriteStream's metaclass
        // They would include: #on:
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] WriteStream: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs ReadStream methods.
    /// </summary>
    private static void InstallReadStreamMethods()
    {
        var readStreamClassRef = SystemDictionary.ReadStreamClassRef;
        if (readStreamClassRef.IsNil) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing ReadStream instance methods: #next, #peek, #position, #position:, #skip:, #upTo:, #atEnd");
        }
        
        // next - read and advance position
        var method11 = ObjectMemoryNew.CreateCompiledMethod(
            "next",
            [0x01, 0x01, 0x76, 0xB0, 0x60, // position := position + 1
             0x00, 0x01, 0xC1, 0x7C], // ^collection at: position
            [ObjectMemoryNew.InternSymbol("collection"),
             ObjectMemoryNew.InternSymbol("position"),
             ObjectMemoryNew.InternSymbol("at:")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(readStreamClassRef.AsObjectPointer(), "next", method11);
        instanceMethodCount++;

        // peek - look at next element without advancing
        var method12 = ObjectMemoryNew.CreateCompiledMethod(
            "peek",
            [0x01, 0x00, 0xC2, 0xB5, 0x9D, 0x02, 0x73, 0x7C, // position >= collection size ifTrue: [^nil]
             0x00, 0x01, 0x76, 0xB0, 0xC1, 0x7C], // ^collection at: position + 1
            [ObjectMemoryNew.InternSymbol("collection"),
             ObjectMemoryNew.InternSymbol("position"),
             ObjectMemoryNew.InternSymbol("size"),
             ObjectMemoryNew.InternSymbol(">="),
             ObjectMemoryNew.InternSymbol("at:")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(readStreamClassRef.AsObjectPointer(), "peek", method12);
        instanceMethodCount++;

        // position - return current position
        var method13 = ObjectMemoryNew.CreateCompiledMethod(
            "position",
            [0x01, 0x7C], // ^position
            [ObjectMemoryNew.InternSymbol("position")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(readStreamClassRef.AsObjectPointer(), "position", method13);
        instanceMethodCount++;

        // position: - set position
        var method14 = ObjectMemoryNew.CreateCompiledMethod(
            "position:",
            [0x10, 0x61, 0x10, 0x7C], // position := anInteger. ^anInteger
            [ObjectMemoryNew.InternSymbol("position")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(readStreamClassRef.AsObjectPointer(), "position:", method14);
        instanceMethodCount++;

        // skip: - advance position by n
        var method15 = ObjectMemoryNew.CreateCompiledMethod(
            "skip:",
            [0x01, 0x10, 0xB0, 0x61, 0x70, 0x7C], // position := position + n. ^self
            [ObjectMemoryNew.InternSymbol("position")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(readStreamClassRef.AsObjectPointer(), "skip:", method15);
        instanceMethodCount++;

        // upTo: - read elements up to a delimiter
        var method16 = ObjectMemoryNew.CreateCompiledMethod(
            "upTo:",
            [0x41, 0xD0, 0x68, // result := OrderedCollection new
             0x70, 0xD2, 0x9C, 0xAC, 0x12, // [self atEnd not] whileTrue:
             0x70, 0xD3, 0x69, // element := self next
             0x69, 0x10, 0xB6, 0x99, 0x02, 0x68, 0x7C, // element = delimiter ifTrue: [^result]
             0x68, 0x69, 0xD2, 0xA3, 0xEB, // result add: element. loop
             0x68, 0x7C], // ^result
            [ObjectMemoryNew.InternSymbol("OrderedCollection"),
             ObjectMemoryNew.InternSymbol("new"),
             ObjectMemoryNew.InternSymbol("atEnd"),
             ObjectMemoryNew.InternSymbol("next"),
             ObjectMemoryNew.InternSymbol("="),
             ObjectMemoryNew.InternSymbol("add:")],
            1, 2, 0);
        ObjectMemoryNew.AddMethod(readStreamClassRef.AsObjectPointer(), "upTo:", method16);
        instanceMethodCount++;

        // atEnd - check if at end of stream
        var method17 = ObjectMemoryNew.CreateCompiledMethod(
            "atEnd",
            [0x01, 0x00, 0xC2, 0xB5, 0x7C], // ^position >= collection size
            [ObjectMemoryNew.InternSymbol("position"),
             ObjectMemoryNew.InternSymbol("collection"),
             ObjectMemoryNew.InternSymbol("size"),
             ObjectMemoryNew.InternSymbol(">=")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(readStreamClassRef.AsObjectPointer(), "atEnd", method17);
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] ReadStream: {instanceMethodCount} instance methods installed");
        }
    }
}
