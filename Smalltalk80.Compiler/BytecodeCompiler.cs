using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Smalltalk80.Compiler.Generated;
using Smalltalk80.Compiler.Analysis;
using Smalltalk80.Core.Bytecodes;

namespace Smalltalk80.Compiler
{
    /// <summary>
    /// Compiles Smalltalk-80 AST nodes to Blue Book bytecode.
    /// Uses visitor pattern to traverse AST and emit bytecodes.
    /// </summary>
    public class BytecodeCompiler : IBytecodeCompiler
    {
        private CompilationEnvironment environment = null!;
        private BytecodeEmitter emitter = null!;
        private readonly VariableResolver resolver = new();  // Unified variable resolution
        private TempFrameAllocator allocator = null!;  // Unified temp frame allocation with slot reuse
        private string selector = "";
        private int primitiveNumber = 0;
        private bool hasPrimitive = false;
        private string currentMethodIdentifier = "";  // For tracking undeclared variables
        private Dictionary<IParseTree, TailCallInfo>? tailCalls = null;  // Tail call analysis results
        private int methodStartLabel = -1;  // Bytecode offset of method entry point for tail recursion

        /// <summary>
        /// Static registry of undeclared variables (global name -> list of methods that reference it).
        /// This tracks variables that couldn't be resolved during compilation.
        /// </summary>
        private static readonly Dictionary<string, List<string>> _undeclaredVariables = new();

        /// <summary>
        /// Get read-only view of undeclared variables.
        /// Returns dictionary of variable name -> list of method identifiers.
        /// </summary>
        public static IReadOnlyDictionary<string, List<string>> GetUndeclaredVariables() => _undeclaredVariables;

        /// <summary>
        /// Clear undeclared variables registry (call before starting new compilation).
        /// </summary>
        public static void ClearUndeclaredVariables() => _undeclaredVariables.Clear();

        /// <summary>
        /// Record that a variable was undeclared in a method.
        /// </summary>
        private void RecordUndeclaredVariable(string varName, string methodIdentifier)
        {
            if (!_undeclaredVariables.ContainsKey(varName))
            {
                _undeclaredVariables[varName] = new List<string>();
            }
            _undeclaredVariables[varName].Add(methodIdentifier);
        }

        /// <summary>
        /// Compiles a file-scope expression to bytecode.
        /// The expression will be compiled as if it were a method with no arguments.
        /// </summary>
        public CompiledMethodData CompileExpressionFromTree(
            Smalltalk80Parser.ExpressionContext expression)
        {
            // Initialize compilation state
            environment = new CompilationEnvironment();
            environment.Type = ScopeType.Method; // Set scope type for method
            emitter = new BytecodeEmitter();
            emitter.currentMethodName = "doIt";
            selector = "doIt"; // Standard selector for expressions
            primitiveNumber = 0;
            hasPrimitive = false;
            argumentNames.Clear();
            temporaryNames.Clear();

            try
            {
                // No arguments or temporaries for file-scope expressions
                environment.ArgumentCount = 0;
                environment.TemporariesAllocatedInFrame = 0;

                // Initialize temp frame allocator (no args or temps for expressions)
                allocator = new TempFrameAllocator();
                allocator.EnterMethodScope(new List<string>(), new List<string>());
                
                // Collect literals first (just like in CompileMethod)
                var visitor = new LiteralCollector(this, emitter, environment);
                visitor.Visit(expression);
                
                // Compile the expression (with literals already collected)
                CompileExpression(expression);
                
                // Expression should leave its result on the stack
                // Add return if not already present
                var bytecodes = emitter.GetBytecodes();
                if (bytecodes.Length == 0 || !IsReturnBytecode(bytecodes[^1]))
                {
                    // For standalone expressions, return the top of stack
                    // (The expression result should be on the stack)
                    emitter.ReturnTop();
                }
            }
            catch (Exception)
            {
//Console.WriteLine($"Error compiling expression: {ex.Message}");
                // Return minimal stub
                emitter = new BytecodeEmitter();
                emitter.PushNil();
                emitter.ReturnTop();
            }

            // Get bytecode for scanning
            byte[] bytecodeArray = emitter.GetBytecodes();
            int tempCount = allocator.GetMaxFrameSize();

            // CRITICAL FIX: Scan bytecode to find the highest temp index actually referenced
            // This is necessary because blocks can reference parent scope variables, and those
            // indices may be higher than what TempFrameAllocator allocated for THIS scope.
            int maxTempIndex = -1;
            for (int i = 0; i < bytecodeArray.Length; i++)
            {
                byte bc = bytecodeArray[i];
                if (bc >= 16 && bc <= 31 && (i == 0 || bytecodeArray[i-1] != 128))  // PUSH_TEMP
                {
                    int tempIndex = bc - 16;
                    maxTempIndex = Math.Max(maxTempIndex, tempIndex);
                }
            }

            // Use the maximum of allocated and referenced indices
            // (maxTempIndex is 0-based, so +1 converts to count)
            int actualTempCount = Math.Max(tempCount, maxTempIndex + 1);

            return new CompiledMethodData
            {
                Selector = selector,
                ArgumentCount = 0,
                TemporaryCount = actualTempCount,
                IsPrimitive = false,
                PrimitiveNumber = 0,
                Bytecode = bytecodeArray,
                Literals = emitter.GetLiterals(),
                ArgumentNames = new List<string>(),
                TemporaryNames = new List<string>(temporaryNames),
                VariableSlotMap = allocator.GetAllAllocatedVariables()  // DIAGNOSTIC: Add variable-to-slot mapping
            };
        }

        /// <summary>
        /// Compiles a Smalltalk method from a parse tree to Blue Book bytecode.
        /// </summary>
        public CompiledMethodData CompileMethodFromTree(
            Smalltalk80Parser.MethodDefContext methodDef,
            List<string> instanceVars,
            Dictionary<string, object>? classVars = null,
            Dictionary<string, object>? poolVars = null,
            CompilerOptions? options = null,
            HashSet<string>? knownClassNames = null,
            HashSet<string>? knownPoolDictionaryNames = null)
        {
            // Use default options if not specified
            options ??= CompilerOptions.Default;

            // Initialize compilation state
            environment = new CompilationEnvironment();
            environment.Type = ScopeType.Method; // Set scope type for method
            emitter = new BytecodeEmitter();
            selector = "";
            primitiveNumber = 0;
            hasPrimitive = false;
            argumentNames.Clear();
            temporaryNames.Clear();
            tailCalls = null;
            methodStartLabel = -1;
            
            // Set up instance variables with correct offsets
            for (int i = 0; i < instanceVars.Count; i++)
            {
                environment.InstanceVariables[instanceVars[i]] = i;
            }
            
            // Set up known class names for proper variable resolution
            environment.KnownClassNames = knownClassNames;
            
            // Set up known pool dictionary names for proper variable resolution
            environment.KnownPoolDictionaryNames = knownPoolDictionaryNames;
            if (knownPoolDictionaryNames != null && knownPoolDictionaryNames.Count > 0)
            {
                // Console.WriteLine($"[BYTECODE COMPILER] Set KnownPoolDictionaryNames: [{string.Join(", ", knownPoolDictionaryNames)}]");
            }
            
            // Set up class variables
            if (classVars != null)
            {
                foreach (var kvp in classVars)
                {
                    environment.ClassVariables[kvp.Key] = kvp.Value;
                }
            }
            
            // Set up pool variables
            if (poolVars != null)
            {
                foreach (var kvp in poolVars)
                {
                    environment.PoolVariables[kvp.Key] = kvp.Value;
                }
            }
            
            try
            {
                // Process method header
                ProcessMethodHeader(methodDef.methodHeader());

                // Set method identifier for undeclared tracking (just selector for now)
                currentMethodIdentifier = selector;

                // Process temporaries
                if (methodDef.temporaries() != null)
                {
                    ProcessTemporaries(methodDef.temporaries());
                }

                // Initialize temp frame allocator with method args and temps
                // This must happen AFTER ProcessMethodHeader and ProcessTemporaries
                allocator = new TempFrameAllocator();
                allocator.EnterMethodScope(argumentNames, temporaryNames);

                // Process primitive declaration
                if (methodDef.primitiveDecl() != null)
                {
                    ProcessPrimitive(methodDef.primitiveDecl());
                }

                // Perform tail recursion analysis if enabled
                if (options.OptimizeTailRecursion)
                {
                    var analyzer = new TailRecursionAnalyzer();
                    if (options.EmitOptimizationComments)
                    {
                        analyzer.EnableDiagnostics();
                    }

                    tailCalls = analyzer.Analyze(methodDef.methodBody(), selector);

                    if (options.EmitOptimizationComments && tailCalls.Count > 0)
                    {
                        foreach (var diagnostic in analyzer.GetDiagnosticMessages())
                        {
                        }
                    }
                }

                // Collect literals from method body first
                // This walks the entire AST to find all literals
                CollectLiterals(methodDef.methodBody());

                // Mark method entry point BEFORE generating bytecode (if tail recursion enabled)
                if (options.OptimizeTailRecursion && tailCalls != null && tailCalls.Count > 0)
                {
                    methodStartLabel = emitter.MarkMethodStart();
                }

                // Generate bytecode
//Console.WriteLine($"[BYTECODE] About to compile method body");
                CompileMethodBody(methodDef.methodBody());
//Console.WriteLine($"[BYTECODE] After compiling method body, bytecode count: {emitter.GetBytecodes().Length}");
                
                // Ensure method has a return
                EnsureMethodReturn();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error compiling method {selector}: {ex.Message}");
                // Return minimal stub method
                emitter = new BytecodeEmitter();
                emitter.ReturnSelf();
            }

            // Detect quick-return patterns (flags=5 or flags=6)
            var (isQuickReturnSelf, quickReturnFieldIndex) = DetectQuickReturn(
                methodDef.methodBody(),
                null);

            // Get bytecode array
            var bytecodeArray = emitter.GetBytecodes();

            int tempCount = allocator.GetMaxFrameSize();

            // CRITICAL FIX: Scan bytecode to find the highest temp index actually referenced
            // This is necessary because blocks can reference parent scope variables, and those
            // indices may be higher than what TempFrameAllocator allocated for THIS scope.
            int maxTempIndex = -1;
            for (int i = 0; i < bytecodeArray.Length; i++)
            {
                byte bc = bytecodeArray[i];
                if (bc >= 16 && bc <= 31 && (i == 0 || bytecodeArray[i-1] != 128))  // PUSH_TEMP
                {
                    int tempIndex = bc - 16;
                    maxTempIndex = Math.Max(maxTempIndex, tempIndex);
                }
            }

            // Use the maximum of allocated and referenced indices
            // (maxTempIndex is 0-based, so +1 converts to count)
            int actualTempCount = Math.Max(tempCount, maxTempIndex + 1);

            var compiledMethod = new CompiledMethodData
            {
                Selector = selector,
                ArgumentCount = environment.ArgumentCount,
                TemporaryCount = actualTempCount,
                IsPrimitive = hasPrimitive,
                PrimitiveNumber = primitiveNumber,
                Bytecode = bytecodeArray,
                Literals = emitter.GetLiterals(),
                ArgumentNames = new List<string>(argumentNames),
                TemporaryNames = new List<string>(temporaryNames),
                IsQuickReturnSelf = isQuickReturnSelf,
                QuickReturnFieldIndex = quickReturnFieldIndex,
                VariableSlotMap = allocator.GetAllAllocatedVariables()  // DIAGNOSTIC: Add variable-to-slot mapping
            };

            // DIAGNOSTIC: Print variable slot mapping for hasInterned:ifTrue: specifically
            if (selector == "hasInterned:ifTrue:" && compiledMethod.VariableSlotMap != null && compiledMethod.VariableSlotMap.Count > 0)
            {
                Console.WriteLine($"[VAR SLOTS] {selector}: {string.Join(", ", compiledMethod.VariableSlotMap.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
            }

            return compiledMethod;
        }

        private List<string> argumentNames = new();

        private void ProcessMethodHeader(Smalltalk80Parser.MethodHeaderContext header)
        {
            argumentNames.Clear();

            if (header.unarySelector() != null)
            {
                selector = header.unarySelector().GetText();
                environment.ArgumentCount = 0;
            }
            else if (header.binarySelector() != null)
            {
                selector = header.binarySelector().GetText();
                var arg = header.formalArg(0);
                if (arg != null)
                {
                    var argName = arg.GetText();
                    argumentNames.Add(argName);
                    // Populate OwnArguments for method scope
                    environment.LocalArgumentIndices[argName] = 0;
                    // Maintain backward compatibility
                    environment.Arguments[argName] = 0;
                    environment.ArgumentCount = 1;
                }
            }
            else
            {
                // Keyword selector
                var keywords = header.identifierColon();
                var parts = new List<string>();
                var args = header.formalArg();

                for (int i = 0; i < keywords.Length; i++)
                {
                    var keywordText = keywords[i].GetText();
                    parts.Add(keywordText);
                    if (i < args.Length)
                    {
                        var argText = args[i].GetText();
                        argumentNames.Add(argText);
                        // Populate OwnArguments for method scope
                        environment.LocalArgumentIndices[argText] = i;
                        // Maintain backward compatibility
                        environment.Arguments[argText] = i;
                    }
                }

                selector = string.Join("", parts);
                environment.ArgumentCount = args.Length;
            }

            // Set emitter method name for debugging
            emitter.currentMethodName = selector;
        }
        
        private List<string> temporaryNames = new();

        private void ProcessTemporaries(Smalltalk80Parser.TemporariesContext temps)
        {
            temporaryNames.Clear();
            var tempNames = temps.temporaryName();
            for (int i = 0; i < tempNames.Length; i++)
            {
                var name = tempNames[i].GetText();
                temporaryNames.Add(name);
                // Populate OwnTemporaries for method scope (0-based, no inheritance)
                environment.LocalTemporaryIndices[name] = i;
                // Maintain backward compatibility (absolute index including args)
                environment.Temporaries[name] = environment.ArgumentCount + i;
            }
            environment.TemporariesAllocatedInFrame = tempNames.Length;
        }
        
        private void ProcessPrimitive(Smalltalk80Parser.PrimitiveDeclContext primitive)
        {
            var text = primitive.GetText();
            var match = System.Text.RegularExpressions.Regex.Match(text, @"<primitive:\s*(\d+)>");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var num))
            {
                hasPrimitive = true;
                primitiveNumber = num;
            }
        }
        
