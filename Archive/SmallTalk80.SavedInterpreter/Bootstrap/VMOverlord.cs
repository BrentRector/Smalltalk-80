using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Abstractions.Memory;
using Core.Memory;
using Microsoft.Extensions.Logging;

namespace Smalltalk80.Interpreter.Bootstrap
{
    /// <summary>
    /// VM Overlord - A high-level bootstrap orchestrator that solves the circular dependency problem
    /// during cold start by providing a two-phase approach: collection and materialization.
    /// 
    /// The Overlord has internal knowledge of VM data structures and provides a clean API
    /// for cold start to use without worrying about circular dependencies.
    /// </summary>
    public sealed class VMOverlord
    {
        private readonly IObjectMemory _memory;
        private readonly ILogger<VMOverlord> _logger;
        
        // Phase 1: Collection structures - these are just C# data structures, not Smalltalk objects yet
        private readonly Dictionary<string, ClassSpecification> _classSpecifications = new();
        private readonly Dictionary<string, SymbolSpecification> _symbolSpecifications = new();
        private readonly Dictionary<string, MethodSpecification> _methodSpecifications = new();
        private readonly List<GlobalSpecification> _globalSpecifications = new();
        private readonly List<SingletonSpecification> _singletonSpecifications = new();
        
        // Phase 2: Materialization maps - track what we've created
        private readonly Dictionary<string, OOP> _materializedClasses = new();
        private readonly Dictionary<string, OOP> _materializedSymbols = new();
        private readonly Dictionary<string, OOP> _materializedGlobals = new();
        
        // Special bootstrap objects that we'll need
        private OOP _nilObject = OOP.Nil;
        private OOP _trueObject = OOP.Nil;
        private OOP _falseObject = OOP.Nil;
        private OOP _smalltalkDict = OOP.Nil;
        private OOP _symbolTable = OOP.Nil;
        
        // Bootstrap state
        private bool _collectionPhaseComplete = false;
        private bool _materializationPhaseComplete = false;
        
