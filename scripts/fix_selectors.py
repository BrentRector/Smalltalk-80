#!/usr/bin/env python3
"""
Fix selector spaces in Phase 2 JSON without corrupting bytecode.
Uses Python's JSON library which preserves data types correctly.
"""

import json
import re
import sys
from datetime import datetime

def fix_selector(selector):
    """Remove spaces from multi-word selectors"""
    # Only fix selectors that have colons and spaces (multi-word selectors)
    if ':' in selector and ' ' in selector:
        return selector.replace(' ', '')
    return selector

def process_phase2_json(input_file, output_file):
    """Process Phase 2 JSON to fix selector spaces"""
    
    print(f"Loading {input_file}...")
    with open(input_file, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    fixed_count = 0
    problem_bytecode_count = 0
    
    # Process each class
    for class_def in data.get('classes', []):
        class_name = class_def.get('name', '')
        
        # Fix instance methods
        for method in class_def.get('methods', []):
            old_selector = method.get('selector', '')
            new_selector = fix_selector(old_selector)
            
            if old_selector != new_selector:
                method['selector'] = new_selector
                fixed_count += 1
                print(f"  Fixed: {class_name}>>{old_selector} -> {new_selector}")
            
            # Check for problem bytecode
            bytecode = method.get('bytecode', [])
            if bytecode == [64, 124]:
                problem_bytecode_count += 1
                print(f"  WARNING: {class_name}>>{new_selector} has [64, 124] bytecode!")
        
        # Fix class methods
        for method in class_def.get('classMethods', []):
            old_selector = method.get('selector', '')
            new_selector = fix_selector(old_selector)
            
            if old_selector != new_selector:
                method['selector'] = new_selector
                fixed_count += 1
                print(f"  Fixed: {class_name} class>>{old_selector} -> {new_selector}")
            
            # Check for problem bytecode
            bytecode = method.get('bytecode', [])
            if bytecode == [64, 124]:
                problem_bytecode_count += 1
                print(f"  WARNING: {class_name} class>>{new_selector} has [64, 124] bytecode!")
    
    print(f"\nFixed {fixed_count} selectors")
    
    if problem_bytecode_count > 0:
        print(f"\n⚠️  WARNING: Found {problem_bytecode_count} methods with [64, 124] bytecode!")
        print("This indicates the input file may already be corrupted.")
    else:
        print("✓ No problematic bytecode patterns found")
    
    # Save the fixed JSON
    print(f"\nSaving to {output_file}...")
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
    
    print("✓ Done!")
    return fixed_count, problem_bytecode_count

if __name__ == "__main__":
    import os
    
    # Change to project root
    script_dir = os.path.dirname(os.path.abspath(__file__))
    project_root = os.path.dirname(script_dir)
    os.chdir(project_root)
    
    input_file = "Bootstrap/bootstrap_phase2.json"
    backup_file = f"Bootstrap/bootstrap_phase2_backup_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json"
    
    # Create backup
    print(f"Creating backup: {backup_file}")
    import shutil
    shutil.copy2(input_file, backup_file)
    
    # Process the file
    fixed, problems = process_phase2_json(input_file, input_file)
    
    if problems > 0:
        print("\n⚠️  File has corrupted bytecode. Consider restoring from backup.")
        sys.exit(1)
    else:
        sys.exit(0)