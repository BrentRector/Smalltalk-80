# Native 32bpp Implementation Plan

**Date**: 2026-01-01
**Status**: Planning
**Branch**: `claude/32bpp-display-graphics-RLW1k`

## Executive Summary

The current 32bpp implementation works by intercepting primitives and maintaining a separate 32bpp display buffer in C#, while Smalltalk Forms remain 1bpp. This has limitations:

1. **Off-screen Forms remain 1bpp** - halftone patterns "baked in" to intermediate Forms appear dithered when copied to Display
2. **No true color support** - only halftone-to-gray simulation
3. **Dual rendering overhead** - every BitBlt runs twice (1bpp + 32bpp)

A **native** multi-depth implementation would modify Smalltalk source code to make Forms depth-aware, enabling true 32bpp throughout the graphics stack.

## Three Implementation Approaches

### Approach A: Current Display-Only 32bpp (Already Implemented)

**What we have now:**
- Smalltalk Forms: Always 1bpp
- Display buffer: 32bpp in C#
- Conversion: At primitive level, halftone patterns → gray colors

**Pros:**
- ✅ Zero Smalltalk code changes
- ✅ Working now
- ✅ Smooth gray rendering for direct Display operations

**Cons:**
- ❌ Intermediate Forms (window backing stores) remain dithered
- ❌ Selection/scroll bar artifacts from Form-to-Display copies
- ❌ No path to real color support

---

### Approach B: Native Multi-Depth Forms (Recommended)

**Core changes to Smalltalk source:**

1. **Form class gains `depth` instance variable**
   ```smalltalk
   Form instance variables: 'bits width height offset depth'

   Form>>depth
       depth isNil ifTrue: [^1].  "backward compatibility"
       ^depth
   ```

2. **Form allocation becomes depth-aware**
   ```smalltalk
   Form>>extent: aPoint depth: bitsPerPixel
       | wordsPerLine |
       wordsPerLine := (aPoint x * bitsPerPixel + 15) // 16.
       bits := WordArray new: aPoint y * wordsPerLine.
       depth := bitsPerPixel
   ```

3. **BitBlt handles depth conversion**
   - 1bpp → 32bpp: Bit expansion with halftone-to-gray
   - 32bpp → 1bpp: Threshold/dithering (for compatibility)
   - Same-depth: Direct operation

4. **DisplayScreen created at user-selected depth**
   ```smalltalk
   DisplayScreen displayExtent: 800@600 depth: 32
   ```

**Pros:**
- ✅ All Forms can be 32bpp - no dithering artifacts
- ✅ True gray colors throughout
- ✅ Path to full color support
- ✅ Backward compatible (default depth=1)

**Cons:**
- ⚠️ Significant Smalltalk source changes
- ⚠️ Primitive 96 needs complete rewrite
- ⚠️ Memory usage: 32× for 32bpp Forms

---

### Approach C: Parallel Form Classes (Form1/Form32)

**Separate class hierarchy:**
```
Form (abstract)
├── Form1 (current 1bpp)
└── Form32 (new 32bpp)
```

**Pros:**
- ✅ Clean separation of concerns
- ✅ 1bpp code unchanged

**Cons:**
- ❌ Code duplication
- ❌ Requires parallel hierarchies (Pen1/Pen32, etc.)
- ❌ Polymorphism problems

**Not recommended** - creates complexity explosion.

---

## Recommended: Approach B Implementation Phases

### Phase 1: Form Depth Infrastructure

**Smalltalk changes:**

```smalltalk
"Form class - add depth ivar and accessors"
Form>>depth
    depth isNil ifTrue: [^1].
    ^depth

Form>>depth: bitsPerPixel
    depth := bitsPerPixel

"Form allocation - depth aware"
Form class>>extent: extentPoint depth: bitsPerPixel
    | newForm wordsPerLine |
    newForm := self basicNew.
    wordsPerLine := (extentPoint x * bitsPerPixel + 15) // 16.
    newForm
        setExtent: extentPoint;
        depth: bitsPerPixel;
        bits: (WordArray new: extentPoint y * wordsPerLine).
    ^newForm

"Backward compatible extent:"
Form class>>extent: extentPoint
    ^self extent: extentPoint depth: 1
```

**C# primitive changes:**
- Primitive 96: Read Form depth from ivar, dispatch to appropriate BitBlt path
- New primitive: Create Form with depth

**Test criteria:**
- Existing 1bpp code works unchanged
- Can create 32bpp Form programmatically
- BitBlt copies 1bpp→32bpp with halftone conversion

---

### Phase 2: DisplayScreen at 32bpp

**Smalltalk changes:**

```smalltalk
"DisplayScreen initialization with depth"
DisplayScreen class>>displayExtent: extentPoint depth: bitsPerPixel
    Display := DisplayScreen new
        extent: extentPoint
        depth: bitsPerPixel.
    Display beDisplay.
    ^Display

"System startup configuration"
DisplayScreen class>>preferredDepth
    ^32  "or read from config"
```

**C# changes:**
- Primitive 102 (beDisplay): Read Form depth, configure display accordingly
- Remove 32bpp display buffer hack - Form itself is 32bpp

