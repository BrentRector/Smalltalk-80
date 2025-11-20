# Font Support Implementation

**Last Updated**: 2025-11-19

## Overview

This document describes the font support implementation for the Smalltalk-80 interpreter, which enables proper text rendering during cold start by bootstrapping the `DefaultTextStyle` pool variable with real `StrikeFont` objects.

## Prerequisites

### Pool Dictionary Bootstrap

**IMPORTANT**: For DefaultTextStyle to be accessible to Smalltalk code, the cold start must:

1. Create the TextConstants pool dictionary
2. Migrate DefaultTextStyle Association from Undeclared to TextConstants
3. Set the Association's value to the TextStyle object

See **[Pool-Dictionary-Bootstrap-Requirements.md](Pool-Dictionary-Bootstrap-Requirements.md)** for details.

Without this migration, Smalltalk code declaring `poolDictionaries: 'TextConstants'` will not be able to resolve DefaultTextStyle, even though the TextStyle object exists on the heap.

## Host vs Smalltalk Responsibilities

### Host System (Windows/WinForms)

The host operating system provides:
- **Font discovery**: APIs to enumerate installed fonts (families, styles, weights)
- **Metrics**: Ascent, descent, line spacing, baseline, per-character advance widths
- **Shaping**: Complex script handling via DirectWrite (ligatures, kerning, RTL)
- **Rasterization**: Glyph outlines to bitmaps with hinting and antialiasing
- **DPI scaling**: System DPI settings (96, 120, etc.)

### Smalltalk System

Smalltalk expects:
- **Canonical representation**: StrikeFont objects - compact bitmap atlases with per-glyph metrics
- **Pool variables**: DefaultTextStyle in TextConstants, holding StrikeFonts keyed by size
- **Primitives**: widthOfCharacter:, widthOfString:, ascent, descent, leading, drawString:
- **Determinism**: Reproducible metrics and rendering across runs

### Interaction Flow

1. **Initialization (Bootstrap)**
   - VM calls host APIs (WinForms/GDI+) to resolve font faces
   - Metrics and glyphs are extracted and cached
   - StrikeFont objects are built and inserted into DefaultTextStyle

2. **Primitive Execution**
   - `widthOfCharacter:` → lookup width table built from host metrics
   - `drawString:` → BitBlt glyph bitmaps into Smalltalk display form

3. **Determinism Enforcement**
   - VM fixes DPI at 96 and disables ClearType
   - Glyphs thresholded to 1bpp before storage in StrikeFont

### Key Design Principle

The Smalltalk VM never directly exposes Windows font objects. Instead:
- It queries Windows for metrics and glyphs
- Converts them into VM-native StrikeFont/TextStyle objects
- Registers them in TextConstants for consistent, canonical font model

### Font Rendering Flow

![Font Interactions](Smalltalk%20Image%20Files/Font%20Interactions.png)

The diagram shows the interaction between Smalltalk and Windows layers:

1. **TextStyle** contains an array of fonts, selects **StrikeFont** by size
2. **StrikeFont** handles text rendering and glyph lookup
3. **FontBridge Interface** mediates between Smalltalk and Windows
4. **Windows** provides the actual font rendering engine
5. **Result glyphs** are cached and returned to Smalltalk

## Architecture

### Component Structure

```
Core/VM/Graphics/
├── IFontBridge.cs          # Interface and data structures
├── WinFormsFontBridge.cs   # GDI+ implementation (Windows)
├── FontBridgeFactory.cs    # Platform abstraction factory + NullFontBridge
├── BitBltEngine.cs         # Existing BitBlt for glyph rendering

Engine/LoaderPhases/Phase5/
├── FontObjectBuilder.cs    # Creates Smalltalk objects from C# data
├── RuntimeObjectCreator.cs # Bootstrap routine (CreateDefaultTextStyle)

Core/VM/Primitives/
├── FontPrimitives.cs       # Performance optimizations (primitives 147-150)
├── PrimitiveRegistry.cs    # Registers font primitives
```

### Key Interfaces

#### IFontBridge

Abstracts platform font APIs from the VM:

```csharp
public interface IFontBridge
{
    bool ResolveFace(FontDescriptor descriptor);
    FontMetrics GetMetrics(FontDescriptor descriptor);
    int GetCharWidth(FontDescriptor descriptor, char character);
    int GetStringWidth(FontDescriptor descriptor, string text);
    GlyphData RenderGlyph(FontDescriptor descriptor, char character);
    StrikeFontData BuildStrikeFont(FontDescriptor descriptor, int minAscii = 0, int maxAscii = 255);
    FontIdentity GetFontIdentity(FontDescriptor descriptor);
    int RenderingDpi { get; }
    bool IsDeterministicMode { get; }
}
```

### Data Flow

1. **Bootstrap** (Phase 5):
   - `RuntimeObjectCreator.CreateDefaultTextStyle()` is called
   - Creates `WinFormsFontBridge` instance
   - Builds `StrikeFontData` for canonical sizes (10, 12, 14 pixels)
   - `FontObjectBuilder` converts C# data to Smalltalk objects
   - Resulting `TextStyle` is installed in `TextConstants` pool