        private void CollectLiterals(Smalltalk80Parser.MethodBodyContext? body)
        {
            if (body == null) return;
            
            // Walk the tree to collect all literals that will be needed
            var visitor = new LiteralCollector(this, emitter, environment);
            visitor.Visit(body);
        }
        
        private void CompileMethodBodyNoBang(Smalltalk80Parser.MethodBodyNoBangContext? body)
        {
//Console.WriteLine($"[BYTECODE] CompileMethodBodyNoBang called");
            if (body == null)
            {
//Console.WriteLine($"[BYTECODE] CompileMethodBodyNoBang: body is null, pushing self");
                emitter.PushSelf();
                return;
            }
            
//Console.WriteLine($"[BYTECODE] CompileMethodBodyNoBang: body text = '{body.GetText()}'");
//Console.WriteLine($"[BYTECODE] CompileMethodBodyNoBang: children count = {body.ChildCount}");
            
            var returnStmt = body.returnStatement();
            if (returnStmt != null)
            {
//Console.WriteLine($"[BYTECODE] CompileMethodBodyNoBang: Found return statement");
                CompileReturnStatement(returnStmt);
//Console.WriteLine($"[BYTECODE] CompileMethodBodyNoBang: After CompileReturnStatement, bytecode count: {emitter.GetBytecodes().Length}");
                return;
            }
            
            var statements = body.statements();
            if (statements != null)
            {
//Console.WriteLine($"[BYTECODE] CompileMethodBodyNoBang: Found statements");
                CompileStatements(statements);
//Console.WriteLine($"[BYTECODE] CompileMethodBodyNoBang: After CompileStatements, bytecode count: {emitter.GetBytecodes().Length}");
            }
            else
            {
//Console.WriteLine($"[BYTECODE] CompileMethodBodyNoBang: No statements, pushing self");
                emitter.PushSelf();
            }
        }
        
        private void CompileMethodBody(Smalltalk80Parser.MethodBodyContext? body)
        {
//Console.WriteLine($"[BYTECODE] CompileMethodBody called");
            if (body == null)
            {
//Console.WriteLine($"[BYTECODE] CompileMethodBody: body is null, pushing self");
                emitter.PushSelf();
                return;
            }
            
//Console.WriteLine($"[BYTECODE] CompileMethodBody: body text = '{body.GetText()}'");
            
            var returnStmt = body.returnStatement();
            if (returnStmt != null)
            {
//Console.WriteLine($"[BYTECODE] CompileMethodBody: Found return statement");
                CompileReturnStatement(returnStmt);
//Console.WriteLine($"[BYTECODE] CompileMethodBody: After CompileReturnStatement, bytecode count: {emitter.GetBytecodes().Length}");
                return;
            }
            
            var statements = body.statements();
            if (statements != null)
            {
//Console.WriteLine($"[BYTECODE] CompileMethodBody: Found statements");
                CompileStatements(statements);
//Console.WriteLine($"[BYTECODE] CompileMethodBody: After CompileStatements, bytecode count: {emitter.GetBytecodes().Length}");
            }
            else
            {
//Console.WriteLine($"[BYTECODE] CompileMethodBody: No statements, pushing self");
                emitter.PushSelf();
            }
        }
        
        private void CompileStatements(Smalltalk80Parser.StatementsContext stmts, bool inBlock = false)
        {
            var statements = stmts.statement();

            for (int i = 0; i < statements.Length; i++)
            {
                bool isLast = (i == statements.Length - 1);
                CompileStatement(statements[i], isLast, inBlock);
            }
        }

        private void CompileStatement(Smalltalk80Parser.StatementContext stmt, bool isLast, bool inBlock)
        {
//Console.WriteLine($"[BYTECODE] CompileStatement called, isLast={isLast}, inBlock={inBlock}");
            var returnStmt = stmt.returnStatement();
            if (returnStmt != null)
            {
//Console.WriteLine($"[BYTECODE] CompileStatement: Found return statement");
                CompileReturnStatement(returnStmt);
                return;
            }

            var exprStmt = stmt.expressionStatement();
            if (exprStmt != null)
            {
                // In blocks: last statement must leave value (for block return)
                // In methods: statements don't leave values (consumed by implicit ReturnSelf)
                bool leaveOnStack = inBlock && isLast;
//Console.WriteLine($"[BYTECODE] CompileStatement: Found expression statement, compiling with leaveOnStack={leaveOnStack}");
                CompileExpression(exprStmt.expression(), leaveOnStack);
//Console.WriteLine($"[BYTECODE] CompileStatement: After CompileExpression, bytecode count: {emitter.GetBytecodes().Length}");
            }
            else
            {
//Console.WriteLine($"[BYTECODE] CompileStatement: No return or expression statement found!");
            }
        }
        
        private void CompileReturnStatement(Smalltalk80Parser.ReturnStatementContext returnStmt)
        {
//Console.WriteLine($"[BYTECODE] CompileReturnStatement called");
            var expr = returnStmt.expression();
            if (expr != null)
            {
//Console.WriteLine($"[BYTECODE] CompileReturnStatement: Has expression, about to compile it");
                CompileExpression(expr);
//Console.WriteLine($"[BYTECODE] CompileReturnStatement: After CompileExpression, bytecode count: {emitter.GetBytecodes().Length}");
            }
            else
            {
//Console.WriteLine($"[BYTECODE] CompileReturnStatement: No expression, pushing self");
                emitter.PushSelf();
            }
            
            if (environment.ScopeDepth > 0)
            {
//Console.WriteLine($"[BYTECODE] CompileReturnStatement: Block scope, emitting ReturnTopFromBlock");
                emitter.ReturnTopFromBlock();
            }
            else
            {
//Console.WriteLine($"[BYTECODE] CompileReturnStatement: Method scope, emitting ReturnTop");
                emitter.ReturnTop();
            }
//Console.WriteLine($"[BYTECODE] CompileReturnStatement: Final bytecode count: {emitter.GetBytecodes().Length}");
        }
        
        private void CompileExpression(Smalltalk80Parser.ExpressionContext expr, bool leaveOnStack = true)
        {
//Console.WriteLine($"[BYTECODE] CompileExpression called, leaveOnStack={leaveOnStack}");
//Console.WriteLine($"[BYTECODE] CompileExpression: Current bytecode count: {emitter.GetBytecodes().Length}");
            var assignExpr = expr.assignmentExpr();
            if (assignExpr != null)
            {
//Console.WriteLine($"[BYTECODE] CompileExpression: Found assignmentExpr");
                CompileAssignmentExpr(assignExpr, leaveOnStack);
//Console.WriteLine($"[BYTECODE] CompileExpression: After CompileAssignmentExpr, bytecode count: {emitter.GetBytecodes().Length}");
            }
            else
            {
//Console.WriteLine($"[BYTECODE] CompileExpression: No assignmentExpr found - this should not happen with current grammar!");
            }
        }
        
        private void CompileAssignmentExpr(Smalltalk80Parser.AssignmentExprContext expr, bool leaveOnStack = true)
        {
            var variable = expr.variable();
//Console.WriteLine($"[BYTECODE] CompileAssignmentExpr: variable={variable?.GetText()}, hasAssign={expr.ASSIGN() != null}, leaveOnStack={leaveOnStack}");
//Console.WriteLine($"[BYTECODE] CompileAssignmentExpr: Current bytecode count: {emitter.GetBytecodes().Length}");
            
            if (variable != null && expr.ASSIGN() != null)
            {
//Console.WriteLine($"[BYTECODE] CompileAssignmentExpr: Processing assignment to {variable.GetText()}");
                // Assignment: compile RHS first
                // RHS must ALWAYS leave value on stack for the store operation
                var rhsAssignExpr = expr.assignmentExpr();
                if (rhsAssignExpr == null)
                {
                    throw new InvalidOperationException("Assignment expression has no RHS");
                }

                CompileAssignmentExpr(rhsAssignExpr, leaveOnStack: true);

                // Store the value
                // Use leaveOnStack directly - the caller knows if the result is needed
                // Examples:
                //   - "a _ b _ c" → inner assignment (b_c) must leave value for outer assignment
                //   - "(position _ x) bitShift: -8" → assignment must leave value for bitShift:
                //   - "x _ y. z" → final store in sequence doesn't need to leave value
                StoreVariable(variable.GetText(), leaveOnStack: leaveOnStack);
            }
            else
            {
                // No assignment, compile cascade expression
                var cascadeExpr = expr.cascadeExpr();
//Console.WriteLine($"[BYTECODE] CompileAssignmentExpr: No assignment, cascadeExpr={cascadeExpr != null}");
                if (cascadeExpr != null)
                {
//Console.WriteLine($"[BYTECODE] CompileAssignmentExpr: About to compile cascadeExpr");
                    CompileCascadeExpr(cascadeExpr);
//Console.WriteLine($"[BYTECODE] CompileAssignmentExpr: After CompileCascadeExpr, bytecode count: {emitter.GetBytecodes().Length}");
                    if (!leaveOnStack)
                    {
//Console.WriteLine($"[BYTECODE] CompileAssignmentExpr: Popping result (leaveOnStack=false)");
                        emitter.Pop();
                    }
                }
                else
                {
//Console.WriteLine($"[BYTECODE] CompileAssignmentExpr: ERROR - No cascadeExpr found!");
                }
            }
        }
        
