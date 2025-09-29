using System;
using System.Reflection;
using Xunit;

namespace TestContextRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Running SMethodContext and SBlockContext tests...\n");
            
            // Run tests using reflection
            RunTestClass(typeof(Smalltalk80.Tests.TestSMethodContext));
            RunTestClass(typeof(Smalltalk80.Tests.TestSBlockContext));
            
            Console.WriteLine("\n✅ All tests completed!");
        }
        
        static void RunTestClass(Type testClass)
        {
            Console.WriteLine($"\n📋 Running {testClass.Name}:");
            Console.WriteLine(new string('-', 60));
            
            var instance = Activator.CreateInstance(testClass);
            var methods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            
            int passed = 0;
            int failed = 0;
            
            foreach (var method in methods)
            {
                var factAttribute = method.GetCustomAttribute<FactAttribute>();
                if (factAttribute != null)
                {
                    try
                    {
                        method.Invoke(instance, null);
                        Console.WriteLine($"✓ {method.Name}");
                        passed++;
                    }
                    catch (TargetInvocationException ex)
                    {
                        Console.WriteLine($"✗ {method.Name}: {ex.InnerException?.Message}");
                        failed++;
                    }
                }
            }
            
            Console.WriteLine($"\nResults: {passed} passed, {failed} failed");
        }
    }
}