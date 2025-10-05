namespace Smalltalk80.Interpreter.ColdStart;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Config;
using System.Reflection;
using Smalltalk80.Interpreter.Generated;
using Smalltalk80.Interpreter.Generated.Layouts;
using Smalltalk80.Interpreter.Core.BlueBook;

/// <summary>
/// Main orchestrator for the Smalltalk-80 cold start initialization.
/// Implements the exact 4-pass + 2-phase architecture from docs/Interpreter-Architecture.md sections 8.1-8.5.
///
/// ARCHITECTURE:
/// Phase 1: Create all 128 class objects (64 classes + 64 metaclasses)
///   - Pass 1: Create 6 bootstrap classes
///   - Interlude: Create nil singleton, fix Object.superclass
///   - Pass 2: Create 6 bootstrap metaclasses
///   - Pass 3: Wire Metaclass class.class = Metaclass class (circular)
///   - Pass 4: Create remaining 58 classes + metaclasses
///
/// Phase 2: Create instances and infrastructure
///   - 2.1: Create SystemDictionary instance, register all classes
///   - 2.2: Create singletons (true, false, ProcessorScheduler, ActiveProcess)
///   - 2.3: Install all methods from compiler data
///   - 2.4: Create USTable, migrate symbols using Smalltalk uniqueness
/// </summary>
public static class ColdStartBootstrap
{
    /// <summary>
    /// Cache of all created classes and metaclasses during bootstrap.
    /// Keys are class names (e.g., "Object") and metaclass names (e.g., "Object class").
    /// </summary>
    private static Dictionary<string, SObjectRef> _createdClasses = new();

    /// <summary>
    /// The 6 bootstrap class names that form the core metaclass triangle.
    /// These are the ONLY hardcoded class names - all other data comes from ColdStartData.
    /// </summary>
    private static readonly string[] BootstrapClassNames = new[]
    {
        "Object",
        "UndefinedObject",
        "Behavior",
        "ClassDescription",
        "Class",
        "Metaclass"
    };

    /// <summary>
    /// Main entry point for cold start initialization.
    /// Executes the complete 4-pass + 2-phase bootstrap process.
    /// </summary>
    public static void Initialize()
    {
        LogColdStart("Starting Smalltalk-80 cold start initialization...");
        LogColdStart("Executing 4-pass + 2-phase architecture");

        // Clear any previous state
        _createdClasses.Clear();

        // PHASE 1: Create all 128 class objects
        Phase1_CreateAllClassObjects();

        // PHASE 2: Create instances and infrastructure
        Phase2_CreateInstancesAndInfrastructure();

        LogColdStart("Cold start initialization complete!");
    }

    /// <summary>
    /// PHASE 1: Create all 128 class objects (64 classes + 64 metaclasses).
    /// This phase creates ONLY class objects, no other instances.
    /// </summary>
    private static void Phase1_CreateAllClassObjects()
    {
        LogColdStart("=== PHASE 1: Creating all 128 class objects ===");

        // Pass 1: Create 6 bootstrap classes
        Pass1_CreateBootstrapClasses();

        // Interlude: Fix nil singleton circular dependency
        Interlude_FixNilSingleton();

        // Pass 2: Create 6 bootstrap metaclasses
        Pass2_CreateBootstrapMetaclasses();

        // Pass 3: Wire Metaclass circular reference
        Pass3_WireMetaclassCircularReference();

        // Pass 4: Create remaining 58 classes + metaclasses
        Pass4_CreateRemainingClasses();

        LogColdStart($"Phase 1 complete: Created {_createdClasses.Count} class objects");
    }

    /// <summary>
    /// PASS 1: Create the 6 bootstrap classes that form the metaclass triangle.
    /// These are created WITHOUT metaclasses initially.
    /// </summary>
    private static void Pass1_CreateBootstrapClasses()
    {
        LogColdStart("Pass 1: Creating 6 bootstrap classes...");

        foreach (var className in BootstrapClassNames)
        {
            var metadata = ReadClassMetadata(typeof(ColdStartData.Classes), className);
            if (metadata == null)
            {
                throw new InvalidOperationException($"Bootstrap class {className} not found in ColdStartData");
            }

            // Get superclass from cache (Object uses temporary nil placeholder)
            SObjectRef superclassRef = SObjectRef.Nil;  // Temporary placeholder
            if (!string.IsNullOrEmpty(metadata.SuperclassName) &&
                metadata.SuperclassName != "nil" &&
                _createdClasses.ContainsKey(metadata.SuperclassName))
            {
                superclassRef = _createdClasses[metadata.SuperclassName];
            }

            var format = DetermineObjectFormat(metadata.SubclassKind, className);
            var classRef = ObjectMemory.CreateClassObject(
                className,
                superclassRef,
                metadata.InstanceVariableNames,
                format
            );

            _createdClasses[className] = classRef;
            LogColdStart($"  Created {className} (superclass: {metadata.SuperclassName ?? "nil"})");
        }
    }

