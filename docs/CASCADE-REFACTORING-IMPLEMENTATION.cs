/// <summary>
/// CASCADE COMPILATION - REFACTORED IMPLEMENTATION
/// This file demonstrates the clean architecture using modern C# 13 patterns.
///
/// Key Principles:
/// 1. No flag passing through nested methods
/// 2. Clear separation of concerns (receiver vs final message)
/// 3. Single responsibility per method
/// 4. Leverage C# 13 features for readability
/// </summary>

using Smalltalk80Parser = Smalltalk80.Compiler.Generated.Smalltalk80Parser;

namespace Smalltalk80.Compiler;

public partial class BytecodeCompiler
{
    // =========================================================================
    // PUBLIC COMPILATION METHODS (unchanged interface)
    // =========================================================================

    /// <summary>
    /// Compiles a cascade expression.
    /// </summary>
    /// <param name="expr">The cascade expression to compile.</param>
    /// <remarks>
    /// Cascade syntax: receiver messages; cascadeMsg1; cascadeMsg2
    /// Example: stream nextPut: $a; nextPut: $b; cr
    ///
    /// Bytecode structure:
    /// 1. Compile cascade receiver (all but last message)
    /// 2. DUP (duplicate receiver on stack)
    /// 3. Compile final message from base expression
    /// 4. For each cascade part:
    ///    - POP (remove previous result)
    ///    - DUP (duplicate receiver, except for last)
    ///    - Send message
    /// </remarks>
    private void CompileCascadeExpr(Smalltalk80Parser.CascadeExprContext expr)
    {
        var messageExpr = expr.messageExpr();
        var messageParts = expr.messagePart();

        // No cascade - compile normally
        if (messageParts is null || messageParts.Length == 0)
        {
            CompileMessageExpr(messageExpr);
            return;
        }

        // STEP 1: Compile the cascade receiver (all messages except the last one)
        // For "obj foo bar; baz" → compiles "obj foo"
        CompileCascadeReceiver(messageExpr);

        // STEP 2: Duplicate the cascade receiver
        emitter.Dup();

        // STEP 3: Compile the final message from the base expression
        // For "obj foo bar; baz" → compiles "bar" message
        CompileFinalMessage(messageExpr);

        // STEP 4: Process cascade parts (messages after semicolons)
        for (int i = 0; i < messageParts.Length; i++)
        {
            bool isLast = i == messageParts.Length - 1;

            // Pop the previous message result
            emitter.Pop();

            // Duplicate receiver for next message (except for the last)
            if (!isLast)
            {
                emitter.Dup();
            }

            // Compile the cascade message
            CompileMessagePart(messageParts[i]);
        }
    }

    // =========================================================================
    // CASCADE RECEIVER COMPILATION (everything except final message)
    // =========================================================================

    /// <summary>
    /// Compiles the cascade receiver (all messages except the last one).
    /// </summary>
    /// <param name="messageExpr">The message expression to decompose.</param>
    /// <remarks>
    /// For "obj foo bar; baz" → compiles "obj foo"
    /// For "obj at: 1 put: x; at: 2" → compiles "obj"
    /// For "obj + 1 - 2; * 3" → compiles "obj + 1"
    /// </remarks>
    private void CompileCascadeReceiver(Smalltalk80Parser.MessageExprContext messageExpr)
    {
        var binaryExpr = messageExpr.binaryExpr();
        var keywordMessage = messageExpr.keywordMessage();

        // Case 1: Keyword message present
        // For "obj foo at: 1; at: 2" → receiver is "obj foo" (full binaryExpr)
        if (keywordMessage is not null)
        {
            CompileBinaryExpr(binaryExpr, receiverIsSuper: false);
            return;
        }

        // Case 2: No keyword message
        // For "obj foo bar; baz" → receiver is "obj foo" (all but last message)
        CompileBinaryExprExceptLast(binaryExpr);
    }

