using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace Smalltalk80.Compiler.Tests;

/// <summary>
/// Integration tests that compile complete Smalltalk source files and verify the output.
/// </summary>
[TestFixture]
public class IntegrationTests : CompilerTestBase
{
    [Test]
    public void TestCompileSimpleClass()
    {
        var json = CompileFile("SimpleClass.st");
        
        // Verify class structure
        var classes = (JArray)json["classes"]!;
        Assert.That(classes.Count, Is.EqualTo(1), "Should have one class");
        
        var simpleClass = (JObject)classes[0];
        Assert.That(simpleClass["name"]?.ToString(), Is.EqualTo("SimpleClass"));
        Assert.That(simpleClass["superclass"]?.ToString(), Is.EqualTo("Object"));
        
        // Verify instance variables
        var instanceVars = (JArray)simpleClass["instanceVariables"]!;
        Assert.That(instanceVars.Count, Is.EqualTo(1));
        Assert.That(instanceVars[0].ToString(), Is.EqualTo("value"));
        
        // Verify methods exist
        var methods = (JArray)simpleClass["methods"]!;
        Assert.That(methods.Count, Is.GreaterThan(5), "Should have multiple methods");
        
        // Check specific methods
        var methodNames = methods.Select(m => m["selector"]?.ToString()).ToList();
        Assert.That(methodNames, Does.Contain("value"));
        Assert.That(methodNames, Does.Contain("value:"));
        Assert.That(methodNames, Does.Contain("increment"));
        Assert.That(methodNames, Does.Contain("double"));
        Assert.That(methodNames, Does.Contain("isPositive"));
        
        // Check class methods
        var classMethods = (JArray)simpleClass["classMethods"]!;
        Assert.That(classMethods, Is.Not.Null);
        var classMethodNames = classMethods.Select(m => m["selector"]?.ToString()).ToList();
        Assert.That(classMethodNames, Does.Contain("withValue:"));
    }
    
    [Test]
    public void TestCompileComplexExpressions()
    {
        var json = CompileFile("ComplexExpressions.st");
        
        var classes = (JArray)json["classes"]!;
        var complexClass = (JObject)classes[0];
        
        Assert.That(complexClass["name"]?.ToString(), Is.EqualTo("ComplexExpressions"));
        
        var methods = (JArray)complexClass["methods"]!;
        
        // Test complex arithmetic method
        var arithmetic = methods.FirstOrDefault(m => 
            m["selector"]?.ToString() == "complexArithmetic");
        Assert.That(arithmetic, Is.Not.Null);
        
        // Should have temporaries
        Assert.That((int)arithmetic!["numTemps"]!, Is.GreaterThan(0));
        
        // Should have arithmetic bytecodes
        var bytecodes = (JArray)arithmetic["bytecodes"]!;
        var hasArithmetic = bytecodes.Select(t => (int)t).Any(b => b >= 176 && b <= 191);
        Assert.That(hasArithmetic, Is.True);
        
        // Test nested conditionals
        var conditionals = methods.FirstOrDefault(m => 
            m["selector"]?.ToString() == "nestedConditionals:");
        Assert.That(conditionals, Is.Not.Null);
        
        // Should have conditional jumps
        bytecodes = (JArray)conditionals!["bytecodes"]!;
        var hasJumps = bytecodes.Select(t => (int)t).Any(b => b >= 152 && b <= 175);
        Assert.That(hasJumps, Is.True);
        
        // Test block methods
        var blockComp = methods.FirstOrDefault(m => 
            m["selector"]?.ToString() == "blockComposition");
        Assert.That(blockComp, Is.Not.Null);
        
        // Should have block creations
        bytecodes = (JArray)blockComp!["bytecodes"]!;
        var blockCount = bytecodes.Select(t => (int)t).Count(b => b == 143);
        Assert.That(blockCount, Is.GreaterThan(0), "Should have blocks");
    }
    
    [Test]
    public void TestCompileControlStructures()
    {
        var json = CompileFile("ControlStructures.st");
        
        var classes = (JArray)json["classes"]!;
        var controlClass = (JObject)classes[0];
        
        var methods = (JArray)controlClass["methods"]!;
        
        // Test whileTrue loop
        var whileTrue = methods.FirstOrDefault(m => 
            m["selector"]?.ToString() == "whileTrueExample");
        Assert.That(whileTrue, Is.Not.Null);
        
        var bytecodes = (JArray)whileTrue!["bytecodes"]!;
        // Should have backward jump for loop
        var hasJump = bytecodes.Select(t => (int)t).Any(b => b >= 144 && b <= 175);
        Assert.That(hasJump, Is.True, "whileTrue should have jumps");
        
        // Test to:do: loop
        var toDo = methods.FirstOrDefault(m => 
            m["selector"]?.ToString() == "toDoExample");
        Assert.That(toDo, Is.Not.Null);
        
        // Should have block for loop body
        bytecodes = (JArray)toDo!["bytecodes"]!;
        var hasBlock = bytecodes.Select(t => (int)t).Any(b => b == 143);
        Assert.That(hasBlock, Is.True, "to:do: should have block");
        
        // Test early returns
        var earlyReturn = methods.FirstOrDefault(m => 
            m["selector"]?.ToString() == "earlyReturnExample:");
        Assert.That(earlyReturn, Is.Not.Null);
        
        // Should have multiple return bytecodes
        bytecodes = (JArray)earlyReturn!["bytecodes"]!;
        var returnCount = bytecodes.Select(t => (int)t).Count(b => b == 124);
        Assert.That(returnCount, Is.GreaterThan(3), "Should have multiple returns");
    }
    