    /// <summary>
    /// INTERLUDE: Create nil singleton and fix Object's superclass circular dependency.
    /// This resolves the bootstrap paradox where Object.superclass = nil,
    /// but nil is an instance of UndefinedObject which inherits from Object.
    /// </summary>
    private static void Interlude_FixNilSingleton()
    {
        LogColdStart("Interlude: Creating nil singleton and fixing Object.superclass...");

        // 1. Get UndefinedObject class (created in Pass 1)
        var undefinedObjectClass = _createdClasses["UndefinedObject"];

        // 2. Create nil singleton instance
        var nilObject = ObjectMemory.CreateInstance(
            undefinedObjectClass.AsObjectPointer(),
            0  // No instance variables
        );

        // 3. Store in SpecialObjectsArray - this becomes the canonical reference for all VM code
        SpecialObjectsArray.SetNil(nilObject);

        // 4. Also set in SystemDictionary for backward compatibility
        SystemDictionary.NilObject = nilObject;

        // 5. Fix up Object's superclass to point to real nil singleton
        var objectClass = _createdClasses["Object"];
        STClass objectClassStruct = objectClass;
        objectClassStruct.Superclass = nilObject;

        LogColdStart("  Created nil singleton and fixed Object.superclass");
    }

    /// <summary>
    /// PASS 2: Create metaclasses for the 6 bootstrap classes.
    /// All metaclasses are instances of Metaclass.
    /// </summary>
    private static void Pass2_CreateBootstrapMetaclasses()
    {
        LogColdStart("Pass 2: Creating 6 bootstrap metaclasses...");

        var metaclassClass = _createdClasses["Metaclass"];
        var classClass = _createdClasses["Class"];

        foreach (var className in BootstrapClassNames)
        {
            var classRef = _createdClasses[className];
            var metaclassName = $"{className} class";
            var metadata = ReadClassMetadata(typeof(ColdStartData.Classes), metaclassName);

            // Determine metaclass superclass
            SObjectRef metaclassSuperclass;
            if (className == "Object")
            {
                // Object class's superclass is Class
                metaclassSuperclass = classClass;
            }
            else
            {
                // Get the superclass name from the base class
                var baseClassMetadata = ReadClassMetadata(typeof(ColdStartData.Classes), className);
                if (baseClassMetadata != null && !string.IsNullOrEmpty(baseClassMetadata.SuperclassName))
                {
                    var superMetaclassName = $"{baseClassMetadata.SuperclassName} class";
                    if (_createdClasses.ContainsKey(superMetaclassName))
                    {
                        metaclassSuperclass = _createdClasses[superMetaclassName];
                    }
                    else
                    {
                        // Fallback to Class if metaclass not found yet
                        metaclassSuperclass = classClass;
                    }
                }
                else
                {
                    metaclassSuperclass = classClass;
                }
            }

            // Create metaclass as instance of Metaclass
            // All metaclasses are instances of Metaclass, so they all have the same instance variable count
            var totalInstVars = CalculateTotalInstanceVariableCount("Metaclass");
            var metaclassRef = ObjectMemory.CreateInstance(
                metaclassClass.AsObjectPointer(),
                totalInstVars  // All metaclasses have same inst var count (Behavior:4 + ClassDescription:2 + Metaclass:1 = 7)
            );

            // Set metaclass attributes from compiler data
            SetClassAttributes(metaclassRef, metaclassSuperclass, metadata ?? new ClassMetadata
            {
                ClassName = metaclassName,
                SuperclassName = className == "Object" ? "Class" : $"{GetSuperclassName(className)} class",
                SubclassKind = "subclass:",
                InstanceVariableNames = metadata?.InstanceVariableNames ?? Array.Empty<string>()
            });

            _createdClasses[metaclassName] = metaclassRef;

            // Wire class ↔ metaclass relationship
            WireClassMetaclassRelationship(classRef, metaclassRef);

            LogColdStart($"  Created {metaclassName}");
        }
    }

    /// <summary>
    /// PASS 3: Complete the bootstrap triangle by making Metaclass class.class = Metaclass class.
    /// This creates the circular reference that allows the metaclass system to work.
    /// </summary>
    private static void Pass3_WireMetaclassCircularReference()
    {
        LogColdStart("Pass 3: Wiring Metaclass circular reference...");

        // Metaclass class.class = Metaclass class (points to itself)
        // This is done by updating the ObjectData's ClassRef, not an instance variable
        var metaclassMetaclass = _createdClasses["Metaclass class"];
        var metaclassMetaclassOop = metaclassMetaclass.AsObjectPointer();

        // Get the ObjectData and set its ClassRef to point to itself
        var objectData = ObjectMemory.GetObjectData(metaclassMetaclassOop);
        objectData.ClassRef = metaclassMetaclass;  // Points to itself!

        LogColdStart("  Metaclass class.class now points to itself (circular reference complete)");
    }

