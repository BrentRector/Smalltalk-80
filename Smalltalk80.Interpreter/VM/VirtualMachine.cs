using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Smalltalk80.Interpreter.Bootstrap;
using Smalltalk80.Interpreter.Config;
using Smalltalk80.Interpreter.Execution;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.Primitives;

namespace Smalltalk80.Interpreter.VM
{
    /// <summary>
    /// The Smalltalk-80 Virtual Machine.
    /// Coordinates all components and provides the main execution engine.
    /// </summary>
    public sealed partial class VirtualMachine : IExecutionContext, IPrimitiveContext, Bootstrap.ISymbolTable, IFieldAccessor
    {
        private readonly ILogger<VirtualMachine> logger;
        private readonly ObjectMemory memory;
        private readonly PrimitiveTable primitiveTable;
        private readonly GarbageCollector gc;
        private readonly ClassBuilder classBuilder;
        private readonly FieldLookup fieldLookup;
        private readonly BytecodeDispatcher bytecodeDispatcher;
        private readonly MessageDispatcher messageDispatcher;
        private readonly ProcessScheduler processScheduler;
        private readonly OOP[] specialObjectsArray;
        private readonly Dictionary<string, OOP> symbolTable;
        private readonly Stack<MethodContext> executionStack;
        private readonly MethodContextPool contextPool;
        
        private MethodContext? activeContext;
        private bool bootstrapComplete = false;
        private int recursionDepth = 0;
        private const int MaxRecursionDepth = 10000; // Increased to allow deep call stacks during initialization
        private bool checkRecursionDepth = true; // Can be disabled during initialization if needed
        private bool schedulerEnabled = false;
        private OOP doItProcess = OOP.Nil;
        private BootstrapManifest? lastLoadedManifest = null;
        private BootstrapManifest? savedManifest = null; // Store manifest for post-bootstrap initialization
        private InitializationConfig initializationConfig;
        private bool executingInitialization = false; // Track if we're in initialization phase

        /// <summary>
        /// Creates a new virtual machine instance.
        /// </summary>
        /// <param name="logger">The logger instance for logging VM operations.</param>
        /// <param name="initialMemorySize">Initial object memory size. Default is 262144 for complete system.</param>
        public VirtualMachine(ILogger<VirtualMachine>? logger = null, int initialMemorySize = 262144)
        {
            this.logger = logger ?? NullLogger<VirtualMachine>.Instance;
            memory = new ObjectMemory(initialMemorySize);
            primitiveTable = new PrimitiveTable();
            gc = new GarbageCollector(memory);
            
            // Phase -1: Create FieldLookup without dependencies
            // It will be populated with field mappings before any objects are created
            fieldLookup = new FieldLookup();
            
            // ClassBuilder now receives the FieldLookup for field index queries
            classBuilder = new ClassBuilder(memory, fieldLookup);
            
            bytecodeDispatcher = new BytecodeDispatcher(this);
            messageDispatcher = new MessageDispatcher(memory, primitiveTable, this, this);
            processScheduler = new ProcessScheduler(this, memory);
            specialObjectsArray = new OOP[SpecialObjects.Count];
            symbolTable = new Dictionary<string, OOP>();
            executionStack = new Stack<MethodContext>();
            contextPool = MethodContextPool.Instance;
            
            // Initialize special objects array with nil
            for (int i = 0; i < specialObjectsArray.Length; i++)
            {
                specialObjectsArray[i] = OOP.Nil;
            }
            
            // Create default initialization configuration
            initializationConfig = new InitializationConfig
            {
                SkipUIInitializations = false,  // Can be changed via property
                ContinueOnError = true          // Continue on errors by default
            };
            
            RegisterPrimitives();
        }

        /// <summary>
        /// Gets the object memory.
        /// </summary>
        public ObjectMemory Memory => memory;

        /// <summary>
        /// Gets whether bootstrap has completed.
        /// </summary>
        public bool IsBootstrapped => bootstrapComplete;
        
        /// <summary>
        /// Gets the process scheduler.
        /// </summary>
        public ProcessScheduler ProcessScheduler => processScheduler;
        
        /// <summary>
        /// Gets a class by name from the loaded system.
        /// </summary>
        /// <param name="className">The name of the class to retrieve.</param>
        /// <returns>The OOP of the class, or OOP.Nil if not found.</returns>
        public OOP GetClassByName(string className) => classBuilder.GetClass(className);
        
        /// <summary>
        /// Gets or sets the initialization configuration.
        /// Configure this before calling Bootstrap to control initialization behavior.
        /// </summary>
        public InitializationConfig InitializationConfig 
        { 
            get => initializationConfig;
            set => initializationConfig = value ?? new InitializationConfig();
        }
        
        /// <summary>
        /// Gets or sets whether we're currently executing initialization expressions.
        /// When true, Message Not Understood errors will throw exceptions instead of returning nil.
        /// </summary>
        public bool ExecutingInitialization
        {
            get => executingInitialization;
            set => executingInitialization = value;
        }
        
        /// <summary>
        /// Configures the VM to skip UI-related initializations.
        /// Call this before Bootstrap() if running in a headless environment.
        /// </summary>
        /// <param name="skip">True to skip UI initializations; false to include them.</param>
        public void SetSkipUIInitializations(bool skip = true)
        {
            initializationConfig.SkipUIInitializations = skip;
            if (skip)
            {
                logger.LogInformation("Configured to skip UI-related initializations");
            }
        }
        
        /// <summary>
        /// Gets or sets whether process scheduling is enabled.
        /// </summary>
        public bool IsSchedulerEnabled
        {
            get => schedulerEnabled;
            set => schedulerEnabled = value;
        }
        
        /// <summary>
        /// Gets the currently active context.
        /// </summary>
        internal MethodContext? GetActiveContext() => activeContext;
        
        /// <summary>
        /// Sets the active context.
        /// </summary>
        internal void SetActiveContext(MethodContext? context) => activeContext = context;
        
        /// <summary>
        /// Gets statistics from the method context pool for performance monitoring.
        /// </summary>
        /// <returns>Tuple containing (rented, returned, created, currentSharedSize) counts.</returns>
        public (long rented, long returned, long created, int currentSharedSize) GetContextPoolStatistics()
        {
            return contextPool.GetStatistics();
        }

        /// <summary>
        /// Executes the bootstrap process.
        /// </summary>
        public void Bootstrap()
        {
            logger.LogInformation("Starting bootstrap process...");
            
            // Get kernel class specifications
            var kernelClasses = KernelSpecification.Classes;
            
            // Phase 1: Allocate stubs
            classBuilder.AllocateStubs(kernelClasses);
            
            // Phase 2: Wire class pointers
            classBuilder.WireClassPointers();
            
            // Phase 3: Wire superclasses
            classBuilder.WireSuperclasses(kernelClasses);
            
            // Phase 4: Set class names (Symbol class now exists)
            classBuilder.SetClassNames(this);
            
            // Phase 5: Set instance variables (Array class now exists)
            classBuilder.SetInstanceVariables(kernelClasses, this);
            
            // Phase 6: Create special objects
            CreateSpecialObjects();
            
            // Phase 7: Create initial symbol table
            CreateInitialSymbolTable();
            
            // Phase 8: Create system dictionary
            CreateSystemDictionary();
            
            bootstrapComplete = true;
            logger.LogInformation("Bootstrap complete!");
            
            // Initialize message dispatcher with common selectors
            messageDispatcher.Initialize();
            
            // Run garbage collection to verify everything is reachable
            RunGarbageCollection();
        }

        /// <summary>
        /// Loads the bootstrap from a JSON file.
        /// </summary>
        /// <param name="bootstrapPath">Path to the bootstrap.json file.</param>
        public void LoadBootstrapFromJson(string bootstrapPath)
        {
            if (DebugOptions.EnableClassRegistrationOutput)
            {
                logger.LogInformation("Loading bootstrap from JSON: {BootstrapPath}", bootstrapPath);
            }
            
            // Load the manifest file first
            var manifest = LoadManifestFromFile(bootstrapPath);
            
            // Store the manifest for later use (e.g., post-bootstrap initialization)
            lastLoadedManifest = manifest;
            
            // Create the classes using ClassBuilder directly
            ProcessBootstrapClasses(manifest);
            
            // Create special objects now that classes exist
            CreateSpecialObjects();
            
            // Create initial symbol table (needed for SystemDictionary)
            CreateInitialSymbolTable();
            
            // Create system dictionary AND pool dictionaries BEFORE installing methods
            // This ensures that Associations in methods can find pool dictionaries
            CreateSystemDictionary();
            
            // NOW create a single MethodInstaller with access to special objects
            // and install the methods (pool dictionaries now exist)
            var methodInstaller = new MethodInstaller(memory, classBuilder, this, this, 
                NullLogger<MethodInstaller>.Instance, GetSpecialObject);
            methodInstaller.InstallMethods(manifest);
            
            bootstrapComplete = true;
            logger.LogInformation("Bootstrap complete! Loaded {ClassCount} classes", manifest.Classes.Count);
            
            // Initialize message dispatcher with common selectors
            messageDispatcher.Initialize();
            
            logger.LogInformation("Load statistics - Classes: {ClassCount}, Methods: {MethodCount}", 
                manifest.Classes.Count, methodInstaller.GetAllCachedMethods().Count);
            
            // Run garbage collection to verify everything is reachable
            RunGarbageCollection();
            
            // Start execution with an initial process - but only for complete system
            // For bootstrap only, we don't create any temporary startup objects
            // This MUST be done before initialization expressions so we have a process context
            if (manifest.Classes.Count > 100)  // Complete system has 223 classes, bootstrap has 85
            {
                StartExecution();
            }
            
            // Store the manifest for post-bootstrap initialization
            savedManifest = manifest;

            // Skip SystemDictionary>>install during bootstrap - it's for snapshot resume, not initial load
            // The install method tries to initialize classes that need working arithmetic,
            // but arithmetic needs those initializations - circular dependency
            // CallSystemInstall();

            // Then execute initialization expressions with retry logic
            if (manifest.InitializationExpressions != null && manifest.InitializationExpressions.Count > 0)
            {
                logger.LogInformation("Found {Count} initialization expressions", manifest.InitializationExpressions.Count);
                ExecuteInitializationExpressions(manifest, manifest.InitializationExpressions);
            }
            else
            {
                logger.LogDebug("No initialization expressions found");
            }
        }

