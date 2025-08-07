using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Antlr4.Runtime;
using Smalltalk80.Generated;

namespace Smalltalk80.Parser;

/// <summary>
/// Enhanced parser integration with optimized cascade message handling,
/// improved temporary variable parsing, robust error handling, and
/// comprehensive file-in loading capabilities.
/// </summary>
public static class EnhancedParserIntegration
{
    /// <summary>
    /// Enhanced cascade message integration with optimized AST building
    /// and bytecode generation for multiple receivers.
    /// </summary>
    public class OptimizedCascadeVisitor : BaseAstBuildingVisitor
    {
        private readonly Dictionary<string, int> _temporaryVariableCache = new();
        private readonly List<ParseError> _errors = new();

        public IReadOnlyList<ParseError> Errors => _errors;

        /// <summary>
        /// Optimized cascade expression handling with receiver caching
        /// and efficient message dispatch.
        /// </summary>
        public override object? VisitCascadeExpr(Smalltalk80Parser.CascadeExprContext context)
        {
            try
            {
                // Parse the primary receiver expression
                var receiver = (ExpressionNode)Visit(context.messageExpr())!;
                var messages = new List<MessageNode>();

                // Process each cascade message with optimized handling
                foreach (var cascadeMsg in context.cascadeMessage())
                {
                    var message = ParseCascadeMessage(cascadeMsg);
                    if (message != null)
                    {
                        messages.Add(message);
                    }
                }

                // Create optimized cascade expression with receiver caching
                return new OptimizedCascadeExpression(receiver, messages)
                {
                    UseReceiverCache = messages.Count > 2, // Cache for multiple messages
                    InlineShortCascades = messages.Count <= 2 // Inline for efficiency
                };
            }
            catch (Exception ex)
            {
                _errors.Add(new ParseError(
                    context.Start.Line,
                    context.Start.Column,
                    $"Error parsing cascade expression: {ex.Message}"));
                return null;
            }
        }

        /// <summary>
        /// Enhanced message expression parsing with cascade optimization.
        /// </summary>
        public override object? VisitMessageExpr(Smalltalk80Parser.MessageExprContext context)
        {
            try
            {
                var current = (ExpressionNode)Visit(context.messageBase())!;

                // Optimized unary message chain processing
                foreach (var unary in context.unaryMessage())
                {
                    var selector = unary.IDENTIFIER().GetText();
                    current = new OptimizedUnarySendExpression(current, selector)
                    {
                        CanInline = IsInlinableUnaryMessage(selector),
                        UseDirectDispatch = true
                    };
                }

                // Optimized binary message chain processing
                foreach (var binary in context.binaryMessage())
                {
                    var selector = binary.binaryMessageSelector().GetText();
                    var argument = (ExpressionNode)Visit(binary.messageBase())!;
                    current = new OptimizedBinarySendExpression(current, selector, argument)
                    {
                        CanInline = IsInlinableBinaryMessage(selector),
                        UseDirectDispatch = IsPrimitiveSelector(selector)
                    };
                }

                // Optimized keyword message processing
                foreach (var keyword in context.keywordMessage())
                {
                    var parts = new List<string>();
                    var arguments = new List<ExpressionNode>();

                    foreach (var keywordPart in keyword.keywordMessagePart())
                    {
                        parts.Add(keywordPart.KEYWORD().GetText());
                        arguments.Add((ExpressionNode)Visit(keywordPart.messageExpr())!);
                    }

                    var selector = string.Join("", parts);
                    current = new OptimizedKeywordSendExpression(current, parts, arguments)
                    {
                        CanInline = IsInlinableKeywordMessage(selector),
                        UseDirectDispatch = IsPrimitiveSelector(selector),
                        ArgumentCount = arguments.Count
                    };
                }

                return current;
            }
            catch (Exception ex)
            {
                _errors.Add(new ParseError(
                    context.Start.Line,
                    context.Start.Column,
                    $"Error parsing message expression: {ex.Message}"));
                return new VariableExpression("nil"); // Fallback
            }
        }

