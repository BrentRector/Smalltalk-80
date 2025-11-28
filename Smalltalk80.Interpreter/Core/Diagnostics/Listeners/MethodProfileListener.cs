using System.Diagnostics;
using Smalltalk80.Interpreter.Shared.Types.Diagnostics;

namespace Smalltalk80.Interpreter.Core.Diagnostics.Listeners;

/// <summary>
/// Profiles method execution time and reports cumulative statistics.
/// Tracks both total time (including callees) and self time (excluding callees).
/// </summary>
public class MethodProfileListener : IDiagnosticListener
{
    public string Name => "MethodProfile";
    public IDiagnosticFilter? Filter => null;

    // Call stack to track entry times for matching with exits
    private readonly Stack<MethodCallEntry> _callStack = new();

    // Accumulated statistics per method
    private readonly Dictionary<string, MethodStats> _methodStats = new();

    // Stopwatch for high-precision timing
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    // Track total profiled time
    private long _profilingStartTicks;
    private long _totalMethodCalls;

    // Minimum threshold for reporting (in milliseconds)
    public double MinReportThresholdMs { get; set; } = 0.1;

    // Maximum methods to report (top N by total time)
    public int MaxMethodsToReport { get; set; } = 50;

    private record struct MethodCallEntry(
        string MethodKey,
        long EntryTicks,
        long ChildTicks);

    private class MethodStats
    {
        public long TotalTicks;      // Total time including callees
        public long SelfTicks;       // Self time excluding callees
        public long CallCount;
        public long MaxTicks;        // Maximum single call time
    }

    public MethodProfileListener()
    {
        _profilingStartTicks = _stopwatch.ElapsedTicks;
    }

    public void OnEvent(DiagnosticEvent evt)
    {
        switch (evt)
        {
            case MethodEntryEvent entry:
                HandleMethodEntry(entry);
                break;

            case MethodExitEvent exit:
                HandleMethodExit(exit);
                break;
        }
    }

    private void HandleMethodEntry(MethodEntryEvent entry)
    {
        string methodKey = $"{entry.ReceiverClass}>>{entry.Selector}";
        _callStack.Push(new MethodCallEntry(methodKey, _stopwatch.ElapsedTicks, 0));
        _totalMethodCalls++;
    }

    private void HandleMethodExit(MethodExitEvent exit)
    {
        if (_callStack.Count == 0)
        {
            // Mismatched exit - can happen if profiling started mid-execution
            return;
        }

        string exitMethodKey = $"{exit.ReceiverClass}>>{exit.Selector}";
        var entry = _callStack.Pop();

        // Verify method keys match (they should in normal execution)
        // Note: May not match perfectly due to NLR or other edge cases
        string methodKey = entry.MethodKey;

        long exitTicks = _stopwatch.ElapsedTicks;
        long totalTicks = exitTicks - entry.EntryTicks;
        long selfTicks = totalTicks - entry.ChildTicks;

        // Update statistics
        if (!_methodStats.TryGetValue(methodKey, out var stats))
        {
            stats = new MethodStats();
            _methodStats[methodKey] = stats;
        }

        stats.TotalTicks += totalTicks;
        stats.SelfTicks += selfTicks;
        stats.CallCount++;
        if (totalTicks > stats.MaxTicks)
        {
            stats.MaxTicks = totalTicks;
        }

        // Add this method's total time to parent's child ticks
        if (_callStack.Count > 0)
        {
            var parent = _callStack.Pop();
            _callStack.Push(parent with { ChildTicks = parent.ChildTicks + totalTicks });
        }
    }

    /// <summary>
    /// Reports profiling statistics to the console.
    /// Call this when profiling is complete.
    /// </summary>
    public void ReportStatistics()
    {
        long totalProfilingTicks = _stopwatch.ElapsedTicks - _profilingStartTicks;
        double totalProfilingMs = TicksToMs(totalProfilingTicks);

        Console.WriteLine();
        Console.WriteLine("=".PadRight(100, '='));
        Console.WriteLine("METHOD PROFILING RESULTS");
        Console.WriteLine("=".PadRight(100, '='));
        Console.WriteLine($"Total profiling time: {totalProfilingMs:F2} ms");
        Console.WriteLine($"Total method calls: {_totalMethodCalls:N0}");
        Console.WriteLine($"Unique methods: {_methodStats.Count:N0}");
        Console.WriteLine();

        // Sort by self time (most impactful methods first)
        var sortedByTotal = _methodStats
            .OrderByDescending(kv => kv.Value.TotalTicks)
            .Take(MaxMethodsToReport)
            .ToList();

        Console.WriteLine("TOP METHODS BY TOTAL TIME (including callees):");
        Console.WriteLine("-".PadRight(100, '-'));
        Console.WriteLine($"{"Method",-60} {"Total ms",10} {"Self ms",10} {"Calls",10} {"Avg ms",10}");
        Console.WriteLine("-".PadRight(100, '-'));

        foreach (var (methodKey, stats) in sortedByTotal)
        {
            double totalMs = TicksToMs(stats.TotalTicks);
            double selfMs = TicksToMs(stats.SelfTicks);
            double avgMs = stats.CallCount > 0 ? totalMs / stats.CallCount : 0;

            if (totalMs < MinReportThresholdMs) continue;

            string displayName = methodKey.Length > 58 ? methodKey[..55] + "..." : methodKey;
            Console.WriteLine($"{displayName,-60} {totalMs,10:F2} {selfMs,10:F2} {stats.CallCount,10:N0} {avgMs,10:F4}");
        }

        Console.WriteLine();

        // Also show top methods by self time
        var sortedBySelf = _methodStats
            .OrderByDescending(kv => kv.Value.SelfTicks)
            .Take(MaxMethodsToReport)
            .ToList();

        Console.WriteLine("TOP METHODS BY SELF TIME (excluding callees):");
        Console.WriteLine("-".PadRight(100, '-'));
        Console.WriteLine($"{"Method",-60} {"Self ms",10} {"Total ms",10} {"Calls",10} {"% Self",10}");
        Console.WriteLine("-".PadRight(100, '-'));

        foreach (var (methodKey, stats) in sortedBySelf)
        {
            double totalMs = TicksToMs(stats.TotalTicks);
            double selfMs = TicksToMs(stats.SelfTicks);
            double selfPercent = stats.TotalTicks > 0 ? 100.0 * stats.SelfTicks / stats.TotalTicks : 0;

            if (selfMs < MinReportThresholdMs) continue;

            string displayName = methodKey.Length > 58 ? methodKey[..55] + "..." : methodKey;
            Console.WriteLine($"{displayName,-60} {selfMs,10:F2} {totalMs,10:F2} {stats.CallCount,10:N0} {selfPercent,9:F1}%");
        }

        Console.WriteLine("=".PadRight(100, '='));
        Console.WriteLine();
    }

    /// <summary>
    /// Resets all profiling statistics.
    /// </summary>
    public void Reset()
    {
        _callStack.Clear();
        _methodStats.Clear();
        _profilingStartTicks = _stopwatch.ElapsedTicks;
        _totalMethodCalls = 0;
    }

    private static double TicksToMs(long ticks)
    {
        return ticks * 1000.0 / Stopwatch.Frequency;
    }
}
