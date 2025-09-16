#!/usr/bin/env python3
"""
Add Missing Methods - Fixed Version
Properly adds all missing methods to achieve 100% perfect matches
"""

import json
import os

def add_missing_methods_fixed():
    """Add missing methods with proper error handling"""
    
    print("Loading system image...")
    with open('complete_system_final.json', 'r', encoding='utf-8') as f:
        system_image = json.load(f)
    
    print("Loading bootstrap...")
    with open('Bootstrap/bootstrap.json', 'r', encoding='utf-8') as f:
        bootstrap = json.load(f)
    
    # Create backup
    backup_path = 'Bootstrap/bootstrap.json.backup_before_100percent'
    print(f"Creating backup at {backup_path}")
    with open(backup_path, 'w', encoding='utf-8') as f:
        json.dump(bootstrap, f, indent=2, ensure_ascii=False)
    
    # Build system method lookup
    system_methods = {}
    for method in system_image['methods']:
        class_name = method['class']
        is_class_method = class_name.endswith(' class')
        if is_class_method:
            class_name = class_name[:-6]
        
        if class_name not in system_methods:
            system_methods[class_name] = {'instance': {}, 'class': {}}
        
        method_type = 'class' if is_class_method else 'instance'
        system_methods[class_name][method_type][method['selector']] = method
    
    # Target classes that need missing methods
    target_classes = {
        'ArrayedCollection': 6, 'Association': 1, 'Behavior': 1, 'Boolean': 1, 
        'Class': 3, 'ClassOrganizer': 3, 'Collection': 4, 'CompiledMethod': 5, 
        'LookupKey': 1, 'Metaclass': 1, 'Object': 6, 'String': 3, 'Symbol': 8, 
        'SystemDictionary': 1, 'UndefinedObject': 1
    }
    
    total_added = 0
    
    print("\nAdding missing methods to each class...")
    
    for bootstrap_class in bootstrap['classes']:
        class_name = bootstrap_class['name']
        
        if class_name not in target_classes:
            continue
        
        if class_name not in system_methods:
            print(f"ERROR: {class_name} not found in system methods")
            continue
        
        print(f"\nProcessing {class_name}...")
        
        # Get current bootstrap methods
        current_instance = {m['selector'] for m in bootstrap_class.get('methods', [])}
        current_class = {m['selector'] for m in bootstrap_class.get('classMethods', [])}
        
        # Get system methods
        system_instance = system_methods[class_name]['instance']
        system_class = system_methods[class_name]['class']
        
        # Find missing instance methods
        missing_instance = []
        for selector, method in system_instance.items():
            if selector not in current_instance:
                missing_instance.append(method)
        
        # Find missing class methods
        missing_class = []
        for selector, method in system_class.items():
            if selector not in current_class:
                missing_class.append(method)
        
        # Add missing instance methods
        if missing_instance:
            print(f"  Adding {len(missing_instance)} instance methods")
            if 'methods' not in bootstrap_class:
                bootstrap_class['methods'] = []
            
            for method in missing_instance:
                bootstrap_method = {
                    "selector": method["selector"],
                    "bytecode": method["bytecode"], 
                    "literals": method["literals"],
                    "numArgs": method["numArgs"],
                    "numTemps": method["numTemps"],
                    "primitiveIndex": method["primitiveIndex"],
                    "source": method.get("source", ""),
                    "isClassMethod": False
                }
                bootstrap_class['methods'].append(bootstrap_method)
                total_added += 1
                print(f"    Added instance method: {method['selector']}")
        
        # Add missing class methods
        if missing_class:
            print(f"  Adding {len(missing_class)} class methods")
            if 'classMethods' not in bootstrap_class:
                bootstrap_class['classMethods'] = []
            
            for method in missing_class:
                bootstrap_method = {
                    "selector": method["selector"],
                    "bytecode": method["bytecode"],
                    "literals": method["literals"], 
                    "numArgs": method["numArgs"],
                    "numTemps": method["numTemps"],
                    "primitiveIndex": method["primitiveIndex"],
                    "source": method.get("source", ""),
                    "isClassMethod": True
                }
                bootstrap_class['classMethods'].append(bootstrap_method)
                total_added += 1
                print(f"    Added class method: {method['selector']}")
        
        # Verify counts
        new_instance_count = len(bootstrap_class.get('methods', []))
        new_class_count = len(bootstrap_class.get('classMethods', []))
        expected_instance = len(system_instance)
        expected_class = len(system_class)
        
        print(f"  Verification: instance {new_instance_count}/{expected_instance}, class {new_class_count}/{expected_class}")
        
        if new_instance_count != expected_instance or new_class_count != expected_class:
            print(f"  WARNING: Method counts don't match expected values!")
    
    print(f"\nTotal methods added: {total_added}")
    
    # Save updated bootstrap
    print("Saving updated bootstrap.json...")
    try:
        with open('Bootstrap/bootstrap.json', 'w', encoding='utf-8') as f:
            json.dump(bootstrap, f, indent=2, ensure_ascii=False)
        print("Bootstrap saved successfully!")
        
        # Verify the save worked
        with open('Bootstrap/bootstrap.json', 'r', encoding='utf-8') as f:
            verification = json.load(f)
        
        # Check a few classes to verify
        for cls in verification['classes']:
            if cls['name'] == 'Object':
                class_methods = len(cls.get('classMethods', []))
                print(f"Verification: Object now has {class_methods} class methods")
                break
        
    except Exception as e:
        print(f"ERROR saving bootstrap: {e}")
        # Restore backup
        print("Restoring backup...")
        os.rename(backup_path, 'Bootstrap/bootstrap.json')
        return 0
    
    return total_added

if __name__ == "__main__":
    added = add_missing_methods_fixed()
    print(f"\nCompleted: successfully added {added} methods")