using System.Runtime.CompilerServices;

namespace Smalltalk80.Interpreter.Core.Diagnostics;

/// <summary>
/// Central hub for diagnostic event dispatch.
/// Thread-safe implementation with fast-path checking.
/// </summary>
public class DiagnosticHub : IDiagnosticHub
{
    private readonly List<(IDiagnosticListener listener, DiagnosticEventTypes events)> _listeners = new();
    private readonly object _lock = new();
    private DiagnosticEventTypes _enabledEvents = DiagnosticEventTypes.None;

    /// <summary>
    /// Registers a listener for specific event types.
    /// </summary>
    public void Register(IDiagnosticListener listener, DiagnosticEventTypes events)
    {
        lock (_lock)
        {
            _listeners.Add((listener, events));
            _enabledEvents |= events;
        }
    }

    /// <summary>
    /// Unregisters a listener.
    /// </summary>
    public void Unregister(IDiagnosticListener listener)
    {
        lock (_lock)
        {
            _listeners.RemoveAll(x => x.listener == listener);

            // Recalculate enabled events
            _enabledEvents = DiagnosticEventTypes.None;
            foreach (var (_, events) in _listeners)
            {
                _enabledEvents |= events;
            }
        }
    }

    /// <summary>
    /// Fast-path check if any listener wants this event type.
    /// Inlined for performance in hot paths.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEnabled(DiagnosticEventTypes eventType)
    {
        return (_enabledEvents & eventType) != 0;
    }

    /// <summary>
    /// Dispatches an event to all interested listeners.
    /// </summary>
    public void Emit(DiagnosticEvent evt)
    {
        var eventType = GetEventType(evt);

        // Fast path: no listeners want this event
        if ((_enabledEvents & eventType) == 0)
            return;

        // Copy listeners to avoid lock during dispatch
        List<(IDiagnosticListener listener, DiagnosticEventTypes events)> listenersSnapshot;
        lock (_lock)
        {
            listenersSnapshot = new List<(IDiagnosticListener, DiagnosticEventTypes)>(_listeners);
        }

        foreach (var (listener, events) in listenersSnapshot)
        {
            if ((events & eventType) != 0)
            {
                // Apply filter if present
                if (listener.Filter == null || listener.Filter.ShouldDeliver(evt))
                {
                    try
                    {
                        listener.OnEvent(evt);
                    }
                    catch (Exception ex)
                    {
                        // Don't let listener errors crash the system
                        Console.Error.WriteLine($"[DiagnosticHub] Listener '{listener.Name}' threw: {ex.Message}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Maps an event instance to its DiagnosticEventTypes flag.
    /// </summary>
    private static DiagnosticEventTypes GetEventType(DiagnosticEvent evt)
    {
        return evt switch
        {
            // Cold start events
            PhaseStartEvent => DiagnosticEventTypes.PhaseStart,
            PhaseEndEvent => DiagnosticEventTypes.PhaseEnd,
            SymbolInternedEvent => DiagnosticEventTypes.SymbolInterned,
            ClassCreatedEvent => DiagnosticEventTypes.ClassCreated,
            MethodCompiledEvent => DiagnosticEventTypes.MethodCompiled,
            PatchAppliedEvent => DiagnosticEventTypes.PatchApplied,
            GlobalInstalledEvent => DiagnosticEventTypes.GlobalInstalled,
            AssociationCreatedEvent => DiagnosticEventTypes.AssociationCreated,
            DiagnosticMessageEvent => DiagnosticEventTypes.Message,

            // Runtime events
            MethodEntryEvent => DiagnosticEventTypes.MethodEntry,
            MethodExitEvent => DiagnosticEventTypes.MethodExit,
            InstructionEvent => DiagnosticEventTypes.InstructionExecute,
            PrimitiveEvent e => e.Success ? DiagnosticEventTypes.PrimitiveCall : DiagnosticEventTypes.PrimitiveFailure,
            AllocationEvent => DiagnosticEventTypes.ObjectAllocate,
            GCEvent => DiagnosticEventTypes.GarbageCollect,

            // Granular GC events
            GCStartEvent => DiagnosticEventTypes.GCStart,
            GCMarkPhaseStartEvent => DiagnosticEventTypes.GCMarkPhaseStart,
            GCMarkPhaseEndEvent => DiagnosticEventTypes.GCMarkPhaseEnd,
            GCSweepPhaseStartEvent => DiagnosticEventTypes.GCSweepPhaseStart,
            GCSweepPhaseEndEvent => DiagnosticEventTypes.GCSweepPhaseEnd,
            GCEndEvent => DiagnosticEventTypes.GCEnd,
            GCObjectMarkedEvent => DiagnosticEventTypes.GCObjectMarked,
            GCObjectCollectedEvent => DiagnosticEventTypes.GCObjectCollected,

            _ => DiagnosticEventTypes.None
        };
    }
}