        private MessageNode? ParseCascadeMessage(Smalltalk80Parser.CascadeMessageContext context)
        {
            try
            {
                if (context.unaryMessage() != null)
                {
                    var selector = context.unaryMessage().IDENTIFIER().GetText();
                    return new UnaryMessageNode(selector);
                }
                
                if (context.binaryMessage() != null)
                {
                    var selector = context.binaryMessage().binaryMessageSelector().GetText();
                    var argument = (ExpressionNode)Visit(context.binaryMessage().messageBase())!;
                    return new BinaryMessageNode(selector, argument);
                }
                
                if (context.keywordMessage() != null)
                {
                    var parts = new List<string>();
                    var arguments = new List<ExpressionNode>();

                    foreach (var keywordPart in context.keywordMessage().keywordMessagePart())
                    {
                        parts.Add(keywordPart.KEYWORD().GetText());
                        arguments.Add((ExpressionNode)Visit(keywordPart.messageExpr())!);
                    }

                    return new KeywordMessageNode(parts, arguments);
                }

                return null;
            }
            catch (Exception ex)
            {
                _errors.Add(new ParseError(
                    context.Start.Line,
                    context.Start.Column,
                    $"Error parsing cascade message: {ex.Message}"));
                return null;
            }
        }

        private bool IsInlinableUnaryMessage(string selector) =>
            selector switch
            {
                "class" or "size" or "yourself" or "isNil" or "notNil" => true,
                _ => false
            };

        private bool IsInlinableBinaryMessage(string selector) =>
            selector switch
            {
                "+" or "-" or "*" or "/" or "=" or "~=" or "<" or ">" or "<=" or ">=" => true,
                _ => false
            };

        private bool IsInlinableKeywordMessage(string selector) =>
            selector switch
            {
                "at:" or "at:put:" or "new:" or "ifTrue:" or "ifFalse:" or "ifTrue:ifFalse:" => true,
                _ => false
            };

        private bool IsPrimitiveSelector(string selector) =>
            selector switch
            {
                "+" or "-" or "*" or "/" or "=" or "~=" or "<" or ">" or "<=" or ">=" or
                "at:" or "at:put:" or "size" or "class" => true,
                _ => false
            };
    }

    /// <summary>
    /// Enhanced temporary variable parsing with improved scope tracking,
    /// variable resolution, and conflict detection.
    /// </summary>
    public class EnhancedTemporaryVariableParser
    {
        private readonly Dictionary<string, VariableScope> _scopes = new();
        private readonly List<ParseError> _errors = new();
        private int _currentScopeLevel = 0;

        public IReadOnlyList<ParseError> Errors => _errors;

        /// <summary>
        /// Parse temporaries with enhanced scope tracking and validation.
        /// </summary>
        public TemporaryVariableInfo ParseTemporaries(
            Smalltalk80Parser.TemporariesContext? context,
            IReadOnlyList<string> arguments,
            IReadOnlyList<string> instanceVariables)
        {
            var temporaries = new List<string>();
            var scopeInfo = new Dictionary<string, VariableInfo>();

            // Register arguments in scope
            for (int i = 0; i < arguments.Count; i++)
            {
                var argName = arguments[i];
                scopeInfo[argName] = new VariableInfo
                {
                    Name = argName,
                    Type = VariableType.Argument,
                    Index = i,
                    ScopeLevel = 0,
                    IsReadOnly = true
                };
            }

            // Register instance variables
            for (int i = 0; i < instanceVariables.Count; i++)
            {
                var instVarName = instanceVariables[i];
                scopeInfo[instVarName] = new VariableInfo
                {
                    Name = instVarName,
                    Type = VariableType.InstanceVariable,
                    Index = i,
                    ScopeLevel = 0,
                    IsReadOnly = false
                };
            }

            // Parse temporary variables with validation
            if (context != null)
            {
                foreach (var tempContext in context.temporaryName())
                {
                    var tempName = tempContext.GetText().Trim();
                    
                    // Validate temporary variable name
                    if (string.IsNullOrWhiteSpace(tempName))
                    {
                        _errors.Add(new ParseError(
                            tempContext.Start.Line,
                            tempContext.Start.Column,
                            "Empty temporary variable name"));
                        continue;
                    }

                    // Check for conflicts with arguments
                    if (arguments.Contains(tempName))
                    {
                        _errors.Add(new ParseError(
                            tempContext.Start.Line,
                            tempContext.Start.Column,
                            $"Temporary variable '{tempName}' conflicts with argument"));
                        continue;
                    }

                    // Check for conflicts with instance variables
                    if (instanceVariables.Contains(tempName))
                    {
                        _errors.Add(new ParseError(
                            tempContext.Start.Line,
                            tempContext.Start.Column,
                            $"Temporary variable '{tempName}' shadows instance variable"));
                        // Allow but warn
                    }

                    // Check for duplicate temporaries
                    if (temporaries.Contains(tempName))
                    {
                        _errors.Add(new ParseError(
                            tempContext.Start.Line,
                            tempContext.Start.Column,
                            $"Duplicate temporary variable '{tempName}'"));
                        continue;
                    }

                    temporaries.Add(tempName);
                    scopeInfo[tempName] = new VariableInfo
                    {
                        Name = tempName,
                        Type = VariableType.Temporary,
                        Index = temporaries.Count - 1,
                        ScopeLevel = _currentScopeLevel,
                        IsReadOnly = false
                    };
                }
            }

            return new TemporaryVariableInfo
            {
                Temporaries = temporaries,
                ScopeInfo = scopeInfo,
                Arguments = arguments.ToList(),
                InstanceVariables = instanceVariables.ToList()
            };
        }

