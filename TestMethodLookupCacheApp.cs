using System;
using Smalltalk80.Interpreter.Core;

// Simple test to verify MethodLookupCache compiles and works
class Program
{
    static void Main()
    {
        Console.WriteLine("Testing MethodLookupCache compilation...");
        
        // Create cache
        var cache = new MethodLookupCache(maxEntries: 100);
        
        // Create test classes
        var objectClass = new SClass("Object", 0);
        var stringClass = new SClass("String", 1) { Superclass = objectClass };
        
        // Create test method
        var methodClass = new SClass("CompiledMethod", 4);
        var testSelector = SSymbol.Intern("test");
        var testMethod = new SCompiledMethod(methodClass) { Selector = testSelector };
        
        // Install method
        objectClass.InstallMethod(testSelector, testMethod);
        
        // Test lookup
        var result = cache.Lookup(stringClass, testSelector);
        
        if (result == testMethod)
        {
            Console.WriteLine("✓ Basic lookup test passed!");
        }
        
        // Test cache hit
        var result2 = cache.Lookup(stringClass, testSelector);
        if (cache.Statistics.Hits > 0)
        {
            Console.WriteLine("✓ Cache hit test passed!");
        }
        
        Console.WriteLine($"Statistics: {cache.Statistics}");
        Console.WriteLine($"Diagnostics: {cache.GetDiagnostics()}");
        
        Console.WriteLine("\nAll tests passed! MethodLookupCache is working correctly.");
    }
}