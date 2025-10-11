using Smalltalk80.Interpreter.Schema;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.LoaderPhases;

/// <summary>
/// Phase 2: Wire Metaclass Relationships
/// Establishes bidirectional links between classes and their metaclasses.
/// Sets the 'class' slot on instance-side classes and 'metaclassOf' on metaclasses.
/// </summary>
public class Phase2_MetaclassWiring
{
    private readonly ColdStartBundle _bundle;
    private readonly ISmalltalkHeap _heap;
    private readonly Dictionary<string, int> _localIdToObjectId;

    public Phase2_MetaclassWiring(ColdStartBundle bundle, ISmalltalkHeap heap, Dictionary<string, int> localIdMap)
    {
        _bundle = bundle;
        _heap = heap;
        _localIdToObjectId = localIdMap;
    }

    public void Run()
    {
        int count = 0;
        int actionMenuOop = 0;
        int actionMenuMetaOop = 0;

        foreach (var cls in _bundle.Classes.Where(c => c.IsMetaclass))
        {
            if (cls.MetaclassOfLocalId is string instanceId)
            {
                int metaId = _localIdToObjectId[cls.LocalId];
                int instId = _localIdToObjectId[instanceId];

                // Debug: capture ActionMenu IDs
                if (instanceId == "class:ActionMenu")
                {
                    actionMenuOop = instId;
                    actionMenuMetaOop = metaId;
                    Console.WriteLine($"DEBUG Phase3 START: ActionMenu OOP={instId}, meta OOP={metaId}");
                }

                // Wire bidirectional metaclass relationship
                _heap.SetNamedSlot(instId, "class", metaId);
                _heap.SetNamedSlot(metaId, "metaclassOf", instId);

                // CRITICAL: Update the actual classId field so GetClass() returns the correct metaclass
                _heap.SetClass(instId, metaId);
                count++;

                // Debug: verify immediately for ActionMenu
                if (instanceId == "class:ActionMenu")
                {
                    var heap = (SmalltalkHeap)_heap;
                    int verifyClassId = heap.GetClass(instId);
                    Console.WriteLine($"DEBUG Phase3 AFTER SetClass: ActionMenu classId={verifyClassId}");
                    if (verifyClassId != metaId)
                    {
                        throw new InvalidOperationException($"SetClass FAILED! Expected {metaId}, got {verifyClassId}");
                    }
                }
            }
        }

        // Debug: verify SetClass was called
        if (count == 0)
        {
            throw new InvalidOperationException("Phase 3: No metaclass relationships were wired! This is a critical bug.");
        }

        // Final check at end of Phase 3
        if (actionMenuOop != 0)
        {
            var heap = (SmalltalkHeap)_heap;
            int finalClassId = heap.GetClass(actionMenuOop);
            Console.WriteLine($"DEBUG Phase3 END: ActionMenu OOP={actionMenuOop}, classId={finalClassId}");

            // Also check the Association
            int assocOop = 7842;
            int assocValue = heap.GetField(assocOop, 1);
            Console.WriteLine($"DEBUG Phase3 END: ActionMenu Association[1 (value)]={assocValue}");
        }

        // CRITICAL: Wire the fundamental metaclass hierarchy
        // This must happen BEFORE computing metaclass superclasses
        WireFundamentalMetaclassHierarchy();

        // CRITICAL: Compute and set metaclass superclass chain
        // For each metaclass, if it has no explicit superclass, set it to the metaclass of
        // its instance-side class's superclass
        WireMetaclassSuperclasses();
    }