        /// <summary>
        /// Enhanced variable resolution with scope tracking.
        /// </summary>
        public VariableResolution ResolveVariable(string variableName, TemporaryVariableInfo scopeInfo)
        {
            if (scopeInfo.ScopeInfo.TryGetValue(variableName, out var varInfo))
            {
                return new VariableResolution
                {
                    Found = true,
                    VariableInfo = varInfo,
                    ResolutionType = varInfo.Type switch
                    {
                        VariableType.Argument => VariableResolutionType.Argument,
                        VariableType.Temporary => VariableResolutionType.Temporary,
                        VariableType.InstanceVariable => VariableResolutionType.InstanceVariable,
                        _ => VariableResolutionType.Unknown
                    }
                };
            }

            // Check for special variables
            if (IsSpecialVariable(variableName))
            {
                return new VariableResolution
                {
                    Found = true,
                    VariableInfo = new VariableInfo
                    {
                        Name = variableName,
                        Type = VariableType.Special,
                        Index = -1,
                        ScopeLevel = -1,
                        IsReadOnly = variableName != "self"
                    },
                    ResolutionType = VariableResolutionType.Special
                };
            }

            return new VariableResolution
            {
                Found = false,
                ResolutionType = VariableResolutionType.Unknown
            };
        }

        private bool IsSpecialVariable(string name) =>
            name is "self" or "super" or "nil" or "true" or "false";
    }

    /// <summary>
    /// Enhanced file-in loader with comprehensive error handling,
    /// progress reporting, and robust chunk processing.
    /// </summary>
    public class EnhancedFileInLoader
    {
        private readonly List<ParseError> _errors = new();
        private readonly List<LoadWarning> _warnings = new();
        private readonly Dictionary<string, int> _processedChunks = new();

        public IReadOnlyList<ParseError> Errors => _errors;
        public IReadOnlyList<LoadWarning> Warnings => _warnings;
        public IReadOnlyDictionary<string, int> ProcessedChunks => _processedChunks;

        /// <summary>
        /// Enhanced file loading with comprehensive error handling and progress tracking.
        /// </summary>
        public FileLoadResult LoadFile(string path, FileLoadOptions? options = null)
        {
            options ??= new FileLoadOptions();
            var result = new FileLoadResult { SourcePath = path };

            try
            {
                _errors.Clear();
                _warnings.Clear();
                _processedChunks.Clear();

                // Parse file with enhanced error handling
                FileNode fileAst;
                try
                {
                    fileAst = EnhancedFileInReader.Parse(path, options);
                }
                catch (ParseException ex)
                {
                    _errors.Add(new ParseError(0, 0, $"Failed to parse file: {ex.Message}"));
                    result.Success = false;
                    result.Errors = _errors.ToList();
                    return result;
                }

                var context = new LoadContext();
                
                // Process chunks with enhanced error recovery
                foreach (var chunk in fileAst.Chunks)
                {
                    try
                    {
                        ProcessChunk(chunk, context, options);
                        IncrementChunkCounter(chunk.GetType().Name);
                    }
                    catch (Exception ex)
                    {
                        _errors.Add(new ParseError(
                            chunk.LineNumber ?? 0,
                            chunk.ColumnNumber ?? 0,
                            $"Error processing {chunk.GetType().Name}: {ex.Message}"));

                        if (!options.ContinueOnError)
                        {
                            result.Success = false;
                            result.Errors = _errors.ToList();
                            result.Warnings = _warnings.ToList();
                            return result;
                        }
                    }
                }

                result.Success = _errors.Count == 0;
                result.Errors = _errors.ToList();
                result.Warnings = _warnings.ToList();
                result.ProcessedChunks = _processedChunks.ToDictionary(x => x.Key, x => x.Value);

                return result;
            }
            catch (Exception ex)
            {
                _errors.Add(new ParseError(0, 0, $"Unexpected error loading file: {ex.Message}"));
                result.Success = false;
                result.Errors = _errors.ToList();
                return result;
            }
        }

