using System.Text;
using Antlr4.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Smalltalk80.Compiler.AST;

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
        var lexer = new global::Smalltalk80Lexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new global::Smalltalk80Parser(tokenStream);
        
        // Determine entry point
        Antlr4.Runtime.Tree.IParseTree parseTree;
        if (source.Contains('!'))
        {
            parseTree = parser.fileIn();
        }
        else
        {
            parseTree = parser.statementsEntry();
        }
        
        // Build AST
        var astBuilder = new AstBuilder(source);
        var ast = astBuilder.Build(parseTree);
        
        if (ast == null)
        {
            throw new InvalidOperationException("Failed to build AST");
        }
        
        // Compile to bytecode
        var compiler = new Smalltalk80.Compiler.Compilation.BytecodeCompiler();
        var context = new ProcessorContext("test.st", source);
        var result = compiler.Process(ast, context);
        
        if (!result.Success)
        {
            throw new InvalidOperationException($"Compilation failed: {result.ErrorMessage}");
        }
        
        if (result is BytecodeResult bytecodeResult && bytecodeResult.JsonOutput != null)
        {
            return JObject.Parse(bytecodeResult.JsonOutput);
        }
        
        throw new InvalidOperationException("No bytecode generated");
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
        var classes = (JArray)json["classes"]!;
        Assert.That(classes, Is.Not.Null, "No classes in output");
        Assert.That(classes.Count, Is.GreaterThan(0), "No classes found");
        
        var firstClass = (JObject)classes[0];
        var methods = (JArray)firstClass["methods"]!;
        Assert.That(methods, Is.Not.Null, "No methods in class");
        Assert.That(methods.Count, Is.GreaterThan(0), "No methods found");
        
        return (JObject)methods[0];
    }
    
    /// <summary>
    /// Asserts that the bytecode sequence matches the expected values.
    /// </summary>
    protected void AssertBytecode(JObject method, params int[] expectedBytecodes)
    {
        var bytecodes = (JArray)method["bytecode"]!;
        Assert.That(bytecodes, Is.Not.Null, "No bytecode in method");
        
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
                    // Symbol
                    Assert.That(actual["type"]?.ToString(), Is.EqualTo("Symbol"), 
                        $"Literal {i} should be a Symbol");
                    Assert.That(actual["value"]?.ToString(), Is.EqualTo(str.Substring(1)), 
                        $"Symbol value mismatch at index {i}");
                }
                else if (str.StartsWith("$"))
                {
                    // Character
                    Assert.That(actual["type"]?.ToString(), Is.EqualTo("Character"),
                        $"Literal {i} should be a Character");
                    Assert.That(actual["value"]?.ToString(), Is.EqualTo(str.Substring(1)),
                        $"Character value mismatch at index {i}");
                }
                else
                {
                    // String
                    Assert.That(actual["type"]?.ToString(), Is.EqualTo("String"),
                        $"Literal {i} should be a String");
                    Assert.That(actual["value"]?.ToString(), Is.EqualTo(str),
                        $"String value mismatch at index {i}");
                }
            }
            else if (expected is int intVal)
            {
                Assert.That(actual["type"]?.ToString(), Is.EqualTo("SmallInteger"),
                    $"Literal {i} should be a SmallInteger");
                Assert.That((int)actual["value"]!, Is.EqualTo(intVal),
                    $"Integer value mismatch at index {i}");
            }
            else if (expected is double doubleVal)
            {
                Assert.That(actual["type"]?.ToString(), Is.EqualTo("Float"),
                    $"Literal {i} should be a Float");
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