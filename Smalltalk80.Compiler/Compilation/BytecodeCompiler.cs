using System.Collections.Immutable;
using System.Text.Json;
using Smalltalk80.Compiler.AST;
using Smalltalk80.Compiler.AST.Processors;

namespace Smalltalk80.Compiler.Compilation;

/// <summary>
/// Compiles AST to Smalltalk-80 bytecode in JSON format.
/// </summary>
public sealed class BytecodeCompiler : IAstProcessor, IAstVisitor<object?>
{
    private readonly BytecodeEmitter _emitter = new();
    private readonly CompilationEnvironment _environment = new();
    private readonly Stack<bool> _needsValueStack = new();
    private ProcessorContext? _context;

    /// <inheritdoc/>
    public string Name => "Bytecode Compiler";

    /// <inheritdoc/>
    public ProcessorResult Process(AstNode node, ProcessorContext context)
    {
        _context = context;
        _emitter.Clear();
        _environment.Clear();
        _needsValueStack.Clear();

        try
        {
            // Visit the AST to generate bytecode
            node.Accept(this);

            // Generate JSON output
            var json = GenerateJsonOutput();
            return new BytecodeResult(json);
        }
        catch (Exception ex)
        {
            return BytecodeResult.Failure($"Compilation failed: {ex.Message}");
        }
    }

