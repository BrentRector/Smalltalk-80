using System;
using System.Collections.Generic;
using System.Linq;
using Smalltalk80.Parser;

namespace Smalltalk80.Compiler;

/// <summary>
/// Compiles Smalltalk AST nodes to bytecode following the Blue Book specification.
/// </summary>
public class AstToBytecodeCompiler
{
    private readonly CompilationContext context;
    private readonly List<byte> bytecodes = new();
    private readonly List<object> literals = new();
    private readonly Dictionary<object, int> literalIndex = new();
    private readonly Dictionary<string, int> temporaries = new();
    private readonly Dictionary<string, int> arguments = new();
    private int argumentCount = 0;
    private int temporaryCount = 0;
    private int primitiveNumber = 0;
    private string? selector = null;
    
    public AstToBytecodeCompiler(CompilationContext context)
    {
        this.context = context;
    }
    
    /// <summary>
    /// Compile a method AST node to a CompiledMethod.
    /// </summary>
    public CompiledMethod CompileMethod(MethodNode methodNode)
    {
        // Extract selector and arguments
        selector = methodNode.Selector;
        var methodArgs = methodNode.Arguments;
        
        // Set up argument indices
        argumentCount = methodArgs.Count;
        for (int i = 0; i < methodArgs.Count; i++)
        {
            arguments[methodArgs[i]] = i;
        }
        
        // Process temporaries
        if (methodNode.Body.Temporaries.Count > 0)
        {
            foreach (var temp in methodNode.Body.Temporaries)
            {
                temporaries[temp] = argumentCount + temporaryCount;
                temporaryCount++;
            }
        }
        
        // Check for primitive
        if (methodNode.Primitive.HasValue)
        {
            primitiveNumber = methodNode.Primitive.Value;
        }
        
        // Compile the method body
        CompileSequence(methodNode.Body);
        
        // Ensure method ends with return self if no explicit return
        if (bytecodes.Count == 0 || !IsReturnBytecode(bytecodes[^1]))
        {
            EmitByte(120); // Return receiver (self)
        }
        
        // Create the CompiledMethod matching existing structure
        var header = new MethodHeader(
            Raw: (primitiveNumber & 0x1FF) | (temporaryCount << 10),
            Extension: 0
        );
        
        var method = new CompiledMethod
        {
            Header = header,
            Bytecodes = bytecodes.ToArray(),
            Literals = literals.ToArray(),
            TemporariesCount = temporaryCount,
            HasPrimitive = primitiveNumber > 0,
            PrimitiveIndex = primitiveNumber,
            SelectorName = selector
        };
        
        return method;
    }
    
    /// <summary>
    /// Compile a sequence of statements.
    /// </summary>
    private void CompileSequence(SequenceNode sequence)
    {
        foreach (var statement in sequence.Statements)
        {
            CompileStatement(statement);
        }
    }
    
    /// <summary>
    /// Compile a statement.
    /// </summary>
    private void CompileStatement(StatementNode statement)
    {
        switch (statement)
        {
            case ExpressionStatement exprStmt:
                CompileExpression(exprStmt.Expression);
                EmitByte(135); // Pop stack top
                break;
                
            case AnswerStatement answer:
                CompileExpression(answer.Expression);
                EmitByte(124); // Return stack top
                break;
                
            case PrimitiveStatement prim:
                // Primitive is handled in method header, not as bytecode
                break;
                
            default:
                throw new CompilationException($"Unknown statement type: {statement.GetType()}");
        }
    }
    
    /// <summary>
    /// Compile an expression.
    /// </summary>
    private void CompileExpression(ExpressionNode expression)
    {
        switch (expression)
        {
            case LiteralExpression literal:
                CompileLiteral(literal.Value);
                break;
                
            case VariableExpression variable:
                CompileVariableRead(variable.Name);
                break;
                
            case AssignmentExpression assignment:
                CompileAssignment(assignment);
                break;
                
            case PseudoVariableExpression pseudo:
                CompilePseudoVariable(pseudo.Name);
                break;
                
            case UnarySendExpression unary:
                CompileUnarySend(unary);
                break;
                
            case BinarySendExpression binary:
                CompileBinarySend(binary);
                break;
                
            case KeywordSendExpression keyword:
                CompileKeywordSend(keyword);
                break;
                
            case CascadeExpression cascade:
                CompileCascade(cascade);
                break;
                
            case BlockExpression block:
                CompileBlock(block);
                break;
                
            default:
                throw new CompilationException($"Unknown expression type: {expression.GetType()}");
        }
    }
    
    /// <summary>
    /// Compile a literal value.
    /// </summary>
    private void CompileLiteral(object value)
    {
        // Special handling for common constants
        if (value is bool boolValue)
        {
            if (boolValue)
                EmitByte(121); // Push true
            else
                EmitByte(122); // Push false
            return;
        }
        
        if (value == null)
        {
            EmitByte(123); // Push nil
            return;
        }
        
        if (value is int intValue)
        {
            // Special bytecodes for small integers
            if (intValue == -1)
            {
                EmitByte(116); // Push -1
                return;
            }
            if (intValue >= 0 && intValue <= 2)
            {
                EmitByte((byte)(117 + intValue)); // Push 0, 1, or 2
                return;
            }
        }
        
        // Add to literal frame
        var index = AddLiteral(value);
        
        // Push literal constant (bytecodes 32-63)
        if (index < 32)
        {
            EmitByte((byte)(32 + index));
        }
        else
        {
            // Extended push for larger indices
            EmitByte(128); // Single extended
            EmitByte(0);   // Push literal constant extended
            EmitByte((byte)index);
        }
    }
    
