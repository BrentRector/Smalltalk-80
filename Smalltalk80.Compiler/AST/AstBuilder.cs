using System.Collections.Immutable;
using System.Globalization;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Smalltalk80.Compiler.Generated;

namespace Smalltalk80.Compiler.AST;

/// <summary>
/// Builds Abstract Syntax Trees from ANTLR parse trees.
/// </summary>
public sealed class AstBuilder : Smalltalk80BaseVisitor<AstNode?>
{
    private readonly string _sourceCode;

    /// <summary>
    /// Initializes a new instance of the AstBuilder class.
    /// </summary>
    /// <param name="sourceCode">The original source code.</param>
    public AstBuilder(string sourceCode)
    {
        _sourceCode = sourceCode ?? string.Empty;
    }

    /// <summary>
    /// Builds an AST from a parse tree.
    /// </summary>
    /// <param name="parseTree">The ANTLR parse tree.</param>
    /// <returns>The root AST node.</returns>
    public AstNode? Build(IParseTree parseTree)
    {
        return Visit(parseTree);
    }

    private SourceLocation GetLocation(ParserRuleContext context)
    {
        var start = context.Start;
        var stop = context.Stop ?? context.Start;
        
        return new SourceLocation(
            start.Line,
            start.Column,
            start.StartIndex,
            stop.StopIndex);
    }
    
    /// <summary>
    /// Extracts the source text for a given parse context.
    /// </summary>
    /// <param name="context">The parser rule context.</param>
    /// <returns>The source text, or null if unavailable.</returns>
    private string? GetSourceText(ParserRuleContext context)
    {
        if (string.IsNullOrEmpty(_sourceCode))
            return context.GetText();
        
        var location = GetLocation(context);
        if (location.StartIndex >= 0 && location.EndIndex < _sourceCode.Length)
        {
            return _sourceCode.Substring(location.StartIndex, location.EndIndex - location.StartIndex + 1);
        }
        
        return context.GetText();
    }

    public override AstNode? VisitMethodEntry(Smalltalk80Parser.MethodEntryContext context)
    {
        var header = context.methodHeader();
        var selector = BuildMethodSelector(header);
        var arguments = BuildMethodArguments(header);
        var temporaries = context.temporaries() != null ? BuildTemporaries(context.temporaries()) : ImmutableArray<TemporaryDeclarationNode>.Empty;
        var primitive = context.primitiveDecl() != null ? BuildPrimitive(context.primitiveDecl()) : null;
        var body = BuildMethodBody(context.methodBodyNoBang());

        return new MethodDefinitionNode(
            selector,
            arguments,
            temporaries,
            primitive,
            body,
            context.GetText(),
            GetLocation(context));
    }

    public override AstNode? VisitStatementsEntry(Smalltalk80Parser.StatementsEntryContext context)
    {
        var statements = BuildStatements(context.statements());
        return new ProgramNode(ImmutableArray.Create<AstNode>(statements), GetLocation(context));
    }

    public override AstNode? VisitExpressionEntry(Smalltalk80Parser.ExpressionEntryContext context)
    {
        var expr = BuildExpression(context.expression());
        if (expr != null)
        {
            var stmt = new ExpressionStatementNode(expr, GetLocation(context));
            var stmtList = new StatementListNode(ImmutableArray.Create<StatementNode>(stmt), GetLocation(context));
            return new ProgramNode(ImmutableArray.Create<AstNode>(stmtList), GetLocation(context));
        }
        return null;
    }

    private string BuildMethodSelector(Smalltalk80Parser.MethodHeaderContext header)
    {
        if (header.unarySelector() != null)
            return header.unarySelector().IDENTIFIER().GetText();
        
        if (header.binarySelector() != null)
            return header.binarySelector().GetText();
        
        var identifierColons = header.identifierColon();
        if (identifierColons != null && identifierColons.Length > 0)
        {
            return string.Concat(identifierColons.Select(ic => ic.GetText()));
        }

        return "unknown";
    }

