namespace Smalltalk80.Interpreter.ColdStart;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Generated.Layouts;

/// <summary>
/// Installs critical compilation methods during cold start to enable self-hosted compilation.
/// These methods are essential for the Scanner, Parser, Compiler, and related classes
/// to function properly and compile new methods.
/// </summary>
public static class CompilationMethodsInstaller
{
    /// <summary>
    /// Installs all critical compilation methods needed for self-hosted compilation.
    /// </summary>
    public static void InstallAll()
    {
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing compilation methods...");
        }

        // Install Scanner methods
        InstallScannerMethods();
        
        // Install Parser methods
        InstallParserMethods();
        
        // Install Compiler methods
        InstallCompilerMethods();
        
        // Install MethodNode methods
        InstallMethodNodeMethods();
        
        // Install Encoder methods
        InstallEncoderMethods();
        
        // Install Behavior compilation methods
        InstallBehaviorCompilationMethods();
        
        // Initialize Scanner type table
        InitializeScannerTypeTable();

        // NOTE: Stream methods are already installed by StreamMethodsInstaller in KernelMethodsInstaller
        // No need to install them again here - removed duplicate installation

        // Install Object class compilation support methods
        InstallObjectCompilationSupportMethods();
        
        // Install CompiledMethod creation methods
        InstallCompiledMethodCreationMethods();
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            // Count compilation methods installed
            var compilationClassCount = 0;
            var compilationMethodCount = 0;
            
            var compilationClasses = new[] {
                "Scanner", "Parser", "Compiler", "Encoder", "MethodNode",
                "ParseNode", "LeafNode", "VariableNode", "LiteralNode",
                "SelectorNode", "MessageNode", "AssignmentNode", "ReturnNode",
                "BlockNode", "CascadeNode"
            };
            
            foreach (var className in compilationClasses)
            {
                var sClassRef = SystemDictionary.LookupClassRef(className);
                if (sClassRef.HasValue && !sClassRef.Value.IsNil)
                {
                    compilationClassCount++;
                    // TODO: Update when GetMethodCount is available for SObjectRef
                    // compilationMethodCount += sClassRef.GetMethodCount();
                }
            }
            
