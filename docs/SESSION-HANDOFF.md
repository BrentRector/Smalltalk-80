# Session Handoff: 32bpp Display Graphics

**Date**: 2026-01-01
**Status**: Ready for Phase 1 of 32bpp implementation
**Branch**: `claude/32bpp-display-graphics-RLW1k`
**Priority**: Enhancement

---

## Quick Summary

The 1bpp graphics system is fully functional with all UI polish complete. Mouse wheel scrolling works for both list panes (selection movement) and text panes (content scrolling). The System Browser opens with proper initial selection. Ready to begin 32bpp native color support implementation.

---

## Current State

### What Works
- ✅ Build: 0 errors, 0 warnings
- ✅ Cold start: All 6 phases succeed
- ✅ Init expressions: 286/286 succeed (100%)
- ✅ System Browser: Fully functional with source code display
- ✅ Mouse wheel scrolling: List panes move selection, text panes scroll
- ✅ Scroll limiting: Text stops when last line reaches bottom
- ✅ Browser initial selection: Opens with first category selected
- ✅ All 16 BitBlt combination rules working
- ✅ 1bpp→32bpp conversion for screen display

### Recent Commits (this session)
```
d4ba09b Initialize Browser with first category selected
cc44b8b Limit scroll to stop when last line reaches bottom of pane
941bb3b Add mouse wheel scrolling support for Smalltalk-80 panes
```

---

## Mouse Wheel Implementation Details

### C# Side
- `InputWordRingBuffer.cs`: Added `TypeMouseWheel = 6 << 12` constant
- `EnqueueMouseWheel(int deltaLines)`: Encodes as offset (delta + 2048) for signed 12-bit
- `WinFormsDisplay.cs`: `OnMouseWheel` handler converts `e.Delta / 120` to notches

### Smalltalk Side
- `InputState`: Added `wheelDelta` instance variable, handles Type 6 in `run`
- `InputSensor>>mouseWheelDelta`: Returns and resets accumulated delta
- `ScrollController>>wheelActivity:`: Scrolls view content (positive = up)
- `ListController>>wheelActivity:`: Moves selection up/down with auto-scroll
- `ParagraphEditor>>controlActivity`: Handles wheel before other input
- `ListView>>changeModelSelection:`: Added for polymorphic dispatch
- `Browser>>on:`: Sets initial category to first in list

---

## Next Steps: 32bpp Native Color Support

The graphics system currently operates in 1bpp mode with conversion to 32bpp only for final screen display. The next phase implements native 32bpp Forms with full color support.

### Implementation Phases (from Graphics-Architecture.md Section 6)

| Phase | Scope | Complexity | Status |
|-------|-------|------------|--------|
| **1** | Color class | Low | Not started |
| **2** | Form depth | Medium | Not started |
| **3** | BitBltEngine32 | High | Not started |
| **4** | Depth dispatch | Medium | Not started |
| **5** | Depth conversion | Medium | Not started |
| **6** | Extended rules | Medium | Not started |
| **7** | Widget updates | High | Not started |

### Phase 1: Color Class (Recommended Start)

Create new Smalltalk classes for color values:

```smalltalk
Object subclass: #Color
    instanceVariableNames: 'rgb'
    classVariableNames: 'Black White Transparent'

Color subclass: #TranslucentColor
    instanceVariableNames: 'alpha'
```

Key methods needed:
- `Color class>>r:g:b:` - Create from float RGB (0.0-1.0)
- `Color class>>fromInteger:` - Create from 32-bit ARGB
- `Color>>red`, `green`, `blue` - Component accessors
- `Color>>asPixelValue` - Convert to 32-bit ARGB integer
- `Color>>asFormColor` - Convert to 1bpp (luminance threshold)

### Phase 2: Form Depth

Add `depth` instance variable to Form:

```smalltalk
DisplayMedium subclass: #Form
    instanceVariableNames: 'bits width height offset depth'
```

Only depths 1 and 32 are supported. Key changes:
- `Form class>>extent:depth:` - Create with specific depth
- `Form>>depth` - Returns 1 if nil (backward compatible)
- `Form>>wordsPerRow` - Different calculation for 1bpp vs 32bpp
- `Form>>is32bpp` - Convenience test

### Design Constraints

**Two depths only: 1 and 32**
- No indexed colors (2, 4, 8 bit)
- No ColorForm
- No color palettes
- Simplifies implementation significantly

**Backward Compatibility**
- `Form>>depth` returns 1 if nil
- Existing Form creation defaults to depth: 1
- Cursors and fonts remain 1bpp
- Gradual migration path

---

## Key Documentation

- **`docs/Graphics-Architecture.md`** - Complete graphics system architecture including 32bpp plan (Section 6)
- **`docs/CLAUDE.md`** - Project status and development guidelines
- **`Smalltalk-80 Source/Smalltalk-80.st`** - All Smalltalk source code

---

## Files to Modify for 32bpp

### Phase 1 (Color class)
- `Smalltalk-80.st` - Add Color and TranslucentColor classes

### Phase 2 (Form depth)
- `Smalltalk-80.st` - Modify Form class, add depth ivar and methods

### Phase 3 (BitBltEngine32)
- New file: `Core/VM/Graphics/BitBltEngine32.cs` - 32bpp BitBlt implementation

### Phase 4 (Depth dispatch)
- `GraphicsPrimitives.cs` - Route primitive 96 based on source/dest depths

---

## Build and Test Commands

```bash
# Build and run
cd Smalltalk80.Interpreter
dotnet build
dotnet run

# Regenerate coldstart after Smalltalk changes
cd "Smalltalk-80 Source"
dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- "Smalltalk-80.st"
cp ../Smalltalk80.Compiler/bin/Debug/net9.0/Data/* ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/

# Run regression tests
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/coldstart_with_runner.json
# Expected: Returns 1191 (120 tests pass)
```

---

## Known Issue

**Race Condition on Early Mouse Movement**: Interpreter may crash if mouse moves into window immediately after startup. Wait 2-3 seconds before moving mouse. Under investigation.

---

## For Next Session

**Start Here**:
1. Read this handoff document
2. Read `docs/Graphics-Architecture.md` Section 6 for detailed 32bpp plan
3. Begin Phase 1: Implement Color class in Smalltalk-80.st
4. Test with simple color creation and conversion methods
5. Proceed to Phase 2: Add depth to Form class

**Goal**: Implement native 32bpp Form support with full Color class, enabling color graphics without runtime 1bpp→32bpp conversion.
