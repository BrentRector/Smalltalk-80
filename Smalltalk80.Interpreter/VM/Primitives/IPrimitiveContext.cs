namespace Smalltalk80.Interpreter.VM.Primitives;

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
    /// Checks if an object ID is a SmallInteger.
    /// </summary>
    bool IsSmallInteger(int objectId);

    /// <summary>
    /// Encodes an integer as a SmallInteger object ID.
    /// </summary>
    int EncodeSmallInteger(int value);

    /// <summary>
    /// Decodes a SmallInteger object ID to an integer.
    /// </summary>
    int DecodeSmallInteger(int objectId);
}
