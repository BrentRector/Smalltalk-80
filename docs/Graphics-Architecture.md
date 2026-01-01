# Smalltalk-80 Graphics System Architecture

**Last Updated**: 2026-01-01
**Status**: Phases 1-6 Complete - Full 1bpp graphics system operational

---

## Overview

This document describes the Smalltalk-80 graphics system implementation in our C# interpreter. The system provides:

- Complete BitBlt engine with all 16 combination rules
- Unified display abstraction (IDisplay) for pluggable backends
- Current: WinForms software rendering (1bpp internal, 32bpp display)
- Future: GPU-accelerated backends via Silk.NET

---

## 1. Smalltalk-80 Graphics Model

The Smalltalk-80 graphics architecture is built entirely on a single primitive: **BitBlt** (Bit Block Transfer).

### Core Classes

| Class | Purpose |
|-------|---------|
| **Form** | Bitmap image (width, height, bits array, offset) |
| **DisplayScreen** | The display device (subclass of Form) |
| **BitBlt** | Bitmap block transfer engine |
| **Cursor** | Mouse cursor (16x16 bitmap with hotspot) |
| **Pen** | Drawing tool built on BitBlt |
| **DisplayText** | Text rendering with strike fonts |

### Primitive 96 (copyBits)

The sole graphics primitive - all other graphics operations are built on it.

**Parameters:**
```smalltalk
destForm        "Destination bitmap"
sourceForm      "Source bitmap (may be nil)"
halftoneForm    "Pattern/mask for operations"
combinationRule "Operation mode (0-15)"
destX, destY    "Destination coordinates"
width, height   "Operation dimensions"
sourceX, sourceY "Source coordinates"
clipX, clipY, clipWidth, clipHeight "Clipping rectangle"
```

### 16 Combination Rules

| Rule | Name | Operation | Use Case |
|------|------|-----------|----------|
| 0 | Clear | 0 | Clear destination |
| 1 | And | src AND dst | Masking |
| 2 | AndReverse | src AND (NOT dst) | Erase pattern |
| 3 | Copy | src | Normal copy |
| 4 | AndInverted | (NOT src) AND dst | Reverse erase |
| 5 | Noop | dst | No operation |
| 6 | Xor | src XOR dst | Selection highlighting |
| 7 | Or | src OR dst | Paint over |
| 8 | Nor | NOT (src OR dst) | |
| 9 | Equiv | NOT (src XOR dst) | |
| 10 | Invert | NOT dst | |
| 11 | OrReverse | src OR (NOT dst) | |
| 12 | CopyInverted | NOT src | |
| 13 | OrInverted | (NOT src) OR dst | |
| 14 | Nand | NOT (src AND dst) | |
| 15 | Set | 1 | Fill with black |

### Display Characteristics

- **Bit Depth**: 1-bit monochrome (original Smalltalk-80)
- **Coordinate System**: Origin at top-left, Y increases downward
- **Color Model**: 0 = white, 1 = black

### Input Primitives

| Primitive | Name | Purpose |
|-----------|------|---------|
| 90 | mousePoint | Returns Point with mouse coordinates |
| 91 | cursorLocPut: | Set cursor position |
| 93 | inputSemaphore: | Set event notification semaphore |
| 95 | inputWord | Read encoded input event |
| 101 | beCursor | Set hardware cursor |
| 202 | bitState | Atomic mouse button state for polling |

---

## 2. Current Architecture

### System Stack

```
+-------------------------------------------------------------------+
|           Smalltalk-80 Code (Form, BitBlt, DisplayText)           |
+-------------------------------------------------------------------+
|      Graphics Primitives (90-102) - GraphicsPrimitives.cs         |
+-------------------------------------------------------------------+
|         BitBltEngine (Pure C# - All 16 rules, 1bpp)               |
+-------------------------------------------------------------------+
|              IDisplay (Unified abstraction)                       |
+-------------------------------------------------------------------+
|         WinFormsDisplay (Windows Forms + GDI)                     |
+-------------------------------------------------------------------+
|          1bpp -> 32bpp conversion for screen                      |
+-------------------------------------------------------------------+
```

### Key Design: Unified IDisplay Interface

