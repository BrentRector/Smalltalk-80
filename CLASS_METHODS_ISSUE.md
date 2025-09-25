# Class Methods Display Issue

## Problem
Class methods are not displaying in the --print-classes output even though the ClassPrinter code correctly looks for them in the metaclass.

## Root Cause
The cold start bootstrap has a fundamental issue with setting class pointers:

1. Classes are allocated with a temporary SmallInteger (999999) as their class
2. `SetClass()` is called to fix up the class pointers to point to proper metaclasses
3. But `SetClass()` doesn't actually work - it doesn't update the object table
4. So `FetchClassOf()` still returns the SmallInteger

## Evidence
When trying to install a test class method on Behavior:
```
[COLD START] Behavior metaclass is SmallInt(16959)
```

The metaclass should be an OOP to the Behavior metaclass object, not a SmallInteger.

## Impact
- Class methods cannot be installed during bootstrap
- Class methods won't display in --print-classes output
- The class hierarchy isn't fully formed

## Solution Required
ObjectMemory needs a `SetClassOf(OOP object, OOP class)` method that:
1. Updates the object table entry for the object
2. Changes the class pointer in the object's header
3. Allows fixing up classes after initial allocation

## Current Workaround
The system works for instance methods because those are installed directly on the class objects. Only class methods (which need to be installed on metaclasses) are affected.

## Updated Features That Do Work
✅ Literals array now displays for every method (even if empty)
✅ All bytecode bytes display correctly
✅ Instance methods display properly
✅ ClassPrinter correctly attempts to find class methods

## Command Line Output
The --print-classes option now shows:
- Instance methods with their literals array
- Properly formatted bytecode with all bytes
- Would show class methods if the class pointers were correctly set