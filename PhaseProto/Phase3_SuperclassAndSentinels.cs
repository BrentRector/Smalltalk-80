// Phase3_SuperclassAndSentinels.cs
// Responsibility:
//  - Set metaclass.thisClass for every metaclass
//  - Wire the fundamental metaclass circle (meta:Object → class:Class → class:ClassDescription → class:Behavior → class:Object)
//  - Wire regular metaclass.superclass = meta:SuperClass (mirror instance-side inheritance)
//  - Set sentinel class slots (nil.class, true.class, false.class) now that class objects exist
//  - Record initializedFields entries for fields set in this phase
//
// Assumptions:
//  - Phase2 produced LocalIdToObjectId mapping and allocated sentinels (LocalIdToObjectId contains class localIds and "nil"/"true"/"false")
//  - IHeapManager supports SetInstanceVariable(oop, index, value) and GetInstanceVariableCount/GetTotalSlotCount/GetSlot
//  - LayoutResolver is frozen and can answer GetFieldIndex(classLocalId, fieldName)
//  - InitializedFields is a HashSet<(SmalltalkOOP, int)> passed from caller for tracking explicit writes

using System;
using System.Collections.Generic;
using System.Linq;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases
{
    public sealed class Phase3_SuperclassAndSentinels
    {
        private readonly ColdStartBundle _bundle;
        private readonly IHeapManager _heap;
        private readonly ILayoutResolver _layout;
        private readonly IValidationReporter _reporter;

        // Inputs / shared state (provided by caller)
        private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
        private readonly HashSet<(SmalltalkOOP oop, int slot)> _initializedFields;

        public Phase3_SuperclassAndSentinels(
            ColdStartBundle bundle,
            IHeapManager heapManager,
            ILayoutResolver layoutResolver,
            Dictionary<string, SmalltalkOOP> localIdToObjectId,
            HashSet<(SmalltalkOOP oop, int slot)> initializedFields,
            IValidationReporter reporter)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _heap = heapManager ?? throw new ArgumentNullException(nameof(heapManager));
            _layout = layoutResolver ?? throw new ArgumentNullException(nameof(layoutResolver));
            _localIdToObjectId = localIdToObjectId ?? throw new ArgumentNullException(nameof(localIdToObjectId));
            _initializedFields = initializedFields ?? throw new ArgumentNullException(nameof(initializedFields));
            _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
        }

        public void Run()
        {
            _reporter.LogPhaseStart("Phase 3: Superclass Wiring & Sentinel Finalization");

            try
            {
                SetThisClassForMetaclasses();
                WireFundamentalMetaclassCircle();
                WireRegularMetaclassSuperclasses();
                FinalizeSentinelClassSlots();
                _reporter.LogPhaseSuccess("Phase 3: Superclass Wiring & Sentinel Finalization");
            }
            catch (LoaderPhaseException ex)
            {
                _reporter.LogPhaseFailure("Phase 3: Superclass Wiring & Sentinel Finalization", ex);
                throw;
            }
        }

        private void SetThisClassForMetaclasses()
        {
            _reporter.LogInfo("Phase3: Setting thisClass for all metaclasses");

            // Determine the 'thisClass' index once using layout resolver on class:Metaclass template
            int thisClassIndex = _layout.GetFieldIndex("class:Metaclass", "thisClass");
            if (thisClassIndex < 0)
                ThrowValidation("layout.thisClass", "could not find thisClass field index in class:Metaclass");

            // For each metaclass descriptor in bundle, set its thisClass instance var
            foreach (var cls in _bundle.Classes.Where(c => c.IsMetaclass))
            {
                string instanceLocalId = cls.MetaclassOfLocalId;
                if (string.IsNullOrEmpty(instanceLocalId))
                    ThrowValidation("metaclass.metaclassOfLocalId", $"metaclass {cls.LocalId} missing MetaclassOfLocalId");

                if (!_localIdToObjectId.TryGetValue(cls.LocalId, out var metaOOP))
                    ThrowValidation("metaclass.allocation", $"metaclass {cls.LocalId} not allocated in Phase2");

                if (!_localIdToObjectId.TryGetValue(instanceLocalId, out var instanceOOP))
                    ThrowValidation("metaclass.instanceNotFound", $"instance class {instanceLocalId} for {cls.LocalId} not found");

                _heap.SetInstanceVariable(metaOOP, thisClassIndex, instanceOOP);
                _initializedFields.Add((metaOOP, thisClassIndex));
            }

            _reporter.RecordMetric("phase3.metaclassesThisClassSet", _bundle.Classes.Count(c => c.IsMetaclass));
        }

        private void WireFundamentalMetaclassCircle()
        {
            _reporter.LogInfo("Phase3: Wiring fundamental metaclass circle");

            // Local IDs must exist from Phase2
            EnsureLocalIdExists("meta:Object", "class:Class", "class:ClassDescription", "class:Behavior", "class:Object");

            var metaObject = _localIdToObjectId["meta:Object"];
            var classClass = _localIdToObjectId["class:Class"];
            var classDesc = _localIdToObjectId["class:ClassDescription"];
            var behavior = _localIdToObjectId["class:Behavior"];
            var objClass = _localIdToObjectId["class:Object"];

            // Find superclass field index in Class template (Behavior supplies 'superclass')
            int superclassIndexForClass = _layout.GetFieldIndex("class:Class", "superclass");
            if (superclassIndexForClass < 0)
                ThrowValidation("layout.superclass", "could not find 'superclass' field index in class:Class");

            // Wire the required edges (set and record)
            _heap.SetInstanceVariable(metaObject, superclassIndexForClass, classClass);
            _initializedFields.Add((metaObject, superclassIndexForClass));

            _heap.SetInstanceVariable(classClass, superclassIndexForClass, classDesc);
            _initializedFields.Add((classClass, superclassIndexForClass));

            _heap.SetInstanceVariable(classDesc, superclassIndexForClass, behavior);
            _initializedFields.Add((classDesc, superclassIndexForClass));

            _heap.SetInstanceVariable(behavior, superclassIndexForClass, objClass);
            _initializedFields.Add((behavior, superclassIndexForClass));

            _reporter.LogInfo("Phase3: Fundamental metaclass circle wired (meta:Object → class:Class → class:ClassDescription → class:Behavior → class:Object)");
        }

        private void WireRegularMetaclassSuperclasses()
        {
            _reporter.LogInfo("Phase3: Wiring regular metaclass.superclass = meta:SuperClass (mirrors instance-side inheritance)");

            int superclassIndexForMeta = _layout.GetFieldIndex("class:Metaclass", "superclass");
            if (superclassIndexForMeta < 0)
                ThrowValidation("layout.metaclassSuperclass", "could not find superclass index for Metaclass");

            int wired = 0;
            int skipped = 0;

            // For each metaclass, determine the instance class's superclass and wire meta:instance superclass accordingly
            foreach (var metaDesc in _bundle.Classes.Where(c => c.IsMetaclass))
            {
                string instanceLocalId = metaDesc.MetaclassOfLocalId;
                if (string.IsNullOrEmpty(instanceLocalId))
                {
                    ThrowValidation("metaclass.mapping", $"metaclass {metaDesc.LocalId} missing MetaclassOfLocalId");
                }

                // Get instance class descriptor to find its superclass LocalId
                var instanceDesc = _bundle.Classes.FirstOrDefault(c => c.LocalId == instanceLocalId);
                if (instanceDesc == null)
                {
                    ThrowValidation("instanceClassLookup", $"instance class descriptor not found for {instanceLocalId}");
                }

                // If instance class has no superclass (Object), skip — fundamental circle handled earlier
                if (!instanceDesc.SuperNameIndex.HasValue)
                {
                    skipped++;
                    continue;
                }

                // Resolve instance superclass localId by symbol index
                var superSymbol = _bundle.Symbols[instanceDesc.SuperNameIndex.Value];
                string superMetaLocalId = $"meta:{superSymbol}";

                if (!_localIdToObjectId.TryGetValue(metaDesc.LocalId, out var metaOOP))
                    ThrowValidation("metaclass.allocation", $"metaclass {metaDesc.LocalId} not allocated");

                if (!_localIdToObjectId.TryGetValue(superMetaLocalId, out var superMetaOOP))
                {
                    // If metaclass for superclass not found, log and skip (won't happen on well-formed bundle)
                    _reporter.LogInfo($"Phase3: Warning - super-metaclass {superMetaLocalId} not found for {metaDesc.LocalId}");
                    skipped++;
                    continue;
                }

                _heap.SetInstanceVariable(metaOOP, superclassIndexForMeta, superMetaOOP);
                _initializedFields.Add((metaOOP, superclassIndexForMeta));
                wired++;
            }

            _reporter.LogInfo($"Phase3: Wired {wired} metaclass.superclass entries ({skipped} skipped)");
            _reporter.RecordMetric("phase3.metaclassSuperWired", wired);
        }

        private void FinalizeSentinelClassSlots()
        {
            _reporter.LogInfo("Phase3: Finalizing sentinel class slots (nil, true, false)");

            // Expected class localIds for sentinel types
            EnsureLocalIdExists("class:UndefinedObject", "class:True", "class:False");

            var undefinedClass = _localIdToObjectId["class:UndefinedObject"];
            var trueClass = _localIdToObjectId["class:True"];
            var falseClass = _localIdToObjectId["class:False"];

            // Retrieve preallocated sentinel OOPs (from Phase2 LocalIdToObjectId entries "nil"/"true"/"false")
            if (!_localIdToObjectId.TryGetValue("nil", out var nilOOP))
                ThrowValidation("sentinel.nil", "nil sentinel OOP not found in localId map");

            if (!_localIdToObjectId.TryGetValue("true", out var trueOOP))
                ThrowValidation("sentinel.true", "true sentinel OOP not found in localId map");

            if (!_localIdToObjectId.TryGetValue("false", out var falseOOP))
                ThrowValidation("sentinel.false", "false sentinel OOP not found in localId map");

            // slot 0 is the class slot for all objects; set via SetSlot (index 0)
            _heap.SetSlot(nilOOP, 0, undefinedClass);
            _initializedFields.Add((nilOOP, 0));

            _heap.SetSlot(trueOOP, 0, trueClass);
            _initializedFields.Add((trueOOP, 0));

            _heap.SetSlot(falseOOP, 0, falseClass);
            _initializedFields.Add((falseOOP, 0));

            _reporter.RecordMetric("phase3.sentinelsFinalized", 3);
        }

        private void EnsureLocalIdExists(params string[] required)
        {
            foreach (var id in required)
            {
                if (!_localIdToObjectId.ContainsKey(id))
                    ThrowValidation("localId.missing", $"required localId '{id}' missing from LocalIdToObjectId map");
            }
        }

        private void ThrowValidation(string key, string message)
        {
            var err = new LoaderPhaseException($"Phase3 validation failed [{key}]: {message}");
            _reporter.LogValidationError(key, message);
            throw err;
        }
    }

    // Expected additional IHeapManager API surface for this phase
    public partial interface IHeapManager
    {
        // SetInstanceVariable uses field index (0-based within instance vars) and writes the pointer
        void SetInstanceVariable(SmalltalkOOP objectOOP, int fieldIndex, SmalltalkOOP value);

        // SetSlot writes the fixed slot by absolute slot index (slot 0 is class pointer)
        void SetSlot(SmalltalkOOP objectOOP, int slotIndex, SmalltalkOOP value);

        // Some helpers used elsewhere
        SmalltalkOOP GetSlot(SmalltalkOOP objectOOP, int slotIndex);
    }
}
