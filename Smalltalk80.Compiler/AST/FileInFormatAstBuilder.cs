using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Antlr4.Runtime.Misc;
using Smalltalk80.Compiler.Generated;

namespace Smalltalk80.Compiler.AST
{
    /// <summary>
    /// AST builder for Smalltalk-80 file-in format using the new grammar.
    /// Processes the file-out format and builds a complete AST that generates
    /// the expected JSON format for the interpreter.
    /// </summary>
    public class FileInFormatAstBuilder : Smalltalk80ParserBaseVisitor<object?>
    {
        private readonly ITokenStream _tokenStream;
        private readonly CompilationResult _result;
        private string? _currentClass;
        private bool _isMetaclass;
        private bool _collectingClassesOnly = false; // Two-pass compilation: pass 1 = collect classes, pass 2 = compile methods
        private string _currentCategory = "";
        private readonly Dictionary<string, ClassDefinitionNode> _classes;
        private readonly List<InitializationExpression> _initExpressions;
        private readonly BytecodeCompiler _bytecodeCompiler;
        
        public FileInFormatAstBuilder(ITokenStream tokenStream)
        {
            _tokenStream = tokenStream;
            _result = new CompilationResult
            {
                Schema = "./bootstrap-schema.json",
                Description = $"Compiled from source on {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
            };
            _classes = new Dictionary<string, ClassDefinitionNode>();
            _initExpressions = new List<InitializationExpression>();
            _bytecodeCompiler = new BytecodeCompiler();
        }

        public CompilationResult GetResult() => _result;

        public override object? VisitFileIn(Smalltalk80Parser.FileInContext context)
        {
            // TWO-PASS COMPILATION:
            // Pass 1: Collect all class definitions (populate _classes dictionary)
            // This ensures all classes are known before we compile any methods
            Console.WriteLine("[AST] Pass 1: Collecting class definitions...");
            _collectingClassesOnly = true;
            foreach (var chunk in context.chunk())
            {
                Visit(chunk);
            }
            Console.WriteLine($"[AST] Pass 1 complete: {_classes.Count} classes collected");

            // Pass 2: Compile all methods
            // Now that all classes are in _classes, ComputeInheritedInstanceVariables will work correctly
            Console.WriteLine("[AST] Pass 2: Compiling methods...");
            _collectingClassesOnly = false;
            foreach (var chunk in context.chunk())
            {
                Visit(chunk);
            }
            var totalMethods = _classes.Values.Sum(c => c.Methods.Count + c.ClassMethods.Count);
            Console.WriteLine($"[AST] Pass 2 complete: {totalMethods} methods compiled");

            // Build the final result
            _result.Classes = _classes.Values.ToList();

            // Compute total instance variable count for all classes
            // Must be done AFTER all classes are loaded so inheritance chains are complete
            foreach (var classNode in _result.Classes)
            {
                classNode.TotalInstanceVariableCount = ComputeInheritedInstanceVariables(classNode.Name).Count;
            }

            // Deduplicate initialization expressions based on source text
            // The Smalltalk-80.st file contains two complete copies of the system concatenated together
            // (194 out of 217 classes are defined twice), so we need to deduplicate
            var uniqueExpressions = new Dictionary<string, InitializationExpression>();
            foreach (var expr in _initExpressions)
            {
                // Use the source text as the key for deduplication
                var key = expr.Source ?? "";
                if (!uniqueExpressions.ContainsKey(key))
                {
                    uniqueExpressions[key] = expr;
                }
            }
            _result.InitializationExpressions = uniqueExpressions.Values.ToList();
            
            if (_initExpressions.Count > uniqueExpressions.Count)
            {
                Console.WriteLine($"[AST] Deduplicated initialization expressions: {_initExpressions.Count} -> {uniqueExpressions.Count} (removed {_initExpressions.Count - uniqueExpressions.Count} duplicates)");
            }
            
            return _result;
        }

        public override object? VisitClassDefChunk(Smalltalk80Parser.ClassDefChunkContext context)
        {
            Console.WriteLine("[AST] DEBUG: VisitClassDefChunk called");
            var classHeader = context.classHeader();
            var classBody = context.classBody();

            // Extract class name and superclass
            string className = "";
            string superclassName = "Object";
            string subclassKind = "subclass:";  // Default to fixed pointer

            // Get superclass name from superclassName()
            if (classHeader.superclassName() != null)
            {
                superclassName = classHeader.superclassName().IDENTIFIER().GetText();
            }

            // Get subclass kind from subclassKind()
            if (classHeader.subclassKind() != null)
            {
                var kindContext = classHeader.subclassKind();
                if (kindContext.identifierColon() != null)
                {
                    subclassKind = kindContext.identifierColon().GetText();
                }
            }

            // Get actual class name from symbolConst
            if (classHeader.symbolConst() != null)
            {
                className = ExtractSymbolValue(classHeader.symbolConst());
            }

            // Create class definition node
            var classNode = new ClassDefinitionNode
            {
                Name = className,
                Superclass = superclassName,
                SubclassKind = subclassKind,
                InstanceVariables = new List<string>(),
                ClassVariables = new List<string>(),
                PoolDictionaries = new List<string>(),
                Category = "",
                Comment = null,
                Methods = new List<MethodDefinitionNode>(),
                ClassMethods = new List<MethodDefinitionNode>()
            };

            // Process class body specifications
            if (classBody != null)
            {
                foreach (var spec in classBody.classSlotSpec())
                {
                    var specType = spec.identifierColon().GetText();
                    
                    switch (specType)
                    {
                        case "instanceVariableNames:":
                            if (spec.stringConst() != null)
                            {
                                var varNames = StripQuotes(spec.stringConst().STRING().GetText());
                                if (!string.IsNullOrWhiteSpace(varNames))
                                {
                                    classNode.InstanceVariables = varNames.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                                }
                            }
                            break;
                        case "classVariableNames:":
                            if (spec.stringConst() != null)
                            {
                                var varNames = StripQuotes(spec.stringConst().STRING().GetText());
                                if (!string.IsNullOrWhiteSpace(varNames))
                                {
                                    classNode.ClassVariables = varNames.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                                }
                            }
                            break;
                        case "poolDictionaries:":
                            if (spec.stringConst() != null)
                            {
                                var rawStringConst = spec.stringConst().STRING()?.GetText();
                                var poolNames = StripQuotes(rawStringConst ?? "");
                                if (!string.IsNullOrWhiteSpace(poolNames))
                                {
                                    classNode.PoolDictionaries = poolNames.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                                }
                            }
                            break;
                        case "category:":
                            if (spec.stringConst() != null)
                            {
                                classNode.Category = StripQuotes(spec.stringConst().STRING().GetText());
                            }
                            break;
                    }
                }
            }

            // Store the class
            _classes[className] = classNode;
            _currentClass = className;
            _isMetaclass = false;

            // Note: TotalInstanceVariableCount is computed later in VisitFileIn()
            // after all classes are loaded, so inheritance chains are complete

            return classNode;
        }

