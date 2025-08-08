using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Antlr4.Runtime;
using Smalltalk80.Generated;

namespace Smalltalk80.Compiler
{
    /// <summary>
    /// Compiles Phase 2 methods from the system image to bytecode.
    /// </summary>
    public class Phase2BytecodeCompiler
    {
        private readonly SimpleBytecodeGenerator bytecodeGenerator;
        private readonly SimpleClassExtractor classExtractor;
        
        public Phase2BytecodeCompiler()
        {
            bytecodeGenerator = new SimpleBytecodeGenerator();
            classExtractor = new SimpleClassExtractor();
        }
        
        /// <summary>
        /// Compiles all Phase 2 methods to bytecode and updates the JSON.
        /// </summary>
        public void CompilePhase2Methods(string systemImagePath, string phase2JsonPath)
        {
            Console.WriteLine("Compiling Phase 2 methods to bytecode...");
            
            // Get Phase 2 classes from the current JSON
            var phase2Classes = GetPhase2Classes(phase2JsonPath);
            Console.WriteLine($"Found {phase2Classes.Count} Phase 2 classes");
            
            // Extract methods from system image for these classes
            var classMethods = ExtractMethodsFromSystemImage(systemImagePath, phase2Classes);
            
            // Compile methods to bytecode
            var compiledMethods = CompileMethodsToBytecode(classMethods);
            
            // Update Phase 2 JSON with bytecode
            UpdatePhase2Json(phase2JsonPath, compiledMethods);
            
            Console.WriteLine("Phase 2 bytecode compilation complete");
        }
        
        private List<string> GetPhase2Classes(string jsonPath)
        {
            var jsonText = File.ReadAllText(jsonPath);
            var doc = JsonDocument.Parse(jsonText);
            var classes = new List<string>();
            
            if (doc.RootElement.TryGetProperty("classes", out var classesArray))
            {
                foreach (var classElement in classesArray.EnumerateArray())
                {
                    if (classElement.TryGetProperty("name", out var nameElement))
                    {
                        classes.Add(nameElement.GetString()!);
                    }
                }
            }
            
            return classes;
        }
        
        private Dictionary<string, List<MethodInfo>> ExtractMethodsFromSystemImage(
            string systemImagePath, 
            List<string> classNames)
        {
            var result = new Dictionary<string, List<MethodInfo>>();
            
            foreach (var className in classNames)
            {
                Console.WriteLine($"Extracting methods for {className}...");
                
                try 
                {
                    var methods = classExtractor.ExtractMethodsForClass(systemImagePath, className);
                    result[className] = methods;
                    
                    Console.WriteLine($"  Found {methods.Count} methods for {className}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Warning: Could not extract methods for {className}: {ex.Message}");
                    result[className] = new List<MethodInfo>();
                }
            }
            
            return result;
        }
        
