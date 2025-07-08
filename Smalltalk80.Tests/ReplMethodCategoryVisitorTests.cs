using System;
using Antlr4.Runtime;
using Smalltalk80.Generated;
using Smalltalk80.Parser;
using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Tests for parsing multiple methods within a single method category header
/// using the <see cref="AstBuildingVisitor"/>.
/// </summary>
public class ReplMethodCategoryVisitorTests
{
    [Fact]
    public void VisitReplMethodsForCategory_BuildsFileNodeWithMultipleMethods()
    {
        var source = string.Join(Environment.NewLine, new[]
        {
            "Example methodsFor: 'testing'",
            "foo",
            "    ^1",
            string.Empty,
            "bar",
            "    ^2"
        });

        var input = new AntlrInputStream(source);
        var lexer = new Smalltalk80Lexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);
        var unit = parser.replUnit();
        var visitor = new AstBuildingVisitor(tokens);
        var file = (FileNode)visitor.Visit(unit)!;

        Assert.Equal(3, file.Chunks.Count);
        Assert.IsType<MethodHeaderNode>(file.Chunks[0]);
        Assert.IsType<MethodDefinitionNode>(file.Chunks[1]);
        Assert.IsType<MethodDefinitionNode>(file.Chunks[2]);
    }
}
