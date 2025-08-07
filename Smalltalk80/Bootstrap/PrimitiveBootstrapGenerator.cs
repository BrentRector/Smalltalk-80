using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Smalltalk80.Parser;

namespace Smalltalk80.Bootstrap;

/// <summary>
/// Generates C# bootstrap code from Bootstrap.st using only VM primitives.
/// Follows the exact sequence from REVISED_BOOTSTRAP_PROCEDURE.md.
/// </summary>
public class PrimitiveBootstrapGenerator
{
    private readonly string _bootstrapPath;
    private readonly Dictionary<string, ClassDefinition> _classes = new();
    private readonly Dictionary<string, List<MethodDefinition>> _methods = new();
    private readonly Dictionary<string, List<MethodDefinition>> _classMethods = new();
    
    /// <summary>
    /// Represents a parsed class definition from Bootstrap.st
    /// </summary>
    private class ClassDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string SuperclassName { get; set; } = string.Empty;
        public List<string> InstanceVariables { get; set; } = new();
        public List<string> ClassVariables { get; set; } = new();
        public string Category { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Represents a parsed method definition
    /// </summary>
    private class MethodDefinition
    {
        public string Selector { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int? PrimitiveNumber { get; set; }
    }
    
    public PrimitiveBootstrapGenerator(string bootstrapPath)
    {
        _bootstrapPath = bootstrapPath;
    }
    
    /// <summary>
    /// Generate the complete bootstrap C# code
    /// </summary>
    public string Generate()
    {
        // Parse Bootstrap.st
        ParseBootstrapFile();
        
        var sb = new StringBuilder();
        
        // Generate file header
        GenerateHeader(sb);
        
        // Generate the main bootstrap class
        sb.AppendLine("namespace Smalltalk80.Bootstrap;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Primitive-based bootstrap following REVISED_BOOTSTRAP_PROCEDURE.md");
        sb.AppendLine("/// Creates all classes using only VM primitives, no C# shadow structures.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class PrimitiveBootstrap");
        sb.AppendLine("{");
        
        // Generate field declarations
        GenerateFieldDeclarations(sb);
        
        // Generate the main Initialize method
        GenerateInitializeMethod(sb);
        
        // Generate helper methods
        GenerateHelperMethods(sb);
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private void ParseBootstrapFile()
    {
        var content = File.ReadAllText(_bootstrapPath);
        var fileNode = ParserService.ParseFileIn(content);
        
        // Process each chunk in the file
        foreach (var chunk in fileNode.Chunks)
        {
            switch (chunk)
            {
                case ClassDefinitionNode classNode:
                    ProcessClassDefinition(classNode);
                    break;
                    
                case MethodCategoryNode methodCategory:
                    ProcessMethodCategory(methodCategory);
                    break;
            }
        }
    }
    
    private void ProcessClassDefinition(ClassDefinitionNode node)
    {
        var classDef = new ClassDefinition
        {
            Name = node.ClassName,
            SuperclassName = node.Superclass ?? "nil",
            InstanceVariables = node.InstanceVariables.ToList(),
            ClassVariables = node.ClassVariables.ToList(),
            Category = node.Category ?? string.Empty
        };
        
        _classes[classDef.Name] = classDef;
        
        // Initialize method lists
        if (!_methods.ContainsKey(classDef.Name))
            _methods[classDef.Name] = new List<MethodDefinition>();
        if (!_classMethods.ContainsKey(classDef.Name))
            _classMethods[classDef.Name] = new List<MethodDefinition>();
    }
    
    private void ProcessMethodCategory(MethodCategoryNode category)
    {
        var className = category.Header.ClassName;
        var isClassMethod = category.Header.IsClassSide;
        
        foreach (var method in category.Methods)
        {
            var methodDef = new MethodDefinition
            {
                Selector = method.Selector,
                Source = method.Source,
                Category = category.Header.Category,
                PrimitiveNumber = method.HasPrimitive ? method.PrimitiveIndex : null
            };
            
            var targetList = isClassMethod ? _classMethods : _methods;
            if (!targetList.ContainsKey(className))
                targetList[className] = new List<MethodDefinition>();
            
            targetList[className].Add(methodDef);
        }
    }
    
    
    private void GenerateHeader(StringBuilder sb)
    {
        sb.AppendLine("// Generated by PrimitiveBootstrapGenerator");
        sb.AppendLine("// This file is auto-generated from Bootstrap.st");
        sb.AppendLine("// Following the procedure in REVISED_BOOTSTRAP_PROCEDURE.md");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
    }
    
    private void GenerateFieldDeclarations(StringBuilder sb)
    {
        sb.AppendLine("    // Core class references");
        sb.AppendLine("    private static SmalltalkClass metaclass;");
        sb.AppendLine("    private static SmalltalkClass classClass;");
        sb.AppendLine("    private static SmalltalkClass behavior;");
        sb.AppendLine("    private static SmalltalkClass classDescription;");
        sb.AppendLine("    private static SmalltalkClass protoObject;");
        sb.AppendLine("    private static SmalltalkClass protoObjectClass;");
        sb.AppendLine("    private static SmalltalkClass objectClass;");
        sb.AppendLine("    private static SmalltalkClass object;");
        sb.AppendLine();
        sb.AppendLine("    // Singleton instances");
        sb.AppendLine("    private static SmalltalkObject nil;");
        sb.AppendLine("    private static SmalltalkObject trueObj;");
        sb.AppendLine("    private static SmalltalkObject falseObj;");
        sb.AppendLine();
        sb.AppendLine("    // Global dictionary");
        sb.AppendLine("    private static SmalltalkObject smalltalk;");
        sb.AppendLine();
        sb.AppendLine("    // Slot indices based on VM layout");
        sb.AppendLine("    private const int SUPERCLASS = 0;");
        sb.AppendLine("    private const int METHODDICT = 1;");
        sb.AppendLine("    private const int FORMAT = 2;");
        sb.AppendLine("    private const int INSTANCEVARIABLES = 3;");
        sb.AppendLine("    private const int ORGANIZATION = 4;");
        sb.AppendLine("    private const int NAME = 5;");
        sb.AppendLine("    private const int CLASSPOOL = 6;");
        sb.AppendLine("    private const int SHAREDPOOLS = 7;");
        sb.AppendLine("    private const int THISCLASS = 5; // For Metaclass");
        sb.AppendLine();
    }
    
    private void GenerateInitializeMethod(StringBuilder sb)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Initialize the bootstrap following exact procedure from REVISED_BOOTSTRAP_PROCEDURE.md");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static void Initialize(VirtualMachine vm)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Step 1: Create Metaclass (self-referential)");
        sb.AppendLine("        metaclass = PrimitiveNewClass();");
        sb.AppendLine("        PrimitiveSetClass(metaclass, metaclass); // SELF-REFERENTIAL!");
        sb.AppendLine("        PrimitiveSetSlot(metaclass, NAME, CreateSymbol(\"Metaclass\"));");
        sb.AppendLine();
        sb.AppendLine("        // Step 2: Create Class (instance of Metaclass)");
        sb.AppendLine("        classClass = PrimitiveNewClass();");
        sb.AppendLine("        PrimitiveSetClass(classClass, metaclass);");
        sb.AppendLine("        PrimitiveSetSlot(classClass, NAME, CreateSymbol(\"Class\"));");
        sb.AppendLine();
        sb.AppendLine("        // Create Class's metaclass");
        sb.AppendLine("        var classMetaclass = PrimitiveNewMetaclass();");
        sb.AppendLine("        PrimitiveSetClass(classMetaclass, metaclass);");
        sb.AppendLine("        PrimitiveSetSlot(classMetaclass, THISCLASS, classClass);");
        sb.AppendLine("        PrimitiveSetClass(classClass, classMetaclass);");
        sb.AppendLine();
        sb.AppendLine("        // Step 3: Create Behavior (instance of Class)");
        sb.AppendLine("        behavior = PrimitiveNewClass();");
        sb.AppendLine("        PrimitiveSetClass(behavior, classClass);");
        sb.AppendLine("        PrimitiveSetSlot(behavior, NAME, CreateSymbol(\"Behavior\"));");
        sb.AppendLine();
        sb.AppendLine("        // Create Behavior's metaclass");
        sb.AppendLine("        var behaviorMetaclass = PrimitiveNewMetaclass();");
        sb.AppendLine("        PrimitiveSetClass(behaviorMetaclass, metaclass);");
        sb.AppendLine("        PrimitiveSetSlot(behaviorMetaclass, THISCLASS, behavior);");
        sb.AppendLine("        PrimitiveSetClass(behavior, behaviorMetaclass);");
        sb.AppendLine();
        sb.AppendLine("        // Step 4: Create ProtoObject (the root)");
        sb.AppendLine("        protoObject = PrimitiveNewClass();");
        sb.AppendLine("        PrimitiveSetClass(protoObject, classClass);");
        sb.AppendLine("        PrimitiveSetSlot(protoObject, NAME, CreateSymbol(\"ProtoObject\"));");
        sb.AppendLine("        PrimitiveSetSlot(protoObject, SUPERCLASS, nil); // ROOT CLASS!");
        sb.AppendLine();
        sb.AppendLine("        // Create ProtoObject's metaclass");
        sb.AppendLine("        protoObjectClass = PrimitiveNewMetaclass();");
        sb.AppendLine("        PrimitiveSetClass(protoObjectClass, metaclass);");
        sb.AppendLine("        PrimitiveSetSlot(protoObjectClass, THISCLASS, protoObject);");
        sb.AppendLine("        PrimitiveSetSlot(protoObjectClass, SUPERCLASS, classClass); // CRITICAL LOOP!");
        sb.AppendLine("        PrimitiveSetClass(protoObject, protoObjectClass);");
        sb.AppendLine();
        sb.AppendLine("        // Step 5: Fix superclass relationships");
        sb.AppendLine("        PrimitiveSetSlot(behavior, SUPERCLASS, protoObject);");
        sb.AppendLine("        PrimitiveSetSlot(behaviorMetaclass, SUPERCLASS, protoObjectClass);");
        sb.AppendLine();
        sb.AppendLine("        // Step 6: Create ClassDescription");
        sb.AppendLine("        classDescription = PrimitiveNewClass();");
        sb.AppendLine("        PrimitiveSetClass(classDescription, classClass);");
        sb.AppendLine("        PrimitiveSetSlot(classDescription, NAME, CreateSymbol(\"ClassDescription\"));");
        sb.AppendLine("        PrimitiveSetSlot(classDescription, SUPERCLASS, behavior);");
        sb.AppendLine();
        sb.AppendLine("        // Create ClassDescription's metaclass");
        sb.AppendLine("        var classDescriptionMetaclass = PrimitiveNewMetaclass();");
        sb.AppendLine("        PrimitiveSetClass(classDescriptionMetaclass, metaclass);");
        sb.AppendLine("        PrimitiveSetSlot(classDescriptionMetaclass, THISCLASS, classDescription);");
        sb.AppendLine("        PrimitiveSetSlot(classDescriptionMetaclass, SUPERCLASS, behaviorMetaclass);");
        sb.AppendLine("        PrimitiveSetClass(classDescription, classDescriptionMetaclass);");
        sb.AppendLine();
        sb.AppendLine("        // Fix Class and Metaclass to inherit from ClassDescription");
        sb.AppendLine("        PrimitiveSetSlot(classClass, SUPERCLASS, classDescription);");
        sb.AppendLine("        PrimitiveSetSlot(classMetaclass, SUPERCLASS, classDescriptionMetaclass);");
        sb.AppendLine("        PrimitiveSetSlot(metaclass, SUPERCLASS, classDescription);");
        sb.AppendLine();
        sb.AppendLine("        // Step 7: Create Object (common protocol)");
        sb.AppendLine("        object = PrimitiveNewClass();");
        sb.AppendLine("        PrimitiveSetClass(object, classClass);");
        sb.AppendLine("        PrimitiveSetSlot(object, NAME, CreateSymbol(\"Object\"));");
        sb.AppendLine("        PrimitiveSetSlot(object, SUPERCLASS, protoObject);");
        sb.AppendLine();
        sb.AppendLine("        // Create Object's metaclass");
        sb.AppendLine("        objectClass = PrimitiveNewMetaclass();");
        sb.AppendLine("        PrimitiveSetClass(objectClass, metaclass);");
        sb.AppendLine("        PrimitiveSetSlot(objectClass, THISCLASS, object);");
        sb.AppendLine("        PrimitiveSetSlot(objectClass, SUPERCLASS, protoObjectClass);");
        sb.AppendLine("        PrimitiveSetClass(object, objectClass);");
        sb.AppendLine();
        sb.AppendLine("        // Step 8: Create other essential classes");
        sb.AppendLine("        CreateEssentialClasses(vm);");
        sb.AppendLine();
        sb.AppendLine("        // Step 9: Install core methods");
        sb.AppendLine("        InstallCoreMethods(vm);");
        sb.AppendLine();
        sb.AppendLine("        // Step 10: Create singleton instances");
        sb.AppendLine("        CreateSingletons(vm);");
        sb.AppendLine();
        sb.AppendLine("        // Step 11: Create Smalltalk global dictionary");
        sb.AppendLine("        CreateSmalltalkDictionary(vm);");
        sb.AppendLine();
        sb.AppendLine("        // Register in VM");
        sb.AppendLine("        vm.RegisterBootstrapClasses(GetBootstrapClasses());");
        sb.AppendLine("    }");
        sb.AppendLine();
    }
    
    private void GenerateHelperMethods(StringBuilder sb)
    {
        // Generate primitive wrapper methods
        sb.AppendLine("    // ===== Primitive Wrapper Methods =====");
        sb.AppendLine();
        sb.AppendLine("    private static SmalltalkClass PrimitiveNewClass()");
        sb.AppendLine("    {");
        sb.AppendLine("        // Allocate 8 slots for Class instances");
        sb.AppendLine("        return new SmalltalkClass(null, 8);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static SmalltalkClass PrimitiveNewMetaclass()");
        sb.AppendLine("    {");
        sb.AppendLine("        // Allocate 6 slots for Metaclass instances");
        sb.AppendLine("        return new SmalltalkClass(null, 6);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static void PrimitiveSetClass(SmalltalkObject obj, SmalltalkClass cls)");
        sb.AppendLine("    {");
        sb.AppendLine("        obj.ClassPointer = cls;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static void PrimitiveSetSlot(SmalltalkObject obj, int index, object value)");
        sb.AppendLine("    {");
        sb.AppendLine("        obj.Fields[index] = value;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static Symbol CreateSymbol(string name)");
        sb.AppendLine("    {");
        sb.AppendLine("        return new Symbol(name);");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Generate CreateEssentialClasses method
        sb.AppendLine("    private static void CreateEssentialClasses(VirtualMachine vm)");
        sb.AppendLine("    {");
        
        // Generate creation for each class from Bootstrap.st
        foreach (var classDef in _classes.Values)
        {
            if (IsBootstrapCoreClass(classDef.Name))
                continue; // Already created in main method
            
            sb.AppendLine($"        // Create {classDef.Name}");
            sb.AppendLine($"        CreateClass(vm, \"{classDef.Name}\", \"{classDef.SuperclassName}\",");
            sb.AppendLine($"            new[] {{ {FormatStringArray(classDef.InstanceVariables)} }},");
            sb.AppendLine($"            new[] {{ {FormatStringArray(classDef.ClassVariables)} }});");
            sb.AppendLine();
        }
        
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Generate InstallCoreMethods
        sb.AppendLine("    private static void InstallCoreMethods(VirtualMachine vm)");
        sb.AppendLine("    {");
        
        foreach (var className in _methods.Keys)
        {
            var methods = _methods[className];
            if (methods.Count > 0)
            {
                sb.AppendLine($"        // {className} instance methods");
                foreach (var method in methods)
                {
                    sb.AppendLine($"        InstallMethod(vm, \"{className}\", \"{method.Selector}\",");
                    sb.AppendLine($"            @\"{EscapeString(method.Source)}\");");
                }
                sb.AppendLine();
            }
        }
        
        foreach (var className in _classMethods.Keys)
        {
            var methods = _classMethods[className];
            if (methods.Count > 0)
            {
                sb.AppendLine($"        // {className} class methods");
                foreach (var method in methods)
                {
                    sb.AppendLine($"        InstallClassMethod(vm, \"{className}\", \"{method.Selector}\",");
                    sb.AppendLine($"            @\"{EscapeString(method.Source)}\");");
                }
                sb.AppendLine();
            }
        }
        
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Generate other helper methods
        sb.AppendLine("    private static void CreateSingletons(VirtualMachine vm)");
        sb.AppendLine("    {");
        sb.AppendLine("        var undefinedObject = vm.LookupClass(\"UndefinedObject\");");
        sb.AppendLine("        var trueClass = vm.LookupClass(\"True\");");
        sb.AppendLine("        var falseClass = vm.LookupClass(\"False\");");
        sb.AppendLine();
        sb.AppendLine("        nil = vm.CreateInstance(undefinedObject);");
        sb.AppendLine("        trueObj = vm.CreateInstance(trueClass);");
        sb.AppendLine("        falseObj = vm.CreateInstance(falseClass);");
        sb.AppendLine();
        sb.AppendLine("        vm.RegisterGlobal(\"nil\", nil);");
        sb.AppendLine("        vm.RegisterGlobal(\"true\", trueObj);");
        sb.AppendLine("        vm.RegisterGlobal(\"false\", falseObj);");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    private static void CreateSmalltalkDictionary(VirtualMachine vm)");
        sb.AppendLine("    {");
        sb.AppendLine("        var systemDictionary = vm.LookupClass(\"SystemDictionary\");");
        sb.AppendLine("        smalltalk = vm.CreateInstance(systemDictionary);");
        sb.AppendLine();
        sb.AppendLine("        // Register all classes in Smalltalk dictionary");
        sb.AppendLine("        foreach (var className in GetAllClassNames())");
        sb.AppendLine("        {");
        sb.AppendLine("            var cls = vm.LookupClass(className);");
        sb.AppendLine("            vm.DictionaryAtPut(smalltalk, className, cls);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        vm.RegisterGlobal(\"Smalltalk\", smalltalk);");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    private static Dictionary<string, SmalltalkObject> GetBootstrapClasses()");
        sb.AppendLine("    {");
        sb.AppendLine("        return new Dictionary<string, SmalltalkObject>");
        sb.AppendLine("        {");
        sb.AppendLine("            { \"ProtoObject\", protoObject },");
        sb.AppendLine("            { \"Object\", object },");
        sb.AppendLine("            { \"Behavior\", behavior },");
        sb.AppendLine("            { \"ClassDescription\", classDescription },");
        sb.AppendLine("            { \"Class\", classClass },");
        sb.AppendLine("            { \"Metaclass\", metaclass }");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    private static string[] GetAllClassNames()");
        sb.AppendLine("    {");
        sb.AppendLine("        return new[]");
        sb.AppendLine("        {");
        foreach (var className in _classes.Keys.OrderBy(k => k))
        {
            sb.AppendLine($"            \"{className}\",");
        }
        sb.AppendLine("        };");
        sb.AppendLine("    }");
    }
    
    private bool IsBootstrapCoreClass(string className)
    {
        return className == "ProtoObject" || className == "Object" ||
               className == "Behavior" || className == "ClassDescription" ||
               className == "Class" || className == "Metaclass";
    }
    
    private string FormatStringArray(List<string> items)
    {
        if (items.Count == 0)
            return string.Empty;
        
        return string.Join(", ", items.Select(s => $"\"{s}\""));
    }
    
    private string EscapeString(string str)
    {
        return str.Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
    }
}