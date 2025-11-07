#!/usr/bin/env python3
"""
Script to comment out all references to _blockContextMapping and _blockClosureCache
in VirtualMachine.cs to fix compilation errors.
"""

def main():
    file_path = r"Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs"

    with open(file_path, 'r', encoding='utf-8') as f:
        lines = f.readlines()

    modified_lines = []
    in_multiline_comment = False

    for i, line in enumerate(lines, 1):
        # Check if this line references the non-existent fields
        if ('_blockContextMapping' in line or '_blockClosureCache' in line):
            # Check if already commented
            stripped = line.lstrip()
            if not stripped.startswith('//'):
                # Comment out the line
                indent = len(line) - len(stripped)
                modified_lines.append(' ' * indent + '// TODO: STUBBED - ' + stripped)
                print(f"Line {i}: Commented out: {line.rstrip()[:80]}")
            else:
                # Already commented, keep as-is
                modified_lines.append(line)
        else:
            modified_lines.append(line)

    # Write back
    with open(file_path, 'w', encoding='utf-8') as f:
        f.writelines(modified_lines)

    print(f"\nDone! Modified {file_path}")

if __name__ == '__main__':
    main()
