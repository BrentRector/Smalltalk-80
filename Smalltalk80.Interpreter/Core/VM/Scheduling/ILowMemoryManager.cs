using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Scheduling;

/// <summary>
/// Interface for managing low memory threshold semaphore signals.
/// Used by primitive 116 (signal:atOopsLeft:wordsLeft:) to implement low memory warnings.
///
/// Blue Book: The Smalltalk VM monitors available memory and signals a semaphore
/// when thresholds are crossed. This allows the system to respond to low memory
/// conditions, typically by triggering garbage collection or warning the user.
/// </summary>
public interface ILowMemoryManager
{
    /// <summary>
    /// Sets the low memory thresholds and semaphore to signal.
    /// </summary>
    /// <param name="semaphoreOOP">The semaphore to signal when thresholds are crossed, or nil to disable.</param>
    /// <param name="oopsThreshold">Minimum number of available object pointers before signaling.</param>
    /// <param name="wordsThreshold">Minimum number of available words (memory) before signaling.</param>
    void SetThresholds(SmalltalkOOP semaphoreOOP, int oopsThreshold, int wordsThreshold);

    /// <summary>
    /// Checks if memory thresholds have been crossed and returns the semaphore to signal.
    /// Returns default if thresholds have not been crossed or no semaphore is registered.
    /// </summary>
    /// <param name="currentFreeOops">Current number of free object table slots.</param>
    /// <param name="currentFreeWords">Current amount of free memory in words.</param>
    /// <returns>The semaphore OOP to signal, or default if none.</returns>
    SmalltalkOOP CheckThresholds(int currentFreeOops, int currentFreeWords);

    /// <summary>
    /// Gets whether low memory monitoring is enabled (semaphore is not nil).
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Clears the registered semaphore and disables monitoring.
    /// </summary>
    void Disable();
}
