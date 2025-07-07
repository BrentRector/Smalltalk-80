using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime.Tree;
using Smalltalk80.Generated;

namespace Smalltalk80.Parser;

/// <summary>
/// Visitor that constructs an abstract syntax tree (AST) from the parse tree
/// produced by <see cref="Smalltalk80Parser"/>.
/// </summary>
public sealed class AstBuildingVisitor : Smalltalk80BaseVisitor<AstNode>
{
    /// <summary>
    /// Tracks the current scope depth while visiting sequences.
    /// </summary>
    private int scopeDepth;

    /// <summary>
    /// Maintains the stack of variable scopes for debugging output.
    /// </summary>
    private readonly Stack<HashSet<string>> scopeStack = new();

    /// <summary>
    /// Gets the name of the method currently being visited, if any.
    /// </summary>
    private readonly string? currentMethodName;
    private readonly IReadOnlyList<string> argumentNames;
    private readonly IReadOnlyList<string> initialVariables;
    private readonly HashSet<string> instanceVariableSet;

    /// <summary>
    /// Initializes a new instance of the <see cref="AstBuildingVisitor"/> class.
    /// </summary>
    /// <param name="methodName">Optional selector of the method being parsed.</param>
    /// <param name="arguments">Method argument names available in the outer scope.</param>
    /// <param name="variables">Instance or class variables available in the outer scope.</param>
    public AstBuildingVisitor(string? methodName = null, IReadOnlyList<string>? arguments = null, IReadOnlyList<string>? variables = null)
    {
        currentMethodName = methodName;
        argumentNames = arguments ?? Array.Empty<string>();
        initialVariables = variables ?? Array.Empty<string>();
        instanceVariableSet = variables is not null
            ? new HashSet<string>(variables)
            : new HashSet<string>();
    }
    /// <inheritdoc/>
    public override AstNode VisitScriptChunk(Smalltalk80Parser.ScriptChunkContext context)
        => new ScriptNode((SequenceNode)Visit(context.sequence())!);

    /// <inheritdoc/>
    public override AstNode VisitSequence(Smalltalk80Parser.SequenceContext context)
        => ProcessSequence(context);

    /// <summary>
    /// Processes a sequence context while optionally injecting additional temporaries.
    /// </summary>
    /// <param name="context">The parse context to process.</param>
    /// <param name="extraTemps">Any temporary variable names declared outside the context.</param>
    /// <returns>The constructed <see cref="SequenceNode"/>.</returns>
    private SequenceNode ProcessSequence(Smalltalk80Parser.SequenceContext context, IReadOnlyList<string>? extraTemps = null)
        => ProcessSequence(context.temps(), context.statements(), extraTemps);

    /// <summary>
    /// Processes a sequence represented by discrete temporary and statement contexts.
    /// </summary>
    /// <param name="tempsCtx">The optional temporary variable declarations.</param>
    /// <param name="statementsCtx">The statements comprising the sequence.</param>
    /// <param name="extraTemps">Any additional temporary variables in scope.</param>
    /// <returns>The constructed <see cref="SequenceNode"/>.</returns>
    private SequenceNode ProcessSequence(
        Smalltalk80Parser.TempsContext? tempsCtx,
        Smalltalk80Parser.StatementsContext? statementsCtx,
        IReadOnlyList<string>? extraTemps = null)
    {
        var scopeName = scopeDepth == 0
            ? (currentMethodName ?? "(unknown)")
            : $"block {scopeDepth}";
        Tracer.Log($"Push scope {scopeName}: {scopeDepth + 1}");
        scopeDepth++;

        var temps = new List<string>();
        if (extraTemps is not null)
            temps.AddRange(extraTemps);
        if (tempsCtx is { })
        {
            // All temporary variable tokens are parsed as UNARY_SELECTOR.
            temps.AddRange(tempsCtx.UNARY_SELECTOR().Select(n => n.GetText().Trim()));
        }

        var current = scopeStack.Count > 0
            ? new HashSet<string>(scopeStack.Peek())
            : new HashSet<string>(scopeDepth == 1 ? argumentNames.Concat(initialVariables) : Array.Empty<string>());
        foreach (var t in temps)
            current.Add(t);
        scopeStack.Push(current);

        if (temps.Count > 0)
            Tracer.Log($"Temps: {string.Join(", ", temps)}");

        if (instanceVariableSet.Count > 0)
            Tracer.Log($"Instance variables in scope: {string.Join(", ", instanceVariableSet)}");

        var locals = current.Where(v => !instanceVariableSet.Contains(v));
        Tracer.Log($"Local variables in scope: {string.Join(", ", locals)}");

        var statements = new List<StatementNode>();
        if (statementsCtx is { })
        {
            foreach (var stmtCtx in statementsCtx.statement())
                statements.Add((StatementNode)Visit(stmtCtx)!);
        }

        var node = new SequenceNode(temps, statements);
        scopeStack.Pop();
        scopeDepth--;
        Tracer.Log($"Pop scope {scopeName}: {scopeDepth}");
        return node;
    }

