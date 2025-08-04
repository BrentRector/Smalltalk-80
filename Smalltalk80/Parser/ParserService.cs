using Antlr4.Runtime;
using Smalltalk80.Generated;

namespace Smalltalk80.Parser;

/// <summary>
/// Service that integrates the ANTLR parser with the AST building visitors.
/// Provides high-level parsing methods for different types of Smalltalk input.
/// </summary>
public static class ParserService
{
    /// <summary>
    /// Parse a complete file-in containing class definitions and methods.
    /// </summary>
    /// <param name="input">The Smalltalk file-in source code.</param>
    /// <returns>A FileNode containing all parsed chunks.</returns>
    public static FileNode ParseFileIn(string input)
    {
        var (parser, _) = CreateParser(input);
        var tree = parser.fileIn();
        var visitor = new ClassDefBuildingVisitor();
        return (FileNode)visitor.Visit(tree)!;
    }

    /// <summary>
    /// Parse a single expression for REPL or workspace evaluation.
    /// </summary>
    /// <param name="input">The Smalltalk expression source code.</param>
    /// <returns>The parsed expression AST node.</returns>
    public static ExpressionNode ParseExpression(string input)
    {
        var (parser, _) = CreateParser(input);
        var tree = parser.expressionEntry();
        var visitor = new SimpleAstBuildingVisitor();
        return (ExpressionNode)visitor.Visit(tree.expression())!;
    }

    /// <summary>
    /// Parse a sequence of statements for workspace evaluation.
    /// </summary>
    /// <param name="input">The Smalltalk statements source code.</param>
    /// <returns>A sequence node containing the parsed statements.</returns>
    public static SequenceNode ParseStatements(string input)
    {
        var (parser, _) = CreateParser(input);
        var tree = parser.statementsEntry();
        var visitor = new SimpleAstBuildingVisitor();
        var statements = (List<StatementNode>)visitor.Visit(tree.statements())!;
        return new SequenceNode(new List<string>(), statements);
    }

    /// <summary>
    /// Parse a method definition for the class browser.
    /// </summary>
    /// <param name="input">The Smalltalk method source code.</param>
    /// <returns>The parsed method definition node.</returns>
    public static MethodDefinitionNode ParseMethod(string input)
    {
        var (parser, _) = CreateParser(input);
        var tree = parser.methodEntry();
        var visitor = new ClassDefBuildingVisitor();

        // Build a temporary method definition context
        var header = tree.methodHeader();
        var temporariesCtx = tree.temporaries();
        var primitiveCtx = tree.primitiveDecl();
        var body = tree.methodBodyNoBang();

        // Parse method selector and arguments
        var (selector, arguments) = ParseMethodHeader(header);

        // Parse temporaries
        var temporaries = new List<string>();
        if (temporariesCtx != null)
        {
            foreach (var temp in temporariesCtx.temporaryName())
            {
                temporaries.Add(temp.GetText());
            }
        }

        // Parse primitive declaration
        bool hasPrimitive = primitiveCtx != null;
        int primitiveIndex = 0;
        if (hasPrimitive && primitiveCtx != null)
        {
            var primitiveText = primitiveCtx.GetText();
            var match = System.Text.RegularExpressions.Regex.Match(primitiveText, @"<primitive:\s*(\d+)>");
            if (match.Success)
            {
                primitiveIndex = int.Parse(match.Groups[1].Value);
            }
        }

        // Parse method body
        var statements = new List<StatementNode>();
        if (body.statements() != null)
        {
            var stmtList = (List<StatementNode>)visitor.Visit(body.statements())!;
            statements.AddRange(stmtList);
        }

        // Check for explicit return
        if (body.returnStatement() != null)
        {
            statements.Add((StatementNode)visitor.Visit(body.returnStatement())!);
        }

        var sequence = new SequenceNode(temporaries, statements);

        return new MethodDefinitionNode(
            selector,
            arguments,
            sequence,
            null, // comment
            input, // source
            hasPrimitive,
            primitiveIndex
        );
    }

    /// <summary>
    /// Parse a class definition from a class browser input.
    /// </summary>
    /// <param name="input">The Smalltalk class definition source code.</param>
    /// <returns>The parsed class definition node.</returns>
    public static ClassDefinitionNode ParseClassDefinition(string input)
    {
        var (parser, _) = CreateParser(input);
        var tree = parser.classEntry();
        var visitor = new ClassDefBuildingVisitor();
        return (ClassDefinitionNode)visitor.VisitClassDefChunk(
            new Smalltalk80Parser.ClassDefChunkContext(null, 0)
            {
                // Create a synthetic class def chunk context
            })!;
    }

