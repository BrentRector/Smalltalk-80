using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;

namespace Smalltalk80.Compiler
{
    /// <summary>
    /// Custom error listener to collect parse errors.
    /// </summary>
    public class CollectingErrorListener : BaseErrorListener
    {
        public List<string> Errors { get; } = [];

        public override void SyntaxError(
            TextWriter output,
            IRecognizer recognizer,
            IToken? offendingSymbol,
            int line,
            int charPositionInLine,
            string msg,
            RecognitionException? e)
        {
            Errors.Add($"line {line}:{charPositionInLine} {msg}");
        }
    }
}
