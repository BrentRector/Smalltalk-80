using System;
using System.Collections.Generic;
using System.Linq;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.ObjectModel;

namespace Smalltalk80.Interpreter.Bootstrap
{
    /// <summary>
    /// Installs compiled methods into Smalltalk classes.
    /// Creates CompiledMethod objects and updates method dictionaries.
    /// </summary>
    public sealed class MethodInstaller
    {
        private readonly ObjectMemory memory;
        private readonly ClassBuilder classBuilder;
        private readonly ISymbolTable symbolTable;
        private readonly Dictionary<string, OOP> methodCache = new();
        private readonly Func<int, OOP>? getSpecialObject;
        
        /// <summary>
        /// Initializes a new instance of the MethodInstaller class.
        /// </summary>
        /// <param name="memory">The object memory to use for allocations.</param>
        /// <param name="classBuilder">The class builder for class lookups.</param>
        /// <param name="symbolTable">The symbol table for selector interning.</param>
        /// <param name="getSpecialObject">Optional function to get special objects (true, false, nil).</param>
        public MethodInstaller(ObjectMemory memory, ClassBuilder classBuilder, ISymbolTable symbolTable, Func<int, OOP>? getSpecialObject = null)
        {
            this.memory = memory ?? throw new ArgumentNullException(nameof(memory));
            this.classBuilder = classBuilder ?? throw new ArgumentNullException(nameof(classBuilder));
            this.symbolTable = symbolTable ?? throw new ArgumentNullException(nameof(symbolTable));
            this.getSpecialObject = getSpecialObject;
        }

        /// <summary>
        /// Installs a batch of methods from a manifest.
        /// </summary>
        /// <param name="manifest">The bootstrap manifest containing method definitions.</param>
        public void InstallMethods(BootstrapManifest manifest)
        {
            Console.WriteLine("[MethodInstaller] Beginning method installation...");
            
            // Install inline methods from classes
            foreach (var classDef in manifest.Classes)
            {
                // Install instance methods
                if (classDef.Methods != null)
                {
                    InstallMethodsToClass(classDef.Name, classDef.Methods, isClassMethod: false);
                }
                
                // Install class methods
                if (classDef.ClassMethods != null)
                {
                    InstallMethodsToClass(classDef.Name, classDef.ClassMethods, isClassMethod: true);
                }
            }
            
            // Install flat methods array
            if (manifest.Methods != null)
            {
                foreach (var methodGroup in manifest.Methods.GroupBy(m => new { m.ClassName, m.IsClassMethod }))
                {
                    if (methodGroup.Key.ClassName != null)
                    {
                        InstallMethodsToClass(
                            methodGroup.Key.ClassName, 
                            methodGroup.ToList(), 
                            methodGroup.Key.IsClassMethod);
                    }
                }
            }
            
            Console.WriteLine($"[MethodInstaller] Installation complete. {methodCache.Count} methods installed.");
        }

        /// <summary>
        /// Installs a list of methods to a specific class.
        /// </summary>
        /// <param name="className">The name of the class to install methods to.</param>
        /// <param name="methods">The list of method definitions.</param>
        /// <param name="isClassMethod">Whether to install as class methods (true) or instance methods (false).</param>
        public void InstallMethodsToClass(string className, List<MethodDefinition> methods, bool isClassMethod)
        {
            if (methods.Count == 0) return;
            
            // Get the target class OOP
            OOP targetClass = isClassMethod 
                ? classBuilder.GetMetaclass(className) 
                : classBuilder.GetClass(className);
            
            if (targetClass.IsNil)
            {
                Console.WriteLine($"[MethodInstaller] Warning: Class {className} not found, skipping {methods.Count} methods");
                return;
            }
            
            Console.WriteLine($"[MethodInstaller] Installing {methods.Count} {(isClassMethod ? "class" : "instance")} methods to {className}");
            
            // Get or create method dictionary with proper capacity
            var methodDict = GetOrCreateMethodDictionary(targetClass, methods.Count);
            if (methodDict.IsNil)
            {
                Console.WriteLine($"[MethodInstaller] Warning: Could not get method dictionary for {className}");
                return;
            }
            
            // Install each method
            foreach (var methodDef in methods)
            {
                var compiledMethod = CreateCompiledMethod(methodDef);
                if (!compiledMethod.IsNil)
                {
                    InstallMethodToDictionary(methodDict, methodDef.Selector, compiledMethod);
                    
                    // Cache the method for debugging/introspection
                    var cacheKey = $"{className}.{(isClassMethod ? "class." : "")}{methodDef.Selector}";
                    methodCache[cacheKey] = compiledMethod;
                }
            }
        }

