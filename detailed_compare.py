#!/usr/bin/env python3
import json
import sys

def load_json(filename):
    with open(filename, 'r', encoding='utf-8-sig') as f:
        return json.load(f)

def compare_detailed(standalone_file, new_compiler_file):
    standalone = load_json(standalone_file)
    new_comp = load_json(new_compiler_file)
    
    # Check class methods
    print("=== CLASS METHOD COMPARISON ===")
    standalone_class_methods = {(m['class'], m['selector']) for m in standalone['methods'] if m.get('isClassMethod', False)}
    new_class_methods = {(m['class'], m['selector']) for m in new_comp['methods'] if m.get('isClassMethod', False)}
    
    print(f"Class methods in standalone: {len(standalone_class_methods)}")
    print(f"Class methods in new compiler: {len(new_class_methods)}")
    
    # Check protocols
    print("\n=== PROTOCOL COMPARISON ===")
    standalone_protocols = {m.get('protocol', 'unclassified') for m in standalone['methods']}
    new_protocols = {m.get('protocol', 'unclassified') for m in new_comp['methods']}
    
    print(f"Protocols in standalone: {sorted(standalone_protocols)[:10]}")
    print(f"Protocols in new compiler: {sorted(new_protocols)[:10]}")
    
    # Compare specific methods in detail
    print("\n=== DETAILED METHOD COMPARISON ===")
    
    # Find Point>>x method in both
    for name, data in [("Standalone", standalone), ("New", new_comp)]:
        for m in data['methods']:
            if m['class'] == 'Point' and m['selector'] == 'x':
                print(f"\n{name} - Point>>x:")
                print(f"  Bytecode: {m['bytecode']}")
                print(f"  Literals: {m.get('literals', [])}")
                print(f"  Protocol: {m.get('protocol', 'unclassified')}")
                print(f"  IsClassMethod: {m.get('isClassMethod', False)}")
                break
    
    # Find a class method
    for name, data in [("Standalone", standalone), ("New", new_comp)]:
        for m in data['methods']:
            if m['class'] == 'Point class' and m['selector'] == 'x:y:':
                print(f"\n{name} - Point class>>x:y::")
                print(f"  Bytecode: {m['bytecode'][:10]}...")
                print(f"  Protocol: {m.get('protocol', 'unclassified')}")
                print(f"  IsClassMethod: {m.get('isClassMethod', False)}")
                break
    
    # Check initialization expressions
    print("\n=== INITIALIZATION EXPRESSIONS ===")
    if standalone.get('initializationExpressions'):
        print(f"Standalone has {len(standalone['initializationExpressions'])} init expressions")
        print("First few:")
        for expr in standalone['initializationExpressions'][:3]:
            print(f"  Source: {expr.get('source', 'N/A')[:50]}...")
    
    if new_comp.get('initializationExpressions'):
        print(f"New compiler has {len(new_comp['initializationExpressions'])} init expressions")
    else:
        print("New compiler has NO init expressions!")

if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: detailed_compare.py <standalone.json> <new_compiler.json>")
        sys.exit(1)
    
    compare_detailed(sys.argv[1], sys.argv[2])