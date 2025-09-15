#!/usr/bin/env python3
"""
Remove all unnecessary methods from bootstrap.json that are never executed during bootstrap.
Since no Smalltalk code runs during bootstrap, we only need methods that will be there
for Phase 3 when Smalltalk execution begins.
"""

import json

def load_json(filepath):
    with open(filepath, 'r') as f:
        return json.load(f)

def save_json(filepath, data):
    with open(filepath, 'w') as f:
        json.dump(data, f, indent=2)

print("Loading files...")
bootstrap = load_json('Bootstrap/bootstrap.json')
system = load_json('complete_system_final.json')

# Build system method index to know what exists
system_methods = set()
for method in system.get('methods', []):
    class_name = method.get('class', '')
    selector = method.get('selector', '')
    is_class = method.get('isClassMethod', False)
    system_methods.add((class_name, selector, is_class))

removed_methods = []
kept_initialization = []

# Process each class
for class_def in bootstrap.get('classes', []):
    class_name = class_def.get('name', '')
    
    # Filter class methods
    if 'classMethods' in class_def:
        original_count = len(class_def['classMethods'])
        filtered_class_methods = []
        
        for method in class_def['classMethods']:
            selector = method.get('selector', '')
            
            # Check if this method exists in system image
            if (class_name, selector, True) in system_methods:
                # It's in the system image, keep it
                filtered_class_methods.append(method)
            elif selector in ['initialize', 'init'] and class_name in ['Object', 'Behavior', 'Symbol', 
                             'CompiledMethod', 'SystemDictionary', 'ClassOrganizer']:
                # These initialize class variables that might be needed
                # BUT since no Smalltalk runs during bootstrap, we don't need them either!
                removed_methods.append(f"{class_name} class>>{selector}")
                print(f"Removing {class_name} class>>{selector} (not needed - no execution during bootstrap)")
            elif selector == 'new' and class_name in ['Boolean', 'UndefinedObject']:
                # Safety guards - but again, not needed during bootstrap since no code runs
                removed_methods.append(f"{class_name} class>>{selector}")
                print(f"Removing {class_name} class>>{selector} (safety guard not needed during bootstrap)")
            else:
                # Remove all other methods not in system image
                removed_methods.append(f"{class_name} class>>{selector}")
                print(f"Removing {class_name} class>>{selector} (not in system image)")
        
        class_def['classMethods'] = filtered_class_methods
        
        if original_count != len(filtered_class_methods):
            print(f"  {class_name}: Reduced class methods from {original_count} to {len(filtered_class_methods)}")
    
    # Filter instance methods
    if 'methods' in class_def:
        original_count = len(class_def['methods'])
        filtered_methods = []
        
        for method in class_def['methods']:
            selector = method.get('selector', '')
            
            # Check if this method exists in system image
            if (class_name, selector, False) in system_methods:
                filtered_methods.append(method)
            else:
                # Remove methods not in system image
                removed_methods.append(f"{class_name}>>{selector}")
                print(f"Removing {class_name}>>{selector} (instance method not in system image)")
        
        class_def['methods'] = filtered_methods
        
        if original_count != len(filtered_methods):
            print(f"  {class_name}: Reduced instance methods from {original_count} to {len(filtered_methods)}")

print(f"\n=== Summary ===")
print(f"Total methods removed: {len(removed_methods)}")
print(f"\nRemoved methods by category:")

# Categorize removed methods
convenience = [m for m in removed_methods if 'with:' in m or 'key:value:' in m or 'fromString:' in m]
initialization = [m for m in removed_methods if 'init' in m.lower()]
safety = [m for m in removed_methods if '>>new' in m and ('Boolean' in m or 'UndefinedObject' in m)]
other = [m for m in removed_methods if m not in convenience and m not in initialization and m not in safety]

if convenience:
    print(f"\nConvenience methods ({len(convenience)}):")
    for m in convenience:
        print(f"  - {m}")

if initialization:
    print(f"\nInitialization methods ({len(initialization)}):")
    for m in initialization:
        print(f"  - {m}")

if safety:
    print(f"\nSafety guards ({len(safety)}):")
    for m in safety:
        print(f"  - {m}")

if other:
    print(f"\nOther methods ({len(other)}):")
    for m in other:
        print(f"  - {m}")

if removed_methods:
    # Create backup
    print(f"\nCreating backup: Bootstrap/bootstrap.json.backup3")
    save_json('Bootstrap/bootstrap.json.backup3', load_json('Bootstrap/bootstrap.json'))
    
    # Save updated file
    print(f"Saving updated Bootstrap/bootstrap.json...")
    save_json('Bootstrap/bootstrap.json', bootstrap)
    print("Done! All unnecessary methods removed.")
else:
    print("\nNo methods to remove.")