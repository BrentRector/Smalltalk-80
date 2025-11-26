#!/usr/bin/env python3
"""
Create Panel.form by compositing all 23 FormMenuView icons.

Layout from Form-File-Format.md:
Row 1: select, singlecopy, repeatcopy, line, curve, block, over, under, reverse, erase, in
Row 2: magnify, white, lightgray, gray, darkgray, black, xgrid, ygrid, togglegrids, out
Row 3: borderform, specialborderform
"""

import struct
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

# Panel margins
MARGIN_LEFT = 16
MARGIN_RIGHT = 16
MARGIN_TOP = 16
MARGIN_BOTTOM = 16

# Icon dimensions and spacing
ICON_SIZE = 32
ICON_SPACING = 16  # Horizontal spacing between icons
LABEL_HEIGHT = 16  # Space for label below icon
ROW_SPACING = 16   # Vertical spacing between rows (icon+label)
FONT_SIZE = 12     # Font size for labels

# Icon layout: (display_name, label, filename_patterns)
# Icons are arranged in rows of 6, positions calculated automatically
ROW1_ICONS = [
    ('select', 'select', ['select.form.bmp']),
    ('singlecopy', 'single', ['single.copy.bmp', 'singlecopy.form.bmp']),
    ('repeatcopy', 'repeat', ['repeat.copy.bmp', 'repeatcopy.form.bmp']),
    ('line', 'line', ['line.form.bmp']),
    ('curve', 'curve', ['curve.form.bmp']),
    ('block', 'block', ['block.form.bmp']),
]

ROW2_ICONS = [
    ('over', 'over', ['over.form.bmp']),
    ('under', 'under', ['under.form.bmp']),
    ('reverse', 'reverse', ['reverse.form.bmp']),
    ('erase', 'erase', ['erase.form.bmp']),
    ('in', 'in', ['in.form.bmp']),
    ('magnify', 'magnify', ['magnify.form.bmp']),
]

ROW3_ICONS = [
    ('white', 'white', ['white.form.bmp']),
    ('lightgray', 'ltgray', ['lightgray.form.bmp']),
    ('gray', 'gray', ['gray.form.bmp']),
    ('darkgray', 'dkgray', ['darkgray.form.bmp']),
    ('black', 'black', ['black.form.bmp']),
    ('xgrid', 'xgrid', ['xgrid.form.bmp']),
]

ROW4_ICONS = [
    ('ygrid', 'ygrid', ['ygrid.form.bmp']),
    ('togglegrids', 'grids', ['togglegrids.form.bmp']),
    ('out', 'out', ['out.form.bmp']),
    ('borderform', 'border', ['border.form.bmp', 'borderform.form.bmp']),
    ('specialborderform', 'special', ['special.form.bmp', 'specialborderform.form.bmp']),
]

ALL_ROWS = [ROW1_ICONS, ROW2_ICONS, ROW3_ICONS, ROW4_ICONS]


def create_panel(bitmaps_dir: Path, output_dir: Path):
    """Create Panel.form from individual icon BMPs."""

    # Calculate content dimensions
    max_icons_per_row = max(len(row) for row in ALL_ROWS)
    content_width = max_icons_per_row * ICON_SIZE + (max_icons_per_row - 1) * ICON_SPACING

    row_height = ICON_SIZE + LABEL_HEIGHT
    num_rows = len(ALL_ROWS)
    content_height = num_rows * row_height + (num_rows - 1) * ROW_SPACING

    # Add margins to get total panel dimensions
    panel_width = MARGIN_LEFT + content_width + MARGIN_RIGHT
    panel_height = MARGIN_TOP + content_height + MARGIN_BOTTOM

    # Round up width to word boundary (16 pixels)
    panel_width = ((panel_width + 15) // 16) * 16

    print(f"Creating Panel: {panel_width}x{panel_height}")
    print(f"  Margins: L={MARGIN_LEFT}, R={MARGIN_RIGHT}, T={MARGIN_TOP}, B={MARGIN_BOTTOM}")
    print(f"  Icon spacing: {ICON_SPACING}px horizontal, {ROW_SPACING}px between rows")
    print(f"  Label height: {LABEL_HEIGHT}px")

    # Create blank white panel (use 'L' mode for drawing, convert to '1' later)
    panel = Image.new('L', (panel_width, panel_height), 255)
    draw = ImageDraw.Draw(panel)

    # Try to get a readable font for labels
    try:
        font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf", FONT_SIZE)
    except:
        try:
            font = ImageFont.truetype("/usr/share/fonts/TTF/DejaVuSansMono.ttf", FONT_SIZE)
        except:
            try:
                font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", FONT_SIZE)
            except:
                font = ImageFont.load_default()

    # Paste each icon row by row
    for row_idx, row_icons in enumerate(ALL_ROWS):
        row_y = MARGIN_TOP + row_idx * (row_height + ROW_SPACING)

        for col_idx, (name, label, patterns) in enumerate(row_icons):
            icon_x = MARGIN_LEFT + col_idx * (ICON_SIZE + ICON_SPACING)
            icon_y = row_y

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
            icon = Image.open(bmp_path).convert('L')

            # Paste onto panel
            panel.paste(icon, (icon_x, icon_y))
            print(f"  Placed {name} at ({icon_x}, {icon_y})")

            # Draw label below icon (centered)
            label_y = icon_y + ICON_SIZE + 1
            # Get text bounding box for centering
            bbox = draw.textbbox((0, 0), label, font=font)
            text_width = bbox[2] - bbox[0]
            label_x = icon_x + (ICON_SIZE - text_width) // 2
            draw.text((label_x, label_y), label, fill=0, font=font)

    # Convert to 1-bit
    panel = panel.point(lambda x: 0 if x < 128 else 255).convert('1')

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
