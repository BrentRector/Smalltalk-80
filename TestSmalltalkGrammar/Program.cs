using System;
using System.IO;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Smalltalk80.Generated;

namespace Smalltalk80.Tools
{
    /// <summary>
    /// Utility program that validates the Smalltalk-80 grammar by parsing
    /// a classic <c>file-out</c> source file and reporting any syntax errors.
    /// </summary>
    public static class TestSmalltalkGrammar
    {
        /// <summary>
        /// Application entry point.
        /// </summary>
        /// <param name="args">An array containing the path to the file-out source.</param>
        /// <returns>Zero if parsing succeeds; otherwise one.</returns>
        public static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: TestSmalltalkGrammar <path to Smalltalk-80.st>");
                return 1;
            }

            // 1) Read the entire file-out source
            var source = File.ReadAllText(args[0]);

            // 2) Run it through the ANTLR4 pipeline
            var input = CharStreams.fromString(source);
            var lexer = new Smalltalk80Lexer(input);
            var tokens = new CommonTokenStream(lexer);

            // Dump tokens for debugging
            tokens.Fill(); // Load all tokens

            Console.WriteLine("🔍 Token Stream:");
            foreach (var token in tokens.GetTokens())
            {
                var tokenName = lexer.Vocabulary.GetSymbolicName(token.Type);
                Console.WriteLine($"  [{token.Line}:{token.Column}] {tokenName} → '{token.Text}'");
            }
            Console.WriteLine();

            var parser = new Smalltalk80Parser(tokens);
            parser.Interpreter.PredictionMode = Antlr4.Runtime.Atn.PredictionMode.LL_EXACT_AMBIG_DETECTION;
            parser.Trace = true;

            // 3) Hook up a listener to catch any lex/parse errors
            parser.RemoveErrorListeners();
            var listener = new CountingErrorListener();
            parser.AddErrorListener(listener);

            // 4) Parse in "fileIn" (batch) mode
            parser.fileIn();

            // 5) Report results
            if (listener.ErrorCount == 0 && parser.NumberOfSyntaxErrors == 0)
            {
                Console.WriteLine("🎉 Parsed entire file-out with zero syntax errors.");
                return 0;
            }
            else
            {
                var total = parser.NumberOfSyntaxErrors + listener.ErrorCount;
                Console.Error.WriteLine($"❌ Found {total} syntax errors.");
                return 1;
            }
        }

        /// <summary>
        /// Error listener that counts syntax errors encountered during parsing.
        /// </summary>
        private sealed class CountingErrorListener : BaseErrorListener
        {
            /// <summary>
            /// Gets the number of syntax errors recorded.
            /// </summary>
            public int ErrorCount { get; private set; }

            /// <inheritdoc />
            public override void SyntaxError(
                TextWriter output,
                IRecognizer recognizer,
                IToken offendingSymbol,
                int line,
                int charPositionInLine,
                string msg,
                RecognitionException e)
            {
                ErrorCount++;
                Console.Error.WriteLine($"Syntax Error @ line {line}:{charPositionInLine} – {msg}");
                Environment.Exit(1); // Immediately stop the program
            }
        }
    }
}
