using System;
using System.Collections.Generic;

namespace Smalltalk80.Parser;

/// <summary>
/// Compiles <see cref="SequenceNode"/> AST nodes into <see cref="CompiledMethod"/> instances.
/// </summary>
public static class AstMethodCompiler
{
    /// <summary>
    /// Compiles a method body represented as an AST sequence.
    /// </summary>
    /// <param name="sequence">The method body sequence.</param>
    /// <param name="argumentNames">Names of the method parameters in order.</param>
    /// <param name="instanceVariables">Names of instance variables on the method's class.</param>
    /// <param name="comment">Optional source comment attached to the method.</param>
    /// <returns>The compiled method.</returns>
    public static CompiledMethod CompileMethod(
        SequenceNode sequence,
        IReadOnlyList<string> argumentNames,
        IReadOnlyList<string> instanceVariables,
        string? comment = null)
        => new Compiler(argumentNames, instanceVariables).Compile(sequence, comment);

    private sealed class Compiler
    {
        private readonly IReadOnlyList<string> arguments;
        private readonly IReadOnlyList<string> instanceVariables;
        private readonly List<byte> bytecodes = new();
        private readonly List<object> literals = new();
        private readonly Dictionary<string, int> temporaries = new();

        public Compiler(IReadOnlyList<string> arguments, IReadOnlyList<string> instanceVariables)
        {
            this.arguments = arguments;
            this.instanceVariables = instanceVariables;
        }

        public CompiledMethod Compile(SequenceNode sequence, string? comment)
        {
            foreach (var arg in arguments)
                temporaries[arg] = temporaries.Count;

            foreach (var temp in sequence.Temporaries)
                temporaries[temp] = temporaries.Count;

            for (var i = 0; i < sequence.Statements.Count; i++)
            {
                var isLast = i == sequence.Statements.Count - 1;
                CompileStatement(sequence.Statements[i], isLast);
            }

            return new CompiledMethod
            {
                Header = BuildHeader(),
                Bytecodes = bytecodes.ToArray(),
                Literals = literals.ToArray(),
                TemporariesCount = temporaries.Count,
                HasPrimitive = false,
                PrimitiveIndex = 0,
                Comment = comment
            };
        }

        private void CompileStatement(StatementNode statement, bool isLast)
        {
            switch (statement)
            {
                case ExpressionStatement es:
                    CompileExpression(es.Expression);
                    bytecodes.Add(isLast ? (byte)Bytecode.ReturnTopFromMethod : (byte)Bytecode.PopStack);
                    break;
                case AnswerStatement ans:
                    CompileExpression(ans.Expression);
                    bytecodes.Add((byte)Bytecode.ReturnTopFromMethod);
                    break;
            }
        }

        private void CompileExpression(ExpressionNode expression)
        {
            switch (expression)
            {
                case LiteralExpression lit:
                    EmitLiteral(lit.Value);
                    break;
                case VariableExpression var:
                    EmitVariable(var.Name);
                    break;
                case AssignmentExpression assign:
                    CompileExpression(assign.Value);
                    bytecodes.Add((byte)Bytecode.DuplicateTop);
                    StoreVariable(assign.Variable);
                    break;
                case UnarySendExpression unary:
                    CompileExpression(unary.Receiver);
                    EmitSend(unary.Selector, 0);
                    break;
                case KeywordSendExpression kw:
                    CompileExpression(kw.Receiver);
                    foreach (var arg in kw.Arguments)
                        CompileExpression(arg);
                    var selector = string.Concat(kw.SelectorParts);
                    EmitSend(selector, kw.Arguments.Count);
                    break;
                case PseudoVariableExpression pv:
                    EmitPseudoVariable(pv.Name);
                    break;
                case BinarySendExpression bin:
                    CompileExpression(bin.Receiver);
                    CompileExpression(bin.Argument);
                    EmitSend(bin.Selector, 1);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported expression node {expression.GetType().Name}");
            }
        }

