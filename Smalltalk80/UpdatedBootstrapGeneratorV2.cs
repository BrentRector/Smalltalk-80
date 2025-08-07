// ==============================================================================
// Updated Bootstrap Generator V2 - Following Correct Creation Sequence
// ==============================================================================
// This version follows the required bootstrap sequence:
// 1. Class → 2. Metaclass → 3. Behavior → 4. ProtoObject → 5. Object
// 6. Symbol → 7. Dictionary → 8. Smalltalk
// ==============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Smalltalk80.Parser;

namespace Smalltalk80;

/// <summary>
/// Generates C# bootstrap code from Bootstrap.st following the correct creation sequence.
/// ProtoObject is the root, not Object.
/// </summary>
public class UpdatedBootstrapGeneratorV2
{
    // Field indices from Smalltalk-80 specification
    private const string BEHAVIOR_SUPERCLASS = "0";
    private const string BEHAVIOR_METHODDICT = "1";
    private const string BEHAVIOR_FORMAT = "2";
    private const string BEHAVIOR_INSTANCEVARS = "3";
    private const string BEHAVIOR_ORGANIZATION = "4";
    
    private const string CLASS_NAME = "5";
    private const string CLASS_CLASSPOOL = "6";  // CRITICAL for class variables!
    private const string CLASS_SHAREDPOOLS = "7";
    
    private const string METACLASS_THISCLASS = "5";
    
    // Track what we're generating
    private readonly List<ClassDefinition> classes = new();
    private readonly List<MethodDefinition> methods = new();
    private readonly Dictionary<string, string> classVariables = new();
    private readonly Dictionary<string, List<string>> instanceVariables = new();
    
    // Required bootstrap sequence
    private readonly string[] requiredSequence = new[]
    {
        "Class", "Metaclass", "Behavior", "ProtoObject", 
        "Object", "Symbol", "Dictionary", "Smalltalk"
    };
    
    /// <summary>
    /// Generate C# bootstrap code from Bootstrap.st
    /// </summary>
    public static void Generate(string bootstrapStPath, string outputPath)
    {
        var generator = new UpdatedBootstrapGeneratorV2();
        generator.ParseBootstrapFile(bootstrapStPath);
        generator.ValidateBootstrapClasses();
        var code = generator.GenerateCSharpCode();
        File.WriteAllText(outputPath, code);
        Console.WriteLine($"Generated bootstrap code to {outputPath}");
        Console.WriteLine($"  Classes: {generator.classes.Count}");
        Console.WriteLine($"  Methods: {generator.methods.Count}");
    }
    
    /// <summary>
    /// Validate that all required bootstrap classes are present
    /// </summary>
    private void ValidateBootstrapClasses()
    {
        foreach (var required in requiredSequence)
        {
            if (!classes.Any(c => c.Name == required))
            {
                throw new InvalidOperationException(
                    $"Missing required bootstrap class: {required}. " +
                    $"Bootstrap.st must define: {string.Join(", ", requiredSequence)}");
            }
        }
        
        // Verify ProtoObject is the root
        var protoObject = classes.First(c => c.Name == "ProtoObject");
        if (protoObject.Superclass != "nil")
        {
            throw new InvalidOperationException(
                $"ProtoObject must have nil superclass, but has: {protoObject.Superclass}");
        }
        
        // Verify Object extends ProtoObject
        var objectClass = classes.First(c => c.Name == "Object");
        if (objectClass.Superclass != "ProtoObject")
        {
            throw new InvalidOperationException(
                $"Object must extend ProtoObject, but extends: {objectClass.Superclass}");
        }
    }
    
    /// <summary>
    /// Parse Bootstrap.st to extract class and method definitions
    /// </summary>
    private void ParseBootstrapFile(string path)
    {
        var content = File.ReadAllText(path);
        
        // Use ParserService to parse the file
        var fileNode = ParserService.ParseFileIn(content);
        
        // Process each chunk in the file
        foreach (var chunk in fileNode.Chunks)
        {
            switch (chunk)
            {
                case ClassDefinitionNode classDef:
                    ProcessClassDefinition(classDef);
                    break;
                    
                case MethodCategoryNode methodCat:
                    foreach (var method in methodCat.Methods)
                    {
                        ProcessMethodDefinition(methodCat.Header, method);
                    }
                    break;
                    
                case ClassCommentNode:
                    // Skip comments
                    break;
            }
        }
    }
    
