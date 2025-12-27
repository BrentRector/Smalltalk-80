using System.Collections.Generic;
using Smalltalk80.Compiler.Analysis;
using Smalltalk80.Compiler.Generated;

namespace Smalltalk80.Compiler
{
    /// <summary>
    /// Message expression compilation methods for the BytecodeCompiler.
    /// Handles cascade, keyword, binary, and unary message compilation.
    /// </summary>
    public partial class BytecodeCompiler
    {
        /// <summary>
        /// Compiles a cascade expression.
        /// </summary>
        private InlineResult CompileCascadeExpr(Smalltalk80Parser.CascadeExprContext expr, bool forValue = true)
        {
            var messageExpr = expr.messageExpr();
            var messageParts = expr.messagePart();

            if (messageParts is not { Length: > 0 })
            {
                if (messageExpr != null)
                {
                    return CompileMessageExpr(messageExpr, forValue);
                }
                return InlineResult.NotInlined;
            }

            if (messageExpr != null && IsJustReceiver(messageExpr))
            {
                CompileMessageExpr(messageExpr, forValue: true);

                for (int i = 0; i < messageParts.Length; i++)
                {
                    bool isLast = (i == messageParts.Length - 1);

                    if (!isLast)
                    {
                        emitter.Dup();
                    }

                    CompileMessagePart(messageParts[i]);

                    if (!isLast)
                    {
                        emitter.Pop();
                    }
                }
                return InlineResult.NotInlined;
            }

            if (messageExpr == null) return InlineResult.NotInlined;

            CompileCascadeReceiver(messageExpr);
            emitter.Dup();
            CompileFinalMessage(messageExpr, forValue: true);

            for (int i = 0; i < messageParts.Length; i++)
            {
                bool isLast = (i == messageParts.Length - 1);
                emitter.Pop();

                if (!isLast)
                {
                    emitter.Dup();
                }

                CompileMessagePart(messageParts[i]);
            }

            return InlineResult.NotInlined;
        }

        /// <summary>
        /// Compiles the cascade receiver (all messages except the last one).
        /// </summary>
        private void CompileCascadeReceiver(Smalltalk80Parser.MessageExprContext messageExpr)
        {
            if (messageExpr == null) return;

            var binaryExpr = messageExpr.binaryExpr();
            var keywordMessage = messageExpr.keywordMessage();

            if (keywordMessage != null)
            {
                if (binaryExpr != null)
                {
                    var unaryExpr = binaryExpr.unaryExpr();
                    bool receiverIsSuper = IsReceiverSuper(unaryExpr?.messageBase());
                    CompileBinaryExpr(binaryExpr, receiverIsSuper);
                }
                return;
            }

            CompileBinaryExprExceptLast(binaryExpr);
        }

        /// <summary>
        /// Compiles a binary expression except its last message.
        /// </summary>
        private void CompileBinaryExprExceptLast(Smalltalk80Parser.BinaryExprContext? binaryExpr)
        {
            if (binaryExpr == null) return;

            var unaryExpr = binaryExpr.unaryExpr();
            var binaryMessages = binaryExpr.binaryMessage();

            if (binaryMessages is { Length: > 0 })
            {
                if (unaryExpr == null) return;
                bool receiverIsSuper = IsReceiverSuper(unaryExpr.messageBase());
                CompileUnaryExpr(unaryExpr, receiverIsSuper);

                var unaryMessages = unaryExpr.unaryMessage();
                bool hasUnaryMessages = unaryMessages is { Length: > 0 };
                bool binaryReceiverIsSuper = receiverIsSuper && !hasUnaryMessages;

                for (int i = 0; i < binaryMessages.Length - 1; i++)
                {
                    var msg = binaryMessages[i];
                    var selector = msg.binarySelector().GetText();
                    var argument = msg.unaryExpr();

                    if (i == 0 && binaryReceiverIsSuper)
                    {
                        emitter.AddLiteral(new Symbol(selector));
                    }

                    CompileUnaryExpr(argument, false);

                    if (i == 0 && binaryReceiverIsSuper)
                    {
                        emitter.EmitSuperSend(selector, 1);
                    }
                    else
                    {
                        emitter.EmitSend(selector, 1);
                    }
                }
                return;
            }

            CompileUnaryExprExceptLast(unaryExpr);
        }

