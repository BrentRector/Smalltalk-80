using System.Diagnostics;
using System.Text;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Diagnostic tracking for file primitive operations.
/// Tracks call counts and execution time to identify performance bottlenecks.
/// </summary>
public static class FilePrimitiveDiagnostics
{
    private static readonly object _lock = new();
    private static readonly Dictionary<string, PrimitiveStats> _stats = [];
    private static bool _enabled = true;

    /// <summary>
    /// Statistics for a single primitive operation.
    /// </summary>
    private class PrimitiveStats
    {
        public long CallCount { get; set; }
        public long TotalTicks { get; set; }
        public long MinTicks { get; set; } = long.MaxValue;
        public long MaxTicks { get; set; }

        public double AverageMs => CallCount > 0
            ? (TotalTicks / (double)CallCount) / TimeSpan.TicksPerMillisecond
            : 0;

        public double TotalMs => TotalTicks / (double)TimeSpan.TicksPerMillisecond;
    }

    /// <summary>
    /// Enables or disables diagnostic tracking.
    /// </summary>
    public static bool Enabled
    {
        get { lock (_lock) { return _enabled; } }
        set { lock (_lock) { _enabled = value; } }
    }

    /// <summary>
    /// Records execution of a primitive operation.
    /// </summary>
    /// <param name="primitiveName">Name of the primitive (e.g., "FileOpen", "FileRead").</param>
    /// <param name="elapsedTicks">Elapsed ticks from Stopwatch.</param>
    public static void Record(string primitiveName, long elapsedTicks)
    {
        if (!_enabled) return;

        lock (_lock)
        {
            if (!_stats.TryGetValue(primitiveName, out var stats))
            {
                stats = new PrimitiveStats();
                _stats[primitiveName] = stats;
            }

            stats.CallCount++;
            stats.TotalTicks += elapsedTicks;
            if (elapsedTicks < stats.MinTicks) stats.MinTicks = elapsedTicks;
            if (elapsedTicks > stats.MaxTicks) stats.MaxTicks = elapsedTicks;
        }
    }

    /// <summary>
    /// Clears all recorded statistics.
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _stats.Clear();
        }
    }

    /// <summary>
    /// Gets a formatted report of all file primitive statistics.
    /// </summary>
    public static string GetReport()
    {
        lock (_lock)
        {
            if (_stats.Count == 0)
                return "No file primitive diagnostics recorded.";

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("╔════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║           FILE PRIMITIVE PERFORMANCE DIAGNOSTICS                   ║");
            sb.AppendLine("╚════════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();
            sb.AppendLine($"{"Primitive",-20} {"Calls",10} {"Total ms",12} {"Avg ms",12} {"Min ms",12} {"Max ms",12}");
            sb.AppendLine(new string('─', 80));

            var orderedStats = _stats.OrderByDescending(kvp => kvp.Value.TotalMs);

            foreach (var kvp in orderedStats)
            {
                var name = kvp.Key;
                var stats = kvp.Value;
                var minMs = stats.MinTicks / (double)TimeSpan.TicksPerMillisecond;
                var maxMs = stats.MaxTicks / (double)TimeSpan.TicksPerMillisecond;

                sb.AppendLine($"{name,-20} {stats.CallCount,10} {stats.TotalMs,12:F3} {stats.AverageMs,12:F3} {minMs,12:F3} {maxMs,12:F3}");
            }

            sb.AppendLine(new string('─', 80));

            var totalMs = _stats.Values.Sum(s => s.TotalMs);
            var totalCalls = _stats.Values.Sum(s => s.CallCount);

            sb.AppendLine($"{"TOTAL",-20} {totalCalls,10} {totalMs,12:F3}");
            sb.AppendLine();

            return sb.ToString();
        }
    }

    /// <summary>
    /// Prints diagnostic report to console.
    /// </summary>
    public static void PrintReport()
    {
        Console.WriteLine(GetReport());
    }
}
