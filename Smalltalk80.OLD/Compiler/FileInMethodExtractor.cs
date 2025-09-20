using System.Collections.Generic;
using Smalltalk80.Generated;
using Smalltalk80.Parser;

namespace Smalltalk80.Compiler;

/// <summary>
/// Visitor that extracts a method from file-out format chunks.
/// Used when compiling methods that include bang (!) terminators.
/// </summary>
public class FileInMethodExtractor : Smalltalk80BaseVisitor<object?>
{
    /// <summary>
    /// Visit a fileIn context and extract the first method definition.
    /// </summary>
    public override object? VisitFileIn(Smalltalk80Parser.FileInContext context)
    {
        // Look for method definition chunks
        foreach (var chunk in context.chunk())
        {
            // Check if this is a method category chunk
            var methodCategoryChunk = chunk.methodCategoryChunk();
            if (methodCategoryChunk != null)
            {
                // Extract the first method from the category
                var methodDefs = methodCategoryChunk.methodDef();
                if (methodDefs.Length > 0)
                {
                    return VisitMethodDef(methodDefs[0]);
                }
            }
        }
        
        // If no method category chunk, look for a single method in expression chunks
        // This handles the case where someone might send just "methodName ! body !"
        throw new CompilationException("No method definition found in file-out format");
    }
    
    /// <summary>
    /// Visit a method definition and create a MethodNode.
    /// </summary>
    public override object? VisitMethodDef(Smalltalk80Parser.MethodDefContext context)
    {
        // Extract method header (selector and arguments)
        var methodHeader = context.methodHeader();
        string selector;
        var arguments = new List<string>();
        
        // Handle different method header types
        if (methodHeader.unarySelector() != null)
        {
            // Unary selector (no arguments)
            selector = methodHeader.unarySelector().GetText();
        }
        else if (methodHeader.binarySelector() != null)
        {
            // Binary selector with one argument
            selector = methodHeader.binarySelector().GetText();
            arguments.Add(methodHeader.formalArg()[0].GetText());
        }
        else
        {
            // Keyword selector with multiple arguments
            var selectorParts = new List<string>();
            var formalArgs = methodHeader.formalArg();
            var identifierColons = methodHeader.identifierColon();
            
            for (int i = 0; i < identifierColons.Length; i++)
            {
                selectorParts.Add(identifierColons[i].GetText());
                arguments.Add(formalArgs[i].GetText());
            }
            selector = string.Join("", selectorParts);
        }
        
        // Extract temporaries
        var temporaries = new List<string>();
        if (context.temporaries() != null)
        {
            foreach (var temp in context.temporaries().temporaryName())
            {
                temporaries.Add(temp.GetText());
            }
        }
        
        // Extract primitive number if present
        int? primitiveNumber = null;
        if (context.primitiveDecl() != null)
        {
            var primitiveText = context.primitiveDecl().GetText();
            // Extract number from <primitive: 123>
            var match = System.Text.RegularExpressions.Regex.Match(primitiveText, @"\d+");
            if (match.Success && int.TryParse(match.Value, out var primitive))
            {
                primitiveNumber = primitive;
            }
        }
        
        // Parse method body
        var bodyContext = context.methodBody();
        var statements = new List<StatementNode>();
        
        if (bodyContext.statements() != null)
        {
            // Use the SimpleAstBuildingVisitor to parse statements
            var visitor = new SimpleAstBuildingVisitor();
            var stmtList = (List<StatementNode>)visitor.Visit(bodyContext.statements())!;
            statements.AddRange(stmtList);
        }
        else if (bodyContext.returnStatement() != null)
        {
            var visitor = new SimpleAstBuildingVisitor();
            statements.Add((StatementNode)visitor.Visit(bodyContext.returnStatement())!);
        }
        
        var body = new SequenceNode(temporaries, statements);
        
        return new MethodNode(selector, arguments, body, primitiveNumber);
    }
}