    /// <summary>
    /// Compiles a binary expression except its last message.
    /// </summary>
    /// <param name="binaryExpr">The binary expression to compile.</param>
    /// <remarks>
    /// Grammar: binaryExpr = unaryExpr (binaryMessage)*
    ///
    /// For "obj foo bar + 1 - 2" → compiles "obj foo bar + 1"
    /// For "obj foo bar" → compiles "obj foo"
    /// For "obj" → compiles nothing (just the base)
    /// </remarks>
    private void CompileBinaryExprExceptLast(Smalltalk80Parser.BinaryExprContext binaryExpr)
    {
        var unaryExpr = binaryExpr.unaryExpr();
        var binaryMessages = binaryExpr.binaryMessage();

        // Case 1: Has binary messages → compile unaryExpr + all but last binary
        if (binaryMessages is not null && binaryMessages.Length > 0)
        {
            CompileUnaryExpr(unaryExpr, receiverIsSuper: false);

            // Send all but the last binary message
            for (int i = 0; i < binaryMessages.Length - 1; i++)
            {
                var msg = binaryMessages[i];
                var selector = msg.binarySelector().GetText();
                var argument = msg.unaryExpr();

                CompileUnaryExpr(argument, receiverIsSuper: false);
                emitter.EmitMessageSend(selector, argumentCount: 1, isSuper: false);
            }
            return;
        }

        // Case 2: No binary messages → compile unaryExpr except last
        CompileUnaryExprExceptLast(unaryExpr);
    }

    /// <summary>
    /// Compiles a unary expression except its last message.
    /// </summary>
    /// <param name="unaryExpr">The unary expression to compile.</param>
    /// <remarks>
    /// Grammar: unaryExpr = messageBase unaryMessage*
    ///
    /// For "obj foo bar baz" → compiles "obj foo bar"
    /// For "obj foo" → compiles "obj"
    /// For "obj" → compiles "obj" (just the base)
    /// </remarks>
    private void CompileUnaryExprExceptLast(Smalltalk80Parser.UnaryExprContext unaryExpr)
    {
        var messageBase = unaryExpr.messageBase();
        var unaryMessages = unaryExpr.unaryMessage();

        // Always compile the message base (receiver)
        CompileMessageBase(messageBase);

        // Send all but the last unary message
        if (unaryMessages is not null && unaryMessages.Length > 1)
        {
            bool receiverIsSuper = IsReceiverSuper(messageBase);

            for (int i = 0; i < unaryMessages.Length - 1; i++)
            {
                var selector = unaryMessages[i].unarySelector().GetText();
                bool isSuper = i == 0 && receiverIsSuper;

                emitter.EmitMessageSend(selector, argumentCount: 0, isSuper);
            }
        }
    }

    // =========================================================================
    // FINAL MESSAGE COMPILATION (only the last message)
    // =========================================================================

    /// <summary>
    /// Compiles the final message from a message expression.
    /// </summary>
    /// <param name="messageExpr">The message expression.</param>
    /// <remarks>
    /// For "obj foo bar; baz" → compiles "bar" message
    /// For "obj at: 1 put: x; at: 2" → compiles "at: 1 put: x" message
    /// For "obj + 1 - 2; * 3" → compiles "- 2" message
    /// </remarks>
    private void CompileFinalMessage(Smalltalk80Parser.MessageExprContext messageExpr)
    {
        var binaryExpr = messageExpr.binaryExpr();
        var keywordMessage = messageExpr.keywordMessage();

        // Case 1: Keyword message present → that's the final message
        if (keywordMessage is not null)
        {
            bool receiverIsSuper = DetermineIfSuper(binaryExpr);
            CompileKeywordMessage(keywordMessage, receiverIsSuper);
            return;
        }

        // Case 2: No keyword → final message is from binaryExpr
        CompileFinalBinaryMessage(binaryExpr);
    }

