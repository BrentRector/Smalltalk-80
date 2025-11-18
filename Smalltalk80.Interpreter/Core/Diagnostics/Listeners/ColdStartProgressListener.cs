namespace Smalltalk80.Interpreter.Core.Diagnostics.Listeners;

/// <summary>
/// Outputs cold start phase progress to console.
/// Reproduces the current console output format.
/// </summary>
public class ColdStartProgressListener : IDiagnosticListener
{
    public string Name => "ColdStartProgress";
    public IDiagnosticFilter? Filter => null;

    public void OnEvent(DiagnosticEvent evt)
    {
        switch (evt)
        {
            case PhaseStartEvent e:
                Console.Write($"{e.PhaseName}...");
                break;

            case PhaseEndEvent e:
                if (e.Success)
                {
                    Console.WriteLine($" \u2713 ({e.Duration.TotalMilliseconds:F0}ms)");
                }
                else
                {
                    Console.WriteLine($" \u2717 FAILED: {e.ErrorMessage}");
                }
                break;

            case DiagnosticMessageEvent e:
                var prefix = e.Severity switch
                {
                    MessageSeverity.Warning => "  \u26a0\ufe0f  ",
                    MessageSeverity.Error => "  \u274c  ",
                    MessageSeverity.Debug => "  [DEBUG] ",
                    _ => "  "
                };
                Console.WriteLine($"{prefix}{e.Message}");
                break;

            case GCEvent e:
                var efficiency = e.ObjectsBefore > 0
                    ? (100.0 * e.ObjectsCollected / e.ObjectsBefore)
                    : 0;
                Console.WriteLine($"  GC Results: {e.ObjectsCollected}/{e.ObjectsBefore} objects collected " +
                    $"({efficiency:F1}% efficiency) in {e.MarkTimeMs + e.SweepTimeMs:F2}ms " +
                    $"(Mark: {e.MarkTimeMs:F2}ms, Sweep: {e.SweepTimeMs:F2}ms), " +
                    $"{e.BytesFreed:N0} bytes freed");
                break;
        }
    }
}
