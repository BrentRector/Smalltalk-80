using System.Linq;
using Antlr4.Runtime.Tree;
using Smalltalk80.Compiler.Generated;

namespace Smalltalk80.Compiler.BytecodeGen
{
    /// <summary>
    /// Control flow inlining methods for the BytecodeCompiler.
    /// Handles optimization of ifTrue:, ifFalse:, ifTrue:ifFalse:, ifFalse:ifTrue:,
    /// and:, or:, whileTrue:, whileFalse:, and their no-argument variants.
    /// </summary>
    public partial class BytecodeCompiler
    {
        /// <summary>
        /// Attempts to inline a conditional control structure.
        ///
        /// Blue Book Chapter 26: "The control structures that are so optimized by the
        /// compiler are the conditional selection messages to Booleans (ifTrue:, ifFalse:,
        /// and ifTrue:ifFalse:), some of the logical operation messages to Booleans (and:
        /// and or:), and the conditional repetition messages to blocks (whileTrue: and
        /// whileFalse:)."
        ///
        /// This optimization replaces message sends with conditional jump bytecodes,
        /// avoiding BlockContext creation for simple control flow.
        /// </summary>
        /// <param name="selector">The message selector</param>
        /// <param name="keywordParts">The keyword parts containing block arguments</param>
        /// <param name="forValue">True if result is needed, false if for effect only</param>
        /// <returns>InlineResult indicating whether/how the message was inlined</returns>
        private InlineResult TryInlineConditional(string selector, Smalltalk80Parser.KeywordPartContext[] keywordParts, bool forValue) =>
            selector switch
            {
                "ifTrue:" => TryInlineIfTrue(keywordParts, forValue),
                "ifFalse:" => TryInlineIfFalse(keywordParts, forValue),
                "ifTrue:ifFalse:" => TryInlineIfTrueIfFalse(keywordParts, forValue),
                "ifFalse:ifTrue:" => TryInlineIfFalseIfTrue(keywordParts, forValue),
                "and:" => TryInlineAnd(keywordParts, forValue),
                "or:" => TryInlineOr(keywordParts, forValue),
                // Note: whileTrue:/whileFalse: are handled specially in CompileMessageExpr via TryInlineWhileLoop
                // because the receiver is a block (not a boolean). These cases return NotInlined here.
                "whileTrue:" or "whileFalse:" or "whileTrue" or "whileFalse" => InlineResult.NotInlined,
                _ => InlineResult.NotInlined
            };