    /// <summary>
    /// PASS 4: Create the remaining 58 classes along with their metaclasses.
    /// Each class and its metaclass are created together in dependency order.
    /// </summary>
    private static void Pass4_CreateRemainingClasses()
    {
        LogColdStart("Pass 4: Creating remaining 58 classes + metaclasses...");

        // Get all class metadata from ColdStartData
        var allClassMetadata = GetAllClassMetadata();
        var remainingClasses = allClassMetadata
            .Where(m => !BootstrapClassNames.Contains(m.ClassName) && !m.ClassName.EndsWith(" class"))
            .ToList();

        LogColdStart($"  Found {remainingClasses.Count} remaining classes to create");

        // Sort in dependency order
        var sortedClasses = SortClassesInDependencyOrder(remainingClasses);

        foreach (var metadata in sortedClasses)
        {
            // 1. Create the class
            SObjectRef superclassRef;
            if (string.IsNullOrEmpty(metadata.SuperclassName) || metadata.SuperclassName == "nil")
            {
                superclassRef = SystemDictionary.NilObject;  // Use real nil now!
            }
            else
            {
                if (!_createdClasses.ContainsKey(metadata.SuperclassName))
                {
                    LogColdStart($"  WARNING: Superclass {metadata.SuperclassName} not found for {metadata.ClassName}");
                    superclassRef = _createdClasses["Object"];  // Fallback to Object
                }
                else
                {
                    superclassRef = _createdClasses[metadata.SuperclassName];
                }
            }

            var format = DetermineObjectFormat(metadata.SubclassKind, metadata.ClassName);
            var classRef = ObjectMemory.CreateClassObject(
                metadata.ClassName,
                superclassRef,
                metadata.InstanceVariableNames,
                format
            );
            _createdClasses[metadata.ClassName] = classRef;

            // Set well-known classes for ObjectMemory
            if (metadata.ClassName == "CompiledMethod")
            {
                ObjectMemory.SetWellKnownClass("CompiledMethod", classRef);
            }
            else if (metadata.ClassName == "Symbol")
            {
                ObjectMemory.SetWellKnownClass("Symbol", classRef);
            }

            // 2. Create its metaclass immediately
            var metaclassName = $"{metadata.ClassName} class";
            var metaclassMetadata = allClassMetadata
                .FirstOrDefault(m => m.ClassName == metaclassName);

            var metaclassClass = _createdClasses["Metaclass"];
            // All metaclasses are instances of Metaclass, so they all have the same instance variable count
            var metaclassTotalInstVars = CalculateTotalInstanceVariableCount("Metaclass");
            var metaclassRef = ObjectMemory.CreateInstance(
                metaclassClass.AsObjectPointer(),
                metaclassTotalInstVars  // All metaclasses have same inst var count (Behavior:4 + ClassDescription:2 + Metaclass:1 = 7)
            );

            // Determine metaclass superclass
            SObjectRef metaclassSuperclass;
            if (!string.IsNullOrEmpty(metadata.SuperclassName) && metadata.SuperclassName != "nil")
            {
                var superMetaclassName = $"{metadata.SuperclassName} class";
                if (_createdClasses.ContainsKey(superMetaclassName))
                {
                    metaclassSuperclass = _createdClasses[superMetaclassName];
                }
                else
                {
                    metaclassSuperclass = _createdClasses["Class"];
                }
            }
            else
            {
                metaclassSuperclass = _createdClasses["Class"];
            }

            SetClassAttributes(metaclassRef, metaclassSuperclass, metaclassMetadata ?? new ClassMetadata
            {
                ClassName = metaclassName,
                SuperclassName = GetMetaclassSuperclassName(metadata),
                SubclassKind = "subclass:",
                InstanceVariableNames = Array.Empty<string>()
            });

            _createdClasses[metaclassName] = metaclassRef;

            // 3. Wire class ↔ metaclass relationship
            WireClassMetaclassRelationship(classRef, metaclassRef);

            if ((Program.RuntimeOptions?.EnableColdStartLogging ?? false) &&
                (metadata.ClassName == "Dictionary" || metadata.ClassName == "Array" ||
                 metadata.ClassName == "Symbol" || metadata.ClassName == "Process"))
            {
                LogColdStart($"  Created {metadata.ClassName} and {metaclassName}");
            }
        }

        LogColdStart($"  Created {sortedClasses.Count} classes with their metaclasses");
    }

