using System.Text;
using System.Text.Json;
using Antlr4.Runtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Smalltalk80.Compiler.AST;
using Smalltalk80.Compiler.CodeGen;
using Smalltalk80.Compiler.Generated;

namespace Smalltalk80.Compiler.Tests;

/// <summary>
/// Base class for compiler test cases providing common test utilities.
/// </summary>
public abstract class CompilerTestBase
{
    /// <summary>
    /// Compiles Smalltalk source and returns the JSON output.
    /// </summary>
    protected JObject CompileSource(string source)
    {
        // Parse the source code
        var inputStream = new AntlrInputStream(source);
        var lexer = new Smalltalk80Lexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokenStream);
        
        // Parse the file
        var tree = parser.fileIn();
        
        // Check for syntax errors
        if (parser.NumberOfSyntaxErrors > 0)
        {
            throw new InvalidOperationException($"Parsing failed with {parser.NumberOfSyntaxErrors} syntax errors.");
        }
        
        // Build AST
        var astBuilder = new FileInFormatAstBuilder(tokenStream);
        astBuilder.Visit(tree);
        var result = astBuilder.GetResult();
        
        // Generate JSON
        var generator = new ColdStartJsonGenerator("test.st");
        string json = generator.Generate(result);
        
        return JObject.Parse(json);
    }
    
    /// <summary>
    /// Compiles a source file and returns the JSON output.
    /// </summary>
    protected JObject CompileFile(string fileName)
    {
        var path = Path.Combine("TestSources", fileName);
        var source = File.ReadAllText(path);
        return CompileSource(source);
    }
    
    /// <summary>
    /// Gets the first method from the compiled output.
    /// </summary>
    protected JObject GetFirstMethod(JObject json)
    {
        var methods = (JArray)json["methods"]!;
        Assert.That(methods, Is.Not.Null, "No methods in output");
        Assert.That(methods.Count, Is.GreaterThan(0), "No methods found");
        
        return (JObject)methods[0];
    }
    
    /// <summary>
    /// Asserts that the bytecode sequence matches the expected values.
    /// </summary>
    protected void AssertBytecode(JObject method, params int[] expectedBytecodes)
    {
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null, "No bytecodes in method");
        
        var actualBytes = bytecodes.Select(b => (int)b).ToArray();
        
        // Create detailed message for better debugging
        var message = new StringBuilder();
        message.AppendLine("Bytecode mismatch:");
        message.AppendLine($"Expected: [{string.Join(", ", expectedBytecodes)}]");
        message.AppendLine($"Actual:   [{string.Join(", ", actualBytes)}]");
        
        Assert.That(actualBytes, Is.EqualTo(expectedBytecodes), message.ToString());
    }
    
    /// <summary>
    /// Asserts that the literals contain the expected values.
    /// </summary>
    protected void AssertLiterals(JObject method, params object[] expectedLiterals)
    {
        var literals = (JArray)method["literals"]!;
        Assert.That(literals, Is.Not.Null, "No literals in method");
        
        Assert.That(literals.Count, Is.EqualTo(expectedLiterals.Length), 
            $"Expected {expectedLiterals.Length} literals, but got {literals.Count}");
        
        for (int i = 0; i < expectedLiterals.Length; i++)
        {
            var expected = expectedLiterals[i];
            var actual = literals[i];
            
            if (expected is string str)
            {
                if (str.StartsWith("#"))
                {
                    // Symbol - should be an index into symbols array
                    Assert.That(actual["type"]?.ToString(), Is.EqualTo("symbol"), 
                        $"Literal {i} should be a symbol");
                    // Note: We can't easily check the value without the symbols array
                }
                else if (str.StartsWith("$"))
                {
                    // Character - compiler outputs lowercase "character"
                    Assert.That(actual["type"]?.ToString(), Is.EqualTo("character"),
                        $"Literal {i} should be a character");
                    Assert.That(actual["value"]?.ToString(), Is.EqualTo(str.Substring(1)),
                        $"Character value mismatch at index {i}");
                }
                else
                {
                    // String - should be an index into strings array
                    Assert.That(actual["type"]?.ToString(), Is.EqualTo("string"),
                        $"Literal {i} should be a string");
                    // Note: We can't easily check the value without the strings array
                }
            }
            else if (expected is int intVal)
            {
                // Compiler outputs "smallInt" or "SmallInteger" for integers
                var actualType = actual["type"]?.ToString();
                Assert.That(actualType == "smallInt" || actualType == "SmallInteger" || actualType == "integer",
                    Is.True, $"Literal {i} should be an integer type, got: {actualType}");
                Assert.That((int)actual["value"]!, Is.EqualTo(intVal),
                    $"Integer value mismatch at index {i}");
            }
            else if (expected is double doubleVal)
            {
                Assert.That(actual["type"]?.ToString(), Is.EqualTo("float"),
                    $"Literal {i} should be a float");
                Assert.That((double)actual["value"]!, Is.EqualTo(doubleVal).Within(0.0001),
                    $"Float value mismatch at index {i}");
            }
            else if (expected is bool boolVal)
            {
                var expectedType = boolVal ? "True" : "False";
                Assert.That(actual["type"]?.ToString(), Is.EqualTo(expectedType),
                    $"Literal {i} should be {expectedType}");
            }
            else if (expected == null)
            {
                Assert.That(actual["type"]?.ToString(), Is.EqualTo("Nil"),
                    $"Literal {i} should be Nil");
            }
        }
    }
    
    /// <summary>
    /// Creates a simple test class wrapper around a method.
    /// </summary>
    protected string WrapInTestClass(string methodSource, string className = "TestClass")
    {
        return $@"
Object subclass: #{className}
    instanceVariableNames: 'x y z'
    classVariableNames: 'ClassVar1 ClassVar2'
    poolDictionaries: ''
    category: 'Test'!

!{className} methodsFor: 'testing'!

{methodSource}
! !
";
    }
    
    /// <summary>
    /// Asserts a specific bytecode at a given index.
    /// </summary>
    protected void AssertBytecodeAt(JArray bytecodes, int index, int expected, string description = "")
    {
        Assert.That(bytecodes.Count, Is.GreaterThan(index), 
            $"Bytecode array too short, expected at least {index + 1} bytes");
        
        var actual = (int)bytecodes[index];
        var message = string.IsNullOrEmpty(description) 
            ? $"Bytecode at index {index}" 
            : $"Bytecode at index {index} ({description})";
            
        Assert.That(actual, Is.EqualTo(expected), 
            $"{message}: expected {expected} but got {actual}");
    }
}