using System;
using System.Collections.Generic;
using System.Linq;
using Smalltalk80.Compiler;

class MethodCountAnalyzer
{
    static void Main()
    {
        var extractor = new SimpleClassExtractor();
        var classes = new string[] { 
            "Object", "Behavior", "Class", "Metaclass", "CompiledMethod", 
            "MethodDictionary", "Symbol", "String", "Array", "ByteArray", 
            "Collection", "Dictionary", "SystemDictionary" 
        };

        Console.WriteLine("Class Method Count Analysis");
        Console.WriteLine("===========================");
        Console.WriteLine();

        foreach (var className in classes)
        {
            try 
            {
                var methods = extractor.ExtractMethodsForClass(@"Smalltalk80\Smalltalk-80 Source\Smalltalk-80.st", className);
                var instanceMethods = methods.Where(m => !m.IsClassMethod).Count();
                var classMethods = methods.Where(m => m.IsClassMethod).Count();
                
                Console.WriteLine($"{className}:");
                Console.WriteLine($"  Instance methods: {instanceMethods}");
                Console.WriteLine($"  Class methods: {classMethods}");
                Console.WriteLine($"  Total: {instanceMethods + classMethods}");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{className}: Error - {ex.Message}");
                Console.WriteLine();
            }
        }
    }
}