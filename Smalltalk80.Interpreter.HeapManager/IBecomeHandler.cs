using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.HeapManager;

/// <summary>
/// Interface for handling become operations (identity swapping).
/// Per the Blue Book specification, become: swaps the receiver and argument
/// so that all pointers to the receiver now point to the argument and vice versa.
/// </summary>
public interface IBecomeHandler
{
    /// <summary>
    /// Implements the become: primitive (primitive 72) which swaps the identities of two objects.
    /// This is a bidirectional swap - all references to oop1 now point to oop2 and vice versa.
    /// </summary>
    /// <param name="oop1">First object to swap</param>
    /// <param name="oop2">Second object to swap</param>
    void BecomeTwoWay(SmalltalkOOP oop1, SmalltalkOOP oop2);
}
