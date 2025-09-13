using System;
using Smalltalk80.Compiler;

class Program
{
    static void Main()
    {
        Console.WriteLine("Testing Smalltalk Compiler Integration");
        Console.WriteLine("=======================================");
        
        try
        {
            // Create a minimal interpreter instance
            var interpreter = new Interpreter();
            
            // Create the compiler
            var compiler = new SmalltalkCompiler(interpreter);
            
            // Test 1: Compile a simple method
            Console.WriteLine("\nTest 1: Compile simple getter method");
            var source1 = "name\n\t^name";
            var method1 = compiler.CompileMethod(source1, "Person", false);
            
            Console.WriteLine($"  Success! Method compiled:");
            Console.WriteLine($"    Selector: {method1.SelectorName}");
            Console.WriteLine($"    Bytecodes: {method1.Bytecodes.Length} bytes");
            Console.WriteLine($"    Literals: {method1.Literals.Length}");
            
            // Test 2: Compile a method with arguments
            Console.WriteLine("\nTest 2: Compile setter method with argument");
            var source2 = "name: aString\n\tname _ aString";
            var method2 = compiler.CompileMethod(source2, "Person", false);
            
            Console.WriteLine($"  Success! Method compiled:");
            Console.WriteLine($"    Selector: {method2.SelectorName}");
            Console.WriteLine($"    Bytecodes: {method2.Bytecodes.Length} bytes");
            Console.WriteLine($"    Literals: {method2.Literals.Length}");
            
            // Test 3: Compile a method with literals
            Console.WriteLine("\nTest 3: Compile method with literals");
            var source3 = "initialize\n\tcount _ 0.\n\tname _ 'Unknown'";
            var method3 = compiler.CompileMethod(source3, "Person", false);
            
            Console.WriteLine($"  Success! Method compiled:");
            Console.WriteLine($"    Selector: {method3.SelectorName}");
            Console.WriteLine($"    Bytecodes: {method3.Bytecodes.Length} bytes");
            Console.WriteLine($"    Literals: {method3.Literals.Length}");
            
            // Display bytecodes for analysis
            Console.WriteLine($"    Bytecode sequence: {BitConverter.ToString(method3.Bytecodes)}");
            
            // Test 4: Compile a method with message sends
            Console.WriteLine("\nTest 4: Compile method with message sends");
            var source4 = "displayName\n\tTranscript show: name.\n\tTranscript cr";
            var method4 = compiler.CompileMethod(source4, "Person", false);
            
            Console.WriteLine($"  Success! Method compiled:");
            Console.WriteLine($"    Selector: {method4.SelectorName}");
            Console.WriteLine($"    Bytecodes: {method4.Bytecodes.Length} bytes");
            Console.WriteLine($"    Literals: {method4.Literals.Length}");
            
            // Test 5: Compile a doIt expression
            Console.WriteLine("\nTest 5: Compile doIt expression");
            var expression = "3 + 4";
            var method5 = compiler.CompileDoIt(expression);
            
            Console.WriteLine($"  Success! DoIt compiled:");
            Console.WriteLine($"    Selector: {method5.SelectorName}");
            Console.WriteLine($"    Bytecodes: {method5.Bytecodes.Length} bytes");
            Console.WriteLine($"    Bytecode sequence: {BitConverter.ToString(method5.Bytecodes)}");
            
            Console.WriteLine("\n✓ All compiler tests passed!");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ Test failed with error:");
            Console.WriteLine($"  {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  Inner: {ex.InnerException.Message}");
            }
            Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}