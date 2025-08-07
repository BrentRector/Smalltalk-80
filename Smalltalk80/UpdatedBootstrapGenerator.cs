// ==============================================================================
// Updated Bootstrap Generator
// Reads Bootstrap.st and generates C# code that creates Smalltalk objects
// using only VM primitives as specified in COMPREHENSIVE_BOOTSTRAP_DESIGN.md
// ==============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Smalltalk80.Parser;

namespace Smalltalk80;

/// <summary>
/// Generates C# bootstrap code from Bootstrap.st that creates real Smalltalk objects
/// using only VM primitives - no C# shadow structures.
/// </summary>
public class UpdatedBootstrapGenerator
{
    // Field indices from COMPREHENSIVE_BOOTSTRAP_DESIGN.md
    private const string BEHAVIOR_SUPERCLASS = "0";
    private const string BEHAVIOR_METHODDICT = "1";
    private const string BEHAVIOR_FORMAT = "2";
    private const string BEHAVIOR_INSTANCEVARS = "3";
    private const string BEHAVIOR_ORGANIZATION = "4";
    
    private const string CLASS_NAME = "5";
    private const string CLASS_CLASSPOOL = "6";
    private const string CLASS_SHAREDPOOLS = "7";
    
    private const string METACLASS_THISCLASS = "5";
    
    // Track what we're generating
    private readonly List<ClassDefinition> classes = new();
    private readonly List<MethodDefinition> methods = new();
    private readonly Dictionary<string, string> classVariables = new();
    private readonly Dictionary<string, List<string>> instanceVariables = new();
    
