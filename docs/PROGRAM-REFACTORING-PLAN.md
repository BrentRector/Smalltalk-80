# Program.cs Thread Refactoring Plan

**Version:** 1.0
**Date:** 2025-11-22
**Purpose:** Refactor Program.cs to separate Windows UI thread from Smalltalk interpreter thread

---

## Current Architecture Analysis

### Current Flow (All on Main Thread)

```
[STAThread] Main()
  ├─ 1. Initialize WinForms (lines 27-28)
  ├─ 2. Parse command line (lines 36-60)
  ├─ 3. Create diagnostics (lines 63-119)
  ├─ 4. Load cold start bundle (lines 123-131)
  ├─ 5. Create heap manager (lines 134-135)
  ├─ 6. Run cold start - 6 phases (lines 138-157)
  ├─ 7. Create WinForms display window (lines 160-162)
  ├─ 8. Run GC (lines 165-167)
  ├─ 9. Execute init expressions (lines 169-448) ← RUNS ON MAIN THREAD
  ├─ 10. Show display window (lines 455-456)
  ├─ 11. Execute user expression (lines 459-572)
  └─ 12. Run WinForms event loop (lines 581-583)
```

**Problem:** Init expressions and all Smalltalk execution happens on main thread, blocking the UI event loop.

**Result:** Window doesn't respond to events during init expression execution.

---

## Target Architecture

### Refactored Flow (Two Threads)

```
[STAThread] Main() - WINDOWS UI THREAD
  ├─ 1. Initialize WinForms
  ├─ 2. Parse command line
  ├─ 3. Create diagnostics
  ├─ 4. Load cold start bundle
  ├─ 5. Create heap manager
  ├─ 6. Run cold start - 6 phases
  ├─ 7. Create event queue (NEW)
  ├─ 8. Create WinForms display window (with heap reference)
  ├─ 9. Create Smalltalk thread (NEW)
  │      └─ RunSmalltalkInterpreter() - SMALLTALK THREAD
  │           ├─ Convert thread to fiber (NEW)
  │           ├─ Create fiber scheduler (NEW)
  │           ├─ Create VM
  │           ├─ Execute init expressions
  │           ├─ Execute user expression (if requested)
  │           ├─ Start controller loop (NEW)
  │           └─ Run fibers forever (NEW)
  ├─ 10. Start Smalltalk thread
  ├─ 11. Show display window
  └─ 12. Run WinForms event loop (Application.Run)
           └─ When window closes: signal Smalltalk thread to shutdown
```

**Benefit:** UI remains responsive while Smalltalk code runs in separate thread.

---

## Detailed Refactoring Steps

### Step 1: Create SmalltalkThreadContext

**Purpose:** Package all data needed by Smalltalk thread

**New File:** `Smalltalk80.Interpreter/SmalltalkThreadContext.cs`

```csharp
namespace Smalltalk80.Interpreter;

/// <summary>
/// Context object containing all data needed by the Smalltalk interpreter thread.
/// </summary>
public class SmalltalkThreadContext
{
    public required ISmalltalkHeap HeapManager { get; init; }
    public required LayoutResolver LayoutResolver { get; init; }
    public required VMMetadata VMMetadata { get; init; }
    public required ColdStartBundle Bundle { get; init; }
    public required SmalltalkEventQueue EventQueue { get; init; }
    public required WinFormsDisplayProvider DisplayProvider { get; init; }
    public required DiagnosticHub Diagnostics { get; init; }
    public required CommandLineConfig Config { get; init; }
}
```

---

### Step 2: Create SmalltalkInterpreterThread Class

**Purpose:** Encapsulate all Smalltalk thread logic

**New File:** `Smalltalk80.Interpreter/SmalltalkInterpreterThread.cs`