    /// <inheritdoc/>
    public override AstNode VisitStatement(Smalltalk80Parser.StatementContext context)
    {
        if (context.answer() is { } answer)
        {
            var expr = (ExpressionNode)Visit(answer.expression())!;
            return new AnswerStatement(expr);
        }

        if (context.expression() is { } exprCtx)
            return new ExpressionStatement((ExpressionNode)Visit(exprCtx)!);

        // Standalone comments produce no executable statement.
        return new ExpressionStatement(new LiteralExpression(null!));
    }

    /// <inheritdoc/>
    public override AstNode VisitExpressions(Smalltalk80Parser.ExpressionsContext context)
    {
        var expressions = new List<ExpressionNode>();
        foreach (var exprCtx in context.expression())
        {
            expressions.Add((ExpressionNode)Visit(exprCtx)!);
        }
        return new ExpressionsNode(expressions);
    }

    /// <inheritdoc/>
    public override AstNode VisitExpression(Smalltalk80Parser.ExpressionContext context)
    {
        ExpressionNode receiver = (ExpressionNode)Visit(context.keywordExpr())!;

        foreach (var cascade in context.cascade())
        {
            receiver = ApplyMessageSends(receiver, cascade.messageSend());
        }

        return receiver;
    }

    /// <summary>
    /// Visits a <c>keywordExpr</c> rule consisting of zero or more keyword
    /// messages sent to a binary expression.
    /// </summary>
    public override AstNode VisitKeywordExpr(Smalltalk80Parser.KeywordExprContext context)
    {
        ExpressionNode receiver = (ExpressionNode)Visit(context.binaryExpr())!;

        var keywords = context.KEYWORD();
        if (keywords.Length == 0)
            return receiver;

        var expressions = context.expression();
        var parts = new List<string>(keywords.Length);
        var args = new List<ExpressionNode>(keywords.Length);

        for (var i = 0; i < keywords.Length; i++)
        {
            parts.Add(keywords[i].GetText());
            args.Add((ExpressionNode)Visit(expressions[i])!);
        }

        return FlattenKeywordSends(new KeywordSendExpression(receiver, parts, args));
    }

    /// <summary>
    /// Visits a <c>binaryExpr</c> rule consisting of one or more unary
    /// expressions combined with binary messages.
    /// </summary>
    public override AstNode VisitBinaryExpr(Smalltalk80Parser.BinaryExprContext context)
    {
        ExpressionNode receiver = (ExpressionNode)Visit(context.unaryExpr())!;

        foreach (var binary in context.binaryMessage())
        {
            var arg = (ExpressionNode)Visit(binary.unaryExpr())!;
            var selector = binary.BINARY_SELECTOR()?.GetText() ??
                           binary.MINUS()?.GetText() ?? string.Empty;
            receiver = new BinarySendExpression(receiver, selector, arg);
        }

        return receiver;
    }

