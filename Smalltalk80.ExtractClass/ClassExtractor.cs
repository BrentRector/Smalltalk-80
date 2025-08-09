using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Smalltalk80.Compiler;

namespace Smalltalk80.ExtractClass
{
    /// <summary>
    /// Represents an extracted Smalltalk class with full bytecode compilation.
    /// </summary>
    public class ExtractedClass
    {
        public string ClassName { get; set; } = "";
        public string Superclass { get; set; } = "Object";
        public List<string> InstanceVariables { get; set; } = new();
        public List<string> ClassVariables { get; set; } = new();
        public List<string> PoolDictionaries { get; set; } = new();
        public string? Category { get; set; }
        
        public List<ExtractedMethod> InstanceMethods { get; set; } = new();
        public List<ExtractedMethod> ClassMethods { get; set; } = new();
        
        public ExtractionStatistics Statistics { get; set; } = new();
        
        [JsonIgnore]
        public int InstanceMethodCount => InstanceMethods.Count;
        
        [JsonIgnore]
        public int ClassMethodCount => ClassMethods.Count;
    }
    
    /// <summary>
    /// Represents an extracted method with compiled bytecode.
    /// </summary>
    public class ExtractedMethod
    {
        public string Selector { get; set; } = "";
        public string Source { get; set; } = "";
        public string? Category { get; set; }
        
        public int ArgumentCount { get; set; }
        public int TemporaryCount { get; set; }
        public bool IsPrimitive { get; set; }
        public int PrimitiveNumber { get; set; }
        
        public string Bytecode { get; set; } = "";
        public object[] Literals { get; set; } = Array.Empty<object>();
        
        public string? CompilationError { get; set; }
        
        [JsonIgnore]
        public bool CompiledSuccessfully => CompilationError == null;
    }
    
    /// <summary>
    /// Statistics about the extraction process.
    /// </summary>
    public class ExtractionStatistics
    {
        public int TotalMethods { get; set; }
        public int SuccessfullyCompiled { get; set; }
        public int FailedCompilation { get; set; }
        public double CompilationRate { get; set; }
        public DateTime ExtractionTime { get; set; } = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Extracts complete class definitions from Smalltalk source with compiled bytecode.
    /// </summary>
    public class ClassExtractor
    {
        private readonly BytecodeCompiler bytecodeCompiler;
        
        public ClassExtractor()
        {
            bytecodeCompiler = new BytecodeCompiler();
        }
        