```csharp
using System.Runtime.InteropServices;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Engine.InitExecution;
using Smalltalk80.Interpreter.Platform.Windows;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Manages the Smalltalk interpreter thread and fiber scheduling.
/// </summary>
public class SmalltalkInterpreterThread
{
    private readonly SmalltalkThreadContext _context;
    private FiberScheduler? _fiberScheduler;
    private VirtualMachine? _vm;
    private bool _shutdownRequested;

    public SmalltalkInterpreterThread(SmalltalkThreadContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Main entry point for Smalltalk thread.
    /// This method runs on the Smalltalk thread (not main UI thread).
    /// </summary>
    public void Run()
    {
        try
        {
            Console.WriteLine("\n[Smalltalk Thread] Starting...");

            // STEP 1: Convert this thread to a Windows fiber
            Console.WriteLine("[Smalltalk Thread] Converting to fiber...");
            ConvertThreadToFiber();

            // STEP 2: Create VM
            Console.WriteLine("[Smalltalk Thread] Creating virtual machine...");
            _vm = CreateVirtualMachine();

            // STEP 3: Create fiber scheduler (will be used after init expressions)
            Console.WriteLine("[Smalltalk Thread] Creating fiber scheduler...");
            _fiberScheduler = new FiberScheduler(
                _context.HeapManager,
                _vm,
                _context.EventQueue
            );
            _fiberScheduler.Initialize();

            // STEP 4: Execute init expressions (runs in initial process, not yet using fibers)
            Console.WriteLine("[Smalltalk Thread] Executing init expressions...");
            ExecuteInitExpressions();

            // STEP 5: Execute user expression if requested
            if (_context.Config.ExecuteExpression != null)
            {
                Console.WriteLine("[Smalltalk Thread] Executing user expression...");
                ExecuteUserExpression();
            }

            // STEP 6: Start controller loop and background processes
            Console.WriteLine("[Smalltalk Thread] Starting controller loop...");
            StartControllerLoop();

            // STEP 7: Run fibers forever (never returns unless shutdown requested)
            Console.WriteLine("[Smalltalk Thread] Entering fiber event loop...");
            RunFiberLoop();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n[Smalltalk Thread] Fatal error: {ex.Message}");
            Console.Error.WriteLine($"Stack trace:\n{ex.StackTrace}");
            Environment.Exit(1);
        }
    }

    private void ConvertThreadToFiber()
    {
        var fiberPtr = NativeMethods.ConvertThreadToFiber(IntPtr.Zero);
        if (fiberPtr == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"Failed to convert thread to fiber. Error code: {error}");
        }
    }

    private VirtualMachine CreateVirtualMachine()
    {
        var vm = new VirtualMachine(
            _context.HeapManager,
            _context.LayoutResolver,
            _context.VMMetadata,
            platformBridge: _context.DisplayProvider, // DisplayProvider implements IPlatformBridge
            _context.Diagnostics
        );

        vm.VerboseDebug = _context.Config.VerboseDebug;
        vm.InitializeSelectorCaches();

        return vm;
    }

    private void ExecuteInitExpressions()
    {
        if (_context.Bundle.InitExpressions.Length == 0)
        {
            Console.WriteLine("  No init expressions found");
            return;
        }

        Console.WriteLine("\n=== Init Expression Execution ===");

        var executor = new InitExpressionExecutor(
            _context.HeapManager,
            _vm!,
            _context.VMMetadata,
            _context.Diagnostics,
            InitPolicy.ContinueAndCollect,
            defaultTimeoutMs: 5000
        );

        // Essential init expressions (always run)
        var essentialList = new HashSet<string>
        {
            "Character initialize",
            "SmallInteger initialize",
            "Float initialize",
            "InputSensor initialize",
            "InputSensor install",
            "InputSensor initSensor",
            "BlockContextTest validate",
        };

        // Skip list
        var skipList = new HashSet<string>
        {
            "FormMenuView initialize", // Requires file I/O primitives
        };

        // Build phase lists
        var phase1Indices = new List<int>();
        var skippedIndices = new List<(int index, string reason)>();

        for (int i = 0; i < _context.Bundle.InitExpressions.Length; i++)
        {
            var expr = _context.Bundle.InitExpressions[i];
            if (expr.Source == null) continue;

            bool isEssential = essentialList.Contains(expr.Source);

            if (!isEssential && skipList.Contains(expr.Source))
            {
                skippedIndices.Add((i, "Requires features not available"));
                continue;
            }

            if (_context.Config.SkipInitExpressions && !isEssential)
            {
                skippedIndices.Add((i, "Skipped by --skip-init-expressions"));
                continue;
            }

            if (expr.Source.EndsWith(" initialize") ||
                expr.Source == "InputSensor install" ||
                expr.Source == "InputSensor initSensor" ||
                expr.Source.Contains("Test validate"))
            {
                phase1Indices.Add(i);
            }
        }

        // Dependency ordering
        var dependencyOrder = InitExpressionOrdering.GetDependencyOrder();
        phase1Indices = InitExpressionOrdering.ReorderByDependencies(
            phase1Indices, _context.Bundle, dependencyOrder);

        // Execute all
        var compiler = new InitExpressionCompiler(
            _context.HeapManager, _context.Bundle, _context.VMMetadata);

        int succeeded = 0;
        int failed = 0;

        foreach (int index in phase1Indices)
        {
            var expr = _context.Bundle.InitExpressions[index];
            var result = executor.ExecuteInitExpressionOnDemand(expr, compiler);

            if (result.Status == InitStatus.Success)
            {
                succeeded++;
            }
            else
            {
                failed++;
                Console.WriteLine($"    ❌ Failed: {expr.Source}");
                Console.WriteLine($"       Status: {result.Status}");
            }
        }

        Console.WriteLine($"\n=== Init Expression Execution Complete ===");
        Console.WriteLine($"  {succeeded} succeeded, {failed} failed");
    }

    private void ExecuteUserExpression()
    {
        // Implementation similar to current Program.cs lines 459-572
        // TODO: Extract this logic
    }

    private void StartControllerLoop()
    {
        // TODO: Execute Smalltalk code:
        // ProcessorScheduler hiddenBackgroundProcess.
        // ScheduledControllers _ ControlManager new.
        // ScheduledControllers searchForActiveController.

        Console.WriteLine("  Controller loop start: NOT YET IMPLEMENTED");
    }

    private void RunFiberLoop()
    {
        // This never returns - fibers run forever
        // The fiber scheduler manages all process switching

        while (!_shutdownRequested)
        {
            // In actual implementation, this would be handled by fiber switching
            // For now, just sleep to prevent busy-wait
            Thread.Sleep(100);
        }
    }

    public void RequestShutdown()
    {
        _shutdownRequested = true;
    }
}
```