        /// <summary>
        /// Creates a CompiledMethod object from a method definition.
        /// </summary>
        /// <param name="methodDef">The method definition from JSON.</param>
        /// <returns>The OOP of the created CompiledMethod.</returns>
        private OOP CreateCompiledMethod(MethodDefinition methodDef)
        {
            // Get CompiledMethod class
            var compiledMethodClass = classBuilder.GetClass("CompiledMethod");
            if (compiledMethodClass.IsNil)
            {
                Console.WriteLine("[MethodInstaller] Error: CompiledMethod class not found!");
                return OOP.Nil;
            }
            
            // Calculate method header
            var header = CalculateMethodHeader(
                methodDef.NumArgs,
                methodDef.EffectiveTemporaries,
                methodDef.Literals.Count,
                methodDef.EffectivePrimitive > 0);
            
            // Allocate CompiledMethod object (4 fixed fields per Blue Book)
            var methodOop = memory.AllocateObject(compiledMethodClass, 4);
            var methodObj = memory.GetObject(methodOop);
            
            if (methodObj == null)
            {
                Console.WriteLine("[MethodInstaller] Error: Failed to allocate CompiledMethod");
                return OOP.Nil;
            }
            
            // Set fields according to Blue Book layout:
            // Field 0: selector (Symbol)
            var selectorSymbol = symbolTable.Intern(methodDef.Selector);
            methodObj.Fields[0] = selectorSymbol;
            
            // Field 1: bytecode (ByteArray)
            var bytecodeArray = CreateByteArray(methodDef.Bytecode);
            methodObj.Fields[1] = bytecodeArray;
            
            // Field 2: literals (Array)
            var literalsArray = CreateLiteralsArray(methodDef.Literals);
            methodObj.Fields[2] = literalsArray;
            
            // Field 3: header (SmallInteger)
            methodObj.Fields[3] = OOP.FromSmallInteger(header);
            
            return methodOop;
        }

        /// <summary>
        /// Calculates the method header according to Blue Book specification.
        /// </summary>
        private int CalculateMethodHeader(int numArgs, int numTemps, int numLiterals, bool hasPrimitive)
        {
            // Blue Book method header format (16 bits):
            // Bits 15-14: Header type (00 = short, 01 = long, 10 = primitive)
            // Bits 13-12: Number of arguments (0-3, or flag if more)
            // Bits 11-6:  Number of temporaries (0-63)
            // Bits 5-0:   Number of literals (0-63)
            
            int headerType = hasPrimitive ? 2 : (numLiterals > 63 || numTemps > 63 ? 1 : 0);
            int argField = Math.Min(numArgs, 3);
            int tempField = Math.Min(numTemps, 63);
            int litField = Math.Min(numLiterals, 63);
            
            return (headerType << 14) | (argField << 12) | (tempField << 6) | litField;
        }

        /// <summary>
        /// Creates a ByteArray from a list of bytecode integers.
        /// </summary>
        private OOP CreateByteArray(List<int> bytecode)
        {
            var byteArrayClass = classBuilder.GetClass("ByteArray");
            if (byteArrayClass.IsNil)
            {
                Console.WriteLine("[MethodInstaller] Warning: ByteArray class not found");
                return OOP.Nil;
            }
            
            // Allocate ByteArray with proper size
            var byteArrayOop = memory.AllocateByteArray(byteArrayClass, bytecode.Count);
            var byteArrayObj = memory.GetObject(byteArrayOop);
            
            if (byteArrayObj != null)
            {
                // Store bytes as SmallIntegers in Fields array
                for (int i = 0; i < bytecode.Count && i < byteArrayObj.Fields.Length; i++)
                {
                    byteArrayObj.Fields[i] = OOP.FromSmallInteger(bytecode[i] & 0xFF);
                }
            }
            
            return byteArrayOop;
        }

        /// <summary>
        /// Creates an Array of literal objects from a list of literal values.
        /// </summary>
        private OOP CreateLiteralsArray(List<object> literals)
        {
            var arrayClass = classBuilder.GetClass("Array");
            if (arrayClass.IsNil)
            {
                Console.WriteLine("[MethodInstaller] Warning: Array class not found");
                return OOP.Nil;
            }
            
            // Allocate Array for literals
            var literalsOop = memory.AllocateWordArray(arrayClass, literals.Count);
            var literalsObj = memory.GetObject(literalsOop);
            
            if (literalsObj != null)
            {
                // Convert each literal to appropriate OOP
                for (int i = 0; i < literals.Count; i++)
                {
                    literalsObj.Fields[i] = ConvertLiteralToOop(literals[i]);
                }
            }
            
            return literalsOop;
        }