        private Dictionary<string, List<CompiledMethodInfo>> CompileMethodsToBytecode(
            Dictionary<string, List<MethodInfo>> classMethods)
        {
            var result = new Dictionary<string, List<CompiledMethodInfo>>();
            
            foreach (var kvp in classMethods)
            {
                var className = kvp.Key;
                var methods = kvp.Value;
                var compiledMethods = new List<CompiledMethodInfo>();
                
                Console.WriteLine($"Compiling {methods.Count} methods for {className}...");
                
                // Get instance variables for this class from system image
                var instanceVars = GetInstanceVariables(className);
                
                foreach (var method in methods)
                {
                    try
                    {
                        var compiled = CompileMethod(method, instanceVars);
                        compiledMethods.Add(compiled);
                        
                        var primitiveInfo = compiled.IsPrimitive ? $" <primitive: {compiled.PrimitiveNumber}>" : "";
                        Console.WriteLine($"  ✓ {method.Selector}{primitiveInfo}: [{string.Join(", ", compiled.Bytecode)}]");
                        
                        if (compiled.Literals.Length > 0)
                        {
                            Console.WriteLine($"    Literals: [{string.Join(", ", compiled.Literals.Select(l => l.ToString()))}]");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ✗ {method.Selector}: {ex.Message}");
                    }
                }
                
                result[className] = compiledMethods;
            }
            
            return result;
        }
        
        private CompiledMethodInfo CompileMethod(MethodInfo method, List<string> instanceVars)
        {
            var compiledData = bytecodeGenerator.CompileMethod(method.Source, instanceVars);
            
            return new CompiledMethodInfo
            {
                Selector = method.Selector,
                Bytecode = compiledData.Bytecode,
                Literals = compiledData.Literals,
                ArgumentCount = compiledData.ArgumentCount,
                TemporaryCount = compiledData.TemporaryCount,
                IsPrimitive = method.IsPrimitive,
                PrimitiveNumber = method.PrimitiveNumber
            };
        }
        
        private List<string> GetInstanceVariables(string className)
        {
            // For now, return some common instance variables based on class name
            // This should be extracted from the system image eventually
            return className switch
            {
                "Object" => new List<string>(),
                "UndefinedObject" => new List<string>(),
                "Boolean" => new List<string>(),
                "True" => new List<string>(),
                "False" => new List<string>(),
                "Magnitude" => new List<string>(),
                "Number" => new List<string>(),
                "Integer" => new List<string>(),
                "SmallInteger" => new List<string>(),
                "LookupKey" => new List<string> { "key" },
                "Association" => new List<string> { "key", "value" },
                "Collection" => new List<string>(),
                "SequenceableCollection" => new List<string>(),
                "ArrayedCollection" => new List<string>(),
                "Array" => new List<string>(),
                "ByteArray" => new List<string>(),
                "String" => new List<string>(),
                "Set" => new List<string> { "tally" },
                "Dictionary" => new List<string> { "tally" },
                "IdentityDictionary" => new List<string> { "tally", "valueArray" },
                "MethodDictionary" => new List<string> { "tally" },
                "Symbol" => new List<string>(),
                "CompiledMethod" => new List<string> { "selector", "bytecode", "literals", "header" },
                "Behavior" => new List<string> { "superclass", "methodDict", "format" },
                "ClassDescription" => new List<string> { "superclass", "methodDict", "format", "instanceVariables", "organization" },
                "Class" => new List<string> { "superclass", "methodDict", "format", "instanceVariables", "organization", "name" },
                "Metaclass" => new List<string> { "superclass", "methodDict", "format", "instanceVariables", "organization", "thisClass" },
                "Message" => new List<string> { "selector", "args" },
                "InstructionStream" => new List<string> { "sender", "pc" },
                "ContextPart" => new List<string> { "sender", "pc", "stackp" },
                "MethodContext" => new List<string> { "sender", "pc", "stackp", "method", "receiverMap", "receiver" },
                "BlockContext" => new List<string> { "sender", "pc", "stackp", "nargs", "startpc", "home" },
                _ => new List<string>()
            };
        }
        
        private void UpdatePhase2Json(string jsonPath, Dictionary<string, List<CompiledMethodInfo>> compiledMethods)
        {
            Console.WriteLine("Updating Phase 2 JSON with compiled bytecode...");
            
            // Read existing JSON
            var jsonText = File.ReadAllText(jsonPath);
            var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;
            
            // Create new structure with bytecode
            var updatedPhase2 = new Dictionary<string, object>();
            
            // Copy existing properties
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name == "classes")
                {
                    // Update classes with bytecode
                    var updatedClasses = new List<object>();
                    
                    foreach (var classElement in property.Value.EnumerateArray())
                    {
                        var className = classElement.GetProperty("name").GetString();
                        if (className == null) continue;
                        var updatedClass = new Dictionary<string, object>();
                        
                        // Copy existing class properties
                        foreach (var classProp in classElement.EnumerateObject())
                        {
                            if (classProp.Name == "methods")
                            {
                                // Replace with compiled methods
                                if (compiledMethods.ContainsKey(className))
                                {
                                    var methods = compiledMethods[className];
                                    var methodsArray = new List<object>();
                                    
                                    foreach (var method in methods)
                                    {
                                        var methodObj = new Dictionary<string, object>
                                        {
                                            ["selector"] = method.Selector,
                                            ["bytecode"] = method.Bytecode,
                                            ["argumentCount"] = method.ArgumentCount,
                                            ["temporaryCount"] = method.TemporaryCount
                                        };
                                        
                                        if (method.Literals.Length > 0)
                                        {
                                            methodObj["literals"] = method.Literals.Select(ConvertLiteral).ToArray();
                                        }
                                        
                                        if (method.IsPrimitive)
                                        {
                                            methodObj["primitive"] = method.PrimitiveNumber;
                                        }
                                        
                                        methodsArray.Add(methodObj);
                                    }
                                    
                                    updatedClass["methods"] = methodsArray;
                                    Console.WriteLine($"Updated {className} with {methods.Count} compiled methods");
                                }
                                else
                                {
                                    // Keep existing methods (copy as-is)
                                    var value = JsonElementToObject(classProp.Value);
                                    if (value != null) updatedClass[classProp.Name] = value;
                                }
                            }
                            else
                            {
                                var value = JsonElementToObject(classProp.Value);
                                if (value != null) updatedClass[classProp.Name] = value;
                            }
                        }
                        
                        // Add methods if class didn't have any
                        if (!updatedClass.ContainsKey("methods") && compiledMethods.ContainsKey(className))
                        {
                            var methods = compiledMethods[className];
                            var methodsArray = new List<object>();
                            
                            foreach (var method in methods)
                            {
                                var methodObj = new Dictionary<string, object>
                                {
                                    ["selector"] = method.Selector,
                                    ["bytecode"] = method.Bytecode,
                                    ["argumentCount"] = method.ArgumentCount,
                                    ["temporaryCount"] = method.TemporaryCount
                                };
                                
                                if (method.Literals.Length > 0)
                                {
                                    methodObj["literals"] = method.Literals.Select(ConvertLiteral).ToArray();
                                }
                                
                                if (method.IsPrimitive)
                                {
                                    methodObj["primitive"] = method.PrimitiveNumber;
                                }
                                
                                methodsArray.Add(methodObj);
                            }
                            
                            updatedClass["methods"] = methodsArray;
                            Console.WriteLine($"Added {methods.Count} compiled methods to {className}");
                        }
                        
                        updatedClasses.Add(updatedClass);
                    }
                    
                    updatedPhase2["classes"] = updatedClasses;
                }
                else
                {
                    var value = JsonElementToObject(property.Value);
                    if (value != null) updatedPhase2[property.Name] = value;
                }
            }
            
