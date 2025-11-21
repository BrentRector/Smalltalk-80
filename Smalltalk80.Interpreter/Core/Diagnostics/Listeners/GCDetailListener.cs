using Smalltalk80.Interpreter.Shared.Types.Diagnostics;

namespace Smalltalk80.Interpreter.Core.Diagnostics.Listeners;

/// <summary>
/// Outputs detailed GC diagnostics with per-object collection information.
/// Aggregates collected objects by class and displays comprehensive statistics.
/// </summary>
public class GCDetailListener : IDiagnosticListener
{
    public string Name => "GCDetail";
    public IDiagnosticFilter? Filter => null;

    private Dictionary<string, int> _collectedByClass = new();
    private int _totalCollected = 0;

    public void OnEvent(DiagnosticEvent evt)
    {
        switch (evt)
        {
            case GCStartEvent e:
                _collectedByClass.Clear();
                _totalCollected = 0;
                break;

            case GCObjectCollectedEvent e:
                var className = e.ClassName ?? "unknown";
                _collectedByClass[className] = _collectedByClass.GetValueOrDefault(className) + 1;
                _totalCollected++;
                break;

            case GCEndEvent e:
                // Only output if we have collected object details
                if (_collectedByClass.Count > 0)
                {
                    Console.WriteLine($"    GC collected {e.ObjectsCollected} objects:");

                    // Show all collected classes sorted by count
                    var sortedClasses = _collectedByClass
                        .OrderByDescending(kvp => kvp.Value)
                        .ToList();

                    foreach (var (className, count) in sortedClasses)
                    {
                        Console.WriteLine($"      {className}: {count}");
                    }

                    // Verify accounting
                    if (_totalCollected != e.ObjectsCollected)
                    {
                        Console.WriteLine($"      WARNING: Accounted for {_totalCollected}/{e.ObjectsCollected} objects");
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Resets internal state for a fresh GC cycle.
    /// </summary>
    public void Reset()
    {
        _collectedByClass.Clear();
        _totalCollected = 0;
    }
}