        /// <summary>
        /// Converts a JSON literal value to an OOP.
        /// </summary>
        private OOP ConvertLiteralToOop(object literal)
        {
            return literal switch
            {
                null => OOP.Nil,
                string s when s == "nil" => OOP.Nil,
                string s when s == "true" => GetTrueObject(),
                string s when s == "false" => GetFalseObject(),
                string s when s.StartsWith("#") => symbolTable.Intern(s[1..]), // Symbol literal
                string s => CreateString(s),
                int i => OOP.FromSmallInteger(i),
                long l => OOP.FromSmallInteger((int)l),
                double d => CreateFloat(d),
                System.Text.Json.JsonElement je => ConvertJsonElement(je),
                _ => OOP.Nil
            };
        }

        /// <summary>
        /// Converts a JsonElement to an OOP.
        /// </summary>
        private OOP ConvertJsonElement(System.Text.Json.JsonElement element)
        {
            return element.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Null => OOP.Nil,
                System.Text.Json.JsonValueKind.True => GetTrueObject(),
                System.Text.Json.JsonValueKind.False => GetFalseObject(),
                System.Text.Json.JsonValueKind.Number => element.TryGetInt32(out var i) 
                    ? OOP.FromSmallInteger(i) 
                    : CreateFloat(element.GetDouble()),
                System.Text.Json.JsonValueKind.String => ConvertLiteralToOop(element.GetString() ?? string.Empty),
                _ => OOP.Nil
            };
        }

        /// <summary>
        /// Creates a String object from a C# string.
        /// </summary>
        private OOP CreateString(string value)
        {
            var stringClass = classBuilder.GetClass("String");
            if (stringClass.IsNil) return OOP.Nil;
            
            var bytes = System.Text.Encoding.UTF8.GetBytes(value);
            var stringOop = memory.AllocateByteArray(stringClass, bytes.Length);
            var stringObj = memory.GetObject(stringOop);
            
            if (stringObj != null)
            {
                // Store bytes as SmallIntegers in Fields array
                for (int i = 0; i < bytes.Length && i < stringObj.Fields.Length; i++)
                {
                    stringObj.Fields[i] = OOP.FromSmallInteger(bytes[i]);
                }
            }
            
            return stringOop;
        }

        /// <summary>
        /// Creates a Float object from a double value.
        /// </summary>
        private OOP CreateFloat(double value)
        {
            // For now, return nil as Float class may not exist during bootstrap
            // This can be enhanced when Float class is available
            Console.WriteLine($"[MethodInstaller] Warning: Float literal {value} converted to nil (Float class not implemented)");
            return OOP.Nil;
        }

        /// <summary>
        /// Gets the true object OOP.
        /// </summary>
        private OOP GetTrueObject()
        {
            // Use the singleton true object from Special Objects Array if available
            if (getSpecialObject != null)
            {
                var trueObj = getSpecialObject(SpecialObjects.TrueObject);
                if (!trueObj.IsNil)
                    return trueObj;
            }
            
            // Fallback: should not happen after bootstrap is complete
            Console.WriteLine("[MethodInstaller] Warning: true object not found in Special Objects Array");
            return OOP.Nil;
        }

        /// <summary>
        /// Gets the false object OOP.
        /// </summary>
        private OOP GetFalseObject()
        {
            // Use the singleton false object from Special Objects Array if available
            if (getSpecialObject != null)
            {
                var falseObj = getSpecialObject(SpecialObjects.FalseObject);
                if (!falseObj.IsNil)
                    return falseObj;
            }
            
            // Fallback: should not happen after bootstrap is complete
            Console.WriteLine("[MethodInstaller] Warning: false object not found in Special Objects Array");
            return OOP.Nil;
        }

