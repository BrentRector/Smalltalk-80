using System;
using System.Linq;

/// <summary>
/// Provides helper methods to output the contents of the
/// <see cref="SystemDictionary"/> in a human readable form.
/// </summary>
public static class SystemStatePrinter
{
    /// <summary>
    /// Prints a summary of all classes and their method selectors to the
    /// specified <see cref="Console"/> output.
    /// </summary>
    public static void PrintSummary()
    {
        foreach (var (name, cls) in SystemDictionary.AllClasses.OrderBy(c => c.Key))
        {
            Console.WriteLine($"Class {name}");
            foreach (var selector in cls.MethodDictionary.Keys.Select(k => k.ToString()))
            {
                Console.WriteLine($"  {selector}");
            }

            // Print class methods if they exist
            if (cls.ClassMethodDictionary.Count > 0)
            {
                Console.WriteLine($"Class {name} class");
                foreach (var selector in cls.ClassMethodDictionary.Keys.Select(k => k.ToString()))
                {
                    Console.WriteLine($"  {selector}");
                }
            }
        }
    }
}

