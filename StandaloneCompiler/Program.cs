using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace StandaloneCompiler
{
    /// <summary>
    /// Main entry point for the Smalltalk-80 standalone compiler.
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            // Check for test mode
            if (args.Length > 0 && args[0] == "--test-cascade")
            {
                Console.WriteLine("Running cascade compilation tests...\n");
                TestCascadeCompilation.RunTests();
                return;
            }
            
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: StandaloneCompiler <input.st> [output.json]");
                Console.WriteLine("       StandaloneCompiler --test-cascade");
                Console.WriteLine();
                Console.WriteLine("Compiles a Smalltalk source file to JSON with bytecode.");
                Console.WriteLine("Use --test-cascade to run cascade compilation tests.");
                return;
            }
            
            string inputFile = args[0];
            string outputFile = args.Length > 1 ? args[1] : Path.ChangeExtension(inputFile, ".json");
            
            try
            {
                Console.WriteLine($"Smalltalk-80 Standalone Compiler");
                Console.WriteLine($"=================================");
                Console.WriteLine($"Input:  {inputFile}");
                Console.WriteLine($"Output: {outputFile}");
                Console.WriteLine();
                
                if (!File.Exists(inputFile))
                {
                    Console.Error.WriteLine($"Error: Input file '{inputFile}' not found.");
                    return;
                }
                
                var compiler = new SmalltalkCompiler();
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
                Console.WriteLine($"  Initialization expressions: {result.InitializationExpressions?.Count ?? 0}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Compilation failed: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
            }
        }
    }
    
    /// <summary>
    /// The main Smalltalk compiler that coordinates parsing and compilation.
    /// </summary>
    public class SmalltalkCompiler
    {
        private readonly BytecodeCompiler bytecodeCompiler = new();
        private readonly Dictionary<string, ClassDefinition> classMap = new();
        private readonly List<InitializationExpression> initializationExpressions = new();
        
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
                Methods = new List<MethodDefinition>(),
                InitializationExpressions = new List<InitializationExpression>()
            };
            
            // Parse the entire source file using ANTLR
            var inputStream = new AntlrInputStream(source);
            var lexer = new Smalltalk80Lexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new Smalltalk80Parser(tokenStream);
            
            // Parse as file-in format (chunks separated by !)
            var fileInContext = parser.fileIn();
            
            // Visit the parse tree to extract classes, methods, and file-scope expressions
            var visitor = new SmalltalkVisitor(this, result, tokenStream);
            // Console.WriteLine($"Parse tree has {fileInContext.chunk()?.Length ?? 0} chunks");
            visitor.Visit(fileInContext);
            
            // Copy collected data to result
            result.Classes = new List<ClassDefinition>(classMap.Values);
            result.InitializationExpressions = new List<InitializationExpression>(initializationExpressions);
            
            return result;
        }
        
        /// <summary>
        /// Parse tree visitor that extracts classes, methods, and file-scope expressions.
        /// </summary>
        private class SmalltalkVisitor : Smalltalk80BaseVisitor<object?>
        {
            private readonly SmalltalkCompiler compiler;
            private readonly CompilationResult result;
            private readonly ITokenStream tokenStream;
            private ClassDefinition? currentClass;
            private bool isClassMethod;
            private string? currentCategory;
            
            public SmalltalkVisitor(SmalltalkCompiler compiler, CompilationResult result, ITokenStream tokenStream)
            {
                this.compiler = compiler;
                this.result = result;
                this.tokenStream = tokenStream;
            }
            
            public override object? VisitClassDefChunk(Smalltalk80Parser.ClassDefChunkContext context)
            {
                // Extract class definition details
                var classHeader = context.classHeader();
                // In the grammar: className subclassKind symbolConst
                // className is actually the superclass name
                // symbolConst is the new class being defined
                var superclass = classHeader.className()?.GetText() ?? "Object";
                var className = classHeader.symbolConst()?.GetText()?.TrimStart('#') ?? "";
                
                // Parse class body for instance variables, etc.
                var classBody = context.classBody();
                var instanceVars = new List<string>();
                var classVars = new List<string>();
                var poolDicts = new List<string>();
                var category = "";
                
                foreach (var spec in classBody.classSlotSpec())
                {
                    var specType = spec.identifierColon()?.GetText();
                    var specValue = ExtractStringLiteral(spec.stringConst()?.GetText());
                    
                    switch (specType)
                    {
                        case "instanceVariableNames:":
                            instanceVars = ParseVariableNames(specValue);
                            break;
                        case "classVariableNames:":
                            classVars = ParseVariableNames(specValue);
                            break;
                        case "poolDictionaries:":
                            poolDicts = ParseVariableNames(specValue);
                            break;
                    }
                }
                
                if (classBody.categoryDecl() != null)
                {
                    category = ExtractStringLiteral(classBody.categoryDecl().stringConst()?.GetText());
                }
                
                currentClass = new ClassDefinition
                {
                    Name = className,
                    Superclass = superclass,
                    InstanceVariables = instanceVars,
                    ClassVariables = classVars,
                    PoolDictionaries = poolDicts,
                    Category = category
                };
                
                compiler.classMap[currentClass.Name] = currentClass;
                Console.WriteLine($"Found class: {currentClass.Name}");
                
                return base.VisitClassDefChunk(context);
            }
            
            public override object? VisitSimpleMethodChunk(Smalltalk80Parser.SimpleMethodChunkContext context)
            {
                // Get the class name from the simple method chunk  
                var className = context.className()?.GetText() ?? "";
                
                // Check if it's a class method (the optional "class" keyword after the class name)
                bool isClassMethodDef = context.IDENTIFIER()?.GetText() == "class";
                
                // If we don't have this class yet, create a stub for it
                if (!compiler.classMap.TryGetValue(className, out var classDef))
                {
                        // Creating stub class definition
                    classDef = new ClassDefinition
                    {
                        Name = className,
                        Superclass = "Object", // Default superclass
                        InstanceVariables = new List<string>(),
                        ClassVariables = new List<string>(),
                        PoolDictionaries = new List<string>(),
                        Category = ""
                    };
                    compiler.classMap[className] = classDef;
                }
                
                currentClass = classDef;
                isClassMethod = isClassMethodDef;
                
                // Compile the single method
                CompileMethod(context.methodDef());
                
                return base.VisitSimpleMethodChunk(context);
            }
            
            public override object? VisitMethodCategoryChunk(Smalltalk80Parser.MethodCategoryChunkContext context)
            {
                // Get the class name from the method category chunk
                var className = context.className()?.GetText() ?? "";
                
                // If we don't have this class yet, create a stub for it
                if (!compiler.classMap.TryGetValue(className, out var classDef))
                {
                        // Creating stub class definition
                    classDef = new ClassDefinition
                    {
                        Name = className,
                        Superclass = "Object", // Default superclass
                        InstanceVariables = new List<string>(),
                        ClassVariables = new List<string>(),
                        PoolDictionaries = new List<string>(),
                        Category = ""
                    };
                    compiler.classMap[className] = classDef;
                }
                
                currentClass = classDef;
                
                // Check if this is a class method by looking for the "class" identifier in the parse tree
                // The grammar: className (cls = IDENTIFIER)? methodsFor: '...'
                // If IDENTIFIER is present and equals "class", these are class methods
                isClassMethod = context.IDENTIFIER()?.GetText() == "class";
                
                // Extract category from the string literal
                var stringConstContext = context.stringConst();
                if (stringConstContext != null)
                {
                    var categoryText = stringConstContext.GetText();
                    // Remove surrounding quotes
                    currentCategory = categoryText.Trim('\'', '"');
                }
                
                // Visit methods in this category
                foreach (var method in context.methodDef())
                {
                    CompileMethod(method);
                }
                
                return base.VisitMethodCategoryChunk(context);
            }
            
            private void CompileMethod(Smalltalk80Parser.MethodDefContext context)
            {
                if (currentClass == null) return;
                
                try
                {
                    // Get all inherited instance variables for instance methods
                    var instanceVars = isClassMethod 
                        ? new List<string>()
                        : ComputeInheritedInstanceVariables(currentClass.Name);
                    
                    // Compile the method using BytecodeCompiler - pass the parse tree directly
                    var compiledData = compiler.bytecodeCompiler.CompileMethodFromTree(context, instanceVars);
                    
                    // Calculate stack depth from the generated bytecode
                    var stackDepth = BytecodeEmitter.CalculateMaxStackDepth(compiledData.Bytecode);
                    
                    var method = new MethodDefinition
                    {
                        Class = isClassMethod ? $"{currentClass.Name} class" : currentClass.Name,
                        Selector = compiledData.Selector,
                        Bytecode = compiledData.Bytecode,
                        Literals = ConvertLiterals(compiledData.Literals),
                        PrimitiveIndex = compiledData.PrimitiveNumber,
                        NumArgs = compiledData.ArgumentCount,
                        NumTemps = compiledData.TemporaryCount,
                        StackDepth = stackDepth,
                        IsClassMethod = isClassMethod
                    };
                    
                    // Add method to the appropriate collection in the class
                    if (isClassMethod)
                    {
                        currentClass.ClassMethods.Add(method);
                    }
                    else
                    {
                        currentClass.Methods.Add(method);
                    }
                    
                    // Also add to global methods list for backward compatibility
                    result.Methods?.Add(method);
                    Console.WriteLine($"  Compiled {method.Class ?? "Unknown"}>>{method.Selector}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  Failed to compile method in {currentClass.Name}: {ex.Message}");
                }
            }
            
            public override object? VisitExpressionChunk(Smalltalk80Parser.ExpressionChunkContext context)
            {
                // This is a file-scope expression!
                var expression = context.expression();
                
                // Get the original source text with proper spacing using the CharStream
                // This preserves all whitespace, comments, and formatting
                var start = expression.Start;
                var stop = expression.Stop;
                var charStream = start.InputStream;
                var charStart = start.StartIndex;
                var charStop = stop?.StopIndex ?? charStart;
                var sourceText = charStream.GetText(new Antlr4.Runtime.Misc.Interval(charStart, charStop));
                
                // Compile the expression to bytecode
                var compiledData = compiler.bytecodeCompiler.CompileExpressionFromTree(expression);
                
                // Calculate stack depth from the generated bytecode
                var stackDepth = BytecodeEmitter.CalculateMaxStackDepth(compiledData.Bytecode);
                
                // Also extract receiver and selector for documentation
                string receiver = "";
                string selector = "";
                var arguments = new List<string>();
                
                // Navigate the parse tree to extract components for documentation
                var assignmentExpr = expression.assignmentExpr();
                if (assignmentExpr != null)
                {
                    var cascadeExpr = assignmentExpr.cascadeExpr();
                    if (cascadeExpr != null)
                    {
                        var messageExpr = cascadeExpr.messageExpr();
                        if (messageExpr != null)
                        {
                            // Extract the receiver (messageBase)
                            var messageBase = messageExpr.messageBase();
                            if (messageBase != null)
                            {
                                receiver = tokenStream.GetText(messageBase.Start, messageBase.Stop);
                            }
                            
                            // Extract the selector from messages
                            var unaryMessages = messageExpr.unaryMessage();
                            if (unaryMessages != null && unaryMessages.Length > 0)
                            {
                                selector = unaryMessages[0].unarySelector()?.GetText() ?? "";
                            }
                            else 
                            {
                                var binaryMessages = messageExpr.binaryMessage();
                                if (binaryMessages != null && binaryMessages.Length > 0)
                                {
                                    selector = binaryMessages[0].binarySelector()?.GetText() ?? "";
                                    var arg = binaryMessages[0].messageBase();
                                    if (arg != null)
                                    {
                                        arguments.Add(tokenStream.GetText(arg.Start, arg.Stop));
                                    }
                                }
                                else
                                {
                                    var keywordMessage = messageExpr.keywordMessage();
                                    if (keywordMessage != null)
                                    {
                                        var keywordParts = keywordMessage.keywordPart();
                                        selector = string.Join("", keywordParts.Select(p => p.IDENTIFIER()?.GetText() + ":"));
                                        foreach (var part in keywordParts)
                                        {
                                            var argExpr = part.nonKeywordExpr();
                                            if (argExpr != null)
                                            {
                                                arguments.Add(tokenStream.GetText(argExpr.Start, argExpr.Stop));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                var initExpr = new InitializationExpression
                {
                    Bytecode = compiledData.Bytecode,
                    Literals = ConvertLiterals(compiledData.Literals),
                    NumTemps = compiledData.TemporaryCount,
                    StackDepth = stackDepth,
                    Source = sourceText,
                    // These are optional/documentation fields
                    Receiver = string.IsNullOrEmpty(receiver) ? null : receiver,
                    Selector = string.IsNullOrEmpty(selector) ? null : selector,
                    Arguments = arguments.Count > 0 ? arguments : null
                };
                
                compiler.initializationExpressions.Add(initExpr);
                Console.WriteLine($"  Found file-scope expression: {receiver} {selector}");
                
                return base.VisitExpressionChunk(context);
            }
            
            private List<string> ParseVariableNames(string? varString)
            {
                if (string.IsNullOrWhiteSpace(varString))
                    return new List<string>();
                
                // Remove quotes and split
                varString = varString.Trim('\'', '"');
                return varString.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }
            
            private string ExtractStringLiteral(string? text)
            {
                if (string.IsNullOrEmpty(text))
                    return "";
                
                // Remove surrounding quotes
                text = text.Trim();
                if ((text.StartsWith("'") && text.EndsWith("'")) ||
                    (text.StartsWith("\"") && text.EndsWith("\"")))
                {
                    return text.Substring(1, text.Length - 2);
                }
                return text;
            }
            
            private string? ExtractMethodCategory(string methodsForText)
            {
                // Extract category from "methodsFor: 'category'" pattern
                var match = Regex.Match(methodsForText, @"methodsFor:\s*'([^']*)'");
                if (match.Success)
                    return match.Groups[1].Value;
                return null;
            }
            
            private List<string> ComputeInheritedInstanceVariables(string className)
            {
                var allVars = new List<string>();
                var visited = new HashSet<string>();
                
                var current = className;
                while (!string.IsNullOrEmpty(current) && current != "nil" && visited.Add(current))
                {
                    if (compiler.classMap.TryGetValue(current, out var classDef))
                    {
                        if (classDef.InstanceVariables.Count > 0)
                        {
                            allVars.InsertRange(0, classDef.InstanceVariables);
                        }
                        current = classDef.Superclass;
                        if (current == "Object" || current == "nil" || string.IsNullOrEmpty(current))
                        {
                            break;
                        }
                    }
                    else
                    {
                        // Handle well-known system classes
                        break;
                    }
                }
                
                return allVars;
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
                    else if (literal is int || literal is long || literal is short || literal is byte)
                    {
                        // Preserve numeric literals as numbers
                        result.Add(literal);
                    }
                    else if (literal is float || literal is double || literal is decimal)
                    {
                        // Preserve floating point literals as numbers
                        result.Add(literal);
                    }
                    else if (literal is object[] array)
                    {
                        // Handle array literals recursively
                        var arrayElements = new List<object>();
                        foreach (var element in array)
                        {
                            // Recursively convert each element
                            var converted = ConvertLiterals(new[] { element });
                            arrayElements.Add(converted[0]);
                        }
                        result.Add(arrayElements);
                    }
                    else
                    {
                        // For strings and other types, keep as is or convert to string
                        result.Add(literal.ToString() ?? "");
                    }
                }
                return result;
            }
        }
    }
    
    // Data structure classes
    public class CompilationResult
    {
        [JsonPropertyName("$schema")]
        public string? Schema { get; set; }
        public string? Description { get; set; }
        public List<ClassDefinition>? Classes { get; set; }
        public List<MethodDefinition>? Methods { get; set; }
        public List<InitializationExpression>? InitializationExpressions { get; set; }
    }
    
    public class ClassDefinition
    {
        public string Name { get; set; } = "";
        public string Superclass { get; set; } = "Object";
        public List<string> InstanceVariables { get; set; } = new();
        public List<string> ClassVariables { get; set; } = new();
        public List<string> PoolDictionaries { get; set; } = new();
        public string Category { get; set; } = "";
        public List<MethodDefinition> Methods { get; set; } = new();
        public List<MethodDefinition> ClassMethods { get; set; } = new();
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
        public int StackDepth { get; set; }
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