    /// <summary>
    /// Compiles the final message from a binary expression.
    /// </summary>
    /// <param name="binaryExpr">The binary expression.</param>
    /// <remarks>
    /// For "obj + 1 - 2" → compiles "- 2" message
    /// For "obj foo bar" → compiles "bar" message
    /// For "obj" → does nothing (no final message)
    /// </remarks>
    private void CompileFinalBinaryMessage(Smalltalk80Parser.BinaryExprContext binaryExpr)
    {
        var binaryMessages = binaryExpr.binaryMessage();

        // Case 1: Has binary messages → last binary is the final message
        if (binaryMessages is not null && binaryMessages.Length > 0)
        {
            var lastMsg = binaryMessages[^1];  // C# 13: index from end
            var selector = lastMsg.binarySelector().GetText();
            var argument = lastMsg.unaryExpr();

            CompileUnaryExpr(argument, receiverIsSuper: false);
            emitter.EmitMessageSend(selector, argumentCount: 1, isSuper: false);
            return;
        }

        // Case 2: No binary messages → final message is from unaryExpr
        CompileFinalUnaryMessage(binaryExpr.unaryExpr());
    }

    /// <summary>
    /// Compiles the final message from a unary expression.
    /// </summary>
    /// <param name="unaryExpr">The unary expression.</param>
    /// <remarks>
    /// For "obj foo bar" → compiles "bar" message
    /// For "obj foo" → compiles "foo" message
    /// For "obj" → does nothing (no final message)
    /// </remarks>
    private void CompileFinalUnaryMessage(Smalltalk80Parser.UnaryExprContext unaryExpr)
    {
        var messageBase = unaryExpr.messageBase();
        var unaryMessages = unaryExpr.unaryMessage();

        if (unaryMessages is not null && unaryMessages.Length > 0)
        {
            var lastMsg = unaryMessages[^1];  // C# 13: index from end
            var selector = lastMsg.unarySelector().GetText();

            // Super send only if: single message AND receiver is super
            bool isSuper = unaryMessages.Length == 1 && IsReceiverSuper(messageBase);

            emitter.EmitMessageSend(selector, argumentCount: 0, isSuper);
        }
    }

    // =========================================================================
    // HELPER METHODS
    // =========================================================================

    /// <summary>
    /// Determines if a binary expression has super as receiver with no intermediate messages.
    /// </summary>
    /// <param name="binaryExpr">The binary expression to check.</param>
    /// <returns>True if receiver is super with no unary or binary messages.</returns>
    private bool DetermineIfSuper(Smalltalk80Parser.BinaryExprContext binaryExpr)
    {
        var unaryExpr = binaryExpr.unaryExpr();
        if (unaryExpr is null) return false;

        var messageBase = unaryExpr.messageBase();
        var unaryMessages = unaryExpr.unaryMessage();
        var binaryMessages = binaryExpr.binaryMessage();

        // Only super send if there are no intervening messages
        bool hasNoUnaryMessages = unaryMessages is null || unaryMessages.Length == 0;
        bool hasNoBinaryMessages = binaryMessages is null || binaryMessages.Length == 0;

        return IsReceiverSuper(messageBase) && hasNoUnaryMessages && hasNoBinaryMessages;
    }

    /// <summary>
    /// Checks if a message base is the 'super' pseudo-variable.
    /// </summary>
    /// <param name="messageBase">The message base to check.</param>
    /// <returns>True if the message base is 'super'.</returns>
    private bool IsReceiverSuper(Smalltalk80Parser.MessageBaseContext messageBase) =>
        messageBase?.variable()?.GetText() == "super";

    // =========================================================================
    // EXISTING METHODS (unchanged, no cascade logic)
    // =========================================================================

    /// <summary>
    /// Compiles a message expression (no cascade logic).
    /// </summary>
    /// <param name="expr">The message expression to compile.</param>
    /// <remarks>
    /// This method is now free of cascade logic - it only handles normal message sends.
    /// Cascade compilation is entirely handled by CompileCascadeExpr and its helpers.
    /// </remarks>
    private void CompileMessageExpr(Smalltalk80Parser.MessageExprContext expr)
    {
        var binaryExpr = expr.binaryExpr();
        var keywordMessage = expr.keywordMessage();

        if (binaryExpr is not null)
        {
            var unaryExpr = binaryExpr.unaryExpr();
            bool receiverIsSuper = false;

            if (unaryExpr is not null)
            {
                var messageBase = unaryExpr.messageBase();
                receiverIsSuper = IsReceiverSuper(messageBase);
            }

            CompileBinaryExpr(binaryExpr, receiverIsSuper);
        }

        if (keywordMessage is not null)
        {
            bool receiverIsSuper = DetermineIfSuper(expr.binaryExpr());
            CompileKeywordMessage(keywordMessage, receiverIsSuper);
        }
    }