---

### Step 3: Refactor Program.cs Main Method

**Purpose:** Separate UI thread from Smalltalk thread

**Modified File:** `Smalltalk80.Interpreter/Program.cs`

```csharp
[STAThread]
public static void Main(string[] args)
{
    // Enable Windows Forms visual styles
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);

    Console.WriteLine("Smalltalk-80 Interpreter");
    Console.WriteLine("========================\n");

    try
    {
        // ===== MAIN THREAD: UI THREAD PORTION =====

        // 1. Parse command line arguments
        var config = CommandLineParser.Parse(args);
        if (config == null)
        {
            Environment.Exit(0);
            return;
        }

        if (!File.Exists(config.ColdStartFile))
        {
            Console.Error.WriteLine($"Error: Cold start file not found: {config.ColdStartFile}");
            CommandLineParser.PrintUsage();
            Environment.Exit(1);
            return;
        }

        // 2. Create diagnostic hub
        var diagnostics = new DiagnosticHub();
        RegisterDiagnosticListeners(diagnostics, config.DiagnosticCategories);

        // 3. Load cold start bundle
        Console.WriteLine($"Loading cold start bundle: {config.ColdStartFile}");
        var bundle = ColdStartBundle.LoadFromFile(config.ColdStartFile);
        Console.WriteLine($"Bundle loaded: {bundle.Metadata.Version}");

        // 4. Create heap manager
        Console.WriteLine("Initializing heap manager...");
        var heapManager = new SmalltalkHeapManager(diagnostics);

        // 5. Run cold start (6 phases)
        Console.WriteLine("Starting cold start loader (6-phase)...");
        var loader = new ColdStartOrchestrator(bundle, heapManager, config.ColdStartFile, diagnostics);
        loader.Run();
        var layoutResolver = loader.LayoutResolver;
        Console.WriteLine("Cold start completed successfully!");

        // 6. Build VM metadata
        var vmMetadata = BuildVMMetadata(layoutResolver, loader);

        // 7. Create event queue (shared between threads)
        Console.WriteLine("\n=== Creating Event Queue ===");
        var eventQueue = new SmalltalkEventQueue();

        // 8. Create WinForms display window (but don't show yet)
        Console.WriteLine("\n=== Creating Display Window ===");
        var displayProvider = new WinFormsDisplayProvider(
            eventQueue,
            heapManager
        );
        Console.WriteLine("Display window created (not shown yet)...");

        // 9. Create Smalltalk thread context
        var threadContext = new SmalltalkThreadContext
        {
            HeapManager = heapManager,
            LayoutResolver = layoutResolver,
            VMMetadata = vmMetadata,
            Bundle = bundle,
            EventQueue = eventQueue,
            DisplayProvider = displayProvider,
            Diagnostics = diagnostics,
            Config = config
        };

        // 10. Create and start Smalltalk thread
        Console.WriteLine("\n=== Starting Smalltalk Thread ===");
        var interpreterThread = new SmalltalkInterpreterThread(threadContext);

        var smalltalkThread = new Thread(interpreterThread.Run);
        smalltalkThread.SetApartmentState(ApartmentState.MTA);
        smalltalkThread.IsBackground = false; // Foreground thread
        smalltalkThread.Name = "Smalltalk Interpreter";
        smalltalkThread.Start();

        // Give Smalltalk thread time to start init expressions
        Thread.Sleep(500);

        // 11. Show display window
        Console.WriteLine("\n=== Showing Display Window ===");
        displayProvider.Show();

        // 12. Run WinForms event loop (blocks until window closes)
        Console.WriteLine("\n=== Entering GUI Event Loop ===");
        Console.WriteLine("Display window is visible. Close window to exit.\n");

        Application.Run(displayProvider);

        // When window closes, signal Smalltalk thread to shutdown
        Console.WriteLine("\n=== Shutting Down ===");
        interpreterThread.RequestShutdown();

        // Wait for Smalltalk thread to finish (with timeout)
        if (!smalltalkThread.Join(TimeSpan.FromSeconds(5)))
        {
            Console.WriteLine("Warning: Smalltalk thread did not shut down cleanly");
            smalltalkThread.Interrupt();
        }

        Console.WriteLine("Interpreter shutdown complete");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"\nFatal error: {ex.Message}");
        Console.Error.WriteLine($"\nStack trace:\n{ex.StackTrace}");
        Environment.Exit(1);
    }
}

private static void RegisterDiagnosticListeners(DiagnosticHub diagnostics, string? categories)
{
    // Existing diagnostic listener registration code (lines 66-119)
    // Extracted to separate method for clarity
}
```

