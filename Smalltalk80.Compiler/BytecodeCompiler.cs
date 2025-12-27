using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Smalltalk80.Compiler.Generated;
using Smalltalk80.Compiler.Analysis;
using Smalltalk80.Core.Bytecodes;

namespace Smalltalk80.Compiler
{
    /// <summary>
    /// Result of attempting to inline a conditional message.
    /// Used to determine whether the caller needs to pop the result.
    /// </summary>
    public enum InlineResult
    {
        /// <summary>Message was not inlined - normal send leaves value on stack, caller should pop if not needed.</summary>
        NotInlined,
        /// <summary>Message was inlined and leaves value on stack (e.g., ifTrue:ifFalse:) - caller should pop if not needed.</summary>
        InlinedForValue,
        /// <summary>Message was inlined for effect (e.g., ifTrue: as statement) - no value left, no pop needed.</summary>
        InlinedForEffect
    }

    /// <summary>
    /// Compiles Smalltalk-80 AST nodes to Blue Book bytecode.
    /// Uses visitor pattern to traverse AST and emit bytecodes.
    /// </summary>
    public partial class BytecodeCompiler : IBytecodeCompiler
    {
        private CompilationEnvironment environment = null!;
        private BytecodeEmitter emitter = null!;
        private readonly VariableResolver resolver = new();
        private TempFrameAllocator allocator = null!;
        private string selector = "";
        private int primitiveNumber = 0;
        private bool hasPrimitive = false;
        private string currentMethodIdentifier = "";
        private Dictionary<IParseTree, TailCallInfo>? tailCalls = null;
        private int methodStartLabel = -1;

        private static readonly Dictionary<string, List<string>> _undeclaredVariables = [];

        /// <summary>
        /// Get read-only view of undeclared variables.
        /// </summary>
        public static IReadOnlyDictionary<string, List<string>> GetUndeclaredVariables() => _undeclaredVariables;

        /// <summary>
        /// Clear undeclared variables registry.
        /// </summary>
        public static void ClearUndeclaredVariables() => _undeclaredVariables.Clear();

        private void RecordUndeclaredVariable(string varName, string methodIdentifier)
        {
            if (!_undeclaredVariables.TryGetValue(varName, out var list))
            {
                list = [];
                _undeclaredVariables[varName] = list;
            }
            list.Add(methodIdentifier);
        }

