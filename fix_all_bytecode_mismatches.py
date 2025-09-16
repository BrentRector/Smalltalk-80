#!/usr/bin/env python3
"""
Fix All Bytecode Mismatches
Analyzes and corrects all bytecode differences between bootstrap and system image
"""

import json

def analyze_bytecode_mismatches():
    """Analyze all bytecode mismatches in detail"""
    
    print("Loading comparison results...")
    with open('vm_system_comparison_results.json', 'r') as f:
        results = json.load(f)
    
    print("Loading system image...")
    with open('complete_system_final.json', 'r') as f:
        system_image = json.load(f)
    
    # Build system method lookup
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
    
    all_issues = []
    
    print("\n=== DETAILED BYTECODE MISMATCH ANALYSIS ===")
    
    for match in results['comparison_results']['structural_matches']:
        class_name = match['class_name']
        
        # Analyze instance methods
        instance_details = match['method_comparison']['instance']['method_details']
        for selector, differences in instance_details.items():
            if differences != 'exact_match':
                print(f"\n{class_name}.{selector} (instance):")
                print(f"  Differences: {differences}")
                
                # Get system method details
                if (class_name in system_methods and 
                    'instance' in system_methods[class_name] and
                    selector in system_methods[class_name]['instance']):
                    
                    sys_method = system_methods[class_name]['instance'][selector]
                    print(f"  System: bytecode={sys_method['bytecode']}, numArgs={sys_method['numArgs']}, numTemps={sys_method['numTemps']}, primitive={sys_method['primitiveIndex']}")
                    
                    all_issues.append({
                        'class': class_name,
                        'selector': selector,
                        'method_type': 'instance',
                        'system_method': sys_method,
                        'differences': differences
                    })
        
        # Analyze class methods
        class_details = match['method_comparison']['class']['method_details']
        for selector, differences in class_details.items():
            if differences != 'exact_match':
                print(f"\n{class_name} class.{selector}:")
                print(f"  Differences: {differences}")
                
                # Get system method details
                if (class_name in system_methods and 
                    'class' in system_methods[class_name] and
                    selector in system_methods[class_name]['class']):
                    
                    sys_method = system_methods[class_name]['class'][selector]
                    print(f"  System: bytecode={sys_method['bytecode']}, numArgs={sys_method['numArgs']}, numTemps={sys_method['numTemps']}, primitive={sys_method['primitiveIndex']}")
                    
                    all_issues.append({
                        'class': class_name,
                        'selector': selector,
                        'method_type': 'class',
                        'system_method': sys_method,
                        'differences': differences
                    })
        
        # Check for bootstrap-only methods
        bootstrap_only_instance = match['method_comparison']['instance']['bootstrap_only_methods']
        bootstrap_only_class = match['method_comparison']['class']['bootstrap_only_methods']
        
        if bootstrap_only_instance:
            print(f"\n{class_name} has {len(bootstrap_only_instance)} bootstrap-only instance methods:")
            print(f"  {', '.join(bootstrap_only_instance)}")
            
        if bootstrap_only_class:
            print(f"\n{class_name} has {len(bootstrap_only_class)} bootstrap-only class methods:")
            print(f"  {', '.join(bootstrap_only_class)}")
    
    print(f"\n=== SUMMARY ===")
    print(f"Found {len(all_issues)} methods with bytecode mismatches")
    
    return all_issues

