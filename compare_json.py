#!/usr/bin/env python3
import json
import sys

def load_json(filename):
    with open(filename, 'r', encoding='utf-8-sig') as f:
        return json.load(f)

def compare_compilers(standalone_file, new_compiler_file):
    standalone = load_json(standalone_file)
    new_comp = load_json(new_compiler_file)
    
    print("=== SUMMARY ===")
    print(f"Classes: {len(standalone['classes'])} vs {len(new_comp['classes'])}")
    print(f"Methods: {len(standalone['methods'])} vs {len(new_comp['methods'])}")
    print(f"Init Expressions: {len(standalone.get('initializationExpressions', []))} vs {len(new_comp.get('initializationExpressions', []))}")
    print()
    
    # Compare classes
    standalone_classes = {c['name'] for c in standalone['classes']}
    new_classes = {c['name'] for c in new_comp['classes']}
    
    missing_in_new = standalone_classes - new_classes
    extra_in_new = new_classes - standalone_classes
    
    if missing_in_new:
        print(f"Classes missing in new compiler: {sorted(missing_in_new)[:5]}...")
    if extra_in_new:
        print(f"Extra classes in new compiler: {sorted(extra_in_new)[:5]}...")
    
    # Compare methods
    standalone_methods = {(m['class'], m['selector']) for m in standalone['methods']}
    new_methods = {(m['class'], m['selector']) for m in new_comp['methods']}
    
    missing_methods = standalone_methods - new_methods
    extra_methods = new_methods - standalone_methods
    
    if missing_methods:
        print(f"\nMissing methods in new compiler ({len(missing_methods)} total):")
        for cls, sel in sorted(missing_methods)[:10]:
            print(f"  {cls}>>{sel}")
    
    if extra_methods:
        print(f"\nExtra methods in new compiler ({len(extra_methods)} total):")
        for cls, sel in sorted(extra_methods)[:10]:
            print(f"  {cls}>>{sel}")
    
    # Compare a specific method's bytecode
    print("\n=== BYTECODE COMPARISON (sample) ===")
    # Find a common method
    for m in standalone['methods']:
        if m['class'] == 'Object' and m['selector'] == 'class':
            standalone_method = m
            break
    
    for m in new_comp['methods']:
        if m['class'] == 'Object' and m['selector'] == 'class':
            new_method = m
            break
    
    if 'standalone_method' in locals() and 'new_method' in locals():
        print(f"Object>>class bytecode:")
        print(f"  Standalone: {standalone_method['bytecode']}")
        print(f"  New:        {new_method['bytecode']}")
        print(f"  Match: {standalone_method['bytecode'] == new_method['bytecode']}")

if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: compare_json.py <standalone.json> <new_compiler.json>")
        sys.exit(1)
    
    compare_compilers(sys.argv[1], sys.argv[2])