        private void CompileCascadeExpr(Smalltalk80Parser.CascadeExprContext expr)
        {
//Console.WriteLine($"[BYTECODE] CompileCascadeExpr called");
//Console.WriteLine($"[BYTECODE] CompileCascadeExpr: Current bytecode count: {emitter.GetBytecodes().Length}");
            var messageExpr = expr.messageExpr();
            var messageParts = expr.messagePart();
//Console.WriteLine($"[BYTECODE] CompileCascadeExpr: messageExpr={messageExpr != null}, messageParts count={messageParts?.Length ?? 0}");
            
            if (messageParts != null && messageParts.Length > 0)
            {
                // Cascade implementation following Smalltalk-80 Blue Book specification:
                // 1. Push receiver once
                // 2. For each message except the last:
                //    - Dup the receiver (bytecode 136)
                //    - Send the message
                //    - Pop the result (bytecode 135)
                // 3. For the last message:
                //    - Send the message
                //    - Leave result on stack
                
                // Check if the messageExpr actually sends a message or is just a receiver
                bool firstIsJustReceiver = messageExpr != null && IsJustReceiver(messageExpr);
                
                if (firstIsJustReceiver && messageExpr != null)
                {
                    // The first part is just a receiver (like "anObject"), not a message send
                    // Push the receiver once
                    CompileMessageExpr(messageExpr, isCascadeBase: false, totalCascadeMessages: 0);
                    
                    // Process all cascaded messages
                    for (int i = 0; i < messageParts.Length; i++)
                    {
                        bool isLast = (i == messageParts.Length - 1);
                        
                        // Dup receiver for this message (except for the last)
                        if (!isLast)
                        {
                            emitter.Dup(); // bytecode 136 - duplicate receiver for next message
                        }
                        
                        // Compile the message part
                        CompileMessagePart(messageParts[i]);
                        
                        // Pop result (except for the last)
                        if (!isLast)
                        {
                            emitter.Pop(); // bytecode 135 - pop the result
                        }
                    }
                }
                else if (messageExpr != null)
                {
                    // The first part is a full message expression
                    // Compile it with cascade support
                    CompileMessageExpr(messageExpr, isCascadeBase: true, totalCascadeMessages: messageParts.Length + 1);
                    
                    // Process cascaded messages
                    for (int i = 0; i < messageParts.Length; i++)
                    {
                        bool isLast = (i == messageParts.Length - 1);
                        
                        // Pop previous message result
                        emitter.Pop(); // bytecode 135 - pop the result of the previous message
                        
                        // Dup receiver for next message (except for the last)
                        if (!isLast)
                        {
                            emitter.Dup(); // bytecode 136 - duplicate receiver for next message
                        }
                        
                        // Compile the message part
                        CompileMessagePart(messageParts[i]);
                    }
                }
            }
            else
            {
                // No cascade, just compile the message
//Console.WriteLine($"[BYTECODE] CompileCascadeExpr: No cascade, compiling single messageExpr");
                if (messageExpr != null)
                {
                    CompileMessageExpr(messageExpr, isCascadeBase: false, totalCascadeMessages: 0);
//Console.WriteLine($"[BYTECODE] CompileCascadeExpr: After CompileMessageExpr, bytecode count: {emitter.GetBytecodes().Length}");
                }
                else
                {
//Console.WriteLine($"[BYTECODE] CompileCascadeExpr: ERROR - messageExpr is null!");
                }
            }
        }
        
        private bool IsJustReceiver(Smalltalk80Parser.MessageExprContext expr)
        {
            // Check if this message expression is just a receiver without any messages
            if (expr == null) return false;
            
            // A messageExpr is just a receiver if it has only a base with no messages
            // New grammar: messageExpr = binaryExpr keywordMessage?
            // binaryExpr = unaryExpr (binaryMessage)*
            // unaryExpr = messageBase unaryMessage*
            var binaryExpr = expr.binaryExpr();
            if (binaryExpr == null) return true;

            var unaryExpr = binaryExpr.unaryExpr();
            if (unaryExpr == null) return true;

            // Check if there are no messages at all
            bool hasUnaryMsgs = unaryExpr.unaryMessage() != null && unaryExpr.unaryMessage().Length > 0;
            bool hasBinaryMsgs = binaryExpr.binaryMessage() != null && binaryExpr.binaryMessage().Length > 0;
            bool hasKeywordMsg = expr.keywordMessage() != null;

            return !hasUnaryMsgs && !hasBinaryMsgs && !hasKeywordMsg;
        }
        
        private void CompileMessageExpr(Smalltalk80Parser.MessageExprContext expr, bool isCascadeBase = false, int totalCascadeMessages = 0)
        {
            // New grammar structure: messageExpr = binaryExpr keywordMessage?
            var binaryExpr = expr.binaryExpr();

            if (binaryExpr != null)
            {
                // Check if receiver is super (look at the unaryExpr's messageBase)
                var unaryExpr = binaryExpr.unaryExpr();
                bool receiverIsSuper = false;
                if (unaryExpr != null)
                {
                    var messageBase = unaryExpr.messageBase();
                    receiverIsSuper = IsReceiverSuper(messageBase);
                }

                // Compile the binary expression
                CompileBinaryExpr(binaryExpr, receiverIsSuper, isCascadeBase, totalCascadeMessages);
            }

            // Process keyword message if present
            var keywordMessage = expr.keywordMessage();
            if (keywordMessage != null)
            {
                // Pass receiverIsSuper flag to handle super sends correctly
                // This is necessary ONLY when:
                // 1. The messageBase is super, AND
                // 2. There are NO unary messages, AND
                // 3. There are NO binary messages
                // (e.g., "^super extent:fromArray:offset:")
                //
                // But NOT for cases like "super new compositionRectangle:..." where
                // the keyword message's receiver is the result of "super new", not super itself.
                var receiverIsSuper = false;
                if (binaryExpr != null)
                {
                    var unaryExpr = binaryExpr.unaryExpr();
                    if (unaryExpr != null)
                    {
                        var messageBase = unaryExpr.messageBase();
                        var unaryMessages = unaryExpr.unaryMessage();
                        var binaryMessages = binaryExpr.binaryMessage();

                        // Only mark as super send if there are no intervening messages
                        bool hasNoUnaryMessages = unaryMessages == null || unaryMessages.Length == 0;
                        bool hasNoBinaryMessages = binaryMessages == null || binaryMessages.Length == 0;

                        receiverIsSuper = IsReceiverSuper(messageBase) && hasNoUnaryMessages && hasNoBinaryMessages;
                    }
                }
                CompileKeywordMessage(keywordMessage, receiverIsSuper);
            }
        }

        private void CompileBinaryExpr(Smalltalk80Parser.BinaryExprContext expr, bool receiverIsSuper, bool isCascadeBase = false, int totalCascadeMessages = 0)
        {
            // Grammar: unaryExpr (binaryMessage)*
            var unaryExpr = expr.unaryExpr();
            var binaryMessages = expr.binaryMessage();

            if (unaryExpr == null)
                return;

            // Check if there are unary messages - if so, they "consume" the super flag
            // For "super isControlActive & ..." the super send is for isControlActive,
            // and & is sent to the Boolean result, not to super.
            var unaryMessages = unaryExpr.unaryMessage();
            bool hasUnaryMessages = unaryMessages != null && unaryMessages.Length > 0;

            // Binary messages are only super sends if:
            // 1. The receiver is super, AND
            // 2. There are NO unary messages (so the binary message is directly on super)
            bool binaryReceiverIsSuper = receiverIsSuper && !hasUnaryMessages;

            if (isCascadeBase && totalCascadeMessages > 1)
            {
                // For cascades, we need to DUP the receiver BEFORE any messages are sent.
                // The receiver is the messageBase of the unaryExpr.
                //
                // For "self step; step":
                // Correct: PUSH self → DUP → SEND step → POP → SEND step
                // Wrong:   PUSH self → SEND step → DUP (result!) → POP → SEND step (crash!)

                // 1. Push just the message base (the actual receiver)
                var messageBase = unaryExpr.messageBase();
                CompileMessageBase(messageBase);

                // 2. DUP the receiver for cascade support
                emitter.Dup();

                // 3. Send unary messages (if any)
                if (unaryMessages != null)
                {
                    for (int i = 0; i < unaryMessages.Length; i++)
                    {
                        var msg = unaryMessages[i];
                        var selector = msg.unarySelector().GetText();

                        // Check for tail-recursive call optimization
                        if (tailCalls != null && tailCalls.TryGetValue(msg, out var tailCallInfo))
                        {
                            if (tailCallInfo.IsSafeToOptimize && methodStartLabel >= 0)
                            {
                                emitter.EmitTailRecursiveJump(methodStartLabel, 0);
                                continue;
                            }
                        }

                        if (i == 0 && receiverIsSuper)
                        {
                            emitter.EmitSuperSend(selector, 0);
                        }
                        else
                        {
                            emitter.EmitSend(selector, 0);
                        }
                    }
                }
            }
            else
            {
                // Normal case - compile the full unary expression
                CompileUnaryExpr(unaryExpr, receiverIsSuper);
            }

            // Process binary messages
            if (binaryMessages != null)
            {
                for (int i = 0; i < binaryMessages.Length; i++)
                {
                    var msg = binaryMessages[i];
                    var selector = msg.binarySelector().GetText();

                    // For super sends, pre-add selector to literal frame BEFORE compiling arguments.
                    // This is critical because super sends always need the selector in the literal frame
                    // (they can't use special bytecodes), and argument literal indices depend on
                    // selectors.Count being accurate.
                    if (i == 0 && binaryReceiverIsSuper)
                    {
                        emitter.AddLiteral(new Symbol(selector));
                    }

                    // Compile the argument (unaryExpr)
                    var argExpr = msg.unaryExpr();
                    if (argExpr != null)
                    {
                        CompileUnaryExpr(argExpr, false); // Arguments are never super sends
                    }

                    // Emit the send
                    if (i == 0 && binaryReceiverIsSuper)
                    {
                        emitter.EmitSuperSend(selector, 1);
                    }
                    else
                    {
                        emitter.EmitSend(selector, 1);
                    }
                }
            }
        }

