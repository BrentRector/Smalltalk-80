using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Smalltalk80.Bootstrap;

/// <summary>
/// Creates classes by generating temporary Smalltalk "Builder" classes that contain
/// all the necessary message sends to create a target class. This approach ensures
/// pure Smalltalk semantics and automatic side effects like class registration.
/// </summary>
public class SmalltalkLevelClassBuilder
{
    private readonly ISmalltalkVM vm;
    private readonly string standaloneCompilerPath;
    private readonly string systemImagePath;
    private int builderClassCounter = 0;
    private SystemImageData? systemImageData;
    
    /// <summary>
    /// Represents a class definition from the system image JSON.
    /// </summary>
    public class ClassDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        
        [JsonPropertyName("superclass")]
        public string Superclass { get; set; } = "";
        
        [JsonPropertyName("instanceVariables")]
        public List<string> InstanceVariables { get; set; } = new();
        
        [JsonPropertyName("classVariables")]
        public List<string> ClassVariables { get; set; } = new();
        
        [JsonPropertyName("poolDictionaries")]
        public List<string> PoolDictionaries { get; set; } = new();
        
        [JsonPropertyName("category")]
        public string Category { get; set; } = "";
    }
    
    /// <summary>
    /// Represents a method definition from the system image JSON.
    /// </summary>
    public class MethodDefinition
    {
        [JsonPropertyName("class")]
        public string ClassName { get; set; } = "";
        
        [JsonPropertyName("selector")]
        public string Selector { get; set; } = "";
        
        [JsonPropertyName("bytecode")]
        public List<int> Bytecode { get; set; } = new();
        
        [JsonPropertyName("literals")]
        public List<string> Literals { get; set; } = new();
        
        [JsonPropertyName("primitiveIndex")]
        public int PrimitiveIndex { get; set; } = 0;
        
        [JsonPropertyName("numArgs")]
        public int NumArgs { get; set; } = 0;
        
        [JsonPropertyName("numTemps")]
        public int NumTemps { get; set; } = 0;
        
        [JsonPropertyName("isClassMethod")]
        public bool IsClassMethod { get; set; } = false;
    }
    
    public SmalltalkLevelClassBuilder(ISmalltalkVM vm, string standaloneCompilerPath, string systemImagePath)
    {
        this.vm = vm;
        this.standaloneCompilerPath = standaloneCompilerPath;
        this.systemImagePath = systemImagePath;
        LoadSystemImageData();
    }
    
    /// <summary>
    /// Loads the system image data for dependency analysis and source extraction.
    /// </summary>
    private void LoadSystemImageData()
    {
        if (!File.Exists(systemImagePath))
        {
            throw new FileNotFoundException($"System image not found: {systemImagePath}");
        }
        
        var jsonContent = File.ReadAllText(systemImagePath);
        systemImageData = JsonSerializer.Deserialize<SystemImageData>(jsonContent);
        
        if (systemImageData?.Classes == null)
        {
            throw new InvalidOperationException("Failed to load system image classes");
        }
        
        Console.WriteLine($"[STBUILDER] Loaded system image with {systemImageData.Classes.Count} classes");
    }
    
    /// <summary>
    /// Creates a class using pure Smalltalk message sends via a generated builder class.
    /// </summary>
    public int CreateClass(ClassDefinition classDef, List<MethodDefinition> methods)
    {
        Console.WriteLine($"[STBUILDER] Creating class {classDef.Name} via Smalltalk builder");
        
        try
        {
            // Generate unique builder class name
            var builderClassName = $"ClassBuilder{++builderClassCounter}";
            
            // Generate self-contained Smalltalk source code including all dependencies
            var smalltalkSource = GenerateSelfContainedBuilderSource(builderClassName, classDef, methods);
            Console.WriteLine($"[STBUILDER] Generated Smalltalk source for {builderClassName}");
            
            // Debug: Show a portion of the generated source to verify buildIt method
            var builderStart = smalltalkSource.IndexOf($"Object subclass: #{builderClassName}");
            if (builderStart >= 0)
            {
                var builderSection = smalltalkSource.Substring(builderStart, Math.Min(800, smalltalkSource.Length - builderStart));
                Console.WriteLine($"[STBUILDER] Builder section preview:\n{builderSection}");
            }
            
            // Use standalone compiler to compile the builder class
            var compiledBuilder = CompileBuilderClass(builderClassName, smalltalkSource);
            Console.WriteLine($"[STBUILDER] Compiled {builderClassName} to bytecode");
            
            // Load the builder class into the VM
            var builderClassOop = LoadBuilderIntoVM(builderClassName, compiledBuilder);
            Console.WriteLine($"[STBUILDER] Loaded {builderClassName} into VM at OOP {builderClassOop}");
            
            // Execute the builder to create the target class
            var targetClassOop = ExecuteBuilder(builderClassOop);
            Console.WriteLine($"[STBUILDER] Builder execution returned OOP {targetClassOop}");
            
            // Verify the target class was created correctly
            VerifyTargetClass(classDef, targetClassOop);
            
            Console.WriteLine($"[STBUILDER] Successfully created and verified {classDef.Name} at OOP {targetClassOop}");
            
            // The builder class can now be garbage collected
            return targetClassOop;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create class {classDef.Name} via Smalltalk builder: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Generates self-contained Smalltalk source code including all dependencies and the builder class.
    /// </summary>
    private string GenerateSelfContainedBuilderSource(string builderClassName, ClassDefinition classDef, List<MethodDefinition> methods)
    {
        var source = new StringBuilder();
        
        // First, analyze and include all dependencies
        var dependencies = AnalyzeDependencies(classDef);
        
        foreach (var depClass in dependencies)
        {
            source.AppendLine(GenerateClassSourceFromSystemImage(depClass));
            source.AppendLine();
        }
        
        // Then generate the builder class
        source.AppendLine(GenerateBuilderClassSource(builderClassName, classDef, methods));
        
        return source.ToString();
    }
    
    /// <summary>
    /// Generates Smalltalk source code for a builder class that creates the target class.
    /// </summary>
    private string GenerateBuilderClassSource(string builderClassName, ClassDefinition classDef, List<MethodDefinition> methods)
    {
        var source = new StringBuilder();
        
        // Class definition
        source.AppendLine($"Object subclass: #{builderClassName}");
        source.AppendLine("    instanceVariableNames: ''");
        source.AppendLine("    classVariableNames: ''");
        source.AppendLine("    poolDictionaries: ''");
        source.AppendLine("    category: 'Bootstrap-Builders'!");
        source.AppendLine();
        
        // buildIt method - call VM primitive to create class using existing mechanism
        source.AppendLine($"!{builderClassName} methodsFor: 'building'!");
        source.AppendLine();
        source.AppendLine("buildIt");
        source.AppendLine("    \"Create class using VM primitive that delegates to existing class creation mechanism\"");
        source.AppendLine("    <primitive: 999>");  // Special primitive we'll implement
        source.AppendLine("    self primitiveFailed");
        source.AppendLine("    \"Install a compiled method on the target class\"");
        source.AppendLine("    | compiledMethod header |");
        source.AppendLine();
        source.AppendLine("    \"Create method header\"");
        source.AppendLine("    header := primIndex bitOr: (tempCount bitShift: 10).");
        source.AppendLine();
        source.AppendLine("    \"Create compiled method (this will need VM primitive support)\"");
        source.AppendLine("    compiledMethod := CompiledMethod new.");
        source.AppendLine("    compiledMethod");
        source.AppendLine("        bytecodes: bytecodeArray;");
        source.AppendLine("        literals: literalsArray;");
        source.AppendLine("        header: header;");
        source.AppendLine("        numArgs: argCount.");
        source.AppendLine();
        source.AppendLine("    \"Install in method dictionary\"");
        source.AppendLine("    targetClass methodDict at: selector put: compiledMethod.");
        source.AppendLine();
        
        source.AppendLine("! !");
        
        return source.ToString();
    }
    
    /// <summary>
    /// Analyzes dependencies for a class definition and returns all required classes.
    /// </summary>
    private List<string> AnalyzeDependencies(ClassDefinition classDef)
    {
        var dependencies = new HashSet<string>();
        var toProcess = new Queue<string>();
        
        // Start with the superclass
        if (!string.IsNullOrEmpty(classDef.Superclass) && classDef.Superclass != "nil")
        {
            toProcess.Enqueue(classDef.Superclass);
        }
        
        // Process dependencies transitively
        while (toProcess.Count > 0)
        {
            var className = toProcess.Dequeue();
            
            // Skip if already processed or is a basic class
            if (dependencies.Contains(className) || IsBasicClass(className))
                continue;
                
            dependencies.Add(className);
            
            // Find this class in system image and add its superclass
            var classInfo = FindClassInSystemImage(className);
            if (classInfo != null && !string.IsNullOrEmpty(classInfo.Superclass) && classInfo.Superclass != "nil")
            {
                toProcess.Enqueue(classInfo.Superclass);
            }
        }
        
        // Return in dependency order (superclasses first)
        return TopologicalSort(dependencies.ToList());
    }
    
    /// <summary>
    /// Checks if a class is a basic bootstrap class that doesn't need to be included.
    /// </summary>
    private bool IsBasicClass(string className)
    {
        var basicClasses = new HashSet<string>
        {
            "Object", "Behavior", "Class", "Metaclass", "CompiledMethod", "Symbol", "String",
            "Array", "ByteArray", "Collection", "Dictionary", "MethodDictionary", "SystemDictionary",
            "UndefinedObject", "Boolean", "True", "False", "Magnitude", "Number", "Integer",
            "LookupKey", "Association", "SequenceableCollection", "ArrayedCollection"
        };
        
        return basicClasses.Contains(className);
    }
    
    /// <summary>
    /// Finds a class definition in the loaded system image.
    /// </summary>
    private SystemImageClass? FindClassInSystemImage(string className)
    {
        return systemImageData?.Classes?.FirstOrDefault(c => c.Name == className);
    }
    
    /// <summary>
    /// Sorts classes in dependency order (superclasses first).
    /// </summary>
    private List<string> TopologicalSort(List<string> classNames)
    {
        var result = new List<string>();
        var visited = new HashSet<string>();
        
        foreach (var className in classNames)
        {
            TopologicalSortHelper(className, visited, result);
        }
        
        return result;
    }
    
    /// <summary>
    /// Helper for topological sort.
    /// </summary>
    private void TopologicalSortHelper(string className, HashSet<string> visited, List<string> result)
    {
        if (visited.Contains(className))
            return;
            
        visited.Add(className);
        
        // Process superclass first
        var classInfo = FindClassInSystemImage(className);
        if (classInfo?.Superclass != null && !string.IsNullOrEmpty(classInfo.Superclass) && classInfo.Superclass != "nil")
        {
            TopologicalSortHelper(classInfo.Superclass, visited, result);
        }
        
        result.Add(className);
    }
    
    /// <summary>
    /// Generates Smalltalk source code for a class from the system image.
    /// </summary>
    private string GenerateClassSourceFromSystemImage(string className)
    {
        var classInfo = FindClassInSystemImage(className);
        if (classInfo == null)
        {
            throw new InvalidOperationException($"Class {className} not found in system image");
        }
        
        var source = new StringBuilder();
        
        // Generate class definition
        var instVars = string.Join(" ", classInfo.InstanceVariables);
        var classVars = string.Join(" ", classInfo.ClassVariables);
        var poolDicts = string.Join(" ", classInfo.PoolDictionaries);
        
        source.AppendLine($"{classInfo.Superclass} subclass: #{classInfo.Name}");
        source.AppendLine($"    instanceVariableNames: '{instVars}'");
        source.AppendLine($"    classVariableNames: '{classVars}'");
        source.AppendLine($"    poolDictionaries: '{poolDicts}'");
        source.AppendLine($"    category: '{classInfo.Category}'!");
        source.AppendLine();
        
        // Find and generate methods for this class
        var classMethods = systemImageData?.Methods?.Where(m => m.ClassName == className).ToList() ?? new List<CompiledMethod>();
        
        if (classMethods.Any())
        {
            var instanceMethods = classMethods.Where(m => !m.IsClassMethod).ToList();
            var classMethodsList = classMethods.Where(m => m.IsClassMethod).ToList();
            
            // Instance methods
            if (instanceMethods.Any())
            {
                source.AppendLine($"!{className} methodsFor: 'generated'!");
                source.AppendLine();
                
                foreach (var method in instanceMethods)
                {
                    source.AppendLine(GenerateMethodSource(method));
                    source.AppendLine();
                }
                
                source.AppendLine("!");
                source.AppendLine();
            }
            
            // Class methods
            if (classMethodsList.Any())
            {
                source.AppendLine($"!{className} class methodsFor: 'generated'!");
                source.AppendLine();
                
                foreach (var method in classMethodsList)
                {
                    source.AppendLine(GenerateMethodSource(method));
                    source.AppendLine();
                }
                
                source.AppendLine("!");
                source.AppendLine();
            }
        }
        
        return source.ToString();
    }
    
    /// <summary>
    /// Generates Smalltalk source code for a method.
    /// Since we only have bytecode, we'll generate a stub that uses the original bytecode.
    /// </summary>
    private string GenerateMethodSource(CompiledMethod method)
    {
        var source = new StringBuilder();
        
        // Method signature - this is simplified since we don't have the original source
        source.AppendLine($"{method.Selector}");
        source.AppendLine($"    \"Generated method with {method.Bytecode.Count} bytecodes\"");
        source.AppendLine($"    <primitive: {method.PrimitiveIndex}>");
        
        // For now, generate a simple stub - in a real implementation, we'd need
        // a bytecode decompiler or keep the original source
        source.AppendLine("    self primitiveFailed");
        
        return source.ToString();
    }
    
    /// <summary>
    /// Uses the standalone compiler to compile the builder class.
    /// </summary>
    private CompiledClass CompileBuilderClass(string builderClassName, string smalltalkSource)
    {
        // Write source to temporary file
        var tempSourceFile = Path.GetTempFileName() + ".st";
        var tempOutputFile = Path.GetTempFileName() + ".json";
        
        try
        {
            File.WriteAllText(tempSourceFile, smalltalkSource);
            Console.WriteLine($"[STBUILDER] Wrote source to temp file: {tempSourceFile}");
            
            // Run standalone compiler
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = standaloneCompilerPath,
                    Arguments = $"\"{tempSourceFile}\" \"{tempOutputFile}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(standaloneCompilerPath)
                }
            };
            
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            
            if (process.ExitCode != 0)
            {
                Console.WriteLine($"[STBUILDER] Standalone compiler failed with exit code {process.ExitCode}");
                Console.WriteLine($"[STBUILDER] Compiler stderr: {error}");
                Console.WriteLine($"[STBUILDER] Compiler stdout: {output}");
                throw new InvalidOperationException($"Standalone compiler failed (exit code {process.ExitCode}): {error}\nOutput: {output}");
            }
            
            if (!File.Exists(tempOutputFile))
            {
                throw new InvalidOperationException($"Compiler succeeded but output file not created: {tempOutputFile}\nOutput: {output}");
            }
            
            // Parse compiled output
            var compiledJson = File.ReadAllText(tempOutputFile);
            var systemData = JsonSerializer.Deserialize<SystemImageData>(compiledJson);
            
            if (systemData?.Classes == null)
            {
                throw new InvalidOperationException("No classes found in compiled output");
            }
            
            var builderClass = systemData.Classes.FirstOrDefault(c => c.Name == builderClassName);
            if (builderClass == null)
            {
                throw new InvalidOperationException($"Compiled class {builderClassName} not found in output");
            }
            
            var builderMethods = systemData.Methods?.Where(m => m.ClassName == builderClassName).ToList() ?? new List<CompiledMethod>();
            Console.WriteLine($"[STBUILDER] Found {builderMethods.Count} methods for {builderClassName}:");
            foreach (var method in builderMethods)
            {
                Console.WriteLine($"  - {method.Selector} (args: {method.NumArgs}, bytecode: {method.Bytecode.Count})");
            }
            
            return new CompiledClass
            {
                ClassDef = builderClass,
                Methods = builderMethods
            };
        }
        finally
        {
            // Cleanup temp files - but keep them for debugging for now
            Console.WriteLine($"[STBUILDER] Debug: Temp files preserved for inspection:");
            Console.WriteLine($"  Source: {tempSourceFile}");
            Console.WriteLine($"  Output: {tempOutputFile}");
            // if (File.Exists(tempSourceFile)) File.Delete(tempSourceFile);
            // if (File.Exists(tempOutputFile)) File.Delete(tempOutputFile);
        }
    }
    
    /// <summary>
    /// Loads the compiled builder class into the VM.
    /// </summary>
    private int LoadBuilderIntoVM(string builderClassName, CompiledClass compiledBuilder)
    {
        Console.WriteLine($"[STBUILDER] Loading builder {builderClassName} into VM");
        
        // Create the builder class using low-level primitives
        var builderClassOop = vm.PrimitiveNewClass();
        
        // Set superclass to Object - try both lookup methods
        var objectClassOop = vm.PrimitiveLookupClass("Object");
        if (objectClassOop < 0)
        {
            // Try looking up via global
            objectClassOop = vm.PrimitiveLookupGlobal("Object");
            if (objectClassOop < 0)
            {
                Console.WriteLine($"[STBUILDER] Warning: Object class not found via lookup. Available globals check...");
                // For now, let's continue without setting superclass and see what happens
                Console.WriteLine($"[STBUILDER] Proceeding without superclass - this is a prototype test");
                objectClassOop = vm.PrimitiveNil(); // Use nil as fallback
            }
        }
        Console.WriteLine($"[STBUILDER] Object class OOP: {objectClassOop}");
        vm.PrimitiveSetSlot(builderClassOop, 0, objectClassOop);
        
        // Create and set method dictionary
        var methodDictOop = vm.PrimitiveNewDictionary();
        vm.PrimitiveSetSlot(builderClassOop, 1, methodDictOop);
        
        // Set class name
        var nameSymbolOop = vm.PrimitiveInternSymbol(builderClassName);
        vm.PrimitiveSetSlot(builderClassOop, 5, nameSymbolOop);
        
        // Install the buildIt method
        var buildItMethod = compiledBuilder.Methods.FirstOrDefault(m => m.Selector == "buildIt");
        if (buildItMethod == null)
        {
            throw new InvalidOperationException($"buildIt method not found in compiled {builderClassName}");
        }
        
        InstallCompiledMethodInVM(builderClassOop, buildItMethod);
        
        // Register the builder class globally so we can find it later
        vm.PrimitiveRegisterGlobal(builderClassName, builderClassOop);
        
        Console.WriteLine($"[STBUILDER] Builder {builderClassName} loaded at OOP {builderClassOop}");
        return builderClassOop;
    }
    
    /// <summary>
    /// Installs a compiled method in the VM's method dictionary.
    /// </summary>
    private void InstallCompiledMethodInVM(int classOop, CompiledMethod method)
    {
        Console.WriteLine($"[STBUILDER] Installing method {method.Selector} on class OOP {classOop}");
        
        // Create CompiledMethod object - try both lookup methods
        var compiledMethodClassOop = vm.PrimitiveLookupClass("CompiledMethod");
        if (compiledMethodClassOop < 0)
        {
            compiledMethodClassOop = vm.PrimitiveLookupGlobal("CompiledMethod");
            if (compiledMethodClassOop < 0)
            {
                Console.WriteLine($"[STBUILDER] Warning: CompiledMethod class not found. Skipping method installation for prototype test.");
                return; // Skip method installation for now
            }
        }
        
        var methodOop = vm.PrimitiveNewInstance(compiledMethodClassOop);
        
        // Set bytecode (convert from List<int> to byte array)
        var bytecodeArray = method.Bytecode.Select(b => (byte)b).ToArray();
        var bytecodeArrayOop = vm.PrimitiveNewByteArray(bytecodeArray.Length);
        for (int i = 0; i < bytecodeArray.Length; i++)
        {
            vm.PrimitiveByteArrayAtPut(bytecodeArrayOop, i, bytecodeArray[i]);
        }
        vm.PrimitiveSetSlot(methodOop, 0, bytecodeArrayOop); // Bytecode in slot 0
        
        // Set literals array (for now, empty - we'd need to properly convert)
        var literalsArrayOop = vm.PrimitiveNewArray(0);
        vm.PrimitiveSetSlot(methodOop, 1, literalsArrayOop); // Literals in slot 1
        
        // Set header
        var headerValue = method.PrimitiveIndex | (method.NumTemps << 10);
        var headerOop = vm.PrimitiveNewSmallInteger(headerValue);
        vm.PrimitiveSetSlot(methodOop, 2, headerOop); // Header in slot 2
        
        // Get method dictionary and install method
        var methodDictOop = vm.GetSlot(classOop, 1);
        var selectorOop = vm.PrimitiveInternSymbol(method.Selector);
        
        // Use at:put: primitive to install method
        vm.PrimitiveDictionaryAtPut(methodDictOop, selectorOop, methodOop);
        
        Console.WriteLine($"[STBUILDER] Method {method.Selector} installed successfully");
    }
    
    /// <summary>
    /// Executes the builder class to create the target class.
    /// </summary>
    private int ExecuteBuilder(int builderClassOop)
    {
        // Create instance of builder class and send buildIt message
        var builderInstance = vm.PrimitiveNewInstance(builderClassOop);
        var buildItSelector = vm.PrimitiveInternSymbol("buildIt");
        
        // Send buildIt message
        var result = vm.PrimitiveSendMessage(builderInstance, buildItSelector, new int[0]);
        
        return result;
    }
    
    /// <summary>
    /// Verifies that the target class was created correctly in the VM.
    /// </summary>
    private void VerifyTargetClass(ClassDefinition classDef, int targetClassOop)
    {
        Console.WriteLine($"[STBUILDER] Verifying target class {classDef.Name} at OOP {targetClassOop}");
        
        // Check that the class exists and is accessible
        if (targetClassOop <= 0)
        {
            throw new InvalidOperationException($"Invalid target class OOP: {targetClassOop}");
        }
        
        // Verify the class is registered globally
        var registeredClassOop = vm.PrimitiveLookupClass(classDef.Name);
        if (registeredClassOop != targetClassOop)
        {
            throw new InvalidOperationException($"Target class {classDef.Name} not properly registered globally. Expected OOP {targetClassOop}, found {registeredClassOop}");
        }
        Console.WriteLine($"✓ Class {classDef.Name} is properly registered globally");
        
        // Verify superclass relationship
        var superclassOop = vm.GetSlot(targetClassOop, 0); // Superclass in slot 0
        var expectedSuperclassOop = vm.PrimitiveLookupClass(classDef.Superclass);
        if (superclassOop != expectedSuperclassOop)
        {
            throw new InvalidOperationException($"Target class {classDef.Name} has wrong superclass. Expected {classDef.Superclass} (OOP {expectedSuperclassOop}), found OOP {superclassOop}");
        }
        Console.WriteLine($"✓ Class {classDef.Name} has correct superclass {classDef.Superclass}");
        
        // Verify method dictionary exists
        var methodDictOop = vm.GetSlot(targetClassOop, 1); // Method dictionary in slot 1
        if (methodDictOop <= 0)
        {
            throw new InvalidOperationException($"Target class {classDef.Name} has no method dictionary");
        }
        Console.WriteLine($"✓ Class {classDef.Name} has method dictionary at OOP {methodDictOop}");
        
        // Verify class name
        var classNameOop = vm.GetSlot(targetClassOop, 5); // Class name in slot 5
        // Note: In a real implementation, we'd check if this symbol matches the expected name
        // For now, just verify it exists
        if (classNameOop <= 0)
        {
            throw new InvalidOperationException($"Target class {classDef.Name} has no class name symbol");
        }
        Console.WriteLine($"✓ Class {classDef.Name} has class name symbol at OOP {classNameOop}");
        
        Console.WriteLine($"[STBUILDER] Target class {classDef.Name} verification PASSED");
    }
    
    /// <summary>
    /// Helper classes for compilation results.
    /// </summary>
    private class CompiledClass
    {
        public SystemImageClass ClassDef { get; set; } = new();
        public List<CompiledMethod> Methods { get; set; } = new();
    }
    
    private class CompiledMethod
    {
        [JsonPropertyName("class")]
        public string ClassName { get; set; } = "";
        
        [JsonPropertyName("selector")]
        public string Selector { get; set; } = "";
        
        [JsonPropertyName("bytecode")]
        public List<int> Bytecode { get; set; } = new();
        
        [JsonPropertyName("literals")]
        public List<string> Literals { get; set; } = new();
        
        [JsonPropertyName("primitiveIndex")]
        public int PrimitiveIndex { get; set; } = 0;
        
        [JsonPropertyName("numArgs")]
        public int NumArgs { get; set; } = 0;
        
        [JsonPropertyName("numTemps")]
        public int NumTemps { get; set; } = 0;
        
        [JsonPropertyName("isClassMethod")]
        public bool IsClassMethod { get; set; } = false;
    }
    
    /// <summary>
    /// System image data structure for JSON deserialization.
    /// </summary>
    private class SystemImageData
    {
        [JsonPropertyName("classes")]
        public List<SystemImageClass> Classes { get; set; } = new();
        
        [JsonPropertyName("methods")]
        public List<CompiledMethod> Methods { get; set; } = new();
    }
    
    /// <summary>
    /// System image class structure.
    /// </summary>
    private class SystemImageClass
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        
        [JsonPropertyName("superclass")]
        public string Superclass { get; set; } = "";
        
        [JsonPropertyName("instanceVariables")]
        public List<string> InstanceVariables { get; set; } = new();
        
        [JsonPropertyName("classVariables")]
        public List<string> ClassVariables { get; set; } = new();
        
        [JsonPropertyName("poolDictionaries")]
        public List<string> PoolDictionaries { get; set; } = new();
        
        [JsonPropertyName("category")]
        public string Category { get; set; } = "";
    }
}