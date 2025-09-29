using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace StandaloneCompiler
{
    /// <summary>
    /// AST visitor for processing Smalltalk-80 file-out format and generating JSON output.
    /// This visitor replaces the old Smalltalk80.Compiler implementation and works with
    /// the new grammar structure in StandaloneCompiler/Smalltalk80.g4.
    /// </summary>
    public class FileOutFormatVisitor : Smalltalk80BaseVisitor<object?>
    {
        private readonly CompilationResult compilationResult;
        private readonly BytecodeCompiler bytecodeCompiler;
        private readonly Dictionary<string, ClassDefinition> classMap;
        private readonly List<InitializationExpression> initializationExpressions;
        private readonly ITokenStream tokenStream;
        
        private ClassDefinition? currentClass;
        private bool isClassMethod;
        private string currentCategory = "unclassified";
        
        public FileOutFormatVisitor(ITokenStream tokenStream)
        {
            this.tokenStream = tokenStream;
            this.compilationResult = new CompilationResult
            {
                Schema = "./bootstrap-schema.json",
                Description = $"Compiled from source on {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                Classes = new List<ClassDefinition>(),
                // Methods list removed - methods are stored within their respective classes
                InitializationExpressions = new List<InitializationExpression>()
            };
            this.bytecodeCompiler = new BytecodeCompiler();
            this.classMap = new Dictionary<string, ClassDefinition>();
            this.initializationExpressions = new List<InitializationExpression>();
        }
        
        /// <summary>
        /// Gets the compilation result after visiting the parse tree.
        /// </summary>
        public CompilationResult GetResult()
        {
            // Copy collected data to result
            compilationResult.Classes = new List<ClassDefinition>(classMap.Values);
            compilationResult.InitializationExpressions = new List<InitializationExpression>(initializationExpressions);
            return compilationResult;
        }
        
        #region Main Entry Points
        
        /// <summary>
        /// Visit a file-in format parse tree (root node).
        /// </summary>
        public override object? VisitFileIn(Smalltalk80Parser.FileInContext context)
        {
            // Process all chunks in the file
            foreach (var chunk in context.chunk())
            {
                Visit(chunk);
            }
            return null;
        }
        
        /// <summary>
        /// Visit a chunk - dispatches to specific chunk handlers.
        /// </summary>
        public override object? VisitChunk(Smalltalk80Parser.ChunkContext context)
        {
            // The grammar automatically dispatches to the correct chunk type
            return base.VisitChunk(context);
        }
        
        #endregion
        
        #region Class Definition Handling
        
        /// <summary>
        /// Process a class definition chunk.
        /// Format: Object subclass: #ClassName instanceVariableNames: '...' ... !
        /// </summary>
        public override object? VisitClassDefChunk(Smalltalk80Parser.ClassDefChunkContext context)
        {
            var classHeader = context.classHeader();
            var classBody = context.classBody();
            
            // Extract class name and superclass
            // In the grammar: className (superclass) subclassKind symbolConst (new class)
            var superclass = classHeader.className()?.GetText() ?? "Object";
            var className = ExtractSymbolName(classHeader.symbolConst());
            
            // Create new class definition
            var classDef = new ClassDefinition
            {
                Name = className,
                Superclass = superclass,
                InstanceVariables = new List<string>(),
                ClassVariables = new List<string>(),
                PoolDictionaries = new List<string>(),
                Category = ""
            };
            
            // Process class body specifications
            foreach (var spec in classBody.classSlotSpec())
            {
                ProcessClassSlotSpec(spec, classDef);
            }
            
            // Process category declaration
            if (classBody.categoryDecl() != null)
            {
                classDef.Category = ExtractStringLiteral(classBody.categoryDecl().stringConst());
            }
            
            // Store the class definition
            classMap[classDef.Name] = classDef;
            currentClass = classDef;
            
            Console.WriteLine($"Parsed class: {classDef.Name} < {classDef.Superclass}");
            
            return null;
        }
        
        /// <summary>
        /// Process a metaclass definition chunk.
        /// Format: ClassName class instanceVariableNames: '...'!
        /// </summary>
        public override object? VisitMetaclassDefinitionChunk(Smalltalk80Parser.MetaclassDefinitionChunkContext context)
        {
            var className = context.className()?.GetText() ?? "";
            
            // Find or create the class definition
            if (!classMap.TryGetValue(className, out var classDef))
            {
                classDef = CreateStubClass(className);
                classMap[className] = classDef;
            }
            
            // Process metaclass instance variables (class-instance variables)
            foreach (var spec in context.classSlotSpec())
            {
                var specType = spec.identifierColon()?.GetText();
                if (specType == "instanceVariableNames:")
                {
                    // These are actually class-instance variables for the metaclass
                    // We might want to track these separately if needed
                    var varNames = ParseVariableNames(ExtractStringLiteral(spec.stringConst()));
                    // For now, we'll just log them
                    Console.WriteLine($"  Metaclass {className} class has instance variables: {string.Join(", ", varNames)}");
                }
            }
            
            return null;
        }
        
        #endregion
        
        #region Method Definition Handling
        
        /// <summary>
        /// Process a method category chunk containing multiple methods.
        /// Format: !ClassName [class] methodsFor: 'category'! methods... !
        /// </summary>
        public override object? VisitMethodCategoryChunk(Smalltalk80Parser.MethodCategoryChunkContext context)
        {
            var className = context.className()?.GetText() ?? "";
            
            // Check if these are class methods
            isClassMethod = context.IDENTIFIER()?.GetText() == "class";
            
            // Extract category
            currentCategory = ExtractStringLiteral(context.stringConst());
            
            // Find or create the class
            if (!classMap.TryGetValue(className, out var classDef))
            {
                classDef = CreateStubClass(className);
                classMap[className] = classDef;
            }
            
            currentClass = classDef;
            
            Console.WriteLine($"Processing methods for {className}{(isClassMethod ? " class" : "")} in category '{currentCategory}'");
            
            // Process all methods in this category
            foreach (var methodDef in context.methodDef())
            {
                CompileMethod(methodDef);
            }
            
            return null;
        }
        
        /// <summary>
        /// Process a simple method chunk (rare format).
        /// Format: ClassName [class] >> methodDefinition
        /// </summary>
        public override object? VisitSimpleMethodChunk(Smalltalk80Parser.SimpleMethodChunkContext context)
        {
            var className = context.className()?.GetText() ?? "";
            
            // Check if it's a class method
            isClassMethod = context.IDENTIFIER()?.GetText() == "class";
            
            // Find or create the class
            if (!classMap.TryGetValue(className, out var classDef))
            {
                classDef = CreateStubClass(className);
                classMap[className] = classDef;
            }
            
            currentClass = classDef;
            currentCategory = "unclassified";
            
            // Compile the method
            CompileMethod(context.methodDef());
            
            return null;
        }
        
        #endregion
        
        #region Expression Handling
        
        /// <summary>
        /// Process a standalone expression chunk (initialization code).
        /// Format: expression!
        /// </summary>
        public override object? VisitExpressionChunk(Smalltalk80Parser.ExpressionChunkContext context)
        {
            var expression = context.expression();
            
            // Get the source text for this expression
            var sourceText = GetSourceText(expression);
            
            // Compile the expression to bytecode
            var compiledData = bytecodeCompiler.CompileExpressionFromTree(expression);
            
            // Calculate stack depth
            var stackDepth = BytecodeEmitter.CalculateMaxStackDepth(compiledData.Bytecode);
            
            // Extract receiver and selector for documentation
            var (receiver, selector, arguments) = ExtractMessageComponents(expression);
            
            // Create initialization expression
            var initExpr = new InitializationExpression
            {
                Source = sourceText,
                Receiver = receiver,
                Selector = selector,
                Arguments = arguments,
                Bytecode = compiledData.Bytecode,
                Literals = ConvertLiterals(compiledData.Literals),
                TemporaryCount = compiledData.TemporaryCount,
                StackDepth = stackDepth
            };
            
            initializationExpressions.Add(initExpr);
            compilationResult.InitializationExpressions?.Add(initExpr);
            
            Console.WriteLine($"Compiled initialization expression: {receiver}>>{selector}");
            
            return null;
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Compile a method definition to bytecode.
        /// </summary>
        private void CompileMethod(Smalltalk80Parser.MethodDefContext context)
        {
            if (currentClass == null) return;
            
            try
            {
                // Get all inherited instance variables for instance methods
                var instanceVars = isClassMethod 
                    ? new List<string>()
                    : ComputeInheritedInstanceVariables(currentClass.Name);
                
                // Get class and pool variables
                var classVars = GetClassVariables(currentClass);
                var poolVars = GetPoolVariables(currentClass);
                
                // Compile the method using BytecodeCompiler
                var compiledData = bytecodeCompiler.CompileMethodFromTree(
                    context, 
                    instanceVars,
                    classVars,
                    poolVars);
                
                // Calculate stack depth
                var stackDepth = BytecodeEmitter.CalculateMaxStackDepth(compiledData.Bytecode);
                
                // Get the source text for the method
                var sourceText = GetSourceText(context);
                
                // Create method definition
                var method = new MethodDefinition
                {
                    Class = isClassMethod ? $"{currentClass.Name} class" : currentClass.Name,
                    Selector = compiledData.Selector,
                    Source = sourceText,
                    Bytecode = compiledData.Bytecode,
                    Literals = ConvertLiterals(compiledData.Literals),
                    PrimitiveIndex = compiledData.PrimitiveNumber,
                    NumArgs = compiledData.ArgumentCount,
                    NumTemps = compiledData.TemporaryCount,
                    StackDepth = stackDepth,
                    IsClassMethod = isClassMethod
                };
                
                // Add to class
                if (isClassMethod)
                {
                    currentClass.ClassMethods.Add(method);
                }
                else
                {
                    currentClass.Methods.Add(method);
                }
                
                // Removed adding to global methods list - methods are stored within classes
                
                Console.WriteLine($"  Compiled {method.Class}>>{method.Selector}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Failed to compile method in {currentClass.Name}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Process a class slot specification (instance variables, class variables, etc.).
        /// </summary>
        private void ProcessClassSlotSpec(Smalltalk80Parser.ClassSlotSpecContext spec, ClassDefinition classDef)
        {
            var specType = spec.identifierColon()?.GetText();
            var specValue = ExtractStringLiteral(spec.stringConst());
            
            switch (specType)
            {
                case "instanceVariableNames:":
                    classDef.InstanceVariables = ParseVariableNames(specValue);
                    break;
                case "classVariableNames:":
                    classDef.ClassVariables = ParseVariableNames(specValue);
                    break;
                case "poolDictionaries:":
                    classDef.PoolDictionaries = ParseVariableNames(specValue);
                    break;
                case "classInstanceVariableNames:":
                    // These are instance variables of the metaclass
                    Console.WriteLine($"  Class instance variables: {specValue}");
                    break;
            }
        }
        
        /// <summary>
        /// Create a stub class definition for methods without prior class definition.
        /// </summary>
        private ClassDefinition CreateStubClass(string className)
        {
            return new ClassDefinition
            {
                Name = className,
                Superclass = "Object",
                InstanceVariables = new List<string>(),
                ClassVariables = new List<string>(),
                PoolDictionaries = new List<string>(),
                Category = ""
            };
        }
        
        /// <summary>
        /// Compute all instance variables including inherited ones.
        /// </summary>
        private List<string> ComputeInheritedInstanceVariables(string className)
        {
            var allVars = new List<string>();
            var visited = new HashSet<string>();
            
            void CollectVars(string name)
            {
                if (!visited.Add(name)) return;
                
                if (classMap.TryGetValue(name, out var classDef))
                {
                    // First collect from superclass
                    if (!string.IsNullOrEmpty(classDef.Superclass) && 
                        classDef.Superclass != "nil" && 
                        classDef.Superclass != name)
                    {
                        CollectVars(classDef.Superclass);
                    }
                    
                    // Then add this class's variables
                    allVars.AddRange(classDef.InstanceVariables);
                }
            }
            
            CollectVars(className);
            return allVars;
        }
        
        /// <summary>
        /// Get class variables as a dictionary.
        /// </summary>
        private Dictionary<string, object>? GetClassVariables(ClassDefinition classDef)
        {
            if (classDef.ClassVariables.Count == 0) return null;
            
            var result = new Dictionary<string, object>();
            foreach (var varName in classDef.ClassVariables)
            {
                result[varName] = new Association(new Symbol(varName), null);
            }
            return result;
        }
        
        /// <summary>
        /// Get pool variables as a dictionary.
        /// </summary>
        private Dictionary<string, object>? GetPoolVariables(ClassDefinition classDef)
        {
            if (classDef.PoolDictionaries.Count == 0) return null;
            
            var result = new Dictionary<string, object>();
            foreach (var poolName in classDef.PoolDictionaries)
            {
                // Pool dictionaries are typically global dictionaries
                // For now, we'll just create placeholders
                result[poolName] = new Symbol(poolName);
            }
            return result;
        }
        
        /// <summary>
        /// Extract the name from a symbol constant.
        /// </summary>
        private string ExtractSymbolName(Smalltalk80Parser.SymbolConstContext? context)
        {
            if (context == null) return "";
            
            var text = context.GetText();
            
            // Handle quoted symbols #'...'
            if (context.QUOTED_SYMBOL() != null)
            {
                // Remove #' and trailing '
                return text.Substring(2, text.Length - 3).Replace("''", "'");
            }
            
            // Handle regular symbols #name
            if (text.StartsWith("#"))
            {
                return text.Substring(1);
            }
            
            return text;
        }
        
        /// <summary>
        /// Extract the string value from a string literal.
        /// </summary>
        private string ExtractStringLiteral(Smalltalk80Parser.StringConstContext? context)
        {
            if (context == null) return "";
            
            var text = context.GetText();
            
            // Remove surrounding quotes and handle doubled quotes
            if (text.Length >= 2 && text[0] == '\'' && text[text.Length - 1] == '\'')
            {
                return text.Substring(1, text.Length - 2).Replace("''", "'");
            }
            
            return text;
        }
        
        /// <summary>
        /// Parse variable names from a space-separated string.
        /// </summary>
        private List<string> ParseVariableNames(string variableString)
        {
            if (string.IsNullOrWhiteSpace(variableString))
                return new List<string>();
            
            return variableString
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }
        
        /// <summary>
        /// Convert literals from the bytecode compiler format.
        /// </summary>
        private List<object> ConvertLiterals(object[] literals)
        {
            var result = new List<object>();
            
            foreach (var literal in literals)
            {
                if (literal is Symbol symbol)
                {
                    // Use the correct format for the interpreter: "#symbolName"
                    result.Add($"#{symbol.Value}");
                }
                else if (literal is Association assoc)
                {
                    result.Add(new Dictionary<string, object>
                    {
                        ["type"] = "association",
                        ["key"] = assoc.Key.Value,
                        ["value"] = assoc.Value ?? ""
                    });
                }
                else if (literal is string str)
                {
                    result.Add(str);
                }
                else if (literal is int || literal is long || literal is double || literal is float)
                {
                    result.Add(literal);
                }
                else if (literal is object[] array)
                {
                    result.Add(ConvertLiterals(array));
                }
                else if (literal is NilObject)
                {
                    result.Add("nil");
                }
                else if (literal == null)
                {
                    result.Add("nil");
                }
                else if (literal is bool b)
                {
                    result.Add(b ? "true" : "false");
                }
                else
                {
                    result.Add(literal.ToString() ?? "");
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Get the source text for a parse tree node.
        /// </summary>
        private string GetSourceText(ParserRuleContext context)
        {
            var start = context.Start;
            var stop = context.Stop;
            var charStream = start.InputStream;
            var charStart = start.StartIndex;
            var charStop = stop?.StopIndex ?? charStart;
            return charStream.GetText(new Antlr4.Runtime.Misc.Interval(charStart, charStop));
        }
        
        /// <summary>
        /// Extract message components from an expression for documentation.
        /// </summary>
        private (string receiver, string selector, List<string> arguments) ExtractMessageComponents(
            Smalltalk80Parser.ExpressionContext expression)
        {
            string receiver = "";
            string selector = "";
            var arguments = new List<string>();
            
            // Navigate the parse tree to extract components
            var assignmentExpr = expression.assignmentExpr();
            if (assignmentExpr != null)
            {
                // Check for assignment
                if (assignmentExpr.variable() != null)
                {
                    // This is an assignment
                    receiver = assignmentExpr.variable().GetText();
                    selector = "_";
                    
                    // Get the assigned value
                    var valueExpr = assignmentExpr.assignmentExpr();
                    if (valueExpr != null)
                    {
                        arguments.Add(GetSourceText(valueExpr));
                    }
                }
                else
                {
                    // Not an assignment, continue to cascade expression
                    var cascadeExpr = assignmentExpr.cascadeExpr();
                    if (cascadeExpr != null)
                    {
                        var messageExpr = cascadeExpr.messageExpr();
                        if (messageExpr != null)
                        {
                            // Extract receiver
                            var messageBase = messageExpr.messageBase();
                            if (messageBase != null)
                            {
                                receiver = GetSourceText(messageBase);
                            }
                            
                            // Extract selector and arguments from messages
                            ExtractSelectorFromMessageExpr(messageExpr, ref selector, arguments);
                        }
                    }
                }
            }
            
            return (receiver, selector, arguments);
        }
        
        /// <summary>
        /// Extract selector and arguments from a message expression.
        /// </summary>
        private void ExtractSelectorFromMessageExpr(
            Smalltalk80Parser.MessageExprContext messageExpr,
            ref string selector,
            List<string> arguments)
        {
            // Check for unary messages
            var unaryMessages = messageExpr.unaryMessage();
            if (unaryMessages != null && unaryMessages.Length > 0)
            {
                selector = unaryMessages[0].unarySelector()?.GetText() ?? "";
            }
            
            // Check for binary messages
            var binaryMessages = messageExpr.binaryMessage();
            if (binaryMessages != null && binaryMessages.Length > 0)
            {
                selector = binaryMessages[0].binarySelector()?.GetText() ?? "";
                var arg = binaryMessages[0].messageBase();
                if (arg != null)
                {
                    arguments.Add(GetSourceText(arg));
                }
            }
            
            // Check for keyword message
            var keywordMessage = messageExpr.keywordMessage();
            if (keywordMessage != null)
            {
                var parts = keywordMessage.keywordPart();
                selector = string.Join("", parts.Select(p => p.IDENTIFIER().GetText() + ":"));
                
                foreach (var part in parts)
                {
                    var arg = part.nonKeywordExpr();
                    if (arg != null)
                    {
                        arguments.Add(GetSourceText(arg));
                    }
                }
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Context type mapping information for migration from old grammar.
    /// </summary>
    public static class ContextMapping
    {
        /// <summary>
        /// Maps old context types to new context types in the updated grammar.
        /// </summary>
        public static readonly Dictionary<string, string> OldToNew = new Dictionary<string, string>
        {
            // Class definitions
            ["ClassDefinitionContext"] = "ClassDefChunkContext",
            
            // Method definitions
            ["MethodDefinitionContext"] = "MethodDefContext",
            ["MethodCategoryContext"] = "MethodCategoryChunkContext",
            
            // Expressions and statements
            ["AssignmentContext"] = "AssignmentExprContext",
            ["BasicExpressionContext"] = "MessageBaseContext",
            ["MessagesContext"] = "MessageExprContext",
            ["PrimaryContext"] = "MessageBaseContext",
            ["CommentContext"] = "COMMENT token (lexer rule)",
            ["PrimitiveDeclarationContext"] = "PrimitiveDeclContext",
            ["CascadeMessagesContext"] = "CascadeExprContext",
            ["UnaryExpressionContext"] = "UnaryMessageContext",
            ["BinaryExpressionContext"] = "BinaryMessageContext",
            
            // Additional mappings
            ["KeywordExpressionContext"] = "KeywordMessageContext",
            ["BlockContext"] = "BlockContext", // Same name
            ["LiteralContext"] = "LiteralContext", // Same name
            ["VariableContext"] = "VariableContext", // Same name
            ["TemporariesContext"] = "TemporariesContext", // Same name
            ["StatementsContext"] = "StatementsContext", // Same name
            ["ReturnStatementContext"] = "ReturnStatementContext", // Same name
        };
        
        /// <summary>
        /// Get a description of the changes between old and new grammar.
        /// </summary>
        public static string GetMigrationNotes()
        {
            return @"
Migration Notes from Old Grammar to New Grammar:

1. File Structure:
   - Old: Multiple entry points, file structure not well defined
   - New: Clear 'fileIn' entry point with 'chunk' structure

2. Class Definitions:
   - Old: ClassDefinitionContext
   - New: ClassDefChunkContext (chunk-based structure)
   - New: Added metaclass chunks (MetaclassDefinitionChunkContext)

3. Method Organization:
   - Old: Individual method definitions
   - New: MethodCategoryChunkContext groups methods by category
   - New: SimpleMethodChunkContext for standalone methods

4. Expression Hierarchy:
   - Old: BasicExpression, UnaryExpression, BinaryExpression as separate
   - New: Unified MessageExprContext with message precedence rules

5. Message Sending:
   - Old: MessagesContext for message chains
   - New: MessageExprContext with proper precedence handling
   - New: CascadeExprContext for cascade messages

6. Comments:
   - Old: CommentContext as parser rule
   - New: COMMENT as lexer token on hidden channel

7. Primitive Declarations:
   - Old: PrimitiveDeclarationContext
   - New: PrimitiveDeclContext with PRIMITIVE_DECL lexer token

The new grammar is more structured around the file-out format chunks
and provides better separation between lexer and parser concerns.
";
        }
    }
}