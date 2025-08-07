using System;
using System.Collections.Generic;

namespace Smalltalk80.Parser;

/// <summary>
/// Fixed version of AstMethodCompiler that properly checks classPool for class variables.
/// This replaces the shadow C# structures with real Smalltalk object access.
/// </summary>
public static class FixedAstMethodCompiler
{
    // Field indices for Class objects (0-based)
    private const int CLASS_CLASSPOOL_INDEX = 6;  // classPool is at field index 6
    
    // Dictionary structure (0-based)
    private const int DICT_TALLY = 0;
    private const int DICT_ARRAY = 1;
    
    // Association structure (0-based)
    private const int ASSOC_KEY = 0;
    private const int ASSOC_VALUE = 1;
    
    /// <summary>
    /// Compiles a method body with proper class variable support.
    /// </summary>
    /// <param name="sequence">The method body sequence.</param>
    /// <param name="argumentNames">Names of the method parameters in order.</param>
    /// <param name="instanceVariables">Names of instance variables on the method's class.</param>
    /// <param name="compilingClass">The class being compiled for (to access classPool).</param>
    /// <param name="hasPrimitive">Indicates whether the method invokes a primitive.</param>
    /// <param name="primitiveIndex">The index of the primitive to invoke if <paramref name="hasPrimitive"/> is <c>true</c>.</param>
    /// <param name="comment">Optional source comment attached to the method.</param>
    /// <returns>The compiled method.</returns>
    public static CompiledMethod CompileMethod(
        SequenceNode sequence,
        IReadOnlyList<string> argumentNames,
        IReadOnlyList<string> instanceVariables,
        SmalltalkObject? compilingClass = null,  // NEW PARAMETER!
        bool hasPrimitive = false,
        int primitiveIndex = 0,
        string? comment = null)
        => new Compiler(argumentNames, instanceVariables, compilingClass, hasPrimitive, primitiveIndex)
            .Compile(sequence, comment);

    private sealed class Compiler
    {
        private readonly IReadOnlyList<string> arguments;
        private readonly IReadOnlyList<string> instanceVariables;
        private readonly SmalltalkObject? compilingClass;  // The class we're compiling for
        private readonly bool hasPrimitive;
        private readonly int primitiveIndex;
        private readonly List<byte> bytecodes = new();
        private readonly List<object> literals = new();
        private readonly Dictionary<string, int> temporaries = new();

        public Compiler(
            IReadOnlyList<string> arguments,
            IReadOnlyList<string> instanceVariables,
            SmalltalkObject? compilingClass,
            bool hasPrimitive,
            int primitiveIndex)
        {
            this.arguments = arguments;
            this.instanceVariables = instanceVariables;
            this.compilingClass = compilingClass;
            this.hasPrimitive = hasPrimitive;
            this.primitiveIndex = primitiveIndex;
            
            // Register arguments as temporaries
            for (int i = 0; i < arguments.Count; i++)
            {
                temporaries[arguments[i]] = i;
            }
        }

        public CompiledMethod Compile(SequenceNode sequence, string? comment)
        {
            // Register temporaries
            int tempIndex = arguments.Count;
            foreach (var temp in sequence.Temporaries)
            {
                temporaries[temp] = tempIndex++;
            }

            // Compile statements
            foreach (var statement in sequence.Statements)
            {
                CompileStatement(statement);
            }

            // Ensure method returns something
            if (bytecodes.Count == 0 || !IsReturnBytecode(bytecodes[^1]))
            {
                bytecodes.Add((byte)Bytecode.ReturnSelf);
            }

            // Build method header
            var header = BuildHeader();

            return new CompiledMethod
            {
                Header = header,
                Literals = literals.ToArray(),
                Bytecodes = bytecodes.ToArray(),
                Comment = comment
            };
        }

        private void CompileStatement(StatementNode statement)
        {
            switch (statement)
            {
                case ExpressionStatement exprStmt when exprStmt.Expression is AssignmentExpression assignment:
                    CompileExpression(assignment.Value);
                    StoreVariable(assignment.Variable);
                    break;
                case AnswerStatement returnStmt:
                    CompileExpression(returnStmt.Expression);
                    bytecodes.Add((byte)Bytecode.ReturnTop);
                    break;
                case ExpressionStatement exprStmt:
                    CompileExpression(exprStmt.Expression);
                    // Pop result unless it's the last statement
                    bytecodes.Add((byte)Bytecode.PopStack);
                    break;
            }
        }

