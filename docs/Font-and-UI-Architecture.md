# Font and User Interface Architecture Specification

**Created**: 2025-11-04
**Status**: Architecture Design Document
**Target Platform**: Windows (C# with WinForms/WPF)

---

## Executive Summary

This document provides a complete architectural roadmap for implementing the Smalltalk-80 graphical user interface on Windows. The approach bridges Smalltalk-80's bitmap-based graphics system with Windows native rendering, providing a path from the current headless interpreter to a fully functional windowed environment.

**Current State**: Interpreter with font/graphics support - 31/46 init expressions passing, Form initialization working
**Target State**: Full Smalltalk-80 UI with windows, menus, text editing, and graphics

---

## Table of Contents

1. [Architectural Principles](#architectural-principles)
2. [Phase 0: Current State Analysis](#phase-0-current-state-analysis)
3. [Phase 1: Minimal Font Infrastructure](#phase-1-minimal-font-infrastructure-immediate)
4. [Phase 2: Display Primitive Foundation](#phase-2-display-primitive-foundation)
5. [Phase 3: BitBlt Graphics Engine](#phase-3-bitblt-graphics-engine)
6. [Phase 4: Windows Integration Layer](#phase-4-windows-integration-layer)
7. [Phase 5: Text Rendering](#phase-5-text-rendering)
8. [Phase 6: Window Management](#phase-6-window-management)
9. [Phase 7: Input System](#phase-7-input-system)
10. [Phase 8: Complete UI Stack](#phase-8-complete-ui-stack)
11. [Testing Strategy](#testing-strategy)
12. [Performance Considerations](#performance-considerations)

---

## Architectural Principles

### 1. **Hybrid Native Approach**

**Principle**: Bridge Smalltalk-80's bitmap graphics model with Windows native rendering.

**Rationale**:
- Original Smalltalk-80 used bitmap fonts and direct framebuffer access
- Modern Windows provides high-quality font rendering, Unicode support, and hardware acceleration
- Hybrid approach: Preserve Smalltalk-80 API, delegate rendering to Windows

### 2. **Lazy Windows Integration**

**Principle**: Create minimal stubs during cold-start, bind to Windows on first render.

**Implementation**:
- Cold-start Phase 3: Create `DefaultTextStyle` with nil glyphs
- Runtime: Detect nil glyphs, map to Windows fonts
- Benefits: Fast cold-start, no font file parsing during initialization

### 3. **Primitive-Driven Graphics**

**Principle**: Implement graphics operations as VM primitives, not Smalltalk code.

**Why**:
- Performance: Direct C# → Windows GDI+/DirectX
- Correctness: Avoid Smalltalk-level bitmap manipulation complexity
- Flexibility: Can swap rendering backends without changing Smalltalk code

### 4. **Compatibility with Blue Book**

**Principle**: Smalltalk code should not know it's running on Windows.

**Guarantees**:
- All Smalltalk-80 graphics messages work unchanged
- BitBlt operations produce identical results
- Display coordinates match Smalltalk-80 conventions (origin top-left)

---

## Phase 0: Current State Analysis

### What We Have

**Interpreter Core**:
- ✅ Complete bytecode execution engine
- ✅ Method dispatch and primitive support
- ✅ Garbage collection
- ✅ 474 classes loaded, 10,100 methods

**Missing for UI**:
- ✅ DefaultTextStyle - IMPLEMENTED
- ✅ Display primitive (primitive 90-96) - IMPLEMENTED
- ✅ BitBlt primitive (primitive 96) - IMPLEMENTED
- ✅ Font rendering - IMPLEMENTED
- ❌ Window system integration
- ❌ Mouse/keyboard input primitives

### The Immediate Problem

**Error**: `ChangeListController initialize` fails with:
```
Method not found: fontAt:
Receiver: nil (expected: DefaultTextStyle)
```

**Call Chain**:
1. `ChangeListController initialize`
2. → `PopUpMenu class>>labels:lines:`
3. → `TextStyle default` (returns `DefaultTextStyle copy`)
4. → `fontAt: 1` (fails because DefaultTextStyle is nil)

**Root Cause**: DefaultTextStyle is an **image-provided ambient object**, never created by source code. Similar to Display, Sensor, ProcessorScheduler.

---

## Phase 1: Minimal Font Infrastructure (IMMEDIATE)

**Goal**: Create DefaultTextStyle during cold-start so PopUpMenu can instantiate.

**Status**: ✅ COMPLETED (November 2025)

DefaultTextStyle is now properly created with real StrikeFont objects using WinFormsFontBridge for font metrics and glyph rendering. See `Font-Support-Implementation.md` for details.

### 1.1 Cold-Start Font Stubs

**Location**: `Phase3_ObjectAllocation.cs` (after AllocateSpecialObjects)

**Create**:
```csharp
// Smalltalk Objects Created:
DefaultTextStyle (instance of TextStyle)
  └─ fontArray: Array(1)
       └─ [0]: StrikeFont stub

// StrikeFont stub fields:
name: "SystemFont" (String)
ascent: 10 (SmallInteger)
descent: 3 (SmallInteger)
maxWidth: 8 (SmallInteger)
minAscii: 32 (SmallInteger)
maxAscii: 126 (SmallInteger)
glyphs: nil (NO bitmap data)
xTable: nil (NO character widths)

// TextStyle fields:
fontArray: Array with one StrikeFont
lineGrid: 14 (SmallInteger: ascent + descent + padding)
baseline: 10 (SmallInteger: matches ascent)
alignment: 0 (SmallInteger: left flush)
firstIndent: 0
restIndent: 0
rightIndent: 0
tabsArray: nil
marginTabsArray: nil
outputMedium: nil
```

**Registration**: Add DefaultTextStyle to Undeclared dictionary (since no pool:TextConstants Association exists yet)

### 1.2 Why This Works

**For PopUpMenu instantiation**:
- ✅ `TextStyle default` returns valid TextStyle object
- ✅ `fontAt: 1` returns valid StrikeFont object
- ✅ `isMemberOf: StrikeFont` check passes
- ❌ Actual rendering won't work (no glyphs)

**For Future Rendering**:
- Font name "SystemFont" maps to Windows font
- Primitives detect nil glyphs → use Windows rendering
- Metrics (ascent/descent) match Windows font

### 1.3 Files Modified

- `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase3_ObjectAllocation.cs`
  - Add `CreateDefaultTextStyle(nilOOP)` method
  - Call after AllocateSpecialObjects()

### 1.4 Success Criteria

- ✅ Cold-start completes without errors
- ✅ ChangeListController initialize passes
- ✅ BitEditor initialize passes
- ✅ Init expression count increases (currently 23/45)

---

## Phase 2: Display Primitive Foundation

**Goal**: Create Display object and primitive infrastructure for rendering.

**Status**: ✅ COMPLETED (November 2025)

Display object is created during Phase 5 cold start. Graphics primitives (96-104) are implemented.

### 2.1 Display Object Creation

**Smalltalk Structure** (from Blue Book):
```smalltalk
Display (instance of DisplayScreen)
  └─ bits: Bitmap (16-bit words)
  └─ width: SmallInteger (pixels)
  └─ height: SmallInteger (pixels)
  └─ offset: Point (0@0 typically)
```

**Windows Mapping**:
```csharp
// C# Backing Store
private static Bitmap _displayBitmap;
private static Graphics _displayGraphics;
private static Form _displayWindow;

// Create during cold-start
DisplayScreen instance:
  width: 1024 (or screen width)
  height: 768 (or screen height)
  bits: ByteArray (backing store OOP, not real bitmap)
```

### 2.2 Display Primitives (Primitive 90-96)

**Primitive 90**: `beDisplay` - Make this Form the Display
```csharp
public static void BeDisplay(IPrimitiveContext ctx, SmalltalkOOP receiverOOP, SmalltalkOOP[] args)
{
    // Set global Display reference
    SpecialObjectsArray.Instance.Display = receiverOOP;

    // Initialize Windows backing bitmap
    int width = GetFormWidth(receiverOOP);
    int height = GetFormHeight(receiverOOP);
    _displayBitmap = new Bitmap(width, height);
    _displayGraphics = Graphics.FromImage(_displayBitmap);
}
```

**Primitive 91**: `primShowDisplay` - Force screen update
```csharp
public static void PrimShowDisplay(IPrimitiveContext ctx, SmalltalkOOP receiverOOP, SmalltalkOOP[] args)
{
    // Blit backing bitmap to actual window
    if (_displayWindow != null && _displayBitmap != null)
    {
        var graphics = _displayWindow.CreateGraphics();
        graphics.DrawImage(_displayBitmap, 0, 0);
    }
}
```

### 2.3 Display Window Creation

**WinForms Approach** (simpler, recommended for Phase 2):
```csharp
public class SmalltalkDisplayForm : Form
{
    public SmalltalkDisplayForm()
    {
        Text = "Smalltalk-80";
        Size = new Size(1024, 768);
        BackColor = Color.White;

        // Double-buffering for smooth rendering
        DoubleBuffered = true;

        // Handle paint events
        Paint += OnPaint;
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        KeyDown += OnKeyDown;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_displayBitmap != null)
            e.Graphics.DrawImage(_displayBitmap, 0, 0);
    }
}
```

**WPF Approach** (future, for better graphics):
- Use WriteableBitmap for direct pixel access
- GPU acceleration for BitBlt operations
- Better touch/stylus support

---

## Phase 3: BitBlt Graphics Engine

**Goal**: Implement BitBlt primitive for all Smalltalk graphics operations.

**Status**: ✅ COMPLETED (November 2025)

BitBlt primitive (96) is fully implemented with all 16 combination rules. Form initialization works correctly.

### 3.1 BitBlt Overview

**Smalltalk BitBlt** (Primitive 96):
```smalltalk
BitBlt>>copyBits
    "Perform raster operation from source to destination"
    <primitive: 96>
```

**Parameters** (from BitBlt instance):
- `destForm`: Destination Form
- `sourceForm`: Source Form (or nil for solid color)
- `halftoneForm`: Halftone pattern (or nil)
- `combinationRule`: Integer (0-15, raster op)
- `destX`, `destY`: Destination origin
- `width`, `height`: Extent
- `sourceX`, `sourceY`: Source origin
- `clipX`, `clipY`, `clipWidth`, `clipHeight`: Clipping rectangle

### 3.2 Combination Rules (Raster Operations)

```
0:  src AND dest AND NOT halftone (erase)
1:  src AND halftone (and)
2:  src AND NOT dest (reverse)
3:  src (copy/paint/replace)
4:  NOT src AND dest (under)
5:  dest (noop)
6:  src XOR dest (xor)
7:  src OR dest (or)
8:  NOT (src OR dest) (nor)
9:  NOT (src XOR dest) (equiv)
10: NOT dest (invert)
11: src OR NOT dest (overdraw)
12: NOT src (not)
13: NOT src OR dest (storeOn)
14: NOT (src AND dest) (nand)
15: ALL 1s (set)
```

### 3.3 Windows GDI+ Implementation

**Strategy**: Translate BitBlt to GDI+ operations

```csharp
public static void CopyBits(IPrimitiveContext ctx, SmalltalkOOP bitBltOOP)
{
    // Extract BitBlt parameters
    var destFormOOP = GetInstanceVariable(bitBltOOP, "destForm");
    var sourceFormOOP = GetInstanceVariable(bitBltOOP, "sourceForm");
    var rule = GetInstanceVariable(bitBltOOP, "combinationRule").AsSmallInteger();

    // Get Windows bitmaps
    Bitmap destBitmap = GetFormBitmap(destFormOOP);
    Bitmap sourceBitmap = sourceFormOOP == nil ? null : GetFormBitmap(sourceFormOOP);

    // Extract rectangles
    var destRect = GetDestRect(bitBltOOP);
    var sourceRect = GetSourceRect(bitBltOOP);
    var clipRect = GetClipRect(bitBltOOP);

    // Perform operation
    using (var graphics = Graphics.FromImage(destBitmap))
    {
        graphics.SetClip(clipRect);

        switch (rule)
        {
            case 3: // Copy
                if (sourceBitmap != null)
                    graphics.DrawImage(sourceBitmap, destRect, sourceRect, GraphicsUnit.Pixel);
                break;

            case 6: // XOR
                // Use CompositingMode for XOR
                graphics.CompositingMode = CompositingMode.SourceOver;
                // ... implement XOR logic
                break;

            // ... other rules
        }
    }
}
```

**Challenges**:
- GDI+ doesn't support all 16 raster ops directly
- May need pixel-level operations for some rules
- Performance critical - optimize common cases (rule 3 = copy)

### 3.4 Form ↔ Bitmap Mapping

**Strategy**: Maintain dictionary mapping Form OOPs to Windows Bitmaps

```csharp
private static Dictionary<SmalltalkOOP, Bitmap> _formBitmaps = new();

private static Bitmap GetFormBitmap(SmalltalkOOP formOOP)
{
    if (_formBitmaps.TryGetValue(formOOP, out var bitmap))
        return bitmap;

    // Create new bitmap from Form dimensions
    int width = GetFormWidth(formOOP);
    int height = GetFormHeight(formOOP);
    bitmap = new Bitmap(width, height);

    // Copy bits from Smalltalk Bitmap to Windows Bitmap
    var bitsOOP = GetInstanceVariable(formOOP, "bits");
    CopySmtalktalkBitsToWindows(bitsOOP, bitmap);

    _formBitmaps[formOOP] = bitmap;
    return bitmap;
}
```

---

## Phase 4: Windows Integration Layer

**Goal**: Bidirectional synchronization between Smalltalk Forms and Windows Bitmaps.

**Status**: 🔴 NOT STARTED

### 4.1 Architecture Overview

```
Smalltalk Layer:
  Form instances (bits: Bitmap)
       ↕ (Sync via primitives)

Windows Layer:
  System.Drawing.Bitmap instances
       ↕ (Render via GDI+)

Display Window:
  WinForms Form (or WPF Window)
```

### 4.2 Form Synchronization

**When to Sync**:
1. **Smalltalk → Windows**: After BitBlt operation modifies Form
2. **Windows → Smalltalk**: Never (Smalltalk owns the data)

**Optimization**: Dirty rectangles
```csharp
class FormTracker
{
    public SmalltalkOOP FormOOP { get; set; }
    public Bitmap WindowsBitmap { get; set; }
    public Rectangle DirtyRect { get; set; }
    public bool IsDirty { get; set; }
}
```

### 4.3 Display Update Strategy

**Option A: Immediate Update** (simple, slow)
```csharp
// After every BitBlt:
PrimShowDisplay();
```

**Option B: Deferred Update** (complex, fast)
```csharp
// Mark dirty, update on timer or explicit request
DisplayManager.MarkDirty(destFormOOP, dirtyRect);

// Timer tick (60 FPS):
void OnTimerTick()
{
    if (DisplayManager.IsDirty)
        DisplayManager.FlushToScreen();
}
```

**Option C: Smalltalk-Driven** (Blue Book compliant)
```csharp
// Only update when Smalltalk calls Display update
Primitive 91 (primShowDisplay)
```

**Recommendation**: Start with Option A, optimize to Option C

---

## Phase 5: Text Rendering

**Goal**: Render Smalltalk text using Windows fonts.

**Status**: 🔴 NOT STARTED

### 5.1 Font Mapping Architecture

**StrikeFont → Windows Font Bridge**:

```csharp
public class FontBridge
{
    // Map Smalltalk font names to Windows font families
    private static Dictionary<string, string> _fontMapping = new()
    {
        ["SystemFont"] = "Consolas",      // Monospace for code
        ["DefaultFont"] = "Segoe UI",     // General UI
        ["TextFont"] = "Segoe UI",        // Body text
        ["BoldFont"] = "Segoe UI Bold",   // Bold text
        ["ItalicFont"] = "Segoe UI Italic", // Italic text
        ["MenuFont"] = "Segoe UI",        // Menu text
        ["WindowFont"] = "Segoe UI",      // Window titles
    };

    public static Font GetWindowsFont(SmalltalkOOP strikeFontOOP)
    {
        // Extract font name from StrikeFont
        var nameOOP = GetInstanceVariable(strikeFontOOP, "name");
        string smalltalkName = ExtractString(nameOOP);

        // Map to Windows font
        string windowsFontFamily = _fontMapping.GetValueOrDefault(smalltalkName, "Segoe UI");

        // Extract size from ascent
        var ascentOOP = GetInstanceVariable(strikeFontOOP, "ascent");
        int ascent = ascentOOP.AsSmallInteger();
        float pointSize = ascent * 0.75f; // Convert pixels to points

        return new Font(windowsFontFamily, pointSize);
    }
}
```

### 5.2 Text Rendering Primitive

**Primitive 200** (Custom): Render DisplayText
```csharp
public static void RenderDisplayText(
    IPrimitiveContext ctx,
    SmalltalkOOP displayTextOOP,
    SmalltalkOOP[] args) // [targetForm, position]
{
    // Extract text string
    var textOOP = GetInstanceVariable(displayTextOOP, "text");
    string text = ExtractTextString(textOOP);

    // Extract font from textStyle
    var textStyleOOP = GetInstanceVariable(displayTextOOP, "textStyle");
    var fontOOP = GetTextStyleDefaultFont(textStyleOOP);
    Font windowsFont = FontBridge.GetWindowsFont(fontOOP);

    // Extract position
    var positionOOP = args[1];
    int x = GetPointX(positionOOP);
    int y = GetPointY(positionOOP);

    // Get target Form bitmap
    var formOOP = args[0];
    Bitmap bitmap = GetFormBitmap(formOOP);

    // Render using Windows
    using (var graphics = Graphics.FromImage(bitmap))
    {
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        graphics.DrawString(text, windowsFont, Brushes.Black, x, y);
    }

    // Mark dirty for display update
    DisplayManager.MarkDirty(formOOP, new Rectangle(x, y, 1000, 50));
}
```

### 5.3 StrikeFont Metrics from Windows

**Query Windows for accurate metrics**:
```csharp
public static void PopulateStrikeFontMetrics(SmalltalkOOP strikeFontOOP)
{
    Font windowsFont = FontBridge.GetWindowsFont(strikeFontOOP);

    // Measure font metrics
    using (var bitmap = new Bitmap(1, 1))
    using (var graphics = Graphics.FromImage(bitmap))
    {
        var fontFamily = windowsFont.FontFamily;
        int emHeight = fontFamily.GetEmHeight(FontStyle.Regular);
        int ascent = fontFamily.GetCellAscent(FontStyle.Regular);
        int descent = fontFamily.GetCellDescent(FontStyle.Regular);

        float pixelsPerEm = windowsFont.Size / 72.0f * graphics.DpiY;
        int ascentPixels = (int)(ascent * pixelsPerEm / emHeight);
        int descentPixels = (int)(descent * pixelsPerEm / emHeight);

        // Update StrikeFont fields
        SetInstanceVariable(strikeFontOOP, "ascent", SmallInteger(ascentPixels));
        SetInstanceVariable(strikeFontOOP, "descent", SmallInteger(descentPixels));

        // Measure max character width (for monospace)
        var size = graphics.MeasureString("M", windowsFont);
        SetInstanceVariable(strikeFontOOP, "maxWidth", SmallInteger((int)size.Width));
    }
}
```

---

## Phase 6: Window Management

**Goal**: Support multiple Smalltalk windows (ScheduledControllers, Views).

**Status**: 🔴 NOT STARTED

### 6.1 Smalltalk Window Model

**Blue Book Architecture**:
```
ScheduledControllers (singleton)
  └─ activeController: Controller (currently active)
  └─ scheduledControllers: OrderedCollection (window stack)

Each Controller:
  └─ model: Object (data)
  └─ view: View (display)
  └─ sensor: InputSensor (events)
```

### 6.2 Windows Window Mapping

**Strategy**: Each Smalltalk ScheduledController → One Windows Form

```csharp
public class SmalltalkWindow : Form
{
    public SmalltalkOOP ControllerOOP { get; set; }
    public SmalltalkOOP ViewOOP { get; set; }

    private Bitmap _backBuffer;

    public SmalltalkWindow(SmalltalkOOP controllerOOP)
    {
        ControllerOOP = controllerOOP;
        ViewOOP = GetControllerView(controllerOOP);

        // Get window bounds from View
        var boundsOOP = GetInstanceVariable(ViewOOP, "bounds");
        Rectangle bounds = ExtractRectangle(boundsOOP);

        Location = bounds.Location;
        Size = bounds.Size;
        Text = GetWindowTitle(ViewOOP);

        // Create back buffer
        _backBuffer = new Bitmap(bounds.Width, bounds.Height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // Ask Smalltalk to render View to back buffer
        RenderSmalltalkView(ViewOOP, _backBuffer);

        // Blit to screen
        e.Graphics.DrawImage(_backBuffer, 0, 0);
    }
}
```

### 6.3 Window Lifecycle

**Opening a Window**:
```csharp
// Primitive 201: Open Smalltalk window
public static void OpenWindow(IPrimitiveContext ctx, SmalltalkOOP controllerOOP)
{
    var window = new SmalltalkWindow(controllerOOP);
    _windowMap[controllerOOP] = window;
    window.Show();
}
```

**Closing a Window**:
```csharp
// Primitive 202: Close Smalltalk window
public static void CloseWindow(IPrimitiveContext ctx, SmalltalkOOP controllerOOP)
{
    if (_windowMap.TryGetValue(controllerOOP, out var window))
    {
        window.Close();
        _windowMap.Remove(controllerOOP);
    }
}
```

---

## Phase 7: Input System

**Goal**: Bridge Windows mouse/keyboard events to Smalltalk InputSensor.

**Status**: 🔴 NOT STARTED

### 7.1 InputSensor Model

**Smalltalk Structure**:
```smalltalk
InputSensor instance variables:
  mousePosition: Point
  mouseButtons: SmallInteger (bitfield: left=1, middle=2, right=4)
  keyboardBuffer: SharedQueue (characters)
  shiftState: SmallInteger (shift=1, ctrl=2, alt=4)
```

### 7.2 Mouse Primitives

**Primitive 92**: `primMousePoint` - Get mouse position
```csharp
public static SmalltalkOOP PrimMousePoint(IPrimitiveContext ctx)
{
    Point mousePos = Control.MousePosition;

    // Convert screen coords to Display coords
    if (_displayWindow != null)
    {
        mousePos = _displayWindow.PointToClient(mousePos);
    }

    return AllocatePoint(mousePos.X, mousePos.Y);
}
```

**Primitive 93**: `primCursorLocPut:` - Set cursor position
```csharp
public static void PrimCursorLocPut(IPrimitiveContext ctx, SmalltalkOOP pointOOP)
{
    int x = GetPointX(pointOOP);
    int y = GetPointY(pointOOP);

    if (_displayWindow != null)
    {
        Point screenPos = _displayWindow.PointToScreen(new Point(x, y));
        Cursor.Position = screenPos;
    }
}
```

**Primitive 94**: `primMouseButtons` - Get button state
```csharp
public static SmalltalkOOP PrimMouseButtons(IPrimitiveContext ctx)
{
    int buttons = 0;
    if (Control.MouseButtons.HasFlag(MouseButtons.Left))
        buttons |= 1;  // Red button
    if (Control.MouseButtons.HasFlag(MouseButtons.Middle))
        buttons |= 2;  // Yellow button
    if (Control.MouseButtons.HasFlag(MouseButtons.Right))
        buttons |= 4;  // Blue button

    return SmalltalkOOP.FromSmallInteger(buttons);
}
```

### 7.3 Keyboard Primitives

**Primitive 95**: `primKbdNext` - Get next keyboard event
```csharp
private static Queue<char> _keyboardBuffer = new();

public static SmalltalkOOP PrimKbdNext(IPrimitiveContext ctx)
{
    if (_keyboardBuffer.Count > 0)
    {
        char ch = _keyboardBuffer.Dequeue();
        return AllocateCharacter(ch);
    }

    return SpecialObjectsArray.Instance.Nil;
}
```

**Primitive 96**: `primKbdPeek` - Peek at next key (don't remove)
```csharp
public static SmalltalkOOP PrimKbdPeek(IPrimitiveContext ctx)
{
    if (_keyboardBuffer.Count > 0)
    {
        char ch = _keyboardBuffer.Peek();
        return AllocateCharacter(ch);
    }

    return SpecialObjectsArray.Instance.Nil;
}
```

### 7.4 Event Routing

**Windows Event Handlers**:
```csharp
protected override void OnKeyPress(KeyPressEventArgs e)
{
    _keyboardBuffer.Enqueue(e.KeyChar);
    base.OnKeyPress(e);
}

protected override void OnMouseDown(MouseEventArgs e)
{
    // Translate to Smalltalk event
    var mouseEvent = CreateSmalltalkMouseEvent(e);

    // Queue for Smalltalk processing
    _eventQueue.Enqueue(mouseEvent);

    base.OnMouseDown(e);
}
```

---

## Phase 8: Complete UI Stack

**Goal**: All Smalltalk UI features working.

**Status**: 🔴 NOT STARTED

### 8.1 Cursor Management

**Primitive 97**: `primCursorLink:` - Change cursor shape

```csharp
private static Dictionary<int, Cursor> _cursorMap = new()
{
    [0] = Cursors.Arrow,      // Normal arrow
    [1] = Cursors.IBeam,      // Text insertion
    [2] = Cursors.Cross,      // Crosshair
    [3] = Cursors.Hand,       // Hand/point
    [4] = Cursors.Wait,       // Hourglass
    [5] = Cursors.SizeNWSE,   // Resize diagonal
    [6] = Cursors.SizeNS,     // Resize vertical
    [7] = Cursors.SizeWE,     // Resize horizontal
};

public static void PrimCursorLink(IPrimitiveContext ctx, SmalltalkOOP cursorOOP)
{
    // Extract cursor shape index
    var shapeOOP = GetInstanceVariable(cursorOOP, "shape");
    int shapeIndex = shapeOOP.AsSmallInteger();

    if (_cursorMap.TryGetValue(shapeIndex, out var cursor))
    {
        Cursor.Current = cursor;
    }
}
```

### 8.2 Menu System

**PopUpMenu Rendering**:
```csharp
public static void RenderPopUpMenu(SmalltalkOOP popUpMenuOOP, Point location)
{
    // Extract menu labels
    var labelsOOP = GetInstanceVariable(popUpMenuOOP, "labels");
    string[] labels = ExtractStringArray(labelsOOP);

    // Create Windows ContextMenuStrip
    var menu = new ContextMenuStrip();
    foreach (var label in labels)
    {
        var item = new ToolStripMenuItem(label);
        item.Click += (s, e) => OnMenuItemSelected(popUpMenuOOP, label);
        menu.Items.Add(item);
    }

    // Show at location
    menu.Show(_displayWindow, location);
}
```

### 8.3 Dialog Boxes

**Smalltalk Dialogs → Windows Dialogs**:

```csharp
// FillInTheBlank → InputBox
public static SmalltalkOOP ShowFillInTheBlank(string prompt, string defaultAnswer)
{
    string result = Microsoft.VisualBasic.Interaction.InputBox(
        prompt, "Smalltalk-80", defaultAnswer);

    return AllocateString(result);
}

// Confirm → MessageBox
public static SmalltalkOOP ShowConfirm(string message)
{
    var result = MessageBox.Show(message, "Confirm",
        MessageBoxButtons.YesNo, MessageBoxIcon.Question);

    return result == DialogResult.Yes
        ? SpecialObjectsArray.Instance.True
        : SpecialObjectsArray.Instance.False;
}
```

### 8.4 Scrollbars

**View Scrollbars → Windows Scrollbars**:

```csharp
public class SmalltalkScrollableView : Panel
{
    public SmalltalkOOP ViewOOP { get; set; }

    public SmalltalkScrollableView()
    {
        AutoScroll = true;

        HScroll += OnHorizontalScroll;
        VScroll += OnVerticalScroll;
    }

    private void OnVerticalScroll(object sender, ScrollEventArgs e)
    {
        // Update Smalltalk View's displayTransform
        var transformOOP = GetInstanceVariable(ViewOOP, "displayTransform");
        SetTransformY(transformOOP, -e.NewValue);

        // Trigger repaint
        Invalidate();
    }
}
```

---

## Testing Strategy

### Unit Tests

**Phase 1 Tests**: Font Infrastructure
```csharp
[Test]
public void DefaultTextStyle_ExistsAfterColdStart()
{
    var defaultTextStyleOOP = GlobalRegistry.Get("DefaultTextStyle");
    Assert.NotEqual(SmalltalkOOP.Nil, defaultTextStyleOOP);
}

[Test]
public void DefaultTextStyle_HasValidFont()
{
    var textStyleOOP = GlobalRegistry.Get("DefaultTextStyle");
    var fontArrayOOP = GetInstanceVariable(textStyleOOP, "fontArray");
    var fontOOP = GetIndexedOOP(fontArrayOOP, 0);

    Assert.True(IsInstanceOf(fontOOP, "StrikeFont"));
}
```

**Phase 3 Tests**: BitBlt Operations
```csharp
[Test]
public void BitBlt_CopyRule_ProducesCorrectBitmap()
{
    // Create source and dest Forms
    var sourceForm = AllocateForm(100, 100);
    var destForm = AllocateForm(200, 200);

    // Fill source with pattern
    FillFormWithPattern(sourceForm);

    // Perform BitBlt
    var bitBlt = AllocateBitBlt(sourceForm, destForm, rule: 3);
    PerformBitBlt(bitBlt);

    // Verify destination has correct pixels
    AssertFormMatchesExpected(destForm, expectedBitmap);
}
```

### Integration Tests

**End-to-End UI Tests**:
```csharp
[Test]
public void PopUpMenu_RendersAndRespondsToMouse()
{
    // Initialize UI system
    InitializeDisplay();

    // Create PopUpMenu from Smalltalk
    ExecuteSmalltalk(@"
        PopUpMenu labels: 'One\Two\Three' lines: #(2)
    ");

    // Simulate mouse click on menu item
    SimulateMouseClick(50, 30); // Click "Two"

    // Verify selection
    var result = GetMenuSelection();
    Assert.Equal(2, result);
}
```

### Visual Regression Tests

**Screenshot Comparison**:
```csharp
[Test]
public void TextRendering_MatchesReferenceImage()
{
    // Render Smalltalk text
    var bitmap = RenderSmalltalkCode(@"
        'Hello, World!' displayAt: 100@100
    ");

    // Compare to reference
    var reference = LoadReferenceImage("hello-world.png");
    Assert.ImagesEqual(bitmap, reference, tolerance: 0.01);
}
```

---

## Performance Considerations

### 1. **Display Update Batching**

**Problem**: Calling Display update after every BitBlt is slow.

**Solution**: Batch updates
```csharp
DisplayManager.BeginBatchUpdate();
try
{
    // Perform many BitBlts
    for (int i = 0; i < 1000; i++)
        PerformBitBlt(bitBlts[i]);
}
finally
{
    DisplayManager.EndBatchUpdate(); // Single screen update
}
```

### 2. **Font Glyph Caching**

**Problem**: Creating Windows Font objects is expensive.

**Solution**: Cache per StrikeFont OOP
```csharp
private static Dictionary<SmalltalkOOP, Font> _fontCache = new();

public static Font GetCachedFont(SmalltalkOOP strikeFontOOP)
{
    if (!_fontCache.TryGetValue(strikeFontOOP, out var font))
    {
        font = FontBridge.GetWindowsFont(strikeFontOOP);
        _fontCache[strikeFontOOP] = font;
    }
    return font;
}
```

### 3. **Dirty Rectangle Tracking**

**Problem**: Repainting entire Display is slow.

**Solution**: Track dirty regions
```csharp
class DisplayManager
{
    private List<Rectangle> _dirtyRegions = new();

    public static void MarkDirty(Rectangle rect)
    {
        _dirtyRegions.Add(rect);
    }

    public static void FlushToScreen()
    {
        // Union all dirty rects
        var unionRect = _dirtyRegions.Aggregate(Rectangle.Union);

        // Copy only dirty region to screen
        using (var graphics = _displayWindow.CreateGraphics())
        {
            graphics.DrawImage(_displayBitmap, unionRect, unionRect, GraphicsUnit.Pixel);
        }

        _dirtyRegions.Clear();
    }
}
```

### 4. **GPU Acceleration** (Future)

**WPF WriteableBitmap** for GPU-accelerated rendering:
```csharp
WriteableBitmap displayBitmap = new WriteableBitmap(1024, 768, 96, 96,
    PixelFormats.Bgr32, null);

// Direct pixel access
displayBitmap.Lock();
try
{
    unsafe
    {
        uint* pixels = (uint*)displayBitmap.BackBuffer;
        // Fast pixel manipulation
    }
}
finally
{
    displayBitmap.Unlock();
}
```

---

## Implementation Priority

### Critical Path (Phases 1-3)

**Week 1**: Phase 1 - Minimal Font Infrastructure
- Create DefaultTextStyle during cold-start
- Enable PopUpMenu instantiation
- Get init expressions passing

**Week 2-3**: Phase 2 - Display Primitive Foundation
- Create Display object
- Implement primitive 90-96 (display primitives)
- Create Windows display window
- Show first pixels on screen

**Week 4-6**: Phase 3 - BitBlt Graphics Engine
- Implement BitBlt primitive (primitive 96)
- Support all 16 combination rules
- Form ↔ Bitmap synchronization
- Basic graphics working

### Extended Features (Phases 4-8)

**Month 2**: Phases 4-5 - Text and Windows Integration
- Text rendering with Windows fonts
- Window management
- Multiple Smalltalk windows

**Month 3**: Phases 6-7 - Input and Interaction
- Mouse input primitives
- Keyboard input primitives
- Event routing

**Month 4**: Phase 8 - Complete UI Polish
- Menus, dialogs, scrollbars
- Cursor management
- Performance optimization

---

## Success Criteria

### Phase 1 Complete
- ✅ All init expressions pass (45/45)
- ✅ No font-related errors during cold-start
- ✅ PopUpMenu instances created successfully

### Phase 3 Complete
- ✅ Display window appears on screen
- ✅ Smalltalk graphics operations render correctly
- ✅ BitBlt operations produce expected results

### Phase 8 Complete
- ✅ Full Smalltalk-80 workspace visible
- ✅ Mouse and keyboard input working
- ✅ Can browse and edit Smalltalk code
- ✅ All UI elements (menus, dialogs, scrollbars) functional

---

## Appendix A: Smalltalk-80 Graphics Primitives

**Complete List** (from Blue Book):

| Primitive | Selector | Purpose |
|-----------|----------|---------|
| 90 | beDisplay | Make this Form the Display |
| 91 | primShowDisplay | Force screen update |
| 92 | primMousePoint | Get mouse position |
| 93 | primCursorLocPut: | Set cursor position |
| 94 | primMouseButtons | Get button state |
| 95 | primKbdNext | Get next keyboard char |
| 96 | primKbdPeek | Peek at next key |
| 97 | primCursorLink: | Change cursor shape |
| 98 | primTimeWordsInto: | Get system time |
| 99 | primTickWordsInto: | Get tick count |

**BitBlt** (Primitive 96):
- Handles ALL graphics operations
- 16 combination rules (raster ops)
- Clipping, patterns, masks

---

## Appendix B: Font File Formats (Future)

**If we later want to load Smalltalk bitmap fonts**:

### Strike Font File Format
```
Header (6 words):
  ascent, descent, maxWidth, minAscii, maxAscii, xTable

Glyph Bitmap:
  Rows of 16-bit words
  One row per scanline
  Width = maxWidth * (maxAscii - minAscii + 1)
  Height = ascent + descent

Character Position Table (xTable):
  One entry per character
  Offset into bitmap for character start
```

**Loading**:
```csharp
public static SmalltalkOOP LoadStrikeFontFromFile(string path)
{
    using (var stream = File.OpenRead(path))
    using (var reader = new BinaryReader(stream))
    {
        // Read header
        int ascent = reader.ReadUInt16();
        int descent = reader.ReadUInt16();
        int maxWidth = reader.ReadUInt16();
        int minAscii = reader.ReadUInt16();
        int maxAscii = reader.ReadUInt16();
        int xTableSize = reader.ReadUInt16();

        // Read glyph bitmap
        int bitmapSize = maxWidth * (ascent + descent);
        ushort[] bitmap = new ushort[bitmapSize];
        for (int i = 0; i < bitmapSize; i++)
            bitmap[i] = reader.ReadUInt16();

        // Read xTable
        ushort[] xTable = new ushort[xTableSize];
        for (int i = 0; i < xTableSize; i++)
            xTable[i] = reader.ReadUInt16();

        // Create StrikeFont object
        return AllocateStrikeFont(ascent, descent, maxWidth, minAscii, maxAscii, bitmap, xTable);
    }
}
```

---

## Appendix C: Windows → Smalltalk Coordinate Mapping

**Coordinate Systems**:

```
Smalltalk-80:
  Origin: Top-left (0@0)
  Y-axis: Down (positive = down)
  Units: Pixels

Windows:
  Origin: Top-left (0, 0)
  Y-axis: Down (positive = down)
  Units: Pixels

Result: IDENTICAL - No conversion needed!
```

**Window Coordinates**:
```csharp
// Screen → Client conversion
Point screenPos = new Point(100, 200);
Point clientPos = window.PointToClient(screenPos);

// Client → Screen conversion
Point clientPos = new Point(50, 75);
Point screenPos = window.PointToScreen(clientPos);
```

---

## Document Version History

- **v1.0** (2025-11-04): Initial architecture specification
- Created during Phase 0 → Phase 1 implementation
- Covers complete roadmap from headless interpreter to full UI

