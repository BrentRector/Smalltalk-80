#!/usr/bin/env python3
"""
Display all FormMenuView .form icons as graphical images.

Requires: pip install Pillow
"""

import struct
import os
import sys

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:
    print("This script requires Pillow. Install with:")
    print("  pip install Pillow")
    sys.exit(1)


def read_form_file(filename):
    """
    Read a Smalltalk-80 .form file and return as PIL Image.
    """
    with open(filename, 'rb') as f:
        data = f.read()

    file_code = struct.unpack('>H', data[0:2])[0]
    width = struct.unpack('>H', data[2:4])[0]
    height = struct.unpack('>H', data[4:6])[0]

    if file_code != 1:
        raise ValueError(f"Invalid file code: {file_code}")

    word_cols = (width + 15) // 16

    # Read bitmap data (column-major)
    bitmap_words = []
    for i in range(10, len(data), 2):
        bitmap_words.append(struct.unpack('>H', data[i:i+2])[0])

    # Create image (1-bit, white background)
    img = Image.new('1', (width, height), 1)  # 1 = white
    pixels = img.load()

    # Set pixels from bitmap
    for row in range(height):
        for col in range(word_cols):
            word_idx = col * height + row
            if word_idx < len(bitmap_words):
                word = bitmap_words[word_idx]
                for bit in range(16):
                    pixel_x = col * 16 + bit
                    if pixel_x < width:
                        if word & (1 << (15 - bit)):
                            pixels[pixel_x, row] = 0  # 0 = black

    return img


def create_icon_grid(forms_dir, icons, scale=4, padding=10, label_height=20):
    """
    Create a grid image of all icons with labels.
    """
    # Load all icons
    loaded = []
    for filename, label in icons:
        filepath = os.path.join(forms_dir, filename)
        if os.path.exists(filepath):
            img = read_form_file(filepath)
            # Scale up for visibility
            img = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
            loaded.append((label, img))
        else:
            print(f"Warning: {filename} not found")
            loaded.append((label, None))

    if not loaded:
        return None

    # Calculate grid dimensions
    cols = min(6, len(loaded))
    rows = (len(loaded) + cols - 1) // cols

    # Get max icon size
    max_width = max(img.width for _, img in loaded if img) if loaded else 32 * scale
    max_height = max(img.height for _, img in loaded if img) if loaded else 32 * scale

    cell_width = max_width + padding * 2
    cell_height = max_height + padding * 2 + label_height

    # Create output image (white background)
    grid_width = cols * cell_width
    grid_height = rows * cell_height
    grid = Image.new('RGB', (grid_width, grid_height), (255, 255, 255))
    draw = ImageDraw.Draw(grid)

    # Place icons
    for idx, (label, img) in enumerate(loaded):
        row = idx // cols
        col = idx % cols

        x = col * cell_width + padding
        y = row * cell_height + padding

        if img:
            # Convert 1-bit to RGB for pasting
            img_rgb = img.convert('RGB')
            grid.paste(img_rgb, (x, y))

            # Draw border around icon
            draw.rectangle(
                [x - 1, y - 1, x + img.width, y + img.height],
                outline=(200, 200, 200)
            )

        # Draw label
        label_x = x + (max_width - len(label) * 6) // 2
        label_y = y + max_height + 5
        draw.text((label_x, label_y), label, fill=(0, 0, 0))

    return grid


def main():
    # Determine forms directory
    script_dir = os.path.dirname(os.path.abspath(__file__))
    forms_dir = os.path.join(script_dir, '..', 'forms')

    if not os.path.exists(forms_dir):
        print(f"Forms directory not found: {forms_dir}")
        return

    # All icons organized by category
    all_icons = [
        # Tools
        ('select.form', 'select'),
        ('singlecopy.form', 'singlecopy'),
        ('repeatcopy.form', 'repeatcopy'),
        ('line.form', 'line'),
        ('curve.form', 'curve'),
        ('block.form', 'block'),
        # Modes
        ('over.form', 'over'),
        ('under.form', 'under'),
        ('reverse.form', 'reverse'),
        ('erase.form', 'erase'),
        ('in.form', 'in'),
        ('out.form', 'out'),
        # Editor & Colors
        ('magnify.form', 'magnify'),
        ('white.form', 'white'),
        ('lightgray.form', 'lightgray'),
        ('gray.form', 'gray'),
        ('darkgray.form', 'darkgray'),
        ('black.form', 'black'),
        # Grid
        ('xgrid.form', 'xgrid'),
        ('ygrid.form', 'ygrid'),
        ('togglegrids.form', 'togglegrids'),
        # Borders
        ('borderform.form', 'border'),
        ('specialborderform.form', 'special'),
    ]

    print("Creating icon grid...")
    grid = create_icon_grid(forms_dir, all_icons, scale=4)

    if grid:
        # Save to file
        output_path = os.path.join(forms_dir, 'all_icons.png')
        grid.save(output_path)
        print(f"Saved to: {output_path}")

        # Try to display
        try:
            grid.show()
            print("Opened image viewer")
        except Exception as e:
            print(f"Could not open viewer: {e}")
            print(f"Open the image manually: {output_path}")


if __name__ == '__main__':
    main()