Window management, rendering, and input are unified in a single `IDisplay` interface. This design enables future GPU backends where these concerns are inseparable.

```csharp
public interface IDisplay : IDisposable
{
    // Window
    int Width { get; }
    int Height { get; }
    PixelDepth BitsPerPixel { get; }
    void Show();
    void Resize(int width, int height);

    // Rendering
    void BitBlt(Span<byte> dest, ..., int combinationRule);
    void InvalidateRegion(int x, int y, int width, int height);

    // Input (atomic for thread-safety)
    (int X, int Y) MousePosition { get; }
    int ButtonState { get; }
    int GetInputWord();

    // Event loop
    WaitHandle? EventAvailableHandle { get; }
    void ProcessEvents();
    void RunEventLoop();
}
```

### File Organization

```
Smalltalk80.Interpreter/
├── Core/VM/Graphics/
│   ├── BitBltEngine.cs           # 1bpp BitBlt engine (all 16 rules)
│   ├── IFontBridge.cs            # Font abstraction
│   ├── WinFormsFontBridge.cs     # TrueType fonts
│   └── GdiBitmapFontBridge.cs    # Bitmap fonts (Fixedsys)
│
├── Core/VM/Platform/
│   └── IDisplay.cs               # Unified display interface
│
├── Platform/
│   ├── DisplaySystemFactory.cs   # Display factory
│   └── Windows/
│       ├── WinFormsDisplay.cs    # WinForms implementation
│       └── WinFormsDisplaySystem.cs
│
├── Core/VM/Primitives/
│   ├── GraphicsPrimitives.cs     # BitBlt, cursor, display
│   ├── InputPrimitives.cs        # Mouse, keyboard
│   └── TestDisplay.cs            # Test/headless implementation
```

### Threading Model

```
+--------------------------------------------------------------------+
|                     UI Thread (WinForms)                           |
|                                                                    |
|  - Runs Windows message loop                                       |
|  - Updates atomic mouse position/buttons                           |
|  - Enqueues input events to ring buffer                            |
|  - Signals EventAvailableHandle                                    |
|  - Renders 32bpp back buffer to screen                             |
+--------------------------------------------------------------------+
                              |
                              | WaitHandle.WaitAny([EventHandle, TimerHandle])
                              v
+--------------------------------------------------------------------+
|                     Smalltalk Thread                               |
|                                                                    |
|  - Executes bytecodes                                              |
|  - Polls MousePosition, ButtonState (atomic reads)                 |
|  - Reads events via GetInputWord()                                 |
|  - Executes BitBlt → modifies 1bpp heap buffers                    |
|  - Calls InvalidateRegion() to trigger screen refresh              |
+--------------------------------------------------------------------+
```

---

## 3. Implementation Status

### Phase 1: BitBlt Engine - COMPLETE

- `BitBltEngine.cs` - Pure C# with unsafe code for performance
- All 16 combination rules implemented and verified
- Clipping, halftone patterns, nil source handling
- MSB-first bit packing (Smalltalk convention)

### Phase 2: Display Integration - COMPLETE

- `WinFormsDisplay.cs` - Windows Forms backend
- Thread-safe synchronization (Smalltalk thread -> UI thread)
- 1bpp -> 32bpp conversion for screen rendering only
- Dirty rectangle optimization via InvalidateRegion()
- Double buffering for flicker-free updates

### Phase 3: Graphics Primitives - COMPLETE

- Primitive 96 (copyBits) - Full BitBlt implementation
- Form parameter extraction from Smalltalk heap objects
- Display invalidation triggers UI refresh

### Phase 4: Input & Additional Primitives - COMPLETE

- Primitive 90 (mousePoint), 91 (cursorLocPut:)
- Primitive 93 (inputSemaphore:), 95 (inputWord)
- Primitive 101 (beCursor), 102 (beDisplay)
- Primitive 104 (drawLoopX:Y:) - Bresenham line drawing, 3.6x faster
- Primitive 202 (bitState) - Atomic button state for tight loops

### Phase 5: Font Support - COMPLETE

- `IFontBridge` interface for platform-agnostic fonts
- `WinFormsFontBridge` - GDI+ TrueType rendering
- `GdiBitmapFontBridge` - Native GDI bitmap fonts (Fixedsys)
- Underscore -> left-arrow conversion for assignment operator
- Currently using bitmap fonts for clean 1bpp rendering