**Test criteria:**
- System starts with 32bpp Display
- All rendering operations produce smooth grays
- No dithering artifacts

---

### Phase 3: Color Class and True Color Support

**New Smalltalk class:**

```smalltalk
Object subclass: #Color
    instanceVariableNames: 'red green blue alpha'

Color class>>gray: brightness
    "0.0 = black, 1.0 = white"
    | v |
    v := (brightness * 255) asInteger.
    ^self red: v green: v blue: v

Color>>asPixel32
    ^(alpha << 24) + (red << 16) + (green << 8) + blue

Color>>asHalftone
    "Convert to 1bpp dithered pattern for compatibility"
    | density |
    density := (red + green + blue) // 3.
    ^Form grayForDensity: density
```

**Integration:**
```smalltalk
Pen>>color: aColor
    destForm depth = 32
        ifTrue: [self source32: aColor asPixel32]
        ifFalse: [halftoneForm := aColor asHalftone]
```

---

### Phase 4: Window System Integration

**Changes needed:**
- `StandardSystemView` creates 32bpp backing Forms
- `ScrollBar`, `Button`, etc. use Color instead of halftone
- `TextMorphic` (if used) renders to 32bpp

---

## Primitive Implementation Details

### Primitive 96 (copyBits) - Depth-Aware

```csharp
// Read depths from Forms
int sourceDepth = GetFormDepth(sourceForm);  // 1 or 32
int destDepth = GetFormDepth(destForm);      // 1 or 32

// Dispatch based on depth combination
(sourceDepth, destDepth) switch
{
    (1, 1) => BitBltEngine.CopyBits(...),      // Current implementation
    (32, 32) => BitBltEngine32.CopyBits32(...), // 32-bit pixel ops
    (1, 32) => BitBltEngine32.CopyBits1To32(...), // Expand with halftone→gray
    (32, 1) => BitBltEngine32.CopyBits32To1(...), // Threshold/dither
};
```

### Form Depth Instance Variable Access

```csharp
int GetFormDepth(SmalltalkOOP formOOP)
{
    // Depth is ivar index 4 (after bits, width, height, offset)
    var depthOOP = heap.GetInstanceVariable(formOOP, 4);
    if (depthOOP.IsNil || depthOOP == heap.GetNil())
        return 1;  // Default for backward compatibility
    return depthOOP.IsSmallInteger ? depthOOP.AsSmallInteger() : 1;
}
```

---

## Migration Path

### Step 1: Add Form depth ivar (backward compatible)
- Default nil = 1bpp
- Existing code unchanged

### Step 2: Update primitive 96 to read depth
- If depth=1: current BitBlt
- If depth=32: new 32bpp BitBlt

### Step 3: Add Form creation with depth
- `Form extent: 100@100 depth: 32`

### Step 4: DisplayScreen at 32bpp
- `DisplayScreen displayExtent: 800@600 depth: 32`
- Remove C# display buffer hack

### Step 5: Color class and integration
- True color support
- Pen>>color:, etc.

---

## Memory Implications

| Content | 1bpp (800×600) | 32bpp (800×600) |
|---------|----------------|-----------------|
| Display | 60 KB | 1.9 MB |
| Window backing (4 windows) | 240 KB | 7.6 MB |
| Cursor, icons | 1 KB | 32 KB |
| **Total estimate** | ~300 KB | ~10 MB |

Modern systems: Not a concern.

---

## Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| Breaks existing Smalltalk code | Default depth=1 for backward compatibility |
| Performance regression | 32bpp operations use SIMD where possible |
| Complex depth conversions | Test matrix: 1→1, 1→32, 32→1, 32→32 |
| Font rendering changes | Keep fonts 1bpp, expand on BitBlt |

---

## Recommendation

**Implement Approach B in phases:**

1. **Phase 1** (1-2 days): Form depth infrastructure - minimal risk, enables testing
2. **Phase 2** (1-2 days): DisplayScreen at 32bpp - eliminates current artifacts
3. **Phase 3** (2-3 days): Color class - enables true color
4. **Phase 4** (ongoing): UI integration - incremental improvements

**Alternative:** If Smalltalk changes are too invasive, enhance current Approach A by:
- Detecting halftone patterns in source Forms during BitBlt
- Converting detected patterns to gray even in Form-to-Display copies

---

## Files to Modify

### Smalltalk Source (`Smalltalk-80 Source/Smalltalk-80.st`)
- Form class: Add depth ivar, modify allocation
- DisplayScreen class: Add depth parameter
- New Color class
- BitBlt class: Depth-aware documentation/fallbacks

### C# Interpreter
- `GraphicsPrimitives.cs`: Depth-aware primitive 96
- `BitBltEngine.cs`: 32bpp operations
- `VMConstants.cs`: Form ivar indices
- `ColdStartPhases.cs`: Form class with depth ivar

### Compiler
- `ClassDefinitions.cs`: Form with 5 ivars (add depth)

---

## Decision Required

**Options:**
1. **Continue with Approach A** - fix remaining issues in current implementation
2. **Implement Approach B** - native multi-depth Forms (recommended)
3. **Hybrid** - Approach A + pattern detection for intermediate Forms

Please advise which direction to pursue.
