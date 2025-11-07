#!/usr/bin/env python3
"""
Stub out all broken bytecode handlers that reference _blockContextMapping or _blockClosureCache.
This allows the code to compile while we implement clean versions based on the Single-Context Architecture.
"""

import re

# Read the file
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Pattern 1: Replace compact PUSH_TEMP handler (lines ~951-1279)
# Find the block starting with "if (BytecodeUtil.IsPushTemp(bytecode))" and ending with "return true;\n        }"
push_temp_pattern = r'(        // Push temporary variable 0-15.*?\n        // STUB:.*?\n        if \(BytecodeUtil\.IsPushTemp\(bytecode\)\)\s*\{)\s*int index = BytecodeUtil\.PushTempIndex\(bytecode\);.*?Push\(valueOOP\);\s*return true;\s*\}'

push_temp_stub = r'''\1
            int index = BytecodeUtil.PushTempIndex(bytecode);
            // TODO: Implement using lexicalParent traversal (Single-Context Architecture)
            throw new NotImplementedException($"PUSH_TEMP compact bytecode 0x{bytecode:X2} (index={index}) - awaiting lexicalParent implementation");
        }'''

content = re.sub(push_temp_pattern, push_temp_stub, content, flags=re.DOTALL)

print("Stubbed PUSH_TEMP compact handler")

# Write the file back
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print("Done! File updated.")
