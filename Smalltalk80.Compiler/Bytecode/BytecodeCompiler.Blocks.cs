using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime.Tree;
using Smalltalk80.Compiler.Generated;
using Smalltalk80.Compiler.Model;
using Smalltalk80.Core.Bytecodes;

namespace Smalltalk80.Compiler.Bytecode
{
    /// <summary>
    /// Block and dynamic array compilation methods for the BytecodeCompiler.
    /// </summary>
    public partial class BytecodeCompiler
    {
        /// <summary>
        /// Encapsulates the save/restore pattern for compilation environment state during block compilation.
        /// When a block is compiled, we need to:
        /// 1. Save the current method/outer-block environment state
        /// 2. Create a new block scope environment
        /// 3. Compile the block body with the new scope
        /// 4. Restore the original environment when done
        ///
        /// This class handles steps 1 and 4 via the IDisposable pattern, ensuring proper cleanup
        /// even if an exception occurs during block compilation.
        /// </summary>
        private sealed class BlockCompilationState : IDisposable
        {
            private readonly CompilationEnvironment _environment;
            private readonly Dictionary<string, int> _savedArguments;
            private readonly int _savedArgCount;
            private readonly int _savedScopeDepth;
            private readonly Dictionary<string, int> _savedTemporaries;
            private readonly Dictionary<string, int> _savedOwnArguments;
            private readonly Dictionary<string, int> _savedOwnTemporaries;
            private readonly int _savedCumulativeBlockArgOffset;
            private readonly int _savedTemporariesAllocatedInFrame;
            private readonly CompilationEnvironment? _savedParent;
            private readonly List<string> _argumentNames;
            private bool _disposed;

            /// <summary>
            /// Gets the saved scope depth to determine if this is a nested block.
            /// Scope depth 0 = method level, > 0 = inside a block (nested).
            /// </summary>
            public int SavedScopeDepth => _savedScopeDepth;

            /// <summary>
            /// Creates a new block compilation state, saving the current environment.
            /// </summary>
            /// <param name="environment">The compilation environment to save and later restore.</param>
            /// <param name="argumentNames">Block argument names (needed for cleanup).</param>
            public BlockCompilationState(CompilationEnvironment environment, List<string> argumentNames)
            {
                _environment = environment;
                _argumentNames = argumentNames;

                // Save all mutable state that will be modified during block compilation
                _savedArguments = new Dictionary<string, int>(environment.Arguments);
                _savedArgCount = environment.ArgumentCount;
                _savedScopeDepth = environment.ScopeDepth;
                _savedTemporaries = new Dictionary<string, int>(environment.Temporaries);
                _savedOwnArguments = new Dictionary<string, int>(environment.LocalArgumentIndices);
                _savedOwnTemporaries = new Dictionary<string, int>(environment.LocalTemporaryIndices);
                _savedCumulativeBlockArgOffset = environment.CumulativeBlockArgOffset;
                _savedTemporariesAllocatedInFrame = environment.TemporariesAllocatedInFrame;
                _savedParent = environment.Parent;

                // If entering a block from method level, record method-level counts
                // These are needed for proper unified temp frame indexing
                if (_savedScopeDepth == 0)
                {
                    environment.MethodArgumentCount = _savedArgCount;
                    environment.MethodTemporaryCount = environment.TemporariesAllocatedInFrame;
                }
            }

            /// <summary>
            /// Creates the parent environment snapshot that the block will reference for outer variable access.
            /// </summary>
            public CompilationEnvironment CreateParentSnapshot()
            {
                var parentSnapshot = new CompilationEnvironment
                {
                    Type = (_savedScopeDepth == 0) ? ScopeType.Method : ScopeType.Block,
                    ArgumentCount = _savedArgCount,
                    // At method level (depth 0), temps start at 0; otherwise preserve current count
                    TemporariesAllocatedInFrame = (_savedScopeDepth == 0) ? 0 : _savedTemporariesAllocatedInFrame,
                    ScopeDepth = _savedScopeDepth,
                    Parent = _savedParent,
                    MethodArgumentCount = _environment.MethodArgumentCount,
                    MethodTemporaryCount = _environment.MethodTemporaryCount,
                    CumulativeBlockArgOffset = _savedCumulativeBlockArgOffset,
                    KnownClassNames = _environment.KnownClassNames,
                    KnownPoolDictionaryNames = _environment.KnownPoolDictionaryNames
                };

                // Copy all variable dictionaries to the snapshot
                foreach (var kvp in _savedArguments)
                    parentSnapshot.Arguments[kvp.Key] = kvp.Value;
                foreach (var kvp in _savedTemporaries)
                    parentSnapshot.Temporaries[kvp.Key] = kvp.Value;
                foreach (var kvp in _savedOwnArguments)
                    parentSnapshot.LocalArgumentIndices[kvp.Key] = kvp.Value;
                foreach (var kvp in _savedOwnTemporaries)
                    parentSnapshot.LocalTemporaryIndices[kvp.Key] = kvp.Value;
                foreach (var kvp in _environment.InstanceVariables)
                    parentSnapshot.InstanceVariables[kvp.Key] = kvp.Value;
                foreach (var kvp in _environment.ClassVariables)
                    parentSnapshot.ClassVariables[kvp.Key] = kvp.Value;
                foreach (var kvp in _environment.PoolVariables)
                    parentSnapshot.PoolVariables[kvp.Key] = kvp.Value;

                return parentSnapshot;
            }