        public override object? VisitMethodCategoryChunk(Smalltalk80Parser.MethodCategoryChunkContext context)
        {
            Console.WriteLine($"[AST] DEBUG: VisitMethodCategoryChunk called, collectingClassesOnly={_collectingClassesOnly}");
            
            // Skip method compilation during pass 1 (class collection phase)
            if (_collectingClassesOnly)
                return null;

            // Extract the class name from the chunk - this is critical!
            // The grammar has: className (cls = IDENTIFIER)?
            if (context.className() != null)
            {
                var classNameNode = context.className();
                if (classNameNode.IDENTIFIER() != null)
                {
                    _currentClass = classNameNode.IDENTIFIER().GetText();
                }
            }

            // Only proceed if we have a valid current class
            if (_currentClass == null || !_classes.ContainsKey(_currentClass))
                return null;
            
            // Check if this is a class-side method category using the cls token
            bool isClassSide = context.cls != null;
            
            // DEBUG: Report class-side method detection
            if (isClassSide)
            {
                Console.WriteLine($"[AST] Found class-side method category: {_currentClass} class methodsFor: '{_currentCategory}'");
            }
            
            // Set the metaclass flag for this entire category
            _isMetaclass = isClassSide;

            // Extract and set current category
            if (context.stringConst() != null)
            {
                _currentCategory = StripQuotes(context.stringConst().STRING().GetText());
            }
            else
            {
                _currentCategory = "";
            }

            // Process all methods in this category - use methodDef() array
            foreach (var methodDef in context.methodDef())
            {
                var method = ProcessMethodDefinition(methodDef);

                if (method != null)
                {
                    var classNode = _classes[_currentClass];
                    if (_isMetaclass)
                    {
                        classNode.ClassMethods.Add(method);
                    }
                    else
                    {
                        classNode.Methods.Add(method);
                    }
                }
            }

            // Reset metaclass flag and category after processing this category
            _isMetaclass = false;
            _currentCategory = "";

            return null;
        }

        public override object? VisitClassCommentChunk(Smalltalk80Parser.ClassCommentChunkContext context)
        {
            // Extract the class name from the chunk
            if (context.className() != null)
            {
                var classNameNode = context.className();
                if (classNameNode.IDENTIFIER() != null)
                {
                    _currentClass = classNameNode.IDENTIFIER().GetText();
                }
            }
            
            if (_currentClass != null && _classes.TryGetValue(_currentClass, out var classNode))
            {
                if (context.stringConst() != null)
                {
                    classNode.Comment = StripQuotes(context.stringConst().STRING().GetText());
                }
            }
            return null;
        }

        public override object? VisitMetaclassDefinitionChunk(Smalltalk80Parser.MetaclassDefinitionChunkContext context)
        {
            // Use className() instead of symbolConst()
            if (context.className() != null)
            {
                var className = context.className().IDENTIFIER().GetText();
                if (_classes.ContainsKey(className))
                {
                    _currentClass = className;
                    // Note: We set _isMetaclass = true, but it should be reset
                    // by the next chunk that processes methods (VisitMethodCategoryChunk)
                    // This is just metadata about the class, not a method category
                    _isMetaclass = false; // Reset immediately since this isn't a method category
                }
            }
            return null;
        }

        public override object? VisitExpressionChunk(Smalltalk80Parser.ExpressionChunkContext context)
        {
            Console.WriteLine($"[AST] DEBUG: VisitExpressionChunk called with: {context.GetText()}");
            // Skip init expression compilation during pass 1 (class collection phase)
            if (_collectingClassesOnly)
                return null;

            // ALL expression chunks are executable code that should be evaluated during file-in
            // This includes "ClassName comment: '...'" which sends the comment: message to store class documentation

            var sourceText = GetOriginalText(context.expression());
            
            // Extract receiver and selector info for documentation
            var (receiver, selector) = ExtractReceiverAndSelector(context.expression());
            
            // For better display, format the source with proper spacing if we have receiver and selector
            if (!string.IsNullOrEmpty(receiver) && !string.IsNullOrEmpty(selector))
            {
                // Format as "Receiver selector" or "Receiver selector: argument"
                // This makes the display cleaner than the raw source which may not have spaces
                sourceText = $"{receiver} {selector}";
                // If the source text contains the actual arguments, append them
                var originalText = GetOriginalText(context.expression());
                var selectorEnd = originalText.IndexOf(selector) + selector.Length;
                if (selectorEnd < originalText.Length && originalText[selectorEnd] == '\'')
                {
                    // There's an argument after the selector, include it
                    sourceText = $"{receiver} {originalText.Substring(originalText.IndexOf(selector))}";
                }
            }
            
            // Compile the expression to bytecode
            var compiledData = _bytecodeCompiler.CompileExpressionFromTree(context.expression());
            
            // Calculate stack depth
            var stackDepth = BytecodeEmitter.CalculateMaxStackDepth(compiledData.Bytecode);
            
            _initExpressions.Add(new InitializationExpression
            {
                Bytecode = compiledData.Bytecode.Select(b => (int)b).ToList(),
                Literals = ConvertLiterals(compiledData.Literals),
                NumTemps = compiledData.TemporaryCount,
                StackDepth = stackDepth,
                Source = sourceText,
                Receiver = receiver,
                Selector = selector
            });
            
            return null;
        }

