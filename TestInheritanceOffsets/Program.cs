using System;
using System.Collections.Generic;
using System.Reflection;
using StandaloneCompiler;

class Program
{
    static void Main()
    {
        Console.WriteLine("Testing Instance Variable Inheritance in Compiler");
        Console.WriteLine("==================================================\n");
        
        var compiler = new SimpleCompiler();
        
        // Use reflection to test the private method ComputeInheritedInstanceVariables
        var compilerType = compiler.GetType();
        var computeMethod = compilerType.GetMethod("ComputeInheritedInstanceVariables", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (computeMethod == null)
        {
            Console.WriteLine("ERROR: Could not find ComputeInheritedInstanceVariables method");
            return;
        }
        
        // First compile some test classes
        string source = @"
Object subclass: #Base
    instanceVariableNames: 'base1 base2'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!

Base subclass: #Derived
    instanceVariableNames: 'derived1'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!

Magnitude subclass: #TestKey
    instanceVariableNames: 'keyField'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!

TestKey subclass: #TestPair
    instanceVariableNames: 'valueField'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!
";

        var result = compiler.CompileSource(source);
        Console.WriteLine($"Compiled {result.Classes?.Count ?? 0} classes\n");
        
        // Now test the inheritance computation
        TestInheritance(compiler, computeMethod, "Base");
        TestInheritance(compiler, computeMethod, "Derived");
        TestInheritance(compiler, computeMethod, "TestKey");
        TestInheritance(compiler, computeMethod, "TestPair");
        
        // Test with well-known system class
        Console.WriteLine("\nTesting with system class inheritance:");
        TestSystemClass(compiler, computeMethod, "Association");
        TestSystemClass(compiler, computeMethod, "LookupKey");
    }
    
    static void TestInheritance(SimpleCompiler compiler, System.Reflection.MethodInfo computeMethod, string className)
    {
        var result = computeMethod.Invoke(compiler, new object[] { className });
        if (result is List<string> vars)
        {
            Console.WriteLine($"{className}:");
            Console.WriteLine($"  All instance variables (with inheritance): [{string.Join(", ", vars)}]");
            Console.WriteLine($"  Field offsets:");
            for (int i = 0; i < vars.Count; i++)
            {
                Console.WriteLine($"    {vars[i]} -> offset {i}");
            }
            Console.WriteLine();
        }
    }
    
    static void TestSystemClass(SimpleCompiler compiler, System.Reflection.MethodInfo computeMethod, string className)
    {
        // For system classes not in our compilation
        var result = computeMethod.Invoke(compiler, new object[] { className });
        if (result is List<string> vars)
        {
            Console.WriteLine($"{className} (system class):");
            Console.WriteLine($"  All instance variables: [{string.Join(", ", vars)}]");
            Console.WriteLine($"  Total fields: {vars.Count}");
            Console.WriteLine();
        }
    }
}