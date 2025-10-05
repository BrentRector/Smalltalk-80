# Smalltalk-80 Graphics System Architecture

## Overview

This document describes the architecture for implementing the Smalltalk-80 graphics system in our C# interpreter. The design is based on analysis of the Smalltalk-80.st system image and evaluation of modern .NET graphics frameworks.

## Requirements

### Smalltalk-80 Graphics Model

The Smalltalk-80 system has an elegant graphics architecture built entirely on a single primitive operation: **BitBlt** (Bit Block Transfer).

**Key Classes:**
- **Form** - Bitmap/raster image (width, height, bits array, offset)
- **DisplayScreen** - The actual display device (subclass of Form)
- **BitBlt** - The bitmap block transfer engine
- **Cursor** - Mouse cursor (16x16 bitmap with hotspot)
- **Pen** - Drawing tool built on BitBlt
- **DisplayText** - Text rendering with strike fonts

### Critical Primitive: 96 (copyBits)

Primitive 96 is the **sole graphics primitive** - all other graphics operations are built on top of it in Smalltalk code.

**BitBlt Parameters:**
```smalltalk
destForm       "Destination bitmap"
sourceForm     "Source bitmap"
halftoneForm   "Pattern/mask for operations"
combinationRule "Operation mode (0-15)"
destX, destY   "Destination coordinates"
width, height  "Operation dimensions"
sourceX, sourceY "Source coordinates"
clipX, clipY, clipWidth, clipHeight "Clipping rectangle"
```

**16 Combination Rules:**
| Rule | Name | Operation | Use Case |
|------|------|-----------|----------|
| 0 | clear | 0 | Clear destination |
| 1 | and | src AND dst | Masking |
| 2 | andReverse | src AND (NOT dst) | Erase pattern |
| 3 | copy | src | Normal copy |
| 4 | andInverted | (NOT src) AND dst | Reverse erase |
| 5 | noop | dst | No operation |
| 6 | xor | src XOR dst | Invert/toggle |
| 7 | or | src OR dst | Paint over |
| 8 | nor | NOT (src OR dst) | |
| 9 | equiv | NOT (src XOR dst) | |
| 10 | invert | NOT dst | |
| 11 | orReverse | src OR (NOT dst) | |
| 12 | copyInverted | NOT src | |
| 13 | orInverted | (NOT src) OR dst | |
| 14 | nand | NOT (src AND dst) | |
| 15 | set | 1 | Fill with white |

**Special Modes:**
- **Rule 3** with halftone: Pattern fill
- **Rule 7** with halftone: Paint with pattern
- **Clipping**: All operations respect clip rectangle

### Display Requirements

- **Bit Depth**: Originally 1-bit monochrome, flexible architecture
- **Resolution**: Dynamic (no fixed resolution)
- **Coordinate System**: Origin at top-left, Y increases downward
- **Color Model**: 0 = white, 1 = black (traditional monochrome)

### Input System

**Mouse:**
- Position tracking (X, Y coordinates)
- 3-button support (red, yellow, blue buttons)
- **Primitive 90**: mousePoint (returns Point)
- **Primitive 91**: cursorLocPut: (set cursor position)

**Cursor:**
- 16x16 monochrome bitmap
- Hotspot offset point
- **Primitive 101**: beCursor (set hardware cursor)

**Keyboard:**
- **Primitive 95**: inputWord (read encoded input event)
- **Primitive 93**: inputSemaphore: (set event notification)

## Architecture Design

### Layered Architecture

```
┌─────────────────────────────────────────────────┐
│         Smalltalk-80 Code (Form, BitBlt)        │
├─────────────────────────────────────────────────┤
│  Primitive 96 (copyBits) - C# Implementation    │
├─────────────────────────────────────────────────┤
│    BitBltEngine (Pure C# BitBlt Implementation) │
├─────────────────────────────────────────────────┤
│   Display Abstraction (IDisplayProvider)        │
├─────────────────────────────────────────────────┤
│       Windows Forms (Windowing & Rendering)     │
└─────────────────────────────────────────────────┘
```

**Technology Stack:**
- **BitBlt Engine**: Pure C# with unsafe code for performance
- **Display**: Windows Forms with System.Drawing.Bitmap (built into .NET)
- **Bitmap Format**: Format1bppIndexed for monochrome display
- **Platform**: Windows only (no cross-platform requirement)
- **Dependencies**: Zero - uses only built-in .NET libraries

### Component Breakdown

#### 1. BitBltEngine (Core Graphics)

**Responsibility:** Implement all 16 BitBlt combination rules in pure C#

**Key Design Decisions:**
- Use `unsafe` code for performance-critical pixel operations
- Support 1-bit, 8-bit, and 32-bit pixel formats
- Implement clipping at BitBlt level
- Use `Span<byte>` for zero-copy bitmap access
- Optional SIMD acceleration for bulk operations