---

### Step 4: Update WinFormsDisplayProvider Constructor

**Purpose:** Accept heap reference for display bridge

**Modified File:** `Smalltalk80.Interpreter/Platform/Windows/WinFormsDisplayProvider.cs`

```csharp
public class WinFormsDisplayProvider : Form, IPlatformBridge
{
    private readonly SmalltalkEventQueue _eventQueue;
    private readonly ISmalltalkHeap _heap;
    private readonly SmalltalkOOP _displayOOP;
    private readonly byte[] _localDisplayBuffer;
    private readonly Bitmap _backBuffer;
    private readonly object _displayLock = new object();

    public WinFormsDisplayProvider(
        SmalltalkEventQueue eventQueue,
        ISmalltalkHeap heap)
    {
        _eventQueue = eventQueue;
        _heap = heap;

        // Get Display OOP from globals (created during cold start)
        _displayOOP = GlobalRegistry.Get("Display");

        // Create local display buffer (copy of Display bits)
        _localDisplayBuffer = new byte[38400]; // 640x480 @ 1bpp

        // Create 32bpp back buffer for rendering
        _backBuffer = new Bitmap(640, 480, PixelFormat.Format32bppArgb);

        // Configure window
        this.ClientSize = new Size(640, 480);
        this.DoubleBuffered = true;
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.Text = "Smalltalk-80";
        this.StartPosition = FormStartPosition.CenterScreen;

        // Register event handlers
        this.MouseMove += OnMouseMove;
        this.MouseDown += OnMouseDown;
        this.MouseUp += OnMouseUp;
        this.KeyPress += OnKeyPress;
        this.Paint += OnPaint;

        // Initial sync from heap
        CopyEntireDisplayFromHeap();
    }

    // Event handlers enqueue to _eventQueue
    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        _eventQueue.Enqueue(new SmalltalkEvent
        {
            Type = SmalltalkEventType.Mouse,
            X = e.X,
            Y = e.Y,
            RedButton = (e.Button & MouseButtons.Left) != 0,
            YellowButton = (e.Button & MouseButtons.Middle) != 0,
            BlueButton = (e.Button & MouseButtons.Right) != 0,
            Milliseconds = Environment.TickCount64
        });
    }

    // IPlatformBridge implementation
    public void DisplayRegionChanged(Rectangle region)
    {
        lock (_displayLock)
        {
            CopyDisplayBitsFromHeap(region);
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Invalidate(region)));
        }
        else
        {
            Invalidate(region);
        }
    }

    private void CopyDisplayBitsFromHeap(Rectangle region)
    {
        var bitsOOP = _heap.GetInstanceVariable(_displayOOP, VMConstants.FORM_BITS_INDEX);

        // Copy bytes from heap to local buffer
        // (implementation as shown in planning doc)
    }

    // ... rest of implementation
}
```

