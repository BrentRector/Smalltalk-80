#!/usr/bin/env python3

import json
import sys

def move_basicnew_to_class_methods():
    # Read the bootstrap file
    with open('Bootstrap/bootstrap.json', 'r') as f:
        data = json.load(f)
    
    # Find Behavior class
    behavior_class = None
    for c in data['classes']:
        if c['name'] == 'Behavior':
            behavior_class = c
            break
    
    if not behavior_class:
        print("Error: Behavior class not found")
        return
    
    # Find basicNew methods in instance methods
    methods_to_move = []
    remaining_methods = []
    
    for method in behavior_class.get('methods', []):
        if method.get('selector') in ['basicNew', 'basicNew:']:
            print(f"Moving {method.get('selector')} from instance methods to class methods")
            methods_to_move.append(method)
        else:
            remaining_methods.append(method)
    
    # Update instance methods (remove basicNew methods)
    behavior_class['methods'] = remaining_methods
    
    # Add to class methods
    if 'classMethods' not in behavior_class:
        behavior_class['classMethods'] = []
    
    behavior_class['classMethods'].extend(methods_to_move)
    
    print(f"Moved {len(methods_to_move)} methods to class methods")
    print(f"Behavior now has {len(behavior_class['methods'])} instance methods and {len(behavior_class['classMethods'])} class methods")
    
    # Write back to file
    with open('Bootstrap/bootstrap.json', 'w') as f:
        json.dump(data, f, indent=2)
    
    print("Bootstrap file updated successfully")

if __name__ == '__main__':
    move_basicnew_to_class_methods()