using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Smalltalk80.Interpreter.Config;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.VM;

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
        private readonly VirtualMachine vm;
        private readonly Dictionary<string, OOP> methodCache = new();
        private readonly Func<int, OOP>? getSpecialObject;
        private readonly ILogger<MethodInstaller> logger;
        
        /// <summary>
        /// Initializes a new instance of the MethodInstaller class.
        /// </summary>
        /// <param name="memory">The object memory to use for allocations.</param>
        /// <param name="classBuilder">The class builder for class lookups.</param>
        /// <param name="symbolTable">The symbol table for selector interning.</param>
        /// <param name="vm">The virtual machine for field access.</param>
        /// <param name="logger">The logger for diagnostic output.</param>
        /// <param name="getSpecialObject">Optional function to get special objects (true, false, nil).</param>
        public MethodInstaller(
            ObjectMemory memory,
            ClassBuilder classBuilder,
            ISymbolTable symbolTable,
            VirtualMachine vm,
            ILogger<MethodInstaller> logger,
            Func<int, OOP>? getSpecialObject = null)
        {
            this.memory = memory ?? throw new ArgumentNullException(nameof(memory));
            this.classBuilder = classBuilder ?? throw new ArgumentNullException(nameof(classBuilder));
            this.symbolTable = symbolTable ?? throw new ArgumentNullException(nameof(symbolTable));
            this.vm = vm ?? throw new ArgumentNullException(nameof(vm));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.getSpecialObject = getSpecialObject;
        }

        /// <summary>
        /// Installs a batch of methods from a manifest.
        /// </summary>
        /// <param name="manifest">The bootstrap manifest containing method definitions.</param>
        public void InstallMethods(BootstrapManifest manifest)
        {
            if (DebugOptions.EnableMethodInstallerOutput)
            {
                logger.LogDebug("Beginning method installation");
            }
            
            // Check if we have inline methods in classes
            bool hasInlineMethods = manifest.Classes.Any(c => c.Methods != null || c.ClassMethods != null);
            
            if (DebugOptions.EnableMethodInstallerOutput)
            {
                logger.LogDebug("Method installation configuration: HasInlineMethods={HasInlineMethods}, MethodCount={MethodCount}",
                    hasInlineMethods, manifest.Methods?.Count ?? 0);
            }
            
            if (hasInlineMethods)
            {
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
            }
            else if (manifest.Methods != null)
            {
                // Install from flat methods array only if no inline methods exist
                // Group methods by actual class name and whether they're class methods
                foreach (var methodGroup in manifest.Methods.GroupBy(m => 
                {
                    // Parse "ClassName class" format for class methods
                    var className = m.ClassName ?? string.Empty;
                    var isClassMethod = m.IsClassMethod;
                    
                    if (className.EndsWith(" class"))
                    {
                        className = className[..^6]; // Remove " class" suffix
                        isClassMethod = true;
                    }
                    
                    return new { ClassName = className, IsClassMethod = isClassMethod };
                }))
                {
                    if (!string.IsNullOrEmpty(methodGroup.Key.ClassName))
                    {
                        InstallMethodsToClass(
                            methodGroup.Key.ClassName, 
                            methodGroup.ToList(), 
                            methodGroup.Key.IsClassMethod);
                    }
                }
            }
            
            // Count actual methods installed to dictionaries
            int totalInstalled = 0;
            foreach (var classDef in manifest.Classes)
            {
                var classOop = classBuilder.GetClass(classDef.Name);
                if (!classOop.IsNil)
                {
                    var dictOop = vm.GetFieldBootstrap(classOop, "Behavior", "methodDict");
                    
                    if (!dictOop.IsNil)
                    {
                        var tallyOop = vm.GetFieldBootstrap(dictOop, "MethodDictionary", "tally");
                            
                        if (tallyOop.IsSmallInteger)
                        {
                            totalInstalled += tallyOop.SmallIntegerValue;
                        }
                    }
                }
                
                // Check metaclass methods too
                var metaclassOop = classBuilder.GetMetaclass(classDef.Name);
                if (!metaclassOop.IsNil)
                {
                    var dictOop2 = vm.GetFieldBootstrap(metaclassOop, "Behavior", "methodDict");
                        
                    if (!dictOop2.IsNil)
                    {
                        var tallyOop2 = vm.GetFieldBootstrap(dictOop2, "MethodDictionary", "tally");
                            
                        if (tallyOop2.IsSmallInteger)
                        {
                            totalInstalled += tallyOop2.SmallIntegerValue;
                        }
                    }
                }
            }
            
            logger.LogInformation("Method installation complete. CachedMethods={CachedCount}, InstalledMethods={InstalledCount}",
                methodCache.Count, totalInstalled);
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
            
            // Check for duplicate methods
            var duplicates = methods
                .GroupBy(m => m.Selector)
                .Where(g => g.Count() > 1)
                .Select(g => new { Selector = g.Key, Count = g.Count() })
                .ToList();
            
            if (duplicates.Any())
            {
                foreach (var dup in duplicates)
                {
                    logger.LogError("Duplicate method definition in class {ClassName}: Selector '{Selector}' defined {Count} times",
                        className, dup.Selector, dup.Count);
                }
                throw new InvalidOperationException($"Duplicate method definitions found in {className}");
            }
            
            // Get the target class OOP
            OOP targetClass = isClassMethod 
                ? classBuilder.GetMetaclass(className) 
                : classBuilder.GetClass(className);
            
            if (targetClass.IsNil)
            {
                logger.LogWarning("Class {ClassName} not found, skipping {MethodCount} methods",
                    className, methods.Count);

                return;
            }
            
            if (DebugOptions.EnableMethodInstallerOutput)
            {
                logger.LogDebug("Installing {MethodCount} {MethodType} methods to {ClassName}",
                    methods.Count, isClassMethod ? "class" : "instance", className);
                
                // Special debug for Object class methods
                if (className == "Object" && isClassMethod)
                {
                    logger.LogDebug("Installing to Object metaclass (OOP {TargetClass})", targetClass);
                    foreach (var method in methods)
                    {
                        if (method.Selector == "initialize")
                        {
                            logger.LogDebug("Found Object class>>initialize method");
                        }
                    }
                }
            }
            
            // Get or create method dictionary with proper capacity
            var methodDict = GetOrCreateMethodDictionary(targetClass, methods.Count);
            if (methodDict.IsNil)
            {
                logger.LogWarning("Could not get method dictionary for {ClassName}", className);
                return;
            }
            
            // Install each method
            int installedCount = 0;
            foreach (var methodDef in methods)
            {
                var compiledMethod = CreateCompiledMethod(methodDef);
                if (!compiledMethod.IsNil)
                {
                    InstallMethodToDictionary(methodDict, methodDef.Selector, compiledMethod);
                    installedCount++;
                    
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
                logger.LogError("CompiledMethod class not found");
                return OOP.Nil;
            }
            
            // Debug output for methods with interesting fields
            if (DebugOptions.EnableMethodInstallerOutput && 
                (methodDef.Selector == "initialize" || methodDef.PrimitiveIndex > 0 || methodDef.StackDepth > 10))
            {
                logger.LogDebug("Creating method {Selector}: BytecodeCount={BytecodeCount}, Primitive={Primitive}, NumArgs={NumArgs}, NumTemps={NumTemps}, StackDepth={StackDepth}",
                    methodDef.Selector, methodDef.Bytecode.Count, methodDef.PrimitiveIndex,
                    methodDef.NumArgs, methodDef.EffectiveTemporaries, methodDef.StackDepth);
                
                if (methodDef.Bytecode.Count > 0)
                {
                    logger.LogTrace("Method {Selector} bytecode: [{Bytecode}]",
                        methodDef.Selector, string.Join(", ", methodDef.Bytecode));
                }
            }
            
            // Calculate method header with all fields including stack depth
            var header = CalculateMethodHeader(
                methodDef.NumArgs,
                methodDef.EffectiveTemporaries,
                methodDef.StackDepth,
                methodDef.PrimitiveIndex);
            
            if (DebugOptions.EnableMethodInstallerOutput && 
                (methodDef.Selector == "initialize" || methodDef.PrimitiveIndex > 0 || methodDef.StackDepth > 10))
            {
                logger.LogTrace("Method {Selector} calculated header: 0x{Header:X8}",
                    methodDef.Selector, header);
            }
            
            // Allocate CompiledMethod object (4 fixed fields per Blue Book)
            var methodOop = memory.AllocateObject(compiledMethodClass, 4);
            var methodObj = memory.GetObject(methodOop);
            
            if (methodObj == null)
            {
                logger.LogError("Failed to allocate CompiledMethod");
                return OOP.Nil;
            }
            
            // Set fields according to our implementation layout:
            // CompiledMethod has 4 fields but no instanceVariables in the manifest
            // So we set them directly by index
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
        /// Calculates the method header according to the system's specification.
        /// </summary>
        /// <param name="numArgs">Number of arguments.</param>
        /// <param name="numTemps">Number of temporary variables.</param>
        /// <param name="stackDepth">Maximum stack depth required.</param>
        /// <param name="primitiveIndex">Primitive index (0 if none).</param>
        /// <returns>The calculated method header value.</returns>
        private int CalculateMethodHeader(int numArgs, int numTemps, int stackDepth, int primitiveIndex)
        {
            // Method header format (32-bit integer):
            // Bits 1-8:   Primitive index (0 if none)
            // Bits 9-13:  Number of arguments (0-31)
            // Bits 14-18: Number of temporaries (0-31)
            // Bits 19-24: Stack size (0-63)
            // Bit 0 and bits 25-31: Reserved/unused
            
            // Ensure stack depth has a reasonable minimum
            stackDepth = Math.Max(stackDepth, 2);
            
            // Clamp values to their bit field limits
            primitiveIndex = Math.Min(primitiveIndex, 255);  // 8 bits
            numArgs = Math.Min(numArgs, 31);                 // 5 bits
            numTemps = Math.Min(numTemps, 31);               // 5 bits
            stackDepth = Math.Min(stackDepth, 63);           // 6 bits
            
            return (primitiveIndex << 1) | (numArgs << 9) | (numTemps << 14) | (stackDepth << 19);
        }

        /// <summary>
        /// Creates a ByteArray from a list of bytecode integers.
        /// </summary>
        private OOP CreateByteArray(List<int> bytecode)
        {
            var byteArrayClass = classBuilder.GetClass("ByteArray");
            if (byteArrayClass.IsNil)
            {
                logger.LogWarning("ByteArray class not found");
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
                logger.LogWarning("Array class not found");
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
                string s when s.StartsWith("Association(key:") => ParseAssociation(s), // Parse Association literal
                string s when s.StartsWith("#(") && s.EndsWith(")") => ParseArrayLiteral(s), // Array literal
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
            logger.LogTrace("Float literal {Value} converted to nil (Float class not implemented)", value);
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
            logger.LogWarning("True object not found in Special Objects Array");
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
            logger.LogWarning("False object not found in Special Objects Array");
            return OOP.Nil;
        }

        /// <summary>
        /// Looks up a key in a Dictionary object.
        /// </summary>
        private OOP LookupInDictionary(OOP dictOop, OOP keyOop)
        {
            var dictObj = memory.GetObject(dictOop);
            if (dictObj == null) return OOP.Nil;
            
            // Dictionary has tally at index 0, then key-value pairs
            int capacity = dictObj.Fields.Length - 1;
            int pairCapacity = capacity / 2;
            
            // Simple linear search for the key
            for (int i = 0; i < pairCapacity; i++)
            {
                var keyIndex = 1 + (i * 2);
                var valueIndex = keyIndex + 1;
                
                if (keyIndex < dictObj.Fields.Length && valueIndex < dictObj.Fields.Length)
                {
                    if (dictObj.Fields[keyIndex].Equals(keyOop))
                    {
                        return dictObj.Fields[valueIndex];
                    }
                }
            }
            
            return OOP.Nil;
        }
        
        /// <summary>
        /// Parses an array literal from Smalltalk syntax.
        /// Format: "#(element1 element2 ...)"
        /// Supports numbers, symbols, strings, nil, true, false, and nested arrays.
        /// </summary>
        private OOP ParseArrayLiteral(string arrayStr)
        {
            // Get Array class
            var arrayClass = classBuilder.GetClass("Array");
            if (arrayClass.IsNil)
            {
                logger.LogError("Array class not found for array literal: {ArrayStr}", arrayStr);
                // Return nil instead of falling back to Symbol to avoid confusion
                return OOP.Nil;
            }
            
            // Validate format
            if (!arrayStr.StartsWith("#(") || !arrayStr.EndsWith(")"))
            {
                logger.LogWarning("Invalid array literal format: {ArrayStr}", arrayStr);
                return OOP.Nil;
            }
            
            // Remove #( and ) to get the content
            if (arrayStr.Length <= 3) // Empty array #() or invalid
            {
                return memory.AllocateWordArray(arrayClass, 0);
            }
            
            var content = arrayStr[2..^1].Trim(); // Using C# 13 range syntax
            if (string.IsNullOrEmpty(content))
            {
                // Empty array
                return memory.AllocateWordArray(arrayClass, 0);
            }
            
            // Parse elements using a more robust tokenizer
            var elements = ParseArrayElements(content);
            
            // Create array
            var arrayOop = memory.AllocateWordArray(arrayClass, elements.Count);
            var arrayObj = memory.GetObject(arrayOop);
            
            if (arrayObj != null)
            {
                for (int i = 0; i < elements.Count; i++)
                {
                    arrayObj.Fields[i] = ParseArrayElement(elements[i]);
                }
            }
            
            if (elements.Count > 0)
            {
                logger.LogTrace("Created array literal with {ElementCount} elements from: {ArrayStr}",
                    elements.Count, arrayStr);
            }
            
            return arrayOop;
        }
        
        /// <summary>
        /// Parses the elements of an array literal into individual tokens.
        /// Handles nested arrays, quoted strings, and proper tokenization.
        /// </summary>
        private List<string> ParseArrayElements(string content)
        {
            var elements = new List<string>();
            var currentElement = new System.Text.StringBuilder();
            var depth = 0;
            var inString = false;
            var escapeNext = false;
            
            for (int i = 0; i < content.Length; i++)
            {
                var ch = content[i];
                
                if (escapeNext)
                {
                    currentElement.Append(ch);
                    escapeNext = false;
                    continue;
                }
                
                if (ch == '\\' && inString)
                {
                    escapeNext = true;
                    currentElement.Append(ch);
                    continue;
                }
                
                if (ch == '\'' && !inString)
                {
                    // Start of string literal
                    inString = true;
                    currentElement.Append(ch);
                }
                else if (ch == '\'' && inString && i + 1 < content.Length && content[i + 1] == '\'')
                {
                    // Escaped quote in string
                    currentElement.Append("''");
                    i++; // Skip next quote
                }
                else if (ch == '\'' && inString)
                {
                    // End of string literal
                    inString = false;
                    currentElement.Append(ch);
                }
                else if (!inString)
                {
                    if (ch == '(')
                    {
                        depth++;
                        currentElement.Append(ch);
                    }
                    else if (ch == ')')
                    {
                        depth--;
                        currentElement.Append(ch);
                    }
                    else if (depth == 0 && char.IsWhiteSpace(ch))
                    {
                        // Element separator at top level
                        if (currentElement.Length > 0)
                        {
                            elements.Add(currentElement.ToString());
                            currentElement.Clear();
                        }
                    }
                    else
                    {
                        currentElement.Append(ch);
                    }
                }
                else
                {
                    currentElement.Append(ch);
                }
            }
            
            // Add last element if any
            if (currentElement.Length > 0)
            {
                elements.Add(currentElement.ToString());
            }
            
            return elements;
        }
        
        /// <summary>
        /// Parses a single element from an array literal.
        /// </summary>
        private OOP ParseArrayElement(string element)
        {
            // Handle nested array
            if (element.StartsWith("#(") && element.EndsWith(")"))
            {
                return ParseArrayLiteral(element);
            }
            
            // Handle string literal (enclosed in single quotes)
            if (element.StartsWith("'") && element.EndsWith("'") && element.Length >= 2)
            {
                // Remove quotes and handle escaped quotes
                var stringContent = element[1..^1].Replace("''", "'");
                return CreateString(stringContent);
            }
            
            // Handle integer
            if (int.TryParse(element, out var intValue))
            {
                return OOP.FromSmallInteger(intValue);
            }
            
            // Handle negative integer
            if (element.StartsWith("-") && int.TryParse(element[1..], out var negValue))
            {
                return OOP.FromSmallInteger(-negValue);
            }
            
            // Handle float
            if (double.TryParse(element, System.Globalization.NumberStyles.Float, 
                System.Globalization.CultureInfo.InvariantCulture, out var floatValue))
            {
                return CreateFloat(floatValue);
            }
            
            // Handle symbol (starts with #)
            if (element.StartsWith("#"))
            {
                return symbolTable.Intern(element[1..]);
            }
            
            // Handle special literals
            return element switch
            {
                "nil" => OOP.Nil,
                "true" => GetTrueObject(),
                "false" => GetFalseObject(),
                _ => CreateString(element) // Default to string for unrecognized elements
            };
        }
        
        /// <summary>
        /// Parses an Association literal from the JSON string format.
        /// Format: "Association(key: #Name, value: null)"
        /// </summary>
        private OOP ParseAssociation(string associationStr)
        {
            // Parse the association string to extract the key
            var match = System.Text.RegularExpressions.Regex.Match(
                associationStr, 
                @"Association\(key:\s*#?(\w+),\s*value:\s*(\w+)\)"  // Make # optional with ?
            );
            
            if (match.Success)
            {
                var keyName = match.Groups[1].Value;
                var valueName = match.Groups[2].Value;
                
                // Create Association object
                var associationClass = classBuilder.GetClass("Association");
                if (associationClass.IsNil)
                {
                    logger.LogWarning("Association class not found for {KeyName}", keyName);
                    return OOP.Nil;
                }
                
                // Allocate Association with 2 fields (key and value)
                var assocOop = memory.AllocateWordArray(associationClass, 2);
                var assocObj = memory.GetObject(assocOop);
                
                if (assocObj != null)
                {
                    // Field 0: key (Symbol)
                    assocObj.Fields[0] = symbolTable.Intern(keyName);
                    
                    // Field 1: value (initially the global object or nil)
                    if (valueName == "null" || valueName == "nil")
                    {
                        // Try to resolve the global variable as a class
                        logger.LogTrace("Attempting to resolve global variable: {KeyName}", keyName);
                        var globalOop = classBuilder.GetClass(keyName); // Try as class name
                        
                        if (!globalOop.IsNil)
                        {
                            assocObj.Fields[1] = globalOop;
                            logger.LogTrace("Successfully resolved Association key {KeyName} to class {GlobalOop}",
                                keyName, globalOop);
                        }
                        else
                        {
                            logger.LogTrace("Failed to resolve {KeyName} as a class, trying SystemDictionary", keyName);
                            // Try to look it up in SystemDictionary (for pool dictionaries and other globals)
                            var systemDictOop = vm.GetSpecialObject(SpecialObjects.SystemDictionaryInstance);
                            if (!systemDictOop.IsNil)
                            {
                                var symbolOop = symbolTable.Intern(keyName);
                                var valueOop = LookupInDictionary(systemDictOop, symbolOop);
                                if (!valueOop.IsNil)
                                {
                                    assocObj.Fields[1] = valueOop;
                                    logger.LogTrace("Resolved Association key {KeyName} to global {ValueOop} from SystemDictionary",
                                        keyName, valueOop);
                                }
                                else
                                {
                                    // Not found - leave as nil
                                    assocObj.Fields[1] = OOP.Nil;
                                    logger.LogTrace("Association key {KeyName} not found in SystemDictionary, value = nil", keyName);
                                }
                            }
                            else
                            {
                                // SystemDictionary not available yet - leave as nil
                                assocObj.Fields[1] = OOP.Nil;
                                logger.LogTrace("Association key {KeyName} not resolved (SystemDictionary not available), value = nil",
                                    keyName);
                            }
                        }
                    }
                    else
                    {
                        // Value specified in the Association literal
                        assocObj.Fields[1] = OOP.Nil; // For now, handle as nil
                    }
                    
                    logger.LogTrace("Created Association for {KeyName} -> {Value}",
                        keyName, assocObj.Fields[1]);
                }
                
                return assocOop;
            }
            else
            {
                logger.LogWarning("Failed to parse Association: {AssociationStr}", associationStr);
                return OOP.Nil;
            }
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
            
            // Get method dictionary from Behavior
            var methodDict = vm.GetFieldBootstrap(classOop, "Behavior", "methodDict");
            
            if (methodDict.IsNil)
            {
                // Create new MethodDictionary
                var methodDictClass = classBuilder.GetClass("MethodDictionary");
                if (methodDictClass.IsNil)
                {
                    logger.LogWarning("MethodDictionary class not found");
                    return OOP.Nil;
                }
                
                // Calculate capacity - need space for hash table, plus 50% for hash efficiency
                int capacity = Math.Max(32, (expectedMethodCount * 3) / 2);
                // Round up to next power of 2 for better hashing
                capacity = NextPowerOfTwo(capacity);
                
                // MethodDictionary inherits from IdentityDictionary which inherits from Set
                // It has 2 named fields: tally (from Set), valueArray (from IdentityDictionary)
                // Plus indexed portion for keys
                methodDict = memory.AllocateObject(methodDictClass, 2 + capacity);
                
                if (!methodDict.IsNil)
                {
                    var methodDictObj = memory.GetObject(methodDict);
                    if (methodDictObj != null)
                    {
                        // Initialize tally to 0
                        vm.SetFieldBootstrap(methodDict, "MethodDictionary", "tally", OOP.FromSmallInteger(0));
                        
                        // Initialize indexed portion (keys) to nil
                        for (int i = 2; i < methodDictObj.Fields.Length; i++)
                        {
                            methodDictObj.Fields[i] = OOP.Nil;
                        }
                        
                        // Create valueArray to hold the compiled methods
                        var arrayClass = classBuilder.GetClass("Array");
                        if (!arrayClass.IsNil)
                        {
                            var valueArray = memory.AllocateWordArray(arrayClass, capacity);
                            // Initialize valueArray slots to nil
                            var valueArrayObj = memory.GetObject(valueArray);
                            if (valueArrayObj != null)
                            {
                                for (int i = 0; i < valueArrayObj.Fields.Length; i++)
                                {
                                    valueArrayObj.Fields[i] = OOP.Nil;
                                }
                            }
                            
                            // MethodDictionary inherits from IdentityDictionary which has "valueArray"
                            vm.SetFieldBootstrap(methodDict, "MethodDictionary", "valueArray", valueArray);
                            logger.LogDebug("Created method dictionary with capacity {Capacity} for {ExpectedMethodCount} methods",
                                capacity, expectedMethodCount);
                            
                            // Verify valueArray was set
                            if (DebugOptions.EnableDebugOutput)
                            {
                                var checkArray = vm.GetFieldBootstrap(methodDict, "MethodDictionary", "valueArray");
                                logger.LogTrace("MethodDict {MethodDict} valueArray set to {ValueArray}, verify: {CheckArray}",
                                    methodDict, valueArray, checkArray);
                            }
                        }
                    }
                }
                
                // Store in class
                vm.SetFieldBootstrap(classOop, "Behavior", "methodDict", methodDict);
                
                // Verify it was stored
                if (DebugOptions.EnableDebugOutput)
                {
                    var checkDict = vm.GetFieldBootstrap(classOop, "Behavior", "methodDict");
                    logger.LogTrace("Stored methodDict {MethodDict} in class {ClassOop}, verify: {CheckDict}",
                        methodDict, classOop, checkDict);
                }
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
        /// MethodDictionary/IdentityDictionary structure:
        /// - Keys are in the indexed portion (Fields[2+])
        /// - Values are in the valueArray at corresponding indices
        /// </summary>
        private void InstallMethodToDictionary(OOP dictOop, string selector, OOP methodOop)
        {
            var dictObj = memory.GetObject(dictOop);
            if (dictObj == null) return;
            
            // Get the valueArray from the dictionary
            var valueArrayOop = vm.GetFieldBootstrap(dictOop, "MethodDictionary", "valueArray");
            var valueArrayObj = memory.GetObject(valueArrayOop);
            if (valueArrayObj == null || valueArrayObj.Fields.Length == 0)
            {
                logger.LogError("Dictionary has no valueArray or empty valueArray for selector {Selector}", selector);
                return;
            }
            
            // Get selector symbol
            var selectorSymbol = symbolTable.Intern(selector);
            
            // The indexed portion of the dictionary starts at field 2
            // Calculate capacity from the indexed portion size
            var capacity = dictObj.Fields.Length - 2; // Subtract tally and valueArray fields
            if (capacity == 0 || capacity != valueArrayObj.Fields.Length)
            {
                logger.LogError("Dictionary capacity mismatch for selector {Selector}", selector);
                return;
            }
            
            var hash = Math.Abs(selectorSymbol.RawValue) % capacity;
            
            // Find empty slot or matching selector
            for (int i = 0; i < capacity; i++)
            {
                var index = (hash + i) % capacity;
                var keyFieldIndex = 2 + index; // Skip tally and valueArray fields
                
                var key = dictObj.Fields[keyFieldIndex];
                
                if (key.IsNil || key.Equals(selectorSymbol))
                {
                    // Install selector in indexed portion and method in valueArray
                    dictObj.Fields[keyFieldIndex] = selectorSymbol;
                    valueArrayObj.Fields[index] = methodOop;
                    
                    // Log method installation for special selectors
                    if (DebugOptions.EnableMethodInstallerOutput && 
                        (selector == "initialize" || selector == "new" || selector == "at:"))
                    {
                        logger.LogTrace("Installing method {Selector} to dictionary {DictOop} at key index {KeyIndex}, value index {ValueIndex}",
                            selector, dictOop, keyFieldIndex, index);
                        
                        // Check for overwrites
                        if (!key.IsNil && !key.Equals(selectorSymbol))
                        {
                            var existingSelector = vm.GetSymbolString(key);
                            logger.LogWarning("Overwriting existing selector {ExistingSelector} with {NewSelector} at index {Index}",
                                existingSelector, selector, index);
                        }
                    }
                    
                    // Update tally if new entry
                    if (key.IsNil)
                    {
                        var tallyOop = vm.GetFieldBootstrap(dictOop, "MethodDictionary", "tally");
                            
                        if (tallyOop.IsSmallInteger)
                        {
                            var tally = tallyOop.SmallIntegerValue;
                            vm.SetFieldBootstrap(dictOop, "MethodDictionary", "tally", OOP.FromSmallInteger(tally + 1));
                        }
                    }
                    
                    return;
                }
            }
            
            logger.LogError("Method dictionary full for selector {Selector} - method NOT installed!", selector);
            // This is a critical error - the method won't be reachable and will be GC'd
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