        private void ProcessChunk(ChunkNode chunk, LoadContext context, FileLoadOptions options)
        {
            switch (chunk)
            {
                case ClassDefinitionNode classDef:
                    ProcessClassDefinition(classDef, context, options);
                    break;

                case MethodDefinitionNode methodDef:
                    ProcessMethodDefinition(methodDef, context, options);
                    break;

                case MethodCategoryNode categoryNode:
                    ProcessMethodCategory(categoryNode, context, options);
                    break;

                case ClassCommentNode commentNode:
                    ProcessClassComment(commentNode, context, options);
                    break;

                case ScriptNode scriptNode:
                    ProcessScript(scriptNode, context, options);
                    break;

                case ExpressionChunkNode exprNode:
                    ProcessExpression(exprNode, context, options);
                    break;

                default:
                    _warnings.Add(new LoadWarning(
                        chunk.LineNumber ?? 0,
                        chunk.ColumnNumber ?? 0,
                        $"Unknown chunk type: {chunk.GetType().Name}"));
                    break;
            }
        }

        private void ProcessClassDefinition(ClassDefinitionNode classDef, LoadContext context, FileLoadOptions options)
        {
            try
            {
                var super = SystemDictionary.TryGetClass(classDef.Superclass, out var sup) ? sup : null;
                
                if (super == null && !string.IsNullOrEmpty(classDef.Superclass) && classDef.Superclass != "nil")
                {
                    _warnings.Add(new LoadWarning(
                        classDef.LineNumber ?? 0,
                        classDef.ColumnNumber ?? 0,
                        $"Superclass '{classDef.Superclass}' not found for class '{classDef.ClassName}'"));
                }

                var cls = SystemDictionary.DefineClass(
                    classDef.ClassName,
                    super,
                    classDef.InstanceVariables,
                    classDef.ClassVariables,
                    classDef.PoolDictionaries,
                    classDef.Category);

                context.CurrentClass = cls;
                context.CurrentClassName = classDef.ClassName;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to define class '{classDef.ClassName}': {ex.Message}", ex);
            }
        }

        private void ProcessMethodDefinition(MethodDefinitionNode methodDef, LoadContext context, FileLoadOptions options)
        {
            if (context.CurrentClass == null)
            {
                throw new InvalidOperationException("Method definition found without preceding class definition");
            }

            try
            {
                var tempParser = new EnhancedTemporaryVariableParser();
                var variables = context.CurrentHeader?.IsClassSide == true 
                    ? context.CurrentClass.ClassVariables 
                    : context.CurrentClass.InstanceVariables;

                CompiledMethod compiled;
                if (string.IsNullOrWhiteSpace(methodDef.Source))
                {
                    var emptyBody = new SequenceNode(new List<string>(), new List<StatementNode>());
                    compiled = AstMethodCompiler.CompileMethod(
                        emptyBody,
                        methodDef.Arguments,
                        variables,
                        methodDef.HasPrimitive,
                        methodDef.PrimitiveIndex);
                }
                else
                {
                    var parser = new SmalltalkParser(methodDef.Source);
                    compiled = parser.ParseMethod(methodDef.Arguments, variables, methodDef.Selector);
                }

                // Validate method compilation
                if (compiled.Bytecodes.Length == 0 && !methodDef.HasPrimitive)
                {
                    _warnings.Add(new LoadWarning(
                        methodDef.LineNumber ?? 0,
                        methodDef.ColumnNumber ?? 0,
                        $"Method '{methodDef.Selector}' compiled to empty bytecode sequence"));
                }

                compiled = new CompiledMethod
                {
                    Header = compiled.Header,
                    Bytecodes = compiled.Bytecodes,
                    Literals = compiled.Literals,
                    TemporariesCount = compiled.TemporariesCount,
                    HasPrimitive = methodDef.HasPrimitive,
                    PrimitiveIndex = methodDef.PrimitiveIndex,
                    Comment = compiled.Comment
                };

                context.CurrentClass.MethodDictionary[new Symbol(methodDef.Selector)] = compiled;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to compile method '{methodDef.Selector}': {ex.Message}", ex);
            }
        }

