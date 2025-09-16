#!/usr/bin/env python3
import json

with open('vm_system_comparison_results.json', 'r') as f:
    data = json.load(f)

print('Checking final remaining issues...\n')

for match in data['comparison_results']['structural_matches']:
    name = match['class_name']
    if not match['is_perfect_match']:
        print(f'=== {name} ===')
        print(f"Structure matches: {match['structure_matches']}")
        
        # Check instance methods
        instance = match['method_comparison']['instance']
        print(f"Instance methods: {instance['bootstrap_count']} bootstrap vs {instance['system_count']} system")
        print(f"  Matching: {instance['matching_count']}, Different: {instance['different_bytecode']}")
        print(f"  Bootstrap-only: {instance['bootstrap_only']}, System-only: {instance['system_only']}")
        print(f"  Perfect: {instance['perfect_match']}")
        
        # Check class methods
        cls = match['method_comparison']['class']
        print(f"Class methods: {cls['bootstrap_count']} bootstrap vs {cls['system_count']} system")
        print(f"  Matching: {cls['matching_count']}, Different: {cls['different_bytecode']}")
        print(f"  Bootstrap-only: {cls['bootstrap_only']}, System-only: {cls['system_only']}")
        print(f"  Perfect: {cls['perfect_match']}")
        
        # Check specific method issues
        instance_details = instance.get('method_details', {})
        for selector, diff in instance_details.items():
            if diff != 'exact_match':
                print(f"  Instance {selector}: {diff}")
        
        class_details = cls.get('method_details', {})
        for selector, diff in class_details.items():
            if diff != 'exact_match':
                print(f"  Class {selector}: {diff}")
        
        print()