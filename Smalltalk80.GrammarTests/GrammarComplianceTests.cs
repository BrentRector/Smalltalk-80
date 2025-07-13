using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Antlr4.Runtime;
using Smalltalk80.Generated;
using Smalltalk80.Parser;

namespace Smalltalk80.GrammarTests;

[TestClass]
public class GrammarComplianceTests
{
    private static readonly string Root = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Smalltalk80", "Parser.TestCases"));


    [TestMethod]
    public void AllGrammarTestFiles_ShouldParseWithoutErrors()
    {
        var files = Directory.GetFiles(Root, "*.st", SearchOption.AllDirectories);
        Assert.IsTrue(files.Length > 0, "No test files found.");

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var input = new AntlrInputStream(text);
            var lexer = new Smalltalk80Lexer(input);
            var tokens = new CommonTokenStream(lexer);
            var parser = new Smalltalk80Parser(tokens);

            parser.RemoveErrorListeners();
            var errorListener = new CollectingErrorListener();
            parser.AddErrorListener(errorListener);

            var directory = Path.GetFileName(Path.GetDirectoryName(file)!);
            switch (directory)
            {
                case "expressionEntry":
                    parser.expressionEntry();
                    break;
                case "statementsEntry":
                    parser.statementsEntry();
                    break;
                case "methodEntry":
                    parser.methodEntry();
                    break;
                case "classEntry":
                    parser.classEntry();
                    break;
                default:
                    parser.fileIn();
                    break;
            }

            if (errorListener.HasErrors)
            {
                Assert.Fail($"Parse errors in file: {file}\n{errorListener.Errors}");
            }
        }
    }

    [TestMethod]
    public void KeywordUses_ValidFiles_ShouldParse()
    {
        var dir = Path.Combine(Root, "keywordUses");
        foreach (var file in Directory.GetFiles(dir, "valid*.st"))
            ParseFile(file, expectErrors: false);
    }

    [TestMethod]
    public void KeywordUses_InvalidFiles_ShouldProduceErrors()
    {
        var dir = Path.Combine(Root, "..", "Parser.FailCases");
        foreach (var file in Directory.GetFiles(dir, "*.st"))
            ParseFile(file, expectErrors: true);
    }

    private static void ParseFile(string file, bool expectErrors)
    {
        var text = File.ReadAllText(file);
        var input = new AntlrInputStream(text);
        var lexer = new Smalltalk80Lexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);

        parser.RemoveErrorListeners();
        var errorListener = new CollectingErrorListener();
        parser.AddErrorListener(errorListener);

        var directory = Path.GetFileName(Path.GetDirectoryName(file)!);
        switch (directory)
        {
            case "expressionEntry":
                parser.expressionEntry();
                break;
            case "statementsEntry":
                parser.statementsEntry();
                break;
            case "methodEntry":
                parser.methodEntry();
                break;
            case "classEntry":
                parser.classEntry();
                break;
            default:
                parser.fileIn();
                break;
        }

        if (expectErrors)
            Assert.IsTrue(errorListener.HasErrors, $"Expected errors in {file}");
        else if (errorListener.HasErrors)
            Assert.Fail($"Parse errors in file: {file}\n{errorListener.Errors}");
    }

    private class CollectingErrorListener : BaseErrorListener
    {
        public bool HasErrors => _errors.Count > 0;
        public string Errors => string.Join("\n", _errors);
        private readonly System.Collections.Generic.List<string> _errors = new();

        public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            _errors.Add($"Line {line}:{charPositionInLine} {msg}");
        }
    }
}
