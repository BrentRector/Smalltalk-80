# Alto .AL Font File Format

**Status**: Work in Progress - Parser under development

## Format Specification

Based on Alto documentation screenshots in `/images/`:

### File Structure

```
Header:
  Height (word) - Font height in scan-lines
  [proportional bit, baseline (7 bits), maxWidth (8 bits)]

Pointers Table:
  256+ entries of self-relative word pointers
  Each pointer points to XW word of character encoding
  First 255 (0377 octal) entries for ASCII characters

Character Data Region:
  Variable number of character encodings

Character Encoding (XHdata):
  bitData (XH words) - Top scan-line first
  HD (byte) - Number of scan-lines omitted at top
  XH (byte) - Number of scan-lines of bit data
  XW (word) - Width encoding
```

### XW Width Encoding

- If `XW` is odd: `width = (XW - 1) / 2`
- If `XW` is even: Extension character, `xCode = XW / 2`

### Pointer Interpretation

Pointers are **self-relative** and point to the **XW word** location.

From pointer location at offset P:
- Pointer value V
- XW location: `P + V`
- Read backwards from XW to get character structure

### Dummy Character

By convention, first character encoding is "dummy" for non-existent codes:
- XH = 0 (no bitmap data)
- HD = 0
- XW = 1 (width = 0)

## Current Issues

The parser (`parse_al_font.py`) is under development. Current challenges:

1. **Byte order interpretation** - Need to verify reading direction for HD/XH/XW
2. **Pointer calculation** - Self-relative pointer arithmetic needs validation
3. **Structure layout** - Exact byte positions of HD, XH relative to XW unclear

## Test Files

- `GACHA10.AL` - 10-point font (2,288 bytes, height=12)
- `GACHA12.AL` - 12-point font
- `MATH10.AL` - Mathematical symbols
- `ARROWS10.AL` - Cursor/arrow bitmaps (may be different format)

## Next Steps

1. Find working Alto font parser reference code
2. Validate pointer interpretation with known-good parser
3. Implement complete character extraction
4. Output to Smalltalk-80 StrikeFont format

## References

- Alto Font Format documentation (screenshots in `/images/`)
- Xerox PARC Font Representations and Formats (March 1977)
- Alto Hardware Manual - CONVERT instruction
