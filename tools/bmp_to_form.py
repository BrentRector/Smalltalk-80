#!/usr/bin/env python3
"""
Convert Windows BMP files (1-bit) to Smalltalk-80 .form binary format.

Form file format (big-endian 16-bit words):
- Word 0: fileCode (must be 1)
- Word 1: width in pixels
- Word 2: height in pixels
- Word 3: offsetX (signed, two's complement)
- Word 4: offsetY (signed, two's complement)
- Words 5+: bitmap data in column-major order
"""

import struct
import os
from pathlib import Path
from PIL import Image


def bmp_to_form(bmp_path: Path, form_path: Path, offset_x: int = 0, offset_y: int = 0):
    """
    Convert a BMP file to Smalltalk-80 .form format.

    Args:
        bmp_path: Path to input BMP file
        form_path: Path to output .form file
        offset_x: X offset for the form (default 0)
        offset_y: Y offset for the form (default 0)
    """
    # Load BMP and convert to 1-bit
    img = Image.open(bmp_path)

    # Convert to 1-bit if not already
    if img.mode != '1':
        img = img.convert('1')

    width, height = img.size

    # Calculate word columns (16 pixels per word)
    word_cols = (width + 15) // 16

    # Get pixel data
    # In PIL, 1-bit images: 0 = black, 255 = white
    # In Smalltalk Form: 1 = black, 0 = white
    pixels = img.load()

    # Build bitmap in column-major order
    # For each word-column, for each row, pack 16 pixels into a word
    bitmap_words = []

    for col in range(word_cols):
        for row in range(height):
            word_val = 0
            for bit in range(16):
                pixel_x = col * 16 + bit
                if pixel_x < width:
                    # Get pixel value (0 = black in PIL 1-bit mode)
                    pixel = pixels[pixel_x, row]
                    # In Smalltalk: 1 = black, 0 = white
                    # MSB first (bit 15 is leftmost pixel)
                    if pixel == 0:  # Black pixel
                        word_val |= (1 << (15 - bit))
            bitmap_words.append(word_val)

    # Handle negative offsets (two's complement)
    file_offset_x = offset_x if offset_x >= 0 else 65536 + offset_x
    file_offset_y = offset_y if offset_y >= 0 else 65536 + offset_y

    # Write .form file (big-endian)
    with open(form_path, 'wb') as f:
        # Header
        f.write(struct.pack('>H', 1))           # fileCode = 1
        f.write(struct.pack('>H', width))       # width
        f.write(struct.pack('>H', height))      # height
        f.write(struct.pack('>H', file_offset_x))  # offsetX
        f.write(struct.pack('>H', file_offset_y))  # offsetY

        # Bitmap data (column-major)
        for word in bitmap_words:
            f.write(struct.pack('>H', word))

    print(f"Created {form_path.name}: {width}x{height}, offset=({offset_x},{offset_y}), {len(bitmap_words)} words")


def convert_all_forms(input_dir: Path, output_dir: Path):
    """Convert all .form.bmp files to .form files."""
    output_dir.mkdir(exist_ok=True)

    # Find all .form.bmp files
    bmp_files = sorted(input_dir.glob('*.form.bmp'))

    print(f"Found {len(bmp_files)} .form.bmp files to convert\n")

    for bmp_path in bmp_files:
        # Output filename: replace .form.bmp with .form
        form_name = bmp_path.stem  # e.g., "black.form"

        # Handle special naming cases
        if form_name == 'border.form':
            form_name = 'borderform.form'
        elif form_name == 'special.form':
            form_name = 'specialborderform.form'
        elif not form_name.endswith('.form'):
            form_name = form_name + '.form'

        form_path = output_dir / form_name

        bmp_to_form(bmp_path, form_path)

    # Also handle special files that might have different naming
    special_files = list(input_dir.glob('*.bmp'))
    for bmp_path in special_files:
        if bmp_path in bmp_files:
            continue  # Already processed

        stem = bmp_path.stem
        # Handle files like "single.copy.bmp" -> "singlecopy.form"
        if 'copy' in stem.lower():
            # Normalize the name
            if 'single' in stem.lower():
                form_name = 'singlecopy.form'
            elif 'repeat' in stem.lower():
                form_name = 'repeatcopy.form'
            else:
                form_name = stem.replace('.', '') + '.form'

            form_path = output_dir / form_name
            bmp_to_form(bmp_path, form_path)
        elif stem == 'Panel':
            # Convert Panel.bmp to Panel.form
            form_path = output_dir / 'Panel.form'
            bmp_to_form(bmp_path, form_path)

    print(f"\nConversion complete. Files written to: {output_dir}")


def main():
    # Default paths
    script_dir = Path(__file__).parent
    input_dir = script_dir.parent / 'bitmaps'
    output_dir = script_dir.parent / 'Forms'

    convert_all_forms(input_dir, output_dir)


if __name__ == '__main__':
    main()