    private ImmutableArray<MethodArgumentNode> BuildMethodArguments(Smalltalk80Parser.MethodHeaderContext header)
    {
        var args = ImmutableArray.CreateBuilder<MethodArgumentNode>();

        if (header.binarySelector() != null)
        {
            var arg = header.formalArg(0);
            if (arg != null)
                args.Add(new MethodArgumentNode(arg.argumentName().GetText(), GetLocation(arg)));
        }
        else
        {
            var formalArgs = header.formalArg();
            if (formalArgs != null)
            {
                foreach (var arg in formalArgs)
                {
                    args.Add(new MethodArgumentNode(arg.argumentName().GetText(), GetLocation(arg)));
                }
            }
        }

        return args.ToImmutable();
    }

    private ImmutableArray<TemporaryDeclarationNode> BuildTemporaries(Smalltalk80Parser.TemporariesContext context)
    {
        var temps = ImmutableArray.CreateBuilder<TemporaryDeclarationNode>();
        var tempNames = context.temporaryName();
        
        foreach (var temp in tempNames)
        {
            temps.Add(new TemporaryDeclarationNode(temp.GetText(), GetLocation(temp)));
        }

        return temps.ToImmutable();
    }

    private PrimitiveDeclarationNode? BuildPrimitive(Smalltalk80Parser.PrimitiveDeclContext context)
    {
        var text = context.GetText();
        if (text.StartsWith("<primitive:") && text.EndsWith(">"))
        {
            var numStr = text.Substring("<primitive:".Length, text.Length - "<primitive:".Length - 1).Trim();
            if (int.TryParse(numStr, out var num))
            {
                return new PrimitiveDeclarationNode(num, GetLocation(context));
            }
        }
        return null;
    }

    private StatementListNode BuildMethodBody(Smalltalk80Parser.MethodBodyNoBangContext context)
    {
        if (context.statements() != null)
            return BuildStatements(context.statements());
        
        if (context.returnStatement() != null)
        {
            var returnExpr = BuildReturnStatement(context.returnStatement());
            if (returnExpr != null)
                return new StatementListNode(ImmutableArray.Create<StatementNode>(returnExpr), GetLocation(context));
        }

        return new StatementListNode(ImmutableArray<StatementNode>.Empty, GetLocation(context));
    }

    private StatementListNode BuildMethodBodyWithBang(Smalltalk80Parser.MethodBodyContext context)
    {
        if (context.statements() != null)
            return BuildStatements(context.statements());
        
        if (context.returnStatement() != null)
        {
            var returnExpr = BuildReturnStatement(context.returnStatement());
            if (returnExpr != null)
                return new StatementListNode(ImmutableArray.Create<StatementNode>(returnExpr), GetLocation(context));
        }

        // Empty method body (just BANG)
        return new StatementListNode(ImmutableArray<StatementNode>.Empty, GetLocation(context));
    }

    private StatementListNode BuildStatements(Smalltalk80Parser.StatementsContext context)
    {
        var stmts = ImmutableArray.CreateBuilder<StatementNode>();
        var statements = context.statement();

        foreach (var stmt in statements)
        {
            var stmtNode = BuildStatement(stmt);
            if (stmtNode != null)
                stmts.Add(stmtNode);
        }

        return new StatementListNode(stmts.ToImmutable(), GetLocation(context));
    }

    private StatementNode? BuildStatement(Smalltalk80Parser.StatementContext context)
    {
        if (context.returnStatement() != null)
            return BuildReturnStatement(context.returnStatement());
        
        if (context.expressionStatement() != null)
        {
            var expr = BuildExpression(context.expressionStatement().expression());
            if (expr != null)
                return new ExpressionStatementNode(expr, GetLocation(context));
        }

        return null;
    }

    private ReturnStatementNode? BuildReturnStatement(Smalltalk80Parser.ReturnStatementContext context)
    {
        var expr = BuildExpression(context.expression());
        if (expr != null)
            return new ReturnStatementNode(expr, GetLocation(context));
        return null;
    }

    private ExpressionNode? BuildExpression(Smalltalk80Parser.ExpressionContext context)
    {
        if (context.assignmentExpr() != null)
            return BuildAssignment(context.assignmentExpr());
        return null;
    }

