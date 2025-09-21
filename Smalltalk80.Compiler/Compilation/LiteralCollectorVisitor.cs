using Smalltalk80.Compiler.AST;

namespace Smalltalk80.Compiler.Compilation;

/// <summary>
/// First-pass visitor that collects all literals for the literal frame.
/// </summary>
internal sealed class LiteralCollectorVisitor : IAstVisitor
{
    private readonly BytecodeEmitter _emitter;

    public LiteralCollectorVisitor(BytecodeEmitter emitter)
    {
        _emitter = emitter;
    }

    // Program structure - traverse children
    public void VisitProgram(ProgramNode node)
    {
        foreach (var element in node.Elements)
            element.Accept(this);
    }

    public void VisitClassDefinition(ClassDefinitionNode node)
    {
        foreach (var method in node.Methods)
            method.Accept(this);
    }

    public void VisitMethodDefinition(MethodDefinitionNode node)
    {
        node.Body.Accept(this);
    }

    // Statements - traverse children
    public void VisitStatementList(StatementListNode node)
    {
        foreach (var statement in node.Statements)
            statement.Accept(this);
    }

    public void VisitReturnStatement(ReturnStatementNode node)
    {
        node.Expression.Accept(this);
    }

    public void VisitExpressionStatement(ExpressionStatementNode node)
    {
        node.Expression.Accept(this);
    }

    // Expressions - traverse children and collect relevant literals
    public void VisitAssignment(AssignmentNode node)
    {
        // Check if variable is a class/global variable
        if (!IsLocalVariable(node.Variable))
        {
            _emitter.AddLiteral(new Association(node.Variable, null));
        }
        node.Value.Accept(this);
    }

    public void VisitCascade(CascadeNode node)
    {
        node.Receiver.Accept(this);
        foreach (var message in node.Messages)
            CollectMessageLiterals(message);
    }

    public void VisitMessageSend(MessageSendNode node)
    {
        node.Receiver.Accept(this);
        CollectMessageLiterals(node.Message);
    }

    public void VisitBlock(BlockNode node)
    {
        // blockCopy: selector
        _emitter.AddLiteral(new Symbol("blockCopy:"));
        node.Body.Accept(this);
    }

    public void VisitVariable(VariableNode node)
    {
        // Check if it's a class/global variable
        if (!IsLocalVariable(node.Name) && !IsPseudoVariable(node.Name))
        {
            _emitter.AddLiteral(new Association(node.Name, null));
        }
    }

    // Literals - collect literal values
    public void VisitNumberLiteral(NumberLiteralNode node)
    {
        _emitter.AddLiteral(node.Value);
    }

    public void VisitStringLiteral(StringLiteralNode node)
    {
        _emitter.AddLiteral(node.Value);
    }

    public void VisitSymbolLiteral(SymbolLiteralNode node)
    {
        _emitter.AddLiteral(new Symbol(node.Value));
    }

    public void VisitArrayLiteral(ArrayLiteralNode node)
    {
        var elements = new object[node.Elements.Length];
        for (int i = 0; i < node.Elements.Length; i++)
        {
            elements[i] = ExtractLiteralValue(node.Elements[i]);
        }
        _emitter.AddLiteral(elements);
    }

    public void VisitCharacterLiteral(CharacterLiteralNode node)
    {
        _emitter.AddLiteral(node.Value);
    }

    public void VisitBooleanLiteral(BooleanLiteralNode node)
    {
        // Booleans are pseudo-variables, not literals
    }

    public void VisitNilLiteral(NilLiteralNode node)
    {
        // Nil is a pseudo-variable, not a literal
    }

    public void VisitSelfLiteral(SelfLiteralNode node)
    {
        // Self is a pseudo-variable, not a literal
    }

    public void VisitSuperLiteral(SuperLiteralNode node)
    {
        // Super is a pseudo-variable, not a literal
    }

    // Message components
    public void VisitUnaryMessage(UnaryMessageNode node)
    {
        _emitter.AddLiteral(new Symbol(node.Selector));
    }

    public void VisitBinaryMessage(BinaryMessageNode node)
    {
        if (!IsSpecialSelector(node.Selector))
        {
            _emitter.AddLiteral(new Symbol(node.Selector));
        }
        node.Argument.Accept(this);
    }

    public void VisitKeywordMessage(KeywordMessageNode node)
    {
        string selector = node.GetSelector();
        if (!IsSpecialSelector(selector))
        {
            _emitter.AddLiteral(new Symbol(selector));
        }
        foreach (var arg in node.Arguments)
            arg.Accept(this);
    }

    // Declarations - nothing to collect
    public void VisitTemporaryDeclaration(TemporaryDeclarationNode node) { }
    public void VisitBlockArgument(BlockArgumentNode node) { }
    public void VisitMethodArgument(MethodArgumentNode node) { }
    public void VisitPrimitiveDeclaration(PrimitiveDeclarationNode node) { }

    // Helper methods
    private void CollectMessageLiterals(MessageNode message)
    {
        message.Accept(this);
    }

    private bool IsLocalVariable(string name)
    {
        // This is a simplified check - in reality, we'd need access to the environment
        // For literal collection, we assume anything that could be local is local
        return false;
    }

    private bool IsPseudoVariable(string name)
    {
        return name is "self" or "super" or "true" or "false" or "nil";
    }

    private bool IsSpecialSelector(string selector)
    {
        // Special selectors that don't need to be in the literal frame
        return selector is "+" or "-" or "<" or ">" or "<=" or ">=" or "=" or "~=" or
               "*" or "/" or "\\\\" or "@" or "bitShift:" or "//" or "bitAnd:" or "bitOr:" or
               "at:" or "at:put:" or "size" or "next" or "nextPut:" or "atEnd" or
               "==" or "class" or "blockCopy:" or "value" or "value:" or "do:" or
               "new" or "new:" or "x" or "y";
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