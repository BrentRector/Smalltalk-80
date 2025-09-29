namespace Smalltalk80.Interpreter.Bootstrap;

using Smalltalk80.Interpreter.Core.Objects;

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
        
        // Install Stream methods for compilation support
        InstallStreamMethods();
        
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
                var sClass = SystemDictionary.LookupClass(className);
                if (sClass != null)
                {
                    compilationClassCount++;
                    compilationMethodCount += sClass.GetMethodCount();
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
        var scannerClass = SystemDictionary.ScannerClass;
        if (scannerClass == null)
        {
            Console.WriteLine("[WARNING] Scanner class not found");
            return;
        }
        
        // scan: aString
        // "Initialize the receiver to scan aString"
        var scanMethod = SCompiledMethod.CreateCompiledMethod(
            "scan:",
            [112, 16, 224, 135, 112, 209, 120], // self source: aString. self scanToken. ^self
            SArray.From(SSymbol.Intern("source:"), SSymbol.Intern("scanToken")),
            1, 0, 0
        );
        scannerClass.AddMethod("scan:", scanMethod);
        
        // scanToken
        // "Scan the next token and store it in token"
        var scanTokenMethod = SCompiledMethod.CreateCompiledMethod(
            "scanToken",
            [137, 117, 200, 151, 8, 2, 208, 192, 101, 33, 198, 125, 137, 117, 200, 146, 112, 211, 125, 
             226, 135, 0, 212, 118, 177, 97, 5, 118, 192, 40, 182, 157, 112, 5, 230, 148, 112, 211, 
             215, 100, 135, 4, 124],
            SArray.From(
                SSymbol.Intern("step"), 
                SSymbol.Intern("endOfStream"),
                SSymbol.Intern("isNil"),
                SSymbol.Intern("xEndOfStream"),
                SSymbol.Intern("perform:")
            ),
            0, 2, 0
        );
        scannerClass.AddMethod("scanToken", scanTokenMethod);
        
        // xLetter
        // "Scan an identifier or keyword"
        var xLetterMethod = SCompiledMethod.CreateCompiledMethod(
            "xLetter",
            [7, 208, 135, 137, 117, 200, 160, 17, 8, 2, 209, 192, 104, 16, 34, 198, 137, 117, 200, 147, 
             16, 36, 198, 125, 227, 125, 137, 117, 200, 160, 15, 7, 2, 196, 135, 3, 98, 0, 197, 156, 71, 
             99, 147, 0, 195, 99, 125, 229, 135, 16, 40, 198, 137, 117, 200, 160, 12, 8, 3, 209, 192, 
             101, 5, 34, 198, 5, 43, 198, 234, 125, 233, 137, 117, 200, 160, 29, 7, 112, 221, 196, 135, 
             7, 222, 105, 112, 5, 239, 135, 17, 4, 131, 48, 100, 5, 49, 198, 156, 4, 131, 18, 100, 115, 
             135, 112, 125, 125, 236, 135, 16, 51, 198, 168, 8, 7, 112, 221, 196, 135, 52, 101, 146, 53, 
             101, 135, 7, 222, 100, 120],
            SArray.From(
                SSymbol.Intern("buffer"),
                SSymbol.Intern("reset"),
                SSymbol.Intern("isAlphaNumeric"),
                SSymbol.Intern("nextPut:"),
                SSymbol.Intern("step"),
                SSymbol.Intern("contents"),
                SSymbol.Intern("findKeyword"),
                SSymbol.Intern("isKeyword"),
                SSymbol.Intern("asSymbol"),
                SSymbol.Intern("at:"),
                SSymbol.Intern("isLiteral"),
                SSymbol.Intern("notifyError:"),
                SSymbol.Intern("xIllegal")
            ),
            0, 3, 0
        );
        scannerClass.AddMethod("xLetter", xLetterMethod);
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Scanner methods installed");
        }
    }
    
    /// <summary>
    /// Installs Parser methods required for syntax analysis.
    /// </summary>
    private static void InstallParserMethods()
    {
        var parserClass = SystemDictionary.ParserClass;
        if (parserClass == null)
        {
            Console.WriteLine("[WARNING] Parser class not found");
            return;
        }
        
        // parse:class:noPattern:context:notifying:ifFail:
        // "Main parser entry point"
        var parseMethod = SCompiledMethod.CreateCompiledMethod(
            "parse:class:noPattern:context:notifying:ifFail:",
            [112, 16, 20, 137, 117, 200, 147, 21, 201, 125, 125, 131, 96, 135, 65, 204, 17, 19, 112, 
             131, 98, 101, 21, 129, 96, 8, 135, 112, 18, 19, 243, 110, 115, 103, 102, 129, 96, 8, 135, 
             101, 22, 124],
            SArray.From(
                SSymbol.Intern("init:notifying:failBlock:"),
                SSymbol.Intern("encoder"),
                SSymbol.Intern("init:context:notifying:"),
                SSymbol.Intern("parseMethodComment"),
                SSymbol.Intern("advance"),
                SSymbol.Intern("parseMessagePattern"),
                SSymbol.Intern("parseBody"),
                SSymbol.Intern("sourceText:"),
                SSymbol.Intern("generate:")
            ),
            6, 3, 0
        );
        parserClass.AddMethod("parse:class:noPattern:context:notifying:ifFail:", parseMethod);
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Parser methods installed");
        }
    }
    
    /// <summary>
    /// Installs Compiler methods.
    /// </summary>
    private static void InstallCompilerMethods()
    {
        var compilerClass = SystemDictionary.CompilerClass;
        if (compilerClass == null)
        {
            Console.WriteLine("[WARNING] Compiler class not found");
            return;
        }
        
        // compile:in:notifying:ifFail:
        // "Compile source in the context of class"
        var compileMethod = SCompiledMethod.CreateCompiledMethod(
            "compile:in:notifying:ifFail:",
            [112, 16, 17, 115, 18, 131, 128, 135, 112, 0, 114, 19, 131, 97, 124],
            SArray.From(
                SSymbol.Intern("parse:in:notifying:ifFail:"),
                SSymbol.Intern("translate:ifFail:")
            ),
            4, 1, 0
        );
        compilerClass.AddMethod("compile:in:notifying:ifFail:", compileMethod);
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Compiler methods installed");
        }
    }
    
    /// <summary>
    /// Installs MethodNode methods for code generation.
    /// </summary>
    private static void InstallMethodNodeMethods()
    {
        var methodNodeClass = SystemDictionary.MethodNodeClass;
        if (methodNodeClass == null)
        {
            Console.WriteLine("[WARNING] MethodNode class not found");
            return;
        }
        
        // generate: trailer
        // "Generate bytecode for this method"
        var generateMethod = SCompiledMethod.CreateCompiledMethod(
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
            SArray.From(
                SSymbol.Intern("block"),
                SSymbol.Intern("emitForValue:"),
                SSymbol.Intern("encoder"),
                SSymbol.Intern("methodReturnTop"),
                SSymbol.Intern("literals"),
                SSymbol.Intern("size"),
                SSymbol.Intern("tempNames"),
                SSymbol.Intern("encodeLiteral:"),
                SSymbol.Intern("addLast:"),
                SSymbol.Intern("primitive"),
                SSymbol.Intern("isQuick"),
                SSymbol.Intern("generate"),
                SSymbol.Intern("literals:"),
                SSymbol.Intern("selector"),
                SSymbol.Intern("precedence"),
                SSymbol.Intern("properties"),
                SSymbol.Intern("trailer:"),
                SSymbol.Intern("numArgs:numTemps:primitive:literals:"),
                SSymbol.Intern("copyWithTempsFromMethodNode:")
            ),
            1, 7, 0
        );
        methodNodeClass.AddMethod("generate:", generateMethod);
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] MethodNode methods installed");
        }
    }
    
    /// <summary>
    /// Installs Encoder methods for managing literals and temporaries.
    /// </summary>
    private static void InstallEncoderMethods()
    {
        var encoderClass = SystemDictionary.EncoderClass;
        if (encoderClass == null)
        {
            Console.WriteLine("[WARNING] Encoder class not found");
            return;
        }
        
        // init:context:notifying:
        // "Initialize the encoder for a class and context"
        var initMethod = SCompiledMethod.CreateCompiledMethod(
            "init:context:notifying:",
            [112, 16, 96, 135, 112, 17, 97, 135, 112, 18, 98, 135, 112, 217, 209, 99, 135, 112, 218, 
             209, 100, 135, 112, 219, 209, 101, 135, 112, 220, 209, 102, 135, 112, 221, 209, 103, 135, 
             112, 118, 104, 135, 120],
            SArray.From(
                SSymbol.Intern("class:"),
                SSymbol.Intern("context:"),
                SSymbol.Intern("requestor:"),
                SSymbol.Intern("scopeTable:"),
                SSymbol.Intern("new"),
                SSymbol.Intern("Dictionary"),
                SSymbol.Intern("literalStream:"),
                SSymbol.Intern("WriteStream"),
                SSymbol.Intern("on:"),
                SSymbol.Intern("Array"),
                SSymbol.Intern("selectorSet:"),
                SSymbol.Intern("Set"),
                SSymbol.Intern("litIndSet:"),
                SSymbol.Intern("litSet:"),
                SSymbol.Intern("nTemps:")
            ),
            3, 0, 0
        );
        encoderClass.AddMethod("init:context:notifying:", initMethod);
        
        // encodeLiteral:
        // "Encode a literal value and return its index"
        var encodeLiteralMethod = SCompiledMethod.CreateCompiledMethod(
            "encodeLiteral:",
            [5, 16, 230, 106, 18, 115, 146, 6, 16, 194, 108, 5, 20, 193, 135, 20, 124],
            SArray.From(
                SSymbol.Intern("literalStream"),
                SSymbol.Intern("nextPut:"),
                SSymbol.Intern("contents"),
                SSymbol.Intern("indexOf:")
            ),
            1, 2, 0
        );
        encoderClass.AddMethod("encodeLiteral:", encodeLiteralMethod);
        
        // tempNames
        // "Return the names of temporaries"
        var tempNamesMethod = SCompiledMethod.CreateCompiledMethod(
            "tempNames",
            [218, 209, 208, 124],
            SArray.From(
                SSymbol.Intern("OrderedCollection"),
                SSymbol.Intern("new")
            ),
            0, 0, 0
        );
        encoderClass.AddMethod("tempNames", tempNamesMethod);
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Encoder methods installed");
        }
    }
    
    /// <summary>
    /// Installs compilation-related methods on Behavior class.
    /// </summary>
    private static void InstallBehaviorCompilationMethods()
    {
        var behaviorClass = SystemDictionary.BehaviorClass;
        if (behaviorClass == null)
        {
            Console.WriteLine("[WARNING] Behavior class not found");
            return;
        }
        
        // compile:notifying:trailer:ifFail:
        // "Compile source code and install the resulting method"
        var compileMethod = SCompiledMethod.CreateCompiledMethod(
            "compile:notifying:trailer:ifFail:",
            [112, 208, 204, 16, 112, 17, 19, 131, 129, 108, 20, 210, 109, 112, 21, 20, 18, 228, 243, 
             135, 21, 124],
            SArray.From(
                SSymbol.Intern("compilerClass"),
                SSymbol.Intern("compile:in:notifying:ifFail:"),
                SSymbol.Intern("selector"),
                SSymbol.Intern("addSelector:withMethod:notifying:")
            ),
            4, 2, 0
        );
        behaviorClass.AddMethod("compile:notifying:trailer:ifFail:", compileMethod);
        
        // addSelector:withMethod:
        // "Add a method to the method dictionary"
        var addSelectorMethod = SCompiledMethod.CreateCompiledMethod(
            "addSelector:withMethod:",
            [1, 16, 224, 106, 1, 16, 17, 193, 135, 112, 209, 135, 66, 16, 137, 117, 200, 146, 68, 204, 
             125, 243, 112, 229, 158, 112, 112, 16, 66, 131, 103, 115, 135, 72, 16, 137, 117, 200, 146, 
             68, 204, 125, 243, 112, 229, 158, 112, 112, 16, 72, 131, 103, 115, 135, 18, 157, 112, 16, 
             234, 160, 10, 112, 219, 137, 118, 200, 147, 76, 16, 237, 125, 203, 120],
            SArray.From(
                SSymbol.Intern("methodDictionary"),
                SSymbol.Intern("at:put:"),
                SSymbol.Intern("flushCache"),
                SSymbol.Intern("isObsolete"),
                SSymbol.Intern("notify:"),
                SSymbol.Intern("Organization"),
                SSymbol.Intern("classify:under:")
            ),
            2, 1, 0
        );
        behaviorClass.AddMethod("addSelector:withMethod:", addSelectorMethod);
        
        // Also add compile:classified: for convenience
        var compileClassifiedMethod = SCompiledMethod.CreateCompiledMethod(
            "compile:classified:",
            [112, 16, 115, 115, 115, 240, 105, 112, 17, 21, 131, 98, 135, 21, 124],
            SArray.From(
                SSymbol.Intern("compile:notifying:trailer:ifFail:"),
                SSymbol.Intern("classify:under:")
            ),
            2, 1, 0
        );
        behaviorClass.AddMethod("compile:classified:", compileClassifiedMethod);
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Behavior compilation methods installed");
        }
    }
    
    /// <summary>
    /// Initializes the Scanner type table for character classification.
    /// </summary>
    private static void InitializeScannerTypeTable()
    {
        var scannerClass = SystemDictionary.ScannerClass;
        if (scannerClass == null)
        {
            Console.WriteLine("[WARNING] Scanner class not found - cannot initialize type table");
            return;
        }
        
        // Create a type table array
        // This maps ASCII characters to their lexical types
        var typeTable = new SArray(256);
        
        // Initialize all to xIllegal (type 0)
        for (int i = 1; i <= 256; i++)
        {
            typeTable.AtPut(i, SSymbol.Intern("xIllegal"));
        }
        
        // Set up character classifications
        
        // Whitespace (space, tab, CR, LF, FF) - using 1-based indexing
        typeTable.AtPut(' ' + 1, SSymbol.Intern("xDelimiter"));
        typeTable.AtPut('\t' + 1, SSymbol.Intern("xDelimiter"));
        typeTable.AtPut('\r' + 1, SSymbol.Intern("xDelimiter"));
        typeTable.AtPut('\n' + 1, SSymbol.Intern("xDelimiter"));
        typeTable.AtPut('\f' + 1, SSymbol.Intern("xDelimiter"));
        
        // Letters (uppercase and lowercase) - using 1-based indexing
        for (char c = 'A'; c <= 'Z'; c++)
        {
            typeTable.AtPut(c + 1, SSymbol.Intern("xLetter"));
        }
        for (char c = 'a'; c <= 'z'; c++)
        {
            typeTable.AtPut(c + 1, SSymbol.Intern("xLetter"));
        }
        
        // Digits - using 1-based indexing
        for (char c = '0'; c <= '9'; c++)
        {
            typeTable.AtPut(c + 1, SSymbol.Intern("xDigit"));
        }
        
        // Special characters - using 1-based indexing
        typeTable.AtPut('.' + 1, SSymbol.Intern("xPeriod"));
        typeTable.AtPut(':' + 1, SSymbol.Intern("xColon"));
        typeTable.AtPut(';' + 1, SSymbol.Intern("xSemicolon"));
        typeTable.AtPut('\'' + 1, SSymbol.Intern("xString"));
        typeTable.AtPut('"' + 1, SSymbol.Intern("xComment"));
        typeTable.AtPut('#' + 1, SSymbol.Intern("xLiteral"));
        typeTable.AtPut('$' + 1, SSymbol.Intern("xDollar"));
        typeTable.AtPut('(' + 1, SSymbol.Intern("xLeftParenthesis"));
        typeTable.AtPut(')' + 1, SSymbol.Intern("xRightParenthesis"));
        typeTable.AtPut('[' + 1, SSymbol.Intern("xLeftBracket"));
        typeTable.AtPut(']' + 1, SSymbol.Intern("xRightBracket"));
        typeTable.AtPut('{' + 1, SSymbol.Intern("xLeftBrace"));
        typeTable.AtPut('}' + 1, SSymbol.Intern("xRightBrace"));
        typeTable.AtPut('^' + 1, SSymbol.Intern("xReturn"));
        typeTable.AtPut('_' + 1, SSymbol.Intern("xUnderscore"));
        // Note: The left arrow '←' is a Unicode character outside ASCII range
        // In classic Smalltalk-80 this was often mapped to underscore
        // We'll handle underscore as assignment as well
        
        // Binary selectors - using 1-based indexing
        var binaryChars = "+-*/\\~<>=@%|&?!,";
        foreach (char c in binaryChars)
        {
            typeTable.AtPut(c + 1, SSymbol.Intern("xBinary"));
        }
        
        // Store the type table in a Scanner instance variable or as a global
        // For now, we'll create a method that returns the type table
        var typeTableMethod = SCompiledMethod.CreateCompiledMethod(
            "typeTable",
            [120], // Just return self for now - would need to return the actual table
            new SArray(),
            0, 0, 0
        );
        scannerClass.AddMethod("typeTable", typeTableMethod);
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Scanner type table initialized");
        }
    }
    
    /// <summary>
    /// Installs Stream methods needed for compilation.
    /// </summary>
    private static void InstallStreamMethods()
    {
        InstallReadStreamMethods();
        InstallWriteStreamMethods();
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Stream methods installed");
        }
    }
    
    /// <summary>
    /// Installs ReadStream methods for reading source code.
    /// </summary>
    private static void InstallReadStreamMethods()
    {
        var readStreamClass = SystemDictionary.ReadStreamClass;
        if (readStreamClass == null)
        {
            Console.WriteLine("[WARNING] ReadStream class not found");
            return;
        }
        
        // on: aCollection
        // "Create a stream on the given collection"
        var onMethod = SCompiledMethod.CreateCompiledMethod(
            "on:",
            [66, 204, 16, 233, 135, 17, 118, 193, 135, 17, 16, 194, 195, 135, 17, 124],
            SArray.From(
                SSymbol.Intern("new"),
                SSymbol.Intern("on:from:to:"),
                SSymbol.Intern("size")
            ),
            1, 1, 0
        );
        // Note: ReadStream class methods should be added to ReadStream's class side
        // For now, add to instance side as a workaround
        readStreamClass.AddMethod("on:", onMethod);
        
        // on:from:to:
        // "Create a stream on part of a collection"
        var onFromToMethod = SCompiledMethod.CreateCompiledMethod(
            "on:from:to:",
            [112, 0, 96, 135, 112, 1, 96, 135, 112, 2, 97, 135, 112, 124],
            SArray.From(
                SSymbol.Intern("collection:"),
                SSymbol.Intern("position:"),
                SSymbol.Intern("readLimit:")
            ),
            3, 0, 0
        );
        readStreamClass.AddMethod("on:from:to:", onFromToMethod);
        
        // next
        // "Answer the next element"
        var nextMethod = SCompiledMethod.CreateCompiledMethod(
            "next",
            [2, 3, 180, 155, 115, 124, 0, 2, 118, 176, 192, 105, 112, 2, 17, 176, 97, 135, 17, 124],
            SArray.From(
                SSymbol.Intern("collection"),
                SSymbol.Intern("position"),
                SSymbol.Intern("readLimit"),
                SSymbol.Intern("at:"),
                SSymbol.Intern("position:")
            ),
            0, 1, 0
        );
        readStreamClass.AddMethod("next", nextMethod);
        
        // atEnd
        // "Answer whether at end of stream"
        var atEndMethod = SCompiledMethod.CreateCompiledMethod(
            "atEnd",
            [2, 3, 181, 124],
            SArray.From(
                SSymbol.Intern("position"),
                SSymbol.Intern("readLimit")
            ),
            0, 0, 0
        );
        readStreamClass.AddMethod("atEnd", atEndMethod);
        
        // peek
        // "Answer next element without advancing"
        var peekMethod = SCompiledMethod.CreateCompiledMethod(
            "peek",
            [112, 211, 155, 115, 124, 0, 2, 118, 176, 192, 124],
            SArray.From(
                SSymbol.Intern("atEnd"),
                SSymbol.Intern("collection"),
                SSymbol.Intern("position"),
                SSymbol.Intern("at:")
            ),
            0, 0, 0
        );
        readStreamClass.AddMethod("peek", peekMethod);
    }
    
    /// <summary>
    /// Installs WriteStream methods for generating code.
    /// </summary>
    private static void InstallWriteStreamMethods()
    {
        var writeStreamClass = SystemDictionary.WriteStreamClass;
        if (writeStreamClass == null)
        {
            Console.WriteLine("[WARNING] WriteStream class not found");
            return;
        }
        
        // on:
        // "Create a stream on the given collection"
        var onMethod = SCompiledMethod.CreateCompiledMethod(
            "on:",
            [66, 204, 16, 118, 16, 194, 234, 135, 17, 124],
            SArray.From(
                SSymbol.Intern("new"),
                SSymbol.Intern("on:from:to:"),
                SSymbol.Intern("size")
            ),
            1, 1, 0
        );
        // Note: WriteStream class methods should be added to WriteStream's class side
        // For now, add to instance side as a workaround
        writeStreamClass.AddMethod("on:", onMethod);
        
        // nextPut:
        // "Add an element to the stream"
        var nextPutMethod = SCompiledMethod.CreateCompiledMethod(
            "nextPut:",
            [2, 3, 180, 157, 112, 213, 135, 0, 2, 118, 176, 16, 193, 135, 112, 2, 118, 176, 97, 135, 
             3, 2, 183, 157, 112, 2, 98, 135, 16, 124],
            SArray.From(
                SSymbol.Intern("collection"),
                SSymbol.Intern("position"),
                SSymbol.Intern("writeLimit"),
                SSymbol.Intern("pastEndPut:"),
                SSymbol.Intern("at:put:"),
                SSymbol.Intern("position:"),
                SSymbol.Intern("writeLimit:")
            ),
            1, 0, 0
        );
        writeStreamClass.AddMethod("nextPut:", nextPutMethod);
        
        // nextPutAll:
        // "Add a collection of elements"
        var nextPutAllMethod = SCompiledMethod.CreateCompiledMethod(
            "nextPutAll:",
            [16, 137, 118, 200, 151, 112, 17, 226, 125, 242, 135, 16, 124],
            SArray.From(
                SSymbol.Intern("do:"),
                SSymbol.Intern("nextPut:")
            ),
            1, 1, 0
        );
        writeStreamClass.AddMethod("nextPutAll:", nextPutAllMethod);
        
        // contents
        // "Answer the contents written so far"
        var contentsMethod = SCompiledMethod.CreateCompiledMethod(
            "contents",
            [0, 118, 2, 231, 124],
            SArray.From(
                SSymbol.Intern("collection"),
                SSymbol.Intern("copyFrom:to:"),
                SSymbol.Intern("position")
            ),
            0, 0, 0
        );
        writeStreamClass.AddMethod("contents", contentsMethod);
        
        // reset
        // "Reset the stream to the beginning"
        var resetMethod = SCompiledMethod.CreateCompiledMethod(
            "reset",
            [112, 118, 96, 135, 112, 3, 197, 176, 98, 135, 120],
            SArray.From(
                SSymbol.Intern("position:"),
                SSymbol.Intern("readLimit"),
                SSymbol.Intern("writeLimit:")
            ),
            0, 0, 0
        );
        writeStreamClass.AddMethod("reset", resetMethod);
        
        // size
        // "Answer the size of the contents"
        var sizeMethod = SCompiledMethod.CreateCompiledMethod(
            "size",
            [3, 124],
            SArray.From(SSymbol.Intern("writeLimit")),
            0, 0, 0
        );
        writeStreamClass.AddMethod("size", sizeMethod);
    }
    
    /// <summary>
    /// Installs Object class methods that support compilation.
    /// </summary>
    private static void InstallObjectCompilationSupportMethods()
    {
        var objectClass = SystemDictionary.ObjectClass;
        if (objectClass == null)
        {
            Console.WriteLine("[WARNING] Object class not found");
            return;
        }
        
        // compilerClass
        // "Return the compiler class to use"
        var compilerClassMethod = SCompiledMethod.CreateCompiledMethod(
            "compilerClass",
            [65, 124],
            SArray.From(SSymbol.Intern("Compiler")),
            0, 0, 0
        );
        // Note: Object class methods should be added to Object's class side
        // For now, add to instance side as a workaround
        objectClass.AddMethod("compilerClass", compilerClassMethod);
        
        // compile:classified:
        // "Compile source code with classification"
        var compileClassifiedMethod = SCompiledMethod.CreateCompiledMethod(
            "compile:classified:",
            [112, 16, 115, 115, 115, 240, 105, 112, 17, 21, 131, 98, 135, 21, 124],
            SArray.From(
                SSymbol.Intern("compile:notifying:trailer:ifFail:"),
                SSymbol.Intern("classify:under:")
            ),
            2, 1, 0
        );
        // Note: Object class methods should be added to Object's class side
        // For now, add to instance side as a workaround
        objectClass.AddMethod("compile:classified:", compileClassifiedMethod);
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Object compilation support methods installed");
        }
    }
    
    /// <summary>
    /// Installs CompiledMethod class creation methods.
    /// </summary>
    private static void InstallCompiledMethodCreationMethods()
    {
        var compiledMethodClass = SystemDictionary.CompiledMethodClass;
        if (compiledMethodClass == null)
        {
            Console.WriteLine("[WARNING] CompiledMethod class not found");
            return;
        }
        
        // newMethod:header:
        // "Create a new compiled method with bytecodes and header"
        var newMethodHeaderMethod = SCompiledMethod.CreateCompiledMethod(
            "newMethod:header:",
            [66, 208, 104, 16, 16, 194, 249, 105, 17, 118, 16, 194, 240, 106, 16, 118, 21, 195, 107, 
             18, 20, 193, 102, 22, 20, 194, 103, 20, 124],
            SArray.From(
                SSymbol.Intern("basicNew:"),
                SSymbol.Intern("size"),
                SSymbol.Intern("numLiterals:"),
                SSymbol.Intern("at:put:"),
                SSymbol.Intern("header"),
                SSymbol.Intern("bytecodes")
            ),
            2, 4, 0
        );
        // Note: CompiledMethod class methods should be added to CompiledMethod's class side
        // For now, add to instance side as a workaround
        compiledMethodClass.AddMethod("newMethod:header:", newMethodHeaderMethod);
        
        // numArgs:numTemps:primitive:literals:
        // "Create a method with specified parameters"
        var numArgsMethod = SCompiledMethod.CreateCompiledMethod(
            "numArgs:numTemps:primitive:literals:",
            [19, 194, 104, 66, 20, 208, 105, 21, 118, 225, 102, 21, 16, 196, 103, 21, 17, 197, 104, 
             21, 18, 198, 105, 21, 20, 199, 106, 21, 124],
            SArray.From(
                SSymbol.Intern("size"),
                SSymbol.Intern("newMethod:header:"),
                SSymbol.Intern("numArgs:"),
                SSymbol.Intern("numTemps:"),
                SSymbol.Intern("primitive:"),
                SSymbol.Intern("literals:")
            ),
            4, 2, 0
        );
        // Note: CompiledMethod class methods should be added to CompiledMethod's class side
        // For now, add to instance side as a workaround
        compiledMethodClass.AddMethod("numArgs:numTemps:primitive:literals:", numArgsMethod);
        
        // copyWithTempsFromMethodNode:
        // "Copy temporaries from a method node"
        var copyWithTempsMethod = SCompiledMethod.CreateCompiledMethod(
            "copyWithTempsFromMethodNode:",
            [112, 16, 210, 104, 20, 215, 155, 120, 112, 20, 117, 194, 197, 106, 112, 20, 117, 22, 
             196, 105, 21, 124],
            SArray.From(
                SSymbol.Intern("temporaries"),
                SSymbol.Intern("tempNames"),
                SSymbol.Intern("isEmpty"),
                SSymbol.Intern("size"),
                SSymbol.Intern("at:put:")
            ),
            1, 3, 0
        );
        compiledMethodClass.AddMethod("copyWithTempsFromMethodNode:", copyWithTempsMethod);
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] CompiledMethod creation methods installed");
        }
    }
}