    private ExpressionNode? BuildAssignment(Smalltalk80Parser.AssignmentExprContext context)
    {
        var variable = context.variable();
        var assignExpr = context.assignmentExpr();

        if (variable != null && context.ASSIGN() != null && assignExpr != null)
        {
            var value = BuildAssignment(assignExpr);
            if (value != null)
            {
                return new AssignmentNode(variable.GetText(), value, GetLocation(context));
            }
        }

        if (context.cascadeExpr() != null)
            return BuildCascade(context.cascadeExpr());

        return null;
    }

    private ExpressionNode? BuildCascade(Smalltalk80Parser.CascadeExprContext context)
    {
        var messageExpr = context.messageExpr();
        if (messageExpr == null) return null;

        var firstMessage = BuildMessageExpr(messageExpr);
        if (firstMessage == null) return null;

        var cascadeParts = context.messagePart();
        if (cascadeParts != null && cascadeParts.Length > 0)
        {
            // Extract receiver from first message
            if (firstMessage is MessageSendNode firstSend)
            {
                var messages = ImmutableArray.CreateBuilder<MessageNode>();
                messages.Add(firstSend.Message);

                foreach (var part in cascadeParts)
                {
                    var msg = BuildMessagePart(part);
                    if (msg != null)
                        messages.Add(msg);
                }

                return new CascadeNode(firstSend.Receiver, messages.ToImmutable(), GetLocation(context));
            }
        }

        return firstMessage;
    }

    private ExpressionNode? BuildMessageExpr(Smalltalk80Parser.MessageExprContext context)
    {
        var receiver = BuildMessageBase(context.messageBase());
        if (receiver == null) return null;

        // Process unary and binary messages
        var unaryMessages = context.unaryMessage();
        foreach (var unaryMsg in unaryMessages)
        {
            var selector = unaryMsg.unarySelector().GetText();
            var msg = new UnaryMessageNode(selector, GetLocation(unaryMsg));
            receiver = new MessageSendNode(receiver, msg, GetLocation(unaryMsg));
        }

        var binaryMessages = context.binaryMessage();
        foreach (var binMsg in binaryMessages)
        {
            var msg = BuildBinaryMessage(binMsg);
            if (msg != null)
                receiver = new MessageSendNode(receiver, msg, GetLocation(binMsg));
        }

        // Process keyword message if present
        var keywordMessage = context.keywordMessage();
        if (keywordMessage != null)
        {
            var msg = BuildKeywordMessage(keywordMessage);
            if (msg != null)
                return new MessageSendNode(receiver, msg, GetLocation(context));
        }

        return receiver;
    }

    private ExpressionNode? BuildMessageBase(Smalltalk80Parser.MessageBaseContext context)
    {
        if (context.literal() != null)
            return BuildLiteral(context.literal());

        if (context.variable() != null)
            return BuildVariable(context.variable());

        if (context.block() != null)
            return BuildBlock(context.block());

        if (context.parenthesizedExpr() != null)
        {
            var parenExpr = context.parenthesizedExpr();
            return BuildExpression(parenExpr.expression());
        }

        if (context.negativeNumber() != null)
        {
            var negNum = context.negativeNumber();
            var baseExpr = BuildMessageBase(negNum.messageBase());
            if (baseExpr is NumberLiteralNode numLit)
            {
                // Negate the number
                if (numLit.IsFloat)
                {
                    var negValue = -(double)numLit.Value;
                    return new NumberLiteralNode(negValue, true, GetLocation(context));
                }
                else
                {
                    var negValue = -(int)numLit.Value;
                    return new NumberLiteralNode(negValue, false, GetLocation(context));
                }
            }
        }

        if (context.dynamicArray() != null)
        {
            // Handle dynamic arrays as a special case
            var dynArray = context.dynamicArray();
            var expressions = dynArray.expression();
            // For now, we'll treat this as a special message send
            // In the future, we might want a dedicated DynamicArrayNode
            return null;
        }

        return null;
    }

