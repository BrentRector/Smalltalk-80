using System;
using System.Collections.Generic;
using System.Linq;
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
            
            // Sort expressions by class hierarchy
            var sortedExpressions = SortByClassHierarchy(expressions);
            
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
            var blockContextClass = classBuilder.GetClass("BlockContext");
            var compiledMethodClass = classBuilder.GetClass("CompiledMethod");
            
            if (processClass.IsNil || blockContextClass.IsNil || compiledMethodClass.IsNil)
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
                doItMethodObj.Fields[3] = OOP.FromSmallInteger(0); // header
            }
            
            // Create a context for the process
            var context = memory.AllocateObject(blockContextClass, 32); // Allocate with space for stack
            var contextObj = memory.GetObject(context);
            if (contextObj != null)
            {
                // Set up the context
                vm.SetFieldBootstrap(context, "BlockContext", "method", doItMethod);
                vm.SetFieldBootstrap(context, "BlockContext", "receiver", OOP.Nil);
                vm.SetFieldBootstrap(context, "BlockContext", "caller", OOP.Nil);
                vm.SetFieldBootstrap(context, "BlockContext", "pc", OOP.FromSmallInteger(0));
                vm.SetFieldBootstrap(context, "BlockContext", "sp", OOP.FromSmallInteger(0));
            }
            
            // Create the process
            var process = memory.AllocateObject(processClass, 10); // Standard Process size
            vm.SetFieldBootstrap(process, "Process", "suspendedContext", context);
            vm.SetFieldBootstrap(process, "Process", "priority", OOP.FromSmallInteger(4)); // User priority
            
            return process;
        }
        
        /// <summary>
        /// Executes a single initialization expression.
        /// </summary>
        private void ExecuteExpression(InitializationExpression expr, OOP process)
        {
            // For initialization expressions, the compiled bytecode already contains
            // the receiver lookup and message send. The expressions are compiled as
            // top-level expressions that will be executed in the context of nil.
            
            // Check if we have bytecode to execute
            if (expr.Bytecode == null || expr.Bytecode.Count == 0)
            {
                Console.WriteLine($"[InitExpr] No bytecode for expression: {expr.Source}");
                return;
            }
            
            // Since initialization expressions are typically of the form "ClassName initialize",
            // and we have the bytecode that performs that send, we can execute it using
            // the documented receiver and selector if available
            
            if (!string.IsNullOrEmpty(expr.Receiver) && !string.IsNullOrEmpty(expr.Selector))
            {
                // Look up the receiver (usually a class)
                var receiver = classBuilder.GetClass(expr.Receiver);
                if (receiver.IsNil)
                {
                    Console.WriteLine($"[InitExpr] Warning: Cannot find receiver '{expr.Receiver}'");
                    return;
                }
                
                // Create the selector symbol
                var selector = symbolTable.Intern(expr.Selector);
                
                // Convert arguments if any
                var args = new OOP[expr.Arguments?.Count ?? 0];
                for (int i = 0; i < args.Length; i++)
                {
                    // For now, treat arguments as literals or symbols
                    if (expr.Arguments != null && expr.Arguments[i] != null)
                    {
                        args[i] = symbolTable.Intern(expr.Arguments[i]);
                    }
                }
                
                // Send the message
                vm.Send(receiver, selector, args);
            }
            else
            {
                // For expressions without explicit receiver/selector info,
                // we would need to execute the bytecode directly
                Console.WriteLine($"[InitExpr] Skipping expression without receiver/selector info: {expr.Source}");
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
            
            // Field 0: selector
            methodObj.Fields[0] = symbolTable.Intern("doIt");
            
            // Field 1: bytecode
            var bytecodeArray = CreateByteArray(expr.Bytecode.Select(b => (byte)b).ToArray());
            methodObj.Fields[1] = bytecodeArray;
            
            // Field 2: literals
            var literalsArray = CreateLiteralsArray(expr.Literals);
            methodObj.Fields[2] = literalsArray;
            
            // Field 3: header (method header format)
            // Format: 0 args, numTemps temporaries, literals count
            var header = (expr.NumTemps << 6) | Math.Min(expr.Literals.Count, 63);
            methodObj.Fields[3] = OOP.FromSmallInteger(header);
            
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
                string s when s.StartsWith("#") => symbolTable.Intern(s[1..]), // Symbol literal
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
    }
}