    /// <summary>
    /// Visits a <c>unaryExpr</c> rule consisting of a primary expression
    /// followed by zero or more unary messages.
    /// </summary>
    public override AstNode VisitUnaryExpr(Smalltalk80Parser.UnaryExprContext context)
    {
        ExpressionNode receiver = (ExpressionNode)Visit(context.primaryExpression())!;

        foreach (var unary in context.unaryMessage())
        {
            receiver = new UnarySendExpression(receiver, unary.UNARY_SELECTOR().GetText());
        }

        return receiver;
    }

    /// <summary>
    /// Applies a keyword message to the specified receiver.
    /// </summary>
    /// <param name="receiver">The receiver of the message.</param>
    /// <param name="keywords">The keyword selector tokens comprising the message.</param>
    /// <param name="expressions">The expressions serving as arguments.</param>
    /// <returns>The resulting expression node.</returns>
    private ExpressionNode ApplyKeywordMessage(
        ExpressionNode receiver,
        IReadOnlyList<ITerminalNode> keywords,
        IReadOnlyList<Smalltalk80Parser.ExpressionContext> expressions)
    {
        var parts = new List<string>();
        var args = new List<ExpressionNode>();

        for (var i = 0; i < keywords.Count; i++)
        {
            parts.Add(keywords[i].GetText());
            args.Add((ExpressionNode)Visit(expressions[i])!);
        }

        return FlattenKeywordSends(new KeywordSendExpression(receiver, parts, args));
    }

    private ExpressionNode ApplyMessageSends(
        ExpressionNode receiver,
        IEnumerable<Smalltalk80Parser.MessageSendContext> sends)
    {
        foreach (var send in sends)
        {
            if (send.KEYWORD().Length > 0)
            {
                receiver = ApplyKeywordMessage(receiver, send.KEYWORD(), send.expression());
            }
            else if (send.binaryMessage() is { } binary)
            {
                var arg = (ExpressionNode)Visit(binary.unaryExpr())!;
                var selector = binary.BINARY_SELECTOR()?.GetText() ??
                               binary.MINUS()?.GetText() ?? string.Empty;
                receiver = new BinarySendExpression(receiver, selector, arg);
            }
            else if (send.unaryMessage() is { } unary)
            {
                receiver = new UnarySendExpression(receiver, unary.UNARY_SELECTOR().GetText());
            }
        }

        return receiver;
    }

    /// <inheritdoc/>
    public override AstNode VisitAssignment(Smalltalk80Parser.AssignmentContext context)
    {
        var variable = context.variable().GetText();
        var value = (ExpressionNode)Visit(context.expression())!;
        return new AssignmentExpression(variable, value);
    }

    /// <inheritdoc/>
    public override AstNode VisitVariable(Smalltalk80Parser.VariableContext context)
    {
        var name = context.UNARY_SELECTOR().GetText();
        return name switch
        {
            "self" or "true" or "false" or "nil" or "super" => new PseudoVariableExpression(name),
            _ => new VariableExpression(name)
        };
    }



    private static ExpressionNode FlattenKeywordSends(ExpressionNode node)
    {
        while (node is KeywordSendExpression outer &&
               outer.Arguments.Count == 1 &&
               outer.Arguments[0] is KeywordSendExpression inner)
        {
            var parts = outer.SelectorParts.Concat(inner.SelectorParts).ToList();
            var args = new List<ExpressionNode> { inner.Receiver };
            args.AddRange(inner.Arguments);
            node = new KeywordSendExpression(outer.Receiver, parts, args);
        }

        return node;
    }




    /// <inheritdoc/>
    public override AstNode VisitOperand(Smalltalk80Parser.OperandContext context)
    {
        if (context.literal() is not null) return Visit(context.literal());
        if (context.reference() is not null) return Visit(context.reference());
        return Visit(context.subexpression());
    }

    /// <inheritdoc/>
    public override AstNode VisitRuntimeLiteral(Smalltalk80Parser.RuntimeLiteralContext context)
    {
        if (context.dynamicDictionary() is { } dd)
            return Visit(dd);
        if (context.dynamicArray() is { } da)
            return Visit(da);
        if (context.block() is { } blk)
            return Visit(blk);
        return base.VisitRuntimeLiteral(context);
    }

