namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Configuration for event architecture behavior.
/// The interpreter uses cooperative multi-processing. Events are processed
/// only at explicit yield points (semaphore wait, process suspend, yield, terminate).
/// There is no proactive interrupt simulation.
/// </summary>
public static class EventConfig
{
    // ============================================================================
    // Event Coalescing
    // ============================================================================

    /// <summary>
    /// Enable mouse move coalescing.
    /// Combines consecutive move events to reduce queue pressure.
    /// </summary>
    public static bool MoveCoalescingEnabled = false;

    /// <summary>
    /// Maximum number of consecutive moves to coalesce before forcing a discrete event.
    /// Prevents losing all intermediate positions during continuous movement.
    /// </summary>
    public const int MaxCoalescedMoves = 10;

    // ============================================================================
    // Diagnostics
    // ============================================================================

    /// <summary>
    /// Enable event flow tracing (Console.WriteLine output).
    /// </summary>
    public static bool EnableEventTracing = false;

    /// <summary>
    /// Enable latency probes (timestamp tracking for performance analysis).
    /// </summary>
    public static bool EnableLatencyProbes = false;

    // ============================================================================
    // Configuration Profiles
    // ============================================================================

    /// <summary>
    /// Apply a named configuration profile.
    /// </summary>
    /// <param name="profile">Profile name: "conservative", "responsive", "stress", or "debug"</param>
    public static void ApplyProfile(string profile)
    {
        switch (profile.ToLower())
        {
            case "conservative":
                // Minimal features
                MoveCoalescingEnabled = false;
                EnableEventTracing = false;
                EnableLatencyProbes = false;
                break;

            case "responsive":
                // Enable coalescing for better performance
                MoveCoalescingEnabled = true;
                EnableEventTracing = false;
                EnableLatencyProbes = false;
                break;

            case "stress":
                // Same as responsive (cooperative model)
                MoveCoalescingEnabled = true;
                EnableEventTracing = false;
                EnableLatencyProbes = false;
                break;

            case "debug":
                // Enable diagnostics
                MoveCoalescingEnabled = false;
                EnableEventTracing = true;
                EnableLatencyProbes = true;
                break;

            default:
                throw new ArgumentException($"Unknown profile: {profile}. Valid profiles: conservative, responsive, stress, debug");
        }
    }
}