    /// <summary>
    /// Process a class definition from Bootstrap.st
    /// </summary>
    private void ProcessClassDefinition(ClassDefinitionNode node)
    {
        var classDef = new ClassDefinition
        {
            Name = node.ClassName,
            Superclass = node.Superclass ?? "nil",
            InstanceVariables = node.InstanceVariables.ToList(),
            ClassVariables = node.ClassVariables.ToList(),
            Category = node.Category ?? "Kernel"
        };
        
        classes.Add(classDef);
        
        // Track instance variables for method compilation
        instanceVariables[classDef.Name] = classDef.InstanceVariables;
        
        // Track class variables
        foreach (var cv in classDef.ClassVariables)
        {
            classVariables[$"{classDef.Name}.{cv}"] = cv;
        }
    }
    
    /// <summary>
    /// Process a method definition from Bootstrap.st
    /// </summary>
    private void ProcessMethodDefinition(MethodHeaderNode header, MethodDefinitionNode node)
    {
        var methodDef = new MethodDefinition
        {
            ClassName = header.ClassName,
            Selector = node.Selector,
            Source = node.Source,
            Category = header.Category,
            IsClassMethod = header.IsClassSide
        };
        
        methodDef.Arguments = node.Arguments.ToList();
        methodDef.Temporaries = node.Body.Temporaries.ToList();
        methodDef.Statements = node.Body.Statements.ToList();
        methodDef.PrimitiveIndex = node.HasPrimitive ? node.PrimitiveIndex : (int?)null;
        
        methods.Add(methodDef);
    }
    