**Interface:**
```csharp
public interface IBitBltEngine
{
    void CopyBits(
        BitBltOperation operation);
}

public class BitBltOperation
{
    public Memory<byte> DestBits { get; set; }
    public Memory<byte> SourceBits { get; set; }
    public Memory<byte>? HalftoneBits { get; set; }
    public int DestWidth { get; set; }
    public int DestHeight { get; set; }
    public int SourceWidth { get; set; }
    public int SourceHeight { get; set; }
    public int DestX { get; set; }
    public int DestY { get; set; }
    public int SourceX { get; set; }
    public int SourceY { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public Rectangle ClipRect { get; set; }
    public CombinationRule Rule { get; set; }
}

public enum CombinationRule
{
    Clear = 0,
    And = 1,
    AndReverse = 2,
    Copy = 3,
    AndInverted = 4,
    Noop = 5,
    Xor = 6,
    Or = 7,
    Nor = 8,
    Equiv = 9,
    Invert = 10,
    OrReverse = 11,
    CopyInverted = 12,
    OrInverted = 13,
    Nand = 14,
    Set = 15
}
```

#### 2. Display Abstraction

**Responsibility:** Abstract platform-specific display operations

**Interface:**
```csharp
public interface IDisplayProvider
{
    // Display buffer access
    Memory<byte> GetDisplayBits();
    int DisplayWidth { get; }
    int DisplayHeight { get; }
    int BitsPerPixel { get; }

    // Update dirty regions
    void InvalidateRect(Rectangle rect);
    void InvalidateAll();

    // Input events
    event EventHandler<MouseEventArgs> MouseMoved;
    event EventHandler<MouseButtonEventArgs> MouseButtonDown;
    event EventHandler<MouseButtonEventArgs> MouseButtonUp;
    event EventHandler<KeyEventArgs> KeyDown;
    event EventHandler<KeyEventArgs> KeyUp;

    // Cursor management
    void SetCursor(Memory<byte> cursorBits, int hotspotX, int hotspotY);
}
```

#### 3. Windows Forms Display Provider

**Responsibility:** Integrate with Windows Forms for windowing and rendering

**Key Features:**
- Form window hosting the display bitmap
- System.Drawing.Bitmap with Format1bppIndexed for monochrome
- PictureBox for efficient bitmap rendering
- Event translation from WinForms to Smalltalk input primitives
- Double buffering for flicker-free updates
- Dirty rectangle optimization

**Implementation:**
```csharp
public class WinFormsDisplayProvider : IDisplayProvider
{
    private byte[] _displayBits;
    private Bitmap _displayBitmap;  // Format1bppIndexed
    private Form _window;
    private PictureBox _pictureBox;

    public WinFormsDisplayProvider(int width, int height)
    {
        // Allocate display buffer
        _displayBits = new byte[(width * height + 7) / 8];

        // Create 1-bit monochrome bitmap
        _displayBitmap = new Bitmap(width, height,
            PixelFormat.Format1bppIndexed);

        // Create window with PictureBox
        _window = new Form
        {
            ClientSize = new Size(width, height),
            Text = "Smalltalk-80",
            FormBorderStyle = FormBorderStyle.FixedSingle,
            MaximizeBox = false
        };

        _pictureBox = new PictureBox
        {
            Image = _displayBitmap,
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Normal
        };

        _window.Controls.Add(_pictureBox);

        // Hook up events
        _pictureBox.MouseMove += OnMouseMove;
        _pictureBox.MouseDown += OnMouseDown;
        _pictureBox.MouseUp += OnMouseUp;
        _window.KeyDown += OnKeyDown;
        _window.KeyUp += OnKeyUp;
    }

    public void InvalidateRect(Rectangle rect)
    {
        // Copy _displayBits to _displayBitmap using LockBits
        UpdateBitmapFromBuffer(rect);
        _pictureBox.Invalidate(rect);
    }

    private unsafe void UpdateBitmapFromBuffer(Rectangle rect)
    {
        var bitmapData = _displayBitmap.LockBits(
            new System.Drawing.Rectangle(0, 0, _displayBitmap.Width, _displayBitmap.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format1bppIndexed);

        // Copy _displayBits to bitmapData.Scan0
        Marshal.Copy(_displayBits, 0, bitmapData.Scan0, _displayBits.Length);

        _displayBitmap.UnlockBits(bitmapData);
    }
}
```

#### 4. Primitive 96 Implementation

**Responsibility:** Bridge Smalltalk primitive call to BitBltEngine

**Key Responsibilities:**
- Extract BitBlt parameters from Smalltalk objects (Form, Rectangle)
- Convert object references to bitmap memory
- Invoke BitBltEngine with proper parameters
- Handle primitive failure cases

