using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Smalltalk80.Parser;

namespace Smalltalk80;

/// <summary>
/// Generates C# bootstrap code from Bootstrap.st at build time.
/// This tool parses the minimal Smalltalk bootstrap definitions and generates
/// efficient C# code that creates these classes directly in memory.
/// </summary>
public class BootstrapGenerator
{
    private readonly Dictionary<string, ClassInfo> classes = new();
    private readonly List<string> classOrder = new();
    
    private class ClassInfo
    {
        public string Name { get; set; } = "";
        public string? Superclass { get; set; }
        public List<string> InstanceVariables { get; set; } = new();
        public List<string> ClassVariables { get; set; } = new();
        public bool IsVariableClass { get; set; }
        public bool IsByteClass { get; set; }
        public string Category { get; set; } = "";
        public Dictionary<string, MethodInfo> InstanceMethods { get; set; } = new();
        public Dictionary<string, MethodInfo> ClassMethods { get; set; } = new();
    }
    
    private class MethodInfo
    {
        public string Selector { get; set; } = "";
        public List<string> Arguments { get; set; } = new();
        public string Source { get; set; } = "";
        public CompiledMethod? Compiled { get; set; }
        public bool HasPrimitive { get; set; }
        public int PrimitiveIndex { get; set; }
    }
    
    /// <summary>
    /// Entry point for running as a standalone tool.
    /// </summary>
    public static void RunAsToolTool(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: BootstrapGenerator <input.st> <output.cs>");
            Environment.Exit(1);
        }
        