    /// <summary>
    /// Generate the C# bootstrap code following the correct sequence
    /// </summary>
    private string GenerateCSharpCode()
    {
        var sb = new StringBuilder();
        
        // File header
        sb.AppendLine("// ==============================================================================");
        sb.AppendLine("// Generated Bootstrap Code from Bootstrap.st");
        sb.AppendLine($"// Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("// This code creates real Smalltalk objects using only VM primitives");
        sb.AppendLine("// Following the required sequence: Class → Metaclass → Behavior → ProtoObject");
        sb.AppendLine("// ==============================================================================");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine("namespace Smalltalk80;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Generated bootstrap that creates Smalltalk objects from Bootstrap.st");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class GeneratedBootstrapV2");
        sb.AppendLine("{");
        
        // Field index constants
        GenerateFieldConstants(sb);
        
        // Class references
        GenerateClassReferences(sb);
        
        // Initialize method with correct sequence
        GenerateInitializeMethodV2(sb);
        
        // Primitive wrapper methods
        GeneratePrimitiveWrappers(sb);
        
        // Helper methods
        GenerateHelperMethods(sb);
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generate field index constants
    /// </summary>
    private void GenerateFieldConstants(StringBuilder sb)
    {
        sb.AppendLine("    // Field indices (0-based)");
        sb.AppendLine("    private const int BEHAVIOR_SUPERCLASS = 0;");
        sb.AppendLine("    private const int BEHAVIOR_METHODDICT = 1;");
        sb.AppendLine("    private const int BEHAVIOR_FORMAT = 2;");
        sb.AppendLine("    private const int BEHAVIOR_INSTANCEVARS = 3;");
        sb.AppendLine("    private const int BEHAVIOR_ORGANIZATION = 4;");
        sb.AppendLine("    private const int CLASS_NAME = 5;");
        sb.AppendLine("    private const int CLASS_CLASSPOOL = 6;  // CRITICAL!");
        sb.AppendLine("    private const int CLASS_SHAREDPOOLS = 7;");
        sb.AppendLine("    private const int METACLASS_THISCLASS = 5;");
        sb.AppendLine("    private const int DICT_TALLY = 0;");
        sb.AppendLine("    private const int DICT_ARRAY = 1;");
        sb.AppendLine("    private const int ASSOC_KEY = 0;");
        sb.AppendLine("    private const int ASSOC_VALUE = 1;");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generate class reference fields
    /// </summary>
    private void GenerateClassReferences(StringBuilder sb)
    {
        sb.AppendLine("    // Special objects");
        sb.AppendLine("    private static SmalltalkObject nil;");
        sb.AppendLine("    private static SmalltalkObject trueObj;");
        sb.AppendLine("    private static SmalltalkObject falseObj;");
        sb.AppendLine();
        sb.AppendLine("    // Core bootstrap classes (in required order)");
        
        // Generate in the required sequence order
        foreach (var className in requiredSequence)
        {
            var varName = GetClassVariableName(className);
            sb.AppendLine($"    private static SmalltalkObject {varName};");
            sb.AppendLine($"    private static SmalltalkObject {varName}Metaclass;");
        }
        
        // Add any additional classes not in the core sequence
        foreach (var cls in classes.Where(c => !requiredSequence.Contains(c.Name)))
        {
            var varName = GetClassVariableName(cls.Name);
            sb.AppendLine($"    private static SmalltalkObject {varName};");
            sb.AppendLine($"    private static SmalltalkObject {varName}Metaclass;");
        }
        
        sb.AppendLine();
        sb.AppendLine("    private static Dictionary<string, SmalltalkObject> symbolTable = new();");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generate the Initialize method following the correct sequence
    /// </summary>
    private void GenerateInitializeMethodV2(StringBuilder sb)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Initialize the bootstrap following the required sequence");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static void Initialize()");
        sb.AppendLine("    {");
        sb.AppendLine("        Console.WriteLine(\"[BOOTSTRAP_V2] Starting bootstrap with correct sequence...\");");
        sb.AppendLine();
        sb.AppendLine("        // Clear any existing state");
        sb.AppendLine("        SystemDictionary.Clear();");
        sb.AppendLine("        symbolTable.Clear();");
        sb.AppendLine();
        
        // Phase 0: Create special objects
        sb.AppendLine("        // ================== Phase 0: Special Objects ==================");
        sb.AppendLine("        nil = Primitive71_New(null, 0);");
        sb.AppendLine("        trueObj = Primitive71_New(null, 0);");
        sb.AppendLine("        falseObj = Primitive71_New(null, 0);");
        sb.AppendLine();
        
        // Phase 1: Create all class and metaclass objects
        sb.AppendLine("        // ================== Phase 1: Create Objects ==================");
        sb.AppendLine("        // Create all class objects (8 fields each)");
        foreach (var className in requiredSequence)
        {
            var varName = GetClassVariableName(className);
            sb.AppendLine($"        {varName} = Primitive71_New(null, 8);");
        }
        foreach (var cls in classes.Where(c => !requiredSequence.Contains(c.Name)))
        {
            var varName = GetClassVariableName(cls.Name);
            sb.AppendLine($"        {varName} = Primitive71_New(null, 8);");
        }
        sb.AppendLine();
        
        sb.AppendLine("        // Create all metaclass objects (6 fields each)");
        foreach (var className in requiredSequence)
        {
            var varName = GetClassVariableName(className);
            sb.AppendLine($"        {varName}Metaclass = Primitive71_New(null, 6);");
        }
        foreach (var cls in classes.Where(c => !requiredSequence.Contains(c.Name)))
        {
            var varName = GetClassVariableName(cls.Name);
            sb.AppendLine($"        {varName}Metaclass = Primitive71_New(null, 6);");
        }
        sb.AppendLine();
        
        // Phase 2: Core relationships
        sb.AppendLine("        // ================== Phase 2: Core Relationships ==================");
        sb.AppendLine("        // Step 1: Class extends Behavior");
        sb.AppendLine("        Primitive61_AtPut(classClass, BEHAVIOR_SUPERCLASS + 1, behaviorClass);");
        sb.AppendLine("        SetClassPointer(classClass, classMetaclass);");
        sb.AppendLine();
        
        sb.AppendLine("        // Step 2: Metaclass extends Class (creates the loop!)");
        sb.AppendLine("        Primitive61_AtPut(metaclassClass, BEHAVIOR_SUPERCLASS + 1, classClass);");
        sb.AppendLine("        SetClassPointer(metaclassClass, metaclassMetaclass);");
        sb.AppendLine();
        
        sb.AppendLine("        // Step 3: Behavior extends ProtoObject");
        sb.AppendLine("        Primitive61_AtPut(behaviorClass, BEHAVIOR_SUPERCLASS + 1, protoObjectClass);");
        sb.AppendLine("        SetClassPointer(behaviorClass, behaviorMetaclass);");
        sb.AppendLine();
        
        sb.AppendLine("        // Step 4: ProtoObject has no superclass (root)");
        sb.AppendLine("        Primitive61_AtPut(protoObjectClass, BEHAVIOR_SUPERCLASS + 1, nil);");
        sb.AppendLine("        SetClassPointer(protoObjectClass, protoObjectMetaclass);");
        sb.AppendLine();
        
        // Phase 3: Metaclass hierarchy
        sb.AppendLine("        // ================== Phase 3: Metaclass Hierarchy ==================");
        sb.AppendLine("        // All metaclasses are instances of Metaclass");
        sb.AppendLine("        SetClassPointer(classMetaclass, metaclassClass);");
        sb.AppendLine("        SetClassPointer(metaclassMetaclass, metaclassClass);  // Self-reference!");
        sb.AppendLine("        SetClassPointer(behaviorMetaclass, metaclassClass);");
        sb.AppendLine("        SetClassPointer(protoObjectMetaclass, metaclassClass);");
        sb.AppendLine();
        
        sb.AppendLine("        // Metaclass hierarchy parallels class hierarchy");
        sb.AppendLine("        Primitive61_AtPut(classMetaclass, BEHAVIOR_SUPERCLASS + 1, behaviorMetaclass);");
        sb.AppendLine("        Primitive61_AtPut(metaclassMetaclass, BEHAVIOR_SUPERCLASS + 1, classMetaclass);");
        sb.AppendLine("        Primitive61_AtPut(behaviorMetaclass, BEHAVIOR_SUPERCLASS + 1, protoObjectMetaclass);");
        sb.AppendLine("        Primitive61_AtPut(protoObjectMetaclass, BEHAVIOR_SUPERCLASS + 1, classClass);  // Loop back!");
        sb.AppendLine();
        
        // Phase 4: Set thisClass
        sb.AppendLine("        // ================== Phase 4: Set thisClass ==================");
        foreach (var className in requiredSequence)
        {
            var varName = GetClassVariableName(className);
            sb.AppendLine($"        Primitive61_AtPut({varName}Metaclass, METACLASS_THISCLASS + 1, {varName});");
        }
        foreach (var cls in classes.Where(c => !requiredSequence.Contains(c.Name)))
        {
            var varName = GetClassVariableName(cls.Name);
            sb.AppendLine($"        Primitive61_AtPut({varName}Metaclass, METACLASS_THISCLASS + 1, {varName});");
        }
        sb.AppendLine();
        
        // Phase 5: Remaining classes and relationships
        sb.AppendLine("        // ================== Phase 5: Remaining Classes ==================");
        sb.AppendLine("        // Object extends ProtoObject");
        sb.AppendLine("        Primitive61_AtPut(objectClass, BEHAVIOR_SUPERCLASS + 1, protoObjectClass);");
        sb.AppendLine("        SetClassPointer(objectClass, objectMetaclass);");
        sb.AppendLine("        SetClassPointer(objectMetaclass, metaclassClass);");
        sb.AppendLine("        Primitive61_AtPut(objectMetaclass, BEHAVIOR_SUPERCLASS + 1, protoObjectMetaclass);");
        sb.AppendLine();
        
        // Set up remaining classes
        foreach (var cls in classes.Where(c => !requiredSequence.Take(5).Contains(c.Name)))
        {
            var varName = GetClassVariableName(cls.Name);
            var superVar = cls.Superclass == "nil" ? "nil" : GetClassVariableName(cls.Superclass);
            
            sb.AppendLine($"        // {cls.Name} extends {cls.Superclass}");
            sb.AppendLine($"        Primitive61_AtPut({varName}, BEHAVIOR_SUPERCLASS + 1, {superVar});");
            sb.AppendLine($"        SetClassPointer({varName}, {varName}Metaclass);");
            sb.AppendLine($"        SetClassPointer({varName}Metaclass, metaclassClass);");
            
            if (cls.Superclass != "nil")
            {
                var superMetaVar = GetClassVariableName(cls.Superclass) + "Metaclass";
                sb.AppendLine($"        Primitive61_AtPut({varName}Metaclass, BEHAVIOR_SUPERCLASS + 1, {superMetaVar});");
            }
            sb.AppendLine();
        }
        
        // Phase 6: Initialize class fields
        sb.AppendLine("        // ================== Phase 6: Initialize Fields ==================");
        foreach (var cls in classes)
        {
            var varName = GetClassVariableName(cls.Name);
            
            sb.AppendLine($"        // Initialize {cls.Name}");
            sb.AppendLine($"        Primitive61_AtPut({varName}, CLASS_NAME + 1, InternSymbol(\"{cls.Name}\"));");
            sb.AppendLine($"        Primitive61_AtPut({varName}, BEHAVIOR_METHODDICT + 1, CreateEmptyDictionary());");
            sb.AppendLine($"        Primitive61_AtPut({varName}Metaclass, BEHAVIOR_METHODDICT + 1, CreateEmptyDictionary());");
            
            // Class variables - CRITICAL!
            if (cls.ClassVariables.Count > 0)
            {
                sb.AppendLine($"        {{");
                sb.AppendLine($"            var classPool = CreateEmptyDictionary();");
                foreach (var cv in cls.ClassVariables)
                {
                    sb.AppendLine($"            AddToClassPool(classPool, \"{cv}\", nil);");
                }
                sb.AppendLine($"            // CRITICAL: classPool at index 6 fixes class variable lookup!");
                sb.AppendLine($"            Primitive61_AtPut({varName}, CLASS_CLASSPOOL + 1, classPool);");
                sb.AppendLine($"        }}");
            }
            else
            {
                sb.AppendLine($"        Primitive61_AtPut({varName}, CLASS_CLASSPOOL + 1, CreateEmptyDictionary());");
            }
            
            // Instance variables
            if (cls.InstanceVariables.Count > 0)
            {
                sb.AppendLine($"        {{");
                sb.AppendLine($"            var ivars = Primitive71_New(arrayClass, {cls.InstanceVariables.Count});");
                for (int i = 0; i < cls.InstanceVariables.Count; i++)
                {
                    sb.AppendLine($"            Primitive61_AtPut(ivars, {i + 1}, InternSymbol(\"{cls.InstanceVariables[i]}\"));");
                }
                sb.AppendLine($"            Primitive61_AtPut({varName}, BEHAVIOR_INSTANCEVARS + 1, ivars);");
                sb.AppendLine($"        }}");
            }
            sb.AppendLine();
        }
        
        // Phase 7: Install methods
        sb.AppendLine("        // ================== Phase 7: Install Methods ==================");
        foreach (var method in methods)
        {
            GenerateMethodInstallation(sb, method);
        }
        sb.AppendLine();
        
        // Phase 8: Register with SystemDictionary
        sb.AppendLine("        // ================== Phase 8: Register Classes ==================");
        foreach (var cls in classes)
        {
            var varName = GetClassVariableName(cls.Name);
            sb.AppendLine($"        SystemDictionary.RegisterBootstrapClass(\"{cls.Name}\", {varName} as SmalltalkClass);");
        }
        sb.AppendLine();
        
        sb.AppendLine("        // Register special objects");
        sb.AppendLine("        SystemDictionary.SetGlobal(\"nil\", nil);");
        sb.AppendLine("        SystemDictionary.SetGlobal(\"true\", trueObj);");
        sb.AppendLine("        SystemDictionary.SetGlobal(\"false\", falseObj);");
        sb.AppendLine();
        
        // Phase 9: Create Smalltalk global
        sb.AppendLine("        // ================== Phase 9: Smalltalk Global ==================");
        sb.AppendLine("        CreateSmalltalkGlobal();");
        sb.AppendLine();
        
        // Validation
        sb.AppendLine("        // ================== Validation ==================");
        sb.AppendLine("        ValidateBootstrap();");
        sb.AppendLine();
        
        sb.AppendLine("        Console.WriteLine(\"[BOOTSTRAP_V2] Bootstrap complete!\");");
        sb.AppendLine("    }");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generate primitive wrapper methods
    /// </summary>
    private void GeneratePrimitiveWrappers(StringBuilder sb)
    {
        sb.AppendLine("    // ============================================");
        sb.AppendLine("    // PRIMITIVE WRAPPERS");
        sb.AppendLine("    // ============================================");
        sb.AppendLine();
        
        // Primitive 71: new:
        sb.AppendLine("    private static SmalltalkObject Primitive71_New(SmalltalkObject? cls, int size)");
        sb.AppendLine("    {");
        sb.AppendLine("        return new SmalltalkObject((SmalltalkClass?)cls, size);");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Primitive 61: at:put:
        sb.AppendLine("    private static void Primitive61_AtPut(SmalltalkObject obj, int oneBasedIndex, object? value)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (oneBasedIndex < 1 || oneBasedIndex > obj.Fields.Length)");
        sb.AppendLine("            throw new ArgumentOutOfRangeException(nameof(oneBasedIndex));");
        sb.AppendLine("        obj.Fields[oneBasedIndex - 1] = value;");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // SetClassPointer
        sb.AppendLine("    private static void SetClassPointer(SmalltalkObject obj, SmalltalkObject cls)");
        sb.AppendLine("    {");
        sb.AppendLine("        obj.ClassPointer = (SmalltalkClass)cls;");
        sb.AppendLine("    }");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generate helper methods
    /// </summary>
    private void GenerateHelperMethods(StringBuilder sb)
    {
        // InternSymbol
        sb.AppendLine("    private static SmalltalkObject InternSymbol(string str)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (symbolTable.TryGetValue(str, out var existing))");
        sb.AppendLine("            return existing;");
        sb.AppendLine("        var symbol = new Symbol(str);");
        sb.AppendLine("        symbolTable[str] = symbol;");
        sb.AppendLine("        return symbol;");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // CreateEmptyDictionary
        sb.AppendLine("    private static SmalltalkObject CreateEmptyDictionary()");
        sb.AppendLine("    {");
        sb.AppendLine("        var dict = Primitive71_New(dictionaryClass, 2);");
        sb.AppendLine("        Primitive61_AtPut(dict, DICT_TALLY + 1, new SmallInteger(0));");
        sb.AppendLine("        var array = Primitive71_New(arrayClass, 11);");
        sb.AppendLine("        for (int i = 1; i <= 11; i++)");
        sb.AppendLine("            Primitive61_AtPut(array, i, nil);");
        sb.AppendLine("        Primitive61_AtPut(dict, DICT_ARRAY + 1, array);");
        sb.AppendLine("        return dict;");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // AddToClassPool
        sb.AppendLine("    private static void AddToClassPool(SmalltalkObject classPool, string name, object? value)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Simplified: would need proper Dictionary implementation");
        sb.AppendLine("        // This is a placeholder that maintains the structure");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // InstallMethod
        sb.AppendLine("    private static void InstallMethod(SmalltalkObject cls, string selector, CompiledMethod method)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Simplified: would need proper MethodDictionary implementation");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // CreateSmalltalkGlobal
        sb.AppendLine("    private static void CreateSmalltalkGlobal()");
        sb.AppendLine("    {");
        sb.AppendLine("        // Smalltalk is an instance of SystemDictionary (or Dictionary)");
        sb.AppendLine("        var smalltalk = Primitive71_New(dictionaryClass, 2);");
        sb.AppendLine("        Primitive61_AtPut(smalltalk, DICT_TALLY + 1, new SmallInteger(0));");
        sb.AppendLine("        var array = Primitive71_New(arrayClass, 101);");
        sb.AppendLine("        for (int i = 1; i <= 101; i++)");
        sb.AppendLine("            Primitive61_AtPut(array, i, nil);");
        sb.AppendLine("        Primitive61_AtPut(smalltalk, DICT_ARRAY + 1, array);");
        sb.AppendLine("        SystemDictionary.SetGlobal(\"Smalltalk\", smalltalk);");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // ValidateBootstrap
        sb.AppendLine("    private static void ValidateBootstrap()");
        sb.AppendLine("    {");
        sb.AppendLine("        // Verify critical relationships");
        sb.AppendLine("        Console.WriteLine(\"[VALIDATION] Checking bootstrap integrity...\");");
        sb.AppendLine("        ");
        sb.AppendLine("        // ProtoObject should be the root");
        sb.AppendLine("        if (protoObjectClass.Fields[BEHAVIOR_SUPERCLASS] != nil)");
        sb.AppendLine("            throw new InvalidOperationException(\"ProtoObject superclass should be nil\");");
        sb.AppendLine("        ");
        sb.AppendLine("        // Object should extend ProtoObject");
        sb.AppendLine("        if (objectClass.Fields[BEHAVIOR_SUPERCLASS] != protoObjectClass)");
        sb.AppendLine("            throw new InvalidOperationException(\"Object should extend ProtoObject\");");
        sb.AppendLine("        ");
        sb.AppendLine("        // Metaclass loop check");
        sb.AppendLine("        if (metaclassClass.ClassPointer != metaclassClass)");
        sb.AppendLine("            throw new InvalidOperationException(\"Metaclass class should be Metaclass (self-loop)\");");
        sb.AppendLine("        ");
        sb.AppendLine("        Console.WriteLine(\"[VALIDATION] Bootstrap integrity verified!\");");
        sb.AppendLine("    }");
    }
    
    /// <summary>
    /// Generate method installation code
    /// </summary>
    private void GenerateMethodInstallation(StringBuilder sb, MethodDefinition method)
    {
        var className = GetClassVariableName(method.ClassName);
        var targetClass = method.IsClassMethod ? $"{className}Metaclass" : className;
        
        sb.AppendLine($"        // Install {method.ClassName}{(method.IsClassMethod ? " class" : "")}>>{method.Selector}");
        
        // For now, create a simple stub method
        sb.AppendLine($"        {{");
        sb.AppendLine($"            var method = new CompiledMethod");
        sb.AppendLine($"            {{");
        sb.AppendLine($"                Bytecodes = new byte[] {{ 120 }},  // return self");
        sb.AppendLine($"                Literals = new object[0]");
        sb.AppendLine($"            }};");
        sb.AppendLine($"            InstallMethod({targetClass}, \"{method.Selector}\", method);");
        sb.AppendLine($"        }}");
    }
    
    /// <summary>
    /// Get C# variable name for a class
    /// </summary>
    private string GetClassVariableName(string className)
    {
        return char.ToLower(className[0]) + className.Substring(1) + "Class";
    }
    
    // Helper classes for tracking definitions
    private class ClassDefinition
    {
        public string Name { get; set; } = "";
        public string Superclass { get; set; } = "";
        public List<string> InstanceVariables { get; set; } = new();
        public List<string> ClassVariables { get; set; } = new();
        public string Category { get; set; } = "";
    }
    
    private class MethodDefinition
    {
        public string ClassName { get; set; } = "";
        public string Selector { get; set; } = "";
        public string Source { get; set; } = "";
        public string Category { get; set; } = "";
        public bool IsClassMethod { get; set; }
        public List<string> Arguments { get; set; } = new();
        public List<string> Temporaries { get; set; } = new();
        public List<StatementNode> Statements { get; set; } = new();
        public int? PrimitiveIndex { get; set; }
    }
}