    /// <summary>
    /// Wires the fundamental metaclass inheritance chain per Blue Book specification.
    /// This creates the "metaclass circle" that allows ClassDescription methods to be
    /// inherited by all metaclasses.
    ///
    /// Hierarchy:
    ///   meta:Object (Object class) → superclass = class:Class
    ///   class:Class → superclass = class:ClassDescription
    ///   class:ClassDescription → superclass = class:Object
    ///
    /// This allows class-side methods like comment: (defined in ClassDescription) to be
    /// available on all classes through inheritance.
    /// </summary>
    private void WireFundamentalMetaclassHierarchy()
    {
        // Get OOPs for fundamental classes
        if (!_localIdToObjectId.TryGetValue("meta:Object", out int metaObjectOop))
        {
            Console.WriteLine("WARNING: meta:Object not found, skipping fundamental hierarchy wiring");
            return;
        }

        if (!_localIdToObjectId.TryGetValue("class:Class", out int classClassOop))
        {
            Console.WriteLine("WARNING: class:Class not found, skipping fundamental hierarchy wiring");
            return;
        }

        if (!_localIdToObjectId.TryGetValue("class:ClassDescription", out int classDescriptionOop))
        {
            Console.WriteLine("WARNING: class:ClassDescription not found, skipping fundamental hierarchy wiring");
            return;
        }

        if (!_localIdToObjectId.TryGetValue("class:Object", out int classObjectOop))
        {
            Console.WriteLine("WARNING: class:Object not found, skipping fundamental hierarchy wiring");
            return;
        }

        // Wire the metaclass circle
        _heap.SetNamedSlot(metaObjectOop, "superclass", classClassOop);
        _heap.SetNamedSlot(classClassOop, "superclass", classDescriptionOop);
        _heap.SetNamedSlot(classDescriptionOop, "superclass", classObjectOop);

        Console.WriteLine($"DEBUG: Wired fundamental metaclass hierarchy:");
        Console.WriteLine($"  meta:Object ({metaObjectOop}) → superclass = class:Class ({classClassOop})");
        Console.WriteLine($"  class:Class ({classClassOop}) → superclass = class:ClassDescription ({classDescriptionOop})");
        Console.WriteLine($"  class:ClassDescription ({classDescriptionOop}) → superclass = class:Object ({classObjectOop})");
    }

    /// <summary>
    /// Computes and sets the superclass for each metaclass based on its instance-side class's superclass.
    ///
    /// Rule: If metaclass M is the class of instance class I, and I has superclass S,
    /// then M's superclass is the metaclass of S.
    ///
    /// Example:
    ///   ActionMenu (instance class) has superclass PopUpMenu
    ///   ActionMenu class (metaclass) should have superclass PopUpMenu class
    /// </summary>
    private void WireMetaclassSuperclasses()
    {
        int wiredCount = 0;
        var symbols = _bundle.Symbols;

        foreach (var metaCls in _bundle.Classes.Where(c => c.IsMetaclass))
        {
            if (metaCls.MetaclassOfLocalId is not string instanceLocalId)
                continue;

            // Find the instance-side class descriptor
            var instanceCls = _bundle.Classes.FirstOrDefault(c => c.LocalId == instanceLocalId);
            if (instanceCls == null)
                continue;

            // If instance class has a superclass, set metaclass's superclass to superclass's metaclass
            if (instanceCls.SuperNameIndex is int superNameIdx)
            {
                string superName = symbols[superNameIdx];

                // Find the superclass's metaclass localId
                string superMetaLocalId = $"meta:{superName}";

                if (_localIdToObjectId.TryGetValue(metaCls.LocalId, out int metaOop) &&
                    _localIdToObjectId.TryGetValue(superMetaLocalId, out int superMetaOop))
                {
                    _heap.SetNamedSlot(metaOop, "superclass", superMetaOop);
                    wiredCount++;

                    // Debug: trace PopUpMenu class hierarchy
                    if (metaCls.LocalId == "meta:PopUpMenu" || metaCls.LocalId == "meta:ActionMenu")
                    {
                        Console.WriteLine($"DEBUG: Wired {metaCls.LocalId} ({metaOop}) → superclass = {superMetaLocalId} ({superMetaOop})");
                    }
                }
            }
        }

        Console.WriteLine($"DEBUG: Wired {wiredCount} metaclass superclass relationships");
    }
}
