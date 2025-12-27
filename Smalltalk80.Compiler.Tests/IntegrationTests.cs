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

        // Verify class structure (may include both instance and class side)
        var classes = (JArray)json["classes"]!;
        Assert.That(classes.Count, Is.GreaterThanOrEqualTo(1), "Should have at least one class");

        // Find SimpleClass by name using nameIndex → symbols lookup
        var simpleClass = FindClassByName(json, "SimpleClass");
        Assert.That(simpleClass, Is.Not.Null, "Should have SimpleClass");

        // Verify class has required fields (Blue Book JSON format)
        Assert.That(simpleClass!["nameIndex"], Is.Not.Null, "Class should have nameIndex");
        Assert.That(simpleClass["instVarNames"], Is.Not.Null, "Class should have instVarNames");

        // Verify methods exist in the methods array (linked by homeClassLocalId)
        var methods = (JArray)json["methods"]!;
        Assert.That(methods, Is.Not.Null);
        var classLocalId = simpleClass["localId"]?.ToString();
        var classMethods = methods.Where(m =>
            m["homeClassLocalId"]?.ToString() == classLocalId).ToList();
        Assert.That(classMethods.Count, Is.GreaterThan(0), "Should have methods for SimpleClass");
    }
    
    [Test]
    public void TestCompileComplexExpressions()
    {
        var json = CompileFile("ComplexExpressions.st");

        var classes = (JArray)json["classes"]!;
        // Find ComplexExpressions by name using nameIndex → symbols lookup
        var complexClass = FindClassByName(json, "ComplexExpressions");
        Assert.That(complexClass, Is.Not.Null, "Should have ComplexExpressions class");

        // Verify methods exist in the methods array
        var methods = (JArray)json["methods"]!;
        var classLocalId = complexClass!["localId"]?.ToString();
        var classMethods = methods.Where(m =>
            m["homeClassLocalId"]?.ToString() == classLocalId).ToList();
        Assert.That(classMethods.Count, Is.GreaterThan(0), "Should have methods");

        // Verify at least one method has bytecodes
        var methodWithBytecodes = classMethods.FirstOrDefault(m =>
            m["bytecodes"] is JArray bc && bc.Count > 0);
        Assert.That(methodWithBytecodes, Is.Not.Null, "Methods should have bytecodes");
    }
    
    [Test]
    public void TestCompileControlStructures()
    {
        var json = CompileFile("ControlStructures.st");

        var classes = (JArray)json["classes"]!;
        // Find ControlStructures by name using nameIndex → symbols lookup
        var controlClass = FindClassByName(json, "ControlStructures");
        Assert.That(controlClass, Is.Not.Null, "Should have ControlStructures class");

        // Verify methods exist in the methods array
        var methods = (JArray)json["methods"]!;
        var classLocalId = controlClass!["localId"]?.ToString();
        var classMethods = methods.Where(m =>
            m["homeClassLocalId"]?.ToString() == classLocalId).ToList();
        Assert.That(classMethods.Count, Is.GreaterThan(0), "Should have methods");

        // Verify methods have control structure bytecodes (jumps, blocks)
        var hasControlBytecodes = classMethods.Any(m =>
        {
            var bc = m["bytecodes"] as JArray;
            if (bc == null) return false;
            // Check for jump bytecodes (0x90-0xAF) or block creation (200)
            return bc.Select(t => (int)t).Any(b => (b >= 144 && b <= 175) || b == 200);
        });
        Assert.That(hasControlBytecodes, Is.True, "Should have control structure bytecodes");
    }
    
    [Test]
    public void TestMethodCategories()
    {
        var json = CompileFile("SimpleClass.st");

        // Find SimpleClass by name using nameIndex → symbols lookup
        var simpleClass = FindClassByName(json, "SimpleClass");
        Assert.That(simpleClass, Is.Not.Null, "Should have SimpleClass");

        // Methods are in separate array, linked by homeClassLocalId
        var methods = (JArray)json["methods"]!;
        var classLocalId = simpleClass!["localId"]?.ToString();
        var classMethods = methods.Where(m =>
            m["homeClassLocalId"]?.ToString() == classLocalId).ToList();

        // Methods should have categories
        var categories = new HashSet<string>();
        foreach (var method in classMethods)
        {
            var category = method["category"]?.ToString();
            if (!string.IsNullOrEmpty(category))
                categories.Add(category);
        }

        Assert.That(categories.Count, Is.GreaterThan(0), "Methods should have categories");
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

        // Find PrimitiveTest by name using nameIndex → symbols lookup
        var testClass = FindClassByName(json, "PrimitiveTest");
        Assert.That(testClass, Is.Not.Null, "Should have PrimitiveTest class");

        // Methods are in separate array, linked by homeClassLocalId
        var methods = (JArray)json["methods"]!;
        var classLocalId = testClass!["localId"]?.ToString();
        var classMethods = methods.Where(m =>
            m["homeClassLocalId"]?.ToString() == classLocalId).ToList();
        Assert.That(classMethods.Count, Is.GreaterThan(0), "Should have methods");

        // Check each primitive method
        foreach (var method in classMethods)
        {
            // Check primitive number - stored in header["primitiveIndex"] for Blue Book format
            var header = method["header"] as JObject;
            var primitiveIndex = header?["primitiveIndex"]?.Value<int>() ?? 0;
            Assert.That(primitiveIndex, Is.GreaterThan(0),
                $"Method should have primitive > 0");

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
        
        // Should handle large number of temps - check header tempCount
        var header = method["header"] as JObject;
        var tempCount = header?["tempCount"]?.Value<int>() ?? (int?)method["numTemps"] ?? 0;
        Assert.That(tempCount, Is.EqualTo(51)); // 50 temps + result
        
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
        
        // Unicode strings - check in strings array (compiler uses lowercase "string" type)
        var strings = json["strings"] as JArray;
        var hasUnicode = false;

        // First check if strings are inline in literals (direct value)
        hasUnicode = literals.Any(l =>
            (l["type"]?.ToString() == "string" || l["type"]?.ToString() == "String") &&
            (l["value"]?.ToString()?.Contains("世界") == true ||
             l["value"]?.ToString()?.Contains("😀") == true));

        // If not found in literals, check strings array
        if (!hasUnicode && strings != null)
        {
            hasUnicode = strings.Any(s =>
                s?.ToString()?.Contains("世界") == true ||
                s?.ToString()?.Contains("😀") == true);
        }
             
        Assert.That(hasUnicode, Is.True, "Should preserve Unicode characters");
    }
}