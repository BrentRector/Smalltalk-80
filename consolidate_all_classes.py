#!/usr/bin/env python3
"""
Consolidate all classes into a single bootstrap.json file, removing duplicates
and ensuring proper dependency ordering (superclasses before subclasses).
"""

import json
import sys
from pathlib import Path
from typing import Dict, List, Set

def load_json_file(file_path: Path) -> dict:
    """Load and parse a JSON file."""
    with open(file_path, 'r', encoding='utf-8') as f:
        return json.load(f)

def build_dependency_graph(classes: List[dict]) -> Dict[str, Set[str]]:
    """Build a dependency graph where each class maps to its dependencies (superclasses)."""
    graph = {}
    class_by_name = {cls['name']: cls for cls in classes}
    
    for cls in classes:
        name = cls['name']
        superclass = cls['superclass']
        
        # Initialize dependencies set
        graph[name] = set()
        
        # Add superclass dependency if it exists and is in our class set
        if superclass and superclass != 'nil' and superclass in class_by_name:
            graph[name].add(superclass)
    
    return graph

def topological_sort(classes: List[dict]) -> List[dict]:
    """Sort classes topologically so superclasses come before subclasses."""
    class_by_name = {cls['name']: cls for cls in classes}
    graph = build_dependency_graph(classes)
    
    # Kahn's algorithm for topological sorting
    in_degree = {name: 0 for name in graph}
    for name in graph:
        for dep in graph[name]:
            in_degree[dep] += 1
    
    # Start with classes that have no dependencies
    queue = [name for name, degree in in_degree.items() if degree == 0]
    result = []
    
    while queue:
        # Sort queue for consistent ordering
        queue.sort()
        current = queue.pop(0)
        result.append(class_by_name[current])
        
        # Reduce in-degree of dependent classes
        for name in graph:
            if current in graph[name]:
                in_degree[name] -= 1
                if in_degree[name] == 0:
                    queue.append(name)
    
    if len(result) != len(classes):
        # Circular dependency detected - fall back to original order
        print("Warning: Circular dependency detected, using original order")
        return classes
    
    return result

def merge_class_collections(bootstrap_classes: List[dict], phase3_classes: List[dict], 
                          enhancement_classes: List[dict]) -> List[dict]:
    """Merge all class collections, removing duplicates and preserving the most complete version."""
    merged = {}
    
    def add_classes(classes: List[dict], source_name: str):
        for cls in classes:
            name = cls['name']
            if name in merged:
                # Keep the version with more methods or more recent
                existing_methods = len(merged[name].get('methods', []))
                new_methods = len(cls.get('methods', []))
                if new_methods > existing_methods:
                    print(f"Updating {name} from {source_name} ({new_methods} vs {existing_methods} methods)")
                    merged[name] = cls
                else:
                    print(f"Keeping existing {name} ({existing_methods} vs {new_methods} methods)")
            else:
                print(f"Adding {name} from {source_name}")
                merged[name] = cls
    
    # Add classes in priority order - enhancement classes have priority over others
    add_classes(bootstrap_classes, "bootstrap")
    add_classes(phase3_classes, "phase3")
    add_classes(enhancement_classes, "enhancement")
    
    return list(merged.values())

def embed_methods_in_classes(classes: List[dict], methods: List[dict]) -> List[dict]:
    """Embed methods into their corresponding classes."""
    methods_by_class = {}
    for method in methods:
        class_name = method['class']
        if class_name not in methods_by_class:
            methods_by_class[class_name] = []
        methods_by_class[class_name].append(method)
    
    result = []
    for cls in classes:
        class_name = cls['name']
        class_copy = cls.copy()
        
        # Merge methods - combine existing methods with new ones
        existing_methods = class_copy.get('methods', [])
        new_methods = methods_by_class.get(class_name, [])
        
        # Remove duplicates by selector and isClassMethod
        method_key = lambda m: (m['selector'], m.get('isClassMethod', False))
        existing_by_key = {method_key(m): m for m in existing_methods}
        
        for method in new_methods:
            key = method_key(method)
            if key not in existing_by_key:
                existing_methods.append(method)
        
        class_copy['methods'] = existing_methods
        result.append(class_copy)
    
    return result

def main():
    # Load all class collections
    print("Loading current bootstrap...")
    bootstrap_data = load_json_file(Path("Bootstrap/bootstrap.json"))
    bootstrap_classes = bootstrap_data.get('classes', [])
    
    print("Loading Phase 3 classes...")
    phase3_data = load_json_file(Path("phase3_classes_extracted.json"))
    phase3_classes = phase3_data.get('classes', [])
    phase3_methods = phase3_data.get('methods', [])
    
    print("Loading enhancement classes...")
    enhancement_data = load_json_file(Path("enhancement_classes_extracted.json"))
    enhancement_classes = enhancement_data.get('classes', [])
    enhancement_methods = enhancement_data.get('methods', [])
    
    print(f"\nClass counts:")
    print(f"  Bootstrap: {len(bootstrap_classes)}")
    print(f"  Phase 3: {len(phase3_classes)}")
    print(f"  Enhancement: {len(enhancement_classes)}")
    
    # Embed methods from separate collections into their classes
    phase3_classes_with_methods = embed_methods_in_classes(phase3_classes, phase3_methods)
    enhancement_classes_with_methods = embed_methods_in_classes(enhancement_classes, enhancement_methods)
    
    # Merge all classes, removing duplicates
    print(f"\nMerging classes...")
    all_classes = merge_class_collections(bootstrap_classes, phase3_classes_with_methods, 
                                        enhancement_classes_with_methods)
    
    print(f"Total unique classes: {len(all_classes)}")
    
    # Sort topologically (superclasses before subclasses)
    print(f"\nSorting classes by dependency order...")
    sorted_classes = topological_sort(all_classes)
    
    # Create final bootstrap data
    final_bootstrap = {
        "timestamp": "2025-09-16",
        "comments": [
            "Consolidated bootstrap with all classes (formerly bootstrap + Phase 3)",
            "Classes sorted in dependency order (superclasses before subclasses)",
            "Removed low-level phase, all classes now loaded via bootstrap process",
            f"Total classes: {len(sorted_classes)}"
        ],
        "classes": sorted_classes
    }
    
    # Create backup of original bootstrap
    backup_file = Path("Bootstrap/bootstrap.json.backup_pre_consolidation")
    print(f"\nCreating backup at {backup_file}")
    with open(backup_file, 'w', encoding='utf-8') as f:
        json.dump(bootstrap_data, f, indent=2)
    
    # Write consolidated bootstrap
    output_file = Path("Bootstrap/bootstrap.json")
    print(f"Writing consolidated bootstrap to {output_file}")
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(final_bootstrap, f, indent=2)
    
    print(f"\n=== CONSOLIDATION SUMMARY ===")
    print(f"Final class count: {len(sorted_classes)}")
    print(f"Total methods: {sum(len(cls.get('methods', [])) for cls in sorted_classes)}")
    print(f"Backup saved to: {backup_file}")
    
    # Print class hierarchy for verification
    print(f"\nClass hierarchy (first 20 classes):")
    for i, cls in enumerate(sorted_classes[:20]):
        name = cls['name']
        superclass = cls['superclass']
        method_count = len(cls.get('methods', []))
        print(f"  {i+1:2d}. {name} -> {superclass} ({method_count} methods)")
    
    if len(sorted_classes) > 20:
        print(f"  ... and {len(sorted_classes) - 20} more classes")

if __name__ == "__main__":
    main()