        private void CompileUnaryExpr(Smalltalk80Parser.UnaryExprContext expr, bool receiverIsSuper)
        {
            // Grammar: messageBase unaryMessage*
            var messageBase = expr.messageBase();
            var unaryMessages = expr.unaryMessage();

            // Compile the base
            CompileMessageBase(messageBase);

            // Process unary messages
            if (unaryMessages != null)
            {
                for (int i = 0; i < unaryMessages.Length; i++)
                {
                    var msg = unaryMessages[i];
                    var selector = msg.unarySelector().GetText();

                    // Check if this unary message is a tail-recursive call
                    if (tailCalls != null && tailCalls.TryGetValue(msg, out var tailCallInfo))
                    {
                        if (tailCallInfo.IsSafeToOptimize && methodStartLabel >= 0)
                        {
                            // Emit tail-recursive jump instead of SEND
                            // No arguments for unary messages
                            emitter.EmitTailRecursiveJump(methodStartLabel, 0);

                            continue; // Skip normal send emission
                        }
                    }

                    // Normal (non-tail-recursive) send
                    if (i == 0 && receiverIsSuper)
                    {
                        emitter.EmitSuperSend(selector, 0);
                    }
                    else
                    {
                        emitter.EmitSend(selector, 0);
                    }
                }
            }
        }
        
        private void CompileMessagePart(Smalltalk80Parser.MessagePartContext part)
        {
            var unaryMessage = part.unaryMessage();
            if (unaryMessage != null)
            {
                var selector = unaryMessage.unarySelector().GetText();
                emitter.SendMessage(selector, 0);
                return;
            }
            
            var binaryMessage = part.binaryMessage();
            if (binaryMessage != null)
            {
                var selector = binaryMessage.binarySelector().GetText();
                // Binary message argument is a unaryExpr
                CompileUnaryExpr(binaryMessage.unaryExpr(), false);
                emitter.SendMessage(selector, 1);
                return;
            }
            
            var keywordMessage = part.keywordMessage();
            if (keywordMessage != null)
            {
                CompileKeywordMessage(keywordMessage);
            }
        }
        
        private void CompileKeywordMessage(Smalltalk80Parser.KeywordMessageContext msg, bool isSuper = false)
        {
//Console.WriteLine($"[BYTECODE] CompileKeywordMessage called, isSuper={isSuper}");
            var keywordParts = msg.keywordPart();
            var keywords = new List<string>();

            // Build selector first to check for conditional patterns
            foreach (var part in keywordParts)
            {
                var keyword = part.IDENTIFIER().GetText() + ":";
                keywords.Add(keyword);
            }
            var selector = string.Join("", keywords);
//Console.WriteLine($"[BYTECODE] CompileKeywordMessage: selector='{selector}'");

            // Check if this is a conditional message that can be inlined
            if (!isSuper && TryInlineConditional(selector, keywordParts))
            {
//Console.WriteLine($"[BYTECODE] CompileKeywordMessage: Successfully inlined conditional");
                return; // Successfully inlined
            }
//Console.WriteLine($"[BYTECODE] CompileKeywordMessage: Not inlined, compiling normally");

            // Check if this keyword message is a tail-recursive call BEFORE compiling arguments
            bool isTailRecursive = false;
            TailCallInfo? tailCallInfo = null;
            if (tailCalls != null && tailCalls.TryGetValue(msg, out tailCallInfo))
            {
                if (tailCallInfo.IsSafeToOptimize && methodStartLabel >= 0)
                {
                    isTailRecursive = true;
                }
            }

            // For super sends, pre-add selector to literal frame BEFORE compiling arguments.
            // This is critical because super sends always need the selector in the literal frame
            // (they can't use special bytecodes), and argument literal indices depend on
            // selectors.Count being accurate.
            if (isSuper)
            {
                emitter.AddLiteral(new Symbol(selector));
            }

            // Compile arguments (push onto stack)
            foreach (var part in keywordParts)
            {
                // New grammar: keywordPart uses binaryExpr directly
                var argExpr = part.binaryExpr();
                if (argExpr != null)
                {
                    CompileBinaryExpr(argExpr, false); // Arguments are never super sends
                }
            }

            // Emit tail-recursive jump or normal send
            if (isTailRecursive)
            {
                // Emit tail-recursive jump instead of SEND
                emitter.EmitTailRecursiveJump(methodStartLabel, keywordParts.Length);
            }
            else
            {
                // Send the message (normal non-tail-recursive case)
                if (isSuper)
                {
                    emitter.EmitSuperSend(selector, keywordParts.Length);
                }
                else
                {
                    emitter.EmitSend(selector, keywordParts.Length);
                }
            }
        }
        
        /// <summary>
        /// Attempts to inline conditional message patterns (ifTrue:, ifFalse:, ifTrue:ifFalse:).
        /// Returns true if successfully inlined, false otherwise.
        ///
        /// DISABLED (2025-10-07): Blue Book Smalltalk-80 does NOT inline conditionals!
        /// Control structures like ifTrue: and ifFalse: are ordinary message sends.
        /// Blocks are compiled as literals and control flow happens via polymorphic
        /// dispatch on True/False classes. See docs/Interpreter-Architecture.md
        /// "Block Compilation and Control Flow" for complete explanation.
        ///
        /// Future optimization: Inlining code removed (550 lines). See IA documentation
        /// section "Future Optimization: Inlined Control Flow" for how to implement
        /// this as an optional optimization.
        /// </summary>
        private bool TryInlineConditional(string selector, Smalltalk80Parser.KeywordPartContext[] keywordParts)
        {
            // ALWAYS return false - disable inlining optimization
            // ifTrue:, ifFalse:, ifTrue:ifFalse: are ORDINARY MESSAGE SENDS
            return false;
        }

        private void CompileNonKeywordExpr(Smalltalk80Parser.BinaryExprContext expr)
        {
            // Grammar: nonKeywordExpr = binaryExpr
            // So this method now takes BinaryExprContext directly
            var unaryExpr = expr.unaryExpr();
            if (unaryExpr == null) return;

            var messageBase = unaryExpr.messageBase();
            bool receiverIsSuper = IsReceiverSuper(messageBase);

            // Compile the unary expression
            CompileUnaryExpr(unaryExpr, receiverIsSuper);

            // Process binary messages
            var binaryMessages = expr.binaryMessage();
            if (binaryMessages != null)
            {
                foreach (var msg in binaryMessages)
                {
                    var selector = msg.binarySelector().GetText();
                    CompileUnaryExpr(msg.unaryExpr(), false);
                    emitter.EmitSend(selector, 1);
                }
            }
        }
        
        private void CompileMessageBase(Smalltalk80Parser.MessageBaseContext baseCtx)
        {
//Console.WriteLine($"[BYTECODE] CompileMessageBase called");
            if (baseCtx.literal() != null)
            {
//Console.WriteLine($"[BYTECODE] CompileMessageBase: Compiling literal");
                CompileLiteral(baseCtx.literal());
            }
            else if (baseCtx.variable() != null)
            {
                var varName = baseCtx.variable().GetText();
//Console.WriteLine($"[BYTECODE] CompileMessageBase: Pushing variable '{varName}'");
                PushVariable(varName);
            }
            else if (baseCtx.negativeNumber() != null)
            {
                // Handle negative numbers (e.g., -1, -42)
                var negNum = baseCtx.negativeNumber();
                var innerBase = negNum.messageBase();
                
                // Check if the inner base is a literal number
                if (innerBase.literal() != null && innerBase.literal().numberLiteral() != null)
                {
                    var numText = innerBase.literal().numberLiteral().GetText();

                    // Use TryParseSmalltalkNumber to handle all number formats including radix notation
                    if (TryParseSmalltalkNumber(numText, out object? positiveValue))
                    {
                        if (positiveValue is int positiveIntVal)
                        {
                            int negativeVal = -positiveIntVal;

                            // Use special bytecode for -1
                            if (negativeVal == -1)
                            {
                                emitter.PushMinusOne();
                            }
                            else
                            {
                                // For other negative integers, push as literal constant
                                emitter.PushLiteralConstant(negativeVal);
                            }
                        }
                        else if (positiveValue is double positiveFloatVal)
                        {
                            // For negative floats, push as literal constant
                            emitter.PushLiteralConstant(-positiveFloatVal);
                        }
                    }
                }
                else
                {
                    // For complex expressions like -(x + 1), compile and send negated
                    CompileMessageBase(innerBase);
                    emitter.EmitSend("negated", 0);
                }
            }
            else if (baseCtx.parenthesizedExpr() != null)
            {
                CompileExpression(baseCtx.parenthesizedExpr().expression());
            }
            else if (baseCtx.block() != null)
            {
                CompileBlock(baseCtx.block());
            }
            else if (baseCtx.dynamicArray() != null)
            {
                CompileDynamicArray(baseCtx.dynamicArray());
            }
        }
        
