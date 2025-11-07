#!/usr/bin/env python3
"""
Comment out the final 5 lines that reference undefined variables.
"""

# Read the file
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Lines to comment (1-indexed in error messages, but 0-indexed in array)
lines_to_comment = [1710, 1808, 1812, 1912, 1916]  # Subtract 1 for 0-based indexing

for line_num in lines_to_comment:
    line = lines[line_num]
    if not line.strip().startswith('//'):
        indent = len(line) - len(line.lstrip())
        lines[line_num] = ' ' * indent + '// OBSOLETE: ' + line.lstrip()
        print(f"Line {line_num+1}: {line.strip()[:80]}")

# Write back
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'w', encoding='utf-8') as f:
    f.writelines(lines)

print("\n" + "="*60)
print("Done! Commented out 5 final error lines.")
print("="*60)
