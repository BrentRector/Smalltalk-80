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

This section documents the Smalltalk-side changes required for full 32bpp color support, based on research of Squeak and Pharo implementations.

### 6.1 Reference Implementations

Modern Smalltalk systems that support color graphics:

| System | Color Depths | Key Features |
|--------|--------------|--------------|
| [Squeak](https://squeak.org) | 1, 2, 4, 8, 16, 32 | Full color with alpha blending |
| [Pharo](https://pharo.org) | 1, 2, 4, 8, 16, 32 | Modern Morphic with 32bpp |
| [Cuis](https://cuis.st) | 1, 2, 4, 8, 16, 32 | Vector graphics focus |

**Recommendation**: Adopt Squeak's approach. It evolved directly from Smalltalk-80 and provides a well-tested migration path.

Sources:
- [Squeak BitBlt Tutorial](https://www.bildungsgueter.de/Smalltalk/Pages/MVCTutorial/Pages/BitBlt.htm)
- [Squeak Color Class](https://bildungsgueter.de/Smalltalk/Pages/MVCTutorial/Pages/Color.htm)
- [Squeak ColorForm Tutorial](http://bildungsgueter.de/Smalltalk/Pages/MVCTutorial/Pages/ColorForm.htm)

### 6.2 Proposed Color Class

Squeak's Color class stores RGB as 10-bits per component in a single integer, providing 1024 levels per channel while remaining compact.

```smalltalk
Object subclass: #Color
    instanceVariableNames: 'rgb cachedDepth cachedBitPattern'
    classVariableNames: 'Black White Gray Red Green Blue
                         IndexedColors ColorRegistry'
    poolDictionaries: ''
    category: 'Graphics-Primitives'
```

**Instance Variables:**
- `rgb` - Single integer encoding R, G, B each as 0-1023 (30 bits total)
- `cachedDepth` - Depth for which cachedBitPattern was computed
- `cachedBitPattern` - Cached pixel value for fast repeated use

**Key Methods:**

```smalltalk
Color class>>r: r g: g b: b
    "Create color from 0.0-1.0 float components"
    ^self basicNew setRed: r green: g blue: b

Color>>red
    "Answer red component as 0.0-1.0"
    ^(rgb bitShift: -20) / 1023.0

Color>>green
    ^((rgb bitShift: -10) bitAnd: 1023) / 1023.0

Color>>blue
    ^(rgb bitAnd: 1023) / 1023.0

Color>>pixelValueForDepth: depth
    "Answer pixel value for this color at given depth"
    depth = 32 ifTrue: [
        ^(255 << 24) +                           "Alpha = 255 (opaque)"
         ((self red * 255) rounded << 16) +      "Red"
         ((self green * 255) rounded << 8) +     "Green"
         (self blue * 255) rounded               "Blue"
    ].
    depth = 16 ifTrue: [
        ^((self red * 31) rounded << 10) +
         ((self green * 31) rounded << 5) +
         (self blue * 31) rounded
    ].
    "For 1-8 bit depths, use indexed color lookup"
    ^(IndexedColors findNearestTo: self) - 1
```

### 6.3 Proposed TranslucentColor Class

For alpha blending support:

```smalltalk
Color subclass: #TranslucentColor
    instanceVariableNames: 'alpha'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Graphics-Primitives'
```

**Instance Variables:**
- `alpha` - Float 0.0 (transparent) to 1.0 (opaque)

**Key Methods:**

```smalltalk
Color>>alpha: alphaValue
    "Answer a TranslucentColor with given alpha"
    ^TranslucentColor new
        setRed: self red green: self green blue: self blue;
        alpha: alphaValue

TranslucentColor>>pixelValueForDepth: depth
    "32-bit ARGB format"
    depth = 32 ifTrue: [
        ^((alpha * 255) rounded << 24) +
         ((self red * 255) rounded << 16) +
         ((self green * 255) rounded << 8) +
         (self blue * 255) rounded
    ].
    "Alpha not supported at lower depths"
    ^super pixelValueForDepth: depth
```

### 6.4 Form Class Changes

Current Blue Book Form has 4 instance variables. Add `depth`:

```smalltalk
DisplayMedium subclass: #Form
    instanceVariableNames: 'bits width height offset depth'
    classVariableNames: 'OneIsBlack'
    poolDictionaries: ''
    category: 'Graphics-Display Objects'
```

**Critical Method Changes:**

```smalltalk
Form class>>extent: extentPoint depth: bitsPerPixel
    "Create Form with specified depth"
    | newForm |
    newForm := self new.
    newForm setExtent: extentPoint depth: bitsPerPixel.
    ^newForm

Form>>setExtent: extentPoint depth: bitsPerPixel
    "Initialize with size and depth"
    width := extentPoint x.
    height := extentPoint y.
    depth := bitsPerPixel.
    offset := 0@0.
    bits := WordArray new: self wordsPerRow * height.

Form>>wordsPerRow
    "Answer words needed per row at current depth"
    depth = 32 ifTrue: [^width].           "1 word per pixel"
    depth = 16 ifTrue: [^(width + 1) // 2]. "2 pixels per word"
    depth = 8  ifTrue: [^(width + 3) // 4]. "4 pixels per word"
    depth = 4  ifTrue: [^(width + 7) // 8]. "8 pixels per word"
    depth = 2  ifTrue: [^(width + 15) // 16]. "16 pixels per word"
    ^(width + 31) // 32                     "1bpp: 32 pixels per word"

Form>>depth
    "Answer bits per pixel. Default 1 for compatibility."
    ^depth ifNil: [1]
```

### 6.5 ColorForm Class (Optional)

For indexed color (1-8 bit depths with palette):

```smalltalk
Form subclass: #ColorForm
    instanceVariableNames: 'colors cachedDepth cachedColormap'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Graphics-Display Objects'
```

**Instance Variables:**
- `colors` - Array of 2^depth Color objects (the palette)
- `cachedDepth` - Depth for which colormap was computed
- `cachedColormap` - Bitmap for depth conversion

ColorForm uses less memory than 32bpp for large images with limited colors.

### 6.6 Extended BitBlt Combination Rules

Squeak extends BitBlt beyond the original 16 rules for color operations:

| Rule | Name | Operation | Notes |
|------|------|-----------|-------|
| 20 | rgbAdd | Component-wise add | Clamps at 255 |
| 21 | rgbSub | Component-wise subtract | Clamps at 0 |
| 24 | alphaBlend | Alpha blending | 32-bit only |
| 25 | pixPaint | Replace where non-zero | Transparency |
| 26 | pixMask | Zero where non-zero | Inverse mask |
| 27 | rgbMax | Max of components | Lighten |
| 28 | rgbMin | Min of components | Darken |
| 30 | alphaBlendConst | Alpha with constant | 16-bit compatible |
| 34 | alphaBlendScaled | Scaled alpha blend | Pre-multiplied |

### 6.7 Primitive 96 Depth Dispatch

The C# primitive must dispatch based on Form depths:

```csharp
// In GraphicsPrimitives.cs
private static void CopyBitsDispatch(
    int sourceDepth, int destDepth,
    Span<byte> destBits, ReadOnlySpan<byte> sourceBits, ...)
{
    (sourceDepth, destDepth) switch
    {
        (1, 1)   => BitBltEngine.CopyBits(...),      // Current 1bpp
        (32, 32) => BitBltEngine32.CopyBits(...),   // 32bpp operations
        (1, 32)  => BitBltEngine32.Expand1To32(...), // Expand bits
        (32, 1)  => BitBltEngine32.Reduce32To1(...), // Threshold/dither
        (8, 32)  => BitBltEngine32.Expand8To32(...), // Indexed to RGB
        _        => throw new NotSupportedException(
                       $"Depth conversion {sourceDepth}->{destDepth}")
    };
}
```

### 6.8 Implementation Phases

| Phase | Scope | Complexity | Dependencies |
|-------|-------|------------|--------------|
| 1 | Color class | Low | None |
| 2 | Form depth ivar | Medium | Color class |
| 3 | DisplayScreen 32bpp | Low | Form depth |
| 4 | BitBltEngine32 | High | Form depth |
| 5 | TranslucentColor | Low | Color class |
| 6 | Extended rules (20+) | Medium | BitBltEngine32 |
| 7 | ColorForm (optional) | Medium | Color class |
| 8 | Widget updates | High | All above |

### 6.9 Migration Strategy

**Option A: Clean Break**
- Implement all changes at once
- Existing 1bpp image won't load
- Requires rebuilding from source

**Option B: Compatibility Mode**
- Form>>depth defaults to 1 if nil
- Existing methods work unchanged
- Gradually add color support
- **Recommended approach**

**Option C: Adopt Squeak Image**
- Port Squeak's graphics classes wholesale
- Maximum compatibility with Squeak code
- Significant integration work

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
