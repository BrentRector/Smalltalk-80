#!/usr/bin/env python3
"""
Insert compilation enhancement classes into the bootstrap.json file.
"""

import json
import sys
from pathlib import Path

def main():
    bootstrap_file = Path("Bootstrap/bootstrap.json")
    extracted_file = Path("enhancement_classes_extracted.json")
    
    # Read the bootstrap file
    print("Reading bootstrap.json...")
    with open(bootstrap_file, 'r', encoding='utf-8') as f:
        bootstrap_data = json.load(f)
    
    # Read the extracted classes
    print("Reading extracted enhancement classes...")
    with open(extracted_file, 'r', encoding='utf-8') as f:
        extracted_data = json.load(f)
    
    # Get the current classes count
    original_count = len(bootstrap_data["classes"])
    print(f"Original bootstrap has {original_count} classes")
    
    # We need to merge the methods into the class definitions
    # First, let's organize methods by class
    new_methods = extracted_data["methods"]
    methods_by_class = {}
    for method in new_methods:
        class_name = method["class"]
        if class_name not in methods_by_class:
            methods_by_class[class_name] = []
        methods_by_class[class_name].append(method)
    
    # Insert the new classes and embed their methods
    new_classes = []
    for class_def in extracted_data["classes"]:
        class_name = class_def["name"]
        
        # Add the methods to this class definition
        if class_name in methods_by_class:
            class_def["methods"] = methods_by_class[class_name]
        else:
            class_def["methods"] = []
            
        new_classes.append(class_def)
    
    bootstrap_data["classes"].extend(new_classes)
    
    new_count = len(bootstrap_data["classes"])
    added_count = new_count - original_count
    total_methods = sum(len(methods_by_class.get(c["name"], [])) for c in new_classes)
    print(f"Added {added_count} classes. Total now: {new_count}")
    print(f"Added {total_methods} methods embedded in classes")
    
    # Update the timestamp and comments
    from datetime import datetime
    now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    bootstrap_data["timestamp"] = now
    bootstrap_data["comments"].append(f"Added compilation enhancement classes: {now}")
    bootstrap_data["comments"].append(f"Added classes: {', '.join([c['name'] for c in new_classes])}")
    
    # Create backup
    backup_file = bootstrap_file.with_suffix('.json.backup2')
    print(f"Creating backup at {backup_file}")
    with open(backup_file, 'w', encoding='utf-8') as f:
        json.dump(bootstrap_data, f, indent=2)
    
    # Write the updated bootstrap file
    print(f"Writing updated bootstrap.json...")
    with open(bootstrap_file, 'w', encoding='utf-8') as f:
        json.dump(bootstrap_data, f, indent=2)
    
    print(f"""
Successfully updated bootstrap.json:
- Added {added_count} compilation enhancement classes
- Added {len(new_methods)} methods
- Total classes: {new_count}
- Backup saved to: {backup_file}

Added classes:
{chr(10).join([f"  - {c['name']} ({c['superclass']} subclass)" for c in new_classes])}
""")

if __name__ == "__main__":
    main()