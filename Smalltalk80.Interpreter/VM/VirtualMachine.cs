using System;
using System.Collections.Generic;
using System.IO;
using Smalltalk80.Interpreter.Bootstrap;
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
    public sealed class VirtualMachine : IExecutionContext, IPrimitiveContext, Bootstrap.ISymbolTable
    {
        private readonly ObjectMemory memory;
        private readonly PrimitiveTable primitiveTable;
        private readonly GarbageCollector gc;
        private readonly ClassBuilder classBuilder;
        private readonly BytecodeDispatcher bytecodeDispatcher;
        private readonly MessageDispatcher messageDispatcher;
        private readonly ProcessScheduler processScheduler;
        private readonly OOP[] specialObjectsArray;
        private readonly Dictionary<string, OOP> symbolTable;
        private readonly Stack<MethodContext> executionStack;
        
        private MethodContext? activeContext;
        private bool bootstrapComplete = false;
        private int recursionDepth = 0;
        private const int MaxRecursionDepth = 1000;
        private bool schedulerEnabled = false;

        /// <summary>
        /// Creates a new virtual machine instance.
        /// </summary>
        /// <param name="initialMemorySize">Initial object memory size. Default is 262144 for complete system.</param>
        public VirtualMachine(int initialMemorySize = 262144)
        {
            memory = new ObjectMemory(initialMemorySize);
            primitiveTable = new PrimitiveTable();
            gc = new GarbageCollector(memory);
            classBuilder = new ClassBuilder(memory);
            bytecodeDispatcher = new BytecodeDispatcher(this);
            messageDispatcher = new MessageDispatcher(memory, primitiveTable, this);
            processScheduler = new ProcessScheduler(this, memory);
            specialObjectsArray = new OOP[SpecialObjects.Count];
            symbolTable = new Dictionary<string, OOP>();
            executionStack = new Stack<MethodContext>();
            
            // Initialize special objects array with nil
            for (int i = 0; i < specialObjectsArray.Length; i++)
            {
                specialObjectsArray[i] = OOP.Nil;
            }
            
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
        /// Executes the bootstrap process.
        /// </summary>
        public void Bootstrap()
        {
            Console.WriteLine("[VM] Starting bootstrap process...");
            
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
            Console.WriteLine("[VM] Bootstrap complete!");
            
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
            Console.WriteLine($"[VM] Loading bootstrap from JSON: {bootstrapPath}");
            
            // Create the image loader 
            // We'll pass GetSpecialObject later after special objects are created
            var imageLoader = new ImageLoader(memory, classBuilder, this);
            
            // First, just load and create the classes (without installing methods)
            var manifest = imageLoader.LoadManifestFromFile(bootstrapPath);
            imageLoader.ProcessClasses(manifest);
            
            // Create special objects now that classes exist
            CreateSpecialObjects();
            
            // NOW create a new MethodInstaller with access to special objects
            // and install the methods
            var methodInstaller = new MethodInstaller(memory, classBuilder, this, GetSpecialObject);
            methodInstaller.InstallMethods(manifest);
            
            // Create initial symbol table
            CreateInitialSymbolTable();
            
            // Create system dictionary
            CreateSystemDictionary();
            
            bootstrapComplete = true;
            Console.WriteLine($"[VM] Bootstrap complete! Loaded {manifest.Classes.Count} classes");
            
            // Initialize message dispatcher with common selectors
            messageDispatcher.Initialize();
            
            // Get load statistics
            var stats = imageLoader.GetStatistics();
            Console.WriteLine($"[VM] Load statistics: {stats}");
            
            // Run garbage collection to verify everything is reachable
            RunGarbageCollection();
            
            // Start execution with an initial process
            StartExecution();
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
            
            Console.WriteLine($"[VM] Loading extended manifests from: {manifestDirectory}");
            
            // Create the image loader with getSpecialObject function
            var imageLoader = new ImageLoader(memory, classBuilder, this, GetSpecialObject);
            
            // Load all manifests from directory
            imageLoader.LoadManifestsFromDirectory(manifestDirectory);
            
            // Get load statistics
            var stats = imageLoader.GetStatistics();
            Console.WriteLine($"[VM] Extended load statistics: {stats}");
        }

        private void CreateSpecialObjects()
        {
            Console.WriteLine("[VM] Creating special objects...");
            
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
                    var linkedListClass = classBuilder.GetClass("LinkedList");
                    
                    if (!arrayClass.IsNil && !linkedListClass.IsNil)
                    {
                        // Create array of 100 priority queues
                        var priorityListsArray = memory.AllocateWordArray(arrayClass, 100);
                        var priorityListsObj = memory.GetObject(priorityListsArray);
                        
                        if (priorityListsObj != null)
                        {
                            // Initialize each priority queue as a LinkedList
                            for (int i = 0; i < 100; i++)
                            {
                                var list = memory.AllocateObject(linkedListClass, 2);
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
            Console.WriteLine("[VM] Creating initial symbol table...");
            
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
            Console.WriteLine("[VM] Creating system dictionary...");
            
            var systemDictClass = classBuilder.GetClass("SystemDictionary");
            if (!systemDictClass.IsNil)
            {
                var systemDict = memory.AllocateObject(systemDictClass, 2);
                specialObjectsArray[SpecialObjects.SystemDictionaryInstance] = systemDict;
                
                // Initialize and register all classes
                RegisterAllClasses(systemDict);
            }
        }

        private void RegisterAllClasses(OOP systemDict)
        {
            var systemDictObj = memory.GetObject(systemDict);
            if (systemDictObj == null) return;
            
            // Initialize dictionary with appropriate capacity
            var arrayClass = classBuilder.GetClass("Array");
            if (arrayClass.IsNil) return;
            
            var allClasses = classBuilder.GetAllClasses();
            int capacity = NextPowerOfTwo(allClasses.Count * 3); // Extra space for hash efficiency
            
            // Create the dictionary's internal array
            var dictArray = memory.AllocateWordArray(arrayClass, capacity * 2); // key-value pairs
            var dictArrayObj = memory.GetObject(dictArray);
            if (dictArrayObj == null) return;
            
            // Initialize all slots to nil
            for (int i = 0; i < dictArrayObj.Fields.Length; i++)
            {
                dictArrayObj.Fields[i] = OOP.Nil;
            }
            
            // Set dictionary fields (tally and array)
            systemDictObj.Fields[0] = OOP.FromSmallInteger(0); // tally
            systemDictObj.Fields[1] = dictArray; // array
            
            // Register all classes with Symbol keys
            int registeredCount = 0;
            foreach (var kvp in allClasses)
            {
                // Class name must be a Symbol
                var classNameSymbol = Intern(kvp.Key);
                var classOop = kvp.Value;
                
                // Simple hash table insertion with linear probing
                var hash = Math.Abs(classNameSymbol.RawValue) % capacity;
                
                for (int i = 0; i < capacity; i++)
                {
                    var index = (hash + i) % capacity;
                    var keyIndex = index * 2;
                    var valueIndex = keyIndex + 1;
                    
                    if (dictArrayObj.Fields[keyIndex].IsNil)
                    {
                        dictArrayObj.Fields[keyIndex] = classNameSymbol;
                        dictArrayObj.Fields[valueIndex] = classOop;
                        registeredCount++;
                        Console.WriteLine($"  Registered class: {kvp.Key} as Symbol {classNameSymbol}");
                        break;
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
            ControlPrimitives.RegisterAll(primitiveTable);
            ProcessPrimitives.RegisterAll(primitiveTable);
            StringPrimitives.RegisterAll(primitiveTable);
            SystemPrimitives.RegisterAll(primitiveTable);
            IOPrimitives.RegisterAll(primitiveTable);
            // Note: ComparisonPrimitives are already registered by ArithmeticPrimitives
            // Note: Some primitives may be registered by multiple classes, last registration wins
        }

        /// <summary>
        /// Runs garbage collection.
        /// </summary>
        public void RunGarbageCollection()
        {
            Console.WriteLine("[VM] Running garbage collection...");
            var stats = gc.Collect(specialObjectsArray);
            Console.WriteLine($"[VM] GC complete: {stats}");
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

        public void Send(OOP receiver, OOP selector, OOP[] arguments)
        {
            // Check recursion depth
            if (++recursionDepth > MaxRecursionDepth)
            {
                recursionDepth--;
                throw new InvalidOperationException($"Maximum recursion depth ({MaxRecursionDepth}) exceeded");
            }
            
            try
            {
                // Look up method in receiver's class
                var (method, methodClass) = messageDispatcher.Dispatch(receiver, selector, arguments);
                
                if (method.IsNil)
                {
                    // Method not found - send doesNotUnderstand:
                    HandleDoesNotUnderstand(receiver, selector, arguments);
                    return;
                }
                
                // Execute the method
                ExecuteMethod(receiver, method, methodClass, arguments);
            }
            finally
            {
                recursionDepth--;
            }
        }

        public void SuperSend(OOP receiver, OOP selector, OOP[] arguments)
        {
            // Check recursion depth
            if (++recursionDepth > MaxRecursionDepth)
            {
                recursionDepth--;
                throw new InvalidOperationException($"Maximum recursion depth ({MaxRecursionDepth}) exceeded");
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
                
                var classObj = memory.GetObject(methodClass);
                if (classObj == null || classObj.Fields.Length == 0)
                {
                    throw new InvalidOperationException("Invalid class object for super send");
                }
                
                var superclass = classObj.Fields[0]; // Field 0 is superclass in Behavior
                
                // Look up method starting from superclass
                var (method, actualMethodClass) = messageDispatcher.Dispatch(receiver, selector, arguments, superclass);
                
                if (method.IsNil)
                {
                    // Method not found - send doesNotUnderstand:
                    HandleDoesNotUnderstand(receiver, selector, arguments);
                    return;
                }
                
                // Execute the method
                ExecuteMethod(receiver, method, actualMethodClass, arguments);
            }
            finally
            {
                recursionDepth--;
            }
        }

        public OOP InternSymbol(string value) => Intern(value);

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
            Send(receiver, selector, arguments);
            // Would return the result after execution
            return OOP.Nil;
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
            
            var headerOop = methodObj.Fields[0];
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
                    // Primitive succeeded - push result and return
                    if (activeContext != null)
                    {
                        activeContext.Stack.Push(result.Value);
                    }
                    return;
                }
                // Primitive failed - continue with method execution
            }
            
            // Create new method context
            var newContext = new MethodContext(
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
            
            try
            {
                // Execute bytecodes
                ExecuteBytecodes(newContext);
            }
            finally
            {
                // Pop context from stack
                if (executionStack.Count > 0)
                    executionStack.Pop();
                activeContext = previousContext;
            }
        }
        
        private void ExecuteBytecodes(MethodContext context)
        {
            var methodObj = memory.GetObject(context.Method);
            if (methodObj == null || methodObj.Fields.Length < 2)
                return;
            
            var bytecodeArrayOop = methodObj.Fields[1];
            var bytecodeArray = memory.GetObject(bytecodeArrayOop);
            if (bytecodeArray == null)
                return;
            
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
                
                // Execute bytecode - returns false on return instructions
                if (!bytecodeDispatcher.ExecuteBytecode(context, bytecode))
                    break;
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
                Console.WriteLine($"[VM] Process switch failed: {ex.Message}");
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
                Console.WriteLine($"[VM] Process suspend failed: {ex.Message}");
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
                Console.WriteLine($"[VM] Process resume failed: {ex.Message}");
            }
        }
        
        private void HandleDoesNotUnderstand(OOP receiver, OOP selector, OOP[] arguments)
        {
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
                    var methodDict = classObj.Fields[1];
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
            
            var arrayOop = dictObj.Fields[1];
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
        /// Starts system execution by creating an initial process and context.
        /// </summary>
        private void StartExecution()
        {
            Console.WriteLine("[VM] Starting system execution...");
            
            try
            {
                // Create the initial process
                var initialProcess = CreateInitialProcess();
                if (initialProcess.IsNil)
                {
                    Console.WriteLine("[VM] Warning: Could not create initial process (Process class may not be loaded)");
                    return;
                }
                
                // Create the startup context that will execute system initialization
                var startupContext = CreateStartupContext();
                if (startupContext == null)
                {
                    Console.WriteLine("[VM] Warning: Could not create startup context");
                    return;
                }
                
                // Set the process's suspended context to our startup context
                var processObj = memory.GetObject(initialProcess);
                if (processObj != null && processObj.Fields.Length >= 3)
                {
                    // Process has fields: suspendedContext, priority, myList (from superclass Link)
                    // Actually Link has nextLink field, so Process fields are at indices 1, 2, 3
                    processObj.Fields[1] = CreateContextObject(startupContext); // suspendedContext
                    processObj.Fields[2] = OOP.FromSmallInteger(50); // priority (normal user priority)
                    processObj.Fields[3] = OOP.Nil; // myList (not on any list initially)
                }
                
                // Set as the active process
                activeContext = startupContext;
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
                
                Console.WriteLine("[VM] Initial process created and activated");
                
                // Begin executing the startup context
                Console.WriteLine("[VM] Beginning bytecode execution...");
                ExecuteStartup(startupContext);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VM] Error during startup: {ex.Message}");
                Console.WriteLine($"[VM] Stack trace: {ex.StackTrace}");
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
            }
            
            return process;
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
            
            // Create context with no arguments
            var context = new MethodContext(
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
            
            // Create a simple startup method that:
            // 1. Pushes a greeting string
            // 2. Sends #print to it
            // 3. Returns nil
            
            // Create the method object with 3 fields: header, bytecode, literals
            var method = memory.AllocateObject(compiledMethodClass, 3);
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
            int stackSize = 4;      // Small stack needed
            
            int header = (primitiveIndex << 1) | (argCount << 9) | (tempCount << 14) | (stackSize << 19);
            methodObj.Fields[0] = OOP.FromSmallInteger(header);
            
            // Create bytecode array
            var byteArrayClass = classBuilder.GetClass("ByteArray");
            if (!byteArrayClass.IsNil)
            {
                // Simple bytecode sequence:
                // Push literal 0 (the string "Smalltalk system initialized")
                // Send selector #print (literal 1)
                // Push nil
                // Return top
                byte[] bytecodes = [
                    32,  // Push literal constant 0
                    209, // Send literal selector 1 with 0 arguments (192 + 17)
                    115, // Push nil
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
                methodObj.Fields[1] = bytecodeArray;
                
                // Create literals array
                var arrayClass = classBuilder.GetClass("Array");
                if (!arrayClass.IsNil)
                {
                    var literalsArray = memory.AllocateWordArray(arrayClass, 2);
                    var literalsObj = memory.GetObject(literalsArray);
                    if (literalsObj != null)
                    {
                        // Literal 0: String "Smalltalk system initialized"
                        var stringClass = classBuilder.GetClass("String");
                        if (!stringClass.IsNil)
                        {
                            var message = "Smalltalk-80 system initialized";
                            var stringObj = memory.AllocateByteArray(stringClass, message.Length);
                            var strObj = memory.GetObject(stringObj);
                            if (strObj != null)
                            {
                                for (int i = 0; i < message.Length; i++)
                                {
                                    strObj.Fields[i] = OOP.FromSmallInteger(message[i]);
                                }
                            }
                            literalsObj.Fields[0] = stringObj;
                        }
                        else
                        {
                            literalsObj.Fields[0] = OOP.Nil;
                        }
                        
                        // Literal 1: Symbol #print (or #printString)
                        literalsObj.Fields[1] = Intern("printString");
                    }
                    methodObj.Fields[2] = literalsArray;
                }
                else
                {
                    methodObj.Fields[2] = OOP.Nil;
                }
            }
            else
            {
                // No ByteArray class, create empty bytecode
                methodObj.Fields[1] = OOP.Nil;
                methodObj.Fields[2] = OOP.Nil;
            }
            
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
                
                Console.WriteLine("[VM] Startup execution completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VM] Error during startup execution: {ex.Message}");
                // Don't rethrow - allow the system to continue even if startup fails
            }
        }

        #endregion
    }
}