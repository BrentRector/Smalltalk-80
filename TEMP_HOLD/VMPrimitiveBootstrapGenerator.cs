using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Smalltalk80.Parser;

namespace Smalltalk80.Bootstrap;

/// <summary>
/// Generates C# bootstrap code that invokes VM primitives to construct actual Smalltalk objects.
/// This generator produces code that orchestrates primitive-level VM operations.
/// </summary>
public class VMPrimitiveBootstrapGenerator
{
    private readonly string _bootstrapPath;
    private readonly Dictionary<string, ClassDefinition> _classes = new();
    private readonly Dictionary<string, List<MethodDefinition>> _methods = new();
    private readonly Dictionary<string, List<MethodDefinition>> _classMethods = new();
    
    // VM slot indices based on Smalltalk-80 object layout
    private const int SLOT_CLASS = 0;        // Object's class pointer
    private const int SLOT_SUPERCLASS = 0;   // In class objects
    private const int SLOT_METHODDICT = 1;   // Method dictionary
    private const int SLOT_FORMAT = 2;       // Instance format
    private const int SLOT_INSTANCEVARS = 3; // Instance variable names
    private const int SLOT_ORGANIZATION = 4; // Method organization
    private const int SLOT_NAME = 5;         // Class name (Class objects)
    private const int SLOT_CLASSPOOL = 6;    // Class variables (CRITICAL!)
    private const int SLOT_SHAREDPOOLS = 7;  // Shared pools
    private const int SLOT_THISCLASS = 5;    // For Metaclass objects
    