        /// <summary>
        /// Loads extended class manifests from a directory.
        /// </summary>
        /// <param name="manifestDirectory">Directory containing extended manifest JSON files.</param>
        public void LoadExtendedManifests(string manifestDirectory)
        {
            if (!bootstrapComplete)
            {
                throw new InvalidOperationException("Bootstrap must be completed before loading extended manifests");
            }
            
            logger.LogInformation("Loading extended manifests from: {Directory}", manifestDirectory);
            // TODO: Implement loading of extended manifests
        }

        private void CreateSpecialObjects()
        {
            logger.LogDebug("Creating special objects...");
            
            // nil (already exists at index 0)
            specialObjectsArray[SpecialObjects.NilObject] = OOP.Nil;
            
            // false
            var falseClass = classBuilder.GetClass("False");
            if (!falseClass.IsNil)
            {
                var falseObj = memory.AllocateObject(falseClass, 0);
                specialObjectsArray[SpecialObjects.FalseObject] = falseObj;
            }
            
            // true
            var trueClass = classBuilder.GetClass("True");
            if (!trueClass.IsNil)
            {
                var trueObj = memory.AllocateObject(trueClass, 0);
                specialObjectsArray[SpecialObjects.TrueObject] = trueObj;
            }
            
            // Behavior class
            specialObjectsArray[SpecialObjects.BehaviorClass] = classBuilder.GetClass("Behavior");
            
            // Metaclass class
            specialObjectsArray[SpecialObjects.MetaclassClass] = classBuilder.GetClass("Metaclass");
            
            // Create ProcessList to track all processes (for GC roots)
            var linkedListClass = classBuilder.GetClass("LinkedList");
            if (!linkedListClass.IsNil)
            {
                var processList = memory.AllocateObject(linkedListClass, 2);
                var processListObj = memory.GetObject(processList);
                if (processListObj != null)
                {
                    processListObj.Fields[0] = OOP.Nil; // firstLink
                    processListObj.Fields[1] = OOP.Nil; // lastLink
                }
                specialObjectsArray[SpecialObjects.ProcessList] = processList;
                
                if (DebugOptions.EnableDebugOutput)
                {
                    logger.LogTrace("Created ProcessList at index {Index}", SpecialObjects.ProcessList);
                }
            }
            
            // Create ProcessorScheduler if the class exists
            var processorSchedulerClass = classBuilder.GetClass("ProcessorScheduler");
            if (!processorSchedulerClass.IsNil)
            {
                // Create the singleton ProcessorScheduler instance
                var schedulerOop = memory.AllocateObject(processorSchedulerClass, 2);
                specialObjectsArray[SpecialObjects.ProcessorScheduler] = schedulerOop;
                processScheduler.ProcessorSchedulerOop = schedulerOop;
                
                // Initialize the scheduler's fields
                var schedulerObj = memory.GetObject(schedulerOop);
                if (schedulerObj != null)
                {
                    // Create array of process lists (one for each priority)
                    var arrayClass = classBuilder.GetClass("Array");
                    var linkedListClass2 = classBuilder.GetClass("LinkedList");
                    
                    if (!arrayClass.IsNil && !linkedListClass2.IsNil)
                    {
                        // Create array of 100 priority queues
                        var priorityListsArray = memory.AllocateWordArray(arrayClass, 100);
                        var priorityListsObj = memory.GetObject(priorityListsArray);
                        
                        if (priorityListsObj != null)
                        {
                            // Initialize each priority queue as a LinkedList
                            for (int i = 0; i < 100; i++)
                            {
                                var list = memory.AllocateObject(linkedListClass2, 2);
                                var listObj = memory.GetObject(list);
                                if (listObj != null)
                                {
                                    listObj.Fields[0] = OOP.Nil; // firstLink
                                    listObj.Fields[1] = OOP.Nil; // lastLink
                                }
                                priorityListsObj.Fields[i] = list;
                            }
                            
                            schedulerObj.Fields[0] = priorityListsArray; // quiescentProcessLists
                            schedulerObj.Fields[1] = OOP.Nil; // activeProcess (initially nil)
                        }
                    }
                }
            }
        }

        private void CreateInitialSymbolTable()
        {
            logger.LogDebug("Creating initial symbol table...");
            
            // Create a Dictionary to hold symbols
            var dictionaryClass = classBuilder.GetClass("Dictionary");
            if (!dictionaryClass.IsNil)
            {
                var symbolTableObj = memory.AllocateObject(dictionaryClass, 2);
                specialObjectsArray[SpecialObjects.SymbolTable] = symbolTableObj;
                
                // Initialize with basic capacity
                // Dictionary uses a single array for storing Associations (key-value pairs)
                var arrayClass = classBuilder.GetClass("Array");
                if (!arrayClass.IsNil)
                {
                    // Dictionary/Set stores elements in a single array
                    // For symbols, we'll need space for Association objects
                    var dictArray = memory.AllocateWordArray(arrayClass, 64); // Initial capacity
                    
                    var symbolTableLayout = memory.GetObject(symbolTableObj);
                    if (symbolTableLayout != null)
                    {
                        symbolTableLayout.Fields[0] = OOP.FromSmallInteger(0); // tally
                        symbolTableLayout.Fields[1] = dictArray; // array for dictionary
                    }
                }
            }
        }

        private void CreateSystemDictionary()
        {
            logger.LogDebug("Creating system dictionary...");
            
            var systemDictClass = classBuilder.GetClass("SystemDictionary");
            if (!systemDictClass.IsNil)
            {
                var allClasses = classBuilder.GetAllClasses();
                
                // Calculate capacity: need 2 slots per entry (key, value) plus buffer
                // We need space for classes AND metaclasses, plus pool dictionaries
                // Add extra space for pool dictionaries and other globals
                int capacity = (allClasses.Count * 8) + 100; // Extra space for pools and globals
                
                // Create SystemDictionary with proper size:
                // 1 field for tally + capacity for indexed/variable portion
                var systemDict = memory.AllocateObject(systemDictClass, 1 + capacity);
                specialObjectsArray[SpecialObjects.SystemDictionaryInstance] = systemDict;
                
                // Initialize and register all classes
                RegisterAllClasses(systemDict);
                
                // Create pool dictionaries AFTER registering classes
                CreatePoolDictionaries(systemDict);
            }
        }

        private void CreatePoolDictionaries(OOP systemDict)
        {
            logger.LogDebug("Creating pool dictionaries...");
            
            var dictClass = classBuilder.GetClass("Dictionary");
            if (dictClass.IsNil)
            {
                logger.LogWarning("Dictionary class not found - cannot create pool dictionaries");
                return;
            }
            
            // List of pool dictionaries needed in the Smalltalk-80 system
            // AltoFilePool removed - no longer needed with Windows file implementation
            var poolNames = new[] { "FilePool", "TextConstants" };
            
            foreach (var poolName in poolNames)
            {
                // Create a new Dictionary instance for the pool
                // Dictionary has tally field + variable indexed part
                // Start with a reasonable size (32 slots = 16 key-value pairs)
                var poolDict = memory.AllocateObject(dictClass, 1 + 32);
                var poolObj = memory.GetObject(poolDict);
                
                if (poolObj != null)
                {
                    // Initialize tally to 0
                    poolObj.Fields[0] = OOP.FromSmallInteger(0);
                    
                    // Initialize all indexed slots to nil
                    for (int i = 1; i < poolObj.Fields.Length; i++)
                    {
                        poolObj.Fields[i] = OOP.Nil;
                    }
                    
                    // Register the pool dictionary in the system dictionary
                    var poolSymbol = Intern(poolName);
                    AddToSystemDictionary(systemDict, poolSymbol, poolDict);
                    
                    logger.LogDebug("Created pool dictionary: {PoolName}", poolName);
                }
            }
        }
        
        private void AddToSystemDictionary(OOP systemDict, OOP key, OOP value)
        {
            var systemDictObj = memory.GetObject(systemDict);
            if (systemDictObj == null) return;
            
            int capacity = systemDictObj.Fields.Length - 1; // -1 for tally field
            int pairCapacity = capacity / 2; // Each entry uses 2 slots
            
            // Simple hash table insertion with linear probing
            var hash = Math.Abs(key.RawValue) % pairCapacity;
            
            for (int i = 0; i < pairCapacity; i++)
            {
                var pairIndex = (hash + i) % pairCapacity;
                var keyIndex = 1 + (pairIndex * 2);     // +1 to skip tally field
                var valueIndex = keyIndex + 1;
                
                if (systemDictObj.Fields[keyIndex].IsNil)
                {
                    systemDictObj.Fields[keyIndex] = key;
                    systemDictObj.Fields[valueIndex] = value;
                    
                    // Update tally
                    var tally = systemDictObj.Fields[0].SmallIntegerValue;
                    systemDictObj.Fields[0] = OOP.FromSmallInteger(tally + 1);
                    break;
                }
                else if (systemDictObj.Fields[keyIndex].Equals(key))
                {
                    // Update existing value
                    systemDictObj.Fields[valueIndex] = value;
                    break;
                }
            }
        }
        
