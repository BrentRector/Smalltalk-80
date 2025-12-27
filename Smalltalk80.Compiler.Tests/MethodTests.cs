using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace Smalltalk80.Compiler.Tests;

/// <summary>
/// Tests for method compilation including returns, temporaries, primitives, and complex expressions.
/// </summary>
[TestFixture]
public class MethodTests : CompilerTestBase
{
    [Test]
    public void TestSimpleReturn()
    {
        var source = WrapInTestClass(@"
testSimpleReturn
    ^42");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push 42, return top
        AssertBytecode(method, 32, 124);
        
        // Check method metadata - use selectorIndex and header for Blue Book format
        Assert.That(GetMethodSelector(json, method), Is.EqualTo("testSimpleReturn"));
        var header = method["header"] as JObject;
        Assert.That(header?["argCount"]?.Value<int>() ?? (int?)method["numArgs"] ?? 0, Is.EqualTo(0));
        Assert.That(header?["tempCount"]?.Value<int>() ?? (int?)method["numTemps"] ?? 0, Is.EqualTo(0));
    }
    
    [Test]
    public void TestImplicitReturn()
    {
        var source = WrapInTestClass(@"
testImplicitReturn
    x := 42");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Methods without explicit return should return self (bytecode 120)
        var bytecodes = (JArray)method["bytecodes"]!;
        var lastBytecode = (int)bytecodes[bytecodes.Count - 1];
        Assert.That(lastBytecode, Is.EqualTo(120), "Should end with return self");
    }
    
    [Test]
    public void TestReturnSelf()
    {
        var source = WrapInTestClass(@"
testReturnSelf
    ^self");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Optimized return self (bytecode 120)
        AssertBytecode(method, 120);
    }
    
    [Test]
    public void TestReturnTrue()
    {
        var source = WrapInTestClass(@"
testReturnTrue
    ^true");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Optimized return true (bytecode 121)
        AssertBytecode(method, 121);
    }
    
    [Test]
    public void TestReturnFalse()
    {
        var source = WrapInTestClass(@"
testReturnFalse
    ^false");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Optimized return false (bytecode 122)
        AssertBytecode(method, 122);
    }
    
    [Test]
    public void TestReturnNil()
    {
        var source = WrapInTestClass(@"
testReturnNil
    ^nil");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Optimized return nil (bytecode 123)
        AssertBytecode(method, 123);
    }
    
    [Test]
    public void TestMethodWithTemporaries()
    {
        var source = WrapInTestClass(@"
testTemporaries
    | temp1 temp2 |
    temp1 := 10.
    temp2 := 20.
    ^temp1 + temp2");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Check temp count - use header for Blue Book format
        var header = method["header"] as JObject;
        Assert.That(header?["tempCount"]?.Value<int>() ?? (int?)method["numTemps"] ?? 0, Is.EqualTo(2));
        
        // Should store and access temporaries
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(104), Is.True, "Should store to temp1");
        Assert.That(bytecodes.Select(t => (int)t).Contains(105), Is.True, "Should store to temp2");
    }
    
    [Test]
    public void TestMethodWithParameters()
    {
        var source = WrapInTestClass(@"
add: a to: b
    ^a + b");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Check parameter count - use header for Blue Book format
        var header = method["header"] as JObject;
        Assert.That(header?["argCount"]?.Value<int>() ?? (int?)method["numArgs"] ?? 0, Is.EqualTo(2));
        Assert.That(GetMethodSelector(json, method), Is.EqualTo("add:to:"));
        
        // Parameters accessed as temps 0 and 1
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(16), Is.True, "Should push first param");
        Assert.That(bytecodes.Select(t => (int)t).Contains(17), Is.True, "Should push second param");
    }
    
    [Test]
    public void TestPrimitiveMethod()
    {
        var source = WrapInTestClass(@"
testPrimitive
    <primitive: 1>
    ^self primitiveFailed");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Check primitive number - stored in header["primitiveIndex"] for Blue Book format
        var header = method["header"] as JObject;
        var primitiveIndex = header?["primitiveIndex"]?.Value<int>() ?? (int?)method["primitive"] ?? 0;
        Assert.That(primitiveIndex, Is.EqualTo(1), "Should have primitive 1");

        // Should have fallback code
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Count, Is.GreaterThan(0), "Should have fallback bytecode");
    }

    [Test]
    public void TestPrimitiveWithoutFallback()
    {
        var source = WrapInTestClass(@"
testPrimitiveNoFallback
    <primitive: 60>");

        var json = CompileSource(source);
        var method = GetFirstMethod(json);

        // Check primitive number - stored in header["primitiveIndex"] for Blue Book format
        var header = method["header"] as JObject;
        var primitiveIndex = header?["primitiveIndex"]?.Value<int>() ?? (int?)method["primitive"] ?? 0;
        Assert.That(primitiveIndex, Is.EqualTo(60), "Should have primitive 60");

        // Might have minimal bytecode or just return self
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);
    }
    
    [Test]
    public void TestComplexExpression()
    {
        var source = WrapInTestClass(@"
testComplex
    | result |
    result := (3 + 4) * 2.
    result := result - 1.
    ^result negated");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Should have multiple operations
        var bytecodes = (JArray)method["bytecodes"]!;
        
        // Check for arithmetic operations
        var arithmeticOps = bytecodes.Select(t => (int)t).Count(b => b >= 176 && b <= 191);
        Assert.That(arithmeticOps, Is.GreaterThan(0), "Should have arithmetic operations");
    }
    
    [Test]
    public void TestMethodWithComment()
    {
        var source = WrapInTestClass(@"
testWithComment
    ""This is a method comment""
    ^42");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Comments don't affect bytecode
        AssertBytecode(method, 32, 124);
    }
    
    [Test]
    public void TestLongMethod()
    {
        var source = WrapInTestClass(@"
testLongMethod
    | a b c d e f g h i j |
    a := 1.
    b := 2.
    c := 3.
    d := 4.
    e := 5.
    f := 6.
    g := 7.
    h := 8.
    i := 9.
    j := 10.
    ^a + b + c + d + e + f + g + h + i + j");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Check large number of temps - use header for Blue Book format
        var header = method["header"] as JObject;
        Assert.That(header?["tempCount"]?.Value<int>() ?? (int?)method["numTemps"] ?? 0, Is.EqualTo(10));
        
        // Should have extended temp access if needed
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Count, Is.GreaterThan(20), "Should have many bytecodes");
    }
    
    [Test]
    public void TestUnaryMethod()
    {
        var source = WrapInTestClass(@"
negated
    ^self * -1");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        Assert.That(GetMethodSelector(json, method), Is.EqualTo("negated"));
        var header = method["header"] as JObject;
        Assert.That(header?["argCount"]?.Value<int>() ?? (int?)method["numArgs"] ?? 0, Is.EqualTo(0));
    }
    
    [Test]
    public void TestBinaryMethod()
    {
        var source = WrapInTestClass(@"
+ other
    ^self addWith: other");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        Assert.That(GetMethodSelector(json, method), Is.EqualTo("+"));
        var header = method["header"] as JObject;
        Assert.That(header?["argCount"]?.Value<int>() ?? (int?)method["numArgs"] ?? 0, Is.EqualTo(1));
    }
    
    [Test]
    public void TestKeywordMethod()
    {
        var source = WrapInTestClass(@"
at: index put: value
    ^self basicAt: index put: value");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        Assert.That(GetMethodSelector(json, method), Is.EqualTo("at:put:"));
        var header = method["header"] as JObject;
        Assert.That(header?["argCount"]?.Value<int>() ?? (int?)method["numArgs"] ?? 0, Is.EqualTo(2));
    }
    
    [Test]
    public void TestRecursiveMethod()
    {
        var source = WrapInTestClass(@"
factorial
    self <= 1 ifTrue: [^1].
    ^self * (self - 1) factorial");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Should have recursive call to factorial (symbol in literals)
        Assert.That(LiteralContainsSymbol(json, method, "factorial"), Is.True);
    }
    
    [Test]
    public void TestMethodWithMultipleReturns()
    {
        var source = WrapInTestClass(@"
testMultipleReturns
    x < 0 ifTrue: [^'negative'].
    x = 0 ifTrue: [^'zero'].
    x > 0 ifTrue: [^'positive'].
    ^'unknown'");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Should have multiple return bytecodes
        var bytecodes = (JArray)method["bytecodes"]!;
        var returnCount = bytecodes.Select(t => (int)t).Count(b => b == 124);
        Assert.That(returnCount, Is.GreaterThanOrEqualTo(4), "Should have multiple returns");
    }
    
    [Test]
    public void TestAccessor()
    {
        var source = WrapInTestClass(@"
x
    ^x");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Simple accessor - push instance var, return
        AssertBytecode(method, 0, 124);
    }
    
    [Test]
    public void TestMutator()
    {
        var source = WrapInTestClass(@"
x: aValue
    x := aValue");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Mutator - push param, store in instance var, return self
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes.Select(t => (int)t).Contains(16), Is.True, "Should push parameter");
        Assert.That(bytecodes.Select(t => (int)t).Contains(96), Is.True, "Should store in instance var");
        Assert.That((int)bytecodes[bytecodes.Count - 1], Is.EqualTo(120), "Should return self");
    }
}