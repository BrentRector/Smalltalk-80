using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Generated;
using Smalltalk80.Interpreter.Memory;
using System.Reflection;

namespace Smalltalk80.Interpreter.ColdStart;

/// <summary>
/// Creates all classes from compiler-generated ColdStartData.
/// This is the ONLY class creator - all class structure comes from the compiler's analysis
/// of the Smalltalk-80.st source file.
/// </summary>
public static class ColdStartClassCreator
{
    private static Dictionary<string, SObjectRef> _createdClasses = new();

    /// <summary>
    /// Creates all classes from ColdStartData in proper dependency order.
    /// </summary>
    public static void CreateAllClasses()
    {
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[ColdStartClassCreator] Creating {ColdStartData.ClassCount} classes from compiler-generated data");
        }

        // Get all class metadata from ColdStartData.Classes
        var classesType = typeof(ColdStartData.Classes);
        var allClassTypes = classesType.GetNestedTypes(BindingFlags.Public | BindingFlags.Static);

        // Build class metadata map
        var classMetadata = new Dictionary<string, ClassMetadata>();
        foreach (var classType in allClassTypes)
        {
            var metadata = ReadClassMetadata(classType);
            if (metadata != null)
            {
                classMetadata[metadata.ClassName] = metadata;
            }
        }

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[ColdStartClassCreator] Read metadata for {classMetadata.Count} classes");
        }

        // Create classes in dependency order (superclasses before subclasses)
        var created = new HashSet<string>();
        foreach (var metadata in classMetadata.Values)
        {
            CreateClassWithDependencies(metadata, classMetadata, created);
        }

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[ColdStartClassCreator] Successfully created all {created.Count} classes");

            // Verify classes are in SystemDictionary
            var processScheduler = SystemDictionary.LookupClassRef("ProcessorScheduler");
            if (processScheduler.HasValue)
            {
                Console.WriteLine($"[ColdStartClassCreator] ProcessorScheduler found in SystemDictionary");
            }
            else
            {
                Console.WriteLine($"[ColdStartClassCreator] ERROR: ProcessorScheduler NOT found in SystemDictionary!");
            }
        }
    }

    /// <summary>
    /// Reads class metadata from a ColdStartData.Classes nested type.
    /// </summary>
    private static ClassMetadata? ReadClassMetadata(Type classType)
    {
        try
        {
            var className = (string?)classType.GetField("ClassName")?.GetValue(null);
            if (className == null) return null;

            var superclassName = (string?)classType.GetField("SuperclassName")?.GetValue(null);
            var subclassKind = (string?)classType.GetField("SubclassKind")?.GetValue(null) ?? "subclass:";
            var instanceVarNames = (string[]?)classType.GetField("InstanceVariableNames")?.GetValue(null) ?? Array.Empty<string>();

            return new ClassMetadata
            {
                ClassName = className,
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
    /// Creates a class and all its dependencies (superclasses) recursively.
    /// </summary>
    private static void CreateClassWithDependencies(
        ClassMetadata metadata,
        Dictionary<string, ClassMetadata> allMetadata,
        HashSet<string> created)
    {
        // Already created?
        if (created.Contains(metadata.ClassName))
        {
            return;
        }

        // Create superclass first (if it exists and hasn't been created)
        if (!string.IsNullOrEmpty(metadata.SuperclassName) &&
            metadata.SuperclassName != "nil" &&
            allMetadata.ContainsKey(metadata.SuperclassName))
        {
            CreateClassWithDependencies(allMetadata[metadata.SuperclassName], allMetadata, created);
        }

        // Now create this class
        CreateClass(metadata);
        created.Add(metadata.ClassName);

        if ((Program.RuntimeOptions?.EnableColdStartLogging ?? false) &&
            (metadata.ClassName == "Object" || metadata.ClassName == "Class" || metadata.ClassName == "Array"))
        {
            Console.WriteLine($"[ColdStartClassCreator] Created {metadata.ClassName} (superclass: {metadata.SuperclassName ?? "nil"}, format: {metadata.SubclassKind})");
        }
    }

    /// <summary>
    /// Creates a single class.
    /// </summary>
    private static void CreateClass(ClassMetadata metadata)
    {
        // Get superclass reference
        SObjectRef superclassRef;
        if (string.IsNullOrEmpty(metadata.SuperclassName) || metadata.SuperclassName == "nil")
        {
            superclassRef = SObjectRef.Nil;
        }
        else
        {
            // First try temporary cache, then SystemDictionary
            if (_createdClasses.TryGetValue(metadata.SuperclassName, out var cached))
            {
                superclassRef = cached;
            }
            else
            {
                var superclassRefNullable = SystemDictionary.LookupClassRef(metadata.SuperclassName);
                if (!superclassRefNullable.HasValue || superclassRefNullable.Value.IsNil)
                {
                    Console.Error.WriteLine($"[ColdStartClassCreator] ERROR: Superclass {metadata.SuperclassName} not found for {metadata.ClassName}");
                    superclassRef = SObjectRef.Nil;
                }
                else
                {
                    superclassRef = superclassRefNullable.Value;
                }
            }
        }

        // Determine ObjectFormat from subclass kind
        var format = DetermineObjectFormat(metadata.SubclassKind, metadata.ClassName);

        // Create the class
        var classRef = ObjectMemoryNew.CreateClassObject(
            metadata.ClassName,
            superclassRef,
            metadata.InstanceVariableNames,
            format
        );

        // Cache it temporarily
        _createdClasses[metadata.ClassName] = classRef;

        // Set well-known classes for ObjectMemory
        if (metadata.ClassName == "CompiledMethod")
        {
            ObjectMemoryNew.SetWellKnownClass("CompiledMethod", classRef);
        }
        else if (metadata.ClassName == "Symbol")
        {
            ObjectMemoryNew.SetWellKnownClass("Symbol", classRef);
        }

        // Create SystemDictionary instance when Dictionary class is created
        if (metadata.ClassName == "Dictionary")
        {
            if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
            {
                Console.WriteLine("[ColdStartClassCreator] Creating SystemDictionary instance");
            }
            SystemDictionary.CreateInstance(classRef, initialCapacity: 100);

            // Now register all previously created classes into the Instance
            if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
            {
                Console.WriteLine($"[ColdStartClassCreator] Registering {_createdClasses.Count} previously created classes");
            }
            foreach (var (name, cref) in _createdClasses)
            {
                SystemDictionary.RegisterClassRef(name, cref);
            }
        }
        else
        {
            // Register in SystemDictionary (if Instance exists)
            SystemDictionary.RegisterClassRef(metadata.ClassName, classRef);
        }
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
    /// Metadata for a single class.
    /// </summary>
    private class ClassMetadata
    {
        public required string ClassName { get; init; }
        public string? SuperclassName { get; init; }
        public required string SubclassKind { get; init; }
        public required string[] InstanceVariableNames { get; init; }
    }
}
