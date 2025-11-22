#!/usr/bin/env python3
"""
Parses Alto .AL font files according to the AL format specification.

AL Format Structure:
- Header: Height, proportional flag, baseline, maxWidth
- Pointers table: Self-relative pointers to character encodings (256+ entries)
- Character data: XH (height), HD (top offset), bitData, XW (width encoding)

Reference: Alto Font File Format documentation
"""

import sys
import os
import struct
import argparse

def read_word(data, offset):
    """Read big-endian 16-bit word."""
    if offset + 1 >= len(data):
        return None
    return (data[offset] << 8) | data[offset + 1]

def read_byte(data, offset):
    """Read single byte."""
    if offset >= len(data):
        return None
    return data[offset]

def write_word(f, value):
    """Write big-endian 16-bit word."""
    f.write(struct.pack('>H', value & 0xFFFF))

def parse_al_header(data):
    """Parse AL font file header."""
    if len(data) < 2:
        raise ValueError("File too small to contain header")

    # Word 0: Height and flags
    word0 = read_word(data, 0)

    height = word0  # Full word is height in some formats
    # Alternative interpretation if bit-packed:
    # proportional = (word0 >> 15) & 1
    # baseline = (word0 >> 8) & 0x7F
    # maxWidth = word0 & 0xFF

    return {
        'height': height,
        'header_word': word0
    }

def parse_pointers_table(data, start_offset, num_chars=256):
    """Parse self-relative pointers table.

    The pointers table contains self-relative pointers to character encodings.
    First 255 (0377 octal) entries are for ASCII characters.
    """
    pointers = []
    offset = start_offset

    for i in range(num_chars):
        ptr_word = read_word(data, offset)
        if ptr_word is None:
            break

        # Self-relative pointer: points to XW word relative to this position
        # Pointer value of 0 means character doesn't exist (points to dummy)
        pointers.append({
            'index': i,
            'pointer_offset': offset,
            'pointer_value': ptr_word,
            'absolute_offset': offset + ptr_word if ptr_word > 0 else None
        })
        offset += 2

    return pointers, offset

def decode_character_width(xw):
    """Decode character width from XW word.

    If width <= 16 bits: XW = (2*width)+1 (odd)
    If width > 16 bits: XW = 2*xCode (even, extension character)
    """
    if xw & 1:  # Odd - simple width encoding
        width = (xw - 1) // 2
        return width, False  # width, is_extension
    else:  # Even - extension character
        x_code = xw // 2
        return x_code, True  # extension code, is_extension

def parse_character_data(data, char_offset):
    """Parse character encoding at given offset.

    Structure XHdata:
        bitData word XH    // XH words of bitmap data (top scan-line first)
        HD byte            // Top scan-lines omitted
        XH byte            // Number of scan-lines
        XW word            // Width encoding

    Note: bitData comes FIRST, then HD, XH, XW at the end.
    The XH value tells us how many words of bitData precede it.
    """
    # We need to read backwards or scan to find XH first
    # Let's try reading forward and looking for the pattern

    # Try reading XH byte first (it should be at a reasonable position)
    # Scan forward looking for plausible XH values (1-255 scan-lines)

    # Alternative: Read tentative XH and work backwards
    # For now, let's read the structure assuming we know the format

    # Read last 4 bytes of the structure: HD, XH, XW (word)
    # But we need to find where the structure ends...

    # Let's try a different approach: assume we find XH, HD, XW pattern
    offset = char_offset

    # Try to detect the character data by scanning
    # This is tricky without knowing the exact structure size

    # Let's try reading assuming minimal structure:
    # Some bitmap words, then HD(byte), XH(byte), XW(word)

    # Read ahead to find XW word (should point back or encode width)
    # For now, return placeholder
    return None

def parse_al_font_v2(data):
    """Parse AL font file - version 2 with better structure detection."""

    print(f"File size: {len(data)} bytes")
    print(f"First 16 bytes (hex): {data[:16].hex()}")

    # Parse header
    header = parse_al_header(data)
    print(f"\nHeader:")
    print(f"  Word 0: 0x{header['header_word']:04X} ({header['header_word']})")
    print(f"  Height: {header['height']}")

    # The pointers table starts after the header
    # Try different starting positions
    pointers_start = 2  # After first word

    print(f"\nAttempting to parse pointers table at offset {pointers_start}...")
    pointers, data_start = parse_pointers_table(data, pointers_start, num_chars=256)

    print(f"Parsed {len(pointers)} pointer entries")
    print(f"Character data region starts at: 0x{data_start:04X}")

    # Show first few non-zero pointers
    print(f"\nFirst 10 non-zero pointers:")
    count = 0
    for ptr in pointers:
        if ptr['absolute_offset'] is not None and count < 10:
            print(f"  Char {ptr['index']:3d} (0x{ptr['index']:02X}): "
                  f"pointer=0x{ptr['pointer_value']:04X}, "
                  f"absolute=0x{ptr['absolute_offset']:04X}")
            count += 1

    return header, pointers

def main():
    parser = argparse.ArgumentParser(
        description='Parse Alto .AL font files',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
AL Font File Format:
  Header: Height, proportional, baseline, maxWidth
  Pointers: Self-relative pointers to character encodings
  CharData: XH (height), HD (offset), bitData, XW (width)

Example:
  python3 parse_al_font.py GACHA10.AL
        """
    )
    parser.add_argument('input_file', help='Path to .AL font file')
    parser.add_argument('--output-dir', help='Directory for output files')
    parser.add_argument('--dump', action='store_true', help='Dump detailed structure')

    args = parser.parse_args()

    if not os.path.exists(args.input_file):
        print(f"Error: File not found: {args.input_file}")
        sys.exit(1)

    # Read file
    with open(args.input_file, 'rb') as f:
        data = f.read()

    # Parse
    header, pointers = parse_al_font_v2(data)

    print(f"\n=== AL Font Parsing Complete ===")
    print(f"Font height: {header['height']}")
    print(f"Pointer entries: {len(pointers)}")

if __name__ == '__main__':
    main()
