using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime.Tree;
using Smalltalk80.Compiler.Generated;

namespace Smalltalk80.Compiler
{
    /// <summary>
    /// Block and dynamic array compilation methods for the BytecodeCompiler.
    /// </summary>
    public partial class BytecodeCompiler
    {
        /// <summary>
        /// Compiles a message base (receiver of a message).
        /// </summary>
        private void CompileMessageBase(Smalltalk80Parser.MessageBaseContext? baseCtx)
        {
            if (baseCtx == null) return;

            if (baseCtx.literal() != null)
            {
                CompileLiteral(baseCtx.literal());
            }
            else if (baseCtx.variable() != null)
            {
                var varName = baseCtx.variable().GetText();
                PushVariable(varName);
            }
            else if (baseCtx.negativeNumber() != null)
            {
                var negNum = baseCtx.negativeNumber();
                var innerBase = negNum.messageBase();

                if (innerBase.literal() != null && innerBase.literal().numberLiteral() != null)
                {
                    var numText = innerBase.literal().numberLiteral().GetText();

                    if (TryParseSmalltalkNumber(numText, out object? positiveValue))
                    {
                        if (positiveValue is int positiveIntVal)
                        {
                            int negativeVal = -positiveIntVal;

                            if (negativeVal == -1)
                            {
                                emitter.PushMinusOne();
                            }
                            else
                            {
                                emitter.PushLiteralConstant(negativeVal);
                            }
                        }
                        else if (positiveValue is double positiveFloatVal)
                        {
                            emitter.PushLiteralConstant(-positiveFloatVal);
                        }
                    }
                }
                else
                {
                    CompileMessageBase(innerBase);
                    emitter.EmitSend("negated", 0);
                }
            }
            else if (baseCtx.parenthesizedExpr() != null)
            {
                CompileExpression(baseCtx.parenthesizedExpr().expression());
            }
            else if (baseCtx.block() != null)
            {
                CompileBlock(baseCtx.block());
            }
            else if (baseCtx.dynamicArray() != null)
            {
                CompileDynamicArray(baseCtx.dynamicArray());
            }
        }