    private class ClassDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string SuperclassName { get; set; } = string.Empty;
        public List<string> InstanceVariables { get; set; } = new();
        public List<string> ClassVariables { get; set; } = new();
        public string Category { get; set; } = string.Empty;
    }
    
    private class MethodDefinition
    {
        public string Selector { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int? PrimitiveNumber { get; set; }
    }
    
    public VMPrimitiveBootstrapGenerator(string bootstrapPath)
    {
        _bootstrapPath = bootstrapPath;
    }
    
    public string Generate()
    {
        // Parse Bootstrap.st
        ParseBootstrapFile();
        
        var sb = new StringBuilder();
        
        // Generate file header
        GenerateHeader(sb);
        
        // Generate the bootstrap class
        sb.AppendLine("namespace Smalltalk80.Bootstrap;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Bootstrap implementation using VM primitives.");
        sb.AppendLine("/// Creates actual Smalltalk objects inside the VM via primitive operations.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class VMPrimitiveBootstrap");
        sb.AppendLine("{");
        
        // Generate field declarations for tracking OOPs
        GenerateOopTracking(sb);
        
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
        sb.AppendLine("// Generated by VMPrimitiveBootstrapGenerator");
        sb.AppendLine("// This file orchestrates VM primitives to construct the Smalltalk-80 class hierarchy");
        sb.AppendLine("// Following REVISED_BOOTSTRAP_PROCEDURE.md");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
    }
    
    private void GenerateOopTracking(StringBuilder sb)
    {
        sb.AppendLine("    // Object IDs (OOPs) for core classes");
        sb.AppendLine("    private static int metaclassId;");
        sb.AppendLine("    private static int classId;");
        sb.AppendLine("    private static int behaviorId;");
        sb.AppendLine("    private static int classDescriptionId;");
        sb.AppendLine("    private static int protoObjectId;");
        sb.AppendLine("    private static int protoObjectClassId;");
        sb.AppendLine("    private static int objectId;");
        sb.AppendLine("    private static int objectClassId;");
        sb.AppendLine();
        sb.AppendLine("    // OOPs for essential classes");
        sb.AppendLine("    private static int undefinedObjectId;");
        sb.AppendLine("    private static int undefinedObjectClassId;");
        sb.AppendLine("    private static int booleanId;");
        sb.AppendLine("    private static int trueClassId;");
        sb.AppendLine("    private static int falseClassId;");
        sb.AppendLine();
        sb.AppendLine("    // Singleton OOPs");
        sb.AppendLine("    private static int nilId;");
        sb.AppendLine("    private static int trueId;");
        sb.AppendLine("    private static int falseId;");
        sb.AppendLine();
        sb.AppendLine("    // Dictionary to track all class OOPs");
        sb.AppendLine("    private static readonly Dictionary<string, int> classOops = new();");
        sb.AppendLine("    private static readonly Dictionary<string, int> metaclassOops = new();");
        sb.AppendLine();
    }
    
    private void GenerateInitializeMethod(StringBuilder sb)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Initialize the bootstrap using VM primitives.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static void Initialize(ISmalltalkVM vm)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Step 1: Create Metaclass (self-referential)");
        sb.AppendLine("        metaclassId = vm.PrimitiveNewMetaclass();");
        sb.AppendLine("        vm.PrimitiveSetClass(metaclassId, metaclassId); // SELF-REFERENTIAL!");
        sb.AppendLine("        vm.PrimitiveSetSlot(metaclassId, SLOT_NAME, vm.PrimitiveInternSymbol(\"Metaclass\"));");
        sb.AppendLine("        vm.PrimitiveSetSlot(metaclassId, SLOT_METHODDICT, vm.PrimitiveNewDictionary());");
        sb.AppendLine("        vm.PrimitiveSetSlot(metaclassId, SLOT_INSTANCEVARS, vm.PrimitiveNewArray(1));");
        sb.AppendLine("        vm.PrimitiveArrayAtPut(vm.PrimitiveSetSlot(metaclassId, SLOT_INSTANCEVARS), 0, vm.PrimitiveInternSymbol(\"thisClass\"));");
        sb.AppendLine("        classOops[\"Metaclass\"] = metaclassId;");
        sb.AppendLine();
        
        sb.AppendLine("        // Step 2: Create Class (instance of Metaclass)");
        sb.AppendLine("        classId = vm.PrimitiveNewClass();");
        sb.AppendLine("        vm.PrimitiveSetSlot(classId, SLOT_NAME, vm.PrimitiveInternSymbol(\"Class\"));");
        sb.AppendLine("        vm.PrimitiveSetSlot(classId, SLOT_METHODDICT, vm.PrimitiveNewDictionary());");
        sb.AppendLine("        ");
        sb.AppendLine("        // Create Class's metaclass");
        sb.AppendLine("        int classMetaclassId = vm.PrimitiveNewMetaclass();");
        sb.AppendLine("        vm.PrimitiveSetClass(classMetaclassId, metaclassId);");
        sb.AppendLine("        vm.PrimitiveSetSlot(classMetaclassId, SLOT_THISCLASS, classId);");
        sb.AppendLine("        vm.PrimitiveSetClass(classId, classMetaclassId);");
        sb.AppendLine("        classOops[\"Class\"] = classId;");
        sb.AppendLine("        metaclassOops[\"Class\"] = classMetaclassId;");
        sb.AppendLine();
        
        sb.AppendLine("        // Step 3: Create Behavior");
        sb.AppendLine("        behaviorId = vm.PrimitiveNewClass();");
        sb.AppendLine("        vm.PrimitiveSetClass(behaviorId, classId);");
        sb.AppendLine("        vm.PrimitiveSetSlot(behaviorId, SLOT_NAME, vm.PrimitiveInternSymbol(\"Behavior\"));");
        sb.AppendLine("        vm.PrimitiveSetSlot(behaviorId, SLOT_METHODDICT, vm.PrimitiveNewDictionary());");
        sb.AppendLine("        ");
        sb.AppendLine("        // Create Behavior's metaclass");
        sb.AppendLine("        int behaviorMetaclassId = vm.PrimitiveNewMetaclass();");
        sb.AppendLine("        vm.PrimitiveSetClass(behaviorMetaclassId, metaclassId);");
        sb.AppendLine("        vm.PrimitiveSetSlot(behaviorMetaclassId, SLOT_THISCLASS, behaviorId);");
        sb.AppendLine("        vm.PrimitiveSetClass(behaviorId, behaviorMetaclassId);");
        sb.AppendLine("        classOops[\"Behavior\"] = behaviorId;");
        sb.AppendLine("        metaclassOops[\"Behavior\"] = behaviorMetaclassId;");
        sb.AppendLine();
        
        sb.AppendLine("        // Step 4: Create ProtoObject (the root)");
        sb.AppendLine("        protoObjectId = vm.PrimitiveNewClass();");
        sb.AppendLine("        vm.PrimitiveSetClass(protoObjectId, classId);");
        sb.AppendLine("        vm.PrimitiveSetSlot(protoObjectId, SLOT_NAME, vm.PrimitiveInternSymbol(\"ProtoObject\"));");
        sb.AppendLine("        vm.PrimitiveSetSlot(protoObjectId, SLOT_SUPERCLASS, vm.PrimitiveNil()); // ROOT!");
        sb.AppendLine("        vm.PrimitiveSetSlot(protoObjectId, SLOT_METHODDICT, vm.PrimitiveNewDictionary());");
        sb.AppendLine("        ");
        sb.AppendLine("        // Create ProtoObject's metaclass");
        sb.AppendLine("        protoObjectClassId = vm.PrimitiveNewMetaclass();");
        sb.AppendLine("        vm.PrimitiveSetClass(protoObjectClassId, metaclassId);");
        sb.AppendLine("        vm.PrimitiveSetSlot(protoObjectClassId, SLOT_THISCLASS, protoObjectId);");
        sb.AppendLine("        vm.PrimitiveSetSlot(protoObjectClassId, SLOT_SUPERCLASS, classId); // CRITICAL LOOP!");
        sb.AppendLine("        vm.PrimitiveSetClass(protoObjectId, protoObjectClassId);");
        sb.AppendLine("        classOops[\"ProtoObject\"] = protoObjectId;");
        sb.AppendLine("        metaclassOops[\"ProtoObject\"] = protoObjectClassId;");
        sb.AppendLine();
        
        sb.AppendLine("        // Step 5: Fix superclass relationships");
        sb.AppendLine("        vm.PrimitiveSetSlot(behaviorId, SLOT_SUPERCLASS, protoObjectId);");
        sb.AppendLine("        vm.PrimitiveSetSlot(behaviorMetaclassId, SLOT_SUPERCLASS, protoObjectClassId);");
        sb.AppendLine();
        
        sb.AppendLine("        // Step 6: Create ClassDescription");
        sb.AppendLine("        classDescriptionId = vm.PrimitiveNewClass();");
        sb.AppendLine("        vm.PrimitiveSetClass(classDescriptionId, classId);");
        sb.AppendLine("        vm.PrimitiveSetSlot(classDescriptionId, SLOT_NAME, vm.PrimitiveInternSymbol(\"ClassDescription\"));");
        sb.AppendLine("        vm.PrimitiveSetSlot(classDescriptionId, SLOT_SUPERCLASS, behaviorId);");
        sb.AppendLine("        vm.PrimitiveSetSlot(classDescriptionId, SLOT_METHODDICT, vm.PrimitiveNewDictionary());");
        sb.AppendLine("        ");
        sb.AppendLine("        int classDescriptionMetaclassId = vm.PrimitiveNewMetaclass();");
        sb.AppendLine("        vm.PrimitiveSetClass(classDescriptionMetaclassId, metaclassId);");
        sb.AppendLine("        vm.PrimitiveSetSlot(classDescriptionMetaclassId, SLOT_THISCLASS, classDescriptionId);");
        sb.AppendLine("        vm.PrimitiveSetSlot(classDescriptionMetaclassId, SLOT_SUPERCLASS, behaviorMetaclassId);");
        sb.AppendLine("        vm.PrimitiveSetClass(classDescriptionId, classDescriptionMetaclassId);");
        sb.AppendLine("        classOops[\"ClassDescription\"] = classDescriptionId;");
        sb.AppendLine("        metaclassOops[\"ClassDescription\"] = classDescriptionMetaclassId;");
        sb.AppendLine();
        
        sb.AppendLine("        // Fix Class and Metaclass to inherit from ClassDescription");
        sb.AppendLine("        vm.PrimitiveSetSlot(classId, SLOT_SUPERCLASS, classDescriptionId);");
        sb.AppendLine("        vm.PrimitiveSetSlot(classMetaclassId, SLOT_SUPERCLASS, classDescriptionMetaclassId);");
        sb.AppendLine("        vm.PrimitiveSetSlot(metaclassId, SLOT_SUPERCLASS, classDescriptionId);");
        sb.AppendLine();
        
        sb.AppendLine("        // Step 7: Create remaining essential classes");
        sb.AppendLine("        CreateEssentialClasses(vm);");
        sb.AppendLine();
        
        sb.AppendLine("        // Step 8: Install methods");
        sb.AppendLine("        InstallMethods(vm);");
        sb.AppendLine();
        
        sb.AppendLine("        // Step 9: Create singleton instances");
        sb.AppendLine("        CreateSingletons(vm);");
        sb.AppendLine();
        
        sb.AppendLine("        // Step 10: Register globals");
        sb.AppendLine("        RegisterGlobals(vm);");
        sb.AppendLine("    }");
        sb.AppendLine();
    }
    
    private void GenerateHelperMethods(StringBuilder sb)
    {
        // Generate CreateEssentialClasses
        sb.AppendLine("    private static void CreateEssentialClasses(ISmalltalkVM vm)");
        sb.AppendLine("    {");
        
        foreach (var classDef in _classes.Values)
        {
            if (IsBootstrapCoreClass(classDef.Name))
                continue;
            
            sb.AppendLine($"        // Create {classDef.Name}");
            sb.AppendLine($"        CreateClass(vm, \"{classDef.Name}\", \"{classDef.SuperclassName}\",");
            sb.AppendLine($"            new[] {{ {FormatStringArray(classDef.InstanceVariables)} }},");
            sb.AppendLine($"            new[] {{ {FormatStringArray(classDef.ClassVariables)} }});");
            sb.AppendLine();
        }
        
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Generate CreateClass helper
        sb.AppendLine("    private static void CreateClass(ISmalltalkVM vm, string className, string superclassName,");
        sb.AppendLine("        string[] instanceVars, string[] classVars)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Create the class");
        sb.AppendLine("        int classOop = vm.PrimitiveNewClass();");
        sb.AppendLine("        vm.PrimitiveSetSlot(classOop, SLOT_NAME, vm.PrimitiveInternSymbol(className));");
        sb.AppendLine("        ");
        sb.AppendLine("        // Set superclass");
        sb.AppendLine("        if (superclassName != \"nil\" && classOops.ContainsKey(superclassName))");
        sb.AppendLine("        {");
        sb.AppendLine("            vm.PrimitiveSetSlot(classOop, SLOT_SUPERCLASS, classOops[superclassName]);");
        sb.AppendLine("        }");
        sb.AppendLine("        else");
        sb.AppendLine("        {");
        sb.AppendLine("            vm.PrimitiveSetSlot(classOop, SLOT_SUPERCLASS, vm.PrimitiveNil());");
        sb.AppendLine("        }");
        sb.AppendLine("        ");
        sb.AppendLine("        // Set instance variables");
        sb.AppendLine("        int instVarArray = vm.PrimitiveNewArray(instanceVars.Length);");
        sb.AppendLine("        for (int i = 0; i < instanceVars.Length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            vm.PrimitiveArrayAtPut(instVarArray, i, vm.PrimitiveInternSymbol(instanceVars[i]));");
        sb.AppendLine("        }");
        sb.AppendLine("        vm.PrimitiveSetSlot(classOop, SLOT_INSTANCEVARS, instVarArray);");
        sb.AppendLine("        ");
        sb.AppendLine("        // Set class variables (CRITICAL - slot 6!)");
        sb.AppendLine("        if (classVars.Length > 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            int classVarDict = vm.PrimitiveNewDictionary();");
        sb.AppendLine("            foreach (var classVar in classVars)");
        sb.AppendLine("            {");
        sb.AppendLine("                vm.PrimitiveDictionaryAtPut(classVarDict, ");
        sb.AppendLine("                    vm.PrimitiveInternSymbol(classVar), vm.PrimitiveNil());");
        sb.AppendLine("            }");
        sb.AppendLine("            vm.PrimitiveSetSlot(classOop, SLOT_CLASSPOOL, classVarDict);");
        sb.AppendLine("        }");
        sb.AppendLine("        ");
        sb.AppendLine("        // Set method dictionary");
        sb.AppendLine("        vm.PrimitiveSetSlot(classOop, SLOT_METHODDICT, vm.PrimitiveNewDictionary());");
        sb.AppendLine("        ");
        sb.AppendLine("        // Create metaclass");
        sb.AppendLine("        int metaclassOop = vm.PrimitiveNewMetaclass();");
        sb.AppendLine("        vm.PrimitiveSetClass(metaclassOop, metaclassOops[\"Metaclass\"]);");
        sb.AppendLine("        vm.PrimitiveSetSlot(metaclassOop, SLOT_THISCLASS, classOop);");
        sb.AppendLine("        ");
        sb.AppendLine("        // Set metaclass superclass (parallel hierarchy)");
        sb.AppendLine("        if (superclassName != \"nil\" && metaclassOops.ContainsKey(superclassName))");
        sb.AppendLine("        {");
        sb.AppendLine("            vm.PrimitiveSetSlot(metaclassOop, SLOT_SUPERCLASS, metaclassOops[superclassName]);");
        sb.AppendLine("        }");
        sb.AppendLine("        ");
        sb.AppendLine("        // Link class to metaclass");
        sb.AppendLine("        vm.PrimitiveSetClass(classOop, metaclassOop);");
        sb.AppendLine("        ");
        sb.AppendLine("        // Store in dictionaries");
        sb.AppendLine("        classOops[className] = classOop;");
        sb.AppendLine("        metaclassOops[className] = metaclassOop;");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Generate InstallMethods
        sb.AppendLine("    private static void InstallMethods(ISmalltalkVM vm)");
        sb.AppendLine("    {");
        
        foreach (var className in _methods.Keys)
        {
            var methods = _methods[className];
            if (methods.Count > 0)
            {
                sb.AppendLine($"        // {className} instance methods");
                foreach (var method in methods)
                {
                    sb.AppendLine($"        if (classOops.ContainsKey(\"{className}\"))");
                    sb.AppendLine($"            vm.PrimitiveInstallMethod(classOops[\"{className}\"], \"{method.Selector}\",");
                    sb.AppendLine($"                @\"{EscapeString(method.Source)}\");");
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
                    sb.AppendLine($"        if (metaclassOops.ContainsKey(\"{className}\"))");
                    sb.AppendLine($"            vm.PrimitiveInstallMethod(metaclassOops[\"{className}\"], \"{method.Selector}\",");
                    sb.AppendLine($"                @\"{EscapeString(method.Source)}\");");
                }
                sb.AppendLine();
            }
        }
        
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Generate CreateSingletons
        sb.AppendLine("    private static void CreateSingletons(ISmalltalkVM vm)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Create nil (instance of UndefinedObject)");
        sb.AppendLine("        if (classOops.ContainsKey(\"UndefinedObject\"))");
        sb.AppendLine("        {");
        sb.AppendLine("            nilId = vm.PrimitiveNewInstance(classOops[\"UndefinedObject\"]);");
        sb.AppendLine("        }");
        sb.AppendLine("        ");
        sb.AppendLine("        // Create true (instance of True)");
        sb.AppendLine("        if (classOops.ContainsKey(\"True\"))");
        sb.AppendLine("        {");
        sb.AppendLine("            trueId = vm.PrimitiveNewInstance(classOops[\"True\"]);");
        sb.AppendLine("        }");
        sb.AppendLine("        ");
        sb.AppendLine("        // Create false (instance of False)");
        sb.AppendLine("        if (classOops.ContainsKey(\"False\"))");
        sb.AppendLine("        {");
        sb.AppendLine("            falseId = vm.PrimitiveNewInstance(classOops[\"False\"]);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Generate RegisterGlobals
        sb.AppendLine("    private static void RegisterGlobals(ISmalltalkVM vm)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Register nil, true, false");
        sb.AppendLine("        vm.PrimitiveRegisterGlobal(\"nil\", nilId);");
        sb.AppendLine("        vm.PrimitiveRegisterGlobal(\"true\", trueId);");
        sb.AppendLine("        vm.PrimitiveRegisterGlobal(\"false\", falseId);");
        sb.AppendLine("        ");
        sb.AppendLine("        // Register all classes");
        sb.AppendLine("        foreach (var kvp in classOops)");
        sb.AppendLine("        {");
        sb.AppendLine("            vm.PrimitiveRegisterGlobal(kvp.Key, kvp.Value);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Constants
        sb.AppendLine("    // VM slot indices");
        sb.AppendLine("    private const int SLOT_SUPERCLASS = 0;");
        sb.AppendLine("    private const int SLOT_METHODDICT = 1;");
        sb.AppendLine("    private const int SLOT_FORMAT = 2;");
        sb.AppendLine("    private const int SLOT_INSTANCEVARS = 3;");
        sb.AppendLine("    private const int SLOT_ORGANIZATION = 4;");
        sb.AppendLine("    private const int SLOT_NAME = 5;");
        sb.AppendLine("    private const int SLOT_CLASSPOOL = 6; // CRITICAL for class variables!");
        sb.AppendLine("    private const int SLOT_SHAREDPOOLS = 7;");
        sb.AppendLine("    private const int SLOT_THISCLASS = 5; // For Metaclass");
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
        return str.Replace("\"", "\"\"").Replace("\r", "").Replace("\n", "\n                ");
    }
}