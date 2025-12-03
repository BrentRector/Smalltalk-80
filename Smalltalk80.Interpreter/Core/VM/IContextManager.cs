using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Manages execution contexts for the Smalltalk-80 VM.
/// Handles context creation, pooling, switching, and chain traversal.
/// </summary>
public interface IContextManager
{
    /// <summary>
    /// Gets the currently active execution context.
    /// </summary>
    SmalltalkOOP ActiveContext { get; }

    /// <summary>
    /// Creates a MethodContext for executing a method.
    /// </summary>
    /// <param name="methodOOP">CompiledMethod OOP to execute.</param>
    /// <param name="receiverOOP">Receiver OOP for this activation.</param>
    /// <param name="arguments">Method arguments as OOPs.</param>
    /// <param name="callerContextOOP">Optional caller context (sender), default for top-level DoIts.</param>
    /// <returns>Allocated and initialized MethodContext OOP.</returns>
    SmalltalkOOP CreateMethodContext(SmalltalkOOP methodOOP, SmalltalkOOP receiverOOP, SmalltalkOOP[] arguments, SmalltalkOOP callerContextOOP = default);

    /// <summary>
    /// Sets the active context, used during execution and context switching.
    /// </summary>
    /// <param name="contextOOP">The context to make active.</param>
    void SetActiveContext(SmalltalkOOP contextOOP);

    /// <summary>
    /// Finds the ultimate MethodContext by traversing the home chain.
    /// BlockContexts link to their home MethodContext via the 'home' field.
    /// </summary>
    /// <param name="contextOOP">Starting context (may be MethodContext or BlockContext).</param>
    /// <returns>The MethodContext at the end of the home chain.</returns>
    SmalltalkOOP FindMethodContext(SmalltalkOOP contextOOP);

    /// <summary>
    /// Checks if a context is in the sender chain of another context.
    /// </summary>
    /// <param name="contextToFind">Context to search for.</param>
    /// <param name="startContext">Context to start searching from.</param>
    /// <returns>True if contextToFind is in the sender chain of startContext.</returns>
    bool IsInSenderChain(SmalltalkOOP contextToFind, SmalltalkOOP startContext);

    /// <summary>
    /// Adds a returned context to the pool for reuse.
    /// Contexts in the active sender chain will not be pooled.
    /// </summary>
    /// <param name="contextOOP">Context to add to pool.</param>
    void AddContextToPool(SmalltalkOOP contextOOP);

    /// <summary>
    /// Initializes the ContextManager and registers for diagnostic events.
    /// Must be called after construction to enable pool clearing before GC.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Marks a context as having created one or more BlockContexts.
    /// Such contexts cannot be pooled because their blocks may outlive them
    /// and still reference them via the 'home' field.
    /// </summary>
    /// <param name="contextOOP">The MethodContext that created a block.</param>
    void MarkContextCreatedBlock(SmalltalkOOP contextOOP);
}
