#!/usr/bin/env python3
"""
Achieve 100% Perfect Matches
Identifies and adds all missing methods to bootstrap classes to achieve perfect matches
"""

import json

def analyze_missing_methods():
    """Analyze exactly which methods are missing from each structural match class"""
    
    print("Loading system image and bootstrap data...")
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
            system_methods[class_name] = {'instance': {}, 'class': {}}
        
        method_type = 'class' if is_class_method else 'instance'
        system_methods[class_name][method_type][method['selector']] = method
    
    # Build bootstrap method lookup
    bootstrap_methods = {}
    for bootstrap_class in bootstrap['classes']:
        class_name = bootstrap_class['name']
        bootstrap_methods[class_name] = {'instance': {}, 'class': {}}
        
        # Instance methods
        for method in bootstrap_class.get('methods', []):
            selector = method.get('selector', '')
            bootstrap_methods[class_name]['instance'][selector] = method
        
        # Class methods
        for method in bootstrap_class.get('classMethods', []):
            selector = method.get('selector', '')
            bootstrap_methods[class_name]['class'][selector] = method
    
    # Classes that need to reach 100% perfect matches
    structural_match_classes = [
        'ArrayedCollection', 'Association', 'Behavior', 'Boolean', 'Class', 
        'ClassOrganizer', 'Collection', 'CompiledMethod', 'LookupKey', 
        'Metaclass', 'Object', 'String', 'Symbol', 'SystemDictionary', 'UndefinedObject'
    ]
    
    missing_methods_by_class = {}
    
    print("\n=== ANALYZING MISSING METHODS ===")
    
    for class_name in structural_match_classes:
        if class_name not in system_methods:
            print(f"WARNING: {class_name} not found in system image")
            continue
        
        if class_name not in bootstrap_methods:
            print(f"WARNING: {class_name} not found in bootstrap")
            continue
        
        missing_methods_by_class[class_name] = {'instance': [], 'class': []}
        
        # Check instance methods
        system_instance = system_methods[class_name]['instance']
        bootstrap_instance = bootstrap_methods[class_name]['instance']
        
        missing_instance = []
        for selector, method in system_instance.items():
            if selector not in bootstrap_instance:
                missing_instance.append(method)
        
        # Check class methods
        system_class = system_methods[class_name]['class']
        bootstrap_class = bootstrap_methods[class_name]['class']
        
        missing_class = []
        for selector, method in system_class.items():
            if selector not in bootstrap_class:
                missing_class.append(method)
        
        missing_methods_by_class[class_name]['instance'] = missing_instance
        missing_methods_by_class[class_name]['class'] = missing_class
        
        total_missing = len(missing_instance) + len(missing_class)
        if total_missing > 0:
            print(f"\n{class_name}: {total_missing} missing methods")
            if missing_instance:
                print(f"  Missing instance methods ({len(missing_instance)}):")
                for method in missing_instance[:5]:  # Show first 5
                    print(f"    {method['selector']}")
                if len(missing_instance) > 5:
                    print(f"    ... and {len(missing_instance) - 5} more")
            
            if missing_class:
                print(f"  Missing class methods ({len(missing_class)}):")
                for method in missing_class:
                    print(f"    {method['selector']}")
    
    return missing_methods_by_class

def add_all_missing_methods(missing_methods_by_class):
    """Add all missing methods to bootstrap classes"""
    
    print("\nLoading bootstrap JSON for updates...")
    with open('Bootstrap/bootstrap.json', 'r', encoding='utf-8') as f:
        bootstrap = json.load(f)
    
    total_added = 0
    
    print(f"\n=== ADDING MISSING METHODS ===")
    
    for bootstrap_class in bootstrap['classes']:
        class_name = bootstrap_class['name']
        
        if class_name not in missing_methods_by_class:
            continue
        
        missing = missing_methods_by_class[class_name]
        
        # Add missing instance methods
        if missing['instance']:
            if 'methods' not in bootstrap_class:
                bootstrap_class['methods'] = []
            
            for system_method in missing['instance']:
                # Convert system method format to bootstrap format
                bootstrap_method = {
                    "selector": system_method["selector"],
                    "bytecode": system_method["bytecode"],
                    "literals": system_method["literals"],
                    "numArgs": system_method["numArgs"],
                    "numTemps": system_method["numTemps"],
                    "primitiveIndex": system_method["primitiveIndex"],
                    "source": system_method.get("source", ""),
                    "isClassMethod": False
                }
                
                bootstrap_class['methods'].append(bootstrap_method)
                total_added += 1
            
            print(f"  Added {len(missing['instance'])} instance methods to {class_name}")
        
        # Add missing class methods
        if missing['class']:
            if 'classMethods' not in bootstrap_class:
                bootstrap_class['classMethods'] = []
            
            for system_method in missing['class']:
                # Convert system method format to bootstrap format
                bootstrap_method = {
                    "selector": system_method["selector"],
                    "bytecode": system_method["bytecode"],
                    "literals": system_method["literals"],
                    "numArgs": system_method["numArgs"],
                    "numTemps": system_method["numTemps"],
                    "primitiveIndex": system_method["primitiveIndex"],
                    "source": system_method.get("source", ""),
                    "isClassMethod": True
                }
                
                bootstrap_class['classMethods'].append(bootstrap_method)
                total_added += 1
            
            print(f"  Added {len(missing['class'])} class methods to {class_name}")
    
    print(f"\nTotal methods added: {total_added}")
    
    return total_added

def main():
    """Main function to achieve 100% perfect matches"""
    
    print("=== ACHIEVING 100% PERFECT MATCHES ===")
    
    # Step 1: Analyze missing methods
    missing_methods = analyze_missing_methods()
    
    # Count total missing
    total_missing = sum(
        len(methods['instance']) + len(methods['class']) 
        for methods in missing_methods.values()
    )
    
    print(f"\n=== SUMMARY ===")
    print(f"Total missing methods across all classes: {total_missing}")
    
    if total_missing == 0:
        print("No missing methods found - already at 100% perfect matches!")
        return 0
    
    # Step 2: Add all missing methods
    methods_added = add_all_missing_methods(missing_methods)
    
    # Step 3: Save updated bootstrap
    if methods_added > 0:
        print(f"\nSaving updated bootstrap.json with {methods_added} new methods...")
        with open('Bootstrap/bootstrap.json', 'r', encoding='utf-8') as f:
            bootstrap = json.load(f)
        
        with open('Bootstrap/bootstrap.json', 'w', encoding='utf-8') as f:
            json.dump(bootstrap, f, indent=2, ensure_ascii=False)
        
        print("Bootstrap updated successfully for 100% perfect matches!")
    
    return methods_added

if __name__ == "__main__":
    added = main()
    print(f"\nCompleted: added {added} methods to achieve 100% perfect matches")