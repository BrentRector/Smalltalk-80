using System;
using System.Collections.Generic;
using System.Linq;
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
    {
        var scopeName = scopeDepth == 0
            ? (currentMethodName ?? "(unknown)")
            : $"block {scopeDepth}";
        Tracer.Log($"Push scope {scopeName}: {scopeDepth + 1}");
        scopeDepth++;

        var temps = new List<string>();
        if (extraTemps is not null)
            temps.AddRange(extraTemps);
        if (context.temps() is { } tctx)
        {
            temps.AddRange(
                tctx.IDENTIFIER()
                    .Select(id => id.GetText().Trim()));
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
        if (context.expressions() is { } exCtx)
        {
            var exprs = ((ExpressionsNode)Visit(exCtx)!).Expressions;
            statements.AddRange(exprs.Select(e => (StatementNode)new ExpressionStatement(e)));
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

        return new ExpressionStatement((ExpressionNode)Visit(context.expression())!);
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
        var expr = (ExpressionNode)Visit(context.messageChain())!;

        if (context.message().Length == 0)
            return expr;

        var receiver = expr;
        var messages = new List<ExpressionNode>();

        if (receiver is CascadeExpression existing)
        {
            receiver = existing.Receiver;
            messages.AddRange(existing.Messages);
        }

        foreach (var msgCtx in context.message())
            messages.Add(ProcessMessage(receiver, msgCtx));

        return new CascadeExpression(receiver, messages);
    }

    /// <inheritdoc/>
    public override AstNode VisitMessageChain(Smalltalk80Parser.MessageChainContext context)
    {
        if (context.assignment() is { } assignment)
            return Visit(assignment);

        if (context.primitive() is { } primitive)
            return Visit(primitive);

        if (context.operand() is { } operand)
            return Visit(operand);

        var recv = (ExpressionNode)Visit(context.messageChain())!;

        if (context.unaryMessage() is { } unary)
        {
            var selector = unary.unarySelector().GetText();
            return new UnarySendExpression(recv, selector);
        }

        if (context.binaryMessage() is { } binary)
        {
            var selector = binary.BINARY_SELECTOR()?.GetText() ?? binary.MINUS()?.GetText() ?? string.Empty;
            var arg = (ExpressionNode)Visit(binary.expression())!;
            return new BinarySendExpression(recv, selector, arg);
        }

        if (context.keywordMessage() is { } keyword)
        {
            var parts = new List<string>();
            var args = new List<ExpressionNode>();
            foreach (var pair in keyword.keywordPair())
            {
                parts.Add(pair.KEYWORD().GetText());
                args.Add((ExpressionNode)Visit(pair.expression())!);
            }
            var expr = new KeywordSendExpression(recv, parts, args);
            return FlattenKeywordSends(expr);
        }

        return recv;
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
        => new VariableExpression(context.IDENTIFIER().GetText());



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

    private ExpressionNode ProcessMessage(ExpressionNode receiver, Smalltalk80Parser.MessageContext context)
    {
        if (context.unaryMessage() is { } unary)
        {
            var selector = unary.unarySelector().GetText();
            return new UnarySendExpression(receiver, selector);
        }

        if (context.binaryMessage() is { } binary)
        {
            var selector = binary.BINARY_SELECTOR()?.GetText() ?? binary.MINUS()?.GetText() ?? string.Empty;
            var arg = (ExpressionNode)Visit(binary.expression())!;
            return new BinarySendExpression(receiver, selector, arg);
        }

        if (context.keywordMessage() is { } keyword)
        {
            var parts = new List<string>();
            var args = new List<ExpressionNode>();
            foreach (var pair in keyword.keywordPair())
            {
                parts.Add(pair.KEYWORD().GetText());
                args.Add((ExpressionNode)Visit(pair.expression())!);
            }
            var expr = new KeywordSendExpression(receiver, parts, args);
            return FlattenKeywordSends(expr);
        }

        return receiver;
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
            return new BlockExpression(
                Array.Empty<string>(),
                new SequenceNode(new List<string>(), new List<StatementNode>()));

        if (body.blockParamList() is { } paramsCtx)
        {
            var parameters = paramsCtx.BLOCK_PARAM().Select(p => p.GetText().TrimStart(':')).ToList();
            var seq = body.sequence() is { } seqCtx
                ? ProcessSequence(seqCtx)
                : new SequenceNode(new List<string>(), new List<StatementNode>());
            return new BlockExpression(parameters, seq);
        }

        var outerTemps = body.temps().IDENTIFIER().Select(i => i.GetText()).ToList();
        var innerSeq = ProcessSequence(body.sequence(), outerTemps);
        return new BlockExpression(Array.Empty<string>(), innerSeq);
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
        var className = context.IDENTIFIER(1).GetText();
        Tracer.Log($"Parsing class definition: {className}");

        var superclass = context.IDENTIFIER(0).GetText();
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
        var className = context.IDENTIFIER().GetText();
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
        string? comment = null;
        var commentTokens = context.COMMENT();
        if (commentTokens.Length > 0)
            comment = string.Join("\n", commentTokens.Select(c => c.GetText().Trim('"')));
        scopeStack.Clear();

        Tracer.Log($"Exiting method: {selector}");
        return new MethodDefinitionNode(selector, args, body, comment);
    }

    private static (string Selector, List<string> Args) ParseMethodSelector(Smalltalk80Parser.MethodSelectorContext context)
    {
        if (context.unarySelector() is { } u)
        {
            return (u.IDENTIFIER().GetText(), new List<string>());
        }

        if (context.binarySelector() is { } b)
        {
            var selector = b.GetText();
            var arg = context.IDENTIFIER().GetText();
            return (selector, new List<string> { arg });
        }

        var selectorParts = new List<string>();
        var args = new List<string>();
        foreach (var part in context.keywordSelector().keywordPart())
        {
            selectorParts.Add(part.KEYWORD().GetText());
            args.Add(part.IDENTIFIER().GetText());
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