        /// <summary>
        /// Compiles a file-scope expression to bytecode.
        /// </summary>
        public CompiledMethodData CompileExpressionFromTree(
            Smalltalk80Parser.ExpressionContext expression)
        {
            environment = new CompilationEnvironment { Type = ScopeType.Method };
            emitter = new BytecodeEmitter { currentMethodName = "doIt" };
            selector = "doIt";
            primitiveNumber = 0;
            hasPrimitive = false;
            argumentNames.Clear();
            temporaryNames.Clear();

            try
            {
                environment.ArgumentCount = 0;
                environment.TemporariesAllocatedInFrame = 0;

                allocator = new TempFrameAllocator();
                allocator.EnterMethodScope([], []);

                var visitor = new LiteralCollector(this, emitter, environment);
                visitor.Visit(expression);

                CompileExpression(expression);

                var bytecodes = emitter.GetBytecodes();
                if (bytecodes.Length == 0 || !IsReturnBytecode(bytecodes[^1]))
                {
                    emitter.ReturnTop();
                }
            }
            catch (Exception)
            {
                emitter = new BytecodeEmitter();
                emitter.PushNil();
                emitter.ReturnTop();
            }

            byte[] bytecodeArray = emitter.GetBytecodes();
            int tempCount = allocator.GetMaxFrameSize();
            int maxTempIndex = ScanMaxTempIndex(bytecodeArray);
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
                ArgumentNames = [],
                TemporaryNames = [.. temporaryNames],
                VariableSlotMap = allocator.GetAllAllocatedVariables()
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
            options ??= CompilerOptions.Default;

            environment = new CompilationEnvironment { Type = ScopeType.Method };
            emitter = new BytecodeEmitter();
            selector = "";
            primitiveNumber = 0;
            hasPrimitive = false;
            argumentNames.Clear();
            temporaryNames.Clear();
            tailCalls = null;
            methodStartLabel = -1;

            for (int i = 0; i < instanceVars.Count; i++)
            {
                environment.InstanceVariables[instanceVars[i]] = i;
            }

            environment.KnownClassNames = knownClassNames;
            environment.KnownPoolDictionaryNames = knownPoolDictionaryNames;

            if (classVars != null)
            {
                foreach (var kvp in classVars)
                {
                    environment.ClassVariables[kvp.Key] = kvp.Value;
                }
            }

            if (poolVars != null)
            {
                foreach (var kvp in poolVars)
                {
                    environment.PoolVariables[kvp.Key] = kvp.Value;
                }
            }

            try
            {
                ProcessMethodHeader(methodDef.methodHeader());
                currentMethodIdentifier = selector;

                if (methodDef.temporaries() != null)
                {
                    ProcessTemporaries(methodDef.temporaries());
                }

                allocator = new TempFrameAllocator();
                allocator.EnterMethodScope(argumentNames, temporaryNames);

                if (methodDef.primitiveDecl() != null)
                {
                    ProcessPrimitive(methodDef.primitiveDecl());
                }

                if (options.OptimizeTailRecursion)
                {
                    var analyzer = new TailRecursionAnalyzer();
                    if (options.EmitOptimizationComments)
                    {
                        analyzer.EnableDiagnostics();
                    }

                    tailCalls = analyzer.Analyze(methodDef.methodBody(), selector);
                }

                CollectLiterals(methodDef.methodBody());

                if (options.OptimizeTailRecursion && tailCalls != null && tailCalls.Count > 0)
                {
                    methodStartLabel = emitter.MarkMethodStart();
                }

                CompileMethodBody(methodDef.methodBody());
                EnsureMethodReturn();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error compiling method {selector}: {ex.Message}");
                emitter = new BytecodeEmitter();
                emitter.ReturnSelf();
            }

            var (isQuickReturnSelf, quickReturnFieldIndex) = DetectQuickReturn(methodDef.methodBody(), null);
            var bytecodeArray = emitter.GetBytecodes();

            int tempCount = allocator.GetMaxFrameSize();
            int maxTempIndex = ScanMaxTempIndex(bytecodeArray);
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
                ArgumentNames = [.. argumentNames],
                TemporaryNames = [.. temporaryNames],
                IsQuickReturnSelf = isQuickReturnSelf,
                QuickReturnFieldIndex = quickReturnFieldIndex,
                VariableSlotMap = allocator.GetAllAllocatedVariables()
            };

            if (selector == "hasInterned:ifTrue:" && compiledMethod.VariableSlotMap is { Count: > 0 })
            {
                Console.WriteLine($"[VAR SLOTS] {selector}: {string.Join(", ", compiledMethod.VariableSlotMap.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
            }

            return compiledMethod;
        }

        private static int ScanMaxTempIndex(byte[] bytecodeArray)
        {
            int maxTempIndex = -1;
            for (int i = 0; i < bytecodeArray.Length; i++)
            {
                byte bc = bytecodeArray[i];
                if (bc >= 16 && bc <= 31 && (i == 0 || bytecodeArray[i - 1] != 128))
                {
                    int tempIndex = bc - 16;
                    maxTempIndex = Math.Max(maxTempIndex, tempIndex);
                }
            }
            return maxTempIndex;
        }