    /// <summary>
    /// Compile an expression into bytecode for immediate execution.
    /// </summary>
    /// <param name="input">The Smalltalk expression source code.</param>
    /// <param name="instanceVariables">Available instance variables (empty for workspace).</param>
    /// <returns>A compiled method ready for execution.</returns>
    public static CompiledMethod CompileExpression(string input, IReadOnlyList<string>? instanceVariables = null)
    {
        var expression = ParseExpression(input);
        var statements = new List<StatementNode> { new ExpressionStatement(expression) };
        var sequence = new SequenceNode(new List<string>(), statements);

        return AstMethodCompiler.CompileMethod(
            sequence,
            argumentNames: new List<string>(),
            instanceVariables: instanceVariables ?? new List<string>()
        );
    }

    /// <summary>
    /// Compile statements into bytecode for workspace execution.
    /// </summary>
    /// <param name="input">The Smalltalk statements source code.</param>
    /// <param name="instanceVariables">Available instance variables (empty for workspace).</param>
    /// <returns>A compiled method ready for execution.</returns>
    public static CompiledMethod CompileStatements(string input, IReadOnlyList<string>? instanceVariables = null)
    {
        var sequence = ParseStatements(input);

        return AstMethodCompiler.CompileMethod(
            sequence,
            argumentNames: new List<string>(),
            instanceVariables: instanceVariables ?? new List<string>()
        );
    }

    /// <summary>
    /// Compile a method definition into bytecode.
    /// </summary>
    /// <param name="input">The Smalltalk method source code.</param>
    /// <param name="instanceVariables">Available instance variables for the target class.</param>
    /// <returns>A compiled method ready to be installed in a class.</returns>
    public static CompiledMethod CompileMethod(string input, IReadOnlyList<string> instanceVariables)
    {
        var method = ParseMethod(input);

        return AstMethodCompiler.CompileMethod(
            method.Body,
            argumentNames: method.Arguments,
            instanceVariables: instanceVariables,
            hasPrimitive: method.HasPrimitive,
            primitiveIndex: method.PrimitiveIndex,
            comment: method.Comment
        );
    }

    /// <summary>
    /// Create a parser instance for the given input.
    /// </summary>
    private static (Smalltalk80Parser parser, CommonTokenStream tokens) CreateParser(string input)
    {
        var inputStream = new AntlrInputStream(input);
        var lexer = new Smalltalk80Lexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);

        // Add error handling
        parser.RemoveErrorListeners();
        parser.AddErrorListener(new ThrowingErrorListener());

        return (parser, tokens);
    }

    /// <summary>
    /// Parse method header to extract selector and argument names.
    /// </summary>
    private static (string selector, List<string> arguments) ParseMethodHeader(Smalltalk80Parser.MethodHeaderContext header)
    {
        var arguments = new List<string>();

        // Check for unary selector
        if (header.unarySelector() != null)
        {
            return (header.unarySelector().GetText(), arguments);
        }

        // Check for binary selector
        if (header.binarySelector() != null && header.formalArg() != null && header.formalArg().Length > 0)
        {
            arguments.Add(header.formalArg()[0].GetText());
            return (header.binarySelector().GetText(), arguments);
        }

        // Check for keyword selector
        if (header.identifierColon() != null)
        {
            var selectorParts = new List<string>();
            var formalArgs = header.formalArg();

            for (int i = 0; i < header.identifierColon().Length; i++)
            {
                selectorParts.Add(header.identifierColon(i).GetText());
                if (i < formalArgs.Length)
                {
                    arguments.Add(formalArgs[i].GetText());
                }
            }

            var selector = string.Join("", selectorParts);
            return (selector, arguments);
        }

        throw new InvalidOperationException("Unable to parse method header");
    }
}

/// <summary>
/// Error listener that throws exceptions for parse errors.
/// This listener converts ANTLR parse errors into .NET exceptions for easier error handling.
/// </summary>
public class ThrowingErrorListener : BaseErrorListener
{
    /// <summary>
    /// Handles syntax errors by throwing a ParseException.
    /// This method is called by ANTLR when a syntax error is encountered during parsing.
    /// </summary>
    /// <param name="output">The output stream for error messages (not used in this implementation).</param>
    /// <param name="recognizer">The parser that encountered the error.</param>
    /// <param name="offendingSymbol">The token that caused the error.</param>
    /// <param name="line">The line number where the error occurred.</param>
    /// <param name="charPositionInLine">The character position within the line where the error occurred.</param>
    /// <param name="msg">The error message generated by ANTLR.</param>
    /// <param name="e">The recognition exception that caused the error, if any.</param>
    /// <exception cref="ParseException">Always thrown to convert the ANTLR error into a .NET exception.</exception>
    public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
        int line, int charPositionInLine, string msg, RecognitionException e)
    {
        throw new ParseException($"Parse error at line {line}, position {charPositionInLine}: {msg}");
    }
}

/// <summary>
/// Exception thrown when parsing fails.
/// This exception wraps ANTLR parsing errors in a standard .NET exception format.
/// </summary>
public class ParseException : Exception
{
    /// <summary>
    /// Initializes a new instance of the ParseException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    public ParseException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the ParseException class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ParseException(string message, Exception innerException) : base(message, innerException) { }
}