        private void CompileExpression(ExpressionNode expression)
        {
            switch (expression)
            {
                case VariableExpression variable:
                    EmitVariable(variable.Name);
                    break;
                case LiteralExpression literal:
                    EmitLiteral(literal.Value);
                    break;
                case BlockExpression block:
                    CompileBlock(block);
                    break;
                case UnarySendExpression unary:
                    CompileExpression(unary.Receiver);
                    EmitSend(unary.Selector, 0);
                    break;
                case BinarySendExpression binary:
                    CompileExpression(binary.Receiver);
                    CompileExpression(binary.Argument);
                    EmitSend(binary.Selector, 1);
                    break;
                case KeywordSendExpression keyword:
                    CompileExpression(keyword.Receiver);
                    foreach (var arg in keyword.Arguments)
                        CompileExpression(arg);
                    var selector = string.Concat(keyword.SelectorParts);
                    EmitSend(selector, keyword.Arguments.Count);
                    break;
                case CascadeExpression cascade:
                    CompileCascade(cascade);
                    break;
                case DynamicArrayExpression dynArray:
                    CompileDynamicArray(dynArray);
                    break;
                case DynamicDictionaryExpression dynDict:
                    CompileDynamicDictionary(dynDict);
                    break;
                default:
                    throw new NotSupportedException($"Expression type {expression.GetType().Name} not supported");
            }
        }

        private void EmitVariable(string name)
        {
            // Handle pseudo variables first
            switch (name)
            {
                case "self":
                    bytecodes.Add((byte)Bytecode.PushSelf);
                    return;
                case "super":
                    bytecodes.Add((byte)Bytecode.PushSelf);
                    return;
                case "nil":
                    bytecodes.Add((byte)Bytecode.PushNil);
                    return;
                case "true":
                    bytecodes.Add((byte)Bytecode.PushTrue);
                    return;
                case "false":
                    bytecodes.Add((byte)Bytecode.PushFalse);
                    return;
            }

            // Check temporaries (including arguments)
            if (temporaries.TryGetValue(name, out var index))
            {
                if (index < 16)
                {
                    bytecodes.Add((byte)((int)Bytecode.PushTemporaryVariable0 + index));
                }
                else
                {
                    throw new NotSupportedException("Only up to 16 temporaries are supported.");
                }
                return;
            }

            // Check instance variables
            int ivIndex = -1;
            for (int i = 0; i < instanceVariables.Count; i++)
            {
                if (instanceVariables[i] == name)
                {
                    ivIndex = i;
                    break;
                }
            }
            if (ivIndex >= 0 && ivIndex < 16)
            {
                bytecodes.Add((byte)((int)Bytecode.PushReceiverVariable0 + ivIndex));
                return;
            }

            // NEW: Check class variables in classPool!
            if (compilingClass != null && CheckClassVariable(name))
            {
                return;  // CheckClassVariable handles the bytecode emission
            }

            // Check for global variables and classes in SystemDictionary
            if (SystemDictionary.TryGetGlobal(name, out var globalValue))
            {
                // Create an association for the global and add it to literals
                var association = new Association(new Symbol(name), globalValue);
                int literalIndex = AddLiteral(association);

                if (literalIndex < 32)
                {
                    bytecodes.Add((byte)((int)Bytecode.PushLiteralVariable0 + literalIndex));
                }
                else
                {
                    throw new NotSupportedException("Too many literals in method.");
                }
                return;
            }

            throw new InvalidOperationException($"Unknown variable '{name}'. Available temporaries: [{string.Join(", ", temporaries.Keys)}]. Instance variables: [{string.Join(", ", instanceVariables)}]. Class: {compilingClass?.GetType().Name}");
        }

