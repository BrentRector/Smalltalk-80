// Phase2_ClassInfrastructure.cs
// Responsibility:
//  - Allocate class and metaclass shells for all classes in bundle
//  - Populate localIdToObjectId mapping for class/metaclass localIds
//  - Allocate sentinel objects (nil, true, false) with class slot left unset
//  - Load precomputed layouts from bundle into LayoutResolver and Freeze it
//  - Record initializedFields for fields set during this phase
//
// Assumptions:
//  - IHeapManager, SmalltalkOOP, ObjectFormat, ColdStartBundle, IValidationReporter exist (see Phase1 file).
//  - LayoutResolver provides SetFieldLayout(localId, IReadOnlyList<string>) and Freeze()
//  - ClassDescriptor in bundle contains LocalId, IsMetaclass flag, Format word, FieldLayout array (precomputed).
//  - SemanticAllocator or a small helper is available to compute total instance var count for templates.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases
{
    public sealed class Phase2_ClassInfrastructure
    {
        private readonly ColdStartBundle _bundle;
        private readonly IHeapManager _heap;
        private readonly IValidationReporter _reporter;
        private readonly ILayoutResolver _layoutResolver;

        // Outputs:
        public Dictionary<string, SmalltalkOOP> LocalIdToObjectId { get; }
            = new Dictionary<string, SmalltalkOOP>(StringComparer.Ordinal);
        public HashSet<(SmalltalkOOP oop, int slot)> InitializedFields { get; }
            = new HashSet<(SmalltalkOOP, int)>();

        // Sentinels (allocated here, class slot set later)
        public SmalltalkOOP NilOOP { get; private set; } = default;
        public SmalltalkOOP TrueOOP { get; private set; } = default;
        public SmalltalkOOP FalseOOP { get; private set; } = default;

        public Phase2_ClassInfrastructure(
            ColdStartBundle bundle,
            IHeapManager heapManager,
            ILayoutResolver layoutResolver,
            IValidationReporter reporter)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _heap = heapManager ?? throw new ArgumentNullException(nameof(heapManager));
            _layoutResolver = layoutResolver ?? throw new ArgumentNullException(nameof(layoutResolver));
            _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
        }

        public void Run()
        {
            _reporter.LogPhaseStart("Phase 2: Class Infrastructure");

            try
            {
                AllocateClassShells();
                AllocateSentinels();            // nil/true/false (class slot = 0 for now)
                LoadPrecomputedLayouts();      // populate LayoutResolver from bundle and Freeze
                _reporter.LogPhaseSuccess("Phase 2: Class Infrastructure");
            }
            catch (LoaderPhaseException ex)
            {
                _reporter.LogPhaseFailure("Phase 2: Class Infrastructure", ex);
                throw;
            }
        }

        private void AllocateClassShells()
        {
            // Expect classes array populated by Phase1 validations
            var classes = _bundle.Classes;
            if (classes == null || classes.Length == 0)
                ThrowValidation("classes", "no classes present in bundle");

            _reporter.LogInfo($"Phase2: Allocating {classes.Length} class/metaclass shells");

            // First pass: allocate all class objects as shells (PointerIndexed)
            foreach (var cls in classes)
            {
                // Determine template: regular class uses 'Class' template, metaclass uses 'Metaclass' template.
                string templateName = cls.IsMetaclass ? "Metaclass" : "Class";

                // Use precomputed total instance var count if present, else compute from FieldLayout length
                int totalInstanceVarCount = GetTotalInstanceVarCountFromDescriptor(cls);

                // Classes are PointerIndexed (instance vars are pointers)
                SmalltalkOOP classOOP = _heap.AllocateObject(ObjectFormat.PointerIndexed, totalInstanceVarCount, indexedSize: 0);

                // NOTE: slot 0 (class reference) left unset here; will be set in Phase6
                // All other slots initialized to 0 by AllocateObject
                LocalIdToObjectId[cls.LocalId] = classOOP;
            }

            _reporter.RecordMetric("phase2.classCount", classes.Length);
        }

        private int GetTotalInstanceVarCountFromDescriptor(ClassDescriptor cls)
        {
            // Prefer TotalInstanceVarCount field if present (v1.0.6 precomputed)
            if (cls.TotalInstanceVarCount.HasValue)
                return cls.TotalInstanceVarCount.Value;

            // Fallback: if FieldLayout provided, use its length
            if (cls.FieldLayout != null && cls.FieldLayout.Length > 0)
                return cls.FieldLayout.Length;

            // Conservative default: 0 (should rarely happen)
            return 0;
        }

        private void AllocateSentinels()
        {
            _reporter.LogInfo("Phase2: Allocating sentinels (nil, true, false) with class slot unset");

            // Sentinels are Fixed format with zero instance variables
            NilOOP = _heap.AllocateObject(ObjectFormat.Fixed, fixedSlots: 0, indexedSize: 0);
            TrueOOP = _heap.AllocateObject(ObjectFormat.Fixed, fixedSlots: 0, indexedSize: 0);
            FalseOOP = _heap.AllocateObject(ObjectFormat.Fixed, fixedSlots: 0, indexedSize: 0);

            // Register these in localId map for convenience
            LocalIdToObjectId["nil"] = NilOOP;
            LocalIdToObjectId["true"] = TrueOOP;
            LocalIdToObjectId["false"] = FalseOOP;

            _reporter.RecordMetric("phase2.sentinelsAllocated", 3);
        }

        private void LoadPrecomputedLayouts()
        {
            _reporter.LogInfo("Phase2: Loading precomputed layouts into LayoutResolver");

            // Expect bundle.Classes to contain FieldLayout arrays when v1.0.6
            foreach (var cls in _bundle.Classes)
            {
                if (cls.FieldLayout == null || cls.FieldLayout.Length == 0)
                {
                    // Accept empty layout (no instance vars) but log
                    _layoutResolver.SetFieldLayout(cls.LocalId, Array.Empty<string>());
                    continue;
                }

                var fields = new List<string>(cls.FieldLayout.Length);
                foreach (var fe in cls.FieldLayout)
                {
                    if (fe.NameIndex < 0 || fe.NameIndex >= _bundle.Symbols.Length)
                        ThrowValidation("fieldLayout.index", $"class {cls.LocalId} has invalid field layout name index {fe.NameIndex}");

                    string fieldName = _bundle.Symbols[fe.NameIndex];
                    fields.Add(fieldName);
                }

                _layoutResolver.SetFieldLayout(cls.LocalId, fields);
            }

            _layoutResolver.Freeze();
            _reporter.LogInfo("Phase2: LayoutResolver frozen");
        }

        private void ThrowValidation(string key, string message)
        {
            var err = new LoaderPhaseException($"Phase2 validation failed [{key}]: {message}");
            _reporter.LogValidationError(key, message);
            throw err;
        }
    }

    // Minimal expected layout resolver interface
    public interface ILayoutResolver
    {
        void SetFieldLayout(string classLocalId, IReadOnlyList<string> fields);
        IReadOnlyList<string> GetFieldLayout(string classLocalId);
        void Freeze();
        bool IsFrozen { get; }
    }

    // Extend ClassDescriptor shape used in this phase
    public partial class ClassDescriptor
    {
        public string LocalId;
        public bool IsMetaclass;
        public int? TotalInstanceVarCount;     // optional, precomputed
        public FieldLayoutEntry[] FieldLayout; // optional, precomputed layout entries
    }

    public struct FieldLayoutEntry
    {
        public int NameIndex;
    }
}
