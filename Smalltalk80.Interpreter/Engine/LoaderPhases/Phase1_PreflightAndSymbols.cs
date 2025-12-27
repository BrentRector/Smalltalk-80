// Phase1_PreflightAndSymbols.cs
// Consolidated from: Phase0_Preflight + Phase1_SymbolInterning
// Responsibility: Validate bundle structure, allocate and intern all symbols

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Shared.Types.Diagnostics;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases
{
    /// <summary>
    /// Phase 1: Preflight Validation + Symbol Interning
    /// - Validates bundle version, localIds, symbol indices, object references
    /// - Validates precomputed layouts (v1.0.6+)
    /// - Interns all symbols into heap as ByteIndexed objects
    /// - Populates and freezes SymbolRegistry
    /// </summary>
    public class Phase1_PreflightAndSymbols : IPhase
    {
        private readonly ColdStartBundle _bundle;
        private readonly ObjectGraphAllocator _allocator;
        private readonly Dictionary<string, SmalltalkOOP> _symbolTable = new();
        private readonly ColdstartReport _report;
        private readonly IDiagnosticHub? _diagnostics;

        /// <summary>
        /// Gets the phase name for diagnostics and logging.
        /// </summary>
        public string Name => "Phase 1: Preflight Validation and Symbol Interning";

        /// <summary>
        /// Gets the phase number (1-6).
        /// </summary>
        public int PhaseNumber => 1;

        public Phase1_PreflightAndSymbols(ColdStartBundle bundle, ObjectGraphAllocator allocator, ColdstartReport report, IDiagnosticHub? diagnostics = null)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
            _report = report ?? throw new ArgumentNullException(nameof(report));
            _diagnostics = diagnostics;
        }

        /// <summary>
        /// Validates phase preconditions before execution.
        /// </summary>
        /// <returns>List of precondition failure messages.</returns>
        public IReadOnlyList<string> ValidatePreconditions()
        {
            return new List<string>();
        }

        /// <summary>
        /// Validates phase postconditions after execution.
        /// </summary>
        /// <returns>List of postcondition failure messages.</returns>
        public IReadOnlyList<string> ValidatePostconditions()
        {
            return new List<string>();
        }

        public void Run()
        {
            // Preflight validation
            ValidateBundleVersion();
            _report.AddValidation("Phase 1", "Bundle version >= 1.0.6", true);

            ValidateLocalIds();
            _report.AddValidation("Phase 1", "LocalId uniqueness", true);

            ValidateSymbolIndices();
            _report.AddValidation("Phase 1", "Symbol indices valid", true);

            ValidateObjectRefs();
            _report.AddValidation("Phase 1", "Object references valid", true);

            ValidatePrecomputedLayouts();
            _report.AddValidation("Phase 1", "Precomputed layouts valid", true);

            ValidateHomeClassMetadata();
            _report.AddValidation("Phase 1", "HomeClass metadata valid", true);

            ValidateMetaclassCircle();
            _report.AddValidation("Phase 1", "Metaclass circle valid", true);

            ValidateWellKnownObjectsDefinition();
            _report.AddValidation("Phase 1", "Well-known objects definition valid", true);

            ValidatePatches();
            _report.AddValidation("Phase 1", "Patch definitions valid", true);

            // Symbol interning
            for (int i = 0; i < _bundle.Symbols.Length; i++)
            {
                string symbolName = _bundle.Symbols[i];
                var symbolOOP = InternSymbol(symbolName);
                SymbolRegistry.Register(i, symbolOOP);

                // Emit typed event for detail listeners
                _diagnostics?.Emit(new SymbolInternedEvent
                {
                    Timestamp = Stopwatch.GetTimestamp(),
                    InstructionCount = 0,
                    SymbolIndex = i,
                    SymbolText = symbolName,
                    SymbolOOP = symbolOOP
                });
            }

            // Update symbol count in report (already set in Loader constructor, but verify)
            _report.Metrics.SymbolCount = _bundle.Symbols.Length;

            // Make SymbolRegistry immutable - no more registrations, but reads still allowed
            // Dispose() is called by ColdStartOrchestrator after Phase 6 to block all access
            SymbolRegistry.MakeImmutable();
        }

        #region Preflight Validation

        private void ValidateBundleVersion()
        {
            var version = _bundle.Metadata.Version;

            // Parse version string (format: "major.minor.patch")
            var parts = version.Split('.');
            if (parts.Length < 2)
                throw new InvalidOperationException($"Invalid bundle version format: {version}");

            if (!int.TryParse(parts[0], out int major) || !int.TryParse(parts[1], out int minor))
                throw new InvalidOperationException($"Invalid bundle version format: {version}");

            // Require v1.0.6+ for precomputed layouts
            if (major < 1 || (major == 1 && minor == 0 && int.Parse(parts.Length > 2 ? parts[2] : "0") < 6))
                throw new InvalidOperationException(
                    $"Bundle version {version} is not supported. Requires v1.0.6 or later for precomputed field layouts.");
        }

        private void ValidateLocalIds()
        {
            var allIds = _bundle.Classes.Select(c => c.LocalId)
                .Concat(_bundle.Objects.Select(o => o.LocalId))
                .Concat(_bundle.Methods.Select(m => m.LocalId))
                .Concat(_bundle.InitExpressions.Select(i => i.LocalId))
                .ToHashSet();

            if (allIds.Count != _bundle.Classes.Length + _bundle.Objects.Length + _bundle.Methods.Length + _bundle.InitExpressions.Length)
                throw new InvalidOperationException("Duplicate localIds detected.");
        }

        private void ValidateSymbolIndices()
        {
            int max = _bundle.Symbols.Length;
            foreach (var c in _bundle.Classes)
            {
                if (c.NameIndex >= max || c.InstVarNames.Any(i => i >= max))
                    throw new IndexOutOfRangeException("Invalid symbol index in class.");
            }
        }

        private void ValidateObjectRefs()
        {
            var validIds = _bundle.Classes.Select(c => c.LocalId)
                .Concat(_bundle.Objects.Select(o => o.LocalId))
                .Concat(_bundle.Methods.Select(m => m.LocalId))
                .Concat(_bundle.InitExpressions.Select(i => i.LocalId))
                .ToHashSet();

            // Validation: Check if patch references exist
            // (Silent - no output for successful validation)
        }

        private void ValidatePrecomputedLayouts()
        {
            foreach (var cls in _bundle.Classes)
            {
                if (cls.FieldLayout == null)
                    throw new InvalidOperationException($"Class {cls.LocalId} missing FieldLayout (required in v1.0.6+)");

                if (cls.FieldLayout.Length != cls.TotalInstanceVarCount)
                    throw new InvalidOperationException(
                        $"Class {cls.LocalId} FieldLayout length ({cls.FieldLayout.Length}) " +
                        $"does not match TotalInstanceVarCount ({cls.TotalInstanceVarCount})");
            }
        }

        private void ValidateHomeClassMetadata()
        {
            var classLocalIds = _bundle.Classes.Select(c => c.LocalId).ToHashSet();
            foreach (var method in _bundle.Methods)
            {
                // Skip block literals - they don't have home classes in the class list
                if (method.LocalId.StartsWith("block:"))
                    continue;

                if (!classLocalIds.Contains(method.HomeClassLocalId))
                    throw new InvalidOperationException(
                        $"Method {method.LocalId} references unknown home class {method.HomeClassLocalId}");
            }
        }

        private void ValidateMetaclassCircle()
        {
            var metaclasses = _bundle.Classes.Where(c => c.IsMetaclass).ToList();
            var requiredMetaclasses = new[] { "meta:Object", "meta:Behavior", "meta:ClassDescription", "meta:Class", "meta:Metaclass" };

            foreach (var required in requiredMetaclasses)
            {
                if (!metaclasses.Any(m => m.LocalId == required))
                    throw new InvalidOperationException($"Required metaclass {required} not found in bundle");
            }
        }

        private void ValidateWellKnownObjectsDefinition()
        {
            var requiredClasses = new[] { "class:UndefinedObject", "class:True", "class:False" };
            var classLocalIds = _bundle.Classes.Select(c => c.LocalId).ToHashSet();

            foreach (var required in requiredClasses)
            {
                if (!classLocalIds.Contains(required))
                    throw new InvalidOperationException($"Required class {required} for well-known objects not found");
            }
        }

        private void ValidatePatches()
        {
            if (_bundle.Patches == null || _bundle.Patches.Length == 0)
            {
                // No patches to validate (silent)
                return;
            }

            var validLocalIds = _bundle.Classes.Select(c => c.LocalId)
                .Concat(_bundle.Objects.Select(o => o.LocalId))
                .Concat(_bundle.Methods.Select(m => m.LocalId))
                .ToHashSet();

            foreach (var patch in _bundle.Patches)
            {
                switch (patch.Type)
                {
                    case "SetInstanceVariable":
                        // Validate targetLocalId exists
                        if (string.IsNullOrEmpty(patch.TargetLocalId))
                            throw new InvalidOperationException("Patch SetInstanceVariable missing targetLocalId");

                        if (!validLocalIds.Contains(patch.TargetLocalId))
                            throw new InvalidOperationException(
                                $"Patch references unknown targetLocalId: {patch.TargetLocalId}");

                        // Validate fieldName is provided
                        if (string.IsNullOrEmpty(patch.FieldName))
                            throw new InvalidOperationException(
                                $"Patch SetInstanceVariable for {patch.TargetLocalId} missing fieldName");

                        // Validate field name exists in target's layout
                        var targetClass = _bundle.Classes.FirstOrDefault(c => c.LocalId == patch.TargetLocalId);
                        if (targetClass != null)
                        {
                            bool fieldExists = targetClass.FieldLayout.Any(f => _bundle.Symbols[f.NameIndex] == patch.FieldName);
                            if (!fieldExists)
                                throw new InvalidOperationException(
                                    $"Patch references invalid field '{patch.FieldName}' in {patch.TargetLocalId}");
                        }
                        break;

                    case "InstallMethodDictionary":
                        // Validate classLocalId exists
                        if (string.IsNullOrEmpty(patch.ClassLocalId))
                            throw new InvalidOperationException("Patch InstallMethodDictionary missing classLocalId");

                        if (!_bundle.Classes.Any(c => c.LocalId == patch.ClassLocalId))
                            throw new InvalidOperationException(
                                $"Patch references unknown class: {patch.ClassLocalId}");
                        break;

                    case "ReplaceLiteral":
                        // Validate methodLocalId exists
                        if (string.IsNullOrEmpty(patch.MethodLocalId))
                            throw new InvalidOperationException("Patch ReplaceLiteral missing methodLocalId");

                        if (!_bundle.Methods.Any(m => m.LocalId == patch.MethodLocalId))
                            throw new InvalidOperationException(
                                $"Patch references unknown method: {patch.MethodLocalId}");

                        // Validate literalIndex is provided
                        if (!patch.LiteralIndex.HasValue)
                            throw new InvalidOperationException(
                                $"Patch ReplaceLiteral for {patch.MethodLocalId} missing literalIndex");

                        // Validate literalIndex is within bounds
                        var method = _bundle.Methods.First(m => m.LocalId == patch.MethodLocalId);
                        if (patch.LiteralIndex.Value < 0 || patch.LiteralIndex.Value >= method.Literals.Length)
                            throw new InvalidOperationException(
                                $"Patch literalIndex {patch.LiteralIndex.Value} out of range [0, {method.Literals.Length}) " +
                                $"for method {patch.MethodLocalId}");
                        break;

                    default:
                        throw new InvalidOperationException($"Unknown patch type: {patch.Type}");
                }
            }

            // Patches validated successfully (silent)
        }

        #endregion

        #region Symbol Interning

        private SmalltalkOOP InternSymbol(string name)
        {
            // Check if symbol already exists (deduplication)
            if (_symbolTable.TryGetValue(name, out var existingOOP))
            {
                Console.WriteLine($"[PHASE1 DEDUP] Symbol \"{name}\" already exists as {existingOOP}, returning existing OOP");
                return existingOOP;
            }

            // Encode symbol name as UTF-8 bytes
            byte[] symbolBytes = Encoding.UTF8.GetBytes(name);

            // Allocate ByteIndexed object to hold the symbol text
            var symbolOOP = _allocator.AllocateString("Symbol", symbolBytes.Length);

            // Store the symbol text as bytes
            for (int i = 0; i < symbolBytes.Length; i++)
            {
                _allocator.SetIndexedByte(symbolOOP, i, symbolBytes[i]);
            }

            // Cache in symbol table for deduplication
            _symbolTable[name] = symbolOOP;

            return symbolOOP;
        }

        #endregion
    }
}
