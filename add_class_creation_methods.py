#!/usr/bin/env python3
"""Add missing class creation methods to bootstrap.json"""

import json

# The two methods to add
methods_to_add = [
    {
        "selector": "name:inEnvironment:subclassOf:instanceVariableNames:variable:words:pointers:classVariableNames:poolDictionaries:category:comment:changed:",
        "argumentCount": 12,
        "temporaryCount": 6,
        "bytecode": [
            16, 208, 209, 137, 0, 11, 112, 36, 227, 129, 104, 17, 135, 131, 96, 125, 125, 226, 129, 104, 17, 135,
            17, 16, 229, 136, 129, 104, 12, 135, 137, 0, 50, 17, 16, 192, 136, 129, 104, 13, 135, 129, 104, 17, 135,
            29, 131, 129, 232, 137, 0, 29, 112, 16, 42, 233, 227, 129, 104, 17, 135, 131, 96, 136, 129, 104, 12, 135,
            129, 104, 17, 135, 112, 16, 235, 136, 129, 104, 13, 135, 125, 226, 125, 137, 0, 9, 112, 16, 235, 136,
            129, 104, 13, 135, 125, 246, 129, 104, 17, 135, 29, 220, 136, 129, 104, 14, 135, 129, 104, 17, 135,
            27, 30, 18, 29, 19, 20, 21, 22, 137, 0, 4, 131, 96, 125, 125, 131, 238, 237, 136, 129, 104, 15, 135,
            129, 104, 17, 135, 31, 137, 0, 13, 30, 223, 129, 104, 17, 135, 29, 136, 129, 104, 14, 135, 125, 226,
            129, 104, 17, 135, 31, 30, 23, 131, 48, 237, 30, 24, 131, 49, 237, 136, 129, 104, 16, 135, 129, 104,
            17, 135, 26, 131, 98, 198, 137, 0, 5, 30, 26, 131, 51, 125, 226, 129, 104, 17, 135, 17, 16, 229, 137,
            0, 15, 17, 16, 131, 131, 131, 84, 129, 104, 17, 135, 17, 16, 30, 193, 125, 226, 129, 104, 17, 135,
            131, 132, 30, 131, 23, 25, 131, 24, 131, 86, 129, 104, 17, 135, 30, 29, 17, 31, 128, 16, 16, 132, 4,
            25, 129, 104, 17, 135, 30, 131, 26, 29, 131, 59, 129, 104, 17, 135, 30, 131, 26, 30, 131, 60, 129,
            104, 17, 135, 28, 137, 0, 8, 131, 133, 131, 29, 30, 131, 62, 125, 137, 0, 8, 131, 133, 131, 29, 30,
            131, 63, 125, 246, 129, 104, 17, 135, 30, 124
        ],
        "literals": [
            "#first", "#isUppercase", "#ifFalse:", "#error:", "Class names must be capitalized",
            "#includesKey:", "#ifTrue:ifFalse:", "#at:", "#isKindOf:", "#,",
            " already exists!!  Proceed will store over it", "#newNamed:", "#copy", "#|",
            "#subclassOf:oldClass:instanceVariableNames:variable:words:pointers:ifBad:",
            "#obsolete", "#declare:", "#sharing:", "#==", "#comment:", "#declare:from:",
            "#at:put:", "#classify:under:", "#name", "#asSymbol",
            "#validateFrom:in:instanceVariableNames:methods:", "#superclass",
            "#removeSubclass:", "#addSubclass:", "#changes", "#changeClass:", "#addClass:",
            "false", "#Behavior", "nil", "#Undeclared", "#SystemOrganization", "#Smalltalk"
        ],
        "primitiveIndex": 0,
        "source": "name:inEnvironment:subclassOf:instanceVariableNames:variable:words:pointers:classVariableNames:poolDictionaries:category:comment:changed: (core class creation method)"
    },
    {
        "selector": "name:inEnvironment:subclassOf:and:instanceVariableNames:variable:words:pointers:classVariableNames:poolDictionaries:category:comment:changed:",
        "argumentCount": 13,
        "temporaryCount": 7,
        "bytecode": [
            16, 208, 209, 137, 0, 11, 112, 36, 227, 129, 104, 18, 135, 131, 100, 125, 125, 226, 129, 104, 18, 135,
            17, 16, 229, 136, 129, 104, 13, 135, 137, 0, 51, 17, 16, 192, 136, 129, 104, 14, 135, 129, 104, 18, 135,
            30, 131, 133, 232, 137, 0, 30, 112, 16, 42, 233, 227, 129, 104, 18, 135, 131, 100, 136, 129, 104, 13, 135,
            129, 104, 18, 135, 112, 16, 19, 251, 136, 129, 104, 14, 135, 125, 226, 125, 137, 0, 10, 112, 16, 19, 251,
            136, 129, 104, 14, 135, 125, 246, 129, 104, 18, 135, 30, 220, 136, 129, 104, 15, 135, 129, 104, 18, 135,
            28, 31, 18, 30, 20, 21, 22, 23, 137, 0, 4, 131, 100, 125, 125, 131, 238, 237, 136, 129, 104, 16, 135,
            129, 104, 18, 135, 128, 16, 16, 137, 0, 13, 31, 223, 129, 104, 18, 135, 30, 136, 129, 104, 15, 135, 125,
            226, 129, 104, 18, 135, 128, 16, 16, 31, 24, 131, 48, 237, 31, 25, 131, 49, 237, 136, 129, 104, 17, 135,
            129, 104, 18, 135, 27, 131, 102, 198, 137, 0, 5, 31, 27, 131, 51, 125, 226, 129, 104, 18, 135, 17, 16,
            229, 137, 0, 15, 17, 16, 131, 135, 131, 84, 129, 104, 18, 135, 17, 16, 31, 193, 125, 226, 129, 104, 18,
            135, 131, 136, 31, 131, 23, 26, 131, 24, 131, 86, 129, 104, 18, 135, 31, 30, 17, 128, 16, 16, 128, 16,
            17, 132, 4, 25, 129, 104, 18, 135, 31, 131, 26, 137, 0, 20, 18, 136, 129, 104, 19, 135, 30, 131, 60,
            129, 104, 18, 135, 128, 16, 19, 31, 131, 61, 125, 203, 129, 104, 18, 135, 29, 137, 0, 8, 131, 137,
            131, 30, 31, 131, 63, 125, 137, 0, 8, 131, 137, 131, 30, 31, 131, 32, 125, 246, 129, 104, 18, 135,
            128, 16, 16, 137, 0, 12, 31, 131, 2, 129, 104, 18, 135, 31, 199, 131, 2, 125, 131, 33, 129, 104, 18,
            135, 31, 124
        ],
        "literals": [
            "#first", "#isUppercase", "#ifFalse:", "#error:", "Class names must be capitalized",
            "#includesKey:", "#ifTrue:ifFalse:", "#at:", "#isKindOf:", "#,",
            " already exists!!  Proceed will store over it", "#newNamed:otherSupers:", "#copy", "#|",
            "#subclassOf:oldClass:instanceVariableNames:variable:words:pointers:ifBad:",
            "#obsolete", "#declare:", "#sharing:", "#==", "#comment:", "#declare:from:",
            "#at:put:", "#classify:under:", "#name", "#asSymbol",
            "#validateFrom:in:instanceVariableNames:methods:", "#superclasses",
            "#do:", "#removeSubclass:", "#addSubclass:", "#changes", "#changeClass:",
            "#addClass:", "#ifTrue:", "#copyMethods", "#class",
            "false", "#Behavior", "nil", "#Undeclared", "#SystemOrganization", "#Smalltalk"
        ],
        "primitiveIndex": 0,
        "source": "name:inEnvironment:subclassOf:and:instanceVariableNames:variable:words:pointers:classVariableNames:poolDictionaries:category:comment:changed: (multiple inheritance class creation)"
    }
]

# Load bootstrap.json
with open('Bootstrap/bootstrap.json', 'r') as f:
    bootstrap = json.load(f)

# Find Metaclass and add the methods
for class_def in bootstrap['classes']:
    if class_def['name'] == 'Metaclass':
        print(f"Found Metaclass with {len(class_def.get('methods', []))} existing methods")
        
        # Add the new methods
        if 'methods' not in class_def:
            class_def['methods'] = []
        
        # Check if methods already exist
        existing_selectors = {m['selector'] for m in class_def['methods']}
        
        for method in methods_to_add:
            if method['selector'] not in existing_selectors:
                class_def['methods'].append(method)
                print(f"Added method: {method['selector']}")
            else:
                print(f"Method already exists: {method['selector']}")
        
        print(f"Metaclass now has {len(class_def['methods'])} methods")
        break
else:
    print("ERROR: Metaclass not found in bootstrap.json")
    exit(1)

# Write back the updated bootstrap.json
with open('Bootstrap/bootstrap.json', 'w') as f:
    json.dump(bootstrap, f, indent=2)

print("Successfully updated bootstrap.json")