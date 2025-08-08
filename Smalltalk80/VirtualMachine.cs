using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Smalltalk80;
using Smalltalk80.Bootstrap;
using Smalltalk80.Generated;
using Smalltalk80.Parser;

/// <summary>
/// Provides a high level interface combining the interpreter and object memory
/// into a simple virtual machine suitable for the REPL and other hosts.
/// </summary>
public sealed class VirtualMachine
{
    /// <summary>
    /// Gets the interpreter instance executing methods for this VM.
    /// </summary>
    public Interpreter Interpreter { get; } = new() { ExitOnReturn = false };
    
    private VMPrimitiveAdapter? bootstrapAdapter;
    
    /// <summary>
    /// Gets the bootstrap adapter for accessing VM primitives.
    /// </summary>
    public VMPrimitiveAdapter? Adapter => bootstrapAdapter;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualMachine"/> class using JSON bootstrap.
    /// </summary>
    public VirtualMachine(bool skipStandardLibrary = false)
    {
        InitializeWithJsonBootstrap(skipStandardLibrary);
    }
    
    /// <summary>
    /// Initializes the VM using the three-phase JSON bootstrap system.
    /// </summary>
    private void InitializeWithJsonBootstrap(bool skipStandardLibrary)
    {
        Console.WriteLine("[BOOTSTRAP] Starting JSON-based bootstrap...");
        
        var bootstrapDir = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
            "Bootstrap");
            