        /// <summary>
        /// Check if a variable is a class variable and emit appropriate bytecode.
        /// </summary>
        private bool CheckClassVariable(string name)
        {
            if (compilingClass == null)
                return false;

            // Get classPool from field 6 of the class
            if (compilingClass.Fields.Length <= CLASS_CLASSPOOL_INDEX)
                return false;

            var classPool = compilingClass.Fields[CLASS_CLASSPOOL_INDEX] as SmalltalkObject;
            if (classPool == null)
                return false;

            // Search the classPool Dictionary for the variable
            var association = FindInDictionary(classPool, name);
            if (association != null)
            {
                // Add the association to literals and emit bytecode
                int literalIndex = AddLiteral(association);
                
                if (literalIndex < 32)
                {
                    bytecodes.Add((byte)((int)Bytecode.PushLiteralVariable0 + literalIndex));
                }
                else
                {
                    throw new NotSupportedException("Too many literals in method.");
                }
                
                return true;
            }

            // Also check superclass classPools
            var superclass = compilingClass.Superclass;
            while (superclass != null)
            {
                if (superclass.Fields.Length > CLASS_CLASSPOOL_INDEX)
                {
                    classPool = superclass.Fields[CLASS_CLASSPOOL_INDEX] as SmalltalkObject;
                    if (classPool != null)
                    {
                        association = FindInDictionary(classPool, name);
                        if (association != null)
                        {
                            int literalIndex = AddLiteral(association);
                            if (literalIndex < 32)
                            {
                                bytecodes.Add((byte)((int)Bytecode.PushLiteralVariable0 + literalIndex));
                            }
                            else
                            {
                                throw new NotSupportedException("Too many literals in method.");
                            }
                            return true;
                        }
                    }
                }
                superclass = superclass.Superclass;
            }

            return false;
        }

        /// <summary>
        /// Find an association in a Smalltalk Dictionary by key.
        /// </summary>
        private SmalltalkObject? FindInDictionary(SmalltalkObject dict, string key)
        {
            // Get the array from the dictionary
            if (dict.Fields.Length <= DICT_ARRAY)
                return null;

            var array = dict.Fields[DICT_ARRAY] as SmalltalkObject;
            if (array == null)
                return null;

            var searchKey = new Symbol(key);

            // Search through the array for an association with matching key
            foreach (var item in array.Fields)
            {
                if (item is SmalltalkObject assoc && assoc.Fields.Length > ASSOC_KEY)
                {
                    var assocKey = assoc.Fields[ASSOC_KEY];
                    if (assocKey is Symbol sym && sym.Equals(searchKey))
                    {
                        return assoc;
                    }
                }
            }

            return null;
        }

        private void StoreVariable(string name)
        {
            // Check temporaries
            if (temporaries.TryGetValue(name, out var index))
            {
                if (index < 8)
                {
                    bytecodes.Add((byte)((int)Bytecode.StoreAndPopTemporaryVariable0 + index));
                }
                else
                {
                    bytecodes.Add((byte)Bytecode.ExtendedStoreAndPop);
                    byte descriptor = (byte)((1 << 6) | index);
                    bytecodes.Add(descriptor);
                }
                return;
            }

            // Check instance variables
            int ivIndex = -1;
            for (int i = 0; i < instanceVariables.Count; i++)
            {
                if (instanceVariables[i] == name)
                {
                    ivIndex = i;
                    break;
                }
            }
            if (ivIndex >= 0 && ivIndex < 8)
            {
                bytecodes.Add((byte)((int)Bytecode.StoreAndPopReceiverVariable0 + ivIndex));
                return;
            }

            // NEW: Check class variables for storage
            if (compilingClass != null && StoreClassVariable(name))
            {
                return;
            }

            throw new InvalidOperationException($"Unknown variable '{name}' for assignment");
        }

        /// <summary>
        /// Store to a class variable.
        /// </summary>
        private bool StoreClassVariable(string name)
        {
            if (compilingClass == null)
                return false;

            // Find the association in classPool
            if (compilingClass.Fields.Length <= CLASS_CLASSPOOL_INDEX)
                return false;

            var classPool = compilingClass.Fields[CLASS_CLASSPOOL_INDEX] as SmalltalkObject;
            if (classPool == null)
                return false;

            var association = FindInDictionary(classPool, name);
            if (association != null)
            {
                // For class variable assignment, we need to use the association
                // The value on stack should be stored into the association's value field
                int literalIndex = AddLiteral(association);
                
                // Use StoreIntoLiteralVariable bytecode
                if (literalIndex < 32)
                {
                    bytecodes.Add((byte)Bytecode.ExtendedStore);
                    bytecodes.Add((byte)(0x60 | literalIndex)); // Store into literal variable
                }
                else
                {
                    throw new NotSupportedException("Too many literals in method.");
                }
                
                return true;
            }

            return false;
        }