    /// <summary>
    /// PHASE 2: Create instances and infrastructure.
    /// This phase creates the SystemDictionary instance, singletons, installs methods, and creates USTable.
    /// </summary>
    private static void Phase2_CreateInstancesAndInfrastructure()
    {
        LogColdStart("=== PHASE 2: Creating instances and infrastructure ===");

        // 2.1: Create SystemDictionary instance and register all classes
        Phase2_1_CreateSystemDictionary();

        // 2.2: Create singleton objects
        Phase2_2_CreateSingletons();

        // 2.3: Install all methods from compiler data
        Phase2_3_InstallMethods();

        // 2.4: Create USTable and migrate symbols
        Phase2_4_CreateUSTable();

        LogColdStart("Phase 2 complete: System fully initialized");
    }

    /// <summary>
    /// Phase 2.1: Create SystemDictionary instance and register all 64 classes.
    /// SystemDictionary is created ONLY after all 128 class objects exist.
    /// </summary>
    private static void Phase2_1_CreateSystemDictionary()
    {
        LogColdStart("Phase 2.1: Creating SystemDictionary instance...");

        // Get Dictionary class
        var dictionaryClass = _createdClasses["Dictionary"];

        // DEBUG: Check Dictionary class and superclass chain
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            var dictOop = dictionaryClass.AsObjectPointer();
            var objectData = ObjectMemory.GetObjectData(dictOop);
            Console.WriteLine($"[DEBUG] Dictionary class OOP: {dictOop}");
            Console.WriteLine($"[DEBUG] Dictionary class InstanceVars.Length: {objectData.InstanceVariables.Length}");
            Console.WriteLine($"[DEBUG] Dictionary class Format: {objectData.Format}");

            // Check superclass chain
            var currentClass = dictionaryClass;
            int depth = 0;
            while (!currentClass.IsNil && currentClass.IsHeapObject && depth < 10)
            {
                var currentOop = currentClass.AsObjectPointer();
                var currentData = ObjectMemory.GetObjectData(currentOop);

                // Try to get the name safely
                string nameStr = "unknown";
                try
                {
                    STClass currentClassStruct = currentClass;
                    var nameRef = currentClassStruct.Name;
                    nameStr = nameRef.IsNil ? "nil" : ObjectMemory.SymbolToString(nameRef);
                }
                catch { }

                Console.WriteLine($"[DEBUG]   Superclass chain[{depth}]: {nameStr} (OOP={currentOop}, InstVars={currentData.InstanceVariables.Length})");

                // Get superclass safely
                STClass classStruct = currentClass;
                currentClass = classStruct.Superclass;
                depth++;
            }
        }

        // Create SystemDictionary instance
        SystemDictionary.CreateInstance(dictionaryClass, initialCapacity: 100);

        // Store in SpecialObjectsArray - this becomes the canonical reference
        SpecialObjectsArray.SetSmalltalk(SystemDictionary.Instance);

        // Register all 64 classes (not metaclasses)
        int classCount = 0;
        foreach (var (className, classRef) in _createdClasses)
        {
            if (!className.EndsWith(" class"))  // Only classes, not metaclasses
            {
                var nameSymbol = ObjectMemory.InternSymbolBootstrap(className);
                ObjectMemory.DictionaryAtPut(
                    SystemDictionary.Instance,
                    nameSymbol,
                    classRef
                );
                classCount++;
            }
        }

        // Create "Smalltalk" global variable (self-reference)
        var smalltalkSymbol = ObjectMemory.InternSymbolBootstrap("Smalltalk");
        ObjectMemory.DictionaryAtPut(
            SystemDictionary.Instance,
            smalltalkSymbol,
            SystemDictionary.Instance
        );

        LogColdStart($"  SystemDictionary created with {classCount} classes registered");