        private void ProcessMethodCategory(MethodCategoryNode categoryNode, LoadContext context, FileLoadOptions options)
        {
            if (categoryNode.Header == null)
            {
                throw new InvalidOperationException("Method category without header");
            }

            context.CurrentHeader = categoryNode.Header;
            var categoryClass = SystemDictionary.DefineClass(categoryNode.Header.ClassName);
            context.CurrentClass = categoryClass;

            foreach (var method in categoryNode.Methods)
            {
                try
                {
                    ProcessMethodDefinition(method, context, options);
                }
                catch (Exception ex)
                {
                    if (!options.ContinueOnError)
                        throw;
                    
                    _errors.Add(new ParseError(
                        method.LineNumber ?? 0,
                        method.ColumnNumber ?? 0,
                        $"Failed to process method in category: {ex.Message}"));
                }
            }
        }

        private void ProcessClassComment(ClassCommentNode commentNode, LoadContext context, FileLoadOptions options)
        {
            if (context.CurrentClass != null)
            {
                context.CurrentClass.Comment = commentNode.Comment;
            }
        }

        private void ProcessScript(ScriptNode scriptNode, LoadContext context, FileLoadOptions options)
        {
            if (options.ExecuteScripts)
            {
                // Execute script in appropriate context
                _warnings.Add(new LoadWarning(
                    scriptNode.LineNumber ?? 0,
                    scriptNode.ColumnNumber ?? 0,
                    "Script execution not yet implemented"));
            }
        }

        private void ProcessExpression(ExpressionChunkNode exprNode, LoadContext context, FileLoadOptions options)
        {
            if (options.ExecuteExpressions)
            {
                // Execute expression in workspace context
                _warnings.Add(new LoadWarning(
                    exprNode.LineNumber ?? 0,
                    exprNode.ColumnNumber ?? 0,
                    "Expression execution not yet implemented"));
            }
        }

        private void IncrementChunkCounter(string chunkType)
        {
            _processedChunks.TryGetValue(chunkType, out var count);
            _processedChunks[chunkType] = count + 1;
        }
    }

    /// <summary>
    /// Enhanced error handling with detailed diagnostics and recovery strategies.
    /// </summary>
    public class EnhancedErrorHandler : BaseErrorListener
    {
        private readonly List<ParseError> _errors = new();
        private readonly List<ParseWarning> _warnings = new();
        private readonly ErrorRecoveryStrategy _recoveryStrategy;

        public IReadOnlyList<ParseError> Errors => _errors;
        public IReadOnlyList<ParseWarning> Warnings => _warnings;

        public EnhancedErrorHandler(ErrorRecoveryStrategy recoveryStrategy = ErrorRecoveryStrategy.Continue)
        {
            _recoveryStrategy = recoveryStrategy;
        }

        public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var error = new ParseError(line, charPositionInLine, msg)
            {
                OffendingSymbol = offendingSymbol?.Text,
                ExpectedTokens = GetExpectedTokens(recognizer, e),
                ErrorCode = ClassifyError(msg),
                Severity = GetErrorSeverity(msg)
            };

            _errors.Add(error);

            // Apply recovery strategy
            if (_recoveryStrategy == ErrorRecoveryStrategy.Throw)
            {
                throw new ParseException($"Parse error at line {line}, position {charPositionInLine}: {msg}");
            }

            // Suggest recovery actions
            var suggestion = SuggestRecovery(error);
            if (!string.IsNullOrEmpty(suggestion))
            {
                error.RecoverySuggestion = suggestion;
            }
        }