        /// <summary>
        /// Compiles a unary expression except its last message.
        /// </summary>
        private void CompileUnaryExprExceptLast(Smalltalk80Parser.UnaryExprContext? unaryExpr)
        {
            if (unaryExpr == null) return;

            var messageBase = unaryExpr.messageBase();
            var unaryMessages = unaryExpr.unaryMessage();

            CompileMessageBase(messageBase);

            if (unaryMessages is { Length: > 1 })
            {
                bool receiverIsSuper = IsReceiverSuper(messageBase);

                for (int i = 0; i < unaryMessages.Length - 1; i++)
                {
                    var msg = unaryMessages[i];
                    var selector = msg.unarySelector().GetText();
                    bool isSuper = i == 0 && receiverIsSuper;

                    if (tailCalls != null && tailCalls.TryGetValue(msg, out var tailCallInfo))
                    {
                        if (tailCallInfo.IsSafeToOptimize && methodStartLabel >= 0)
                        {
                            emitter.EmitTailRecursiveJump(methodStartLabel, 0);
                            continue;
                        }
                    }

                    if (isSuper)
                    {
                        emitter.EmitSuperSend(selector, 0);
                    }
                    else
                    {
                        emitter.EmitSend(selector, 0);
                    }
                }
            }
        }

        /// <summary>
        /// Compiles the final message from a message expression.
        /// </summary>
        private InlineResult CompileFinalMessage(Smalltalk80Parser.MessageExprContext messageExpr, bool forValue = true)
        {
            if (messageExpr == null) return InlineResult.NotInlined;

            var binaryExpr = messageExpr.binaryExpr();
            var keywordMessage = messageExpr.keywordMessage();

            if (keywordMessage != null)
            {
                bool receiverIsSuper = DetermineIfKeywordReceiverIsSuper(binaryExpr);
                return CompileKeywordMessage(keywordMessage, receiverIsSuper, forValue);
            }

            CompileFinalBinaryMessage(binaryExpr);
            return InlineResult.NotInlined;
        }

        /// <summary>
        /// Determines if a keyword message's receiver is super.
        /// </summary>
        private bool DetermineIfKeywordReceiverIsSuper(Smalltalk80Parser.BinaryExprContext? binaryExpr)
        {
            if (binaryExpr == null) return false;

            var unaryExpr = binaryExpr.unaryExpr();
            if (unaryExpr == null) return false;

            var messageBase = unaryExpr.messageBase();
            var unaryMessages = unaryExpr.unaryMessage();
            var binaryMessages = binaryExpr.binaryMessage();

            bool hasNoUnaryMessages = unaryMessages is not { Length: > 0 };
            bool hasNoBinaryMessages = binaryMessages is not { Length: > 0 };

            return hasNoUnaryMessages && hasNoBinaryMessages && IsReceiverSuper(messageBase);
        }

        /// <summary>
        /// Compiles the final binary message.
        /// </summary>
        private void CompileFinalBinaryMessage(Smalltalk80Parser.BinaryExprContext? binaryExpr)
        {
            if (binaryExpr == null) return;

            var binaryMessages = binaryExpr.binaryMessage();
            if (binaryMessages is not { Length: > 0 })
            {
                CompileFinalUnaryMessage(binaryExpr.unaryExpr());
                return;
            }

            var lastBinaryMsg = binaryMessages[^1];
            var selector = lastBinaryMsg.binarySelector().GetText();
            var argument = lastBinaryMsg.unaryExpr();

            CompileUnaryExpr(argument, false);
            emitter.EmitSend(selector, 1);
        }

        /// <summary>
        /// Compiles the final unary message.
        /// </summary>
        private void CompileFinalUnaryMessage(Smalltalk80Parser.UnaryExprContext? unaryExpr)
        {
            if (unaryExpr == null) return;

            var unaryMessages = unaryExpr.unaryMessage();
            if (unaryMessages is not { Length: > 0 }) return;

            var lastUnaryMsg = unaryMessages[^1];
            var selector = lastUnaryMsg.unarySelector().GetText();

            if (tailCalls != null && tailCalls.TryGetValue(lastUnaryMsg, out var tailCallInfo))
            {
                if (tailCallInfo.IsSafeToOptimize && methodStartLabel >= 0)
                {
                    emitter.EmitTailRecursiveJump(methodStartLabel, 0);
                    return;
                }
            }

            emitter.EmitSend(selector, 0);
        }

