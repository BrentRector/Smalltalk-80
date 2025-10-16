using System;
using System.IO;
using System.Text.Json;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Engine;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Minimal test harness to validate cold start loader execution and heap correctness.
/// Tests all phases 0-9 and post-cold-start validation without requiring VM components.
/// </summary>
public class TestColdStart
{
    public static int Main(string[] args)
    {
        try
        {
            Console.WriteLine("=== Cold Start Loader Test ===\n");

            // Parse command line arguments
            string coldStartPath = "bin/Debug/net9.0/coldstart.json";
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--coldstart" && i + 1 < args.Length)
                {
                    coldStartPath = args[i + 1];
                    i++; // Skip the next argument
                }
                else if (args.Length == 1)
                {
                    // Legacy support: single argument is the path
                    coldStartPath = args[0];
                }
            }

            if (!File.Exists(coldStartPath))
            {
                Console.WriteLine($"Error: Cold start file not found: {coldStartPath}");
                Console.WriteLine("Please run the compiler first to generate coldstart.json");
                return 1;
            }

            Console.WriteLine($"Loading cold start bundle: {coldStartPath}");

            // Load bundle
            var bundleJson = File.ReadAllText(coldStartPath);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            var bundle = JsonSerializer.Deserialize<ColdStartBundle>(bundleJson, options) 
                ?? throw new InvalidOperationException("Failed to deserialize cold start bundle");

            Console.WriteLine($"Bundle loaded: {bundle.Symbols.Length} symbols, {bundle.Methods.Length} methods, {bundle.Classes.Length} classes");

            // Create heap manager
            var heapManager = new SmalltalkHeapManager();
            Console.WriteLine("Heap manager created");

            // Create and run loader
            var loader = new Loader(bundle, heapManager);
            loader.Run();

            // Success - message already printed by Loader
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Cold start loader test FAILED:");
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner: {ex.InnerException.Message}");
            }
            Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
            return 1;
        }
    }
}