using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime.Tree;
using Smalltalk80.Generated;

namespace Smalltalk80.Compiler
{
    /// <summary>
    /// Data structure to hold compiled method information.
    /// </summary>
    public class CompiledMethodData
    {
        /// <summary>
        /// The bytecode array for the method.
        /// </summary>
        public byte[] Bytecode { get; set; } = Array.Empty<byte>();
        
        /// <summary>
        /// The literal objects referenced by the method.
        /// </summary>
        public object[] Literals { get; set; } = Array.Empty<object>();
        
        /// <summary>
        /// The number of arguments the method expects.
        /// </summary>
        public int ArgumentCount { get; set; }
        
        /// <summary>
        /// The number of temporary variables in the method.
        /// </summary>
        public int TemporaryCount { get; set; }
        
        /// <summary>
        /// The selector (method name) of the method.
        /// </summary>
        public string Selector { get; set; } = "";
        
        /// <summary>
        /// Whether this method has a primitive declaration.
        /// </summary>
        public bool IsPrimitive { get; set; }
        
        /// <summary>
        /// The primitive number if this is a primitive method.
        /// </summary>
        public int PrimitiveNumber { get; set; }
    }
    
    /// <summary>
    /// Represents a Smalltalk symbol.
    /// </summary>
    public class Symbol
    {
        public string Value { get; }
        
        public Symbol(string value)
        {
            Value = value;
        }
        
        public override bool Equals(object? obj)
        {
            return obj is Symbol other && Value == other.Value;
        }
        
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
        
        public override string ToString()
        {
            return $"#{Value}";
        }
    }

    /// <summary>
    /// Simple bytecode generator for Smalltalk methods.
    /// Generates Blue Book bytecode from parsed Smalltalk method AST.
    /// </summary>
    public class SimpleBytecodeGenerator
    {
        private readonly List<byte> bytecode = new();
        private readonly List<object> literals = new();
        private readonly Dictionary<string, int> temporaryIndices = new();
        private readonly Dictionary<string, int> instanceVarIndices = new();
        private string selector = "";
        private int argCount = 0;
        private int tempCount = 0;
        
        /// <summary>
        /// Compiles a simple method to bytecode.
        /// </summary>
        public CompiledMethodData CompileMethod(
            string methodSource,
            List<string> instanceVars)
        {
            // Reset state
            bytecode.Clear();
            literals.Clear();
            temporaryIndices.Clear();
            argCount = 0;
            tempCount = 0;
            selector = "";
            bool isPrimitive = false;
            int primitiveNumber = 0;
            
            // Set up instance variable indices
            instanceVarIndices.Clear();
            for (int i = 0; i < instanceVars.Count; i++)
            {
                instanceVarIndices[instanceVars[i]] = i;
            }
            
            // Parse the method source
            var inputStream = new Antlr4.Runtime.AntlrInputStream(methodSource);
            var lexer = new Smalltalk80Lexer(inputStream);
            var tokenStream = new Antlr4.Runtime.CommonTokenStream(lexer);
            var parser = new Smalltalk80Parser(tokenStream);
            
            // Parse as method definition
            var methodContext = parser.methodDef();
            
            // Process method header to get selector and arguments
            ProcessMethodHeader(methodContext.methodHeader());
            
            // Process temporaries if present
            var temps = methodContext.temporaries();
            if (temps != null)
            {
                ProcessTemporaries(temps);
            }
            
            // Check for primitive declaration
            var primitiveDecl = methodContext.primitiveDecl();
            if (primitiveDecl != null)
            {
                isPrimitive = true;
                var primitiveText = primitiveDecl.GetText();
                var match = System.Text.RegularExpressions.Regex.Match(primitiveText, @"<primitive:\s*(\d+)>");
                if (match.Success)
                {
                    int.TryParse(match.Groups[1].Value, out primitiveNumber);
                }
            }
            
            // Set up indices for arguments and temporaries
            SetupVariableIndices();
            
            // Process method body
            var body = methodContext.methodBody();
            bool hasMethodBody = HasNonEmptyMethodBody(body);
            
            if (isPrimitive && !hasMethodBody)
            {
                // Primitive-only method: no fallback code, just return self when primitive fails
                bytecode.Add(176); // ReturnReceiver
            }
            else if (hasMethodBody)
            {
                // Regular method or primitive with fallback
                ProcessMethodBody(body);
                
                // Ensure method returns something
                if (bytecode.Count == 0 || !IsReturnBytecode(bytecode[^1]))
                {
                    bytecode.Add(176); // ReturnReceiver (return self)
                }
            }
            else
            {
                // Empty method body - return self
                bytecode.Add(176); // ReturnReceiver
            }
            
            return new CompiledMethodData
            {
                Bytecode = bytecode.ToArray(),
                Literals = literals.ToArray(),
                ArgumentCount = argCount,
                TemporaryCount = tempCount,
                Selector = selector,
                IsPrimitive = isPrimitive,
                PrimitiveNumber = primitiveNumber
            };
        }
        