    /// <summary>
    /// Compiles a binary expression (no cascade logic).
    /// </summary>
    /// <param name="expr">The binary expression to compile.</param>
    /// <param name="receiverIsSuper">Whether the receiver is 'super'.</param>
    private void CompileBinaryExpr(Smalltalk80Parser.BinaryExprContext expr, bool receiverIsSuper)
    {
        var unaryExpr = expr.unaryExpr();
        var binaryMessages = expr.binaryMessage();

        if (unaryExpr is null) return;

        // Check if unary messages consume the super flag
        var unaryMessages = unaryExpr.unaryMessage();
        bool hasUnaryMessages = unaryMessages is not null && unaryMessages.Length > 0;
        bool binaryReceiverIsSuper = receiverIsSuper && !hasUnaryMessages;

        // Compile unary expression
        CompileUnaryExpr(unaryExpr, receiverIsSuper);

        // Compile binary messages
        if (binaryMessages is not null)
        {
            for (int i = 0; i < binaryMessages.Length; i++)
            {
                var msg = binaryMessages[i];
                var selector = msg.binarySelector().GetText();
                var argument = msg.unaryExpr();

                CompileUnaryExpr(argument, receiverIsSuper: false);

                bool isSuper = i == 0 && binaryReceiverIsSuper;
                emitter.EmitMessageSend(selector, argumentCount: 1, isSuper);
            }
        }
    }

    /// <summary>
    /// Compiles a unary expression (no cascade logic).
    /// </summary>
    /// <param name="expr">The unary expression to compile.</param>
    /// <param name="receiverIsSuper">Whether the receiver is 'super'.</param>
    private void CompileUnaryExpr(Smalltalk80Parser.UnaryExprContext expr, bool receiverIsSuper)
    {
        var messageBase = expr.messageBase();
        var unaryMessages = expr.unaryMessage();

        // Compile message base
        CompileMessageBase(messageBase);

        // Compile unary messages
        if (unaryMessages is not null)
        {
            for (int i = 0; i < unaryMessages.Length; i++)
            {
                var selector = unaryMessages[i].unarySelector().GetText();
                bool isSuper = i == 0 && receiverIsSuper;

                emitter.EmitMessageSend(selector, argumentCount: 0, isSuper);
            }
        }
    }

    // Additional methods (CompileKeywordMessage, CompileMessagePart, etc.) remain unchanged
}

// =========================================================================
// DESIGN NOTES
// =========================================================================

/*
 * KEY ARCHITECTURAL DECISIONS:
 *
 * 1. SEPARATION OF CONCERNS
 *    - CompileCascadeExpr: Orchestrates cascade compilation
 *    - CompileCascadeReceiver: Handles receiver compilation
 *    - CompileFinalMessage: Handles final message compilation
 *    - CompileMessageExpr: Normal message compilation (no cascade logic)
 *
 * 2. NO FLAG PASSING
 *    - Removed isCascadeBase and totalCascadeMessages parameters
 *    - Each method has a clear, single purpose
 *    - Easier to test and maintain
 *
 * 3. HELPER METHOD NAMING
 *    - "ExceptLast" methods: Compile everything except final message
 *    - "Final" methods: Compile only the final message
 *    - Clear semantic meaning
 *
 * 4. C# 13 FEATURES USED
 *    - Index from end operator (^1)
 *    - Pattern matching (is not null)
 *    - Expression-bodied members
 *    - Comprehensive XML documentation
 *
 * 5. SUPER SEND HANDLING
 *    - Isolated in dedicated helper methods
 *    - DetermineIfSuper checks for super with no intermediate messages
 *    - IsReceiverSuper checks if messageBase is 'super'
 *
 * 6. EXTENSIBILITY
 *    - Easy to add new message types
 *    - Easy to modify cascade semantics
 *    - Clear entry points for debugging
 */