2. **Runtime**:
   - Smalltalk code accesses `DefaultTextStyle` from pool
   - Character widths read from `xTable` array
   - Glyph rendering done via BitBlt (primitive 96)

## Determinism Guarantees

### Fixed Parameters

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| DPI | 96 | Standard Windows DPI, ensures pixel-perfect metrics |
| Antialiasing | Disabled | 1bpp output, no ClearType |
| Hinting | Standard | Consistent rasterization |

### Cache Key Structure

Font data is cached by:
- Family name
- Size in pixels
- DPI
- Bold flag
- Italic flag
- Hinting mode

### Reproducibility

The `WinFormsFontBridge` ensures deterministic output by:

1. **Fixed DPI**: All metrics calculated at 96 DPI
2. **No Antialiasing**: `TextRenderingHint.SingleBitPerPixel`
3. **No Smoothing**: `SmoothingMode.None`
4. **Nearest Neighbor**: No interpolation artifacts
5. **Consistent Rounding**: `Math.Ceiling` for all pixel conversions

### Validation

To verify determinism:

```csharp
// Hash glyph data for comparison
var hash = ComputeHash(strikeFontData.GlyphsBitmap);

// Compare metrics
Assert.Equal(expectedAscent, fontData.Metrics.Ascent);
Assert.Equal(expectedDescent, fontData.Metrics.Descent);
```

## StrikeFont Structure

### Instance Variables

| Variable | Type | Description |
|----------|------|-------------|
| xTable | Array | Left x-coordinate for each character |
| glyphs | Form | Combined glyph bitmap |
| name | String | Font name (e.g., "Courier New-12") |
| stopConditions | Array | CharacterScanner stop conditions |
| type | Integer | Font type (0 for StrikeFont) |
| minAscii | Integer | First ASCII code (usually 0) |
| maxAscii | Integer | Last ASCII code (usually 255) |
| maxWidth | Integer | Maximum character width |
| strikeLength | Integer | Total width of glyphs form |
| ascent | Integer | Height above baseline |
| descent | Integer | Height below baseline |
| xOffset | Integer | Rendering offset |
| raster | Integer | Words per row |
| subscript | Integer | Subscript offset |
| superscript | Integer | Superscript offset |
| emphasis | Integer | Bold/italic flags |

### Glyph Storage

All character glyphs are stored in a single `Form`:
- Width = `strikeLength` (sum of all character widths)
- Height = `ascent + descent`
- Format = 1 bit per pixel, packed into 16-bit words

Character lookup:
```smalltalk
widthOf: aCharacter
    | ascii |
    ascii := aCharacter asciiValue.
    ^(xTable at: ascii + 2) - (xTable at: ascii + 1)
```

## TextStyle Structure

### Instance Variables

| Variable | Type | Description |
|----------|------|-------------|
| fontArray | Array | StrikeFonts at different sizes |
| lineGrid | Integer | Line height in pixels |
| baseline | Integer | Baseline position |
| alignment | Integer | Text alignment (0=left) |
| firstIndent | Integer | First line indent |
| restIndent | Integer | Subsequent line indent |
| rightIndent | Integer | Right margin indent |
| tabsArray | Array | Tab stop positions |
| marginTabsArray | Array | Margin tab stops |
| outputMedium | Symbol | Output target |

## Usage

### Accessing DefaultTextStyle

```smalltalk
"Get the default style"
style := TextConstants at: #DefaultTextStyle.

"Get the default font"
font := style defaultFont.

"Measure text"
width := font widthOfString: 'Hello'.
```

### Creating Text

```smalltalk
"Create a Paragraph"
Paragraph withText: 'Hello World' style: DefaultTextStyle copy.
```

## Future Extensibility

### DirectWrite Migration

The `IFontBridge` interface enables future migration to DirectWrite for:
- Unicode shaping
- Kerning and ligatures
- RTL script support
- Better hinting

Implementation would be a new `DirectWriteFontBridge` class.

### Additional Font Formats

- TrueType embedding
- OpenType features
- Variable fonts

### Performance Optimizations

The `FontPrimitives.cs` file contains optional primitives that can be registered if Smalltalk text operations become bottlenecks:

- `CharacterWidth` - Fast xTable lookup
- `StringWidth` - Fast string width calculation
- `FontAscent` / `FontDescent` - Direct field access

## Troubleshooting

### Font Not Found

If requested font is unavailable:
1. Tries Arial as fallback
2. Falls back to stub font with minimal metrics

### Headless Operation

When running without GUI:
- Font bridge creation may fail
- System falls back to stub TextStyle
- Diagnostic warning is emitted

### Metrics Mismatch

If text rendering appears incorrect:
1. Verify DPI is 96
2. Check antialiasing is disabled
3. Confirm xTable values match character widths

## Testing

### Validation Tests

```csharp
// Test metrics consistency
Assert.Equal(lineGrid, ascent + descent + leading);

// Test string widths
var iWidth = font.GetStringWidth("iiii");
var mWidth = font.GetStringWidth("mmmm");
Assert.True(mWidth > iWidth);

// Test baseline alignment
Assert.Equal(baseline, ascent);
```