        public VMOverlord(IObjectMemory memory, ILogger<VMOverlord> logger)
        {
            _memory = memory ?? throw new ArgumentNullException(nameof(memory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        #region Phase 1: Collection API - Used by ColdStartBootstrap
        
        /// <summary>
        /// Define a class specification. This doesn't create any objects yet.
        /// </summary>
        public void DefineClass(
            string name,
            string? superclassName,
            string[] instanceVariables,
            string[] classVariables = null,
            string[] poolDictionaries = null,
            ClassFormat format = ClassFormat.FixedSize,
            string category = "")
        {
            if (_collectionPhaseComplete)
                throw new InvalidOperationException("Cannot define classes after collection phase is complete");
            
            var spec = new ClassSpecification
            {
                Name = name,
                SuperclassName = superclassName,
                InstanceVariables = instanceVariables ?? Array.Empty<string>(),
                ClassVariables = classVariables ?? Array.Empty<string>(),
                PoolDictionaries = poolDictionaries ?? Array.Empty<string>(),
                Format = format,
                Category = category
            };
            
            _classSpecifications[name] = spec;
            _logger.LogDebug("Defined class specification: {Name}", name);
        }
        
        /// <summary>
        /// Define a method for a class. This doesn't compile or install it yet.
        /// </summary>
        public void DefineMethod(
            string className,
            string selector,
            byte[] bytecode,
            string[] literalStrings,
            int numArgs,
            int numTemps,
            int stackSize,
            int primitiveIndex = 0,
            bool isClassMethod = false)
        {
            if (_collectionPhaseComplete)
                throw new InvalidOperationException("Cannot define methods after collection phase is complete");
            
            var spec = new MethodSpecification
            {
                ClassName = className,
                Selector = selector,
                Bytecode = bytecode,
                LiteralStrings = literalStrings,
                NumArgs = numArgs,
                NumTemps = numTemps,
                StackSize = stackSize,
                PrimitiveIndex = primitiveIndex,
                IsClassMethod = isClassMethod
            };
            
            var key = $"{className}#{selector}#{(isClassMethod ? "class" : "instance")}";
            _methodSpecifications[key] = spec;
            _logger.LogDebug("Defined method specification: {Key}", key);
        }
        
        /// <summary>
        /// Define a symbol that will be needed. Pre-declaring symbols avoids circular dependencies.
        /// </summary>
        public void DefineSymbol(string symbol)
        {
            if (_collectionPhaseComplete)
                throw new InvalidOperationException("Cannot define symbols after collection phase is complete");
            
            if (!_symbolSpecifications.ContainsKey(symbol))
            {
                _symbolSpecifications[symbol] = new SymbolSpecification { Value = symbol };
                _logger.LogDebug("Defined symbol specification: {Symbol}", symbol);
            }
        }
        
        /// <summary>
        /// Define a global variable.
        /// </summary>
        public void DefineGlobal(string name, string? initialValueClassName = null)
        {
            if (_collectionPhaseComplete)
                throw new InvalidOperationException("Cannot define globals after collection phase is complete");
            
            _globalSpecifications.Add(new GlobalSpecification
            {
                Name = name,
                InitialValueClassName = initialValueClassName
            });
            _logger.LogDebug("Defined global specification: {Name}", name);
        }
        
        /// <summary>
        /// Define a singleton object (like nil, true, false).
        /// </summary>
        public void DefineSingleton(string name, string className)
        {
            if (_collectionPhaseComplete)
                throw new InvalidOperationException("Cannot define singletons after collection phase is complete");
            
            _singletonSpecifications.Add(new SingletonSpecification
            {
                Name = name,
                ClassName = className
            });
            _logger.LogDebug("Defined singleton specification: {Name} of class {Class}", name, className);
        }
        
        /// <summary>
        /// Complete the collection phase and prepare for materialization.
        /// </summary>
        public void CompleteCollectionPhase()
        {
            if (_collectionPhaseComplete)
                throw new InvalidOperationException("Collection phase already complete");
            
            _collectionPhaseComplete = true;
            _logger.LogInformation("Collection phase complete. Collected {Classes} classes, {Methods} methods, {Symbols} symbols",
                _classSpecifications.Count, _methodSpecifications.Count, _symbolSpecifications.Count);
            
            // Analyze and validate dependencies
            ValidateDependencies();
        }
        
        #endregion
        
        #region Phase 2: Materialization API
        
        /// <summary>
        /// Materialize all collected specifications into actual Smalltalk objects.
        /// This is where the magic happens - we create everything in the right order.
        /// </summary>
        public void Materialize()
        {
            if (!_collectionPhaseComplete)
                throw new InvalidOperationException("Must complete collection phase before materialization");
            
            if (_materializationPhaseComplete)
                throw new InvalidOperationException("Materialization already complete");
            
            _logger.LogInformation("Starting materialization phase...");
            
            // Step 1: Create placeholder objects for singletons with temporary classes
            MaterializeSingletonPlaceholders();
            
            // Step 2: Create all class objects with temporary metaclasses
            MaterializeClassPlaceholders();
            
            // Step 3: Create all metaclass objects
            MaterializeMetaclasses();
            
            // Step 4: Wire up the class hierarchy (superclasses)
            WireClassHierarchy();
            
            // Step 5: Set up the metaclass hierarchy
            WireMetaclassHierarchy();
            
            // Step 6: Fix singleton classes
            FixSingletonClasses();
            
            // Step 7: Create all symbols
            MaterializeSymbols();
            
            // Step 8: Create method dictionaries
            MaterializeMethodDictionaries();
            
            // Step 9: Install all methods
            MaterializeMethods();
            
            // Step 10: Create global dictionaries
            MaterializeGlobalDictionaries();
            
            // Step 11: Create Special Objects Array
            MaterializeSpecialObjectsArray();
            
            _materializationPhaseComplete = true;
            _logger.LogInformation("Materialization phase complete!");
        }
        
        /// <summary>
        /// Get a materialized class by name.
        /// </summary>
        public OOP GetClass(string name)
        {
            if (!_materializationPhaseComplete)
                throw new InvalidOperationException("Materialization not complete");
            
            return _materializedClasses.TryGetValue(name, out var classOop) 
                ? classOop 
                : throw new KeyNotFoundException($"Class {name} not found");
        }
        
        /// <summary>
        /// Get a materialized symbol by value.
        /// </summary>
        public OOP GetSymbol(string value)
        {
            if (!_materializationPhaseComplete)
                throw new InvalidOperationException("Materialization not complete");
            
            return _materializedSymbols.TryGetValue(value, out var symbolOop)
                ? symbolOop
                : throw new KeyNotFoundException($"Symbol {value} not found");
        }
        
        /// <summary>
        /// Get the nil object.
        /// </summary>
        public OOP Nil => _nilObject;
        
        /// <summary>
        /// Get the true object.
        /// </summary>
        public OOP True => _trueObject;
        
        /// <summary>
        /// Get the false object.
        /// </summary>
        public OOP False => _falseObject;
        
        /// <summary>
        /// Get the Smalltalk global dictionary.
        /// </summary>
        public OOP SmalltalkDictionary => _smalltalkDict;
        
        #endregion
        
        #region Private Materialization Steps
        
        private void MaterializeSingletonPlaceholders()
        {
            _logger.LogDebug("Creating singleton placeholders...");
            
            // Use a temporary class marker for now
            var tempClass = OOP.FromSmallInteger(999999);
            
            // Create nil, true, false as the first objects (indices 0, 1, 2)
            _nilObject = _memory.AllocateObject(tempClass, 0);
            _trueObject = _memory.AllocateObject(tempClass, 0);
            _falseObject = _memory.AllocateObject(tempClass, 0);
            
            // Update static OOP values
            OOP.Nil = _nilObject;
            OOP.True = _trueObject;
            OOP.False = _falseObject;
            
            // Tell memory system about nil
            _memory.SetNilOop(_nilObject);
            
            _logger.LogDebug("Created singletons: nil={0}, true={1}, false={2}", 
                _nilObject, _trueObject, _falseObject);
        }
        
        private void MaterializeClassPlaceholders()
        {
            _logger.LogDebug("Creating class placeholders...");
            
            // Create all class objects with temporary metaclass
            var tempMetaclass = OOP.FromSmallInteger(999999);
            
            foreach (var spec in _classSpecifications.Values)
            {
                // Behavior has 5 fields: superclass, methodDict, format, instanceVariables, organization
                // ClassDescription adds: name, classPool, sharedPools
                // Class/Metaclass don't add more instance variables
                var classObj = _memory.AllocateObject(tempMetaclass, 8); // Standard class size
                _materializedClasses[spec.Name] = classObj;
                _logger.LogDebug("Created class placeholder: {Name} -> {OOP}", spec.Name, classObj);
            }
        }
        
        private void MaterializeMetaclasses()
        {
            _logger.LogDebug("Creating metaclasses...");
            
            // Get the Metaclass class
            var metaclassClass = _materializedClasses["Metaclass"];
            
            foreach (var className in _materializedClasses.Keys.ToList())
            {
                var classObj = _materializedClasses[className];
                
                // Create metaclass for this class
                // Metaclass has 6 fields per Blue Book
                var metaclass = _memory.AllocateObject(metaclassClass, 6);
                
                // Set the class's class to its metaclass
                _memory.SetClassOf(classObj, metaclass);
                
                // Store thisClass field (field 5 in Metaclass)
                _memory.StorePointer(metaclass, 5, classObj);
                
                _logger.LogDebug("Created metaclass for {Name}", className);
            }
        }
        
        private void WireClassHierarchy()
        {
            _logger.LogDebug("Wiring class hierarchy...");
            
            foreach (var spec in _classSpecifications.Values)
            {
                var classObj = _materializedClasses[spec.Name];
                
                if (spec.SuperclassName != null)
                {
                    var superclass = _materializedClasses[spec.SuperclassName];
                    _memory.StorePointer(classObj, 0, superclass); // Field 0 is superclass in Behavior
                }
                else
                {
                    // Object has no superclass
                    _memory.StorePointer(classObj, 0, _nilObject);
                }
                
                _logger.LogDebug("Set superclass: {Name} -> {Superclass}", 
                    spec.Name, spec.SuperclassName ?? "nil");
            }
        }
        
        private void WireMetaclassHierarchy()
        {
            _logger.LogDebug("Wiring metaclass hierarchy...");
            
            var classClass = _materializedClasses["Class"];
            
            foreach (var spec in _classSpecifications.Values)
            {
                var classObj = _materializedClasses[spec.Name];
                var metaclass = _memory.FetchClassOf(classObj);
                
                if (spec.Name == "Object")
                {
                    // Special case: Object class superclass is Class
                    _memory.StorePointer(metaclass, 0, classClass);
                }
                else if (spec.SuperclassName != null)
                {
                    // Metaclass hierarchy parallels class hierarchy
                    var superclassObj = _materializedClasses[spec.SuperclassName];
                    var superMetaclass = _memory.FetchClassOf(superclassObj);
                    _memory.StorePointer(metaclass, 0, superMetaclass);
                }
                
                _logger.LogDebug("Set metaclass superclass for {Name}", spec.Name);
            }
        }
        
        private void FixSingletonClasses()
        {
            _logger.LogDebug("Fixing singleton classes...");
            
            // Fix nil, true, false classes
            _memory.SetClassOf(_nilObject, _materializedClasses["UndefinedObject"]);
            _memory.SetClassOf(_trueObject, _materializedClasses["True"]);
            _memory.SetClassOf(_falseObject, _materializedClasses["False"]);
            
            _logger.LogDebug("Fixed singleton classes");
        }
        
        private void MaterializeSymbols()
        {
            _logger.LogDebug("Creating symbols...");
            
            var symbolClass = _materializedClasses["Symbol"];
            
            foreach (var spec in _symbolSpecifications.Values)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(spec.Value);
                var symbol = _memory.AllocateByteObject(symbolClass, bytes.Length);
                
                for (int i = 0; i < bytes.Length; i++)
                {
                    _memory.StoreByte(symbol, i, bytes[i]);
                }
                
                _materializedSymbols[spec.Value] = symbol;
                _logger.LogDebug("Created symbol: #{Value} -> {OOP}", spec.Value, symbol);
            }
        }
        
        private void MaterializeMethodDictionaries()
        {
            _logger.LogDebug("Creating method dictionaries...");
            
            var methodDictClass = _materializedClasses["MethodDictionary"];
            var arrayClass = _materializedClasses["Array"];
            
            // Create method dictionary for each class and its metaclass
            foreach (var className in _materializedClasses.Keys)
            {
                var classObj = _materializedClasses[className];
                
                // Create instance method dictionary
                var instanceDict = CreateEmptyMethodDictionary(methodDictClass, arrayClass);
                _memory.StorePointer(classObj, 1, instanceDict); // Field 1 is methodDict in Behavior
                
                // Create class method dictionary for metaclass
                var metaclass = _memory.FetchClassOf(classObj);
                var classDict = CreateEmptyMethodDictionary(methodDictClass, arrayClass);
                _memory.StorePointer(metaclass, 1, classDict);
                
                _logger.LogDebug("Created method dictionaries for {Name}", className);
            }
        }
        
        private OOP CreateEmptyMethodDictionary(OOP methodDictClass, OOP arrayClass)
        {
            var dict = _memory.AllocateObject(methodDictClass, 2);
            _memory.StorePointer(dict, 0, OOP.FromSmallInteger(0)); // tally
            
            // Create array for methods (start with capacity for 64 entries)
            var array = _memory.AllocateObject(arrayClass, 128); // 64 key-value pairs
            for (int i = 0; i < 128; i++)
            {
                _memory.StorePointer(array, i, _nilObject);
            }
            _memory.StorePointer(dict, 1, array);
            
            return dict;
        }
        
        private void MaterializeMethods()
        {
            _logger.LogDebug("Installing methods...");
            
            var compiledMethodClass = _materializedClasses["CompiledMethod"];
            var arrayClass = _materializedClasses["Array"];
            var byteArrayClass = _materializedClasses["ByteArray"];
            
            foreach (var spec in _methodSpecifications.Values)
            {
                // Get the class or metaclass
                var classObj = _materializedClasses[spec.ClassName];
                OOP targetClass = spec.IsClassMethod 
                    ? _memory.FetchClassOf(classObj)  // Install in metaclass
                    : classObj;                        // Install in class
                
                // Create the compiled method
                var method = CreateCompiledMethod(
                    compiledMethodClass, 
                    arrayClass, 
                    byteArrayClass,
                    spec);
                
                // Install in method dictionary
                var methodDict = _memory.FetchPointer(targetClass, 1);
                var selector = _materializedSymbols[spec.Selector];
                AddToMethodDictionary(methodDict, selector, method);
                
                _logger.LogDebug("Installed method {Selector} in {Class}", 
                    spec.Selector, spec.ClassName);
            }
        }
        
        private OOP CreateCompiledMethod(
            OOP compiledMethodClass,
            OOP arrayClass,
            OOP byteArrayClass,
            MethodSpecification spec)
        {
            var method = _memory.AllocateObject(compiledMethodClass, 4);
            
            // Build header
            var header = (spec.NumArgs & 0x1F) |
                        ((spec.NumTemps & 0x3F) << 5) |
                        ((spec.LiteralStrings.Length & 0x3F) << 11) |
                        ((spec.StackSize & 0x1F) << 17);
            
            if (spec.PrimitiveIndex > 0)
            {
                header |= 0x10000; // Set primitive flag
            }
            
            _memory.StorePointer(method, 0, OOP.FromSmallInteger(header));
            _memory.StorePointer(method, 1, OOP.FromSmallInteger(spec.PrimitiveIndex));
            
            // Create literals array
            var literals = _memory.AllocateObject(arrayClass, spec.LiteralStrings.Length);
            for (int i = 0; i < spec.LiteralStrings.Length; i++)
            {
                var literal = _materializedSymbols[spec.LiteralStrings[i]];
                _memory.StorePointer(literals, i, literal);
            }
            _memory.StorePointer(method, 2, literals);
            
            // Create bytecode array
            var bytecodeArray = _memory.AllocateByteObject(byteArrayClass, spec.Bytecode.Length);
            for (int i = 0; i < spec.Bytecode.Length; i++)
            {
                _memory.StoreByte(bytecodeArray, i, spec.Bytecode[i]);
            }
            _memory.StorePointer(method, 3, bytecodeArray);
            
            return method;
        }
        
        private void AddToMethodDictionary(OOP dictOop, OOP selectorOop, OOP methodOop)
        {
            var tallyOop = _memory.FetchPointer(dictOop, 0);
            var arrayOop = _memory.FetchPointer(dictOop, 1);
            var tally = tallyOop.IsSmallInteger ? tallyOop.SmallIntegerValue : 0;
            
            // Simple linear search for empty slot
            for (int i = 0; i < 64; i++)
            {
                var key = _memory.FetchPointer(arrayOop, i * 2);
                if (key.IsNil)
                {
                    _memory.StorePointer(arrayOop, i * 2, selectorOop);
                    _memory.StorePointer(arrayOop, i * 2 + 1, methodOop);
                    _memory.StorePointer(dictOop, 0, OOP.FromSmallInteger(tally + 1));
                    return;
                }
                else if (key.Value == selectorOop.Value)
                {
                    // Replace existing
                    _memory.StorePointer(arrayOop, i * 2 + 1, methodOop);
                    return;
                }
            }
        }
        
        private void MaterializeGlobalDictionaries()
        {
            _logger.LogDebug("Creating global dictionaries...");
            
            var dictClass = _materializedClasses["Dictionary"];
            var arrayClass = _materializedClasses["Array"];
            
            // Create Smalltalk global dictionary
            _smalltalkDict = CreateDictionary(dictClass, arrayClass);
            
            // Add all classes to Smalltalk dictionary
            foreach (var kvp in _materializedClasses)
            {
                var nameSymbol = _materializedSymbols[kvp.Key];
                AddToDictionary(_smalltalkDict, nameSymbol, kvp.Value);
            }
            
            // Add Smalltalk itself
            var smalltalkSymbol = _materializedSymbols["Smalltalk"];
            AddToDictionary(_smalltalkDict, smalltalkSymbol, _smalltalkDict);
            
            // Create symbol table
            _symbolTable = CreateDictionary(dictClass, arrayClass);
            foreach (var kvp in _materializedSymbols)
            {
                AddToDictionary(_symbolTable, kvp.Value, kvp.Value);
            }
            
            _logger.LogDebug("Created global dictionaries");
        }
        
        private OOP CreateDictionary(OOP dictClass, OOP arrayClass)
        {
            var dict = _memory.AllocateObject(dictClass, 2);
            _memory.StorePointer(dict, 0, OOP.FromSmallInteger(0)); // tally
            
            var array = _memory.AllocateObject(arrayClass, 512); // Large capacity
            for (int i = 0; i < 512; i++)
            {
                _memory.StorePointer(array, i, _nilObject);
            }
            _memory.StorePointer(dict, 1, array);
            
            return dict;
        }
        
        private void AddToDictionary(OOP dictOop, OOP key, OOP value)
        {
            var tallyOop = _memory.FetchPointer(dictOop, 0);
            var arrayOop = _memory.FetchPointer(dictOop, 1);
            var tally = tallyOop.IsSmallInteger ? tallyOop.SmallIntegerValue : 0;
            
            // Find slot for key
            for (int i = 0; i < 256; i++)
            {
                var existingKey = _memory.FetchPointer(arrayOop, i * 2);
                if (existingKey.IsNil)
                {
                    _memory.StorePointer(arrayOop, i * 2, key);
                    _memory.StorePointer(arrayOop, i * 2 + 1, value);
                    _memory.StorePointer(dictOop, 0, OOP.FromSmallInteger(tally + 1));
                    return;
                }
                else if (existingKey.Value == key.Value)
                {
                    _memory.StorePointer(arrayOop, i * 2 + 1, value);
                    return;
                }
            }
        }
        
        private void MaterializeSpecialObjectsArray()
        {
            _logger.LogDebug("Creating Special Objects Array...");
            
            var arrayClass = _materializedClasses["Array"];
            var specialObjects = _memory.AllocateObject(arrayClass, 9);
            
            // Standard special objects positions
            _memory.StorePointer(specialObjects, 0, _nilObject);
            _memory.StorePointer(specialObjects, 1, _falseObject);
            _memory.StorePointer(specialObjects, 2, _trueObject);
            _memory.StorePointer(specialObjects, 3, _nilObject); // ProcessorScheduler placeholder
            _memory.StorePointer(specialObjects, 4, _materializedClasses["Metaclass"]);
            _memory.StorePointer(specialObjects, 5, _smalltalkDict);
            _memory.StorePointer(specialObjects, 6, _symbolTable);
            _memory.StorePointer(specialObjects, 7, _nilObject); // Process list placeholder
            _memory.StorePointer(specialObjects, 8, _materializedClasses["Behavior"]);
            
            // Tell memory about special objects
            _memory.SetSpecialObjectsArray(specialObjects);
            
            _logger.LogDebug("Special Objects Array created and registered");
        }
        
        private void ValidateDependencies()
        {
            _logger.LogDebug("Validating dependencies...");
            
            // Check that all superclasses exist
            foreach (var spec in _classSpecifications.Values)
            {
                if (spec.SuperclassName != null && !_classSpecifications.ContainsKey(spec.SuperclassName))
                {
                    throw new InvalidOperationException($"Class {spec.Name} has undefined superclass {spec.SuperclassName}");
                }
            }
            
            // Check that all method classes exist
            foreach (var spec in _methodSpecifications.Values)
            {
                if (!_classSpecifications.ContainsKey(spec.ClassName))
                {
                    throw new InvalidOperationException($"Method {spec.Selector} references undefined class {spec.ClassName}");
                }
            }
            
            _logger.LogDebug("Dependencies validated successfully");
        }
        
        #endregion
        
        #region Internal Data Structures
        
        private class ClassSpecification
        {
            public string Name { get; set; }
            public string? SuperclassName { get; set; }
            public string[] InstanceVariables { get; set; }
            public string[] ClassVariables { get; set; }
            public string[] PoolDictionaries { get; set; }
            public ClassFormat Format { get; set; }
            public string Category { get; set; }
        }
        
        private class MethodSpecification
        {
            public string ClassName { get; set; }
            public string Selector { get; set; }
            public byte[] Bytecode { get; set; }
            public string[] LiteralStrings { get; set; }
            public int NumArgs { get; set; }
            public int NumTemps { get; set; }
            public int StackSize { get; set; }
            public int PrimitiveIndex { get; set; }
            public bool IsClassMethod { get; set; }
        }
        
        private class SymbolSpecification
        {
            public string Value { get; set; }
        }
        
        private class GlobalSpecification
        {
            public string Name { get; set; }
            public string? InitialValueClassName { get; set; }
        }
        
        private class SingletonSpecification
        {
            public string Name { get; set; }
            public string ClassName { get; set; }
        }
        
        public enum ClassFormat
        {
            FixedSize = 0,
            VariableByteSubclass = 1,
            VariableWordSubclass = 2,
            VariableSubclass = 3,
            CompiledMethod = 4
        }
        
        #endregion
    }
}