#!/usr/bin/env python3
"""
Generate Smalltalk-80 .form files for FormMenuView icons.

Based on Blue Book Chapter 7 "How to Make Pictures" icon specifications
and screenshots from the original Smalltalk-80 system.

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

def write_form_file(filename, width, height, offset_x, offset_y, bitmap_rows):
    """
    Write a Smalltalk-80 .form file.

    bitmap_rows: list of strings, one per row, where '1' = black, '0' = white
                 Each string should be 'width' characters long
    """
    # Calculate word columns
    word_cols = (width + 15) // 16

    # Convert bitmap rows to column-major word array
    bitmap_words = []
    for col in range(word_cols):
        for row in range(height):
            # Get 16 pixels for this word (or pad with 0s)
            start_bit = col * 16
            word_val = 0
            for bit in range(16):
                pixel_x = start_bit + bit
                if pixel_x < width and row < len(bitmap_rows):
                    row_str = bitmap_rows[row]
                    if pixel_x < len(row_str) and row_str[pixel_x] == '1':
                        # MSB first: bit 15 is leftmost
                        word_val |= (1 << (15 - bit))
            bitmap_words.append(word_val)

    # Handle negative offsets (two's complement)
    if offset_x < 0:
        offset_x = 65536 + offset_x
    if offset_y < 0:
        offset_y = 65536 + offset_y

    with open(filename, 'wb') as f:
        # Header
        f.write(struct.pack('>H', 1))           # fileCode
        f.write(struct.pack('>H', width))       # width
        f.write(struct.pack('>H', height))      # height
        f.write(struct.pack('>H', offset_x))    # offsetX
        f.write(struct.pack('>H', offset_y))    # offsetY

        # Bitmap data
        for word in bitmap_words:
            f.write(struct.pack('>H', word))

    print(f"Created {filename}: {width}x{height}, {len(bitmap_words)} words")


def create_icon_32x32(pattern_func):
    """Create a 32x32 icon from a pattern function."""
    rows = []
    for y in range(32):
        row = ''
        for x in range(32):
            row += '1' if pattern_func(x, y) else '0'
        rows.append(row)
    return rows


# ============================================================================
# Icon Pattern Functions (32x32 pixels each)
# ============================================================================

def select_pattern(x, y):
    """Form Source - corner brackets for selection"""
    # Top-left corner bracket
    if (x < 12 and y < 3) or (x < 3 and y < 12):
        return True
    # Bottom-right corner bracket
    if (x >= 20 and y >= 29) or (x >= 29 and y >= 20):
        return True
    return False


def singlecopy_pattern(x, y):
    """Single Copy - single filled circle"""
    cx, cy = 16, 16
    r = 6
    dx, dy = x - cx, y - cy
    return dx*dx + dy*dy <= r*r


def repeatcopy_pattern(x, y):
    """Repeat Copy - multiple dots (one large, two small)"""
    # Large dot at bottom-left
    cx1, cy1, r1 = 10, 22, 6
    dx1, dy1 = x - cx1, y - cy1
    if dx1*dx1 + dy1*dy1 <= r1*r1:
        return True
    # Small dot at top-right
    cx2, cy2, r2 = 22, 10, 4
    dx2, dy2 = x - cx2, y - cy2
    if dx2*dx2 + dy2*dy2 <= r2*r2:
        return True
    # Small dot in middle
    cx3, cy3, r3 = 18, 18, 3
    dx3, dy3 = x - cx3, y - cy3
    if dx3*dx3 + dy3*dy3 <= r3*r3:
        return True
    return False


def line_pattern(x, y):
    """Line Tool - diagonal filled line"""
    # Thick diagonal line from top-right to bottom-left
    # Line from (28, 4) to (4, 28)
    # Distance from point to line
    x1, y1 = 28, 4
    x2, y2 = 4, 28
    thickness = 4

    # Check if point is within thick line
    dx = x2 - x1
    dy = y2 - y1
    length_sq = dx*dx + dy*dy

    if length_sq == 0:
        return False

    t = max(0, min(1, ((x - x1) * dx + (y - y1) * dy) / length_sq))
    proj_x = x1 + t * dx
    proj_y = y1 + t * dy

    dist_sq = (x - proj_x)**2 + (y - proj_y)**2
    return dist_sq <= thickness * thickness


def curve_pattern(x, y):
    """Curve Tool - curved stroke shape"""
    # Draw a curved shape like an arc or comma
    # Arc from top to right
    cx, cy = 8, 24
    r_inner = 10
    r_outer = 16

    dx, dy = x - cx, y - cy
    dist_sq = dx*dx + dy*dy

    # Check if within ring
    if dist_sq >= r_inner*r_inner and dist_sq <= r_outer*r_outer:
        # Limit to upper-right quadrant (arc shape)
        if dx >= -2 and dy <= 2:
            return True
    return False


def block_pattern(x, y):
    """Block Tool - filled rectangle with outline"""
    # Outer border
    if x >= 6 and x <= 25 and y >= 6 and y <= 25:
        # Border (2 pixels)
        if x <= 7 or x >= 24 or y <= 7 or y >= 24:
            return True
        # Inner filled square
        if x >= 12 and x <= 19 and y >= 12 and y <= 19:
            return True
    return False


def over_pattern(x, y):
    """Over Mode - black shape over checkerboard showing opaque copy"""
    # Background checkerboard in lower-right
    if x >= 10 and y >= 10:
        if (x + y) % 2 == 0:
            # Foreground black triangle in upper-left area
            if x + y <= 28:
                return True
            return True  # checkerboard
    # Solid black triangle in upper portion
    if x + y <= 22 and x >= 4 and y >= 4:
        return True
    return False


def under_pattern(x, y):
    """Under Mode - shape painted behind existing"""
    # This shows black appearing only where destination is white
    # Gray background (checkerboard) with black showing through
    if x >= 4 and x <= 27 and y >= 4 and y <= 27:
        # Checkerboard background
        is_check = (x + y) % 2 == 0
        # Black shape in one area
        if x <= 18 and y <= 18:
            if x + y >= 10:
                return True
        # Show checkerboard elsewhere
        if x >= 14 or y >= 14:
            return is_check
    return False


def reverse_pattern(x, y):
    """Reverse Mode - hourglass/bowtie XOR effect"""
    # Two triangles meeting at center (hourglass shape)
    cx, cy = 16, 16

    # Left triangle pointing right
    if x <= 16:
        dist_from_center = abs(y - cy)
        width_at_y = 16 - x
        if dist_from_center <= width_at_y * 0.6 and x >= 4:
            return True

    # Right triangle pointing left
    if x >= 16:
        dist_from_center = abs(y - cy)
        width_at_y = x - 16
        if dist_from_center <= width_at_y * 0.6 and x <= 28:
            return True

    return False


def erase_pattern(x, y):
    """Erase Mode - shape being erased (dashed outline with arrow)"""
    # Dashed rectangle outline
    if x >= 4 and x <= 24 and y >= 8 and y <= 24:
        on_edge = (x <= 6 or x >= 22 or y <= 10 or y >= 22)
        if on_edge:
            # Dashed pattern
            if ((x + y) // 3) % 2 == 0:
                return True

    # Arrow pointing away (indicating removal)
    if x >= 18 and x <= 28 and y >= 4 and y <= 14:
        # Arrow shaft
        if y >= 8 and y <= 10 and x <= 26:
            return True
        # Arrow head
        if x >= 24:
            if abs(y - 9) <= (28 - x):
                return True
    return False


def in_pattern(x, y):
    """File In - left arrow (retrieve/load)"""
    cx = 16
    cy = 16

    # Arrow pointing LEFT
    # Shaft
    if y >= 12 and y <= 20 and x >= 14 and x <= 28:
        return True

    # Arrow head (pointing left)
    if x >= 4 and x <= 16:
        dist_from_center = abs(y - cy)
        max_dist = (16 - x) * 0.7
        if dist_from_center <= max_dist:
            return True

    return False


def magnify_pattern(x, y):
    """Magnify - filled square in corner representing pixel editing"""
    # Large filled black square in bottom-right corner
    if x >= 8 and x <= 28 and y >= 8 and y <= 28:
        return True
    return False


def white_pattern(x, y):
    """White Tone - empty square (border only with inner white)"""
    # Outer border
    if x >= 4 and x <= 27 and y >= 4 and y <= 27:
        # Just the border frame
        if x <= 6 or x >= 25 or y <= 6 or y >= 25:
            return True
        # Inner is empty (white)
    return False


def lightgray_pattern(x, y):
    """Light Gray Tone - square with sparse halftone pattern"""
    # Border
    if x >= 4 and x <= 27 and y >= 4 and y <= 27:
        if x <= 6 or x >= 25 or y <= 6 or y >= 25:
            return True
        # Light gray: 25% black (every 4th pixel in a pattern)
        if x >= 8 and x <= 23 and y >= 8 and y <= 23:
            if x % 4 == 0 and y % 4 == 0:
                return True
    return False


def gray_pattern(x, y):
    """Gray Tone - square with checkerboard pattern"""
    # Border
    if x >= 4 and x <= 27 and y >= 4 and y <= 27:
        if x <= 6 or x >= 25 or y <= 6 or y >= 25:
            return True
        # Gray: 50% checkerboard
        if x >= 8 and x <= 23 and y >= 8 and y <= 23:
            if (x + y) % 2 == 0:
                return True
    return False


def darkgray_pattern(x, y):
    """Dark Gray Tone - square with dense halftone pattern"""
    # Border
    if x >= 4 and x <= 27 and y >= 4 and y <= 27:
        if x <= 6 or x >= 25 or y <= 6 or y >= 25:
            return True
        # Dark gray: 75% black (every 4th pixel is white)
        if x >= 8 and x <= 23 and y >= 8 and y <= 23:
            if not (x % 4 == 2 and y % 4 == 2):
                return True
    return False


def black_pattern(x, y):
    """Black Tone - solid black square"""
    # Solid filled square
    if x >= 4 and x <= 27 and y >= 4 and y <= 27:
        return True
    return False


def xgrid_pattern(x, y):
    """X Grid - horizontal line at bottom (horizontal constraint)"""
    # Border
    if x >= 4 and x <= 27 and y >= 4 and y <= 27:
        if x <= 6 or x >= 25 or y <= 6 or y >= 25:
            return True

    # Horizontal line at bottom (underscore shape)
    if x >= 8 and x <= 23 and y >= 22 and y <= 24:
        return True

    return False


def ygrid_pattern(x, y):
    """Y Grid - vertical line on left (vertical constraint)"""
    # Border
    if x >= 4 and x <= 27 and y >= 4 and y <= 27:
        if x <= 6 or x >= 25 or y <= 6 or y >= 25:
            return True

    # Vertical line on left
    if x >= 8 and x <= 10 and y >= 8 and y <= 23:
        return True

    return False


def togglegrids_pattern(x, y):
    """Toggle Grids - grid with 4 quadrants"""
    # Border
    if x >= 4 and x <= 27 and y >= 4 and y <= 27:
        if x <= 6 or x >= 25 or y <= 6 or y >= 25:
            return True

    # Cross dividing into 4 quadrants
    if x >= 8 and x <= 23 and y >= 8 and y <= 23:
        # Vertical divider
        if x >= 14 and x <= 17:
            return True
        # Horizontal divider
        if y >= 14 and y <= 17:
            return True

    return False


def out_pattern(x, y):
    """File Out - right arrow (save)"""
    cx = 16
    cy = 16

    # Arrow pointing RIGHT
    # Shaft
    if y >= 12 and y <= 20 and x >= 4 and x <= 18:
        return True

    # Arrow head (pointing right)
    if x >= 16 and x <= 28:
        dist_from_center = abs(y - cy)
        max_dist = (x - 16) * 0.7
        if dist_from_center <= max_dist:
            return True

    return False


def borderform_pattern(x, y, width=16, height=16):
    """Border Form - simple border pattern (tileable)"""
    # Simple line pattern for borders
    return y < 2


def specialborderform_pattern(x, y, width=16, height=16):
    """Special Border Form - emphasized/selected border pattern"""
    # Thicker or dashed pattern for selected borders
    if y < 4:
        if y < 2:
            return True
        else:
            return (x % 2) == 0
    return False


# ============================================================================
# Main Generation
# ============================================================================

def main():
    # Output directory
    output_dir = os.path.join(os.path.dirname(__file__), '..', 'forms')
    os.makedirs(output_dir, exist_ok=True)

    # Define all icons with their patterns
    # Note: Original Smalltalk-80 used trailing periods (e.g., 'select.form.')
    # but Windows doesn't support filenames ending with periods, so we omit them.
    icons = [
        # Tools (Row 1)
        ('select.form', select_pattern),
        ('singlecopy.form', singlecopy_pattern),
        ('repeatcopy.form', repeatcopy_pattern),
        ('line.form', line_pattern),
        ('curve.form', curve_pattern),
        ('block.form', block_pattern),

        # Modes (Row 1 continued)
        ('over.form', over_pattern),
        ('under.form', under_pattern),
        ('reverse.form', reverse_pattern),
        ('erase.form', erase_pattern),

        # File I/O
        ('in.form', in_pattern),
        ('out.form', out_pattern),

        # Editor
        ('magnify.form', magnify_pattern),

        # Colors (Row 2)
        ('white.form', white_pattern),
        ('lightgray.form', lightgray_pattern),
        ('gray.form', gray_pattern),
        ('darkgray.form', darkgray_pattern),
        ('black.form', black_pattern),

        # Grid controls
        ('xgrid.form', xgrid_pattern),
        ('ygrid.form', ygrid_pattern),
        ('togglegrids.form', togglegrids_pattern),
    ]

    # Generate all 32x32 icons
    for filename, pattern_func in icons:
        rows = create_icon_32x32(pattern_func)
        filepath = os.path.join(output_dir, filename)
        write_form_file(filepath, 32, 32, 0, 0, rows)

    # Generate border forms (16x16 tileable patterns)
    print("\nGenerating border forms...")

    # borderform.form
    rows = []
    for y in range(16):
        row = ''
        for x in range(16):
            row += '1' if borderform_pattern(x, y) else '0'
        rows.append(row)
    write_form_file(os.path.join(output_dir, 'borderform.form'), 16, 16, 0, 0, rows)

    # specialborderform.form
    rows = []
    for y in range(16):
        row = ''
        for x in range(16):
            row += '1' if specialborderform_pattern(x, y) else '0'
        rows.append(row)
    write_form_file(os.path.join(output_dir, 'specialborderform.form'), 16, 16, 0, 0, rows)

    print(f"\nAll form files generated in: {output_dir}")
    print(f"Total files: {len(icons) + 2}")


if __name__ == '__main__':
    main()