### Regression Test

After font changes, run the BlockContext test suite:

```bash
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_runner.json
```

Expected: Returns `1191` (120 tests pass)

## Smalltalk Code

### TextStyle class>>initialize

This method can be used to create DefaultTextStyle from Smalltalk side:

```smalltalk
TextStyle class>>initialize
    | defaultFonts defaultStyle |
    defaultFonts := Array with: (StrikeFont new: 12).
    defaultStyle := self new.
    defaultStyle newFontArray: defaultFonts.
    TextConstants at: #DefaultTextStyle put: defaultStyle.

TextStyle initialize!
```

Note: In the current implementation, DefaultTextStyle is bootstrapped from C# during Phase 5 of cold start, before Smalltalk code executes.

## Phased Implementation Roadmap

### Phase 1: Bootstrap with WinForms/GDI+ ✅ COMPLETED
- IFontBridge and WinFormsFontBridge implementation
- Font metrics and character widths via System.Drawing
- Glyph rendering with Graphics.DrawString
- 1bpp threshold for canonical StrikeFonts
- StrikeFonts for sizes 10, 12, 14
- DefaultTextStyle registration in TextConstants

### Phase 2: Determinism & Validation ✅ COMPLETE
- Fixed DPI at 96
- Disabled ClearType/antialiasing
- Font caching by (family, sizePx, dpi, hinting)
- Font identity recording
- Validation test suite (Smalltalk tests created)

### Recent Progress (November 2025)
- Form initialization now works correctly
- CopyBits (primitive 96) handles WordArray bits properly
- Graphics primitives fully functional

## Validation Test Plan

### Test Files

- **C# Tests**: `Smalltalk80.Interpreter/Tests/FontBridgeTests.cs` - *Not yet created*
- **Smalltalk Tests**: `Smalltalk-80 Source/Tests/FontValidationTests.st` - ✅ Exists

### Running Tests

**Smalltalk Tests:**
```smalltalk
FontValidationTests runAll
```

Run during cold start by including `FontValidationTests.st` in the test bundle.

**C# Tests (Future):**
When created, tests would be run as:
```csharp
FontBridgeTests.RunAllTests();
```

### Phase 1 Tests: Bootstrap with WinForms/GDI+

| Test | C# | Smalltalk | Description |
|------|-----|-----------|-------------|
| Metrics Consistency | ✓ | ✓ | ascent + descent + leading > 0 |
| Width Sanity | ✓ | ✓ | 'm' >= 'i', positive widths |
| Glyph Rasterization | ✓ | - | Bitmap not null, valid dimensions |
| StrikeFont Build | ✓ | - | xTable monotonic, valid structure |
| DefaultTextStyle Exists | - | ✓ | Pool variable present |
| Font Array | - | ✓ | fontArray has fonts |
| String Widths | ✓ | ✓ | "Hello" > "Hi" |

### Phase 2 Tests: Determinism & Validation

| Test | C# | Smalltalk | Description |
|------|-----|-----------|-------------|
| DPI Lock | ✓ | - | RenderingDpi = 96 |
| Atlas Hashing | ✓ | - | Same font → same hash |
| Cache Consistency | ✓ | - | Cached results match |
| Baseline Alignment | - | ✓ | baseline = ascent |
| Width Determinism | - | ✓ | Same string → same width |
| Metrics Consistency | - | ✓ | lineGrid >= ascent + descent |

### Phase 3 Tests: DirectWrite (Future)

| Test | Description |
|------|-------------|
| Ligature Shaping | "ffi" < "f" + "f" + "i" |
| RTL Script | Arabic/Hebrew glyph order |
| Kerning | "AV" < "A" + "V" |

### Phase 4 Tests: Advanced Typography (Future)

| Test | Description |
|------|-------------|
| Fallback Chain | Emoji → fallback font |
| Combining Marks | "á" composed correctly |
| Variable Fonts | Weight interpolation |

### Atlas Hashing Utility

For detecting non-determinism across runs:

```csharp
var font = bridge.BuildStrikeFont(descriptor);
var rowHashes = FontBridgeTests.ComputeRowHashes(font);

// Compare with previous run
for (int i = 0; i < rowHashes.Length; i++)
{
    Console.WriteLine($"Row {i}: {rowHashes[i]}");
}
```

### Phase 3: DirectWrite Integration (Future)
- DirectWriteFontBridge for accuracy and shaping
- DirectWrite for font discovery, metrics, shaping
- Direct2D/WIC for rasterization
- Glyph ID and advance mapping

### Phase 4: Advanced Typography (Future)
- Unicode shaping, kerning, ligatures, RTL scripts
- Font fallback chains
- Combining marks and emoji support
- Extended TextStyle with shaping flags

## References

- [Blue Book Chapter 18: Graphics Kernel](http://www.mirandabanda.org/bluebook/bluebook_chapter18.html)
- [Smalltalk-80 Virtual Machine](http://www.mirandabanda.org/bluebook/)
- [WinForms Graphics Documentation](https://docs.microsoft.com/en-us/dotnet/api/system.drawing)
