using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    public sealed partial class VirtualMachine : IExecutionContext, IPrimitiveContext, Bootstrap.ISymbolTable, IFieldAccessor
    {
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
        /// Gets a class by name from the loaded system.
        /// </summary>
        /// <param name="className">The name of the class to retrieve.</param>
        /// <returns>The OOP of the class, or OOP.Nil if not found.</returns>
        public OOP GetClassByName(string className) => classBuilder.GetClass(className);
        
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
            
            // Load the manifest file first
            var manifest = LoadManifestFromFile(bootstrapPath);
            
            // Create the classes using ClassBuilder directly
            ProcessBootstrapClasses(manifest);
            
            // Create special objects now that classes exist
            CreateSpecialObjects();
            
            // NOW create a single MethodInstaller with access to special objects
            // and install the methods
            var methodInstaller = new MethodInstaller(memory, classBuilder, this, this, GetSpecialObject);
            methodInstaller.InstallMethods(manifest);
            
            // Create initial symbol table
            CreateInitialSymbolTable();
            
            // Create system dictionary
            CreateSystemDictionary();
            
            bootstrapComplete = true;
            Console.WriteLine($"[VM] Bootstrap complete! Loaded {manifest.Classes.Count} classes");
            
            // Initialize message dispatcher with common selectors
            messageDispatcher.Initialize();
            
            Console.WriteLine($"[VM] Load statistics: Classes: {manifest.Classes.Count}, Methods: {methodInstaller.GetAllCachedMethods().Count}");
            
            // Run garbage collection to verify everything is reachable
            RunGarbageCollection();
            
            // Execute initialization expressions if present
            // This will initialize Transcript and other globals through Smalltalk code
            // DISABLED: ExecuteInitializationExpressions(manifest);
            
            // Start execution with an initial process - but only for complete system
            // For bootstrap only, we don't create any temporary startup objects
            if (manifest.Classes.Count > 100)  // Complete system has 223 classes, bootstrap has 85
            {
                StartExecution();
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
            
            Console.WriteLine($"[VM] Loading extended manifests from: {manifestDirectory}");
            // TODO: Implement loading of extended manifests
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
                var allClasses = classBuilder.GetAllClasses();
                
                // Calculate capacity: need 2 slots per entry (key, value) plus buffer
                // We need space for classes AND metaclasses
                int capacity = allClasses.Count * 8; // 2x entries * 2 (class+metaclass) * 2 for efficiency
                
                // Create SystemDictionary with proper size:
                // 1 field for tally + capacity for indexed/variable portion
                var systemDict = memory.AllocateObject(systemDictClass, 1 + capacity);
                specialObjectsArray[SpecialObjects.SystemDictionaryInstance] = systemDict;
                
                // Initialize and register all classes
                RegisterAllClasses(systemDict);
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
                        Console.WriteLine($"  Registered class: {kvp.Key} as Symbol {classNameSymbol}");
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
                            Console.WriteLine($"  Registered metaclass: {kvp.Key} class as Symbol {metaclassNameSymbol}");
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
                
            obj.Fields[index] = value;
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
                    Console.WriteLine("[VM] Compiler class found, but full evaluation not yet implemented");
                    Console.WriteLine("[VM] Currently only simple arithmetic expressions are supported (e.g., '3 + 4')");
                }
                else
                {
                    Console.WriteLine("[VM] Compiler class not found in loaded system");
                    Console.WriteLine("[VM] Only simple arithmetic expressions are supported (e.g., '3 + 4')");
                }
                
                return OOP.Nil;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VM] Exception during evaluation: {ex.Message}");
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
            
            var bytecodeArrayOop = GetFieldByName(context.Method, "bytecode");
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
            Console.WriteLine("[VM] Processing bootstrap classes through ClassBuilder phases");
            
            // PHASE -1: Build field mappings BEFORE any objects are created
            Console.WriteLine("[VM] Phase -1: Building field mappings from manifest...");
            fieldLookup.RegisterManifest(manifest);
            Console.WriteLine("[VM] Phase -1 complete: Field mappings established for all classes and metaclasses");
            
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
                    Console.WriteLine($"[VM] Set {classDef.ClassVariables.Count} class variables for {classDef.Name}");
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
                    Console.WriteLine($"[VM] Set {classDef.PoolDictionaries.Count} pool dictionaries for {classDef.Name}");
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
        /// Executes initialization expressions from the loaded system image.
        /// </summary>
        private void ExecuteInitializationExpressions(BootstrapManifest manifest)
        {
            Console.WriteLine("[VM] Executing initialization expressions...");
            
            if (manifest.InitializationExpressions == null || manifest.InitializationExpressions.Count == 0)
            {
                Console.WriteLine("[VM] No initialization expressions to execute");
                return;
            }
            
            Console.WriteLine($"[VM] Found {manifest.InitializationExpressions.Count} initialization expressions");
            
            // For now, execute them in order (we can add sorting later if needed)
            var expressions = manifest.InitializationExpressions;
            
            // Track which classes have been initialized to avoid duplicates
            var initializedClasses = new HashSet<string>();
            
            foreach (var expr in expressions)
            {
                try
                {
                    Console.WriteLine($"[VM] Executing: {expr.Source ?? $"{expr.Receiver} {expr.Selector}"}");
                    
                    // Look up the receiver class
                    var receiverOop = classBuilder.GetClass(expr.Receiver);
                    if (receiverOop.IsNil)
                    {
                        // Try to find it as a global in Smalltalk dictionary
                        receiverOop = LookupGlobal(expr.Receiver);
                        if (receiverOop.IsNil)
                        {
                            Console.WriteLine($"[VM] Warning: Cannot find receiver '{expr.Receiver}' for initialization");
                            continue;
                        }
                    }
                    
                    // Create selector symbol
                    var selectorOop = InternSymbol(expr.Selector);
                    
                    // Convert arguments to OOPs
                    var argumentOops = new OOP[expr.Arguments.Count];
                    for (int i = 0; i < expr.Arguments.Count; i++)
                    {
                        // For now, treat arguments as symbol names or literals
                        argumentOops[i] = InternSymbol(expr.Arguments[i]);
                    }
                    
                    // Send the initialization message
                    Send(receiverOop, selectorOop, argumentOops);
                    
                    // Track that this class has been initialized
                    initializedClasses.Add(expr.Receiver);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[VM] Error executing initialization for {expr.Receiver}: {ex.Message}");
                    // Continue with other initializations
                }
            }
            
            Console.WriteLine($"[VM] Completed initialization for {initializedClasses.Count} classes");
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
            
            Console.WriteLine("[VM] Created minimal startup method (push nil, return)");
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