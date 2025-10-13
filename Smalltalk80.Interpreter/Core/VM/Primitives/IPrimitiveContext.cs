using Smalltalk80.Interpreter.Core.Values;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Context provided to primitive operations.
/// Provides access to the heap and allows primitives to interact with the VM.
/// </summary>
public interface IPrimitiveContext
{
    /// <summary>
    /// Gets the heap instance.
    /// </summary>
    SmalltalkHeap Heap { get; }

    /// <summary>
    /// Gets the layout resolver for field lookups.
    /// </summary>
    LayoutResolver LayoutResolver { get; }

    /// <summary>
    /// Gets or sets the primitive success flag.
    /// Primitives set this to false to signal failure, causing the VM to execute fallback bytecodes.
    /// The VM sets this to true before calling each primitive.
    /// </summary>
    bool PrimitiveSuccess { get; set; }

    // Graphics/Input support methods

    /// <summary>
    /// Gets a field value from an object using the layout resolver.
    /// </summary>
    /// <param name="oop">Object pointer.</param>
    /// <param name="className">Class name (e.g., "class:Point").</param>
    /// <param name="fieldName">Field name (e.g., "x").</param>
    /// <returns>Field value as object pointer or SmallInteger value.</returns>
    int GetFieldValue(int oop, string className, string fieldName);

    /// <summary>
    /// Sets a field value in an object using the layout resolver.
    /// </summary>
    /// <param name="oop">Object pointer.</param>
    /// <param name="className">Class name (e.g., "class:Point").</param>
    /// <param name="fieldName">Field name (e.g., "x").</param>
    /// <param name="value">Field value as object pointer or SmallInteger value.</param>
    void SetFieldValue(int oop, string className, string fieldName, int value);

    /// <summary>
    /// Gets byte array data from an indexed object (e.g., ByteArray, String, Form bits).
    /// </summary>
    /// <param name="oop">Object pointer to indexed byte object.</param>
    /// <returns>Memory span of bytes, or empty if not a byte array.</returns>
    Memory<byte> GetByteArray(int oop);

    /// <summary>
    /// Allocates a new object instance with optional indexed fields.
    /// </summary>
    /// <param name="className">Class name (e.g., "class:Point", "class:ByteArray").</param>
    /// <param name="indexedSize">Number of indexed elements (0 for fixed-size objects).</param>
    /// <returns>Object pointer of newly allocated instance.</returns>
    int AllocateObject(string className, int indexedSize);

    // Dynamic message sending support

    /// <summary>
    /// Sends a message to a receiver object (for perform primitives).
    /// </summary>
    /// <param name="receiverOop">Receiver object pointer.</param>
    /// <param name="selectorOop">Selector Symbol object pointer.</param>
    /// <param name="argumentsArray">Arguments array object pointer (contains indexed arguments).</param>
    /// <returns>Result of message send.</returns>
    int SendMessage(int receiverOop, int selectorOop, int argumentsArray);

    // BlockContext support

    /// <summary>
    /// Gets the currently active context (MethodContext or BlockContext OOP).
    /// </summary>
    /// <returns>Object pointer of the active context.</returns>
    int GetActiveContext();

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
    /// <returns>Object pointer of newly created BlockContext.</returns>
    int CreateBlockContext(int homeContextOop, int startPC, int argCount);

    /// <summary>
    /// Activates a BlockContext with given arguments (for value/value: primitives).
    /// Switches execution to the block and returns the result.
    /// </summary>
    /// <param name="blockContextOop">BlockContext to activate.</param>
    /// <param name="argumentsArray">Arguments array object pointer.</param>
    /// <returns>Result of block execution.</returns>
    int ActivateBlockContext(int blockContextOop, int argumentsArray);

    // Object enumeration support

    /// <summary>
    /// Gets all object pointers in the heap (for enumeration primitives).
    /// </summary>
    /// <returns>Enumerable of all object pointers (excluding SmallIntegers).</returns>
    IEnumerable<int> GetAllObjects();

    // Object identity operations

    /// <summary>
    /// One-way become: All references to fromOop become references to toOop.
    /// </summary>
    /// <param name="fromOop">Source object pointer.</param>
    /// <param name="toOop">Target object pointer.</param>
    void BecomeOneWay(int fromOop, int toOop);

    /// <summary>
    /// Two-way become: Swaps identities of two objects.
    /// </summary>
    /// <param name="oop1">First object pointer.</param>
    /// <param name="oop2">Second object pointer.</param>
    void BecomeTwoWay(int oop1, int oop2);
}
