#!/usr/bin/env python3
"""
Fix bootstrap.json:
1. Move basicNew/basicNew: from Behavior class methods to instance methods
2. Remove unnecessary documentation methods
3. Update with correct bytecode from system image
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

# Build system method index
system_methods = {}
for method in system.get('methods', []):
    class_name = method.get('class', '')
    selector = method.get('selector', '')
    is_class = method.get('isClassMethod', False)
    key = (class_name, selector, is_class)
    system_methods[key] = method

changes_made = []

# Process each class
for class_def in bootstrap.get('classes', []):
    class_name = class_def.get('name', '')
    
    if class_name == 'Behavior':
        print(f"\nProcessing {class_name}...")
        
        # Find basicNew and basicNew: in class methods
        class_methods = class_def.get('classMethods', [])
        methods_to_move = []
        remaining_class_methods = []
        
        for method in class_methods:
            selector = method.get('selector', '')
            if selector in ['basicNew', 'basicNew:']:
                # Get the correct bytecode from system image (instance methods)
                system_key = ('Behavior', selector, False)  # False = instance method
                if system_key in system_methods:
                    system_method = system_methods[system_key]
                    # Update with system bytecode
                    method['bytecode'] = system_method.get('bytecode', [])
                    method['literals'] = system_method.get('literals', [])
                    if 'primitiveIndex' in system_method:
                        method['primitiveIndex'] = system_method['primitiveIndex']
                    if 'primitive' in system_method:
                        method['primitive'] = system_method['primitive']
                    methods_to_move.append(method)
                    changes_made.append(f"Move and update {class_name} class>>{selector} to instance method")
                    print(f"  Moving {selector} from class to instance methods with correct bytecode")
                else:
                    print(f"  WARNING: Could not find {selector} in system image")
                    methods_to_move.append(method)
            elif selector == 'init':
                # Keep init as it's needed for bootstrap
                remaining_class_methods.append(method)
                print(f"  Keeping class method {selector} (bootstrap initialization)")
            else:
                remaining_class_methods.append(method)
        
        # Update class methods
        class_def['classMethods'] = remaining_class_methods
        
        # Add to instance methods
        if 'methods' not in class_def:
            class_def['methods'] = []
        
        # Check if already exist as instance methods
        existing_selectors = {m.get('selector', '') for m in class_def['methods']}
        for method in methods_to_move:
            if method['selector'] not in existing_selectors:
                class_def['methods'].append(method)
            else:
                # Update existing
                for i, m in enumerate(class_def['methods']):
                    if m.get('selector', '') == method['selector']:
                        class_def['methods'][i] = method
                        break
    
    elif class_name == 'Object':
        print(f"\nProcessing {class_name}...")
        
        # Remove documentation methods
        class_methods = class_def.get('classMethods', [])
        filtered_methods = []
        
        for method in class_methods:
            selector = method.get('selector', '')
            if selector in ['howToModifyPrimitives', 'whatIsAPrimitive']:
                changes_made.append(f"Remove {class_name} class>>{selector} (documentation)")
                print(f"  Removing documentation method: {selector}")
            elif selector in ['initialize', 'initializeDependentsFields', 'initializeErrorRecursion']:
                # Keep initialization methods
                filtered_methods.append(method)
                print(f"  Keeping {selector} (needed for bootstrap)")
            else:
                filtered_methods.append(method)
        
        class_def['classMethods'] = filtered_methods

# Also check for other classes with unnecessary methods
print("\nChecking other classes for unnecessary methods...")

for class_def in bootstrap.get('classes', []):
    class_name = class_def.get('name', '')
    
    # Remove 'new' methods from singleton classes
    if class_name in ['Boolean', 'UndefinedObject']:
        class_methods = class_def.get('classMethods', [])
        filtered = []
        for method in class_methods:
            if method.get('selector', '') == 'new':
                # Keep these - they prevent invalid object creation
                filtered.append(method)
                print(f"  Keeping {class_name} class>>new (safety guard)")
        class_def['classMethods'] = filtered

print(f"\n=== Summary ===")
print(f"Total changes made: {len(changes_made)}")
for change in changes_made:
    print(f"  - {change}")

if changes_made:
    # Create backup
    print(f"\nCreating backup: Bootstrap/bootstrap.json.backup2")
    save_json('Bootstrap/bootstrap.json.backup2', load_json('Bootstrap/bootstrap.json'))
    
    # Save updated file
    print(f"Saving updated Bootstrap/bootstrap.json...")
    save_json('Bootstrap/bootstrap.json', bootstrap)
    print("✓ Done!")
else:
    print("\nNo changes needed.")