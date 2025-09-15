#!/usr/bin/env python3
"""
Analyze methods that exist in bootstrap.json but not in the system image.
"""

import json

def load_json(filepath):
    with open(filepath, 'r') as f:
        return json.load(f)

# Load both files
bootstrap = load_json('Bootstrap/bootstrap.json')
system = load_json('complete_system_final.json')

# Build system image index
system_methods = set()
for method in system.get('methods', []):
    class_name = method.get('class', '')
    selector = method.get('selector', '')
    is_class = method.get('isClassMethod', False)
    system_methods.add((class_name, selector, is_class))

# Find bootstrap methods not in system
extra_methods = []
for class_def in bootstrap.get('classes', []):
    class_name = class_def.get('name', '')
    
    # Check instance methods
    for method in class_def.get('methods', []):
        selector = method.get('selector', '')
        if (class_name, selector, False) not in system_methods:
            extra_methods.append({
                'class': class_name,
                'selector': selector,
                'type': 'instance',
                'source': method.get('source', ''),
                'bytecode_len': len(method.get('bytecode', []))
            })
    
    # Check class methods
    for method in class_def.get('classMethods', []):
        selector = method.get('selector', '')
        if (class_name, selector, True) not in system_methods:
            extra_methods.append({
                'class': class_name,
                'selector': selector,
                'type': 'class',
                'source': method.get('source', ''),
                'bytecode_len': len(method.get('bytecode', []))
            })

# Analyze and categorize
print("=== Methods in Bootstrap but NOT in System Image ===\n")

# Group by class
by_class = {}
for m in extra_methods:
    if m['class'] not in by_class:
        by_class[m['class']] = []
    by_class[m['class']].append(m)

for class_name in sorted(by_class.keys()):
    methods = by_class[class_name]
    print(f"\n{class_name} ({len(methods)} methods):")
    for m in methods:
        print(f"  {m['type']:8} >> {m['selector']}")
        if m['source']:
            # Show first 100 chars of source
            src = m['source'][:100].replace('\n', ' ').replace('\r', '')
            print(f"           Source: {src}...")
        else:
            print(f"           Bytecode: {m['bytecode_len']} bytes")

print(f"\n=== Summary ===")
print(f"Total extra methods: {len(extra_methods)}")

# Look for patterns
class_methods = [m for m in extra_methods if m['type'] == 'class']
init_methods = [m for m in extra_methods if 'init' in m['selector'].lower()]
primitive_methods = [m for m in extra_methods if 'primitive' in m['selector'].lower()]

print(f"Class methods: {len(class_methods)}")
print(f"Initialization-related: {len(init_methods)}")
print(f"Primitive-related: {len(primitive_methods)}")

# Check if these are referenced in bootstrap process
print("\n=== Checking Usage ===")
print("Methods that might be called during bootstrap:")

critical = ['initialize', 'basicNew', 'basicNew:', 'init', 'new', 'new:']
for m in extra_methods:
    if m['selector'] in critical:
        print(f"  CRITICAL: {m['class']} >> {m['selector']} ({m['type']})")