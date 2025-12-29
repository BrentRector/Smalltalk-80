using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Context provided to primitive operations.
/// Provides access to the heap and allows primitives to interact with the VM.
/// </summary>
public interface IPrimitiveContext
{
    /// <summary>
    /// Gets the heap manager instance.
    /// </summary>
    ISmalltalkHeap HeapManager { get; }


    /// <summary>
    /// Gets the platform bridge for platform-exempt primitives (graphics, input, clipboard, etc.).
    /// May be null if running in headless/test mode.
    /// </summary>
    IPlatformBridge? PlatformBridge { get; }

    /// <summary>
    /// Gets or sets the primitive success flag.
    /// Primitives set this to false to signal failure, causing the VM to execute fallback bytecodes.
    /// The VM sets this to true before calling each primitive.
    /// </summary>
    bool PrimitiveSuccess { get; set; }

    // Graphics/Input support methods

    /// <summary>
    /// Gets byte array data from an indexed object (e.g., ByteArray, String, Form bits).
    /// </summary>
    /// <param name="oop">Object pointer to indexed byte object.</param>
    /// <returns>Memory span of bytes, or empty if not a byte array.</returns>
    Memory<byte> GetByteArray(SmalltalkOOP oop);

    /// <summary>
    /// Allocates a new object instance with optional indexed fields.
    /// </summary>
    /// <param name="className">Class name (e.g., "class:Point", "class:ByteArray").</param>
    /// <param name="indexedSize">Number of indexed elements (0 for fixed-size objects).</param>
    /// <returns>SmalltalkOOP of newly allocated instance.</returns>
    SmalltalkOOP AllocateObject(string className, int indexedSize);

    // Dynamic message sending support

    /// <summary>
    /// Sends a message to a receiver object (for perform primitives).
    /// </summary>
    /// <param name="receiverOop">Receiver object pointer.</param>
    /// <param name="selectorOop">Selector Symbol object pointer.</param>
    /// <param name="argumentsArray">Arguments array object pointer (contains indexed arguments).</param>
    /// <returns>Result of message send.</returns>
    SmalltalkOOP SendMessage(SmalltalkOOP receiverOop, SmalltalkOOP selectorOop, SmalltalkOOP argumentsArray);

    // BlockContext support

    /// <summary>
    /// Gets the currently active context (MethodContext or BlockContext OOP).
    /// </summary>
    /// <returns>SmalltalkOOP of the active context.</returns>
    SmalltalkOOP GetActiveContext();

    /// <summary>
    /// Gets the current instruction pointer (PC) of the active context.
    /// </summary>
    /// <returns>Instruction pointer value.</returns>
    int GetInstructionPointer();

    /// <summary>
    /// Creates a BlockContext for a block closure.
    /// </summary>
    /// <param name="homeContextOop">Home context (MethodContext where block is defined).</param>
    /// <param name="startPC">Starting program counter for the block.</param>
    /// <param name="argCount">Number of arguments the block expects.</param>
    /// <returns>SmalltalkOOP of newly created BlockContext.</returns>
    SmalltalkOOP CreateBlockContext(SmalltalkOOP homeContextOop, int startPC, int argCount);

    /// <summary>
    /// Activates a BlockContext with given arguments (for value/value: primitives).
    /// Switches execution to the block and returns the result.
    /// </summary>
    /// <param name="blockContextOop">BlockContext to activate.</param>
    /// <param name="argumentsArray">Arguments array object pointer.</param>
    /// <returns>Result of block execution.</returns>
    SmalltalkOOP ActivateBlockContext(SmalltalkOOP blockContextOop, SmalltalkOOP argumentsArray);

    // Object enumeration support

    /// <summary>
    /// Gets all object pointers in the heap (for enumeration primitives).
    /// </summary>
    /// <returns>Enumerable of all SmalltalkOOPs (excluding SmallIntegers).</returns>
    IEnumerable<SmalltalkOOP> GetAllObjects();

