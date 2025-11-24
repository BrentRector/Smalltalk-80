#!/usr/bin/env python3
"""
Extract individual figures from Bluebook Chapter 18 page screenshots.
Crops specific regions containing figures and saves them as separate files.
"""

from PIL import Image
import os

# Base paths
images_dir = "/home/user/Smalltalk-80/images"
figures_dir = "/home/user/Smalltalk-80/docs/figures"

# Figure extraction definitions: (source_file, crop_box, output_name)
# crop_box is (left, top, right, bottom) in pixels
figures = [
    # Class Hierarchy diagram (chapter opener page)
    ("Screenshot 2025-11-23 132155.png", (0, 100, 1280, 700), "Figure 18-0 Class Hierarchy.png"),

    # Figure 18.1 - Display screen view (page 332)
    ("Screenshot 2025-11-23 132210.png", (0, 150, 1280, 850), "Figure 18-1.png"),

    # Figure 18.2 - Man shape (appears in text description, not as standalone figure on page 331)
    # This figure is embedded in the text on page 132202, but let's extract the relevant diagram area
    ("Screenshot 2025-11-23 132202.png", (50, 400, 600, 900), "Figure 18-2.png"),

    # Figure 18.3 - Text copying (page 335 top)
    ("Screenshot 2025-11-23 132235.png", (0, 150, 900, 620), "Figure 18-3.png"),

    # Figure 18.4 - Clipping rectangle (page 335 bottom)
    ("Screenshot 2025-11-23 132235.png", (0, 670, 900, 920), "Figure 18-4.png"),

    # Figure 18.5 - Halftone modes (page 336)
    ("Screenshot 2025-11-23 132243.png", (0, 200, 1280, 750), "Figure 18-5.png"),

    # Figure 18.6 - Combination rule diagram (page 337 top)
    ("Screenshot 2025-11-23 132252.png", (150, 280, 550, 620), "Figure 18-6.png"),

    # Figure 18.7 - Combination rules examples (page 337 bottom)
    ("Screenshot 2025-11-23 132252.png", (0, 650, 1100, 930), "Figure 18-7.png"),

    # Figure 18.8 - Form structure (page 339)
    ("Screenshot 2025-11-23 132308.png", (50, 100, 700, 600), "Figure 18-8.png"),

    # Figure 18.9 - Coordinate system (page 341/342)
    ("Screenshot 2025-11-23 132330.png", (0, 100, 800, 700), "Figure 18-9.png"),

    # Figure 18.10 - Rectangle positions (page 344)
    ("Screenshot 2025-11-23 132350.png", (50, 100, 800, 650), "Figure 18-10.png"),

    # Figure 18.11 - Three rectangles (page 347)
    ("Screenshot 2025-11-23 132404.png", (0, 100, 1000, 850), "Figure 18-11.png"),

    # Figure 18.12 - Pen shapes and lines (page 353)
    ("Screenshot 2025-11-23 132449.png", (100, 150, 1200, 900), "Figure 18-12.png"),
]

def extract_figures():
    """Extract all figures from source screenshots."""
    print(f"Extracting {len(figures)} figures...")

    for source_file, crop_box, output_name in figures:
        source_path = os.path.join(images_dir, source_file)
        output_path = os.path.join(figures_dir, output_name)

        try:
            # Open source image
            img = Image.open(source_path)

            # Crop to figure region
            figure_img = img.crop(crop_box)

            # Save as PNG
            figure_img.save(output_path, "PNG")
            print(f"✓ Extracted: {output_name}")

        except Exception as e:
            print(f"✗ Error extracting {output_name}: {e}")

    print(f"\nDone! Figures saved to {figures_dir}")

if __name__ == "__main__":
    extract_figures()