    /// <summary>
    /// Compile a variable read.
    /// </summary>
    private void CompileVariableRead(string name)
    {
        // Check if it's an argument
        if (arguments.TryGetValue(name, out var argIndex))
        {
            // Arguments are treated as temporaries in bytecode
            if (argIndex < 16)
            {
                EmitByte((byte)(16 + argIndex)); // Push temporary 0-15
            }
            else
            {
                EmitByte(128); // Single extended
                EmitByte(16);  // Push temporary extended
                EmitByte((byte)argIndex);
            }
            return;
        }
        
        // Check if it's a temporary
        if (temporaries.TryGetValue(name, out var tempIndex))
        {
            if (tempIndex < 16)
            {
                EmitByte((byte)(16 + tempIndex)); // Push temporary 0-15
            }
            else
            {
                EmitByte(128); // Single extended
                EmitByte(16);  // Push temporary extended
                EmitByte((byte)tempIndex);
            }
            return;
        }
        
        // Check if it's an instance variable
        if (context.InstanceVariables.TryGetValue(name, out var instVarIndex))
        {
            if (instVarIndex < 16)
            {
                EmitByte((byte)instVarIndex); // Push receiver variable 0-15
            }
            else
            {
                EmitByte(128); // Single extended
                EmitByte(0);   // Push receiver variable extended
                EmitByte((byte)instVarIndex);
            }
            return;
        }
        
        // Must be a global variable - for now, create a placeholder Association
        // In a real implementation, we'd look up the global in the Smalltalk dictionary
        // For Phase 2, we'll create an Association with nil value that will be resolved at runtime
        var assoc = new Association(new Symbol(name), null);
        var index = AddLiteral(assoc);
        
        // Push literal variable (bytecodes 64-95)
        if (index < 32)
        {
            EmitByte((byte)(64 + index));
        }
        else
        {
            EmitByte(128); // Single extended
            EmitByte(64);  // Push literal variable extended
            EmitByte((byte)index);
        }
    }
    
    /// <summary>
    /// Compile an assignment.
    /// </summary>
    private void CompileAssignment(AssignmentExpression assignment)
    {
        // Compile the value expression
        CompileExpression(assignment.Value);
        
        var name = assignment.Variable;
        
        // Check if it's a temporary
        if (temporaries.TryGetValue(name, out var tempIndex))
        {
            EmitByte(129); // Extended store
            EmitByte(96);  // Store into temporary
            EmitByte((byte)tempIndex);
            return;
        }
        
        // Check if it's an instance variable
        if (context.InstanceVariables.TryGetValue(name, out var instVarIndex))
        {
            if (instVarIndex < 8)
            {
                EmitByte((byte)(96 + instVarIndex)); // Pop and store receiver variable 0-7
            }
            else
            {
                EmitByte(129); // Extended store
                EmitByte(96);  // Store into receiver variable
                EmitByte((byte)instVarIndex);
            }
            return;
        }
        
        // Global variable assignment - create placeholder Association
        var assoc = new Association(new Symbol(name), null);
        var index = AddLiteral(assoc);
        
        EmitByte(129); // Extended store
        EmitByte(224); // Store into literal variable
        EmitByte((byte)index);
    }
    
    /// <summary>
    /// Compile a pseudo-variable.
    /// </summary>
    private void CompilePseudoVariable(string name)
    {
        switch (name)
        {
            case "self":
                EmitByte(112); // Push receiver
                break;
            case "true":
                EmitByte(121); // Push true
                break;
            case "false":
                EmitByte(122); // Push false
                break;
            case "nil":
                EmitByte(123); // Push nil
                break;
            case "super":
                EmitByte(112); // Push receiver (super uses same receiver)
                break;
            case "thisContext":
                EmitByte(137); // Push active context
                break;
            default:
                throw new CompilationException($"Unknown pseudo-variable: {name}");
        }
    }
    
    /// <summary>
    /// Compile a unary message send.
    /// </summary>
    private void CompileUnarySend(UnarySendExpression unary)
    {
        // Compile receiver
        CompileExpression(unary.Receiver);
        
        // Add selector to literals
        var selectorSymbol = new Symbol(unary.Selector);
        var index = AddLiteral(selectorSymbol);
        
        // Send with 0 arguments
        CompileSend(index, 0, unary.Receiver is PseudoVariableExpression { Name: "super" });
    }
    
