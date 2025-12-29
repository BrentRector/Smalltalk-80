using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Scheduling;

/// <summary>
/// Manages low memory threshold monitoring and semaphore signaling.
///
/// Blue Book: When available memory (oops or words) drops below configured thresholds,
/// the VM signals a registered semaphore to allow the system to respond to low memory
/// conditions. Typically used to trigger garbage collection or warn the user.
///
/// Hysteresis: To prevent repeated signaling when memory is persistently low,
/// we require memory to recover above threshold before signaling again.
/// This means:
/// 1. Signal when memory drops below threshold
/// 2. Don't signal again until memory goes ABOVE threshold first
/// 3. Then signal again when it drops below threshold
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
            // DON'T reset _hasSignaled here - we use hysteresis to prevent
            // repeated signaling when memory is persistently low.
            // Only reset when memory recovers above threshold.
        }
    }

    /// <inheritdoc/>
    public SmalltalkOOP CheckThresholds(int currentFreeOops, int currentFreeWords)
    {
        lock (_lock)
        {
            // Not enabled
            if (_semaphore == default)
                return default;

            // Check if either threshold has been crossed
            bool oopsBelowThreshold = currentFreeOops < _oopsThreshold;
            bool wordsBelowThreshold = currentFreeWords < _wordsThreshold;
            bool belowThreshold = oopsBelowThreshold || wordsBelowThreshold;

            if (_hasSignaled)
            {
                // Already signaled - check if memory has recovered above threshold
                if (!belowThreshold)
                {
                    // Memory recovered - reset so we can signal again next time it drops
                    _hasSignaled = false;
                }
                // Either way, don't signal now
                return default;
            }

            // Not yet signaled - check if we should signal now
            if (belowThreshold)
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