        /// <summary>
        /// Compiles a block to bytecode.
        /// Blue Book: Blocks compile bytecode INLINE in the method.
        /// </summary>
        private void CompileBlock(Smalltalk80Parser.BlockContext block)
        {
            var blockArgs = block.blockArgs();
            int argCount = 0;
            var argumentNames = new List<string>();

            if (blockArgs != null)
            {
                var argNodes = blockArgs.children
                    .OfType<Smalltalk80Parser.ArgumentNameContext>()
                    .Select(ctx => ctx.IDENTIFIER())
                    .ToList();

                argCount = argNodes.Count;
                for (int i = 0; i < argNodes.Count; i++)
                {
                    argumentNames.Add(argNodes[i].GetText());
                }
            }

            var blockTemps = block.blockTemps();
            int tempCount = 0;
            if (blockTemps != null)
            {
                var tempNodes = (blockTemps.children ?? [])
                    .OfType<Smalltalk80Parser.TemporaryNameContext>()
                    .ToList();
                tempCount = tempNodes.Count;
            }

            // Save current environment state
            var savedArguments = new Dictionary<string, int>(environment.Arguments);
            int savedArgCount = environment.ArgumentCount;
            int savedScopeDepth = environment.ScopeDepth;
            var savedTemporaries = new Dictionary<string, int>(environment.Temporaries);
            var savedOwnArguments = new Dictionary<string, int>(environment.LocalArgumentIndices);
            var savedOwnTemporaries = new Dictionary<string, int>(environment.LocalTemporaryIndices);
            int savedCumulativeBlockArgOffset = environment.CumulativeBlockArgOffset;
            int savedTemporariesAllocatedInFrame = environment.TemporariesAllocatedInFrame;
            var savedParent = environment.Parent;

            if (savedScopeDepth == 0)
            {
                environment.MethodArgumentCount = savedArgCount;
                environment.MethodTemporaryCount = environment.TemporariesAllocatedInFrame;
            }

            // Create parent environment snapshot
            var parentSnapshot = new CompilationEnvironment
            {
                Type = (savedScopeDepth == 0) ? ScopeType.Method : ScopeType.Block,
                ArgumentCount = savedArgCount,
                TemporariesAllocatedInFrame = (savedScopeDepth == 0) ? 0 : savedTemporariesAllocatedInFrame,
                ScopeDepth = savedScopeDepth,
                Parent = savedParent,
                MethodArgumentCount = environment.MethodArgumentCount,
                MethodTemporaryCount = environment.MethodTemporaryCount,
                CumulativeBlockArgOffset = savedCumulativeBlockArgOffset,
                KnownClassNames = environment.KnownClassNames,
                KnownPoolDictionaryNames = environment.KnownPoolDictionaryNames
            };

            foreach (var kvp in savedArguments)
                parentSnapshot.Arguments[kvp.Key] = kvp.Value;
            foreach (var kvp in savedTemporaries)
                parentSnapshot.Temporaries[kvp.Key] = kvp.Value;
            foreach (var kvp in savedOwnArguments)
                parentSnapshot.LocalArgumentIndices[kvp.Key] = kvp.Value;
            foreach (var kvp in savedOwnTemporaries)
                parentSnapshot.LocalTemporaryIndices[kvp.Key] = kvp.Value;
            foreach (var kvp in environment.InstanceVariables)
                parentSnapshot.InstanceVariables[kvp.Key] = kvp.Value;
            foreach (var kvp in environment.ClassVariables)
                parentSnapshot.ClassVariables[kvp.Key] = kvp.Value;
            foreach (var kvp in environment.PoolVariables)
                parentSnapshot.PoolVariables[kvp.Key] = kvp.Value;

            environment.Parent = parentSnapshot;
            environment.Type = ScopeType.Block;
            environment.ScopeDepth = savedScopeDepth + 1;
            environment.CumulativeBlockArgOffset += argCount;

            environment.LocalArgumentIndices.Clear();
            for (int i = 0; i < argumentNames.Count; i++)
            {
                environment.LocalArgumentIndices[argumentNames[i]] = i;
            }

            for (int i = 0; i < argumentNames.Count; i++)
            {
                environment.Arguments[argumentNames[i]] = i;
            }
            environment.ArgumentCount = argCount;
            environment.TemporariesAllocatedInFrame = 0;
            environment.LocalTemporaryIndices.Clear();

            var blockTempNames = new List<string>();
            if (blockTemps != null)
            {
                var tempNodes = (blockTemps.children ?? [])
                    .OfType<Smalltalk80Parser.TemporaryNameContext>()
                    .ToList();

                for (int i = 0; i < tempNodes.Count; i++)
                {
                    var tempName = tempNodes[i].GetText();
                    blockTempNames.Add(tempName);
                    environment.LocalTemporaryIndices[tempName] = i;
                }

                int tempBase = environment.ArgumentCount + environment.TemporariesAllocatedInFrame;
                for (int i = 0; i < tempNodes.Count; i++)
                {
                    var tempName = tempNodes[i].GetText();
                    environment.Temporaries[tempName] = tempBase + i;
                }
                environment.TemporariesAllocatedInFrame += tempNodes.Count;
            }

            bool isNested = (savedScopeDepth > 0);
            allocator.EnterBlockScope(argumentNames, blockTempNames, isNested);

            var argSlotIndices = new List<int>();
            foreach (var argName in argumentNames)
            {
                argSlotIndices.Add(allocator.GetSlotIndex(argName));
            }

            // Emit blockCopy: send
            emitter.PushActiveContext();
            emitter.PushSmallInteger(argCount);
            emitter.EmitByte(0xC8);

            // Emit jump placeholder
            int jumpBytecodeOffset = emitter.CurrentOffset;
            emitter.EmitByte(0xA4);
            int jumpOffsetLocation = emitter.CurrentOffset;
            emitter.EmitByte(0x00);

            int blockStartOffset = emitter.CurrentOffset;

            // Store block arguments
            for (int i = argCount - 1; i >= 0; i--)
            {
                emitter.StoreTemporary(argSlotIndices[i]);
            }

            // Compile block body
            var statements = block.statements();
            if (statements != null)
            {
                CompileStatements(statements, inBlock: true);
            }
            else
            {
                emitter.PushNil();
            }

            emitter.ReturnTopFromBlock();

            // Patch jump offset
            if (!emitter.IsSizingMode)
            {
                int afterBlockOffset = emitter.CurrentOffset;
                int jumpOffset = afterBlockOffset - (jumpOffsetLocation + 1);

                if (jumpOffset < 0 || jumpOffset > 1023)
                {
                    throw new InvalidOperationException(
                        $"Block body too large: jump offset {jumpOffset} exceeds Blue Book forward jump range (0-1023)");
                }

                int hiPart = jumpOffset / 256;
                int extensionByte = jumpOffset % 256;
                byte opcode = (byte)(0xA4 + hiPart);

                emitter.PatchByteAt(jumpBytecodeOffset, opcode);
                emitter.PatchByteAt(jumpOffsetLocation, (byte)extensionByte);
            }

            allocator.ExitScope();

            // Restore environment
            foreach (var argName in argumentNames)
            {
                environment.Arguments.Remove(argName);
            }
            foreach (var kvp in savedArguments)
            {
                environment.Arguments[kvp.Key] = kvp.Value;
            }
            environment.ArgumentCount = savedArgCount;

            environment.Temporaries.Clear();
            foreach (var kvp in savedTemporaries)
            {
                environment.Temporaries[kvp.Key] = kvp.Value;
            }

            environment.LocalArgumentIndices.Clear();
            foreach (var kvp in savedOwnArguments)
            {
                environment.LocalArgumentIndices[kvp.Key] = kvp.Value;
            }
            environment.LocalTemporaryIndices.Clear();
            foreach (var kvp in savedOwnTemporaries)
            {
                environment.LocalTemporaryIndices[kvp.Key] = kvp.Value;
            }

            environment.ScopeDepth = savedScopeDepth;
            environment.CumulativeBlockArgOffset = savedCumulativeBlockArgOffset;
            environment.Parent = savedParent;
            environment.TemporariesAllocatedInFrame = savedTemporariesAllocatedInFrame;
        }

        /// <summary>
        /// Compiles a dynamic array literal {expr1. expr2. expr3} into bytecode.
        /// </summary>
        private void CompileDynamicArray(Smalltalk80Parser.DynamicArrayContext dynArray)
        {
            var expressions = dynArray.expression();
            int elementCount = expressions?.Length ?? 0;

            PushVariable("Array");
            emitter.PushLiteralConstant(elementCount);
            emitter.EmitSend("new:", 1);

            if (expressions != null)
            {
                for (int i = 0; i < expressions.Length; i++)
                {
                    emitter.Dup();
                    emitter.PushLiteralConstant(i + 1);
                    CompileExpression(expressions[i]);
                    emitter.EmitSend("at:put:", 2);
                    emitter.Pop();
                }
            }
        }
    }
}