```csharp
public static class GraphicsPrimitives
{
    public static PrimitiveResult PrimitiveCopyBits(
        SObjectRef receiver,
        SObjectRef[] arguments,
        IPrimitiveContext context)
    {
        // 1. Extract BitBlt instance variables
        // 2. Get bitmap memory from Forms
        // 3. Build BitBltOperation
        // 4. Execute through BitBltEngine
        // 5. Return receiver on success
    }
}
```

## Implementation Strategy

### Phase 1: Core BitBlt Engine (Week 1)

**Goal:** Implement pure C# BitBlt engine with all 16 rules

**Tasks:**
1. Create BitBltEngine class with unsafe pixel operations
2. Implement all 16 combination rules for 1-bit monochrome
3. Add clipping support
4. Implement halftone pattern support
5. Unit test each combination rule extensively

**Deliverables:**
- BitBltEngine class with full BitBlt support
- Comprehensive unit tests for all rules
- Performance benchmarks

### Phase 2: Display Integration (Week 2)

**Goal:** Create Windows Forms-based display system

**Tasks:**
1. Create IDisplayProvider interface
2. Implement WinFormsDisplayProvider
3. Build Form window with PictureBox control
4. Implement bitmap update from display buffer (LockBits/UnlockBits)
5. Add dirty rectangle tracking

**Deliverables:**
- Working Windows Forms window showing Smalltalk display
- Mouse/keyboard event handling integrated
- Efficient bitmap update mechanism

### Phase 3: Primitive 96 Implementation (Week 3)

**Goal:** Complete primitive 96 (copyBits) and integrate with interpreter

**Tasks:**
1. Create GraphicsPrimitives.cs
2. Implement PrimitiveCopyBits
3. Extract Form bitmap data from ObjectMemory
4. Handle coordinate systems and clipping
5. Test with Smalltalk graphics code

**Deliverables:**
- Working primitive 96
- Smalltalk graphics operations functional
- Demo showing Form operations

### Phase 4: Additional Graphics Primitives (Week 3)

**Goal:** Implement remaining graphics primitives

**Tasks:**
1. Primitive 97 - snapshot (image save/load)
2. Primitive 98-99 - oopsLeft, coreLeft (memory stats)
3. Primitive 100 - timeWords (current time)
4. Primitives 101-104 - display control, beep

**Deliverables:**
- All graphics primitives implemented
- Basic Smalltalk UI operational
- Performance optimization

## Performance Considerations

### Critical Paths

1. **BitBlt operations** - Inner loop of all graphics
   - Use unsafe code for pixel manipulation
   - Consider SIMD for bulk operations (Vector256<byte>)
   - Optimize for common cases (copy, xor)

2. **Display updates** - Frequent operation
   - Dirty rectangle tracking to minimize redraws
   - Double buffering to prevent flicker
   - Batch updates when possible

3. **Event handling** - Must be responsive
   - Direct event routing without queuing overhead
   - Efficient coordinate transformation

### Memory Layout

**Bitmap Format:**
- Use byte arrays for 1-bit monochrome (8 pixels per byte)
- Pack bits left-to-right, MSB first (Smalltalk convention)
- Align rows to word boundaries for faster access

**Display Buffer:**
- Direct mapping to Smalltalk Display object bits
- No intermediate copying during BitBlt
- Shared memory between engine and display

## Testing Strategy

### Unit Tests

1. **BitBlt Rules** - Test each of 16 rules independently
2. **Clipping** - Test edge cases and boundary conditions
3. **Halftone** - Test pattern application
4. **Coordinates** - Test negative, zero, large values

### Integration Tests

1. **Smalltalk Graphics** - Run actual Smalltalk drawing code
2. **Display Updates** - Verify screen matches bitmap state
3. **Input Events** - Test mouse/keyboard integration
4. **Performance** - Benchmark BitBlt throughput

### Visual Tests

1. Create test Forms in Smalltalk
2. Draw patterns using BitBlt
3. Render text using strike fonts
4. Display cursors and icons

## Future Enhancements

### Short Term
- Support 8-bit grayscale for better visual quality
- Hardware cursor support (OS-level)
- Window resize handling

### Medium Term
- 32-bit color support for modern applications
- GPU acceleration for BitBlt operations
- Multi-monitor support

### Long Term
- OpenGL/Vulkan backend for 3D graphics
- Vector graphics support
- Modern anti-aliasing

## References

1. Goldberg, A., & Robson, D. (1983). *Smalltalk-80: The Language and its Implementation* (Blue Book) - Chapter on BitBlt
2. Ingalls, D. (1981). *Design Principles Behind Smalltalk* - Graphics system design
3. Avalonia Documentation: https://docs.avaloniaui.net/
4. SkiaSharp Documentation: https://docs.microsoft.com/en-us/xamarin/xamarin-forms/user-interface/graphics/skiasharp/

---

**Document Status**: Design Phase
**Last Updated**: 2025-10-04
**Next Steps**: Begin Phase 1 implementation of BitBltEngine
