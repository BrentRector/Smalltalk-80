#!/usr/bin/env python3
"""
Remove Bootstrap-Only Methods
Removes methods that exist in bootstrap but not in system image
"""

import json

def remove_bootstrap_only_methods():
    """Remove methods that exist in bootstrap but not in system image"""
    
    print("Loading system image and bootstrap...")
    with open('complete_system_final.json', 'r') as f:
        system_image = json.load(f)
    
    with open('Bootstrap/bootstrap.json', 'r') as f:
        bootstrap = json.load(f)
    
    # Build comprehensive system method lookup
    system_methods = {}
    for method in system_image['methods']:
        class_name = method['class']
        is_class_method = class_name.endswith(' class')
        if is_class_method:
            class_name = class_name[:-6]  # Remove ' class'
        
        if class_name not in system_methods:
            system_methods[class_name] = {'instance': set(), 'class': set()}
        
        method_type = 'class' if is_class_method else 'instance'
        system_methods[class_name][method_type].add(method['selector'])
    
    print(f"System image has methods for {len(system_methods)} classes")
    
    total_removed = 0
    
    for bootstrap_class in bootstrap['classes']:
        class_name = bootstrap_class['name']
        
        print(f"\nProcessing {class_name}:")
        
        if class_name not in system_methods:
            print(f"  WARNING: {class_name} not found in system image")
            continue
        
        # Process instance methods
        if 'methods' in bootstrap_class:
            original_methods = bootstrap_class['methods'][:]
            valid_methods = []
            
            for method in original_methods:
                selector = method.get('selector', '')
                if selector in system_methods[class_name]['instance']:
                    valid_methods.append(method)
                else:
                    print(f"    Removing instance method: {selector}")
                    total_removed += 1
            
            bootstrap_class['methods'] = valid_methods
            
            removed_instance = len(original_methods) - len(valid_methods)
            if removed_instance > 0:
                print(f"  Removed {removed_instance} instance methods")
        
        # Process class methods
        if 'classMethods' in bootstrap_class:
            original_methods = bootstrap_class['classMethods'][:]
            valid_methods = []
            
            for method in original_methods:
                selector = method.get('selector', '')
                if selector in system_methods[class_name]['class']:
                    valid_methods.append(method)
                else:
                    print(f"    Removing class method: {selector}")
                    total_removed += 1
            
            bootstrap_class['classMethods'] = valid_methods
            
            removed_class = len(original_methods) - len(valid_methods)
            if removed_class > 0:
                print(f"  Removed {removed_class} class methods")
    
    print(f"\nTotal methods removed: {total_removed}")
    
    # Save updated bootstrap
    if total_removed > 0:
        print("Saving updated bootstrap.json...")
        with open('Bootstrap/bootstrap.json', 'w', encoding='utf-8') as f:
            json.dump(bootstrap, f, indent=2, ensure_ascii=False)
        print("Bootstrap-only methods removed successfully!")
    
    return total_removed

if __name__ == "__main__":
    removed = remove_bootstrap_only_methods()
    print(f"\nCompleted: removed {removed} bootstrap-only methods")