    /// <inheritdoc/>
    public override AstNode VisitSubexpression(Smalltalk80Parser.SubexpressionContext context)
        => Visit(context.expression());

    /// <inheritdoc/>
    public override AstNode VisitLiteral(Smalltalk80Parser.LiteralContext context)
    {
        if (context.runtimeLiteral() is not null) return Visit(context.runtimeLiteral());
        return Visit(context.parsetimeLiteral());
    }

    /// <inheritdoc/>
    public override AstNode VisitParsetimeLiteral(Smalltalk80Parser.ParsetimeLiteralContext context)
    {
        if (context.number() is not null) return Visit(context.number());
        if (context.stringLiteral() is not null) return new LiteralExpression(context.stringLiteral().GetText().Trim('\''));
        if (context.symbol() is not null) return new LiteralExpression(context.symbol().GetText());
        if (context.charConstant() is not null) return new LiteralExpression(context.charConstant().GetText());
        if (context.pseudoVariable() is { } pv)
            return new PseudoVariableExpression(pv.GetText());
        return base.VisitParsetimeLiteral(context);
    }

    /// <inheritdoc/>
    public override AstNode VisitNumber(Smalltalk80Parser.NumberContext context)
        => Visit(context.stInteger());

    /// <inheritdoc/>
    public override AstNode VisitStInteger(Smalltalk80Parser.StIntegerContext context)
    {
        var value = int.Parse(context.GetText());
        return new LiteralExpression(value);
    }

    /// <inheritdoc/>
    public override AstNode VisitDynamicArray(Smalltalk80Parser.DynamicArrayContext context)
    {
        var elements = context.expressions() is { } ex
            ? ((ExpressionsNode)Visit(ex)!).Expressions
            : Array.Empty<ExpressionNode>();
        return new DynamicArrayExpression(elements);
    }

    /// <inheritdoc/>
    public override AstNode VisitDynamicDictionary(Smalltalk80Parser.DynamicDictionaryContext context)
    {
        var elements = context.expressions() is { } ex
            ? ((ExpressionsNode)Visit(ex)!).Expressions
            : Array.Empty<ExpressionNode>();
        return new DynamicDictionaryExpression(elements);
    }

    /// <inheritdoc/>
    public override AstNode VisitBlock(Smalltalk80Parser.BlockContext context)
    {
        if (context.nonEmptyBlockBody() is not { } body)
        {
            return new BlockExpression(
                Array.Empty<string>(),
                new SequenceNode(new List<string>(), new List<StatementNode>()));
        }

        var parameters = body.blockParamList() is { } paramsCtx
            ? paramsCtx.BLOCK_PARAM().Select(p => p.GetText().TrimStart(':')).ToList()
            : new List<string>();

        var seq = ProcessSequence(body.temps(), body.statements());
        return new BlockExpression(parameters, seq);
    }

    /// <inheritdoc/>
    public override AstNode VisitFileIn(Smalltalk80Parser.FileInContext context)
    {
        var chunks = new List<AstNode>();
        foreach (var chunk in context.chunk())
        {
            var node = Visit(chunk);
            if (node is not null)
                chunks.Add(node);
        }

        return new FileNode(chunks);
    }

    /// <inheritdoc/>
    public override AstNode VisitStandaloneCommentChunk(Smalltalk80Parser.StandaloneCommentChunkContext context)
        => null!;


    /// <inheritdoc/>
    public override AstNode VisitClassDefinition(Smalltalk80Parser.ClassDefinitionContext context)
    {
        var className = context.UNARY_SELECTOR(1).GetText();
        Tracer.Log($"Parsing class definition: {className}");

        var superclass = context.UNARY_SELECTOR(0).GetText();
        var instanceVars = new List<string>();
        var classVars = new List<string>();
        var pools = new List<string>();
        string? category = null;

        foreach (var opt in context.classOption())
        {
            var text = opt.GetChild(0).GetText();
            var value = opt.stringLiteral().GetText();
            if (text.StartsWith("instanceVariableNames:", StringComparison.Ordinal))
            {
                instanceVars.AddRange(ExtractIdentifiersFromString(value));
            }
            else if (text.StartsWith("classVariableNames:", StringComparison.Ordinal))
            {
                classVars.AddRange(ExtractIdentifiersFromString(value));
            }
            else if (text.StartsWith("poolDictionaries:", StringComparison.Ordinal))
            {
                pools.AddRange(ExtractIdentifiersFromString(value));
            }
            else if (text.StartsWith("category:", StringComparison.Ordinal))
            {
                category = value.Trim('\'');
            }
        }

        return new ClassDefinitionNode(superclass, className, instanceVars, classVars, pools, category);
    }

