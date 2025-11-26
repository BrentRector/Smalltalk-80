#!/usr/bin/env python3
"""Fix the form BMP files based on feedback."""

from PIL import Image
from pathlib import Path

FORMS_DIR = Path('/home/user/Smalltalk-80/forms')

def create_32x32_1bit():
    """Create a new 32x32 1-bit image (white background)."""
    return Image.new('1', (32, 32), 1)

def draw_outer_border(img):
    """Draw the outer black border (1 pixel wide)."""
    pixels = img.load()
    for x in range(32):
        pixels[x, 0] = 0   # Top row
        pixels[x, 31] = 0  # Bottom row
    for y in range(32):
        pixels[0, y] = 0   # Left column
        pixels[31, y] = 0  # Right column

def get_triangle_pixels(apex_x, apex_y, top_x, top_y, bottom_x, bottom_y):
    """Get all pixels inside a triangle defined by apex and two other vertices."""
    pixels = set()
    min_y = min(apex_y, top_y, bottom_y)
    max_y = max(apex_y, top_y, bottom_y)

    for y in range(min_y, max_y + 1):
        # Find x range for this row
        x_intersections = []

        # Check each edge
        edges = [(apex_x, apex_y, top_x, top_y),
                 (top_x, top_y, bottom_x, bottom_y),
                 (bottom_x, bottom_y, apex_x, apex_y)]

        for x1, y1, x2, y2 in edges:
            if y1 == y2:
                if y == y1:
                    x_intersections.extend([min(x1, x2), max(x1, x2)])
            elif min(y1, y2) <= y <= max(y1, y2):
                # Line intersection
                t = (y - y1) / (y2 - y1)
                x = x1 + t * (x2 - x1)
                x_intersections.append(x)

        if len(x_intersections) >= 2:
            x_min = int(min(x_intersections))
            x_max = int(max(x_intersections))
            for x in range(x_min, x_max + 1):
                pixels.add((x, y))

    return pixels

def fix_erase():
    """Fix erase.form - outlined white triangle overlapping black rectangle."""
    img = create_32x32_1bit()
    pixels = img.load()
    draw_outer_border(img)

    # Black rectangle (filled)
    rect_left, rect_right = 15, 25
    rect_top, rect_bottom = 7, 25

    for y in range(rect_top, rect_bottom + 1):
        for x in range(rect_left, rect_right + 1):
            pixels[x, y] = 0

    # Triangle: apex at left, opening to right
    # Apex at (5, 16), top-right at (25, 7), bottom-right at (25, 25)
    apex_x, apex_y = 5, 16
    tr_x, tr_y = 25, 7
    br_x, br_y = 25, 25

    # Draw white outlined triangle using Bresenham-style line drawing
    def draw_line(x1, y1, x2, y2):
        """Draw a white line from (x1,y1) to (x2,y2)."""
        dx = abs(x2 - x1)
        dy = abs(y2 - y1)
        sx = 1 if x1 < x2 else -1
        sy = 1 if y1 < y2 else -1
        err = dx - dy

        x, y = x1, y1
        while True:
            pixels[x, y] = 1  # White
            if x == x2 and y == y2:
                break
            e2 = 2 * err
            if e2 > -dy:
                err -= dy
                x += sx
            if e2 < dx:
                err += dx
                y += sy

    # Draw the three edges of the triangle in white
    draw_line(apex_x, apex_y, tr_x, tr_y)      # Apex to top-right
    draw_line(apex_x, apex_y, br_x, br_y)      # Apex to bottom-right
    draw_line(tr_x, tr_y, br_x, br_y)          # Top-right to bottom-right (vertical)

    img.save(FORMS_DIR / 'erase.form.bmp')
    print("Fixed erase.form.bmp")

def fix_lightgray():
    """Fix lightgray.form - grid centered horizontally, touching top/bottom but not left/right."""
    img = create_32x32_1bit()
    pixels = img.load()
    draw_outer_border(img)

    # Inner border box
    inner_left = 4
    inner_right = 27
    inner_top = 5
    inner_bottom = 26

    # Draw inner border (black rectangle outline)
    for x in range(inner_left, inner_right + 1):
        pixels[x, inner_top] = 0      # Top edge
        pixels[x, inner_bottom] = 0   # Bottom edge
    for y in range(inner_top, inner_bottom + 1):
        pixels[inner_left, y] = 0     # Left edge
        pixels[inner_right, y] = 0    # Right edge

    # Grid area: inside the inner border
    # Grid should touch top and bottom inner edges but NOT touch left/right
    grid_top = inner_top + 1      # Row 6
    grid_bottom = inner_bottom - 1  # Row 25

    # Interior area: columns 5 to 26 (22 pixels wide)
    # We want gaps on left and right sides so grid doesn't touch the inner border
    # Add 2 pixel gaps on each side for visual separation
    grid_left = inner_left + 3   # Column 7 - clear gap from left border
    grid_right = inner_right - 3  # Column 24 - clear gap from right border

    # Draw sparse halftone grid pattern (every other row, every other column)
    for y in range(grid_top, grid_bottom + 1):
        for x in range(grid_left, grid_right + 1):
            # Sparse pattern: black dots on even rows at even columns only
            if (y - grid_top) % 2 == 0 and (x - grid_left) % 2 == 0:
                pixels[x, y] = 0

    img.save(FORMS_DIR / 'lightgray.form.bmp')
    print("Fixed lightgray.form.bmp")

