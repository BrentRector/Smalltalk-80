import re
import json
from collections import defaultdict, deque

# Read the file
with open('Smalltalk-80.st', 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Find all class definitions and their class variables
class_variables = {}  # Maps class name -> set of class variable names
current_class = None
current_class_vars = set()

for i, line in enumerate(lines, 1):
    # Match class definition
    if re.match(r'^[A-Z]\w+\s+(variable)?[Ss]ubclass:\s+#', line):
        match = re.search(r'#(\w+)', line)
        if match:
            if current_class:
                class_variables[current_class] = current_class_vars
            current_class = match.group(1)
            current_class_vars = set()

    # Match classVariableNames line
    elif current_class and re.match(r"\s*classVariableNames:\s*'", line):
        var_string = re.search(r"classVariableNames:\s*'([^']*)'", line)
        if var_string:
            vars_list = var_string.group(1).split()
            current_class_vars.update(vars_list)

if current_class:
    class_variables[current_class] = current_class_vars

# Find all initialize methods
current_class = None
initialize_methods = []

for i, line in enumerate(lines, 1):
    if re.match(r'^[A-Z]\w+\s+(variable)?[Ss]ubclass:\s+#', line):
        match = re.search(r'#(\w+)', line)
        if match:
            current_class = match.group(1)
    elif re.match(r'^(\w+)\s+class\s*$', line.strip()):
        match = re.search(r'^(\w+)\s+class\s*$', line.strip())
        if match:
            current_class = match.group(1) + ' class'
    elif re.match(r'^initialize\s*$', line.strip()) and current_class:
        initialize_methods.append((i, current_class))

# Function to read method body
def read_method_body(start_line, lines):
    body = []
    i = start_line
    while i < len(lines):
        line = lines[i]
        if re.match(r'^!', line):
            break
        body.append(line.rstrip())
        i += 1
    return '\n'.join(body)

# Build dependency map
dependency_map = {}
global_writes = {}  # Maps global name -> class that writes it

for line_num, class_name in initialize_methods:
    method_body = read_method_body(line_num, lines)
    base_class = class_name.replace(' class', '')

    # Get class variables for this class
    class_vars = class_variables.get(base_class, set())

    # Extract global variable writes
    writes = []
    for line in method_body.split('\n'):
        match = re.search(r'^\s*([A-Z][A-Za-z0-9]*)\s*_', line)
        if match:
            global_name = match.group(1)
            # Only count as global if not a class variable of this class
            if global_name not in class_vars:
                writes.append(global_name)
                global_writes[global_name] = class_name

    # Extract class instantiations
    instantiates = []
    for match in re.finditer(r'\b([A-Z][A-Za-z0-9]*)\s+new(?::|[ \.\[])', method_body):
        class_inst = match.group(1)
        # Skip primitive classes always available
        if class_inst not in ['Array', 'String', 'ByteArray', 'OrderedCollection',
                              'Dictionary', 'Set', 'Semaphore', 'Cursor', 'Form',
                              'PopUpMenu', 'Text', 'SortedCollection',
                              'FormButtonCache', 'InputState', 'Quadrangle',
                              'FormView', 'ScreenController']:
            instantiates.append(class_inst)

    dependency_map[class_name] = {
        'line': line_num,
        'writes': writes,
        'instantiates': list(set(instantiates)),
        'reads_globals': []
    }

# Second pass: identify global reads
for class_name, data in dependency_map.items():
    method_body = read_method_body(data['line'], lines)
    base_class = class_name.replace(' class', '')
    class_vars = class_variables.get(base_class, set())

    for global_name, writer_class in global_writes.items():
        if writer_class != class_name:  # Don't depend on self
            # Check if this global is read
            if re.search(r'\b' + re.escape(global_name) + r'\b', method_body):
                # Make sure it's not our own class variable
                if global_name not in class_vars:
                    data['reads_globals'].append(global_name)

# Build dependency graph using topological sort
def build_dependency_levels():
    # Build adjacency list
    depends_on = defaultdict(set)  # class -> set of classes it depends on

    for class_name, data in dependency_map.items():
        # Depend on classes that write globals we read
        for global_name in data['reads_globals']:
            if global_name in global_writes:
                writer = global_writes[global_name]
                if writer != class_name:
                    depends_on[class_name].add(writer)

        # Depend on classes we instantiate
        for inst_class in data['instantiates']:
            inst_class_key = inst_class + ' class'
            if inst_class_key in dependency_map and inst_class_key != class_name:
                depends_on[class_name].add(inst_class_key)

    # Topological sort using Kahn's algorithm
    in_degree = {cls: 0 for cls in dependency_map}
    for cls in dependency_map:
        for dep in depends_on[cls]:
            in_degree[cls] += 1

    levels = []
    remaining = set(dependency_map.keys())

    while remaining:
        # Find all nodes with no dependencies
        ready = [cls for cls in remaining if in_degree[cls] == 0]

        if not ready:
            # Circular dependency - report and break
            print(f"\nCIRCULAR DEPENDENCY DETECTED among: {list(remaining)[:10]}")
            # Add all remaining to final level
            ready = list(remaining)

        levels.append(sorted(ready))

        # Remove ready nodes and update in-degrees
        for cls in ready:
            remaining.remove(cls)
            # Update dependencies
            for other_cls in remaining:
                if cls in depends_on[other_cls]:
                    in_degree[other_cls] -= 1

    return levels

levels = build_dependency_levels()

# Print results
print("\n=== REFINED DEPENDENCY ANALYSIS ===\n")
print(f"Total initialize methods: {len(initialize_methods)}\n")

for level_num, level_classes in enumerate(levels):
    print(f"\n--- Level {level_num} ({len(level_classes)} classes) ---")
    for class_name in level_classes:
        data = dependency_map[class_name]
        print(f"  {class_name} (line {data['line']})")
        if data['writes']:
            print(f"    Writes globals: {', '.join(sorted(data['writes']))}")
        if data['reads_globals']:
            depends = []
            for g in sorted(data['reads_globals']):
                if g in global_writes:
                    depends.append(f"{g} <- {global_writes[g]}")
            if depends:
                print(f"    Depends on: {', '.join(depends)}")
        if data['instantiates']:
            print(f"    Instantiates: {', '.join(sorted(data['instantiates']))}")

# Create execution order list
print("\n\n=== RECOMMENDED EXECUTION ORDER ===\n")
execution_order = []
for level_num, level_classes in enumerate(levels):
    print(f"Level {level_num}:")
    for class_name in sorted(level_classes):
        print(f"  {class_name} initialize")
        execution_order.append(class_name)

# Export for code generation
output = {
    'execution_order': execution_order,
    'levels': levels,
    'dependencies': dependency_map,
    'global_writes': global_writes
}

with open('init_order.json', 'w') as f:
    json.dump(output, f, indent=2)

print(f"\n\nTotal: {len(execution_order)} init expressions")
print("Detailed data saved to init_order.json")