        private void RegisterAllClasses(OOP systemDict)
        {
            var systemDictObj = memory.GetObject(systemDict);
            if (systemDictObj == null) return;
            
            var allClasses = classBuilder.GetAllClasses();
            int capacity = systemDictObj.Fields.Length - 1; // -1 for tally field
            
            // Initialize all indexed slots to nil
            for (int i = 1; i < systemDictObj.Fields.Length; i++)
            {
                systemDictObj.Fields[i] = OOP.Nil;
            }
            
            // Initialize tally to 0
            systemDictObj.Fields[0] = OOP.FromSmallInteger(0);
            
            // Register all classes AND metaclasses with Symbol keys
            int registeredCount = 0;
            int pairCapacity = capacity / 2; // Each entry uses 2 slots
            
            // First register regular classes
            foreach (var kvp in allClasses)
            {
                // Class name must be a Symbol
                var classNameSymbol = Intern(kvp.Key);
                var classOop = kvp.Value;
                
                // Simple hash table insertion with linear probing
                var hash = Math.Abs(classNameSymbol.RawValue) % pairCapacity;
                
                for (int i = 0; i < pairCapacity; i++)
                {
                    var pairIndex = (hash + i) % pairCapacity;
                    var keyIndex = 1 + (pairIndex * 2);     // +1 to skip tally field
                    var valueIndex = keyIndex + 1;
                    
                    if (systemDictObj.Fields[keyIndex].IsNil)
                    {
                        systemDictObj.Fields[keyIndex] = classNameSymbol;
                        systemDictObj.Fields[valueIndex] = classOop;
                        registeredCount++;
                        if (DebugOptions.EnableClassRegistrationOutput)
                        {
                            logger.LogTrace("Registered class: {ClassName} as Symbol {Symbol}", kvp.Key, classNameSymbol);
                        }
                        break;
                    }
                    // Check if key already exists
                    else if (systemDictObj.Fields[keyIndex].Equals(classNameSymbol))
                    {
                        // Update existing value
                        systemDictObj.Fields[valueIndex] = classOop;
                        break;
                    }
                }
                
                // Also register the metaclass with name "ClassName class"
                var metaclassOop = classBuilder.GetMetaclass(kvp.Key);
                if (!metaclassOop.IsNil)
                {
                    var metaclassNameSymbol = Intern(kvp.Key + " class");
                    hash = Math.Abs(metaclassNameSymbol.RawValue) % pairCapacity;
                    
                    for (int i = 0; i < pairCapacity; i++)
                    {
                        var pairIndex = (hash + i) % pairCapacity;
                        var keyIndex = 1 + (pairIndex * 2);
                        var valueIndex = keyIndex + 1;
                        
                        if (systemDictObj.Fields[keyIndex].IsNil)
                        {
                            systemDictObj.Fields[keyIndex] = metaclassNameSymbol;
                            systemDictObj.Fields[valueIndex] = metaclassOop;
                            registeredCount++;
                            if (DebugOptions.EnableClassRegistrationOutput)
                            {
                                logger.LogTrace("Registered metaclass: {ClassName} class as Symbol {Symbol}", kvp.Key, metaclassNameSymbol);
                            }
                            break;
                        }
                        else if (systemDictObj.Fields[keyIndex].Equals(metaclassNameSymbol))
                        {
                            systemDictObj.Fields[valueIndex] = metaclassOop;
                            break;
                        }
                    }
                }
            }
            
            // Update tally
            systemDictObj.Fields[0] = OOP.FromSmallInteger(registeredCount);
        }
        
        private static int NextPowerOfTwo(int n)
        {
            if (n <= 1) return 2;
            n--;
            n |= n >> 1;
            n |= n >> 2;
            n |= n >> 4;
            n |= n >> 8;
            n |= n >> 16;
            return n + 1;
        }

        private void RegisterPrimitives()
        {
            // Register all primitive classes
            ArithmeticPrimitives.RegisterAll(primitiveTable);
            ArrayPrimitives.RegisterAll(primitiveTable);
            ClassPrimitives.RegisterAll(primitiveTable);
            CompilerPrimitives.RegisterAll(primitiveTable);
            ControlPrimitives.RegisterAll(primitiveTable);
            ProcessPrimitives.RegisterAll(primitiveTable);
            StringPrimitives.RegisterAll(primitiveTable);
            SystemPrimitives.RegisterAll(primitiveTable);
            TranscriptPrimitives.RegisterAll(primitiveTable);
            IOPrimitives.RegisterAll(primitiveTable);
            // Note: ComparisonPrimitives are already registered by ArithmeticPrimitives
            // Note: Some primitives may be registered by multiple classes, last registration wins
        }

        /// <summary>
        /// Runs garbage collection.
        /// </summary>
        /// <summary>
        /// Runs a full garbage collection cycle.
        /// </summary>
        /// <param name="verbose">Whether to output verbose GC information.</param>
        /// <param name="compact">Whether to compact memory after collection.</param>
        /// <returns>GC statistics from the collection.</returns>
        public GCStatistics RunGarbageCollection(bool verbose = false, bool compact = false)
        {
            // Add any active context objects as temporary roots
            // MethodContext is a C# object, but it holds Smalltalk objects that need to be roots
            if (activeContext != null)
            {
                // Add the receiver and method as roots
                gc.AddRoot(activeContext.Receiver);
                gc.AddRoot(activeContext.Method);
                
                // Add all arguments and temporaries
                foreach (var arg in activeContext.Arguments)
                {
                    if (!arg.IsNil && !arg.IsSmallInteger)
                        gc.AddRoot(arg);
                }
                
                foreach (var temp in activeContext.Temporaries)
                {
                    if (!temp.IsNil && !temp.IsSmallInteger)
                        gc.AddRoot(temp);
                }
                
                // Add stack contents
                foreach (var item in activeContext.Stack.ToArray())
                {
                    if (!item.IsNil && !item.IsSmallInteger)
                        gc.AddRoot(item);
                }
            }
            
            // Add doItProcess if it exists
            if (!doItProcess.IsNil)
            {
                gc.AddRoot(doItProcess);
            }
            
            var options = new GCOptions
            {
                Generation = 2, // Full collection
                Verbose = verbose,
                Compact = compact
            };
            
            logger.LogDebug("Running garbage collection...");
            var stats = gc.Collect(specialObjectsArray, options);
            logger.LogDebug("GC complete: {Stats}", stats);
            
            // Remove temporary roots after collection
            if (activeContext != null)
            {
                gc.RemoveRoot(activeContext.Receiver);
                gc.RemoveRoot(activeContext.Method);
                
                foreach (var arg in activeContext.Arguments)
                {
                    if (!arg.IsNil && !arg.IsSmallInteger)
                        gc.RemoveRoot(arg);
                }
                
                foreach (var temp in activeContext.Temporaries)
                {
                    if (!temp.IsNil && !temp.IsSmallInteger)
                        gc.RemoveRoot(temp);
                }
                
                foreach (var item in activeContext.Stack.ToArray())
                {
                    if (!item.IsNil && !item.IsSmallInteger)
                        gc.RemoveRoot(item);
                }
            }
            
            if (!doItProcess.IsNil)
            {
                gc.RemoveRoot(doItProcess);
            }
            
            return stats;
        }
        
        /// <summary>
        /// Adds a temporary GC root to prevent collection during execution.
        /// Note: The only permanent root is the Special Objects Array.
        /// </summary>
        /// <param name="root">The object to add as a temporary GC root.</param>
        public void AddGCRoot(OOP root)
        {
            gc.AddRoot(root);
        }
        
        /// <summary>
        /// Removes a temporary GC root, allowing collection if unreachable.
        /// </summary>
        /// <param name="root">The root to remove.</param>
        public void RemoveGCRoot(OOP root)
        {
            gc.RemoveRoot(root);
        }
        
        /// <summary>
        /// Gets current GC statistics.
        /// </summary>
        /// <returns>GC performance statistics.</returns>
        public (int TotalCollections, long TotalBytesFreed, TimeSpan TotalGCTime) GetGCStatistics()
        {
            return (gc.TotalCollections, gc.TotalBytesFreed, gc.TotalGCTime);
        }

        #region IExecutionContext Implementation

        public OOP GetSpecialObject(int index)
        {
            if (index < 0 || index >= specialObjectsArray.Length)
                return OOP.Nil;
            return specialObjectsArray[index];
        }

        public void Return(MethodContext context, OOP value)
        {
            // Return to sender
            if (context.Sender != null)
            {
                context.Sender.Stack.Push(value);
                activeContext = context.Sender;
                
                // Return the context to the pool for reuse
                contextPool.Return(context);
            }
        }

        public void ReturnFromBlock(MethodContext context, OOP value)
        {
            // Block return logic
            var home = context.GetHome();
            Return(home, value);
        }

        public OOP CreateContextObject(MethodContext context)
        {
            // Create a context object representation
            var contextClass = classBuilder.GetClass("MethodContext");
            if (!contextClass.IsNil)
            {
                // Create context object with appropriate fields
                return memory.AllocateObject(contextClass, 6);
            }
            return OOP.Nil;
        }

        public OOP CreateBlockContext(MethodContext context, int blockIndex)
        {
            // Get the block literal from the method's literals array
            var methodObj = memory.GetObject(context.Method);
            if (methodObj == null)
            {
                return OOP.Nil;
            }
            
            var literalsOop = GetFieldByName(context.Method, "literals");
            if (literalsOop.IsNil)
            {
                return OOP.Nil;
            }
            
            var literalsObj = memory.GetObject(literalsOop);
            if (literalsObj == null || blockIndex >= literalsObj.Fields.Length)
            {
                return OOP.Nil;
            }
            
            // Get the block literal at the specified index
            var blockLiteral = literalsObj.Fields[blockIndex];
            if (blockLiteral.IsNil)
            {
                return OOP.Nil;
            }
            
            // In Smalltalk-80, a block literal is typically an Array containing:
            // [0] = argument count (SmallInteger)
            // [1] = start PC (SmallInteger) - where the block body starts
            // [2] = end PC (SmallInteger) - where the block body ends (optional)
            // [3] = number of temporaries (SmallInteger)
            var blockLiteralObj = memory.GetObject(blockLiteral);
            if (blockLiteralObj == null || blockLiteralObj.Fields.Length < 2)
            {
                return OOP.Nil;
            }
            
            // Extract block information
            int argCount = 0;
            int startPC = 0;
            int tempCount = 0;
            
            // Get argument count
            if (blockLiteralObj.Fields.Length > 0 && blockLiteralObj.Fields[0].IsSmallInteger)
            {
                argCount = blockLiteralObj.Fields[0].SmallIntegerValue;
            }
            
            // Get start PC
            if (blockLiteralObj.Fields.Length > 1 && blockLiteralObj.Fields[1].IsSmallInteger)
            {
                startPC = blockLiteralObj.Fields[1].SmallIntegerValue;
            }
            
            // Get temp count if available
            if (blockLiteralObj.Fields.Length > 3 && blockLiteralObj.Fields[3].IsSmallInteger)
            {
                tempCount = blockLiteralObj.Fields[3].SmallIntegerValue;
            }
            
            // Create the BlockContext object
            var blockContextClass = classBuilder.GetClass("BlockContext");
            if (blockContextClass.IsNil)
            {
                return OOP.Nil;
            }
            
            // BlockContext inherits from ContextPart and has these fields:
            // From ContextPart: sender, pc, stackp
            // From BlockContext: nargs, startpc, home
            // Total: 6 fields minimum
            var blockContextOop = memory.AllocateObject(blockContextClass, 6);
            var blockContextObj = memory.GetObject(blockContextOop);
            
            if (blockContextObj == null)
            {
                return OOP.Nil;
            }
            
            // Initialize BlockContext fields
            // Field 0: sender (initially nil, set when block is activated)
            blockContextObj.Fields[0] = OOP.Nil;
            
            // Field 1: pc (program counter - set to startPC when activated)
            blockContextObj.Fields[1] = OOP.FromSmallInteger(startPC);
            
            // Field 2: stackp (stack pointer - initially 0)
            blockContextObj.Fields[2] = OOP.FromSmallInteger(0);
            
            // Field 3: nargs (number of arguments)
            blockContextObj.Fields[3] = OOP.FromSmallInteger(argCount);
            
            // Field 4: startpc (initial PC for the block)
            blockContextObj.Fields[4] = OOP.FromSmallInteger(startPC);
            
            // Field 5: home (the home context - the method context that created this block)
            // Create a context object for the home context if not already created
            var homeContextOop = CreateContextObject(context);
            if (!homeContextOop.IsNil)
            {
                // Copy current context state to the home context object
                var homeContextObj = memory.GetObject(homeContextOop);
                if (homeContextObj != null && homeContextObj.Fields.Length >= 6)
                {
                    // Set basic context fields
                    homeContextObj.Fields[0] = context.Sender != null ? CreateContextObject(context.Sender) : OOP.Nil; // sender
                    homeContextObj.Fields[1] = OOP.FromSmallInteger(context.PC); // pc
                    homeContextObj.Fields[2] = OOP.FromSmallInteger(context.Stack.Count); // stackp
                    homeContextObj.Fields[3] = context.Method; // method
                    homeContextObj.Fields[4] = context.Receiver; // receiver
                    
                    // Store arguments and temporaries if space available
                    if (homeContextObj.Fields.Length > 5)
                    {
                        int fieldIndex = 5;
                        
                        // Store arguments
                        for (int i = 0; i < context.Arguments.Length && fieldIndex < homeContextObj.Fields.Length; i++)
                        {
                            homeContextObj.Fields[fieldIndex++] = context.Arguments[i];
                        }
                        
                        // Store temporaries
                        for (int i = 0; i < context.Temporaries.Length && fieldIndex < homeContextObj.Fields.Length; i++)
                        {
                            homeContextObj.Fields[fieldIndex++] = context.Temporaries[i];
                        }
                    }
                }
            }
            blockContextObj.Fields[5] = homeContextOop;
            
            if (DebugOptions.EnableMethodTrace)
            {
                logger.LogTrace("Created BlockContext - argCount: {ArgCount}, startPC: {StartPC}, tempCount: {TempCount}", 
                    argCount, startPC, tempCount);
            }
            
            return blockContextOop;
        }

