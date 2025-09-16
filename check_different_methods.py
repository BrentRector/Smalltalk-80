#!/usr/bin/env python3
import json

with open('vm_system_comparison_results.json', 'r') as f:
    data = json.load(f)

print('Methods with different bytecode:')
for match in data['comparison_results']['structural_matches']:
    name = match['class_name']
    
    # Check instance methods
    instance_details = match['method_comparison']['instance']['method_details']
    for selector, diff in instance_details.items():
        if diff != 'exact_match':
            print(f'  {name}.{selector}: {diff}')
    
    # Check class methods  
    class_details = match['method_comparison']['class']['method_details']
    for selector, diff in class_details.items():
        if diff != 'exact_match':
            print(f'  {name} class.{selector}: {diff}')