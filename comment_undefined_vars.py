#!/usr/bin/env python3
"""
Comment out all code blocks that reference undefined variables (currentBlockOOP, found).
These are in the obsolete temp/ivar access code that will be completely rewritten.
"""

import re

# Read the file
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Pattern: Large blocks that contain currentBlockOOP or 'bool found' references
# These are the while loops in the extended PUSH_TEMP, STORE_TEMP handlers

# Find all while loops that traverse block contexts (lines ~1170-1235, ~1690-1850, ~1790-1920)
# These contain the obsolete traversal logic

patterns_to_comment = [
    # Pattern 1: STORE_TEMP/POP_STORE_TEMP block traversal (contains currentBlockOOP)
    (r'(while \(adjustedIndex >= currentLocalCount\)\s*\{.*?currentContext = homeContext;\s*\})',
     'Obsolete block context traversal'),

    # Pattern 2: Lines referencing currentBlockOOP directly
    (r'(\s+var blockClass = GetClassName\(GetClass\(currentBlockOOP\)\);)',
     'Reference to undefined currentBlockOOP'),

    (r'(\s+homeContext = _heapManager\.GetInstanceVariable\(currentBlockOOP, homeIndex\);)',
     'Reference to undefined currentBlockOOP'),

    (r'(\s+var objClass = _heapManager\.GetSlot\(currentBlockOOP, 0\);)',
     'Reference to undefined currentBlockOOP'),

    # Pattern 3: Lines referencing 'found' variable
    (r'(\s+if \(!found\))',
     'Reference to undefined found variable'),
]

replacement_count = 0

for pattern, description in patterns_to_comment:
    matches = list(re.finditer(pattern, content, flags=re.DOTALL))
    if matches:
        print(f"Found {len(matches)} match(es) for: {description}")
        # Replace from end to start to preserve positions
        for match in reversed(matches):
            start, end = match.span(1)
            matched_text = content[start:end]

            # Comment out each line
            lines = matched_text.split('\n')
            commented_lines = []
            for line in lines:
                if line.strip() and not line.strip().startswith('//'):
                    indent = len(line) - len(line.lstrip())
                    commented_lines.append(' ' * indent + '// OBSOLETE: ' + line.lstrip())
                else:
                    commented_lines.append(line)

            commented_text = '\n'.join(commented_lines)
            content = content[:start] + commented_text + content[end:]
            replacement_count += 1

# Write back
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print(f"\n{'='*60}")
print(f"Done! Commented out {replacement_count} code blocks.")
print(f"{'='*60}")
