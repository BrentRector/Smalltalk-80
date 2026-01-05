using System.Diagnostics;
using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Engine;
using Smalltalk80.Interpreter.Engine.LoaderPhases;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Core.VM.Platform;

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
    /// VM metadata including special selectors and class OOPs.
    /// </summary>
    public required VMMetadata VMMetadata { get; init; }

    /// <summary>
    /// The cold start bundle containing init expressions and metadata.
    /// Null when restoring from a saved image (init expressions already ran).
    /// </summary>
    public ColdStartBundle? Bundle { get; init; }

    /// <summary>
    /// Display for Smalltalk -> UI communication.
    /// Initially null - created lazily when Smalltalk calls BeDisplay.
    /// Smalltalk thread calls methods on this object (thread-safe).
    /// </summary>
    public IDisplay? Display { get; set; }

    /// <summary>
    /// Factory function to create the display window.
    /// Called from UI thread when display creation is requested.
    /// Parameters: (width, height, heap, metadata) -> IDisplay
    /// </summary>
    public Func<int, int, ISmalltalkHeap, VMMetadata, IDisplay>? DisplayFactory { get; init; }

    /// <summary>
    /// Signaled by Smalltalk thread when BeDisplay is called and display needs to be created.
    /// UI thread waits on this, then creates the display.
    /// </summary>
    public ManualResetEventSlim DisplayNeeded { get; } = new(false);

    /// <summary>
    /// Signaled by UI thread after display has been created.
    /// Smalltalk thread waits on this before continuing after BeDisplay.
    /// </summary>
    public ManualResetEventSlim DisplayCreated { get; } = new(false);

    /// <summary>
    /// Requested display width (set by Smalltalk thread before signaling DisplayNeeded).
    /// </summary>
    public int RequestedDisplayWidth { get; set; }

    /// <summary>
    /// Requested display height (set by Smalltalk thread before signaling DisplayNeeded).
    /// </summary>
    public int RequestedDisplayHeight { get; set; }

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

    /// <summary>
    /// Signaled by main thread after window has been displayed.
    /// Smalltalk thread waits on this before executing init expressions to ensure
    /// window is ready for graphics operations.
    /// </summary>
    public ManualResetEventSlim WindowDisplayed { get; } = new(false);

    /// <summary>
    /// Stopwatch for timing image restore operations.
    /// Started in Program.cs when restore begins, stopped in SmalltalkInterpreterThread
    /// when interpreter resumes execution.
    /// </summary>
    public Stopwatch? RestoreTimer { get; set; }
}
