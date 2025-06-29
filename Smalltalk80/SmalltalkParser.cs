public class SmalltalkParser {
    private readonly SmalltalkTokenizer tokenizer;
    private List<byte> bytecodes = new();
    private List<object> literals = new();
    private Dictionary<string, int> temporaryVariables = new();
    private int tempIndex = 0;

    public SmalltalkParser(SmalltalkTokenizer tokenizer) {
        this.tokenizer = tokenizer;
    }

    public CompiledMethod ParseMethod() {
        ParseTemporaries();
        ParseStatements();
        bytecodes.Add(124); // return
        return new CompiledMethod {
            Bytecodes = bytecodes.ToArray(),
            Literals = literals.ToArray(),
            TemporariesCount = temporaryVariables.Count
        };
    }

    private void ParseTemporaries() {
        if (tokenizer.Peek() == "|") {
            tokenizer.Next(); // consume |
            while (tokenizer.HasNext && tokenizer.Peek() != "|") {
                var name = tokenizer.Next();
                if (!temporaryVariables.ContainsKey(name))
                    temporaryVariables[name] = tempIndex++;
            }
            tokenizer.Next(); // consume |
        }
    }

    private void ParseStatements() {
        while (tokenizer.HasNext) {
            ParseExpression();
            if (tokenizer.Peek() == ".") tokenizer.Next();
        }
    }

    private void ParseExpression() {
        var left = tokenizer.Next();
        int leftIndex = temporaryVariables.ContainsKey(left) ? temporaryVariables[left] : -1;
        if (leftIndex >= 0) bytecodes.Add((byte)(16 + leftIndex));

        if (tokenizer.HasNext && tokenizer.Peek() == "+") {
            tokenizer.Next();
            var right = tokenizer.Next();
            int rightIndex = temporaryVariables.ContainsKey(right) ? temporaryVariables[right] : -1;
            if (rightIndex >= 0) bytecodes.Add((byte)(16 + rightIndex));

            int litIndex = AddLiteral(new Symbol("+"));
            bytecodes.Add((byte)(128 + litIndex));
        }
    }

    private int AddLiteral(object lit) {
        int index = literals.IndexOf(lit);
        if (index == -1) {
            literals.Add(lit);
            return literals.Count - 1;
        }
        return index;
    }
}