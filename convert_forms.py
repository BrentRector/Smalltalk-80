#!/usr/bin/env python3
"""
Convert high-resolution .form.png screen captures to 32x32 1-bit BMP files.

Special handling for:
- Halftone patterns (gray, darkgray, lightgray)
- Framed icons (white, black, togglegrids)
- Arrow icons (in, out)
"""

import os
from PIL import Image
import numpy as np
from pathlib import Path


# Known halftone pattern files (use fixed frame position)
HALFTONE_FILES = {
    'gray.form.png': 'medium',
    'darkgray.form.png': 'dark',
    'lightgray.form.png': 'light',
}

# Framed icons (compute margin from source)
FRAMED_FILES = {
    'white.form.png': 'white',
    'black.form.png': 'black',
    'togglegrids.form.png': 'grid',
}

# Arrow icons (recreate programmatically)
ARROW_FILES = {
    'in.form.png': 'right',   # Arrow pointing right
    'out.form.png': 'left',   # Arrow pointing left
}


def find_border_rect(img_array, threshold=128):
    """Find the bounding rectangle of black content."""
    binary = img_array < threshold
    rows_with_black = np.any(binary, axis=1)
    cols_with_black = np.any(binary, axis=0)
    rows_indices = np.where(rows_with_black)[0]
    cols_indices = np.where(cols_with_black)[0]
    if len(rows_indices) == 0 or len(cols_indices) == 0:
        return None
    return (cols_indices[0], rows_indices[0], cols_indices[-1], rows_indices[-1])


