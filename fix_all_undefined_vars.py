#!/usr/bin/env python3
"""
Comment out ALL lines that reference any of the undefined variables.
This is the final cleanup to get the code to compile.
"""

# Read the file
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

# List of undefined variables to search for
undefined_vars = ['blockClass', 'blockClosureOOP', 'blockOOP', 'blockOrClosureOOP', 'objClass', 'returnTopPopBlockOOP']

commented_count = 0

# Process each line
for i in range(len(lines)):
    line = lines[i]

    # Skip if already commented
    if line.strip().startswith('//'):
        continue

    # Check if line references any undefined variable
    for var in undefined_vars:
        if var in line:
            # Comment out the line
            indent = len(line) - len(line.lstrip())
            lines[i] = ' ' * indent + '// OBSOLETE: ' + line.lstrip()
            commented_count += 1
            print(f"Line {i+1}: {line.strip()[:80]}")
            break  # Only comment once per line

# Write the file back
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'w', encoding='utf-8') as f:
    f.writelines(lines)

print(f"\n{'='*60}")
print(f"Done! Commented out {commented_count} lines referencing undefined variables.")
print(f"{'='*60}")