    private static IReadOnlyList<string> ExtractIdentifiersFromString(string value)
        => value.Trim('\'').Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <inheritdoc/>
    public override AstNode VisitMethodCategoryHeader(Smalltalk80Parser.MethodCategoryHeaderContext context)
    {
        var className = context.UNARY_SELECTOR().GetText();
        var isClassSide = context.GetToken(Smalltalk80Parser.T__4, 0) is not null;
        var category = context.stringLiteral().GetText().Trim('\'');
        return new MethodHeaderNode(className, isClassSide, category);
    }

    /// <inheritdoc/>
    public override AstNode VisitClassComment(Smalltalk80Parser.ClassCommentContext context)
    {
        var className = context.className().GetText();
        var comment = context.stringLiteral().GetText().Trim('\'');
        return new ClassCommentNode(className, comment);
    }

    /// <inheritdoc/>
    public override AstNode VisitMethodDefinition(Smalltalk80Parser.MethodDefinitionContext context)
    {
        var (selector, args) = ParseMethodSelector(context.methodSelector());
        Tracer.Log($"Parsing method: {selector}");
        Tracer.Log($"Entering method: {selector}");

        scopeStack.Clear();
        var methodVisitor = new AstBuildingVisitor(selector, args, initialVariables);
        var bodyCtx = context.sequence();
        SequenceNode body = bodyCtx is not null
            ? (SequenceNode)methodVisitor.Visit(bodyCtx)!
            : new SequenceNode(new List<string>(), new List<StatementNode>());
        bool hasPrimitive = false;
        int primitiveIndex = 0;
        if (context.primitive() is { } primitive)
        {
            var text = primitive.PRIMITIVE().GetText();
            var digits = text.AsSpan("<primitive:".Length, text.Length - "<primitive:".Length - 1);
            primitiveIndex = int.Parse(digits);
            hasPrimitive = true;
        }
        string? comment = null;
        var commentTokens = context.COMMENT();
        if (commentTokens.Length > 0)
            comment = string.Join("\n", commentTokens.Select(c => c.GetText().Trim('"')));
        scopeStack.Clear();

        Tracer.Log($"Exiting method: {selector}");
        return new MethodDefinitionNode(selector, args, body, comment, "", hasPrimitive, primitiveIndex);
    }

    private static (string Selector, List<string> Args) ParseMethodSelector(Smalltalk80Parser.MethodSelectorContext context)
    {
        if (context.unarySelector() is { } u)
        {
            return (u.UNARY_SELECTOR().GetText(), new List<string>());
        }

        if (context.binarySelector() is { } b)
        {
            var selector = b.GetText();
            var arg = context.UNARY_SELECTOR().GetText();
            return (selector, new List<string> { arg });
        }

        var selectorParts = new List<string>();
        var args = new List<string>();
        foreach (var part in context.keywordSelector().keywordPart())
        {
            selectorParts.Add(part.KEYWORD().GetText());
            args.Add(part.UNARY_SELECTOR().GetText());
        }

        return (string.Join(string.Empty, selectorParts), args);
    }
}

/// <summary>
/// Node representing a list of expressions.
/// </summary>
/// <param name="Expressions">The expressions in evaluation order.</param>
public sealed record ExpressionsNode(IReadOnlyList<ExpressionNode> Expressions) : AstNode;

/// <summary>
/// Node representing a collection of statements.
/// </summary>
/// <param name="Statements">The statements in execution order.</param>
public sealed record StatementsNode(IReadOnlyList<StatementNode> Statements) : AstNode;

