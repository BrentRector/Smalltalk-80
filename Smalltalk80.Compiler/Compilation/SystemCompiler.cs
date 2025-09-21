namespace Smalltalk80.Compiler.Compilation;

using System.Text.Json;
using System.Text.Json.Serialization;
using Antlr4.Runtime;
using Smalltalk80.Compiler.AST;
using Smalltalk80.Compiler.AST.Processors;
using Smalltalk80.Compiler.Compilation.Models;
using Smalltalk80.Compiler.Generated;

/// <summary>
/// Modern C# 13 implementation of a Smalltalk-80 system compiler.
/// Processes file-in format source code and generates JSON output containing
/// all classes, methods, and initialization expressions with full bytecode.
/// </summary>
public sealed class SystemCompiler : IAstProcessor, IAstVisitor<object?>
{
    private readonly CompilationContext _compilationContext = new();
    private readonly Dictionary<string, string> _sourceFragments = new(StringComparer.Ordinal);
    private ProcessorContext? _processorContext;
    private ITokenStream? _tokenStream;
    private string _currentSourceCode = string.Empty;

    /// <summary>
    /// Gets the name of this processor for identification.
    /// </summary>
    public string Name => "Modern System Compiler v2.0";

    /// <summary>
    /// Processes an AST node and generates complete system bytecode output.
    /// </summary>
    /// <param name="node">The root AST node to process.</param>
    /// <param name="context">The processor context with source information.</param>
    /// <returns>A BytecodeResult containing the JSON output or error information.</returns>
    public ProcessorResult Process(AstNode node, ProcessorContext context)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(context);
        
        // Reset state for fresh compilation
        _compilationContext.Reset();
        _sourceFragments.Clear();
        _processorContext = context;
        _currentSourceCode = context.SourceCode;
        