        private void EmitLiteral(object value)
        {
            switch (value)
            {
                case int i when i >= -1 && i <= 2:
                    bytecodes.Add((byte)((int)Bytecode.PushMinusOne + i + 1));
                    break;
                case int i:
                    EmitSmallInteger(i);
                    break;
                case Symbol symbol:
                    int symbolIndex = AddLiteral(symbol);
                    if (symbolIndex < 32)
                    {
                        bytecodes.Add((byte)((int)Bytecode.PushLiteralConstant0 + symbolIndex));
                    }
                    else
                    {
                        throw new NotSupportedException("Too many literals");
                    }
                    break;
                case string s:
                    int stringIndex = AddLiteral(s);
                    if (stringIndex < 32)
                    {
                        bytecodes.Add((byte)((int)Bytecode.PushLiteralConstant0 + stringIndex));
                    }
                    else
                    {
                        throw new NotSupportedException("Too many literals");
                    }
                    break;
                case char c:
                    int charIndex = AddLiteral(c);
                    if (charIndex < 32)
                    {
                        bytecodes.Add((byte)((int)Bytecode.PushLiteralConstant0 + charIndex));
                    }
                    else
                    {
                        throw new NotSupportedException("Too many literals");
                    }
                    break;
                case double d:
                    int floatIndex = AddLiteral(d);
                    if (floatIndex < 32)
                    {
                        bytecodes.Add((byte)((int)Bytecode.PushLiteralConstant0 + floatIndex));
                    }
                    else
                    {
                        throw new NotSupportedException("Too many literals");
                    }
                    break;
                case SmalltalkObject obj:
                    int objIndex = AddLiteral(obj);
                    if (objIndex < 32)
                    {
                        bytecodes.Add((byte)((int)Bytecode.PushLiteralConstant0 + objIndex));
                    }
                    else
                    {
                        throw new NotSupportedException("Too many literals");
                    }
                    break;
                default:
                    throw new NotSupportedException($"Literal type {value.GetType()} not supported");
            }
        }

        private void EmitSmallInteger(int value)
        {
            if (value >= -1 && value <= 2)
            {
                bytecodes.Add((byte)((int)Bytecode.PushMinusOne + value + 1));
            }
            else
            {
                int index = AddLiteral(new SmallInteger(value));
                if (index < 32)
                {
                    bytecodes.Add((byte)((int)Bytecode.PushLiteralConstant0 + index));
                }
                else
                {
                    throw new NotSupportedException("Too many literals");
                }
            }
        }

        private void CompileBlock(BlockExpression block)
        {
            // Blocks need special handling - compile to a CompiledMethod
            var blockCompiler = new Compiler(
                block.Parameters,
                instanceVariables,
                compilingClass,  // Pass the class to block compiler too
                false,
                0);

            // Register block temporaries
            int tempIndex = block.Parameters.Count;
            foreach (var temp in block.Body.Temporaries)
            {
                blockCompiler.temporaries[temp] = tempIndex++;
            }

            // Compile block body
            foreach (var statement in block.Body.Statements)
            {
                blockCompiler.CompileStatement(statement);
            }

            // Blocks return last value or nil
            if (blockCompiler.bytecodes.Count == 0 || !IsReturnBytecode(blockCompiler.bytecodes[^1]))
            {
                blockCompiler.bytecodes.Add((byte)Bytecode.ReturnTop);
            }

            var blockMethod = new CompiledMethod
            {
                Header = blockCompiler.BuildHeader(),
                Literals = blockCompiler.literals.ToArray(),
                Bytecodes = blockCompiler.bytecodes.ToArray()
            };

            // Add block as literal and push block closure
            int blockIndex = AddLiteral(blockMethod);
            bytecodes.Add((byte)Bytecode.PushActiveContext);
            bytecodes.Add((byte)((byte)Bytecode.PushLiteralConstant0 + blockIndex));
            EmitSend("blockCopy:", 0);
        }

        private void EmitSend(string selector, int argCount)
        {
            var symbol = new Symbol(selector);
            var index = AddLiteral(symbol);
            if (index < 32 && argCount < 8)
            {
                bytecodes.Add((byte)Bytecode.SingleExtendedSend);
                byte descriptor = (byte)((argCount << 5) | index);
                bytecodes.Add(descriptor);
            }
            else
            {
                bytecodes.Add((byte)Bytecode.DoubleExtendedSend);
                bytecodes.Add((byte)argCount);
                bytecodes.Add((byte)index);
            }
        }