    private string GenerateJsonOutput()
    {
        var bytecodeArray = _emitter.GetBytecode();
        var output = new
        {
            bytecode = bytecodeArray.Select(b => (int)b).ToArray(),  // Convert bytes to int array
            literals = _emitter.GetLiterals().Select(ConvertLiteral).ToArray(),
            numArgs = _environment.ArgumentCount,
            numTemps = _environment.TemporaryCount,
            stackDepth = _emitter.MaxStackDepth,
            primitive = _environment.PrimitiveNumber,
            source = _context?.SourceCode ?? string.Empty
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(output, options);
    }

    private object ConvertLiteral(object literal)
    {
        return literal switch
        {
            Symbol sym => $"#{sym.Name}",
            Association assoc => $"Association(key: #{assoc.Key}, value: {assoc.Value ?? "null"})",
            object[] array => array.Select(ConvertLiteral).ToArray(),
            _ => literal
        };
    }

    // Program structure visitors
    public object? VisitProgram(ProgramNode node)
    {
        foreach (var element in node.Elements)
        {
            element.Accept(this);
        }
        return null;
    }

    public object? VisitClassDefinition(ClassDefinitionNode node)
    {
        // Store class information in environment
        _environment.SetClass(node.ClassName, node.InstanceVariables, node.ClassVariables);

        // Compile each method
        foreach (var method in node.Methods)
        {
            method.Accept(this);
        }
        return null;
    }

    public object? VisitMethodDefinition(MethodDefinitionNode node)
    {
        // Clear for new method
        _emitter.Clear();
        _environment.ClearMethod();

        // Set up method environment
        _environment.SetMethod(node.Selector, node.Arguments.Length);

        // Register arguments as temporaries (they occupy the first temp slots)
        for (int i = 0; i < node.Arguments.Length; i++)
        {
            _environment.AddArgument(node.Arguments[i].Name, i);
        }

        // Register temporary variables
        int tempIndex = node.Arguments.Length;
        foreach (var temp in node.Temporaries)
        {
            _environment.AddTemporary(temp.Name, tempIndex++);
        }

        // Handle primitive declaration
        if (node.Primitive != null)
        {
            _environment.PrimitiveNumber = node.Primitive.PrimitiveNumber;
        }

        // First pass: collect literals
        var literalCollector = new LiteralCollectorVisitor(_emitter);
        node.Body.Accept(literalCollector);

        // Second pass: generate bytecode
        _needsValueStack.Push(false);
        node.Body.Accept(this);
        _needsValueStack.Pop();

        // Add implicit return self if needed
        if (!_emitter.LastInstructionIsReturn())
        {
            _emitter.ReturnSelf();
        }

        return null;
    }

    // Statement visitors
    public object? VisitStatementList(StatementListNode node)
    {
        for (int i = 0; i < node.Statements.Length; i++)
        {
            bool isLast = i == node.Statements.Length - 1;
            bool needsValue = isLast && _needsValueStack.Count > 0 && _needsValueStack.Peek();

            _needsValueStack.Push(needsValue);
            node.Statements[i].Accept(this);
            _needsValueStack.Pop();

            // Pop intermediate values except for the last one if needed
            if (!isLast && !needsValue)
            {
                _emitter.Pop();
            }
        }
        return null;
    }

    public object? VisitReturnStatement(ReturnStatementNode node)
    {
        _needsValueStack.Push(true);
        node.Expression.Accept(this);
        _needsValueStack.Pop();

        _emitter.ReturnTop();
        return null;
    }

    public object? VisitExpressionStatement(ExpressionStatementNode node)
    {
        bool needsValue = _needsValueStack.Count > 0 && _needsValueStack.Peek();
        _needsValueStack.Push(needsValue);
        node.Expression.Accept(this);
        _needsValueStack.Pop();
        return null;
    }

    // Expression visitors
    public object? VisitAssignment(AssignmentNode node)
    {
        // Compile the value expression first
        _needsValueStack.Push(true);
        node.Value.Accept(this);
        _needsValueStack.Pop();

        // Store to the variable
        bool leaveOnStack = _needsValueStack.Count > 0 && _needsValueStack.Peek();
        StoreVariable(node.Variable, leaveOnStack);

        return null;
    }

    public object? VisitCascade(CascadeNode node)
    {
        // Compile receiver once
        _needsValueStack.Push(true);
        node.Receiver.Accept(this);
        _needsValueStack.Pop();

        // Send each message in the cascade
        for (int i = 0; i < node.Messages.Length; i++)
        {
            bool isLast = i == node.Messages.Length - 1;

            if (!isLast)
            {
                _emitter.Dup(); // Duplicate receiver for next message
            }

            CompileMessage(node.Messages[i]);

            if (!isLast)
            {
                _emitter.Pop(); // Pop result, keep receiver
            }
        }

        return null;
    }

    public object? VisitMessageSend(MessageSendNode node)
    {
        // Check for super send
        bool isSuper = node.Receiver is SuperLiteralNode;

        // Compile receiver
        _needsValueStack.Push(true);
        node.Receiver.Accept(this);
        _needsValueStack.Pop();

        // Compile and send message
        CompileMessage(node.Message, isSuper);

        return null;
    }

    public object? VisitBlock(BlockNode node)
    {
        // Emit blockCopy: with argument count
        _emitter.EmitBlockCopy(node.Arguments.Length);
        int blockCopyPos = _emitter.CurrentPosition - 1;

        // Jump over block body
        _emitter.EmitJump(0); // Placeholder
        int blockStartPos = _emitter.CurrentPosition;

        // Enter block scope
        _environment.EnterBlockScope();

        // Register block arguments
        for (int i = 0; i < node.Arguments.Length; i++)
        {
            _environment.AddBlockArgument(node.Arguments[i].Name, i);
        }

        // Register block temporaries
        int tempIndex = node.Arguments.Length;
        foreach (var temp in node.Temporaries)
        {
            _environment.AddBlockTemporary(temp.Name, tempIndex++);
        }

        // Compile block body
        _needsValueStack.Push(true);
        node.Body.Accept(this);
        _needsValueStack.Pop();

        // Block return
        _emitter.ReturnTopFromBlock();

        // Exit block scope
        _environment.ExitBlockScope();

        // Patch jump distance
        int blockSize = _emitter.CurrentPosition - blockStartPos;
        _emitter.PatchBlockSize(blockCopyPos, blockSize);

        return null;
    }

    public object? VisitVariable(VariableNode node)
    {
        PushVariable(node.Name);
        return null;
    }

    // Literal visitors
    public object? VisitNumberLiteral(NumberLiteralNode node)
    {
        _emitter.PushLiteralConstant(node.Value);
        return null;
    }

    public object? VisitStringLiteral(StringLiteralNode node)
    {
        _emitter.PushLiteralConstant(node.Value);
        return null;
    }

    public object? VisitSymbolLiteral(SymbolLiteralNode node)
    {
        _emitter.PushLiteralConstant(new Symbol(node.Value));
        return null;
    }

    public object? VisitArrayLiteral(ArrayLiteralNode node)
    {
        var elements = new object[node.Elements.Length];
        for (int i = 0; i < node.Elements.Length; i++)
        {
            elements[i] = ExtractLiteralValue(node.Elements[i]);
        }
        _emitter.PushLiteralConstant(elements);
        return null;
    }

    public object? VisitCharacterLiteral(CharacterLiteralNode node)
    {
        _emitter.PushLiteralConstant(node.Value);
        return null;
    }

    public object? VisitBooleanLiteral(BooleanLiteralNode node)
    {
        if (node.Value)
            _emitter.PushTrue();
        else
            _emitter.PushFalse();
        return null;
    }

    public object? VisitNilLiteral(NilLiteralNode node)
    {
        _emitter.PushNil();
        return null;
    }

    public object? VisitSelfLiteral(SelfLiteralNode node)
    {
        _emitter.PushSelf();
        return null;
    }

    public object? VisitSuperLiteral(SuperLiteralNode node)
    {
        _emitter.PushSelf(); // Super uses self as receiver
        return null;
    }

    // Message visitors
    public object? VisitUnaryMessage(UnaryMessageNode node)
    {
        // Handled by CompileMessage
        return null;
    }

    public object? VisitBinaryMessage(BinaryMessageNode node)
    {
        // Handled by CompileMessage  
        return null;
    }

    public object? VisitKeywordMessage(KeywordMessageNode node)
    {
        // Handled by CompileMessage
        return null;
    }

    // Declaration visitors
    public object? VisitTemporaryDeclaration(TemporaryDeclarationNode node)
    {
        // Handled during method setup
        return null;
    }

    public object? VisitBlockArgument(BlockArgumentNode node)
    {
        // Handled during block setup
        return null;
    }

    public object? VisitMethodArgument(MethodArgumentNode node)
    {
        // Handled during method setup
        return null;
    }

    public object? VisitPrimitiveDeclaration(PrimitiveDeclarationNode node)
    {
        // Handled during method setup
        return null;
    }

    // Helper methods
    private void CompileMessage(MessageNode message, bool isSuper = false)
    {
        // Compile arguments
        var arguments = message.GetArguments();
        foreach (var arg in arguments)
        {
            _needsValueStack.Push(true);
            arg.Accept(this);
            _needsValueStack.Pop();
        }

        // Send the message
        string selector = message.GetSelector();
        int argCount = arguments.Length;

        if (isSuper)
        {
            _emitter.EmitSuperSend(selector, argCount);
        }
        else
        {
            _emitter.EmitSend(selector, argCount);
        }
    }

    private void PushVariable(string name)
    {
        // Check for pseudo-variables
        switch (name)
        {
            case "self":
                _emitter.PushSelf();
                return;
            case "super":
                _emitter.PushSelf(); // Super uses self as receiver
                return;
            case "true":
                _emitter.PushTrue();
                return;
            case "false":
                _emitter.PushFalse();
                return;
            case "nil":
                _emitter.PushNil();
                return;
        }

        // Check for arguments and temporaries
        if (_environment.TryGetTemporary(name, out int tempIndex))
        {
            _emitter.PushTemporary(tempIndex);
            return;
        }

        // Check for instance variables
        if (_environment.TryGetInstanceVariable(name, out int instIndex))
        {
            _emitter.PushReceiverVariable(instIndex);
            return;
        }

        // Must be a class or global variable
        _emitter.PushLiteralVariable(name);
    }

    private void StoreVariable(string name, bool leaveOnStack)
    {
        // Check for temporaries
        if (_environment.TryGetTemporary(name, out int tempIndex))
        {
            if (leaveOnStack)
                _emitter.StoreTemporary(tempIndex);
            else
                _emitter.PopAndStoreTemporary(tempIndex);
            return;
        }

        // Check for instance variables  
        if (_environment.TryGetInstanceVariable(name, out int instIndex))
        {
            if (leaveOnStack)
                _emitter.StoreReceiverVariable(instIndex);
            else
                _emitter.PopAndStoreReceiverVariable(instIndex);
            return;
        }

        // Must be a class or global variable
        if (leaveOnStack)
            _emitter.StoreLiteralVariable(name);
        else
            _emitter.PopAndStoreLiteralVariable(name);
    }

    private object ExtractLiteralValue(LiteralNode literal)
    {
        return literal switch
        {
            NumberLiteralNode num => num.Value,
            StringLiteralNode str => str.Value,
            SymbolLiteralNode sym => new Symbol(sym.Value),
            CharacterLiteralNode chr => chr.Value,
            ArrayLiteralNode arr => arr.Elements.Select(ExtractLiteralValue).ToArray(),
            _ => throw new NotSupportedException($"Unsupported literal type: {literal.GetType()}")
        };
    }
}