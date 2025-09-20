using System;
using System.Collections.Generic;
using StandaloneCompiler;

/// <summary>
/// Test program to verify proper handling of literal variables according to Blue Book specification.
/// Tests that the compiler correctly distinguishes between:
/// - PushLiteralConstant (for literal objects)
/// - PushLiteralVariable (for Associations - globals, class vars, pool vars)
/// </summary>
class TestLiteralVariables
{
    static void Main()
    {
        Console.WriteLine("=== Testing Literal Variable Compilation ===\n");
        
        // Test 1: Instance variable access (should use PushReceiverVariable)
        TestInstanceVariable();
        
        // Test 2: Class variable access (should use PushLiteralVariable with Association)
        TestClassVariable();
        
        // Test 3: Pool variable access (should use PushLiteralVariable with Association)
        TestPoolVariable();
        
        // Test 4: Global variable access (should use PushLiteralVariable with Association)
        TestGlobalVariable();
        
        // Test 5: Mixed variable access
        TestMixedVariables();
        
        Console.WriteLine("\n=== All Tests Complete ===");
    }
    
    static void TestInstanceVariable()
    {
        Console.WriteLine("Test 1: Instance Variable Access");
        Console.WriteLine("---------------------------------");
        
        var compiler = new BytecodeCompiler();
        var instanceVars = new List<string> { "x", "y", "z" };
        
        var methodSource = "getX\n\t^x!";
        var result = compiler.CompileMethod(methodSource, instanceVars);
        
        Console.WriteLine($"Method: {methodSource.Replace("\n", " ").Replace("\t", "").Replace("!", "")}");
        Console.WriteLine($"Bytecodes: {BytecodeToString(result.Bytecode)}");
        
        // Should be: 0 (push receiver variable 0), 124 (return top)
        if (result.Bytecode.Length == 2 && result.Bytecode[0] == 0 && result.Bytecode[1] == 124)
        {
            Console.WriteLine("✓ Correctly uses PushReceiverVariable for instance variable");
        }
        else
        {
            Console.WriteLine("✗ Incorrect bytecode for instance variable");
        }
        Console.WriteLine();
    }
    
    static void TestClassVariable()
    {
        Console.WriteLine("Test 2: Class Variable Access");
        Console.WriteLine("-----------------------------");
        
        var compiler = new BytecodeCompiler();
        var instanceVars = new List<string> { "x", "y" };
        var classVars = new Dictionary<string, object> 
        { 
            ["Count"] = new object(),
            ["MaxSize"] = new object()
        };
        
        var methodSource = "getCount\n\t^Count!";
        var result = compiler.CompileMethod(methodSource, instanceVars, classVars);
        
        Console.WriteLine($"Method: {methodSource.Replace("\n", " ").Replace("\t", "").Replace("!", "")}");
        Console.WriteLine($"Bytecodes: {BytecodeToString(result.Bytecode)}");
        Console.WriteLine($"Literals: {LiteralsToString(result.Literals)}");
        
        // Should use PushLiteralVariable (72-79 for indices 0-7)
        if (result.Bytecode.Length == 2 && result.Bytecode[0] >= 72 && result.Bytecode[0] <= 79)
        {
            Console.WriteLine("✓ Correctly uses PushLiteralVariable for class variable");
            
            // Check that literal is an Association
            var index = result.Bytecode[0] - 72;
            if (index < result.Literals.Length && result.Literals[index] is Association assoc)
            {
                Console.WriteLine($"✓ Literal is an Association with key: {assoc.Key}");
            }
            else
            {
                Console.WriteLine("✗ Literal should be an Association");
            }
        }
        else
        {
            Console.WriteLine("✗ Should use PushLiteralVariable for class variable");
        }
        Console.WriteLine();
    }
    
    static void TestPoolVariable()
    {
        Console.WriteLine("Test 3: Pool Variable Access");
        Console.WriteLine("----------------------------");
        
        var compiler = new BytecodeCompiler();
        var instanceVars = new List<string> { "x" };
        var poolVars = new Dictionary<string, object> 
        { 
            ["Pi"] = 3.14159,
            ["MaxInt"] = int.MaxValue
        };
        
        var methodSource = "getPi\n\t^Pi!";
        var result = compiler.CompileMethod(methodSource, instanceVars, null, poolVars);
        
        Console.WriteLine($"Method: {methodSource.Replace("\n", " ").Replace("\t", "").Replace("!", "")}");
        Console.WriteLine($"Bytecodes: {BytecodeToString(result.Bytecode)}");
        Console.WriteLine($"Literals: {LiteralsToString(result.Literals)}");
        
        // Should use PushLiteralVariable (72-79 for indices 0-7)
        if (result.Bytecode.Length == 2 && result.Bytecode[0] >= 72 && result.Bytecode[0] <= 79)
        {
            Console.WriteLine("✓ Correctly uses PushLiteralVariable for pool variable");
            
            // Check that literal is an Association
            var index = result.Bytecode[0] - 72;
            if (index < result.Literals.Length && result.Literals[index] is Association assoc)
            {
                Console.WriteLine($"✓ Literal is an Association with key: {assoc.Key}");
            }
            else
            {
                Console.WriteLine("✗ Literal should be an Association");
            }
        }
        else
        {
            Console.WriteLine("✗ Should use PushLiteralVariable for pool variable");
        }
        Console.WriteLine();
    }
    