        if (!Directory.Exists(bootstrapDir))
        {
            // Fallback: look in project directory during development
            bootstrapDir = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
                "..", "..", "..", "..", "Bootstrap");
        }
        
        if (!Directory.Exists(bootstrapDir))
        {
            throw new DirectoryNotFoundException($"Bootstrap directory not found. Searched: {bootstrapDir}");
        }
        
        Console.WriteLine($"[BOOTSTRAP] Using bootstrap files from: {bootstrapDir}");
        
        // Phase 1: VM primitive operations (create objects and patch pointers)
        ExecutePhase1Bootstrap(bootstrapDir);
        
        // Phase 2: Message-based class hierarchy construction
        ExecutePhase2Bootstrap(bootstrapDir);
        
        // Phase 3: Standard library loading (if not skipped)
        if (!skipStandardLibrary)
        {
            ExecutePhase3Bootstrap(bootstrapDir);
        }
        
        Console.WriteLine("[BOOTSTRAP] JSON bootstrap complete!");
    }
    
    /// <summary>
    /// Executes Phase 1 bootstrap from JSON: VM primitive operations.
    /// </summary>
    private void ExecutePhase1Bootstrap(string bootstrapDir)
    {
        Console.WriteLine("[BOOTSTRAP] Phase 1: VM primitive operations...");
        
        var phase1Path = Path.Combine(bootstrapDir, "bootstrap_phase1.json");
        if (!File.Exists(phase1Path))
        {
            throw new FileNotFoundException($"Phase 1 bootstrap file not found: {phase1Path}");
        }
        
        // Clear object memory
        ObjectMemory.Reset();
        Console.WriteLine("[BOOTSTRAP] Cleared VM object memory");
        
        // Parse and execute JSON bootstrap operations
        var jsonContent = File.ReadAllText(phase1Path);
        bootstrapAdapter = new VMPrimitiveAdapter(this);
        var bootstrapExecutor = new JsonBootstrapExecutor(bootstrapAdapter);
        bootstrapExecutor.ExecutePhase1(jsonContent);
        
        Console.WriteLine($"[BOOTSTRAP] Created {ObjectMemory.Objects.Count} objects in VM memory");
        Console.WriteLine("[BOOTSTRAP] Phase 1 complete: VM primitives executed");
        
        // Dump classes after Phase 1
        // DumpAllClasses("PHASE 1 CLASSES");  // Commented out - Phase 1 is solid
    }
    
    /// <summary>
    /// Executes Phase 2 bootstrap from JSON: Message-based class construction.
    /// </summary>
    private void ExecutePhase2Bootstrap(string bootstrapDir)
    {
        Console.WriteLine("[BOOTSTRAP] Phase 2: Message-based class construction...");
        
        var phase2Path = Path.Combine(bootstrapDir, "bootstrap_phase2.json");
        if (!File.Exists(phase2Path))
        {
            Console.WriteLine($"[BOOTSTRAP] Phase 2 file not found at {phase2Path}, skipping Phase 2");
            return;
        }
        
        // Parse and execute Phase 2 bootstrap operations
        var jsonContent = File.ReadAllText(phase2Path);
        if (bootstrapAdapter == null)
        {
            throw new InvalidOperationException("Bootstrap adapter not initialized. Run Phase 1 first.");
        }
        
        var bootstrapExecutor = new JsonBootstrapExecutor(bootstrapAdapter);
        bootstrapExecutor.ExecutePhase2(jsonContent);
        
        Console.WriteLine($"[BOOTSTRAP] Phase 2 complete: Messaging infrastructure created");
        Console.WriteLine($"[BOOTSTRAP] Total objects in VM: {ObjectMemory.Objects.Count}");
        
        // Dump classes after Phase 2
        DumpAllClasses("PHASE 2 CLASSES");
        
        // Now that we have methods installed (including at:put:), migrate to Smalltalk dictionary
        Console.WriteLine("[BOOTSTRAP] Migrating from C# dictionaries to Smalltalk SystemDictionary using message sends...");
        if (bootstrapAdapter is VMPrimitiveAdapter adapter)
        {
            adapter.CompleteBootstrapWithMessageSends();
        }
    }
    
    /// <summary>
    /// Executes Phase 3 bootstrap: Standard library loading or JSON manifest execution.
    /// </summary>
    private void ExecutePhase3Bootstrap(string bootstrapDir)
    {
        Console.WriteLine("[BOOTSTRAP] Phase 3: Message-driven class construction...");
        
        if (bootstrapAdapter == null)
        {
            throw new InvalidOperationException("Bootstrap adapter not initialized. Run Phase 1 first.");
        }
        
        // Always load the main Phase 3 bootstrap file
        var phase3Path = Path.Combine(bootstrapDir, "bootstrap_phase3.json");
        if (File.Exists(phase3Path))
        {
            Console.WriteLine("[BOOTSTRAP] Loading Phase 3 manifest: bootstrap_phase3.json");
            var jsonContent = File.ReadAllText(phase3Path);
            
            var phase3Executor = new Phase3Executor(bootstrapAdapter);
            phase3Executor.ExecuteManifest(jsonContent);
            
            Console.WriteLine("[BOOTSTRAP] Phase 3 manifest executed");
        }
        else
        {
            Console.WriteLine("[BOOTSTRAP] Warning: bootstrap_phase3.json not found");
        }
        
        // Fall back to loading Smalltalk source files
        var libraryDir = Path.GetDirectoryName(bootstrapDir) ?? "";
        var headlessLibraryPath = Path.Combine(libraryDir, "HeadlessStandardLibrary.st");
        var standardLibraryPath = Path.Combine(libraryDir, "StandardLibrary.st");
        
        string libraryPath = "";
        if (File.Exists(headlessLibraryPath))
        {
            libraryPath = headlessLibraryPath;
            Console.WriteLine("[BOOTSTRAP] Using HeadlessStandardLibrary.st");
        }
        else if (File.Exists(standardLibraryPath))
        {
            libraryPath = standardLibraryPath;
            Console.WriteLine("[BOOTSTRAP] Using StandardLibrary.st");
        }
        else
        {
            Console.WriteLine("[BOOTSTRAP] No standard library found - continuing with bootstrap classes only");
            return;
        }
        
        // Load standard library via SmalltalkLoader
        var loader = new SmalltalkLoader(Interpreter);
        var result = loader.LoadFile(libraryPath);
        
        if (result.Success)
        {
            Console.WriteLine($"[BOOTSTRAP] Loaded {result.ClassesLoaded} classes and {result.MethodsLoaded} methods");
        }
        else
        {
            Console.WriteLine($"[BOOTSTRAP] Warning: Failed to load some parts of standard library");
            foreach (var error in result.Errors.Take(3))
            {
                Console.WriteLine($"  Error: {error}");
            }
        }
        
        Console.WriteLine("[BOOTSTRAP] Phase 3 complete: Standard library processed");
    }
    
    /// <summary>
    /// Loads an additional JSON manifest file (Phase 3 format).
    /// </summary>
    public void LoadJsonManifest(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"JSON manifest file not found: {filePath}");
        }
        
        var jsonContent = File.ReadAllText(filePath);
        
        if (bootstrapAdapter == null)
        {
            throw new InvalidOperationException("Bootstrap adapter not initialized. Bootstrap must be run first.");
        }
        
        var phase3Executor = new Phase3Executor(bootstrapAdapter);
        phase3Executor.ExecuteManifest(jsonContent);
        
        Console.WriteLine($"[BOOTSTRAP] Additional JSON manifest executed: {Path.GetFileName(filePath)}");
    }
    
    /// <summary>
    /// Loads a Smalltalk source file.
    /// </summary>
    public void LoadSourceFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File not found: {filePath}");
            return;
        }
        
        try
        {
            // TODO: Implement Smalltalk source file loading
            Console.WriteLine($"[INTERPRETER] Source file loading not yet implemented for: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading {filePath}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Evaluates a Smalltalk expression.
    /// </summary>
    public void EvaluateSource(string sourceCode)
    {
        try
        {
            // Simple evaluation via interpreter
            // This is a basic implementation - a full implementation would need proper parsing and execution
            Console.WriteLine($"Evaluating: {sourceCode}");
            // TODO: Implement proper source code evaluation
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error evaluating '{sourceCode}': {ex.Message}");
        }
    }
    
    /// <summary>
    /// Dumps a symbolic representation of all Smalltalk objects currently in the VM's object memory.
    /// </summary>
    /// <param name="title">Title to display for this dump.</param>
    public void DumpAllClasses(string title)
    {
        Console.WriteLine($"\n=== {title} ===");
        Console.WriteLine($"VM Object Memory Analysis");
        Console.WriteLine($"========================");
        
        var allObjects = ObjectMemory.Objects.ToList();
        
        if (allObjects.Count == 0)
        {
            Console.WriteLine("  No objects found in VM object memory");
            Console.WriteLine($"=== End {title} ===\n");
            return;
        }
        
        Console.WriteLine($"  Total objects in VM: {allObjects.Count}");
        Console.WriteLine();
        
        // Classify objects
        var classObjects = new List<SmalltalkObject>();
        var regularObjects = new List<SmalltalkObject>();
        
        foreach (var obj in allObjects)
        {
            // Check if this object is itself a class (by looking at its structure)
            // Class objects typically have specific patterns
            if (IsLikelyClassObject(obj))
            {
                classObjects.Add(obj);
            }
            else
            {
                regularObjects.Add(obj);
            }
        }
        
        // Dump class objects first
        if (classObjects.Count > 0)
        {
            Console.WriteLine($"📋 Class Objects ({classObjects.Count}):");
            foreach (var classObj in classObjects)
            {
                DumpSmalltalkObject(classObj, "  ", "CLASS");
            }
            Console.WriteLine();
        }
        
        // Dump other objects
        if (regularObjects.Count > 0)
        {
            Console.WriteLine($"📦 Other Objects ({regularObjects.Count}):");
            foreach (var obj in regularObjects)
            {
                DumpSmalltalkObject(obj, "  ", "OBJECT");
            }
            Console.WriteLine();
        }
        
        // Show object relationships
        DumpObjectRelationships(allObjects);
        
        Console.WriteLine($"=== End {title} ===\n");
    }
    
    // DumpClass method removed - no longer needed without shadow classes
    
    // DumpClassHierarchy methods removed - no longer needed without shadow classes
    
    /// <summary>
    /// Determines if a SmalltalkObject is likely a class object based on its structure.
    /// </summary>
    private bool IsLikelyClassObject(SmalltalkObject obj)
    {
        // Class objects typically have specific characteristics:
        // 1. They often have a ClassPointer that points to a metaclass
        // 2. They may have fields that reference method dictionaries, superclasses, etc.
        // 3. The structure varies, but we can make educated guesses
        
        if (obj.ClassPointer == null)
            return false;
            
        // Check if this object's class name contains "class" or "Class" 
        // (this is a heuristic based on common Smalltalk patterns)
        var className = GetObjectClassName(obj);
        if (className?.ToLower().Contains("class") == true)
            return true;
            
        // Check if the object has fields that might indicate it's a class
        // (method dictionary, superclass reference, instance variables, etc.)
        if (obj.Fields.Length >= 3) // Classes typically have multiple fields
        {
            // Look for patterns in field contents that suggest this is a class
            foreach (var field in obj.Fields)
            {
                if (field is SmalltalkObject fieldObj)
                {
                    var fieldClassName = GetObjectClassName(fieldObj);
                    if (fieldClassName?.Contains("Dictionary") == true || 
                        fieldClassName?.Contains("Method") == true ||
                        fieldClassName?.Contains("Symbol") == true)
                    {
                        return true;
                    }
                }
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Gets the class name of a SmalltalkObject by examining its structure.
    /// </summary>
    private string? GetObjectClassName(SmalltalkObject obj)
    {
        if (obj.ClassPointer == null)
            return null;
            
        // Try to look up the class name from the bootstrap adapter's global registry
        if (bootstrapAdapter != null)
        {
            var globals = bootstrapAdapter.GetGlobals();
            foreach (var (name, oop) in globals)
            {
                if (oop == obj.ClassPointer.Oop)
                {
                    // Convert naming convention: "FooClass" -> "Foo class" for metaclasses
                    if (name.EndsWith("Class") && name != "Class" && name != "Metaclass")
                    {
                        var baseName = name.Substring(0, name.Length - 5);
                        return $"{baseName} class";
                    }
                    return name;
                }
            }
        }
            
        // Since we no longer have SystemDictionary, we'll identify classes by their OOP
        // This is a simplified approach - in a full implementation we'd have a name table
        return $"Class#{obj.ClassPointer.Oop}";
    }
    
    /// <summary>
    /// Dumps detailed information about a SmalltalkObject.
    /// </summary>
    private void DumpSmalltalkObject(SmalltalkObject obj, string indent, string objectType)
    {
        // Check if this object is itself a globally named object
        string? objName = null;
        if (bootstrapAdapter != null)
        {
            var globals = bootstrapAdapter.GetGlobals();
            foreach (var (name, globalOop) in globals)
            {
                if (globalOop == obj.Oop)
                {
                    objName = name;
                    break;
                }
            }
        }
        
        // Convert naming convention: "FooClass" -> "Foo class"
        if (objName != null && objName.EndsWith("Class") && objName != "Class" && objName != "Metaclass")
        {
            var baseName = objName.Substring(0, objName.Length - 5);
            objName = $"{baseName} class";
        }
        
        var className = GetObjectClassName(obj) ?? "(unknown class)";
        var oop = obj.Oop;
        var fieldCount = obj.Fields.Length;
        
        // Use objName if available (it's already properly formatted), otherwise show as instance of className
        var displayName = objName ?? $"a {className}";
        Console.WriteLine($"{indent}🔸 {objectType} #{oop}: {displayName} ({fieldCount} fields)");
        
        // Show class pointer info
        if (obj.ClassPointer != null)
        {
            var classInfo = className;
            if (objName != null && className.StartsWith("Class#"))
            {
                // Try to get the actual class name
                classInfo = GetObjectClassName(obj) ?? className;
            }
            Console.WriteLine($"{indent}  📋 Class: {classInfo} @ OOP#{obj.ClassPointer.Oop}");
        }
        else
        {
            Console.WriteLine($"{indent}  ❌ Class: null (no class pointer)");
        }
        
        // Show field contents (limited to avoid overwhelming output)
        if (fieldCount > 0)
        {
            Console.WriteLine($"{indent}  📦 Fields:");
            int maxFields = Math.Min(fieldCount, 5); // Limit to first 5 fields
            
            // Get field names based on the object type
            var fieldNames = GetFieldNamesForObject(obj, objName ?? className);
            
            for (int i = 0; i < maxFields; i++)
            {
                var field = obj.Fields[i];
                string fieldDesc = DescribeFieldValue(field, indent + "    ");
                string fieldName = (fieldNames != null && i < fieldNames.Count) ? fieldNames[i] : "?";
                Console.WriteLine($"{indent}    [{i} - {fieldName}] = {fieldDesc}");
            }
            
            if (fieldCount > maxFields)
            {
                Console.WriteLine($"{indent}    ... and {fieldCount - maxFields} more fields");
            }
        }
    }
    
    /// <summary>
    /// Gets field names for a given object based on its class.
    /// </summary>
    private List<string>? GetFieldNamesForObject(SmalltalkObject obj, string displayName)
    {
        // Check if this object IS a class (has a metaclass as its class)
        bool isClass = false;
        bool isMetaclass = false;
        
        if (obj.ClassPointer != null)
        {
            // Get the name of this object's class
            var classPointerName = GetObjectClassName(obj.ClassPointer);
            
            // Check if the class pointer itself is registered as a metaclass
            if (bootstrapAdapter != null)
            {
                var globals = bootstrapAdapter.GetGlobals();
                foreach (var (name, globalOop) in globals)
                {
                    if (globalOop == obj.ClassPointer.Oop)
                    {
                        // Found the class pointer in globals
                        if (name.EndsWith("Class") && name != "Class" && name != "Metaclass")
                        {
                            // It's a metaclass (e.g., SymbolClass)
                            isClass = true;
                            break;
                        }
                    }
                }
            }
            
            if (!isClass && classPointerName == "Metaclass")
            {
                // This object is a metaclass itself
                isMetaclass = true;
            }
        }
        
        // If it's a class object, return class field names
        if (isClass)
        {
            return new List<string>
            {
                "superclass", "methodDict", "format", "instanceVariables",
                "organization", "name", "classPool", "sharedPools"
            };
        }
        
        // If it's a metaclass object, return metaclass field names
        if (isMetaclass || displayName?.EndsWith(" class") == true)
        {
            return new List<string>
            {
                "superclass", "methodDict", "format", "instanceVariables",
                "organization", "thisClass"
            };
        }
        
        // Otherwise, it's an instance - get field names based on its class
        var className = GetObjectClassName(obj) ?? displayName;
        
        return className switch
        {
            // Instance field names
            "Symbol" => new List<string> { "value" },
            "String" => new List<string> { "characters" },
            "Message" => new List<string> { "selector", "arguments" },
            "CompiledMethod" => new List<string> { "selector", "bytecode", "literals", "header" },
            "MethodContext" => new List<string> { "sender", "receiver", "method", "arguments", "pc", "stackPointer" },
            "MethodDictionary" => new List<string> { "keys", "values" },
            
            // Arrays are variable-sized
            "Array" or "ByteArray" => null,
            
            _ => null
        };
    }
    
    /// <summary>
    /// Creates a descriptive string for a field value.
    /// </summary>
    private string DescribeFieldValue(object? field, string indent)
    {
        if (field == null)
            return "nil";
            
        if (field is SmalltalkObject fieldObj)
        {
            var fieldClassName = GetObjectClassName(fieldObj) ?? "(unknown)";
            
            // Special handling for Symbol and String objects to show their value
            if (fieldClassName == "Symbol" || fieldClassName == "String")
            {
                // Try to get the value from the first field
                if (fieldObj.Fields.Length > 0 && fieldObj.Fields[0] is string value)
                {
                    if (fieldClassName == "Symbol")
                        return $"#{value}";
                    else
                        return $"\"{value}\"";
                }
            }
            
            // Check if this object itself is registered as a class
            if (bootstrapAdapter != null)
            {
                var globals = bootstrapAdapter.GetGlobals();
                foreach (var (name, globalOop) in globals)
                {
                    if (globalOop == fieldObj.Oop)
                    {
                        // Convert naming convention: "FooClass" -> "Foo class"
                        var displayName = name;
                        if (name.EndsWith("Class") && name != "Class" && name != "Metaclass")
                        {
                            var baseName = name.Substring(0, name.Length - 5);
                            displayName = $"{baseName} class";
                        }
                        return $"{displayName} (OOP #{fieldObj.Oop})";
                    }
                }
            }
            
            // For non-global objects, show them more clearly
            // Use "a/an" article for instances
            string article = "aeiouAEIOU".Contains(fieldClassName[0]) ? "an" : "a";
            return $"{article} {fieldClassName} @ #{fieldObj.Oop}";
        }
        
        if (field is string str)
            return $"String: \"{str}\"";
            
        if (field is int intVal)
            return $"Integer: {intVal}";
            
        if (field is bool boolVal)
            return $"Boolean: {boolVal}";
            
        return $"{field.GetType().Name}: {field}";
    }
    
    /// <summary>
    /// Dumps object relationships to show the object graph structure.
    /// </summary>
    private void DumpObjectRelationships(List<SmalltalkObject> allObjects)
    {
        Console.WriteLine("🔗 Object Relationships:");
        
        if (allObjects.Count == 0)
        {
            Console.WriteLine("  No objects to analyze");
            return;
        }
        
        // Count references between objects
        var referenceCount = new Dictionary<int, int>(); // OOP -> count of references to it
        var outgoingRefs = new Dictionary<int, List<int>>(); // OOP -> list of OOPs it references
        
        foreach (var obj in allObjects)
        {
            referenceCount[obj.Oop] = 0;
            outgoingRefs[obj.Oop] = new List<int>();
            
            // Examine each field for references to other objects
            foreach (var field in obj.Fields)
            {
                if (field is SmalltalkObject referencedObj)
                {
                    referenceCount[referencedObj.Oop] = referenceCount.GetValueOrDefault(referencedObj.Oop, 0) + 1;
                    outgoingRefs[obj.Oop].Add(referencedObj.Oop);
                }
            }
        }
        
        // Show objects with high reference counts (likely important objects)
        var highlyReferenced = referenceCount
            .Where(kvp => kvp.Value > 0)
            .OrderByDescending(kvp => kvp.Value)
            .Take(5)
            .ToList();
            
        if (highlyReferenced.Count > 0)
        {
            Console.WriteLine("  📈 Most Referenced Objects:");
            foreach (var (oop, refCount) in highlyReferenced)
            {
                var obj = ObjectMemory.FromOop(oop);
                var className = obj != null ? GetObjectClassName(obj) ?? "(unknown)" : "(invalid)";
                Console.WriteLine($"    #{oop} ({className}): {refCount} references");
            }
        }
        
        // Show objects that reference many others (likely container objects)
        var highConnectivity = outgoingRefs
            .Where(kvp => kvp.Value.Count > 0)
            .OrderByDescending(kvp => kvp.Value.Count)
            .Take(3)
            .ToList();
            
        if (highConnectivity.Count > 0)
        {
            Console.WriteLine("  📤 Objects with Most Outgoing References:");
            foreach (var (oop, refs) in highConnectivity)
            {
                var obj = ObjectMemory.FromOop(oop);
                var className = obj != null ? GetObjectClassName(obj) ?? "(unknown)" : "(invalid)";
                Console.WriteLine($"    #{oop} ({className}): references {refs.Count} objects");
            }
        }
        
        Console.WriteLine($"  📊 Total objects: {allObjects.Count}, Total references: {referenceCount.Values.Sum()}");
    }
    
    // InitializeClasses method removed - no longer needed without shadow classes
    
    /// <summary>
    /// Gets the global variables currently registered in the VM.
    /// </summary>
    public Dictionary<string, SmalltalkObject> GetGlobalVariables()
    {
        var globals = new Dictionary<string, SmalltalkObject>();
        
        // Add known globals from ObjectMemory
        // TODO: Implement global object access once ObjectMemory is updated
        
        // In a full implementation, we would have a proper global dictionary
        // For now, return just the basic globals
        return globals;
    }
}