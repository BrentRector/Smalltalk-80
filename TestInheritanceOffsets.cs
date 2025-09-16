using System;
using System.Collections.Generic;
using StandaloneCompiler;

class TestInheritanceOffsets
{
    static void Main()
    {
        var compiler = new SimpleCompiler();
        
        // Test case 1: Simple inheritance
        string source1 = @"
Object subclass: #Base
    instanceVariableNames: 'var1 var2'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!

Base subclass: #Derived
    instanceVariableNames: 'var3'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!
";

        // Test case 2: Like LookupKey/Association
        string source2 = @"
Magnitude subclass: #MyKey
    instanceVariableNames: 'key'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!

MyKey subclass: #MyPair
    instanceVariableNames: 'value'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!
";

        Console.WriteLine("Testing Instance Variable Offset Calculation");
        Console.WriteLine("============================================\n");
        
        Console.WriteLine("Test 1: Simple Base/Derived");
        var result1 = compiler.CompileSource(source1);
        ShowClassInfo(result1);
        
        Console.WriteLine("\nTest 2: MyKey/MyPair (like LookupKey/Association)");
        var result2 = compiler.CompileSource(source2);
        ShowClassInfo(result2);
    }
    
    static void ShowClassInfo(CompilationResult result)
    {
        foreach (var cls in result.Classes)
        {
            Console.WriteLine($"Class: {cls.Name}");
            Console.WriteLine($"  Superclass: {cls.Superclass}");
            Console.WriteLine($"  Direct instance vars: [{string.Join(", ", cls.InstanceVariables)}]");
            
            // The compiler should have computed the full list when compiling methods
            // Let's just show what we have
            Console.WriteLine();
        }
    }
}