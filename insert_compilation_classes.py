#!/usr/bin/env python3
"""
Insert compilation infrastructure classes into the bootstrap.json file.
"""

import json
import sys
from pathlib import Path

def main():
    bootstrap_file = Path("Bootstrap/bootstrap.json")
    extracted_file = Path("compilation_classes_extracted.json")
    
    # Read the bootstrap file
    print("Reading bootstrap.json...")
    with open(bootstrap_file, 'r', encoding='utf-8') as f:
        bootstrap_data = json.load(f)
    
    # Read the extracted classes
    print("Reading extracted compilation classes...")
    with open(extracted_file, 'r', encoding='utf-8') as f:
        extracted_data = json.load(f)
    
    # Get the current classes count
    original_count = len(bootstrap_data["classes"])
    print(f"Original bootstrap has {original_count} classes")
    
    # Insert the new classes at the end of the classes array
    new_classes = extracted_data["classes"]
    bootstrap_data["classes"].extend(new_classes)
    
    new_count = len(bootstrap_data["classes"])
    added_count = new_count - original_count
    print(f"Added {added_count} classes. Total now: {new_count}")
    
    # Update the timestamp and comments
    from datetime import datetime
    now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    bootstrap_data["timestamp"] = now
    bootstrap_data["comments"].append(f"Added compilation infrastructure classes: {now}")
    bootstrap_data["comments"].append(f"Added classes: {', '.join([c['name'] for c in new_classes])}")
    
    # Create backup
    backup_file = bootstrap_file.with_suffix('.json.backup')
    print(f"Creating backup at {backup_file}")
    with open(backup_file, 'w', encoding='utf-8') as f:
        json.dump(bootstrap_data, f, indent=2)
    
    # Write the updated bootstrap file
    print(f"Writing updated bootstrap.json...")
    with open(bootstrap_file, 'w', encoding='utf-8') as f:
        json.dump(bootstrap_data, f, indent=2)
    
    print(f"""
Successfully updated bootstrap.json:
- Added {added_count} compilation infrastructure classes
- Total classes: {new_count}
- Backup saved to: {backup_file}

Added classes:
{chr(10).join([f"  - {c['name']} ({c['superclass']} subclass)" for c in new_classes])}
""")

if __name__ == "__main__":
    main()