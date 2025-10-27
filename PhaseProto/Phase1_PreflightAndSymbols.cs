// Phase1_PreflightAndSymbols.cs
// Responsibility: Validate bundle, allocate Symbols, populate & freeze SymbolRegistry.
// Assumptions:
//  - HeapManager exposes: AllocateObject(format, fixedSlots, indexedSize), SetIndexedByte(oop, index, byte), SetSlot/get helpers.
//  - ObjectFormat enum: Fixed, PointerIndexed, WordIndexed, ByteIndexed.
//  - SmalltalkOOP helper for small integers and checks.
//  - SymbolRegistry is a runtime singleton with Register(index, oop) and Freeze().

using System;
using System.Collections.Generic;
using System.Text;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases
{
    public sealed class Phase1_PreflightAndSymbols
    {
        private readonly ColdStartBundle _bundle;
        private readonly IHeapManager _heap;
        private readonly ISymbolRegistry _symbols;
        private readonly IValidationReporter _reporter;

        // Exposed result: symbol count and some diagnostics
        public int SymbolCount => _bundle.Symbols?.Length ?? 0;

        public Phase1_PreflightAndSymbols(
            ColdStartBundle bundle,
            IHeapManager heapManager,
            ISymbolRegistry symbolRegistry,
            IValidationReporter reporter)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _heap = heapManager ?? throw new ArgumentNullException(nameof(heapManager));
            _symbols = symbolRegistry ?? throw new ArgumentNullException(nameof(symbolRegistry));
            _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
        }

        public void Run()
        {
            // Phase entry log
            _reporter.LogPhaseStart("Phase 1: Preflight & Symbols");

            try
            {
                ValidateBundleStructure();
                AllocateSymbols();
                _symbols.Freeze();
                _reporter.RecordMetric("phase1.symbolCount", SymbolCount);
                _reporter.LogPhaseSuccess("Phase 1: Preflight & Symbols");
            }
            catch (LoaderPhaseException ex)
            {
                _reporter.LogPhaseFailure("Phase 1: Preflight & Symbols", ex);
                throw;
            }
        }

        private void ValidateBundleStructure()
        {
            if (string.IsNullOrEmpty(_bundle?.Version))
                ThrowValidation("bundle.version", "missing or empty");

            // Accept v1.0.6 schema (or exact expected version)
            if (_bundle.Version != "1.0.6" && _bundle.Version != "1.0.6+")
                ThrowValidation("bundle.version",
                    $"unsupported bundle version '{_bundle.Version}', expected '1.0.6'");

            if (_bundle.Symbols == null || _bundle.Symbols.Length == 0)
                ThrowValidation("symbols", "missing or empty");

            if (_bundle.Classes == null || _bundle.Classes.Length == 0)
                ThrowValidation("classes", "missing or empty");

            if (_bundle.Methods == null)
                ThrowValidation("methods", "missing (expected array)");

            // Check expected class count sanity (tunable)
            if (_bundle.Classes.Length < 10)
                ThrowValidation("classes.count", $"unexpectedly small ({_bundle.Classes.Length})");

            // Ensure unique localIds across bundle (fast check)
            var seen = new HashSet<string>(StringComparer.Ordinal);
            void CheckAndAdd(string id, string kind)
            {
                if (string.IsNullOrEmpty(id))
                    ThrowValidation($"{kind}.localId", "null or empty localId");

                if (!seen.Add(id))
                    ThrowValidation("localId.duplicate", $"duplicate localId '{id}'");
            }

            foreach (var c in _bundle.Classes) CheckAndAdd(c.LocalId, "class");
            if (_bundle.Methods != null)
                foreach (var m in _bundle.Methods) CheckAndAdd(m.LocalId, "method");
            if (_bundle.Objects != null)
                foreach (var o in _bundle.Objects) CheckAndAdd(o.LocalId, "object");
            if (_bundle.InitExpressions != null)
                foreach (var ie in _bundle.InitExpressions) CheckAndAdd(ie.LocalId, "initExpression");

            // Validate symbol indices referenced by classes
            int maxSymbol = _bundle.Symbols.Length - 1;
            foreach (var cls in _bundle.Classes)
            {
                if (cls.NameIndex < 0 || cls.NameIndex > maxSymbol)
                    ThrowValidation("class.nameIndex", $"class {cls.LocalId} has invalid nameIndex {cls.NameIndex}");

                if (cls.SuperNameIndex.HasValue)
                {
                    var idx = cls.SuperNameIndex.Value;
                    if (idx < 0 || idx > maxSymbol)
                        ThrowValidation("class.superNameIndex", $"class {cls.LocalId} has invalid superNameIndex {idx}");
                }

                if (cls.InstVarNames != null)
                {
                    foreach (var n in cls.InstVarNames)
                        if (n < 0 || n > maxSymbol)
                            ThrowValidation("class.instVarNames", $"class {cls.LocalId} has invalid instVar name index {n}");
                }
            }

            // Validate method selector indices referenced by methods
            if (_bundle.Methods != null)
            {
                foreach (var m in _bundle.Methods)
                {
                    if (m.SelectorIndex < 0 || m.SelectorIndex > maxSymbol)
                        ThrowValidation("method.selectorIndex", $"method {m.LocalId} has invalid selector index {m.SelectorIndex}");
                }
            }
        }

        private void AllocateSymbols()
        {
            int count = _bundle.Symbols.Length;
            _reporter.LogInfo($"Phase1: Allocating {count} symbols");

            // Reserve local map not exported; symbol OOPs returned by heap manager
            for (int i = 0; i < count; i++)
            {
                string text = _bundle.Symbols[i] ?? string.Empty;
                byte[] utf8 = Encoding.UTF8.GetBytes(text);
                int bytes = utf8.Length;

                // Allocate ByteIndexed object (0 fixed slots)
                SmalltalkOOP symbolOOP = _heap.AllocateObject(ObjectFormat.ByteIndexed, fixedSlots: 0, indexedSize: bytes);

                // Write bytes
                for (int b = 0; b < bytes; b++)
                    _heap.SetIndexedByte(symbolOOP, b, utf8[b]);

                // NOTE: class slot left unset for now; Phase6 will set slot 0 for all objects
                _symbols.Register(i, symbolOOP);

                if ((i + 1) % 512 == 0)
                    _reporter.LogInfo($"  Allocated {i + 1}/{count} symbols");
            }

            // Spot-check a few symbol encodings for sanity
            if (count > 0)
            {
                var idx = Math.Min(0, count - 1);
                var sample = _symbols.GetSymbolOOP(idx);
                if (!_heap.IsValidOOP(sample))
                    ThrowValidation("symbol.allocation", $"symbol OOP invalid for index {idx}");
            }
        }

        private void ThrowValidation(string key, string message)
        {
            var err = new LoaderPhaseException($"Validation failed [{key}]: {message}");
            _reporter.LogValidationError(key, message);
            throw err;
        }
    }

    // ---- Interfaces and helpers (minimal shapes expected by the loader) ----

    public interface IHeapManager
    {
        SmalltalkOOP AllocateObject(ObjectFormat format, int fixedSlots, int indexedSize);
        void SetIndexedByte(SmalltalkOOP oop, int index, byte value);
        bool IsValidOOP(SmalltalkOOP oop);
    }

    public interface ISymbolRegistry
    {
        void Register(int index, SmalltalkOOP symbolOOP);
        SmalltalkOOP GetSymbolOOP(int index);
        void Freeze();
    }

    public interface IValidationReporter
    {
        void LogPhaseStart(string message);
        void LogPhaseSuccess(string message);
        void LogPhaseFailure(string phase, Exception ex);
        void LogInfo(string message);
        void RecordMetric(string key, int value);
        void LogValidationError(string key, string message);
    }

    // Small helper value types (placeholders)
    public enum ObjectFormat { Fixed, PointerIndexed, WordIndexed, ByteIndexed }
    public readonly struct SmalltalkOOP { public readonly uint Raw; public static SmalltalkOOP FromRaw(uint r) => new SmalltalkOOP(r); }
    public class ColdStartBundle { public string Version; public string[] Symbols; public ClassDescriptor[] Classes; public MethodDescriptor[] Methods; public ObjectDescriptor[] Objects; public InitExpression[] InitExpressions; }
    public class ClassDescriptor { public string LocalId; public int NameIndex; public int? SuperNameIndex; public int[] InstVarNames; }
    public class MethodDescriptor { public string LocalId; public int SelectorIndex; }
    public class ObjectDescriptor { public string LocalId; }
    public class InitExpression { public string LocalId; }

    public sealed class LoaderPhaseException : Exception
    {
        public LoaderPhaseException(string message) : base(message) { }
    }
}
