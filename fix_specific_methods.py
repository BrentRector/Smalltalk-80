#!/usr/bin/env python3
"""
Fix Specific Method Bytecode Issues
"""

import json

def fix_specific_methods():
    """Fix the specific methods that still have bytecode issues"""
    
    print("Loading bootstrap JSON...")
    with open('Bootstrap/bootstrap.json', 'r', encoding='utf-8') as f:
        bootstrap = json.load(f)
    
    fixes_applied = 0
    
    # Fix 1: Behavior.basicNew - numTemps should be 1
    for bootstrap_class in bootstrap['classes']:
        if bootstrap_class['name'] == 'Behavior':
            for method in bootstrap_class.get('methods', []):
                if method.get('selector') == 'basicNew':
                    print(f"Fixing Behavior.basicNew: numTemps {method.get('numTemps')} -> 1")
                    method['numTemps'] = 1
                    method['bytecode'] = [112, 208, 137, 0, 5, 112, 35, 226, 125, 125, 225, 104, 112, 212, 124]
                    fixes_applied += 1
                    break
            break
    
    # Fix 2: Metaclass.newNamed: - bytecode and primitive
    for bootstrap_class in bootstrap['classes']:
        if bootstrap_class['name'] == 'Metaclass':
            for method in bootstrap_class.get('methods', []):
                if method.get('selector') == 'newNamed:':
                    print(f"Fixing Metaclass.newNamed: bytecode and primitive")
                    method['bytecode'] = [112, 124]
                    method['primitiveIndex'] = 0
                    fixes_applied += 1
                    break
            break
    
    print(f"Applied {fixes_applied} specific method fixes")
    
    # Save updated bootstrap
    if fixes_applied > 0:
        print("Saving updated bootstrap.json...")
        with open('Bootstrap/bootstrap.json', 'w', encoding='utf-8') as f:
            json.dump(bootstrap, f, indent=2, ensure_ascii=False)
        print("Specific method fixes applied successfully!")
    
    return fixes_applied

if __name__ == "__main__":
    fixes = fix_specific_methods()
    print(f"\nCompleted with {fixes} fixes applied")