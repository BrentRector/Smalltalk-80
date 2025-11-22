using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Engine;
using Smalltalk80.Interpreter.Engine.LoaderPhases;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Platform;
using Smalltalk80.Interpreter.Platform.Windows;

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
    /// The WinForms display provider (implements IPlatformBridge).
    /// </summary>
    public required WinFormsDisplayProvider DisplayProvider { get; init; }

    /// <summary>
    /// Diagnostic hub for logging and events.
    /// </summary>
    public required DiagnosticHub Diagnostics { get; init; }

    /// <summary>
    /// Command-line configuration options.
    /// </summary>
    public required CommandLineConfig Config { get; init; }
}
