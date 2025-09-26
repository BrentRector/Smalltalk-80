using System;
using System.Collections.Generic;

namespace Smalltalk80.Interpreter.Bootstrap
{
    /// <summary>
    /// Specification of kernel classes required for bootstrap.
    /// These are the absolute minimal classes needed to bootstrap the system.
    /// </summary>
    public static class KernelSpecification
    {
        /// <summary>
        /// Kernel class definitions in dependency order.
        /// </summary>
        public static readonly List<KernelClass> Classes = new()
        {
            // Phase 1: Core metaclass infrastructure (must be first)
            new("Object", null, Array.Empty<string>()),
            new("Behavior", "Object", new[] { "superclass", "methodDict", "format", "instanceVariables", "organization" }),
            new("ClassDescription", "Behavior", new[] { "name", "classPool", "sharedPools" }),
            new("Metaclass", "ClassDescription", new[] { "thisClass" }),
            new("Class", "ClassDescription", Array.Empty<string>()),
            
            // Phase 2: Method infrastructure
            new("CompiledMethod", "Object", new[] { "selector", "bytecode", "literals", "header" }),
            new("MethodDictionary", "Dictionary", Array.Empty<string>()),
            
            // Phase 3: Collection infrastructure
            new("Collection", "Object", Array.Empty<string>()),
            new("SequenceableCollection", "Collection", Array.Empty<string>()),
            new("ArrayedCollection", "SequenceableCollection", Array.Empty<string>()),
            new("Array", "ArrayedCollection", Array.Empty<string>()), // Variable size
            new("ByteArray", "ArrayedCollection", Array.Empty<string>()), // Variable size
            new("String", "ArrayedCollection", Array.Empty<string>()), // Variable size
            new("Symbol", "String", Array.Empty<string>()), // Variable size
            
            // Phase 4: Dictionary infrastructure
            new("Set", "Collection", new[] { "tally", "array" }),
            new("Dictionary", "Set", Array.Empty<string>()),
            new("SystemDictionary", "Dictionary", Array.Empty<string>()),
            
            // Phase 5: Essential value objects
            new("Magnitude", "Object", Array.Empty<string>()),
            new("Number", "Magnitude", Array.Empty<string>()),
            new("Integer", "Number", Array.Empty<string>()),
            new("SmallInteger", "Integer", Array.Empty<string>()), // No instances - immediate values
            
            // Phase 6: Boolean infrastructure
            new("Boolean", "Object", Array.Empty<string>()),
            new("True", "Boolean", Array.Empty<string>()),
            new("False", "Boolean", Array.Empty<string>()),
            new("UndefinedObject", "Object", Array.Empty<string>()),
            
            // Phase 7: Association for dictionaries
            new("LookupKey", "Magnitude", new[] { "key" }),
            new("Association", "LookupKey", new[] { "value" }),
            
            // Phase 8: Process infrastructure
            new("Link", "Object", new[] { "nextLink" }),
            new("Process", "Link", new[] { "suspendedContext", "priority", "myList" }),
            new("LinkedList", "SequenceableCollection", new[] { "firstLink", "lastLink" }),
            new("Semaphore", "LinkedList", new[] { "excessSignals" }),
            new("ProcessorScheduler", "Object", new[] { "quiescentProcessLists", "activeProcess" }),
            
            // Phase 9: Context classes for execution
            new("InstructionStream", "Object", new[] { "sender", "pc" }),
            new("ContextPart", "InstructionStream", new[] { "stackp", "stack", "method", "tempframe" }),
            new("MethodContext", "ContextPart", new[] { "receiver" }),
            new("BlockContext", "ContextPart", new[] { "nargs", "startpc", "home" }),
            
            // Phase 10: I/O and Transcript classes
            new("Stream", "Object", Array.Empty<string>()),
            new("PositionableStream", "Stream", new[] { "collection", "position", "readLimit" }),
            new("WriteStream", "PositionableStream", new[] { "writeLimit" }),
            new("ReadStream", "PositionableStream", Array.Empty<string>()),
            new("StringHolder", "Object", new[] { "contents", "isLocked" }),
            new("TextCollector", "StringHolder", new[] { "entryStream" }),
            new("Text", "ArrayedCollection", new[] { "string", "runs" })
        };

        /// <summary>
        /// Gets the kernel classes that must be created in Phase 1.
        /// </summary>
        public static IEnumerable<string> GetPhase1Classes()
        {
            // Only the absolute core classes needed to establish the metaclass hierarchy
            yield return "Object";
            yield return "Behavior";
            yield return "ClassDescription";
            yield return "Metaclass";
            yield return "Class";
        }

        /// <summary>
        /// Gets the kernel classes needed for symbols (Phase 4).
        /// </summary>
        public static IEnumerable<string> GetSymbolClasses()
        {
            yield return "Collection";
            yield return "SequenceableCollection";
            yield return "ArrayedCollection";
            yield return "String";
            yield return "Symbol";
        }

        /// <summary>
        /// Gets the kernel classes needed for arrays (Phase 5).
        /// </summary>
        public static IEnumerable<string> GetArrayClasses()
        {
            yield return "Array";
            yield return "ByteArray";
        }

        /// <summary>
        /// Gets the kernel classes needed for dictionaries (Phase 6).
        /// </summary>
        public static IEnumerable<string> GetDictionaryClasses()
        {
            yield return "Set";
            yield return "Dictionary";
            yield return "MethodDictionary";
            yield return "SystemDictionary";
        }
    }

    /// <summary>
    /// Specification for a kernel class.
    /// </summary>
    public sealed class KernelClass
    {
        /// <summary>
        /// Class name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Superclass name (null for Object).
        /// </summary>
        public string? Superclass { get; }

        /// <summary>
        /// Instance variable names.
        /// </summary>
        public string[] InstanceVariables { get; }

        public KernelClass(string name, string? superclass, string[] instanceVariables)
        {
            Name = name;
            Superclass = superclass;
            InstanceVariables = instanceVariables;
        }
    }
}