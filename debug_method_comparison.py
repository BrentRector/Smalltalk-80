#!/usr/bin/env python3
"""
Debug Method Comparison
Shows exactly what methods exist in bootstrap vs system for problematic classes
"""

import json

def debug_method_comparison():
    """Debug the method comparison for classes with bootstrap-only methods"""
    
    with open('complete_system_final.json', 'r') as f:
        system_image = json.load(f)
    
    with open('Bootstrap/bootstrap.json', 'r') as f:
        bootstrap = json.load(f)
    
    # Build system method lookup
    system_methods = {}
    for method in system_image['methods']:
        class_name = method['class']
        is_class_method = class_name.endswith(' class')
        if is_class_method:
            class_name = class_name[:-6]
        
        if class_name not in system_methods:
            system_methods[class_name] = {'instance': set(), 'class': set()}
        
        method_type = 'class' if is_class_method else 'instance'
        system_methods[class_name][method_type].add(method['selector'])
    
    # Classes that had bootstrap-only methods
    problematic_classes = [
        'ContextPart', 'IdentityDictionary', 'InstructionStream', 'Integer', 
        'Number', 'PositionableStream', 'ReadStream', 'Set', 'Stream', 'WriteStream'
    ]
    
    for class_name in problematic_classes:
        print(f"\n=== {class_name} ===")
        
        # Find bootstrap class
        bootstrap_class = None
        for cls in bootstrap['classes']:
            if cls['name'] == class_name:
                bootstrap_class = cls
                break
        
        if not bootstrap_class:
            print(f"Bootstrap class {class_name} not found!")
            continue
        
        # Show bootstrap methods
        bootstrap_instance = set()
        bootstrap_class_methods = set()
        
        if 'methods' in bootstrap_class:
            bootstrap_instance = {m.get('selector', '') for m in bootstrap_class['methods']}
        
        if 'classMethods' in bootstrap_class:
            bootstrap_class_methods = {m.get('selector', '') for m in bootstrap_class['classMethods']}
        
        # Show system methods
        system_instance = system_methods.get(class_name, {}).get('instance', set())
        system_class_methods = system_methods.get(class_name, {}).get('class', set())
        
        print(f"Instance methods:")
        print(f"  Bootstrap ({len(bootstrap_instance)}): {sorted(bootstrap_instance)}")
        print(f"  System ({len(system_instance)}): {sorted(system_instance)}")
        bootstrap_only_instance = bootstrap_instance - system_instance
        if bootstrap_only_instance:
            print(f"  Bootstrap-only: {sorted(bootstrap_only_instance)}")
        
        print(f"Class methods:")
        print(f"  Bootstrap ({len(bootstrap_class_methods)}): {sorted(bootstrap_class_methods)}")
        print(f"  System ({len(system_class_methods)}): {sorted(system_class_methods)}")
        bootstrap_only_class = bootstrap_class_methods - system_class_methods
        if bootstrap_only_class:
            print(f"  Bootstrap-only: {sorted(bootstrap_only_class)}")

if __name__ == "__main__":
    debug_method_comparison()