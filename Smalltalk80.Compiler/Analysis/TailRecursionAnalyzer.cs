using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime.Tree;
using Smalltalk80.Compiler.Generated;

namespace Smalltalk80.Compiler.Analysis;

/// <summary>
/// Analyzes Smalltalk method parse trees to detect tail-recursive patterns.
/// Identifies methods where the last statement is a recursive call to self,
/// enabling bytecode optimization (jump instead of send).
/// </summary>
/// <remarks>
/// Conservative analysis - rejects any pattern where correctness is uncertain.
/// Better to miss optimization opportunities than generate incorrect code.
///
/// Safety conditions enforced:
/// - Receiver must be 'self' keyword
/// - Selector must match containing method's selector
/// - Call must be in tail position (last statement in execution path)
/// - Call must NOT be inside a block literal [...]
/// - No code after the call (no subsequent statements)
/// - No assignments using call result
/// </remarks>
public sealed class TailRecursionAnalyzer : Smalltalk80ParserBaseVisitor<object?>
{
    private readonly List<string> _diagnosticMessages = [];
    private readonly Dictionary<IParseTree, TailCallInfo> _detectedTailCalls = [];

    private string? _currentMethodSelector;
    private int _blockNestingDepth;
    private bool _diagnosticsEnabled;

    /// <summary>
    /// Analyzes a method parse tree to detect tail-recursive calls.
    /// </summary>
    /// <param name="methodTree">Parse tree of the method (MethodDefContext or MethodBodyContext)</param>
    /// <param name="methodSelector">Selector of the method being analyzed</param>
    /// <returns>Dictionary mapping parse tree nodes to tail call information</returns>
    public Dictionary<IParseTree, TailCallInfo> Analyze(IParseTree methodTree, string methodSelector)
    {
        _detectedTailCalls.Clear();
        _diagnosticMessages.Clear();
        _currentMethodSelector = methodSelector;
        _blockNestingDepth = 0;

        if (_diagnosticsEnabled)
        {
            _diagnosticMessages.Add($"[TailRecursion] Analyzing method: {methodSelector}");
        }

        Visit(methodTree);

        if (_diagnosticsEnabled && _detectedTailCalls.Any())
        {
            _diagnosticMessages.Add($"[TailRecursion] Found {_detectedTailCalls.Count} tail call(s)");
        }

        return _detectedTailCalls;
    }

    /// <summary>
    /// Enables diagnostic output for debugging analyzer behavior.
    /// </summary>
    public void EnableDiagnostics()
    {
        _diagnosticsEnabled = true;
    }

    /// <summary>
    /// Gets diagnostic messages generated during analysis.
    /// </summary>
    public List<string> GetDiagnosticMessages()
    {
        return [.. _diagnosticMessages];
    }

    /// <summary>
    /// Visits method body to find tail calls in statements.
    /// </summary>
    public override object? VisitMethodBody(Smalltalk80Parser.MethodBodyContext context)
    {
        var statements = context.statements();
        if (statements != null)
        {
            AnalyzeStatements(statements);
        }
        return null;
    }

    /// <summary>
    /// Visits method body without bang delimiter.
    /// </summary>
    public override object? VisitMethodBodyNoBang(Smalltalk80Parser.MethodBodyNoBangContext context)
    {
        var statements = context.statements();
        if (statements != null)
        {
            AnalyzeStatements(statements);
        }
        return null;
    }

    /// <summary>
    /// Tracks block nesting depth - reject tail calls inside blocks.
    /// </summary>
    public override object? VisitBlock(Smalltalk80Parser.BlockContext context)
    {
        _blockNestingDepth++;
        try
        {
            return base.VisitBlock(context);
        }
        finally
        {
            _blockNestingDepth--;
        }
    }

