using System.Text.RegularExpressions;

public class SourceCompiler {
    public static CompiledMethod Compile(string source) {
        // Simplified source compiler: only handles `a + b` expressions
        var tokens = Regex.Split(source, @"\s+");
        if (tokens.Length != 3 || tokens[1] != "+") throw new Exception("Only 'a + b' supported.");

        var method = new CompiledMethod {
            Bytecodes = new byte[] {
                16, 0, // Push temp 0 (a)
                16, 1, // Push temp 1 (b)
                128,   // Send +
                124    // Return top of stack
            },
            Literals = new object[] { new Symbol("+") },
            TemporariesCount = 2,
            HasPrimitive = false,
            PrimitiveIndex = 0
        };

        return method;
    }
}