            Console.WriteLine($"[COLD START] Compilation methods installed: {compilationMethodCount} methods across {compilationClassCount} compiler classes");
        }
    }
    
    /// <summary>
    /// Installs Scanner methods required for lexical analysis.
    /// </summary>
    private static void InstallScannerMethods()
    {
        var scannerClassRef = SystemDictionary.ScannerClassRef;
        if (scannerClassRef.IsNil)
        {
            Console.WriteLine("[WARNING] Scanner class not found");
            return;
        }

        var instanceMethodCount = 0;

        // scan: aString
        // "Initialize the receiver to scan aString"
        var method1 = ObjectMemoryNew.CreateCompiledMethod(
            "scan:",
            [112, 16, 224, 135, 112, 209, 120], // self source: aString. self scanToken. ^self
            [ObjectMemoryNew.InternSymbol("source:"), ObjectMemoryNew.InternSymbol("scanToken")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(scannerClassRef.AsObjectPointer(), "scan:", method1);
        instanceMethodCount++;

        // scanToken
        // "Scan the next token and store it in token"
        var method2 = ObjectMemoryNew.CreateCompiledMethod(
            "scanToken",
            [137, 117, 200, 151, 8, 2, 208, 192, 101, 33, 198, 125, 137, 117, 200, 146, 112, 211, 125,
             226, 135, 0, 212, 118, 177, 97, 5, 118, 192, 40, 182, 157, 112, 5, 230, 148, 112, 211,
             215, 100, 135, 4, 124],
            [ObjectMemoryNew.InternSymbol("step"),
             ObjectMemoryNew.InternSymbol("endOfStream"),
             ObjectMemoryNew.InternSymbol("isNil"),
             ObjectMemoryNew.InternSymbol("xEndOfStream"),
             ObjectMemoryNew.InternSymbol("perform:")],
            0, 2, 0);
        ObjectMemoryNew.AddMethod(scannerClassRef.AsObjectPointer(), "scanToken", method2);
        instanceMethodCount++;

        // xLetter
        // "Scan an identifier or keyword"
        var method3 = ObjectMemoryNew.CreateCompiledMethod(
            "xLetter",
            [7, 208, 135, 137, 117, 200, 160, 17, 8, 2, 209, 192, 104, 16, 34, 198, 137, 117, 200, 147,
             16, 36, 198, 125, 227, 125, 137, 117, 200, 160, 15, 7, 2, 196, 135, 3, 98, 0, 197, 156, 71,
             99, 147, 0, 195, 99, 125, 229, 135, 16, 40, 198, 137, 117, 200, 160, 12, 8, 3, 209, 192,
             101, 5, 34, 198, 5, 43, 198, 234, 125, 233, 137, 117, 200, 160, 29, 7, 112, 221, 196, 135,
             7, 222, 105, 112, 5, 239, 135, 17, 4, 131, 48, 100, 5, 49, 198, 156, 4, 131, 18, 100, 115,
             135, 112, 125, 125, 236, 135, 16, 51, 198, 168, 8, 7, 112, 221, 196, 135, 52, 101, 146, 53,
             101, 135, 7, 222, 100, 120],
            [ObjectMemoryNew.InternSymbol("buffer"),
             ObjectMemoryNew.InternSymbol("reset"),
             ObjectMemoryNew.InternSymbol("isAlphaNumeric"),
             ObjectMemoryNew.InternSymbol("nextPut:"),
             ObjectMemoryNew.InternSymbol("step"),
             ObjectMemoryNew.InternSymbol("contents"),
             ObjectMemoryNew.InternSymbol("findKeyword"),
             ObjectMemoryNew.InternSymbol("isKeyword"),
             ObjectMemoryNew.InternSymbol("asSymbol"),
             ObjectMemoryNew.InternSymbol("at:"),
             ObjectMemoryNew.InternSymbol("isLiteral"),
             ObjectMemoryNew.InternSymbol("notifyError:"),
             ObjectMemoryNew.InternSymbol("xIllegal")],
            0, 3, 0);
        ObjectMemoryNew.AddMethod(scannerClassRef.AsObjectPointer(), "xLetter", method3);
        instanceMethodCount++;

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Scanner: {instanceMethodCount} methods installed");
        }
    }
    
    /// <summary>
    /// Installs Parser methods required for syntax analysis.
    /// </summary>
    private static void InstallParserMethods()
    {
        var parserClassRef = SystemDictionary.ParserClassRef;
        if (parserClassRef.IsNil)
        {
            Console.WriteLine("[WARNING] Parser class not found");
            return;
        }

        var instanceMethodCount = 0;

        // parse:class:noPattern:context:notifying:ifFail:
        // "Main parser entry point"
        var method4 = ObjectMemoryNew.CreateCompiledMethod(
            "parse:class:noPattern:context:notifying:ifFail:",
            [112, 16, 20, 137, 117, 200, 147, 21, 201, 125, 125, 131, 96, 135, 65, 204, 17, 19, 112,
             131, 98, 101, 21, 129, 96, 8, 135, 112, 18, 19, 243, 110, 115, 103, 102, 129, 96, 8, 135,
             101, 22, 124],
            [ObjectMemoryNew.InternSymbol("init:notifying:failBlock:"),
             ObjectMemoryNew.InternSymbol("encoder"),
             ObjectMemoryNew.InternSymbol("init:context:notifying:"),
             ObjectMemoryNew.InternSymbol("parseMethodComment"),
             ObjectMemoryNew.InternSymbol("advance"),
             ObjectMemoryNew.InternSymbol("parseMessagePattern"),
             ObjectMemoryNew.InternSymbol("parseBody"),
             ObjectMemoryNew.InternSymbol("sourceText:"),
             ObjectMemoryNew.InternSymbol("generate:")],
            6, 3, 0);
        ObjectMemoryNew.AddMethod(parserClassRef.AsObjectPointer(), "parse:class:noPattern:context:notifying:ifFail:", method4);
        instanceMethodCount++;

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Parser: {instanceMethodCount} methods installed");
        }
    }
    
    /// <summary>
    /// Installs Compiler methods.
    /// </summary>
    private static void InstallCompilerMethods()
    {
        var compilerClassRef = SystemDictionary.CompilerClassRef;
        if (compilerClassRef.IsNil)
        {
            Console.WriteLine("[WARNING] Compiler class not found");
            return;
        }

        var instanceMethodCount = 0;

        // compile:in:notifying:ifFail:
        // "Compile source in the context of class"
        var method5 = ObjectMemoryNew.CreateCompiledMethod(
            "compile:in:notifying:ifFail:",
            [112, 16, 17, 115, 18, 131, 128, 135, 112, 0, 114, 19, 131, 97, 124],
            [ObjectMemoryNew.InternSymbol("parse:in:notifying:ifFail:"),
             ObjectMemoryNew.InternSymbol("translate:ifFail:")],
            4, 1, 0);
        ObjectMemoryNew.AddMethod(compilerClassRef.AsObjectPointer(), "compile:in:notifying:ifFail:", method5);
        instanceMethodCount++;

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Compiler: {instanceMethodCount} methods installed");
        }
    }
    
    /// <summary>
    /// Installs MethodNode methods for code generation.
    /// </summary>
    private static void InstallMethodNodeMethods()
    {
        var methodNodeClassRef = SystemDictionary.MethodNodeClassRef;
        if (methodNodeClassRef.IsNil)
        {
            Console.WriteLine("[WARNING] MethodNode class not found");
            return;
        }

        var instanceMethodCount = 0;

        // generate: trailer
        // "Generate bytecode for this method"
        var method6 = ObjectMemoryNew.CreateCompiledMethod(
            "generate:",
            [112, 137, 118, 200, 160, 28, 118, 34, 137, 118, 200, 160, 11, 18, 18, 194, 34, 177, 24,
             176, 16, 24, 192, 193, 125, 241, 135, 18, 112, 212, 227, 135, 18, 125, 125, 224, 135, 2,
             194, 111, 3, 6, 229, 105, 6, 5, 23, 246, 100, 6, 215, 40, 179, 137, 117, 200, 148, 112,
             43, 234, 125, 125, 233, 135, 4, 194, 107, 44, 179, 137, 117, 200, 148, 112, 45, 234, 125,
             125, 233, 135, 78, 17, 23, 48, 180, 137, 117, 200, 147, 5, 117, 182, 125, 131, 49, 155,
             23, 145, 51, 6, 215, 117, 19, 131, 175, 106, 84, 18, 131, 53, 110, 22, 18, 131, 23, 118,
             177, 131, 54, 135, 88, 204, 131, 25, 109, 3, 21, 22, 131, 90, 135, 21, 131, 27, 118, 183,
             137, 117, 200, 148, 112, 60, 234, 125, 125, 233, 135, 22, 131, 27, 18, 194, 34, 177, 183,
             137, 117, 200, 148, 112, 61, 234, 125, 125, 233, 135, 118, 19, 137, 118, 200, 151, 18, 20,
             4, 20, 192, 131, 94, 125, 241, 135, 18, 21, 194, 6, 131, 95, 135, 118, 34, 137, 118, 200,
             160, 11, 18, 18, 194, 34, 177, 24, 176, 16, 24, 192, 193, 125, 241, 135, 18, 112, 212, 227,
             135, 18, 124],
            [ObjectMemoryNew.InternSymbol("block"),
             ObjectMemoryNew.InternSymbol("emitForValue:"),
             ObjectMemoryNew.InternSymbol("encoder"),
             ObjectMemoryNew.InternSymbol("methodReturnTop"),
             ObjectMemoryNew.InternSymbol("literals"),
             ObjectMemoryNew.InternSymbol("size"),
             ObjectMemoryNew.InternSymbol("tempNames"),
             ObjectMemoryNew.InternSymbol("encodeLiteral:"),
             ObjectMemoryNew.InternSymbol("addLast:"),
             ObjectMemoryNew.InternSymbol("primitive"),
             ObjectMemoryNew.InternSymbol("isQuick"),
             ObjectMemoryNew.InternSymbol("generate"),
             ObjectMemoryNew.InternSymbol("literals:"),
             ObjectMemoryNew.InternSymbol("selector"),
             ObjectMemoryNew.InternSymbol("precedence"),
             ObjectMemoryNew.InternSymbol("properties"),
             ObjectMemoryNew.InternSymbol("trailer:"),
             ObjectMemoryNew.InternSymbol("numArgs:numTemps:primitive:literals:"),
             ObjectMemoryNew.InternSymbol("copyWithTempsFromMethodNode:")],
            1, 7, 0);
        ObjectMemoryNew.AddMethod(methodNodeClassRef.AsObjectPointer(), "generate:", method6);
        instanceMethodCount++;

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] MethodNode: {instanceMethodCount} methods installed");
        }
    }
    
    /// <summary>
    /// Installs Encoder methods for managing literals and temporaries.
    /// </summary>
    private static void InstallEncoderMethods()
    {
        var encoderClassRef = SystemDictionary.EncoderClassRef;
        if (encoderClassRef.IsNil)
        {
            Console.WriteLine("[WARNING] Encoder class not found");
            return;
        }

        var instanceMethodCount = 0;

        // init:context:notifying:
        // "Initialize the encoder for a class and context"
        var method7 = ObjectMemoryNew.CreateCompiledMethod(
            "init:context:notifying:",
            [112, 16, 96, 135, 112, 17, 97, 135, 112, 18, 98, 135, 112, 217, 209, 99, 135, 112, 218,
             209, 100, 135, 112, 219, 209, 101, 135, 112, 220, 209, 102, 135, 112, 221, 209, 103, 135,
             112, 118, 104, 135, 120],
            [ObjectMemoryNew.InternSymbol("class:"),
             ObjectMemoryNew.InternSymbol("context:"),
             ObjectMemoryNew.InternSymbol("requestor:"),
             ObjectMemoryNew.InternSymbol("scopeTable:"),
             ObjectMemoryNew.InternSymbol("new"),
             ObjectMemoryNew.InternSymbol("Dictionary"),
             ObjectMemoryNew.InternSymbol("literalStream:"),
             ObjectMemoryNew.InternSymbol("WriteStream"),
             ObjectMemoryNew.InternSymbol("on:"),
             ObjectMemoryNew.InternSymbol("Array"),
             ObjectMemoryNew.InternSymbol("selectorSet:"),
             ObjectMemoryNew.InternSymbol("Set"),
             ObjectMemoryNew.InternSymbol("litIndSet:"),
             ObjectMemoryNew.InternSymbol("litSet:"),
             ObjectMemoryNew.InternSymbol("nTemps:")],
            3, 0, 0);
        ObjectMemoryNew.AddMethod(encoderClassRef.AsObjectPointer(), "init:context:notifying:", method7);
        instanceMethodCount++;

        // encodeLiteral:
        // "Encode a literal value and return its index"
        var method8 = ObjectMemoryNew.CreateCompiledMethod(
            "encodeLiteral:",
            [5, 16, 230, 106, 18, 115, 146, 6, 16, 194, 108, 5, 20, 193, 135, 20, 124],
            [ObjectMemoryNew.InternSymbol("literalStream"),
             ObjectMemoryNew.InternSymbol("nextPut:"),
             ObjectMemoryNew.InternSymbol("contents"),
             ObjectMemoryNew.InternSymbol("indexOf:")],
            1, 2, 0);
        ObjectMemoryNew.AddMethod(encoderClassRef.AsObjectPointer(), "encodeLiteral:", method8);
        instanceMethodCount++;

        // tempNames
        // "Return the names of temporaries"
        var method9 = ObjectMemoryNew.CreateCompiledMethod(
            "tempNames",
            [218, 209, 208, 124],
            [ObjectMemoryNew.InternSymbol("OrderedCollection"),
             ObjectMemoryNew.InternSymbol("new")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(encoderClassRef.AsObjectPointer(), "tempNames", method9);
        instanceMethodCount++;

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Encoder: {instanceMethodCount} methods installed");
        }
    }
    
    /// <summary>
    /// Installs compilation-related methods on Behavior class.
    /// </summary>
    private static void InstallBehaviorCompilationMethods()
    {
        var behaviorClassRef = SystemDictionary.BehaviorClassRef;
        if (behaviorClassRef.IsNil)
        {
            Console.WriteLine("[WARNING] Behavior class not found");
            return;
        }

        var instanceMethodCount = 0;

        // compile:notifying:trailer:ifFail:
        // "Compile source code and install the resulting method"
        var method10 = ObjectMemoryNew.CreateCompiledMethod(
            "compile:notifying:trailer:ifFail:",
            [112, 208, 204, 16, 112, 17, 19, 131, 129, 108, 20, 210, 109, 112, 21, 20, 18, 228, 243,
             135, 21, 124],
            [ObjectMemoryNew.InternSymbol("compilerClass"),
             ObjectMemoryNew.InternSymbol("compile:in:notifying:ifFail:"),
             ObjectMemoryNew.InternSymbol("selector"),
             ObjectMemoryNew.InternSymbol("addSelector:withMethod:notifying:")],
            4, 2, 0);
        ObjectMemoryNew.AddMethod(behaviorClassRef.AsObjectPointer(), "compile:notifying:trailer:ifFail:", method10);
        instanceMethodCount++;

        // addSelector:withMethod:
        // "Add a method to the method dictionary"
        var method11 = ObjectMemoryNew.CreateCompiledMethod(
            "addSelector:withMethod:",
            [1, 16, 224, 106, 1, 16, 17, 193, 135, 112, 209, 135, 66, 16, 137, 117, 200, 146, 68, 204,
             125, 243, 112, 229, 158, 112, 112, 16, 66, 131, 103, 115, 135, 72, 16, 137, 117, 200, 146,
             68, 204, 125, 243, 112, 229, 158, 112, 112, 16, 72, 131, 103, 115, 135, 18, 157, 112, 16,
             234, 160, 10, 112, 219, 137, 118, 200, 147, 76, 16, 237, 125, 203, 120],
            [ObjectMemoryNew.InternSymbol("methodDictionary"),
             ObjectMemoryNew.InternSymbol("at:put:"),
             ObjectMemoryNew.InternSymbol("flushCache"),
             ObjectMemoryNew.InternSymbol("isObsolete"),
             ObjectMemoryNew.InternSymbol("notify:"),
             ObjectMemoryNew.InternSymbol("Organization"),
             ObjectMemoryNew.InternSymbol("classify:under:")],
            2, 1, 0);
        ObjectMemoryNew.AddMethod(behaviorClassRef.AsObjectPointer(), "addSelector:withMethod:", method11);
        instanceMethodCount++;

        // Also add compile:classified: for convenience
        var method12 = ObjectMemoryNew.CreateCompiledMethod(
            "compile:classified:",
            [112, 16, 115, 115, 115, 240, 105, 112, 17, 21, 131, 98, 135, 21, 124],
            [ObjectMemoryNew.InternSymbol("compile:notifying:trailer:ifFail:"),
             ObjectMemoryNew.InternSymbol("classify:under:")],
            2, 1, 0);
        ObjectMemoryNew.AddMethod(behaviorClassRef.AsObjectPointer(), "compile:classified:", method12);
        instanceMethodCount++;

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Behavior compilation: {instanceMethodCount} methods installed");
        }
    }
    
    /// <summary>
    /// Initializes the Scanner type table for character classification.
    /// </summary>
    private static void InitializeScannerTypeTable()
    {
        // TODO: Implement type table initialization when array instantiation API is available
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Scanner type table initialization skipped - not yet implemented");
        }
    }

    /// <summary>
    /// Installs Object class methods that support compilation.
    /// </summary>
    private static void InstallObjectCompilationSupportMethods()
    {
        var objectClassRef = SystemDictionary.ObjectClassRef;
        if (objectClassRef.IsNil)
        {
            Console.WriteLine("[WARNING] Object class not found");
            return;
        }

        var instanceMethodCount = 0;

        // compilerClass
        // "Return the compiler class to use"
        var method25 = ObjectMemoryNew.CreateCompiledMethod(
            "compilerClass",
            [65, 124],
            [ObjectMemoryNew.InternSymbol("Compiler")],
            0, 0, 0);
        // Note: Object class methods should be added to Object's class side
        // For now, add to instance side as a workaround
        ObjectMemoryNew.AddMethod(objectClassRef.AsObjectPointer(), "compilerClass", method25);
        instanceMethodCount++;

        // compile:classified:
        // "Compile source code with classification"
        var method26 = ObjectMemoryNew.CreateCompiledMethod(
            "compile:classified:",
            [112, 16, 115, 115, 115, 240, 105, 112, 17, 21, 131, 98, 135, 21, 124],
            [ObjectMemoryNew.InternSymbol("compile:notifying:trailer:ifFail:"),
             ObjectMemoryNew.InternSymbol("classify:under:")],
            2, 1, 0);
        // Note: Object class methods should be added to Object's class side
        // For now, add to instance side as a workaround
        ObjectMemoryNew.AddMethod(objectClassRef.AsObjectPointer(), "compile:classified:", method26);
        instanceMethodCount++;

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Object compilation support: {instanceMethodCount} methods installed");
        }
    }
    
    /// <summary>
    /// Installs CompiledMethod class creation methods.
    /// </summary>
    private static void InstallCompiledMethodCreationMethods()
    {
        var compiledMethodClassRef = SystemDictionary.CompiledMethodClassRef;
        if (compiledMethodClassRef.IsNil)
        {
            Console.WriteLine("[WARNING] CompiledMethod class not found");
            return;
        }

        var instanceMethodCount = 0;

        // newMethod:header:
        // "Create a new compiled method with bytecodes and header"
        var method27 = ObjectMemoryNew.CreateCompiledMethod(
            "newMethod:header:",
            [66, 208, 104, 16, 16, 194, 249, 105, 17, 118, 16, 194, 240, 106, 16, 118, 21, 195, 107,
             18, 20, 193, 102, 22, 20, 194, 103, 20, 124],
            [ObjectMemoryNew.InternSymbol("basicNew:"),
             ObjectMemoryNew.InternSymbol("size"),
             ObjectMemoryNew.InternSymbol("numLiterals:"),
             ObjectMemoryNew.InternSymbol("at:put:"),
             ObjectMemoryNew.InternSymbol("header"),
             ObjectMemoryNew.InternSymbol("bytecodes")],
            2, 4, 0);
        // Note: CompiledMethod class methods should be added to CompiledMethod's class side
        // For now, add to instance side as a workaround
        ObjectMemoryNew.AddMethod(compiledMethodClassRef.AsObjectPointer(), "newMethod:header:", method27);
        instanceMethodCount++;

        // numArgs:numTemps:primitive:literals:
        // "Create a method with specified parameters"
        var method28 = ObjectMemoryNew.CreateCompiledMethod(
            "numArgs:numTemps:primitive:literals:",
            [19, 194, 104, 66, 20, 208, 105, 21, 118, 225, 102, 21, 16, 196, 103, 21, 17, 197, 104,
             21, 18, 198, 105, 21, 20, 199, 106, 21, 124],
            [ObjectMemoryNew.InternSymbol("size"),
             ObjectMemoryNew.InternSymbol("newMethod:header:"),
             ObjectMemoryNew.InternSymbol("numArgs:"),
             ObjectMemoryNew.InternSymbol("numTemps:"),
             ObjectMemoryNew.InternSymbol("primitive:"),
             ObjectMemoryNew.InternSymbol("literals:")],
            4, 2, 0);
        // Note: CompiledMethod class methods should be added to CompiledMethod's class side
        // For now, add to instance side as a workaround
        ObjectMemoryNew.AddMethod(compiledMethodClassRef.AsObjectPointer(), "numArgs:numTemps:primitive:literals:", method28);
        instanceMethodCount++;

        // copyWithTempsFromMethodNode:
        // "Copy temporaries from a method node"
        var method29 = ObjectMemoryNew.CreateCompiledMethod(
            "copyWithTempsFromMethodNode:",
            [112, 16, 210, 104, 20, 215, 155, 120, 112, 20, 117, 194, 197, 106, 112, 20, 117, 22,
             196, 105, 21, 124],
            [ObjectMemoryNew.InternSymbol("temporaries"),
             ObjectMemoryNew.InternSymbol("tempNames"),
             ObjectMemoryNew.InternSymbol("isEmpty"),
             ObjectMemoryNew.InternSymbol("size"),
             ObjectMemoryNew.InternSymbol("at:put:")],
            1, 3, 0);
        ObjectMemoryNew.AddMethod(compiledMethodClassRef.AsObjectPointer(), "copyWithTempsFromMethodNode:", method29);
        instanceMethodCount++;

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] CompiledMethod creation: {instanceMethodCount} methods installed");
        }
    }
}