        /// <summary>
        /// Tracks the last result from a Send operation for retrieval by SendWithResult.
        /// </summary>
        private OOP lastSendResult = OOP.Nil;
        
        public void Send(OOP receiver, OOP selector, OOP[] arguments)
        {
            // Delegate to SendWithResult but discard the result for backward compatibility
            SendWithResult(receiver, selector, arguments);
        }
        
        /// <summary>
        /// Sends a message and returns the result.
        /// </summary>
        public OOP SendWithResult(OOP receiver, OOP selector, OOP[] arguments)
        {
            // Increment recursion depth
            recursionDepth++;
            
            // Check recursion depth (can be disabled during initialization)
            if (checkRecursionDepth && recursionDepth > MaxRecursionDepth)
            {
                recursionDepth--; // Restore the depth before throwing
                var selectorStr = GetSymbolString(selector);
                throw new InvalidOperationException($"Maximum recursion depth ({MaxRecursionDepth}) exceeded while sending '{selectorStr}'");
            }
            
            try
            {
                // Look up method in receiver's class
                var (method, methodClass) = messageDispatcher.Dispatch(receiver, selector, arguments);
                
                if (method.IsNil)
                {
                    // Method not found - send doesNotUnderstand:
                    HandleDoesNotUnderstand(receiver, selector, arguments);
                    return OOP.Nil;
                }
                
                // Execute the method and get result
                var result = ExecuteMethodWithResult(receiver, method, methodClass, arguments);
                lastSendResult = result;
                return result;
            }
            finally
            {
                recursionDepth--;
            }
        }

        public void SuperSend(OOP receiver, OOP selector, OOP[] arguments)
        {
            // Delegate to SuperSendWithResult but discard the result for backward compatibility
            SuperSendWithResult(receiver, selector, arguments);
        }
        
        /// <summary>
        /// Performs a super send and returns the result.
        /// </summary>
        public OOP SuperSendWithResult(OOP receiver, OOP selector, OOP[] arguments)
        {
            // Increment recursion depth
            recursionDepth++;
            
            // Check recursion depth (can be disabled during initialization)
            if (checkRecursionDepth && recursionDepth > MaxRecursionDepth)
            {
                recursionDepth--; // Restore the depth before throwing
                var selectorStr = GetSymbolString(selector);
                throw new InvalidOperationException($"Maximum recursion depth ({MaxRecursionDepth}) exceeded while super sending '{selectorStr}'");
            }
            
            try
            {
                // Get the superclass of the current method's class
                if (activeContext == null)
                {
                    throw new InvalidOperationException("SuperSend called with no active context");
                }
                
                var methodObj = memory.GetObject(activeContext.Method);
                if (methodObj == null)
                {
                    throw new InvalidOperationException("Invalid method object in active context");
                }
                
                // Get the class that owns the current method
                // This would normally be tracked during method lookup
                // For now, we need to find it
                var methodClass = FindMethodClass(activeContext.Method);
                if (methodClass.IsNil)
                {
                    throw new InvalidOperationException("Cannot determine method class for super send");
                }
                
                // Get superclass for super send
                var superclass = GetFieldByName(methodClass, "superclass");
                if (superclass.IsNil)
                {
                    throw new InvalidOperationException("Invalid superclass for super send");
                }
                
                // Look up method starting from superclass
                var (method, actualMethodClass) = messageDispatcher.Dispatch(receiver, selector, arguments, superclass);
                
                if (method.IsNil)
                {
                    // Method not found - send doesNotUnderstand:
                    HandleDoesNotUnderstand(receiver, selector, arguments);
                    return OOP.Nil;
                }
                
                // Execute the method and return result
                var result = ExecuteMethodWithResult(receiver, method, actualMethodClass, arguments);
                lastSendResult = result;
                return result;
            }
            finally
            {
                recursionDepth--;
            }
        }

        public OOP InternSymbol(string value) => Intern(value);

        /// <summary>
        /// Gets or sets whether recursion depth checking is enabled.
        /// Can be temporarily disabled during initialization if needed.
        /// </summary>
        public bool RecursionCheckEnabled
        {
            get => checkRecursionDepth;
            set => checkRecursionDepth = value;
        }

        /// <summary>
        /// Gets the current recursion depth for debugging.
        /// </summary>
        public int CurrentRecursionDepth => recursionDepth;

        #endregion

        #region IPrimitiveContext Implementation

        public OOP GetClass(string className)
        {
            return classBuilder.GetClass(className);
        }

        public OOP NewInstance(OOP classOOP, int size)
        {
            return memory.AllocateObject(classOOP, size);
        }

        OOP IPrimitiveContext.Send(OOP receiver, OOP selector, OOP[] arguments)
        {
            // Use SendWithResult to properly return the result
            return SendWithResult(receiver, selector, arguments);
        }
        
        OOP IPrimitiveContext.InternSymbol(string value)
        {
            return Intern(value);
        }

        #endregion
        
        #region Field Access Methods
        
        /// <summary>
        /// Gets a field value from an object using the field name.
        /// </summary>
        /// <param name="objectOop">The object to read from.</param>
        /// <param name="className">The class name for field lookup.</param>
        /// <param name="fieldName">The name of the field to read.</param>
        /// <returns>The field value or Nil if not found.</returns>
        public OOP GetField(OOP objectOop, string className, string fieldName)
        {
            if (objectOop.IsNil) return OOP.Nil;
            
            var obj = memory.GetObject(objectOop);
            if (obj == null) return OOP.Nil;
            
            var index = fieldLookup.GetFieldIndex(className, fieldName);
            if (index < 0 || index >= obj.Fields.Length)
                return OOP.Nil;
                
            return obj.Fields[index];
        }
        
        /// <summary>
        /// Sets a field value in an object using the field name.
        /// </summary>
        /// <param name="objectOop">The object to write to.</param>
        /// <param name="className">The class name for field lookup.</param>
        /// <param name="fieldName">The name of the field to write.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool SetField(OOP objectOop, string className, string fieldName, OOP value)
        {
            if (objectOop.IsNil) return false;
            
            var obj = memory.GetObject(objectOop);
            if (obj == null) return false;
            
            var index = fieldLookup.GetFieldIndex(className, fieldName);
            if (index < 0 || index >= obj.Fields.Length)
                return false;
                
            obj.Fields[index] = value;
            return true;
        }
        
        /// <summary>
        /// Gets a field value by index (for performance-critical code).
        /// </summary>
        public OOP GetFieldByIndex(OOP objectOop, int index)
        {
            if (objectOop.IsNil) return OOP.Nil;
            
            var obj = memory.GetObject(objectOop);
            if (obj == null || index < 0 || index >= obj.Fields.Length)
                return OOP.Nil;
            
            // DEBUG: Track Point field access
            var classOop = obj.Header.ClassOOP;
            if (classOop.RawValue == 349) // Point class
            {
                logger.LogTrace("Getting Point {ObjectOop} field {Index} - Value: {Value}, All fields: x={X}, y={Y}", 
                    objectOop, index, obj.Fields[index], obj.Fields[0], 
                    (obj.Fields.Length > 1 ? obj.Fields[1] : OOP.Nil));
            }
                
            return obj.Fields[index];
        }
        
        /// <summary>
        /// Sets a field value by index (for performance-critical code).
        /// </summary>
        public bool SetFieldByIndex(OOP objectOop, int index, OOP value)
        {
            if (objectOop.IsNil) return false;
            
            var obj = memory.GetObject(objectOop);
            if (obj == null || index < 0 || index >= obj.Fields.Length)
                return false;
                
            // DEBUG: Track Point field assignments
            var classOop = obj.Header.ClassOOP;
            if (classOop.RawValue == 349) // Point class
            {
                logger.LogTrace("Setting Point {ObjectOop} field {Index} - Before: {Before}, New: {Value}", 
                    objectOop, index, obj.Fields[index], value);
            }
            
            obj.Fields[index] = value;
            
            if (classOop.RawValue == 349) // Point class
            {
                logger.LogTrace("Point {ObjectOop} after set - Field[{Index}]: {Value}, All fields: x={X}, y={Y}", 
                    objectOop, index, obj.Fields[index], obj.Fields[0], 
                    (obj.Fields.Length > 1 ? obj.Fields[1] : OOP.Nil));
            }
            
            return true;
        }
        