### Phase 6: Unified IDisplay - COMPLETE

Consolidated to single `IDisplay` interface:
- Removed separate `IPlatformBridge` interface
- All primitives use `context.Display`
- `TestDisplay` for headless testing
- Clean separation enables future GPU backends

---

## 4. Architecture Strategy: Depth Modes

**Design Decision**: The system operates in either full 1bpp mode OR full 32bpp mode, with no runtime conversion between them.

### Current: 1bpp Mode (Classic Smalltalk-80)

- All Forms are 1bpp internally
- BitBltEngine operates on 1bpp buffers
- WinFormsDisplay converts 1bpp -> 32bpp only for final screen rendering
- Matches original Blue Book behavior exactly

### Future: 32bpp Mode (Requires Smalltalk Changes)

For full color support, the Smalltalk side needs changes:

1. **Form depth instance variable** - Forms track their bit depth
2. **Color class** - ARGB color values
3. **Primitive depth dispatch** - BitBlt routes to appropriate engine
4. **Widget updates** - Pens, fills use Color instead of halftones

This is a significant Smalltalk-level change, not just a C# change.

---

## 5. Future: GPU Backends

The `IDisplay` abstraction enables GPU-accelerated rendering.

### Why Unified Interface?

GPU graphics libraries (OpenGL, Vulkan, DirectX) require owning:
- Window/surface creation (GPU context tied to window)
- Rendering operations (GPU resource management)
- Input events (received from their window)

You cannot use WinForms for the window and OpenGL for rendering without awkward bridging.

### Recommended: Silk.NET

| Feature | Silk.NET |
|---------|----------|
| Windowing | GLFW or SDL |
| Input | Integrated |
| 2D Rendering | Via OpenGL/Vulkan |
| GPU Acceleration | Yes |
| Cross-Platform | Yes |
| C# Support | Official .NET Foundation |

### Implementation Path

1. Create `SilkNetDisplaySystem : IDisplaySystem`
2. Create `SilkNetDisplay : IDisplay`
3. Implement BitBlt using GPU shaders or texture operations
4. No changes to primitives or Smalltalk code

---

## 6. Future: Native 32bpp and Color Support

This section documents the Smalltalk-side changes required for full 32bpp color support. We support only two depths: **1bpp** (classic monochrome) and **32bpp** (full color with alpha). This simplification eliminates indexed colors, ColorForm, and complex depth conversion matrices.

### 6.1 Design Constraints

**Supported Depths: 1 and 32 only**

| Depth | Use Case | Pixels/Word | Format |
|-------|----------|-------------|--------|
| 1 | Classic Smalltalk-80, cursors, fonts | 32 | MSB-first bits |
| 32 | Full color graphics | 1 | ARGB (8-8-8-8) |

**Not Supported:** 2, 4, 8, 16 bit depths. No ColorForm. No indexed palettes.

**Reference:** Squeak/Pharo support all depths, but we adopt only the subset needed for our use case.

