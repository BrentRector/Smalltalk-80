using System.Collections.Concurrent;

namespace Smalltalk80.Parser;

/// <summary>
/// Manages class definitions and method installations in the runtime system.
/// Provides a bridge between parsed AST nodes and the object memory system.
/// </summary>
public class ClassRegistry
{
    private readonly ConcurrentDictionary<string, ClassInfo> classes = new();
    private readonly ConcurrentDictionary<string, CompiledMethod> globalMethods = new();

    /// <summary>
    /// Gets the singleton instance of the class registry.
    /// </summary>
    public static ClassRegistry Instance { get; } = new ClassRegistry();

    /// <summary>
    /// Information about a registered class.
    /// </summary>
    public record ClassInfo(
        string Name,
        string? Superclass,
        IReadOnlyList<string> InstanceVariables,
        IReadOnlyList<string> ClassVariables,
        IReadOnlyList<string> PoolDictionaries,
        string? Category,
        ConcurrentDictionary<string, CompiledMethod> InstanceMethods,
        ConcurrentDictionary<string, CompiledMethod> ClassMethods
    );

    private ClassRegistry()
    {
        // Initialize with core classes
        InitializeCoreClasses();
    }

    /// <summary>
    /// Register a class definition from a parsed AST node.
    /// </summary>
    /// <param name="classDef">The parsed class definition.</param>
    public void RegisterClass(ClassDefinitionNode classDef)
    {
        var classInfo = new ClassInfo(
            classDef.ClassName,
            classDef.Superclass,
            classDef.InstanceVariables,
            classDef.ClassVariables,
            classDef.PoolDictionaries,
            classDef.Category,
            new ConcurrentDictionary<string, CompiledMethod>(),
            new ConcurrentDictionary<string, CompiledMethod>()
        );

        classes.AddOrUpdate(classDef.ClassName, classInfo, (key, old) => classInfo);
    }

    /// <summary>
    /// Install a method in a class from a parsed method definition.
    /// </summary>
    /// <param name="className">The target class name.</param>
    /// <param name="isClassSide">Whether this is a class-side method.</param>
    /// <param name="methodDef">The parsed method definition.</param>
    public void InstallMethod(string className, bool isClassSide, MethodDefinitionNode methodDef)
    {
        if (!classes.TryGetValue(className, out var classInfo))
        {
            throw new InvalidOperationException($"Class {className} not found. Register the class first.");
        }

        // Compile the method with the class's instance variables
        var compiledMethod = AstMethodCompiler.CompileMethod(
            methodDef.Body,
            methodDef.Arguments,
            classInfo.InstanceVariables,
            methodDef.HasPrimitive,
            methodDef.PrimitiveIndex,
            methodDef.Comment
        );

        // Install in the appropriate method dictionary
        var methodDict = isClassSide ? classInfo.ClassMethods : classInfo.InstanceMethods;
        methodDict.AddOrUpdate(methodDef.Selector, compiledMethod, (key, old) => compiledMethod);
    }

    /// <summary>
    /// Look up a method in a class hierarchy.
    /// </summary>
    /// <param name="className">The class to search.</param>
    /// <param name="selector">The method selector.</param>
    /// <param name="isClassSide">Whether to search class-side methods.</param>
    /// <returns>The compiled method if found, null otherwise.</returns>
    public CompiledMethod? LookupMethod(string className, string selector, bool isClassSide = false)
    {
        var currentClass = className;

        while (currentClass != null)
        {
            if (classes.TryGetValue(currentClass, out var classInfo))
            {
                var methodDict = isClassSide ? classInfo.ClassMethods : classInfo.InstanceMethods;
                if (methodDict.TryGetValue(selector, out var method))
                {
                    return method;
                }

                // Move up the inheritance chain
                currentClass = classInfo.Superclass;
            }
            else
            {
                break;
            }
        }

        return null;
    }

    /// <summary>
    /// Get information about a registered class.
    /// </summary>
    /// <param name="className">The class name.</param>
    /// <returns>Class information if found, null otherwise.</returns>
    public ClassInfo? GetClassInfo(string className)
    {
        return classes.TryGetValue(className, out var classInfo) ? classInfo : null;
    }

    /// <summary>
    /// Get all instance variables for a class, including inherited ones.
    /// </summary>
    /// <param name="className">The class name.</param>
    /// <returns>All instance variables in inheritance order.</returns>
    public IReadOnlyList<string> GetAllInstanceVariables(string className)
    {
        var variables = new List<string>();
        var currentClass = className;

        // Build inheritance chain first
        var inheritanceChain = new List<string>();
        while (currentClass != null)
        {
            if (classes.TryGetValue(currentClass, out var classInfo))
            {
                inheritanceChain.Add(currentClass);
                currentClass = classInfo.Superclass;
            }
            else
            {
                break;
            }
        }

        // Add variables from superclasses first
        inheritanceChain.Reverse();
        foreach (var ancestor in inheritanceChain)
        {
            if (classes.TryGetValue(ancestor, out var classInfo))
            {
                variables.AddRange(classInfo.InstanceVariables);
            }
        }

        return variables;
    }

