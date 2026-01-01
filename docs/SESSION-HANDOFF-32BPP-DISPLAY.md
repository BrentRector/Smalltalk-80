# Session Handoff: 32-bit Display Surface Migration

**Date**: 2026-01-01
**Branch**: `claude/standardize-assignment-operator-mLcex`
**Status**: Ready to resume

## Primary Documentation

**Read**: `docs/Graphics-Architecture.md` - Consolidated comprehensive graphics documentation including:
- Smalltalk-80 graphics model (BitBlt, Forms, primitives)
- Current architecture and file structure
- Unified IDisplay abstraction with GPU backend rationale
- Implementation status (Phases 1-5 complete)
- Future 32bpp migration plan

## What Was Accomplished This Session

### 1. Fixed 25 Failing Unit Tests
- **SystemPrimitivesTests**: Fixed OopsLeft tests (expected theoretical max ~1 billion, not table size)
- **SystemPrimitivesTests**: Fixed SignalAtOopsLeftWordsLeft (3 args, added ILowMemoryManager mock)
- **FloatPrimitivesTests**: Fixed all Float tests (use VMMetadata.FloatClassOOP, mock GetFloatAsDouble)
- **All 385 tests now pass** ✅

### 2. Implemented Fixedsys Bitmap Font Experiment
- **Problem**: TrueType antialiasing on 1bpp display causes pixel dropout artifacts
- **Solution**: Native GDI bitmap font rendering via P/Invoke
- **Files Created**:
  - `Core/VM/Graphics/GdiBitmapFontBridge.cs` - 717 lines, full IFontBridge implementation
  - Uses CreateFont, TextOut, CreateDIBSection for direct 1bpp rendering
- **Files Modified**:
  - `Core/VM/Graphics/FontBridgeFactory.cs` - Added toggle and factory method
  - `Program.cs` - Enabled experiment: `FontBridgeFactory.UseGdiBitmapFonts = true`
- **Status**: Enabled and working - cleaner character rendering

### 3. Updated Graphics Architecture Documentation
- Updated `docs/Graphics-Architecture.md` with:
  - Phase 4 primitives (all complete)
  - Phase 5 font support (complete)
  - Future Phase 6: 32bpp display migration plan
  - Proposed IDisplay unified interface

## Current Architecture

### Display Stack
```
┌─────────────────────────────────────────────────┐
│         Smalltalk-80 Code (Form, BitBlt)        │
├─────────────────────────────────────────────────┤
│  Primitive 96 (copyBits) - GraphicsPrimitives   │
├─────────────────────────────────────────────────┤
│    BitBltEngine (Pure C# - 1bpp operations)     │
├─────────────────────────────────────────────────┤
│   WinFormsDisplay (Windows Forms + heap sync)   │
├─────────────────────────────────────────────────┤
│       32bpp back buffer → Window rendering      │
└─────────────────────────────────────────────────┘
```

### Current Interfaces (Fragmented)
- `IPlatformBridge` - Mixed display/input/font concerns
- `IDisplayProvider` - Partially used
- `IFontBridge` - Font rendering abstraction
- `WinFormsDisplay` - Concrete Windows Forms implementation

## Next Session: 32bpp Display Migration

### Goal
Replace 1bpp display surface with 32bpp ARGB to enable:
1. Proper TrueType antialiasing (not thresholded to 1bpp)
2. Future color support
3. Pluggable graphics backends (SkiaSharp, OpenGL)

### Proposed IDisplay Interface
See `docs/Graphics-Architecture.md` section "Future: 32-bit Display with Pluggable Graphics Backends"

Key points:
- Single unified interface replacing IPlatformBridge fragmentation
- Display surface is 32bpp; Smalltalk Forms remain 1bpp internally
- 1bpp→32bpp conversion happens at BitBlt boundary when dest is Display

### Implementation Steps

**Step 1: Define IDisplay Interface**
```csharp
public interface IDisplay : IDisposable
{
    int Width { get; }
    int Height { get; }
    int BitsPerPixel { get; }  // 1, 8, or 32
    Span<byte> GetDisplayBuffer();
    void InvalidateRegion(int x, int y, int width, int height);
    // ... cursor, input, events (see full interface in docs)
}
```

**Step 2: Refactor WinFormsDisplay**
- Implement new IDisplay interface
- Keep current 1bpp heap sync behavior
- Add BitsPerPixel property (initially 1)

**Step 3: Add 32bpp Display Mode**
- Change display buffer from 1bpp to 32bpp
- Update CopyDisplayFromHeap() to convert 1bpp heap → 32bpp display
- Black (1) → 0xFF000000, White (0) → 0xFFFFFFFF

**Step 4: Update BitBltEngine**
- Add 32bpp pixel format to existing operations
- When dest is Display (32bpp) and source is Form (1bpp), convert on the fly
- All 16 combination rules need 32bpp variants

**Step 5: Remove IPlatformBridge**
- All consumers switch to IDisplay
- Delete old interfaces and bridge code
- Clean architecture with single abstraction

### Files to Modify
- `Core/VM/Graphics/IDisplay.cs` - New (create)
- `Platform/Windows/WinFormsDisplay.cs` - Implement IDisplay
- `Core/VM/BitBltEngine.cs` - Add 32bpp support
- `Core/VM/Primitives/GraphicsPrimitives.cs` - Use IDisplay
- `Core/VM/VirtualMachine.cs` - Replace IPlatformBridge with IDisplay
- `Program.cs` - Create IDisplay instead of old types
- Delete: `IPlatformBridge.cs`, related dead code

### User Guidance
User explicitly stated: "No, we do not need to keep back compat. This should be an implementation of a clean, pluggable graphics feature"

This means:
- No backward compatibility shims
- Delete unused interfaces/code
- Clean break from old architecture

## Key Files Reference

### Graphics Core
- `Core/VM/Graphics/BitBltEngine.cs` - BitBlt implementation
- `Core/VM/Graphics/IFontBridge.cs` - Font abstraction
- `Core/VM/Graphics/FontBridgeFactory.cs` - Font factory with Fixedsys toggle
- `Core/VM/Graphics/GdiBitmapFontBridge.cs` - NEW: Fixedsys support
- `Core/VM/Graphics/WinFormsFontBridge.cs` - TrueType support

### Platform
- `Platform/Windows/WinFormsDisplay.cs` - Display window
- `Platform/Windows/IPlatformBridge.cs` - TO BE REPLACED

### Primitives
- `Core/VM/Primitives/GraphicsPrimitives.cs` - BitBlt primitives
- `Core/VM/Primitives/InputPrimitives.cs` - Mouse/keyboard

### Tests
- All 385 tests passing
- Key test file: `Smalltalk80.Interpreter.Tests/Primitives/`

## Commands to Verify State

```bash
# Build and run tests
cd Smalltalk80.Interpreter.Tests
dotnet test

# Run interpreter (should show System Browser with Fixedsys font)
cd Smalltalk80.Interpreter
dotnet run

# Toggle Fixedsys off for comparison (edit Program.cs line 182)
FontBridgeFactory.UseGdiBitmapFonts = false;
```

## Commits This Session
1. `e2042c0` - Fix 25 failing primitive tests (OopsLeft, SignalAtOopsLeftWordsLeft, Float primitives)
2. `587e6c0` - Add experimental GDI bitmap font support for Fixedsys
3. `86cc808` - Enable Fixedsys bitmap font experiment in Program.cs
