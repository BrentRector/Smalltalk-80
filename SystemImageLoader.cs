using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Smalltalk80;
using Smalltalk80.Parser;

/// <summary>
/// Loads the complete Smalltalk-80 system image, with configurable exclusions
/// for platform-specific or unsupported classes.
/// </summary>
public class SystemImageLoader
{
    private readonly VirtualMachine vm;
    private readonly HashSet<string> excludedClasses;
    private readonly HashSet<string> excludedCategories;
    private readonly Dictionary<string, int> classLoadOrder;
    private readonly List<string> errors = new();
    private readonly List<string> warnings = new();
    private int classesLoaded = 0;
    private int methodsLoaded = 0;
    private int classesSkipped = 0;

    /// <summary>
    /// Default exclusions for classes that are Alto-specific or require
    /// primitives/features we don't support yet.
    /// </summary>
    private static readonly HashSet<string> DefaultExcludedClasses = new()
    {
        // Alto-specific file system
        "AltoFile", "AltoFileDirectory", "AltoFilePage",
        
        // Graphics and UI (would need extensive primitive support)
        "Form", "DisplayScreen", "DisplayMedium", "DisplayObject",
        "BitBlt", "CharacterScanner", "Pen", "DisplayBitmap",
        
        // Alto-specific hardware
        "AltoFileAddressArray", "AltoTextScanner",
        
        // Process scheduling (would need VM support)
        "ProcessHandle", "ProcessorScheduler",
        
        // Sound (not implemented)
        "FMSound", "AbstractSound",
        
        // Network (not implemented)  
        "Socket", "NetNameResolver",
        
        // These require special VM support we don't have yet
        "ObjectMemory", "SystemTracer",
        
        // Multiple inheritance (experimental in Smalltalk-80)
        "MetaclassForMultipleInheritance"
    };

    /// <summary>
    /// Categories to exclude entirely.
    /// </summary>
    private static readonly HashSet<string> DefaultExcludedCategories = new()
    {
        "Graphics-Primitives",
        "Graphics-Display Objects",
        "Graphics-Paths",
        "Graphics-Views",
        "Graphics-Editors",
        "Graphics-Support",
        "Interface-Framework",
        "Interface-Menus",
        "Interface-Browser",
        "Interface-Debugger",
        "Interface-Inspector",
        "Interface-Transcript",
        "System-Primitives",
        "System-Tracing",
        "Files-Alto"
    };

    /// <summary>
    /// Classes that must be loaded in a specific order due to dependencies.
    /// Lower numbers load first.
    /// </summary>
    private static readonly Dictionary<string, int> DefaultLoadOrder = new()
    {
        // Core object system (already in bootstrap)
        ["Object"] = 0,
        ["UndefinedObject"] = 1,
        ["Behavior"] = 2,
        ["ClassDescription"] = 3,
        ["Class"] = 4,
        ["Metaclass"] = 5,
        
        // Basic types
        ["Boolean"] = 10,
        ["True"] = 11,
        ["False"] = 12,
        ["Magnitude"] = 20,
        ["Number"] = 21,
        ["Integer"] = 22,
        ["SmallInteger"] = 23,
        ["LargePositiveInteger"] = 24,
        ["LargeNegativeInteger"] = 25,
        ["Float"] = 26,
        ["Fraction"] = 27,
        ["Character"] = 30,
        
        // Collections foundation
        ["Collection"] = 40,
        ["SequenceableCollection"] = 41,
        ["ArrayedCollection"] = 42,
        ["Array"] = 43,
        ["String"] = 44,
        ["Symbol"] = 45,
        ["ByteArray"] = 46,
        
        // Key-value before dictionaries
        ["LookupKey"] = 50,
        ["Association"] = 51,
        
        // Sets and dictionaries
        ["Set"] = 60,
        ["Dictionary"] = 61,
        ["SystemDictionary"] = 62,
        ["IdentitySet"] = 63,
        ["IdentityDictionary"] = 64,
        ["MethodDictionary"] = 65,
        
        // Other collections
        ["OrderedCollection"] = 70,
        ["SortedCollection"] = 71,
        ["Bag"] = 72,
        ["Interval"] = 73,
        ["LinkedList"] = 74,
        
        // Streams
        ["Stream"] = 80,
        ["PositionableStream"] = 81,
        ["ReadStream"] = 82,
        ["WriteStream"] = 83,
        ["ReadWriteStream"] = 84,
        ["ExternalStream"] = 85,
        ["FileStream"] = 86,
        
        // Execution machinery
        ["CompiledMethod"] = 90,
        ["MethodContext"] = 91,
        ["BlockContext"] = 92,
        ["ContextPart"] = 93,
        ["Message"] = 94,
        
        // Processes
        ["Link"] = 100,
        ["Process"] = 101,
        ["Semaphore"] = 102,
        ["SharedQueue"] = 103,
        ["Delay"] = 104,
        
        // Compiler/parser infrastructure
        ["Scanner"] = 110,
        ["Parser"] = 111,
        ["Compiler"] = 112,
        ["ParseNode"] = 113,
        
        // Change management
        ["Change"] = 120,
        ["ChangeSet"] = 121,
        
        // Text
        ["Text"] = 130,
        ["TextStyle"] = 131,
        ["TextAttribute"] = 132,
        
        // Date and time
        ["Date"] = 140,
        ["Time"] = 141,
        
        // System support
        ["SystemOrganizer"] = 150,
        ["ClassOrganizer"] = 151,
        ["ClassCategoryReader"] = 152,
        
        // File system (modern)
        ["File"] = 160,
        ["FileDirectory"] = 161,
        ["FilePage"] = 162,
        ["FileStream"] = 163
    };

