#!/usr/bin/env python3
import json
import sys

# Read the current bootstrap file
with open('Bootstrap/bootstrap.json', 'r') as f:
    bootstrap = json.load(f)

# Define the missing intermediate classes in dependency order
missing_classes = [
    # Stream hierarchy - add after Object
    {
        "name": "Stream", 
        "superclass": "Object",
        "instanceVariables": [],
        "classVariables": [],
        "methods": [],
        "classMethods": [],
        "instanceMethodCount": 0,
        "classMethodCount": 0
    },
    {
        "name": "PositionableStream",
        "superclass": "Stream", 
        "instanceVariables": ["collection", "position", "readLimit"],
        "classVariables": [],
        "methods": [],
        "classMethods": [],
        "instanceMethodCount": 0,
        "classMethodCount": 0
    },
    {
        "name": "ReadStream",
        "superclass": "PositionableStream",
        "instanceVariables": [],
        "classVariables": [],
        "methods": [],
        "classMethods": [],
        "instanceMethodCount": 0,
        "classMethodCount": 0
    },
    {
        "name": "WriteStream", 
        "superclass": "PositionableStream",
        "instanceVariables": ["writeLimit"],
        "classVariables": [],
        "methods": [],
        "classMethods": [],
        "instanceMethodCount": 0,
        "classMethodCount": 0
    },
    # InstructionStream hierarchy - add after Object
    {
        "name": "InstructionStream",
        "superclass": "Object",
        "instanceVariables": ["sender", "pc"],
        "classVariables": ["SpecialConstants"],
        "methods": [],
        "classMethods": [],
        "instanceMethodCount": 0,
        "classMethodCount": 0
    },
    {
        "name": "ContextPart",
        "superclass": "InstructionStream", 
        "instanceVariables": ["stackp"],
        "classVariables": ["TryPrimitiveMethods"],
        "methods": [],
        "classMethods": [],
        "instanceMethodCount": 0,
        "classMethodCount": 0
    },
    # Number hierarchy - add after Magnitude
    {
        "name": "Number",
        "superclass": "Magnitude",
        "instanceVariables": [],
        "classVariables": [],
        "methods": [],
        "classMethods": [],
        "instanceMethodCount": 0,
        "classMethodCount": 0
    },
    {
        "name": "Integer",
        "superclass": "Number",
        "instanceVariables": [],
        "classVariables": [],
        "methods": [],
        "classMethods": [],
        "instanceMethodCount": 0,
        "classMethodCount": 0
    }
]

# Find existing classes by name to avoid duplicates
existing_names = {cls["name"] for cls in bootstrap["classes"]}

# Filter out classes that already exist
new_classes = [cls for cls in missing_classes if cls["name"] not in existing_names]

print(f"Adding {len(new_classes)} missing classes: {[cls['name'] for cls in new_classes]}")

# Find insertion points
classes = bootstrap["classes"]
insert_points = []

# Find where to insert each class
for new_class in new_classes:
    superclass_name = new_class["superclass"]
    
    # Find the position after the superclass
    insert_pos = None
    for i, cls in enumerate(classes):
        if cls["name"] == superclass_name:
            # Insert after this superclass but before any existing subclasses
            insert_pos = i + 1
            # Check if there are already subclasses of this superclass
            while insert_pos < len(classes) and classes[insert_pos]["superclass"] == superclass_name:
                insert_pos += 1
            break
    
    if insert_pos is not None:
        insert_points.append((insert_pos, new_class))
    else:
        print(f"Warning: Could not find superclass {superclass_name} for {new_class['name']}")

# Sort insert points by position (in reverse order so we don't affect indices)
insert_points.sort(key=lambda x: x[0], reverse=True)

# Insert the new classes
for pos, new_class in insert_points:
    print(f"Inserting {new_class['name']} at position {pos}")
    classes.insert(pos, new_class)

# Write the updated bootstrap file
with open('Bootstrap/bootstrap.json', 'w') as f:
    json.dump(bootstrap, f, indent=2)

print("Successfully added missing intermediate superclasses to bootstrap.json")
print(f"Total classes now: {len(bootstrap['classes'])}")