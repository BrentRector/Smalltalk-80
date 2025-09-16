#!/usr/bin/env python3
"""
Fix Method Separation Between Instance and Class Methods
Properly separates class methods from instance methods and removes duplicates
"""

import json

def fix_method_separation():
    """Fix the separation of instance and class methods in bootstrap"""
    
    print("Loading bootstrap JSON...")
    with open("Bootstrap/bootstrap.json", 'r', encoding='utf-8') as f:
        bootstrap = json.load(f)
    
    changes_made = 0
    
    print(f"Processing {len(bootstrap['classes'])} bootstrap classes...")
    
    for bootstrap_class in bootstrap["classes"]:
        class_name = bootstrap_class["name"]
        
        if "methods" in bootstrap_class:
            instance_methods = []
            class_methods = []
            
            # Separate methods by type
            for method in bootstrap_class["methods"]:
                if method.get("isClassMethod", False):
                    class_methods.append(method)
                else:
                    instance_methods.append(method)
            
            # Remove duplicates within each type based on selector
            def remove_duplicates(methods_list):
                seen_selectors = set()
                unique_methods = []
                for method in methods_list:
                    selector = method.get("selector", "")
                    if selector not in seen_selectors:
                        seen_selectors.add(selector)
                        unique_methods.append(method)
                return unique_methods
            
            instance_methods = remove_duplicates(instance_methods)
            class_methods = remove_duplicates(class_methods)
            
            # Update the class structure
            original_count = len(bootstrap_class["methods"])
            bootstrap_class["methods"] = instance_methods
            
            if class_methods:
                bootstrap_class["classMethods"] = class_methods
                changes_made += 1
                print(f"  {class_name}: separated {len(instance_methods)} instance + {len(class_methods)} class methods")
            elif original_count != len(instance_methods):
                changes_made += 1
                print(f"  {class_name}: removed {original_count - len(instance_methods)} duplicate instance methods")
    
    print(f"\nMade {changes_made} separation/deduplication changes")
    
    # Save updated bootstrap
    if changes_made > 0:
        print("Saving updated bootstrap.json...")
        with open("Bootstrap/bootstrap.json", 'w', encoding='utf-8') as f:
            json.dump(bootstrap, f, indent=2, ensure_ascii=False)
        print("Bootstrap method separation updated successfully!")
    else:
        print("No changes needed - methods already properly separated")
    
    return changes_made

if __name__ == "__main__":
    changes = fix_method_separation()
    print(f"\nCompleted with {changes} classes updated")