#!/usr/bin/env python3
"""
Validate and fix bytecode in bootstrap.json to match the system image JSON.
"""

import json
import sys
from typing import Dict, List, Tuple, Optional

def load_json(filepath: str) -> dict:
    """Load a JSON file."""
    with open(filepath, 'r') as f:
        return json.load(f)

def save_json(filepath: str, data: dict):
    """Save data to a JSON file."""
    with open(filepath, 'w') as f:
        json.dump(data, f, indent=2)

def build_system_image_index(system_data: dict) -> Dict[Tuple[str, str, bool], dict]:
    """
    Build an index of methods from the system image.
    Returns: Dict[(class_name, selector, is_class_method), method_data]
    """
    index = {}
    
    for method in system_data.get('methods', []):
        class_name = method.get('class', '')
        selector = method.get('selector', '')
        is_class_method = method.get('isClassMethod', False)
        
        # Handle class methods - they're marked with isClassMethod in system image
        # but stored on ClassNameClass in bootstrap.json
        key = (class_name, selector, is_class_method)
        index[key] = method
    
    return index

def get_bootstrap_methods(bootstrap_data: dict) -> List[Tuple[str, str, bool, dict]]:
    """
    Extract all methods from bootstrap.json.
    Returns: List of (class_name, selector, is_class_method, method_data)
    """
    methods = []
    
    for class_def in bootstrap_data.get('classes', []):
        class_name = class_def.get('name', '')
        
        # Instance methods
        for method in class_def.get('methods', []):
            selector = method.get('selector', '')
            methods.append((class_name, selector, False, method))
        
        # Class methods
        for method in class_def.get('classMethods', []):
            selector = method.get('selector', '')
            methods.append((class_name, selector, True, method))
    
    return methods

def compare_bytecode(bc1: List[int], bc2: List[int]) -> bool:
    """Compare two bytecode arrays."""
    if len(bc1) != len(bc2):
        return False
    return all(a == b for a, b in zip(bc1, bc2))

def validate_and_fix_bytecode(bootstrap_file: str, system_image_file: str):
    """Main function to validate and fix bytecode."""
    
    print(f"Loading {bootstrap_file}...")
    bootstrap_data = load_json(bootstrap_file)
    
    print(f"Loading {system_image_file}...")
    system_data = load_json(system_image_file)
    
    print("Building system image method index...")
    system_index = build_system_image_index(system_data)
    print(f"  Found {len(system_index)} methods in system image")
    
    print("Extracting bootstrap methods...")
    bootstrap_methods = get_bootstrap_methods(bootstrap_data)
    print(f"  Found {len(bootstrap_methods)} methods in bootstrap.json")
    
    # Statistics
    total_checked = 0
    mismatches = []
    not_found = []
    fixed = 0
    
    print("\nValidating bytecode...")
    
    for class_name, selector, is_class_method, method in bootstrap_methods:
        total_checked += 1
        
        # Look up in system image
        key = (class_name, selector, is_class_method)
        
        if key not in system_index:
            # For metaclass methods, they might be stored differently
            # Try looking for ClassNameClass in bootstrap vs ClassName class in system
            if class_name.endswith('Class'):
                base_class = class_name[:-5]  # Remove 'Class' suffix
                alt_key = (base_class, selector, True)
                if alt_key in system_index:
                    key = alt_key
                else:
                    not_found.append((class_name, selector, is_class_method))
                    continue
            else:
                not_found.append((class_name, selector, is_class_method))
                continue
        
        system_method = system_index[key]
        
        # Compare bytecode
        bootstrap_bc = method.get('bytecode', [])
        system_bc = system_method.get('bytecode', [])
        
        if not compare_bytecode(bootstrap_bc, system_bc):
            mismatches.append({
                'class': class_name,
                'selector': selector,
                'is_class_method': is_class_method,
                'bootstrap_len': len(bootstrap_bc),
                'system_len': len(system_bc),
                'bootstrap_bc': bootstrap_bc[:10] if len(bootstrap_bc) > 10 else bootstrap_bc,
                'system_bc': system_bc[:10] if len(system_bc) > 10 else system_bc
            })
            
            # Fix the bytecode
            method['bytecode'] = system_bc
            
            # Also update literals if present in system image
            if 'literals' in system_method:
                method['literals'] = system_method['literals']
            
            # Update primitive index if present
            if 'primitiveIndex' in system_method:
                method['primitiveIndex'] = system_method['primitiveIndex']
            elif 'primitive' in system_method:
                method['primitive'] = system_method['primitive']
            
            # Update argument and temporary counts if present
            if 'numArgs' in system_method:
                method['argumentCount'] = system_method['numArgs']
            if 'numTemps' in system_method:
                method['temporaryCount'] = system_method['numTemps']
            
            fixed += 1
    
    # Print results
    print(f"\n=== Validation Results ===")
    print(f"Total methods checked: {total_checked}")
    print(f"Methods not found in system image: {len(not_found)}")
    print(f"Bytecode mismatches found: {len(mismatches)}")
    print(f"Methods fixed: {fixed}")
    
    if not_found:
        print(f"\n=== Methods Not Found in System Image ({len(not_found)}) ===")
        for class_name, selector, is_class_method in not_found[:10]:
            method_type = "class" if is_class_method else "instance"
            print(f"  {class_name}>>{selector} ({method_type})")
        if len(not_found) > 10:
            print(f"  ... and {len(not_found) - 10} more")
    
    if mismatches:
        print(f"\n=== Bytecode Mismatches Found ({len(mismatches)}) ===")
        for m in mismatches[:10]:
            method_type = "class" if m['is_class_method'] else "instance"
            print(f"  {m['class']}>>{m['selector']} ({method_type})")
            print(f"    Bootstrap: {m['bootstrap_len']} bytes, starts with {m['bootstrap_bc']}")
            print(f"    System:    {m['system_len']} bytes, starts with {m['system_bc']}")
        if len(mismatches) > 10:
            print(f"  ... and {len(mismatches) - 10} more")
    
    if fixed > 0:
        # Save the updated bootstrap.json
        backup_file = bootstrap_file + '.backup'
        print(f"\n=== Saving Updates ===")
        print(f"Creating backup: {backup_file}")
        save_json(backup_file, load_json(bootstrap_file))  # Save original
        
        print(f"Updating {bootstrap_file} with {fixed} fixes...")
        save_json(bootstrap_file, bootstrap_data)
        print("Bootstrap.json updated successfully!")
    else:
        print("\nAll bytecode matches! No updates needed.")
    
    return fixed

if __name__ == "__main__":
    bootstrap_file = "Bootstrap/bootstrap.json"
    system_image_file = "complete_system_final.json"
    
    fixes = validate_and_fix_bytecode(bootstrap_file, system_image_file)
    
    if fixes > 0:
        print(f"\nSuccessfully fixed {fixes} methods")
        print("Run 'dotnet build' to test the updated bootstrap")
    
    sys.exit(0 if fixes == 0 else 1)