        private MethodDefinitionNode? ProcessMethodDefinition(Smalltalk80Parser.MethodDefContext context)
        {
            if (_currentClass == null || !_classes.ContainsKey(_currentClass))
                return null;

            var classNode = _classes[_currentClass];

            // Compute inherited instance variables
            // Two-pass compilation ensures all classes are parsed before we compile any methods,
            // so ComputeInheritedInstanceVariables will work correctly for both instances and metaclasses
            var instanceVars = ComputeInheritedInstanceVariables(_currentClass);

            // Get class and pool variables
            var classVars = GetClassVariables(classNode);
            
            // For class methods, pool variables are inherited from the BASE class (not the metaclass)
            // Pool dictionaries are declared on the instance-side class definition, not the metaclass
            ClassDefinitionNode poolNode = classNode;
            if (_isMetaclass)
            {
                // For class methods, look up pool variables from the instance-side class
                // _currentClass is the base class name (e.g., "Text")
                // classNode is the metaclass (has no pool dictionaries)
                if (_classes.ContainsKey(_currentClass))
                {
                    poolNode = _classes[_currentClass];
                    Console.WriteLine($"[POOL DEBUG] Class method in '{_currentClass}', using instance-side class for pools");
                }
            }
            var poolVars = GetPoolVariables(poolNode);
            Console.WriteLine($"[POOL DEBUG] Pool vars for {_currentClass}: {poolVars.Count} dictionaries");

            // Compile the method using BytecodeCompiler
            var compiledData = _bytecodeCompiler.CompileMethodFromTree(
                context,
                instanceVars,
                classVars,
                poolVars);

            // Calculate stack depth
            var stackDepth = BytecodeEmitter.CalculateMaxStackDepth(compiledData.Bytecode);

            // Get the source text for the method with proper spacing
            var start = context.Start;
            var stop = context.Stop;
            var charStream = start.InputStream;
            var charStart = start.StartIndex;
            var charStop = stop?.StopIndex ?? charStart;
            var sourceText = charStream.GetText(new Interval(charStart, charStop));
            // Create method definition with the expected format
            var method = new MethodDefinitionNode
            {
                Class = _isMetaclass ? $"{_currentClass} class" : _currentClass,
                Selector = compiledData.Selector ?? string.Empty,
                Source = sourceText,
                Bytecode = compiledData.Bytecode.Select(b => (int)b).ToList(),
                Literals = ConvertLiterals(compiledData.Literals),
                PrimitiveIndex = 0, // Will be updated if method has primitive
                NumArgs = compiledData.ArgumentCount,
                NumTemps = compiledData.TemporaryCount,
                StackDepth = stackDepth,
                IsClassMethod = _isMetaclass,
                ArgNames = compiledData.ArgumentNames,
                TempNames = compiledData.TemporaryNames,
                IsQuickReturnSelf = compiledData.IsQuickReturnSelf,
                QuickReturnFieldIndex = compiledData.QuickReturnFieldIndex,
                Category = _currentCategory  // Assign the current category
            };

            // Check for primitive declaration
            var prim = context.primitiveDecl();
            if (prim != null)
            {
                var primitiveText = prim.PRIMITIVE_DECL().GetText();
                var match = System.Text.RegularExpressions.Regex.Match(primitiveText, @"<primitive:\s*(\d+)\s*>");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int primNumber))
                {
                    method.PrimitiveIndex = primNumber;
                }
            }

