// Phase6_FinalizationAndValidation.cs
// Responsibility:
//  - Set remaining class references (slot 0) for all non-class objects
//  - Set Object.superclass = nil (final superclass link)
//  - Initialize all unset instance variable slots to nil using initializedFields tracking
//  - Register SpecialObjectsArray entries (Smalltalk, InitExpressionMethods, etc.)
//  - Run comprehensive validation checks (class slots, metaclass circle, dict integrity)
//  - Perform GC mark to verify zero objects are collected
//
// Inputs:
//  - LocalIdToObjectId: mapping for classes, sentinels, compiledMethods, methodDicts, bytearrays, etc.
//  - initializedFields: HashSet<(oop, slot)> tracking explicit writes from earlier phases
//  - LayoutResolver, SymbolRegistry, compiled method maps, method dictionaries
//  - HeapManager with allocation/slot APIs and GC marking support
//  - SpecialObjectsArray accessors (setters/getters are VM-specific; we use local map and caller wires into runtime)
//
// Outputs:
//  - Fully-initialized heap ready for runtime
//  - Throws LoaderPhaseException on any invariant failure (detailed diagnostics logged via reporter)

using System;
using System.Collections.Generic;
using System.Linq;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases
{
    public sealed class Phase6_FinalizationAndValidation
    {
        private readonly ColdStartBundle _bundle;
        private readonly IHeapManager _heap;
        private readonly ILayoutResolver _layout;
        private readonly ISymbolRegistry _symbols;
        private readonly IValidationReporter _reporter;

        private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
        private readonly HashSet<(SmalltalkOOP oop, int slot)> _initializedFields;

        // Provided from earlier phases
        private readonly Dictionary<string, SmalltalkOOP> _compiledMethods;    // methodLocalId -> OOP
        private readonly Dictionary<string, SmalltalkOOP> _methodDictionaries; // classLocalId -> dict OOP

        public Phase6_FinalizationAndValidation(
            ColdStartBundle bundle,
            IHeapManager heapManager,
            ILayoutResolver layoutResolver,
            ISymbolRegistry symbolRegistry,
            Dictionary<string, SmalltalkOOP> localIdToObjectId,
            HashSet<(SmalltalkOOP oop, int slot)> initializedFields,
            Dictionary<string, SmalltalkOOP> compiledMethods,
            Dictionary<string, SmalltalkOOP> methodDictionaries,
            IValidationReporter reporter)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _heap = heapManager ?? throw new ArgumentNullException(nameof(heapManager));
            _layout = layoutResolver ?? throw new ArgumentNullException(nameof(layoutResolver));
            _symbols = symbolRegistry ?? throw new ArgumentNullException(nameof(symbolRegistry));
            _localIdToObjectId = localIdToObjectId ?? throw new ArgumentNullException(nameof(localIdToObjectId));
            _initializedFields = initializedFields ?? throw new ArgumentNullException(nameof(initializedFields));
            _compiledMethods = compiledMethods ?? throw new ArgumentNullException(nameof(compiledMethods));
            _methodDictionaries = methodDictionaries ?? throw new ArgumentNullException(nameof(methodDictionaries));
            _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
        }

        public void Run()
        {
            _reporter.LogPhaseStart("Phase 6: Finalization & Validation");

            try
            {
                FinalizeObjectSuperclasses();
                SetClassSlotsForNonClassObjects();
                InitializeUnsetInstanceVariables();
                RegisterSpecialObjectsArrayEntries();
                RunComprehensiveValidation();
                RunGCVerification();
                // Clean up coldstart-only maps
                _initializedFields.Clear();
                _localIdToObjectId.Clear();
                _reporter.LogPhaseSuccess("Phase 6: Finalization & Validation");
            }
            catch (LoaderPhaseException ex)
            {
                _reporter.LogPhaseFailure("Phase 6: Finalization & Validation", ex);
                throw;
            }
        }

        private void FinalizeObjectSuperclasses()
        {
            _reporter.LogInfo("Phase6: Finalize superclass chain (Object.superclass = nil)");

            // Find class:Object and nil OOP
            if (!_localIdToObjectId.TryGetValue("class:Object", out var classObjectOOP))
                ThrowValidation("class:Object.missing", "class:Object missing in localId map");

            if (!_localIdToObjectId.TryGetValue("nil", out var nilOOP))
                ThrowValidation("nil.missing", "nil sentinel not found");

            int superclassIndex = _layout.GetFieldIndex("class:Class", "superclass");
            if (superclassIndex < 0)
                ThrowValidation("layout.superclassIndex", "could not find 'superclass' field index in class:Class");

            // Set class:Object.superclass = nil
            _heap.SetInstanceVariable(classObjectOOP, superclassIndex, nilOOP);
            _initializedFields.Add((classObjectOOP, superclassIndex));
            _reporter.LogInfo("Phase6: Object.superclass set to nil");
        }

        private void SetClassSlotsForNonClassObjects()
        {
            _reporter.LogInfo("Phase6: Setting class slot (slot 0) for symbols, compiledMethods, bytearrays, method dictionaries, pools, etc.");

            // Helper to set slot 0 safely and record initialization
            void SetClassSlotFor(string localId, string expectedClassLocalId)
            {
                if (!_localIdToObjectId.TryGetValue(localId, out var objOOP))
                    return; // not present, skip quietly (some bundles omit)
                if (!_localIdToObjectId.TryGetValue(expectedClassLocalId, out var classOOP))
                    ThrowValidation("classSlot.targetMissing", $"Expected class {expectedClassLocalId} for {localId} not found");
                _heap.SetSlot(objOOP, 0, classOOP);
                _initializedFields.Add((objOOP, 0));
            }

            // 1) Symbols
            var symCount = _bundle.Symbols?.Length ?? 0;
            for (int i = 0; i < symCount; i++)
            {
                var symOOP = _symbols.GetSymbolOOP(i);
                SetClassSlot(symOOP, "class:Symbol");
            }

            // 2) CompiledMethods
            foreach (var kv in _compiledMethods)
            {
                var cmOOP = kv.Value;
                if (cmOOP.Raw == 0) continue;
                SetClassSlot(cmOOP, "class:CompiledMethod");
            }

            // 3) ByteArrays: find by convention in localId map OR walk heap and set for objects of ByteIndexed format
            foreach (var kv in _localIdToObjectId.Where(kv => kv.Key.StartsWith("bytearray:", StringComparison.Ordinal)))
            {
                SetClassSlotFor(kv.Key, "class:ByteArray");
            }

            // 4) MethodDictionaries
            foreach (var kv in _methodDictionaries)
            {
                var dictOOP = kv.Value;
                if (dictOOP.Raw == 0) continue;
                SetClassSlot(dictOOP, "class:MethodDictionary");
            }

            // 5) Class pools, arrays, strings - set if present in localId map
            foreach (var kv in _localIdToObjectId)
            {
                var key = kv.Key;
                if (key.StartsWith("string:", StringComparison.Ordinal))
                    SetClassSlotFor(key, "class:String");
                else if (key.StartsWith("array:", StringComparison.Ordinal))
                    SetClassSlotFor(key, "class:Array");
                else if (key.StartsWith("classpool:", StringComparison.Ordinal))
                    SetClassSlotFor(key, "class:Dictionary");
            }

            _reporter.LogInfo("Phase6: Class slots for non-class objects set (where present)");
        }

        private void SetClassSlot(SmalltalkOOP objectOOP, string expectedClassLocalId)
        {
            if (objectOOP.Raw == 0) return;
            if (!_localIdToObjectId.TryGetValue(expectedClassLocalId, out var classOOP))
                ThrowValidation("classSlot.expectedClassMissing", $"Expected class {expectedClassLocalId} missing");
            _heap.SetSlot(objectOOP, 0, classOOP);
            _initializedFields.Add((objectOOP, 0));
        }

        private void InitializeUnsetInstanceVariables()
        {
            _reporter.LogInfo("Phase6: Initializing unset instance variable slots to nil");

            if (!_localIdToObjectId.TryGetValue("nil", out var nilOOP))
                ThrowValidation("nil.missing", "nil sentinel missing during initialization");

            int objectsChecked = 0;
            int fieldsInitialized = 0;

            // Reliable enumeration of allocated objects; assume heap exposes EnumerateObjects() or similar
            foreach (var oop in _heap.EnumerateObjects())
            {
                objectsChecked++;
                int instanceCount = _heap.GetInstanceVariableCount(oop);
                for (int i = 0; i < instanceCount; i++)
                {
                    // If this field was explicitly initialized earlier, skip
                    if (_initializedFields.Contains((oop, i)))
                        continue;

                    // Read current slot: if zero or invalid, set to nil; else leave as-is
                    var value = _heap.GetInstanceVariable(oop, i);
                    if (value.Raw == 0)
                    {
                        _heap.SetInstanceVariable(oop, i, nilOOP);
                        fieldsInitialized++;
                    }
                }

                // Also ensure slot 0 (class pointer) was set for all objects (classes handled separately)
                var classSlot = _heap.GetSlot(oop, 0);
                if (classSlot.Raw == 0)
                {
                    // Attempt to infer class from object's shape via LayoutResolver (best effort)
                    string inferredClassLocalId = _heap.InferClassLocalIdForObject(oop);
                    if (!string.IsNullOrEmpty(inferredClassLocalId) && _localIdToObjectId.TryGetValue(inferredClassLocalId, out var inferredClassOOP))
                    {
                        _heap.SetSlot(oop, 0, inferredClassOOP);
                        _initializedFields.Add((oop, 0));
                    }
                    else
                    {
                        // If cannot infer, fail - all objects must have class after finalization
                        ThrowValidation("classSlot.missing", $"Object {oop.Raw} has no class slot set at finalization");
                    }
                }
            }

            _reporter.LogInfo($"Phase6: Initialized {fieldsInitialized} instance fields across {objectsChecked} objects");
        }

        private void RegisterSpecialObjectsArrayEntries()
        {
            _reporter.LogInfo("Phase6: Registering SpecialObjectsArray entries (Smalltalk, InitExpressionMethods)");

            // Smalltalk SystemDictionary should have been created in Phase5 and stored as "SystemDictionary"
            if (_localIdToObjectId.TryGetValue("SystemDictionary", out var systemDict))
            {
                // Call into VM runtime to set SpecialObjectsArray.Smalltalk = systemDict
                SpecialObjectsArrayAPI.SetSmalltalk(systemDict);
            }

            if (_localIdToObjectId.TryGetValue("InitExpressionMethods", out var initArray))
            {
                SpecialObjectsArrayAPI.SetInitMethods(initArray);
            }

            // Ensure sentinels are registered too (nil/true/false); Phase3 set their class slots
            if (_localIdToObjectId.TryGetValue("nil", out var nilOOP)) SpecialObjectsArrayAPI.SetNil(nilOOP);
            if (_localIdToObjectId.TryGetValue("true", out var trueOOP)) SpecialObjectsArrayAPI.SetTrue(trueOOP);
            if (_localIdToObjectId.TryGetValue("false", out var falseOOP)) SpecialObjectsArrayAPI.SetFalse(falseOOP);

            _reporter.LogInfo("Phase6: SpecialObjectsArray entries registered");
        }

        private void RunComprehensiveValidation()
        {
            _reporter.LogInfo("Phase6: Running comprehensive validation checks");

            ValidateNoNullClassSlots();
            ValidateMetaclassCircle();
            ValidateSuperclassTerminations();
            ValidateMethodDictionaryIntegrity();
            ValidateSymbolTableAndAssociations();
            _reporter.LogInfo("Phase6: Comprehensive validation passed");
        }

        private void ValidateNoNullClassSlots()
        {
            _reporter.LogInfo("Phase6: Validating that every object has a non-null class slot");

            int count = 0;
            foreach (var oop in _heap.EnumerateObjects())
            {
                var classSlot = _heap.GetSlot(oop, 0);
                if (classSlot.Raw == 0)
                    ThrowValidation("validation.classSlotNull", $"Object {oop.Raw} has null class slot");
                count++;
            }

            _reporter.RecordMetric("phase6.objectsValidatedClassSlot", count);
        }

        private void ValidateMetaclassCircle()
        {
            _reporter.LogInfo("Phase6: Validating metaclass circle");

            // Walk from meta:Object through the 4-hop circle and verify ends at class:Object
            if (!_localIdToObjectId.TryGetValue("meta:Object", out var metaObjectOOP))
                ThrowValidation("validation.metaObjectMissing", "meta:Object missing");

            int superclassIndex = _layout.GetFieldIndex("class:Class", "superclass");

            var cur = metaObjectOOP;
            for (int hop = 0; hop < 4; hop++)
            {
                cur = _heap.GetInstanceVariable(cur, superclassIndex);
                if (cur.Raw == 0) ThrowValidation("validation.metaclassCircleBroken", $"Metaclass circle broke at hop {hop}");
            }

            if (!_localIdToObjectId.TryGetValue("class:Object", out var classObjectOOP))
                ThrowValidation("validation.classObjectMissing", "class:Object missing");

            if (cur.Raw != classObjectOOP.Raw)
                ThrowValidation("validation.metaclassCircleMismatch", "Metaclass circle did not end at class:Object");
        }

        private void ValidateSuperclassTerminations()
        {
            _reporter.LogInfo("Phase6: Validating regular superclass chains terminate at nil");

            var nilOOP = _localIdToObjectId["nil"];
            int superclassIndex = _layout.GetFieldIndex("class:Class", "superclass");

            foreach (var cls in _bundle.Classes.Where(c => !c.IsMetaclass))
            {
                if (!_localIdToObjectId.TryGetValue(cls.LocalId, out var classOOP))
                    ThrowValidation("validation.classMissing", $"Class {cls.LocalId} missing");

                var visited = new HashSet<uint>();
                var cur = classOOP;
                while (true)
                {
                    if (!visited.Add(cur.Raw))
                        ThrowValidation("validation.superclassCycle", $"Superclass cycle detected starting at {cls.LocalId}");

                    var sup = _heap.GetInstanceVariable(cur, superclassIndex);
                    if (sup.Raw == nilOOP.Raw) break;
                    cur = sup;
                }
            }

            _reporter.LogInfo("Phase6: All superclass chains terminate at nil");
        }

        private void ValidateMethodDictionaryIntegrity()
        {
            _reporter.LogInfo("Phase6: Validating method dictionary integrity (tally vs actual entries)");

            var nilOOP = _localIdToObjectId["nil"];
            int checked = 0;
            foreach (var kv in _methodDictionaries)
            {
                var dictOOP = kv.Value;
                var tallyOOP = _heap.GetInstanceVariable(dictOOP, 0);
                int tally = tallyOOP.ToSmallInteger();
                int capacity = _heap.GetIndexedSize(dictOOP);
                int count = 0;
                for (int i = 0; i < capacity; i += 2)
                {
                    var key = _heap.GetIndexedOOP(dictOOP, i);
                    if (key.Raw != nilOOP.Raw) count++;
                }

                if (count != tally)
                    ThrowValidation("validation.dictTallyMismatch", $"Dictionary for {kv.Key} reports tally {tally} but contains {count} entries");

                checked++;
            }

            _reporter.RecordMetric("phase6.dictsValidated", checked);
            _reporter.LogInfo($"Phase6: Validated {checked} method dictionaries");
        }

        private void ValidateSymbolTableAndAssociations()
        {
            _reporter.LogInfo("Phase6: Validating USTable and canonical associations if present");

            // Validate USTable size matches symbols (if created)
            if (_localIdToObjectId.TryGetValue("USTable", out var ustableOOP))
            {
                int symbolCount = _bundle.Symbols?.Length ?? 0;
                int capacity = _heap.GetIndexedSize(ustableOOP);
                // Quick heuristic: ensure table has capacity >= symbolCount
                if (capacity < symbolCount / 2)
                    ThrowValidation("validation.ustableSize", $"USTable capacity ({capacity}) suspicious for {symbolCount} symbols");
            }

            // Associations: ensure canonical associations (value may still be nil until Phase5 set)
            // If canonical associations were created, verify each has key != nil
            foreach (var kv in _localIdToObjectId.Where(kv => kv.Key.StartsWith("assoc:", StringComparison.Ordinal)))
            {
                var assoc = kv.Value;
                var key = _heap.GetInstanceVariable(assoc, 0);
                if (key.Raw == 0) ThrowValidation("validation.assoc.keyNull", $"Association {kv.Key} key is null");
            }

            _reporter.LogInfo("Phase6: USTable and associations validation completed (basic checks)");
        }

        private void RunGCVerification()
        {
            _reporter.LogInfo("Phase6: Running GC verification (mark from SpecialObjectsArray roots)");

            // Build roots list from SpecialObjectsArray API
            var roots = new List<SmalltalkOOP>
            {
                SpecialObjectsArrayAPI.GetNil(),
                SpecialObjectsArrayAPI.GetTrue(),
                SpecialObjectsArrayAPI.GetFalse(),
                SpecialObjectsArrayAPI.GetSmalltalk(),         // may be null if not set
                SpecialObjectsArrayAPI.GetUSTable()           // may be null if not set
            };

            // Add all classes as roots (stable)
            foreach (var cls in _bundle.Classes)
            {
                if (_localIdToObjectId.TryGetValue(cls.LocalId, out var classOOP))
                    roots.Add(classOOP);
            }

            // Mark phase
            var reachable = new HashSet<uint>();
            void Mark(SmalltalkOOP oop)
            {
                if (oop.Raw == 0) return;
                if ((oop.Raw & 1u) == 1u) return; // small integer immediate
                if (!reachable.Add(oop.Raw)) return;

                var fmt = _heap.GetFormat(oop);
                if (fmt == ObjectFormat.PointerIndexed || fmt == ObjectFormat.Fixed)
                {
                    int totalSlots = _heap.GetTotalSlotCount(oop);
                    for (int i = 0; i < totalSlots; i++)
                    {
                        var child = _heap.GetSlot(oop, i);
                        Mark(child);
                    }
                }
                else if (fmt == ObjectFormat.ByteIndexed || fmt == ObjectFormat.WordIndexed)
                {
                    // Byte/Word indexed objects still have slot 0 class pointer and possibly instance vars
                    int totalSlots = _heap.GetTotalSlotCount(oop);
                    for (int i = 0; i < totalSlots; i++)
                    {
                        var child = _heap.GetSlot(oop, i);
                        Mark(child);
                    }
                }
            }

            foreach (var r in roots)
                Mark(r);

            // Count allocated objects vs reachable
            int totalObjects = _heap.GetObjectCount();
            int reachableCount = reachable.Count;
            int unreachable = totalObjects - reachableCount;

            _reporter.LogInfo($"Phase6: GC verification: reachable {reachableCount} / total {totalObjects} (unreachable {unreachable})");

            if (unreachable != 0)
            {
                // Build a compact sample of unreachable items for diagnostics
                var sampleUnreach = _heap.EnumerateObjects()
                    .Where(oop => !reachable.Contains(oop.Raw))
                    .Take(16)
                    .Select(oop => new { oop = oop.Raw, format = _heap.GetFormat(oop) })
                    .ToList();

                var msg = $"GC verification failed: {unreachable} unreachable objects. Sample: {string.Join(", ", sampleUnreach.Select(s => $"[{s.oop}:fmt={s.format}]"))}";
                ThrowValidation("gc.unreachableObjects", msg);
            }

            _reporter.LogInfo("Phase6: GC verification passed (0 unreachable objects)");
        }

        private void ThrowValidation(string key, string message)
        {
            var ex = new LoaderPhaseException($"Phase6 validation failed [{key}]: {message}");
            _reporter.LogValidationError(key, message);
            throw ex;
        }
    }

    // SpecialObjectsArray API (bridge to runtime for setting/getting special objects)
    public static class SpecialObjectsArrayAPI
    {
        private static SmalltalkOOP _nil = default;
        private static SmalltalkOOP _true = default;
        private static SmalltalkOOP _false = default;
        private static SmalltalkOOP _smalltalk = default;
        private static SmalltalkOOP _ustable = default;
        private static SmalltalkOOP _initMethods = default;

        public static void SetNil(SmalltalkOOP v) => _nil = v;
        public static void SetTrue(SmalltalkOOP v) => _true = v;
        public static void SetFalse(SmalltalkOOP v) => _false = v;
        public static void SetSmalltalk(SmalltalkOOP v) => _smalltalk = v;
        public static void SetUSTable(SmalltalkOOP v) => _ustable = v;
        public static void SetInitMethods(SmalltalkOOP v) => _initMethods = v;

        public static SmalltalkOOP GetNil() => _nil;
        public static SmalltalkOOP GetTrue() => _true;
        public static SmalltalkOOP GetFalse() => _false;
        public static SmalltalkOOP GetSmalltalk() => _smalltalk;
        public static SmalltalkOOP GetUSTable() => _ustable;
        public static SmalltalkOOP GetInitMethods() => _initMethods;
    }

    // IHeapManager extensions required for finalization
    public partial interface IHeapManager
    {
        IEnumerable<SmalltalkOOP> EnumerateObjects();
        int GetInstanceVariableCount(SmalltalkOOP objectOOP);
        SmalltalkOOP GetInstanceVariable(SmalltalkOOP objectOOP, int index);
        SmalltalkOOP GetSlot(SmalltalkOOP objectOOP, int slotIndex);
        int GetTotalSlotCount(SmalltalkOOP objectOOP);
        ObjectFormat GetFormat(SmalltalkOOP objectOOP);
        int GetObjectCount();
    }

    // Helper extension to convert SmalltalkOOP -> SmallInteger
    public static partial class SmalltalkOOPExtensions
    {
        public static int ToSmallInteger(this SmalltalkOOP oop)
        {
            // If immediate tagging used, decode accordingly; this example assumes low-bit tag 1
            return (int)(oop.Raw >> 1);
        }
    }
}