def find_inner_border(img_array, outer_rect, threshold=128):
    """Find margin ratio from outer border to inner border."""
    left, top, right, bottom = outer_rect
    height = bottom - top + 1

    border_end = top
    for y in range(top + 1, top + height // 4):
        row = img_array[y, left:right+1]
        black_ratio = np.sum(row < threshold) / len(row)
        if black_ratio < 0.3:
            border_end = y
            break

    inner_border_start = None
    for y in range(border_end, top + height // 2):
        row = img_array[y, left:right+1]
        black_ratio = np.sum(row < threshold) / len(row)
        if black_ratio > 0.5:
            inner_border_start = y
            break

    if inner_border_start is None:
        return None

    return (inner_border_start - top) / height


def create_halftone_icon(pattern_type):
    """Create halftone icon with FIXED frame position (consistent across all halftones)."""
    output = np.ones((32, 32), dtype=np.uint8) * 255

    # Fixed frame position: margin of 5 pixels
    inner_pos = 5
    inner_end = 26

    # Draw inner frame
    output[inner_pos, inner_pos:inner_end+1] = 0
    output[inner_end, inner_pos:inner_end+1] = 0
    output[inner_pos:inner_end+1, inner_pos] = 0
    output[inner_pos:inner_end+1, inner_end] = 0

    # Fill area
    fill_start = inner_pos + 1
    fill_end = inner_end

    if pattern_type == 'medium':
        # 50% checkerboard
        for y in range(fill_start, fill_end):
            for x in range(fill_start, fill_end):
                if (x + y) % 2 == 0:
                    output[y, x] = 0
    elif pattern_type == 'dark':
        # 75% black
        for y in range(fill_start, fill_end):
            for x in range(fill_start, fill_end):
                if (x - fill_start) % 2 == 1 and (y - fill_start) % 2 == 1:
                    output[y, x] = 255
                else:
                    output[y, x] = 0
    elif pattern_type == 'light':
        # 25% black
        for y in range(fill_start, fill_end):
            for x in range(fill_start, fill_end):
                if (x - fill_start) % 2 == 0 and (y - fill_start) % 2 == 0:
                    output[y, x] = 0
                else:
                    output[y, x] = 255

    # Outer border
    output[0, :] = 0
    output[31, :] = 0
    output[:, 0] = 0
    output[:, 31] = 0

    return output


def create_framed_icon(fill_type, img_array):
    """Create framed icon, computing margin from source."""
    output = np.ones((32, 32), dtype=np.uint8) * 255

    outer_rect = find_border_rect(img_array)
    if outer_rect is None:
        return None

    margin_ratio = find_inner_border(img_array, outer_rect)
    if margin_ratio is None:
        margin_ratio = 0.15

    inner_pos = max(2, int(32 * margin_ratio))
    inner_end = 31 - inner_pos

    # Draw inner frame
    output[inner_pos, inner_pos:inner_end+1] = 0
    output[inner_end, inner_pos:inner_end+1] = 0
    output[inner_pos:inner_end+1, inner_pos] = 0
    output[inner_pos:inner_end+1, inner_end] = 0

    # Fill
    if fill_type == 'black':
        output[inner_pos+1:inner_end, inner_pos+1:inner_end] = 0
    elif fill_type == 'grid':
        interior_size = inner_end - inner_pos - 1
        center = inner_pos + interior_size // 2
        output[center, inner_pos:inner_end+1] = 0
        output[inner_pos:inner_end+1, center] = 0

    # Outer border
    output[0, :] = 0
    output[31, :] = 0
    output[:, 0] = 0
    output[:, 31] = 0

    return output


def create_arrow_icon(direction):
    """Create arrow icon programmatically."""
    output = np.ones((32, 32), dtype=np.uint8) * 255

    # Arrow dimensions - centered in 30x30 interior
    # Arrow spans from about x=8 to x=24, vertically centered

    if direction == 'right':
        # Right-pointing arrow (for "in")
        # Horizontal bar from left, then triangular head pointing right
        # Triangle tip at right, base on left side of triangle

        # Draw filled right-pointing arrow
        # Center vertically at y=15-16
        center_y = 15

        # Arrow body (horizontal bar)
        for y in range(center_y - 3, center_y + 4):
            for x in range(8, 18):
                output[y, x] = 0

        # Arrow head (triangle pointing right)
        # Tip at x=25, base from y=6 to y=25 at x=17
        for y in range(6, 26):
            # Width decreases as we go right
            dist_from_center = abs(y - center_y)
            # At x=17, full height. At x=25, point
            for x in range(17, 26):
                max_dist = (25 - x) * 10 // 8  # Linear taper
                if dist_from_center <= max_dist:
                    output[y, x] = 0

    elif direction == 'left':
        # Left-pointing arrow (for "out")
        center_y = 15

        # Arrow body (horizontal bar)
        for y in range(center_y - 3, center_y + 4):
            for x in range(14, 24):
                output[y, x] = 0

        # Arrow head (triangle pointing left)
        # Tip at x=6, base at x=15
        for y in range(6, 26):
            dist_from_center = abs(y - center_y)
            for x in range(6, 15):
                max_dist = (x - 6) * 10 // 8
                if dist_from_center <= max_dist:
                    output[y, x] = 0

    # Outer border
    output[0, :] = 0
    output[31, :] = 0
    output[:, 0] = 0
    output[:, 31] = 0

    return output


def process_regular_image(img_array, target_size=32):
    """Process a regular image by scaling."""
    rect = find_border_rect(img_array)
    if rect is None:
        return None

    left, top, right, bottom = rect
    src_width = right - left + 1
    src_height = bottom - top + 1
    scale = max(src_width, src_height) / target_size
    border_skip = int(scale * 1.0)

    interior_left = left + border_skip
    interior_top = top + border_skip
    interior_right = right - border_skip
    interior_bottom = bottom - border_skip

    if interior_right <= interior_left or interior_bottom <= interior_top:
        interior_left, interior_top = left, top
        interior_right, interior_bottom = right, bottom

    interior = img_array[interior_top:interior_bottom+1, interior_left:interior_right+1]
    src_h, src_w = interior.shape

    if src_h == 0 or src_w == 0:
        return None

    interior_target = target_size - 2
    size = max(src_h, src_w)
    square = np.ones((size, size), dtype=np.uint8) * 255
    y_offset = (size - src_h) // 2
    x_offset = (size - src_w) // 2
    square[y_offset:y_offset+src_h, x_offset:x_offset+src_w] = interior

    interior_scale = size / interior_target
    interior_output = np.ones((interior_target, interior_target), dtype=np.uint8) * 255

    for ty in range(interior_target):
        for tx in range(interior_target):
            src_x1 = int(tx * interior_scale)
            src_y1 = int(ty * interior_scale)
            src_x2 = min(int((tx + 1) * interior_scale), size)
            src_y2 = min(int((ty + 1) * interior_scale), size)
            if src_x2 <= src_x1:
                src_x2 = src_x1 + 1
            if src_y2 <= src_y1:
                src_y2 = src_y1 + 1

            region = square[src_y1:src_y2, src_x1:src_x2]
            if np.mean(region) < 128:
                interior_output[ty, tx] = 0

    output_array = np.ones((target_size, target_size), dtype=np.uint8) * 255
    output_array[1:target_size-1, 1:target_size-1] = interior_output

    # Outer border
    output_array[0, :] = 0
    output_array[target_size-1, :] = 0
    output_array[:, 0] = 0
    output_array[:, target_size-1] = 0

    return output_array


def process_form_image(input_path, output_path, target_size=32):
    """Process a single form image."""
    filename = input_path.name
    print(f"Processing: {filename}")

    if filename in HALFTONE_FILES:
        pattern_type = HALFTONE_FILES[filename]
        print(f"  Halftone: {pattern_type}")
        output_array = create_halftone_icon(pattern_type)
    elif filename in FRAMED_FILES:
        fill_type = FRAMED_FILES[filename]
        print(f"  Framed: {fill_type}")
        img = Image.open(input_path).convert('L')
        img_array = np.array(img)
        output_array = create_framed_icon(fill_type, img_array)
    elif filename in ARROW_FILES:
        direction = ARROW_FILES[filename]
        print(f"  Arrow: {direction}")
        output_array = create_arrow_icon(direction)
    else:
        print(f"  Regular")
        img = Image.open(input_path).convert('L')
        img_array = np.array(img)
        output_array = process_regular_image(img_array, target_size)

    if output_array is None:
        print(f"  WARNING: Could not process")
        return False

    output_img = Image.fromarray(output_array, mode='L')
    output_img = output_img.convert('1')
    output_img.save(output_path, 'BMP')
    print(f"  Saved: {output_path.name}")

    return True


def main():
    input_dir = Path('/home/user/Smalltalk-80/images')
    output_dir = Path('/home/user/Smalltalk-80/forms')
    output_dir.mkdir(exist_ok=True)

    form_files = list(input_dir.glob('*.form.png'))
    print(f"Found {len(form_files)} .form.png files to process\n")

    success_count = 0
    for input_path in sorted(form_files):
        output_name = input_path.stem + '.bmp'
        output_path = output_dir / output_name
        if process_form_image(input_path, output_path):
            success_count += 1
        print()

    print(f"Processed {success_count}/{len(form_files)} files successfully")


if __name__ == '__main__':
    main()