        try
        {
            // For file-in format, always use direct parsing to preserve metadata
            // The AstBuilder doesn't capture class-side and protocol information
            if (context.SourceCode.Contains('!'))
            {
                // Parse file-in format source directly
                ProcessFileInFormat(context.SourceCode);
            }
            else if (node is ProgramNode program)
            {
                // Direct AST processing for non-file-in format
                ProcessProgramNode(program);
            }
            else
            {
                // Fallback: attempt file-in parsing
                ProcessFileInFormat(context.SourceCode);
            }
            
            // Generate and return JSON output
            var jsonOutput = GenerateJsonOutput();
            return new BytecodeResult(jsonOutput);
        }
        catch (Exception ex)
        {
            return BytecodeResult.Failure($"Compilation failed: {ex.Message}\nStack trace: {ex.StackTrace}");
        }
    }

    #region Processing Methods
    
    /// <summary>
    /// Processes a ProgramNode containing multiple compilation units.
    /// </summary>
    /// <param name="program">The program node to process.</param>
    private void ProcessProgramNode(ProgramNode program)
    {
        // Track the current class for method association
        string? currentClassName = null;
        bool isClassSide = false;
        
        foreach (var element in program.Elements)
        {
            // Check if this is a class definition
            if (element is ClassDefinitionNode classDef)
            {
                // Process the class definition
                element.Accept(this);
                
                // Set this as the current class for subsequent methods
                currentClassName = classDef.ClassName;
                isClassSide = false; // Reset to instance side
                
                // Set the class context for subsequent method processing
                _compilationContext.SetClassContext(classDef.ClassName, isClassSide);
            }
            // Check if this is a nested ProgramNode (from method category chunks)
            else if (element is ProgramNode nestedProgram)
            {
                // Process methods in the nested program with current class context
                if (currentClassName != null)
                {
                    _compilationContext.SetClassContext(currentClassName, isClassSide);
                }
                
                foreach (var nestedElement in nestedProgram.Elements)
                {
                    if (nestedElement is MethodDefinitionNode method && currentClassName != null)
                    {
                        // Ensure the class context is set before processing the method
                        _compilationContext.SetClassContext(currentClassName, isClassSide);
                    }
                    nestedElement.Accept(this);
                }
            }
            // Process other elements normally
            else
            {
                element.Accept(this);
            }
        }
    }
    
    /// <summary>
    /// Processes Smalltalk file-in format source code.
    /// </summary>
    /// <param name="sourceCode">The source code in file-in format.</param>
    private void ProcessFileInFormat(string sourceCode)
    {
        // Set up ANTLR parsing pipeline
        var inputStream = new AntlrInputStream(sourceCode);
        var lexer = new Smalltalk80Lexer(inputStream);
        _tokenStream = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(_tokenStream);
        
        // Parse as file-in format
        var fileInTree = parser.fileIn();
        
        // Check for parsing errors
        if (parser.NumberOfSyntaxErrors > 0)
        {
            throw new InvalidOperationException($"Syntax errors found: {parser.NumberOfSyntaxErrors} errors");
        }
        
        // Process the parse tree using our custom visitor
        var fileInVisitor = new FileInFormatVisitor(this, _tokenStream);
        fileInVisitor.Visit(fileInTree);
    }

    /// <summary>
    /// Generates the final JSON output from compiled data.
    /// </summary>
    /// <returns>JSON string representation of the compilation output.</returns>
    private string GenerateJsonOutput()
    {
        // Build output structure with modern C# features
        var output = new Dictionary<string, object>
        {
            ["$schema"] = "./bootstrap-schema.json",
            ["description"] = $"Compiled from source on {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            ["classes"] = _compilationContext.Classes.Values.Select(c => new
            {
                name = c.Name,
                superclass = c.Superclass,
                instanceVariables = c.InstanceVariables,
                classVariables = c.ClassVariables,
                poolDictionaries = c.PoolDictionaries,
                category = c.Category,
                comment = c.Comment,
                methods = c.MethodReferences
            }).ToArray(),
            ["methods"] = _compilationContext.Methods.Select(m => new
            {
                @class = m.ClassName,
                selector = m.Selector,
                bytecode = m.Bytecode.Select(b => (int)b).ToArray(),
                literals = m.Literals.Select(ConvertLiteralForJson).ToArray(),
                primitiveIndex = m.PrimitiveIndex,
                numArgs = m.NumArgs,
                numTemps = m.NumTemps,
                stackDepth = m.StackDepth,
                isClassMethod = m.IsClassMethod,
                protocol = m.Protocol,
                sourceCode = m.SourceCode,
                argumentNames = m.ArgumentNames,
                temporaryNames = m.TemporaryNames
            }).ToArray(),
            ["initializationExpressions"] = _compilationContext.InitializationExpressions.Select(expr => new
            {
                bytecode = expr.Bytecode.Select(b => (int)b).ToArray(),
                literals = expr.Literals.Select(ConvertLiteralForJson).ToArray(),
                numTemps = expr.NumTemps,
                stackDepth = expr.StackDepth,
                source = expr.Source,
                receiver = expr.Receiver,
                selector = expr.Selector,
                arguments = expr.Arguments
            }).ToArray()
        };

        // Configure JSON serialization with modern options
        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        
        return JsonSerializer.Serialize(output, options);
    }

    
    #endregion
    
    #region IAstVisitor Implementation
    
    /// <inheritdoc/>
    public object? VisitProgram(ProgramNode node)
    {
        ProcessProgramNode(node);
        return null;
    }
    
    /// <inheritdoc/>
    public object? VisitClassDefinition(ClassDefinitionNode node)
    {
        // Create and register class definition
        ClassDefinition classDef = new(node.ClassName, node.SuperclassName)
        {
            InstanceVariables = node.InstanceVariables.ToList(),
            ClassVariables = node.ClassVariables.ToList(),
            PoolDictionaries = node.PoolDictionaries.ToList(),
            Category = node.Category ?? string.Empty,
            Comment = null // ClassDefinitionNode doesn't have Comment property
        };
        
        _compilationContext.AddOrUpdateClass(classDef);
        return null;
    }
    
    /// <inheritdoc/>
    public object? VisitMethodDefinition(MethodDefinitionNode node)
    {
        if (_compilationContext.CurrentClass is null)
        {
            // Create anonymous class if needed
            var anonymousClass = new ClassDefinition("AnonymousClass", "Object");
            _compilationContext.AddOrUpdateClass(anonymousClass);
            _compilationContext.SetClassContext("AnonymousClass", false);
        }
        
        // Compile method using bytecode generator
        var generator = new BytecodeGenerator(_compilationContext);
        node.Accept(generator);
        
        // Create compiled method
        CompiledMethod method = new()
        {
            ClassName = _compilationContext.IsClassMethod 
                ? $"{_compilationContext.CurrentClass?.Name} class" 
                : _compilationContext.CurrentClass?.Name ?? "Unknown",
            Selector = node.Selector,
            Bytecode = generator.Bytecode.ToList(),
            Literals = generator.LastMethodLiterals.ToList(),
            PrimitiveIndex = node.Primitive?.PrimitiveNumber ?? 0,
            NumArgs = node.Arguments.Length,
            NumTemps = node.Temporaries.Length,
            StackDepth = generator.MaxStackDepth,
            IsClassMethod = _compilationContext.IsClassMethod,
            Protocol = _compilationContext.CurrentProtocol,
            SourceCode = GetSourceFragment(node),
            ArgumentNames = node.Arguments.Select(a => a.Name).ToList(),
            TemporaryNames = node.Temporaries.Select(t => t.Name).ToList()
        };
        
        _compilationContext.AddMethod(method);
        return null;
    }
    
    // Delegate all other visitor methods to return null (no-op)
    public object? VisitStatementList(StatementListNode node) => null;
    public object? VisitReturnStatement(ReturnStatementNode node) => null;
    public object? VisitExpressionStatement(ExpressionStatementNode node) => null;
    public object? VisitAssignment(AssignmentNode node) => null;
    public object? VisitCascade(CascadeNode node) => null;
    public object? VisitMessageSend(MessageSendNode node) => null;
    public object? VisitBlock(BlockNode node) => null;
    public object? VisitVariable(VariableNode node) => null;
    public object? VisitNumberLiteral(NumberLiteralNode node) => null;
    public object? VisitStringLiteral(StringLiteralNode node) => null;
    public object? VisitSymbolLiteral(SymbolLiteralNode node) => null;
    public object? VisitArrayLiteral(ArrayLiteralNode node) => null;
    public object? VisitCharacterLiteral(CharacterLiteralNode node) => null;
    public object? VisitBooleanLiteral(BooleanLiteralNode node) => null;
    public object? VisitNilLiteral(NilLiteralNode node) => null;
    public object? VisitSelfLiteral(SelfLiteralNode node) => null;
    public object? VisitSuperLiteral(SuperLiteralNode node) => null;
    public object? VisitUnaryMessage(UnaryMessageNode node) => null;
    public object? VisitBinaryMessage(BinaryMessageNode node) => null;
    public object? VisitKeywordMessage(KeywordMessageNode node) => null;
    public object? VisitTemporaryDeclaration(TemporaryDeclarationNode node) => null;
    public object? VisitBlockArgument(BlockArgumentNode node) => null;
    public object? VisitMethodArgument(MethodArgumentNode node) => null;
    public object? VisitPrimitiveDeclaration(PrimitiveDeclarationNode node) => null;
    
    #endregion
    
    #region Helper Methods
    
    /// <summary>
    /// Converts a literal object to its JSON representation.
    /// </summary>
    /// <param name="literal">The literal to convert.</param>
    /// <returns>The JSON-compatible representation.</returns>
    private static object ConvertLiteralForJson(object literal) => literal switch
    {
        Symbol symbol => $"#{symbol.Name}",
        Association assoc => new 
        { 
            type = "Association",
            key = $"#{assoc.Key}",
            value = assoc.Value
        },
        object[] array => array.Select(ConvertLiteralForJson).ToArray(),
        null => "nil",
        bool b => b ? "true" : "false",
        _ => literal
    };
    
    /// <summary>
    /// Gets the source code fragment for an AST node.
    /// </summary>
    /// <param name="node">The AST node.</param>
    /// <returns>The source code text.</returns>
    private string GetSourceFragment(AstNode node)
    {
        // In a full implementation, this would extract from original source
        return _sourceFragments.GetValueOrDefault(node.GetHashCode().ToString(), string.Empty);
    }
    
    #endregion

    #region Nested Types
    
    /// <summary>
    /// Visitor for processing ANTLR parse tree in file-in format.
    /// Extracts classes, methods, and initialization expressions.
    /// </summary>
    private sealed class FileInFormatVisitor : Smalltalk80BaseVisitor<object?>
    {
        private readonly SystemCompiler _compiler;
        private readonly ITokenStream _tokenStream;
        private readonly AstBuilder _astBuilder;
        
        /// <summary>
        /// Initializes a new instance of the FileInFormatVisitor class.
        /// </summary>
        /// <param name="compiler">The parent compiler instance.</param>
        /// <param name="tokenStream">The token stream for source extraction.</param>
        public FileInFormatVisitor(SystemCompiler compiler, ITokenStream tokenStream)
        {
            _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
            _tokenStream = tokenStream ?? throw new ArgumentNullException(nameof(tokenStream));
            _astBuilder = new AstBuilder(_compiler._currentSourceCode);
        }

        /// <summary>
        /// Processes a class definition chunk from the parse tree.
        /// </summary>
        public override object? VisitClassDefChunk(Smalltalk80Parser.ClassDefChunkContext context)
        {
            // Extract class information using modern pattern matching
            var classHeader = context.classHeader();
            var className = ExtractSymbolName(classHeader.symbolConst()?.GetText());
            var superclass = classHeader.className()?.GetText() ?? "Object";
            
            // Parse class body specifications
            var classBody = context.classBody();
            List<string> instanceVars = [];
            List<string> classVars = [];
            List<string> poolDicts = [];
            var category = string.Empty;
            
            // Process each specification using pattern matching
            foreach (var spec in classBody.classSlotSpec())
            {
                var specType = spec.identifierColon()?.GetText();
                var specValue = ExtractStringLiteral(spec.stringConst()?.GetText());
                
                (instanceVars, classVars, poolDicts) = specType switch
                {
                    "instanceVariableNames:" => (ParseVariableNames(specValue), classVars, poolDicts),
                    "classVariableNames:" => (instanceVars, ParseVariableNames(specValue), poolDicts),
                    "poolDictionaries:" => (instanceVars, classVars, ParseVariableNames(specValue)),
                    _ => (instanceVars, classVars, poolDicts)
                };
            }
            
            // Extract category if present
            if (classBody.categoryDecl() is { } categoryDecl)
            {
                category = ExtractStringLiteral(categoryDecl.stringConst()?.GetText());
            }
            
            // Create and register class definition with modern syntax
            ClassDefinition classDef = new(className, superclass)
            {
                InstanceVariables = instanceVars,
                ClassVariables = classVars,
                PoolDictionaries = poolDicts,
                Category = category,
                Comment = null // TODO: Extract from comment chunks
            };
            
            _compiler._compilationContext.AddOrUpdateClass(classDef);
            _compiler._compilationContext.SetClassContext(className, false);
            
            return base.VisitClassDefChunk(context);
        }

        /// <summary>
        /// Processes a simple method chunk from the parse tree.
        /// </summary>
        public override object? VisitSimpleMethodChunk(Smalltalk80Parser.SimpleMethodChunkContext context)
        {
            var className = context.className()?.GetText() ?? string.Empty;
            var isClassMethod = context.IDENTIFIER()?.GetText() == "class";
            
            // Ensure class exists with modern null-coalescing
            if (_compiler._compilationContext.SetClassContext(className, isClassMethod) is null)
            {
                // Create default class if not found
                ClassDefinition defaultClass = new(className, "Object");
                _compiler._compilationContext.AddOrUpdateClass(defaultClass);
                _compiler._compilationContext.SetClassContext(className, isClassMethod);
            }
            
            _compiler._compilationContext.SetProtocol("unclassified");
            
            // Compile the method
            CompileMethod(context.methodDef());
            
            return base.VisitSimpleMethodChunk(context);
        }

        /// <summary>
        /// Processes a method category chunk containing multiple methods.
        /// </summary>
        public override object? VisitMethodCategoryChunk(Smalltalk80Parser.MethodCategoryChunkContext context)
        {
            var className = context.className()?.GetText() ?? string.Empty;
            
            // Check if this is a class method chunk
            // The grammar has: className (cls = IDENTIFIER)?
            // If IDENTIFIER is present and equals "class", it's a class method
            var identifierToken = context.IDENTIFIER();
            var isClassMethod = identifierToken != null && identifierToken.GetText() == "class";
            
            // Ensure class exists
            if (_compiler._compilationContext.SetClassContext(className, isClassMethod) is null)
            {
                ClassDefinition newClass = new(className, "Object");
                _compiler._compilationContext.AddOrUpdateClass(newClass);
                _compiler._compilationContext.SetClassContext(className, isClassMethod);
            }
            
            // Extract and set protocol
            var protocol = ExtractStringLiteral(context.stringConst()?.GetText());
            _compiler._compilationContext.SetProtocol(protocol);
            
            // Compile all methods in this category using foreach
            foreach (var methodDef in context.methodDef())
            {
                CompileMethod(methodDef);
            }
            
            return base.VisitMethodCategoryChunk(context);
        }

        /// <summary>
        /// Processes an expression chunk (initialization expression).
        /// </summary>
        public override object? VisitExpressionChunk(Smalltalk80Parser.ExpressionChunkContext context)
        {
            var expression = context.expression();
            
            // Extract source text using modern interval syntax
            var sourceText = ExtractSourceText(expression.Start, expression.Stop);
            
            // Create expression scope for compilation
            CompilationScope exprScope = new()
            {
                Type = ScopeType.Expression,
                Temporaries = [],
                Arguments = []
            };
            
            _compiler._compilationContext.PushScope(exprScope);
            
            try
            {
                // Compile expression directly from parse tree (like StandaloneCompiler does)
                var compiledExpression = CompileExpressionFromParseTree(expression, exprScope);
                
                if (compiledExpression == null)
                {
                    Console.Error.WriteLine($"Failed to compile expression: {sourceText}");
                    return base.VisitExpressionChunk(context);
                }
                
                // Extract message send information if present
                var (receiver, selector, args) = ExtractMessageInfo(expression);
                
                // Create initialization expression
                InitializationExpression initExpr = new()
                {
                    Bytecode = compiledExpression.Bytecode,
                    Literals = compiledExpression.Literals,
                    NumTemps = 0,
                    StackDepth = compiledExpression.StackDepth,
                    Source = sourceText,
                    Receiver = receiver,
                    Selector = selector,
                    Arguments = args
                };
                
                _compiler._compilationContext.AddInitializationExpression(initExpr);
            }
            finally
            {
                _compiler._compilationContext.PopScope();
            }
            
            return base.VisitExpressionChunk(context);
        }

        /// <summary>
        /// Compiles an expression directly from the parse tree.
        /// </summary>
        private CompiledExpression? CompileExpressionFromParseTree(
            Smalltalk80Parser.ExpressionContext expression,
            CompilationScope scope)
        {
            try
            {
                // Create a simple visitor to walk the parse tree and generate bytecode
                var visitor = new ExpressionCompilerVisitor(_compiler._compilationContext, scope);
                visitor.Visit(expression);
                
                // Add return bytecode if not present
                var bytecode = visitor.Bytecode;
                if (bytecode.Count == 0 || !IsReturnBytecode(bytecode[^1]))
                {
                    // Return top of stack
                    bytecode.Add(124); // returnTop
                }
                
                return new CompiledExpression
                {
                    Bytecode = bytecode,
                    Literals = scope.Literals,
                    StackDepth = visitor.MaxStackDepth
                };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Expression compilation failed: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Checks if a bytecode is a return instruction.
        /// </summary>
        private static bool IsReturnBytecode(byte bytecode)
        {
            // Return bytecodes: 120-125
            return bytecode >= 120 && bytecode <= 125;
        }
        
        /// <summary>
        /// Helper class to compile expressions from parse trees.
        /// </summary>
        private sealed class CompiledExpression
        {
            public List<byte> Bytecode { get; init; } = [];
            public List<object> Literals { get; init; } = [];
            public int StackDepth { get; init; }
        }
        
        /// <summary>
        /// Compiles a method definition from the parse tree.
        /// </summary>
        /// <param name="context">The method definition context.</param>
        private void CompileMethod(Smalltalk80Parser.MethodDefContext context)
        {
            if (_compiler._compilationContext.CurrentClass is null) 
                return;
            
            try
            {
                // Build AST for the method
                var methodAst = _astBuilder.Visit(context);
                
                if (methodAst is not MethodDefinitionNode methodDef)
                {
                    Console.Error.WriteLine($"Failed to build method AST in {_compiler._compilationContext.CurrentClass.Name}");
                    return;
                }
                
                // Extract source code
                var sourceText = ExtractSourceText(context.Start, context.Stop);
                
                // Store source fragment for later reference
                _compiler._sourceFragments[methodDef.GetHashCode().ToString()] = sourceText;
                
                // Extract argument and temporary names
                var (argumentNames, temporaryNames) = ExtractMethodVariables(context);
                
                // Set up instance variables
                var instanceVars = _compiler._compilationContext.IsClassMethod 
                    ? [] 
                    : _compiler._compilationContext.ComputeInheritedInstanceVariables(
                        _compiler._compilationContext.CurrentClass.Name);
                
                // Create method scope
                CompilationScope methodScope = new()
                {
                    Type = ScopeType.Method,
                    Arguments = argumentNames,
                    Temporaries = temporaryNames,
                    InstanceVariables = instanceVars,
                    ClassVariables = _compiler._compilationContext.CurrentClass.ClassVariables
                };
                
                _compiler._compilationContext.PushScope(methodScope);
                
                try
                {
                    // Generate bytecode
                    var generator = new BytecodeGenerator(_compiler._compilationContext);
                    methodDef.Accept(generator);
                    
                    // Create compiled method with all metadata
                    CompiledMethod method = new()
                    {
                        ClassName = _compiler._compilationContext.IsClassMethod 
                            ? $"{_compiler._compilationContext.CurrentClass.Name} class" 
                            : _compiler._compilationContext.CurrentClass.Name,
                        Selector = methodDef.Selector,
                        Bytecode = generator.Bytecode.ToList(),
                        Literals = generator.LastMethodLiterals.ToList(),
                        PrimitiveIndex = methodDef.Primitive?.PrimitiveNumber ?? 0,
                        NumArgs = argumentNames.Count,
                        NumTemps = temporaryNames.Count,
                        StackDepth = generator.MaxStackDepth,
                        IsClassMethod = _compiler._compilationContext.IsClassMethod,
                        Protocol = _compiler._compilationContext.CurrentProtocol,
                        SourceCode = sourceText,
                        ArgumentNames = argumentNames,
                        TemporaryNames = temporaryNames
                    };
                    
                    _compiler._compilationContext.AddMethod(method);
                }
                finally
                {
                    _compiler._compilationContext.PopScope();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Method compilation failed: {ex.Message}");
            }
        }

        
        /// <summary>
        /// Extracts source text from parse tree tokens.
        /// </summary>
        private string ExtractSourceText(IToken start, IToken? stop)
        {
            var charStream = start.InputStream;
            var startIndex = start.StartIndex;
            var stopIndex = stop?.StopIndex ?? startIndex;
            return charStream.GetText(new Antlr4.Runtime.Misc.Interval(startIndex, stopIndex));
        }
        
        /// <summary>
        /// Extracts symbol name from a symbol constant string.
        /// </summary>
        private static string ExtractSymbolName(string? symbolText)
        {
            if (string.IsNullOrEmpty(symbolText))
                return string.Empty;
            
            return symbolText.TrimStart('#');
        }
        
        /// <summary>
        /// Extracts string literal value removing quotes.
        /// </summary>
        private static string ExtractStringLiteral(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            
            text = text.Trim();
            
            // Remove surrounding quotes using pattern matching
            return text switch
            {
                { Length: > 1 } when text[0] == '\'' && text[^1] == '\'' => text[1..^1],
                { Length: > 1 } when text[0] == '"' && text[^1] == '"' => text[1..^1],
                _ => text
            };
        }
        
        /// <summary>
        /// Parses variable names from a space-delimited string.
        /// </summary>
        private static List<string> ParseVariableNames(string? varString)
        {
            if (string.IsNullOrWhiteSpace(varString))
                return [];
            
            // Split on whitespace and filter empty entries
            return [.. varString.Split(' ', '\t', '\r', '\n')
                .Where(s => !string.IsNullOrEmpty(s))];
        }
        
        /// <summary>
        /// Extracts argument and temporary variable names from a method definition.
        /// </summary>
        private static (List<string> arguments, List<string> temporaries) ExtractMethodVariables(
            Smalltalk80Parser.MethodDefContext context)
        {
            List<string> arguments = [];
            List<string> temporaries = [];
            
            // Extract arguments from method header
            var methodHeader = context.methodHeader();
            if (methodHeader is not null)
            {
                // Handle different method types
                if (methodHeader.binarySelector() is not null && methodHeader.formalArg(0) is not null)
                {
                    arguments.Add(methodHeader.formalArg(0).GetText());
                }
                else if (methodHeader.identifierColon() is not null)
                {
                    arguments.AddRange(methodHeader.formalArg().Select(arg => arg.GetText()));
                }
            }
            
            // Extract temporaries
            if (context.temporaries() is { } temps)
            {
                temporaries.AddRange(temps.temporaryName().Select(t => t.GetText()));
            }
            
            return (arguments, temporaries);
        }
        
        /// <summary>
        /// Extracts message send information from an expression.
        /// </summary>
        private (string? receiver, string? selector, IReadOnlyList<string>? arguments) 
            ExtractMessageInfo(Smalltalk80Parser.ExpressionContext expression)
        {
            var assignmentExpr = expression.assignmentExpr();
            if (assignmentExpr?.cascadeExpr()?.messageExpr() is not { } messageExpr)
                return (null, null, null);
            
            // Extract receiver
            string? receiver = null;
            if (messageExpr.messageBase() is { } messageBase)
            {
                receiver = _tokenStream.GetText(messageBase.Start, messageBase.Stop);
            }
            
            // Extract selector and arguments based on message type
            string? selector = null;
            List<string> arguments = [];
            
            // Try unary message
            if (messageExpr.unaryMessage() is { Length: > 0 } unaryMessages)
            {
                selector = unaryMessages[0].unarySelector()?.GetText();
            }
            // Try binary message
            else if (messageExpr.binaryMessage() is { Length: > 0 } binaryMessages)
            {
                selector = binaryMessages[0].binarySelector()?.GetText();
                if (binaryMessages[0].messageBase() is { } arg)
                {
                    arguments.Add(_tokenStream.GetText(arg.Start, arg.Stop));
                }
            }
            // Try keyword message
            else if (messageExpr.keywordMessage() is { } keywordMessage)
            {
                var keywordParts = keywordMessage.keywordPart();
                selector = string.Concat(keywordParts.Select(p => $"{p.IDENTIFIER()?.GetText()}:"));
                
                foreach (var part in keywordParts)
                {
                    if (part.nonKeywordExpr() is { } argExpr)
                    {
                        arguments.Add(_tokenStream.GetText(argExpr.Start, argExpr.Stop));
                    }
                }
            }
            
            return (receiver, selector, arguments.Count > 0 ? arguments : null);
        }
    }
    
    #endregion
}