using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Rebuilds VMMetadata from the Smalltalk dictionary after restoring from a saved image.
///
/// Given only the Smalltalk SystemDictionary OOP (stored in the image header), this class
/// uses RuntimeDictionaryService to find all well-known objects and class OOPs needed by the VM.
///
/// This eliminates the need for a companion metadata file - all VM state is derived from
/// the heap itself, which is the single source of truth.
/// </summary>
public class VMMetadataBuilder
{
    private readonly ISmalltalkHeap _heap;
    private readonly SmalltalkOOP _smalltalkOOP;
    private readonly RuntimeDictionaryService _dictService;

    public VMMetadataBuilder(ISmalltalkHeap heap, SmalltalkOOP smalltalkOOP)
    {
        _heap = heap ?? throw new ArgumentNullException(nameof(heap));
        _smalltalkOOP = smalltalkOOP;
        _dictService = new RuntimeDictionaryService(heap);
    }

    /// <summary>
    /// Builds VMMetadata by looking up well-known objects in the Smalltalk dictionary.
    /// Uses RuntimeDictionaryService for dictionary operations.
    /// </summary>
    public VMMetadata Build()
    {
        Console.WriteLine("[VMMetadataBuilder] Rebuilding VMMetadata from Smalltalk dictionary...");

        // Step 1: Find nil, true, false by locating their classes and finding the sole instance
        // nil is the sole instance of UndefinedObject
        // true is the sole instance of True (subclass of Boolean)
        // false is the sole instance of False (subclass of Boolean)
        var undefinedObjectClassOOP = LookupGlobal("UndefinedObject");
        var trueClassOOP = LookupGlobal("True");
        var falseClassOOP = LookupGlobal("False");

        var nilOOP = FindSoleInstanceOf(undefinedObjectClassOOP, "UndefinedObject");
        var trueOOP = FindSoleInstanceOf(trueClassOOP, "True");
        var falseOOP = FindSoleInstanceOf(falseClassOOP, "False");

        Console.WriteLine($"  nil={nilOOP}, true={trueOOP}, false={falseOOP}");

        // Step 2: Find Processor (ProcessorScheduler instance) and Display - these ARE globals
        var processorOOP = LookupGlobal("Processor");
        var displayOOP = LookupGlobal("Display");
        Console.WriteLine($"  Processor={processorOOP}, Display={displayOOP}");

        // Step 3: Find class OOPs by name
        var classClassOOP = LookupGlobal("Class");
        var metaclassClassOOP = LookupGlobal("Metaclass");
        var symbolClassOOP = LookupGlobal("Symbol");
        var stringClassOOP = LookupGlobal("String");
        var arrayClassOOP = LookupGlobal("Array");
        var byteArrayClassOOP = LookupGlobal("ByteArray");
        var wordArrayClassOOP = LookupGlobal("WordArray");
        var displayBitmapClassOOP = LookupGlobal("DisplayBitmap");
        var compiledMethodClassOOP = LookupGlobal("CompiledMethod");
        var smallIntegerClassOOP = LookupGlobal("SmallInteger");
        var blockContextClassOOP = LookupGlobal("BlockContext");
        var orderedCollectionClassOOP = LookupGlobal("OrderedCollection");
        var processClassOOP = LookupGlobal("Process");
        var pointClassOOP = LookupGlobal("Point");
        var floatClassOOP = LookupGlobal("Float");
        var characterClassOOP = LookupGlobal("Character");

        Console.WriteLine($"  Class={classClassOOP}, Metaclass={metaclassClassOOP}");
        Console.WriteLine($"  Symbol={symbolClassOOP}, String={stringClassOOP}");
        Console.WriteLine($"  BlockContext={blockContextClassOOP}, Process={processClassOOP}");

        // Build the metadata using compile-time field indices from VMConstants
        var metadata = new VMMetadata
        {
            // Field indices are compile-time constants from VMConstants
            ClassNameFieldIndex = VMConstants.CLASS_NAME_INDEX,
            ClassSuperclassFieldIndex = VMConstants.BEHAVIOR_SUPERCLASS_INDEX,
            ClassMethodDictFieldIndex = VMConstants.BEHAVIOR_METHODDICT_INDEX,
            ClassFormatFieldIndex = VMConstants.BEHAVIOR_FORMAT_INDEX,
            MetaclassThisClassFieldIndex = VMConstants.METACLASS_THISCLASS_INDEX,
            MethodDictTallyFieldIndex = VMConstants.METHOD_DICTIONARY_TALLY_INDEX,
            MethodDictKeysFieldIndex = 0, // MethodDictionary stores keys in indexed slots
            MethodDictValuesFieldIndex = VMConstants.METHOD_DICTIONARY_VALUE_ARRAY_INDEX,

            // Class OOPs from heap lookup
            ClassClassOOP = classClassOOP,
            MetaclassClassOOP = metaclassClassOOP,
            SymbolClassOOP = symbolClassOOP,
            StringClassOOP = stringClassOOP,
            ArrayClassOOP = arrayClassOOP,
            ByteArrayClassOOP = byteArrayClassOOP,
            WordArrayClassOOP = wordArrayClassOOP,
            DisplayBitmapClassOOP = displayBitmapClassOOP,
            CompiledMethodClassOOP = compiledMethodClassOOP,
            SmallIntegerClassOOP = smallIntegerClassOOP,
            BlockContextClassOOP = blockContextClassOOP,
            OrderedCollectionClassOOP = orderedCollectionClassOOP,
            ProcessClassOOP = processClassOOP,
            PointClassOOP = pointClassOOP,
            FloatClassOOP = floatClassOOP,
            CharacterClassOOP = characterClassOOP,

            // Known objects
            KnownObjects = new KnownObjects
            {
                Nil = nilOOP,
                True = trueOOP,
                False = falseOOP,
                Smalltalk = _smalltalkOOP,
                Display = displayOOP
            },

            // Special objects
            ProcessorScheduler = processorOOP
        };

        Console.WriteLine("[VMMetadataBuilder] VMMetadata rebuilt successfully.");
        return metadata;
    }

    /// <summary>
    /// Looks up a global variable by name in the Smalltalk dictionary.
    /// Delegates to RuntimeDictionaryService.LookupValueByName.
    /// </summary>
    private SmalltalkOOP LookupGlobal(string globalName)
    {
        var result = _dictService.LookupValueByName(_smalltalkOOP, globalName);

        // RuntimeDictionaryService returns heap.GetNil() if not found
        // For bootstrap, we need to verify the lookup succeeded
        if (result == _heap.GetNil())
        {
            throw new InvalidOperationException($"Global '{globalName}' not found in Smalltalk dictionary during image restore");
        }

        return result;
    }

    /// <summary>
    /// Finds the sole instance of a class by scanning the heap.
    /// Used for nil which is the unique instance of UndefinedObject.
    /// </summary>
    private SmalltalkOOP FindSoleInstanceOf(SmalltalkOOP classOOP, string className)
    {
        foreach (var oop in _heap.EnumerateObjectTable())
        {
            if (_heap.GetClassOOP(oop) == classOOP)
            {
                return oop;
            }
        }

        throw new InvalidOperationException(
            $"No instance of {className} found in heap during image restore");
    }

}