        /// <summary>
        /// Checks if the message expression is just a receiver without any messages.
        /// </summary>
        private bool IsJustReceiver(Smalltalk80Parser.MessageExprContext expr)
        {
            if (expr == null) return false;

            var binaryExpr = expr.binaryExpr();
            if (binaryExpr == null) return true;

            var unaryExpr = binaryExpr.unaryExpr();
            if (unaryExpr == null) return true;

            bool hasUnaryMsgs = unaryExpr.unaryMessage() is { Length: > 0 };
            bool hasBinaryMsgs = binaryExpr.binaryMessage() is { Length: > 0 };
            bool hasKeywordMsg = expr.keywordMessage() != null;

            return !hasUnaryMsgs && !hasBinaryMsgs && !hasKeywordMsg;
        }

        /// <summary>
        /// Compiles a message expression.
        /// </summary>
        private InlineResult CompileMessageExpr(Smalltalk80Parser.MessageExprContext expr, bool forValue = true)
        {
            var binaryExpr = expr.binaryExpr();
            var keywordMessage = expr.keywordMessage();

            if (binaryExpr != null && keywordMessage != null)
            {
                if (TryInlineWhileLoop(binaryExpr, keywordMessage))
                {
                    return InlineResult.InlinedForValue;
                }
            }

            if (binaryExpr != null)
            {
                var unaryExpr = binaryExpr.unaryExpr();
                bool receiverIsSuper = IsReceiverSuper(unaryExpr?.messageBase());
                CompileBinaryExpr(binaryExpr, receiverIsSuper);
            }

            if (keywordMessage != null)
            {
                bool receiverIsSuper = DetermineIfKeywordReceiverIsSuper(binaryExpr);
                return CompileKeywordMessage(keywordMessage, receiverIsSuper, forValue);
            }

            return InlineResult.NotInlined;
        }

        /// <summary>
        /// Compiles a binary expression.
        /// </summary>
        private void CompileBinaryExpr(Smalltalk80Parser.BinaryExprContext expr, bool receiverIsSuper)
        {
            var unaryExpr = expr.unaryExpr();
            var binaryMessages = expr.binaryMessage();

            if (unaryExpr == null)
                return;

            CompileUnaryExpr(unaryExpr, receiverIsSuper);

            var unaryMessages = unaryExpr.unaryMessage();
            bool hasUnaryMessages = unaryMessages is { Length: > 0 };
            bool binaryReceiverIsSuper = receiverIsSuper && !hasUnaryMessages;

            if (binaryMessages != null)
            {
                for (int i = 0; i < binaryMessages.Length; i++)
                {
                    var msg = binaryMessages[i];
                    var selector = msg.binarySelector().GetText();

                    if (i == 0 && binaryReceiverIsSuper)
                    {
                        emitter.AddLiteral(new Symbol(selector));
                    }

                    var argExpr = msg.unaryExpr();
                    if (argExpr != null)
                    {
                        CompileUnaryExpr(argExpr, false);
                    }

                    if (i == 0 && binaryReceiverIsSuper)
                    {
                        emitter.EmitSuperSend(selector, 1);
                    }
                    else
                    {
                        emitter.EmitSend(selector, 1);
                    }
                }
            }
        }

        /// <summary>
        /// Compiles a unary expression.
        /// </summary>
        private void CompileUnaryExpr(Smalltalk80Parser.UnaryExprContext expr, bool receiverIsSuper)
        {
            var messageBase = expr.messageBase();
            var unaryMessages = expr.unaryMessage();

            if (unaryMessages is { Length: > 0 })
            {
                var firstSelector = unaryMessages[0].unarySelector().GetText();
                if ((firstSelector == "whileTrue" || firstSelector == "whileFalse") &&
                    messageBase?.block() != null)
                {
                    var block = messageBase.block();
                    if (IsZeroArgBlock(block))
                    {
                        CompileInlinedWhileLoopNoArg(block, firstSelector == "whileTrue");

                        for (int i = 1; i < unaryMessages.Length; i++)
                        {
                            var selector = unaryMessages[i].unarySelector().GetText();
                            emitter.EmitSend(selector, 0);
                        }
                        return;
                    }
                }
            }

            if (messageBase != null)
            {
                CompileMessageBase(messageBase);
            }

            if (unaryMessages != null)
            {
                for (int i = 0; i < unaryMessages.Length; i++)
                {
                    var msg = unaryMessages[i];
                    var selector = msg.unarySelector().GetText();

                    if (tailCalls != null && tailCalls.TryGetValue(msg, out var tailCallInfo))
                    {
                        if (tailCallInfo.IsSafeToOptimize && methodStartLabel >= 0)
                        {
                            emitter.EmitTailRecursiveJump(methodStartLabel, 0);
                            continue;
                        }
                    }

                    if (i == 0 && receiverIsSuper)
                    {
                        emitter.EmitSuperSend(selector, 0);
                    }
                    else
                    {
                        emitter.EmitSend(selector, 0);
                    }
                }
            }
        }