        /// <summary>
        /// Gets the field index for a class and field name.
        /// Can be cached by callers for performance.
        /// </summary>
        public int GetFieldIndex(string className, string fieldName)
        {
            return fieldLookup.GetFieldIndex(className, fieldName);
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Gets the selector for a CompiledMethod.
        /// </summary>
        private OOP GetMethodSelector(OOP method)
        {
            // The selector is stored in field 0 of the CompiledMethod
            var methodObj = memory.GetObject(method);
            if (methodObj == null) return OOP.Nil;
            
            // Field 0 contains the selector
            if (methodObj.Fields.Length > 0)
            {
                return methodObj.Fields[0];
            }
            
            return OOP.Nil;
        }
        
        /// <summary>
        /// Gets the class of an object.
        /// </summary>
        public OOP GetClassOfObject(OOP objectOop)
        {
            if (objectOop.IsNil) return OOP.Nil;
            if (objectOop.IsSmallInteger) 
            {
                return classBuilder.GetClass("SmallInteger");
            }
            
            var obj = memory.GetObject(objectOop);
            if (obj == null) return OOP.Nil;
            
            return obj.Header.ClassOOP;
        }
        
        /// <summary>
        /// Gets a string representation of a Symbol.
        /// </summary>
        public string GetSymbolString(OOP symbol)
        {
            if (symbol.IsNil) return "<nil>";
            if (symbol.IsSmallInteger) return symbol.SmallIntegerValue.ToString();
            
            var obj = memory.GetObject(symbol);
            if (obj == null) return "<invalid>";
            
            // Symbol is a byte array containing characters stored as SmallIntegers
            if (obj.IsByteIndexable && obj.Fields.Length > 0)
            {
                var chars = new char[obj.Fields.Length];
                for (int i = 0; i < obj.Fields.Length; i++)
                {
                    if (obj.Fields[i].IsSmallInteger)
                    {
                        chars[i] = (char)obj.Fields[i].SmallIntegerValue;
                    }
                    else
                    {
                        chars[i] = '?';
                    }
                }
                return new string(chars);
            }
            
            return "<not-a-symbol>";
        }
        
        
        /// <summary>
        /// Creates a Smalltalk String object from a C# string.
        /// </summary>
        /// <param name="value">The C# string value.</param>
        /// <returns>OOP of the created String object.</returns>
        public OOP CreateString(string value)
        {
            var stringClass = classBuilder.GetClass("String");
            if (stringClass.IsNil)
                return OOP.Nil;
            
            var stringOop = memory.AllocateByteArray(stringClass, value.Length);
            var stringObj = memory.GetObject(stringOop);
            
            if (stringObj != null)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    stringObj.Fields[i] = OOP.FromSmallInteger(value[i]);
                }
            }
            
            return stringOop;
        }
        
        /// <summary>
        /// Evaluates a Smalltalk expression string and returns the result.
        /// For now, handles simple arithmetic expressions directly.
        /// TODO: Integrate with Smalltalk Compiler class when ready.
        /// </summary>
        /// <param name="expression">The Smalltalk expression to evaluate.</param>
        /// <returns>The result of the evaluation, or Nil if evaluation failed.</returns>
        public OOP EvaluateExpression(string expression)
        {
            try
            {
                // For now, handle simple integer arithmetic expressions directly
                // This is a temporary implementation until the Compiler class is fully integrated
                
                // Try to parse simple binary expressions like "3 + 4" or single integers like "42"
                var parts = expression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                
                // Handle single integer literal
                if (parts.Length == 1)
                {
                    if (int.TryParse(parts[0], out int value))
                    {
                        return OOP.FromSmallInteger(value);
                    }
                }
                // Handle binary expressions
                else if (parts.Length == 3)
                {
                    if (int.TryParse(parts[0], out int left) && int.TryParse(parts[2], out int right))
                    {
                        var leftOop = OOP.FromSmallInteger(left);
                        var rightOop = OOP.FromSmallInteger(right);
                        
                        switch (parts[1])
                        {
                            case "+":
                                return OOP.FromSmallInteger(left + right);
                            case "-":
                                return OOP.FromSmallInteger(left - right);
                            case "*":
                                return OOP.FromSmallInteger(left * right);
                            case "/":
                                if (right != 0)
                                    return OOP.FromSmallInteger(left / right);
                                break;
                            case "=":
                                return left == right ? GetSpecialObject(SpecialObjects.TrueObject) : GetSpecialObject(SpecialObjects.FalseObject);
                            case "<":
                                return left < right ? GetSpecialObject(SpecialObjects.TrueObject) : GetSpecialObject(SpecialObjects.FalseObject);
                            case ">":
                                return left > right ? GetSpecialObject(SpecialObjects.TrueObject) : GetSpecialObject(SpecialObjects.FalseObject);
                        }
                    }
                }
                
                // For more complex expressions, we need the Compiler class
                // Check if the Compiler class is available
                var compilerClass = classBuilder.GetClass("Compiler");
                if (!compilerClass.IsNil)
                {
                    logger.LogWarning("Compiler class found, but full evaluation not yet implemented. Currently only simple arithmetic expressions are supported (e.g., '3 + 4')");
                }
                else
                {
                    logger.LogWarning("Compiler class not found in loaded system. Only simple arithmetic expressions are supported (e.g., '3 + 4')");
                }
                
                return OOP.Nil;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception during evaluation");
                return OOP.Nil;
            }
        }
        
        #endregion

        #region ISymbolTable Implementation

        public OOP Intern(string value)
        {
            if (symbolTable.TryGetValue(value, out var existing))
            {
                return existing;
            }
            
            // Create new symbol
            var symbolClass = classBuilder.GetClass("Symbol");
            if (!symbolClass.IsNil)
            {
                var symbolOop = memory.AllocateByteArray(symbolClass, value.Length);
                var symbolObj = memory.GetObject(symbolOop);
                
                if (symbolObj != null)
                {
                    // Store string bytes
                    for (int i = 0; i < value.Length; i++)
                    {
                        symbolObj.Fields[i] = OOP.FromSmallInteger(value[i]);
                    }
                    
                    symbolTable[value] = symbolOop;
                    return symbolOop;
                }
            }
            
            return OOP.Nil;
        }

        #endregion

        #region Method Execution

        private void ExecuteMethod(OOP receiver, OOP method, OOP methodClass, OOP[] arguments)
        {
            // Delegate to ExecuteMethodWithResult but discard the result for backward compatibility
            ExecuteMethodWithResult(receiver, method, methodClass, arguments);
        }
        
        /// <summary>
        /// Executes a method and returns the result.
        /// </summary>
        private OOP ExecuteMethodWithResult(OOP receiver, OOP method, OOP methodClass, OOP[] arguments)
        {
            // Method invocation trace
            if (DebugOptions.EnableMethodTrace || DebugOptions.EnableDebugOutput)
            {
                // Get selector name for the method
                var selectorOop = GetMethodSelector(method);
                var selectorName = selectorOop.IsNil ? "<unknown>" : GetSymbolString(selectorOop);
                
                // Get class name
                var className = GetClassName(methodClass);
                
                // Get receiver info
                var receiverClass = GetClassOfObject(receiver);
                var receiverClassName = receiverClass.IsNil ? "<nil>" : GetClassName(receiverClass);
                
                // Create indentation based on recursion depth
                var indent = new string(' ', recursionDepth * 2);
                
                if (DebugOptions.EnableMethodTrace)
                {
                    logger.LogTrace("[{Depth}] {Indent}{ReceiverClass} >> {Selector} (implementing class: {ImplementingClass})", 
                        recursionDepth, indent, receiverClassName, selectorName, className);
                }
                
                // ENHANCED DEBUG: Show arguments being passed
                if (DebugOptions.EnableDebugOutput)
                {
                    logger.LogTrace("Method Activation: {ReceiverClass}>>{Selector} - Method OOP: {Method}, Receiver: {Receiver}, Receiver class: {ReceiverClassName}, Argument count: {ArgCount}",
                        receiverClassName, selectorName, method, receiver, receiverClassName, arguments.Length);
                    
                    // Special tracking for Cursor class>>extent:fromArray:offset:
                    if (className == "Cursor class" && selectorName == "extent:fromArray:offset:")
                    {
                        logger.LogTrace("*** SPECIAL TRACKING: Cursor class>>extent:fromArray:offset: ***");
                    }
                    
                    for (int i = 0; i < arguments.Length; i++)
                    {
                        var arg = arguments[i];
                        logger.LogTrace("Argument[{Index}]: {Arg} - IsNil: {IsNil}, IsSmallInteger: {IsSmallInt}",
                            i, arg, arg.IsNil, arg.IsSmallInteger);
                        
                        if (!arg.IsNil && !arg.IsSmallInteger)
                        {
                            var argClass = GetClassOfObject(arg);
                            var argClassName = GetClassName(argClass);
                            logger.LogTrace("    Argument class: {ClassName}", argClassName);
                            
                            // Special handling for Point arguments
                            if (argClassName == "Point")
                            {
                                logger.LogTrace("    *** POINT ARGUMENT DETECTED ***");
                                var xField = GetFieldByIndex(arg, 0);
                                var yField = GetFieldByIndex(arg, 1);
                                logger.LogTrace("    Point coordinates - x: {X}, y: {Y}", xField, yField);
                                
                                if (xField.IsSmallInteger && yField.IsSmallInteger)
                                {
                                    logger.LogTrace("    Point value: {X}@{Y}", xField.SmallIntegerValue, yField.SmallIntegerValue);
                                }
                            }
                        }
                        else if (arg.IsSmallInteger)
                        {
                            logger.LogTrace("    SmallInteger value: {Value}", arg.SmallIntegerValue);
                        }
                    }
                }
            }
            
            var methodObj = memory.GetObject(method);
            if (methodObj == null)
            {
                throw new InvalidOperationException("Invalid method object");
            }
            
            // CompiledMethod fields:
            // 0: header (with primitive index, arg count, temp count, etc.)
            // 1: bytecode array
            // 2: literals array
            
            if (methodObj.Fields.Length < 3)
            {
                throw new InvalidOperationException("Invalid CompiledMethod structure");
            }
            
            var headerOop = GetFieldByName(method, "header");
            if (!headerOop.IsSmallInteger)
            {
                throw new InvalidOperationException("Invalid method header");
            }
            
            int header = headerOop.SmallIntegerValue;
            
            // Decode header (Blue Book format)
            int primitiveIndex = (header >> 1) & 0xFF;
            int argCount = (header >> 9) & 0x1F;
            int tempCount = (header >> 14) & 0x1F;
            int stackSize = (header >> 19) & 0x3F;
            
            if (DebugOptions.EnableDebugOutput)
            {
                // Debug methods with 0 temps to see if SmallInteger initialize is one
                var selectorOop = GetFieldByName(method, "selector");
                var selectorStr = GetSymbolString(selectorOop);
                if (selectorStr == "initialize")
                {
                    logger.LogTrace("Method {Selector} header: 0x{Header:X8} - Primitive: {Primitive}, Args: {Args}, Temps: {Temps}, Stack: {Stack}",
                        selectorStr, header, primitiveIndex, argCount, tempCount, stackSize);
                }
            }
            
            // Validate argument count
            if (arguments.Length != argCount)
            {
                throw new ArgumentException($"Method expects {argCount} arguments but received {arguments.Length}");
            }
            
            // Try primitive if present
            if (primitiveIndex > 0)
            {
                var result = primitiveTable.Execute(primitiveIndex, receiver, arguments, this);
                if (result.Success)
                {
                    // Primitive succeeded - just return the result
                    // The caller (bytecode dispatcher) will handle pushing to the stack
                    return result.Value;
                }
                // Primitive failed - continue with method execution
                if (DebugOptions.EnableDebugOutput)
                {
                    logger.LogDebug("Primitive {PrimitiveIndex} failed: {Reason}", primitiveIndex, result.FailureReason);
                }
            }
            
            // Create new method context using pool
            var newContext = contextPool.Rent(
                sender: activeContext,
                receiver: receiver,
                method: method,
                arguments: arguments,
                tempCount: tempCount,
                stackSize: Math.Max(stackSize, 16) // Ensure minimum stack size
            );
            
            // Push context onto execution stack
            executionStack.Push(newContext);
            var previousContext = activeContext;
            activeContext = newContext;
            
            OOP methodResult = OOP.Nil;
            try
            {
                // Execute bytecodes
                ExecuteBytecodes(newContext);
                
                // Get the result from the stack (the last value)
                if (newContext.Stack.Count > 0)
                {
                    methodResult = newContext.Stack.Top;
                }
            }
            finally
            {
                // Pop context from stack and return to pool
                if (executionStack.Count > 0)
                {
                    var contextToReturn = executionStack.Pop();
                    contextPool.Return(contextToReturn);
                }
                activeContext = previousContext;
            }
            
            return methodResult;
        }
        
