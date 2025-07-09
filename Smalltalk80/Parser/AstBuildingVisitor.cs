using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Microsoft.CSharp.RuntimeBinder;
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
    private readonly CommonTokenStream tokens;

    /// <summary>
    /// Extracts temporary variable names from a <see cref="Smalltalk80Parser.TempsContext"/>
    /// using whichever rule names are available in the generated parser.
    /// </summary>
    /// <param name="tempsCtx">The context containing temporary variable declarations.</param>
    /// <returns>An enumeration of the declared temporary variable names.</returns>
    private static IEnumerable<string> GetTempNames(Smalltalk80Parser.TempsContext tempsCtx)
    {
        var varMethod = tempsCtx.GetType().GetMethod("variable", Type.EmptyTypes);
        if (varMethod?.Invoke(tempsCtx, null) is IEnumerable<object> vars)
            return vars.OfType<ParserRuleContext>().Select(v => v.GetText().Trim());

        var idMethod = tempsCtx.GetType().GetMethod("IDENTIFIER", Type.EmptyTypes);
        if (idMethod?.Invoke(tempsCtx, null) is IEnumerable<ITerminalNode> ids)
            return ids.Select(id => id.GetText().Trim());

        return Enumerable.Empty<string>();
    }

    /// <summary>
    /// Extracts block parameter names from a <see cref="Smalltalk80Parser.BlockParamListContext"/>.
    /// </summary>
    /// <param name="ctx">The parameter list context.</param>
    /// <returns>A list of parameter names in declaration order.</returns>
    private static IReadOnlyList<string> GetBlockParameterNames(Smalltalk80Parser.BlockParamListContext ctx)
    {
        var paramMethod = ctx.GetType().GetMethod("BLOCK_PARAM", Type.EmptyTypes);
        if (paramMethod?.Invoke(ctx, null) is IEnumerable<ITerminalNode> tokens)
            return tokens.Select(t => t.GetText().TrimStart(':')).ToList();

        var idMethod = ctx.GetType().GetMethod("IDENTIFIER", Type.EmptyTypes);
        if (idMethod?.Invoke(ctx, null) is IEnumerable<ITerminalNode> ids)
            return ids.Select(id => id.GetText()).ToList();

        return Array.Empty<string>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AstBuildingVisitor"/> class.
    /// </summary>
    /// <param name="tokens">The token stream produced by the lexer.</param>
    /// <param name="methodName">Optional selector of the method being parsed.</param>
    /// <param name="arguments">Method argument names available in the outer scope.</param>
    /// <param name="variables">Instance or class variables available in the outer scope.</param>
    public AstBuildingVisitor(CommonTokenStream tokens, string? methodName = null, IReadOnlyList<string>? arguments = null, IReadOnlyList<string>? variables = null)
    {
        this.tokens = tokens;
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
        => ProcessSequence(context.temps(), context.primitive(), context.statements(), extraTemps);

    /// <summary>
    /// Processes a sequence represented by discrete temporary and statement contexts.
    /// </summary>
    /// <param name="tempsCtx">The optional temporary variable declarations.</param>
    /// <param name="primCtx">The optional primitive declaration appearing before any statements.</param>
    /// <param name="statementsCtx">The statements comprising the sequence.</param>
    /// <param name="extraTemps">Any additional temporary variables in scope.</param>
    /// <returns>The constructed <see cref="SequenceNode"/>.</returns>
    private SequenceNode ProcessSequence(
        Smalltalk80Parser.TempsContext? tempsCtx,
        Smalltalk80Parser.PrimitiveContext? primCtx,
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
            temps.AddRange(GetTempNames(tempsCtx));

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
        if (primCtx is { })
            statements.Add((StatementNode)VisitPrimitive(primCtx)!);

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

    /// <summary>
    /// Processes a block's temporaries and statements to build a <see cref="SequenceNode"/>.
    /// </summary>
    /// <param name="tempsCtx">The optional temporary variable declarations.</param>
    /// <param name="statementsCtx">The optional statements forming the block body.</param>
    /// <returns>The constructed <see cref="SequenceNode"/>.</returns>
    private SequenceNode ProcessBlockSequence(
        Smalltalk80Parser.TempsContext? tempsCtx,
        Smalltalk80Parser.BlockStatementsContext? statementsCtx)
    {
        var scopeName = scopeDepth == 0
            ? (currentMethodName ?? "(unknown)")
            : $"block {scopeDepth}";
        Tracer.Log($"Push scope {scopeName}: {scopeDepth + 1}");
        scopeDepth++;

        var temps = new List<string>();
        if (tempsCtx is { })
            temps.AddRange(GetTempNames(tempsCtx));

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
            var expr = (ExpressionNode)Visit(answer.returnExpr().expression())!;
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
        ExpressionNode receiver = (ExpressionNode)Visit(context.keywordExpression())!;

        foreach (var cascade in context.cascadeMessage())
        {
            receiver = ApplyCascade(receiver, cascade);
        }

        return receiver;
    }

    /// <inheritdoc/>
    public override AstNode VisitUnaryExpression(Smalltalk80Parser.UnaryExpressionContext context)
    {
        ExpressionNode receiver = (ExpressionNode)Visit(context.primaryExpression())!;

        foreach (var unary in context.unaryMessage())
        {
            receiver = ApplyUnaryMessage(receiver, unary);
        }

        return receiver;
    }

    /// <inheritdoc/>
    public override AstNode VisitBinaryExpression(Smalltalk80Parser.BinaryExpressionContext context)
    {
        var expressions = context.unaryExpression();
        ExpressionNode receiver = (ExpressionNode)Visit(expressions[0])!;

        ITerminalNode? opToken = null;
        for (int i = 1; i < context.ChildCount; i++)
        {
            var child = context.GetChild(i);
            switch (child)
            {
                case ITerminalNode t:
                    opToken = t;
                    break;
                case Smalltalk80Parser.UnaryExpressionContext ue when opToken is not null:
                    receiver = ApplyBinaryMessage(receiver, opToken, ue);
                    opToken = null;
                    break;
            }
        }

        return receiver;
    }

    /// <inheritdoc/>
    public override AstNode VisitKeywordExpression(Smalltalk80Parser.KeywordExpressionContext context)
    {
        var binaries = context.binaryExpression();
        ExpressionNode receiver = (ExpressionNode)Visit(binaries[0])!;

        var keywords = context.KEYWORD();
        if (keywords.Length == 0)
            return receiver;

        var args = new List<ParserRuleContext>();
        for (var i = 1; i < binaries.Length; i++)
            args.Add(binaries[i]);

        return ApplyKeywordMessage(receiver, keywords, args);
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
        IReadOnlyList<ParserRuleContext> expressions)
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

    /// <summary>
    /// Applies a unary message send to the specified receiver.
    /// </summary>
    /// <param name="receiver">The expression acting as the message receiver.</param>
    /// <param name="message">The unary message context.</param>
    /// <returns>The resulting expression after applying the message.</returns>
    private ExpressionNode ApplyUnaryMessage(ExpressionNode receiver, Smalltalk80Parser.UnaryMessageContext message)
    {
        var selector = message.IDENTIFIER()?.GetText() ?? message.CLASS().GetText();
        return new UnarySendExpression(receiver, selector);
    }

    /// <summary>
    /// Applies a binary message send to the specified receiver.
    /// </summary>
    /// <param name="receiver">The expression acting as the receiver.</param>
    /// <param name="selectorToken">The token representing the binary selector.</param>
    /// <param name="argument">The unary expression supplying the argument.</param>
    /// <returns>The resulting expression after applying the message.</returns>
    private ExpressionNode ApplyBinaryMessage(
        ExpressionNode receiver,
        ITerminalNode selectorToken,
        Smalltalk80Parser.UnaryExpressionContext argument)
    {
        var arg = (ExpressionNode)Visit(argument)!;
        var selector = selectorToken.GetText();
        return new BinarySendExpression(receiver, selector, arg);
    }

    /// <summary>
    /// Applies a cascade message to the previously evaluated receiver.
    /// </summary>
    /// <param name="receiver">The expression that should receive the cascade message.</param>
    /// <param name="cascade">The cascade message context.</param>
    /// <returns>The resulting expression after applying the cascade.</returns>
    private ExpressionNode ApplyCascade(ExpressionNode receiver, Smalltalk80Parser.CascadeMessageContext cascade)
    {
        if (cascade.keywordExpression() is { } kwExpr)
        {
            var keywords = kwExpr.KEYWORD().Concat(kwExpr.COMMENT_KW()).ToArray();
            var binaries = kwExpr.binaryExpression();
            var args = new List<ParserRuleContext>();
            for (var i = 1; i < binaries.Length; i++)
                args.Add(binaries[i]);
            return ApplyKeywordMessage(receiver, keywords, args);
        }

        var unaryExprs = cascade.unaryExpression();
        if (unaryExprs.Length == 1)
        {
            return ApplyUnaryChain(receiver, unaryExprs[0]);
        }

        if (unaryExprs.Length == 2)
        {
            var first = ApplyUnaryChain(receiver, unaryExprs[0]);
            var op = (ITerminalNode?)cascade.BINARY_SELECTOR() ?? cascade.PIPE() ?? cascade.MINUS();
            if (op is not null)
                return ApplyBinaryMessage(first, op, unaryExprs[1]);
        }

        return receiver;
    }

    /// <summary>
    /// Applies a chain of unary messages represented by a <see cref="Smalltalk80Parser.UnaryExpressionContext"/>.
    /// </summary>
    /// <param name="receiver">The expression acting as the receiver.</param>
    /// <param name="context">The unary expression context containing message tokens.</param>
    /// <returns>The resulting expression after applying all unary messages.</returns>
    private ExpressionNode ApplyUnaryChain(ExpressionNode receiver, Smalltalk80Parser.UnaryExpressionContext context)
    {
        if (TryGetUnarySelector(context.primaryExpression(), out var selector))
            receiver = new UnarySendExpression(receiver, selector);

        foreach (var msg in context.unaryMessage())
            receiver = ApplyUnaryMessage(receiver, msg);

        return receiver;
    }

    /// <summary>
    /// Attempts to extract a unary message selector from the given primary expression context.
    /// </summary>
    /// <param name="context">The primary expression to analyze.</param>
    /// <param name="selector">The resulting selector string if successful.</param>
    /// <returns><see langword="true"/> if a selector was extracted; otherwise <see langword="false"/>.</returns>
    private static bool TryGetUnarySelector(Smalltalk80Parser.PrimaryExpressionContext context, out string selector)
    {
        selector = string.Empty;
        if (context.operand()?.reference()?.variable() is { } varCtx)
        {
            selector = varCtx.IDENTIFIER()?.GetText() ?? varCtx.CLASS()?.GetText() ?? string.Empty;
            return selector.Length > 0;
        }

        return false;
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
        var name = context.IDENTIFIER().GetText();
        return name switch
        {
            "self" or "true" or "false" or "nil" or "super" => new PseudoVariableExpression(name),
            _ => new VariableExpression(name)
        };
    }



    /// <summary>
    /// Collapses nested keyword sends where the outer send has a single argument
    /// that is itself a keyword send.
    /// </summary>
    /// <param name="node">The expression to inspect.</param>
    /// <returns>The flattened expression with nested sends merged.</returns>
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
    public override AstNode VisitPrimitive(Smalltalk80Parser.PrimitiveContext context)
    {
        var text = context.PRIMITIVE().GetText();
        var digits = text.AsSpan("<primitive:".Length, text.Length - "<primitive:".Length - 1);
        var index = int.Parse(digits);
        return new PrimitiveStatement(index);
    }

    /// <inheritdoc/>
    public override AstNode VisitDynamicArray(Smalltalk80Parser.DynamicArrayContext context)
    {
        var elements = context.expression().Select(e => (ExpressionNode)Visit(e)!).ToList();
        return new DynamicArrayExpression(elements);
    }

    /// <inheritdoc/>
    public override AstNode VisitDynamicDictionary(Smalltalk80Parser.DynamicDictionaryContext context)
    {
        var elements = context.expression().Select(e => (ExpressionNode)Visit(e)!).ToList();
        return new DynamicDictionaryExpression(elements);
    }

    /// <summary>
    /// Visits a block literal and converts it into a <see cref="BlockExpression"/>
    /// containing its parameter list and body sequence.
    /// </summary>
    /// <param name="context">The parse context representing the block.</param>
    /// <returns>The constructed <see cref="BlockExpression"/>.</returns>
    public override AstNode VisitBlock(Smalltalk80Parser.BlockContext context)
    {
        List<string> parameters = context.blockParamList() is { } paramsCtx
            ? new List<string>(GetBlockParameterNames(paramsCtx))
            : [];

        // Newer grammars may encapsulate the block body in a <sequence> or
        // <blockBody> rule depending on version.
        var seqMethod = context.GetType().GetMethod("blockBody")
            ?? context.GetType().GetMethod("sequence");
        SequenceNode seq;
        if (seqMethod?.Invoke(context, null) is Smalltalk80Parser.SequenceContext sc)
        {
            seq = ProcessSequence(sc);
        }
        else
        {
            var blockMethod = context.GetType().GetMethod("blockStatements");
            if (blockMethod?.Invoke(context, null) is Smalltalk80Parser.BlockStatementsContext blockStmts)
            {
                seq = ProcessBlockSequence(context.temps(), blockStmts);
            }
            else
            {
                var legacy = context.GetType().GetMethod("statements");
                seq = legacy?.Invoke(context, null) is Smalltalk80Parser.StatementsContext stmts
                    ? ProcessSequence(context.temps(), null, stmts)
                    : ProcessSequence(context.temps(), null, null);
            }
        }
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

    /// <summary>
    /// Splits a quoted space-separated string of identifiers into a list.
    /// </summary>
    /// <param name="value">The quoted string containing identifiers.</param>
    /// <returns>A collection of identifiers extracted from the string.</returns>
    private static IReadOnlyList<string> ExtractIdentifiersFromString(string value)
        => value.Trim('\'').Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <inheritdoc/>
    public override AstNode VisitMethodCategoryHeaderFile(Smalltalk80Parser.MethodCategoryHeaderFileContext context)
        => CreateMethodHeader(context.IDENTIFIER().GetText(), context.CLASS() is not null, context.stringLiteral().GetText());

    /// <inheritdoc/>
    public override AstNode VisitMethodCategoryHeaderRepl(Smalltalk80Parser.MethodCategoryHeaderReplContext context)
        => CreateMethodHeader(context.IDENTIFIER().GetText(), context.CLASS() is not null, context.stringLiteral().GetText());

    /// <summary>
    /// Creates a <see cref="MethodHeaderNode"/> from a parsed category header.
    /// </summary>
    /// <param name="className">The name of the owning class.</param>
    /// <param name="isClassSide">Whether the methods belong to the class side.</param>
    /// <param name="categoryLiteral">The category string literal.</param>
    /// <returns>The constructed <see cref="MethodHeaderNode"/>.</returns>
    private static MethodHeaderNode CreateMethodHeader(string className, bool isClassSide, string categoryLiteral)
        => new(className, isClassSide, categoryLiteral.Trim('\''));

    /// <inheritdoc/>
    public override AstNode VisitReplMethodsForCategory(
        Smalltalk80Parser.ReplMethodsForCategoryContext context)
    {
        var header = (MethodHeaderNode)Visit(context.methodCategoryHeaderRepl())!;
        var chunks = new List<AstNode> { header };
        foreach (var methodCtx in context.methodDefinition())
        {
            var method = (MethodDefinitionNode)Visit(methodCtx)!;
            chunks.Add(method);
        }

        return new FileNode(chunks);
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
        var methodVisitor = new AstBuildingVisitor(tokens, selector, args, initialVariables);
        var bodyCtx = context.sequence();
        SequenceNode body = bodyCtx is not null
            ? (SequenceNode)methodVisitor.Visit(bodyCtx)!
            : new SequenceNode(new List<string>(), new List<StatementNode>());
        bool hasPrimitive = false;
        int primitiveIndex = 0;

        if (body.Statements.Count > 0 && body.Statements[0] is PrimitiveStatement primStmt)
        {
            // Legacy form where the primitive is included as the first statement.
            hasPrimitive = true;
            primitiveIndex = primStmt.PrimitiveIndex;
            body = new SequenceNode(body.Temporaries, body.Statements.Skip(1).ToList());
        }
        string? comment = null;
        var hidden = tokens.GetHiddenTokensToRight(context.Stop.TokenIndex, TokenConstants.HiddenChannel);
        if (hidden is { Count: > 0 })
        {
            var commentLines = hidden
                .Where(t => t.Type == Smalltalk80Parser.COMMENT)
                .Select(t => t.Text.Trim('"'))
                .ToList();
            if (commentLines.Count > 0)
                comment = string.Join("\n", commentLines);
        }
        scopeStack.Clear();

        Tracer.Log($"Exiting method: {selector}");
        return new MethodDefinitionNode(selector, args, body, comment, "", hasPrimitive, primitiveIndex);
    }

    /// <summary>
    /// Visits a <see cref="Smalltalk80Parser.MethodDefinitionWithBangContext"/> and returns a <see cref="MethodDefinitionNode"/>.
    /// </summary>
    /// <param name="context">The parse context to visit.</param>
    /// <returns>The resulting <see cref="MethodDefinitionNode"/>.</returns>
    public override AstNode VisitMethodDefinitionWithBang(Smalltalk80Parser.MethodDefinitionWithBangContext context)
    {
        var (selector, args) = ParseMethodSelector(context.methodSelector());
        Tracer.Log($"Parsing method: {selector}");
        Tracer.Log($"Entering method: {selector}");

        scopeStack.Clear();
        var methodVisitor = new AstBuildingVisitor(tokens, selector, args, initialVariables);
        var bodyCtx = context.sequence();
        SequenceNode body = bodyCtx is not null
            ? (SequenceNode)methodVisitor.Visit(bodyCtx)!
            : new SequenceNode(new List<string>(), new List<StatementNode>());

        bool hasPrimitive = false;
        int primitiveIndex = 0;

        if (body.Statements.Count > 0 && body.Statements[0] is PrimitiveStatement primStmt)
        {
            hasPrimitive = true;
            primitiveIndex = primStmt.PrimitiveIndex;
            body = new SequenceNode(body.Temporaries, body.Statements.Skip(1).ToList());
        }

        string? comment = null;
        var bang = context.GetType().GetMethod("BANG")?.Invoke(context, null) as ITerminalNode;
        if (bang is not null)
        {
            var hidden = tokens.GetHiddenTokensToRight(bang.Symbol.TokenIndex, TokenConstants.HiddenChannel);
            if (hidden is { Count: > 0 })
            {
                var commentLines = hidden
                    .Where(t => t.Type == Smalltalk80Parser.COMMENT)
                    .Select(t => t.Text.Trim('"'))
                    .ToList();
                if (commentLines.Count > 0)
                    comment = string.Join("\n", commentLines);
            }
        }
        scopeStack.Clear();

        Tracer.Log($"Exiting method: {selector}");
        return new MethodDefinitionNode(selector, args, body, comment, "", hasPrimitive, primitiveIndex);
    }

    /// <summary>
    /// Parses a method selector context into its selector string and argument list.
    /// </summary>
    /// <param name="context">The parse context describing the selector.</param>
    /// <returns>The selector string and a list of argument names.</returns>
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

