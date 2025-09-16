#!/usr/bin/env python3
import json

# Read the current bootstrap file
with open('Bootstrap/bootstrap.json', 'r') as f:
    bootstrap = json.load(f)

# Define Set and IdentityDictionary classes
additional_classes = [
    {
        "name": "Set", 
        "superclass": "Collection",
        "instanceVariables": ["tally"],
        "classVariables": [],
        "methods": [],
        "classMethods": [],
        "instanceMethodCount": 0,
        "classMethodCount": 0
    },
    {
        "name": "IdentityDictionary",
        "superclass": "Dictionary", 
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
new_classes = [cls for cls in additional_classes if cls["name"] not in existing_names]

if not new_classes:
    print("Set and IdentityDictionary already exist in bootstrap")
else:
    print(f"Adding {len(new_classes)} missing classes: {[cls['name'] for cls in new_classes]}")

    # Create a combined list of all classes (existing + new)
    all_classes = bootstrap["classes"].copy()
    all_classes.extend(new_classes)

    # Create a mapping of class name to class object
    class_map = {cls["name"]: cls for cls in all_classes}

    # Topological sort to ensure superclasses come before subclasses
    def topological_sort(classes):
        # Build adjacency list (superclass -> list of subclasses)
        graph = {}
        in_degree = {}
        
        for cls in classes:
            name = cls["name"]
            superclass = cls["superclass"]
            
            in_degree[name] = 0
            if name not in graph:
                graph[name] = []
        
        # Build the graph
        for cls in classes:
            name = cls["name"]
            superclass = cls["superclass"]
            
            if superclass != "nil" and superclass in class_map:
                if superclass not in graph:
                    graph[superclass] = []
                graph[superclass].append(name)
                in_degree[name] += 1
        
        # Topological sort using Kahn's algorithm
        queue = []
        for cls in classes:
            if in_degree[cls["name"]] == 0:
                queue.append(cls["name"])
        
        result = []
        while queue:
            current = queue.pop(0)
            result.append(class_map[current])
            
            # Process neighbors
            for neighbor in graph.get(current, []):
                in_degree[neighbor] -= 1
                if in_degree[neighbor] == 0:
                    queue.append(neighbor)
        
        return result

    # Sort all classes properly
    sorted_classes = topological_sort(all_classes)

    # Update the bootstrap with the sorted classes
    bootstrap["classes"] = sorted_classes

    # Write the updated bootstrap file
    with open('Bootstrap/bootstrap.json', 'w') as f:
        json.dump(bootstrap, f, indent=2)

    print("Successfully added Set and IdentityDictionary to bootstrap.json")
    print(f"Total classes now: {len(bootstrap['classes'])}")

    # Verify the order
    print("\nAdded classes:")
    for cls in new_classes:
        print(f"  {cls['name']} < {cls['superclass']}")