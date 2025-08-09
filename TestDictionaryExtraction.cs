#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Smalltalk80.Compiler;

public class TestDictionaryExtraction
{
    static void Main()
    {
        try
        {
            Console.WriteLine("=== Dictionary Class Extraction and Compilation Test ===\n");
            
            var systemImagePath = @"D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80\Smalltalk-80 Source\Smalltalk-80.st";
            
            // First, extract the class structure
            Console.WriteLine("Step 1: Extracting Dictionary class structure...");
            var simpleExtractor = new SimpleClassExtractor(systemImagePath);
            simpleExtractor.ParseSystemImage();
            
            var dictClass = simpleExtractor.GetClass("Dictionary");
            if (dictClass == null)
            {
                Console.WriteLine("Error: Dictionary class not found!");
                return;
            }
            
            Console.WriteLine($"Found Dictionary class:");
            Console.WriteLine($"  Superclass: {dictClass.Superclass}");
            Console.WriteLine($"  Instance Variables: {string.Join(", ", dictClass.InstanceVariables)}");
            Console.WriteLine($"  Class Variables: {string.Join(", ", dictClass.ClassVariables)}");
            
            // Extract methods
            Console.WriteLine("\nStep 2: Extracting Dictionary methods...");
            var methods = simpleExtractor.ExtractMethodsForClass(systemImagePath, "Dictionary");
            
            Console.WriteLine($"Found {methods.Count} methods for Dictionary");
            
            // Group methods by category
            var methodsByCategory = methods.GroupBy(m => m.Category ?? "uncategorized")
                                          .OrderBy(g => g.Key)
                                          .ToList();
            
            Console.WriteLine("\nMethods by category:");
            foreach (var group in methodsByCategory)
            {
                Console.WriteLine($"  {group.Key}: {group.Count()} methods");
            }
            
            // Compile methods using BytecodeCompiler
            Console.WriteLine("\nStep 3: Compiling methods with BytecodeCompiler...");
            var compiler = new BytecodeCompiler();
            var compiledMethods = new List<CompiledMethod>();
            var failedMethods = new List<(string selector, string error)>();
            
            foreach (var method in methods)
            {
                try
                {
                    Console.Write($"  Compiling {method.Selector}...");
                    
                    var compiledData = compiler.CompileMethod(
                        method.Source,
                        dictClass.InstanceVariables
                    );
                    
                    compiledMethods.Add(new CompiledMethod
                    {
                        Selector = method.Selector,
                        Source = method.Source,
                        Category = method.Category,
                        IsClassMethod = method.IsClassMethod,
                        ArgumentCount = compiledData.ArgumentCount,
                        TemporaryCount = compiledData.TemporaryCount,
                        IsPrimitive = compiledData.IsPrimitive,
                        PrimitiveNumber = compiledData.PrimitiveNumber,
                        Literals = compiledData.Literals,
                        Bytecode = compiledData.Bytecode,
                        BytecodeHex = BitConverter.ToString(compiledData.Bytecode).Replace("-", " ")
                    });
                    
                    Console.WriteLine(" OK");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" FAILED: {ex.Message}");
                    failedMethods.Add((method.Selector, ex.Message));
                }
            }
            
            // Create output structure
            Console.WriteLine("\nStep 4: Generating JSON output...");
            
            var output = new
            {
                className = "Dictionary",
                superclass = dictClass.Superclass,
                instanceVariables = dictClass.InstanceVariables,
                classVariables = dictClass.ClassVariables,
                poolDictionaries = dictClass.PoolDictionaries,
                category = dictClass.Category,
                statistics = new
                {
                    totalMethods = methods.Count,
                    compiledSuccessfully = compiledMethods.Count,
                    compilationFailed = failedMethods.Count,
                    instanceMethods = compiledMethods.Count(m => !m.IsClassMethod),
                    classMethods = compiledMethods.Count(m => m.IsClassMethod)
                },
                methods = compiledMethods.OrderBy(m => m.Category).ThenBy(m => m.Selector),
                failedMethods = failedMethods.Select(f => new { selector = f.selector, error = f.error })
            };
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new SymbolConverter(), new NilConverter() }
            };
            
            var json = JsonSerializer.Serialize(output, options);
            var outputPath = @"D:\Users\brent\OneDrive\Src\Smalltalk-80\Dictionary_compiled.json";
            File.WriteAllText(outputPath, json);
            
            Console.WriteLine($"\nOutput written to: {outputPath}");
            Console.WriteLine($"File size: {new FileInfo(outputPath).Length:N0} bytes");
            
            // Summary
            Console.WriteLine("\n=== Summary ===");
            Console.WriteLine($"Total methods: {methods.Count}");
            Console.WriteLine($"Successfully compiled: {compiledMethods.Count}");
            Console.WriteLine($"Failed: {failedMethods.Count}");
            
            if (failedMethods.Any())
            {
                Console.WriteLine("\nFailed methods:");
                foreach (var (selector, error) in failedMethods.Take(10))
                {
                    Console.WriteLine($"  - {selector}: {error}");
                }
                if (failedMethods.Count > 10)
                {
                    Console.WriteLine($"  ... and {failedMethods.Count - 10} more");
                }
            }
            
            // Show sample bytecode
            if (compiledMethods.Any())
            {
                var sample = compiledMethods.First();
                Console.WriteLine($"\nSample compiled method: {sample.Selector}");
                Console.WriteLine($"  Arguments: {sample.ArgumentCount}");
                Console.WriteLine($"  Temporaries: {sample.TemporaryCount}");
                Console.WriteLine($"  Bytecode length: {sample.Bytecode.Length} bytes");
                Console.WriteLine($"  Bytecode (hex): {sample.BytecodeHex.Substring(0, Math.Min(50, sample.BytecodeHex.Length))}...");
                if (sample.Literals.Any())
                {
                    Console.WriteLine($"  Literals: {string.Join(", ", sample.Literals.Take(5).Select(l => l?.ToString() ?? "null"))}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
    
    class CompiledMethod
    {
        public string Selector { get; set; } = "";
        public string Source { get; set; } = "";
        public string? Category { get; set; }
        public bool IsClassMethod { get; set; }
        public int ArgumentCount { get; set; }
        public int TemporaryCount { get; set; }
        public bool IsPrimitive { get; set; }
        public int PrimitiveNumber { get; set; }
        public object[] Literals { get; set; } = Array.Empty<object>();
        public byte[] Bytecode { get; set; } = Array.Empty<byte>();
        public string BytecodeHex { get; set; } = "";
    }
    
    class SymbolConverter : JsonConverter<Symbol>
    {
        public override Symbol Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new Symbol(reader.GetString() ?? "");
        }
        
        public override void Write(Utf8JsonWriter writer, Symbol value, JsonSerializerOptions options)
        {
            writer.WriteStringValue($"#{value.Value}");
        }
    }
    
    class NilConverter : JsonConverter<NilObject>
    {
        public override NilObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new NilObject();
        }
        
        public override void Write(Utf8JsonWriter writer, NilObject value, JsonSerializerOptions options)
        {
            writer.WriteStringValue("nil");
        }
    }
}