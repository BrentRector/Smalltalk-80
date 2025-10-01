namespace Smalltalk80.Interpreter.ColdStart;

using Smalltalk80.Interpreter.ColdStart.MethodInstallers;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;

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

        // Install class methods for Scanner
        var scannerClassRef = SystemDictionary.ScannerClassRef;
        if (!scannerClassRef.IsNil)
        {
            scannerClassRef.AddMethod("new", SCompiledMethod.CreateCompiledMethodRef(
                "new",
                [0x70, 0xCB, 0x7C], // self basicNew
                SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("basicNew")),
                0, 0, 0));
        }
        
        // Install class methods for Parser
        var parserClassRef = SystemDictionary.ParserClassRef;
        if (!parserClassRef.IsNil)
        {
            parserClassRef.AddMethod("new", SCompiledMethod.CreateCompiledMethodRef(
                "new",
                [0x70, 0xCB, 0x7C], // self basicNew
                SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("basicNew")),
                0, 0, 0));
        }

        // Install class methods for Encoder
        var encoderClassRef = SystemDictionary.EncoderClassRef;
        if (!encoderClassRef.IsNil)
        {
            encoderClassRef.AddMethod("new", SCompiledMethod.CreateCompiledMethodRef(
                "new",
                [0x70, 0xCB, 0x7C], // self basicNew
                SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("basicNew")),
                0, 0, 0));
        }

        // Install class methods for Compiler
        var compilerClassRef = SystemDictionary.CompilerClassRef;
        if (!compilerClassRef.IsNil)
        {
            compilerClassRef.AddMethod("new", SCompiledMethod.CreateCompiledMethodRef(
                "new",
                [0x70, 0xCB, 0x7C], // self basicNew
                SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("basicNew")),
                0, 0, 0));
        }
        
        // Install instance methods for Collection
        var collectionClassRef = SystemDictionary.CollectionClassRef;
        if (!collectionClassRef.IsNil)
        {
            // size - basic size method
            collectionClassRef.AddMethod("size", SCompiledMethod.CreateCompiledMethodRef(
                "size",
                [0x75, 0x68, // tally := 0
                 0x70, 0x8F, 0x00, 0x02, 0x68, 0x76, 0xB0, 0x69, 0xF7, // self do: [:each | tally := tally + 1]
                 0x68, 0x7C], // ^tally
                SCompiledMethod.ConvertLiteralsToRefs(
                    SSymbol.Intern("do:"),
                    new SmallInteger(1)),
                0, 1, 0));

            // isEmpty
            collectionClassRef.AddMethod("isEmpty", SCompiledMethod.CreateCompiledMethodRef(
                "isEmpty",
                [0x70, 0xC0, 0x75, 0xB6, 0x7C], // ^self size = 0
                SCompiledMethod.ConvertLiteralsToRefs(
                    SSymbol.Intern("size"),
                    new SmallInteger(0),
                    SSymbol.Intern("=")),
                0, 0, 0));

            // do: - abstract, subclasses must override
            collectionClassRef.AddMethod("do:", SCompiledMethod.CreateCompiledMethodRef(
                "do:",
                [0x70, 0x21, 0xE0, 0x7C], // self subclassResponsibility
                SCompiledMethod.ConvertLiteralsToRefs(
                    SSymbol.Intern("subclassResponsibility")),
                1, 0, 0));
        }
        
        // Install instance methods for SequenceableCollection
        var sequenceableCollectionClassRef = SystemDictionary.SequenceableCollectionClassRef;
        if (!sequenceableCollectionClassRef.IsNil)
        {
            // first
            sequenceableCollectionClassRef.AddMethod("first", SCompiledMethod.CreateCompiledMethodRef(
                "first",
                [0x70, 0x76, 0xC0, 0x7C], // ^self at: 1
                SCompiledMethod.ConvertLiteralsToRefs(
                    new SmallInteger(1),
                    SSymbol.Intern("at:")),
                0, 0, 0));

            // last
            sequenceableCollectionClassRef.AddMethod("last", SCompiledMethod.CreateCompiledMethodRef(
                "last",
                [0x70, 0x70, 0xC0, 0xC1, 0x7C], // ^self at: self size
                SCompiledMethod.ConvertLiteralsToRefs(
                    SSymbol.Intern("size"),
                    SSymbol.Intern("at:")),
                0, 0, 0));

            // at:ifAbsent:
            sequenceableCollectionClassRef.AddMethod("at:ifAbsent:", SCompiledMethod.CreateCompiledMethodRef(
                "at:ifAbsent:",
                [0x10, 0x76, 0xB2, 0x9B, 0x08, // index < 1 ifTrue:
                 0x10, 0x70, 0xC0, 0xB4, 0x99, 0x03, // index > self size ifTrue:
                 0x11, 0xC9, 0x7C, // ^exceptionBlock value
                 0x70, 0x10, 0xC0, 0x7C], // ^self at: index
                SCompiledMethod.ConvertLiteralsToRefs(
                    new SmallInteger(1),
                    SSymbol.Intern("<"),
                    SSymbol.Intern("size"),
                    SSymbol.Intern(">"),
                    SSymbol.Intern("value"),
                    SSymbol.Intern("at:")),
                2, 0, 0));
        }
        
        // Install instance methods for OrderedCollection
        var orderedCollectionClassRef = SystemDictionary.OrderedCollectionClassRef;
        if (!orderedCollectionClassRef.IsNil)
        {
            // add:
            orderedCollectionClassRef.AddMethod("add:", SCompiledMethod.CreateCompiledMethodRef(
                "add:",
                [0x70, 0x10, 0xE0, 0x10, 0x7C], // self addLast: anObject. ^anObject
                SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("addLast:")),
                1, 0, 0));

            // addLast:
            orderedCollectionClassRef.AddMethod("addLast:", SCompiledMethod.CreateCompiledMethodRef(
                "addLast:",
                [0x00, 0x01, 0x76, 0xB0, 0x60, // lastIndex := lastIndex + 1
                 0x00, 0x01, 0x10, 0xE1, 0x10, 0x7C], // self at: lastIndex put: anObject. ^anObject
                SCompiledMethod.ConvertLiteralsToRefs(
                    SSymbol.Intern("lastIndex"),
                    SSymbol.Intern("at:put:")),
                1, 0, 0));

            // addAll:
            orderedCollectionClassRef.AddMethod("addAll:", SCompiledMethod.CreateCompiledMethodRef(
                "addAll:",
                [0x10, 0x8F, 0x00, 0x02, 0x70, 0x11, 0xE0, 0xF7, 0x10, 0x7C], // aCollection do: [:each | self add: each]. ^aCollection
                SCompiledMethod.ConvertLiteralsToRefs(
                    SSymbol.Intern("do:"),
                    SSymbol.Intern("add:")),
                1, 0, 0));

            // isEmpty
            orderedCollectionClassRef.AddMethod("isEmpty", SCompiledMethod.CreateCompiledMethodRef(
                "isEmpty",
                [0x00, 0x01, 0xB6, 0x7C], // firstIndex > lastIndex
                SCompiledMethod.ConvertLiteralsToRefs(
                    SSymbol.Intern("firstIndex"),
                    SSymbol.Intern("lastIndex"),
                    SSymbol.Intern(">")),
                0, 0, 0));

            // first
            orderedCollectionClassRef.AddMethod("first", SCompiledMethod.CreateCompiledMethodRef(
                "first",
                [0x70, 0x00, 0xC0, 0x7C], // self at: firstIndex
                SCompiledMethod.ConvertLiteralsToRefs(
                    SSymbol.Intern("firstIndex"),
                    SSymbol.Intern("at:")),
                0, 0, 0));

            // last
            orderedCollectionClassRef.AddMethod("last", SCompiledMethod.CreateCompiledMethodRef(
                "last",
                [0x70, 0x00, 0xC0, 0x7C], // self at: lastIndex
                SCompiledMethod.ConvertLiteralsToRefs(
                    SSymbol.Intern("lastIndex"),
                    SSymbol.Intern("at:")),
                0, 0, 0));
        }
        
        // Install instance methods for Set
        var setClassRef = SystemDictionary.SetClassRef;
        if (!setClassRef.IsNil)
        {
            // add:
            setClassRef.AddMethod("add:", SCompiledMethod.CreateCompiledMethodRef(
                "add:",
                [0x70, 0x10, 0xE0, 0x68, // index := self findElementOrNil: anObject
                 0x00, 0x68, 0xC1, 0x73, 0xB6, 0x9D, 0x05, // (self at: index) isNil ifTrue:
                 0x00, 0x68, 0x10, 0xE1, // [self at: index put: anObject]
                 0x10, 0x7C], // ^anObject
                SCompiledMethod.ConvertLiteralsToRefs(
                    SSymbol.Intern("findElementOrNil:"),
                    SSymbol.Intern("at:"),
                    SSymbol.Intern("isNil"),
                    SSymbol.Intern("at:put:")),
                1, 1, 0));

            // includes:
            setClassRef.AddMethod("includes:", SCompiledMethod.CreateCompiledMethodRef(
                "includes:",
                [0x70, 0x10, 0xE0, 0x68, // index := self findElementOrNil: anObject
                 0x00, 0x68, 0xC1, 0x73, 0xB6, 0x9C, 0x7C], // ^(self at: index) isNil not
                SCompiledMethod.ConvertLiteralsToRefs(
                    SSymbol.Intern("findElementOrNil:"),
                    SSymbol.Intern("at:"),
                    SSymbol.Intern("isNil")),
                1, 1, 0));
        }
        
        // Install class methods for ReadStream
        var readStreamClassRef = SystemDictionary.ReadStreamClassRef;
        if (!readStreamClassRef.IsNil)
        {
            readStreamClassRef.AddMethod("on:", SCompiledMethod.CreateCompiledMethodRef(
                "on:",
                [0x70, 0xCB, 0x68, // stream := self basicNew
                 0x68, 0x10, 0xE0, // stream collection: aCollection
                 0x68, 0x75, 0xE1, // stream position: 0
                 0x68, 0x7C], // ^stream
                SCompiledMethod.ConvertLiteralsToRefs(
                    SSymbol.Intern("basicNew"),
                    SSymbol.Intern("collection:"),
                    SSymbol.Intern("position:")),
                1, 1, 0));
        }

        // Install class methods for WriteStream
        var writeStreamClassRef = SystemDictionary.WriteStreamClassRef;
        if (!writeStreamClassRef.IsNil)
        {
            writeStreamClassRef.AddMethod("on:", SCompiledMethod.CreateCompiledMethodRef(
                "on:",
                [0x70, 0xCB, 0x68, // stream := self basicNew
                 0x68, 0x10, 0xE0, // stream collection: aCollection
                 0x68, 0x75, 0xE1, // stream position: 0
                 0x68, 0x7C], // ^stream
                SCompiledMethod.ConvertLiteralsToRefs(
                    SSymbol.Intern("basicNew"),
                    SSymbol.Intern("collection:"),
                    SSymbol.Intern("position:")),
                1, 1, 0));
        }

        // Install class methods for Symbol
        var symbolClassRef = SystemDictionary.SymbolClassRef;
        if (!symbolClassRef.IsNil)
        {
            symbolClassRef.AddMethod("intern:", SCompiledMethod.CreateCompiledMethodRef(
                "intern:",
                [0x10, 0xD0, 0x7C], // aString asSymbol
                SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("asSymbol")),
                1, 0, 0));
        }

        // Install class methods for Character
        var characterClassRef = SystemDictionary.CharacterClassRef;
        if (!characterClassRef.IsNil)
        {
            characterClassRef.AddMethod("value:", SCompiledMethod.CreateCompiledMethodRef(
                "value:",
                [], // No fallback needed - primitive 170 cannot fail
                [],
                1, 0, 170));
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
            var forContextLiterals = SCompiledMethod.ConvertLiteralsToRefs(
                SSymbol.Intern("new"),
                SSymbol.Intern("suspendedContext:"),
                SSymbol.Intern("priority:"));
            processClassRef.AddMethod("forContext:priority:", SCompiledMethod.CreateCompiledMethodRef(
                "forContext:priority:",
                [0x70, 0xD0, 0x68,  // self new, store temp
                 0x11, 0x10, 0xF1,  // push temp, push arg0, send suspendedContext:
                 0x87,              // Pop
                 0x11, 0x12, 0xF2,  // push temp, push arg1, send priority:
                 0x87,              // Pop
                 0x11, 0x7C],       // push temp, return
                forContextLiterals,
                2, 1));
        }
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Class-side methods installation complete");
        }
    }
}