    /// <summary>
    /// Compile a binary message send.
    /// </summary>
    private void CompileBinarySend(BinarySendExpression binary)
    {
        // Compile receiver
        CompileExpression(binary.Receiver);
        
        // Compile argument
        CompileExpression(binary.Argument);
        
        // Add selector to literals
        var selectorSymbol = new Symbol(binary.Selector);
        var index = AddLiteral(selectorSymbol);
        
        // Send with 1 argument
        CompileSend(index, 1, binary.Receiver is PseudoVariableExpression { Name: "super" });
    }
    
    /// <summary>
    /// Compile a keyword message send.
    /// </summary>
    private void CompileKeywordSend(KeywordSendExpression keyword)
    {
        // Compile receiver
        CompileExpression(keyword.Receiver);
        
        // Compile arguments
        foreach (var arg in keyword.Arguments)
        {
            CompileExpression(arg);
        }
        
        // Build full selector
        var fullSelector = string.Join("", keyword.SelectorParts);
        var selectorSymbol = new Symbol(fullSelector);
        var index = AddLiteral(selectorSymbol);
        
        // Send with N arguments
        CompileSend(index, keyword.Arguments.Count, keyword.Receiver is PseudoVariableExpression { Name: "super" });
    }
    
    /// <summary>
    /// Compile a cascade expression.
    /// </summary>
    private void CompileCascade(CascadeExpression cascade)
    {
        // Compile the receiver once
        CompileExpression(cascade.Receiver);
        
        // For each message except the last, duplicate the receiver
        for (int i = 0; i < cascade.Messages.Count - 1; i++)
        {
            EmitByte(136); // Duplicate stack top
            CompileCascadeMessage(cascade.Messages[i]);
            EmitByte(135); // Pop (discard result)
        }
        
        // Compile the last message (leave result on stack)
        CompileCascadeMessage(cascade.Messages[^1]);
    }
    
    /// <summary>
    /// Compile a single message in a cascade.
    /// </summary>
    private void CompileCascadeMessage(ExpressionNode message)
    {
        // The receiver is already on the stack
        switch (message)
        {
            case UnarySendExpression unary:
                var unaryIndex = AddLiteral(new Symbol(unary.Selector));
                CompileSend(unaryIndex, 0, false);
                break;
                
            case BinarySendExpression binary:
                CompileExpression(binary.Argument);
                var binaryIndex = AddLiteral(new Symbol(binary.Selector));
                CompileSend(binaryIndex, 1, false);
                break;
                
            case KeywordSendExpression keyword:
                foreach (var arg in keyword.Arguments)
                {
                    CompileExpression(arg);
                }
                var keywordSelector = string.Join("", keyword.SelectorParts);
                var keywordIndex = AddLiteral(new Symbol(keywordSelector));
                CompileSend(keywordIndex, keyword.Arguments.Count, false);
                break;
                
            default:
                throw new CompilationException($"Invalid cascade message type: {message.GetType()}");
        }
    }
    
    /// <summary>
    /// Compile a block expression.
    /// </summary>
    private void CompileBlock(BlockExpression block)
    {
        // Blocks are complex - for now, create a simple block
        // The Blue Book block compilation requires:
        // 1. Push block context
        // 2. Jump around block body
        // 3. Block body bytecodes
        // 4. Block return
        
        // For Phase 2, we'll implement a simplified version
        throw new NotImplementedException("Block compilation not yet implemented");
    }
    
    /// <summary>
    /// Compile a send bytecode.
    /// </summary>
    private void CompileSend(int selectorIndex, int argCount, bool isSuper)
    {
        // Check for special selectors (optimization)
        // For now, use regular send bytecodes
        
        if (isSuper)
        {
            // Super send
            EmitByte(133); // Super send selector
            EmitByte((byte)argCount);
            EmitByte((byte)selectorIndex);
        }
        else if (argCount == 0 && selectorIndex < 16)
        {
            // Send literal selector 0-15 with 0 args (176-191)
            EmitByte((byte)(176 + selectorIndex));
        }
        else if (argCount == 1 && selectorIndex < 16)
        {
            // Send literal selector 0-15 with 1 arg (192-207)
            EmitByte((byte)(192 + selectorIndex));
        }
        else if (argCount == 2 && selectorIndex < 16)
        {
            // Send literal selector 0-15 with 2 args (208-223)
            EmitByte((byte)(208 + selectorIndex));
        }
        else
        {
            // Extended send
            EmitByte(131); // Double extended
            EmitByte((byte)(argCount << 5)); // Argument count in high 3 bits
            EmitByte((byte)selectorIndex);
        }
    }
    
    /// <summary>
    /// Add a literal to the literal frame.
    /// </summary>
    private int AddLiteral(object literal)
    {
        // Check if already exists
        if (literalIndex.TryGetValue(literal, out var index))
        {
            return index;
        }
        
        // Add new literal
        index = literals.Count;
        literals.Add(literal);
        literalIndex[literal] = index;
        return index;
    }
    
    /// <summary>
    /// Emit a bytecode.
    /// </summary>
    private void EmitByte(byte bytecode)
    {
        bytecodes.Add(bytecode);
    }
    
    /// <summary>
    /// Check if a bytecode is a return instruction.
    /// </summary>
    private bool IsReturnBytecode(byte bytecode)
    {
        return bytecode >= 120 && bytecode <= 125;
    }
}