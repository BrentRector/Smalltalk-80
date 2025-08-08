# Smalltalk Metaclass Implementation Plan

## Overview
Smalltalk has a sophisticated metaclass system where every class is an instance of its metaclass. This creates a uniform object model where "everything is an object" including classes themselves.

## Current Problems
1. **No Metaclass Creation**: Classes exist but their metaclasses don't
2. **No Class-Metaclass Linking**: Class objects don't point to their metaclass
3. **No Metaclass Hierarchy**: Metaclasses don't inherit properly
4. **No Root Metaclass**: Missing fundamental `Class` and `Metaclass` classes

## Required Architecture

### Metaclass Hierarchy
```
Object
├── Class
│   ├── Metaclass
│   └── [All regular classes inherit from Class for class-side behavior]
└── [All regular classes for instance-side behavior]

Metaclass relationships:
- Object class -> Object class (root metaclass)
- Class class -> Class class  
- MyClass class -> Metaclass instance
- Metaclass class -> Metaclass class
```

### Key Principles
1. **Every class is an instance of its metaclass**
2. **Metaclasses inherit from other metaclasses following the same hierarchy as their instances**
3. **All metaclasses are instances of `Metaclass`**
4. **`Class` provides common class-side behavior**

## Implementation Steps

### Step 1: Create Fundamental Metaclass Infrastructure
- Add `Class` as the root class for all class behavior
- Add `Metaclass` as the class of all metaclasses
- Modify `SystemDictionary.DefineClass()` to create metaclasses automatically

### Step 2: Fix Class Creation Process
- When creating a class, also create its metaclass
- Set the class's `ClassPointer` to point to its metaclass
- Set up proper metaclass inheritance hierarchy

### Step 3: Update Class Loading
- Modify `SystemImageLoader` to handle metaclass creation
- Ensure class-side methods go into the metaclass's method dictionary
- Fix the three-pass loading to include metaclass setup

### Step 4: Fix Message Dispatch
- Update interpreter to handle class-side message sends
- Ensure `receiver.ClassPointer` works for class objects
- Handle metaclass method lookup properly

### Step 5: Bootstrap the System
- Create the fundamental classes (`Object`, `Class`, `Metaclass`) with proper metaclass relationships
- Update initialization system to work with proper metaclasses

## Expected Outcome
After implementation:
- `SmallInteger initialize` will work because `SmallInteger` (the class object) will have proper `ClassPointer` set to `SmallInteger class` (its metaclass)
- Class-side methods will be properly dispatched
- The object model will be uniform and complete
