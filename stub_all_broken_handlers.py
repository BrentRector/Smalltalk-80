#!/usr/bin/env python3
"""
Stub out ALL broken bytecode handlers and helper methods that reference
_blockContextMapping or _blockClosureCache.

This allows the code to compile while we implement clean versions based
on the Single-Context Architecture.
"""

import re

# Read the file
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Track number of replacements
replacement_count = 0

# Pattern 1: PUSH_LITERAL bytecode handler (lines ~980-1060)
# Replace the entire if block that checks _blockContextMapping
pattern1 = r'// Check if we\'re executing in a block\s*if \(_blockContextMapping\.TryGetValue\(_activeContextOOP.*?Console\.WriteLine\(\$"\[CREATE_BLOCKCLOSURE\] Top-level block: homeContext=MethodContext\(\{homeContextOOP\}\)"\);[\s\n]*\}'

stub1 = '''// TODO: Implement using lexicalParent to find home context (Single-Context Architecture)
                throw new NotImplementedException("PUSH_LITERAL BlockClosure creation - awaiting lexicalParent implementation");'''

if re.search(pattern1, content, flags=re.DOTALL):
    content = re.sub(pattern1, stub1, content, flags=re.DOTALL)
    replacement_count += 1
    print(f"[OK] Stubbed PUSH_LITERAL BlockClosure creation")

# Pattern 2: BlockClosure cache logic (lines ~1040-1055)
pattern2 = r'var methodContextOOP = FindMethodContext\(_activeContextOOP\);[\s\n]*var cacheKey = \(methodContextOOP, literalIndex\);[\s\n]*if \(_blockClosureCache\.TryGetValue\(cacheKey.*?\}\s*_blockClosureCache\[cacheKey\] = blockClosureOOP;'

stub2 = '''// TODO: Implement BlockClosure caching with lexicalParent (Single-Context Architecture)
                // For now, just create a new BlockClosure each time
                var methodContextOOP = _activeContextOOP; // Temporary - should use lexicalParent traversal'''

if re.search(pattern2, content, flags=re.DOTALL):
    content = re.sub(pattern2, stub2, content, flags=re.DOTALL)
    replacement_count += 1
    print(f"[OK] Stubbed BlockClosure cache logic")

# Pattern 3-10: All STORE_IVAR variants (extended bytecode 0x80-0x87)
# These all have similar structure with _blockContextMapping checks
for bytecode_num in range(0x80, 0x88):
    pattern = rf'// Store into instance variable {bytecode_num - 0x80}-{bytecode_num - 0x80 + 7}.*?if \(_blockContextMapping\.TryGetValue.*?return true;[\s\n]*\}}'

    stub = f'''// Store into instance variable {bytecode_num - 0x80}-{bytecode_num - 0x80 + 7}
                // STUB: TODO - Implement using lexicalParent traversal (Single-Context Architecture)
                if (BytecodeUtil.IsStoreIvar(bytecode))
                {{
                    int index = BytecodeUtil.StoreIvarIndex(bytecode);
                    throw new NotImplementedException($"STORE_IVAR extended bytecode 0x{{bytecode:X2}} (index={{index}}) - awaiting lexicalParent implementation");
                }}'''

    if re.search(pattern, content, flags=re.DOTALL):
        content = re.sub(pattern, stub, content, flags=re.DOTALL, count=1)
        replacement_count += 1
        print(f"[OK] Stubbed STORE_IVAR extended handler 0x{bytecode_num:02X}")

# Pattern 11-18: All STORE_TEMP variants (extended bytecode 0x88-0x8F)
for bytecode_num in range(0x88, 0x90):
    pattern = rf'// Store into temporary variable {bytecode_num - 0x88}-{bytecode_num - 0x88 + 7}.*?if \(_blockContextMapping\.TryGetValue.*?return true;[\s\n]*\}}'

    stub = f'''// Store into temporary variable {bytecode_num - 0x88}-{bytecode_num - 0x88 + 7}
                // STUB: TODO - Implement using lexicalParent traversal (Single-Context Architecture)
                if (BytecodeUtil.IsStoreTemp(bytecode))
                {{
                    int index = BytecodeUtil.StoreTempIndex(bytecode);
                    throw new NotImplementedException($"STORE_TEMP extended bytecode 0x{{bytecode:X2}} (index={{index}}) - awaiting lexicalParent implementation");
                }}'''

    if re.search(pattern, content, flags=re.DOTALL):
        content = re.sub(pattern, stub, content, flags=re.DOTALL, count=1)
        replacement_count += 1
        print(f"[OK] Stubbed STORE_TEMP extended handler 0x{bytecode_num:02X}")

# Pattern 19-26: All POP_STORE_TEMP variants (extended bytecode 0x68-0x6F)
for bytecode_num in range(0x68, 0x70):
    pattern = rf'// Pop and store into temporary variable {bytecode_num - 0x68}-{bytecode_num - 0x68 + 7}.*?if \(_blockContextMapping\.TryGetValue.*?return true;[\s\n]*\}}'

    stub = f'''// Pop and store into temporary variable {bytecode_num - 0x68}-{bytecode_num - 0x68 + 7}
                // STUB: TODO - Implement using lexicalParent traversal (Single-Context Architecture)
                if (BytecodeUtil.IsPopStoreTemp(bytecode))
                {{
                    int index = BytecodeUtil.PopStoreTempIndex(bytecode);
                    throw new NotImplementedException($"POP_STORE_TEMP extended bytecode 0x{{bytecode:X2}} (index={{index}}) - awaiting lexicalParent implementation");
                }}'''

    if re.search(pattern, content, flags=re.DOTALL):
        content = re.sub(pattern, stub, content, flags=re.DOTALL, count=1)
        replacement_count += 1
        print(f"[OK] Stubbed POP_STORE_TEMP extended handler 0x{bytecode_num:02X}")

# Pattern 27: GetHomeContext helper method
pattern27 = r'private SmalltalkOOP GetHomeContext\(SmalltalkOOP contextOOP\)[\s\n]*\{[\s\n]*if \(_blockContextMapping\.TryGetValue.*?return contextOOP;[\s\n]*\}'

stub27 = '''private SmalltalkOOP GetHomeContext(SmalltalkOOP contextOOP)
    {
        // TODO: Implement using lexicalParent traversal (Single-Context Architecture)
        // For blocks: Walk lexicalParent chain until nil (top-level method)
        // For methods: Return self (already at home)
        throw new NotImplementedException("GetHomeContext - awaiting lexicalParent implementation");
    }'''

if re.search(pattern27, content, flags=re.DOTALL):
    content = re.sub(pattern27, stub27, content, flags=re.DOTALL)
    replacement_count += 1
    print(f"[OK] Stubbed GetHomeContext helper method")

# Write the file back
with open(r'D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print(f"\n{'='*60}")
print(f"Done! {replacement_count} handlers/methods stubbed successfully.")
print(f"{'='*60}")
