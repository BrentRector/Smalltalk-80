#!/usr/bin/env python3
"""View converted form BMPs as ASCII art for verification."""

import os
from PIL import Image
from pathlib import Path

def show_image_ascii(img_path):
    """Display a 1-bit BMP as ASCII art."""
    img = Image.open(img_path)
    width, height = img.size

    # Convert to binary array
    pixels = img.load()

    print(f"\n{img_path.name} ({width}x{height}):")
    print("+" + "-" * width + "+")

    for y in range(height):
        row = "|"
        for x in range(width):
            pixel = pixels[x, y]
            # In 1-bit images, 0 is black, 255/1 is white
            if pixel == 0:
                row += "#"  # Black pixel
            else:
                row += " "  # White pixel
        row += "|"
        print(row)

    print("+" + "-" * width + "+")

def main():
    forms_dir = Path('/home/user/Smalltalk-80/forms')
    bmp_files = sorted(forms_dir.glob('*.form.bmp'))

    for bmp_path in bmp_files:
        show_image_ascii(bmp_path)

if __name__ == '__main__':
    main()