        private List<string> GetExpectedTokens(IRecognizer recognizer, RecognitionException? e)
        {
            var expected = new List<string>();
            
            if (e?.OffendingToken != null && recognizer is Smalltalk80Parser parser)
            {
                var expectedTokens = e.GetExpectedTokens();
                if (expectedTokens != null)
                {
                    foreach (var tokenType in expectedTokens.ToArray())
                    {
                        var tokenName = parser.Vocabulary.GetDisplayName(tokenType);
                        if (!string.IsNullOrEmpty(tokenName))
                        {
                            expected.Add(tokenName);
                        }
                    }
                }
            }

            return expected;
        }

        private ErrorCode ClassifyError(string message)
        {
            return message.ToLowerInvariant() switch
            {
                var msg when msg.Contains("missing") => ErrorCode.MissingToken,
                var msg when msg.Contains("extraneous") => ErrorCode.ExtraneousToken,
                var msg when msg.Contains("no viable") => ErrorCode.NoViableAlternative,
                var msg when msg.Contains("mismatch") => ErrorCode.InputMismatch,
                _ => ErrorCode.Unknown
            };
        }

        private ErrorSeverity GetErrorSeverity(string message)
        {
            return message.ToLowerInvariant() switch
            {
                var msg when msg.Contains("warning") => ErrorSeverity.Warning,
                var msg when msg.Contains("missing ';'") => ErrorSeverity.Info,
                var msg when msg.Contains("missing") => ErrorSeverity.Warning,
                _ => ErrorSeverity.Error
            };
        }

        private string SuggestRecovery(ParseError error)
        {
            return error.ErrorCode switch
            {
                ErrorCode.MissingToken => $"Try adding the missing token: {string.Join(", ", error.ExpectedTokens)}",
                ErrorCode.ExtraneousToken => $"Try removing the unexpected token: {error.OffendingSymbol}",
                ErrorCode.NoViableAlternative => "Check syntax near this location",
                ErrorCode.InputMismatch => $"Expected one of: {string.Join(", ", error.ExpectedTokens)}",
                _ => "Check syntax and try again"
            };
        }
    }
}

// Supporting data structures and enums

public enum VariableType
{
    Argument,
    Temporary,
    InstanceVariable,
    ClassVariable,
    Special
}

public enum VariableResolutionType
{
    Argument,
    Temporary,
    InstanceVariable,
    ClassVariable,
    Special,
    Unknown
}

public enum ErrorCode
{
    Unknown,
    MissingToken,
    ExtraneousToken,
    NoViableAlternative,
    InputMismatch
}

public enum ErrorSeverity
{
    Info,
    Warning,
    Error,
    Fatal
}

public enum ErrorRecoveryStrategy
{
    Continue,
    Throw,
    Recover
}

public class VariableInfo
{
    public string Name { get; set; } = "";
    public VariableType Type { get; set; }
    public int Index { get; set; }
    public int ScopeLevel { get; set; }
    public bool IsReadOnly { get; set; }
}

public class VariableScope
{
    public int Level { get; set; }
    public Dictionary<string, VariableInfo> Variables { get; set; } = new();
}

public class TemporaryVariableInfo
{
    public List<string> Temporaries { get; set; } = new();
    public Dictionary<string, VariableInfo> ScopeInfo { get; set; } = new();
    public List<string> Arguments { get; set; } = new();
    public List<string> InstanceVariables { get; set; } = new();
}

public class VariableResolution
{
    public bool Found { get; set; }
    public VariableInfo? VariableInfo { get; set; }
    public VariableResolutionType ResolutionType { get; set; }
}

public class ParseError
{
    public int Line { get; set; }
    public int Column { get; set; }
    public string Message { get; set; }
    public string? OffendingSymbol { get; set; }
    public List<string> ExpectedTokens { get; set; } = new();
    public ErrorCode ErrorCode { get; set; }
    public ErrorSeverity Severity { get; set; }
    public string? RecoverySuggestion { get; set; }

    public ParseError(int line, int column, string message)
    {
        Line = line;
        Column = column;
        Message = message;
    }
}

public class ParseWarning
{
    public int Line { get; set; }
    public int Column { get; set; }
    public string Message { get; set; }

    public ParseWarning(int line, int column, string message)
    {
        Line = line;
        Column = column;
        Message = message;
    }
}

public class LoadWarning
{
    public int Line { get; set; }
    public int Column { get; set; }
    public string Message { get; set; }

    public LoadWarning(int line, int column, string message)
    {
        Line = line;
        Column = column;
        Message = message;
    }
}

