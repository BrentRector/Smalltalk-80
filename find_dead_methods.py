#!/usr/bin/env python3
import re

# Read VirtualMachine.cs
with open(r'Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Find all private method definitions
method_pattern = r'^\s*private\s+\w+\s+(\w+)\s*\('
methods = {}

for match in re.finditer(method_pattern, content, re.MULTILINE):
    method_name = match.group(1)
    line_num = content[:match.start()].count('\n') + 1
    methods[method_name] = {'line': line_num, 'calls': 0}

# Count calls to each method (excluding the definition line)
for method_name in methods.keys():
    # Pattern: method_name followed by open paren (but not in definition)
    call_pattern = rf'\b{method_name}\s*\('
    
    for match in re.finditer(call_pattern, content):
        line_num = content[:match.start()].count('\n') + 1
        # Skip if it's the definition line
        if abs(line_num - methods[method_name]['line']) > 5:  # Not near definition
            methods[method_name]['calls'] += 1

# Find methods with 0 calls
dead_methods = []
for method_name, info in sorted(methods.items(), key=lambda x: x[1]['line']):
    if info['calls'] == 0:
        dead_methods.append((method_name, info['line']))

print("=== DEAD METHODS (never called) ===\n")
for method_name, line in dead_methods:
    print(f"Line {line:4d}: {method_name}")

print(f"\nTotal: {len(dead_methods)} dead methods out of {len(methods)} private methods")