        private void CompileBlock(Smalltalk80Parser.BlockContext block)
        {
            // Blue Book Specification: Blocks compile bytecode INLINE in the method.
            // Pattern:
            //   JUMP_FORWARD <offset>      ; Skip over block body during normal execution
            //   <block bytecode inline>     ; Block body bytecode
            //   RETURN_BLOCK               ; Block return
            //   <after jump lands here>    ; Continue method execution
            //   SEND_SPECIAL_BLOCKCOPY     ; 0xC8 - creates BlockContext with startpc pointing back

            // Parse block arguments to get argument count
            var blockArgs = block.blockArgs();
            int argCount = 0;
            var argumentNames = new List<string>();

            if (blockArgs != null)
            {
                var argNodes = blockArgs.children
                    .OfType<Smalltalk80Parser.ArgumentNameContext>()
                    .Select(ctx => ctx.IDENTIFIER())
                    .ToList();

                argCount = argNodes.Count;
                for (int i = 0; i < argNodes.Count; i++)
                {
                    argumentNames.Add(argNodes[i].GetText());
                }
            }

            // Parse block temporaries for unified temp frame allocation
            // Unified Temp Frame: All temps (method + all blocks) allocated in MethodContext
            // Compiler will allocate these in the unified frame (Phase 5)
            var blockTemps = block.blockTemps();
            int tempCount = 0;
            if (blockTemps != null)
            {
                var tempNodes = (blockTemps.children ?? Enumerable.Empty<IParseTree>())
                    .OfType<Smalltalk80Parser.TemporaryNameContext>()
                    .ToList();
                tempCount = tempNodes.Count;
            }

            // Blue Book inline blocks: Share parent method's environment (same temp frame)
            // Block arguments are added to the EXISTING environment temporarily
            // Save current argument mappings to restore later
            var savedArguments = new Dictionary<string, int>(environment.Arguments);
            int savedArgCount = environment.ArgumentCount;
            int savedScopeDepth = environment.ScopeDepth;
            // Save temporaries dictionary to handle shadowing correctly
            var savedTemporaries = new Dictionary<string, int>(environment.Temporaries);
            // Save OwnArguments and OwnTemporaries to restore after block compilation
            // CRITICAL FIX: Without this, nested blocks lose parent's OwnArguments/OwnTemporaries!
            var savedOwnArguments = new Dictionary<string, int>(environment.LocalArgumentIndices);
            var savedOwnTemporaries = new Dictionary<string, int>(environment.LocalTemporaryIndices);
            // Save cumulative block arg offset to restore after block compilation
            int savedCumulativeBlockArgOffset = environment.CumulativeBlockArgOffset;
            // Save temporary count to restore after block compilation
            // BUG FIX: Block temps should not persist in method's TemporariesAllocatedInFrame after block ends
            int savedTemporariesAllocatedInFrame = environment.TemporariesAllocatedInFrame;
            // Save parent reference to restore after block compilation
            var savedParent = environment.Parent;

            // Save method-level counts if this is the first block (ScopeDepth == 0)
            if (savedScopeDepth == 0)
            {
                environment.MethodArgumentCount = savedArgCount;
                environment.MethodTemporaryCount = environment.TemporariesAllocatedInFrame;
            }

            // Create parent environment snapshot for VariableResolver to traverse
            // This allows CalculateBlockLocalOffset to walk the parent chain
            // CRITICAL: For METHOD scope (ScopeDepth=0), TemporariesAllocatedInFrame should be 0
            // because the offset calculation should NOT include method temps, only block locals
            var parentSnapshot = new CompilationEnvironment
            {
                Type = (savedScopeDepth == 0) ? ScopeType.Method : ScopeType.Block,
                ArgumentCount = savedArgCount,
                TemporariesAllocatedInFrame = (savedScopeDepth == 0) ? 0 : savedTemporariesAllocatedInFrame,
                ScopeDepth = savedScopeDepth,
                Parent = savedParent,
                MethodArgumentCount = environment.MethodArgumentCount,
                MethodTemporaryCount = environment.MethodTemporaryCount,
                CumulativeBlockArgOffset = savedCumulativeBlockArgOffset,
                // Inherit known identifiers from parent scope
                KnownClassNames = environment.KnownClassNames,
                KnownPoolDictionaryNames = environment.KnownPoolDictionaryNames
            };
            // Copy dictionary contents (can't assign to get-only properties)
            foreach (var kvp in savedArguments)
                parentSnapshot.Arguments[kvp.Key] = kvp.Value;
            foreach (var kvp in savedTemporaries)
                parentSnapshot.Temporaries[kvp.Key] = kvp.Value;
            // Copy OwnArguments and OwnTemporaries from SAVED state (parent scope's own variables)
            // CRITICAL FIX: Use savedOwnArguments/savedOwnTemporaries, NOT environment.LocalArgumentIndices/OwnTemporaries
            // The environment dictionaries are about to be cleared and repopulated with THIS block's variables
            foreach (var kvp in savedOwnArguments)
                parentSnapshot.LocalArgumentIndices[kvp.Key] = kvp.Value;
            foreach (var kvp in savedOwnTemporaries)
                parentSnapshot.LocalTemporaryIndices[kvp.Key] = kvp.Value;
            foreach (var kvp in environment.InstanceVariables)
                parentSnapshot.InstanceVariables[kvp.Key] = kvp.Value;
            foreach (var kvp in environment.ClassVariables)
                parentSnapshot.ClassVariables[kvp.Key] = kvp.Value;
            foreach (var kvp in environment.PoolVariables)
                parentSnapshot.PoolVariables[kvp.Key] = kvp.Value;

            environment.Parent = parentSnapshot;

            // Set scope type to Block
            environment.Type = ScopeType.Block;

            // Increment scope depth to indicate we're compiling inside a block
            // This is critical for VariableResolver to correctly identify block variables!
            environment.ScopeDepth = savedScopeDepth + 1;

            // Accumulate block argument count for nested block temp variable offset calculation
            // Example: [:x | [:y | methodTemp]] - inner block needs offset of 2 (x + y)
            environment.CumulativeBlockArgOffset += argCount;

            // Populate OwnArguments with block arguments (0-based, no inheritance)
            // CRITICAL: Always clear OwnArguments, even if block has 0 arguments
            environment.LocalArgumentIndices.Clear();
            for (int i = 0; i < argumentNames.Count; i++)
            {
                environment.LocalArgumentIndices[argumentNames[i]] = i;
            }

            // Add block arguments to current environment (they'll be in BlockContext indexed slots)
            // Maintain backward compatibility
            for (int i = 0; i < argumentNames.Count; i++)
            {
                environment.Arguments[argumentNames[i]] = i;
            }
            environment.ArgumentCount = argCount;

            // CRITICAL: Reset TemporariesAllocatedInFrame to 0 for the block's local temp frame
            // Block temps will be added below, starting from 0
            // This is needed for CalculateBlockLocalOffset to compute the correct offset
            environment.TemporariesAllocatedInFrame = 0;

            // Populate OwnTemporaries with block temps (0-based, no inheritance)
            // CRITICAL: Always clear OwnTemporaries FIRST, even if block has 0 temps
            // BUG FIX: Was only clearing inside `if (blockTemps != null)`, causing parent temps to leak into block scope
            environment.LocalTemporaryIndices.Clear();

            // Parse and register block temporaries in parent environment
            // Block temps are allocated after parent method's temps AND block arguments
            var blockTempNames = new List<string>();
            if (blockTemps != null)
            {
                var tempNodes = (blockTemps.children ?? Enumerable.Empty<IParseTree>())
                    .OfType<Smalltalk80Parser.TemporaryNameContext>()
                    .ToList();

                // Populate OwnTemporaries with block temps (0-based, no inheritance)
                for (int i = 0; i < tempNodes.Count; i++)
                {
                    var tempName = tempNodes[i].GetText();
                    blockTempNames.Add(tempName);  // Track for later cleanup
                    environment.LocalTemporaryIndices[tempName] = i;  // 0-based index
                }

                // Maintain backward compatibility (absolute index)
                // BUG FIX: Block temps start after block args + parent temps
                // Example: [:i | | char | ...] → i=0, char=1 (not char=0!)
                int tempBase = environment.ArgumentCount + environment.TemporariesAllocatedInFrame;
                for (int i = 0; i < tempNodes.Count; i++)
                {
                    var tempName = tempNodes[i].GetText();
                    environment.Temporaries[tempName] = tempBase + i;
                }
                environment.TemporariesAllocatedInFrame += tempNodes.Count;  // Extend parent's temp count
            }

            // Enter block scope in temp frame allocator
            // Determine if block is nested (cannot reuse slots) or sequential (can reuse)
            // Simple heuristic: Blocks nested inside other blocks cannot reuse slots
            bool isNested = (savedScopeDepth > 0);
            allocator.EnterBlockScope(argumentNames, blockTempNames, isNested);

            // BLUE BOOK COMPLIANT: Block arguments are stored via STORE_TEMP bytecodes at block body start
            // Primitive 81 pushes arguments onto BlockContext stack, bytecodes pop them into correct slots
            // This handles non-contiguous slot mappings when block args have same names as method temps

            // Get slot indices for each block argument (needed for STORE_TEMP bytecodes)
            var argSlotIndices = new List<int>();
            foreach (var argName in argumentNames)
            {
                argSlotIndices.Add(allocator.GetSlotIndex(argName));
            }

            // Blue Book bytecode pattern: blockCopy: + LONG_JUMP + block body
            // Primitive 80 calculates initialIP = PC + 3 (skips 3-byte LONG_JUMP)
            // Block body comes AFTER the blockCopy: send and jump

            // STEP 1: Emit blockCopy: send (Blue Book compliant - 1 argument: nargs)
            // NOTE: blockCopy: is a SPECIAL operation - it operates on the active context, NOT a receiver from stack
            // The VM's ExecuteSendSpecial handles it specially WITHOUT popping a receiver
            emitter.PushSmallInteger(argCount); // Only arg: block argument count
            emitter.EmitByte(0xC8); // SEND_SPECIAL_BLOCKCOPY ← Primitive executes here

            // STEP 2: Emit 3-byte LONG_JUMP to skip over block body
            // 0xA1 = long jump forward with 2-byte offset
            int jumpBytecodeOffset = emitter.CurrentOffset;
            emitter.EmitByte(0xA1); // LONG_JUMP_FORWARD (3 bytes total: opcode + 2 offset bytes)
            int jumpOffsetLocation = emitter.CurrentOffset;
            emitter.EmitByte(0x00); // Placeholder for offset high byte
            emitter.EmitByte(0x00); // Placeholder for offset low byte

            // STEP 3: Block body starts here (initialIP = PC + 3 points here)
            int blockStartOffset = emitter.CurrentOffset;

            // BLUE BOOK: Emit POP_STORE_TEMP bytecodes to move arguments from stack to temp slots
            // Arguments are pushed in order (arg0, arg1, arg2...) by Primitive 81
            // Stack is LIFO, so we pop in reverse order (last arg first)
            // Each POP_STORE_TEMP pops from stack and stores to the correct temp slot
            for (int i = argCount - 1; i >= 0; i--)
            {
                emitter.StoreTemporary(argSlotIndices[i]);
            }

            // Compile block body statements
            var statements = block.statements();
            if (statements != null)
            {
                CompileStatements(statements, inBlock: true);
            }
            else
            {
                // Empty block - push nil
                emitter.PushNil();
            }

            // STEP 4: Emit local return bytecode for implicit block returns
            // Implicit block returns (no ^) should return to the caller of value/value:/etc.
            // Use RETURN_TOP (0x7C) which returns to sender without non-local semantics
            // This is different from RETURN_TOP_POP (0x7E) which does non-local returns (^)
            emitter.ReturnTop(); // 0x7C - local return to sender

            // STEP 5: Calculate jump offset and patch it
            // The jump lands here (after the block body), so method execution continues
            int afterBlockOffset = emitter.CurrentOffset;
            int jumpDistance = afterBlockOffset - (jumpOffsetLocation + 2); // Distance from after jump operand to here

            // Patch the jump offset (big-endian 16-bit offset, encoded as distance - 1 per Blue Book)
            int encodedDistance = jumpDistance - 1;  // Blue Book: distance encoded as (distance - 1)
            byte offsetHigh = (byte)((encodedDistance >> 8) & 0xFF);
            byte offsetLow = (byte)(encodedDistance & 0xFF);
            emitter.PatchByteAt(jumpOffsetLocation, offsetHigh);
            emitter.PatchByteAt(jumpOffsetLocation + 1, offsetLow);

            // Exit block scope to allow slot reuse for sequential blocks
            // CRITICAL: Must happen BEFORE restoring environment to ensure proper slot reuse
            allocator.ExitScope();

            // Restore parent environment state
            // Clear block arguments from environment
            foreach (var argName in argumentNames)
            {
                environment.Arguments.Remove(argName);
            }
            // Restore saved arguments
            foreach (var kvp in savedArguments)
            {
                environment.Arguments[kvp.Key] = kvp.Value;
            }
            environment.ArgumentCount = savedArgCount;

            // Restore temporaries dictionary (handles shadowed temps correctly)
            // This fixes bug where block temps shadowing method temps would remove the method temp
            environment.Temporaries.Clear();
            foreach (var kvp in savedTemporaries)
            {
                environment.Temporaries[kvp.Key] = kvp.Value;
            }

            // Restore OwnArguments and OwnTemporaries
            // CRITICAL FIX: Without this, nested blocks lose parent's OwnArguments/OwnTemporaries!
            environment.LocalArgumentIndices.Clear();
            foreach (var kvp in savedOwnArguments)
            {
                environment.LocalArgumentIndices[kvp.Key] = kvp.Value;
            }
            environment.LocalTemporaryIndices.Clear();
            foreach (var kvp in savedOwnTemporaries)
            {
                environment.LocalTemporaryIndices[kvp.Key] = kvp.Value;
            }

            environment.ScopeDepth = savedScopeDepth; // Restore scope depth
            environment.CumulativeBlockArgOffset = savedCumulativeBlockArgOffset; // Restore cumulative offset
            environment.Parent = savedParent; // Restore parent reference

            // BUG FIX: Restore TemporariesAllocatedInFrame after block compilation
            // Block temps should NOT persist in the method's TemporariesAllocatedInFrame after the block ends
            // Otherwise, the method header will have incorrect tempCount, causing wrong bytecode indices
            environment.TemporariesAllocatedInFrame = savedTemporariesAllocatedInFrame;
        }

