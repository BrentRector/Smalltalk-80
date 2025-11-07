#!/usr/bin/env python3
"""
Comment out 'else' blocks that follow a closing brace from an if-statement
where the if-condition line was commented with "// OBSOLETE:".
"""

# Read the file
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

fixed = 0
i = 0

while i < len(lines):
    # Look for pattern: line with "// OBSOLETE: if ("
    if '// OBSOLETE: if (' in lines[i]:
        # Find the matching closing brace for this if-block
        # Start tracking from the line after the if-statement
        brace_depth = 0
        j = i + 1

        # Find opening brace
        while j < len(lines) and '{' not in lines[j]:
            j += 1

        if j < len(lines):
            # Start counting from the opening brace
            brace_depth = 1
            j += 1

            # Find the matching closing brace
            while j < len(lines) and brace_depth > 0:
                brace_depth += lines[j].count('{') - lines[j].count('}')
                if brace_depth == 0:
                    # Found the closing brace
                    # Check if next non-empty line is 'else'
                    k = j + 1
                    while k < len(lines) and not lines[k].strip():
                        k += 1

                    if k < len(lines) and lines[k].strip().startswith('else') and not lines[k].strip().startswith('//'):
                        # Comment out the else block
                        indent = len(lines[k]) - len(lines[k].lstrip())
                        lines[k] = ' ' * indent + '// OBSOLETE: ' + lines[k].lstrip()

                        # Find and comment out the else block content
                        else_brace_depth = lines[k].count('{') - lines[k].count('}')
                        m = k + 1

                        while m < len(lines) and else_brace_depth > 0:
                            else_brace_depth += lines[m].count('{') - lines[m].count('}')
                            if lines[m].strip() and not lines[m].strip().startswith('//'):
                                indent = len(lines[m]) - len(lines[m].lstrip())
                                lines[m] = ' ' * indent + '// OBSOLETE: ' + lines[m].lstrip()
                            m += 1

                        fixed += 1
                        print(f"Fixed orphaned else at line {k+1}")
                    break
                j += 1

    i += 1

# Write back
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'w', encoding='utf-8') as f:
    f.writelines(lines)

print(f"\nFixed {fixed} orphaned else blocks")
