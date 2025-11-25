#!/usr/bin/env python3
"""
Create Panel.form by compositing all 23 FormMenuView icons.

Layout from Form-File-Format.md:
Row 1 (y=0): select, singlecopy, repeatcopy, line, curve, block, over, under, reverse, erase, in
Row 2 (y=48): magnify, white, lightgray, gray, darkgray, black, xgrid, ygrid, togglegrids, out
Row 3 (y=96): borderform, specialborderform
"""

import struct
from pathlib import Path
from PIL import Image

# Icon layout: (display_name, filename_patterns, x, y)
# filename_patterns is a list of possible filenames to try
ICON_LAYOUT = [
    # Row 1 (y=0) - Tools and Modes
    ('select', ['select.form.bmp'], 0, 0),
    ('singlecopy', ['single.copy.bmp', 'singlecopy.form.bmp'], 32, 0),
    ('repeatcopy', ['repeat.copy.bmp', 'repeatcopy.form.bmp'], 64, 0),
    ('line', ['line.form.bmp'], 96, 0),
    ('curve', ['curve.form.bmp'], 128, 0),
    ('block', ['block.form.bmp'], 160, 0),
    ('over', ['over.form.bmp'], 208, 0),
    ('under', ['under.form.bmp'], 240, 0),
    ('reverse', ['reverse.form.bmp'], 272, 0),
    ('erase', ['erase.form.bmp'], 304, 0),
    ('in', ['in.form.bmp'], 352, 0),

    # Row 2 (y=48) - Editor, Colors, Grids, File Out
    ('magnify', ['magnify.form.bmp'], 0, 48),
    ('white', ['white.form.bmp'], 32, 48),
    ('lightgray', ['lightgray.form.bmp'], 64, 48),
    ('gray', ['gray.form.bmp'], 96, 48),
    ('darkgray', ['darkgray.form.bmp'], 128, 48),
    ('black', ['black.form.bmp'], 160, 48),
    ('xgrid', ['xgrid.form.bmp'], 208, 48),
    ('ygrid', ['ygrid.form.bmp'], 240, 48),
    ('togglegrids', ['togglegrids.form.bmp'], 272, 48),
    ('out', ['out.form.bmp'], 352, 48),

    # Row 3 (y=96) - Border forms
    ('borderform', ['border.form.bmp', 'borderform.form.bmp'], 0, 96),
    ('specialborderform', ['special.form.bmp', 'specialborderform.form.bmp'], 32, 96),
]


def create_panel(bitmaps_dir: Path, output_dir: Path):
    """Create Panel.form from individual icon BMPs."""

    # Calculate panel dimensions
    max_x = max(x + 32 for _, _, x, _ in ICON_LAYOUT)
    max_y = max(y + 32 for _, _, _, y in ICON_LAYOUT)

    # Round up to word boundary (16 pixels)
    panel_width = ((max_x + 15) // 16) * 16
    panel_height = max_y

    print(f"Creating Panel: {panel_width}x{panel_height}")

    # Create blank white panel
    panel = Image.new('1', (panel_width, panel_height), 1)  # 1 = white in mode '1'

    # Paste each icon
    for name, patterns, x, y in ICON_LAYOUT:
        # Try each filename pattern
        bmp_path = None
        for pattern in patterns:
            candidate = bitmaps_dir / pattern
            if candidate.exists():
                bmp_path = candidate
                break

        if bmp_path is None:
            print(f"  WARNING: Could not find bitmap for {name}")
            continue

        # Load and convert icon
        icon = Image.open(bmp_path)
        if icon.mode != '1':
            icon = icon.convert('1')

        # Paste onto panel
        panel.paste(icon, (x, y))
        print(f"  Placed {name} at ({x}, {y})")

    # Save as BMP for verification
    panel_bmp = output_dir / 'Panel.bmp'
    panel.save(panel_bmp)
    print(f"\nSaved Panel.bmp: {panel_width}x{panel_height}")

    # Convert to .form format
    panel_form = output_dir / 'Panel.form'
    bmp_to_form(panel, panel_form)

    return panel_width, panel_height


def bmp_to_form(img: Image.Image, form_path: Path, offset_x: int = 0, offset_y: int = 0):
    """Convert PIL Image to Smalltalk-80 .form format."""

    if img.mode != '1':
        img = img.convert('1')

    width, height = img.size
    word_cols = (width + 15) // 16
    pixels = img.load()

    # Build bitmap in column-major order
    bitmap_words = []
    for col in range(word_cols):
        for row in range(height):
            word_val = 0
            for bit in range(16):
                pixel_x = col * 16 + bit
                if pixel_x < width:
                    pixel = pixels[pixel_x, row]
                    if pixel == 0:  # Black pixel
                        word_val |= (1 << (15 - bit))
            bitmap_words.append(word_val)

    # Write .form file
    with open(form_path, 'wb') as f:
        f.write(struct.pack('>H', 1))           # fileCode
        f.write(struct.pack('>H', width))       # width
        f.write(struct.pack('>H', height))      # height
        f.write(struct.pack('>H', offset_x))    # offsetX
        f.write(struct.pack('>H', offset_y))    # offsetY
        for word in bitmap_words:
            f.write(struct.pack('>H', word))

    print(f"Created {form_path.name}: {width}x{height}, {len(bitmap_words)} words")


def main():
    script_dir = Path(__file__).parent
    bitmaps_dir = script_dir.parent / 'bitmaps'
    output_dir = script_dir.parent / 'Forms'

    output_dir.mkdir(exist_ok=True)

    create_panel(bitmaps_dir, output_dir)


if __name__ == '__main__':
    main()
