using System;
using System.Collections.Generic;
using System.Linq;
using Smalltalk80.Interpreter.Config;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.Bootstrap
{
    /// <summary>
    /// Executes initialization expressions after bootstrap completes.
    /// These are file-scope expressions that need to run to initialize classes.
    /// </summary>
    public sealed class InitializationExpressionExecutor
    {
        private readonly ObjectMemory memory;
        private readonly VirtualMachine vm;
        private readonly ClassBuilder classBuilder;
        private readonly ISymbolTable symbolTable;
        private readonly IReadOnlyDictionary<string, OOP> classRegistry;
        
        /// <summary>
        /// Initializes a new instance of the InitializationExpressionExecutor class.
        /// </summary>
        public InitializationExpressionExecutor(
            ObjectMemory memory,
            VirtualMachine vm,
            ClassBuilder classBuilder,
            ISymbolTable symbolTable,
            IReadOnlyDictionary<string, OOP> classRegistry)
        {
            this.memory = memory ?? throw new ArgumentNullException(nameof(memory));
            this.vm = vm ?? throw new ArgumentNullException(nameof(vm));
            this.classBuilder = classBuilder ?? throw new ArgumentNullException(nameof(classBuilder));
            this.symbolTable = symbolTable ?? throw new ArgumentNullException(nameof(symbolTable));
            this.classRegistry = classRegistry ?? throw new ArgumentNullException(nameof(classRegistry));
        }
        
        /// <summary>
        /// Executes all initialization expressions in dependency order.
        /// </summary>
        /// <param name="expressions">The initialization expressions to execute.</param>
        /// <param name="doItProcess">Optional existing process to reuse for execution.</param>
        /// <returns>The process used for execution (can be reused for REPL).</returns>
        public OOP ExecuteInitializationExpressions(
            List<InitializationExpression> expressions, 
            OOP doItProcess = default)
        {
            if (expressions == null || expressions.Count == 0)
            {
                Console.WriteLine("[InitExpr] No initialization expressions to execute.");
                return doItProcess.IsNil ? CreateDoItProcess() : doItProcess;
            }
            
            Console.WriteLine($"[InitExpr] Executing {expressions.Count} initialization expressions...");
            
            // TEMPORARY: Only execute "Object initialize" to debug infinite recursion
            var objectInitialize = expressions.FirstOrDefault(e => 
                e.Source != null && e.Source.Trim() == "Object initialize");
            
            if (objectInitialize == null)
            {
                Console.WriteLine("[InitExpr] WARNING: 'Object initialize' not found in initialization expressions");
                Console.WriteLine("[InitExpr] Available expressions:");
                foreach (var e in expressions.Take(10))
                {
                    Console.WriteLine($"  - {e.Source}");
                }
                return doItProcess.IsNil ? CreateDoItProcess() : doItProcess;
            }
            
            // Execute all initialization expressions, starting with Object initialize
            var sortedExpressions = new List<InitializationExpression> { objectInitialize };
            sortedExpressions.AddRange(expressions.Where(e => e != objectInitialize));
            
            // Get or create a process for execution
            if (doItProcess.IsNil)
            {
                doItProcess = CreateDoItProcess();
            }
            
            // Execute each expression in order
            int successCount = 0;
            foreach (var expr in sortedExpressions)
            {
                try
                {
                    Console.WriteLine($"[InitExpr] Executing: {expr.Source ?? "Unknown"}");
            if (expr.Bytecode != null && expr.Bytecode.Count > 0)
            {
                Console.WriteLine($"[InitExpr] Bytecode: [{string.Join(", ", expr.Bytecode)}]");
            }
            if (expr.Literals != null && expr.Literals.Count > 0)
            {
                Console.WriteLine($"[InitExpr] Literals: [{string.Join(", ", expr.Literals)}]");
            }
                    ExecuteExpression(expr, doItProcess);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[InitExpr] Failed to execute '{expr.Source}': {ex.Message}");
                    // Continue with other expressions
                }
            }
            
            Console.WriteLine($"[InitExpr] Completed {successCount}/{expressions.Count} initialization expressions.");
            return doItProcess;
        }
        
        /// <summary>
        /// Sorts initialization expressions by class hierarchy.
        /// Superclass initializations run before subclass initializations.
        /// </summary>
        private List<InitializationExpression> SortByClassHierarchy(List<InitializationExpression> expressions)
        {
            // Build a map of class names to their depth in the hierarchy
            var classDepths = new Dictionary<string, int>();
            
            foreach (var expr in expressions)
            {
                if (!string.IsNullOrEmpty(expr.Receiver) && !classDepths.ContainsKey(expr.Receiver))
                {
                    classDepths[expr.Receiver] = GetClassDepth(expr.Receiver);
                }
            }
            
            // Sort by depth (lower depth = closer to Object = run first)
            // Within same depth, maintain original order
            return expressions.OrderBy(e =>
            {
                if (string.IsNullOrEmpty(e.Receiver))
                    return int.MaxValue; // Unknown receivers go last
                    
                return classDepths.TryGetValue(e.Receiver, out var depth) ? depth : int.MaxValue;
            }).ToList();
        }
        
        /// <summary>
        /// Gets the depth of a class in the inheritance hierarchy.
        /// Object has depth 0, its direct subclasses have depth 1, etc.
        /// </summary>
        private int GetClassDepth(string className)
        {
            var classOop = classBuilder.GetClass(className);
            if (classOop.IsNil)
            {
                // Unknown class, put it at the end
                return int.MaxValue;
            }
            
            int depth = 0;
            var current = classOop;
            
            while (!current.IsNil)
            {
                // Get superclass
                var superclassOop = vm.GetFieldBootstrap(current, "Behavior", "superclass");
                
                if (superclassOop.IsNil)
                    break; // Reached Object
                    
                current = superclassOop;
                depth++;
            }
            
            return depth;
        }
        
        /// <summary>
        /// Creates a process suitable for executing initialization expressions.
        /// </summary>
        private OOP CreateDoItProcess()
        {
            // Get necessary classes
            var processClass = classBuilder.GetClass("Process");
            var methodContextClass = classBuilder.GetClass("MethodContext");
            var compiledMethodClass = classBuilder.GetClass("CompiledMethod");
            
            if (processClass.IsNil || methodContextClass.IsNil || compiledMethodClass.IsNil)
            {
                throw new InvalidOperationException("Required classes for process creation not found");
            }
            
            // Create a minimal compiled method for doIt
            var doItMethod = memory.AllocateObject(compiledMethodClass, 4);
            var doItMethodObj = memory.GetObject(doItMethod);
            if (doItMethodObj != null)
            {
                doItMethodObj.Fields[0] = symbolTable.Intern("doIt"); // selector
                doItMethodObj.Fields[1] = CreateByteArray(new byte[] { 0x78 }); // bytecode: returnSelf
                doItMethodObj.Fields[2] = memory.AllocateWordArray(classBuilder.GetClass("Array"), 0); // empty literals
                
                // Create proper method header with stack size
                int primitiveIndex = 0; // No primitive
                int argCount = 0;       // No arguments
                int tempCount = 0;      // No temporaries
                int methodStackSize = 2;      // Minimal stack
                int header = (primitiveIndex << 1) | (argCount << 9) | (tempCount << 14) | (methodStackSize << 19);
                doItMethodObj.Fields[3] = OOP.FromSmallInteger(header);
            }
            
            // Create a MethodContext (not BlockContext!) for the process
            // Blue Book MethodContext fields: sender, pc, stackp, method, receiver, unused, then stack
            const int stackSize = 20;
            var context = memory.AllocateObject(methodContextClass, 6 + stackSize);
            var contextObj = memory.GetObject(context);
            if (contextObj != null)
            {
                // Set up context fields according to Blue Book specification
                contextObj.Fields[0] = OOP.Nil;                    // sender
                contextObj.Fields[1] = OOP.FromSmallInteger(0);    // pc
                contextObj.Fields[2] = OOP.FromSmallInteger(0);    // stackp
                contextObj.Fields[3] = doItMethod;                 // method
                contextObj.Fields[4] = OOP.Nil;                    // receiver (nil for doIt)
                contextObj.Fields[5] = OOP.Nil;                    // unused
                // Fields 6+ are for the stack
            }
            
            // Create the Process with proper field layout
            // Blue Book Process fields: nextLink (from Link), suspendedContext, priority, myList
            var process = memory.AllocateObject(processClass, 4);
            var processObj = memory.GetObject(process);
            if (processObj != null)
            {
                processObj.Fields[0] = OOP.Nil;                    // nextLink (from Link superclass)
                processObj.Fields[1] = context;                    // suspendedContext
                processObj.Fields[2] = OOP.FromSmallInteger(50);   // priority (user level = 50)
                processObj.Fields[3] = OOP.Nil;                    // myList
            }
            
            // Add the process to the ProcessList for GC tracking
            AddProcessToProcessList(process);
            
            return process;
        }
        
        /// <summary>
        /// Adds a process to the global ProcessList for GC tracking.
        /// </summary>
        private void AddProcessToProcessList(OOP process)
        {
            var processList = vm.GetSpecialObject(SpecialObjects.ProcessList);
            if (processList.IsNil)
            {
                if (DebugOptions.EnableDebugOutput)
                {
                    Console.WriteLine("[DEBUG] ProcessList not found in special objects");
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
                Console.WriteLine($"[DEBUG] Added process {process} to ProcessList");
            }
        }
        
        /// <summary>
        /// Executes a single initialization expression.
        /// </summary>
        private void ExecuteExpression(InitializationExpression expr, OOP process)
        {
            // Check if we have bytecode to execute
            if (expr.Bytecode == null || expr.Bytecode.Count == 0)
            {
                Console.WriteLine($"[InitExpr] No bytecode for expression: {expr.Source}");
                return;
            }
            
            // Create a CompiledMethod from the expression
            var compiledMethod = CreateCompiledMethodFromExpression(expr);
            
            // Get the process's suspended context
            var processObj = memory.GetObject(process);
            if (processObj == null)
            {
                Console.WriteLine($"[InitExpr] Process object not found");
                return;
            }
            
            var suspendedContext = processObj.Fields[1]; // suspendedContext field
            var contextObj = memory.GetObject(suspendedContext);
            if (contextObj == null)
            {
                Console.WriteLine($"[InitExpr] Context object not found");
                return;
            }
            
            // Determine the receiver for this expression
            OOP receiverOop = OOP.Nil;
            
            // For expressions like "Object initialize", the receiver should be the Object class itself
            // The bytecode 72 (PushLiteralVariable) will push the class Object onto the stack
            // But we need to set up the context properly for the top-level doIt
            // The receiver for the doIt context itself is nil (workspace/UndefinedObject)
            receiverOop = OOP.Nil;
            
            // Update the context to execute the new method
            // Blue Book MethodContext fields: sender, pc, stackp, method, receiver, unused
            contextObj.Fields[1] = OOP.FromSmallInteger(0);    // Reset pc to 0
            contextObj.Fields[2] = OOP.FromSmallInteger(0);    // Reset stackp to 0
            contextObj.Fields[3] = compiledMethod;             // Set the new method
            contextObj.Fields[4] = receiverOop;                // receiver for the doIt context
            
            // Clear the stack portion (fields 6+)
            for (int i = 6; i < contextObj.Fields.Length; i++)
            {
                contextObj.Fields[i] = OOP.Nil;
            }
            
            // Save current active context if any
            var previousContext = vm.GetActiveContext();
            
            // Save and enable method tracing
            var previousMethodTrace = Config.DebugOptions.EnableMethodTrace;
            Config.DebugOptions.EnableMethodTrace = true;
            
            try
            {
                // Create a C# MethodContext wrapper for execution
                // CRITICAL: For "Object initialize", the receiver of the doIt method is nil
                // The Object class will be pushed by bytecode 72 (PushLiteralVariable)
                // and will become the receiver for the 'initialize' message send
                var context = new Execution.MethodContext(
                    sender: null, // No sender for top-level initialization
                    receiver: receiverOop, // nil for top-level doIt context
                    method: compiledMethod,
                    arguments: Array.Empty<OOP>(), // No arguments for initialization expressions
                    tempCount: expr.NumTemps, // Number of temporaries from expression
                    stackSize: 16 // Standard stack size
                );
                
                // Set the new context as active and execute the bytecode
                vm.SetActiveContext(context);
                
                // Debug logging to trace execution
                Console.WriteLine($"[InitExpr] About to execute bytecode for: {expr.Source}");
                
                // Pretty-print the MethodContext
                Console.WriteLine($"[InitExpr] MethodContext:");
                Console.WriteLine($"  - Receiver: {(context.Receiver.IsNil ? "nil (workspace/doIt context)" : GetClassNameOrValue(context.Receiver))}");
                Console.WriteLine($"  - Method: CompiledMethod for anonymous doIt");
                Console.WriteLine($"  - Sender: {(context.Sender == null ? "nil (top-level)" : "previous context")}");
                Console.WriteLine($"  - PC: {context.PC}");
                Console.WriteLine($"  - Stack: empty (initial)");
                Console.WriteLine($"  - Arguments: {context.Arguments.Length}");
                Console.WriteLine($"  - Temporaries: {context.Temporaries.Length}");
                
                // Log the compiled method structure with resolved values
                var compiledMethodObj = memory.GetObject(compiledMethod);
                if (compiledMethodObj != null && compiledMethodObj.Fields.Length >= 4)
                {
                    Console.WriteLine($"[InitExpr] CompiledMethod structure:");
                    
                    // Selector
                    var selectorStr = compiledMethodObj.Fields[0].IsNil ? "nil (anonymous doIt)" : OopToString(compiledMethodObj.Fields[0]);
                    Console.WriteLine($"  - Selector: {selectorStr}");
                    
                    // Bytecode array
                    var bytecodeArrayOop = compiledMethodObj.Fields[1];
                    var bytecodeArray = memory.GetObject(bytecodeArrayOop);
                    if (bytecodeArray != null)
                    {
                        var bytecodes = new List<int>();
                        for (int i = 0; i < bytecodeArray.Fields.Length; i++)
                        {
                            if (bytecodeArray.Fields[i].IsSmallInteger)
                            {
                                bytecodes.Add(bytecodeArray.Fields[i].SmallIntegerValue);
                            }
                        }
                        Console.WriteLine($"  - Bytecode: [{string.Join(", ", bytecodes)}]");
                    }
                    
                    // Header
                    if (compiledMethodObj.Fields[3].IsSmallInteger)
                    {
                        int header = compiledMethodObj.Fields[3].SmallIntegerValue;
                        int primitiveIndex = (header >> 1) & 0xFF;
                        int argCount = (header >> 9) & 0x1F;
                        int tempCount = (header >> 14) & 0x1F;
                        int stackSize = (header >> 19) & 0x3F;
                        Console.WriteLine($"  - Header: primitive={primitiveIndex}, args={argCount}, temps={tempCount}, stack={stackSize}");
                    }
                    
                    // Literals array with detailed contents
                    var literalsArrayOop = compiledMethodObj.Fields[2];
                    var literalsArray = memory.GetObject(literalsArrayOop);
                    if (literalsArray != null)
                    {
                        Console.WriteLine($"  - Literals: {literalsArray.Fields.Length} entries");
                        for (int i = 0; i < literalsArray.Fields.Length; i++)
                        {
                            var literalOop = literalsArray.Fields[i];
                            var literalObj = memory.GetObject(literalOop);
                            
                            if (literalObj != null)
                            {
                                var associationClass = classBuilder.GetClass("Association");
                                if (!associationClass.IsNil && literalObj.Header.ClassOOP.RawValue == associationClass.RawValue)
                                {
                                    // It's an Association - show detailed key and value
                                    var keyStr = OopToString(literalObj.Fields[0]);
                                    var valueStr = GetClassNameOrValue(literalObj.Fields[1]);
                                    Console.WriteLine($"    [{i}] Association(#{keyStr} -> {valueStr})");
                                }
                                else
                                {
                                    // It's something else (Symbol, String, etc)
                                    var className = GetClassNameForOop(literalOop);
                                    var value = OopToString(literalOop);
                                    Console.WriteLine($"    [{i}] {className}: #{value}");
                                }
                            }
                            else if (literalOop.IsSmallInteger)
                            {
                                Console.WriteLine($"    [{i}] SmallInteger: {literalOop.SmallIntegerValue}");
                            }
                            else if (literalOop.IsNil)
                            {
                                Console.WriteLine($"    [{i}] nil");
                            }
                        }
                    }
                }
                
                vm.ExecuteBytecodes(context);
                
                Console.WriteLine($"[InitExpr] Successfully executed: {expr.Source}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[InitExpr] Error executing '{expr.Source}': {ex.Message}");
                throw;
            }
            finally
            {
                // Restore previous context
                vm.SetActiveContext(previousContext);
                // Restore method trace setting
                Config.DebugOptions.EnableMethodTrace = previousMethodTrace;
            }
        }
        
        /// <summary>
        /// Creates a CompiledMethod from an initialization expression.
        /// </summary>
        private OOP CreateCompiledMethodFromExpression(InitializationExpression expr)
        {
            var compiledMethodClass = classBuilder.GetClass("CompiledMethod");
            if (compiledMethodClass.IsNil)
            {
                throw new InvalidOperationException("CompiledMethod class not found");
            }
            
            // Allocate CompiledMethod object (4 fields)
            var methodOop = memory.AllocateObject(compiledMethodClass, 4);
            var methodObj = memory.GetObject(methodOop);
            
            if (methodObj == null)
            {
                throw new InvalidOperationException("Failed to allocate CompiledMethod");
            }
            
            // Field 0: selector (nil for anonymous top-level doIt)
            methodObj.Fields[0] = OOP.Nil; // Anonymous doIt has no selector
            
            // Field 1: bytecode
            var bytecodeArray = CreateByteArray(expr.Bytecode.Select(b => (byte)b).ToArray());
            methodObj.Fields[1] = bytecodeArray;
            
            // Field 2: literals
            var literalsArray = CreateLiteralsArray(expr.Literals);
            methodObj.Fields[2] = literalsArray;
            
            // Field 3: header (Blue Book method header format)
            // Header format per Blue Book:
            // Bits 0: primitive flag (1 bit) - 0 for no primitive
            // Bits 1-8: primitive index (8 bits) - 0 for no primitive
            // Bits 9-13: number of arguments (5 bits) - 0 for doIt
            // Bits 14-18: number of temporaries (5 bits) - from expr.NumTemps
            // Bits 19-24: stack size (6 bits) - use 16 as default
            // Bits 25-31: unused
            
            int primitiveIndex = 0; // No primitive for doIt
            int argCount = 0; // No arguments for doIt
            int tempCount = expr.NumTemps;
            // Use stack depth from expression if available, otherwise use reasonable default
            int stackSize = expr.StackDepth > 0 ? Math.Max(expr.StackDepth, 2) : 16;
            
            // Encode header according to Blue Book format
            int header = (primitiveIndex << 1) | 
                        (argCount << 9) | 
                        (tempCount << 14) | 
                        (stackSize << 19);
            
            methodObj.Fields[3] = OOP.FromSmallInteger(header);
            
            Console.WriteLine($"[InitExpr] Created CompiledMethod with header: 0x{header:X8}");
            Console.WriteLine($"  - Primitive: {primitiveIndex}");
            Console.WriteLine($"  - Args: {argCount}");
            Console.WriteLine($"  - Temps: {tempCount}");
            Console.WriteLine($"  - Stack size: {stackSize}");
            
            return methodOop;
        }
        
        /// <summary>
        /// Creates a ByteArray from bytes.
        /// </summary>
        private OOP CreateByteArray(byte[] bytes)
        {
            var byteArrayClass = classBuilder.GetClass("ByteArray");
            if (byteArrayClass.IsNil)
            {
                return OOP.Nil;
            }
            
            var byteArrayOop = memory.AllocateByteArray(byteArrayClass, bytes.Length);
            var byteArrayObj = memory.GetObject(byteArrayOop);
            
            if (byteArrayObj != null)
            {
                for (int i = 0; i < bytes.Length && i < byteArrayObj.Fields.Length; i++)
                {
                    byteArrayObj.Fields[i] = OOP.FromSmallInteger(bytes[i]);
                }
            }
            
            return byteArrayOop;
        }
        
        /// <summary>
        /// Creates an Array of literal objects from a list of literal values.
        /// </summary>
        private OOP CreateLiteralsArray(List<object> literals)
        {
            var arrayClass = classBuilder.GetClass("Array");
            if (arrayClass.IsNil)
            {
                return OOP.Nil;
            }
            
            var literalsOop = memory.AllocateWordArray(arrayClass, literals.Count);
            var literalsObj = memory.GetObject(literalsOop);
            
            if (literalsObj != null)
            {
                for (int i = 0; i < literals.Count; i++)
                {
                    literalsObj.Fields[i] = ConvertLiteralToOop(literals[i]);
                }
            }
            
            return literalsOop;
        }
        
        /// <summary>
        /// Converts a JSON literal value to an OOP.
        /// </summary>
        private OOP ConvertLiteralToOop(object literal)
        {
            return literal switch
            {
                null => OOP.Nil,
                string s when s == "nil" => OOP.Nil,
                string s when s == "true" => vm.GetSpecialObject(SpecialObjects.TrueObject),
                string s when s == "false" => vm.GetSpecialObject(SpecialObjects.FalseObject),
                string s when s.StartsWith("Association(key:") => ParseAssociation(s), // Parse Association literal
                string s when s.StartsWith("#") => ConvertSymbolLiteral(s[1..]), // Symbol literal - may need Association
                string s => CreateString(s),
                int i => OOP.FromSmallInteger(i),
                long l => OOP.FromSmallInteger((int)l),
                System.Text.Json.JsonElement je => ConvertJsonElement(je),
                _ => OOP.Nil
            };
        }
        
        /// <summary>
        /// Converts a JsonElement to an OOP.
        /// </summary>
        private OOP ConvertJsonElement(System.Text.Json.JsonElement element)
        {
            return element.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Null => OOP.Nil,
                System.Text.Json.JsonValueKind.True => vm.GetSpecialObject(SpecialObjects.TrueObject),
                System.Text.Json.JsonValueKind.False => vm.GetSpecialObject(SpecialObjects.FalseObject),
                System.Text.Json.JsonValueKind.Number => element.TryGetInt32(out var i)
                    ? OOP.FromSmallInteger(i)
                    : OOP.Nil,
                System.Text.Json.JsonValueKind.String => ConvertLiteralToOop(element.GetString() ?? string.Empty),
                _ => OOP.Nil
            };
        }
        
        /// <summary>
        /// Creates a String object from a C# string.
        /// </summary>
        private OOP CreateString(string value)
        {
            var stringClass = classBuilder.GetClass("String");
            if (stringClass.IsNil) return OOP.Nil;
            
            var bytes = System.Text.Encoding.UTF8.GetBytes(value);
            var stringOop = memory.AllocateByteArray(stringClass, bytes.Length);
            var stringObj = memory.GetObject(stringOop);
            
            if (stringObj != null)
            {
                for (int i = 0; i < bytes.Length && i < stringObj.Fields.Length; i++)
                {
                    stringObj.Fields[i] = OOP.FromSmallInteger(bytes[i]);
                }
            }
            
            return stringOop;
        }
        
        /// <summary>
        /// Converts a symbol literal to either a Symbol or an Association for global variables.
        /// </summary>
        /// <param name="symbolName">The symbol name without the # prefix.</param>
        /// <returns>Either a Symbol OOP or an Association OOP for global variables.</returns>
        private OOP ConvertSymbolLiteral(string symbolName)
        {
            // First check if this is a known class name
            var classOop = classBuilder.GetClass(symbolName);
            if (!classOop.IsNil)
            {
                // This is a global class reference - create an Association
                return CreateAssociation(symbolName, classOop);
            }
            
            // Check if it's in the classRegistry passed to the constructor
            if (classRegistry.TryGetValue(symbolName, out var registryClassOop))
            {
                // This is a global class reference from registry - create an Association
                return CreateAssociation(symbolName, registryClassOop);
            }
            
            // Check if this is another global in the system dictionary
            // For now, we'll check known special globals
            var globalValue = GetGlobalValue(symbolName);
            if (!globalValue.IsNil)
            {
                // This is a global variable - create an Association
                return CreateAssociation(symbolName, globalValue);
            }
            
            // Not a global - just return a plain symbol
            return symbolTable.Intern(symbolName);
        }
        
        /// <summary>
        /// Creates an Association object with key and value fields.
        /// </summary>
        /// <param name="key">The key as a string (will be interned as a symbol).</param>
        /// <param name="value">The value OOP.</param>
        /// <returns>An OOP pointing to the created Association.</returns>
        private OOP CreateAssociation(string key, OOP value)
        {
            var associationClass = classBuilder.GetClass("Association");
            if (associationClass.IsNil)
            {
                // Fallback to just returning the symbol if Association class not found
                return symbolTable.Intern(key);
            }
            
            // Create an Association with 2 instance variables: key and value
            var associationOop = memory.AllocateObject(associationClass, 2);
            var associationObj = memory.GetObject(associationOop);
            
            if (associationObj != null)
            {
                // Field 0: key (as Symbol)
                associationObj.Fields[0] = symbolTable.Intern(key);
                // Field 1: value
                associationObj.Fields[1] = value;
            }
            
            return associationOop;
        }
        
        /// <summary>
        /// Gets the value of a global variable from the system dictionary.
        /// </summary>
        /// <param name="name">The name of the global variable.</param>
        /// <returns>The value of the global or OOP.Nil if not found.</returns>
        private OOP GetGlobalValue(string name)
        {
            // Check for special known globals
            switch (name)
            {
                case "Smalltalk":
                    return vm.GetSpecialObject(SpecialObjects.SystemDictionaryInstance);
                case "Processor":
                    return vm.GetSpecialObject(SpecialObjects.ProcessorScheduler);
                case "nil":
                    return OOP.Nil;
                case "true":
                    return vm.GetSpecialObject(SpecialObjects.TrueObject);
                case "false":
                    return vm.GetSpecialObject(SpecialObjects.FalseObject);
                default:
                    // For other globals, we could look them up in the system dictionary
                    // but for now we'll return Nil if not a known special global
                    return OOP.Nil;
            }
        }
        
        /// <summary>
        /// Gets the class name for an OOP for debugging.
        /// </summary>
        private string GetClassNameForOop(OOP oop)
        {
            if (oop.IsNil) return "nil";
            if (oop.IsSmallInteger) return "SmallInteger";
            
            var obj = memory.GetObject(oop);
            if (obj == null) return "unknown";
            
            var classOop = obj.Header.ClassOOP;
            
            // Check known classes
            if (classOop.RawValue == classBuilder.GetClass("Symbol").RawValue) return "Symbol";
            if (classOop.RawValue == classBuilder.GetClass("String").RawValue) return "String";
            if (classOop.RawValue == classBuilder.GetClass("Array").RawValue) return "Array";
            if (classOop.RawValue == classBuilder.GetClass("ByteArray").RawValue) return "ByteArray";
            if (classOop.RawValue == classBuilder.GetClass("Association").RawValue) return "Association";
            
            return $"Class({classOop})";
        }
        
        /// <summary>
        /// Converts an OOP to a string representation for debugging.
        /// </summary>
        private string OopToString(OOP oop)
        {
            if (oop.IsNil) return "nil";
            if (oop.IsSmallInteger) return oop.SmallIntegerValue.ToString();
            
            var obj = memory.GetObject(oop);
            if (obj == null) return "?";
            
            var classOop = obj.Header.ClassOOP;
            
            // For Symbol or String, extract the actual text
            if (classOop.RawValue == classBuilder.GetClass("Symbol").RawValue ||
                classOop.RawValue == classBuilder.GetClass("String").RawValue)
            {
                var chars = new List<char>();
                for (int i = 0; i < obj.Fields.Length && i < 100; i++) // Limit to 100 chars
                {
                    if (obj.Fields[i].IsSmallInteger)
                    {
                        chars.Add((char)obj.Fields[i].SmallIntegerValue);
                    }
                }
                return new string(chars.ToArray());
            }
            
            return oop.ToString();
        }
        
        /// <summary>
        /// Gets a readable name or value for an OOP (used for Association values).
        /// </summary>
        private string GetClassNameOrValue(OOP oop)
        {
            if (oop.IsNil) return "nil";
            if (oop.IsSmallInteger) return $"SmallInt({oop.SmallIntegerValue})";
            
            // Check if it's a known class
            foreach (var kvp in classRegistry)
            {
                if (kvp.Value.RawValue == oop.RawValue)
                {
                    return $"class {kvp.Key}";
                }
            }
            
            // Check if it's a metaclass
            var obj = memory.GetObject(oop);
            if (obj != null && obj.Header.ClassOOP.RawValue == classBuilder.GetClass("Metaclass").RawValue)
            {
                // It's a metaclass - try to find which one
                foreach (var kvp in classRegistry)
                {
                    var metaclassOop = classBuilder.GetMetaclass(kvp.Key);
                    if (metaclassOop.RawValue == oop.RawValue)
                    {
                        return $"metaclass {kvp.Key}";
                    }
                }
            }
            
            return GetClassNameForOop(oop) + $"({oop})";
        }
        
        /// <summary>
        /// Parses an Association literal from the JSON string format.
        /// Format: "Association(key: #Name, value: null)"
        /// </summary>
        private OOP ParseAssociation(string associationStr)
        {
            // Parse the association string to extract the key
            var match = System.Text.RegularExpressions.Regex.Match(
                associationStr, 
                @"Association\(key:\s*#(\w+),\s*value:\s*(\w+)\)"
            );
            
            if (match.Success)
            {
                var keyName = match.Groups[1].Value;
                // The value is always null in the JSON, we need to resolve it at runtime
                
                // Check if this is a known class
                var classOop = classBuilder.GetClass(keyName);
                if (!classOop.IsNil)
                {
                    Console.WriteLine($"[InitExpr] Resolved Association for class {keyName}: {classOop}");
                    return CreateAssociation(keyName, classOop);
                }
                
                // Check if it's in the classRegistry passed to the constructor
                if (classRegistry.TryGetValue(keyName, out var registryClassOop))
                {
                    Console.WriteLine($"[InitExpr] Resolved Association for class {keyName} from registry: {registryClassOop}");
                    return CreateAssociation(keyName, registryClassOop);
                }
                
                // Check other globals
                var globalValue = GetGlobalValue(keyName);
                if (!globalValue.IsNil)
                {
                    Console.WriteLine($"[InitExpr] Resolved Association for global {keyName}: {globalValue}");
                    return CreateAssociation(keyName, globalValue);
                }
                
                // Unknown global - create association with nil value
                // It might be resolved later
                Console.WriteLine($"[InitExpr] WARNING: Could not resolve Association for {keyName}, using nil");
                Console.WriteLine($"[InitExpr] DEBUG: ClassBuilder.GetClass returned {classOop}, classRegistry has {classRegistry.Count} entries");
                if (classRegistry.Count > 0)
                {
                    Console.WriteLine($"[InitExpr] DEBUG: First few classes in registry: {string.Join(", ", classRegistry.Keys.Take(5))}");
                }
                return CreateAssociation(keyName, OOP.Nil);
            }
            
            // Couldn't parse - return nil
            return OOP.Nil;
        }
    }
}