    static void TestGlobalVariable()
    {
        Console.WriteLine("Test 4: Global Variable Access");
        Console.WriteLine("------------------------------");
        
        var compiler = new BytecodeCompiler();
        var instanceVars = new List<string>();
        
        var methodSource = "getTranscript\n\t^Transcript!";
        var result = compiler.CompileMethod(methodSource, instanceVars);
        
        Console.WriteLine($"Method: {methodSource.Replace("\n", " ").Replace("\t", "").Replace("!", "")}");
        Console.WriteLine($"Bytecodes: {BytecodeToString(result.Bytecode)}");
        Console.WriteLine($"Literals: {LiteralsToString(result.Literals)}");
        
        // Should use PushLiteralVariable (72-79 for indices 0-7)
        if (result.Bytecode.Length == 2 && result.Bytecode[0] >= 72 && result.Bytecode[0] <= 79)
        {
            Console.WriteLine("✓ Correctly uses PushLiteralVariable for global variable");
            
            // Check that literal is an Association
            var index = result.Bytecode[0] - 72;
            if (index < result.Literals.Length && result.Literals[index] is Association assoc)
            {
                Console.WriteLine($"✓ Literal is an Association with key: {assoc.Key}");
            }
            else
            {
                Console.WriteLine("✗ Literal should be an Association");
            }
        }
        else
        {
            Console.WriteLine("✗ Should use PushLiteralVariable for global variable");
        }
        Console.WriteLine();
    }
    
    static void TestMixedVariables()
    {
        Console.WriteLine("Test 5: Mixed Variable Access");
        Console.WriteLine("-----------------------------");
        
        var compiler = new BytecodeCompiler();
        var instanceVars = new List<string> { "value" };
        var classVars = new Dictionary<string, object> { ["Default"] = new object() };
        var poolVars = new Dictionary<string, object> { ["MaxValue"] = new object() };
        
        // Method that uses instance var, class var, pool var, and literal constant
        var methodSource = @"test
	| temp |
	temp := value.
	temp := Default.
	temp := MaxValue.
	temp := 42.
	^temp!";
        
        var result = compiler.CompileMethod(methodSource, instanceVars, classVars, poolVars);
        
        Console.WriteLine("Method: test with mixed variable types");
        Console.WriteLine($"Bytecodes: {BytecodeToString(result.Bytecode)}");
        Console.WriteLine($"Literals: {LiteralsToString(result.Literals)}");
        
        // Analyze bytecode sequence
        var i = 0;
        
        // First: push instance var 'value' (bytecode 0)
        if (i < result.Bytecode.Length && result.Bytecode[i] == 0)
        {
            Console.WriteLine($"✓ Bytecode[{i}] = {result.Bytecode[i]} : PushReceiverVariable 0 (value)");
            i++;
        }
        
        // Store to temp (bytecode 104)
        if (i < result.Bytecode.Length && result.Bytecode[i] == 104)
        {
            Console.WriteLine($"✓ Bytecode[{i}] = {result.Bytecode[i]} : StoreAndPopTemporary 0");
            i++;
        }
        
        // Push class var 'Default' (should be PushLiteralVariable)
        if (i < result.Bytecode.Length && result.Bytecode[i] >= 72 && result.Bytecode[i] <= 79)
        {
            var index = result.Bytecode[i] - 72;
            Console.WriteLine($"✓ Bytecode[{i}] = {result.Bytecode[i]} : PushLiteralVariable {index} (Default)");
            i++;
        }
        
        // Continue analysis...
        Console.WriteLine($"Total bytecodes: {result.Bytecode.Length}");
        Console.WriteLine();
    }
    
    static string BytecodeToString(byte[] bytecodes)
    {
        return "[" + string.Join(", ", bytecodes) + "]";
    }
    
    static string LiteralsToString(object[] literals)
    {
        var parts = new List<string>();
        for (int i = 0; i < literals.Length; i++)
        {
            if (literals[i] is Association assoc)
            {
                parts.Add($"#{i}: Association({assoc.Key} -> {assoc.Value ?? "nil"})");
            }
            else if (literals[i] is Symbol sym)
            {
                parts.Add($"#{i}: Symbol({sym.Value})");
            }
            else if (literals[i] == null)
            {
                parts.Add($"#{i}: null");
            }
            else
            {
                parts.Add($"#{i}: {literals[i].GetType().Name}({literals[i]})");
            }
        }
        return string.Join(", ", parts);
    }
}