        /// <summary>
        /// Extract a class from the system image with full bytecode compilation.
        /// </summary>
        public ExtractedClass? ExtractClass(string systemImagePath, string className)
        {
            Console.WriteLine($"\nExtracting class: {className}");
            
            try
            {
                // Step 1: Parse system image to get class structure
                Console.WriteLine("  Parsing system image...");
                var extractor = new SimpleClassExtractor(systemImagePath);
                extractor.ParseSystemImage();
                
                var classInfo = extractor.GetClass(className);
                if (classInfo == null)
                {
                    Console.WriteLine($"  Error: Class {className} not found in system image");
                    return null;
                }
                
                Console.WriteLine($"  Found class structure:");
                Console.WriteLine($"    Superclass: {classInfo.Superclass}");
                Console.WriteLine($"    Instance vars: {string.Join(", ", classInfo.InstanceVariables)}");
                Console.WriteLine($"    Class vars: {string.Join(", ", classInfo.ClassVariables)}");
                
                // Step 2: Extract methods
                Console.WriteLine("  Extracting methods...");
                var methods = extractor.ExtractMethodsForClass(systemImagePath, className);
                Console.WriteLine($"  Found {methods.Count} methods");
                
                // Create extracted class data
                var extracted = new ExtractedClass
                {
                    ClassName = className,
                    Superclass = classInfo.Superclass,
                    InstanceVariables = classInfo.InstanceVariables,
                    ClassVariables = classInfo.ClassVariables,
                    PoolDictionaries = classInfo.PoolDictionaries,
                    Category = classInfo.Category,
                    InstanceMethods = new List<ExtractedMethod>(),
                    ClassMethods = new List<ExtractedMethod>(),
                    Statistics = new ExtractionStatistics()
                };
                
                // Step 3: Compile each method
                Console.WriteLine($"  Compiling {methods.Count} methods...");
                var instanceVars = classInfo.InstanceVariables;
                int successCount = 0;
                int failureCount = 0;
                
                foreach (var method in methods)
                {
                    try
                    {
                        // Compile the method
                        var compiledData = bytecodeCompiler.CompileMethod(method.Source, instanceVars);
                        
                        var extractedMethod = new ExtractedMethod
                        {
                            Selector = method.Selector,
                            Source = method.Source,
                            Category = method.Category,
                            ArgumentCount = compiledData.ArgumentCount,
                            TemporaryCount = compiledData.TemporaryCount,
                            Bytecode = BitConverter.ToString(compiledData.Bytecode).Replace("-", " "),
                            Literals = compiledData.Literals,
                            IsPrimitive = compiledData.IsPrimitive,
                            PrimitiveNumber = compiledData.PrimitiveNumber
                        };
                        
                        // Add to appropriate collection
                        if (method.IsClassMethod)
                        {
                            extracted.ClassMethods.Add(extractedMethod);
                        }
                        else
                        {
                            extracted.InstanceMethods.Add(extractedMethod);
                        }
                        
                        successCount++;
                        Console.Write(".");
                    }
                    catch (Exception ex)
                    {
                        // Add method with error information
                        var failedMethod = new ExtractedMethod
                        {
                            Selector = method.Selector,
                            Source = method.Source,
                            Category = method.Category,
                            CompilationError = ex.Message
                        };
                        
                        if (method.IsClassMethod)
                        {
                            extracted.ClassMethods.Add(failedMethod);
                        }
                        else
                        {
                            extracted.InstanceMethods.Add(failedMethod);
                        }
                        
                        failureCount++;
                        Console.Write("x");
                    }
                }
                
                Console.WriteLine(); // New line after progress dots
                
                // Update statistics
                extracted.Statistics.TotalMethods = methods.Count;
                extracted.Statistics.SuccessfullyCompiled = successCount;
                extracted.Statistics.FailedCompilation = failureCount;
                extracted.Statistics.CompilationRate = methods.Count > 0 ? 
                    (double)successCount / methods.Count * 100 : 0;
                
                // Sort methods by category and selector for better organization
                extracted.InstanceMethods = extracted.InstanceMethods
                    .OrderBy(m => m.Category ?? "")
                    .ThenBy(m => m.Selector)
                    .ToList();
                    
                extracted.ClassMethods = extracted.ClassMethods
                    .OrderBy(m => m.Category ?? "")
                    .ThenBy(m => m.Selector)
                    .ToList();
                
                // Report results
                Console.WriteLine($"  Instance methods: {extracted.InstanceMethods.Count}");
                Console.WriteLine($"  Class methods: {extracted.ClassMethods.Count}");
                Console.WriteLine($"  Compilation success rate: {extracted.Statistics.CompilationRate:F1}%");
                
                if (failureCount > 0)
                {
                    Console.WriteLine($"  Warning: {failureCount} methods failed to compile");
                }
                
                return extracted;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error extracting {className}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Save an extracted class to JSON file.
        /// </summary>
        public void SaveToJson(ExtractedClass extractedClass, string outputPath)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = 
                { 
                    new SymbolJsonConverter(), 
                    new NilObjectJsonConverter()
                }
            };
            
            var json = JsonSerializer.Serialize(extractedClass, options);
            File.WriteAllText(outputPath, json);
            
            var fileInfo = new FileInfo(outputPath);
            Console.WriteLine($"  Saved to {outputPath} ({fileInfo.Length:N0} bytes)");
        }
        
        /// <summary>
        /// Extract multiple classes and save to a single file.
        /// </summary>
        public void ExtractMultipleClasses(string systemImagePath, string[] classNames, string outputPath)
        {
            var extractedClasses = new List<ExtractedClass>();
            var failedClasses = new List<string>();
            
            Console.WriteLine($"\nExtracting {classNames.Length} classes from system image...");
            
            foreach (var className in classNames)
            {
                var extracted = ExtractClass(systemImagePath, className);
                if (extracted != null)
                {
                    extractedClasses.Add(extracted);
                }
                else
                {
                    failedClasses.Add(className);
                }
            }
            
            // Create wrapper for multiple classes
            var output = new
            {
                ExtractedClasses = extractedClasses,
                Count = extractedClasses.Count,
                Failed = failedClasses,
                ExtractionTime = DateTime.UtcNow,
                TotalMethods = extractedClasses.Sum(c => c.Statistics.TotalMethods),
                TotalCompiled = extractedClasses.Sum(c => c.Statistics.SuccessfullyCompiled),
                OverallCompilationRate = extractedClasses.Count > 0 ?
                    extractedClasses.Average(c => c.Statistics.CompilationRate) : 0
            };
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = 
                { 
                    new SymbolJsonConverter(), 
                    new NilObjectJsonConverter()
                }
            };
            
            var json = JsonSerializer.Serialize(output, options);
            File.WriteAllText(outputPath, json);
            
            Console.WriteLine($"\nSaved {extractedClasses.Count} classes to {outputPath}");
            if (failedClasses.Count > 0)
            {
                Console.WriteLine($"Failed to extract: {string.Join(", ", failedClasses)}");
            }
        }
    }
    
    /// <summary>
    /// JSON converter for Symbol objects.
    /// </summary>
    public class SymbolJsonConverter : JsonConverter<Symbol>
    {
        public override Symbol Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return new Symbol(value ?? "");
        }
        
        public override void Write(Utf8JsonWriter writer, Symbol value, JsonSerializerOptions options)
        {
            writer.WriteStringValue($"#{value.Value}");
        }
    }
    
    /// <summary>
    /// JSON converter for NilObject.
    /// </summary>
    public class NilObjectJsonConverter : JsonConverter<NilObject>
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