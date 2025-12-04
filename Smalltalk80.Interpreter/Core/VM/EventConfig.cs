namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Configuration for event architecture behavior.
/// Controls placement and behavior of strategic check points for interrupt simulation.
/// </summary>
public static class EventConfig
{
    // ============================================================================
    // Check Point Placement
    // ============================================================================

    /// <summary>
    /// Periodic check interval (bytecode count between checks).
    /// Lower values = more responsive but higher overhead.
    /// </summary>
    public const int PeriodicCheckInterval = 1000;

    /// <summary>
    /// Enable event checks at backward branches (loops).
    /// Ensures tight loops remain responsive to input interrupts.
    /// </summary>
    public static bool EnableBackwardBranchCheck = true;

    /// <summary>
    /// Enable event checks at method returns.
    /// Reduces latency for deeply nested call chains.
    /// </summary>
    public static bool EnableMethodReturnCheck = true;

    /// <summary>
    /// Enable event checks after primitive returns.
    /// Useful for long-running primitives, but adds overhead.
    /// </summary>
    public static bool EnablePrimitiveReturnCheck = false;

    // ============================================================================
    // Event Coalescing (Phase 8)
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
    // Diagnostics (Phase 6)
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
    /// Profiles allow quick switching between conservative, balanced, and aggressive settings.
    /// </summary>
    /// <param name="profile">Profile name: "conservative", "responsive", or "stress"</param>
    public static void ApplyProfile(string profile)
    {
        switch (profile.ToLower())
        {
            case "conservative":
                // Minimal overhead - only periodic checks
                EnableBackwardBranchCheck = false;
                EnableMethodReturnCheck = false;
                EnablePrimitiveReturnCheck = false;
                MoveCoalescingEnabled = false;
                EnableEventTracing = false;
                EnableLatencyProbes = false;
                break;

            case "responsive":
                // Balanced - backward branches and method returns
                EnableBackwardBranchCheck = true;
                EnableMethodReturnCheck = true;
                EnablePrimitiveReturnCheck = false;
                MoveCoalescingEnabled = true;
                EnableEventTracing = false;
                EnableLatencyProbes = false;
                break;

            case "stress":
                // Maximum responsiveness - all check points enabled
                EnableBackwardBranchCheck = true;
                EnableMethodReturnCheck = true;
                EnablePrimitiveReturnCheck = true;
                MoveCoalescingEnabled = true;
                EnableEventTracing = false;
                EnableLatencyProbes = false;
                break;

            case "debug":
                // Debugging - responsive with full diagnostics
                EnableBackwardBranchCheck = true;
                EnableMethodReturnCheck = true;
                EnablePrimitiveReturnCheck = false;
                MoveCoalescingEnabled = false;
                EnableEventTracing = true;
                EnableLatencyProbes = true;
                break;

            default:
                throw new ArgumentException($"Unknown profile: {profile}. Valid profiles: conservative, responsive, stress, debug");
        }
    }
}
