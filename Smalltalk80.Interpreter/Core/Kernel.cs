using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Smalltalk80.Interpreter.Core
{
    /// <summary>
    /// Kernel services for the Smalltalk VM.
    /// Provides object allocation, method lookup, and global namespace management.
    /// </summary>
    public class Kernel
    {
        private readonly ILogger<Kernel> _logger;
        
        /// <summary>
        /// Global namespace dictionary.
        /// Maps symbol names to their values.
        /// </summary>
        public Dictionary<SSymbol, object> Globals { get; } = new();

        /// <summary>
        /// Method lookup cache for performance.
        /// Key is (class, selector), value is (method, version).
        /// </summary>
        private readonly Dictionary<(SClass, SSymbol), (SCompiledMethod method, int version)> _lookupCache = new();

        // Core class references
        public SClass ObjectClass { get; set; }
        public SClass BehaviorClass { get; set; }
        public SClass ClassDescriptionClass { get; set; }
        public SClass MetaclassClass { get; set; }
        public SClass ClassClass { get; set; }
        
        // Essential classes
        public SClass CompiledMethodClass { get; set; }
        public SClass MethodDictionaryClass { get; set; }
        public SClass ArrayClass { get; set; }
        public SClass ByteArrayClass { get; set; }
        public SClass StringClass { get; set; }
        public SClass SymbolClass { get; set; }
        
        // Value classes
        public SClass SmallIntegerClass { get; set; }
        public SClass UndefinedObjectClass { get; set; }
        public SClass TrueClass { get; set; }
        public SClass FalseClass { get; set; }
        
        // Collection classes
        public SClass DictionaryClass { get; set; }
        public SClass SystemDictionaryClass { get; set; }
        
        // Context classes
        public SClass MethodContextClass { get; set; }
        public SClass BlockContextClass { get; set; }

        /// <summary>
        /// Special objects array for VM access.
        /// </summary>
        public SArray SpecialObjectsArray { get; set; }

        /// <summary>
        /// Creates a new kernel.
        /// </summary>
        public Kernel(ILogger<Kernel> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Allocates a new object of the specified class.
        /// </summary>
        public SObject BasicNew(SClass klass)
        {
            if (klass == null)
                throw new ArgumentNullException(nameof(klass));
            
            var obj = klass.BasicNew();
            _logger.LogTrace("Allocated {Class}", klass.Name);
            return obj;
        }

        /// <summary>
        /// Allocates a new object with indexed size.
        /// </summary>
        public SObject BasicNew(SClass klass, int size)
        {
            if (klass == null)
                throw new ArgumentNullException(nameof(klass));
            
            if (size < 0)
                throw new ArgumentOutOfRangeException(nameof(size), "Size must be non-negative");
            
            var obj = klass.BasicNew(size);
            _logger.LogTrace("Allocated {Class}[{Size}]", klass.Name, size);
            return obj;
        }

        /// <summary>
        /// Looks up a method in a class hierarchy with caching.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public SCompiledMethod Lookup(SClass klass, SSymbol selector)
        {
            if (klass == null || selector == null)
                return null;
            
            // Check cache first
            var key = (klass, selector);
            if (_lookupCache.TryGetValue(key, out var cached))
            {
                // Validate cache entry is still valid
                if (cached.version == klass.MethodDict.Version)
                {
                    _logger.LogTrace("Cache hit: {Class}>>{Selector}", klass.Name, selector.Value);
                    return cached.method;
                }
            }
            
            // Perform lookup
            var method = klass.LookupMethod(selector);
            
            if (method != null)
            {
                // Cache the result with current version
                _lookupCache[key] = (method, klass.MethodDict.Version);
                _logger.LogTrace("Lookup: {Class}>>{Selector} found", klass.Name, selector.Value);
            }
            else
            {
                _logger.LogTrace("Lookup: {Class}>>{Selector} not found", klass.Name, selector.Value);
            }
            
            return method;
        }

        /// <summary>
        /// Invalidates the method lookup cache.
        /// Called when methods are added/removed.
        /// </summary>
        public void InvalidateCache()
        {
            _lookupCache.Clear();
            _logger.LogDebug("Method lookup cache invalidated");
        }

        /// <summary>
        /// Invalidates cache entries for a specific class.
        /// </summary>
        public void InvalidateCacheForClass(SClass klass)
        {
            var toRemove = new List<(SClass, SSymbol)>();
            
            foreach (var key in _lookupCache.Keys)
            {
                if (key.Item1 == klass)
                    toRemove.Add(key);
            }
            
            foreach (var key in toRemove)
            {
                _lookupCache.Remove(key);
            }
            
            _logger.LogDebug("Cache invalidated for class {Class}", klass.Name);
        }

        /// <summary>
        /// Registers a global variable.
        /// </summary>
        public void RegisterGlobal(string name, object value)
        {
            var symbol = SSymbol.Intern(name);
            Globals[symbol] = value;
            _logger.LogDebug("Registered global: {Name}", name);
        }

        /// <summary>
        /// Gets a global variable value.
        /// </summary>
        public object GetGlobal(string name)
        {
            var symbol = SSymbol.Intern(name);
            return Globals.TryGetValue(symbol, out var value) ? value : null;
        }

        /// <summary>
        /// Gets a global variable value by symbol.
        /// </summary>
        public object GetGlobal(SSymbol symbol)
        {
            return Globals.TryGetValue(symbol, out var value) ? value : null;
        }

        /// <summary>
        /// Checks if a global exists.
        /// </summary>
        public bool HasGlobal(string name)
        {
            var symbol = SSymbol.Intern(name);
            return Globals.ContainsKey(symbol);
        }

        /// <summary>
        /// Creates the minimal kernel classes needed for cold start.
        /// This solves the circular dependency problem by creating everything directly.
        /// </summary>
        public void CreateKernel()
        {
            _logger.LogInformation("Creating kernel classes...");
            
            // Step 1: Create the core classes with temporary self-referential classes
            ObjectClass = new SClass("Object", 0);
            BehaviorClass = new SClass("Behavior", 5); // 5 instance variables
            ClassDescriptionClass = new SClass("ClassDescription", 3); // +3 = 8 total
            MetaclassClass = new SClass("Metaclass", 1); // +1 = 9 total  
            ClassClass = new SClass("Class", 0); // No additional = 8 total
            
            // Step 2: Wire the inheritance chain
            BehaviorClass.Superclass = ObjectClass;
            ClassDescriptionClass.Superclass = BehaviorClass;
            MetaclassClass.Superclass = ClassDescriptionClass;
            ClassClass.Superclass = ClassDescriptionClass;
            
            // Step 3: Create metaclasses (all are instances of Metaclass)
            var objectMetaclass = new SClass("Object class", 0);
            objectMetaclass.Klass = MetaclassClass;
            objectMetaclass.ThisClass = ObjectClass;
            ObjectClass.Klass = objectMetaclass;
            ObjectClass.Metaclass = objectMetaclass;
            
            var behaviorMetaclass = new SClass("Behavior class", 0);
            behaviorMetaclass.Klass = MetaclassClass;
            behaviorMetaclass.ThisClass = BehaviorClass;
            BehaviorClass.Klass = behaviorMetaclass;
            BehaviorClass.Metaclass = behaviorMetaclass;
            
            var classDescMetaclass = new SClass("ClassDescription class", 0);
            classDescMetaclass.Klass = MetaclassClass;
            classDescMetaclass.ThisClass = ClassDescriptionClass;
            ClassDescriptionClass.Klass = classDescMetaclass;
            ClassDescriptionClass.Metaclass = classDescMetaclass;
            
            var metaclassMetaclass = new SClass("Metaclass class", 0);
            metaclassMetaclass.Klass = MetaclassClass;
            metaclassMetaclass.ThisClass = MetaclassClass;
            MetaclassClass.Klass = metaclassMetaclass;
            MetaclassClass.Metaclass = metaclassMetaclass;
            
            var classMetaclass = new SClass("Class class", 0);
            classMetaclass.Klass = MetaclassClass;
            classMetaclass.ThisClass = ClassClass;
            ClassClass.Klass = classMetaclass;
            ClassClass.Metaclass = classMetaclass;
            
            // Step 4: Wire metaclass hierarchy
            // Object class -> Class
            objectMetaclass.Superclass = ClassClass;
            // Behavior class -> Object class
            behaviorMetaclass.Superclass = objectMetaclass;
            // ClassDescription class -> Behavior class
            classDescMetaclass.Superclass = behaviorMetaclass;
            // Metaclass class -> ClassDescription class
            metaclassMetaclass.Superclass = classDescMetaclass;
            // Class class -> ClassDescription class
            classMetaclass.Superclass = classDescMetaclass;
            
            // Step 5: Fix special singletons
            Nil.Instance.Klass = CreateClass("UndefinedObject", ObjectClass);
            True.Instance.Klass = CreateClass("True", CreateClass("Boolean", ObjectClass));
            False.Instance.Klass = CreateClass("False", True.Instance.Klass.Superclass);
            
            UndefinedObjectClass = Nil.Instance.Klass;
            TrueClass = True.Instance.Klass;
            FalseClass = False.Instance.Klass;
            
            // Step 6: Create essential classes
            CompiledMethodClass = CreateClass("CompiledMethod", ObjectClass);
            CompiledMethodClass.Format = ClassFormat.CompiledMethod;
            
            MethodDictionaryClass = CreateClass("MethodDictionary", CreateClass("Dictionary", ObjectClass));
            
            // Collection hierarchy
            var collectionClass = CreateClass("Collection", ObjectClass);
            var seqCollClass = CreateClass("SequenceableCollection", collectionClass);
            var arrayedCollClass = CreateClass("ArrayedCollection", seqCollClass);
            
            ArrayClass = CreateClass("Array", arrayedCollClass);
            ArrayClass.Format = ClassFormat.VariableWordSubclass;
            
            ByteArrayClass = CreateClass("ByteArray", arrayedCollClass);
            ByteArrayClass.Format = ClassFormat.VariableByteSubclass;
            
            StringClass = CreateClass("String", arrayedCollClass);
            StringClass.Format = ClassFormat.VariableByteSubclass;
            
            SymbolClass = CreateClass("Symbol", StringClass);
            SymbolClass.Format = ClassFormat.VariableByteSubclass;
            
            // Numeric hierarchy
            var magnitudeClass = CreateClass("Magnitude", ObjectClass);
            var numberClass = CreateClass("Number", magnitudeClass);
            var integerClass = CreateClass("Integer", numberClass);
            SmallIntegerClass = CreateClass("SmallInteger", integerClass);
            
            // Dictionary hierarchy
            var setClass = CreateClass("Set", collectionClass);
            DictionaryClass = CreateClass("Dictionary", setClass);
            SystemDictionaryClass = CreateClass("SystemDictionary", DictionaryClass);
            
            // Context classes
            var instStreamClass = CreateClass("InstructionStream", ObjectClass);
            var contextPartClass = CreateClass("ContextPart", instStreamClass);
            MethodContextClass = CreateClass("MethodContext", contextPartClass);
            BlockContextClass = CreateClass("BlockContext", contextPartClass);
            
            // Register all classes as globals
            RegisterCoreClasses();
            
            _logger.LogInformation("Kernel creation complete");
        }

        /// <summary>
        /// Creates a new class with proper metaclass setup.
        /// </summary>
        private SClass CreateClass(string name, SClass superclass)
        {
            // Calculate instance size based on superclass
            int instSize = superclass?.ComputeInstSize() ?? 0;
            
            // Create the class
            var newClass = new SClass(name, instSize);
            newClass.Superclass = superclass;
            
            // Create its metaclass
            var metaclass = new SClass($"{name} class", 0);
            metaclass.Klass = MetaclassClass;
            metaclass.ThisClass = newClass;
            
            // Set metaclass superclass
            if (superclass != null)
            {
                metaclass.Superclass = superclass.Metaclass;
            }
            else
            {
                metaclass.Superclass = ClassClass;
            }
            
            // Wire them together
            newClass.Klass = metaclass;
            newClass.Metaclass = metaclass;
            
            return newClass;
        }

        /// <summary>
        /// Registers all core classes in the global namespace.
        /// </summary>
        private void RegisterCoreClasses()
        {
            RegisterGlobal("Object", ObjectClass);
            RegisterGlobal("Behavior", BehaviorClass);
            RegisterGlobal("ClassDescription", ClassDescriptionClass);
            RegisterGlobal("Metaclass", MetaclassClass);
            RegisterGlobal("Class", ClassClass);
            RegisterGlobal("CompiledMethod", CompiledMethodClass);
            RegisterGlobal("MethodDictionary", MethodDictionaryClass);
            RegisterGlobal("Array", ArrayClass);
            RegisterGlobal("ByteArray", ByteArrayClass);
            RegisterGlobal("String", StringClass);
            RegisterGlobal("Symbol", SymbolClass);
            RegisterGlobal("SmallInteger", SmallIntegerClass);
            RegisterGlobal("UndefinedObject", UndefinedObjectClass);
            RegisterGlobal("True", TrueClass);
            RegisterGlobal("False", FalseClass);
            RegisterGlobal("Dictionary", DictionaryClass);
            RegisterGlobal("SystemDictionary", SystemDictionaryClass);
            RegisterGlobal("MethodContext", MethodContextClass);
            RegisterGlobal("BlockContext", BlockContextClass);
            
            // Register singleton instances
            RegisterGlobal("nil", Nil.Instance);
            RegisterGlobal("true", True.Instance);
            RegisterGlobal("false", False.Instance);
        }

        /// <summary>
        /// Creates the special objects array used by the VM.
        /// </summary>
        public void CreateSpecialObjectsArray()
        {
            SpecialObjectsArray = new SArray(ArrayClass, 16);
            
            // Standard positions per Blue Book
            SpecialObjectsArray.AtPut(1, Nil.Instance);
            SpecialObjectsArray.AtPut(2, False.Instance);
            SpecialObjectsArray.AtPut(3, True.Instance);
            SpecialObjectsArray.AtPut(4, null); // ProcessorScheduler placeholder
            SpecialObjectsArray.AtPut(5, MetaclassClass);
            SpecialObjectsArray.AtPut(6, Globals[SSymbol.Intern("Smalltalk")]); // System dictionary
            SpecialObjectsArray.AtPut(7, null); // Symbol table placeholder
            SpecialObjectsArray.AtPut(8, null); // Process list placeholder
            SpecialObjectsArray.AtPut(9, BehaviorClass);
            
            _logger.LogDebug("Created special objects array");
        }
    }
}