#!/usr/bin/env python3
"""
Create minimal stub implementations for bytecode handlers that reference
obsolete _blockContextMapping and _blockClosureCache fields.

This gets the code to compile, then we can implement proper lexicalParent logic.
"""

import re

# Read the file
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Track changes
changes = []

# 1. PUSH_TEMP handler (lines 951-1279) - Replace entire implementation with simple stub
old_push_temp = r'''        // Push temporary variable 0-15 \(0x10-0x1F\)
        // STUB: Complex BlockContext logic removed - needs reimplementation with lexicalParent
        if \(BytecodeUtil\.IsPushTemp\(bytecode\)\)
        \{
            int index = BytecodeUtil\.PushTempIndex\(bytecode\);

            // CRITICAL FIX:.*?
            Push\(valueOOP\);
            return true;
        \}'''

new_push_temp = '''        // Push temporary variable 0-15 (0x10-0x1F)
        // TODO: Implement using lexicalParent traversal for block variable access (Single-Context Architecture)
        if (BytecodeUtil.IsPushTemp(bytecode))
        {
            int index = BytecodeUtil.PushTempIndex(bytecode);

            // STUB: Simple implementation - read from active context indexed temps
            // This works for methods but NOT for blocks accessing parent temps
            var valueOOP = _heapManager.GetIndexedOOP(_activeContextOOP, index);
            Push(valueOOP);
            return true;
        }'''

if re.search(old_push_temp, content, flags=re.DOTALL):
    content = re.sub(old_push_temp, new_push_temp, content, flags=re.DOTALL)
    changes.append("Stubbed PUSH_TEMP handler")

# 2. Find and replace all extended bytecode STORE handlers that check _blockContextMapping
# Pattern: if (_blockContextMapping.TryGetValue(...)) { ... } OR similar blocks
# We'll use a simpler approach - just comment out the if blocks

# First, let's just do line-by-line replacement of _blockContextMapping references
lines = content.split('\n')
new_lines = []
in_obsolete_block = False
block_depth = 0

for i, line in enumerate(lines):
    # Check if line references obsolete fields
    if '_blockContextMapping' in line or '_blockClosureCache' in line:
        # Start of obsolete block
        if 'if (' in line and ('_blockContextMapping' in line or '_blockClosureCache' in line):
            in_obsolete_block = True
            block_depth = line.count('{') - line.count('}')
            indent = len(line) - len(line.lstrip())
            new_lines.append(' ' * indent + '// OBSOLETE: ' + line.lstrip())
            continue
        else:
            # Single line reference - comment it out
            indent = len(line) - len(line.lstrip())
            new_lines.append(' ' * indent + '// OBSOLETE: ' + line.lstrip())
            continue

    if in_obsolete_block:
        # Track brace depth
        block_depth += line.count('{') - line.count('}')
        indent = len(line) - len(line.lstrip())

        # Comment out this line
        if line.strip() and not line.strip().startswith('//'):
            new_lines.append(' ' * indent + '// OBSOLETE: ' + line.lstrip())
        else:
            new_lines.append(line)

        # Check if block ended
        if block_depth <= 0:
            in_obsolete_block = False
            block_depth = 0
    else:
        new_lines.append(line)

content = '\n'.join(new_lines)

# Write the file back
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print(f"Applied {len(changes)} major changes")
for change in changes:
    print(f"  - {change}")
print("\nNote: All _blockContextMapping/_blockClosureCache references commented out")
