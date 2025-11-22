#!/usr/bin/env python3
"""
Converts Smalltalk-80 Form files to Windows BMP format.

Reads Form files (as created by extract_arrow_bitmaps.py) and outputs
standard Windows bitmap (.bmp) files that can be viewed with any image viewer.
"""

import sys
import os
import struct

def read_word(data, offset):
    """Read big-endian 16-bit word."""
    return (data[offset] << 8) | data[offset + 1]

def signed_int16(value):
    """Convert unsigned 16-bit to signed (two's complement)."""
    if value > 32767:
        return value - 65536
    return value

def read_form_file(filename):
    """Read and parse a Smalltalk-80 Form file."""
    with open(filename, 'rb') as f:
        data = f.read()

    # Read header
    fileCode = read_word(data, 0)
    if fileCode != 1:
        raise ValueError(f"Invalid Form file: fileCode = {fileCode}, expected 1")

    width = read_word(data, 2)
    height = read_word(data, 4)
    offsetX = signed_int16(read_word(data, 6))
    offsetY = signed_int16(read_word(data, 8))

    # Calculate bitmap size
    word_width = (width + 15) // 16
    expected_words = word_width * height

    # Read bitmap data (column-major)
    bitmap_data = []
    pos = 10
    for i in range(expected_words):
        if pos + 1 < len(data):
            word = read_word(data, pos)
            bitmap_data.append(word)
            pos += 2
        else:
            break

    return {
        'width': width,
        'height': height,
        'offsetX': offsetX,
        'offsetY': offsetY,
        'word_width': word_width,
        'bitmap_data': bitmap_data
    }

