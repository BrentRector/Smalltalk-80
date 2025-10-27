// Phase5_DictionariesAndGlobals.cs
// Responsibility:
//  - Install CompiledMethod objects into per-class MethodDictionaries (open addressing, linear probing)
//  - Update MethodDictionary tallies
//  - Apply lightweight patches (if any) to methods or literals
//  - Populate USTable (runtime symbol dictionary) and register in SpecialObjectsArray
//  - Create SystemDictionary (Smalltalk) and populate with canonical associations and class entries
//  - Populate class pools (class-side variables) using canonical associations
//  - Register InitExpressionMethods array in SpecialObjectsArray
//
// Inputs (from previous phases):
//  - Phase4 outputs: CompiledMethods, CanonicalAssociations, MethodDictionaries, BlockOwnerMap
//  - LocalIdToObjectId map (class OOPs, sentinels, etc.)
//  - SymbolRegistry (frozen) and LayoutResolver
//  - SpecialObjectsArray slots for nil/true/false exist (sentinels)
//
// Outputs:
//  - Method dictionaries populated and installed into classes (class.methodDict fields)
//  - USTable allocated and stored
//  - SystemDictionary allocated and stored in SpecialObjectsArray
//  - Class pools populated
//  - InitExpressionMethods array created and stored in SpecialObjectsArray
//
// Notes:
//  - This implementation uses single-threaded insertion for determinism.
//  - Dictionary capacity semantics: MethodDictionaries were preallocated with an indexed size that is a power of two.
//  - All nil references used during insertion are the sentinel nil OOP from localIdToObjectId.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases
{
    public sealed class Phase5_DictionariesAndGlobals
    {
        private readonly ColdStartBundle _bundle;
        private readonly IHeapManager _heap;
        private readonly ILayoutResolver _layoutResolver;
        private readonly ISymbolRegistry _symbolRegistry;
        private readonly IValidationReporter _reporter;

        private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
        private readonly Dictionary<string, SmalltalkOOP> _compiledMethods;
        private readonly Dictionary<string, SmalltalkOOP> _canonicalAssociations;
        private readonly Dictionary<string, SmalltalkOOP> _methodDictionaries; // classLocalId -> dictOOP

        // Outputs:
        public SmalltalkOOP USTableOOP { get; private set; }
        public SmalltalkOOP SystemDictionaryOOP { get; private set; }

        public Phase5_DictionariesAndGlobals(
            ColdStartBundle bundle,
            IHeapManager heapManager,
            ILayoutResolver layoutResolver,
            ISymbolRegistry symbolRegistry,
            Dictionary<string, SmalltalkOOP> localIdToObjectId,
            Dictionary<string, SmalltalkOOP> compiledMethods,
            Dictionary<string, SmalltalkOOP> canonicalAssociations,
            Dictionary<string, SmalltalkOOP> methodDictionaries,
            IValidationReporter reporter)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _heap = heapManager ?? throw new ArgumentNullException(nameof(heapManager));
            _layoutResolver = layoutResolver ?? throw new ArgumentNullException(nameof(layoutResolver));
            _symbolRegistry = symbolRegistry ?? throw new ArgumentNullException(nameof(symbolRegistry));
            _localIdToObjectId = localIdToObjectId ?? throw new ArgumentNullException(nameof(localIdToObjectId));
            _compiledMethods = compiledMethods ?? throw new ArgumentNullException(nameof(compiledMethods));
            _canonicalAssociations = canonicalAssociations ?? throw new ArgumentNullException(nameof(canonicalAssociations));
            _methodDictionaries = methodDictionaries ?? throw new ArgumentNullException(nameof(methodDictionaries));
            _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
        }

        public void Run()
        {
            _reporter.LogPhaseStart("Phase 5: Method Dictionaries & Globals");

            try
            {
                InstallMethodsIntoDictionaries();
                SetClassMethodDictFields();
                ApplyPatchesIfAny();
                CreateAndPopulateUSTable();
                CreateAndPopulateSystemDictionary();
                PopulateClassPools();
                RegisterInitMethods();
                _reporter.LogPhaseSuccess("Phase 5: Method Dictionaries & Globals");
            }
            catch (LoaderPhaseException ex)
            {
                _reporter.LogPhaseFailure("Phase 5: Method Dictionaries & Globals", ex);
                throw;
            }
        }

        private void InstallMethodsIntoDictionaries()
        {
            _reporter.LogInfo($"Phase5: Installing {_compiledMethods.Count} compiled methods into dictionaries");

            var nilOOP = _localIdToObjectId.TryGetValue("nil", out var n) ? n : SmalltalkOOP.FromRaw(0);

            // Iterate compiled methods; each method descriptor LocalId encodes class => find target dictionary
            foreach (var kv in _compiledMethods)
            {
                string methodLocalId = kv.Key;
                SmalltalkOOP methodOOP = kv.Value;

                // Map methodLocalId -> class localId (MethodDescriptor.ClassLocalId was used earlier; here we parse stored id)
                // Assume methodLocalId format is "method:<classLocalId>>selector" or we have a separate index in bundle
                var methodDescriptor = _bundle.Methods.FirstOrDefault(m => m.LocalId == methodLocalId);
                if (methodDescriptor == null)
                    ThrowValidation("method.install.lookup", $"Method descriptor not found for {methodLocalId}");

                string classLocalId = NormalizeClassLocalId(methodDescriptor.ClassLocalId);

                if (!_methodDictionaries.TryGetValue(classLocalId, out var dictOOP))
                    ThrowValidation("method.install.dictMissing", $"MethodDictionary not found for class {classLocalId}");

                // Insert selector -> methodOOP into dictOOP using open addressing (linear probing)
                var selectorOOP = _symbolRegistry.GetSymbolOOP(methodDescriptor.SelectorIndex);
                InsertIntoDictionary(dictOOP, selectorOOP, methodOOP, nilOOP);

                // Note: tally increment done inside InsertIntoDictionary
            }

            _reporter.LogInfo("Phase5: All compiled methods installed into dictionaries");
        }

        private void InsertIntoDictionary(SmalltalkOOP dictOOP, SmalltalkOOP keyOOP, SmalltalkOOP valueOOP, SmalltalkOOP nilOOP)
        {
            int capacity = _heap.GetIndexedSize(dictOOP);
            if (capacity <= 0) ThrowValidation("dict.capacity", "MethodDictionary capacity <= 0");

            // Hash using symbol OOP raw value (positive) - consistent with Smalltalk-80 example
            int hash = ComputeHashForSymbol(keyOOP);
            int index = (hash & 0x7FFFFFFF) % capacity;

            // We store keys and values interleaved: key at index, value at index+1 (but our capacity is indexed slots count)
            // Because capacity is number of indexed slots, ensure probing steps over pairs.
            // We'll probe in steps of 2 to keep key/value pairs aligned.
            int baseIndex = (index & ~1); // make even index
            int probes = 0;
            while (true)
            {
                var existingKey = _heap.GetIndexedOOP(dictOOP, baseIndex);
                if (existingKey.Raw == nilOOP.Raw)
                {
                    // empty slot found -> insert key and value
                    _heap.SetIndexedOOP(dictOOP, baseIndex, keyOOP);
                    _heap.SetIndexedOOP(dictOOP, baseIndex + 1, valueOOP);

                    // increment tally
                    var tallyOOP = _heap.GetInstanceVariable(dictOOP, 0);
                    int tally = tallyOOP.ToSmallInteger();
                    _heap.SetInstanceVariable(dictOOP, 0, SmalltalkOOP.FromSmallInteger(tally + 1));
                    return;
                }

                if (existingKey.Raw == keyOOP.Raw)
                {
                    // replace existing value
                    _heap.SetIndexedOOP(dictOOP, baseIndex + 1, valueOOP);
                    return;
                }

                // advance to next pair
                baseIndex = (baseIndex + 2) % capacity;
                probes++;
                if (probes > capacity)
                    ThrowValidation("dict.insert", "Dictionary insertion failed - table full or probing loop");
            }
        }

        private int ComputeHashForSymbol(SmalltalkOOP symbolOOP)
        {
            // Use OOP raw value as hash (consistent with earlier phases)
            return (int)(symbolOOP.Raw & 0x7FFFFFFF);
        }

        private void SetClassMethodDictFields()
        {
            _reporter.LogInfo("Phase5: Setting class.methodDict fields for each class");

            foreach (var cls in _bundle.Classes)
            {
                string classLocalId = cls.LocalId;
                if (!_methodDictionaries.TryGetValue(classLocalId, out var dictOOP))
                {
                    // Classes with no methods may not have an entry if methodDictionaries allocation skipped; ensure minimal dict exists
                    dictOOP = GetOrCreateEmptyMethodDictionaryForClass(classLocalId);
                }

                int methodDictIndex = _layoutResolver.GetFieldIndex(cls.IsMetaclass ? "class:Metaclass" : "class:Class", "methodDict");
                if (methodDictIndex < 0)
                    ThrowValidation("layout.methodDictIndex", $"Could not find methodDict field index for {classLocalId}");

                var classOOP = _localIdToObjectId[classLocalId];
                _heap.SetInstanceVariable(classOOP, methodDictIndex, dictOOP);
            }
        }

        private SmalltalkOOP GetOrCreateEmptyMethodDictionaryForClass(string classLocalId)
        {
            if (_methodDictionaries.TryGetValue(classLocalId, out var dictOOP))
                return dictOOP;

            // allocate small minimal dictionary
            SmalltalkOOP dictClassOOP = _localIdToObjectId["class:MethodDictionary"];
            var dict = _heap.AllocateObject(ObjectFormat.PointerIndexed, fixedSlots: 1, indexedSize: 8);
            _heap.SetSlot(dict, 0, dictClassOOP);
            _heap.SetInstanceVariable(dict, 0, SmalltalkOOP.FromSmallInteger(0));
            var nilOOP = _localIdToObjectId["nil"];
            for (int i = 0; i < 8; i++) _heap.SetIndexedOOP(dict, i, nilOOP);

            _methodDictionaries[classLocalId] = dict;
            return dict;
        }

        private void ApplyPatchesIfAny()
        {
            _reporter.LogInfo("Phase5: Applying patches (if any)");

            // Example patching hook: iterate _bundle.Patches if present and apply simple fixes.
            if (_bundle.Patches == null || _bundle.Patches.Length == 0)
            {
                _reporter.LogInfo("Phase5: No patches in bundle");
                return;
            }

            foreach (var patch in _bundle.Patches)
            {
                // Minimal example: patch identifies a methodLocalId and an indexed literal to replace
                if (patch.Type == PatchType.ReplaceLiteral)
                {
                    if (!_compiledMethods.TryGetValue(patch.MethodLocalId, out var methodOOP))
                        ThrowValidation("patch.methodNotFound", $"Patch references missing method {patch.MethodLocalId}");

                    // set indexed literal at patch.LiteralIndex to new resolved value
                    var newLit = ResolvePatchLiteral(patch.Literal);
                    _heap.SetIndexedOOP(methodOOP, patch.LiteralIndex + 1, newLit); // +1 header offset
                }
            }
        }

        private SmalltalkOOP ResolvePatchLiteral(LiteralValue lit)
        {
            // Reuse Phase4 resolution logic where possible; minimal here: symbol/class/global
            switch (lit.Type)
            {
                case LiteralType.Symbol:
                    return _symbolRegistry.GetSymbolOOP(lit.SymbolIndex);
                case LiteralType.ClassRef:
                    return _localIdToObjectId.TryGetValue(NormalizeClassLocalId(lit.ClassLocalId), out var o) ? o : SmalltalkOOP.FromRaw(0);
                case LiteralType.Global:
                    return _canonicalAssociations.TryGetValue(lit.GlobalName, out var assoc) ? assoc : AllocateAndRegisterAssociation(lit.GlobalName);
                default:
                    ThrowValidation("patch.literalUnsupported", $"Unsupported patch literal type {lit.Type}");
                    return SmalltalkOOP.FromRaw(0);
            }
        }

        private SmalltalkOOP AllocateAndRegisterAssociation(string globalName)
        {
            if (_canonicalAssociations.TryGetValue(globalName, out var found)) return found;
            var assocClass = _localIdToObjectId["class:Association"];
            var assoc = _heap.AllocateObject(ObjectFormat.PointerIndexed, fixedSlots: 2, indexedSize: 0);
            _heap.SetSlot(assoc, 0, assocClass);
            var keySymbol = _symbolRegistry.GetSymbolOOP(_bundle.IndexOfSymbol(globalName));
            _heap.SetInstanceVariable(assoc, 0, keySymbol);
            var nilOOP = _localIdToObjectId["nil"];
            _heap.SetInstanceVariable(assoc, 1, nilOOP);
            _canonicalAssociations[globalName] = assoc;
            return assoc;
        }

        private void CreateAndPopulateUSTable()
        {
            _reporter.LogInfo("Phase5: Creating and populating USTable (runtime symbol table)");

            int symbolCount = _bundle.Symbols?.Length ?? 0;
            int capacity = NextPowerOfTwo((int)Math.Ceiling(symbolCount / 0.75));

            SmalltalkOOP dictClass = _localIdToObjectId["class:Dictionary"];
            var ustable = _heap.AllocateObject(ObjectFormat.PointerIndexed, fixedSlots: 1, indexedSize: capacity);
            _heap.SetSlot(ustable, 0, dictClass);
            _heap.SetInstanceVariable(ustable, 0, SmalltalkOOP.FromSmallInteger(0));

            var nilOOP = _localIdToObjectId["nil"];
            for (int i = 0; i < capacity; i++) _heap.SetIndexedOOP(ustable, i, nilOOP);

            // Insert each symbol as key -> symbol (key/value = symbol)
            for (int i = 0; i < symbolCount; i++)
            {
                var symOOP = _symbolRegistry.GetSymbolOOP(i);
                InsertIntoDictionary(ustable, symOOP, symOOP, nilOOP);
            }

            USTableOOP = ustable;
            _reporter.RecordMetric("phase5.ustableCapacity", capacity);
            _reporter.LogInfo($"Phase5: USTable populated with {symbolCount} symbols");
        }

        private void CreateAndPopulateSystemDictionary()
        {
            _reporter.LogInfo("Phase5: Creating and populating SystemDictionary (Smalltalk)");

            // Estimate globals count (classes + known system globals)
            int classCount = _bundle.Classes.Count(c => !c.IsMetaclass);
            int extra = 128; // buffer for system & user globals
            int capacity = NextPowerOfTwo((int)Math.Ceiling((classCount + extra) / 0.75));

            SmalltalkOOP systemDictClass = _localIdToObjectId["class:SystemDictionary"];
            var systemDict = _heap.AllocateObject(ObjectFormat.PointerIndexed, fixedSlots: 1, indexedSize: capacity);
            _heap.SetSlot(systemDict, 0, systemDictClass);
            _heap.SetInstanceVariable(systemDict, 0, SmalltalkOOP.FromSmallInteger(0));
            var nilOOP = _localIdToObjectId["nil"];
            for (int i = 0; i < capacity; i++) _heap.SetIndexedOOP(systemDict, i, nilOOP);

            // Insert class associations: for each regular class, create or reuse canonical association and set value to class OOP
            foreach (var cls in _bundle.Classes.Where(c => !c.IsMetaclass))
            {
                string className = _bundle.Symbols[cls.NameIndex];
                var assoc = AllocateAndRegisterAssociation(className);
                // set value to class OOP
                if (_localIdToObjectId.TryGetValue(cls.LocalId, out var classOOP))
                    _heap.SetInstanceVariable(assoc, 1, classOOP);

                InsertIntoDictionary(systemDict, _heap.GetInstanceVariable(assoc, 0), assoc, nilOOP); // key = symbol, value = assoc
            }

            // Insert system globals: Smalltalk -> systemDict, Processor -> nil placeholder, etc.
            var smalltalkAssoc = AllocateAndRegisterAssociation("Smalltalk");
            _heap.SetInstanceVariable(smalltalkAssoc, 1, systemDict);
            InsertIntoDictionary(systemDict, _heap.GetInstanceVariable(smalltalkAssoc, 0), smalltalkAssoc, nilOOP);

            SystemDictionaryOOP = systemDict;

            // Register SystemDictionary in SpecialObjectsArray (store index via known setter)
            // Caller/Loader should set SpecialObjectsArray.Smalltalk to SystemDictionaryOOP in Phase6 when class slots are final.
            _reporter.LogInfo("Phase5: SystemDictionary populated");
        }

        private void PopulateClassPools()
        {
            _reporter.LogInfo("Phase5: Populating class pools (class-side variables) where present");

            // The bundle may contain ClassPool entries per class; iterate and insert associations
            if (_bundle.Classes == null) return;

            foreach (var cls in _bundle.Classes)
            {
                if (cls.ClassPool == null || cls.ClassPool.Count == 0) continue;
                // Find class pool dict OOP created in Phase2/Phase4 - assume stored in localIdToObjectId as "classpool:<classLocalId>" or retrievable via helper
                if (!_localIdToObjectId.TryGetValue($"classpool:{cls.LocalId}", out var poolDictOOP))
                {
                    // If not preallocated, allocate a small dictionary for pool
                    var dictClass = _localIdToObjectId["class:Dictionary"];
                    poolDictOOP = _heap.AllocateObject(ObjectFormat.PointerIndexed, fixedSlots: 1, indexedSize: NextPowerOfTwo((int)Math.Ceiling(cls.ClassPool.Count / 0.75)));
                    _heap.SetSlot(poolDictOOP, 0, dictClass);
                    _heap.SetInstanceVariable(poolDictOOP, 0, SmalltalkOOP.FromSmallInteger(0));
                    var nilOOP = _localIdToObjectId["nil"];
                    for (int i = 0; i < _heap.GetIndexedSize(poolDictOOP); i++) _heap.SetIndexedOOP(poolDictOOP, i, nilOOP);
                    _localIdToObjectId[$"classpool:{cls.LocalId}"] = poolDictOOP;
                }

                var nilOOP = _localIdToObjectId["nil"];
                foreach (var kv in cls.ClassPool)
                {
                    string varName = kv.Key;
                    // Resolve value literal using same semantics as Phase4 ResolveLiteral (simplified)
                    SmalltalkOOP value = ResolvePoolValue(kv.Value);
                    var assoc = AllocateAndRegisterAssociation(varName);
                    _heap.SetInstanceVariable(assoc, 1, value);
                    // Insert assoc into poolDict with key = assoc.key symbol
                    var keySym = _heap.GetInstanceVariable(assoc, 0);
                    InsertIntoDictionary(poolDictOOP, keySym, assoc, nilOOP);
                }
            }

            _reporter.LogInfo("Phase5: Class pools populated");
        }

        private SmalltalkOOP ResolvePoolValue(PoolValue v)
        {
            // Simplified resolver: support smallInt, symbol, classRef, string
            switch (v.Type)
            {
                case PoolValueType.SmallInteger: return SmalltalkOOP.FromSmallInteger(v.IntValue);
                case PoolValueType.Symbol: return _symbolRegistry.GetSymbolOOP(v.SymbolIndex);
                case PoolValueType.ClassRef:
                    var cl = NormalizeClassLocalId(v.ClassLocalId);
                    if (_localIdToObjectId.TryGetValue(cl, out var oop)) return oop;
                    return SmalltalkOOP.FromRaw(0);
                case PoolValueType.String:
                    // allocate string
                    var utf = System.Text.Encoding.UTF8.GetBytes(v.StringValue ?? "");
                    var sClass = _localIdToObjectId["class:String"];
                    var strOOP = _heap.AllocateObject(ObjectFormat.ByteIndexed, 0, utf.Length);
                    _heap.SetSlot(strOOP, 0, sClass);
                    for (int i = 0; i < utf.Length; i++) _heap.SetIndexedByte(strOOP, i, utf[i]);
                    return strOOP;
                default:
                    return SmalltalkOOP.FromRaw(0);
            }
        }

        private void RegisterInitMethods()
        {
            _reporter.LogInfo("Phase5: Registering InitExpressionMethods in SpecialObjectsArray");

            if (_bundle.InitExpressions == null || _bundle.InitExpressions.Length == 0)
            {
                _reporter.LogInfo("Phase5: No init expressions present");
                return;
            }

            // Assume compiled init methods were produced in Phase4 and are present in _compiledMethods map under their LocalIds
            var initMethodOOPs = new List<SmalltalkOOP>();
            foreach (var ie in _bundle.InitExpressions.OrderBy(x => x.Order))
            {
                if (!_compiledMethods.TryGetValue(ie.LocalId, out var mOOP))
                    ThrowValidation("init.methodMissing", $"Init expression method {ie.LocalId} not compiled");
                initMethodOOPs.Add(mOOP);
            }

            // Allocate array for init methods
            var arrayClass = _localIdToObjectId["class:Array"];
            var initArray = _heap.AllocateObject(ObjectFormat.PointerIndexed, fixedSlots: 0, indexedSize: initMethodOOPs.Count);
            _heap.SetSlot(initArray, 0, arrayClass);
            for (int i = 0; i < initMethodOOPs.Count; i++) _heap.SetIndexedOOP(initArray, i, initMethodOOPs[i]);

            // Store in localIdToObjectId so Phase6 can put into SpecialObjectsArray (or set directly if special object available)
            _localIdToObjectId["InitExpressionMethods"] = initArray;
            _reporter.LogInfo($"Phase5: Registered {initMethodOOPs.Count} init methods");
        }

        // Helpers

        private int NextPowerOfTwo(int n)
        {
            int p = 1;
            while (p < n) p <<= 1;
            return p;
        }

        private string NormalizeClassLocalId(string incoming)
        {
            if (string.IsNullOrEmpty(incoming)) return incoming;
            if (incoming.StartsWith("class:") || incoming.StartsWith("meta:")) return incoming;
            return "class:" + incoming;
        }

        private void ThrowValidation(string key, string message)
        {
            var ex = new LoaderPhaseException($"Phase5 validation failed [{key}]: {message}");
            _reporter.LogValidationError(key, message);
            throw ex;
        }

        // Minimal shapes for patching, class pools, and pool values
        public class Patch { public PatchType Type; public string MethodLocalId; public LiteralValue Literal; public int LiteralIndex; }
        public enum PatchType { ReplaceLiteral }

        // Minimal pool types
        public class PoolValue { public PoolValueType Type; public int IntValue; public int SymbolIndex; public string ClassLocalId; public string StringValue; }
        public enum PoolValueType { SmallInteger, Symbol, ClassRef, String }

        private SmalltalkOOP AllocateAndRegisterAssociation(string name)
        {
            if (_canonicalAssociations.TryGetValue(name, out var assoc)) return assoc;
            var assocClass = _localIdToObjectId["class:Association"];
            var assocOOP = _heap.AllocateObject(ObjectFormat.PointerIndexed, fixedSlots: 2, indexedSize: 0);
            _heap.SetSlot(assocOOP, 0, assocClass);
            var keySym = _symbolRegistry.GetSymbolOOP(_bundle.IndexOfSymbol(name));
            _heap.SetInstanceVariable(assocOOP, 0, keySym);
            var nilOOP = _localIdToObjectId["nil"];
            _heap.SetInstanceVariable(assocOOP, 1, nilOOP);
            _canonicalAssociations[name] = assocOOP;
            return assocOOP;
        }
    }

    // Extensions to ColdStartBundle for patches and class pool shapes (optional in bundle)
    public partial class ColdStartBundle
    {
        public Patch[] Patches;
        public BundleClassDescriptor[] Classes; // if different shape from earlier, otherwise reuse
        public InitExpressionDescriptor[] InitExpressions;
        public int IndexOfSymbol(string name) => Array.IndexOf(Symbols, name);
    }

    public partial class BundleClassDescriptor
    {
        public string LocalId;
        public Dictionary<string, PoolValue> ClassPool; // name -> value
    }

    public class InitExpressionDescriptor { public string LocalId; public int Order; }

    // Heap helpers required
    public partial interface IHeapManager
    {
        int GetIndexedSize(SmalltalkOOP objectOOP);
        SmalltalkOOP GetIndexedOOP(SmalltalkOOP objectOOP, int index);
        void SetIndexedOOP(SmalltalkOOP objectOOP, int index, SmalltalkOOP value);
        SmalltalkOOP GetInstanceVariable(SmalltalkOOP objectOOP, int fieldIndex);
        void SetInstanceVariable(SmalltalkOOP objectOOP, int fieldIndex, SmalltalkOOP value);
    }
}
