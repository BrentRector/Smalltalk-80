import json

import sys
filename = sys.argv[1] if len(sys.argv) > 1 else r'D:\Users\brent\OneDrive\Src\Smalltalk-80\system_image.json'
with open(filename) as f:
    data = json.load(f)

# Check OrderedCollection's new: method
for cls in data['classes']:
    if cls['name'] == 'OrderedCollection' and 'classMethods' in cls:
        print(f"Found OrderedCollection class")
        for method in cls['classMethods']:
            if method.get('selector') == 'new:':
                print(f"OrderedCollection new: method found:")
                print(f"  Bytecode: {method.get('bytecode', [])}")
                print(f"  Literals: {method.get('literals', [])}")
                print(f"  Primitive: {method.get('primitiveIndex', 0)}")
                print(f"  Source: {method.get('source', 'no source')}")
                
# Also check what Set's new: looks like
for cls in data['classes']:
    if cls['name'] == 'Set' and 'classMethods' in cls:
        for method in cls['classMethods']:
            if method.get('selector') == 'new:':
                print(f"\nSet new: method (the problematic one):")
                print(f"  Bytecode: {method.get('bytecode', [])}")
                print(f"  Source: {method.get('source', 'no source')}")