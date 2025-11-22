#!/usr/bin/env python3
"""
Converts an octal dump (od output) to a binary file.

Takes the output from 'od -t oC' command and reconstructs the original binary file.
"""

import sys
import re

def parse_octal_dump(input_file, output_file):
    """Parse octal dump and write binary file."""
    bytes_data = []

    with open(input_file, 'r') as f:
        for line in f:
            line = line.strip()
            if not line:
                continue

            # Parse line: "0000000   000 036 236 036 001 021 ..."
            # Split on whitespace and skip the first column (offset)
            parts = line.split()

            if len(parts) > 1:
                # Skip the offset (first column)
                for octal_str in parts[1:]:
                    # Only process valid octal values (3 digits)
                    if re.match(r'^\d{3}$', octal_str):
                        # Convert octal string to byte
                        value = int(octal_str, 8)
                        bytes_data.append(value)

    print(f"Parsed {len(bytes_data)} bytes from octal dump")

    # Write binary file
    with open(output_file, 'wb') as f:
        f.write(bytes(bytes_data))

    print(f"Binary file written to: {output_file}")

if __name__ == '__main__':
    if len(sys.argv) != 3:
        print("Usage: convert_octal_dump.py <input_file> <output_file>")
        print("  input_file  - File containing octal dump text")
        print("  output_file - Output binary file")
        sys.exit(1)

    input_file = sys.argv[1]
    output_file = sys.argv[2]

    parse_octal_dump(input_file, output_file)
