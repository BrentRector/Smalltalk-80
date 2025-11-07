#!/usr/bin/env python3
"""
Find all remaining orphaned 'else' blocks and comment them out completely.
"""

import re

# Read the file
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Track changes
fixes = 0
i = 0
while i < len(lines):
    # Look for orphaned braces followed by else
    if (lines[i].strip() == '}' and
        i + 1 < len(lines) and
        'else' in lines[i+1] and
        not lines[i+1].strip().startswith('//')):

        # Check if the previous line was a commented-out if
        found_commented_if = False
        for j in range(max(0, i-50), i):
            if '// OBSOLETE: if (_blockContextMapping' in lines[j] or '// OBSOLETE: if (_blockClosureCache' in lines[j]:
                found_commented_if = True
                break

        if found_commented_if:
            # This is an orphaned else - comment it out
            indent = len(lines[i+1]) - len(lines[i+1].lstrip())
            lines[i+1] = ' ' * indent + '// OBSOLETE: ' + lines[i+1].lstrip()
            fixes += 1
            print(f"Line {i+2}: Commented out orphaned 'else'")

    i += 1

# Write the file back
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'w', encoding='utf-8') as f:
    f.writelines(lines)

print(f"\n{'='*60}")
print(f"Done! Fixed {fixes} orphaned 'else' statements.")
print(f"{'='*60}")