    // Object identity operations

    /// <summary>
    /// One-way become: All references to fromOop become references to toOop.
    /// </summary>
    /// <param name="fromOop">Source object pointer.</param>
    /// <param name="toOop">Target object pointer.</param>
    void BecomeOneWay(SmalltalkOOP fromOop, SmalltalkOOP toOop);

    /// <summary>
    /// Two-way become: Swaps identities of two objects.
    /// </summary>
    /// <param name="oop1">First object pointer.</param>
    /// <param name="oop2">Second object pointer.</param>
    void BecomeTwoWay(SmalltalkOOP oop1, SmalltalkOOP oop2);

    /// <summary>
    /// Gets an instance variable from an object.
    /// </summary>
    /// <param name="objectOOP">Object pointer.</param>
    /// <param name="index">Instance variable index.</param>
    /// <returns>Instance variable value as SmalltalkOOP.</returns>
    SmalltalkOOP GetInstanceVariable(SmalltalkOOP objectOOP, int index);

    /// <summary>
    /// Gets the indexed size of an indexed object (e.g., Array).
    /// </summary>
    /// <param name="objectOOP">Object pointer.</param>
    /// <returns>Number of indexed elements.</returns>
    int GetIndexedSize(SmalltalkOOP objectOOP);

    /// <summary>
    /// Gets an indexed OOP from an indexed object (e.g., Array).
    /// </summary>
    /// <param name="objectOOP">Object pointer.</param>
    /// <param name="index">Index.</param>
    /// <returns>Indexed element as SmalltalkOOP.</returns>
    SmalltalkOOP GetIndexedOOP(SmalltalkOOP objectOOP, int index);

    /// <summary>
    /// Gets the object format (ByteIndexed, PointerIndexed, etc.) of an object.
    /// </summary>
    /// <param name="objectOOP">Object pointer.</param>
    /// <returns>ObjectFormat enum value.</returns>
    ObjectFormat GetObjectFormat(SmalltalkOOP objectOOP);

    // Global object lookup support

    /// <summary>
    /// Gets a global object by name from the Smalltalk global dictionary (heap-only).
    /// Used to look up class objects, special globals, etc. without using CLR proxies.
    /// </summary>
    /// <param name="globalName">Global name (e.g., "class:LargePositiveInteger", "global:Transcript").</param>
    /// <returns>Global object OOP if found, Nil if not found.</returns>
    SmalltalkOOP GetGlobalObjectByName(string globalName);

    /// <summary>
    /// Gets the class name for an object (e.g., "class:WriteStream", "class:Point").
    /// </summary>
    /// <param name="objectOOP">Object pointer.</param>
    /// <returns>Class name string, or null if object is invalid.</returns>
    string? GetClassNameForObject(SmalltalkOOP objectOOP);

    /// <summary>
    /// Clears the method lookup cache.
    /// Used by primitive 89 (Behavior flushCache).
    /// Implementations without a method cache can make this a no-op.
    /// </summary>
    void ClearMethodCache();

    /// <summary>
    /// Invalidates all runtime caches (method cache, class cache, field layout cache).
    /// Called by primitive 89 (Behavior flushCache) when class definitions change.
    /// This ensures runtime heap access uses updated class structures.
    /// </summary>
    void InvalidateAllRuntimeCaches();

    /// <summary>
    /// Gets the BlockContext class OOP from VMMetadata.
    /// Used by primitive 80 (blockCopy:) to allocate BlockContext instances.
    /// </summary>
    /// <returns>BlockContext class OOP.</returns>
    SmalltalkOOP GetBlockContextClassOOP();

    /// <summary>
    /// Sets the active context and signals that a primitive has switched contexts.
    /// Used by block activation primitives (81, 82) to trampoline to BlockContext execution.
    /// </summary>
    /// <param name="newContextOOP">The new context to switch to.</param>
    void SetActiveContext(SmalltalkOOP newContextOOP);
}