        private void CompileCascade(CascadeExpression cascade)
        {
            // Compile the receiver once
            CompileExpression(cascade.Receiver);

            // For each message except the last, duplicate the receiver and send
            for (int i = 0; i < cascade.Messages.Count - 1; i++)
            {
                bytecodes.Add((byte)Bytecode.DuplicateTop);
                CompileMessage(cascade.Messages[i]);
                bytecodes.Add((byte)Bytecode.PopStack); // Pop result, keep receiver
            }

            // Send the last message without duplicating (result remains on stack)
            CompileMessage(cascade.Messages[cascade.Messages.Count - 1]);
        }

        private void CompileMessage(ExpressionNode message)
        {
            switch (message)
            {
                case UnarySendExpression unary:
                    EmitSend(unary.Selector, 0);
                    break;
                case BinarySendExpression binary:
                    CompileExpression(binary.Argument);
                    EmitSend(binary.Selector, 1);
                    break;
                case KeywordSendExpression keyword:
                    foreach (var arg in keyword.Arguments)
                        CompileExpression(arg);
                    var selector = string.Concat(keyword.SelectorParts);
                    EmitSend(selector, keyword.Arguments.Count);
                    break;
                default:
                    throw new InvalidOperationException($"Invalid cascade message: {message.GetType().Name}");
            }
        }

        private void CompileDynamicArray(DynamicArrayExpression dynArray)
        {
            // Push each element onto the stack
            foreach (var element in dynArray.Elements)
            {
                CompileExpression(element);
            }

            // Push the array size
            EmitSmallInteger(dynArray.Elements.Count);

            // Send #newFrom: to Array class
            int arrayClassIndex = AddLiteral("Array");
            if (arrayClassIndex <= 31)
                bytecodes.Add((byte)((int)Bytecode.PushLiteralVariable0 + arrayClassIndex));
            else
            {
                bytecodes.Add((byte)Bytecode.ExtendedStore);
                bytecodes.Add((byte)(0x40 | arrayClassIndex));
            }
            EmitSend("newFrom:", 1);
        }

        private void CompileDynamicDictionary(DynamicDictionaryExpression dynDict)
        {
            // For each pair, push key and value
            int pairCount = 0;
            for (int i = 0; i < dynDict.Elements.Count; i += 2)
            {
                if (i + 1 < dynDict.Elements.Count)
                {
                    CompileExpression(dynDict.Elements[i]);     // key
                    CompileExpression(dynDict.Elements[i + 1]); // value
                    pairCount++;
                }
            }

            // Push the pair count
            EmitSmallInteger(pairCount);

            // Send #newFromPairs: to Dictionary class
            int dictClassIndex = AddLiteral("Dictionary");
            if (dictClassIndex <= 31)
                bytecodes.Add((byte)((int)Bytecode.PushLiteralVariable0 + dictClassIndex));
            else
            {
                bytecodes.Add((byte)Bytecode.ExtendedStore);
                bytecodes.Add((byte)(0x40 | dictClassIndex));
            }
            EmitSend("newFromPairs:", 1);
        }

        private int AddLiteral(object value)
        {
            var existing = literals.IndexOf(value);
            if (existing >= 0) return existing;
            literals.Add(value);
            return literals.Count - 1;
        }

        private MethodHeader BuildHeader()
        {
            int argCount = arguments.Count;
            bool useExtension = hasPrimitive || argCount >= 5;
            int flagValue = useExtension ? 7 : argCount;
            int temporaryCount = temporaries.Count;
            bool largeContext = temporaryCount > 12;
            int literalCount = literals.Count;

            int raw = flagValue
                      | (temporaryCount << 3)
                      | ((largeContext ? 1 : 0) << 8)
                      | (literalCount << 9);

            int ext = useExtension
                ? ((argCount & 0x1F) << 10) | (primitiveIndex & 0x3FF)
                : 0;

            return new MethodHeader(raw, ext);
        }

        private bool IsReturnBytecode(byte bytecode)
        {
            return bytecode == (byte)Bytecode.ReturnSelf ||
                   bytecode == (byte)Bytecode.ReturnTrue ||
                   bytecode == (byte)Bytecode.ReturnFalse ||
                   bytecode == (byte)Bytecode.ReturnNil ||
                   bytecode == (byte)Bytecode.ReturnTop ||
                   bytecode == (byte)Bytecode.ReturnTopFromBlock ||
                   bytecode == (byte)Bytecode.ReturnTopFromMethod;
        }
    }
}