        /// <summary>
        /// Checks if a block can be inlined (zero arguments, is a block literal).
        /// </summary>
        private bool CanInlineBlock(Smalltalk80Parser.KeywordPartContext keywordPart, out Smalltalk80Parser.BlockContext? block)
        {
            block = null;

            var expr = keywordPart.binaryExpr();
            if (expr == null) return false;

            var unaryExpr = expr.unaryExpr();
            if (unaryExpr == null) return false;
            if (expr.binaryMessage() is { Length: > 0 }) return false;

            var messageBase = unaryExpr.messageBase();
            if (messageBase == null) return false;
            if (unaryExpr.unaryMessage() is { Length: > 0 }) return false;

            block = messageBase.block();
            if (block == null) return false;

            var blockArgs = block.blockArgs();
            if (blockArgs != null)
            {
                var argNodes = blockArgs.children?
                    .OfType<Smalltalk80Parser.ArgumentNameContext>()
                    .ToList();
                if (argNodes is { Count: > 0 }) return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if a block's last statement is a return statement.
        /// Used to optimize conditional code generation - skip unreachable JMP after returns.
        /// </summary>
        private bool BlockEndsWithReturn(Smalltalk80Parser.BlockContext block)
        {
            var statements = block.statements();
            if (statements == null) return false;

            var stmtList = statements.statement();
            if (stmtList is not { Length: > 0 }) return false;

            return stmtList[^1].returnStatement() != null;
        }

        /// <summary>
        /// Compiles a block's statements inline (without BlockContext wrapper).
        /// </summary>
        private void CompileInlineBlockBody(Smalltalk80Parser.BlockContext block, bool forValue = true)
        {
            var blockTemps = block.blockTemps();
            var tempNames = blockTemps != null
                ? (blockTemps.children ?? [])
                    .OfType<Smalltalk80Parser.TemporaryNameContext>()
                    .Select(t => t.GetText())
                    .ToList()
                : [];

            if (tempNames.Count > 0)
            {
                allocator.EnterBlockScope([], tempNames, isNested: false);

                foreach (var tempName in tempNames)
                {
                    int slot = allocator.GetSlotIndex(tempName);
                    environment.Temporaries[tempName] = slot;
                }
            }

            var statements = block.statements();
            if (statements != null)
            {
                var stmtList = statements.statement();
                if (stmtList != null)
                {
                    for (int i = 0; i < stmtList.Length; i++)
                    {
                        var stmt = stmtList[i];
                        bool isLast = (i == stmtList.Length - 1);
                        bool leaveOnStack = isLast && forValue;
                        CompileStatement(stmt, isLast: leaveOnStack, inBlock: true);
                    }
                }
                else if (forValue)
                {
                    emitter.PushNil();
                }
            }
            else if (forValue)
            {
                emitter.PushNil();
            }

            if (tempNames.Count > 0)
            {
                allocator.ExitScope();
            }
        }

        /// <summary>
        /// Calculates the bytecode size of a block body without emitting.
        /// </summary>
        private int CalculateBlockBodySize(Smalltalk80Parser.BlockContext block, bool forValue = true)
        {
            emitter.EnterSizingMode();
            try
            {
                CompileInlineBlockBody(block, forValue);
                return emitter.EndSizingMode();
            }
            catch
            {
                if (emitter.IsSizingMode)
                    emitter.EndSizingMode();
                return 100; // Force long jumps as safe fallback
            }
        }

        /// <summary>
        /// Inlines: condition ifTrue: [trueBlock]
        /// </summary>
        private InlineResult TryInlineIfTrue(Smalltalk80Parser.KeywordPartContext[] keywordParts, bool forValue)
        {
            if (keywordParts.Length != 1) return InlineResult.NotInlined;
            if (!CanInlineBlock(keywordParts[0], out var trueBlock)) return InlineResult.NotInlined;

            bool blockReturns = BlockEndsWithReturn(trueBlock!);

            if (forValue)
            {
                int trueBlockSize = CalculateBlockBodySize(trueBlock!, forValue: true);
                int skipNilJumpSize = blockReturns ? 0 : BytecodeEmitter.SizeForwardJump(1);
                int bfpOffset = trueBlockSize + skipNilJumpSize;

                emitter.EmitJumpOnFalse(bfpOffset);
                CompileInlineBlockBody(trueBlock!, forValue: true);

                if (!blockReturns)
                {
                    emitter.EmitForwardJump(1);
                }

                emitter.PushNil();
                return InlineResult.InlinedForValue;
            }
            else
            {
                int trueBlockSize = CalculateBlockBodySize(trueBlock!, forValue: false);
                emitter.EmitJumpOnFalse(trueBlockSize);
                CompileInlineBlockBody(trueBlock!, forValue: false);
                return InlineResult.InlinedForEffect;
            }
        }

        /// <summary>
        /// Inlines: condition ifFalse: [falseBlock]
        /// </summary>
        private InlineResult TryInlineIfFalse(Smalltalk80Parser.KeywordPartContext[] keywordParts, bool forValue)
        {
            if (keywordParts.Length != 1) return InlineResult.NotInlined;
            if (!CanInlineBlock(keywordParts[0], out var falseBlock)) return InlineResult.NotInlined;

            bool blockReturns = BlockEndsWithReturn(falseBlock!);

            if (forValue)
            {
                int falseBlockSize = CalculateBlockBodySize(falseBlock!, forValue: true);
                int skipNilJumpSize = blockReturns ? 0 : BytecodeEmitter.SizeForwardJump(1);
                int btpOffset = falseBlockSize + skipNilJumpSize;

                emitter.EmitJumpOnTrue(btpOffset);
                CompileInlineBlockBody(falseBlock!, forValue: true);

                if (!blockReturns)
                {
                    emitter.EmitForwardJump(1);
                }

                emitter.PushNil();
                return InlineResult.InlinedForValue;
            }
            else
            {
                int falseBlockSize = CalculateBlockBodySize(falseBlock!, forValue: false);
                emitter.EmitJumpOnTrue(falseBlockSize);
                CompileInlineBlockBody(falseBlock!, forValue: false);
                return InlineResult.InlinedForEffect;
            }
        }

        /// <summary>
        /// Inlines: condition ifTrue: [trueBlock] ifFalse: [falseBlock]
        /// </summary>
        private InlineResult TryInlineIfTrueIfFalse(Smalltalk80Parser.KeywordPartContext[] keywordParts, bool forValue)
        {
            if (keywordParts.Length != 2) return InlineResult.NotInlined;
            if (!CanInlineBlock(keywordParts[0], out var trueBlock)) return InlineResult.NotInlined;
            if (!CanInlineBlock(keywordParts[1], out var falseBlock)) return InlineResult.NotInlined;

            bool trueBlockReturns = BlockEndsWithReturn(trueBlock!);

            int trueBlockSize = CalculateBlockBodySize(trueBlock!);
            int falseBlockSize = CalculateBlockBodySize(falseBlock!);

            int skipFalseJumpSize = trueBlockReturns ? 0 : BytecodeEmitter.SizeForwardJump(falseBlockSize);
            int bfpOffset = trueBlockSize + skipFalseJumpSize;

            emitter.EmitJumpOnFalse(bfpOffset);
            CompileInlineBlockBody(trueBlock!);

            if (!trueBlockReturns)
            {
                emitter.EmitForwardJump(falseBlockSize);
            }

            CompileInlineBlockBody(falseBlock!);

            return InlineResult.InlinedForValue;
        }

        /// <summary>
        /// Inlines: condition ifFalse: [falseBlock] ifTrue: [trueBlock]
        /// </summary>
        private InlineResult TryInlineIfFalseIfTrue(Smalltalk80Parser.KeywordPartContext[] keywordParts, bool forValue)
        {
            if (keywordParts.Length != 2) return InlineResult.NotInlined;
            if (!CanInlineBlock(keywordParts[0], out var falseBlock)) return InlineResult.NotInlined;
            if (!CanInlineBlock(keywordParts[1], out var trueBlock)) return InlineResult.NotInlined;

            bool falseBlockReturns = BlockEndsWithReturn(falseBlock!);

            int falseBlockSize = CalculateBlockBodySize(falseBlock!);
            int trueBlockSize = CalculateBlockBodySize(trueBlock!);

            int skipTrueJumpSize = falseBlockReturns ? 0 : BytecodeEmitter.SizeForwardJump(trueBlockSize);
            int btpOffset = falseBlockSize + skipTrueJumpSize;

            emitter.EmitJumpOnTrue(btpOffset);
            CompileInlineBlockBody(falseBlock!);

            if (!falseBlockReturns)
            {
                emitter.EmitForwardJump(trueBlockSize);
            }

            CompileInlineBlockBody(trueBlock!);

            return InlineResult.InlinedForValue;
        }

        /// <summary>
        /// Inlines: condition and: [block]
        /// Short-circuit: if condition is false, return false without evaluating block.
        /// </summary>
        private InlineResult TryInlineAnd(Smalltalk80Parser.KeywordPartContext[] keywordParts, bool forValue)
        {
            if (keywordParts.Length != 1) return InlineResult.NotInlined;
            if (!CanInlineBlock(keywordParts[0], out var block)) return InlineResult.NotInlined;

            int blockSize = CalculateBlockBodySize(block!);
            int skipFalseJumpSize = BytecodeEmitter.SizeForwardJump(1);
            int bfpOffset = blockSize + skipFalseJumpSize;

            emitter.EmitJumpOnFalse(bfpOffset);
            CompileInlineBlockBody(block!);
            emitter.EmitForwardJump(1);
            emitter.PushFalse();

            return InlineResult.InlinedForValue;
        }

        /// <summary>
        /// Inlines: condition or: [block]
        /// Short-circuit: if condition is true, return true without evaluating block.
        /// </summary>
        private InlineResult TryInlineOr(Smalltalk80Parser.KeywordPartContext[] keywordParts, bool forValue)
        {
            if (keywordParts.Length != 1) return InlineResult.NotInlined;
            if (!CanInlineBlock(keywordParts[0], out var block)) return InlineResult.NotInlined;

            int blockSize = CalculateBlockBodySize(block!);
            int skipTrueJumpSize = BytecodeEmitter.SizeForwardJump(1);
            int btpOffset = blockSize + skipTrueJumpSize;

            emitter.EmitJumpOnTrue(btpOffset);
            CompileInlineBlockBody(block!);
            emitter.EmitForwardJump(1);
            emitter.PushTrue();

            return InlineResult.InlinedForValue;
        }

        /// <summary>
        /// Attempts to inline whileTrue:/whileFalse: loops.
        /// </summary>
        private bool TryInlineWhileLoop(
            Smalltalk80Parser.BinaryExprContext binaryExpr,
            Smalltalk80Parser.KeywordMessageContext keywordMessage)
        {
            var keywordParts = keywordMessage.keywordPart();
            string selector = string.Join("", keywordParts.Select(p => p.IDENTIFIER().GetText() + ":"));

            if (keywordParts.Length == 0)
            {
                return false;
            }

            bool isWhileTrue = selector == "whileTrue:";
            bool isWhileFalse = selector == "whileFalse:";
            if (!isWhileTrue && !isWhileFalse)
            {
                return false;
            }

            if (!IsBlockLiteralReceiver(binaryExpr, out var conditionBlock))
            {
                return false;
            }

            if (!IsZeroArgBlock(conditionBlock!))
            {
                return false;
            }

            if (keywordParts.Length != 1)
            {
                return false;
            }
            if (!CanInlineBlock(keywordParts[0], out var bodyBlock))
            {
                return false;
            }

            int conditionSize = CalculateBlockBodySize(conditionBlock!);
            int bodySize = CalculateBlockBodySize(bodyBlock!);
            int exitJumpTarget = bodySize + 1 + 2; // body + pop + backwardJmp

            int loopStart = emitter.CurrentOffset;
            CompileInlineBlockBody(conditionBlock!);

            if (isWhileTrue)
            {
                emitter.EmitJumpOnFalse(exitJumpTarget);
            }
            else
            {
                emitter.EmitJumpOnTrue(exitJumpTarget);
            }

            CompileInlineBlockBody(bodyBlock!);
            emitter.Pop();
            emitter.EmitJumpBackward(loopStart);
            emitter.PushNil();

            return true;
        }

        /// <summary>
        /// Compiles an inlined while loop without a body argument.
        /// </summary>
        private void CompileInlinedWhileLoopNoArg(Smalltalk80Parser.BlockContext conditionBlock, bool isWhileTrue)
        {
            int exitJumpTarget = 2;
            int loopStart = emitter.CurrentOffset;

            CompileInlineBlockBody(conditionBlock);

            if (isWhileTrue)
            {
                emitter.EmitJumpOnFalse(exitJumpTarget);
            }
            else
            {
                emitter.EmitJumpOnTrue(exitJumpTarget);
            }

            emitter.EmitJumpBackward(loopStart);
            emitter.PushNil();
        }

        /// <summary>
        /// Checks if a binaryExpr is a simple block literal with no messages.
        /// </summary>
        private bool IsBlockLiteralReceiver(
            Smalltalk80Parser.BinaryExprContext binaryExpr,
            out Smalltalk80Parser.BlockContext? block)
        {
            block = null;

            if (binaryExpr.binaryMessage() is { Length: > 0 })
                return false;

            var unaryExpr = binaryExpr.unaryExpr();
            if (unaryExpr == null) return false;

            if (unaryExpr.unaryMessage() is { Length: > 0 })
                return false;

            var messageBase = unaryExpr.messageBase();
            if (messageBase == null) return false;

            block = messageBase.block();
            return block != null;
        }

        /// <summary>
        /// Checks if a block has zero arguments (required for inlining).
        /// </summary>
        private bool IsZeroArgBlock(Smalltalk80Parser.BlockContext block)
        {
            var blockArgs = block.blockArgs();
            if (blockArgs == null) return true;

            var argNodes = blockArgs.children?
                .OfType<Smalltalk80Parser.ArgumentNameContext>()
                .ToList();
            return argNodes is not { Count: > 0 };
        }

    }
}