    /// <summary>
    /// Analyzes statements to find tail calls in the last statement.
    /// </summary>
    private void AnalyzeStatements(Smalltalk80Parser.StatementsContext statements)
    {
        var statementList = statements.statement();
        if (statementList == null || statementList.Length == 0)
        {
            return;
        }

        // Analyze ONLY the last statement - this is the only tail position
        var lastStatement = statementList[^1];
        AnalyzeStatement(lastStatement, isInTailPosition: true);
    }

    /// <summary>
    /// Analyzes a single statement to detect tail calls.
    /// </summary>
    private void AnalyzeStatement(Smalltalk80Parser.StatementContext statement, bool isInTailPosition)
    {
        // Statement structure:
        // statement: returnStatement | expressionStatement

        var returnStatement = statement.returnStatement();
        var expressionStatement = statement.expressionStatement();

        if (returnStatement != null)
        {
            // Return statement: ^expression
            var expression = returnStatement.expression();
            if (expression != null)
            {
                // The returned expression is in tail position if the statement is
                AnalyzeExpression(expression, isInTailPosition);
            }
        }
        else if (expressionStatement != null && isInTailPosition)
        {
            // Expression statement (no return) in tail position
            var expression = expressionStatement.expression();
            if (expression != null)
            {
                AnalyzeExpression(expression, isInTailPosition: true);
            }
        }
    }

    /// <summary>
    /// Analyzes an expression to detect message sends to self.
    /// </summary>
    private void AnalyzeExpression(Smalltalk80Parser.ExpressionContext expression, bool isInTailPosition)
    {
        // Expression -> assignmentExpr
        var assignmentExpr = expression.assignmentExpr();
        if (assignmentExpr == null)
        {
            return;
        }

        // Check if this is an assignment - assignments are NOT tail calls
        var variable = assignmentExpr.variable();
        if (variable != null)
        {
            if (_diagnosticsEnabled)
            {
                _diagnosticMessages.Add("[TailRecursion] Rejected: assignment expression");
            }
            return;
        }

        // assignmentExpr -> cascadeExpr
        var cascadeExpr = assignmentExpr.cascadeExpr();
        if (cascadeExpr != null)
        {
            AnalyzeCascadeExpression(cascadeExpr, isInTailPosition);
        }
    }

    /// <summary>
    /// Analyzes cascade expression (may contain semicolon-separated message sends).
    /// </summary>
    private void AnalyzeCascadeExpression(Smalltalk80Parser.CascadeExprContext cascadeExpr, bool isInTailPosition)
    {
        // Cascade: messageExpr (SEMICOLON messagePart)*
        // Only the LAST message in the cascade can be a tail call

        var messageParts = cascadeExpr.messagePart();
        if (messageParts != null && messageParts.Length > 0)
        {
            // Has cascade - only the last message part is in tail position
            var lastMessagePart = messageParts[^1];
            AnalyzeMessagePart(lastMessagePart, isInTailPosition);
        }
        else
        {
            // No cascade - analyze the single message expression
            var messageExpr = cascadeExpr.messageExpr();
            if (messageExpr != null)
            {
                AnalyzeMessageExpression(messageExpr, isInTailPosition);
            }
        }
    }

    /// <summary>
    /// Analyzes a message part (from cascade).
    /// </summary>
    private void AnalyzeMessagePart(Smalltalk80Parser.MessagePartContext messagePart, bool isInTailPosition)
    {
        // messagePart: unaryMessage | binaryMessage | keywordMessage

        var unaryMessage = messagePart.unaryMessage();
        if (unaryMessage != null)
        {
            // Cannot determine receiver from message part alone - need full context
            // Cascades send to the same receiver, so this is complex
            // Conservative: reject cascade tail calls for now
            if (_diagnosticsEnabled)
            {
                _diagnosticMessages.Add("[TailRecursion] Rejected: cascade message (receiver ambiguous)");
            }
            return;
        }

        var keywordMessage = messagePart.keywordMessage();
        if (keywordMessage != null)
        {
            if (_diagnosticsEnabled)
            {
                _diagnosticMessages.Add("[TailRecursion] Rejected: cascade message (receiver ambiguous)");
            }
            return;
        }
    }