        /// <summary>
        /// Compiles a message part (for cascade handling).
        /// </summary>
        private void CompileMessagePart(Smalltalk80Parser.MessagePartContext part)
        {
            var unaryMessage = part.unaryMessage();
            if (unaryMessage != null)
            {
                var selector = unaryMessage.unarySelector().GetText();
                emitter.SendMessage(selector, 0);
                return;
            }

            var binaryMessage = part.binaryMessage();
            if (binaryMessage != null)
            {
                var selector = binaryMessage.binarySelector().GetText();
                CompileUnaryExpr(binaryMessage.unaryExpr(), false);
                emitter.SendMessage(selector, 1);
                return;
            }

            var keywordMessage = part.keywordMessage();
            if (keywordMessage != null)
            {
                CompileKeywordMessage(keywordMessage);
            }
        }

        /// <summary>
        /// Compiles a keyword message send.
        /// </summary>
        private InlineResult CompileKeywordMessage(Smalltalk80Parser.KeywordMessageContext msg, bool isSuper = false, bool forValue = true)
        {
            var keywordParts = msg.keywordPart();
            List<string> keywords = [];

            foreach (var part in keywordParts)
            {
                var keyword = part.IDENTIFIER().GetText() + ":";
                keywords.Add(keyword);
            }
            var selector = string.Join("", keywords);

            if (!isSuper)
            {
                var inlineResult = TryInlineConditional(selector, keywordParts, forValue);
                if (inlineResult != InlineResult.NotInlined)
                {
                    return inlineResult;
                }
            }

            bool isTailRecursive = false;
            TailCallInfo? tailCallInfo = null;
            if (tailCalls != null && tailCalls.TryGetValue(msg, out tailCallInfo))
            {
                if (tailCallInfo.IsSafeToOptimize && methodStartLabel >= 0)
                {
                    isTailRecursive = true;
                }
            }

            if (isSuper)
            {
                emitter.AddLiteral(new Symbol(selector));
            }

            foreach (var part in keywordParts)
            {
                var argExpr = part.binaryExpr();
                if (argExpr != null)
                {
                    CompileBinaryExpr(argExpr, false);
                }
            }

            if (isTailRecursive)
            {
                emitter.EmitTailRecursiveJump(methodStartLabel, keywordParts.Length);
            }
            else
            {
                if (isSuper)
                {
                    emitter.EmitSuperSend(selector, keywordParts.Length);
                }
                else
                {
                    emitter.EmitSend(selector, keywordParts.Length);
                }
            }

            return InlineResult.NotInlined;
        }

        /// <summary>
        /// Compiles a non-keyword expression (BinaryExpr).
        /// </summary>
        private void CompileNonKeywordExpr(Smalltalk80Parser.BinaryExprContext expr)
        {
            var unaryExpr = expr.unaryExpr();
            if (unaryExpr == null) return;

            var messageBase = unaryExpr.messageBase();
            bool receiverIsSuper = IsReceiverSuper(messageBase);

            CompileUnaryExpr(unaryExpr, receiverIsSuper);

            var binaryMessages = expr.binaryMessage();
            if (binaryMessages != null)
            {
                foreach (var msg in binaryMessages)
                {
                    var selector = msg.binarySelector().GetText();
                    CompileUnaryExpr(msg.unaryExpr(), false);
                    emitter.EmitSend(selector, 1);
                }
            }
        }
    }
}