def fix_bytecode_mismatches(issues):
    """Fix all bytecode mismatches in bootstrap"""
    
    print("\nLoading bootstrap JSON...")
    with open('Bootstrap/bootstrap.json', 'r', encoding='utf-8') as f:
        bootstrap = json.load(f)
    
    changes_made = 0
    
    print(f"Fixing {len(issues)} bytecode mismatches...")
    
    for issue in issues:
        class_name = issue['class']
        selector = issue['selector']
        method_type = issue['method_type']
        system_method = issue['system_method']
        
        # Find the bootstrap class
        bootstrap_class = None
        for cls in bootstrap['classes']:
            if cls['name'] == class_name:
                bootstrap_class = cls
                break
        
        if not bootstrap_class:
            print(f"  ERROR: Bootstrap class {class_name} not found")
            continue
        
        # Find the method to fix
        method_array = bootstrap_class.get('classMethods' if method_type == 'class' else 'methods', [])
        bootstrap_method = None
        
        for method in method_array:
            if method.get('selector') == selector:
                bootstrap_method = method
                break
        
        if not bootstrap_method:
            print(f"  ERROR: Method {class_name}.{selector} ({method_type}) not found in bootstrap")
            continue
        
        # Apply fixes
        print(f"  Fixing {class_name}.{selector} ({method_type})")
        
        bootstrap_method['bytecode'] = system_method['bytecode']
        bootstrap_method['numArgs'] = system_method['numArgs']
        bootstrap_method['numTemps'] = system_method['numTemps']
        bootstrap_method['primitiveIndex'] = system_method['primitiveIndex']
        bootstrap_method['literals'] = system_method['literals']
        
        changes_made += 1
    
    print(f"\nMade {changes_made} bytecode fixes")
    
    return changes_made

def remove_bootstrap_only_methods():
    """Remove methods that exist in bootstrap but not in system"""
    
    print("\nRemoving bootstrap-only methods...")
    
    with open('Bootstrap/bootstrap.json', 'r', encoding='utf-8') as f:
        bootstrap = json.load(f)
    
    with open('complete_system_final.json', 'r') as f:
        system_image = json.load(f)
    
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
    
    changes_made = 0
    
    for bootstrap_class in bootstrap['classes']:
        class_name = bootstrap_class['name']
        
        if class_name not in system_methods:
            continue
        
        # Filter instance methods
        if 'methods' in bootstrap_class:
            original_count = len(bootstrap_class['methods'])
            bootstrap_class['methods'] = [
                method for method in bootstrap_class['methods']
                if method.get('selector') in system_methods[class_name]['instance']
            ]
            removed = original_count - len(bootstrap_class['methods'])
            if removed > 0:
                print(f"  Removed {removed} bootstrap-only instance methods from {class_name}")
                changes_made += removed
        
        # Filter class methods
        if 'classMethods' in bootstrap_class:
            original_count = len(bootstrap_class['classMethods'])
            bootstrap_class['classMethods'] = [
                method for method in bootstrap_class['classMethods']
                if method.get('selector') in system_methods[class_name]['class']
            ]
            removed = original_count - len(bootstrap_class['classMethods'])
            if removed > 0:
                print(f"  Removed {removed} bootstrap-only class methods from {class_name}")
                changes_made += removed
    
    print(f"Removed {changes_made} bootstrap-only methods total")
    
    return changes_made

def main():
    """Main function to fix all bytecode mismatches"""
    
    print("=== FIXING ALL BYTECODE MISMATCHES ===")
    
    # Step 1: Analyze issues
    issues = analyze_bytecode_mismatches()
    
    # Step 2: Fix bytecode mismatches
    bytecode_fixes = fix_bytecode_mismatches(issues)
    
    # Step 3: Remove bootstrap-only methods
    removal_fixes = remove_bootstrap_only_methods()
    
    total_changes = bytecode_fixes + removal_fixes
    
    # Step 4: Save updated bootstrap
    if total_changes > 0:
        print(f"\nSaving updated bootstrap.json with {total_changes} total changes...")
        with open('Bootstrap/bootstrap.json', 'r', encoding='utf-8') as f:
            bootstrap = json.load(f)
        
        with open('Bootstrap/bootstrap.json', 'w', encoding='utf-8') as f:
            json.dump(bootstrap, f, indent=2, ensure_ascii=False)
        
        print("Bootstrap bytecode updated successfully!")
    else:
        print("No changes needed")
    
    return total_changes

if __name__ == "__main__":
    changes = main()
    print(f"\nCompleted with {changes} total changes made")