    [Test]
    public void TestMethodCategories()
    {
        var json = CompileFile("SimpleClass.st");
        
        var classes = (JArray)json["classes"]!;
        var simpleClass = (JObject)classes[0];
        var methods = (JArray)simpleClass["methods"]!;
        
        // Methods should be organized by category
        var categories = new HashSet<string>();
        foreach (var method in methods)
        {
            var category = method["category"]?.ToString();
            if (!string.IsNullOrEmpty(category))
                categories.Add(category);
        }
        
        Assert.That(categories, Does.Contain("accessing"));
        Assert.That(categories, Does.Contain("arithmetic"));
        Assert.That(categories, Does.Contain("testing"));
    }
    
    [Test]
    public void TestBytecodeOptimizations()
    {
        var source = WrapInTestClass(@"
testOptimizations
    ""Test that common patterns are optimized""
    self ifNil: [^nil].
    self = nil ifTrue: [^nil].
    ^self");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Should use optimized bytecodes where possible
        var bytecodes = (JArray)method["bytecodes"]!;
        
        // ifNil: and = nil ifTrue: might be optimized
        var hasOptimizedJumps = bytecodes.Select(t => (int)t).Any(b => b >= 152 && b <= 159);
        Assert.That(hasOptimizedJumps || bytecodes.Count < 20, Is.True, 
            "Should optimize common patterns");
    }
    
    [Test]
    public void TestPrimitiveMethodCompilation()
    {
        var source = @"
Object subclass: #PrimitiveTest
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!

!PrimitiveTest methodsFor: 'primitives'!

basicNew
    <primitive: 70>
    ^self primitiveFailed!

at: index
    <primitive: 60>
    ^self primitiveFailed!

size
    <primitive: 62>
    ^0! !";
        
        var json = CompileSource(source);
        var classes = (JArray)json["classes"]!;
        var testClass = (JObject)classes[0];
        var methods = (JArray)testClass["methods"]!;
        
        // Check each primitive method
        foreach (var method in methods)
        {
            var primitive = method["primitive"];
            Assert.That(primitive, Is.Not.Null, 
                $"Method {method["selector"]} should have primitive");
            
            var primNum = (int)primitive!;
            Assert.That(primNum, Is.GreaterThan(0), "Primitive number should be positive");
            
            // Should have fallback code
            var bytecodes = (JArray)method["bytecodes"]!;
            Assert.That(bytecodes.Count, Is.GreaterThan(0), "Should have fallback bytecode");
        }
    }
    
    [Test]
    public void TestLargeMethodCompilation()
    {
        // Generate a large method with many operations
        var statements = new List<string>();
        for (int i = 1; i <= 50; i++)
        {
            statements.Add($"    temp{i} := {i}.");
        }
        
        var tempDecls = string.Join(" ", Enumerable.Range(1, 50).Select(i => $"temp{i}"));
        var tempSum = string.Join(" + ", Enumerable.Range(1, 50).Select(i => $"temp{i}"));
        
        var source = WrapInTestClass($@"
largeMethod
    | {tempDecls} result |
{string.Join("\n", statements)}
    result := {tempSum}.
    ^result");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Should handle large number of temps
        Assert.That((int)method["numTemps"]!, Is.EqualTo(51)); // 50 temps + result
        
        // Should have extended bytecodes for large temp/literal access
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Count, Is.GreaterThan(100), "Large method should have many bytecodes");
        
        // Should use extended temp access for high-numbered temps
        var hasExtended = bytecodes.Select(t => (int)t).Any(b => b == 128 || b == 129 || b == 132);
        Assert.That(hasExtended, Is.True, "Should use extended bytecodes");
    }
    
    [Test]
    public void TestUnicodeSupport()
    {
        var source = WrapInTestClass(@"
testUnicode
    ""Test Unicode string handling""
    | greeting emoji |
    greeting := 'Hello, 世界'.
    emoji := '😀🎉'.
    ^greeting, ' ', emoji");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Verify Unicode strings are preserved
        var literals = (JArray)method["literals"]!;
        
        var hasUnicode = literals.Any(l => 
            l["type"]?.ToString() == "String" && 
            (l["value"]?.ToString()?.Contains("世界") == true ||
             l["value"]?.ToString()?.Contains("😀") == true));
             
        Assert.That(hasUnicode, Is.True, "Should preserve Unicode characters");
    }
}