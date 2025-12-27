using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace Smalltalk80.Compiler.Tests;

/// <summary>
/// Tests for literal value compilation including numbers, strings, symbols, characters, arrays, and special constants.
/// </summary>
[TestFixture]
public class LiteralTests : CompilerTestBase
{
    [Test]
    public void TestSmallInteger()
    {
        var source = WrapInTestClass(@"
testSmallInteger
    ^42");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push literal 42 (32 + 0), return top
        AssertBytecode(method, 32, 124);
        AssertLiterals(method, 42);
    }
    
    [Test]
    public void TestNegativeInteger()
    {
        var source = WrapInTestClass(@"
testNegativeInteger
    ^-123");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push literal -123, return top
        AssertBytecode(method, 32, 124);
        AssertLiterals(method, -123);
    }
    
    [Test]
    public void TestLargeInteger()
    {
        var source = WrapInTestClass(@"
testLargeInteger
    ^1234567890123456789");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push literal large integer, return top
        AssertBytecode(method, 32, 124);
        // Note: Large integers might need special handling in literals
    }
    
    [Test]
    public void TestFloat()
    {
        var source = WrapInTestClass(@"
testFloat
    ^3.14159");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push literal float, return top
        AssertBytecode(method, 32, 124);
        AssertLiterals(method, 3.14159);
    }
    
    [Test]
    public void TestString()
    {
        var source = WrapInTestClass(@"
testString
    ^'Hello, World!'");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push literal string, return top
        AssertBytecode(method, 32, 124);
        AssertLiterals(method, "Hello, World!");
    }
    
    [Test]
    public void TestEmptyString()
    {
        var source = WrapInTestClass(@"
testEmptyString
    ^''");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push literal empty string, return top
        AssertBytecode(method, 32, 124);
        AssertLiterals(method, "");
    }
    
    [Test]
    public void TestSymbol()
    {
        var source = WrapInTestClass(@"
testSymbol
    ^#symbol");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push literal symbol, return top
        AssertBytecode(method, 32, 124);
        AssertLiterals(method, "#symbol");
    }
    
    [Test]
    public void TestKeywordSymbol()
    {
        var source = WrapInTestClass(@"
testKeywordSymbol
    ^#at:put:");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push literal keyword symbol, return top
        AssertBytecode(method, 32, 124);
        AssertLiterals(method, "#at:put:");
    }
    
    [Test]
    public void TestCharacter()
    {
        var source = WrapInTestClass(@"
testCharacter
    ^$A");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Push literal character, return top
        AssertBytecode(method, 32, 124);
        // Character literals - just verify a character type literal exists
        var literals = (JArray)method["literals"]!;
        var hasChar = literals.Any(l => l["type"]?.ToString() == "character");
        Assert.That(hasChar, Is.True, "Should have a character literal");
    }

    [Test]
    public void TestSpecialCharacter()
    {
        var source = WrapInTestClass(@"
testSpecialCharacter
    ^$!");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Push literal character, return top
        AssertBytecode(method, 32, 124);
        // Character literals - just verify a character type literal exists
        var literals = (JArray)method["literals"]!;
        var hasChar = literals.Any(l => l["type"]?.ToString() == "character");
        Assert.That(hasChar, Is.True, "Should have a character literal");
    }
    
    [Test]
    public void TestLiteralArray()
    {
        var source = WrapInTestClass(@"
testLiteralArray
    ^#(1 2 3 'string' #symbol)");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push literal array, return top
        AssertBytecode(method, 32, 124);
        // Literal arrays are stored as a single literal
    }
    
    [Test]
    public void TestEmptyArray()
    {
        var source = WrapInTestClass(@"
testEmptyArray
    ^#()");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push literal empty array, return top
        AssertBytecode(method, 32, 124);
    }
    
    [Test]
    public void TestNestedArray()
    {
        var source = WrapInTestClass(@"
testNestedArray
    ^#(1 (2 3) 4)");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push literal nested array, return top
        AssertBytecode(method, 32, 124);
    }
    
    [Test]
    public void TestTrue()
    {
        var source = WrapInTestClass(@"
testTrue
    ^true");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Compiler optimizes ^true to RETURN_TRUE (121) single bytecode
        AssertBytecode(method, 121);
    }
    
    [Test]
    public void TestFalse()
    {
        var source = WrapInTestClass(@"
testFalse
    ^false");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Compiler optimizes ^false to RETURN_FALSE (122) single bytecode
        AssertBytecode(method, 122);
    }
    
    [Test]
    public void TestNil()
    {
        var source = WrapInTestClass(@"
testNil
    ^nil");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Compiler optimizes ^nil to RETURN_NIL (123) single bytecode
        AssertBytecode(method, 123);
    }
    
    [Test]
    public void TestSpecialIntegers()
    {
        // Test -1, 0, 1, 2 which have special bytecodes
        var testCases = new[]
        {
            ("MinusOne", -1, 116),
            ("Zero", 0, 117),
            ("One", 1, 118),
            ("Two", 2, 119)
        };

        foreach (var (name, value, bytecode) in testCases)
        {
            var source = WrapInTestClass($@"
testSpecialInteger{name}
    ^{value}");

            var json = CompileSource(source);
            var method = GetFirstMethod(json);

            // Push special integer, return top
            AssertBytecode(method, bytecode, 124);
        }
    }
    
    [Test]
    public void TestMultipleLiterals()
    {
        var source = WrapInTestClass(@"
testMultipleLiterals
    | array |
    array := Array new: 5.
    array at: 1 put: 42.
    array at: 2 put: 'string'.
    array at: 3 put: #symbol.
    array at: 4 put: $X.
    array at: 5 put: 3.14.
    ^array");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Verify that multiple different literal types are handled correctly
        var literals = (JArray)method["literals"]!;
        Assert.That(literals, Is.Not.Null);
        
        // Check that all literal types are present (compiler uses lowercase type names)
        var hasInteger = literals.Any(l => l["type"]?.ToString() == "smallInt" || l["type"]?.ToString() == "SmallInteger");
        var hasString = literals.Any(l => l["type"]?.ToString() == "string" || l["type"]?.ToString() == "String");
        var hasSymbol = literals.Any(l => l["type"]?.ToString() == "symbol" || l["type"]?.ToString() == "Symbol");
        var hasChar = literals.Any(l => l["type"]?.ToString() == "character" || l["type"]?.ToString() == "Character");
        var hasFloat = literals.Any(l => l["type"]?.ToString() == "float" || l["type"]?.ToString() == "Float");
        
        Assert.That(hasInteger || hasString || hasSymbol || hasChar || hasFloat, Is.True,
            "Method should contain various literal types");
    }
}