            /// <summary>
            /// Restores the compilation environment to its state before block compilation began.
            /// </summary>
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                // Remove block arguments from the environment
                foreach (var argName in _argumentNames)
                {
                    _environment.Arguments.Remove(argName);
                }

                // Restore saved arguments
                foreach (var kvp in _savedArguments)
                {
                    _environment.Arguments[kvp.Key] = kvp.Value;
                }
                _environment.ArgumentCount = _savedArgCount;

                // Restore temporaries
                _environment.Temporaries.Clear();
                foreach (var kvp in _savedTemporaries)
                {
                    _environment.Temporaries[kvp.Key] = kvp.Value;
                }

                // Restore local scope dictionaries
                _environment.LocalArgumentIndices.Clear();
                foreach (var kvp in _savedOwnArguments)
                {
                    _environment.LocalArgumentIndices[kvp.Key] = kvp.Value;
                }
                _environment.LocalTemporaryIndices.Clear();
                foreach (var kvp in _savedOwnTemporaries)
                {
                    _environment.LocalTemporaryIndices[kvp.Key] = kvp.Value;
                }

                // Restore scope state
                _environment.ScopeDepth = _savedScopeDepth;
                _environment.CumulativeBlockArgOffset = _savedCumulativeBlockArgOffset;
                _environment.Parent = _savedParent;
                _environment.TemporariesAllocatedInFrame = _savedTemporariesAllocatedInFrame;
            }
        }

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
        /// Blue Book: Blocks compile bytecode INLINE in the method, with a jump over the block body.
        /// The blockCopy: primitive creates a BlockContext that captures the home MethodContext
        /// and initial IP for later execution via value/value:/value:value: etc.
        /// </summary>
        private void CompileBlock(Smalltalk80Parser.BlockContext block)
        {
            // Extract block argument names
            List<string> argumentNames = ExtractBlockArguments(block);
            int argCount = argumentNames.Count;

            // Extract block temporary names
            List<string> blockTempNames = ExtractBlockTemporaries(block);

            // Save environment state and set up block scope (using RAII pattern for cleanup)
            using var state = new BlockCompilationState(environment, argumentNames);

            // Set up the block's compilation environment
            SetupBlockEnvironment(state, argumentNames, blockTempNames, argCount);

            // Determine if this is a nested block (inside another block) vs. top-level (directly in method)
            // This affects slot reuse strategy in the temp frame allocator
            bool isNested = state.SavedScopeDepth > 0;
            allocator.EnterBlockScope(argumentNames, blockTempNames, isNested);

            // Get the allocated slot indices for block arguments
            List<int> argSlotIndices = argumentNames.Select(name => allocator.GetSlotIndex(name)).ToList();

            // Emit bytecode for block creation and body
            EmitBlockBytecode(block, argCount, argSlotIndices);

            allocator.ExitScope();
            // BlockCompilationState.Dispose() restores the environment automatically
        }

        /// <summary>
        /// Extracts block argument names from the block AST.
        /// </summary>
        private static List<string> ExtractBlockArguments(Smalltalk80Parser.BlockContext block)
        {
            var blockArgs = block.blockArgs();
            if (blockArgs == null) return [];

            return blockArgs.children
                .OfType<Smalltalk80Parser.ArgumentNameContext>()
                .Select(ctx => ctx.IDENTIFIER().GetText())
                .ToList();
        }

