#!/usr/bin/env python3
"""
Fix Bootstrap Structure to Match System Image
Updates bootstrap.json to have exact same structure as system image
"""

import json

def load_json_file(filename):
    """Load and parse JSON file"""
    with open(filename, 'r', encoding='utf-8') as f:
        return json.load(f)

def save_json_file(filename, data):
    """Save data to JSON file with proper formatting"""
    with open(filename, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)

def fix_bootstrap_structure():
    """Fix bootstrap structure to match system image exactly"""
    
    print("Loading system image and bootstrap data...")
    system_image = load_json_file("complete_system_final.json")
    bootstrap = load_json_file("Bootstrap/bootstrap.json")
    
    # Create lookup for system classes
    system_classes = {cls["name"]: cls for cls in system_image["classes"]}
    
    # Track changes
    changes_made = 0
    
    print(f"Processing {len(bootstrap['classes'])} bootstrap classes...")
    
    for bootstrap_class in bootstrap["classes"]:
        class_name = bootstrap_class["name"]
        
        if class_name in system_classes:
            system_class = system_classes[class_name]
            
            # Check and fix superclass
            if bootstrap_class.get("superclass") != system_class.get("superclass"):
                print(f"Fixing {class_name} superclass: {bootstrap_class.get('superclass')} -> {system_class.get('superclass')}")
                bootstrap_class["superclass"] = system_class.get("superclass")
                changes_made += 1
            
            # Check and fix instance variables
            bootstrap_inst_vars = bootstrap_class.get("instanceVariables", [])
            system_inst_vars = system_class.get("instanceVariables", [])
            
            if bootstrap_inst_vars != system_inst_vars:
                print(f"Fixing {class_name} instance variables:")
                print(f"  From: {bootstrap_inst_vars}")
                print(f"  To:   {system_inst_vars}")
                bootstrap_class["instanceVariables"] = system_inst_vars
                changes_made += 1
            
            # Check and fix class variables
            bootstrap_class_vars = bootstrap_class.get("classVariables", [])
            system_class_vars = system_class.get("classVariables", [])
            
            if bootstrap_class_vars != system_class_vars:
                print(f"Fixing {class_name} class variables:")
                print(f"  From: {bootstrap_class_vars}")
                print(f"  To:   {system_class_vars}")
                bootstrap_class["classVariables"] = system_class_vars
                changes_made += 1
            
            # Add any missing fields
            for field in ["poolDictionaries", "category"]:
                if field in system_class and field not in bootstrap_class:
                    bootstrap_class[field] = system_class[field]
                    print(f"Added {field} to {class_name}: {system_class[field]}")
                    changes_made += 1
        
        else:
            print(f"WARNING: {class_name} not found in system image")
    
    print(f"\nMade {changes_made} structural changes")
    
    # Save updated bootstrap
    if changes_made > 0:
        print("Saving updated bootstrap.json...")
        save_json_file("Bootstrap/bootstrap.json", bootstrap)
        print("Bootstrap structure updated successfully!")
    else:
        print("No changes needed - bootstrap structure already matches")
    
    return changes_made

if __name__ == "__main__":
    changes = fix_bootstrap_structure()
    print(f"\nCompleted with {changes} changes made")