        /// <summary>
        /// Gets or creates the method dictionary for a class.
        /// </summary>
        /// <param name="classOop">The class to get/create method dictionary for.</param>
        /// <param name="expectedMethodCount">Expected number of methods to store.</param>
        private OOP GetOrCreateMethodDictionary(OOP classOop, int expectedMethodCount = 0)
        {
            var classObj = memory.GetObject(classOop);
            if (classObj == null) return OOP.Nil;
            
            // Method dictionary is at field 1 in Behavior
            var methodDict = classObj.Fields[1];
            
            if (methodDict.IsNil)
            {
                // Create new MethodDictionary
                var methodDictClass = classBuilder.GetClass("MethodDictionary");
                if (methodDictClass.IsNil)
                {
                    Console.WriteLine("[MethodInstaller] Warning: MethodDictionary class not found");
                    return OOP.Nil;
                }
                
                // MethodDictionary has 2 fields: tally and array (inherits from Set)
                methodDict = memory.AllocateObject(methodDictClass, 2);
                var dictObj = memory.GetObject(methodDict);
                
                if (dictObj != null)
                {
                    // Initialize tally to 0
                    dictObj.Fields[0] = OOP.FromSmallInteger(0);
                    
                    // Calculate capacity - need 2x space for key-value pairs, plus 50% for hash efficiency
                    int capacity = Math.Max(32, ((expectedMethodCount * 2) * 3) / 2);
                    // Round up to next power of 2 for better hashing
                    capacity = NextPowerOfTwo(capacity);
                    
                    var arrayClass = classBuilder.GetClass("Array");
                    if (!arrayClass.IsNil)
                    {
                        var array = memory.AllocateWordArray(arrayClass, capacity);
                        dictObj.Fields[1] = array;
                        Console.WriteLine($"[MethodInstaller] Created method dictionary with capacity {capacity} for {expectedMethodCount} methods");
                    }
                }
                
                // Store in class
                classObj.Fields[1] = methodDict;
            }
            
            return methodDict;
        }
        
        /// <summary>
        /// Rounds up to the next power of two.
        /// </summary>
        private static int NextPowerOfTwo(int n)
        {
            if (n <= 1) return 2;
            n--;
            n |= n >> 1;
            n |= n >> 2;
            n |= n >> 4;
            n |= n >> 8;
            n |= n >> 16;
            return n + 1;
        }

        /// <summary>
        /// Installs a method into a method dictionary.
        /// </summary>
        private void InstallMethodToDictionary(OOP dictOop, string selector, OOP methodOop)
        {
            var dictObj = memory.GetObject(dictOop);
            if (dictObj == null) return;
            
            // Get the array from the dictionary
            var arrayOop = dictObj.Fields[1];
            var arrayObj = memory.GetObject(arrayOop);
            if (arrayObj == null) return;
            
            // Get selector symbol
            var selectorSymbol = symbolTable.Intern(selector);
            
            // Simple linear probe hash table implementation
            var size = arrayObj.Fields.Length / 2; // Array stores key-value pairs
            var hash = Math.Abs(selectorSymbol.RawValue) % size;
            
            // Find empty slot or matching selector
            for (int i = 0; i < size; i++)
            {
                var index = (hash + i) % size;
                var keyIndex = index * 2;
                var valueIndex = keyIndex + 1;
                
                if (keyIndex >= arrayObj.Fields.Length) break;
                
                var key = arrayObj.Fields[keyIndex];
                
                if (key.IsNil || key.Equals(selectorSymbol))
                {
                    // Install method
                    arrayObj.Fields[keyIndex] = selectorSymbol;
                    arrayObj.Fields[valueIndex] = methodOop;
                    
                    // Update tally if new entry
                    if (key.IsNil)
                    {
                        var tallyOop = dictObj.Fields[0];
                        if (tallyOop.IsSmallInteger)
                        {
                            var tally = tallyOop.SmallIntegerValue;
                            dictObj.Fields[0] = OOP.FromSmallInteger(tally + 1);
                        }
                    }
                    
                    return;
                }
            }
            
            Console.WriteLine($"[MethodInstaller] Warning: Method dictionary full for selector {selector}");
        }

        /// <summary>
        /// Gets a cached method by key.
        /// </summary>
        /// <param name="className">The class name.</param>
        /// <param name="selector">The method selector.</param>
        /// <param name="isClassMethod">Whether this is a class method.</param>
        /// <returns>The OOP of the cached method, or Nil if not found.</returns>
        public OOP GetCachedMethod(string className, string selector, bool isClassMethod)
        {
            var cacheKey = $"{className}.{(isClassMethod ? "class." : "")}{selector}";
            return methodCache.TryGetValue(cacheKey, out var method) ? method : OOP.Nil;
        }

        /// <summary>
        /// Gets all cached methods for introspection.
        /// </summary>
        public IReadOnlyDictionary<string, OOP> GetAllCachedMethods()
        {
            return methodCache;
        }
    }
}