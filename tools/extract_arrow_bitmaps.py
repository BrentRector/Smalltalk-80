#!/usr/bin/env python3
"""
Extracts individual cursor bitmaps from ARROWS10.AL file and outputs them in Form format.

Reads the Alto bitmap collection file (ARROWS10.AL) and extracts each cursor/arrow
bitmap, outputting them in the Smalltalk-80 Form file format.
"""

import sys
import os
import struct
import argparse

def read_word(data, offset):
    """Read big-endian 16-bit word."""
    return (data[offset] << 8) | data[offset + 1]

def write_word(f, value):
    """Write big-endian 16-bit word."""
    f.write(struct.pack('>H', value))

def signed_int16(value):
    """Convert unsigned 16-bit to signed (two's complement)."""
    if value > 32767:
        return value - 65536
    return value

def extract_bitmaps(input_file, output_dir, prefix="arrow"):
    """Extract bitmaps from Alto .AL file."""
    # Read the entire file
    with open(input_file, 'rb') as f:
        data = f.read()

    print(f"File size: {len(data)} bytes")

    # Parse header
    sig1 = data[0]
    sig2 = read_word(data, 1)
    print(f"\nSignature: 0x{sig1:02X} 0x{sig2:04X}")

    # Scan for bitmap definitions
    print("\nScanning for bitmaps...")

    bitmaps = []
    pos = 0x220  # Starting position after offset table

    while pos < len(data) - 20:
        # Try to read potential bitmap header
        width = read_word(data, pos + 4)
        height = read_word(data, pos + 6)
        offsetX = read_word(data, pos + 8)
        offsetY = read_word(data, pos + 10)

        # Check if this looks like a valid bitmap header
        # Reasonable dimensions for cursor: 1-64 pixels
        if 0 < width <= 64 and 0 < height <= 64:
            # Calculate expected bitmap size
            word_width = (width + 15) // 16
            bitmap_words = word_width * height
            bitmap_bytes = bitmap_words * 2

            # Check if we have enough data
            if (pos + 12 + bitmap_bytes) <= len(data):
                bitmap_count = len(bitmaps) + 1

                signed_offset_x = signed_int16(offsetX)
                signed_offset_y = signed_int16(offsetY)

                print(f"Bitmap {bitmap_count} found at offset 0x{pos:04X}:")
                print(f"  Size: {width}x{height}")
                print(f"  Offset: {signed_offset_x},{signed_offset_y}")
                print(f"  Bitmap data: {bitmap_words} words ({bitmap_bytes} bytes)")

                # Store bitmap info
                bitmaps.append({
                    'number': bitmap_count,
                    'position': pos,
                    'width': width,
                    'height': height,
                    'offsetX': offsetX,
                    'offsetY': offsetY,
                    'bitmap_start': pos + 12,
                    'bitmap_words': bitmap_words
                })

                # Move to next potential bitmap
                pos += 12 + bitmap_bytes
            else:
                pos += 2
        else:
            pos += 2

        # Safety limit
        if len(bitmaps) >= 50:
            break

    print(f"\nFound {len(bitmaps)} bitmaps total")

    # Create output directory
    os.makedirs(output_dir, exist_ok=True)

    # Output each bitmap in Form format
    print("\nGenerating Form files...")

    for bitmap in bitmaps:
        form_file = os.path.join(output_dir, f"{prefix}_{bitmap['number']}.form")

        try:
            with open(form_file, 'wb') as f:
                # Write Form header
                write_word(f, 1)  # fileCode
                write_word(f, bitmap['width'])
                write_word(f, bitmap['height'])
                write_word(f, bitmap['offsetX'])
                write_word(f, bitmap['offsetY'])

                # Write bitmap data
                bitmap_start = bitmap['bitmap_start']
                bitmap_end = bitmap_start + (bitmap['bitmap_words'] * 2)

                for i in range(bitmap_start, bitmap_end, 2):
                    word = read_word(data, i)
                    write_word(f, word)

            print(f"  Created: {form_file} ({bitmap['width']}x{bitmap['height']})")

        except Exception as e:
            print(f"  Error creating {form_file}: {e}")

    # Generate visual text representation
    text_file = os.path.join(output_dir, "arrows_visual.txt")
    with open(text_file, 'w', encoding='utf-8') as f:
        f.write("ARROWS10.AL Bitmap Visualization\n")
        f.write("=" * 80 + "\n\n")

        for bitmap in bitmaps:
            f.write(f"\nBitmap {bitmap['number']}: {bitmap['width']}x{bitmap['height']} ")
            f.write(f"at offset ({signed_int16(bitmap['offsetX'])},{signed_int16(bitmap['offsetY'])})\n")
            f.write("-" * 80 + "\n")

            # Read bitmap data
            bitmap_start = bitmap['bitmap_start']
            word_width = (bitmap['width'] + 15) // 16

            # Build the bitmap as a 2D array
            for row in range(bitmap['height']):
                row_bits = ""
                for col in range(word_width):
                    word_offset = bitmap_start + ((col * bitmap['height'] + row) * 2)
                    if word_offset + 1 < len(data):
                        word = read_word(data, word_offset)

                        # Convert word to binary string (16 bits)
                        for bit in range(15, -1, -1):
                            if (col * 16 + (15 - bit)) < bitmap['width']:
                                if (word & (1 << bit)) != 0:
                                    row_bits += "█"
                                else:
                                    row_bits += "·"
                f.write(row_bits + "\n")

            f.write("\n")

    print(f"\nVisual representation saved to: {text_file}")
    print(f"\nDone! Extracted {len(bitmaps)} bitmaps.")

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Extract bitmaps from Alto .AL file')
    parser.add_argument('input_file', help='Path to .AL file')
    parser.add_argument('output_dir', help='Directory where Form files will be written')
    parser.add_argument('--prefix', default='arrow', help='Prefix for output filenames (default: arrow)')

    args = parser.parse_args()

    if not os.path.exists(args.input_file):
        print(f"Error: Input file not found: {args.input_file}")
        sys.exit(1)

    extract_bitmaps(args.input_file, args.output_dir, args.prefix)
