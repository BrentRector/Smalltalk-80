using Smalltalk80.Interpreter.Core.Values;

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
    /// Gets or sets the primitive success flag.
    /// Primitives set this to false to signal failure, causing the VM to execute fallback bytecodes.
    /// The VM sets this to true before calling each primitive.
    /// </summary>
    bool PrimitiveSuccess { get; set; }
}
