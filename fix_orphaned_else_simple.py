#!/usr/bin/env python3
"""
Find and comment out orphaned 'else' blocks after OBSOLETE comments.
"""

# Read the file
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

fixed = 0
i = 0

while i < len(lines):
    line = lines[i]

    # Check if this line is "// OBSOLETE: }" followed by an uncommented 'else'
    if line.strip() == '// OBSOLETE: }' and i + 1 < len(lines):
        next_line = lines[i + 1]

        # Check if next line is an uncommented 'else'
        if next_line.strip().startswith('else') and not next_line.strip().startswith('//'):
            # Comment out the entire else block
            indent = len(next_line) - len(next_line.lstrip())
            lines[i + 1] = ' ' * indent + '// OBSOLETE: ' + next_line.lstrip()

            # Now comment out everything until the matching closing brace
            brace_count = next_line.count('{') - next_line.count('}')
            j = i + 2

            while j < len(lines) and brace_count > 0:
                current = lines[j]
                brace_count += current.count('{') - current.count('}')

                # Comment out this line if not already commented
                if current.strip() and not current.strip().startswith('//'):
                    indent = len(current) - len(current.lstrip())
                    lines[j] = ' ' * indent + '// OBSOLETE: ' + current.lstrip()

                j += 1

            fixed += 1
            print(f"Fixed orphaned else at line {i+2}")

    i += 1

# Write back
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'w', encoding='utf-8') as f:
    f.writelines(lines)

print(f"\nFixed {fixed} orphaned else blocks")
