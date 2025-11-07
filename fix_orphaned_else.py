#!/usr/bin/env python3
"""
Find and fix orphaned 'else' blocks after commenting out 'if' statements.
"""

import re

# Read the file
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Pattern: Find orphaned '{ ... } else {' where the opening brace has no corresponding if
# We're looking for patterns where:
# 1. Line has "// OBSOLETE: if (...)"
# 2. Followed by "{"
# 3. ... code ...
# 4. Followed by "} else {"

# Find all occurrences of: (commented-out if) { ... } else {
pattern = r'(// OBSOLETE: if \(_blockContextMapping\.TryGetValue.*?\)\s*\n\s*\{)(.*?)(\}\s*else\s*\{)(.*?)(\})'

def comment_out_block(match):
    """Comment out the entire if-else structure"""
    indent = '                                '

    # Extract parts
    if_line = match.group(1)
    if_body = match.group(2)
    else_line = match.group(3)
    else_body = match.group(4)
    closing = match.group(5)

    # Comment out all lines in if_body
    if_lines = if_body.split('\n')
    commented_if = '\n'.join([indent + '// OBSOLETE: ' + line.lstrip() if line.strip() and not line.strip().startswith('//') else line for line in if_lines])

    # Comment out all lines in else_body
    else_lines = else_body.split('\n')
    commented_else = '\n'.join([indent + '// OBSOLETE: ' + line.lstrip() if line.strip() and not line.strip().startswith('//') else line for line in else_lines])

    result = f'''{if_line.replace('{', '// OBSOLETE: {')}
{commented_if}
{indent}// OBSOLETE: {else_line.strip()}
{commented_else}
{indent}// OBSOLETE: {closing}'''

    return result

# Apply the transformation
new_content = re.sub(pattern, comment_out_block, content, flags=re.DOTALL)

# Write the file back
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'w', encoding='utf-8') as f:
    f.write(new_content)

print("Done! Fixed orphaned else blocks.")