        var generator = new BootstrapGenerator();
        generator.Generate(args[0], args[1]);
    }
    
    /// <summary>
    /// Generates C# bootstrap code from a Smalltalk bootstrap file.
    /// </summary>
    public void Generate(string inputPath, string outputPath)
    {
        Console.WriteLine($"[BOOTSTRAP] Parsing {inputPath}...");
        
        // Parse the bootstrap file
        var fileAst = FileInReader.Parse(inputPath);
        
        // Process all chunks to extract class and method definitions
        ProcessChunks(fileAst);
        
        // Generate C# code
        var code = GenerateCSharpCode();
        
        // Write to output file
        File.WriteAllText(outputPath, code);
        Console.WriteLine($"[BOOTSTRAP] Generated {outputPath} with {classes.Count} classes");
    }
    
    /// <summary>
    /// Processes all chunks in the parsed file to extract definitions.
    /// </summary>
    private void ProcessChunks(FileNode fileAst)
    {
        MethodHeaderNode? currentHeader = null;
        
        foreach (var chunk in fileAst.Chunks)
        {
            switch (chunk)
            {
                case ClassDefinitionNode classDef:
                    ProcessClassDefinition(classDef);
                    break;
                    
                case MethodHeaderNode header:
                    currentHeader = header;
                    break;
                    
                case MethodCategoryNode category:
                    ProcessMethodCategory(category);
                    break;
                    
                case MethodDefinitionNode method when currentHeader != null:
                    ProcessMethod(currentHeader, method);
                    break;
            }
        }
    }
    
    /// <summary>
    /// Processes a class definition chunk.
    /// </summary>
    private void ProcessClassDefinition(ClassDefinitionNode classDef)
    {
        var info = new ClassInfo
        {
            Name = classDef.ClassName,
            Superclass = string.IsNullOrEmpty(classDef.Superclass) || classDef.Superclass == "nil" 
                ? null : classDef.Superclass,
            InstanceVariables = classDef.InstanceVariables.ToList(),
            ClassVariables = classDef.ClassVariables.ToList(),
            Category = classDef.Category ?? "Kernel-Objects"
        };
        
        // Check if it's a variable class based on the definition type
        // This would need to be enhanced based on the actual syntax used
        info.IsVariableClass = classDef.GetType().Name.Contains("Variable");
        
        classes[info.Name] = info;
        classOrder.Add(info.Name);
        Console.WriteLine($"[BOOTSTRAP] Found class: {info.Name}");
    }
    
    /// <summary>
    /// Processes a method category with multiple methods.
    /// </summary>
    private void ProcessMethodCategory(MethodCategoryNode category)
    {
        foreach (var method in category.Methods)
        {
            ProcessMethod(category.Header, method);
        }
    }
    
    /// <summary>
    /// Processes a single method definition.
    /// </summary>
    private void ProcessMethod(MethodHeaderNode header, MethodDefinitionNode method)
    {
        if (!classes.TryGetValue(header.ClassName, out var classInfo))
        {
            Console.WriteLine($"[BOOTSTRAP] Warning: Method for unknown class {header.ClassName}");
            return;
        }
        
        var methodInfo = new MethodInfo
        {
            Selector = method.Selector,
            Arguments = method.Arguments.ToList(),
            Source = method.Source,
            HasPrimitive = method.HasPrimitive,
            PrimitiveIndex = method.PrimitiveIndex
        };
        
        // Compile the method to bytecode
        try
        {
            var variables = header.IsClassSide 
                ? classInfo.ClassVariables 
                : CollectAllInstanceVariables(classInfo);
                
            methodInfo.Compiled = AstMethodCompiler.CompileMethod(
                method.Body,
                method.Arguments,
                variables,
                method.HasPrimitive,
                method.PrimitiveIndex);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BOOTSTRAP] Warning: Failed to compile {header.ClassName}>>{method.Selector}: {ex.Message}");
            // Create a stub method that just returns self
            methodInfo.Compiled = CreateStubMethod();
        }
        
        var targetDict = header.IsClassSide ? classInfo.ClassMethods : classInfo.InstanceMethods;
        targetDict[methodInfo.Selector] = methodInfo;
    }
    
    /// <summary>
    /// Collects all instance variables including inherited ones.
    /// </summary>
    private List<string> CollectAllInstanceVariables(ClassInfo classInfo)
    {
        var variables = new List<string>();
        var current = classInfo;
        
        while (current != null)
        {
            variables.AddRange(current.InstanceVariables);
            current = current.Superclass != null && classes.TryGetValue(current.Superclass, out var super) 
                ? super : null;
        }
        
        return variables;
    }
    
    /// <summary>
    /// Creates a stub compiled method that just returns self.
    /// </summary>
    private CompiledMethod CreateStubMethod()
    {
        return new CompiledMethod
        {
            Bytecodes = new byte[] { 120 }, // returnSelf
            Literals = new object[0],
            TemporariesCount = 0
        };
    }
    
    /// <summary>
    /// Generates the C# bootstrap code.
    /// </summary>
    private string GenerateCSharpCode()
    {
        var sb = new StringBuilder();
        
        // File header
        sb.AppendLine("// ==============================================================================");
        sb.AppendLine("// Generated Bootstrap Code - DO NOT EDIT");
        sb.AppendLine($"// Generated from Bootstrap.st on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("// ==============================================================================");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine("namespace Smalltalk80;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Auto-generated bootstrap code that creates the minimal set of classes");
        sb.AppendLine("/// required to execute any Smalltalk code. This code is generated from");
        sb.AppendLine("/// Bootstrap.st at build time and compiled into the interpreter.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class GeneratedBootstrap");
        sb.AppendLine("{");
        
        // Generate the Initialize method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Initializes the bootstrap classes in memory.");
        sb.AppendLine("    /// This method should be called once at interpreter startup.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static void Initialize()");
        sb.AppendLine("    {");
        sb.AppendLine("        Console.WriteLine(\"[BOOTSTRAP] Initializing generated bootstrap...\");");
        sb.AppendLine("        ");
        sb.AppendLine("        // Clear any existing classes");
        sb.AppendLine("        SystemDictionary.Clear();");
        sb.AppendLine("        ");
        sb.AppendLine("        // Create all class objects");
        sb.AppendLine("        var classes = new Dictionary<string, SmalltalkClass>();");
        sb.AppendLine("        ");
        
        // Generate class creation code
        foreach (var className in classOrder)
        {
            var classInfo = classes[className];
            GenerateClassCreation(sb, classInfo);
        }
        
        // Generate superclass relationships
        sb.AppendLine("        ");
        sb.AppendLine("        // Set up superclass relationships");
        foreach (var className in classOrder)
        {
            var classInfo = classes[className];
            if (classInfo.Superclass != null)
            {
                sb.AppendLine($"        classes[\"{classInfo.Name}\"].Superclass = classes[\"{classInfo.Superclass}\"];");
            }
        }
        
        // Generate metaclass relationships
        sb.AppendLine("        ");
        sb.AppendLine("        // Set up metaclass relationships");
        sb.AppendLine("        SetupMetaclassHierarchy(classes);");
        
        // Generate method installations
        sb.AppendLine("        ");
        sb.AppendLine("        // Install methods");
        foreach (var className in classOrder)
        {
            var classInfo = classes[className];
            if (classInfo.InstanceMethods.Count > 0 || classInfo.ClassMethods.Count > 0)
            {
                GenerateMethodInstallation(sb, classInfo);
            }
        }
        
        // Initialize globals
        sb.AppendLine("        ");
        sb.AppendLine("        // Initialize global variables");
        sb.AppendLine("        InitializeGlobals(classes);");
        sb.AppendLine("        ");
        sb.AppendLine($"        Console.WriteLine(\"[BOOTSTRAP] Initialized {classes.Count} bootstrap classes\");");
        sb.AppendLine("    }");
        
        // Generate helper methods
        GenerateHelperMethods(sb);
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generates code to create a single class.
    /// </summary>
    private void GenerateClassCreation(StringBuilder sb, ClassInfo classInfo)
    {
        sb.AppendLine($"        // Create {classInfo.Name}");
        sb.AppendLine($"        classes[\"{classInfo.Name}\"] = SystemDictionary.DefineClass(");
        sb.AppendLine($"            \"{classInfo.Name}\",");
        sb.AppendLine($"            null, // Superclass set later");
        
        // Instance variables
        if (classInfo.InstanceVariables.Count > 0)
        {
            var vars = string.Join(", ", classInfo.InstanceVariables.Select(v => $"\"{v}\""));
            sb.AppendLine($"            new List<string> {{ {vars} }},");
        }
        else
        {
            sb.AppendLine($"            new List<string>(),");
        }
        
        // Class variables
        if (classInfo.ClassVariables.Count > 0)
        {
            var vars = string.Join(", ", classInfo.ClassVariables.Select(v => $"\"{v}\""));
            sb.AppendLine($"            new List<string> {{ {vars} }},");
        }
        else
        {
            sb.AppendLine($"            new List<string>(),");
        }
        
        // Pool dictionaries and category
        sb.AppendLine($"            new List<string>(),");
        sb.AppendLine($"            \"{classInfo.Category}\");");
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generates code to install methods for a class.
    /// </summary>
    private void GenerateMethodInstallation(StringBuilder sb, ClassInfo classInfo)
    {
        sb.AppendLine($"        // Methods for {classInfo.Name}");
        
        // Instance methods
        foreach (var (selector, method) in classInfo.InstanceMethods)
        {
            GenerateMethodCode(sb, classInfo.Name, selector, method, false);
        }
        
        // Class methods
        foreach (var (selector, method) in classInfo.ClassMethods)
        {
            GenerateMethodCode(sb, classInfo.Name, selector, method, true);
        }
        
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generates code to install a single method.
    /// </summary>
    private void GenerateMethodCode(StringBuilder sb, string className, string selector, MethodInfo method, bool isClassSide)
    {
        if (method.Compiled == null) return;
        
        var target = isClassSide ? $"classes[\"{className}\"].Metaclass" : $"classes[\"{className}\"]";
        
        // Generate bytecode array
        var bytecodes = string.Join(", ", method.Compiled.Bytecodes.Select(b => $"0x{b:X2}"));
        
        sb.AppendLine($"        InstallMethod({target}, \"{selector}\",");
        sb.AppendLine($"            new byte[] {{ {bytecodes} }},");
        
        // Generate literals array
        if (method.Compiled.Literals.Length > 0)
        {
            sb.AppendLine($"            new object[] {{");
            foreach (var literal in method.Compiled.Literals)
            {
                sb.AppendLine($"                {GenerateLiteralCode(literal)},");
            }
            sb.AppendLine($"            }},");
        }
        else
        {
            sb.AppendLine($"            new object[0],");
        }
        
        sb.AppendLine($"            {method.Compiled.TemporariesCount},");
        sb.AppendLine($"            {(method.HasPrimitive ? "true" : "false")},");
        sb.AppendLine($"            {method.PrimitiveIndex});");
    }
    
    /// <summary>
    /// Generates code to represent a literal value.
    /// </summary>
    private string GenerateLiteralCode(object literal)
    {
        return literal switch
        {
            null => "null",
            string s => $"\"{s.Replace("\"", "\\\"")}\"",
            Symbol sym => $"new Symbol(\"{sym.Name}\")",
            int i => i.ToString(),
            bool b => b ? "true" : "false",
            _ => $"/* TODO: {literal.GetType().Name} */"
        };
    }
    
    /// <summary>
    /// Generates helper methods for the bootstrap.
    /// </summary>
    private void GenerateHelperMethods(StringBuilder sb)
    {
        sb.AppendLine("    ");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Sets up the metaclass hierarchy.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    private static void SetupMetaclassHierarchy(Dictionary<string, SmalltalkClass> classes)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Create metaclasses for each class");
        sb.AppendLine("        foreach (var kvp in classes)");
        sb.AppendLine("        {");
        sb.AppendLine("            var cls = kvp.Value;");
        sb.AppendLine("            var metaclass = new SmalltalkClass(cls.Name + \" class\", null);");
        sb.AppendLine("            metaclass.ThisClass = cls;");
        sb.AppendLine("            cls.Metaclass = metaclass;");
        sb.AppendLine("            cls.ClassPointer = metaclass;");
        sb.AppendLine("        }");
        sb.AppendLine("        ");
        sb.AppendLine("        // Set up metaclass superclass relationships");
        sb.AppendLine("        foreach (var kvp in classes)");
        sb.AppendLine("        {");
        sb.AppendLine("            var cls = kvp.Value;");
        sb.AppendLine("            if (cls.Superclass != null && cls.Superclass.Metaclass != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                cls.Metaclass.Superclass = cls.Superclass.Metaclass;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("    ");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Installs a compiled method in a class.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    private static void InstallMethod(SmalltalkClass cls, string selector,");
        sb.AppendLine("        byte[] bytecodes, object[] literals, int tempCount,");
        sb.AppendLine("        bool hasPrimitive, int primitiveIndex)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (cls == null) return;");
        sb.AppendLine("        ");
        sb.AppendLine("        var method = new CompiledMethod");
        sb.AppendLine("        {");
        sb.AppendLine("            Bytecodes = bytecodes,");
        sb.AppendLine("            Literals = literals,");
        sb.AppendLine("            TemporariesCount = tempCount,");
        sb.AppendLine("            HasPrimitive = hasPrimitive,");
        sb.AppendLine("            PrimitiveIndex = primitiveIndex");
        sb.AppendLine("        };");
        sb.AppendLine("        ");
        sb.AppendLine("        cls.MethodDictionary[new Symbol(selector)] = method;");
        sb.AppendLine("    }");
        sb.AppendLine("    ");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Initializes global variables.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    private static void InitializeGlobals(Dictionary<string, SmalltalkClass> classes)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Get or create Smalltalk system dictionary");
        sb.AppendLine("        var smalltalk = SystemDictionary.GetSmalltalk();");
        sb.AppendLine("        ");
        sb.AppendLine("        // Create nil, true, false");
        sb.AppendLine("        var nil = new SmalltalkObject(classes[\"UndefinedObject\"], 0);");
        sb.AppendLine("        var trueObj = new SmalltalkObject(classes[\"True\"], 0);");
        sb.AppendLine("        var falseObj = new SmalltalkObject(classes[\"False\"], 0);");
        sb.AppendLine("        ");
        sb.AppendLine("        SystemDictionary.SetGlobal(\"nil\", nil);");
        sb.AppendLine("        SystemDictionary.SetGlobal(\"true\", trueObj);");
        sb.AppendLine("        SystemDictionary.SetGlobal(\"false\", falseObj);");
        sb.AppendLine("        ");
        sb.AppendLine("        // Create other essential globals");
        sb.AppendLine("        if (classes.TryGetValue(\"SystemOrganizer\", out var orgClass))");
        sb.AppendLine("        {");
        sb.AppendLine("            SystemDictionary.SetGlobal(\"SystemOrganization\", new SmalltalkObject(orgClass, 0));");
        sb.AppendLine("        }");
        sb.AppendLine("        ");
        sb.AppendLine("        if (classes.TryGetValue(\"Dictionary\", out var dictClass))");
        sb.AppendLine("        {");
        sb.AppendLine("            SystemDictionary.SetGlobal(\"Undeclared\", new SmalltalkObject(dictClass, 0));");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }
}