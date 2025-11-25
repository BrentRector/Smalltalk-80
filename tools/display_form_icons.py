#!/usr/bin/env python3
"""
Display all FormMenuView .form icons in a visual grid.

Reads the binary .form files and displays them as ASCII art
arranged in the same layout as the original Form Editor menu.
"""

import struct
import os

def read_form_file(filename):
    """
    Read a Smalltalk-80 .form file and return bitmap as 2D array.

    Returns: (width, height, bitmap_rows) where bitmap_rows is list of strings
             with '1' for black and '0' for white pixels
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

    # Convert to row-major bitmap
    rows = []
    for row in range(height):
        line = ''
        for col in range(word_cols):
            word_idx = col * height + row
            if word_idx < len(bitmap_words):
                word = bitmap_words[word_idx]
                for bit in range(16):
                    pixel_x = col * 16 + bit
                    if pixel_x < width:
                        if word & (1 << (15 - bit)):
                            line += '1'
                        else:
                            line += '0'
        rows.append(line)

    return width, height, rows


def render_icon(rows, scale=1, black_char='#', white_char='.'):
    """Render a bitmap as ASCII art."""
    result = []
    for row in rows:
        line = ''
        for char in row:
            if char == '1':
                line += black_char * scale
            else:
                line += white_char * scale
        for _ in range(scale):
            result.append(line)
    return result


def display_icons_grid(forms_dir, icons, columns=6, scale=1,
                       black_char='#', white_char=' ', border=True):
    """
    Display icons in a grid layout.

    icons: list of (filename, label) tuples
    """
    # Load all icons
    loaded = []
    max_width = 0
    max_height = 0

    for filename, label in icons:
        filepath = os.path.join(forms_dir, filename)
        if os.path.exists(filepath):
            width, height, rows = read_form_file(filepath)
            rendered = render_icon(rows, scale, black_char, white_char)
            loaded.append((label, rendered, width * scale))
            max_width = max(max_width, width * scale)
            max_height = max(max_height, height * scale)
        else:
            loaded.append((label, None, 0))
            print(f"Warning: {filename} not found")

    # Add padding for labels
    label_height = 2
    cell_width = max_width + 2
    cell_height = max_height + label_height + 1

    # Arrange in grid
    rows_count = (len(loaded) + columns - 1) // columns

    for grid_row in range(rows_count):
        # Print icons row
        for line_idx in range(max_height):
            line = ''
            for grid_col in range(columns):
                idx = grid_row * columns + grid_col
                if idx < len(loaded):
                    label, rendered, width = loaded[idx]
                    if rendered and line_idx < len(rendered):
                        icon_line = rendered[line_idx]
                        # Pad to cell width
                        padded = icon_line + white_char * (cell_width - len(icon_line))
                        line += padded
                    else:
                        line += white_char * cell_width
                else:
                    line += white_char * cell_width
            print(line)

        # Print labels row
        label_line = ''
        for grid_col in range(columns):
            idx = grid_row * columns + grid_col
            if idx < len(loaded):
                label, _, _ = loaded[idx]
                # Center the label
                padded = label.center(cell_width)[:cell_width]
                label_line += padded
        print(label_line)
        print()  # Blank line between rows


def main():
    # Determine forms directory
    script_dir = os.path.dirname(os.path.abspath(__file__))
    forms_dir = os.path.join(script_dir, '..', 'forms')

    if not os.path.exists(forms_dir):
        print(f"Forms directory not found: {forms_dir}")
        return

    print("=" * 80)
    print("SMALLTALK-80 FORM EDITOR ICONS")
    print("=" * 80)
    print()

    # Row 1: Tools
    # Note: Original Smalltalk-80 used trailing periods but Windows doesn't support them
    print("ROW 1: TOOLS")
    print("-" * 80)
    tools = [
        ('select.form', 'select'),
        ('singlecopy.form', 'singlecopy'),
        ('repeatcopy.form', 'repeatcopy'),
        ('line.form', 'line'),
        ('curve.form', 'curve'),
        ('block.form', 'block'),
    ]
    display_icons_grid(forms_dir, tools, columns=6)

    # Row 1 continued: Modes
    print("ROW 1: MODES")
    print("-" * 80)
    modes = [
        ('over.form', 'over'),
        ('under.form', 'under'),
        ('reverse.form', 'reverse'),
        ('erase.form', 'erase'),
        ('in.form', 'in'),
    ]
    display_icons_grid(forms_dir, modes, columns=5)

    # Row 2: Magnify and Colors
    print("ROW 2: MAGNIFY & COLORS")
    print("-" * 80)
    colors = [
        ('magnify.form', 'magnify'),
        ('white.form', 'white'),
        ('lightgray.form', 'lightgray'),
        ('gray.form', 'gray'),
        ('darkgray.form', 'darkgray'),
        ('black.form', 'black'),
    ]
    display_icons_grid(forms_dir, colors, columns=6)

    # Row 2 continued: Grid controls
    print("ROW 2: GRID CONTROLS & FILE OUT")
    print("-" * 80)
    grids = [
        ('xgrid.form', 'xgrid'),
        ('ygrid.form', 'ygrid'),
        ('togglegrids.form', 'togglegrids'),
        ('out.form', 'out'),
    ]
    display_icons_grid(forms_dir, grids, columns=4)

    # Border forms
    print("BORDER FORMS")
    print("-" * 80)
    borders = [
        ('borderform.form', 'border'),
        ('specialborderform.form', 'special'),
    ]
    display_icons_grid(forms_dir, borders, columns=2)

    print("=" * 80)
    print(f"Total: 23 form files in {os.path.abspath(forms_dir)}")
    print("=" * 80)


if __name__ == '__main__':
    main()
