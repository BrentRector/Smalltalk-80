using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Scheduling;

/// <summary>
/// Manages low memory threshold monitoring and semaphore signaling.
///
/// Blue Book: When available memory (oops or words) drops below configured thresholds,
/// the VM signals a registered semaphore to allow the system to respond to low memory
/// conditions. Typically used to trigger garbage collection or warn the user.
///
/// Thread Safety: All state access is protected by a lock since thresholds can be
/// set from primitives while the VM main loop checks them.
/// </summary>
public class LowMemoryManager : ILowMemoryManager
{
    private SmalltalkOOP _semaphore = default;
    private int _oopsThreshold = 0;
    private int _wordsThreshold = 0;
    private bool _hasSignaled = false;
    private readonly object _lock = new();

    /// <inheritdoc/>
    public bool IsEnabled
    {
        get
        {
            lock (_lock)
            {
                return _semaphore != default;
            }
        }
    }

    /// <inheritdoc/>
    public void SetThresholds(SmalltalkOOP semaphoreOOP, int oopsThreshold, int wordsThreshold)
    {
        lock (_lock)
        {
            _semaphore = semaphoreOOP;
            _oopsThreshold = oopsThreshold;
            _wordsThreshold = wordsThreshold;
            _hasSignaled = false;  // Reset signaled flag when thresholds change
        }
    }

    /// <inheritdoc/>
    public SmalltalkOOP CheckThresholds(int currentFreeOops, int currentFreeWords)
    {
        lock (_lock)
        {
            // Not enabled or already signaled
            if (_semaphore == default || _hasSignaled)
                return default;

            // Check if either threshold has been crossed
            bool oopsBelowThreshold = currentFreeOops < _oopsThreshold;
            bool wordsBelowThreshold = currentFreeWords < _wordsThreshold;

            if (oopsBelowThreshold || wordsBelowThreshold)
            {
                // Mark as signaled to prevent continuous signaling
                _hasSignaled = true;
                return _semaphore;
            }

            return default;
        }
    }

    /// <inheritdoc/>
    public void Disable()
    {
        lock (_lock)
        {
            _semaphore = default;
            _oopsThreshold = 0;
            _wordsThreshold = 0;
            _hasSignaled = false;
        }
    }

    /// <summary>
    /// Resets the signaled state, allowing the semaphore to be signaled again.
    /// Called after memory has been recovered (e.g., after GC).
    /// </summary>
    public void ResetSignaledState()
    {
        lock (_lock)
        {
            _hasSignaled = false;
        }
    }
}