        // Initialize well-known class references
        InitializeWellKnownClassRefs();
    }

    /// <summary>
    /// Initializes SystemDictionary's well-known class references.
    /// These provide fast access to commonly-used classes without dictionary lookup.
    /// </summary>
    private static void InitializeWellKnownClassRefs()
    {
        // Use reflection to set the private setters on SystemDictionary
        var type = typeof(SystemDictionary);

        void SetClassRef(string propertyName, string className)
        {
            if (_createdClasses.TryGetValue(className, out var classRef))
            {
                var property = type.GetProperty(propertyName);
                property?.SetValue(null, classRef);
            }
        }

        // Set all well-known class references
        SetClassRef("ObjectClassRef", "Object");
        SetClassRef("BehaviorClassRef", "Behavior");
        SetClassRef("ClassDescriptionClassRef", "ClassDescription");
        SetClassRef("MetaclassClassRef", "Metaclass");
        SetClassRef("ClassClassRef", "Class");
        SetClassRef("CompiledMethodClassRef", "CompiledMethod");
        SetClassRef("InstructionStreamClassRef", "InstructionStream");
        SetClassRef("ContextPartClassRef", "ContextPart");
        SetClassRef("MethodContextClassRef", "MethodContext");
        SetClassRef("BlockContextClassRef", "BlockContext");
        SetClassRef("CollectionClassRef", "Collection");
        SetClassRef("SequenceableCollectionClassRef", "SequenceableCollection");
        SetClassRef("OrderedCollectionClassRef", "OrderedCollection");
        SetClassRef("SetClassRef", "Set");
        SetClassRef("DictionaryClassRef", "Dictionary");
        SetClassRef("IdentityDictionaryClassRef", "IdentityDictionary");
        SetClassRef("MethodDictionaryClassRef", "MethodDictionary");
        SetClassRef("ArrayClassRef", "Array");
        SetClassRef("ByteArrayClassRef", "ByteArray");
        SetClassRef("StringClassRef", "String");
        SetClassRef("SymbolClassRef", "Symbol");
        SetClassRef("CharacterClassRef", "Character");
        SetClassRef("ProcessClassRef", "Process");
        SetClassRef("ProcessorSchedulerClassRef", "ProcessorScheduler");
        SetClassRef("AssociationClassRef", "Association");
        SetClassRef("UndefinedObjectClassRef", "UndefinedObject");
        SetClassRef("TrueClassRef", "True");
        SetClassRef("FalseClassRef", "False");
        SetClassRef("LinkClassRef", "Link");
        SetClassRef("LinkedListClassRef", "LinkedList");
        SetClassRef("SmallIntegerClassRef", "SmallInteger");

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START]   Initialized well-known class references");
        }
    }

    /// <summary>
    /// Phase 2.2: Create singleton objects (true, false, ProcessorScheduler, ActiveProcess).
    /// These are stored in SpecialObjectsArray for GC reachability.
    /// </summary>
    private static void Phase2_2_CreateSingletons()
    {
        LogColdStart("Phase 2.2: Creating singleton objects...");

        // Create true and false singletons
        var trueClass = _createdClasses["True"];
        var falseClass = _createdClasses["False"];
        var trueObject = ObjectMemory.CreateInstance(trueClass.AsObjectPointer(), 0);
        var falseObject = ObjectMemory.CreateInstance(falseClass.AsObjectPointer(), 0);

        // Store in SpecialObjectsArray - these become the canonical references
        SpecialObjectsArray.SetTrue(trueObject);
        SpecialObjectsArray.SetFalse(falseObject);

        // Also set in SystemDictionary for backward compatibility
        SystemDictionary.TrueObject = trueObject;
        SystemDictionary.FalseObject = falseObject;

        // Create ProcessorScheduler singleton
        var schedulerClass = _createdClasses["ProcessorScheduler"];
        var processorScheduler = ObjectMemory.CreateInstance(
            schedulerClass.AsObjectPointer(),
            ObjectMemory.GetClassTotalInstanceVariableCount(schedulerClass.AsObjectPointer())
        );
        SpecialObjectsArray.SetProcessorScheduler(processorScheduler);

        // Create initial Process instance (ActiveProcess)
        var processClass = _createdClasses["Process"];
        var activeProcess = ObjectMemory.CreateInstance(
            processClass.AsObjectPointer(),
            ObjectMemory.GetClassTotalInstanceVariableCount(processClass.AsObjectPointer())
        );

        // Initialize Process fields according to Blue Book specification
        // The initial activeProcess represents the running system and has specific field values
        var process = new STProcess(activeProcess);

        // NextLink: nil (inherited from Link - not linked to another process)
        process.NextLink = SObjectRef.Nil;

        // SuspendedContext: nil (this is the currently running process, not suspended)
        process.SuspendedContext = SObjectRef.Nil;

        // Priority: SmallInteger 4 (UserSchedulingPriority per Blue Book)
        // Priority levels in Smalltalk-80: 1-3 system, 4 user, 5-8 background
        process.Priority = SObjectRef.FromSmallInteger(4);

        // MyList: nil (not on any waiting list since it's the active process)
        process.MyList = SObjectRef.Nil;

        // CRITICAL: Link activeProcess to ProcessorScheduler to prevent GC collection
        // The Process must be reachable via: SOA[4] -> ProcessorScheduler -> activeProcess
        // Using the generated STProcessorScheduler layout for type-safe access
        var scheduler = new STProcessorScheduler(processorScheduler);
        scheduler.ActiveProcess = activeProcess;

        // Create quiescentProcessLists - Array of 8 LinkedLists (one per priority level)
        // This is required by Blue Book specification for ProcessorScheduler
        var arrayClass = _createdClasses["Array"];
        var linkedListClass = _createdClasses["LinkedList"];

        // Create the array with 8 slots for priority levels
        var quiescentLists = BlueBookFactory.CreateArray(arrayClass, 8);

        // Initialize each priority list as an empty LinkedList
        for (var i = 0; i < 8; i++)
        {
            var linkedList = ObjectMemory.CreateInstance(
                linkedListClass.AsObjectPointer(),
                ObjectMemory.GetClassTotalInstanceVariableCount(linkedListClass.AsObjectPointer())
            );

            // Initialize LinkedList with nil firstLink and lastLink (empty list)
            var list = new STLinkedList(linkedList);
            list.FirstLink = SObjectRef.Nil;
            list.LastLink = SObjectRef.Nil;

            // Store in the array
            quiescentLists.SetIndexedPointer(i, linkedList);
        }

        // Link quiescentProcessLists to ProcessorScheduler
        scheduler.QuiescentProcessLists = quiescentLists;

        LogColdStart("  Created singletons: nil, true, false, ProcessorScheduler, ActiveProcess");
        LogColdStart("  ProcessorScheduler properly linked: quiescentProcessLists (Array[8] of LinkedLists) and activeProcess");
    }

    /// <summary>
    /// Phase 2.3: Install all compiled methods from compiler-generated data.
    /// This uses the existing ColdStartMethodInstaller which handles method creation.
    /// </summary>
    private static void Phase2_3_InstallMethods()
    {
        LogColdStart("Phase 2.3: Installing all methods from compiler data...");

        // Use existing method installer (preserves all functionality)
        ColdStartMethodInstaller.InstallAllMethods();

        LogColdStart("  All methods installed successfully");
    }

    /// <summary>
    /// Phase 2.4: Create USTable and migrate all symbols to use Smalltalk uniqueness.
    /// This is deferred until after method installation to ensure all symbols exist.
    /// </summary>
    private static void Phase2_4_CreateUSTable()
    {
        LogColdStart("Phase 2.4: Creating USTable and migrating symbols...");

        // Create and install USTable
        SymbolTableBuilder.CreateAndInstallUSTable();

        // Migrate all bootstrap symbols to USTable using Smalltalk uniqueness
        ObjectMemory.MigrateBootstrapSymbolsToUSTable();

        // Store USTable in Smalltalk dictionary at #SymbolTable
        var usTableRef = SymbolTableBuilder.GetUSTable();
        if (usTableRef.HasValue && !usTableRef.Value.IsNil)
        {
            var symbolTableKey = ObjectMemory.InternSymbol("SymbolTable");
            ObjectMemory.DictionaryAtPut(
                SystemDictionary.Instance,
                symbolTableKey,
                usTableRef.Value
            );
            LogColdStart("  USTable created and stored at Smalltalk at: #SymbolTable");
        }

        LogColdStart("  All symbols migrated to USTable with Smalltalk uniqueness");
    }

    // ==================== HELPER METHODS ====================

    /// <summary>
    /// Calculates the TOTAL instance variable count for a class by walking its superclass chain.
    /// This is critical during bootstrap when the class objects don't exist yet.
    /// </summary>
    private static int CalculateTotalInstanceVariableCount(string className)
    {
        int total = 0;
        var currentClassName = className;

        while (!string.IsNullOrEmpty(currentClassName))
        {
            var metadata = ReadClassMetadata(typeof(ColdStartData.Classes), currentClassName);
            if (metadata == null)
            {
                break;
            }

            total += metadata.InstanceVariableNames.Length;
            currentClassName = metadata.SuperclassName;

            // Stop at nil (Object's superclass)
            if (currentClassName == null) break;
        }

        return total;
    }

    /// <summary>
    /// Reads class metadata from a ColdStartData.Classes nested type using reflection.
    /// All class structure data comes from the compiler-generated ColdStartData.
    /// </summary>
    private static ClassMetadata? ReadClassMetadata(Type parentType, string className)
    {
        try
        {
            // Handle metaclass names by looking for "ClassName_class" in ColdStartData
            var searchName = className.Replace(" class", "_class");

            var classType = parentType.GetNestedType(searchName, BindingFlags.Public | BindingFlags.Static);
            if (classType == null)
            {
                // Try without replacement for regular classes
                classType = parentType.GetNestedType(className, BindingFlags.Public | BindingFlags.Static);
            }

            if (classType == null) return null;

            var classNameField = (string?)classType.GetField("ClassName")?.GetValue(null);
            if (classNameField == null) return null;

            var superclassName = (string?)classType.GetField("SuperclassName")?.GetValue(null);
            var subclassKind = (string?)classType.GetField("SubclassKind")?.GetValue(null) ?? "subclass:";
            var instanceVarNames = (string[]?)classType.GetField("InstanceVariableNames")?.GetValue(null) ?? Array.Empty<string>();

            return new ClassMetadata
            {
                ClassName = className,  // Use the requested name, not the field value
                SuperclassName = superclassName,
                SubclassKind = subclassKind,
                InstanceVariableNames = instanceVarNames
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets all class metadata from ColdStartData.Classes.
    /// </summary>
    private static List<ClassMetadata> GetAllClassMetadata()
    {
        var classesType = typeof(ColdStartData.Classes);
        var allClassTypes = classesType.GetNestedTypes(BindingFlags.Public | BindingFlags.Static);
        var metadata = new List<ClassMetadata>();

        foreach (var classType in allClassTypes)
        {
            var className = (string?)classType.GetField("ClassName")?.GetValue(null);
            if (className == null) continue;

            // Handle metaclass names
            if (classType.Name.EndsWith("_class"))
            {
                className = className.Replace("_class", " class");
            }

            var superclassName = (string?)classType.GetField("SuperclassName")?.GetValue(null);
            var subclassKind = (string?)classType.GetField("SubclassKind")?.GetValue(null) ?? "subclass:";
            var instanceVarNames = (string[]?)classType.GetField("InstanceVariableNames")?.GetValue(null) ?? Array.Empty<string>();

            metadata.Add(new ClassMetadata
            {
                ClassName = className,
                SuperclassName = superclassName,
                SubclassKind = subclassKind,
                InstanceVariableNames = instanceVarNames
            });
        }

        return metadata;
    }

    /// <summary>
    /// Sorts classes in dependency order (superclasses before subclasses).
    /// </summary>
    private static List<ClassMetadata> SortClassesInDependencyOrder(List<ClassMetadata> classes)
    {
        var sorted = new List<ClassMetadata>();
        var visited = new HashSet<string>();
        var classMap = classes.ToDictionary(c => c.ClassName);

        void Visit(ClassMetadata metadata)
        {
            if (visited.Contains(metadata.ClassName))
                return;

            // Visit superclass first
            if (!string.IsNullOrEmpty(metadata.SuperclassName) &&
                metadata.SuperclassName != "nil" &&
                classMap.ContainsKey(metadata.SuperclassName))
            {
                Visit(classMap[metadata.SuperclassName]);
            }

            visited.Add(metadata.ClassName);
            sorted.Add(metadata);
        }

        foreach (var metadata in classes)
        {
            Visit(metadata);
        }

        return sorted;
    }

    /// <summary>
    /// Sets the attributes of a class object from metadata.
    /// Used for both classes and metaclasses.
    /// </summary>
    private static void SetClassAttributes(SObjectRef classRef, SObjectRef superclassRef, ClassMetadata metadata)
    {
        var classOop = classRef.AsObjectPointer();

        // Set superclass (index 0 - from Behavior)
        STClass classStruct = SObjectRef.FromObjectPointer(classOop);
        classStruct.Superclass = superclassRef;

        // Initialize method dictionary (index 1 - from Behavior) - CRITICAL for method installation
        // Create method dictionary even during early bootstrap (before MethodDictionary class exists)
        var methodDictClass = _createdClasses.ContainsKey("MethodDictionary") ?
            _createdClasses["MethodDictionary"] : SObjectRef.Nil;
        var arrayClass = _createdClasses.ContainsKey("Array") ?
            _createdClasses["Array"] : SObjectRef.Nil;

        int capacity = 64; // Reasonable initial capacity
        var methodDict = ObjectMemory.AllocateObject(
            methodDictClass,  // May be nil during bootstrap - that's OK
            ObjectFormat.PointerIndexed,
            2,  // MethodDictionary has 2 instance variables: tally and valueArray
            capacity);  // indexed size for method storage

        // Initialize tally to 0
        ObjectMemory.SetInstanceVariable(methodDict, 0, SObjectRef.FromSmallInteger(0));

        // Create valueArray for method storage
        var valueArray = ObjectMemory.AllocateObject(
            arrayClass,  // May be nil during bootstrap - that's OK
            ObjectFormat.PointerIndexed,
            0,  // Array has no instance variables
            capacity);
        ObjectMemory.SetInstanceVariable(methodDict, 1, SObjectRef.FromObjectPointer(valueArray));

        // Set method dictionary in the class/metaclass
        classStruct.MethodDict = SObjectRef.FromObjectPointer(methodDict);

        // Set format (index 2 - from Behavior)
        var format = DetermineObjectFormat(metadata.SubclassKind, metadata.ClassName);
        classStruct.Format = SObjectRef.FromSmallInteger((int)format);

        // Set instance variable names (index 4 - from ClassDescription)
        if (metadata.InstanceVariableNames.Length > 0)
        {
            // Reuse arrayClass declared earlier in the method
            if (!arrayClass.IsNil)
            {
                var instVarsArray = ObjectMemory.CreateInstance(
                    arrayClass.AsObjectPointer(),
                    metadata.InstanceVariableNames.Length);  // indexedSize

                for (int i = 0; i < metadata.InstanceVariableNames.Length; i++)
                {
                    var symbol = ObjectMemory.InternSymbolBootstrap(metadata.InstanceVariableNames[i]);
                    instVarsArray.SetIndexedPointer(i, symbol);
                }

                classStruct.InstanceVariables = instVarsArray;
            }
        }

        // Set class name (index 6 - ONLY for Class objects, NOT for Metaclass!)
        // Metaclasses use index 6 for thisClass, not name
        // Only set name if this is NOT a metaclass (metaclass names end with " class")
        if (!metadata.ClassName.EndsWith(" class"))
        {
            var nameSymbol = ObjectMemory.InternSymbolBootstrap(metadata.ClassName);
            classStruct.Name = nameSymbol;
        }
    }

    /// <summary>
    /// Wires the bidirectional relationship between a class and its metaclass.
    /// class.class = metaclass, metaclass.thisClass = class
    /// </summary>
    private static void WireClassMetaclassRelationship(SObjectRef classRef, SObjectRef metaclassRef)
    {
        // Set class.class = metaclass (by setting the ObjectData's ClassRef)
        var classData = ObjectMemory.GetObjectData(classRef.AsObjectPointer());
        classData.ClassRef = metaclassRef;

        // Set metaclass.thisClass = class (this IS an instance variable)
        STMetaclass metaclassStruct = metaclassRef;
        metaclassStruct.ThisClass = classRef;
    }

    /// <summary>
    /// Gets the superclass name for a given class name.
    /// </summary>
    private static string GetSuperclassName(string className)
    {
        var metadata = ReadClassMetadata(typeof(ColdStartData.Classes), className);
        return metadata?.SuperclassName ?? "Object";
    }

    /// <summary>
    /// Gets the metaclass superclass name for a given class metadata.
    /// </summary>
    private static string GetMetaclassSuperclassName(ClassMetadata metadata)
    {
        if (string.IsNullOrEmpty(metadata.SuperclassName) || metadata.SuperclassName == "nil")
        {
            return "Class";
        }
        return $"{metadata.SuperclassName} class";
    }

    /// <summary>
    /// Determines ObjectFormat from Smalltalk subclass kind.
    /// </summary>
    private static ObjectFormat DetermineObjectFormat(string subclassKind, string className)
    {
        // Special case for CompiledMethod
        if (className == "CompiledMethod")
        {
            return ObjectFormat.CompiledMethod;
        }

        return subclassKind switch
        {
            "subclass:" => ObjectFormat.FixedPointer,
            "variableSubclass:" => ObjectFormat.PointerIndexed,
            "variableByteSubclass:" => ObjectFormat.ByteIndexed,
            "variableWordSubclass:" => ObjectFormat.WordIndexed,
            _ => ObjectFormat.FixedPointer
        };
    }

    /// <summary>
    /// Logs a cold start message if logging is enabled.
    /// </summary>
    private static void LogColdStart(string message)
    {
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] {message}");
        }
    }

    /// <summary>
    /// Metadata for a single class from ColdStartData.
    /// </summary>
    private class ClassMetadata
    {
        public required string ClassName { get; init; }
        public string? SuperclassName { get; init; }
        public required string SubclassKind { get; init; }
        public required string[] InstanceVariableNames { get; init; }
    }

    // ==================== POST-COLD-START INITIALIZATION ====================

    /// <summary>
    /// Performs post-cold-start initialization tasks.
    /// This includes creating test classes, installing compilation methods, etc.
    /// Called after the main cold start is complete.
    /// </summary>
    public static void PostColdStartInitialization()
    {
        // NOTE: Post-cold-start initialization creates temporary objects that become garbage
        // Disabling all post-cold-start initialization to achieve zero garbage collection

        // InitializeTranscript(); // TextCollector should NOT be in cold start
        // ProcessSystemInitializer.Initialize(); // Creates temporary Process objects
        // CompilationMethodsInstaller.InstallAll(); // Creates compilation-related methods
        // TestClassBuilder.CreateTestClass(); // Creates test methods
    }

    // REMOVED: InitializeTranscript() and InstallTranscriptMethods()
    // All methods must come from compiler-generated cold start data
    // TextCollector should NOT be in cold start classes
}