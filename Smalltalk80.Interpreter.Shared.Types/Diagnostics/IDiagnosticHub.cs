namespace Smalltalk80.Interpreter.Shared.Types.Diagnostics;

/// <summary>
/// Central hub for diagnostic event dispatch.
/// Created before any other component and shared throughout the system.
/// </summary>
public interface IDiagnosticHub
{
    /// <summary>Registers a listener for specific event types.</summary>
    void Register(IDiagnosticListener listener, DiagnosticEventTypes events);

    /// <summary>Unregisters a listener.</summary>
    void Unregister(IDiagnosticListener listener);

    /// <summary>Checks if any listener wants this event type (fast path).</summary>
    bool IsEnabled(DiagnosticEventTypes eventType);

    /// <summary>Dispatches an event to all interested listeners.</summary>
    void Emit(DiagnosticEvent evt);
}

/// <summary>
/// Consumer of diagnostic events.
/// </summary>
public interface IDiagnosticListener
{
    /// <summary>Unique identifier for this listener.</summary>
    string Name { get; }

    /// <summary>Handles an incoming diagnostic event.</summary>
    void OnEvent(DiagnosticEvent evt);

    /// <summary>Optional filter to reduce events at source.</summary>
    IDiagnosticFilter? Filter { get; }
}

/// <summary>
/// Filters events before delivery to listener.
/// </summary>
public interface IDiagnosticFilter
{
    /// <summary>Returns true if this event should be delivered.</summary>
    bool ShouldDeliver(DiagnosticEvent evt);
}