        /// <summary>
        /// Checks if the method body contains actual code statements.
        /// </summary>
        private bool HasNonEmptyMethodBody(Smalltalk80Parser.MethodBodyContext? body)
        {
            if (body == null) return false;
            
            // Check for return statement
            if (body.returnStatement() != null) return true;
            
            // Check for statements
            var statements = body.statements();
            if (statements?.statement()?.Length > 0) return true;
            
            return false;
        }
        
        private void ProcessMethodHeader(Smalltalk80Parser.MethodHeaderContext header)
        {
            if (header.unarySelector() != null)
            {
                // Unary selector - no arguments
                selector = header.unarySelector().GetText();
                argCount = 0;
            }
            else if (header.binarySelector() != null)
            {
                // Binary selector - one argument
                selector = header.binarySelector().GetText();
                var arg = header.formalArg(0);
                if (arg != null)
                {
                    var argName = arg.GetText();
                    temporaryIndices[argName] = 0;
                    argCount = 1;
                }
            }
            else
            {
                // Keyword selector - multiple arguments
                var keywords = header.identifierColon();
                selector = string.Join("", keywords.Select(k => k.GetText()));
                
                var args = header.formalArg();
                argCount = args.Length;
                for (int i = 0; i < args.Length; i++)
                {
                    var argName = args[i].GetText();
                    temporaryIndices[argName] = i;
                }
            }
        }
        
        private void ProcessTemporaries(Smalltalk80Parser.TemporariesContext temps)
        {
            var tempNames = temps.temporaryName();
            tempCount = tempNames.Length;
            
            // Temporaries come after arguments
            for (int i = 0; i < tempNames.Length; i++)
            {
                var tempName = tempNames[i].GetText();
                temporaryIndices[tempName] = argCount + i;
            }
        }
        
        private void SetupVariableIndices()
        {
            // Already set up in ProcessMethodHeader and ProcessTemporaries
        }
        
        private void ProcessMethodBody(Smalltalk80Parser.MethodBodyContext body)
        {
            // Check for return statement
            var returnStmt = body.returnStatement();
            if (returnStmt != null)
            {
                ProcessReturnStatement(returnStmt);
                return;
            }
            
            // Process regular statements
            var statements = body.statements();
            if (statements != null)
            {
                ProcessStatements(statements);
            }
        }
        
        private void ProcessStatements(Smalltalk80Parser.StatementsContext stmts)
        {
            var statements = stmts.statement();
            foreach (var stmt in statements)
            {
                ProcessStatement(stmt);
            }
        }
        
        private void ProcessStatement(Smalltalk80Parser.StatementContext stmt)
        {
            var returnStmt = stmt.returnStatement();
            if (returnStmt != null)
            {
                ProcessReturnStatement(returnStmt);
                return;
            }
            
            var exprStmt = stmt.expressionStatement();
            if (exprStmt != null)
            {
                ProcessExpression(exprStmt.expression());
            }
        }
        
        private void ProcessReturnStatement(Smalltalk80Parser.ReturnStatementContext returnStmt)
        {
            var expr = returnStmt.expression();
            if (expr != null)
            {
                ProcessExpression(expr);
                bytecode.Add(180); // ReturnTop
            }
            else
            {
                bytecode.Add(176); // ReturnReceiver
            }
        }
        
        private void ProcessExpression(Smalltalk80Parser.ExpressionContext expr)
        {
            // Expression -> assignmentExpr
            var assignExpr = expr.assignmentExpr();
            if (assignExpr != null)
            {
                ProcessAssignmentExpr(assignExpr);
            }
        }
        
