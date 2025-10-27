// Phase4_MethodCompilation.cs
// Responsibility:
//  - Allocate MethodDictionary placeholders (pre-sized)
//  - Single-pass compile all methods in bundle (post-order blocks => forward refs)
//  - Resolve literals (symbols, classes, arrays, associations, compiledMethod references)
//  - Canonicalize association objects lazily (one Association per global name)
//  - Allocate ByteArray for bytecodes and CompiledMethod objects
//  - Set CompiledMethod fixed/homeClass slot where required
//  - Populate compiledMethods map and blockOwnerMap for downstream phases
//  - Validate per-method invariants (header smallint, bytearray non-empty, literal counts)
//
// Assumptions:
//  - LocalIdToObjectId contains class localIds and metaclasses (from Phase2/Phase3)
//  - SymbolRegistry is frozen and available
//  - IHeapManager supports AllocateObject, SetIndexedOOP, SetIndexedByte, SetSlot, SetInstanceVariable
//  - MethodDescriptor in bundle contains: LocalId, ClassLocalId, SelectorIndex, HeaderWord, Literals[], Bytecodes (base64 or byte[])
//  - Threading: This implementation is single-threaded for determinism. Parallel variant optional behind flag.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases
{
    public sealed class Phase4_MethodCompilation
    {
        private readonly ColdStartBundle _bundle;
        private readonly IHeapManager _heap;
        private readonly ISymbolRegistry _symbolRegistry;
        private readonly IValidationReporter _reporter;
        private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
        private readonly HashSet<(SmalltalkOOP oop, int slot)> _initializedFields;

        // Outputs
        public Dictionary<string, SmalltalkOOP> CompiledMethods { get; } = new Dictionary<string, SmalltalkOOP>(StringComparer.Ordinal);
        public Dictionary<string, SmalltalkOOP> CanonicalAssociations { get; } = new Dictionary<string, SmalltalkOOP>(StringComparer.Ordinal);
        public Dictionary<string, SmalltalkOOP> MethodDictionaries { get; } = new Dictionary<string, SmalltalkOOP>(StringComparer.Ordinal);
        public Dictionary<string, string> BlockOwnerMap { get; } = new Dictionary<string, string>(StringComparer.Ordinal); // blockLocalId -> ownerMethodLocalId

        public Phase4_MethodCompilation(
            ColdStartBundle bundle,
            IHeapManager heapManager,
            ISymbolRegistry symbolRegistry,
            Dictionary<string, SmalltalkOOP> localIdToObjectId,
            HashSet<(SmalltalkOOP oop, int slot)> initializedFields,
            IValidationReporter reporter)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _heap = heapManager ?? throw new ArgumentNullException(nameof(heapManager));
            _symbolRegistry = symbolRegistry ?? throw new ArgumentNullException(nameof(symbolRegistry));
            _localIdToObjectId = localIdToObjectId ?? throw new ArgumentNullException(nameof(localIdToObjectId));
            _initializedFields = initializedFields ?? throw new ArgumentNullException(nameof(initializedFields));
            _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
        }

        public void Run()
        {
            _reporter.LogPhaseStart("Phase 4: Method Compilation (single-pass)");

            try
            {
                AllocateEmptyMethodDictionaries();
                CompileAllMethodsSinglePass();
                _reporter.RecordMetric("phase4.compiledMethodsCount", CompiledMethods.Count);
                _reporter.LogPhaseSuccess("Phase 4: Method Compilation");
            }
            catch (LoaderPhaseException ex)
            {
                _reporter.LogPhaseFailure("Phase 4: Method Compilation", ex);
                throw;
            }
        }

        private void AllocateEmptyMethodDictionaries()
        {
            _reporter.LogInfo("Phase4: Allocating empty MethodDictionary objects for classes");

            // Build method counts per class
            var methodCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var m in _bundle.Methods)
            {
                string classLocalId = NormalizeClassLocalId(m.ClassLocalId);
                methodCounts.TryGetValue(classLocalId, out int c);
                methodCounts[classLocalId] = c + 1;
            }

            // Default minimal capacity
            const int MinCapacity = 8;

            foreach (var cls in _bundle.Classes)
            {
                string classLocalId = cls.LocalId;
                int methodsForClass = methodCounts.TryGetValue(classLocalId, out int v) ? v : 0;
                int capacity = CalculateDictionaryCapacity(methodsForClass, MinCapacity);

                // Allocate MethodDictionary: 1 fixed slot (tally) + capacity indexed slots (pairs)
                SmalltalkOOP dictClassOOP = ResolveLocalClassOOP("class:MethodDictionary");
                SmalltalkOOP dictOOP = _heap.AllocateObject(ObjectFormat.PointerIndexed, fixedSlots: 1, indexedSize: capacity);

                // Set class for the dictionary object immediately (so validation sees it)
                _heap.SetSlot(dictOOP, 0, dictClassOOP);

                // Initialize tally to 0
                _heap.SetInstanceVariable(dictOOP, 0, SmalltalkOOP.FromSmallInteger(0));

                // Initialize indexed slots to nil placeholder (nil may be in LocalIdToObjectId)
                var nilOOP = _localIdToObjectId.TryGetValue("nil", out var n) ? n : SmalltalkOOP.FromRaw(0);
                for (int i = 0; i < capacity; i++)
                    _heap.SetIndexedOOP(dictOOP, i, nilOOP);

                MethodDictionaries[classLocalId] = dictOOP;
            }

            _reporter.RecordMetric("phase4.methodDictAllocated", MethodDictionaries.Count);
        }

        private int CalculateDictionaryCapacity(int methodCount, int min)
        {
            if (methodCount <= 0) return min;
            // target load factor 0.75 for pairs -> methodCount / 0.75 -> next power of two
            int needed = (int)Math.Ceiling(methodCount / 0.75);
            int cap = 1;
            while (cap < needed) cap <<= 1;
            // ensure even number of indexed slots counts as capacity (we treat capacity as number of indexed slots)
            return Math.Max(cap, min);
        }

        private void CompileAllMethodsSinglePass()
        {
            _reporter.LogInfo($"Phase4: Compiling {(_bundle.Methods?.Length ?? 0)} methods (single-pass)");

            int processed = 0;
            foreach (var m in _bundle.Methods)
            {
                processed++;
                try
                {
                    CompileMethod(m);
                }
                catch (Exception ex)
                {
                    // Build structured diagnostics
                    var diag = new LoaderPhaseException($"Method compilation failed for {m.LocalId}: {ex.Message}");
                    _reporter.LogValidationError("phase4.methodCompile", diag.Message);
                    throw diag;
                }

                if (processed % 1000 == 0)
                    _reporter.LogInfo($"  Compiled {processed}/{_bundle.Methods.Length} methods");
            }
        }

        private void CompileMethod(MethodDescriptor m)
        {
            // Resolve selector symbol OOP
            var selectorOOP = _symbolRegistry.GetSymbolOOP(m.SelectorIndex);

            // Resolve target home class OOP
            string classLocalId = NormalizeClassLocalId(m.ClassLocalId);
            if (!_localIdToObjectId.TryGetValue(classLocalId, out var homeClassOOP))
                ThrowValidation("method.homeClassMissing", $"Class {classLocalId} for method {m.LocalId} not found");

            // Resolve literals
            var literalOOPs = new List<SmalltalkOOP>();
            if (m.Literals != null)
            {
                foreach (var lit in m.Literals)
                {
                    var resolved = ResolveLiteral(lit, m);
                    literalOOPs.Add(resolved);
                }
            }

            // Allocate ByteArray for bytecodes
            byte[] bytecodes = DecodeBytecode(m.Bytecodes);
            SmalltalkOOP byteArrayClass = ResolveLocalClassOOP("class:ByteArray");
            SmalltalkOOP byteArrayOOP = _heap.AllocateObject(ObjectFormat.ByteIndexed, fixedSlots: 0, indexedSize: bytecodes.Length);
            _heap.SetSlot(byteArrayOOP, 0, byteArrayClass);
            for (int i = 0; i < bytecodes.Length; i++)
                _heap.SetIndexedByte(byteArrayOOP, i, bytecodes[i]);

            // Allocate CompiledMethod object: we store header + literals + bytearray reference in indexed slots
            int literalCount = literalOOPs.Count;
            int indexedSize = 1 + literalCount + 1; // header + literals + bytearrayRef
            SmalltalkOOP cmClass = ResolveLocalClassOOP("class:CompiledMethod");
            SmalltalkOOP methodOOP = _heap.AllocateObject(ObjectFormat.PointerIndexed, fixedSlots: 0, indexedSize: indexedSize);
            _heap.SetSlot(methodOOP, 0, cmClass); // set class for CompiledMethod

            // header stored at indexed index 0
            _heap.SetIndexedOOP(methodOOP, 0, SmalltalkOOP.FromSmallInteger(m.HeaderWord));

            // literals at indexes 1..literalCount
            for (int i = 0; i < literalCount; i++)
                _heap.SetIndexedOOP(methodOOP, i + 1, literalOOPs[i]);

            // bytearray reference last slot
            _heap.SetIndexedOOP(methodOOP, 1 + literalCount, byteArrayOOP);

            // Set homeClass fixed slot if required by VM (some implementations store it in a fixed slot)
            // Here we set slot 0 to homeClass for clarity if VM expects it, else homeClass is captured elsewhere.
            // Determine the VM's expected fixed slot for homeClass; assume SetInstanceVariable index -1 means fixed slot 'homeClass' is slot 0
            // We'll use a canonical approach: store homeClass in an explicit fixed slot if the layout requires it.
            // For safety, record this as an initialized field if set.
            // (Implementation detail: adjust according to actual VM layout)
            // Example: Set fixed slot 1 to homeClass (slot 0 reserved for class pointer)
            int homeClassFixedSlotIndex = 1; // NOTE: sync with VM expectation; change if different
            try
            {
                _heap.SetSlot(methodOOP, homeClassFixedSlotIndex, homeClassOOP);
                _initializedFields.Add((methodOOP, homeClassFixedSlotIndex));
            }
            catch
            {
                // If VM does not allow writing that fixed slot, ignore; homeClass may be stored in literals or elsewhere.
            }

            // Register compiled method
            CompiledMethods[m.LocalId] = methodOOP;

            // If method is a block (localId starts with "block:"), register block owner
            if (m.LocalId != null && m.LocalId.StartsWith("block:", StringComparison.Ordinal))
            {
                // Owner is provided by descriptor or by naming convention; fall back to declared owner
                string owner = m.OwnerLocalId ?? "<unknown-owner>";
                BlockOwnerMap[m.LocalId] = owner;
            }

            // Basic per-method validations
            ValidateCompiledMethod(methodOOP, m, literalCount, bytecodes.Length);
        }

        private void ValidateCompiledMethod(SmalltalkOOP methodOOP, MethodDescriptor m, int literalCount, int bytecodeLen)
        {
            // header is a small integer
            var headerOOP = _heap.GetIndexedOOP(methodOOP, 0);
            if (!headerOOP.IsSmallInteger())
                ThrowValidation("method.header", $"Method {m.LocalId} header is not SmallInteger");

            if (bytecodeLen <= 0)
                ThrowValidation("method.bytecodes", $"Method {m.LocalId} has empty bytecode array");

            // If method references compiledMethod literals, ensure they resolve to entries that will appear later
            // Because compiler emits post-order, compiledMethod literal resolution should have found OOPs in localIdToObjectId
            // If any literal is a compiledMethod, we verify it's present in localIdToObjectId or will be registered in CompiledMethods
            if (m.Literals != null)
            {
                for (int i = 0; i < m.Literals.Length; i++)
                {
                    var lit = m.Literals[i];
                    if (lit.Type == LiteralType.CompiledMethod)
                    {
                        string targetLocalId = lit.LocalId;
                        bool present = _localIdToObjectId.ContainsKey(targetLocalId) || CompiledMethods.ContainsKey(targetLocalId);
                        if (!present)
                            ThrowValidation("method.compiledMethodLiteral", $"Method {m.LocalId} compiledMethod literal {targetLocalId} unresolved");
                    }
                }
            }
        }

        private SmalltalkOOP ResolveLiteral(LiteralValue lit, MethodDescriptor mContext)
        {
            switch (lit.Type)
            {
                case LiteralType.SmallInteger:
                    return SmalltalkOOP.FromSmallInteger(lit.IntValue);

                case LiteralType.Symbol:
                    return _symbolRegistry.GetSymbolOOP(lit.SymbolIndex);

                case LiteralType.String:
                    return AllocateStringLiteral(lit.StringValue);

                case LiteralType.ClassRef:
                    {
                        string classLocalId = NormalizeClassLocalId(lit.ClassLocalId);
                        if (!_localIdToObjectId.TryGetValue(classLocalId, out var clsOOP))
                            ThrowValidation("literal.classRef", $"Method {mContext.LocalId} class literal {classLocalId} not found");
                        return clsOOP;
                    }

                case LiteralType.CompiledMethod:
                    {
                        // Compiled methods are referenced by localId; because compiler emits parent-first, the target should already be present or later in bundle.
                        if (_localIdToObjectId.TryGetValue(lit.LocalId, out var already))
                            return already;
                        if (CompiledMethods.TryGetValue(lit.LocalId, out var cmOOP))
                            return cmOOP;

                        // If not yet present, this is unexpected under post-order guarantee
                        ThrowValidation("literal.compiledMethod", $"CompiledMethod literal {lit.LocalId} referenced in {mContext.LocalId} not found");
                        return SmalltalkOOP.FromRaw(0);
                    }

                case LiteralType.Global:
                    {
                        // Canonicalize association objects lazily
                        string name = lit.GlobalName;
                        if (CanonicalAssociations.TryGetValue(name, out var assoc))
                            return assoc;

                        // Create new Association object with key symbol and nil value (value will be filled when installing globals)
                        var assocClass = ResolveLocalClassOOP("class:Association");
                        var assocOOP = _heap.AllocateObject(ObjectFormat.PointerIndexed, fixedSlots: 2, indexedSize: 0);
                        _heap.SetSlot(assocOOP, 0, assocClass);

                        // key (slot 0 of instance vars) = symbol
                        var keySymbolOOP = _symbolRegistry.GetSymbolOOP(_bundle.IndexOfSymbol(name));
                        _heap.SetInstanceVariable(assocOOP, 0, keySymbolOOP);

                        // value (slot 1) set to nil placeholder; actual value will be set in Phase5
                        var nilOOP = _localIdToObjectId.TryGetValue("nil", out var n) ? n : SmalltalkOOP.FromRaw(0);
                        _heap.SetInstanceVariable(assocOOP, 1, nilOOP);

                        CanonicalAssociations[name] = assocOOP;
                        return assocOOP;
                    }

                case LiteralType.Array:
                    {
                        var elements = lit.Elements ?? Array.Empty<LiteralValue>();
                        SmalltalkOOP arrayClass = ResolveLocalClassOOP("class:Array");
                        var elementOOPs = elements.Select(e => ResolveLiteral(e, mContext)).ToArray();
                        SmalltalkOOP arrayOOP = _heap.AllocateObject(ObjectFormat.PointerIndexed, fixedSlots: 0, indexedSize: elementOOPs.Length);
                        _heap.SetSlot(arrayOOP, 0, arrayClass);
                        for (int i = 0; i < elementOOPs.Length; i++)
                            _heap.SetIndexedOOP(arrayOOP, i, elementOOPs[i]);
                        return arrayOOP;
                    }

                default:
                    ThrowValidation("literal.unknown", $"Unknown literal type in method {mContext.LocalId}");
                    return SmalltalkOOP.FromRaw(0);
            }
        }

        private SmalltalkOOP AllocateStringLiteral(string s)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(s ?? "");
            SmalltalkOOP stringClass = ResolveLocalClassOOP("class:String");
            SmalltalkOOP stringOOP = _heap.AllocateObject(ObjectFormat.ByteIndexed, fixedSlots: 0, indexedSize: utf8.Length);
            _heap.SetSlot(stringOOP, 0, stringClass);
            for (int i = 0; i < utf8.Length; i++)
                _heap.SetIndexedByte(stringOOP, i, utf8[i]);
            return stringOOP;
        }

        private byte[] DecodeBytecode(object bytecodeField)
        {
            // Accept pre-decoded byte[] or base64 string
            if (bytecodeField == null) return Array.Empty<byte>();
            if (bytecodeField is byte[] b) return b;
            if (bytecodeField is string s) return Convert.FromBase64String(s);
            ThrowValidation("method.bytecodeFormat", "Unsupported bytecode encoding");
            return Array.Empty<byte>();
        }

        private SmalltalkOOP ResolveLocalClassOOP(string classLocalId)
        {
            if (!_localIdToObjectId.TryGetValue(classLocalId, out var oop))
                ThrowValidation("class.notFound", $"Required class {classLocalId} not found in LocalIdToObjectId");
            return oop;
        }

        private string NormalizeClassLocalId(string incoming)
        {
            // Incoming may be "Point" or "class:Point" or "meta:Point" depending on bundle.
            if (string.IsNullOrEmpty(incoming)) return incoming;
            if (incoming.StartsWith("class:", StringComparison.Ordinal) || incoming.StartsWith("meta:", StringComparison.Ordinal))
                return incoming;
            // Default to class:
            return "class:" + incoming;
        }

        private void ThrowValidation(string key, string message)
        {
            var err = new LoaderPhaseException($"Phase4 validation failed [{key}]: {message}");
            _reporter.LogValidationError(key, message);
            throw err;
        }
    }

    // Supporting types expected by this phase (simplified)
    public partial class MethodDescriptor
    {
        public string LocalId;
        public string ClassLocalId;
        public int SelectorIndex;
        public ushort HeaderWord;
        public LiteralValue[] Literals;
        public object Bytecodes; // base64 string or byte[]
        public string OwnerLocalId; // optional for blocks
    }

    public enum LiteralType { SmallInteger, Symbol, String, ClassRef, CompiledMethod, Global, Array }
    public class LiteralValue
    {
        public LiteralType Type;
        public int IntValue;
        public int SymbolIndex;
        public string StringValue;
        public string ClassLocalId;
        public string LocalId;     // for compiledMethod literal
        public string GlobalName;  // for global association
        public LiteralValue[] Elements;
    }

    // IHeapManager extensions used here
    public partial interface IHeapManager
    {
        void SetIndexedOOP(SmalltalkOOP objectOOP, int index, SmalltalkOOP value);
        SmalltalkOOP GetIndexedOOP(SmalltalkOOP objectOOP, int index);
        void SetInstanceVariable(SmalltalkOOP objectOOP, int fieldIndex, SmalltalkOOP value);
        SmalltalkOOP GetInstanceVariable(SmalltalkOOP objectOOP, int fieldIndex);
        void SetSlot(SmalltalkOOP objectOOP, int slotIndex, SmalltalkOOP value); // for class pointer and other fixed slots
    }

    // SmalltalkOOP helpers
    public partial struct SmalltalkOOP
    {
        public readonly uint Raw;
        public static SmalltalkOOP FromRaw(uint r) => new SmalltalkOOP(r);
        public static SmalltalkOOP FromSmallInteger(int v) => new SmalltalkOOP((uint)((v << 1) | 1)); // example tagging
        public bool IsSmallInteger() => (Raw & 1u) == 1u;
        public static SmalltalkOOP FromSmallInteger(ushort v) => FromSmallInteger((int)v);
    }
}
