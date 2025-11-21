using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Helper methods needed by extracted VM components.
/// Provides access to common VM operations without circular dependencies.
/// </summary>
internal interface IObjectMetadataAccessor
{
    /// <summary>
    /// Gets the class OOP of an object.
    /// </summary>
    SmalltalkOOP GetClass(SmalltalkOOP objectOOP);

    /// <summary>
    /// Gets the class name as a string.
    /// </summary>
    string GetClassName(SmalltalkOOP classOOP);

    /// <summary>
    /// Traverses the home chain to find the MethodContext for a given context.
    /// BlockContexts link to their home MethodContext via the 'home' field.
    /// </summary>
    SmalltalkOOP FindMethodContext(SmalltalkOOP contextOOP);

    /// <summary>
    /// Parses a CompiledMethod header to extract argument count, temporary count, etc.
    /// </summary>
    MethodHeaderInfo ParseMethodHeader(SmalltalkOOP methodOOP);
}

/// <summary>
/// Parsed information from a CompiledMethod header.
/// Smalltalk-80 method headers encode metadata in a 15-bit field:
///   Bits 0-5   (6 bits): numLiterals (0-63)
///   Bit  6     (1 bit):  needsLargeFrame flag
///   Bits 7-11  (5 bits): numTemps (0-31)
///   Bits 12-14 (3 bits): flags field
/// </summary>
internal record MethodHeaderInfo(
    int NumLiterals,
    bool NeedsLargeFrame,
    int NumTemps,
    int Flags,
    int NumArgs,
    int Primitive,
    bool IsQuickReturnSelf,
    bool IsQuickReturnField,
    int QuickReturnFieldIndex);

/// <summary>
/// Stack operations for the operand stack.
/// </summary>
internal interface IStackOperations
{
    /// <summary>Pushes a value onto the operand stack.</summary>
    void Push(SmalltalkOOP valueOOP);

    /// <summary>Pops a value from the operand stack.</summary>
    SmalltalkOOP Pop();

    /// <summary>Peeks at the top value on the operand stack without removing it.</summary>
    SmalltalkOOP Peek();
}

/// <summary>
/// Non-local return state for block returns.
/// </summary>
internal interface INonLocalReturnState
{
    /// <summary>Gets or sets whether a non-local return is pending.</summary>
    bool NonLocalReturnPending { get; set; }

    /// <summary>Gets or sets the target context for a non-local return.</summary>
    SmalltalkOOP NLRTargetContext { get; set; }

    /// <summary>Gets or sets the value to return during a non-local return.</summary>
    SmalltalkOOP NLRValue { get; set; }
}

/// <summary>
/// Access to CompiledMethod literals and bytecodes.
/// </summary>
internal interface IMethodAccess
{
    /// <summary>Gets a literal from a CompiledMethod's literal frame.</summary>
    SmalltalkOOP GetMethodLiteral(SmalltalkOOP methodOOP, int index);

    /// <summary>Gets the bytecode array from a CompiledMethod.</summary>
    byte[] GetMethodBytecodes(SmalltalkOOP methodOOP);
}

/// <summary>
/// Message sending operations for bytecode interpreter.
/// </summary>
internal interface IMessageSender
{
    /// <summary>
    /// Sends a message and returns true if execution should continue normally.
    /// Sets ContextSwitchPending if a context switch occurred.
    /// </summary>
    bool SendMessageAndContinue(SmalltalkOOP receiverOOP, SmalltalkOOP selectorOOP, SmalltalkOOP[] args);

    /// <summary>
    /// Sends a super message and returns true if execution should continue normally.
    /// Sets ContextSwitchPending if a context switch occurred.
    /// </summary>
    bool SendSuperMessageAndContinue(SmalltalkOOP receiverOOP, SmalltalkOOP selectorOOP, SmalltalkOOP[] args);
}

/// <summary>
/// Extended helper interface for bytecode execution.
/// Aggregates focused interfaces for complete bytecode interpretation support.
/// </summary>
internal interface IBytecodeContext : IObjectMetadataAccessor, IStackOperations, INonLocalReturnState, IMethodAccess, IMessageSender
{
    /// <summary>Gets the name of a symbol as a string.</summary>
    string? GetSymbolName(SmalltalkOOP symbolOOP);

    /// <summary>Gets the local ID for an object (used for debugging/diagnostics).</summary>
    string? GetLocalId(SmalltalkOOP objectOOP);

    /// <summary>Gets a temporary variable using unified temp frame architecture.</summary>
    SmalltalkOOP GetTemporaryVariable(SmalltalkOOP contextOOP, int index);

    /// <summary>Sets a temporary variable using unified temp frame architecture.</summary>
    void SetTemporaryVariable(SmalltalkOOP contextOOP, int index, SmalltalkOOP valueOOP);

    /// <summary>Resolves a pool variable and patches the literal for fast subsequent access.</summary>
    SmalltalkOOP ResolveAndPatchPoolVariable(SmalltalkOOP poolLiteralOOP, SmalltalkOOP methodOOP, int literalIndex, SmalltalkOOP poolNameSymbol, SmalltalkOOP keySymbol);

    /// <summary>Looks up a key in a Dictionary and returns the value.</summary>
    SmalltalkOOP LookupInDictionary(SmalltalkOOP dictionaryOOP, SmalltalkOOP keySymbol);

    /// <summary>Gets or sets whether a context switch is pending.</summary>
    bool ContextSwitchPending { get; set; }

    /// <summary>Dumps the stack for debugging purposes.</summary>
    void DumpStack(string label);

    /// <summary>Gets the active context from the context manager.</summary>
    SmalltalkOOP ActiveContext { get; }

    /// <summary>Checks if a target context is in the sender chain.</summary>
    bool IsInSenderChain(SmalltalkOOP targetOOP, SmalltalkOOP startOOP);

    /// <summary>Adds an Association to a class's classPool Dictionary if it doesn't already exist.</summary>
    void AddAssociationToClassPoolIfNeeded(SmalltalkOOP classOOP, SmalltalkOOP associationOOP);
}
