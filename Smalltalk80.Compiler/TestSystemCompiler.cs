namespace Smalltalk80.Compiler.Test;

using System;
using System.IO;
using Smalltalk80.Compiler.AST.Processors;
using Smalltalk80.Compiler.Compilation;

/// <summary>
/// Test program to verify the new SystemCompiler implementation.
/// </summary>
public static class TestSystemCompiler
{
    /// <summary>
    /// Test method for the SystemCompiler (not a main entry point).
    /// </summary>
    public static void TestCompilation(string[] args)
    {
        try
        {
            // Sample Smalltalk source code
            const string testSource = @"
Object subclass: #Point
    instanceVariableNames: 'x y'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Graphics-Primitives'!

!Point methodsFor: 'accessing'!

x
    ^x!

y  
    ^y!

x: aNumber
    x := aNumber!

y: aNumber
    y := aNumber! !

!Point methodsFor: 'arithmetic'!

+ aPoint
    ^self class new
        x: x + aPoint x;
        y: y + aPoint y;
        yourself! !

!Point class methodsFor: 'instance creation'!

x: xValue y: yValue
    ^self new
        x: xValue;
        y: yValue;
        yourself! !

Point initialize!
";

            Console.WriteLine("Testing Modern SystemCompiler v2.0");
            Console.WriteLine("====================================");
            Console.WriteLine();
            
            // Create compiler instance
            var compiler = new SystemCompiler();
            
            // Create processor context
            var context = new ProcessorContext("test.st", testSource);
            
            // Compile the source
            Console.WriteLine("Compiling Smalltalk source...");
            var result = compiler.Process(null!, context);
            
            // Check result
            if (result is BytecodeResult bytecodeResult && bytecodeResult.Success)
            {
                Console.WriteLine("✓ Compilation successful!");
                Console.WriteLine();
                Console.WriteLine("Generated JSON Output:");
                Console.WriteLine("----------------------");
                Console.WriteLine(bytecodeResult.JsonOutput);
                
                // Optionally save to file
                if (args.Length > 0)
                {
                    var outputFile = args[0];
                    File.WriteAllText(outputFile, bytecodeResult.JsonOutput!);
                    Console.WriteLine();
                    Console.WriteLine($"✓ Output saved to: {outputFile}");
                }
            }
            else
            {
                Console.WriteLine($"✗ Compilation failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}