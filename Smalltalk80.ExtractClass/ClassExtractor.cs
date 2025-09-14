using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Smalltalk80.ExtractClass
{
    /// <summary>
    /// Represents basic class information from the system image.
    /// </summary>
    public class ClassInfo
    {
        public string Name { get; set; } = "";
        public string Superclass { get; set; } = "Object";
        public List<string> InstanceVariables { get; set; } = new();
        public List<string> ClassVariables { get; set; } = new();
        public List<string> PoolDictionaries { get; set; } = new();
        public string? Category { get; set; }
    }
    
    /// <summary>
    /// Represents basic method information from the system image.
    /// </summary>
    public class MethodInfo
    {
        public string Selector { get; set; } = "";
        public string Source { get; set; } = "";
        public bool IsClassMethod { get; set; }
        public string? Category { get; set; }
    }
    
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
        private readonly string compilerPath;
        
        public ClassExtractor()
        {
            // Find the compiler executable
            compilerPath = FindCompilerExecutable();
        }
        
        private string FindCompilerExecutable()
        {
            // Look for the compiler in common locations
            var possiblePaths = new[]
            {
                "Smalltalk80.Compiler.exe",
                "./Smalltalk80.Compiler.exe",
                "../Smalltalk80.Compiler/bin/Debug/net9.0/Smalltalk80.Compiler.exe",
                "../Smalltalk80.Compiler/bin/Release/net9.0/Smalltalk80.Compiler.exe",
                "./bin/Debug/net9.0/Smalltalk80.Compiler.exe",
                "./bin/Release/net9.0/Smalltalk80.Compiler.exe"
            };
            
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }
            
            // Default to hoping it's in the PATH
            return "Smalltalk80.Compiler";
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
                
                // Step 3: Generate Smalltalk source file
                Console.WriteLine($"  Generating source for {methods.Count} methods...");
                var sourceBuilder = GenerateClassSource(classInfo, methods);
                
                // Step 4: Write to temporary file and compile
                var tempSourceFile = Path.GetTempFileName() + ".st";
                var tempJsonFile = Path.ChangeExtension(tempSourceFile, ".json");
                
                int successCount = 0;
                int failureCount = 0;
                
                try
                {
                    File.WriteAllText(tempSourceFile, sourceBuilder.ToString());
                    Console.WriteLine($"  Compiling with Smalltalk80.Compiler...");
                    
                    // Run the compiler
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = compilerPath,
                        Arguments = $"\"{tempSourceFile}\" \"{tempJsonFile}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    
                    using (var process = System.Diagnostics.Process.Start(startInfo))
                    {
                        if (process == null)
                        {
                            throw new Exception("Failed to start compiler process");
                        }
                        
                        var output = process.StandardOutput.ReadToEnd();
                        var error = process.StandardError.ReadToEnd();
                        process.WaitForExit();
                        
                        if (process.ExitCode != 0)
                        {
                            throw new Exception($"Compiler failed: {error}");
                        }
                    }
                    
                    // Read the compiled JSON
                    if (!File.Exists(tempJsonFile))
                    {
                        throw new Exception("Compiler did not produce output file");
                    }
                    
                    var compiledJson = File.ReadAllText(tempJsonFile);
                    var compiledManifest = JsonSerializer.Deserialize<CompilerOutput>(compiledJson);
                    
                    if (compiledManifest == null || compiledManifest.Methods == null)
                    {
                        throw new Exception("Invalid compiler output");
                    }
                    
                    // Convert compiler output to extracted methods
                    
                    foreach (var compiledMethod in compiledManifest.Methods)
                    {
                        var extractedMethod = new ExtractedMethod
                        {
                            Selector = compiledMethod.Selector,
                            Source = "", // We'll need to match this from original methods
                            Category = "",
                            ArgumentCount = compiledMethod.NumArgs,
                            TemporaryCount = compiledMethod.NumTemps,
                            Bytecode = BitConverter.ToString(compiledMethod.Bytecode ?? Array.Empty<byte>()).Replace("-", " "),
                            Literals = compiledMethod.Literals?.ToArray() ?? Array.Empty<object>(),
                            IsPrimitive = compiledMethod.PrimitiveIndex > 0,
                            PrimitiveNumber = compiledMethod.PrimitiveIndex
                        };
                        
                        // Find the original method to get source and category
                        var originalMethod = methods.FirstOrDefault(m => 
                            m.Selector == compiledMethod.Selector && 
                            m.IsClassMethod == compiledMethod.IsClassMethod);
                        
                        if (originalMethod != null)
                        {
                            extractedMethod.Source = originalMethod.Source;
                            extractedMethod.Category = originalMethod.Category;
                        }
                        
                        // Add to appropriate collection
                        if (compiledMethod.IsClassMethod)
                        {
                            extracted.ClassMethods.Add(extractedMethod);
                        }
                        else
                        {
                            extracted.InstanceMethods.Add(extractedMethod);
                        }
                        
                        successCount++;
                    }
                    
                    Console.WriteLine($"  Compiled {successCount} methods successfully");
                }
                finally
                {
                    // Clean up temp files
                    if (File.Exists(tempSourceFile)) File.Delete(tempSourceFile);
                    if (File.Exists(tempJsonFile)) File.Delete(tempJsonFile);
                }
                
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
        
        /// <summary>
        /// Generate Smalltalk source code for a class.
        /// </summary>
        private StringBuilder GenerateClassSource(ClassInfo classInfo, List<MethodInfo> methods)
        {
            var sb = new StringBuilder();
            
            // Generate class definition
            sb.AppendLine($"{classInfo.Superclass} subclass: #{classInfo.Name}");
            sb.AppendLine($"\tinstanceVariableNames: '{string.Join(" ", classInfo.InstanceVariables)}'");
            sb.AppendLine($"\tclassVariableNames: '{string.Join(" ", classInfo.ClassVariables)}'");
            sb.AppendLine($"\tpoolDictionaries: '{string.Join(" ", classInfo.PoolDictionaries)}'");
            sb.AppendLine($"\tcategory: '{classInfo.Category ?? "System"}'");
            sb.AppendLine("!");
            sb.AppendLine();
            
            // Group methods by category and type
            var instanceMethods = methods.Where(m => !m.IsClassMethod)
                .GroupBy(m => m.Category ?? "accessing")
                .OrderBy(g => g.Key);
            
            var classMethods = methods.Where(m => m.IsClassMethod)
                .GroupBy(m => m.Category ?? "instance creation")
                .OrderBy(g => g.Key);
            
            // Generate instance methods
            foreach (var categoryGroup in instanceMethods)
            {
                sb.AppendLine($"!{classInfo.Name} methodsFor: '{categoryGroup.Key}'!");
                sb.AppendLine();
                
                foreach (var method in categoryGroup.OrderBy(m => m.Selector))
                {
                    sb.AppendLine(method.Source);
                    sb.AppendLine("!");
                    sb.AppendLine();
                }
                
                sb.AppendLine("!");
                sb.AppendLine();
            }
            
            // Generate class methods
            foreach (var categoryGroup in classMethods)
            {
                sb.AppendLine($"!{classInfo.Name} class methodsFor: '{categoryGroup.Key}'!");
                sb.AppendLine();
                
                foreach (var method in categoryGroup.OrderBy(m => m.Selector))
                {
                    sb.AppendLine(method.Source);
                    sb.AppendLine("!");
                    sb.AppendLine();
                }
                
                sb.AppendLine("!");
                sb.AppendLine();
            }
            
            return sb;
        }
    }
    
    /// <summary>
    /// Represents the output from the Smalltalk80.Compiler.
    /// </summary>
    internal class CompilerOutput
    {
        [JsonPropertyName("classes")]
        public List<object>? Classes { get; set; }
        
        [JsonPropertyName("methods")]
        public List<CompiledMethod>? Methods { get; set; }
    }
    
    /// <summary>
    /// Represents a compiled method from the compiler output.
    /// </summary>
    internal class CompiledMethod
    {
        [JsonPropertyName("class")]
        public string Class { get; set; } = "";
        
        [JsonPropertyName("selector")]
        public string Selector { get; set; } = "";
        
        [JsonPropertyName("bytecode")]
        public byte[]? Bytecode { get; set; }
        
        [JsonPropertyName("literals")]
        public List<object>? Literals { get; set; }
        
        [JsonPropertyName("primitiveIndex")]
        public int PrimitiveIndex { get; set; }
        
        [JsonPropertyName("numArgs")]
        public int NumArgs { get; set; }
        
        [JsonPropertyName("numTemps")]
        public int NumTemps { get; set; }
        
        [JsonPropertyName("isClassMethod")]
        public bool IsClassMethod { get; set; }
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
    /// Represents nil object for JSON conversion.
    /// </summary>
    public class NilObject
    {
        public override string ToString() => "nil";
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