public class FileLoadOptions
{
    public bool ContinueOnError { get; set; } = true;
    public bool ExecuteScripts { get; set; } = false;
    public bool ExecuteExpressions { get; set; } = false;
    public bool ValidateMethodSignatures { get; set; } = true;
    public bool CheckVariableConflicts { get; set; } = true;
}

public class FileLoadResult
{
    public bool Success { get; set; }
    public string SourcePath { get; set; } = "";
    public List<ParseError> Errors { get; set; } = new();
    public List<LoadWarning> Warnings { get; set; } = new();
    public Dictionary<string, int> ProcessedChunks { get; set; } = new();
}

public class LoadContext
{
    public SmalltalkClass? CurrentClass { get; set; }
    public string? CurrentClassName { get; set; }
    public MethodHeaderNode? CurrentHeader { get; set; }
}

// Enhanced AST nodes for optimized processing

public class OptimizedCascadeExpression : CascadeExpression
{
    public bool UseReceiverCache { get; set; }
    public bool InlineShortCascades { get; set; }

    public OptimizedCascadeExpression(ExpressionNode receiver, List<MessageNode> messages)
        : base(receiver, messages)
    {
    }
}

public class OptimizedUnarySendExpression : UnarySendExpression
{
    public bool CanInline { get; set; }
    public bool UseDirectDispatch { get; set; }

    public OptimizedUnarySendExpression(ExpressionNode receiver, string selector)
        : base(receiver, selector)
    {
    }
}

public class OptimizedBinarySendExpression : BinarySendExpression
{
    public bool CanInline { get; set; }
    public bool UseDirectDispatch { get; set; }

    public OptimizedBinarySendExpression(ExpressionNode receiver, string selector, ExpressionNode argument)
        : base(receiver, selector, argument)
    {
    }
}

public class OptimizedKeywordSendExpression : KeywordSendExpression
{
    public bool CanInline { get; set; }
    public bool UseDirectDispatch { get; set; }
    public int ArgumentCount { get; set; }

    public OptimizedKeywordSendExpression(ExpressionNode receiver, List<string> keywords, List<ExpressionNode> arguments)
        : base(receiver, keywords, arguments)
    {
    }
}

// Enhanced file reader with better error handling

public static class EnhancedFileInReader
{
    public static FileNode Parse(string path, FileLoadOptions options)
    {
        try
        {
            var source = File.ReadAllText(path);
            var input = new AntlrInputStream(source);
            var lexer = new Smalltalk80Lexer(input);
            var tokens = new CommonTokenStream(lexer);
            var parser = new Smalltalk80Parser(tokens);
            
            parser.Interpreter.PredictionMode = Antlr4.Runtime.Atn.PredictionMode.LL_EXACT_AMBIG_DETECTION;
            parser.RemoveErrorListeners();
            
            var errorHandler = new EnhancedParserIntegration.EnhancedErrorHandler(
                options.ContinueOnError ? ErrorRecoveryStrategy.Continue : ErrorRecoveryStrategy.Throw);
            parser.AddErrorListener(errorHandler);

            var fileCtx = parser.fileIn();
            var visitor = new ClassDefBuildingVisitor();

            var result = (FileNode)visitor.VisitFileIn(fileCtx)!;

            // Add line/column information to chunks
            AddLocationInfo(result, source);

            return result;
        }
        catch (Exception ex)
        {
            throw new ParseException($"Failed to parse file '{path}': {ex.Message}", ex);
        }
    }

    private static void AddLocationInfo(FileNode fileNode, string source)
    {
        var lines = source.Split('\n');
        int currentLine = 1;

        foreach (var chunk in fileNode.Chunks)
        {
            chunk.LineNumber = currentLine;
            chunk.ColumnNumber = 1;
            
            // Estimate line advancement based on chunk type
            currentLine += EstimateChunkLines(chunk);
        }
    }

    private static int EstimateChunkLines(ChunkNode chunk)
    {
        return chunk switch
        {
            ClassDefinitionNode => 1,
            MethodDefinitionNode method => method.Source?.Split('\n').Length ?? 1,
            MethodCategoryNode category => category.Methods.Count * 3,
            _ => 1
        };
    }
}

// Base classes for AST nodes with location info

public abstract class ChunkNode
{
    public int? LineNumber { get; set; }
    public int? ColumnNumber { get; set; }
}