def form_to_bmp(form_data):
    """Convert Form data to BMP format (monochrome)."""
    width = form_data['width']
    height = form_data['height']
    word_width = form_data['word_width']
    bitmap_data = form_data['bitmap_data']

    # Convert column-major to row-major
    # Form stores: for each word-column, all rows
    # BMP needs: for each row (bottom-to-top), all pixels

    pixels = []
    for row in range(height):
        row_pixels = []
        for col in range(word_width):
            # Get the word for this column and row
            word_index = col * height + row
            if word_index < len(bitmap_data):
                word = bitmap_data[word_index]

                # Extract 16 bits (MSB first)
                for bit in range(15, -1, -1):
                    pixel_x = col * 16 + (15 - bit)
                    if pixel_x < width:
                        # 1 = black, 0 = white in Form
                        pixel_value = 1 if (word & (1 << bit)) != 0 else 0
                        row_pixels.append(pixel_value)

        pixels.append(row_pixels)

    # BMP format requirements
    # - Rows are stored bottom-to-top
    # - Each row is padded to 4-byte boundary
    # - For monochrome: 1 bit per pixel, packed into bytes

    bytes_per_row_unpacked = (width + 7) // 8  # How many bytes for width pixels
    bytes_per_row = ((bytes_per_row_unpacked + 3) // 4) * 4  # Padded to 4-byte boundary

    # Create BMP data (bottom-to-top)
    bmp_pixels = bytearray()
    for row in reversed(pixels):  # Bottom-to-top
        # Pack bits into bytes
        row_bytes = bytearray()
        for byte_idx in range(bytes_per_row_unpacked):
            byte_val = 0
            for bit_idx in range(8):
                pixel_idx = byte_idx * 8 + bit_idx
                if pixel_idx < len(row):
                    # BMP: 1 = white, 0 = black (opposite of Form)
                    # So invert the pixel value
                    if row[pixel_idx] == 0:  # Form white
                        byte_val |= (1 << (7 - bit_idx))  # BMP white
                    # else: leave as 0 for BMP black
            row_bytes.append(byte_val)

        # Pad to 4-byte boundary
        while len(row_bytes) < bytes_per_row:
            row_bytes.append(0)

        bmp_pixels.extend(row_bytes)

    return create_bmp(width, height, bmp_pixels)

def create_bmp(width, height, pixel_data):
    """Create a BMP file from pixel data."""
    # Calculate sizes
    bytes_per_row_unpacked = (width + 7) // 8
    bytes_per_row = ((bytes_per_row_unpacked + 3) // 4) * 4
    pixel_data_size = bytes_per_row * height

    # BMP file header (14 bytes)
    file_header_size = 14
    dib_header_size = 40
    color_table_size = 2 * 4  # 2 colors, 4 bytes each (RGBQUAD)
    headers_size = file_header_size + dib_header_size + color_table_size
    file_size = headers_size + pixel_data_size

    bmp = bytearray()

    # BMP File Header (14 bytes)
    bmp.extend(b'BM')                              # Signature
    bmp.extend(struct.pack('<I', file_size))       # File size
    bmp.extend(struct.pack('<H', 0))               # Reserved 1
    bmp.extend(struct.pack('<H', 0))               # Reserved 2
    bmp.extend(struct.pack('<I', headers_size))    # Pixel data offset

    # DIB Header (BITMAPINFOHEADER, 40 bytes)
    bmp.extend(struct.pack('<I', dib_header_size)) # Header size
    bmp.extend(struct.pack('<i', width))           # Width (signed)
    bmp.extend(struct.pack('<i', height))          # Height (signed, positive = bottom-up)
    bmp.extend(struct.pack('<H', 1))               # Planes (always 1)
    bmp.extend(struct.pack('<H', 1))               # Bits per pixel (1 = monochrome)
    bmp.extend(struct.pack('<I', 0))               # Compression (0 = none)
    bmp.extend(struct.pack('<I', pixel_data_size)) # Image size
    bmp.extend(struct.pack('<i', 2835))            # X pixels per meter (72 DPI)
    bmp.extend(struct.pack('<i', 2835))            # Y pixels per meter (72 DPI)
    bmp.extend(struct.pack('<I', 2))               # Colors in palette (2)
    bmp.extend(struct.pack('<I', 2))               # Important colors (2)

    # Color Table (2 colors, RGBQUAD format: Blue, Green, Red, Reserved)
    # Color 0 = Black (0, 0, 0)
    bmp.extend(struct.pack('<BBBB', 0, 0, 0, 0))
    # Color 1 = White (255, 255, 255)
    bmp.extend(struct.pack('<BBBB', 255, 255, 255, 0))

    # Pixel data
    bmp.extend(pixel_data)

    return bytes(bmp)

def convert_form_to_bmp(form_file, bmp_file):
    """Convert a Form file to BMP file."""
    print(f"Reading Form file: {form_file}")
    form_data = read_form_file(form_file)

    print(f"  Size: {form_data['width']}x{form_data['height']}")
    print(f"  Offset: ({form_data['offsetX']}, {form_data['offsetY']})")

    print(f"Converting to BMP...")
    bmp_data = form_to_bmp(form_data)

    print(f"Writing BMP file: {bmp_file}")
    with open(bmp_file, 'wb') as f:
        f.write(bmp_data)

    print(f"Done! Created {len(bmp_data)} byte BMP file")

def convert_directory(input_dir, output_dir):
    """Convert all .form files in a directory to .bmp files."""
    os.makedirs(output_dir, exist_ok=True)

    form_files = [f for f in os.listdir(input_dir) if f.endswith('.form')]

    if not form_files:
        print(f"No .form files found in {input_dir}")
        return

    print(f"Found {len(form_files)} Form file(s)")
    print()

    for form_file in sorted(form_files):
        input_path = os.path.join(input_dir, form_file)
        output_file = form_file.replace('.form', '.bmp')
        output_path = os.path.join(output_dir, output_file)

        try:
            convert_form_to_bmp(input_path, output_path)
            print()
        except Exception as e:
            print(f"Error converting {form_file}: {e}")
            print()

if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("Usage:")
        print("  form_to_bmp.py <form_file> [<bmp_file>]")
        print("    Convert a single Form file to BMP")
        print()
        print("  form_to_bmp.py --dir <input_dir> <output_dir>")
        print("    Convert all .form files in a directory")
        print()
        print("Examples:")
        print("  python3 form_to_bmp.py arrow1.form arrow1.bmp")
        print("  python3 form_to_bmp.py --dir ./arrow_output ./bmp_output")
        sys.exit(1)

    if sys.argv[1] == '--dir':
        if len(sys.argv) != 4:
            print("Error: --dir requires input_dir and output_dir")
            sys.exit(1)

        input_dir = sys.argv[2]
        output_dir = sys.argv[3]

        if not os.path.isdir(input_dir):
            print(f"Error: Input directory not found: {input_dir}")
            sys.exit(1)

        convert_directory(input_dir, output_dir)
    else:
        form_file = sys.argv[1]

        if not os.path.exists(form_file):
            print(f"Error: Form file not found: {form_file}")
            sys.exit(1)

        if len(sys.argv) > 2:
            bmp_file = sys.argv[2]
        else:
            bmp_file = form_file.replace('.form', '.bmp')

        convert_form_to_bmp(form_file, bmp_file)
