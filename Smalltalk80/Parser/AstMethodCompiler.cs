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
    /// <param name="comment">Optional source comment attached to the method.</param>
    /// <returns>The compiled method.</returns>
    public static CompiledMethod CompileMethod(SequenceNode sequence, string? comment = null)
        => new Compiler().Compile(sequence, comment);

    private sealed class Compiler
    {
        private readonly List<byte> bytecodes = new();
        private readonly List<object> literals = new();
        private readonly Dictionary<string, int> temporaries = new();

        public CompiledMethod Compile(SequenceNode sequence, string? comment)
        {
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
            if (!temporaries.TryGetValue(name, out var index))
                throw new InvalidOperationException($"Unknown variable '{name}'");

            if (index < 16)
            {
                bytecodes.Add((byte)((int)Bytecode.PushTemporaryVariable0 + index));
            }
            else
            {
                throw new NotSupportedException("Only up to 16 temporaries are supported.");
            }
        }

        private void StoreVariable(string name)
        {
            if (!temporaries.TryGetValue(name, out var index))
                throw new InvalidOperationException($"Unknown variable '{name}'");

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

        private int AddLiteral(object value)
        {
            var existing = literals.IndexOf(value);
            if (existing >= 0) return existing;
            literals.Add(value);
            return literals.Count - 1;
        }

        private MethodHeader BuildHeader()
        {
            int flagValue = 0;
            int temporaryCount = temporaries.Count;
            bool largeContext = temporaryCount > 12;
            int literalCount = literals.Count;
            int raw = flagValue | (temporaryCount << 3) | ((largeContext ? 1 : 0) << 8) | (literalCount << 9);
            return new MethodHeader(raw, 0);
        }
    }
}
