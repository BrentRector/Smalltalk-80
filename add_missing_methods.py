#!/usr/bin/env python3
"""
Add Missing Methods to Bootstrap Classes
Extracts methods from system image and adds them to bootstrap classes that have 0 methods
"""

import json

def add_missing_methods():
    """Add missing methods from system image to bootstrap classes"""
    
    print("Loading system image and bootstrap data...")
    with open("complete_system_final.json", 'r', encoding='utf-8') as f:
        system_image = json.load(f)
    
    with open("Bootstrap/bootstrap.json", 'r', encoding='utf-8') as f:
        bootstrap = json.load(f)
    
    # Create lookup for system classes and their methods
    system_methods = {}
    for method in system_image["methods"]:
        class_name = method["class"]
        # Remove " class" suffix if it's a class method
        if class_name.endswith(" class"):
            class_name = class_name[:-6]
            method["isClassMethod"] = True
        else:
            method["isClassMethod"] = False
            
        if class_name not in system_methods:
            system_methods[class_name] = []
        system_methods[class_name].append(method)
    
    # Track changes
    changes_made = 0
    
    # Classes that need methods added (have 0 bootstrap methods but system methods exist)
    classes_needing_methods = [
        "ContextPart", "IdentityDictionary", "InstructionStream", "Integer", 
        "Number", "PositionableStream", "ReadStream", "Set", "Stream", "WriteStream"
    ]
    
    print(f"Adding methods to {len(classes_needing_methods)} classes...")
    
    for bootstrap_class in bootstrap["classes"]:
        class_name = bootstrap_class["name"]
        
        if class_name in classes_needing_methods and class_name in system_methods:
            print(f"Adding methods to {class_name}...")
            
            # Initialize methods array if it doesn't exist
            if "methods" not in bootstrap_class:
                bootstrap_class["methods"] = []
            
            # Add all methods from system image
            for system_method in system_methods[class_name]:
                # Convert system method format to bootstrap format
                bootstrap_method = {
                    "selector": system_method["selector"],
                    "bytecode": system_method["bytecode"],
                    "literals": system_method["literals"],
                    "numArgs": system_method["numArgs"],
                    "numTemps": system_method["numTemps"],
                    "primitiveIndex": system_method["primitiveIndex"],
                    "source": system_method.get("source", ""),
                    "isClassMethod": system_method["isClassMethod"]
                }
                
                bootstrap_class["methods"].append(bootstrap_method)
                changes_made += 1
            
            print(f"  Added {len(system_methods[class_name])} methods to {class_name}")
    
    print(f"\nMade {changes_made} method additions")
    
    # Save updated bootstrap
    if changes_made > 0:
        print("Saving updated bootstrap.json...")
        with open("Bootstrap/bootstrap.json", 'w', encoding='utf-8') as f:
            json.dump(bootstrap, f, indent=2, ensure_ascii=False)
        print("Bootstrap methods updated successfully!")
    else:
        print("No changes needed - all methods already present")
    
    return changes_made

if __name__ == "__main__":
    changes = add_missing_methods()
    print(f"\nCompleted with {changes} methods added")