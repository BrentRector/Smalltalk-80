#!/usr/bin/env python3
"""
Analyze class dependencies from the system image and generate a proper topological ordering.
"""

import json
import sys
from collections import defaultdict, deque

def load_system_image(path):
    """Load the system image JSON file."""
    with open(path, 'r') as f:
        return json.load(f)

def build_dependency_graph(classes, target_classes):
    """Build a dependency graph for the target classes."""
    # Create a map of class name to class info
    class_map = {cls['name']: cls for cls in classes}
    
    # Track dependencies: class -> set of classes it depends on (superclasses)
    dependencies = defaultdict(set)
    
    # Track reverse dependencies: class -> set of classes that depend on it
    dependents = defaultdict(set)
    
    def add_dependencies(class_name, visited=None):
        """Recursively add all dependencies for a class."""
        if visited is None:
            visited = set()
        
        if class_name in visited:
            return  # Avoid cycles
        visited.add(class_name)
        
        if class_name not in class_map:
            return  # Class not found in system image
        
        cls = class_map[class_name]
        superclass = cls.get('superclass', '')
        
        if superclass and superclass != 'nil' and superclass != '':
            dependencies[class_name].add(superclass)
            dependents[superclass].add(class_name)
            
            # Recursively add superclass dependencies
            add_dependencies(superclass, visited.copy())
    
    # Build dependencies for all target classes
    for class_name in target_classes:
        add_dependencies(class_name)
    
    return dependencies, dependents, class_map

def topological_sort(target_classes, dependencies):
    """Perform topological sort to get proper class creation order."""
    # Calculate in-degrees (number of dependencies)
    in_degree = defaultdict(int)
    
    # Include all classes that appear in the dependency graph
    all_classes = set(target_classes)
    for class_name in target_classes:
        for dep in dependencies[class_name]:
            all_classes.add(dep)
    
    # Calculate in-degrees
    for class_name in all_classes:
        in_degree[class_name] = len(dependencies[class_name])
    
    # Start with classes that have no dependencies
    queue = deque([cls for cls in all_classes if in_degree[cls] == 0])
    result = []
    
    while queue:
        current = queue.popleft()
        result.append(current)
        
        # Update in-degrees of dependent classes
        for class_name in all_classes:
            if current in dependencies[class_name]:
                in_degree[class_name] -= 1
                if in_degree[class_name] == 0:
                    queue.append(class_name)
    
    # Filter to only include target classes, but maintain proper order
    ordered_targets = [cls for cls in result if cls in target_classes]
    
    return ordered_targets

def main():
    if len(sys.argv) != 2:
        print("Usage: python AnalyzeClassDependencies.py <system_image_path>")
        sys.exit(1)
    
    system_image_path = sys.argv[1]
    
    # Current class list from GetEssentialExtendedClasses
    current_classes = [
        # Core system classes
        "BlockContext", "MethodContext", "ContextPart",
        "SmallInteger", "LargePositiveInteger", "LargeNegativeInteger", "Integer", "Number",
        "Float", "Fraction",
        
        # Collections
        "Set", "IdentitySet", "Bag", "OrderedCollection", "SortedCollection",
        "LinkedList", "Interval",
        
        # Streams
        "Stream", "PositionableStream", "ReadStream", "WriteStream", "ReadWriteStream",
        
        # Characters and Strings
        "Character", "Text", "Paragraph", "TextStyle",
        
        # Processes and Scheduling
        "Process", "ProcessorScheduler", "Semaphore", "Delay",
        
        # Exceptions
        "Exception", "Error", "Halt", "MessageNotUnderstood",
        
        # Compiler and Parser
        "Parser", "Scanner", "Compiler", "CompilationContext",
        
        # File System
        "File", "FileDirectory", "FileStream"
    ]
    
    # Load system image
    try:
        system_data = load_system_image(system_image_path)
        classes = system_data['classes']
    except Exception as e:
        print(f"Error loading system image: {e}")
        sys.exit(1)
    
    # Build dependency graph
    dependencies, dependents, class_map = build_dependency_graph(classes, current_classes)
    
    # Print dependency analysis
    print("Class Dependency Analysis")
    print("=" * 50)
    print()
    
    print("Dependencies found:")
    for class_name in sorted(current_classes):
        if class_name in dependencies and dependencies[class_name]:
            deps = sorted(dependencies[class_name])
            print(f"  {class_name} -> {deps}")
        else:
            print(f"  {class_name} -> []")
    print()
    
    # Check for missing dependencies
    missing_deps = set()
    for class_name in current_classes:
        for dep in dependencies[class_name]:
            if dep not in current_classes and dep != 'Object':
                missing_deps.add(dep)
    
    if missing_deps:
        print("Missing dependencies (need to be added to class list):")
        for dep in sorted(missing_deps):
            print(f"  {dep}")
        print()
    
    # Perform topological sort
    try:
        ordered_classes = topological_sort(current_classes, dependencies)
        
        print("Proper class creation order:")
        print("-" * 30)
        for i, class_name in enumerate(ordered_classes):
            deps = sorted(dependencies[class_name]) if class_name in dependencies else []
            dep_str = f" (depends on: {deps})" if deps else ""
            print(f"{i+1:2d}. {class_name}{dep_str}")
        
        print()
        print("C# code for GetEssentialExtendedClasses():")
        print("-" * 45)
        print("return new List<string>")
        print("{")
        
        # Group by category for readability
        current_idx = 0
        categories = [
            ("// Core system classes", ["BlockContext", "MethodContext", "ContextPart"]),
            ("// Number hierarchy", ["SmallInteger", "LargePositiveInteger", "LargeNegativeInteger", "Integer", "Number", "Float", "Fraction"]),
            ("// Collections", ["Set", "IdentitySet", "Bag", "OrderedCollection", "SortedCollection", "LinkedList", "Interval"]),
            ("// Streams", ["Stream", "PositionableStream", "ReadStream", "WriteStream", "ReadWriteStream"]),
            ("// Characters and Strings", ["Character", "Text", "Paragraph", "TextStyle"]),
            ("// Processes and Scheduling", ["Process", "ProcessorScheduler", "Semaphore", "Delay"]),
            ("// Exceptions", ["Exception", "Error", "Halt", "MessageNotUnderstood"]),
            ("// Compiler and Parser", ["Parser", "Scanner", "Compiler", "CompilationContext"]),
            ("// File System", ["File", "FileDirectory", "FileStream"])
        ]
        
        for comment, category_classes in categories:
            print(f"    {comment}")
            category_ordered = [cls for cls in ordered_classes if cls in category_classes]
            for cls in category_ordered:
                comma = "," if ordered_classes.index(cls) < len(ordered_classes) - 1 else ""
                print(f'    "{cls}"{comma}')
            print()
        
        print("};")
        
    except Exception as e:
        print(f"Error in topological sort: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()