        /// <summary>
        /// Compiles a dynamic array literal {expr1. expr2. expr3} into bytecode.
        ///
        /// Generates:
        ///   1. Push Array class
        ///   2. Push element count
        ///   3. Send new: (creates array of size N)
        ///   4. For each expression:
        ///      - DUP (duplicate array reference)
        ///      - Evaluate expression
        ///      - Push index (1-based in Smalltalk)
        ///      - Send at:put:
        ///   5. Array reference remains on stack
        /// </summary>
        private void CompileDynamicArray(Smalltalk80Parser.DynamicArrayContext dynArray)
        {
            // Get all expressions in the dynamic array
            var expressions = dynArray.expression();
            int elementCount = expressions?.Length ?? 0;

            // 1. Push Array class (as global variable)
            PushVariable("Array");

            // 2. Push element count
            emitter.PushLiteralConstant(elementCount);

            // 3. Send new: to create array of specified size
            emitter.EmitSend("new:", 1);

            // 4. For each expression, compile and store in array
            if (expressions != null)
            {
                for (int i = 0; i < expressions.Length; i++)
                {
                    // DUP - duplicate array reference on stack
                    // (we need the array ref for at:put: and to leave on stack at end)
                    emitter.Emit(Bytecode.EXTENDED_PREFIX);
                    emitter.Emit((byte)ExtendedSubOp.DUP);

                    // Push 1-based index (Smalltalk arrays are 1-indexed)
                    // MUST push index FIRST (at: index put: value)
                    emitter.PushLiteralConstant(i + 1);

                    // Compile the expression (leaves value on stack)
                    CompileExpression(expressions[i]);

                    // Send at:put: (pops value, index, array ref from stack; pushes value)
                    // Stack order (bottom to top): array, index, value
                    emitter.EmitSend("at:put:", 2);

                    // Pop the returned value (we don't need it, we have the array ref on stack)
                    emitter.Emit(Bytecode.EXTENDED_PREFIX);
                    emitter.Emit((byte)ExtendedSubOp.POP);
                }
            }

            // Array reference remains on stack (ready to be returned or assigned)
        }

