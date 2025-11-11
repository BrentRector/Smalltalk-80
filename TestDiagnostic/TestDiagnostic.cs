using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Smalltalk80.Interpreter.Core.Schema;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Diagnostic tool for analyzing BlockContextTest test methods.
/// Identifies which tests are failing by parsing the cold start bundle and extracting test metadata.
/// </summary>
public static class TestDiagnostic
{
    /// <summary>
    /// Represents a single test method from BlockContextTest.
    /// </summary>
    public record TestMethod(
        string Name,
        string Category,
        int ExpectedValue,
        int BytecodeLength,
        string? Source,
        string? Description
    );

    /// <summary>
    /// Test category information.
    /// </summary>
    public record TestCategory(
        string Name,
        string Prefix,
        int Count,
        int ExpectedTotal
    );

    /// <summary>
    /// Main entry point for the diagnostic tool.
    /// </summary>
    /// <param name="args">Command-line arguments: path to coldstart_with_tests.json</param>
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: TestDiagnostic <path-to-coldstart_with_tests.json>");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  TestDiagnostic ../bin/Debug/net9.0/coldstart_with_tests.json");
            return;
        }

        var coldStartPath = args[0];
        if (!File.Exists(coldStartPath))
        {
            Console.Error.WriteLine($"Error: File not found: {coldStartPath}");
            return;
        }

        Console.WriteLine("BlockContextTest Diagnostic Tool");
        Console.WriteLine("=================================\n");
        Console.WriteLine($"Loading: {coldStartPath}\n");

        try
        {
            var bundle = ColdStartBundle.LoadFromFile(coldStartPath);
            AnalyzeTests(bundle);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
        }
    }

    /// <summary>
    /// Analyzes all BlockContextTest methods in the bundle.
    /// </summary>
    private static void AnalyzeTests(ColdStartBundle bundle)
    {
        // Find all methods belonging to BlockContextTest
        var testMethods = bundle.Methods
            .Where(m => m.HomeClassLocalId == "class:BlockContextTest")
            .Select(m => ParseTestMethod(m, bundle))
            .Where(t => t != null)
            .Cast<TestMethod>()
            .OrderBy(t => GetTestOrder(t.Name))
            .ToList();

        Console.WriteLine($"Found {testMethods.Count} test methods in BlockContextTest\n");

        // Define test categories based on the testAllCombinations method
        var categories = new[]
        {
            new TestCategory("Single Level", "s", 9, 90),
            new TestCategory("Two Level", "t", 81, 810),
            new TestCategory("Three Level", "n", 27, 270),
            new TestCategory("Method Multi-Temp", "m", 6, 60),
            new TestCategory("Mutation", "mu", 3, 30),
            new TestCategory("Method Args", "ma", 6, 60),
            new TestCategory("Real World", "rw", 5, 50),
            new TestCategory("Edge Cases", "e", 7, 70),
            new TestCategory("Three Level Gaps", "tg", 4, 40),
            new TestCategory("Stress", "stress", 3, 30),
            new TestCategory("Non-Local Returns", "nlr", 3, 30),
            new TestCategory("Exception Handling", "eh", 2, 20),
            new TestCategory("Block Lifetime", "bl", 2, 20),
            new TestCategory("Control Flow", "cf", 2, 20),
            new TestCategory("Edge Special", "es", 2, 20),
            new TestCategory("Stress Deep", "sd", 1, 10),
            new TestCategory("Instance Variables", "iv", 10, 100),
            new TestCategory("Class Variables", "cv", 5, 50),
            new TestCategory("Pool Variables", "pv", 3, 30),
            new TestCategory("Global Variables", "gv", 5, 50),
            new TestCategory("Pseudo Variables", "ps", 5, 50),
            new TestCategory("Mixed Access", "mx", 5, 50)
        };

        // Analyze by category
        Console.WriteLine("Test Summary by Category:");
        Console.WriteLine("=========================\n");

        int totalTests = 0;
        int totalExpected = 0;

        foreach (var category in categories)
        {
            var categoryTests = testMethods
                .Where(t => TestMatchesCategory(t.Name, category.Prefix))
                .ToList();

            if (categoryTests.Count > 0)
            {
                var categorySum = categoryTests.Sum(t => t.ExpectedValue);
                totalTests += categoryTests.Count;
                totalExpected += categorySum;

                Console.WriteLine($"{category.Name}:");
                Console.WriteLine($"  Tests Found: {categoryTests.Count} (expected: {category.Count})");
                Console.WriteLine($"  Expected Sum: {categorySum} (target: {category.ExpectedTotal})");

                if (categoryTests.Count != category.Count)
                {
                    Console.WriteLine($"  WARNING: Test count mismatch!");
                }
                if (categorySum != category.ExpectedTotal)
                {
                    Console.WriteLine($"  WARNING: Expected sum mismatch!");
                }
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine($"{category.Name}: NO TESTS FOUND (expected: {category.Count})");
                Console.WriteLine();
            }
        }

        Console.WriteLine($"Total Tests: {totalTests}");
        Console.WriteLine($"Total Expected Value: {totalExpected}");
        Console.WriteLine($"Target Total (with exceptions skipped): 1958");
        Console.WriteLine();

        // Detailed test listing
        Console.WriteLine("\nDetailed Test Information:");
        Console.WriteLine("==========================\n");

        var currentCategory = "";
        foreach (var test in testMethods)
        {
            var category = GetCategoryName(test.Name);
            if (category != currentCategory)
            {
                Console.WriteLine($"\n--- {category} ---\n");
                currentCategory = category;
            }

            Console.WriteLine($"{test.Name,-25} => {test.ExpectedValue,4}  (bytecode: {test.BytecodeLength,4} bytes)");
            if (!string.IsNullOrEmpty(test.Description))
            {
                Console.WriteLine($"  {test.Description}");
            }
        }

        // Identify potential issues
        Console.WriteLine("\n\nPotential Issues:");
        Console.WriteLine("=================\n");

        var zeroValueTests = testMethods.Where(t => t.ExpectedValue == 0).ToList();
        if (zeroValueTests.Count > 0)
        {
            Console.WriteLine($"Tests expecting 0 (might be ambiguous): {zeroValueTests.Count}");
            foreach (var test in zeroValueTests)
            {
                Console.WriteLine($"  - {test.Name}");
            }
            Console.WriteLine();
        }

        var largeValueTests = testMethods.Where(t => t.ExpectedValue > 100).ToList();
        if (largeValueTests.Count > 0)
        {
            Console.WriteLine($"Tests expecting large values (>100): {largeValueTests.Count}");
            foreach (var test in largeValueTests)
            {
                Console.WriteLine($"  - {test.Name} => {test.ExpectedValue}");
            }
            Console.WriteLine();
        }

        // Generate test execution script
        Console.WriteLine("\nGenerated Test Execution Script:");
        Console.WriteLine("================================\n");
        Console.WriteLine("To test individual methods, use:");
        Console.WriteLine();
        Console.WriteLine("dotnet run --project Smalltalk80.Interpreter -- \\");
        Console.WriteLine("  --coldstart coldstart_with_tests.json \\");
        Console.WriteLine("  --skip-init-expressions \\");
        Console.WriteLine("  --execute \"<test-name>\"");
        Console.WriteLine();
        Console.WriteLine("Example test names:");
        foreach (var test in testMethods.Take(5))
        {
            Console.WriteLine($"  BlockContextTest new {test.Name}  (expects: {test.ExpectedValue})");
        }
    }

    /// <summary>
    /// Parses a method descriptor into a TestMethod record.
    /// </summary>
    private static TestMethod? ParseTestMethod(MethodDescriptor method, ColdStartBundle bundle)
    {
        var selectorName = bundle.Symbols[method.SelectorIndex];

        // Filter for test methods (exclude testAllCombinations)
        if (selectorName == "testAllCombinations")
            return null;

        // Match test method patterns: s*, t*, th*, n*, m*, mu*, ma*, rw*, e*, tg*, st*, nlr*, eh*, bl*, cf*, es*, sd*, iv*, cv*, pv*, gv*, ps*, mx*
        if (!IsTestMethod(selectorName))
            return null;

        var category = GetCategoryName(selectorName);
        var expectedValue = ExtractExpectedValue(method, bundle);
        var bytecodeLength = GetBytecodeLength(method);
        var description = ExtractDescription(method.Source);

        return new TestMethod(selectorName, category, expectedValue, bytecodeLength, method.Source, description);
    }

    /// <summary>
    /// Determines if a method name matches a test method pattern.
    /// </summary>
    private static bool IsTestMethod(string name)
    {
        // Single-level tests
        if (Regex.IsMatch(name, @"^s[0-9]{2}$")) return true;

        // Two-level tests
        if (Regex.IsMatch(name, @"^t[0-9]{4}$")) return true;

        // Three-level tests (includes tg prefix check)
        if (Regex.IsMatch(name, @"^n[0-9]{6}$")) return true;

        // Method temp tests
        if (Regex.IsMatch(name, @"^m[0-9]t[0-9]l")) return true;

        // Category-based tests
        var prefixes = new[] { "mu", "ma", "rw", "nlr", "eh", "bl", "cf", "es", "sd", "iv", "cv", "pv", "gv", "ps", "mx", "e" };
        return prefixes.Any(prefix => name.StartsWith(prefix) && char.IsDigit(name[prefix.Length]));
    }

    /// <summary>
    /// Determines which category a test belongs to based on its name.
    /// </summary>
    private static bool TestMatchesCategory(string testName, string categoryPrefix)
    {
        return categoryPrefix.ToLower() switch
        {
            "s" => Regex.IsMatch(testName, @"^s[0-9]{2}$"),
            "t" => Regex.IsMatch(testName, @"^t[0-9]{4}$"),
            "n" => Regex.IsMatch(testName, @"^n[0-9]{6}$") && !testName.StartsWith("n3"),
            "m" => Regex.IsMatch(testName, @"^m[0-9]t[0-9]l"),
            "tg" => testName.StartsWith("n3"),
            "stress" => testName.StartsWith("stress"),
            _ => testName.StartsWith(categoryPrefix)
        };
    }

    /// <summary>
    /// Gets a human-readable category name for a test.
    /// </summary>
    private static string GetCategoryName(string testName)
    {
        if (Regex.IsMatch(testName, @"^s[0-9]{2}$")) return "Single Level";
        if (Regex.IsMatch(testName, @"^t[0-9]{4}$")) return "Two Level";
        if (Regex.IsMatch(testName, @"^n[0-9]{6}$") && !testName.StartsWith("n3")) return "Three Level";
        if (testName.StartsWith("n3")) return "Three Level Gaps";
        if (Regex.IsMatch(testName, @"^m[0-9]t[0-9]l")) return "Method Multi-Temp";
        if (testName.StartsWith("mu")) return "Mutation";
        if (testName.StartsWith("ma")) return "Method Args";
        if (testName.StartsWith("rw")) return "Real World";
        if (testName.StartsWith("e") && char.IsDigit(testName[1])) return "Edge Cases";
        if (testName.StartsWith("nlr")) return "Non-Local Returns";
        if (testName.StartsWith("eh")) return "Exception Handling";
        if (testName.StartsWith("bl")) return "Block Lifetime";
        if (testName.StartsWith("cf")) return "Control Flow";
        if (testName.StartsWith("es")) return "Edge Special";
        if (testName.StartsWith("sd")) return "Stress Deep";
        if (testName.StartsWith("stress")) return "Stress";
        if (testName.StartsWith("iv")) return "Instance Variables";
        if (testName.StartsWith("cv")) return "Class Variables";
        if (testName.StartsWith("pv")) return "Pool Variables";
        if (testName.StartsWith("gv")) return "Global Variables";
        if (testName.StartsWith("ps")) return "Pseudo Variables";
        if (testName.StartsWith("mx")) return "Mixed Access";

        return "Unknown";
    }

    /// <summary>
    /// Gets the sort order for a test name.
    /// </summary>
    private static int GetTestOrder(string testName)
    {
        var category = GetCategoryName(testName);
        var categoryOrder = category switch
        {
            "Single Level" => 1,
            "Two Level" => 2,
            "Three Level" => 3,
            "Method Multi-Temp" => 4,
            "Mutation" => 5,
            "Method Args" => 6,
            "Real World" => 7,
            "Edge Cases" => 8,
            "Three Level Gaps" => 9,
            "Stress" => 10,
            "Non-Local Returns" => 11,
            "Exception Handling" => 12,
            "Block Lifetime" => 13,
            "Control Flow" => 14,
            "Edge Special" => 15,
            "Stress Deep" => 16,
            "Instance Variables" => 17,
            "Class Variables" => 18,
            "Pool Variables" => 19,
            "Global Variables" => 20,
            "Pseudo Variables" => 21,
            "Mixed Access" => 22,
            _ => 99
        };

        return categoryOrder * 1000 + testName.GetHashCode();
    }

    /// <summary>
    /// Extracts the expected return value from a test method.
    /// Most tests return a fixed integer (usually 10).
    /// </summary>
    private static int ExtractExpectedValue(MethodDescriptor method, ColdStartBundle bundle)
    {
        // Parse source code to find the return value
        if (string.IsNullOrEmpty(method.Source))
            return 10; // Default assumption

        var source = method.Source;

        // Look for explicit numeric literals in return statements
        // Pattern: ^[...] or ifTrue: [...] or value patterns
        var returnMatches = Regex.Matches(source, @"\^(\d+)");
        if (returnMatches.Count > 0)
        {
            // Take the last explicit return in the simple cases
            var lastReturn = returnMatches[returnMatches.Count - 1];
            if (int.TryParse(lastReturn.Groups[1].Value, out var value))
                return value;
        }

        // Look for arithmetic expressions that evaluate to specific values
        // Most tests sum to 10: 5+5, 3+4+3, 2+3+2+3, etc.

        // Special cases based on test patterns:
        // - Tests with ifTrue:ifFalse: often return different values
        // - Tests with == comparison return specific values like 21 or 999

        if (source.Contains("ifTrue: [21]") || source.Contains("ifTrue: [20]"))
        {
            // These tests check conditions and return specific values
            return ExtractConditionalValue(source);
        }

        // Default: most tests are designed to return 10
        return 10;
    }

    /// <summary>
    /// Extracts expected value from conditional tests.
    /// </summary>
    private static int ExtractConditionalValue(string source)
    {
        // Look for ifTrue: [N] patterns
        var match = Regex.Match(source, @"ifTrue:\s*\[(\d+)\]");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var value))
        {
            return value;
        }

        return 10; // Default
    }

    /// <summary>
    /// Gets the bytecode length from a method descriptor.
    /// </summary>
    private static int GetBytecodeLength(MethodDescriptor method)
    {
        return method.Bytecodes switch
        {
            int[] bytecodeArray => bytecodeArray.Length,
            string base64String => Convert.FromBase64String(base64String).Length,
            _ => 0
        };
    }

    /// <summary>
    /// Extracts the description comment from method source.
    /// </summary>
    private static string? ExtractDescription(string? source)
    {
        if (string.IsNullOrEmpty(source))
            return null;

        // Look for quoted strings after method name
        var match = Regex.Match(source, @"""([^""]+)""");
        if (match.Success)
            return match.Groups[1].Value;

        return null;
    }
}
