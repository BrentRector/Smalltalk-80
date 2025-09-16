#!/usr/bin/env python3
"""
Fix Method Field Names to Match System Image
Updates bootstrap.json method field names to match system image format
"""

import json

def fix_method_field_names():
    """Fix method field names in bootstrap to match system image"""
    
    print("Loading bootstrap JSON...")
    with open("Bootstrap/bootstrap.json", 'r', encoding='utf-8') as f:
        bootstrap = json.load(f)
    
    changes_made = 0
    
    print(f"Processing {len(bootstrap['classes'])} bootstrap classes...")
    
    for bootstrap_class in bootstrap["classes"]:
        class_name = bootstrap_class["name"]
        
        if "methods" in bootstrap_class:
            for method in bootstrap_class["methods"]:
                # Fix argumentCount -> numArgs
                if "argumentCount" in method:
                    method["numArgs"] = method["argumentCount"]
                    del method["argumentCount"]
                    changes_made += 1
                
                # Fix temporaryCount -> numTemps
                if "temporaryCount" in method:
                    method["numTemps"] = method["temporaryCount"]
                    del method["temporaryCount"] 
                    changes_made += 1
                
                # Ensure isClassMethod is present (defaults to False for instance methods)
                if "isClassMethod" not in method:
                    method["isClassMethod"] = False
                    changes_made += 1
        
        # Handle class methods if they exist separately
        if "classMethods" in bootstrap_class:
            for method in bootstrap_class["classMethods"]:
                # Fix argumentCount -> numArgs
                if "argumentCount" in method:
                    method["numArgs"] = method["argumentCount"]
                    del method["argumentCount"]
                    changes_made += 1
                
                # Fix temporaryCount -> numTemps
                if "temporaryCount" in method:
                    method["numTemps"] = method["temporaryCount"]
                    del method["temporaryCount"]
                    changes_made += 1
                
                # Mark as class method
                method["isClassMethod"] = True
                changes_made += 1
    
    print(f"\nMade {changes_made} field name changes")
    
    # Save updated bootstrap
    if changes_made > 0:
        print("Saving updated bootstrap.json...")
        with open("Bootstrap/bootstrap.json", 'w', encoding='utf-8') as f:
            json.dump(bootstrap, f, indent=2, ensure_ascii=False)
        print("Bootstrap method field names updated successfully!")
    else:
        print("No changes needed - field names already match")
    
    return changes_made

if __name__ == "__main__":
    changes = fix_method_field_names()
    print(f"\nCompleted with {changes} changes made")