        /// <summary>
        /// Parses a Smalltalk number literal, supporting radix notation (e.g., 8r377, 16rFF),
        /// decimal notation, floating point, and scaled decimals.
        /// </summary>
        private bool TryParseSmalltalkNumber(string text, out object? value)
        {
            value = null;

            // Check for radix notation: <radix>r<digits>
            var rIndex = text.IndexOfAny(new[] { 'r', 'R' });
            if (rIndex > 0 && rIndex < text.Length - 1)
            {
                // Extract radix and digits
                var radixPart = text.Substring(0, rIndex);
                var digitsPart = text.Substring(rIndex + 1);

                if (int.TryParse(radixPart, out int radix) && radix >= 2 && radix <= 36)
                {
                    try
                    {
                        // Convert from specified radix to decimal
                        int result = Convert.ToInt32(digitsPart, radix);
                        value = result;
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }

            // Check for scaled decimal: <digits>s<scale>
            var sIndex = text.IndexOf('s');
            if (sIndex > 0 && sIndex < text.Length - 1)
            {
                // For now, treat scaled decimals as regular doubles
                // In a full implementation, you'd preserve the scale
                var intPart = text.Substring(0, sIndex);
                var scalePart = text.Substring(sIndex + 1);
                if (double.TryParse(intPart, out double intVal) && int.TryParse(scalePart, out int scale))
                {
                    value = intVal / Math.Pow(10, scale);
                    return true;
                }
            }

            // Try parsing as regular integer
            if (int.TryParse(text, out int intResult))
            {
                value = intResult;
                return true;
            }

            // Try parsing as floating point
            if (double.TryParse(text, out double floatResult))
            {
                value = floatResult;
                return true;
            }

            return false;
        }

        private void CompileLiteral(Smalltalk80Parser.LiteralContext literal)
        {
            if (literal.numberLiteral() != null)
            {
                var text = literal.numberLiteral().GetText();
                if (TryParseSmalltalkNumber(text, out object? numValue))
                {
                    if (numValue is int intVal)
                    {
                        // Use special bytecodes for small integers
                        switch (intVal)
                        {
                            case -1:
                                emitter.PushMinusOne();
                                break;
                            case 0:
                                emitter.PushZero();
                                break;
                            case 1:
                                emitter.PushOne();
                                break;
                            case 2:
                                emitter.PushTwo();
                                break;
                            default:
                                emitter.PushLiteralConstant(intVal);
                                break;
                        }
                    }
                    else if (numValue is double floatVal)
                    {
                        emitter.PushLiteralConstant(floatVal);
                    }
                }
            }
            // Note: negativeNumber handling removed - grammar may not support it
            // Negative numbers should be handled as unary minus message
            else if (literal.stringConst() != null)
            {
                var text = literal.stringConst().GetText();
                // Remove surrounding quotes and unescape doubled quotes ('' -> ')
                var str = text.Substring(1, text.Length - 2).Replace("''", "'");
                emitter.PushLiteralConstant(str);
            }
            else if (literal.symbolConst() != null)
            {
                var text = literal.symbolConst().GetText();
                var sym = new Symbol(text.Substring(1)); // Remove #
                emitter.PushLiteralConstant(sym);
            }
            else if (literal.characterLiteral() != null)
            {
                var text = literal.characterLiteral().GetText();
                var ch = text[1]; // Get character after $
                emitter.PushLiteralConstant(ch);
            }
            else if (literal.arrayLiteral() != null)
            {
                // Array literals need special handling
                var array = literal.arrayLiteral();
                var elements = new List<object>();
                
                // Process each array element
                foreach (var element in array.arrayElement())
                {
                    elements.Add(ProcessArrayElement(element));
                }
                
                // Push the array as a literal constant
                emitter.PushLiteralConstant(elements.ToArray());
            }
            else if (literal.byteArrayLiteral() != null)
            {
                // Byte array literals
                var byteArray = literal.byteArrayLiteral();
                emitter.PushLiteralConstant(byteArray.GetText());
            }
            else
            {
                var text = literal.GetText();
                if (text == "true")
                {
                    emitter.PushTrue();
                }
                else if (text == "false")
                {
                    emitter.PushFalse();
                }
                else if (text == "nil")
                {
                    emitter.PushNil();
                }
            }
        }
        
        private object ProcessArrayElement(Smalltalk80Parser.ArrayElementContext element)
        {
            // Handle literals
            if (element.literal() != null)
            {
                var lit = element.literal();
                if (lit.numberLiteral() != null)
                {
                    var text = lit.numberLiteral().GetText();

                    // Use TryParseSmalltalkNumber to handle all number formats including radix notation
                    if (TryParseSmalltalkNumber(text, out object? numValue) && numValue != null)
                    {
                        return numValue; // Returns int or double
                    }
                    else
                    {
                        return text; // Keep as string if parsing failed
                    }
                }
                else if (lit.stringConst() != null)
                {
                    var str = lit.stringConst().GetText();
                    // Remove quotes
                    return str.Substring(1, str.Length - 2).Replace("''", "'");
                }
                else if (lit.symbolConst() != null)
                {
                    return lit.symbolConst().GetText();
                }
                else if (lit.characterLiteral() != null)
                {
                    var text = lit.characterLiteral().GetText();
                    return text[1]; // Get character after $
                }
                else if (lit.arrayLiteral() != null)
                {
                    // Nested array
                    var nestedArray = lit.arrayLiteral();
                    var nestedElements = new List<object>();
                    foreach (var nestedElement in nestedArray.arrayElement())
                    {
                        nestedElements.Add(ProcessArrayElement(nestedElement));
                    }
                    return nestedElements.ToArray();
                }
                else if (lit.byteArrayLiteral() != null)
                {
                    return lit.byteArrayLiteral().GetText();
                }
            }
            // Handle negative numbers (MINUS numberLiteral)
            else if (element.MINUS() != null && element.numberLiteral() != null)
            {
                var text = element.numberLiteral().GetText();
                if (TryParseSmalltalkNumber(text, out object? numValue) && numValue != null)
                {
                    // Negate the value
                    if (numValue is int intVal)
                    {
                        return -intVal;
                    }
                    else if (numValue is double doubleVal)
                    {
                        return -doubleVal;
                    }
                }
                return "-" + text; // Fallback
            }
            // Handle symbol names (identifiers in arrays become symbols)
            else if (element.symbolName() != null)
            {
                return "#" + element.symbolName().GetText();
            }
            // Handle nested parenthesized arrays
            else if (element.LEFT_PAREN() != null)
            {
                var nestedElements = new List<object>();
                foreach (var nestedElement in element.arrayElement())
                {
                    nestedElements.Add(ProcessArrayElement(nestedElement));
                }
                return nestedElements.ToArray();
            }
            
            // Default: return the text
            return element.GetText();
        }
        
        private void PushVariable(string name)
        {
            // Use unified variable resolver with temp frame allocator
            var result = resolver.ResolveVariable(name, environment, allocator);

            switch (result.Type)
            {
                case VariableResolver.VariableType.BlockTemporary:
                case VariableResolver.VariableType.ParentVariable:
                case VariableResolver.VariableType.MethodArgument:
                case VariableResolver.VariableType.MethodTemporary:
                    // UNIFIED TEMP FRAME: All temps use absolute indices from allocator
                    emitter.PushTemporary(result.Index);
                    break;

                case VariableResolver.VariableType.InstanceVariable:
                    emitter.PushReceiverVariable(result.Index);
                    break;

                case VariableResolver.VariableType.ClassVariable:
                case VariableResolver.VariableType.PoolVariable:
                case VariableResolver.VariableType.Global:
                    // These are all resolved via literal associations
                    if (result.Type == VariableResolver.VariableType.Global)
                    {
                        // Record undeclared variables for reporting
                        if (currentMethodIdentifier != null)
                        {
                            RecordUndeclaredVariable(name, currentMethodIdentifier);
                        }
                    }
                    // Pass pool dictionary name for pool variables, null for class variables and globals
                    emitter.PushLiteralVariable(name, result.PoolDictionaryName);
                    break;

                case VariableResolver.VariableType.PseudoVariable:
                    // Handle pseudo-variables
                    switch (name)
                    {
                        case "self":
                            emitter.PushSelf();
                            break;
                        case "super":
                            emitter.PushSelf();  // Super uses self as receiver
                            break;
                        case "true":
                            emitter.PushTrue();
                            break;
                        case "false":
                            emitter.PushFalse();
                            break;
                        case "nil":
                            emitter.PushNil();
                            break;
                        case "thisContext":
                            emitter.PushActiveContext();
                            break;
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Unknown variable type: {result.Type} for variable: {name}");
            }
        }

        private void StoreVariable(string name, bool leaveOnStack)
        {
            // Use unified variable resolver with temp frame allocator
            var result = resolver.ResolveVariable(name, environment, allocator);

            // Check assignment legality
            if (!result.IsAssignable)
            {
                throw new InvalidOperationException($"Cannot assign to {result.Type}: {name}");
            }

            // Duplicate stack value if caller wants to leave it on stack
            if (leaveOnStack)
            {
                emitter.Dup();
            }

            switch (result.Type)
            {
                case VariableResolver.VariableType.BlockTemporary:
                case VariableResolver.VariableType.ParentVariable:
                case VariableResolver.VariableType.MethodArgument:
                case VariableResolver.VariableType.MethodTemporary:
                    // UNIFIED TEMP FRAME: All temps use absolute indices from allocator
                    emitter.StoreTemporary(result.Index);
                    break;

                case VariableResolver.VariableType.InstanceVariable:
                    emitter.StoreReceiverVariable(result.Index);
                    break;

                case VariableResolver.VariableType.ClassVariable:
                case VariableResolver.VariableType.PoolVariable:
                case VariableResolver.VariableType.Global:
                    // These are all stored via literal associations
                    if (result.Type == VariableResolver.VariableType.Global)
                    {
                        // Record undeclared variables for reporting
                        RecordUndeclaredVariable(name, currentMethodIdentifier);
                    }
                    // Pass pool dictionary name for pool variables, null for class variables and globals
                    emitter.StoreLiteralVariable(name, result.PoolDictionaryName);
                    break;

                default:
                    throw new InvalidOperationException($"Cannot store to {result.Type}: {name}");
            }
        }
        
        private void EnsureMethodReturn()
        {
            var bytecodes = emitter.GetBytecodes();
            if (bytecodes.Length == 0 || !IsReturnBytecode(bytecodes[^1]))
            {
                // Smalltalk-80 semantics: methods without explicit returns should return self
                // Use ReturnSelf (bytecode 120) which doesn't require anything on the stack
                emitter.ReturnSelf();
            }
        }
        
        private bool IsReturnBytecode(byte bytecode)
        {
            // Blue Book return bytecodes: 0x78-0x7F (120-127)
            // RETURN_SELF, RETURN_TRUE, RETURN_FALSE, RETURN_NIL, RETURN_TOP,
            // RETURN_SELF_POP (block), RETURN_TOP_POP (block), NOP
            return bytecode >= 120 && bytecode <= 127;
        }
        
        private bool IsReceiverSuper(Smalltalk80Parser.MessageBaseContext baseCtx)
        {
            // Check if the receiver is the special variable "super"
            if (baseCtx.variable() != null)
            {
                var varName = baseCtx.variable().GetText();
                return varName == "super";
            }
            return false;
        }

        /// <summary>
        /// Detects if a method is a quick-return pattern (flags=5 or flags=6).
        ///
        /// Quick-return methods are a Blue Book optimization that allows the VM to bypass
        /// full activation creation and directly return a value. This is safe ONLY when ALL
        /// of the following eligibility constraints are satisfied:
        ///
        /// 1. Deterministic single-value result (returns self or instance variable)
        /// 2. Tail-position single send or trivial body (single return statement)
        /// 3. No local temporaries that affect the return
        /// 4. No required stack/frame side-effects
        /// 5. Safe for subclassing/overriding semantics
        /// 6. No interaction with non-local returns or continuations
        /// 7. Bytecode/primitive compatibility (VM supports flags=5/6)
        /// 8. Side-effect free or side-effects irrelevant to return
        ///
        /// See docs/Interpreter-Architecture.md "Quick-Return Flag Eligibility Constraints"
        /// for complete constraint documentation.
        ///
        /// This method implements the detection by analyzing the AST for the canonical patterns:
        /// - flags=5 (quick-return-self): Method body is exactly "^self"
        /// - flags=6 (quick-return-field): Method body is exactly "^instanceVariableName"
        /// </summary>
        /// <returns>
        /// Tuple of (isQuickReturnSelf, quickReturnFieldIndex):
        /// - (true, -1): Method is quick-return-self (flags=5)
        /// - (false, N where N >= 0): Method is quick-return-field (flags=6), returns ivar at index N
        /// - (false, -1): Method is not a quick-return pattern
        /// </returns>
        private (bool isQuickReturnSelf, int quickReturnFieldIndex) DetectQuickReturn(
            Smalltalk80Parser.MethodBodyContext? methodBody,
            Smalltalk80Parser.MethodBodyNoBangContext? methodBodyNoBang)
        {
            // ========================================================================
            // CONSTRAINT ENFORCEMENT: Zero Arguments and Zero Temporaries
            // ========================================================================
            // WHY: Quick-return methods must have no arguments or temporaries because:
            //   - Constraint #3: No local temporaries that affect the return
            //   - Constraint #4: No required stack/frame side-effects
            //   - The VM skips activation record creation for quick-returns
            //   - Any arguments or temps would require a frame, violating the optimization
            //
            // HOW: We reject any method with ArgumentCount != 0 or TemporariesAllocatedInFrame != 0
            //
            // EXAMPLES:
            //   ✓ position  ^position      (0 args, 0 temps)
            //   ✗ at: index  ^array at: index    (1 arg - not eligible)
            //   ✗ compute  | temp |  temp := x. ^temp   (1 temp - not eligible)

            if (environment.ArgumentCount != 0 || environment.TemporariesAllocatedInFrame != 0)
            {
                return (false, -1);
            }

            // ========================================================================
            // CONSTRAINT ENFORCEMENT: Tail-Position Single Send or Trivial Body
            // ========================================================================
            // WHY: Quick-return methods must have exactly one statement (the return) because:
            //   - Constraint #2: Tail-position single send or trivial body
            //   - Constraint #6: No interaction with non-local returns or continuations
            //   - Constraint #8: Side-effect free or side-effects irrelevant to return
            //   - Multiple statements could involve side-effects, blocks, or complex control flow
            //   - The VM needs to safely skip all bytecode execution
            //
            // HOW: We extract the return statement from the method body AST and verify:
            //   1. If methodBody has a direct returnStatement, use it
            //   2. If methodBody has a statements block, ensure it contains exactly 1 statement
            //   3. That single statement must be a returnStatement
            //
            // ANTLR4 GRAMMAR STRUCTURE:
            //   methodBody: statements? returnStatement?
            //   methodBodyNoBang: statements? returnStatement?
            //   statements: statement ('.' statement)*
            //
            // The grammar allows either:
            //   - Direct return: "^self" (returnStatement directly on methodBody)
            //   - Statement block: "^self" (statements block with one statement containing returnStatement)
            //
            // EXAMPLES:
            //   ✓ ^self                              (direct return)
            //   ✓ "comment" ^position                (statement block with 1 return)
            //   ✗ x := 1. ^x                         (2 statements - not eligible)
            //   ✗ self doSomething. ^self            (2 statements - side effect, not eligible)

            Smalltalk80Parser.ReturnStatementContext? returnStmt = null;

            if (methodBody != null)
            {
                // Try direct return statement first (e.g., "^self")
                returnStmt = methodBody.returnStatement();

                if (returnStmt == null)
                {
                    // Check if there's a statements block with a single return statement
                    // This handles cases like: "comment" ^position
                    var statements = methodBody.statements();
                    if (statements != null)
                    {
                        var statementList = statements.statement();
                        // CRITICAL: Must be exactly 1 statement (Constraint #2)
                        if (statementList != null && statementList.Length == 1)
                        {
                            returnStmt = statementList[0].returnStatement();
                        }
                        // If statementList.Length > 1, reject (multiple statements = not eligible)
                    }
                }
            }
            else if (methodBodyNoBang != null)
            {
                // Same logic for methodBodyNoBang variant (methods without trailing '!')
                returnStmt = methodBodyNoBang.returnStatement();

                if (returnStmt == null)
                {
                    var statements = methodBodyNoBang.statements();
                    if (statements != null)
                    {
                        var statementList = statements.statement();
                        // CRITICAL: Must be exactly 1 statement (Constraint #2)
                        if (statementList != null && statementList.Length == 1)
                        {
                            returnStmt = statementList[0].returnStatement();
                        }
                    }
                }
            }
            else
            {
                // No method body at all - not a valid method
                return (false, -1);
            }

            if (returnStmt == null)
            {
                // No return statement found, or multiple statements found
                // Either case violates quick-return constraints
                return (false, -1);
            }

            // ========================================================================
            // CONSTRAINT ENFORCEMENT: Deterministic Single-Value Result
            // ========================================================================
            // WHY: Quick-return methods must return a deterministic value that the VM can
            //      emit directly without executing bytecode:
            //   - Constraint #1: Deterministic single-value result
            //   - Constraint #7: Bytecode/primitive compatibility
            //   - Constraint #8: Side-effect free
            //   - The VM supports only two quick-return types:
            //     * flags=5: Return receiver (self) immediately
            //     * flags=6: Return instance variable at known index immediately
            //
            // HOW: We analyze the return expression to ensure it's one of:
            //   1. No expression ("^" alone) → implicit "^self" (flags=5)
            //   2. Simple variable reference that is "self" (flags=5)
            //   3. Simple variable reference that is an instance variable (flags=6)
            //
            // We REJECT any return expression that involves:
            //   - Assignments (^x := y)
            //   - Message sends (^self doSomething, ^x + 1)
            //   - Cascades (^x ; doSomething)
            //   - Literals (^nil, ^42, ^'string')
            //   - Blocks (^[x])
            //   - Temporaries or arguments (not possible here - already checked)
            //
            // This ensures the return value is:
            //   - Deterministic (always the same for same receiver state)
            //   - Side-effect free (no computation, just a read)
            //   - VM-compatible (matches flags=5 or flags=6 semantics)

            var expr = returnStmt.expression();
            if (expr == null)
            {
                // "^" with no expression is syntactic sugar for "^self"
                // This is the canonical flags=5 pattern
                return (true, -1);
            }

            // ========================================================================
            // AST Navigation: Verify Simple Variable Reference
            // ========================================================================
            // The ANTLR4 grammar structures expressions as:
            //   expression → assignmentExpr → cascadeExpr → messageExpr → binaryExpr → unaryExpr → messageBase
            //
            // For a simple variable reference like "^position", the tree is:
            //   returnStatement
            //     expression
            //       assignmentExpr (no ASSIGN token)
            //         cascadeExpr (no SEMICOLON tokens)
            //           messageExpr (no keywordMessage)
            //             binaryExpr (no binaryMessage nodes)
            //               unaryExpr (no unaryMessage nodes)
            //                 messageBase
            //                   variable → "position"
            //
            // We navigate down this tree and reject at any level where we find:
            //   - Assignment operators (violates constraint #8 - not side-effect free)
            //   - Message sends (violates constraint #1 - not deterministic without execution)
            //   - Cascades (violates constraint #2 - multiple sends)
            //
            // This ensures we only accept the trivial case: ^variableName

            var assignExpr = expr.assignmentExpr();
            if (assignExpr == null || assignExpr.ASSIGN() != null)
            {
                // Reject: Assignment in return expression (e.g., "^x := 5")
                // Violates constraint #8: Side-effect free
                return (false, -1);
            }

            var cascadeExpr = assignExpr.cascadeExpr();
            if (cascadeExpr == null || cascadeExpr.SEMICOLON().Length > 0)
            {
                // Reject: Cascade in return (e.g., "^x doThis; doThat")
                // Violates constraint #2: Tail-position single send
                return (false, -1);
            }

            var messageExpr = cascadeExpr.messageExpr();
            if (messageExpr == null || messageExpr.keywordMessage() != null)
            {
                // Reject: Keyword message send (e.g., "^array at: 1")
                // Violates constraint #1: Not deterministic without frame/execution
                return (false, -1);
            }

            var binaryExpr = messageExpr.binaryExpr();
            if (binaryExpr == null || binaryExpr.binaryMessage().Length > 0)
            {
                // Reject: Binary message send (e.g., "^x + 1")
                // Violates constraint #1: Not deterministic without execution
                return (false, -1);
            }

            var unaryExpr = binaryExpr.unaryExpr();
            if (unaryExpr == null || unaryExpr.unaryMessage().Length > 0)
            {
                // Reject: Unary message send (e.g., "^x negated")
                // Violates constraint #1: Not deterministic without execution
                return (false, -1);
            }

            var messageBase = unaryExpr.messageBase();
            if (messageBase == null)
            {
                // Malformed expression tree
                return (false, -1);
            }

            // Must be a simple variable reference (not a literal, block, or parenthesized expression)
            var variable = messageBase.variable();
            if (variable == null)
            {
                // Reject: Literals (^nil, ^42, ^'string'), blocks (^[]), parenthesized expressions (^(x))
                // Violates constraint #7: VM has no quick-return encoding for these
                return (false, -1);
            }

            var varName = variable.GetText();

            // ========================================================================
            // FINAL VALIDATION: Self or Instance Variable
            // ========================================================================
            // At this point we have a simple variable reference. Now determine which kind:

            if (varName == "self")
            {
                // Quick-return-self (flags=5)
                // Returns: (isQuickReturnSelf=true, quickReturnFieldIndex=-1)
                return (true, -1);
            }

            if (environment.InstanceVariables.TryGetValue(varName, out int ivarIndex))
            {
                // Quick-return-field (flags=6)
                // Returns: (isQuickReturnSelf=false, quickReturnFieldIndex=N)
                // The ivar index will be stored in the method header's numTemps field
                return (false, ivarIndex);
            }

            // Variable is not "self" or an instance variable
            // Could be a class variable, global, pool variable, or undefined
            // These are not supported by quick-return opcodes (Constraint #7)
            // Reject: Not a quick-return pattern
            return (false, -1);
        }

        /// <summary>
        /// Helper class to collect literals in first pass.
        /// </summary>
        private class LiteralCollector : Smalltalk80ParserBaseVisitor<object?>
        {
            private readonly BytecodeEmitter emitter;
            private readonly CompilationEnvironment? environment;
            private readonly BytecodeCompiler compiler;
            
            // Reference to the static SpecialSelectors dictionary
            private static readonly Dictionary<string, (int bytecode, int args)> SpecialSelectors = BytecodeEmitter.SpecialSelectors;
            
            public LiteralCollector(BytecodeCompiler compiler, BytecodeEmitter emitter, CompilationEnvironment? environment = null)
            {
                this.compiler = compiler;
                this.emitter = emitter;
                this.environment = environment;
            }
            
            public override object? VisitAssignmentExpr(Smalltalk80Parser.AssignmentExprContext context)
            {
                // Check if this is an actual assignment
                if (context.variable() != null && context.ASSIGN() != null)
                {
                    // This is an assignment: variable _ expression
                    var varName = context.variable().GetText();
                    
                    // For assignments to global/class variables, we need an Association in the literals
                    // BUT we should not visit the variable node itself (to avoid pushing it)
                    bool needsAssociation = true;
                    if (environment != null)
                    {
                        // Check if it's a local variable (these don't need Associations)
                        if (environment.Arguments.ContainsKey(varName) ||
                            environment.Temporaries.ContainsKey(varName) ||
                            environment.HiddenTemporaries.ContainsKey(varName) ||
                            environment.InstanceVariables.ContainsKey(varName))
                        {
                            needsAssociation = false;
                        }
                    }
                    
                    // Add literal for the store operation (but don't visit the variable node)
                    if (needsAssociation)
                    {
                        // Always use GlobalVariable for global variables
                        var literalValue = new GlobalVariable(new Symbol(varName), null);
                        emitter.AddLiteral(literalValue);
                    }
                    
                    // Now visit only the RHS of the assignment
                    if (context.assignmentExpr() != null)
                    {
                        Visit(context.assignmentExpr());
                    }
                    
                    // Return without visiting the variable node to prevent it from being pushed
                    return null;
                }
                else
                {
                    // Not an assignment, continue normal visiting
                    return base.VisitAssignmentExpr(context);
                }
            }
            
            public override object? VisitUnaryMessage(Smalltalk80Parser.UnaryMessageContext context)
            {
                // CRITICAL: Collect selectors during Phase 1 to establish selector indices
                // BEFORE other literals are added in Phase 2. This ensures selectors occupy
                // indices 0..N-1 and other literals start at index N.
                var selector = context.unarySelector().GetText();
                if (!SpecialSelectors.ContainsKey(selector))
                {
                    emitter.AddLiteral(new Symbol(selector));
                }
                return base.VisitUnaryMessage(context);
            }
            
            public override object? VisitBinaryMessage(Smalltalk80Parser.BinaryMessageContext context)
            {
                // CRITICAL: Collect selectors during Phase 1.
                var selector = context.binarySelector().GetText();
                if (!SpecialSelectors.ContainsKey(selector))
                {
                    emitter.AddLiteral(new Symbol(selector));
                }
                return base.VisitBinaryMessage(context);
            }
            
            public override object? VisitKeywordMessage(Smalltalk80Parser.KeywordMessageContext context)
            {
                // CRITICAL: Collect selectors during Phase 1.
                var parts = context.keywordPart();
                var keywords = new List<string>();
                foreach (var part in parts)
                {
                    keywords.Add(part.IDENTIFIER().GetText() + ":");
                }
                var selector = string.Join("", keywords);
                if (!SpecialSelectors.ContainsKey(selector))
                {
                    emitter.AddLiteral(new Symbol(selector));
                }
                return base.VisitKeywordMessage(context);
            }
            
            public override object? VisitVariable(Smalltalk80Parser.VariableContext context)
            {
                var name = context.GetText();
                
                // Skip special variables
                if (name == "self" || name == "super" || name == "true" ||
                    name == "false" || name == "nil" || name == "thisContext")
                {
                    return base.VisitVariable(context);
                }
                
                // Check if it needs an Association in the literal frame
                // (i.e., it's a class variable, pool variable, or global variable)
                bool needsAssociation = true;
                if (environment != null)
                {
                    // Check if it's a local variable (argument, temporary, or instance variable)
                    // These DON'T need Associations
                    if (environment.Arguments.ContainsKey(name) ||
                        environment.Temporaries.ContainsKey(name) ||
                        environment.HiddenTemporaries.ContainsKey(name) ||
                        environment.InstanceVariables.ContainsKey(name))
                    {
                        needsAssociation = false;
                    }
                }
                
                // If it's a class variable, pool variable, or global variable,
                // add the appropriate literal for PushLiteralVariable
                if (needsAssociation)
                {
                    // Create GlobalVariable literal
                    // Pool variables are handled by VariableResolver during resolution phase
                    object literalValue = new GlobalVariable(new Symbol(name), null);
                    // Console.WriteLine($"[{literalValue.GetType().Name} created] key={name}");
                    emitter.AddLiteral(literalValue);
                }
                
                return base.VisitVariable(context);
            }
            
            public override object? VisitLiteral(Smalltalk80Parser.LiteralContext context)
            {
                // DO NOT collect literals during Phase 1!
                // All literal collection happens during Phase 2 (bytecode generation)
                // to ensure indices match the order bytecode generation encounters them.
                return base.VisitLiteral(context);
            }
            
            public override object? VisitBlock(Smalltalk80Parser.BlockContext context)
            {
                // CRITICAL: Phase 1 MUST visit block contents to collect selectors from
                // message sends inside blocks. Selectors must be collected BEFORE Phase 2
                // adds other literals, to ensure selectors occupy indices 0..N-1.
                //
                // However, Phase 1 does NOT collect literal constants (numbers, strings, etc.)
                // - only selectors. Literal constants are added during Phase 2 (bytecode gen).
                return base.VisitBlock(context); // Visit blocks to find selectors
            }
            
            public override object? VisitNonKeywordExpr(Smalltalk80Parser.NonKeywordExprContext context)
            {
                // This is crucial - nonKeywordExpr contains the arguments to keyword messages
                // including blocks for ifTrue:ifFalse:, etc.
                return base.VisitNonKeywordExpr(context);
            }
            
            public override object? VisitMessageBase(Smalltalk80Parser.MessageBaseContext context)
            {
                // MessageBase can contain blocks, variables, literals, etc.
                // We need to visit all of them
                return base.VisitMessageBase(context);
            }

            public override object? VisitStatements(Smalltalk80Parser.StatementsContext context)
            {
                // Statements contains multiple statement nodes
                // Must traverse all to collect literals from each statement
                return base.VisitStatements(context);
            }

            public override object? VisitStatement(Smalltalk80Parser.StatementContext context)
            {
                // Statement can be either returnStatement or expressionStatement
                // Must traverse to reach the actual expressions
                return base.VisitStatement(context);
            }

            public override object? VisitExpressionStatement(Smalltalk80Parser.ExpressionStatementContext context)
            {
                // ExpressionStatement just wraps an expression
                // Must traverse to reach the expression
                return base.VisitExpressionStatement(context);
            }

            public override object? VisitReturnStatement(Smalltalk80Parser.ReturnStatementContext context)
            {
                // Return statements contain expressions that may have message sends
                // Base visitor will properly traverse the expression tree
                return base.VisitReturnStatement(context);
            }

            public override object? VisitExpression(Smalltalk80Parser.ExpressionContext context)
            {
                // Expression contains assignmentExpr
                // Must traverse down to reach message sends
                return base.VisitExpression(context);
            }

            public override object? VisitCascadeExpr(Smalltalk80Parser.CascadeExprContext context)
            {
                // CascadeExpr contains messageExpr and cascade parts
                // Must traverse to collect all selectors in cascades
                return base.VisitCascadeExpr(context);
            }

            public override object? VisitMessageExpr(Smalltalk80Parser.MessageExprContext context)
            {
                // MessageExpr contains binaryExpr and optional keywordMessage
                // Must traverse to reach the actual message sends
                return base.VisitMessageExpr(context);
            }

            public override object? VisitBinaryExpr(Smalltalk80Parser.BinaryExprContext context)
            {
                // BinaryExpr contains unaryExpr and binary messages
                // Must traverse to collect binary selectors
                return base.VisitBinaryExpr(context);
            }

            public override object? VisitUnaryExpr(Smalltalk80Parser.UnaryExprContext context)
            {
                // UnaryExpr contains messageBase and unary messages
                // Must traverse to collect unary selectors
                return base.VisitUnaryExpr(context);
            }
        }
    }
}