        /// <summary>
        /// Extracts block temporary variable names from the block AST.
        /// </summary>
        private static List<string> ExtractBlockTemporaries(Smalltalk80Parser.BlockContext block)
        {
            var blockTemps = block.blockTemps();
            if (blockTemps == null) return [];

            return (blockTemps.children ?? [])
                .OfType<Smalltalk80Parser.TemporaryNameContext>()
                .Select(ctx => ctx.GetText())
                .ToList();
        }

        /// <summary>
        /// Sets up the compilation environment for compiling a block's body.
        /// </summary>
        private void SetupBlockEnvironment(
            BlockCompilationState state,
            List<string> argumentNames,
            List<string> blockTempNames,
            int argCount)
        {
            // Create parent snapshot for outer variable access
            var parentSnapshot = state.CreateParentSnapshot();
            environment.Parent = parentSnapshot;

            // Configure environment for block scope
            environment.Type = ScopeType.Block;
            environment.ScopeDepth = state.SavedScopeDepth + 1;
            environment.CumulativeBlockArgOffset += argCount;

            // Set up block arguments in the environment
            environment.LocalArgumentIndices.Clear();
            for (int i = 0; i < argumentNames.Count; i++)
            {
                environment.LocalArgumentIndices[argumentNames[i]] = i;
                environment.Arguments[argumentNames[i]] = i;
            }
            environment.ArgumentCount = argCount;
            environment.TemporariesAllocatedInFrame = 0;
            environment.LocalTemporaryIndices.Clear();

            // Set up block temporaries in the environment
            for (int i = 0; i < blockTempNames.Count; i++)
            {
                var tempName = blockTempNames[i];
                environment.LocalTemporaryIndices[tempName] = i;

                int tempBase = environment.ArgumentCount + environment.TemporariesAllocatedInFrame;
                environment.Temporaries[tempName] = tempBase + i;
            }
            environment.TemporariesAllocatedInFrame += blockTempNames.Count;
        }

        /// <summary>
        /// Emits the bytecode for a block: blockCopy, jump placeholder, body, return, then patches jump.
        /// </summary>
        private void EmitBlockBytecode(
            Smalltalk80Parser.BlockContext block,
            int argCount,
            List<int> argSlotIndices)
        {
            // Emit blockCopy: send - creates a BlockContext referencing the current context
            emitter.PushActiveContext();
            emitter.PushSmallInteger(argCount);
            emitter.EmitByte((byte)Bytecode.SEND_SPECIAL_BLOCKCOPY);

            // Emit jump placeholder to skip over block body during normal execution
            // The block body is only executed when the block is sent value/value:/etc.
            int jumpBytecodeOffset = emitter.CurrentOffset;
            emitter.EmitByte((byte)Bytecode.LONG_JUMP_4);
            int jumpOffsetLocation = emitter.CurrentOffset;
            emitter.EmitByte(0x00);

            // Store block arguments from stack into temp slots
            // Arguments are on stack in order, so pop in reverse to store correctly
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

            // Patch the jump offset now that we know the block body size
            PatchBlockJump(jumpBytecodeOffset, jumpOffsetLocation);
        }

        /// <summary>
        /// Patches the jump instruction that skips over the block body.
        /// </summary>
        private void PatchBlockJump(int jumpBytecodeOffset, int jumpOffsetLocation)
        {
            if (emitter.IsSizingMode) return;

            int afterBlockOffset = emitter.CurrentOffset;
            int jumpOffset = afterBlockOffset - (jumpOffsetLocation + 1);

            if (jumpOffset < 0 || jumpOffset > 1023)
            {
                throw new InvalidOperationException(
                    $"Block body too large: jump offset {jumpOffset} exceeds Blue Book forward jump range (0-1023)");
            }

            // Blue Book long jump encoding: LONG_JUMP_4 + hiPart gives range 0-1023
            // LONG_JUMP_4 (0xA4): offset = ((4-4)*256 + ext) = 0-255
            // LONG_JUMP_5 (0xA5): offset = ((5-4)*256 + ext) = 256-511
            // LONG_JUMP_6 (0xA6): offset = ((6-4)*256 + ext) = 512-767
            // LONG_JUMP_7 (0xA7): offset = ((7-4)*256 + ext) = 768-1023
            int hiPart = jumpOffset / 256;
            int extensionByte = jumpOffset % 256;
            byte opcode = (byte)((byte)Bytecode.LONG_JUMP_4 + hiPart);

            emitter.PatchByteAt(jumpBytecodeOffset, opcode);
            emitter.PatchByteAt(jumpOffsetLocation, (byte)extensionByte);
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