    /// <summary>
    /// Generate C# bootstrap code from Bootstrap.st
    /// </summary>
    public static void Generate(string bootstrapStPath, string outputPath)
    {
        var generator = new UpdatedBootstrapGenerator();
        generator.ParseBootstrapFile(bootstrapStPath);
        var code = generator.GenerateCSharpCode();
        File.WriteAllText(outputPath, code);
        Console.WriteLine($"Generated bootstrap code to {outputPath}");
        Console.WriteLine($"  Classes: {generator.classes.Count}");
        Console.WriteLine($"  Methods: {generator.methods.Count}");
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
    /// Generate the C# bootstrap code
    /// </summary>
    private string GenerateCSharpCode()
    {
        var sb = new StringBuilder();
        
        // File header
        sb.AppendLine("// ==============================================================================");
        sb.AppendLine("// Generated Bootstrap Code from Bootstrap.st");
        sb.AppendLine($"// Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("// This code creates real Smalltalk objects using only VM primitives");
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
        sb.AppendLine("public static class GeneratedBootstrap");
        sb.AppendLine("{");
        
        // Field index constants
        GenerateFieldConstants(sb);
        
        // Class references
        GenerateClassReferences(sb);
        
        // Initialize method
        GenerateInitializeMethod(sb);
        
        // Primitive wrapper methods
        GeneratePrimitiveWrappers(sb);
        
        // Method encoding helpers
        GenerateMethodEncoders(sb);
        
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
        sb.AppendLine("    private const int CLASS_CLASSPOOL = 6;");
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
        sb.AppendLine("    // Class references");
        sb.AppendLine("    private static SmalltalkObject nil;");
        sb.AppendLine("    private static SmalltalkObject trueObj;");
        sb.AppendLine("    private static SmalltalkObject falseObj;");
        
        foreach (var cls in classes)
        {
            var varName = GetClassVariableName(cls.Name);
            sb.AppendLine($"    private static SmalltalkObject {varName};");
            sb.AppendLine($"    private static SmalltalkObject {varName}Metaclass;");
        }
        
        sb.AppendLine("    private static Dictionary<string, SmalltalkObject> symbolTable = new();");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generate the Initialize method
    /// </summary>
    private void GenerateInitializeMethod(StringBuilder sb)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Initialize the bootstrap from Bootstrap.st definitions");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static void Initialize()");
        sb.AppendLine("    {");
        sb.AppendLine("        Console.WriteLine(\"[GENERATED_BOOTSTRAP] Initializing from Bootstrap.st...\");");
        sb.AppendLine();
        sb.AppendLine("        // Clear any existing state");
        sb.AppendLine("        SystemDictionary.Clear();");
        sb.AppendLine("        symbolTable.Clear();");
        sb.AppendLine();
        
        // Phase 1: Create nil, true, false
        sb.AppendLine("        // Phase 1: Create special objects");
        sb.AppendLine("        nil = Primitive71_New(null, 0);");
        sb.AppendLine("        trueObj = Primitive71_New(null, 0);");
        sb.AppendLine("        falseObj = Primitive71_New(null, 0);");
        sb.AppendLine();
        
        // Phase 2: Create class objects
        sb.AppendLine("        // Phase 2: Create class objects (8 fields each)");
        foreach (var cls in classes)
        {
            var varName = GetClassVariableName(cls.Name);
            sb.AppendLine($"        {varName} = Primitive71_New(null, 8);");
        }
        sb.AppendLine();
        
        // Phase 3: Create metaclass objects
        sb.AppendLine("        // Phase 3: Create metaclass objects (6 fields each)");
        foreach (var cls in classes)
        {
            var varName = GetClassVariableName(cls.Name);
            sb.AppendLine($"        {varName}Metaclass = Primitive71_New(null, 6);");
        }
        sb.AppendLine();
        
        // Phase 4: Set class pointers
        sb.AppendLine("        // Phase 4: Set class pointers");
        foreach (var cls in classes)
        {
            var varName = GetClassVariableName(cls.Name);
            sb.AppendLine($"        SetClassPointer({varName}, {varName}Metaclass);");
        }
        sb.AppendLine();
        
        // All metaclasses are instances of Metaclass
        sb.AppendLine("        // All metaclasses are instances of Metaclass");
        var metaclassVarName = GetClassVariableName("Metaclass");
        foreach (var cls in classes)
        {
            var varName = GetClassVariableName(cls.Name);
            sb.AppendLine($"        SetClassPointer({varName}Metaclass, {metaclassVarName});");
        }
        sb.AppendLine();
        
        // Phase 5: Set superclass hierarchy
        sb.AppendLine("        // Phase 5: Set superclass hierarchy");
        foreach (var cls in classes)
        {
            var varName = GetClassVariableName(cls.Name);
            var superVar = cls.Superclass == "nil" ? "nil" : GetClassVariableName(cls.Superclass);
            sb.AppendLine($"        Primitive61_AtPut({varName}, BEHAVIOR_SUPERCLASS + 1, {superVar});");
        }
        sb.AppendLine();
        
        // Phase 6: Set metaclass superclass hierarchy
        sb.AppendLine("        // Phase 6: Set metaclass superclass hierarchy");
        foreach (var cls in classes)
        {
            var varName = GetClassVariableName(cls.Name);
            if (cls.Name == "Object")
            {
                sb.AppendLine($"        Primitive61_AtPut({varName}Metaclass, BEHAVIOR_SUPERCLASS + 1, classClass);");
            }
            else if (cls.Superclass != "nil")
            {
                var superVar = GetClassVariableName(cls.Superclass);
                sb.AppendLine($"        Primitive61_AtPut({varName}Metaclass, BEHAVIOR_SUPERCLASS + 1, {superVar}Metaclass);");
            }
        }
        sb.AppendLine();
        
        // Phase 7: Set thisClass for metaclasses
        sb.AppendLine("        // Phase 7: Set thisClass for metaclasses");
        foreach (var cls in classes)
        {
            var varName = GetClassVariableName(cls.Name);
            sb.AppendLine($"        Primitive61_AtPut({varName}Metaclass, METACLASS_THISCLASS + 1, {varName});");
        }
        sb.AppendLine();
        
        // Phase 8: Initialize class fields
        sb.AppendLine("        // Phase 8: Initialize class fields");
        foreach (var cls in classes)
        {
            var varName = GetClassVariableName(cls.Name);
            
            // Set name
            sb.AppendLine($"        Primitive61_AtPut({varName}, CLASS_NAME + 1, InternSymbol(\"{cls.Name}\"));");
            
            // Create and set method dictionary
            sb.AppendLine($"        Primitive61_AtPut({varName}, BEHAVIOR_METHODDICT + 1, CreateEmptyDictionary());");
            sb.AppendLine($"        Primitive61_AtPut({varName}Metaclass, BEHAVIOR_METHODDICT + 1, CreateEmptyDictionary());");
            
            // Create and set classPool
            if (cls.ClassVariables.Count > 0)
            {
                sb.AppendLine($"        {{");
                sb.AppendLine($"            var classPool = CreateEmptyDictionary();");
                foreach (var cv in cls.ClassVariables)
                {
                    sb.AppendLine($"            AddToClassPool(classPool, \"{cv}\", nil);");
                }
                sb.AppendLine($"            Primitive61_AtPut({varName}, CLASS_CLASSPOOL + 1, classPool);");
                sb.AppendLine($"        }}");
            }
            else
            {
                sb.AppendLine($"        Primitive61_AtPut({varName}, CLASS_CLASSPOOL + 1, CreateEmptyDictionary());");
            }
            
            // Set instance variables
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
        }
        sb.AppendLine();
        
        // Phase 9: Install methods
        sb.AppendLine("        // Phase 9: Install methods");
        foreach (var method in methods)
        {
            GenerateMethodInstallation(sb, method);
        }
        sb.AppendLine();
        
        // Phase 10: Register with SystemDictionary
        sb.AppendLine("        // Phase 10: Register with SystemDictionary");
        foreach (var cls in classes)
        {
            var varName = GetClassVariableName(cls.Name);
            sb.AppendLine($"        SystemDictionary.RegisterBootstrapClass(\"{cls.Name}\", (SmalltalkClass){varName});");
        }
        sb.AppendLine("        // Register special objects as globals");
        sb.AppendLine("        SetGlobal(\"nil\", nil);");
        sb.AppendLine("        SetGlobal(\"true\", trueObj);");
        sb.AppendLine("        SetGlobal(\"false\", falseObj);");
        sb.AppendLine();
        
        // Phase 11: Create Smalltalk global
        sb.AppendLine("        // Phase 11: Create Smalltalk global dictionary");
        sb.AppendLine("        CreateSmalltalkGlobal();");
        sb.AppendLine();
        
        sb.AppendLine("        Console.WriteLine(\"[GENERATED_BOOTSTRAP] Bootstrap complete!\");");
        sb.AppendLine($"        Console.WriteLine(\"  Created {classes.Count} classes\");");
        sb.AppendLine($"        Console.WriteLine(\"  Installed {methods.Count} methods\");");
        sb.AppendLine("    }");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generate method installation code
    /// </summary>
    private void GenerateMethodInstallation(StringBuilder sb, MethodDefinition method)
    {
        var className = GetClassVariableName(method.ClassName);
        var targetClass = method.IsClassMethod ? $"{className}Metaclass" : className;
        
        sb.AppendLine($"        {{");
        sb.AppendLine($"            // {method.ClassName}{(method.IsClassMethod ? " class" : "")}>>{method.Selector}");
        
        // Encode the method
        var bytecodes = EncodeMethod(method);
        var literals = ExtractLiterals(method);
        
        // Generate bytecode array
        sb.Append($"            var bytecodes = new byte[] {{ ");
        foreach (var b in bytecodes)
        {
            sb.Append($"{b}, ");
        }
        sb.AppendLine("};");
        
        // Generate literals array
        if (literals.Count > 0)
        {
            sb.AppendLine($"            var literals = new object[] {{");
            foreach (var lit in literals)
            {
                if (lit is string s)
                    sb.AppendLine($"                \"{s}\",");
                else if (lit is Symbol sym)
                    sb.AppendLine($"                InternSymbol(\"{sym}\"),");
                else
                    sb.AppendLine($"                {lit},");
            }
            sb.AppendLine("            };");
        }
        else
        {
            sb.AppendLine("            var literals = new object[0];");
        }
        
        // Create method header
        var argCount = method.Arguments.Count;
        var tempCount = method.Temporaries.Count;
        var literalCount = literals.Count;
        var primitiveIndex = method.PrimitiveIndex ?? 0;
        
        sb.AppendLine($"            var header = new MethodHeader({argCount}, {tempCount}, " +
                     $"{(primitiveIndex > 0 ? "true" : "false")}, {primitiveIndex});");
        
        // Create and install method
        sb.AppendLine("            var method = new CompiledMethod");
        sb.AppendLine("            {");
        sb.AppendLine("                Header = header,");
        sb.AppendLine("                Literals = literals,");
        sb.AppendLine("                Bytecodes = bytecodes,");
        sb.AppendLine($"                TemporariesCount = {tempCount},");
        sb.AppendLine($"                HasPrimitive = {(primitiveIndex > 0 ? "true" : "false")},");
        sb.AppendLine($"                PrimitiveIndex = {primitiveIndex}");
        sb.AppendLine("            };");
        sb.AppendLine($"            InstallMethod({targetClass}, \"{method.Selector}\", method);");
        sb.AppendLine($"        }}");
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
        sb.AppendLine("        var assoc = Primitive71_New(associationClass, 2);");
        sb.AppendLine("        Primitive61_AtPut(assoc, ASSOC_KEY + 1, InternSymbol(name));");
        sb.AppendLine("        Primitive61_AtPut(assoc, ASSOC_VALUE + 1, value ?? nil);");
        sb.AppendLine("        // Simplified: add to first empty slot");
        sb.AppendLine("        var array = (SmalltalkObject)classPool.Fields[DICT_ARRAY];");
        sb.AppendLine("        for (int i = 0; i < array.Fields.Length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (array.Fields[i] == nil)");
        sb.AppendLine("            {");
        sb.AppendLine("                array.Fields[i] = assoc;");
        sb.AppendLine("                var tally = (SmallInteger)classPool.Fields[DICT_TALLY];");
        sb.AppendLine("                classPool.Fields[DICT_TALLY] = new SmallInteger(tally.Value + 1);");
        sb.AppendLine("                break;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // InstallMethod
        sb.AppendLine("    private static void InstallMethod(SmalltalkObject cls, string selector, CompiledMethod method)");
        sb.AppendLine("    {");
        sb.AppendLine("        var methodDict = cls.Fields[BEHAVIOR_METHODDICT] as SmalltalkObject;");
        sb.AppendLine("        if (methodDict != null)");
        sb.AppendLine("        {");
        sb.AppendLine("            // Simplified dictionary add");
        sb.AppendLine("            var array = methodDict.Fields[DICT_ARRAY] as SmalltalkObject;");
        sb.AppendLine("            if (array != null)");
        sb.AppendLine("            {");
        var assocVarName2 = GetClassVariableName("Association");
        sb.AppendLine($"                var assoc = Primitive71_New({assocVarName2}, 2);");
        sb.AppendLine("                Primitive61_AtPut(assoc, ASSOC_KEY + 1, InternSymbol(selector));");
        sb.AppendLine("                Primitive61_AtPut(assoc, ASSOC_VALUE + 1, method);");
        sb.AppendLine("                for (int i = 0; i < array.Fields.Length; i++)");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (array.Fields[i] == nil)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        array.Fields[i] = assoc;");
        sb.AppendLine("                        var tally = (SmallInteger)methodDict.Fields[DICT_TALLY];");
        sb.AppendLine("                        methodDict.Fields[DICT_TALLY] = new SmallInteger(tally.Value + 1);");
        sb.AppendLine("                        break;");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // CreateSmalltalkGlobal
        sb.AppendLine("    private static void CreateSmalltalkGlobal()");
        sb.AppendLine("    {");
        sb.AppendLine("        // Create SystemDictionary class if not in bootstrap");
        sb.AppendLine("        var systemDictClass = Primitive71_New(null, 8);");
        sb.AppendLine("        Primitive61_AtPut(systemDictClass, BEHAVIOR_SUPERCLASS + 1, dictionaryClass);");
        sb.AppendLine("        Primitive61_AtPut(systemDictClass, CLASS_NAME + 1, InternSymbol(\"SystemDictionary\"));");
        sb.AppendLine("        ");
        sb.AppendLine("        var smalltalk = Primitive71_New(systemDictClass, 2);");
        sb.AppendLine("        Primitive61_AtPut(smalltalk, DICT_TALLY + 1, new SmallInteger(0));");
        sb.AppendLine("        var array = Primitive71_New(arrayClass, 101);");
        sb.AppendLine("        for (int i = 1; i <= 101; i++)");
        sb.AppendLine("            Primitive61_AtPut(array, i, nil);");
        sb.AppendLine("        Primitive61_AtPut(smalltalk, DICT_ARRAY + 1, array);");
        sb.AppendLine("        ");
        sb.AppendLine("        // Add all classes to Smalltalk");
        foreach (var cls in classes)
        {
            var varName = GetClassVariableName(cls.Name);
            sb.AppendLine($"        AddToDictionary(smalltalk, \"{cls.Name}\", {varName});");
        }
        sb.AppendLine("        AddToDictionary(smalltalk, \"nil\", nil);");
        sb.AppendLine("        AddToDictionary(smalltalk, \"true\", trueObj);");
        sb.AppendLine("        AddToDictionary(smalltalk, \"false\", falseObj);");
        sb.AppendLine("        AddToDictionary(smalltalk, \"Smalltalk\", smalltalk);");
        sb.AppendLine("        ");
        sb.AppendLine("        SetGlobal(\"Smalltalk\", smalltalk);");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Add SetGlobal helper
        sb.AppendLine("    private static void SetGlobal(string name, SmalltalkObject value)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Register in SystemDictionary if method exists");
        sb.AppendLine("        var setGlobalMethod = typeof(SystemDictionary).GetMethod(\"SetGlobal\", ");
        sb.AppendLine("            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);");
        sb.AppendLine("        if (setGlobalMethod != null)");
        sb.AppendLine("        {");
        sb.AppendLine("            setGlobalMethod.Invoke(null, new object[] { name, value });");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // AddToDictionary helper
        sb.AppendLine("    private static void AddToDictionary(SmalltalkObject dict, string key, SmalltalkObject value)");
        sb.AppendLine("    {");
        sb.AppendLine("        var array = dict.Fields[DICT_ARRAY] as SmalltalkObject;");
        sb.AppendLine("        if (array != null)");
        sb.AppendLine("        {");
        sb.AppendLine("            var assoc = Primitive71_New(associationClass, 2);");
        sb.AppendLine("            Primitive61_AtPut(assoc, ASSOC_KEY + 1, InternSymbol(key));");
        sb.AppendLine("            Primitive61_AtPut(assoc, ASSOC_VALUE + 1, value);");
        sb.AppendLine("            for (int i = 0; i < array.Fields.Length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (array.Fields[i] == nil)");
        sb.AppendLine("                {");
        sb.AppendLine("                    array.Fields[i] = assoc;");
        sb.AppendLine("                    var tally = (SmallInteger)dict.Fields[DICT_TALLY];");
        sb.AppendLine("                    dict.Fields[DICT_TALLY] = new SmallInteger(tally.Value + 1);");
        sb.AppendLine("                    break;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }
    
    /// <summary>
    /// Generate method encoding helpers
    /// </summary>
    private void GenerateMethodEncoders(StringBuilder sb)
    {
        // This section would contain specific method encoders if needed
        // For now, the encoding is done inline in GenerateMethodInstallation
    }
    
    /// <summary>
    /// Get C# variable name for a class
    /// </summary>
    private string GetClassVariableName(string className)
    {
        return char.ToLower(className[0]) + className.Substring(1) + "Class";
    }
    
    /// <summary>
    /// Encode a method to bytecodes (simplified)
    /// </summary>
    private byte[] EncodeMethod(MethodDefinition method)
    {
        var bytecodes = new List<byte>();
        
        // If method has a primitive, it usually just returns self on failure
        if (method.PrimitiveIndex.HasValue)
        {
            bytecodes.Add(120); // Return self (fallback for primitive failure)
            return bytecodes.ToArray();
        }
        
        // Simple encoding for basic patterns
        if (method.Statements.Count == 1)
        {
            var stmt = method.Statements[0];
            if (stmt is AnswerStatement ret)
            {
                // Check for common return patterns
                if (ret.Expression is VariableExpression varExpr)
                {
                    switch (varExpr.Name)
                    {
                        case "self":
                            bytecodes.Add(120); // Return self
                            break;
                        case "true":
                            bytecodes.Add(121); // Return true
                            break;
                        case "false":
                            bytecodes.Add(122); // Return false
                            break;
                        case "nil":
                            bytecodes.Add(123); // Return nil
                            break;
                        default:
                            // Push variable and return
                            bytecodes.Add(112); // Push self (placeholder)
                            bytecodes.Add(124); // Return top
                            break;
                    }
                }
                else
                {
                    // Generic: evaluate expression and return
                    bytecodes.Add(112); // Push self (placeholder)
                    bytecodes.Add(124); // Return top
                }
            }
            else if (stmt is ExpressionStatement)
            {
                // Expression statement that doesn't return
                bytecodes.Add(112); // Push self
                bytecodes.Add(135); // Pop
                bytecodes.Add(120); // Return self
            }
        }
        else if (method.Statements.Count == 0)
        {
            // Empty method returns self
            bytecodes.Add(120); // Return self
        }
        else
        {
            // Complex method - simplified encoding
            bytecodes.Add(112); // Push self
            bytecodes.Add(124); // Return top
        }
        
        return bytecodes.ToArray();
    }
    
    /// <summary>
    /// Extract literals from a method (simplified)
    /// </summary>
    private List<object> ExtractLiterals(MethodDefinition method)
    {
        var literals = new List<object>();
        
        // Extract symbols from message sends
        foreach (var stmt in method.Statements)
        {
            ExtractLiteralsFromStatement(stmt, literals);
        }
        
        return literals;
    }
    
    private void ExtractLiteralsFromStatement(StatementNode stmt, List<object> literals)
    {
        // Simplified literal extraction
        // In a full implementation, this would walk the AST
        if (stmt is ExpressionStatement exprStmt)
        {
            ExtractLiteralsFromExpression(exprStmt.Expression, literals);
        }
        else if (stmt is AnswerStatement answerStmt)
        {
            ExtractLiteralsFromExpression(answerStmt.Expression, literals);
        }
    }
    
    private void ExtractLiteralsFromExpression(ExpressionNode expr, List<object> literals)
    {
        switch (expr)
        {
            case LiteralExpression lit:
                if (!literals.Contains(lit.Value))
                    literals.Add(lit.Value);
                break;
            case UnarySendExpression unary:
                if (!literals.Any(l => l is Symbol s && s.Name == unary.Selector))
                    literals.Add(new Symbol(unary.Selector));
                ExtractLiteralsFromExpression(unary.Receiver, literals);
                break;
            case BinarySendExpression binary:
                if (!literals.Any(l => l is Symbol s && s.Name == binary.Selector))
                    literals.Add(new Symbol(binary.Selector));
                ExtractLiteralsFromExpression(binary.Receiver, literals);
                ExtractLiteralsFromExpression(binary.Argument, literals);
                break;
            case KeywordSendExpression keyword:
                var selector = string.Join("", keyword.SelectorParts);
                if (!literals.Any(l => l is Symbol s && s.Name == selector))
                    literals.Add(new Symbol(selector));
                ExtractLiteralsFromExpression(keyword.Receiver, literals);
                foreach (var arg in keyword.Arguments)
                    ExtractLiteralsFromExpression(arg, literals);
                break;
        }
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