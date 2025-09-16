using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace StandaloneCompiler
{
    /// <summary>
    /// Simple Smalltalk compiler that extracts classes and compiles methods to bytecode.
    /// </summary>
    public class SimpleCompiler
    {
        private readonly BytecodeCompiler bytecodeCompiler = new();
        private readonly Dictionary<string, ClassDefinition> classMap = new();
        
        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: SimpleCompiler <input.st> [output.json]");
                Console.WriteLine();
                Console.WriteLine("Compiles a Smalltalk source file to JSON with bytecode.");
                return;
            }
            
            string inputFile = args[0];
            string outputFile = args.Length > 1 ? args[1] : Path.ChangeExtension(inputFile, ".json");
            
            try
            {
                Console.WriteLine($"Smalltalk-80 Compiler");
                Console.WriteLine($"=====================");
                Console.WriteLine($"Input:  {inputFile}");
                Console.WriteLine($"Output: {outputFile}");
                Console.WriteLine();
                
                if (!File.Exists(inputFile))
                {
                    Console.Error.WriteLine($"Error: Input file '{inputFile}' not found.");
                    return;
                }
                
                var compiler = new SimpleCompiler();
                var result = compiler.CompileFile(inputFile);
                
                // Write JSON output
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters = { new ByteArrayJsonConverter() }
                };
                
                string json = JsonSerializer.Serialize(result, options);
                File.WriteAllText(outputFile, json);
                
                Console.WriteLine($"\nCompilation complete!");
                Console.WriteLine($"  Classes: {result.Classes?.Count ?? 0}");
                Console.WriteLine($"  Methods: {result.Methods?.Count ?? 0}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Compilation failed: {ex.Message}");
            }
        }
        
        public CompilationResult CompileFile(string filePath)
        {
            var source = File.ReadAllText(filePath);
            return CompileSource(source);
        }
        
        public CompilationResult CompileSource(string source)
        {
            var result = new CompilationResult
            {
                Schema = "./bootstrap-schema.json",
                Description = $"Compiled from source on {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                Classes = new List<ClassDefinition>(),
                Methods = new List<MethodDefinition>()
            };
            
            // Clear class map for new compilation
            classMap.Clear();
            
            // Extract classes - handle all types of class definitions
            var classPattern = @"(\w+)\s+(subclass:|variableSubclass:|variableByteSubclass:|variableWordSubclass:)\s+#(\w+)\s+" +
                              @"instanceVariableNames:\s+'([^']*)'\s+" +
                              @"classVariableNames:\s+'([^']*)'\s+" +
                              @"poolDictionaries:\s+'([^']*)'\s+" +
                              @"category:\s+'([^']*)'";
            
            var classMatches = Regex.Matches(source, classPattern, RegexOptions.Multiline);
            
            foreach (Match match in classMatches)
            {
                var classDef = new ClassDefinition
                {
                    Name = match.Groups[3].Value,  // Group 3 is now the class name
                    Superclass = match.Groups[1].Value,
                    InstanceVariables = ParseVariableNames(match.Groups[4].Value),
                    ClassVariables = ParseVariableNames(match.Groups[5].Value),
                    PoolDictionaries = ParseVariableNames(match.Groups[6].Value),
                    Category = match.Groups[7].Value
                };
                
                result.Classes.Add(classDef);
                classMap[classDef.Name] = classDef;
                Console.WriteLine($"Found class: {classDef.Name}");
            }
            
            // Extract and compile methods
            foreach (var classDef in result.Classes)
            {
                // Instance methods
                var instanceMethodPattern = $@"!{Regex.Escape(classDef.Name)}\s+methodsFor:\s+'([^']*)'!(.*?)! !";
                var instanceMatches = Regex.Matches(source, instanceMethodPattern, RegexOptions.Singleline);
                
                foreach (Match match in instanceMatches)
                {
                    var category = match.Groups[1].Value;
                    var methodsBlock = match.Groups[2].Value;
                    // Get all inherited instance variables for instance methods
                    var allInstanceVars = ComputeInheritedInstanceVariables(classDef.Name);
                    CompileMethods(methodsBlock, classDef.Name, false, category, allInstanceVars, result.Methods);
                }
                
                // Class methods
                var classMethodPattern = $@"!{Regex.Escape(classDef.Name)}\s+class\s+methodsFor:\s+'([^']*)'!(.*?)! !";
                var classMethodMatches = Regex.Matches(source, classMethodPattern, RegexOptions.Singleline);
                
                foreach (Match match in classMethodMatches)
                {
                    var category = match.Groups[1].Value;
                    var methodsBlock = match.Groups[2].Value;
                    // For class methods, we need the metaclass's instance variables
                    // In Smalltalk-80, metaclasses typically have: thisClass, name, comment, subclasses, instanceVariables, etc.
                    // For now, we'll use an empty list as metaclass instance vars are rarely accessed directly
                    var metaclassVars = GetMetaclassInstanceVariables(classDef.Name);
                    CompileMethods(methodsBlock, classDef.Name, true, category, metaclassVars, result.Methods);
                }
            }
            
            return result;
        }
        
        private void CompileMethods(string methodsBlock, string className, bool isClassMethod, 
                                   string category, List<string> instanceVars, List<MethodDefinition> methods)
        {
            // Split methods by ! delimiter - match any non-whitespace character after newline
            var methodSources = Regex.Split(methodsBlock, @"(?<=!)\s*\n(?=\S)");
            
            foreach (var methodSource in methodSources.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                var trimmedSource = methodSource.Trim();
                if (trimmedSource.EndsWith("!"))
                {
                    trimmedSource = trimmedSource.Substring(0, trimmedSource.Length - 1).Trim();
                }
                
                if (string.IsNullOrWhiteSpace(trimmedSource))
                    continue;
                
                try
                {
                    // Compile the method
                    var compiledData = bytecodeCompiler.CompileMethod(trimmedSource, instanceVars);
                    
                    var method = new MethodDefinition
                    {
                        Class = isClassMethod ? $"{className} class" : className,
                        Selector = compiledData.Selector,
                        Bytecode = compiledData.Bytecode,
                        Literals = ConvertLiterals(compiledData.Literals),
                        PrimitiveIndex = compiledData.PrimitiveNumber,
                        NumArgs = compiledData.ArgumentCount,
                        NumTemps = compiledData.TemporaryCount,
                        IsClassMethod = isClassMethod
                    };
                    
                    methods.Add(method);
                    Console.WriteLine($"  Compiled {method.Class}>>{method.Selector}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  Failed to compile {className}>>{ExtractSelector(trimmedSource)}: {ex.Message}");
                }
            }
        }
        
        private List<string> ParseVariableNames(string varString)
        {
            if (string.IsNullOrWhiteSpace(varString))
                return new List<string>();
            
            return varString.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        
        /// <summary>
        /// Computes the complete list of instance variables for a class, including inherited ones.
        /// Instance variables are ordered with superclass variables first.
        /// </summary>
        private List<string> ComputeInheritedInstanceVariables(string className)
        {
            var allVars = new List<string>();
            var visited = new HashSet<string>(); // Prevent infinite loops
            
            var current = className;
            while (!string.IsNullOrEmpty(current) && current != "nil" && visited.Add(current))
            {
                if (classMap.TryGetValue(current, out var classDef))
                {
                    // Prepend parent variables (we'll reverse at the end)
                    if (classDef.InstanceVariables.Count > 0)
                    {
                        allVars.InsertRange(0, classDef.InstanceVariables);
                    }
                    
                    current = classDef.Superclass;
                    
                    // Stop at Object or if no superclass
                    if (current == "Object" || current == "nil" || string.IsNullOrEmpty(current))
                    {
                        // Object class has no instance variables in Smalltalk-80
                        break;
                    }
                }
                else if (IsWellKnownClass(current, out var knownVars) && knownVars != null)
                {
                    // Handle well-known system classes not in current compilation
                    allVars.InsertRange(0, knownVars);
                    break; // Well-known classes are terminal
                }
                else
                {
                    // Unknown superclass - stop here
                    Console.WriteLine($"Warning: Unknown superclass '{current}' for class '{className}'");
                    break;
                }
            }
            
            return allVars;
        }
        
        /// <summary>
        /// Gets instance variables for well-known Smalltalk-80 system classes.
        /// Returns true if the class is known, false otherwise.
        /// </summary>
        private bool IsWellKnownClass(string className, out List<string>? instanceVars)
        {
            // Note: These are the COMPLETE instance variables including inherited ones
            // This is used when we can't find the class in our current compilation
            instanceVars = className switch
            {
                "Object" => new List<string>(),
                "Collection" => new List<string> { "tally" },
                "Set" => new List<string> { "tally" },
                "Dictionary" => new List<string> { "tally" },
                "IdentityDictionary" => new List<string> { "tally", "valueArray" },
                "MethodDictionary" => new List<string> { "tally", "valueArray" },
                "SequenceableCollection" => new List<string>(),
                "ArrayedCollection" => new List<string>(),
                "Array" => new List<string>(),
                "String" => new List<string>(),
                "Symbol" => new List<string>(),
                "ByteArray" => new List<string>(),
                "OrderedCollection" => new List<string> { "firstIndex", "lastIndex" },
                "SortedCollection" => new List<string> { "firstIndex", "lastIndex", "sortBlock" },
                "Bag" => new List<string> { "contents" },
                "Magnitude" => new List<string>(),
                "Number" => new List<string>(),
                "Integer" => new List<string>(),
                "SmallInteger" => new List<string>(),
                "LargePositiveInteger" => new List<string>(),
                "LargeNegativeInteger" => new List<string>(),
                "Float" => new List<string>(),
                "Fraction" => new List<string> { "numerator", "denominator" },
                "LookupKey" => new List<string> { "key" },
                "Association" => new List<string> { "key", "value" },
                "Link" => new List<string> { "nextLink" },
                "Process" => new List<string> { "suspendedContext", "priority", "myList" },
                "Context" => new List<string> { "sender", "instructionPointer", "stackPointer", "method", "receiver" },
                "BlockContext" => new List<string> { "sender", "instructionPointer", "stackPointer", "method", "receiver", "nargs", "startpc", "home" },
                "MethodContext" => new List<string> { "sender", "instructionPointer", "stackPointer", "method", "receiver" },
                "CompiledMethod" => new List<string>(),
                "Behavior" => new List<string> { "superclass", "methodDict", "format" },
                "ClassDescription" => new List<string> { "superclass", "methodDict", "format", "instanceVariables" },
                "Metaclass" => new List<string> { "superclass", "methodDict", "format", "instanceVariables", "thisClass" },
                "Class" => new List<string> { "superclass", "methodDict", "format", "instanceVariables", "name", "classPool", "sharedPools" },
                _ => null
            };
            
            return instanceVars != null;
        }
        
        /// <summary>
        /// Gets instance variables for a metaclass.
        /// </summary>
        private List<string> GetMetaclassInstanceVariables(string className)
        {
            // Metaclasses in Smalltalk-80 inherit from Class
            // Class has these instance variables: superclass, methodDict, format, instanceVariables, name, classPool, sharedPools
            // Metaclass adds: thisClass
            // For simplicity, return empty list as these are rarely accessed directly in class methods
            return new List<string>();
        }
        
        private string ExtractSelector(string methodSource)
        {
            // Handle unary selectors
            var unaryMatch = Regex.Match(methodSource, @"^(\w+)\s*(\n|$|\|)");
            if (unaryMatch.Success)
                return unaryMatch.Groups[1].Value;
            
            // Handle binary selectors
            var binaryMatch = Regex.Match(methodSource, @"^([+\-*/\\<>=~@%&|,]+)\s+\w+");
            if (binaryMatch.Success)
                return binaryMatch.Groups[1].Value;
            
            // Handle keyword selectors
            var keywordParts = new List<string>();
            var keywordPattern = @"(\w+:)";
            var keywordMatches = Regex.Matches(methodSource.Split('\n')[0], keywordPattern);
            
            foreach (Match match in keywordMatches)
            {
                keywordParts.Add(match.Groups[1].Value);
            }
            
            if (keywordParts.Count > 0)
                return string.Join("", keywordParts);
            
            return "unknown";
        }
        
        private List<object> ConvertLiterals(object[] literals)
        {
            var result = new List<object>();
            foreach (var literal in literals)
            {
                if (literal is Symbol symbol)
                {
                    result.Add($"#{symbol.Value}");
                }
                else if (literal is NilObject)
                {
                    result.Add("nil");
                }
                else if (literal == null)
                {
                    result.Add("nil");
                }
                else if (literal is bool b)
                {
                    result.Add(b ? "true" : "false");
                }
                else
                {
                    result.Add(literal.ToString() ?? "");
                }
            }
            return result;
        }
    }
    
    public class CompilationResult
    {
        [JsonPropertyName("$schema")]
        public string? Schema { get; set; }
        public string? Description { get; set; }
        public List<ClassDefinition>? Classes { get; set; }
        public List<MethodDefinition>? Methods { get; set; }
    }
    
    public class ClassDefinition
    {
        public string Name { get; set; } = "";
        public string Superclass { get; set; } = "Object";
        public List<string> InstanceVariables { get; set; } = new();
        public List<string> ClassVariables { get; set; } = new();
        public List<string> PoolDictionaries { get; set; } = new();
        public string Category { get; set; } = "";
    }
    
    public class MethodDefinition
    {
        public string Class { get; set; } = "";
        public string Selector { get; set; } = "";
        public byte[] Bytecode { get; set; } = Array.Empty<byte>();
        public List<object> Literals { get; set; } = new();
        public int PrimitiveIndex { get; set; }
        public int NumArgs { get; set; }
        public int NumTemps { get; set; }
        public bool IsClassMethod { get; set; }
    }
    
    public class ByteArrayJsonConverter : JsonConverter<byte[]>
    {
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var list = new List<byte>();
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    list.Add((byte)reader.GetInt32());
                }
            }
            return list.ToArray();
        }
        
        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var b in value)
            {
                writer.WriteNumberValue(b);
            }
            writer.WriteEndArray();
        }
    }
}