Sources:
- [Squeak Color Class](https://bildungsgueter.de/Smalltalk/Pages/MVCTutorial/Pages/Color.htm)
- [Squeak BitBlt Tutorial](https://www.bildungsgueter.de/Smalltalk/Pages/MVCTutorial/Pages/BitBlt.htm)

### 6.2 Color Class

New class for RGB color values. Stores RGB as 10-bits per component (Squeak convention).

```smalltalk
Object subclass: #Color
    instanceVariableNames: 'rgb'
    classVariableNames: 'Black White Transparent'
    poolDictionaries: ''
    category: 'Graphics-Primitives'
```

**Instance Variables:**
- `rgb` - Single integer: R (bits 20-29), G (bits 10-19), B (bits 0-9)

**Key Methods:**

```smalltalk
Color class>>r: r g: g b: b
    "Create color from 0.0-1.0 float components"
    ^self basicNew setRed: r green: g blue: b

Color class>>fromInteger: argbInteger
    "Create from 32-bit ARGB pixel value"
    ^self basicNew
        setRed: ((argbInteger >> 16) bitAnd: 255) / 255.0
        green: ((argbInteger >> 8) bitAnd: 255) / 255.0
        blue: (argbInteger bitAnd: 255) / 255.0

Color>>red
    ^(rgb bitShift: -20) / 1023.0

Color>>green
    ^((rgb bitShift: -10) bitAnd: 1023) / 1023.0

Color>>blue
    ^(rgb bitAnd: 1023) / 1023.0

Color>>asPixelValue
    "Answer 32-bit ARGB pixel value (alpha = 255)"
    ^16rFF000000 +                          "Alpha = 255 (opaque)"
     ((self red * 255) rounded << 16) +     "Red"
     ((self green * 255) rounded << 8) +    "Green"
     (self blue * 255) rounded              "Blue"

Color>>asFormColor
    "For 1bpp: answer black or white based on luminance"
    | luminance |
    luminance := (self red * 0.299) + (self green * 0.587) + (self blue * 0.114).
    ^luminance < 0.5 ifTrue: [1] ifFalse: [0]
```

**Class Initialization:**

```smalltalk
Color class>>initialize
    Black := self r: 0.0 g: 0.0 b: 0.0.
    White := self r: 1.0 g: 1.0 b: 1.0.
    Transparent := TranslucentColor new setRed: 0.0 green: 0.0 blue: 0.0; alpha: 0.0
```

### 6.3 TranslucentColor Class

Subclass for alpha blending support (32bpp only).

```smalltalk
Color subclass: #TranslucentColor
    instanceVariableNames: 'alpha'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Graphics-Primitives'
```

**Key Methods:**

```smalltalk
Color>>alpha: alphaValue
    "Answer a TranslucentColor with given alpha (0.0-1.0)"
    ^TranslucentColor new
        setRed: self red green: self green blue: self blue;
        alpha: alphaValue

TranslucentColor>>alpha
    ^alpha

TranslucentColor>>asPixelValue
    "32-bit ARGB with alpha"
    ^((alpha * 255) rounded << 24) +
     ((self red * 255) rounded << 16) +
     ((self green * 255) rounded << 8) +
     (self blue * 255) rounded
```

### 6.4 Form Class Changes

Add `depth` instance variable. Only values 1 and 32 are valid.

```smalltalk
DisplayMedium subclass: #Form
    instanceVariableNames: 'bits width height offset depth'
    classVariableNames: 'OneIsBlack'
    poolDictionaries: ''
    category: 'Graphics-Display Objects'
```

**Key Methods:**

```smalltalk
Form class>>extent: extentPoint
    "Create 1bpp Form (backward compatible)"
    ^self extent: extentPoint depth: 1

Form class>>extent: extentPoint depth: bitsPerPixel
    "Create Form with specified depth (1 or 32 only)"
    (bitsPerPixel = 1 or: [bitsPerPixel = 32])
        ifFalse: [self error: 'Only depths 1 and 32 supported'].
    ^self new setExtent: extentPoint depth: bitsPerPixel

Form>>setExtent: extentPoint depth: bitsPerPixel
    width := extentPoint x.
    height := extentPoint y.
    depth := bitsPerPixel.
    offset := 0@0.
    bits := WordArray new: self wordsPerRow * height

Form>>wordsPerRow
    "Words needed per row"
    depth = 32 ifTrue: [^width].        "1 word per pixel"
    ^(width + 31) // 32                  "1bpp: 32 pixels per word"

Form>>depth
    "Default 1 for backward compatibility with existing Forms"
    ^depth ifNil: [1]

Form>>is32bpp
    ^self depth = 32

Form>>pixelAt: aPoint
    "Answer pixel value at point"
    self is32bpp
        ifTrue: [^self pixelAt32: aPoint]
        ifFalse: [^self pixelAt1: aPoint]

Form>>pixelAt: aPoint put: pixelValue
    "Set pixel value at point"
    self is32bpp
        ifTrue: [self pixelAt32: aPoint put: pixelValue]
        ifFalse: [self pixelAt1: aPoint put: (pixelValue bitAnd: 1)]
```

### 6.5 Extended BitBlt Combination Rules

For 32bpp, we support additional rules beyond the original 16:

| Rule | Name | Operation | Notes |
|------|------|-----------|-------|
| 0-15 | (original) | Boolean ops | Work on all depths |
| 24 | alphaBlend | Alpha composite | 32bpp only |
| 25 | pixPaint | Replace where src ≠ 0 | Transparency |
| 26 | pixMask | Zero where src ≠ 0 | Inverse mask |
| 27 | rgbMax | Max of RGB components | Lighten |
| 28 | rgbMin | Min of RGB components | Darken |

Rules 20-23, 29-34 from Squeak are not implemented initially.

### 6.6 Primitive 96 Depth Dispatch

The C# primitive dispatches based on source and dest Form depths:

```csharp
// In GraphicsPrimitives.cs
private void CopyBitsWithDepth(int srcDepth, int dstDepth, ...)
{
    (srcDepth, dstDepth) switch
    {
        (1, 1)   => BitBltEngine.CopyBits(...),       // Current engine
        (32, 32) => BitBltEngine32.CopyBits32(...),   // 32bpp engine
        (1, 32)  => BitBltEngine32.Expand1To32(...),  // Mono to color
        (32, 1)  => BitBltEngine32.Reduce32To1(...),  // Color to mono
        _ => throw new PrimitiveFailed("Unsupported depth combination")
    };
}
```

**Depth Conversion Rules:**
- **1→32**: Black (1) → `0xFF000000`, White (0) → `0xFFFFFFFF`
- **32→1**: Luminance threshold at 50%

### 6.7 Implementation Phases

| Phase | Scope | Complexity | Deliverable |
|-------|-------|------------|-------------|
| **1** | Color class | Low | Color, TranslucentColor classes |
| **2** | Form depth | Medium | Form with depth ivar, 1/32 validation |
| **3** | BitBltEngine32 | High | 32bpp BitBlt with rules 0-15 |
| **4** | Depth dispatch | Medium | Primitive 96 routes by depth |
| **5** | Depth conversion | Medium | 1↔32 conversion in BitBlt |
| **6** | Extended rules | Medium | Rules 24-28 for 32bpp |
| **7** | Widget updates | High | Pen, fills use Color |

### 6.8 Dependency Graph

```
Phase 1: Color ──────────────────────────────────┐
         │                                       │
         ▼                                       ▼
Phase 2: Form depth ──────┐              Phase 7: Widget updates
         │                │                      ▲
         ▼                ▼                      │
Phase 3: BitBltEngine32   Phase 5: Depth         │
         │                conversion             │
         ▼                │                      │
Phase 4: Depth dispatch ◄─┘                      │
         │                                       │
         ▼                                       │
Phase 6: Extended rules ─────────────────────────┘
```

### 6.9 Migration Strategy

**Recommended: Compatibility Mode**

1. `Form>>depth` returns 1 if nil → existing Forms work unchanged
2. Add Color class without breaking existing halftone usage
3. New code can create 32bpp Forms explicitly
4. Gradual widget migration to Color API

**Key Compatibility Points:**
- Cursors remain 1bpp (convert to 32bpp in primitive 101)
- StrikeFonts remain 1bpp (convert when rendering to 32bpp Form)
- Existing Form creation methods default to depth: 1

---

## References

1. Goldberg, A., & Robson, D. (1983). *Smalltalk-80: The Language and its Implementation* (Blue Book) - Chapter on BitBlt
2. Ingalls, D. (1981). *Design Principles Behind Smalltalk* - Graphics system design
3. [Silk.NET](https://github.com/dotnet/Silk.NET) - Official .NET Foundation graphics library
4. [Squeak BitBlt Tutorial](https://www.bildungsgueter.de/Smalltalk/Pages/MVCTutorial/Pages/BitBlt.htm) - Extended combination rules
5. [Squeak Color Class](https://bildungsgueter.de/Smalltalk/Pages/MVCTutorial/Pages/Color.htm) - Color implementation details
6. [Squeak ColorForm](http://bildungsgueter.de/Smalltalk/Pages/MVCTutorial/Pages/ColorForm.htm) - Indexed color forms
7. [Pharo Documentation](https://pharo.org/documentation.html) - Modern Smalltalk reference
8. [Cuis Smalltalk](https://cuis.st) - Minimalist Smalltalk with vector graphics
