namespace Smalltalk80.Interpreter.ColdStart;

using Smalltalk80.Interpreter.ColdStart.MethodInstallers;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Generated.Layouts;

/// <summary>
/// Installs core kernel methods during cold start for all system classes.
/// These are the fundamental methods needed for a functioning Smalltalk system.
/// </summary>
public static class KernelMethodsInstaller
{
    /// <summary>
    /// Installs all kernel methods for system classes.
    /// </summary>
    public static void InstallAll()
    {
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing kernel methods for all system classes...");
        }

        // Install methods using specialized installers
        ObjectMethodsInstaller.Install();
        BooleanMethodsInstaller.Install();
        NumericMethodsInstaller.Install();
        CollectionMethodsInstaller.Install();
        BehaviorMethodsInstaller.Install();
        ProcessMethodsInstaller.Install();
        StreamMethodsInstaller.Install();
        ContextMethodsInstaller.Install();
        
        // Install class-side methods for compilation support
        InstallClassSideMethods();
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Kernel methods installation complete");
        }
    }
    
    /// <summary>
    /// Installs class-side methods for classes that need them for compilation.
    /// Since metaclasses aren't fully implemented, we install these on the class itself for now.
    /// </summary>
    private static void InstallClassSideMethods()
    {
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing class-side methods for compilation support...");
        }

        var instanceMethodCount = 0;

        // Install class methods for Scanner
        var scannerClassRef = SystemDictionary.ScannerClassRef;
        if (!scannerClassRef.IsNil)
        {
            var method1 = ObjectMemoryNew.CreateCompiledMethod(
                "new",
                [0x70, 0xCB, 0x7C], // self basicNew
                [ObjectMemoryNew.InternSymbol("basicNew")],
                0, 0, 0);
            ObjectMemoryNew.AddMethod(scannerClassRef.AsObjectPointer(), "new", method1);
            instanceMethodCount++;
        }

        // Install class methods for Parser
        var parserClassRef = SystemDictionary.ParserClassRef;
        if (!parserClassRef.IsNil)
        {
            var method2 = ObjectMemoryNew.CreateCompiledMethod(
                "new",
                [0x70, 0xCB, 0x7C], // self basicNew
                [ObjectMemoryNew.InternSymbol("basicNew")],
                0, 0, 0);
            ObjectMemoryNew.AddMethod(parserClassRef.AsObjectPointer(), "new", method2);
            instanceMethodCount++;
        }

        // Install class methods for Encoder
        var encoderClassRef = SystemDictionary.EncoderClassRef;
        if (!encoderClassRef.IsNil)
        {
            var method3 = ObjectMemoryNew.CreateCompiledMethod(
                "new",
                [0x70, 0xCB, 0x7C], // self basicNew
                [ObjectMemoryNew.InternSymbol("basicNew")],
                0, 0, 0);
            ObjectMemoryNew.AddMethod(encoderClassRef.AsObjectPointer(), "new", method3);
            instanceMethodCount++;
        }

        // Install class methods for Compiler
        var compilerClassRef = SystemDictionary.CompilerClassRef;
        if (!compilerClassRef.IsNil)
        {
            var method4 = ObjectMemoryNew.CreateCompiledMethod(
                "new",
                [0x70, 0xCB, 0x7C], // self basicNew
                [ObjectMemoryNew.InternSymbol("basicNew")],
                0, 0, 0);
            ObjectMemoryNew.AddMethod(compilerClassRef.AsObjectPointer(), "new", method4);
            instanceMethodCount++;
        }
        
        // Install instance methods for Collection
        var collectionClassRef = SystemDictionary.CollectionClassRef;
        if (!collectionClassRef.IsNil)
        {
            // size - basic size method
            var method5 = ObjectMemoryNew.CreateCompiledMethod(
                "size",
                [0x75, 0x68, // tally := 0
                 0x70, 0x8F, 0x00, 0x02, 0x68, 0x76, 0xB0, 0x69, 0xF7, // self do: [:each | tally := tally + 1]
                 0x68, 0x7C], // ^tally
                [ObjectMemoryNew.InternSymbol("do:"),
                 SObjectRef.FromSmallInteger(1)],
                0, 1, 0);
            ObjectMemoryNew.AddMethod(collectionClassRef.AsObjectPointer(), "size", method5);
            instanceMethodCount++;

            // isEmpty
            var method6 = ObjectMemoryNew.CreateCompiledMethod(
                "isEmpty",
                [0x70, 0xC0, 0x75, 0xB6, 0x7C], // ^self size = 0
                [ObjectMemoryNew.InternSymbol("size"),
                 SObjectRef.FromSmallInteger(0),
                 ObjectMemoryNew.InternSymbol("=")],
                0, 0, 0);
            ObjectMemoryNew.AddMethod(collectionClassRef.AsObjectPointer(), "isEmpty", method6);
            instanceMethodCount++;

            // do: - abstract, subclasses must override
            var method7 = ObjectMemoryNew.CreateCompiledMethod(
                "do:",
                [0x70, 0x21, 0xE0, 0x7C], // self subclassResponsibility
                [ObjectMemoryNew.InternSymbol("subclassResponsibility")],
                1, 0, 0);
            ObjectMemoryNew.AddMethod(collectionClassRef.AsObjectPointer(), "do:", method7);
            instanceMethodCount++;
        }
        
        // Install instance methods for SequenceableCollection
        var sequenceableCollectionClassRef = SystemDictionary.SequenceableCollectionClassRef;
        if (!sequenceableCollectionClassRef.IsNil)
        {
            // first
            var method8 = ObjectMemoryNew.CreateCompiledMethod(
                "first",
                [0x70, 0x76, 0xC0, 0x7C], // ^self at: 1
                [SObjectRef.FromSmallInteger(1),
                 ObjectMemoryNew.InternSymbol("at:")],
                0, 0, 0);
            ObjectMemoryNew.AddMethod(sequenceableCollectionClassRef.AsObjectPointer(), "first", method8);
            instanceMethodCount++;

            // last
            var method9 = ObjectMemoryNew.CreateCompiledMethod(
                "last",
                [0x70, 0x70, 0xC0, 0xC1, 0x7C], // ^self at: self size
                [ObjectMemoryNew.InternSymbol("size"),
                 ObjectMemoryNew.InternSymbol("at:")],
                0, 0, 0);
            ObjectMemoryNew.AddMethod(sequenceableCollectionClassRef.AsObjectPointer(), "last", method9);
            instanceMethodCount++;

            // at:ifAbsent:
            var method10 = ObjectMemoryNew.CreateCompiledMethod(
                "at:ifAbsent:",
                [0x10, 0x76, 0xB2, 0x9B, 0x08, // index < 1 ifTrue:
                 0x10, 0x70, 0xC0, 0xB4, 0x99, 0x03, // index > self size ifTrue:
                 0x11, 0xC9, 0x7C, // ^exceptionBlock value
                 0x70, 0x10, 0xC0, 0x7C], // ^self at: index
                [SObjectRef.FromSmallInteger(1),
                 ObjectMemoryNew.InternSymbol("<"),
                 ObjectMemoryNew.InternSymbol("size"),
                 ObjectMemoryNew.InternSymbol(">"),
                 ObjectMemoryNew.InternSymbol("value"),
                 ObjectMemoryNew.InternSymbol("at:")],
                2, 0, 0);
            ObjectMemoryNew.AddMethod(sequenceableCollectionClassRef.AsObjectPointer(), "at:ifAbsent:", method10);
            instanceMethodCount++;
        }
        
        // Install instance methods for OrderedCollection
        var orderedCollectionClassRef = SystemDictionary.OrderedCollectionClassRef;
        if (!orderedCollectionClassRef.IsNil)
        {
            // add:
            var method11 = ObjectMemoryNew.CreateCompiledMethod(
                "add:",
                [0x70, 0x10, 0xE0, 0x10, 0x7C], // self addLast: anObject. ^anObject
                [ObjectMemoryNew.InternSymbol("addLast:")],
                1, 0, 0);
            ObjectMemoryNew.AddMethod(orderedCollectionClassRef.AsObjectPointer(), "add:", method11);
            instanceMethodCount++;

            // addLast:
            var method12 = ObjectMemoryNew.CreateCompiledMethod(
                "addLast:",
                [0x00, 0x01, 0x76, 0xB0, 0x60, // lastIndex := lastIndex + 1
                 0x00, 0x01, 0x10, 0xE1, 0x10, 0x7C], // self at: lastIndex put: anObject. ^anObject
                [ObjectMemoryNew.InternSymbol("lastIndex"),
                 ObjectMemoryNew.InternSymbol("at:put:")],
                1, 0, 0);
            ObjectMemoryNew.AddMethod(orderedCollectionClassRef.AsObjectPointer(), "addLast:", method12);
            instanceMethodCount++;

            // addAll:
            var method13 = ObjectMemoryNew.CreateCompiledMethod(
                "addAll:",
                [0x10, 0x8F, 0x00, 0x02, 0x70, 0x11, 0xE0, 0xF7, 0x10, 0x7C], // aCollection do: [:each | self add: each]. ^aCollection
                [ObjectMemoryNew.InternSymbol("do:"),
                 ObjectMemoryNew.InternSymbol("add:")],
                1, 0, 0);
            ObjectMemoryNew.AddMethod(orderedCollectionClassRef.AsObjectPointer(), "addAll:", method13);
            instanceMethodCount++;

            // isEmpty
            var method14 = ObjectMemoryNew.CreateCompiledMethod(
                "isEmpty",
                [0x00, 0x01, 0xB6, 0x7C], // firstIndex > lastIndex
                [ObjectMemoryNew.InternSymbol("firstIndex"),
                 ObjectMemoryNew.InternSymbol("lastIndex"),
                 ObjectMemoryNew.InternSymbol(">")],
                0, 0, 0);
            ObjectMemoryNew.AddMethod(orderedCollectionClassRef.AsObjectPointer(), "isEmpty", method14);
            instanceMethodCount++;

            // first
            var method15 = ObjectMemoryNew.CreateCompiledMethod(
                "first",
                [0x70, 0x00, 0xC0, 0x7C], // self at: firstIndex
                [ObjectMemoryNew.InternSymbol("firstIndex"),
                 ObjectMemoryNew.InternSymbol("at:")],
                0, 0, 0);
            ObjectMemoryNew.AddMethod(orderedCollectionClassRef.AsObjectPointer(), "first", method15);
            instanceMethodCount++;

            // last
            var method16 = ObjectMemoryNew.CreateCompiledMethod(
                "last",
                [0x70, 0x00, 0xC0, 0x7C], // self at: lastIndex
                [ObjectMemoryNew.InternSymbol("lastIndex"),
                 ObjectMemoryNew.InternSymbol("at:")],
                0, 0, 0);
            ObjectMemoryNew.AddMethod(orderedCollectionClassRef.AsObjectPointer(), "last", method16);
            instanceMethodCount++;
        }
        
        // Install instance methods for Set
        var setClassRef = SystemDictionary.SetClassRef;
        if (!setClassRef.IsNil)
        {
            // add:
            var method17 = ObjectMemoryNew.CreateCompiledMethod(
                "add:",
                [0x70, 0x10, 0xE0, 0x68, // index := self findElementOrNil: anObject
                 0x00, 0x68, 0xC1, 0x73, 0xB6, 0x9D, 0x05, // (self at: index) isNil ifTrue:
                 0x00, 0x68, 0x10, 0xE1, // [self at: index put: anObject]
                 0x10, 0x7C], // ^anObject
                [ObjectMemoryNew.InternSymbol("findElementOrNil:"),
                 ObjectMemoryNew.InternSymbol("at:"),
                 ObjectMemoryNew.InternSymbol("isNil"),
                 ObjectMemoryNew.InternSymbol("at:put:")],
                1, 1, 0);
            ObjectMemoryNew.AddMethod(setClassRef.AsObjectPointer(), "add:", method17);
            instanceMethodCount++;

            // includes:
            var method18 = ObjectMemoryNew.CreateCompiledMethod(
                "includes:",
                [0x70, 0x10, 0xE0, 0x68, // index := self findElementOrNil: anObject
                 0x00, 0x68, 0xC1, 0x73, 0xB6, 0x9C, 0x7C], // ^(self at: index) isNil not
                [ObjectMemoryNew.InternSymbol("findElementOrNil:"),
                 ObjectMemoryNew.InternSymbol("at:"),
                 ObjectMemoryNew.InternSymbol("isNil")],
                1, 1, 0);
            ObjectMemoryNew.AddMethod(setClassRef.AsObjectPointer(), "includes:", method18);
            instanceMethodCount++;
        }
        
        // Install class methods for ReadStream
        var readStreamClassRef = SystemDictionary.ReadStreamClassRef;
        if (!readStreamClassRef.IsNil)
        {
            var method19 = ObjectMemoryNew.CreateCompiledMethod(
                "on:",
                [0x70, 0xCB, 0x68, // stream := self basicNew
                 0x68, 0x10, 0xE0, // stream collection: aCollection
                 0x68, 0x75, 0xE1, // stream position: 0
                 0x68, 0x7C], // ^stream
                [ObjectMemoryNew.InternSymbol("basicNew"),
                 ObjectMemoryNew.InternSymbol("collection:"),
                 ObjectMemoryNew.InternSymbol("position:")],
                1, 1, 0);
            ObjectMemoryNew.AddMethod(readStreamClassRef.AsObjectPointer(), "on:", method19);
            instanceMethodCount++;
        }

        // Install class methods for WriteStream
        var writeStreamClassRef = SystemDictionary.WriteStreamClassRef;
        if (!writeStreamClassRef.IsNil)
        {
            var method20 = ObjectMemoryNew.CreateCompiledMethod(
                "on:",
                [0x70, 0xCB, 0x68, // stream := self basicNew
                 0x68, 0x10, 0xE0, // stream collection: aCollection
                 0x68, 0x75, 0xE1, // stream position: 0
                 0x68, 0x7C], // ^stream
                [ObjectMemoryNew.InternSymbol("basicNew"),
                 ObjectMemoryNew.InternSymbol("collection:"),
                 ObjectMemoryNew.InternSymbol("position:")],
                1, 1, 0);
            ObjectMemoryNew.AddMethod(writeStreamClassRef.AsObjectPointer(), "on:", method20);
            instanceMethodCount++;
        }

        // Install class methods for Symbol
        var symbolClassRef = SystemDictionary.SymbolClassRef;
        if (!symbolClassRef.IsNil)
        {
            var method21 = ObjectMemoryNew.CreateCompiledMethod(
                "intern:",
                [0x10, 0xD0, 0x7C], // aString asSymbol
                [ObjectMemoryNew.InternSymbol("asSymbol")],
                1, 0, 0);
            ObjectMemoryNew.AddMethod(symbolClassRef.AsObjectPointer(), "intern:", method21);
            instanceMethodCount++;
        }

        // Install class methods for Character
        var characterClassRef = SystemDictionary.CharacterClassRef;
        if (!characterClassRef.IsNil)
        {
            var method22 = ObjectMemoryNew.CreateCompiledMethod(
                "value:",
                [], // No fallback needed - primitive 170 cannot fail
                [],
                1, 0, 170);
            ObjectMemoryNew.AddMethod(characterClassRef.AsObjectPointer(), "value:", method22);
            instanceMethodCount++;
        }

        // Install class methods for Process
        var processClassRef = SystemDictionary.ProcessClassRef;
        if (!processClassRef.IsNil)
        {
            // forContext:priority:
            // | newProcess |
            // newProcess := self new.
            // newProcess suspendedContext: aContext.
            // newProcess priority: anInteger.
            // ^newProcess
            var method23 = ObjectMemoryNew.CreateCompiledMethod(
                "forContext:priority:",
                [0x70, 0xD0, 0x68,  // self new, store temp
                 0x11, 0x10, 0xF1,  // push temp, push arg0, send suspendedContext:
                 0x87,              // Pop
                 0x11, 0x12, 0xF2,  // push temp, push arg1, send priority:
                 0x87,              // Pop
                 0x11, 0x7C],       // push temp, return
                [ObjectMemoryNew.InternSymbol("new"),
                 ObjectMemoryNew.InternSymbol("suspendedContext:"),
                 ObjectMemoryNew.InternSymbol("priority:")],
                2, 1, 0);
            ObjectMemoryNew.AddMethod(processClassRef.AsObjectPointer(), "forContext:priority:", method23);
            instanceMethodCount++;
        }
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Class-side methods installation complete");
        }
    }
}