def fix_over():
    """Fix over.form - half-tone triangle overlapping black rectangle with consistent pattern."""
    img = create_32x32_1bit()
    pixels = img.load()
    draw_outer_border(img)

    # First: Draw black rectangle (destination)
    rect_left, rect_right = 15, 25
    rect_top, rect_bottom = 7, 25

    for y in range(rect_top, rect_bottom + 1):
        for x in range(rect_left, rect_right + 1):
            pixels[x, y] = 0

    # Triangle: apex at left, opening to right
    apex_x, apex_y = 5, 16
    tr_x, tr_y = 25, 7
    br_x, br_y = 25, 25

    # Get all triangle pixels
    triangle_pixels = get_triangle_pixels(apex_x, apex_y, tr_x, tr_y, br_x, br_y)

    # Draw halftone pattern over the triangle area (this is the "over" operation)
    # The halftone should be consistent throughout the triangle, including overlap
    for (x, y) in triangle_pixels:
        # Consistent checkerboard pattern based on absolute coordinates
        if (x + y) % 2 == 0:
            pixels[x, y] = 0  # Black
        else:
            pixels[x, y] = 1  # White

    img.save(FORMS_DIR / 'over.form.bmp')
    print("Fixed over.form.bmp")

def fix_reverse():
    """Fix reverse.form - black triangle and rectangle with white overlap area."""
    img = create_32x32_1bit()
    pixels = img.load()
    draw_outer_border(img)

    # Black rectangle (destination)
    rect_left, rect_right = 15, 25
    rect_top, rect_bottom = 7, 25

    # Triangle: apex at left, opening to right
    apex_x, apex_y = 5, 16
    tr_x, tr_y = 25, 7
    br_x, br_y = 25, 25

    # Get triangle pixels
    triangle_pixels = get_triangle_pixels(apex_x, apex_y, tr_x, tr_y, br_x, br_y)

    # Get rectangle pixels
    rect_pixels = set()
    for y in range(rect_top, rect_bottom + 1):
        for x in range(rect_left, rect_right + 1):
            rect_pixels.add((x, y))

    # XOR logic:
    # - Triangle only (not in rect): black
    # - Rectangle only (not in triangle): black
    # - Both (overlap): white

    for (x, y) in triangle_pixels:
        if (x, y) in rect_pixels:
            pixels[x, y] = 1  # White (overlap - XOR of black and black)
        else:
            pixels[x, y] = 0  # Black (triangle only)

    for (x, y) in rect_pixels:
        if (x, y) not in triangle_pixels:
            pixels[x, y] = 0  # Black (rectangle only)

    img.save(FORMS_DIR / 'reverse.form.bmp')
    print("Fixed reverse.form.bmp")

def fix_select():
    """Fix select.form - equal length bars on all four rectangles."""
    img = create_32x32_1bit()
    pixels = img.load()
    draw_outer_border(img)

    # Selection marquee with four corner brackets
    # Two L-shaped corners (top-left and bottom-right)
    # All bars should be equal length

    bar_length = 10  # Length of each bar
    bar_width = 3    # Width/thickness of each bar

    # Top-left corner bracket (L-shape)
    # Horizontal bar: from (5, 7) going right
    # Vertical bar: from (5, 7) going down
    tl_x, tl_y = 5, 7

    # Top horizontal bar
    for y in range(tl_y, tl_y + bar_width):
        for x in range(tl_x, tl_x + bar_length):
            pixels[x, y] = 0

    # Left vertical bar
    for y in range(tl_y, tl_y + bar_length):
        for x in range(tl_x, tl_x + bar_width):
            pixels[x, y] = 0

    # Bottom-right corner bracket (inverted L-shape)
    # Position: ends at (25, 25)
    br_x, br_y = 25, 25  # Bottom-right corner point

    # Bottom horizontal bar: ends at br_x, br_y
    for y in range(br_y - bar_width + 1, br_y + 1):
        for x in range(br_x - bar_length + 1, br_x + 1):
            pixels[x, y] = 0

    # Right vertical bar: ends at br_x, br_y
    for y in range(br_y - bar_length + 1, br_y + 1):
        for x in range(br_x - bar_width + 1, br_x + 1):
            pixels[x, y] = 0

    img.save(FORMS_DIR / 'select.form.bmp')
    print("Fixed select.form.bmp")

if __name__ == '__main__':
    fix_erase()
    fix_lightgray()
    fix_over()
    fix_reverse()
    fix_select()
    print("\nAll forms fixed!")