---

### Step 5: Create SmalltalkEventQueue

**Purpose:** Thread-safe event queue for UI → Smalltalk communication

**New File:** `Smalltalk80.Interpreter/Platform/EventQueue/SmalltalkEventQueue.cs`

```csharp
using System.Collections.Concurrent;

namespace Smalltalk80.Interpreter.Platform;

public enum SmalltalkEventType
{
    Mouse,
    Keyboard,
    System
}

public class SmalltalkEvent
{
    public SmalltalkEventType Type { get; set; }

    // Mouse data
    public int X { get; set; }
    public int Y { get; set; }
    public bool RedButton { get; set; }
    public bool YellowButton { get; set; }
    public bool BlueButton { get; set; }

    // Keyboard data
    public char Character { get; set; }
    public int KeyCode { get; set; }

    // Timestamp
    public long Milliseconds { get; set; }
}

public class SmalltalkEventQueue
{
    private readonly ConcurrentQueue<SmalltalkEvent> _events;
    private SmalltalkEvent? _lastMouseState;
    private readonly object _mouseLock = new object();

    public SmalltalkEventQueue()
    {
        _events = new ConcurrentQueue<SmalltalkEvent>();
    }

    // Called by WinForms thread
    public void Enqueue(SmalltalkEvent evt)
    {
        if (evt.Type == SmalltalkEventType.Mouse)
        {
            lock (_mouseLock)
            {
                _lastMouseState = evt;
            }
        }
        _events.Enqueue(evt);
    }

    // Called by Smalltalk thread (Primitive 95: inputWord)
    public bool TryDequeue(out SmalltalkEvent? evt)
    {
        return _events.TryDequeue(out evt);
    }

    // Called by Smalltalk thread (Primitive 90: mousePoint)
    public (int x, int y) GetMousePosition()
    {
        lock (_mouseLock)
        {
            if (_lastMouseState != null)
                return (_lastMouseState.X, _lastMouseState.Y);
            return (0, 0);
        }
    }
}
```

---

## Implementation Checklist

**Status: ✅ PHASES 1-5 COMPLETE (Fiber support deferred)**

### Phase 1: Basic Thread Separation ✅ COMPLETE
- [x] Create `SmalltalkThreadContext.cs`
- [x] Create `SmalltalkEventQueue.cs`
- [x] Create `SmalltalkEvent.cs`
- [x] Refactor `WinFormsDisplayProvider` constructor to accept width/height parameters
- [x] Extract diagnostic listener registration to separate method in Program.cs
- [x] Extract VMMetadata building to separate method (already exists)

### Phase 2: Smalltalk Thread ✅ COMPLETE
- [x] Create `SmalltalkInterpreterThread.cs`
- [x] Implement `Run()` method (fiber conversion commented out for Phase 3)
- [x] Implement `ExecuteInitExpressions()` (extracted from Program.cs)
- [x] Implement `ExecuteUserExpression()` (extracted from Program.cs)
- [⏸️] Add P/Invoke for `ConvertThreadToFiber` (deferred to Phase 3 - Fiber Infrastructure)

### Phase 3: Main Thread Refactoring ✅ COMPLETE
- [x] Refactor `Program.cs Main()` to create Smalltalk thread
- [x] Move init expression execution to Smalltalk thread
- [x] Keep WinForms event loop on main thread
- [x] Add thread synchronization (start/stop)

### Phase 4: Testing ✅ COMPLETE
- [x] Test that window remains responsive during init expressions
- [x] Test that display updates work from Smalltalk thread
- [x] Test that events flow from WinForms to Smalltalk
- [x] Test shutdown sequence (window close → thread termination)