        private void ProcessAssignmentExpr(Smalltalk80Parser.AssignmentExprContext expr)
        {
            // Check if it's an assignment
            var variable = expr.variable();
            if (variable != null && expr.ASSIGN() != null)
            {
                // Process the right-hand side first
                ProcessAssignmentExpr(expr.assignmentExpr());
                
                // Store into the variable
                StoreVariable(variable.GetText());
            }
            else
            {
                // It's a cascade expression
                var cascadeExpr = expr.cascadeExpr();
                if (cascadeExpr != null)
                {
                    ProcessCascadeExpr(cascadeExpr);
                }
            }
        }
        
        private void ProcessCascadeExpr(Smalltalk80Parser.CascadeExprContext expr)
        {
            // For now, just process the message expression
            var messageExpr = expr.messageExpr();
            if (messageExpr != null)
            {
                ProcessMessageExpr(messageExpr);
            }
        }
        
        private void ProcessMessageExpr(Smalltalk80Parser.MessageExprContext expr)
        {
            // Process the base (receiver)
            var messageBase = expr.messageBase();
            ProcessMessageBase(messageBase);
            
            // Process unary messages
            var unaryMessages = expr.unaryMessage();
            foreach (var msg in unaryMessages)
            {
                var selector = msg.unarySelector().GetText();
                GenerateSend(selector);
            }
            
            // Process binary messages
            var binaryMessages = expr.binaryMessage();
            foreach (var msg in binaryMessages)
            {
                var selector = msg.binarySelector().GetText();
                ProcessMessageBase(msg.messageBase());
                GenerateSend(selector);
            }
            
            // Process keyword message if present
            var keywordMessage = expr.keywordMessage();
            if (keywordMessage != null)
            {
                var keywordParts = keywordMessage.keywordPart();
                var selector = string.Join("", keywordParts.Select(p => p.IDENTIFIER().GetText() + ":"));
                
                // Process arguments
                foreach (var part in keywordParts)
                {
                    ProcessNonKeywordExpr(part.nonKeywordExpr());
                }
                
                GenerateSend(selector);
            }
        }
        
        private void ProcessMessageBase(Smalltalk80Parser.MessageBaseContext baseCtx)
        {
            // Handle different types of message bases
            if (baseCtx.literal() != null)
            {
                ProcessLiteral(baseCtx.literal());
            }
            else if (baseCtx.variable() != null)
            {
                PushVariable(baseCtx.variable().GetText());
            }
            else if (baseCtx.parenthesizedExpr() != null)
            {
                ProcessExpression(baseCtx.parenthesizedExpr().expression());
            }
            else if (baseCtx.block() != null)
            {
                // Block literals not supported yet
                throw new NotImplementedException("Block literals not yet supported");
            }
        }
        
        private void ProcessNonKeywordExpr(Smalltalk80Parser.NonKeywordExprContext expr)
        {
            // Similar to message expression but without keyword messages
            var messageBase = expr.messageBase();
            ProcessMessageBase(messageBase);
            
            // Process unary messages
            var unaryMessages = expr.unaryMessage();
            foreach (var msg in unaryMessages)
            {
                var selector = msg.unarySelector().GetText();
                GenerateSend(selector);
            }
            
            // Process binary messages
            var binaryMessages = expr.binaryMessage();
            foreach (var msg in binaryMessages)
            {
                var selector = msg.binarySelector().GetText();
                ProcessMessageBase(msg.messageBase());
                GenerateSend(selector);
            }
        }
        
        private void ProcessLiteral(Smalltalk80Parser.LiteralContext literal)
        {
            if (literal.numberLiteral() != null)
            {
                var text = literal.numberLiteral().GetText();
                object value = int.TryParse(text, out int intVal) ? intVal : double.Parse(text);
                PushLiteral(value);
            }
            else if (literal.stringConst() != null)
            {
                var strText = literal.stringConst().GetText();
                // Remove quotes
                var str = strText.Substring(1, strText.Length - 2);
                PushLiteral(str);
            }
            else if (literal.symbolConst() != null)
            {
                var symbolText = literal.symbolConst().GetText();
                // Remove # prefix
                var sym = new Symbol(symbolText.Substring(1));
                PushLiteral(sym);
            }
            else if (literal.GetText() == "true")
            {
                bytecode.Add(177); // Push true (using return true without returning)
            }
            else if (literal.GetText() == "false")
            {
                bytecode.Add(178); // Push false
            }
            else if (literal.GetText() == "nil")
            {
                bytecode.Add(179); // Push nil
            }
        }
        
