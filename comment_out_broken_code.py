#!/usr/bin/env python3
"""
Comment out all lines that reference _blockContextMapping or _blockClosureCache.
This is a simple line-by-line approach to make the code compile.
"""

import re

# Read the file
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Track changes
commented_count = 0

# Process each line
for i in range(len(lines)):
    line = lines[i]

    # Skip if already commented
    if line.strip().startswith('//'):
        continue

    # Check if line references the obsolete fields
    if '_blockContextMapping' in line or '_blockClosureCache' in line:
        # Comment out the line
        indent = len(line) - len(line.lstrip())
        lines[i] = ' ' * indent + '// OBSOLETE: ' + line.lstrip()
        commented_count += 1
        print(f"Line {i+1}: {line.strip()[:80]}")

# Write the file back
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'w', encoding='utf-8') as f:
    f.writelines(lines)

print(f"\n{'='*60}")
print(f"Done! Commented out {commented_count} lines.")
print(f"{'='*60}")