        private void EmitLiteral(object value)
        {
            var index = AddLiteral(value);
            if (index < 32)
            {
                bytecodes.Add((byte)((int)Bytecode.PushLiteralConstant0 + index));
            }
            else
            {
                throw new NotSupportedException("Only up to 32 literals are supported.");
            }
        }

        private void EmitVariable(string name)
        {
            if (temporaries.TryGetValue(name, out var index))
            {
                if (index < 16)
                {
                    bytecodes.Add((byte)((int)Bytecode.PushTemporaryVariable0 + index));
                }
                else
                {
                    throw new NotSupportedException("Only up to 16 temporaries are supported.");
                }
                return;
            }

            int ivIndex = -1;
            for (int i = 0; i < instanceVariables.Count; i++)
            {
                if (instanceVariables[i] == name)
                {
                    ivIndex = i;
                    break;
                }
            }
            if (ivIndex >= 0 && ivIndex < 16)
            {
                bytecodes.Add((byte)((int)Bytecode.PushReceiverVariable0 + ivIndex));
                return;
            }

            throw new InvalidOperationException($"Unknown variable '{name}'");
        }

        private void StoreVariable(string name)
        {
            if (temporaries.TryGetValue(name, out var index))
            {
                if (index < 8)
                {
                    bytecodes.Add((byte)((int)Bytecode.StoreAndPopTemporaryVariable0 + index));
                }
                else
                {
                    bytecodes.Add((byte)Bytecode.ExtendedStoreAndPop);
                    byte descriptor = (byte)((1 << 6) | index);
                    bytecodes.Add(descriptor);
                }
                return;
            }

            int ivIndex = -1;
            for (int i = 0; i < instanceVariables.Count; i++)
            {
                if (instanceVariables[i] == name)
                {
                    ivIndex = i;
                    break;
                }
            }
            if (ivIndex >= 0 && ivIndex < 8)
            {
                bytecodes.Add((byte)((int)Bytecode.StoreAndPopReceiverVariable0 + ivIndex));
                return;
            }

            throw new InvalidOperationException($"Unknown variable '{name}'");
        }

        private void EmitSend(string selector, int argCount)
        {
            var symbol = new Symbol(selector);
            var index = AddLiteral(symbol);
            if (index < 32 && argCount < 8)
            {
                bytecodes.Add((byte)Bytecode.SingleExtendedSend);
                byte descriptor = (byte)((argCount << 5) | index);
                bytecodes.Add(descriptor);
            }
            else
            {
                bytecodes.Add((byte)Bytecode.DoubleExtendedSend);
                bytecodes.Add((byte)argCount);
                bytecodes.Add((byte)index);
            }
        }

        private void EmitPseudoVariable(string name)
        {
            switch (name)
            {
                case "self":
                    bytecodes.Add((byte)Bytecode.PushSelf);
                    break;
                case "nil":
                    bytecodes.Add((byte)Bytecode.PushNil);
                    break;
                case "true":
                    bytecodes.Add((byte)Bytecode.PushTrue);
                    break;
                case "false":
                    bytecodes.Add((byte)Bytecode.PushFalse);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown pseudo variable '{name}'");
            }
        }

        private int AddLiteral(object value)
        {
            var existing = literals.IndexOf(value);
            if (existing >= 0) return existing;
            literals.Add(value);
            return literals.Count - 1;
        }

        private MethodHeader BuildHeader()
        {
            int argCount = arguments.Count;
            int flagValue = argCount >= 5 ? 7 : argCount;
            int temporaryCount = temporaries.Count;
            bool largeContext = temporaryCount > 12;
            int literalCount = literals.Count;
            int raw = flagValue | (temporaryCount << 3) | ((largeContext ? 1 : 0) << 8) | (literalCount << 9);
            int ext = argCount >= 5 ? (argCount << 10) : 0;
            return new MethodHeader(raw, ext);
        }
    }
}
