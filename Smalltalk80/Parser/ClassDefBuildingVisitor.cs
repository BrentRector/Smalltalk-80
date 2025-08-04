using Antlr4.Runtime.Misc;
using Smalltalk80.Generated;

namespace Smalltalk80.Parser;

/// <summary>
/// Visitor that builds class definition and method AST nodes from the parse tree.
/// Extends the simple AST building visitor to handle file-in chunks.
/// </summary>
public class ClassDefBuildingVisitor : SimpleAstBuildingVisitor
{
    /// <summary>
    /// Visit a file-in context and create a FileNode containing all chunks.
    /// </summary>
    public override object? VisitFileIn(Smalltalk80Parser.FileInContext context)
    {
        var chunks = new List<AstNode>();

        foreach (var chunk in context.chunk())
        {
            if (Visit(chunk) is AstNode astNode)
            {
                chunks.Add(astNode);
            }
        }

        return new FileNode(chunks);
    }

    /// <summary>
    /// Visit a class definition chunk.
    /// </summary>
    public override object? VisitClassDefChunk(Smalltalk80Parser.ClassDefChunkContext context)
    {
        var header = context.classHeader();
        var body = context.classBody();

        // Extract superclass name (the first identifier)
        var superclass = header.className().GetText();

        // Extract class name from symbol (remove '#' prefix)
        var classSymbol = header.symbolConst().GetText();
        var className = classSymbol.Substring(1); // Remove '#' prefix

        // Extract instance variables, class variables, etc. from body
        var instanceVariables = new List<string>();
        var classVariables = new List<string>();
        var poolDictionaries = new List<string>();
        string? category = null;

        foreach (var slotSpec in body.classSlotSpec())
        {
            var keyword = slotSpec.identifierColon().GetText();
            var stringValue = slotSpec.stringConst().GetText();
            // Remove quotes from string
            var value = stringValue.Substring(1, stringValue.Length - 2);

            switch (keyword)
            {
                case "instanceVariableNames:":
                    instanceVariables.AddRange(ParseVariableNames(value));
                    break;
                case "classVariableNames:":
                    classVariables.AddRange(ParseVariableNames(value));
                    break;
                case "poolDictionaries:":
                    poolDictionaries.AddRange(ParseVariableNames(value));
                    break;
            }
        }

        // Extract category
        if (body.categoryDecl() != null)
        {
            var categoryString = body.categoryDecl().stringConst().GetText();
            category = categoryString.Substring(1, categoryString.Length - 2);
        }

        return new ClassDefinitionNode(
            superclass,
            className,
            instanceVariables,
            classVariables,
            poolDictionaries,
            category
        );
    }

    /// <summary>
    /// Visit a method category chunk.
    /// </summary>
    public override object? VisitMethodCategoryChunk(Smalltalk80Parser.MethodCategoryChunkContext context)
    {
        var className = context.className().GetText();
        var contextText = context.GetText();

        // More robust class side detection - check if "class" appears after the class name
        var isClassSide = contextText.Contains($"{className}class");

        var categoryString = context.stringConst().GetText();
        var category = categoryString.Substring(1, categoryString.Length - 2);

        var header = new MethodHeaderNode(className, isClassSide, category);

        // Process all method definitions in this category
        var methods = new List<MethodDefinitionNode>();
        foreach (var methodDefContext in context.methodDef())
        {
            if (Visit(methodDefContext) is MethodDefinitionNode methodDef)
            {
                methods.Add(methodDef);
            }
        }

        return new MethodCategoryNode(header, methods);
    }

    /// <summary>
    /// Visit a method definition.
    /// </summary>
    public override object? VisitMethodDef(Smalltalk80Parser.MethodDefContext context)
    {
        var header = context.methodHeader();
        var temporariesCtx = context.temporaries();
        var primitiveCtx = context.primitiveDecl();
        var body = context.methodBody();

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
            // Extract number from "<primitive: N>"
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
            var stmtList = (List<StatementNode>)Visit(body.statements())!;
            statements.AddRange(stmtList);
        }

        // Check for explicit return
        if (body.returnStatement() != null)
        {
            statements.Add((StatementNode)Visit(body.returnStatement())!);
        }

        var sequence = new SequenceNode(temporaries, statements);

        // Extract just the method body source (not the header), and remove trailing BANG
        var methodBodySource = body?.GetText() ?? "";
        if (methodBodySource.EndsWith("!"))
        {
            methodBodySource = methodBodySource.Substring(0, methodBodySource.Length - 1);
        }

        return new MethodDefinitionNode(
            selector,
            arguments,
            sequence,
            null, // comment - TODO: Extract from comments
            methodBodySource, // source - just the body without BANG
            hasPrimitive,
            primitiveIndex
        );
    }

    /// <summary>
    /// Visit a class comment chunk.
    /// </summary>
    public override object? VisitClassCommentChunk(Smalltalk80Parser.ClassCommentChunkContext context)
    {
        var className = context.className().GetText();
        var commentString = context.stringConst().GetText();
        var comment = commentString.Substring(1, commentString.Length - 2);

        return new ClassCommentNode(className, comment);
    }

    /// <summary>
    /// Visit an expression chunk.
    /// </summary>
    public override object? VisitExpressionChunk(Smalltalk80Parser.ExpressionChunkContext context)
    {
        var expression = (ExpressionNode)Visit(context.expression())!;
        return new ExpressionChunkNode(expression);
    }

    /// <summary>
    /// Parse method header to extract selector and argument names.
    /// </summary>
    private (string selector, List<string> arguments) ParseMethodHeader(Smalltalk80Parser.MethodHeaderContext header)
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

    /// <summary>
    /// Parse variable names from a space-separated string.
    /// </summary>
    private static List<string> ParseVariableNames(string variableString)
    {
        if (string.IsNullOrWhiteSpace(variableString))
            return new List<string>();

        return variableString.Split(new[] { ' ', '\t', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}