            // Write updated JSON
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var updatedJson = JsonSerializer.Serialize(updatedPhase2, options);
            File.WriteAllText(jsonPath, updatedJson);
            
            Console.WriteLine($"\nSuccessfully updated {jsonPath} with bytecode for {compiledMethods.Count} classes");
        }
        
        private object? JsonElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
                JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToArray(),
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt32(out int i) ? i : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.ToString()
            };
        }
        
        private object ConvertLiteral(object literal)
        {
            return literal switch
            {
                Symbol sym => new Dictionary<string, object> { ["type"] = "symbol", ["value"] = sym.Value },
                string str => str,
                int num => num,
                double num => num,
                bool b => b,
                _ => literal?.ToString() ?? "nil"
            };
        }
    }
    
    public class MethodInfo
    {
        public string Selector { get; set; } = "";
        public string Source { get; set; } = "";
        public bool IsPrimitive { get; set; }
        public int PrimitiveNumber { get; set; }
    }
    
    public class CompiledMethodInfo
    {
        public string Selector { get; set; } = "";
        public byte[] Bytecode { get; set; } = Array.Empty<byte>();
        public object[] Literals { get; set; } = Array.Empty<object>();
        public int ArgumentCount { get; set; }
        public int TemporaryCount { get; set; }
        public bool IsPrimitive { get; set; }
        public int PrimitiveNumber { get; set; }
    }
}