# Font Support Implementation

**Last Updated**: 2025-11-19

## Overview

This document describes the font support implementation for the Smalltalk-80 interpreter, which enables proper text rendering during cold start by bootstrapping the `DefaultTextStyle` pool variable with real `StrikeFont` objects.

## Architecture

### Component Structure

```
Core/VM/Graphics/
├── IFontBridge.cs          # Interface and data structures
├── WinFormsFontBridge.cs   # GDI+ implementation
├── BitBltEngine.cs         # Existing BitBlt for glyph rendering

Engine/LoaderPhases/Phase5/
├── FontObjectBuilder.cs    # Creates Smalltalk objects from C# data
├── RuntimeObjectCreator.cs # Bootstrap routine (CreateDefaultTextStyle)

Core/VM/Primitives/
├── FontPrimitives.cs       # Optional performance optimizations
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

## References

- [Blue Book Chapter 18: Graphics Kernel](http://www.mirandabanda.org/bluebook/bluebook_chapter18.html)
- [Smalltalk-80 Virtual Machine](http://www.mirandabanda.org/bluebook/)
- [WinForms Graphics Documentation](https://docs.microsoft.com/en-us/dotnet/api/system.drawing)