            return method;
        }

        private ExpressionNode? ProcessExpression(Smalltalk80Parser.ExpressionContext context)
        {
            if (context.assignmentExpr() != null)
            {
                return ProcessAssignmentExpression(context.assignmentExpr());
            }
            return null;
        }

        private ExpressionNode ProcessAssignmentExpression(Smalltalk80Parser.AssignmentExprContext context)
        {
            if (context.variable() != null)
            {
                var variable = context.variable().IDENTIFIER().GetText();
                var value = ProcessAssignmentExpression(context.assignmentExpr());
                
                return new AssignmentExpressionNode
                {
                    Variable = variable,
                    Value = value
                };
            }
            else if (context.cascadeExpr() != null)
            {
                // Return ExpressionNode, not AssignmentExpressionNode
                return ProcessCascadeExpression(context.cascadeExpr());
            }
            
            return new AssignmentExpressionNode();
        }

        private ExpressionNode ProcessCascadeExpression(Smalltalk80Parser.CascadeExprContext context)
        {
            var messageExpr = context.messageExpr();
            
            if (context.messagePart().Length == 0)
            {
                // No cascade, just a message expression
                return ProcessMessageExpression(messageExpr);
            }
            
            // Has cascade
            var receiver = ProcessMessageExpression(messageExpr);
            var messages = new List<MessageNode>();

            foreach (var messagePart in context.messagePart())
            {
                messages.Add(ProcessMessagePart(messagePart));
            }

            return new CascadeExpressionNode
            {
                Receiver = receiver,
                Messages = messages
            };
        }

        private ExpressionNode ProcessMessageExpression(Smalltalk80Parser.MessageExprContext context)
        {
            // New grammar: messageExpr = binaryExpr keywordMessage?
            var binaryExpr = context.binaryExpr();
            if (binaryExpr == null) throw new InvalidOperationException("MessageExpr must have binaryExpr");

            var baseExpr = ProcessBinaryExpression(binaryExpr);

            // Process keyword message if present
            var keywordMsg = context.keywordMessage();
            if (keywordMsg != null)
            {
                baseExpr = ProcessKeywordMessage(baseExpr, keywordMsg);
            }

            return baseExpr;
        }

        private ExpressionNode ProcessBinaryExpression(Smalltalk80Parser.BinaryExprContext context)
        {
            // Grammar: binaryExpr = unaryExpr (binaryMessage)*
            var unaryExpr = context.unaryExpr();
            if (unaryExpr == null) throw new InvalidOperationException("BinaryExpr must have unaryExpr");

            var baseExpr = ProcessUnaryExpression(unaryExpr);

            // Process binary messages
            foreach (var binaryMsg in context.binaryMessage())
            {
                baseExpr = new MessageExpressionNode
                {
                    Receiver = baseExpr,
                    Message = new BinaryMessageNode
                    {
                        Selector = binaryMsg.binarySelector().GetText(),
                        // Binary message argument is a unaryExpr
                        Argument = ProcessUnaryExpression(binaryMsg.unaryExpr())
                    }
                };
            }

            return baseExpr;
        }

        private ExpressionNode ProcessUnaryExpression(Smalltalk80Parser.UnaryExprContext context)
        {
            // Grammar: unaryExpr = messageBase unaryMessage*
            var messageBase = context.messageBase();
            if (messageBase == null) throw new InvalidOperationException("UnaryExpr must have messageBase");

            var baseExpr = ProcessMessageBase(messageBase);

            // Process unary messages
            foreach (var unaryMsg in context.unaryMessage())
            {
                baseExpr = new MessageExpressionNode
                {
                    Receiver = baseExpr,
                    Message = new UnaryMessageNode
                    {
                        Selector = unaryMsg.unarySelector().IDENTIFIER().GetText()
                    }
                };
            }

            return baseExpr;
        }

        private ExpressionNode ProcessMessageBase(Smalltalk80Parser.MessageBaseContext context)
        {
            if (context.literal() != null)
            {
                return ProcessLiteral(context.literal());
            }
            else if (context.variable() != null)
            {
                return new VariableNode { Name = context.variable().IDENTIFIER().GetText() };
            }
            else if (context.block() != null)
            {
                return ProcessBlock(context.block());
            }
            else if (context.parenthesizedExpr() != null)
            {
                var expr = ProcessExpression(context.parenthesizedExpr().expression());
                return expr ?? new VariableNode { Name = "nil" };
            }
            else if (context.dynamicArray() != null)
            {
                return ProcessDynamicArray(context.dynamicArray());
            }
            else if (context.negativeNumber() != null)
            {
                // Handle negative number
                var base_ = ProcessMessageBase(context.negativeNumber().messageBase());
                if (base_ is IntegerLiteralNode intNode)
                {
                    intNode.Value = -intNode.Value;
                    return intNode;
                }
                else if (base_ is FloatLiteralNode floatNode)
                {
                    floatNode.Value = -floatNode.Value;
                    return floatNode;
                }
                return base_;
            }
            
            throw new InvalidOperationException("Unknown message base type");
        }

        private ExpressionNode ProcessDynamicArray(Smalltalk80Parser.DynamicArrayContext context)
        {
            var elements = new List<ExpressionNode>();
            
            foreach (var expr in context.expression())
            {
                var element = ProcessExpression(expr);
                if (element != null)
                {
                    elements.Add(element);
                }
            }
            
            return new DynamicArrayNode { Elements = elements };
        }

        private LiteralNode ProcessLiteral(Smalltalk80Parser.LiteralContext context)
        {
            if (context.numberLiteral() != null)
            {
                var text = context.numberLiteral().NUMBER().GetText();
                if (text.Contains('.') || text.Contains('e') || text.Contains('E'))
                {
                    return new FloatLiteralNode { Value = double.Parse(text) };
                }
                else if (text.Contains('r'))
                {
                    // Handle radix notation
                    var parts = text.Split('r');
                    var radix = int.Parse(parts[0]);
                    var value = Convert.ToInt32(parts[1], radix);
                    return new IntegerLiteralNode { Value = value };
                }
                else
                {
                    return new IntegerLiteralNode { Value = int.Parse(text) };
                }
            }
            else if (context.stringConst() != null)
            {
                return new StringLiteralNode { Value = StripQuotes(context.stringConst().STRING().GetText()) };
            }
            else if (context.symbolConst() != null)
            {
                return ProcessSymbol(context.symbolConst());
            }
            else if (context.characterLiteral() != null)
            {
                var text = context.characterLiteral().CHAR_LITERAL().GetText();
                return new CharacterLiteralNode { Value = text[1] }; // Skip the $
            }
            else if (context.arrayLiteral() != null)
            {
                return ProcessArrayLiteral(context.arrayLiteral());
            }
            else if (context.byteArrayLiteral() != null)
            {
                return ProcessByteArrayLiteral(context.byteArrayLiteral());
            }

            throw new InvalidOperationException("Unknown literal type");
        }

        private SymbolLiteralNode ProcessSymbolName(Smalltalk80Parser.SymbolNameContext symbolName)
        {
            string value;
            
            if (symbolName.IDENTIFIER() != null)
            {
                value = symbolName.IDENTIFIER().GetText();
            }
            else if (symbolName.unarySelector() != null)
            {
                value = symbolName.unarySelector().GetText();
            }
            else if (symbolName.binarySelector() != null)
            {
                value = symbolName.binarySelector().GetText();
            }
            else if (symbolName.SYMBOL_KEYWORD_SELECTOR() != null)
            {
                // SYMBOL_KEYWORD_SELECTOR token from lexer mode contains complete text (e.g., "at:put:", "quo:")
                value = symbolName.SYMBOL_KEYWORD_SELECTOR().GetText();
            }
            else if (symbolName.keywordSymbolInArray() != null)
            {
                // Keyword symbol in array context - reconstruct from IDENTIFIER COLON pairs
                var identifiers = symbolName.keywordSymbolInArray().IDENTIFIER();
                var colons = symbolName.keywordSymbolInArray().COLON();
                var parts = new List<string>();
                for (int i = 0; i < identifiers.Length; i++)
                {
                    parts.Add(identifiers[i].GetText());
                    if (i < colons.Length)
                        parts.Add(colons[i].GetText());
                }
                value = string.Join("", parts);
            }
            else
            {
                throw new InvalidOperationException("Unknown symbol name type");
            }

            return new SymbolLiteralNode { Value = value };
        }

        private SymbolLiteralNode ProcessSymbol(Smalltalk80Parser.SymbolConstContext context)
        {
            if (context.QUOTED_SYMBOL() != null)
            {
                // Handle quoted symbol like #'at:put:'
                var text = context.QUOTED_SYMBOL().GetText();
                // Remove #' prefix and ' suffix
                var value = text.Substring(2, text.Length - 3);
                return new SymbolLiteralNode { Value = value };
            }
            else if (context.symbolName() != null)
            {
                return ProcessSymbolName(context.symbolName());
            }
            else
            {
                throw new InvalidOperationException("Unknown symbol type");
            }
        }

        private ArrayLiteralNode ProcessArrayLiteral(Smalltalk80Parser.ArrayLiteralContext context)
        {
            var elements = new List<LiteralNode>();
            
            foreach (var element in context.arrayElement())
            {
                if (element.literal() != null)
                {
                    elements.Add(ProcessLiteral(element.literal()));
                }
                else if (element.symbolName() != null)
                {
                    elements.Add(ProcessSymbolName(element.symbolName()));
                }
                else if (element.LEFT_PAREN() != null)
                {
                    // Nested array
                    var nestedElements = new List<LiteralNode>();
                    foreach (var nested in element.arrayElement())
                    {
                        // Recursively process nested elements
                        if (nested.literal() != null)
                        {
                            nestedElements.Add(ProcessLiteral(nested.literal()));
                        }
                        else if (nested.symbolName() != null)
                        {
                            nestedElements.Add(ProcessSymbolName(nested.symbolName()));
                        }
                    }
                    elements.Add(new ArrayLiteralNode { Elements = nestedElements });
                }
            }

            return new ArrayLiteralNode { Elements = elements };
        }

        private ByteArrayLiteralNode ProcessByteArrayLiteral(Smalltalk80Parser.ByteArrayLiteralContext context)
        {
            var bytes = new List<byte>();
            
            // Use NUMBER() token array directly
            foreach (var num in context.NUMBER())
            {
                bytes.Add(byte.Parse(num.GetText()));
            }

            return new ByteArrayLiteralNode { Bytes = bytes };
        }

        private BlockNode ProcessBlock(Smalltalk80Parser.BlockContext context)
        {
            var block = new BlockNode
            {
                Parameters = new List<string>(),
                Temporaries = new List<string>(),
                Statements = new List<StatementNode>()
            };

            // Process block parameters
            if (context.blockArgs() != null)
            {
                block.Parameters = context.blockArgs().argumentName()
                    .Select(arg => arg.IDENTIFIER().GetText())
                    .ToList();
            }

            // Process temporaries
            if (context.blockTemps() != null)
            {
                block.Temporaries = context.blockTemps().temporaryName()
                    .Select(tn => tn.IDENTIFIER().GetText())
                    .ToList();
            }

            // Process statements
            if (context.statements() != null)
            {
                block.Statements = ProcessStatements(context.statements());
            }

            return block;
        }

        private List<StatementNode> ProcessStatements(Smalltalk80Parser.StatementsContext context)
        {
            var statements = new List<StatementNode>();
            
            // Process regular statements
            foreach (var statement in context.statement())
            {
                if (statement.returnStatement() != null)
                {
                    var returnStmt = statement.returnStatement();
                    statements.Add(new ReturnStatementNode
                    {
                        Expression = ProcessExpression(returnStmt.expression())
                    });
                }
                else if (statement.expressionStatement() != null)
                {
                    var exprStmt = statement.expressionStatement();
                    statements.Add(new ExpressionStatementNode
                    {
                        Expression = ProcessExpression(exprStmt.expression())
                    });
                }
            }

            return statements;
        }

        private MessageNode ProcessMessagePart(Smalltalk80Parser.MessagePartContext context)
        {
            if (context.unaryMessage() != null)
            {
                return new UnaryMessageNode
                {
                    Selector = context.unaryMessage().unarySelector().IDENTIFIER().GetText()
                };
            }
            else if (context.binaryMessage() != null)
            {
                var binary = context.binaryMessage();
                return new BinaryMessageNode
                {
                    Selector = binary.binarySelector().GetText(),
                    // Binary message argument is a unaryExpr
                    Argument = ProcessUnaryExpression(binary.unaryExpr())
                };
            }
            else if (context.keywordMessage() != null)
            {
                return ProcessKeywordMessageNode(context.keywordMessage());
            }

            throw new InvalidOperationException("Unknown message type");
        }

        private KeywordMessageNode ProcessKeywordMessageNode(Smalltalk80Parser.KeywordMessageContext context)
        {
            var selectorParts = new List<string>();
            var arguments = new List<ExpressionNode>();

            foreach (var part in context.keywordPart())
            {
                // Combine IDENTIFIER and COLON tokens
                selectorParts.Add(part.IDENTIFIER().GetText() + part.COLON().GetText());
                // Keyword arguments use binaryExpr (not nonKeywordExpr anymore)
                arguments.Add(ProcessBinaryExpression(part.binaryExpr()));
            }

            return new KeywordMessageNode
            {
                Selector = string.Join("", selectorParts),
                Arguments = arguments
            };
        }

        private ExpressionNode ProcessKeywordMessage(ExpressionNode receiver, Smalltalk80Parser.KeywordMessageContext context)
        {
            return new MessageExpressionNode
            {
                Receiver = receiver,
                Message = ProcessKeywordMessageNode(context)
            };
        }

        private ExpressionNode ProcessNonKeywordExpression(Smalltalk80Parser.NonKeywordExprContext context)
        {
            // Grammar: nonKeywordExpr = binaryExpr
            return ProcessBinaryExpression(context.binaryExpr());
        }


        private string ExtractSymbolValue(Smalltalk80Parser.SymbolConstContext symbolContext)
        {
            if (symbolContext.QUOTED_SYMBOL() != null)
            {
                var text = symbolContext.QUOTED_SYMBOL().GetText();
                // Remove #' prefix and ' suffix
                return text.Substring(2, text.Length - 3);
            }
            else if (symbolContext.symbolName() != null)
            {
                var symbolName = symbolContext.symbolName();
                if (symbolName.IDENTIFIER() != null)
                {
                    return symbolName.IDENTIFIER().GetText();
                }
                else if (symbolName.unarySelector() != null)
                {
                    return symbolName.unarySelector().GetText();
                }
                else if (symbolName.binarySelector() != null)
                {
                    return symbolName.binarySelector().GetText();
                }
                else if (symbolName.SYMBOL_KEYWORD_SELECTOR() != null)
                {
                    // SYMBOL_KEYWORD_SELECTOR token from lexer mode contains complete text (e.g., "at:put:", "quo:")
                    return symbolName.SYMBOL_KEYWORD_SELECTOR().GetText();
                }
                else if (symbolName.keywordSymbolInArray() != null)
                {
                    // Keyword symbol in array context - reconstruct from IDENTIFIER COLON pairs
                    var identifiers = symbolName.keywordSymbolInArray().IDENTIFIER();
                    var colons = symbolName.keywordSymbolInArray().COLON();
                    var parts = new List<string>();
                    for (int i = 0; i < identifiers.Length; i++)
                    {
                        parts.Add(identifiers[i].GetText());
                        if (i < colons.Length)
                            parts.Add(colons[i].GetText());
                    }
                    return string.Join("", parts);
                }
            }
            return "";
        }

        private string StripQuotes(string text)
        {
            if (text.Length >= 2 && text[0] == '\'' && text[^1] == '\'')
            {
                return text[1..^1].Replace("''", "'");
            }
            return text;
        }

        private string GetOriginalText(ParserRuleContext context)
        {
            var start = context.Start.TokenIndex;
            var stop = context.Stop?.TokenIndex ?? start;
            var interval = new Interval(start, stop);
            return _tokenStream.GetText(interval);
        }

        /// <summary>
        /// Computes all inherited instance variables including those from superclasses.
        /// Superclass instance variables come FIRST, then subclass variables.
        /// </summary>
        private List<string> ComputeInheritedInstanceVariables(string className)
        {
            var instanceVars = new List<string>();
            var currentClass = className;

            // Walk up the inheritance chain and collect all instance variables
            var classChain = new List<ClassDefinitionNode>();
            while (currentClass != null && _classes.ContainsKey(currentClass))
            {
                classChain.Add(_classes[currentClass]);
                currentClass = _classes[currentClass].Superclass;
            }

            // Add instance variables in reverse order (superclass first, subclass last)
            for (int i = classChain.Count - 1; i >= 0; i--)
            {
                instanceVars.AddRange(classChain[i].InstanceVariables);
            }

            return instanceVars;
        }


        /// <summary>
        /// Gets class variables for a class.
        /// </summary>
        private Dictionary<string, object> GetClassVariables(ClassDefinitionNode classNode)
        {
            var classVars = new Dictionary<string, object>();
            foreach (var varName in classNode.ClassVariables)
            {
                classVars[varName] = new { type = "ClassVariable", name = varName };
            }
            return classVars;
        }

        /// <summary>
        /// Gets pool variables for a class.
        /// </summary>
        private Dictionary<string, object> GetPoolVariables(ClassDefinitionNode classNode)
        {
            var poolVars = new Dictionary<string, object>();
            Console.WriteLine($"[GetPoolVariables] Class: {classNode.Name}, Pool count: {classNode.PoolDictionaries.Count}");
            foreach (var poolName in classNode.PoolDictionaries)
            {
                Console.WriteLine($"[GetPoolVariables] Adding pool: {poolName}");
                poolVars[poolName] = poolName; // Store pool dictionary NAME as the value
            }
            return poolVars;
        }

        /// <summary>
        /// Extracts receiver and selector information from an expression for documentation purposes.
        /// </summary>
        private (string? receiver, string? selector) ExtractReceiverAndSelector(Smalltalk80Parser.ExpressionContext expression)
        {
            try
            {
                // Check if it's an assignment expression
                if (expression.assignmentExpr() != null)
                {
                    var assignExpr = expression.assignmentExpr();
                    
                    // If it has a variable assignment, skip to the actual expression
                    if (assignExpr.variable() != null && assignExpr.assignmentExpr() != null)
                    {
                        assignExpr = assignExpr.assignmentExpr();
                    }
                    
                    // Check for cascade expression
                    if (assignExpr.cascadeExpr() != null)
                    {
                        var cascadeExpr = assignExpr.cascadeExpr();
                        if (cascadeExpr.messageExpr() != null)
                        {
                            return ExtractFromMessageExpression(cascadeExpr.messageExpr());
                        }
                    }
                }
                
                return (null, null);
            }
            catch
            {
                // If we can't extract the info, it's not critical
                return (null, null);
            }
        }
        
        /// <summary>
        /// Extracts receiver and selector from a message expression.
        /// </summary>
        private (string? receiver, string? selector) ExtractFromMessageExpression(Smalltalk80Parser.MessageExprContext messageExpr)
        {
            string? receiver = null;
            string? selector = null;

            // New grammar: messageExpr = binaryExpr keywordMessage?
            var binaryExpr = messageExpr.binaryExpr();
            if (binaryExpr == null) return (receiver, selector);

            // Get receiver from unaryExpr -> messageBase
            var unaryExpr = binaryExpr.unaryExpr();
            if (unaryExpr != null)
            {
                var baseExpr = unaryExpr.messageBase();
                if (baseExpr != null)
                {
                    if (baseExpr.variable() != null)
                    {
                        receiver = baseExpr.variable().IDENTIFIER().GetText();
                    }
                    else if (baseExpr.literal() != null && baseExpr.literal().symbolConst() != null)
                    {
                        receiver = ExtractSymbolValue(baseExpr.literal().symbolConst());
                    }
                }

                // Check for unary messages
                var unaryMessages = unaryExpr.unaryMessage();
                if (unaryMessages != null && unaryMessages.Length > 0)
                {
                    var lastUnary = unaryMessages[^1];
                    selector = lastUnary.unarySelector().IDENTIFIER().GetText();
                }
            }

            // Check for binary messages
            var binaryMessages = binaryExpr.binaryMessage();
            if (binaryMessages != null && binaryMessages.Length > 0)
            {
                var lastBinary = binaryMessages[^1];
                selector = lastBinary.binarySelector().GetText();
            }

            // Get the selector from keyword message (overrides unary/binary)
            var keywordMsg = messageExpr.keywordMessage();
            if (keywordMsg != null)
            {
                var parts = new List<string>();
                foreach (var part in keywordMsg.keywordPart())
                {
                    parts.Add(part.IDENTIFIER().GetText() + part.COLON().GetText());
                }
                selector = string.Join("", parts);
            }

            return (receiver, selector);
        }

        /// <summary>
        /// Converts literals from internal format to JSON-serializable format.
        /// </summary>
        private List<object> ConvertLiterals(object[] literals)
        {
            var result = new List<object>();
            foreach (var literal in literals)
            {
                // Check for Symbol first since it's a custom type from CompilerDataTypes
                if (literal is Symbol symbol)
                {
                    // Convert Symbol to the format expected by the interpreter: "#symbolName"
                    result.Add($"#{symbol.Value}");
                }
                else if (literal is GlobalVariable globalVar)
                {
                    // Preserve GlobalVariable object for JSON generation stage
                    result.Add(globalVar);
                }
                else if (literal is PoolVariable poolVar)
                {
                    // Preserve PoolVariable object for JSON generation stage to handle pool dictionaries correctly
                    result.Add(poolVar);
                }
                #pragma warning disable CS0618 // Type is obsolete
                else if (literal is Association assoc)
                {
                    // LEGACY: Preserve Association object for backwards compatibility
                    result.Add(assoc);
                }
                #pragma warning restore CS0618
                else if (literal is string s)
                {
                    result.Add(s);
                }
                else if (literal is int i)
                {
                    result.Add(i);
                }
                else if (literal is double d)
                {
                    result.Add(d);
                }
                else if (literal is bool b)
                {
                    result.Add(b ? "true" : "false");
                }
                else if (literal is object[] array)
                {
                    result.Add(ConvertLiterals(array));
                }
                else if (literal is Dictionary<string, object> dict)
                {
                    result.Add(dict);
                }
                else
                {
                    result.Add(literal ?? "nil");
                }
            }
            return result;
        }
    }

    // AST Node Classes
    public class CompilationResult
    {
        [JsonPropertyName("$schema")]
        public string? Schema { get; set; }
        
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("classes")]
        public List<ClassDefinitionNode> Classes { get; set; } = new();

        [JsonPropertyName("initializationExpressions")]
        public List<InitializationExpression> InitializationExpressions { get; set; } = new();
    }

    public class ClassDefinitionNode
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("superclass")]
        public string Superclass { get; set; } = "";

        [JsonPropertyName("subclassKind")]
        public string SubclassKind { get; set; } = "subclass:";  // Default to fixed pointer

        [JsonPropertyName("instanceVariables")]
        public List<string> InstanceVariables { get; set; } = new();

        /// <summary>
        /// Total count of all instance variables including inherited ones.
        /// Used for format word calculation.
        /// </summary>
        [JsonIgnore]
        public int TotalInstanceVariableCount { get; set; }

        [JsonPropertyName("classVariables")]
        public List<string> ClassVariables { get; set; } = new();

        [JsonPropertyName("poolDictionaries")]
        public List<string> PoolDictionaries { get; set; } = new();

        [JsonPropertyName("category")]
        public string Category { get; set; } = "";

        [JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonPropertyName("methods")]
        public List<MethodDefinitionNode> Methods { get; set; } = new();

        [JsonPropertyName("classMethods")]
        public List<MethodDefinitionNode> ClassMethods { get; set; } = new();
    }

    public class MethodDefinitionNode
    {
        [JsonPropertyName("class")]
        public string Class { get; set; } = "";

        [JsonPropertyName("selector")]
        public string Selector { get; set; } = "";

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("bytecode")]
        public List<int> Bytecode { get; set; } = new();

        [JsonPropertyName("literals")]
        public List<object> Literals { get; set; } = new();

        [JsonPropertyName("primitiveIndex")]
        public int PrimitiveIndex { get; set; }

        [JsonPropertyName("numArgs")]
        public int NumArgs { get; set; }

        [JsonPropertyName("numTemps")]
        public int NumTemps { get; set; }

        [JsonPropertyName("stackDepth")]
        public int StackDepth { get; set; }

        [JsonPropertyName("isClassMethod")]
        public bool IsClassMethod { get; set; }

        [JsonPropertyName("argNames")]
        public List<string> ArgNames { get; set; } = new();

        [JsonPropertyName("tempNames")]
        public List<string> TempNames { get; set; } = new();

        [JsonPropertyName("isQuickReturnSelf")]
        public bool IsQuickReturnSelf { get; set; }

        [JsonPropertyName("quickReturnFieldIndex")]
        public int QuickReturnFieldIndex { get; set; } = -1;
    }

    public class InitializationExpression
    {
        [JsonPropertyName("bytecode")]
        public List<int> Bytecode { get; set; } = new();

        [JsonPropertyName("literals")]
        public List<object> Literals { get; set; } = new();

        [JsonPropertyName("numTemps")]
        public int NumTemps { get; set; }

        [JsonPropertyName("stackDepth")]
        public int StackDepth { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; } = "";

        [JsonPropertyName("receiver")]
        public string? Receiver { get; set; }

        [JsonPropertyName("selector")]
        public string? Selector { get; set; }
    }

    // Expression nodes
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(AssignmentExpressionNode), "assignment")]
    [JsonDerivedType(typeof(CascadeExpressionNode), "cascade")]
    [JsonDerivedType(typeof(MessageExpressionNode), "message")]
    [JsonDerivedType(typeof(VariableNode), "variable")]
    [JsonDerivedType(typeof(BlockNode), "block")]
    [JsonDerivedType(typeof(DynamicArrayNode), "dynamicArray")]
    [JsonDerivedType(typeof(IntegerLiteralNode), "integerLiteral")]
    [JsonDerivedType(typeof(FloatLiteralNode), "floatLiteral")]
    [JsonDerivedType(typeof(StringLiteralNode), "stringLiteral")]
    [JsonDerivedType(typeof(SymbolLiteralNode), "symbolLiteral")]
    [JsonDerivedType(typeof(CharacterLiteralNode), "characterLiteral")]
    [JsonDerivedType(typeof(ArrayLiteralNode), "arrayLiteral")]
    [JsonDerivedType(typeof(ByteArrayLiteralNode), "byteArrayLiteral")]
    public abstract class ExpressionNode { }

    public class AssignmentExpressionNode : ExpressionNode
    {
        [JsonPropertyName("variable")]
        public string Variable { get; set; } = "";

        [JsonPropertyName("value")]
        public ExpressionNode? Value { get; set; }
    }

    public class CascadeExpressionNode : ExpressionNode
    {
        [JsonPropertyName("receiver")]
        public ExpressionNode? Receiver { get; set; }

        [JsonPropertyName("messages")]
        public List<MessageNode> Messages { get; set; } = new();
    }

    public class MessageExpressionNode : ExpressionNode
    {
        [JsonPropertyName("receiver")]
        public ExpressionNode? Receiver { get; set; }

        [JsonPropertyName("message")]
        public MessageNode? Message { get; set; }
    }

    public class VariableNode : ExpressionNode
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }

    public class BlockNode : ExpressionNode
    {
        [JsonPropertyName("parameters")]
        public List<string> Parameters { get; set; } = new();

        [JsonPropertyName("temporaries")]
        public List<string> Temporaries { get; set; } = new();

        [JsonPropertyName("statements")]
        public List<StatementNode> Statements { get; set; } = new();
    }

    public class DynamicArrayNode : ExpressionNode
    {
        [JsonPropertyName("elements")]
        public List<ExpressionNode> Elements { get; set; } = new();
    }

    // Message nodes
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(UnaryMessageNode), "unary")]
    [JsonDerivedType(typeof(BinaryMessageNode), "binary")]
    [JsonDerivedType(typeof(KeywordMessageNode), "keyword")]
    public abstract class MessageNode
    {
        [JsonPropertyName("selector")]
        public string Selector { get; set; } = "";
    }

    public class UnaryMessageNode : MessageNode { }

    public class BinaryMessageNode : MessageNode
    {
        [JsonPropertyName("argument")]
        public ExpressionNode? Argument { get; set; }
    }

    public class KeywordMessageNode : MessageNode
    {
        [JsonPropertyName("arguments")]
        public List<ExpressionNode> Arguments { get; set; } = new();
    }

    // Statement nodes
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(ExpressionStatementNode), "expression")]
    [JsonDerivedType(typeof(ReturnStatementNode), "return")]
    public abstract class StatementNode { }

    public class ExpressionStatementNode : StatementNode
    {
        [JsonPropertyName("expression")]
        public ExpressionNode? Expression { get; set; }
    }

    public class ReturnStatementNode : StatementNode
    {
        [JsonPropertyName("expression")]
        public ExpressionNode? Expression { get; set; }
    }

    // Literal nodes
    public abstract class LiteralNode : ExpressionNode { }

    public class IntegerLiteralNode : LiteralNode
    {
        [JsonPropertyName("value")]
        public int Value { get; set; }
    }

    public class FloatLiteralNode : LiteralNode
    {
        [JsonPropertyName("value")]
        public double Value { get; set; }
    }

    public class StringLiteralNode : LiteralNode
    {
        [JsonPropertyName("value")]
        public string Value { get; set; } = "";
    }

    public class SymbolLiteralNode : LiteralNode
    {
        [JsonPropertyName("value")]
        public string Value { get; set; } = "";
    }

    public class CharacterLiteralNode : LiteralNode
    {
        [JsonPropertyName("value")]
        public char Value { get; set; }
    }

    public class ArrayLiteralNode : LiteralNode
    {
        [JsonPropertyName("elements")]
        public List<LiteralNode> Elements { get; set; } = new();
    }

    public class ByteArrayLiteralNode : LiteralNode
    {
        [JsonPropertyName("bytes")]
        public List<byte> Bytes { get; set; } = new();
    }
}