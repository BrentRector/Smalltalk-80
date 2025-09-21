namespace Smalltalk80.Compiler.AST;

/// <summary>
/// Visitor interface for processing AST nodes with return values.
/// </summary>
/// <typeparam name="T">The type of value returned by visitor methods.</typeparam>
public interface IAstVisitor<T>
{
    // Program structure
    T VisitProgram(ProgramNode node);
    T VisitClassDefinition(ClassDefinitionNode node);
    T VisitMethodDefinition(MethodDefinitionNode node);
    
    // Statements
    T VisitStatementList(StatementListNode node);
    T VisitReturnStatement(ReturnStatementNode node);
    T VisitExpressionStatement(ExpressionStatementNode node);
    
    // Expressions
    T VisitAssignment(AssignmentNode node);
    T VisitCascade(CascadeNode node);
    T VisitMessageSend(MessageSendNode node);
    T VisitBlock(BlockNode node);
    T VisitVariable(VariableNode node);
    
    // Literals
    T VisitNumberLiteral(NumberLiteralNode node);
    T VisitStringLiteral(StringLiteralNode node);
    T VisitSymbolLiteral(SymbolLiteralNode node);
    T VisitArrayLiteral(ArrayLiteralNode node);
    T VisitCharacterLiteral(CharacterLiteralNode node);
    T VisitBooleanLiteral(BooleanLiteralNode node);
    T VisitNilLiteral(NilLiteralNode node);
    T VisitSelfLiteral(SelfLiteralNode node);
    T VisitSuperLiteral(SuperLiteralNode node);
    
    // Message components
    T VisitUnaryMessage(UnaryMessageNode node);
    T VisitBinaryMessage(BinaryMessageNode node);
    T VisitKeywordMessage(KeywordMessageNode node);
    
    // Declarations
    T VisitTemporaryDeclaration(TemporaryDeclarationNode node);
    T VisitBlockArgument(BlockArgumentNode node);
    T VisitMethodArgument(MethodArgumentNode node);
    T VisitPrimitiveDeclaration(PrimitiveDeclarationNode node);
}

/// <summary>
/// Visitor interface for processing AST nodes without return values.
/// </summary>
public interface IAstVisitor
{
    // Program structure
    void VisitProgram(ProgramNode node);
    void VisitClassDefinition(ClassDefinitionNode node);
    void VisitMethodDefinition(MethodDefinitionNode node);
    
    // Statements
    void VisitStatementList(StatementListNode node);
    void VisitReturnStatement(ReturnStatementNode node);
    void VisitExpressionStatement(ExpressionStatementNode node);
    
    // Expressions
    void VisitAssignment(AssignmentNode node);
    void VisitCascade(CascadeNode node);
    void VisitMessageSend(MessageSendNode node);
    void VisitBlock(BlockNode node);
    void VisitVariable(VariableNode node);
    
    // Literals
    void VisitNumberLiteral(NumberLiteralNode node);
    void VisitStringLiteral(StringLiteralNode node);
    void VisitSymbolLiteral(SymbolLiteralNode node);
    void VisitArrayLiteral(ArrayLiteralNode node);
    void VisitCharacterLiteral(CharacterLiteralNode node);
    void VisitBooleanLiteral(BooleanLiteralNode node);
    void VisitNilLiteral(NilLiteralNode node);
    void VisitSelfLiteral(SelfLiteralNode node);
    void VisitSuperLiteral(SuperLiteralNode node);
    
    // Message components
    void VisitUnaryMessage(UnaryMessageNode node);
    void VisitBinaryMessage(BinaryMessageNode node);
    void VisitKeywordMessage(KeywordMessageNode node);
    
    // Declarations
    void VisitTemporaryDeclaration(TemporaryDeclarationNode node);
    void VisitBlockArgument(BlockArgumentNode node);
    void VisitMethodArgument(MethodArgumentNode node);
    void VisitPrimitiveDeclaration(PrimitiveDeclarationNode node);
}