    /// <summary>
    /// Register a global method (for workspace evaluation).
    /// </summary>
    /// <param name="selector">The method selector.</param>
    /// <param name="method">The compiled method.</param>
    public void RegisterGlobalMethod(string selector, CompiledMethod method)
    {
        globalMethods.AddOrUpdate(selector, method, (key, old) => method);
    }

    /// <summary>
    /// Look up a global method.
    /// </summary>
    /// <param name="selector">The method selector.</param>
    /// <returns>The compiled method if found, null otherwise.</returns>
    public CompiledMethod? LookupGlobalMethod(string selector)
    {
        return globalMethods.TryGetValue(selector, out var method) ? method : null;
    }

    /// <summary>
    /// Process a complete file-in, registering all classes and methods.
    /// </summary>
    /// <param name="fileNode">The parsed file-in node.</param>
    public void ProcessFileIn(FileNode fileNode)
    {
        string? currentClassName = null;
        bool currentIsClassSide = false;

        foreach (var chunk in fileNode.Chunks)
        {
            switch (chunk)
            {
                case ClassDefinitionNode classDef:
                    RegisterClass(classDef);
                    currentClassName = classDef.ClassName;
                    currentIsClassSide = false;
                    break;

                case MethodHeaderNode methodHeader:
                    currentClassName = methodHeader.ClassName;
                    currentIsClassSide = methodHeader.IsClassSide;
                    break;

                case MethodDefinitionNode methodDef:
                    if (currentClassName != null)
                    {
                        InstallMethod(currentClassName, currentIsClassSide, methodDef);
                    }
                    break;

                case ClassCommentNode comment:
                    // TODO: Store class comments
                    break;

                case ExpressionChunkNode expression:
                    // TODO: Execute expression chunks
                    break;
            }
        }
    }

    /// <summary>
    /// Get all registered class names.
    /// </summary>
    public IEnumerable<string> GetAllClassNames()
    {
        return classes.Keys;
    }

    /// <summary>
    /// Clear all registered classes and methods.
    /// </summary>
    public void Clear()
    {
        classes.Clear();
        globalMethods.Clear();
        InitializeCoreClasses();
    }

    /// <summary>
    /// Initialize core Smalltalk classes.
    /// </summary>
    private void InitializeCoreClasses()
    {
        // Register fundamental classes with minimal definitions
        var objectClass = new ClassInfo(
            "Object",
            null, // Object has no superclass
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "Kernel-Objects",
            new ConcurrentDictionary<string, CompiledMethod>(),
            new ConcurrentDictionary<string, CompiledMethod>()
        );
        classes["Object"] = objectClass;

        // Add other core classes as needed
        RegisterCoreClass("Class", "Object", "Kernel-Classes");
        RegisterCoreClass("Metaclass", "Class", "Kernel-Classes");
        RegisterCoreClass("Behavior", "Object", "Kernel-Classes");
        RegisterCoreClass("ClassDescription", "Behavior", "Kernel-Classes");

        // Core data types
        RegisterCoreClass("Magnitude", "Object", "Kernel-Numbers");
        RegisterCoreClass("Number", "Magnitude", "Kernel-Numbers");
        RegisterCoreClass("Integer", "Number", "Kernel-Numbers");
        RegisterCoreClass("SmallInteger", "Integer", "Kernel-Numbers");
        RegisterCoreClass("Float", "Number", "Kernel-Numbers");

        // Collections
        RegisterCoreClass("Collection", "Object", "Collections-Abstract");
        RegisterCoreClass("SequenceableCollection", "Collection", "Collections-Sequenceable");
        RegisterCoreClass("ArrayedCollection", "SequenceableCollection", "Collections-Arrayed");
        RegisterCoreClass("Array", "ArrayedCollection", "Collections-Arrayed");
        RegisterCoreClass("String", "ArrayedCollection", "Collections-Strings");
        RegisterCoreClass("Symbol", "String", "Collections-Strings");

        // Other core classes
        RegisterCoreClass("Boolean", "Object", "Kernel-Objects");
        RegisterCoreClass("True", "Boolean", "Kernel-Objects");
        RegisterCoreClass("False", "Boolean", "Kernel-Objects");
        RegisterCoreClass("UndefinedObject", "Object", "Kernel-Objects");
    }

    /// <summary>
    /// Register a core class with minimal definition.
    /// </summary>
    private void RegisterCoreClass(string name, string superclass, string category)
    {
        var classInfo = new ClassInfo(
            name,
            superclass,
            new List<string>(),
            new List<string>(),
            new List<string>(),
            category,
            new ConcurrentDictionary<string, CompiledMethod>(),
            new ConcurrentDictionary<string, CompiledMethod>()
        );
        classes[name] = classInfo;
    }
}