    private ExpressionNode? BuildLiteral(Smalltalk80Parser.LiteralContext context)
    {
        var loc = GetLocation(context);

        if (context.numberLiteral() != null)
        {
            var text = context.numberLiteral().GetText();
            if (text.Contains('.') || text.Contains('e') || text.Contains('E'))
            {
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatVal))
                    return new NumberLiteralNode(floatVal, true, loc);
            }
            else
            {
                if (int.TryParse(text, out var intVal))
                    return new NumberLiteralNode(intVal, false, loc);
            }
        }

        if (context.stringConst() != null)
        {
            var text = context.stringConst().GetText();
            // Remove quotes and handle escaped quotes
            var value = text.Substring(1, text.Length - 2).Replace("''", "'");
            return new StringLiteralNode(value, loc);
        }

        if (context.symbolConst() != null)
        {
            var symbolConst = context.symbolConst();
            string value;
            
            if (symbolConst.QUOTED_SYMBOL() != null)
            {
                var text = symbolConst.QUOTED_SYMBOL().GetText();
                // Remove #' and trailing '
                value = text.Substring(2, text.Length - 3).Replace("''", "'");
            }
            else if (symbolConst.symbolName() != null)
            {
                value = symbolConst.symbolName().GetText();
            }
            else
            {
                value = symbolConst.GetText().TrimStart('#');
            }
            
            return new SymbolLiteralNode(value, loc);
        }

        if (context.characterLiteral() != null)
        {
            var text = context.characterLiteral().GetText();
            // Remove $ prefix
            if (text.Length > 1)
                return new CharacterLiteralNode(text[1], loc);
        }

        if (context.arrayLiteral() != null)
            return BuildArrayLiteral(context.arrayLiteral());

        return null;
    }

    private ArrayLiteralNode BuildArrayLiteral(Smalltalk80Parser.ArrayLiteralContext context)
    {
        var elements = ImmutableArray.CreateBuilder<LiteralNode>();
        var arrayElements = context.arrayElement();

        foreach (var elem in arrayElements)
        {
            var literal = BuildArrayElement(elem);
            if (literal != null)
                elements.Add(literal);
        }

        return new ArrayLiteralNode(elements.ToImmutable(), GetLocation(context));
    }

    private LiteralNode? BuildArrayElement(Smalltalk80Parser.ArrayElementContext element)
    {
        var loc = GetLocation(element);

        if (element.literal() != null)
            return BuildLiteral(element.literal()) as LiteralNode;

        if (element.symbolName() != null)
            return new SymbolLiteralNode(element.symbolName().GetText(), loc);

        // Handle nested arrays
        var nestedElements = element.arrayElement();
        if (nestedElements != null && nestedElements.Length > 0)
        {
            var nested = ImmutableArray.CreateBuilder<LiteralNode>();
            foreach (var nestedElem in nestedElements)
            {
                var nestedLiteral = BuildArrayElement(nestedElem);
                if (nestedLiteral != null)
                    nested.Add(nestedLiteral);
            }
            return new ArrayLiteralNode(nested.ToImmutable(), loc);
        }

        return null;
    }

    private ExpressionNode BuildVariable(Smalltalk80Parser.VariableContext context)
    {
        var name = context.GetText();
        var loc = GetLocation(context);

        return name switch
        {
            "self" => new SelfLiteralNode(loc),
            "super" => new SuperLiteralNode(loc),
            "true" => new BooleanLiteralNode(true, loc),
            "false" => new BooleanLiteralNode(false, loc),
            "nil" => new NilLiteralNode(loc),
            _ => new VariableNode(name, loc)
        };
    }

    private BlockNode BuildBlock(Smalltalk80Parser.BlockContext context)
    {
        var arguments = context.blockArgs() != null 
            ? BuildBlockArguments(context.blockArgs()) 
            : ImmutableArray<BlockArgumentNode>.Empty;

        var temporaries = context.blockTemps() != null
            ? BuildBlockTemporaries(context.blockTemps())
            : ImmutableArray<TemporaryDeclarationNode>.Empty;

        var body = context.statements() != null
            ? BuildStatements(context.statements())
            : new StatementListNode(ImmutableArray<StatementNode>.Empty, GetLocation(context));

        return new BlockNode(arguments, temporaries, body, GetLocation(context));
    }

    private ImmutableArray<BlockArgumentNode> BuildBlockArguments(Smalltalk80Parser.BlockArgsContext context)
    {
        var args = ImmutableArray.CreateBuilder<BlockArgumentNode>();
        var argNames = context.argumentName();

        foreach (var arg in argNames)
        {
            args.Add(new BlockArgumentNode(arg.GetText(), GetLocation(arg)));
        }

        return args.ToImmutable();
    }

    private ImmutableArray<TemporaryDeclarationNode> BuildBlockTemporaries(Smalltalk80Parser.BlockTempsContext context)
    {
        var temps = ImmutableArray.CreateBuilder<TemporaryDeclarationNode>();
        var tempNames = context.temporaryName();
        
        foreach (var temp in tempNames)
        {
            temps.Add(new TemporaryDeclarationNode(temp.GetText(), GetLocation(temp)));
        }

        return temps.ToImmutable();
    }

    private MessageNode? BuildMessagePart(Smalltalk80Parser.MessagePartContext context)
    {
        if (context.unaryMessage() != null)
        {
            var selector = context.unaryMessage().unarySelector().GetText();
            return new UnaryMessageNode(selector, GetLocation(context));
        }

        if (context.binaryMessage() != null)
            return BuildBinaryMessage(context.binaryMessage());

        if (context.keywordMessage() != null)
            return BuildKeywordMessage(context.keywordMessage());

        return null;
    }

    private BinaryMessageNode? BuildBinaryMessage(Smalltalk80Parser.BinaryMessageContext context)
    {
        var selector = context.binarySelector().GetText();
        var argument = BuildMessageBase(context.messageBase());
        
        if (argument != null)
            return new BinaryMessageNode(selector, argument, GetLocation(context));

        return null;
    }

    private KeywordMessageNode? BuildKeywordMessage(Smalltalk80Parser.KeywordMessageContext context)
    {
        var keywords = ImmutableArray.CreateBuilder<string>();
        var arguments = ImmutableArray.CreateBuilder<ExpressionNode>();

        var keywordParts = context.keywordPart();
        foreach (var part in keywordParts)
        {
            keywords.Add(part.IDENTIFIER().GetText() + ":");
            var arg = BuildNonKeywordExpr(part.nonKeywordExpr());
            if (arg != null)
                arguments.Add(arg);
        }

        return new KeywordMessageNode(keywords.ToImmutable(), arguments.ToImmutable(), GetLocation(context));
    }

    private ExpressionNode? BuildNonKeywordExpr(Smalltalk80Parser.NonKeywordExprContext context)
    {
        var receiver = BuildMessageBase(context.messageBase());
        if (receiver == null) return null;

        // Process unary messages
        var unaryMessages = context.unaryMessage();
        foreach (var unaryMsg in unaryMessages)
        {
            var selector = unaryMsg.unarySelector().GetText();
            var msg = new UnaryMessageNode(selector, GetLocation(unaryMsg));
            receiver = new MessageSendNode(receiver, msg, GetLocation(unaryMsg));
        }

        // Process binary messages
        var binaryMessages = context.binaryMessage();
        foreach (var binMsg in binaryMessages)
        {
            var msg = BuildBinaryMessage(binMsg);
            if (msg != null)
                receiver = new MessageSendNode(receiver, msg, GetLocation(binMsg));
        }

        return receiver;
    }

    /// <summary>
    /// Visits the file-in entry point containing multiple chunks.
    /// </summary>
    public override AstNode? VisitFileIn(Smalltalk80Parser.FileInContext context)
    {
        var elements = ImmutableArray.CreateBuilder<AstNode>();
        var chunks = context.chunk();
        
        foreach (var chunk in chunks)
        {
            var node = VisitChunk(chunk);
            if (node != null)
            {
                elements.Add(node);
            }
        }
        
        return new ProgramNode(elements.ToImmutable(), GetLocation(context));
    }

    /// <summary>
    /// Visits a chunk in file-in format.
    /// </summary>
    public override AstNode? VisitChunk(Smalltalk80Parser.ChunkContext context)
    {
        // Determine the type of chunk and delegate to specific handlers
        if (context.classDefChunk() != null)
            return VisitClassDefChunk(context.classDefChunk());
        
        if (context.methodCategoryChunk() != null)
            return VisitMethodCategoryChunk(context.methodCategoryChunk());
        
        if (context.simpleMethodChunk() != null)
            return VisitSimpleMethodChunk(context.simpleMethodChunk());
        
        if (context.expressionChunk() != null)
            return VisitExpressionChunk(context.expressionChunk());
        
        if (context.classCommentChunk() != null)
            return VisitClassCommentChunk(context.classCommentChunk());
        
        if (context.metaclassDefinitionChunk() != null)
            return VisitMetaclassDefinitionChunk(context.metaclassDefinitionChunk());
        
        if (context.metaclassCommentChunk() != null)
            return VisitMetaclassCommentChunk(context.metaclassCommentChunk());
        
        if (context.separatorChunk() != null)
            return VisitSeparatorChunk(context.separatorChunk());
        
        // Handle standalone BANG token (empty chunk)
        if (context.BANG() != null)
            return null; // Skip empty chunks
        
        return null;
    }

    /// <summary>
    /// Visits a class definition chunk.
    /// </summary>
    /// <param name="context">The parse context containing the class definition.</param>
    /// <returns>A ClassDefinitionNode representing the parsed class definition.</returns>
    public override AstNode? VisitClassDefChunk(Smalltalk80Parser.ClassDefChunkContext context)
    {
        var header = context.classHeader();
        var body = context.classBody();
        
        if (header == null || body == null)
            return null;
        
        // Extract superclass name
        var superclassName = header.className()?.GetText() ?? "Object";
        
        // Extract subclass kind (e.g., "subclass:", "variableSubclass:")
        var subclassKind = header.subclassKind()?.GetText() ?? "subclass:";
        
        // Extract class name from the symbol constant
        var classNameSymbol = header.symbolConst();
        var className = ExtractSymbolValue(classNameSymbol);
        
        // Parse class body slots
        var instanceVariables = ImmutableArray<string>.Empty;
        var classVariables = ImmutableArray<string>.Empty;
        var poolDictionaries = ImmutableArray<string>.Empty;
        var category = string.Empty;
        
        // Process each slot specification
        var slots = body.classSlotSpec();
        foreach (var slot in slots)
        {
            var specName = slot.identifierColon()?.GetText();
            var specValue = ExtractStringValue(slot.stringConst());
            
            switch (specName)
            {
                case "instanceVariableNames:":
                    instanceVariables = ParseVariableNames(specValue);
                    break;
                case "classVariableNames:":
                    classVariables = ParseVariableNames(specValue);
                    break;
                case "poolDictionaries:":
                    poolDictionaries = ParseVariableNames(specValue);
                    break;
                case "classInstanceVariableNames:":
                    // Class instance variables are handled separately in metaclass definitions
                    break;
            }
        }
        
        // Extract category
        var categoryDecl = body.categoryDecl();
        if (categoryDecl != null)
        {
            category = ExtractStringValue(categoryDecl.stringConst());
        }
        
        // For now, we don't have methods in a class definition chunk
        // Methods are added separately via method chunks
        var methods = ImmutableArray<MethodDefinitionNode>.Empty;
        
        return new ClassDefinitionNode(
            className,
            superclassName,
            subclassKind,
            instanceVariables,
            classVariables,
            poolDictionaries,
            category,
            methods,
            GetLocation(context),
            GetSourceText(context));
    }
    
    /// <summary>
    /// Extracts the symbol value from a symbol constant context.
    /// </summary>
    /// <param name="context">The symbol constant context.</param>
    /// <returns>The extracted symbol value.</returns>
    private string ExtractSymbolValue(Smalltalk80Parser.SymbolConstContext? context)
    {
        if (context == null)
            return string.Empty;
        
        if (context.QUOTED_SYMBOL() != null)
        {
            var text = context.QUOTED_SYMBOL().GetText();
            // Remove #' and trailing '
            return text.Substring(2, text.Length - 3).Replace("''", "'");
        }
        else if (context.symbolName() != null)
        {
            return context.symbolName().GetText();
        }
        else
        {
            var text = context.GetText();
            // Remove # prefix if present
            return text.StartsWith("#") ? text.Substring(1) : text;
        }
    }
    
    /// <summary>
    /// Extracts the string value from a string constant context.
    /// </summary>
    /// <param name="context">The string constant context.</param>
    /// <returns>The extracted string value.</returns>
    private string ExtractStringValue(Smalltalk80Parser.StringConstContext? context)
    {
        if (context == null)
            return string.Empty;
        
        var text = context.GetText();
        // Remove quotes and handle escaped quotes
        return text.Substring(1, text.Length - 2).Replace("''", "'");
    }
    
    /// <summary>
    /// Parses a space-separated list of variable names into an array.
    /// </summary>
    /// <param name="variableList">The space-separated variable list.</param>
    /// <returns>An immutable array of variable names.</returns>
    private ImmutableArray<string> ParseVariableNames(string variableList)
    {
        if (string.IsNullOrWhiteSpace(variableList))
            return ImmutableArray<string>.Empty;
        
        var names = variableList.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return names.ToImmutableArray();
    }

    /// <summary>
    /// Visits a method category chunk containing multiple methods.
    /// </summary>
    public override AstNode? VisitMethodCategoryChunk(Smalltalk80Parser.MethodCategoryChunkContext context)
    {
        var methods = ImmutableArray.CreateBuilder<AstNode>();
        var methodDefs = context.methodDef();
        
        foreach (var methodDef in methodDefs)
        {
            var method = VisitMethodDef(methodDef);
            if (method != null)
            {
                methods.Add(method);
            }
        }
        
        // Return a program node containing all methods in this category
        return new ProgramNode(methods.ToImmutable(), GetLocation(context));
    }

    /// <summary>
    /// Visits a simple method chunk (single method definition).
    /// </summary>
    public override AstNode? VisitSimpleMethodChunk(Smalltalk80Parser.SimpleMethodChunkContext context)
    {
        var methodDef = context.methodDef();
        if (methodDef != null)
        {
            return VisitMethodDef(methodDef);
        }
        return null;
    }

    /// <summary>
    /// Visits a method definition.
    /// </summary>
    public override AstNode? VisitMethodDef(Smalltalk80Parser.MethodDefContext context)
    {
        var header = context.methodHeader();
        var selector = BuildMethodSelector(header);
        var arguments = BuildMethodArguments(header);
        var temporaries = context.temporaries() != null ? BuildTemporaries(context.temporaries()) : ImmutableArray<TemporaryDeclarationNode>.Empty;
        var primitive = context.primitiveDecl() != null ? BuildPrimitive(context.primitiveDecl()) : null;
        // methodDef uses methodBody (with BANG), not methodBodyNoBang
        var body = BuildMethodBodyWithBang(context.methodBody());

        return new MethodDefinitionNode(
            selector,
            arguments,
            temporaries,
            primitive,
            body,
            context.GetText(),
            GetLocation(context));
    }

    /// <summary>
    /// Visits an expression chunk.
    /// </summary>
    public override AstNode? VisitExpressionChunk(Smalltalk80Parser.ExpressionChunkContext context)
    {
        var expr = BuildExpression(context.expression());
        if (expr != null)
        {
            var stmt = new ExpressionStatementNode(expr, GetLocation(context));
            var stmtList = new StatementListNode(ImmutableArray.Create<StatementNode>(stmt), GetLocation(context));
            return new ProgramNode(ImmutableArray.Create<AstNode>(stmtList), GetLocation(context));
        }
        return null;
    }

    /// <summary>
    /// Visits a class comment chunk.
    /// </summary>
    public override AstNode? VisitClassCommentChunk(Smalltalk80Parser.ClassCommentChunkContext context)
    {
        // Comments are not part of the executable AST, so we skip them
        return null;
    }

    /// <summary>
    /// Visits a metaclass definition chunk.
    /// </summary>
    public override AstNode? VisitMetaclassDefinitionChunk(Smalltalk80Parser.MetaclassDefinitionChunkContext context)
    {
        // Metaclass definitions would require extending the AST model
        return null;
    }

    /// <summary>
    /// Visits a metaclass comment chunk.
    /// </summary>
    public override AstNode? VisitMetaclassCommentChunk(Smalltalk80Parser.MetaclassCommentChunkContext context)
    {
        // Comments are not part of the executable AST, so we skip them
        return null;
    }

    /// <summary>
    /// Visits a separator chunk.
    /// </summary>
    public override AstNode? VisitSeparatorChunk(Smalltalk80Parser.SeparatorChunkContext context)
    {
        // Separator chunks are just visual separators, not executable code
        return null;
    }
}