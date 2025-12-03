using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace Smalltalk80.Compiler.Tests;

/// <summary>
/// Tests for variable access including instance, class, temporary, block parameters, and special variables.
/// </summary>
[TestFixture]
public class VariableTests : CompilerTestBase
{
    [Test]
    public void TestInstanceVariableRead()
    {
        var source = WrapInTestClass(@"
testInstanceVarRead
    ^x");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push receiver variable 0 (x), return top
        AssertBytecode(method, 0, 124);
    }
    
    [Test]
    public void TestInstanceVariableWrite()
    {
        var source = WrapInTestClass(@"
testInstanceVarWrite
    x := 42.
    ^x");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push literal 42, pop and store in receiver variable 0, push receiver variable 0, return top
        AssertBytecode(method, 32, 96, 0, 124);
    }
    
    [Test]
    public void TestMultipleInstanceVariables()
    {
        var source = WrapInTestClass(@"
testMultipleInstanceVars
    x := 1.
    y := 2.
    z := 3.
    ^x + y + z");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // This will involve storing to different instance variables and binary sends
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);
        
        // Verify stores to different instance variables
        Assert.That(bytecodes.Select(t => (int)t).Contains(96), Is.True, "Should store to x (index 0)");
        Assert.That(bytecodes.Select(t => (int)t).Contains(97), Is.True, "Should store to y (index 1)");
        Assert.That(bytecodes.Select(t => (int)t).Contains(98), Is.True, "Should store to z (index 2)");
    }
    
    [Test]
    public void TestTemporaryVariable()
    {
        var source = WrapInTestClass(@"
testTemporaryVar
    | temp |
    temp := 100.
    ^temp");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push literal 100, pop and store in temp 0, push temp 0, return top
        // Temporary variables use bytecodes 104-111 for store, 16-31 for push
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);
        
        // Should contain store to temp and push temp
        Assert.That(bytecodes.Select(t => (int)t).Contains(104), Is.True, "Should store to temp 0");
        Assert.That(bytecodes.Select(t => (int)t).Contains(16), Is.True, "Should push temp 0");
    }
    
    [Test]
    public void TestMultipleTemporaries()
    {
        var source = WrapInTestClass(@"
testMultipleTemps
    | temp1 temp2 temp3 |
    temp1 := 10.
    temp2 := 20.
    temp3 := temp1 + temp2.
    ^temp3");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);
        
        // Verify stores to different temporaries
        Assert.That(bytecodes.Select(t => (int)t).Contains(104), Is.True, "Should store to temp1");
        Assert.That(bytecodes.Select(t => (int)t).Contains(105), Is.True, "Should store to temp2");
        Assert.That(bytecodes.Select(t => (int)t).Contains(106), Is.True, "Should store to temp3");
    }
    
    [Test]
    public void TestMethodParameter()
    {
        var source = WrapInTestClass(@"
testMethodParam: aParam
    ^aParam");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Method parameters are accessed like temporaries
        // Push temp 0 (first parameter), return top
        AssertBytecode(method, 16, 124);
    }
    
    [Test]
    public void TestMultipleParameters()
    {
        var source = WrapInTestClass(@"
testMultipleParams: param1 with: param2 and: param3
    ^param1 + param2 + param3");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);
        
        // Parameters are temps 0, 1, 2
        Assert.That(bytecodes.Select(t => (int)t).Contains(16), Is.True, "Should push param1");
        Assert.That(bytecodes.Select(t => (int)t).Contains(17), Is.True, "Should push param2");
        Assert.That(bytecodes.Select(t => (int)t).Contains(18), Is.True, "Should push param3");
    }
    
    [Test]
    public void TestSelf()
    {
        var source = WrapInTestClass(@"
testSelf
    ^self");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push receiver (self) is bytecode 112, return top
        AssertBytecode(method, 112, 124);
    }
    
    [Test]
    public void TestSelfMethodCall()
    {
        var source = WrapInTestClass(@"
testSelfCall
    ^self someMethod");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push receiver, send someMethod, return top
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);
        AssertBytecodeAt(bytecodes, 0, 112, "Push self");
    }
    
    [Test]
    public void TestSuper()
    {
        var source = WrapInTestClass(@"
testSuper
    ^super");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push receiver (super uses same bytecode as self for push)
        // The difference is in the send bytecode
        AssertBytecode(method, 112, 124);
    }
    
    [Test]
    public void TestSuperMethodCall()
    {
        var source = WrapInTestClass(@"
testSuperCall
    ^super initialize");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Push receiver, super send initialize, return top
        // Super sends use bytecodes 133-134
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);
        AssertBytecodeAt(bytecodes, 0, 112, "Push self for super");
    }
    
    [Test]
    public void TestThisContext()
    {
        var source = WrapInTestClass(@"
testThisContext
    ^thisContext");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // thisContext might be a special push or a variable reference
        // It's typically bytecode 137 (push active context)
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);
        Assert.That(bytecodes.Count, Is.GreaterThan(0));
    }
    
    [Test]
    public void TestBlockParameter()
    {
        var source = WrapInTestClass(@"
testBlockParam
    ^[:param | param * 2] value: 5");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Block creation and parameter access
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);
        
        // Should contain block bytecode (143) and block parameter access
        Assert.That(bytecodes.Select(t => (int)t).Any(b => b == 143), Is.True, "Should contain block creation");
    }
    
    [Test]
    public void TestBlockTemporary()
    {
        var source = WrapInTestClass(@"
testBlockTemp
    ^[| blockTemp | blockTemp := 42. blockTemp] value");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Block with temporary variable
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);
        
        // Should contain block creation
        Assert.That(bytecodes.Select(t => (int)t).Any(b => b == 143), Is.True, "Should contain block creation");
    }
    
    [Test]
    public void TestClassVariable()
    {
        var source = WrapInTestClass(@"
testClassVar
    ClassVar1 := 'test'.
    ^ClassVar1");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Class variables are accessed through associations in the literal frame
        // Push literal variable, store, push literal variable, return
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);
        
        // Class variables use bytecodes 64-95 for push literal variable
        var hasClassVarAccess = bytecodes.Select(t => (int)t).Any(b => b >= 64 && b <= 95);
        Assert.That(hasClassVarAccess, Is.True, "Should access class variable");
    }
    
    [Test]
    public void TestGlobalVariable()
    {
        var source = WrapInTestClass(@"
testGlobalVar
    ^Smalltalk");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // Global variables are accessed like class variables
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);
        
        // Should push global variable
        var hasGlobalAccess = bytecodes.Select(t => (int)t).Any(b => b >= 64 && b <= 95);
        Assert.That(hasGlobalAccess, Is.True, "Should access global variable");
    }
    
    [Test]
    public void TestVariableShadowing()
    {
        var source = WrapInTestClass(@"
testShadowing: x
    ""Parameter x shadows instance variable x""
    | y |
    y := x.  ""Should access parameter, not instance variable""
    ^y");
        
        var json = CompileSource(source);
        var method = GetFirstMethod(json);
        
        // The parameter x (temp 0) should be accessed, not instance var x
        var bytecodes = (JArray)method["bytecodes"]!;
        Assert.That(bytecodes, Is.Not.Null);
        
        // Should push temp 0 (parameter), not receiver variable 0
        Assert.That(bytecodes.Select(t => (int)t).Contains(16), Is.True, "Should access parameter x");
        Assert.That(bytecodes.Select(t => (int)t).Contains(0), Is.False, "Should not access instance var x");
    }
}