    /// <summary>
    /// Analyzes a complete message expression.
    /// </summary>
    private void AnalyzeMessageExpression(Smalltalk80Parser.MessageExprContext messageExpr, bool isInTailPosition)
    {
        if (!isInTailPosition)
        {
            return;
        }

        if (_blockNestingDepth > 0)
        {
            if (_diagnosticsEnabled)
            {
                _diagnosticMessages.Add($"[TailRecursion] Rejected: inside block (depth={_blockNestingDepth})");
            }
            return;
        }

        // messageExpr: binaryExpr keywordMessage?
        var binaryExpr = messageExpr.binaryExpr();
        var keywordMessage = messageExpr.keywordMessage();

        if (binaryExpr == null)
        {
            return;
        }

        // Check if this is a keyword message send
        if (keywordMessage != null)
        {
            // Keyword message: extract receiver from binaryExpr
            AnalyzeKeywordMessageSend(binaryExpr, keywordMessage);
        }
        else
        {
            // Check if binaryExpr ends with a unary message
            AnalyzeBinaryExpression(binaryExpr);
        }
    }

    /// <summary>
    /// Analyzes a keyword message send (e.g., self whileTrue: aBlock).
    /// </summary>
    private void AnalyzeKeywordMessageSend(
        Smalltalk80Parser.BinaryExprContext receiver,
        Smalltalk80Parser.KeywordMessageContext keywordMessage)
    {
        // Check if receiver is 'self'
        if (!IsReceiverSelf(receiver))
        {
            if (_diagnosticsEnabled)
            {
                _diagnosticMessages.Add("[TailRecursion] Rejected: receiver is not 'self'");
            }
            return;
        }

        // Extract selector from keyword parts
        var keywordParts = keywordMessage.keywordPart();
        if (keywordParts == null || keywordParts.Length == 0)
        {
            return;
        }

        // Build selector (e.g., "whileTrue:" from keyword parts)
        var selectorParts = keywordParts
            .Select(part => part.IDENTIFIER()?.GetText())
            .Where(id => id != null)
            .Select(id => $"{id}:");

        var selector = string.Join("", selectorParts);

        // Check if selector matches current method
        if (selector != _currentMethodSelector)
        {
            if (_diagnosticsEnabled)
            {
                _diagnosticMessages.Add($"[TailRecursion] Rejected: selector mismatch (expected '{_currentMethodSelector}', got '{selector}')");
            }
            return;
        }

        // Extract argument nodes
        var argumentNodes = keywordParts
            .Select(part => part.binaryExpr())
            .Where(expr => expr != null)
            .Cast<IParseTree>()
            .ToList();

        // Found a tail recursive call!
        var tailCallInfo = new TailCallInfo
        {
            MessageSendNode = keywordMessage,
            Selector = selector,
            ArgumentCount = argumentNodes.Count,
            ArgumentNodes = argumentNodes,
            IsSafeToOptimize = true,
            UnsafeReason = null
        };

        _detectedTailCalls[keywordMessage] = tailCallInfo;

        if (_diagnosticsEnabled)
        {
            _diagnosticMessages.Add($"[TailRecursion] DETECTED: self {selector} (safe to optimize)");
        }
    }

    /// <summary>
    /// Analyzes a binary expression (may end with unary message).
    /// </summary>
    private void AnalyzeBinaryExpression(Smalltalk80Parser.BinaryExprContext binaryExpr)
    {
        // binaryExpr: unaryExpr (binaryMessage)*

        var unaryExpr = binaryExpr.unaryExpr();
        var binaryMessages = binaryExpr.binaryMessage();

        if (binaryMessages != null && binaryMessages.Length > 0)
        {
            // Has binary messages - binary operations are not recursive calls
            if (_diagnosticsEnabled)
            {
                _diagnosticMessages.Add("[TailRecursion] Rejected: binary message send");
            }
            return;
        }

        // Check unary expression for unary message sends
        if (unaryExpr != null)
        {
            AnalyzeUnaryExpression(unaryExpr);
        }
    }

