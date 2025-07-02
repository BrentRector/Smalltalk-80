using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Smalltalk80.Generated;
using Smalltalk80.Parser;

/// <summary>
/// Provides a parser that converts Smalltalk source code into
/// <see cref="CompiledMethod"/> instances.
/// </summary>
public sealed class SmalltalkParser
{
    private readonly string source;
    private readonly List<byte> bytecodes = new();
    private readonly List<object> literals = new();
    private readonly Dictionary<string, int> temporaries = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SmalltalkParser"/> class.
    /// </summary>
    /// <param name="source">The Smalltalk source code to parse.</param>
    public SmalltalkParser(string source) => this.source = source;

    /// <summary>
    /// Parses the source into a <see cref="CompiledMethod"/>.
    /// </summary>
    /// <returns>The compiled method.</returns>
    public CompiledMethod ParseMethod()
    {
        var ast = BuildAst();
        foreach (var temp in ast.Sequence.Temporaries)
        {
            temporaries[temp] = temporaries.Count;
        }

        for (var i = 0; i < ast.Sequence.Statements.Count; i++)
        {
            var isLast = i == ast.Sequence.Statements.Count - 1;
            CompileStatement(ast.Sequence.Statements[i], isLast);
        }

        var header = BuildHeader();
        return new CompiledMethod
        {
            Header = header,
            Bytecodes = bytecodes.ToArray(),
            Literals = literals.ToArray(),
            TemporariesCount = temporaries.Count,
            HasPrimitive = false,
            PrimitiveIndex = 0
        };
    }

    private ScriptNode BuildAst()
    {
        var input = new AntlrInputStream(source);
        var lexer = new Smalltalk80Lexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);
        var tree = parser.script();
        var visitor = new AstBuildingVisitor();
        return (ScriptNode)visitor.Visit(tree)!;
    }

    private void CompileStatement(StatementNode statement, bool isLast)
    {
        switch (statement)
        {
            case ExpressionStatement es:
                CompileExpression(es.Expression);
                if (!isLast)
                {
                    bytecodes.Add((byte)Bytecode.PopStack);
                }
                else
                {
                    bytecodes.Add((byte)Bytecode.ReturnTopFromMethod);
                }
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
        {
            throw new InvalidOperationException($"Unknown variable '{name}'");
        }

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
        {
            throw new InvalidOperationException($"Unknown variable '{name}'");
        }

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
        if (existing >= 0)
            return existing;
        literals.Add(value);
        return literals.Count - 1;
    }

    private MethodHeader BuildHeader()
    {
        int flagValue = 0; // no primitive, zero args
        int temporaryCount = temporaries.Count;
        bool largeContext = temporaryCount > 12;
        int literalCount = literals.Count;
        int raw = flagValue | (temporaryCount << 3) | ((largeContext ? 1 : 0) << 8) | (literalCount << 9);
        return new MethodHeader(raw, 0);
    }
}
