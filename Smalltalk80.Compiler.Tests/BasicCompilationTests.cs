using System.Text.Json;
using NUnit.Framework;
using Antlr4.Runtime;
using Smalltalk80.Compiler.AST;
using Smalltalk80.Compiler.CodeGen;
using Smalltalk80.Compiler.Generated;

namespace Smalltalk80.Compiler.Tests;

/// <summary>
/// Basic compilation tests that verify the compiler generates valid JSON output.
/// These tests work with the current compiler JSON format (v1.0.3).
/// </summary>
[TestFixture]
public class BasicCompilationTests
{
    /// <summary>
    /// Helper to compile Smalltalk source and return parsed JSON.
    /// </summary>
    private static JsonDocument CompileToJson(string source)
    {
        // Create a temporary file for the source (required by ColdStartJsonGenerator)
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, source);
            
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
            var generator = new ColdStartJsonGenerator(tempFile);
            string json = generator.Generate(result);
            
            return JsonDocument.Parse(json);
        }
        finally
        {
            // Clean up temp file
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Helper to wrap a method in a test class.
    /// </summary>
    private static string WrapInTestClass(string methodSource, string className = "TestClass")
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

    [Test]
    public void TestSimpleMethod()
    {
        var source = WrapInTestClass(@"
simpleMethod
    ^42");
        
        using var json = CompileToJson(source);
        var root = json.RootElement;
        
        // Verify metadata
        Assert.That(root.GetProperty("metadata").GetProperty("version").GetString(), Is.EqualTo("1.0.3"));
        Assert.That(root.GetProperty("metadata").GetProperty("methodCount").GetInt32(), Is.EqualTo(1));
        
        // Verify method
        var methods = root.GetProperty("methods");
        Assert.That(methods.GetArrayLength(), Is.EqualTo(1));
        
        var method = methods[0];
        Assert.That(method.GetProperty("selectorIndex").GetInt32(), Is.GreaterThanOrEqualTo(0));
        
        // Verify bytecodes exist
        var bytecodes = method.GetProperty("bytecodes");
        Assert.That(bytecodes.GetArrayLength(), Is.GreaterThan(0));
    }

    [Test]
    public void TestInstanceVariableAccess()
    {
        var source = WrapInTestClass(@"
getValue
    ^x");
        
        using var json = CompileToJson(source);
        var root = json.RootElement;
        
        var method = root.GetProperty("methods")[0];
        var bytecodes = method.GetProperty("bytecodes");
        
        // Should have push instance variable and return
        Assert.That(bytecodes.GetArrayLength(), Is.EqualTo(2));
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(0)); // Push inst var 0 (x)
        Assert.That(bytecodes[1].GetInt32(), Is.EqualTo(124)); // Return top
    }

    [Test]
    public void TestInstanceVariableAssignment()
    {
        var source = WrapInTestClass(@"
setValue: aValue
    x := aValue");
        
        using var json = CompileToJson(source);
        var root = json.RootElement;
        
        var method = root.GetProperty("methods")[0];
        var bytecodes = method.GetProperty("bytecodes");
        
        // Should have push temp, store inst var, return self
        Assert.That(bytecodes.GetArrayLength(), Is.EqualTo(3));
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(16)); // Push temp 0 (aValue)
        Assert.That(bytecodes[1].GetInt32(), Is.EqualTo(96)); // Store inst var 0 (x)
        Assert.That(bytecodes[2].GetInt32(), Is.EqualTo(120)); // Return self
    }

    [Test]
    public void TestBinaryMessage()
    {
        var source = WrapInTestClass(@"
add: aNumber
    ^x + aNumber");
        
        using var json = CompileToJson(source);
        var root = json.RootElement;
        
        var method = root.GetProperty("methods")[0];
        var bytecodes = method.GetProperty("bytecodes");
        
        // Should have push inst var, push temp, send +, return
        Assert.That(bytecodes.GetArrayLength(), Is.EqualTo(4));
        Assert.That(bytecodes[0].GetInt32(), Is.EqualTo(0)); // Push inst var 0 (x)
        Assert.That(bytecodes[1].GetInt32(), Is.EqualTo(16)); // Push temp 0 (aNumber)
        Assert.That(bytecodes[2].GetInt32(), Is.EqualTo(176)); // Send + (arithmetic)
        Assert.That(bytecodes[3].GetInt32(), Is.EqualTo(124)); // Return top
    }

    [Test]
    public void TestBlockCreation()
    {
        var source = WrapInTestClass(@"
blockTest
    ^[42] value");
        
        using var json = CompileToJson(source);
        var root = json.RootElement;
        
        // Should have 2 methods: main method and block
        var methods = root.GetProperty("methods");
        Assert.That(methods.GetArrayLength(), Is.EqualTo(2));
        
        // First method should push block literal and send value
        var mainMethod = methods[0];
        var mainBytecodes = mainMethod.GetProperty("bytecodes");
        
        // Check for literal push (block) and send
        bool hasLiteralPush = false;
        bool hasSend = false;
        
        for (int i = 0; i < mainBytecodes.GetArrayLength(); i++)
        {
            var bc = mainBytecodes[i].GetInt32();
            if (bc >= 0x20 && bc < 0x40) hasLiteralPush = true; // Push literal
            if (bc >= 0xD0 && bc <= 0xFF) hasSend = true; // Send literal (includes 0xFF)
            if (bc >= 0xC0 && bc < 0xD0) hasSend = true; // Special sends
        }
        
        Assert.That(hasLiteralPush, Is.True, "Should push block literal");
        Assert.That(hasSend, Is.True, "Should send value message");
        
        // Second method should be the block (no selector)
        var blockMethod = methods[1];
        Assert.That(blockMethod.TryGetProperty("selectorIndex", out _), Is.False, "Block should not have selectorIndex");
    }

    [Test]
    public void TestClassAndMetaclass()
    {
        var source = WrapInTestClass(@"test ^1", "MyClass");
        
        using var json = CompileToJson(source);
        var root = json.RootElement;
        
        // Should have 2 classes: regular and metaclass
        var classes = root.GetProperty("classes");
        Assert.That(classes.GetArrayLength(), Is.EqualTo(2));
        
        var regularClass = classes[0];
        var metaclass = classes[1];
        
        Assert.That(regularClass.GetProperty("isMetaclass").GetBoolean(), Is.False);
        Assert.That(metaclass.GetProperty("isMetaclass").GetBoolean(), Is.True);
        
        // Check symbols array contains MyClass
        var symbols = root.GetProperty("symbols");
        bool hasMyClass = false;
        for (int i = 0; i < symbols.GetArrayLength(); i++)
        {
            if (symbols[i].GetString() == "MyClass")
            {
                hasMyClass = true;
                break;
            }
        }
        Assert.That(hasMyClass, Is.True, "Symbols should contain MyClass");
    }

    [Test]
    public void TestSpecialConstants()
    {
        var source = WrapInTestClass(@"
constants
    ^{nil. true. false. self. 0. 1. 2. -1}");
        
        using var json = CompileToJson(source);
        var root = json.RootElement;
        
        var method = root.GetProperty("methods")[0];
        var bytecodes = method.GetProperty("bytecodes");
        
        // Should contain push special constants bytecodes (0x70-0x77)
        bool hasSpecialConstants = false;
        for (int i = 0; i < bytecodes.GetArrayLength(); i++)
        {
            var bc = bytecodes[i].GetInt32();
            if (bc >= 0x70 && bc <= 0x77)
            {
                hasSpecialConstants = true;
                break;
            }
        }
        
        Assert.That(hasSpecialConstants, Is.True, "Should use special constant bytecodes");
    }

    [Test]
    public void TestMethodHeader()
    {
        var source = WrapInTestClass(@"
methodWithArgs: arg1 and: arg2
    | temp1 temp2 |
    temp1 := arg1.
    temp2 := arg2.
    ^temp1 + temp2");
        
        using var json = CompileToJson(source);
        var root = json.RootElement;
        
        var method = root.GetProperty("methods")[0];
        var header = method.GetProperty("header");
        
        // Verify header fields
        Assert.That(header.GetProperty("argCount").GetInt32(), Is.EqualTo(2));
        Assert.That(header.GetProperty("tempCount").GetInt32(), Is.EqualTo(2));
        Assert.That(header.GetProperty("literalCount").GetInt32(), Is.EqualTo(0));
        Assert.That(header.GetProperty("primitiveIndex").GetInt32(), Is.EqualTo(0));
        Assert.That(header.GetProperty("largeContext").GetBoolean(), Is.False);
    }

    [Test]
    public void TestInstallOrder()
    {
        var source = WrapInTestClass(@"test ^1");
        
        using var json = CompileToJson(source);
        var root = json.RootElement;
        
        // Should have install order phases
        var installOrder = root.GetProperty("installOrder");
        var phases = installOrder.GetProperty("phases");
        
        Assert.That(phases.GetArrayLength(), Is.GreaterThan(0));
        
        bool hasClasses = false;
        bool hasMethods = false;
        bool hasPatches = false;
        
        for (int i = 0; i < phases.GetArrayLength(); i++)
        {
            var phase = phases[i];
            var kind = phase.GetProperty("kind").GetString();
            if (kind == "classes") hasClasses = true;
            if (kind == "methods") hasMethods = true;
            if (kind == "patches") hasPatches = true;
        }
        
        Assert.That(hasClasses, Is.True, "Should have classes phase");
        Assert.That(hasMethods, Is.True, "Should have methods phase");
        Assert.That(hasPatches, Is.True, "Should have patches phase");
    }

    [Test]
    public void TestPrimitiveMethod()
    {
        var source = @"
Object subclass: #TestClass
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!

!TestClass methodsFor: 'primitives'!

basicAt: index
    <primitive: 60>
    ^self primitiveFailure
! !
";
        
        using var json = CompileToJson(source);
        var root = json.RootElement;
        
        var method = root.GetProperty("methods")[0];
        var header = method.GetProperty("header");
        
        // Should have primitive 60
        Assert.That(header.GetProperty("primitiveIndex").GetInt32(), Is.EqualTo(60));
        
        // Should have fallback code
        var bytecodes = method.GetProperty("bytecodes");
        Assert.That(bytecodes.GetArrayLength(), Is.GreaterThan(0), "Should have fallback bytecodes");
    }
}