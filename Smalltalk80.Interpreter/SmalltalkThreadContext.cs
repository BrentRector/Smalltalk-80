using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.Engine;
using Smalltalk80.Interpreter.Engine.LoaderPhases;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Platform;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Context object containing all data needed by the Smalltalk interpreter thread.
/// This encapsulates the shared state between the Windows UI thread and Smalltalk thread.
/// </summary>
public class SmalltalkThreadContext
{
    /// <summary>
    /// The Smalltalk heap manager containing all runtime objects.
    /// </summary>
    public required ISmalltalkHeap HeapManager { get; init; }

    /// <summary>
    /// Layout resolver for looking up class structures.
    /// </summary>
    public required LayoutResolver LayoutResolver { get; init; }

    /// <summary>
    /// VM metadata including special selectors and class OOPs.
    /// </summary>
    public required VMMetadata VMMetadata { get; init; }

    /// <summary>
    /// The cold start bundle containing init expressions and metadata.
    /// </summary>
    public required ColdStartBundle Bundle { get; init; }

    /// <summary>
    /// Thread-safe event queue for Windows events -> Smalltalk polling.
    /// </summary>
    public required SmalltalkEventQueue EventQueue { get; init; }

    /// <summary>
    /// Platform bridge for Smalltalk -> UI communication.
    /// Smalltalk thread calls methods on this object (thread-safe).
    /// </summary>
    public required IPlatformBridge PlatformBridge { get; init; }

    /// <summary>
    /// Diagnostic hub for logging and events.
    /// </summary>
    public required DiagnosticHub Diagnostics { get; init; }

    /// <summary>
    /// Command-line configuration options.
    /// </summary>
    public required CommandLineConfig Config { get; init; }

    /// <summary>
    /// Cancellation token source for signaling shutdown to the Smalltalk thread.
    /// The main thread signals this when the window closes.
    /// </summary>
    public CancellationTokenSource ShutdownTokenSource { get; } = new();

    /// <summary>
    /// Cancellation token that the Smalltalk thread checks to know when to stop.
    /// </summary>
    public CancellationToken ShutdownToken => ShutdownTokenSource.Token;

    /// <summary>
    /// Phase 2: Signaled by Smalltalk thread after InitializeEventArchitecture completes.
    /// Main thread waits on this before showing window to prevent race condition where
    /// events arrive before event callbacks are registered.
    /// </summary>
    public ManualResetEventSlim EventCallbacksReady { get; } = new(false);
}
