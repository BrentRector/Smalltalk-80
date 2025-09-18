using System;
using System.Collections.Generic;
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
        private readonly OOP[] specialObjectsArray;
        private readonly Dictionary<string, OOP> symbolTable;
        private readonly Stack<MethodContext> executionStack;
        
        private MethodContext? activeContext;
        private bool bootstrapComplete = false;
        private int recursionDepth = 0;
        private const int MaxRecursionDepth = 1000;

        /// <summary>
        /// Creates a new virtual machine instance.
        /// </summary>
        public VirtualMachine(int initialMemorySize = 65536)
        {
            memory = new ObjectMemory(initialMemorySize);
            primitiveTable = new PrimitiveTable();
            gc = new GarbageCollector(memory);
            classBuilder = new ClassBuilder(memory);
            bytecodeDispatcher = new BytecodeDispatcher(this);
            messageDispatcher = new MessageDispatcher(memory, primitiveTable, this);
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
                var arrayClass = classBuilder.GetClass("Array");
                if (!arrayClass.IsNil)
                {
                    var keysArray = memory.AllocateWordArray(arrayClass, 32);
                    var valuesArray = memory.AllocateWordArray(arrayClass, 32);
                    
                    var symbolTableLayout = memory.GetObject(symbolTableObj);
                    if (symbolTableLayout != null)
                    {
                        symbolTableLayout.Fields[0] = OOP.FromSmallInteger(0); // tally
                        symbolTableLayout.Fields[1] = keysArray; // array for dictionary
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
            var allClasses = classBuilder.GetAllClasses();
            foreach (var kvp in allClasses)
            {
                // Register class in system dictionary
                // This would normally use Dictionary>>at:put: but we're bootstrapping
                Console.WriteLine($"  Registered class: {kvp.Key}");
            }
        }

        private void RegisterPrimitives()
        {
            ObjectPrimitives.RegisterAll(primitiveTable);
            // Additional primitive registrations would go here
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
                var byteOop = bytecodeArray.Fields[context.PC++];
                if (!byteOop.IsSmallInteger)
                    break;
                
                byte bytecode = (byte)byteOop.SmallIntegerValue;
                
                // Execute bytecode - returns false on return instructions
                if (!bytecodeDispatcher.ExecuteBytecode(context, bytecode))
                    break;
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
    }
}