        private void PushVariable(string varName)
        {
            // Check special variables
            if (varName == "self")
            {
                // In Blue Book, to push self explicitly use pushReceiver (bytecode 0)
                // This pushes the receiver itself onto the stack
                bytecode.Add(0); // PushReceiverVariable0 - but semantically pushes receiver (self)
                return;
            }
            
            if (varName == "true")
            {
                bytecode.Add(177);
                return;
            }
            
            if (varName == "false")
            {
                bytecode.Add(178);
                return;
            }
            
            if (varName == "nil")
            {
                bytecode.Add(179);
                return;
            }
            
            // Check temporaries/arguments
            if (temporaryIndices.TryGetValue(varName, out int tempIndex))
            {
                if (tempIndex <= 15)
                {
                    bytecode.Add((byte)(16 + tempIndex)); // PushTemporaryVariable
                }
                else
                {
                    throw new NotSupportedException($"Temporary index {tempIndex} exceeds limit");
                }
            }
            // Check instance variables
            else if (instanceVarIndices.TryGetValue(varName, out int instIndex))
            {
                if (instIndex <= 15)
                {
                    bytecode.Add((byte)(0 + instIndex)); // PushReceiverVariable
                }
                else
                {
                    throw new NotSupportedException($"Instance variable index {instIndex} exceeds limit");
                }
            }
            // Global variable
            else
            {
                var index = AddLiteralVariable(varName);
                if (index <= 31)
                {
                    bytecode.Add((byte)(64 + index)); // PushLiteralVariable
                }
                else
                {
                    throw new NotSupportedException($"Literal variable index {index} exceeds limit");
                }
            }
        }
        
        private void StoreVariable(string varName)
        {
            // Check temporaries/arguments
            if (temporaryIndices.TryGetValue(varName, out int tempIndex))
            {
                if (tempIndex <= 31)
                {
                    bytecode.Add((byte)(128 + tempIndex)); // StoreIntoTemporaryVariable
                }
                else
                {
                    throw new NotSupportedException($"Temporary index {tempIndex} exceeds limit");
                }
            }
            // Check instance variables
            else if (instanceVarIndices.TryGetValue(varName, out int instIndex))
            {
                if (instIndex <= 31)
                {
                    bytecode.Add((byte)(96 + instIndex)); // StoreIntoReceiverVariable
                }
                else
                {
                    throw new NotSupportedException($"Instance variable index {instIndex} exceeds limit");
                }
            }
            // Global variable
            else
            {
                var index = AddLiteralVariable(varName);
                if (index <= 15)
                {
                    bytecode.Add((byte)(160 + index)); // StoreIntoLiteralVariable
                }
                else
                {
                    throw new NotSupportedException($"Literal variable index {index} exceeds limit for store");
                }
            }
        }
        
        private void PushLiteral(object literal)
        {
            int index = AddLiteral(literal);
            if (index <= 31)
            {
                bytecode.Add((byte)(32 + index)); // PushLiteralConstant
            }
            else
            {
                throw new NotSupportedException($"Literal index {index} exceeds limit");
            }
        }
        
        private void GenerateSend(string selector)
        {
            var selectorSymbol = new Symbol(selector);
            int index = AddLiteral(selectorSymbol);
            
            if (index <= 31)
            {
                bytecode.Add((byte)(192 + index)); // SendMessage
            }
            else
            {
                throw new NotSupportedException($"Selector index {index} exceeds limit");
            }
        }
        
        private int AddLiteral(object literal)
        {
            // Check if literal already exists
            for (int i = 0; i < literals.Count; i++)
            {
                if (literals[i].Equals(literal))
                {
                    return i;
                }
            }
            
            // Add new literal
            literals.Add(literal);
            return literals.Count - 1;
        }
        
        private int AddLiteralVariable(string varName)
        {
            // For global variables, add as symbol
            return AddLiteral(new Symbol(varName));
        }
        
        private bool IsReturnBytecode(byte b)
        {
            return b >= 176 && b <= 180;
        }
    }
}