        /// <summary>
        /// Executes the bytecodes of a method in the given context.
        /// Internal method to allow InitializationExpressionExecutor to execute bytecode directly.
        /// </summary>
        internal void ExecuteBytecodes(MethodContext context)
        {
            // VM bytecode execution tracing controlled by flags
            ExecuteBytecodesPrivate(context);
        }
        
        private void ExecuteBytecodesPrivate(MethodContext context)
        {
            var methodObj = memory.GetObject(context.Method);
            if (methodObj == null || methodObj.Fields.Length < 2)
                return;
            
            var bytecodeArrayOop = GetFieldByName(context.Method, "bytecode");
            var bytecodeArray = memory.GetObject(bytecodeArrayOop);
            if (bytecodeArray == null)
                return;
            
            // Detailed bytecode tracing controlled by EnableBytecodeTrace flag
            
            // Execute bytecodes until return or end
            while (context.PC < bytecodeArray.Fields.Length)
            {
                // Check for process switch if scheduler is enabled
                if (schedulerEnabled && ShouldCheckForProcessSwitch())
                {
                    CheckForProcessSwitch();
                    
                    // Context may have changed after process switch
                    if (activeContext != context)
                        break;
                }
                
                var byteOop = bytecodeArray.Fields[context.PC++];
                if (!byteOop.IsSmallInteger)
                    break;
                
                byte bytecode = (byte)byteOop.SmallIntegerValue;
                
                if (DebugOptions.EnableVMBytecodeMessages)
                {
                    logger.LogTrace("Executing bytecode: {Bytecode} (0x{BytecodeHex:X2}) at PC {PC}", 
                        bytecode, bytecode, context.PC - 1);
                }
                
                // Execute bytecode - returns false on return instructions
                try
                {
                    if (!bytecodeDispatcher.ExecuteBytecode(context, bytecode))
                        break;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Stack underflow") || ex.Message.Contains("Stack is empty"))
                {
                    // Enhance the error with PC information
                    var errorType = ex.Message.Contains("underflow") ? "Stack underflow" : "Stack empty";
                    throw new InvalidOperationException($"{errorType} at PC {context.PC - 1} (bytecode {bytecode:X2})", ex);
                }
            }
        }
        
        /// <summary>
        /// Determines if we should check for process switching.
        /// </summary>
        private bool ShouldCheckForProcessSwitch()
        {
            // Check every N bytecodes or on specific conditions
            // This is a simplified implementation
            return recursionDepth == 0 && executionStack.Count % 10 == 0;
        }
        
        /// <summary>
        /// Checks if a process switch is needed and performs it.
        /// </summary>
        private void CheckForProcessSwitch()
        {
            // This would be called by primitives that affect scheduling
            // For now, it's a hook for future integration
            if (!processScheduler.HasRunnableProcesses())
                return;
            
            // Check if a higher priority process is ready
            // This requires more integration with the Smalltalk process system
        }
        
        /// <summary>
        /// Performs a process switch to the specified process.
        /// </summary>
        /// <param name="newProcess">The process to switch to.</param>
        public void SwitchToProcess(OOP newProcess)
        {
            if (!schedulerEnabled)
                return;
            
            try
            {
                processScheduler.SwitchToProcess(newProcess);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Process switch failed");
            }
        }
        
        /// <summary>
        /// Suspends the specified process.
        /// </summary>
        /// <param name="process">The process to suspend.</param>
        /// <returns>The suspended context.</returns>
        public OOP SuspendProcess(OOP process)
        {
            if (!schedulerEnabled)
                return OOP.Nil;
            
            try
            {
                return processScheduler.SuspendProcess(process);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Process suspend failed");
                return OOP.Nil;
            }
        }
        
        /// <summary>
        /// Resumes the specified process.
        /// </summary>
        /// <param name="process">The process to resume.</param>
        public void ResumeProcess(OOP process)
        {
            if (!schedulerEnabled)
                return;
            
            try
            {
                processScheduler.ResumeProcess(process);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Process resume failed");
            }
        }
        
        private void HandleDoesNotUnderstand(OOP receiver, OOP selector, OOP[] arguments)
        {
            // For now, just print a diagnostic message and return nil
            // This prevents infinite loops when doesNotUnderstand: itself is missing
            
            var selectorStr = GetSymbolString(selector);
            var receiverClass = GetClassOfObject(receiver);
            var receiverClassName = GetClassName(receiverClass);
            
            // Print in red to make it visible
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            logger.LogError("Message not understood: {ReceiverClass}>>{Selector} with {ArgCount} arguments - Receiver: {Receiver}, Selector: {SelectorOop} ({SelectorStr})",
                receiverClassName, selectorStr, arguments.Length, receiver, selector, selectorStr);
            if (arguments.Length > 0)
            {
                logger.LogError("        Arguments: {Arguments}", string.Join(", ", arguments.Select(a => a.ToString())));
            }
            Console.ForegroundColor = oldColor;
            
            // If we're executing initialization expressions, throw an exception to stop execution
            if (executingInitialization)
            {
                throw new InvalidOperationException($"Message not understood during initialization: {receiverClassName}>>{selectorStr}");
            }
            
            // Push nil as the result
            if (activeContext != null)
            {
                activeContext.Stack.Push(OOP.Nil);
            }
            
            // TODO: Once the system is stable, implement proper doesNotUnderstand: handling
            // by creating a Message object and sending doesNotUnderstand: to the receiver
            
            /* Original implementation that causes infinite loops:
            // Create a Message object
            var messageClass = classBuilder.GetClass("Message");
            if (messageClass.IsNil)
            {
                throw new InvalidOperationException("Message class not found for doesNotUnderstand:");
            }
            
            var message = messageDispatcher.CreateMessageObject(selector, arguments, messageClass);
            
            // Send doesNotUnderstand: with the Message as argument
            var doesNotUnderstand = messageDispatcher.DoesNotUnderstandSelector;
            if (!doesNotUnderstand.IsNil)
            {
                Send(receiver, doesNotUnderstand, [message]);
            }
            else
            {
                throw new InvalidOperationException($"Message not understood: {selector}");
            }
            */
        }
        
        private OOP FindMethodClass(OOP method)
        {
            // This is a simplified implementation
            // In a full implementation, we'd track the method class during lookup
            // For now, search all classes for this method
            
            var allClasses = classBuilder.GetAllClasses();
            foreach (var kvp in allClasses)
            {
                var classOop = kvp.Value;
                var classObj = memory.GetObject(classOop);
                if (classObj != null && classObj.Fields.Length > 1)
                {
                    var methodDict = GetFieldByName(classOop, "methodDict");
                    if (ContainsMethod(methodDict, method))
                    {
                        return classOop;
                    }
                }
            }
            
            return OOP.Nil;
        }
        
        private bool ContainsMethod(OOP dictionary, OOP method)
        {
            var dictObj = memory.GetObject(dictionary);
            if (dictObj == null || dictObj.Fields.Length < 2)
                return false;
            
            var arrayOop = GetFieldByName(dictionary, "valueArray");
            var arrayObj = memory.GetObject(arrayOop);
            if (arrayObj == null)
                return false;
            
            // Search for method in dictionary values
            for (int i = 1; i < arrayObj.Fields.Length; i += 2)
            {
                if (arrayObj.Fields[i].RawValue == method.RawValue)
                    return true;
            }
            
            return false;
        }

        #endregion

        #region System Startup

        /// <summary>
        /// Loads and parses a manifest file.
        /// </summary>
        private BootstrapManifest LoadManifestFromFile(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                
                var manifest = JsonSerializer.Deserialize<BootstrapManifest>(json, options);
                
                if (manifest == null)
                {
                    throw new InvalidOperationException($"Failed to deserialize manifest from {filePath}");
                }
                
                return manifest;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Invalid JSON in manifest {filePath}: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Processes bootstrap classes using the ClassBuilder phases.
        /// </summary>
        private void ProcessBootstrapClasses(BootstrapManifest manifest)
        {
            if (DebugOptions.EnableClassRegistrationOutput)
            {
                logger.LogInformation("Processing bootstrap classes through ClassBuilder phases");
            }
            
            // PHASE -1: Build field mappings BEFORE any objects are created
            if (DebugOptions.EnableClassRegistrationOutput)
            {
                logger.LogDebug("Phase -1: Building field mappings from manifest...");
            }
            fieldLookup.RegisterManifest(manifest);
            if (DebugOptions.EnableClassRegistrationOutput)
            {
                logger.LogDebug("Phase -1 complete: Field mappings established for all classes and metaclasses");
            }
            
            // Convert manifest classes to KernelClass format
            var kernelClasses = manifest.Classes.Select(c => new KernelClass(
                c.Name,
                c.Superclass,
                c.InstanceVariables.ToArray()
            )).ToList();
            
            // Execute ClassBuilder phases
            classBuilder.AllocateStubs(kernelClasses);
            classBuilder.WireClassPointers();
            classBuilder.WireSuperclasses(kernelClasses);
            classBuilder.SetClassNames(this);
            classBuilder.SetInstanceVariables(kernelClasses, this);
            
            // Set additional class attributes (format, class variables, etc.)
            // This is critical for proper class initialization
            foreach (var classDef in manifest.Classes)
            {
                SetClassAttributes(classDef);
            }
        }
        
        /// <summary>
        /// Sets additional attributes on a class.
        /// </summary>
        private void SetClassAttributes(ClassDefinition classDef)
        {
            var classOop = classBuilder.GetClass(classDef.Name);
            if (classOop.IsNil) return;
            
            var classObj = memory.GetObject(classOop);
            if (classObj == null) return;
            
            // Get field indices using the lookup system for class objects
            // Class objects are instances of their metaclass, so we use GetMetaclassFieldIndex
            var formatIndex = fieldLookup.GetMetaclassFieldIndex(classDef.Name, "format");
            var classPoolIndex = fieldLookup.GetMetaclassFieldIndex(classDef.Name, "classPool");
            var sharedPoolsIndex = fieldLookup.GetMetaclassFieldIndex(classDef.Name, "sharedPools");
            
            // Set format if specified
            if (classDef.Format != 0 && formatIndex >= 0 && formatIndex < classObj.Fields.Length)
            {
                classObj.Fields[formatIndex] = OOP.FromSmallInteger(classDef.Format);
            }
            
            // Check if this class has classPool and sharedPools fields
            // (only Class objects have these, not Behavior or ClassDescription)
            if (classPoolIndex < 0 || sharedPoolsIndex < 0 || 
                classPoolIndex >= classObj.Fields.Length || 
                sharedPoolsIndex >= classObj.Fields.Length)
            {
                // Not a full Class object - might be Behavior or ClassDescription
                return;
            }
            
            // Set class variables (classPool field)
            if (classDef.ClassVariables.Count > 0)
            {
                // Create a Dictionary to hold class variables
                var dictClass = classBuilder.GetClass("Dictionary");
                if (!dictClass.IsNil)
                {
                    // Create dictionary with appropriate capacity
                    var capacity = Math.Max(16, classDef.ClassVariables.Count * 2);
                    var classPoolDict = CreateDictionary(dictClass, capacity);
                    
                    // Add each class variable as an Association in the dictionary
                    foreach (var varName in classDef.ClassVariables)
                    {
                        AddToDictionary(classPoolDict, varName, OOP.Nil);
                    }
                    
                    classObj.Fields[classPoolIndex] = classPoolDict;
                    logger.LogTrace("Set {Count} class variables for {ClassName}", classDef.ClassVariables.Count, classDef.Name);
                }
                else
                {
                    // Dictionary class not available yet during early bootstrap
                    classObj.Fields[classPoolIndex] = OOP.Nil;
                }
            }
            else
            {
                classObj.Fields[classPoolIndex] = OOP.Nil; // No class variables
            }
            
            // Set pool dictionaries (sharedPools field)
            if (classDef.PoolDictionaries.Count > 0)
            {
                // Pool dictionaries are references to shared dictionaries
                // For now, create an Array to hold the pool dictionary names
                var arrayClass = classBuilder.GetClass("Array");
                if (!arrayClass.IsNil)
                {
                    var poolArray = memory.AllocateWordArray(arrayClass, classDef.PoolDictionaries.Count);
                    var poolArrayObj = memory.GetObject(poolArray);
                    
                    if (poolArrayObj != null)
                    {
                        // Store pool dictionary names as Symbols
                        for (int i = 0; i < classDef.PoolDictionaries.Count; i++)
                        {
                            poolArrayObj.Fields[i] = Intern(classDef.PoolDictionaries[i]);
                        }
                    }
                    
                    classObj.Fields[sharedPoolsIndex] = poolArray;
                    logger.LogTrace("Set {Count} pool dictionaries for {ClassName}", classDef.PoolDictionaries.Count, classDef.Name);
                }
                else
                {
                    // Array class not available yet during early bootstrap
                    classObj.Fields[sharedPoolsIndex] = OOP.Nil;
                }
            }
            else
            {
                classObj.Fields[sharedPoolsIndex] = OOP.Nil; // No pool dictionaries
            }
        }
        
        /// <summary>
        /// Creates a Dictionary object with the specified capacity.
        /// </summary>
        private OOP CreateDictionary(OOP dictClass, int capacity)
        {
            // Dictionary has fields: tally, array (inherited from Set)
            var dict = memory.AllocateObject(dictClass, 2);
            var dictObj = memory.GetObject(dict);
            
            if (dictObj != null)
            {
                // Initialize tally to 0
                dictObj.Fields[0] = OOP.FromSmallInteger(0);
                
                // Create backing array with capacity for key-value pairs
                var arrayClass = classBuilder.GetClass("Array");
                if (!arrayClass.IsNil)
                {
                    var array = memory.AllocateWordArray(arrayClass, capacity);
                    dictObj.Fields[1] = array;
                }
                else
                {
                    dictObj.Fields[1] = OOP.Nil;
                }
            }
            
            return dict;
        }
        
        /// <summary>
        /// Adds a key-value pair to a Dictionary.
        /// </summary>
        private void AddToDictionary(OOP dict, string key, OOP value)
        {
            var dictObj = memory.GetObject(dict);
            if (dictObj == null || dictObj.Fields.Length < 2) return;
            
            // Dictionary inherits from Set which has: tally at 0, array at 1
            // But IdentityDictionary has: tally at 0, valueArray at 1
            // For now, we'll use direct field access at index 1 for the array
            var arrayOop = dictObj.Fields[1];
            if (arrayOop.IsNil) return;
            
            var arrayObj = memory.GetObject(arrayOop);
            if (arrayObj == null || arrayObj.Fields.Length == 0) return;
            
            // Create Association for the key-value pair
            var assocClass = classBuilder.GetClass("Association");
            if (!assocClass.IsNil)
            {
                var assoc = memory.AllocateObject(assocClass, 2);
                var assocObj = memory.GetObject(assoc);
                if (assocObj != null)
                {
                    assocObj.Fields[0] = Intern(key); // key as Symbol
                    assocObj.Fields[1] = value;       // value
                    
                    // Simple linear probing to find empty slot
                    var keySymbol = Intern(key);
                    var size = arrayObj.Fields.Length;
                    var hash = Math.Abs(keySymbol.RawValue) % size;
                    
                    for (int i = 0; i < size; i++)
                    {
                        var index = (hash + i) % size;
                        if (arrayObj.Fields[index].IsNil)
                        {
                            arrayObj.Fields[index] = assoc;
                            
                            // Update tally
                            var tallyOop = dictObj.Fields[0];
                            if (tallyOop.IsSmallInteger)
                            {
                                dictObj.Fields[0] = OOP.FromSmallInteger(tallyOop.SmallIntegerValue + 1);
                            }
                            break;
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Gets the stored DoIt process for REPL/initialization use.
        /// </summary>
        private OOP GetDoItProcess() => doItProcess;
        
        /// <summary>
        /// Stores the DoIt process for future REPL use.
        /// </summary>
        private void StoreDoItProcess(OOP process) => doItProcess = process;
        
        /// <summary>
        /// Runs initialization expressions selectively after bootstrap completes.
        /// This should only be called after the system is fully functional.
        /// </summary>
        /// <param name="classNames">Optional list of class names to initialize. If null, runs all.</param>
        public void RunPostBootstrapInitializations(HashSet<string>? classNames = null)
        {
            if (!bootstrapComplete)
            {
                throw new InvalidOperationException("Cannot run initializations before bootstrap is complete");
            }

            var manifest = lastLoadedManifest;
            if (manifest == null)
            {
                logger.LogWarning("No manifest available for initialization");
                return;
            }

            // Filter initialization expressions if class names provided
            var expressions = manifest.InitializationExpressions;
            if (expressions == null || expressions.Count == 0)
            {
                logger.LogDebug("No initialization expressions available");
                return;
            }

            if (classNames != null && classNames.Count > 0)
            {
                expressions = expressions
                    .Where(e => e.Receiver != null && classNames.Contains(e.Receiver))
                    .ToList();
                logger.LogInformation("Running initialization for: {Classes}", string.Join(", ", classNames));
            }

            ExecuteInitializationExpressions(manifest, expressions);
        }

        /// <summary>
        /// Executes initialization expressions from the loaded system image.
        /// </summary>
        private void ExecuteInitializationExpressions(BootstrapManifest manifest, List<InitializationExpression>? expressions = null)
        {
            logger.LogInformation("Executing initialization expressions...");
            
            // Use provided expressions or fall back to all expressions in manifest
            var toExecute = expressions ?? manifest.InitializationExpressions;
            
            if (toExecute == null || toExecute.Count == 0)
            {
                logger.LogDebug("No initialization expressions to execute");
                return;
            }
            
            logger.LogInformation("Found {Count} initialization expressions", toExecute.Count);
            
            // Create the initialization expression executor with configuration
            var executor = new InitializationExpressionExecutor(
                NullLogger<InitializationExpressionExecutor>.Instance,
                memory, 
                this, 
                classBuilder, 
                this, // ISymbolTable
                classBuilder.GetAllClasses(),
                initializationConfig);
            
            // Check if we have an existing process from REPL setup
            var existingProcess = GetDoItProcess();
            
            // Execute the initialization expressions with proper hierarchy ordering
            var process = executor.ExecuteInitializationExpressions(
                toExecute.ToList(), 
                existingProcess);
            
            // Store the process for potential REPL use
            if (existingProcess.IsNil && !process.IsNil)
            {
                StoreDoItProcess(process);
            }
            
            logger.LogInformation("Initialization expressions completed");
        }
        
        /// <summary>
        /// Looks up a global name in the Smalltalk system dictionary.
        /// </summary>
        private OOP LookupGlobal(string name)
        {
            var smalltalkDict = GetSpecialObject(SpecialObjects.SystemDictionaryInstance);
            if (smalltalkDict.IsNil)
            {
                return OOP.Nil;
            }
            
            var dictObj = memory.GetObject(smalltalkDict);
            if (dictObj == null || dictObj.Fields.Length <= 1)
            {
                return OOP.Nil;
            }
            
            // Create symbol for the name
            var nameSymbol = InternSymbol(name);
            
            // Find the dictionary's array (usually at field index 1)
            if (dictObj.Fields[1] is OOP arrayOop && !arrayOop.IsNil)
            {
                var arrayObj = memory.GetObject(arrayOop);
                if (arrayObj != null)
                {
                    // Search for the name in the dictionary
                    int capacity = arrayObj.Fields.Length / 2;
                    for (int i = 0; i < capacity; i++)
                    {
                        int keyIndex = i * 2;
                        int valueIndex = keyIndex + 1;
                        
                        if (arrayObj.Fields[keyIndex] == nameSymbol)
                        {
                            return arrayObj.Fields[valueIndex];
                        }
                    }
                }
            }
            
            return OOP.Nil;
        }
        
        /// <summary>
        /// Prepares system for execution by creating an initial process.
        /// Does NOT execute any bytecode - that will be done by initialization expressions.
        /// </summary>
        private void StartExecution()
        {
            logger.LogInformation("Preparing system for execution...");
            
            try
            {
                // Create the initial process
                var initialProcess = CreateInitialProcess();
                if (initialProcess.IsNil)
                {
                    logger.LogWarning("Could not create initial process (Process class may not be loaded)");
                    return;
                }
                
                // Set as the active process
                // Note: We don't create a startup context here - the initialization
                // expressions will create their own contexts as needed
                processScheduler.SetActiveProcess(initialProcess);
                
                // Update the ProcessorScheduler's activeProcess field
                var schedulerOop = GetSpecialObject(SpecialObjects.ProcessorScheduler);
                if (!schedulerOop.IsNil)
                {
                    var schedulerObj = memory.GetObject(schedulerOop);
                    if (schedulerObj != null && schedulerObj.Fields.Length >= 2)
                    {
                        schedulerObj.Fields[1] = initialProcess; // activeProcess field
                    }
                }
                
                logger.LogInformation("Initial process created and activated");
                
                // DO NOT execute any startup bytecode here!
                // The initialization expressions will handle all execution.
                // This method just sets up the process infrastructure.
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during startup preparation");
            }
        }
        
        /// <summary>
        /// Creates the initial Process object.
        /// </summary>
        private OOP CreateInitialProcess()
        {
            var processClass = classBuilder.GetClass("Process");
            if (processClass.IsNil)
            {
                return OOP.Nil;
            }
            
            // Process inherits from Link which has 1 field (nextLink)
            // Process adds 3 fields: suspendedContext, priority, myList
            // Total: 4 fields
            var process = memory.AllocateObject(processClass, 4);
            var processObj = memory.GetObject(process);
            
            if (processObj != null)
            {
                // Initialize all fields to nil
                for (int i = 0; i < processObj.Fields.Length; i++)
                {
                    processObj.Fields[i] = OOP.Nil;
                }
                
                // Add to ProcessList for GC tracking
                AddProcessToGlobalList(process);
            }
            
            return process;
        }
        
        /// <summary>
        /// Adds a process to the global ProcessList for GC tracking.
        /// </summary>
        private void AddProcessToGlobalList(OOP process)
        {
            var processList = GetSpecialObject(SpecialObjects.ProcessList);
            if (processList.IsNil)
            {
                if (DebugOptions.EnableDebugOutput)
                {
                    logger.LogTrace("ProcessList not found in special objects");
                }
                return;
            }
            
            var processListObj = memory.GetObject(processList);
            if (processListObj == null)
                return;
            
            var processObj = memory.GetObject(process);
            if (processObj == null)
                return;
            
            // LinkedList fields: firstLink, lastLink
            var firstLink = processListObj.Fields[0];
            var lastLink = processListObj.Fields[1];
            
            if (firstLink.IsNil)
            {
                // First process in the list
                processListObj.Fields[0] = process; // firstLink
                processListObj.Fields[1] = process; // lastLink
                processObj.Fields[0] = OOP.Nil;     // nextLink
            }
            else
            {
                // Add to end of list
                var lastProcessObj = memory.GetObject(lastLink);
                if (lastProcessObj != null)
                {
                    lastProcessObj.Fields[0] = process; // Set nextLink of last process
                }
                processListObj.Fields[1] = process;     // Update lastLink
                processObj.Fields[0] = OOP.Nil;         // nextLink of new process
            }
            
            // Set the process's myList to indicate it's in the ProcessList
            processObj.Fields[3] = processList;
            
            if (DebugOptions.EnableDebugOutput)
            {
                logger.LogTrace("Added process {Process} to global ProcessList", process);
            }
        }
        
        /// <summary>
        /// Creates the startup method context that will execute system initialization.
        /// </summary>
        private MethodContext? CreateStartupContext()
        {
            // Create a synthetic startup method that will initialize the system
            var startupMethod = CreateStartupMethod();
            if (startupMethod.IsNil)
            {
                return null;
            }
            
            // The receiver will be the Smalltalk system dictionary
            var receiver = GetSpecialObject(SpecialObjects.SystemDictionaryInstance);
            if (receiver.IsNil)
            {
                // Fall back to nil as receiver
                receiver = OOP.Nil;
            }
            
            // Create context with no arguments using pool
            var context = contextPool.Rent(
                sender: null, // No sender, this is the top-level context
                receiver: receiver,
                method: startupMethod,
                arguments: [],
                tempCount: 0,
                stackSize: 16
            );
            
            return context;
        }
        
        /// <summary>
        /// Creates a synthetic CompiledMethod for system startup.
        /// </summary>
        private OOP CreateStartupMethod()
        {
            var compiledMethodClass = classBuilder.GetClass("CompiledMethod");
            if (compiledMethodClass.IsNil)
            {
                return OOP.Nil;
            }
            
            // Create a minimal startup method that just returns nil immediately
            // This allows the system to start without trying to execute complex operations
            
            // Create the method object with 4 fields: selector, bytecode, literals, header
            var method = memory.AllocateObject(compiledMethodClass, 4);
            var methodObj = memory.GetObject(method);
            
            if (methodObj == null)
            {
                return OOP.Nil;
            }
            
            // Create method header
            // Format: primitive (bits 1-8), argCount (bits 9-13), tempCount (bits 14-18), stackSize (bits 19-24)
            int primitiveIndex = 0; // No primitive
            int argCount = 0;       // No arguments
            int tempCount = 0;      // No temporaries
            int stackSize = 2;      // Minimal stack
            
            int header = (primitiveIndex << 1) | (argCount << 9) | (tempCount << 14) | (stackSize << 19);
            
            // Field 0: selector (use nil for startup method)
            methodObj.Fields[0] = OOP.Nil;
            
            // Field 3: header (moved to correct position)
            methodObj.Fields[3] = OOP.FromSmallInteger(header);
            
            // Create bytecode array
            var byteArrayClass = classBuilder.GetClass("ByteArray");
            if (!byteArrayClass.IsNil)
            {
                // Minimal bytecode sequence:
                // Push nil and return it
                byte[] bytecodes = [
                    115, // Push nil (special constant)
                    124  // Return top
                ];
                
                var bytecodeArray = memory.AllocateByteArray(byteArrayClass, bytecodes.Length);
                var bytecodeObj = memory.GetObject(bytecodeArray);
                if (bytecodeObj != null)
                {
                    for (int i = 0; i < bytecodes.Length; i++)
                    {
                        bytecodeObj.Fields[i] = OOP.FromSmallInteger(bytecodes[i]);
                    }
                }
                // Field 1: bytecode
                methodObj.Fields[1] = bytecodeArray;
                
                // Create empty literals array (no literals needed)
                var arrayClass = classBuilder.GetClass("Array");
                if (!arrayClass.IsNil)
                {
                    var literalsArray = memory.AllocateWordArray(arrayClass, 0);
                    // Field 2: literals
                methodObj.Fields[2] = literalsArray;
                }
                else
                {
                    methodObj.Fields[2] = OOP.Nil;
                }
            }
            else
            {
                // No ByteArray class, create nil fields
                methodObj.Fields[1] = OOP.Nil;
                methodObj.Fields[2] = OOP.Nil;
            }
            
            logger.LogDebug("Created minimal startup method (push nil, return)");
            return method;
        }
        
        /// <summary>
        /// Executes the startup context to initialize the system.
        /// </summary>
        private void ExecuteStartup(MethodContext startupContext)
        {
            try
            {
                // Set as active context and execute
                activeContext = startupContext;
                ExecuteBytecodes(startupContext);
                
                logger.LogInformation("Startup execution completed");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during startup execution");
                // Don't rethrow - allow the system to continue even if startup fails
            }
        }
        
        /// <summary>
        /// Calls SystemDictionary>>install to set up core system state.
        /// This establishes critical system infrastructure like SmallInteger ranges.
        /// </summary>
        private void CallSystemInstall()
        {
            try
            {
                // Get the SystemDictionary INSTANCE (not the class!)
                // The global Smalltalk dictionary is stored as a special object
                var smalltalkDict = GetSpecialObject(SpecialObjects.SystemDictionaryInstance);
                if (smalltalkDict.IsNil)
                {
                    logger.LogWarning("Smalltalk global dictionary not found - skipping install");
                    return;
                }
                
                // Get the install selector
                var installSelector = Intern("install");
                if (installSelector.IsNil)
                {
                    logger.LogWarning("Could not create 'install' selector");
                    return;
                }
                
                // Send the install message to the SystemDictionary INSTANCE (not the class!)
                logger.LogInformation("Sending install message to SystemDictionary instance (Smalltalk global)...");
                
                // Temporarily set executingInitialization to false to avoid throwing on MNU
                var wasExecutingInit = executingInitialization;
                executingInitialization = false;
                
                try
                {
                    Send(smalltalkDict, installSelector, []);
                    logger.LogInformation("SystemDictionary>>install completed successfully");
                }
                catch (Exception ex)
                {
                    // Handle errors gracefully - UI might not be available yet
                    logger.LogWarning(ex, "SystemDictionary>>install failed - This is expected if UI-related code is not available");
                }
                finally
                {
                    executingInitialization = wasExecutingInit;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error calling SystemDictionary>>install");
                // Continue - the system may still work without this initialization
            }
        }
        
        // Removed ExecuteInitializationsWithRetry and ExecuteSingleInitialization
        // All initialization expressions should be executed via InitializationExpressionExecutor
        // which properly handles bytecode execution

        #endregion
    }
}