    public SystemImageLoader(VirtualMachine virtualMachine)
    {
        vm = virtualMachine;
        excludedClasses = new HashSet<string>(DefaultExcludedClasses);
        excludedCategories = new HashSet<string>(DefaultExcludedCategories);
        classLoadOrder = new Dictionary<string, int>(DefaultLoadOrder);
    }

    /// <summary>
    /// Loads the complete system image with exclusions.
    /// </summary>
    public LoadResult LoadSystemImage(string path)
    {
        Console.WriteLine($"[SYSTEM IMAGE] Loading from: {path}");
        Console.WriteLine($"[SYSTEM IMAGE] Excluded categories: {excludedCategories.Count}");
        Console.WriteLine($"[SYSTEM IMAGE] Excluded classes: {excludedClasses.Count}");

        try
        {
            // Parse the file
            var fileAst = FileInReader.Parse(path);
            
            // Collect all class definitions first
            var allClasses = new List<(ClassDefinitionNode def, int order)>();
            
            foreach (var chunk in fileAst.Chunks)
            {
                if (chunk is ClassDefinitionNode classDef)
                {
                    // Check exclusions
                    if (ShouldExcludeClass(classDef))
                    {
                        classesSkipped++;
                        continue;
                    }
                    
                    // Determine load order
                    var order = classLoadOrder.TryGetValue(classDef.ClassName, out var o) ? o : 1000;
                    allClasses.Add((classDef, order));
                }
            }
            
            // Sort by load order
            allClasses.Sort((a, b) => a.order.CompareTo(b.order));
            
            Console.WriteLine($"[SYSTEM IMAGE] Found {allClasses.Count} classes to load (skipped {classesSkipped})");
            
            // Load classes in order
            foreach (var (classDef, _) in allClasses)
            {
                LoadClass(classDef);
            }
            
            // Now load all methods
            LoadMethods(fileAst);
            
            // Load initialization code
            LoadInitializations(fileAst);
            
            return new LoadResult
            {
                Success = errors.Count == 0,
                ClassesLoaded = classesLoaded,
                MethodsLoaded = methodsLoaded,
                ClassesSkipped = classesSkipped,
                Errors = errors,
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to load system image: {ex.Message}");
            return new LoadResult
            {
                Success = false,
                Errors = errors,
                Warnings = warnings
            };
        }
    }

    private bool ShouldExcludeClass(ClassDefinitionNode classDef)
    {
        // Check if class is explicitly excluded
        if (excludedClasses.Contains(classDef.ClassName))
        {
            Console.WriteLine($"[SYSTEM IMAGE] Excluding class: {classDef.ClassName} (explicit exclusion)");
            return true;
        }
        
        // Check if category is excluded
        if (classDef.Category != null && excludedCategories.Contains(classDef.Category))
        {
            Console.WriteLine($"[SYSTEM IMAGE] Excluding class: {classDef.ClassName} (category: {classDef.Category})");
            return true;
        }
        
        // Check if superclass was excluded (avoid orphans)
        if (!string.IsNullOrEmpty(classDef.Superclass) && 
            classDef.Superclass != "nil" &&
            !SystemDictionary.TryGetClass(classDef.Superclass, out _) &&
            excludedClasses.Contains(classDef.Superclass))
        {
            Console.WriteLine($"[SYSTEM IMAGE] Excluding class: {classDef.ClassName} (superclass {classDef.Superclass} was excluded)");
            excludedClasses.Add(classDef.ClassName); // Also exclude this one
            return true;
        }
        
        return false;
    }

    private void LoadClass(ClassDefinitionNode classDef)
    {
        try
        {
            // Check if this is a bootstrap class being enhanced
            if (SystemDictionary.TryGetClass(classDef.ClassName, out var existingClass))
            {
                // Bootstrap class - just update metadata if needed
                warnings.Add($"Class {classDef.ClassName} already exists from bootstrap, will enhance with methods");
                return;
            }
            
            // Use the SmalltalkLoader to create the class via Smalltalk messages
            var loader = new SmalltalkLoader(vm.Interpreter);
            var expression = GenerateClassCreationExpression(classDef);
            
            Console.WriteLine($"[SYSTEM IMAGE] Creating class: {classDef.ClassName}");
            
            // Execute the class creation
            vm.EvaluateSource(expression);
            classesLoaded++;
        }
        catch (Exception ex)
        {
            warnings.Add($"Failed to create class {classDef.ClassName}: {ex.Message}");
        }
    }

    private void LoadMethods(FileNode fileAst)
    {
        MethodHeaderNode? currentHeader = null;
        
        foreach (var chunk in fileAst.Chunks)
        {
            switch (chunk)
            {
                case MethodHeaderNode header:
                    currentHeader = header;
                    break;
                    
                case MethodCategoryNode category:
                    if (!ShouldExcludeClass(category.Header.ClassName))
                    {
                        LoadMethodCategory(category);
                    }
                    break;
                    
                case MethodDefinitionNode method when currentHeader != null:
                    if (!ShouldExcludeClass(currentHeader.ClassName))
                    {
                        LoadMethod(currentHeader, method);
                    }
                    break;
            }
        }
    }

    private bool ShouldExcludeClass(string className)
    {
        return excludedClasses.Contains(className);
    }

    private void LoadMethodCategory(MethodCategoryNode category)
    {
        foreach (var method in category.Methods)
        {
            LoadMethod(category.Header, method);
        }
    }

    private void LoadMethod(MethodHeaderNode header, MethodDefinitionNode method)
    {
        try
        {
            // Check if the class exists
            if (!SystemDictionary.TryGetClass(header.ClassName, out var targetClass))
            {
                // Class doesn't exist, skip method
                return;
            }
            
            // Compile and install the method using the existing infrastructure
            var variables = new List<string>();
            if (header.IsClassSide)
            {
                variables.AddRange(targetClass.ClassVariables);
            }
            else
            {
                // Add inherited instance variables
                var currentClass = targetClass;
                while (currentClass != null)
                {
                    variables.AddRange(currentClass.InstanceVariables);
                    currentClass = currentClass.Superclass;
                }
            }
            
            var compiledMethod = AstMethodCompiler.CompileMethod(
                method.Body,
                method.Arguments,
                variables,
                method.HasPrimitive,
                method.PrimitiveIndex);
            
            var selector = new Symbol(method.Selector);
            if (header.IsClassSide)
            {
                // Install class-side method in the metaclass
                if (targetClass.Metaclass != null)
                {
                    targetClass.Metaclass.MethodDictionary[selector] = compiledMethod;
                }
            }
            else
            {
                // Install instance-side method
                targetClass.MethodDictionary[selector] = compiledMethod;
            }
            
            methodsLoaded++;
        }
        catch (Exception ex)
        {
            // Don't report errors for methods that use unimplemented features
            // This is expected when loading the full system image
            if (!ex.Message.Contains("Unknown variable") && 
                !ex.Message.Contains("primitive"))
            {
                warnings.Add($"Failed to compile {method.Selector} in {header.ClassName}: {ex.Message}");
            }
        }
    }

    private void LoadInitializations(FileNode fileAst)
    {
        foreach (var chunk in fileAst.Chunks)
        {
            if (chunk is ExpressionChunkNode exprChunk)
            {
                // Skip initialization code for now - it often depends on
                // UI and other features we don't have
                continue;
            }
        }
    }

    private string GenerateClassCreationExpression(ClassDefinitionNode classDef)
    {
        var superclass = string.IsNullOrEmpty(classDef.Superclass) || classDef.Superclass == "nil" 
            ? "nil" 
            : classDef.Superclass;
            
        var instVars = string.Join(" ", classDef.InstanceVariables);
        var classVars = string.Join(" ", classDef.ClassVariables);
        var pools = string.Join(" ", classDef.PoolDictionaries);
        var category = classDef.Category ?? "Unclassified";
        
        return $@"{superclass} subclass: #{classDef.ClassName}
    instanceVariableNames: '{instVars}'
    classVariableNames: '{classVars}'
    poolDictionaries: '{pools}'
    category: '{category}'";
    }

    /// <summary>
    /// Adds a class to the exclusion list.
    /// </summary>
    public void ExcludeClass(string className)
    {
        excludedClasses.Add(className);
    }

    /// <summary>
    /// Adds a category to the exclusion list.
    /// </summary>
    public void ExcludeCategory(string category)
    {
        excludedCategories.Add(category);
    }

    /// <summary>
    /// Sets the load order for a class.
    /// </summary>
    public void SetClassLoadOrder(string className, int order)
    {
        classLoadOrder[className] = order;
    }

    /// <summary>
    /// Result of loading the system image.
    /// </summary>
    public class LoadResult
    {
        public bool Success { get; set; }
        public int ClassesLoaded { get; set; }
        public int MethodsLoaded { get; set; }
        public int ClassesSkipped { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}