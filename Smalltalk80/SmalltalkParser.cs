using System;
using System.Collections.Generic;

/// <summary>
///     Parses a subset of Smalltalk source code into bytecode.
/// </summary>
public sealed class SmalltalkParser
{
    private readonly SmalltalkTokenizer tokenizer;
    private readonly List<byte> bytecodes = new();
    private readonly List<object> literals = new();
    private readonly Dictionary<string, int> temporaryVariables = new();
    private int tempIndex = 0;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SmalltalkParser"/> class.
    /// </summary>
    /// <param name="tokenizer">The tokenizer providing the source tokens.</param>
    public SmalltalkParser(SmalltalkTokenizer tokenizer)
    {
        this.tokenizer = tokenizer;
    }

    /// <summary>
    ///     Parses the tokens into a <see cref="CompiledMethod"/> representing a method.
    /// </summary>
    /// <returns>The compiled method.</returns>
    public CompiledMethod ParseMethod()
    {
        ParseTemporaries();
        ParseStatements();
        bytecodes.Add(124); // return
        return new CompiledMethod {
            Bytecodes = bytecodes.ToArray(),
            Literals = literals.ToArray(),
            TemporariesCount = temporaryVariables.Count
        };
    }

    private void ParseTemporaries()
    {
        if (tokenizer.Peek() == "|") {
            tokenizer.Next();
            while (tokenizer.HasNext && tokenizer.Peek() != "|") {
                var name = tokenizer.Next()!;
                if (!temporaryVariables.ContainsKey(name))
                    temporaryVariables[name] = tempIndex++;
            }
            tokenizer.Next();
        }
    }

    private void ParseStatements()
    {
        while (tokenizer.HasNext) {
            ParseExpression();
            if (tokenizer.Peek() == ".") tokenizer.Next();
        }
    }

    private void ParseExpression()
    {
        var left = tokenizer.Next()!;
        int leftIndex = temporaryVariables.ContainsKey(left) ? temporaryVariables[left] : -1;
        if (leftIndex >= 0) bytecodes.Add((byte)(16 + leftIndex));

        if (tokenizer.HasNext && tokenizer.Peek() == "+") {
            tokenizer.Next();
            var right = tokenizer.Next()!;
            int rightIndex = temporaryVariables.ContainsKey(right) ? temporaryVariables[right] : -1;
            if (rightIndex >= 0) bytecodes.Add((byte)(16 + rightIndex));

            int litIndex = AddLiteral(new Symbol("+"));
            bytecodes.Add((byte)(128 + litIndex));
        }
    }

    private int AddLiteral(object lit)
    {
        int index = literals.IndexOf(lit);
        if (index == -1) {
            literals.Add(lit);
            return literals.Count - 1;
        }
        return index;
    }
}