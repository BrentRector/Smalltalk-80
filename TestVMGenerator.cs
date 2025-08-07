using System;
using System.IO;
using Smalltalk80.Bootstrap;

class TestVMGenerator
{
    static void Main()
    {
        try
        {
            Console.WriteLine("Testing VMPrimitiveBootstrapGenerator...");
            
            var generator = new VMPrimitiveBootstrapGenerator("CorrectBootstrap.st");
            var code = generator.Generate();
            
            File.WriteAllText("GeneratedVMBootstrap.cs", code);
            
            Console.WriteLine($"Generated {code.Length} characters of VM primitive bootstrap code");
            Console.WriteLine($"Output written to GeneratedVMBootstrap.cs");
            
            // Count primitive calls
            int primitiveCount = 0;
            string[] lines = code.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("vm.Primitive"))
                    primitiveCount++;
            }
            
            Console.WriteLine($"Total VM primitive calls: {primitiveCount}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}