        private List<string> argumentNames = [];

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
                    environment.LocalArgumentIndices[argName] = 0;
                    environment.Arguments[argName] = 0;
                    environment.ArgumentCount = 1;
                }
            }
            else
            {
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
                        environment.LocalArgumentIndices[argText] = i;
                        environment.Arguments[argText] = i;
                    }
                }

                selector = string.Join("", parts);
                environment.ArgumentCount = args.Length;
            }

            emitter.currentMethodName = selector;
        }

        private List<string> temporaryNames = [];

        private void ProcessTemporaries(Smalltalk80Parser.TemporariesContext temps)
        {
            temporaryNames.Clear();
            var tempNames = temps.temporaryName();
            for (int i = 0; i < tempNames.Length; i++)
            {
                var name = tempNames[i].GetText();
                temporaryNames.Add(name);
                environment.LocalTemporaryIndices[name] = i;
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
            var visitor = new LiteralCollector(this, emitter, environment);
            visitor.Visit(body);
        }

        private void CompileMethodBodyNoBang(Smalltalk80Parser.MethodBodyNoBangContext? body)
        {
            if (body == null)
            {
                emitter.PushSelf();
                return;
            }

            var returnStmt = body.returnStatement();
            if (returnStmt != null)
            {
                CompileReturnStatement(returnStmt);
                return;
            }

            var statements = body.statements();
            if (statements != null)
            {
                CompileStatements(statements);
            }
            else
            {
                emitter.PushSelf();
            }
        }

        private void CompileMethodBody(Smalltalk80Parser.MethodBodyContext? body)
        {
            if (body == null)
            {
                emitter.PushSelf();
                return;
            }

            var returnStmt = body.returnStatement();
            if (returnStmt != null)
            {
                CompileReturnStatement(returnStmt);
                return;
            }

            var statements = body.statements();
            if (statements != null)
            {
                CompileStatements(statements);
            }
            else
            {
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
            var returnStmt = stmt.returnStatement();
            if (returnStmt != null)
            {
                CompileReturnStatement(returnStmt);
                return;
            }

            var exprStmt = stmt.expressionStatement();
            if (exprStmt != null)
            {
                bool leaveOnStack = inBlock && isLast;
                CompileExpression(exprStmt.expression(), leaveOnStack);
            }
        }

        private void CompileReturnStatement(Smalltalk80Parser.ReturnStatementContext returnStmt)
        {
            var expr = returnStmt.expression();
            if (expr != null)
            {
                var pseudoVar = TryGetSimplePseudoVariable(expr);
                if (pseudoVar != null)
                {
                    switch (pseudoVar)
                    {
                        case "self":
                            emitter.ReturnSelf();
                            return;
                        case "true":
                            emitter.ReturnTrue();
                            return;
                        case "false":
                            emitter.ReturnFalse();
                            return;
                        case "nil":
                            emitter.ReturnNil();
                            return;
                    }
                }

                CompileExpression(expr);
            }
            else
            {
                emitter.ReturnSelf();
                return;
            }

            emitter.ReturnTop();
        }

        private string? TryGetSimplePseudoVariable(Smalltalk80Parser.ExpressionContext expr)
        {
            var assignExpr = expr.assignmentExpr();
            if (assignExpr == null) return null;
            if (assignExpr.variable() != null && assignExpr.ASSIGN() != null) return null;

            var cascadeExpr = assignExpr.cascadeExpr();
            if (cascadeExpr == null) return null;

            var cascadeParts = cascadeExpr.messagePart();
            if (cascadeParts is { Length: > 0 }) return null;

            var messageExpr = cascadeExpr.messageExpr();
            if (messageExpr == null) return null;
            if (messageExpr.keywordMessage() != null) return null;

            var binaryExpr = messageExpr.binaryExpr();
            if (binaryExpr == null) return null;

            var binaryMsgs = binaryExpr.binaryMessage();
            if (binaryMsgs is { Length: > 0 }) return null;

            var unaryExpr = binaryExpr.unaryExpr();
            if (unaryExpr == null) return null;

            var unaryMsgs = unaryExpr.unaryMessage();
            if (unaryMsgs is { Length: > 0 }) return null;

            var messageBase = unaryExpr.messageBase();
            if (messageBase == null) return null;

            var variable = messageBase.variable();
            if (variable == null) return null;

            var varName = variable.GetText();

            if (varName is "self" or "true" or "false" or "nil")
            {
                return varName;
            }

            return null;
        }

        private void CompileExpression(Smalltalk80Parser.ExpressionContext expr, bool leaveOnStack = true)
        {
            var assignExpr = expr.assignmentExpr();
            if (assignExpr != null)
            {
                CompileAssignmentExpr(assignExpr, leaveOnStack);
            }
        }

        private void CompileAssignmentExpr(Smalltalk80Parser.AssignmentExprContext expr, bool leaveOnStack = true)
        {
            var variable = expr.variable();

            if (variable != null && expr.ASSIGN() != null)
            {
                var rhsAssignExpr = expr.assignmentExpr();
                if (rhsAssignExpr == null)
                {
                    throw new InvalidOperationException("Assignment expression has no RHS");
                }

                CompileAssignmentExpr(rhsAssignExpr, leaveOnStack: true);
                StoreVariable(variable.GetText(), leaveOnStack: leaveOnStack);
            }
            else
            {
                var cascadeExpr = expr.cascadeExpr();
                if (cascadeExpr != null)
                {
                    var result = CompileCascadeExpr(cascadeExpr, forValue: leaveOnStack);
                    if (!leaveOnStack && result != InlineResult.InlinedForEffect)
                    {
                        emitter.Pop();
                    }
                }
            }
        }

        private void PushVariable(string name)
        {
            var result = resolver.ResolveVariable(name, environment, allocator);

            switch (result.Type)
            {
                case VariableResolver.VariableType.BlockTemporary:
                case VariableResolver.VariableType.ParentVariable:
                case VariableResolver.VariableType.MethodArgument:
                case VariableResolver.VariableType.MethodTemporary:
                    emitter.PushTemporary(result.Index);
                    break;

                case VariableResolver.VariableType.InstanceVariable:
                    emitter.PushReceiverVariable(result.Index);
                    break;

                case VariableResolver.VariableType.ClassVariable:
                case VariableResolver.VariableType.PoolVariable:
                case VariableResolver.VariableType.Global:
                    if (result.Type == VariableResolver.VariableType.Global)
                    {
                        if (currentMethodIdentifier != null)
                        {
                            RecordUndeclaredVariable(name, currentMethodIdentifier);
                        }
                    }
                    emitter.PushLiteralVariable(name, result.PoolDictionaryName);
                    break;

                case VariableResolver.VariableType.PseudoVariable:
                    switch (name)
                    {
                        case "self":
                            emitter.PushSelf();
                            break;
                        case "super":
                            emitter.PushSelf();
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
            var result = resolver.ResolveVariable(name, environment, allocator);

            if (!result.IsAssignable)
            {
                throw new InvalidOperationException($"Cannot assign to {result.Type}: {name}");
            }

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
                    emitter.StoreTemporary(result.Index);
                    break;

                case VariableResolver.VariableType.InstanceVariable:
                    emitter.StoreReceiverVariable(result.Index);
                    break;

                case VariableResolver.VariableType.ClassVariable:
                case VariableResolver.VariableType.PoolVariable:
                case VariableResolver.VariableType.Global:
                    if (result.Type == VariableResolver.VariableType.Global)
                    {
                        RecordUndeclaredVariable(name, currentMethodIdentifier);
                    }
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
                emitter.ReturnSelf();
            }
        }

        private bool IsReturnBytecode(byte bytecode) => bytecode >= 120 && bytecode <= 127;

        private bool IsReceiverSuper(Smalltalk80Parser.MessageBaseContext? baseCtx)
        {
            if (baseCtx?.variable() != null)
            {
                var varName = baseCtx.variable().GetText();
                return varName == "super";
            }
            return false;
        }

        private (bool isQuickReturnSelf, int quickReturnFieldIndex) DetectQuickReturn(
            Smalltalk80Parser.MethodBodyContext? methodBody,
            Smalltalk80Parser.MethodBodyNoBangContext? methodBodyNoBang)
        {
            if (environment.ArgumentCount != 0 || environment.TemporariesAllocatedInFrame != 0)
            {
                return (false, -1);
            }

            Smalltalk80Parser.ReturnStatementContext? returnStmt = null;

            if (methodBody != null)
            {
                returnStmt = methodBody.returnStatement();

                if (returnStmt == null)
                {
                    var statements = methodBody.statements();
                    if (statements != null)
                    {
                        var statementList = statements.statement();
                        if (statementList is { Length: 1 })
                        {
                            returnStmt = statementList[0].returnStatement();
                        }
                    }
                }
            }
            else if (methodBodyNoBang != null)
            {
                returnStmt = methodBodyNoBang.returnStatement();

                if (returnStmt == null)
                {
                    var statements = methodBodyNoBang.statements();
                    if (statements != null)
                    {
                        var statementList = statements.statement();
                        if (statementList is { Length: 1 })
                        {
                            returnStmt = statementList[0].returnStatement();
                        }
                    }
                }
            }
            else
            {
                return (false, -1);
            }

            if (returnStmt == null)
            {
                return (false, -1);
            }

            var expr = returnStmt.expression();
            if (expr == null)
            {
                return (true, -1);
            }

            var assignExpr = expr.assignmentExpr();
            if (assignExpr == null || assignExpr.ASSIGN() != null)
            {
                return (false, -1);
            }

            var cascadeExpr = assignExpr.cascadeExpr();
            if (cascadeExpr == null || cascadeExpr.SEMICOLON().Length > 0)
            {
                return (false, -1);
            }

            var messageExpr = cascadeExpr.messageExpr();
            if (messageExpr == null || messageExpr.keywordMessage() != null)
            {
                return (false, -1);
            }

            var binaryExpr = messageExpr.binaryExpr();
            if (binaryExpr == null || binaryExpr.binaryMessage().Length > 0)
            {
                return (false, -1);
            }

            var unaryExpr = binaryExpr.unaryExpr();
            if (unaryExpr == null || unaryExpr.unaryMessage().Length > 0)
            {
                return (false, -1);
            }

            var messageBase = unaryExpr.messageBase();
            if (messageBase == null)
            {
                return (false, -1);
            }

            var variable = messageBase.variable();
            if (variable == null)
            {
                return (false, -1);
            }

            var varName = variable.GetText();

            if (varName == "self")
            {
                return (true, -1);
            }

            if (environment.InstanceVariables.TryGetValue(varName, out int ivarIndex))
            {
                return (false, ivarIndex);
            }

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

            private static readonly Dictionary<string, (int bytecode, int args)> SpecialSelectors = BytecodeEmitter.SpecialSelectors;

            public LiteralCollector(BytecodeCompiler compiler, BytecodeEmitter emitter, CompilationEnvironment? environment = null)
            {
                this.compiler = compiler;
                this.emitter = emitter;
                this.environment = environment;
            }

            public override object? VisitAssignmentExpr(Smalltalk80Parser.AssignmentExprContext context)
            {
                if (context.variable() != null && context.ASSIGN() != null)
                {
                    if (context.assignmentExpr() != null)
                    {
                        Visit(context.assignmentExpr());
                    }
                    return null;
                }
                else
                {
                    return base.VisitAssignmentExpr(context);
                }
            }

            public override object? VisitUnaryMessage(Smalltalk80Parser.UnaryMessageContext context)
            {
                var selector = context.unarySelector().GetText();
                if (!SpecialSelectors.ContainsKey(selector))
                {
                    emitter.AddLiteral(new Symbol(selector));
                }
                return base.VisitUnaryMessage(context);
            }

            public override object? VisitBinaryMessage(Smalltalk80Parser.BinaryMessageContext context)
            {
                var selector = context.binarySelector().GetText();
                if (!SpecialSelectors.ContainsKey(selector))
                {
                    emitter.AddLiteral(new Symbol(selector));
                }
                return base.VisitBinaryMessage(context);
            }

            public override object? VisitKeywordMessage(Smalltalk80Parser.KeywordMessageContext context)
            {
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

                if (name is "self" or "super" or "true" or "false" or "nil" or "thisContext")
                {
                    return base.VisitVariable(context);
                }

                return base.VisitVariable(context);
            }

            public override object? VisitLiteral(Smalltalk80Parser.LiteralContext context) =>
                base.VisitLiteral(context);

            public override object? VisitBlock(Smalltalk80Parser.BlockContext context) =>
                base.VisitBlock(context);

            public override object? VisitNonKeywordExpr(Smalltalk80Parser.NonKeywordExprContext context) =>
                base.VisitNonKeywordExpr(context);

            public override object? VisitMessageBase(Smalltalk80Parser.MessageBaseContext context) =>
                base.VisitMessageBase(context);

            public override object? VisitStatements(Smalltalk80Parser.StatementsContext context) =>
                base.VisitStatements(context);

            public override object? VisitStatement(Smalltalk80Parser.StatementContext context) =>
                base.VisitStatement(context);

            public override object? VisitExpressionStatement(Smalltalk80Parser.ExpressionStatementContext context) =>
                base.VisitExpressionStatement(context);

            public override object? VisitReturnStatement(Smalltalk80Parser.ReturnStatementContext context) =>
                base.VisitReturnStatement(context);

            public override object? VisitExpression(Smalltalk80Parser.ExpressionContext context) =>
                base.VisitExpression(context);

            public override object? VisitCascadeExpr(Smalltalk80Parser.CascadeExprContext context) =>
                base.VisitCascadeExpr(context);

            public override object? VisitMessageExpr(Smalltalk80Parser.MessageExprContext context) =>
                base.VisitMessageExpr(context);

            public override object? VisitBinaryExpr(Smalltalk80Parser.BinaryExprContext context) =>
                base.VisitBinaryExpr(context);

            public override object? VisitUnaryExpr(Smalltalk80Parser.UnaryExprContext context) =>
                base.VisitUnaryExpr(context);
        }
    }
}