### Phase 5: Integration ✅ COMPLETE
- [x] Test with all existing command-line flags
- [x] Test with --skip-init-expressions
- [x] Test with --execute
- [x] Test with --diagnostics
- [x] Verify BlockContext regression tests still pass (120/120 passing)

---

## Benefits

### Immediate Benefits
1. **Responsive UI:** Window responds to events during init expression execution
2. **Better Architecture:** Clear separation between UI and interpreter
3. **Progress Reporting:** Can update UI during long-running operations
4. **Cancellation:** Can add "Cancel" button to stop init expression execution

### Future Benefits
1. **Fiber Integration:** Easy to add FiberScheduler once threads are separated
2. **Multiple Processes:** Natural fit for Smalltalk process scheduling
3. **Controller Loop:** Can run controller event loop on Smalltalk thread
4. **Hot Reload:** Can restart Smalltalk thread without restarting UI

---

## Risks and Mitigation

### Risk 1: Thread Safety Issues
**Risk:** Race conditions between WinForms thread and Smalltalk thread
**Mitigation:**
- Use `ConcurrentQueue` for event queue (lock-free)
- Use locks for display buffer access
- Use `BeginInvoke` for cross-thread UI calls
- Never call from WinForms → Smalltalk (one-way only)

### Risk 2: Heap Access from Multiple Threads
**Risk:** Heap manager not thread-safe
**Mitigation:**
- Document that heap can only be accessed from Smalltalk thread
- WinForms only reads via platform bridge callbacks (from Smalltalk thread)
- DisplayRegionChanged called from Smalltalk thread (Primitive 96)

### Risk 3: Startup Timing
**Risk:** Window shown before Smalltalk ready
**Mitigation:**
- Show window after Smalltalk thread starts
- Add synchronization signal when init expressions complete
- Use Thread.Sleep as simple synchronization (can improve later)

### Risk 4: Shutdown Deadlock
**Risk:** Threads don't shut down cleanly
**Mitigation:**
- Use timeout on thread join
- Mark Smalltalk thread as foreground (not background)
- Interrupt thread if timeout expires
- Test shutdown sequence thoroughly

---

## Testing Strategy

### Unit Tests
- `SmalltalkEventQueue` thread safety
- Event enqueue/dequeue from different threads
- Mouse position tracking

### Integration Tests
- Full startup with thread separation
- Init expression execution on Smalltalk thread
- WinForms event loop responsiveness
- Display updates from Primitive 96
- Clean shutdown sequence

### Manual Tests
- Resize window during init expression execution
- Move window during init expression execution
- Click on window during init expression execution
- Close window during init expression execution
- All existing command-line flags

---

## Success Criteria

### Must Have
1. ✅ Window remains responsive during init expression execution
2. ✅ All 46 init expressions still pass
3. ✅ BlockContext tests still pass (120/120)
4. ✅ Display updates work correctly
5. ✅ Clean shutdown (no crashes, no hangs)

### Should Have
1. ✅ All command-line flags work
2. ✅ Diagnostic output works
3. ✅ User expressions work (--execute flag)
4. ✅ No performance regression

### Nice to Have
1. ⚠️ Progress bar during init expressions
2. ⚠️ Cancel button for long operations
3. ⚠️ Live display updates during init
4. ⚠️ Thread CPU usage monitoring

---

## Timeline Estimate

**Phase 1 (Basic Thread Separation):** 4 hours
**Phase 2 (Smalltalk Thread):** 6 hours
**Phase 3 (Main Thread Refactoring):** 4 hours
**Phase 4 (Testing):** 6 hours
**Phase 5 (Integration):** 4 hours

**Total:** 24 hours (3 days)

---

## References

**Current Implementation:**
- `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Program.cs` (lines 1-879)
- `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Platform/Windows/WinFormsDisplayProvider.cs`

**Planning Documents:**
- `/home/user/Smalltalk-80/docs/SYSTEM-STARTUP-IMPLEMENTATION-PLAN.md`
- Sections 2.5 (Window Creation Timing) and 2.6 (Display Bridge Architecture)

**Windows API:**
- `ConvertThreadToFiber`: https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-convertthreadtofiber

---

**END OF REFACTORING PLAN**
