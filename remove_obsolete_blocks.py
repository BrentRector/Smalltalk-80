#!/usr/bin/env python3
"""
Surgical removal of obsolete code blocks that reference _blockContextMapping or _blockClosureCache.
Instead of line-by-line commenting, this removes complete if-blocks as units.
"""

import re

# Read the file
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'r', encoding='utf-8') as f:
    content = f.read()

blocks_removed = 0

# Pattern 1: Complete if-else blocks for _blockContextMapping checks
# Matches: if (_blockContextMapping.TryGetValue(...)) { ... } else { ... }
pattern1 = r'if \(_blockContextMapping\.(?:TryGetValue|ContainsKey)[^{]+\{[^}]*?\}(?:\s*else\s*\{[^}]*?\})?'

def comment_block(match):
    global blocks_removed
    block = match.group(0)
    lines = block.split('\n')
    commented = []
    for line in lines:
        if line.strip() and not line.strip().startswith('//'):
            indent = len(line) - len(line.lstrip())
            commented.append(' ' * indent + '// OBSOLETE: ' + line.lstrip())
        else:
            commented.append(line)
    blocks_removed += 1
    return '\n'.join(commented)

# Apply pattern 1
content = re.sub(pattern1, comment_block, content, flags=re.DOTALL)

# Pattern 2: BlockClosure cache logic
pattern2 = r'if \(!_blockClosureCache\.TryGetValue[^{]+\{[^}]*?\}\s*_blockClosureCache\[cacheKey\] = [^;]+;'

content = re.sub(pattern2, comment_block, content, flags=re.DOTALL)

# Write the file back
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print(f"✓ Removed {blocks_removed} obsolete code blocks")