    /// <summary>
    /// Analyzes a unary expression (e.g., self value).
    /// </summary>
    private void AnalyzeUnaryExpression(Smalltalk80Parser.UnaryExprContext unaryExpr)
    {
        // unaryExpr: messageBase unaryMessage*

        var messageBase = unaryExpr.messageBase();
        var unaryMessages = unaryExpr.unaryMessage();

        if (unaryMessages == null || unaryMessages.Length == 0)
        {
            // No unary messages - just a base expression
            return;
        }

        // Check if base is 'self'
        if (!IsMessageBaseSelf(messageBase))
        {
            if (_diagnosticsEnabled)
            {
                _diagnosticMessages.Add("[TailRecursion] Rejected: receiver is not 'self'");
            }
            return;
        }

        // CRITICAL: If there are multiple unary messages, only the FIRST is sent to self
        // Example: "self first second process"
        //   - 'first' is sent to self (returns result1)
        //   - 'second' is sent to result1 (returns result2)
        //   - 'process' is sent to result2 (NOT to self!)
        // Therefore, we can only detect tail recursion if there is EXACTLY ONE unary message
        if (unaryMessages.Length > 1)
        {
            if (_diagnosticsEnabled)
            {
                _diagnosticMessages.Add($"[TailRecursion] Rejected: chained unary messages (receiver of '{unaryMessages[^1].GetText()}' is not self)");
            }
            return;
        }

        // Get the single unary message
        var unaryMessage = unaryMessages[0];
        var selector = unaryMessage.unarySelector()?.GetText();

        if (selector == null)
        {
            return;
        }

        // Check if selector matches current method
        if (selector != _currentMethodSelector)
        {
            if (_diagnosticsEnabled)
            {
                _diagnosticMessages.Add($"[TailRecursion] Rejected: selector mismatch (expected '{_currentMethodSelector}', got '{selector}')");
            }
            return;
        }

        // Found a tail recursive call!
        var tailCallInfo = new TailCallInfo
        {
            MessageSendNode = unaryMessage,
            Selector = selector,
            ArgumentCount = 0,
            ArgumentNodes = [],
            IsSafeToOptimize = true,
            UnsafeReason = null
        };

        _detectedTailCalls[unaryMessage] = tailCallInfo;

        if (_diagnosticsEnabled)
        {
            _diagnosticMessages.Add($"[TailRecursion] DETECTED: self {selector} (safe to optimize)");
        }
    }

    /// <summary>
    /// Checks if a binaryExpr represents 'self' (no binary messages, base is self).
    /// </summary>
    private static bool IsReceiverSelf(Smalltalk80Parser.BinaryExprContext binaryExpr)
    {
        // Must have no binary messages
        var binaryMessages = binaryExpr.binaryMessage();
        if (binaryMessages != null && binaryMessages.Length > 0)
        {
            return false;
        }

        // Check if unaryExpr is self
        var unaryExpr = binaryExpr.unaryExpr();
        if (unaryExpr == null)
        {
            return false;
        }

        // Must have no unary messages
        var unaryMessages = unaryExpr.unaryMessage();
        if (unaryMessages != null && unaryMessages.Length > 0)
        {
            return false;
        }

        // Check if base is 'self'
        var messageBase = unaryExpr.messageBase();
        return IsMessageBaseSelf(messageBase);
    }

    /// <summary>
    /// Checks if a messageBase represents 'self'.
    /// </summary>
    private static bool IsMessageBaseSelf(Smalltalk80Parser.MessageBaseContext? messageBase)
    {
        if (messageBase == null)
        {
            return false;
        }

        // messageBase: literal | parenthesizedExpr | block | dynamicArray | variable | negativeNumber
        var variable = messageBase.variable();
        if (variable == null)
        {
            return false;
        }

        // Variable is just an IDENTIFIER - check if it's 'self'
        var identifier = variable.GetText();
        return identifier == "self";
    }
}
