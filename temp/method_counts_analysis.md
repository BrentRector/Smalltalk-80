# Method Count Analysis for Phase 1/2 Classes

Based on analysis of the Smalltalk-80 system image, here are the method counts for each class:

## Phase 1 Classes

### Core Classes
- **Object**: ~50 instance methods, ~15 class methods
- **Behavior**: ~80 instance methods, ~10 class methods  
- **Class**: ~40 instance methods, ~15 class methods
- **Metaclass**: ~15 instance methods, ~5 class methods

### Method Infrastructure
- **CompiledMethod**: ~35 instance methods, ~8 class methods
- **MethodDictionary**: ~8 instance methods, ~3 class methods

### Essential Data Types
- **Symbol**: ~23 instance methods, ~8 class methods (verified from system image)
- **String**: ~60 instance methods, ~10 class methods
- **Array**: ~30 instance methods, ~8 class methods
- **ByteArray**: ~25 instance methods, ~5 class methods

### Dictionary Classes
- **Collection**: ~40 instance methods, ~8 class methods
- **Dictionary**: ~35 instance methods, ~5 class methods  
- **SystemDictionary**: ~25 instance methods, ~3 class methods

## Recommended MethodDictionary Sizes

For each class, we should size the MethodDictionary to handle:
- Current Phase 2 methods (if any)
- Full complement from system image
- 50% growth buffer

### Recommended Sizes:
- **Object**: 80 slots (50 + 15 + 15 buffer)
- **Behavior**: 140 slots (80 + 10 + 50 buffer)  
- **Class**: 90 slots (40 + 15 + 35 buffer)
- **Metaclass**: 30 slots (15 + 5 + 10 buffer)
- **CompiledMethod**: 70 slots (35 + 8 + 27 buffer)
- **MethodDictionary**: 20 slots (8 + 3 + 9 buffer)
- **Symbol**: 50 slots (23 + 8 + 19 buffer)
- **String**: 110 slots (60 + 10 + 40 buffer)
- **Array**: 60 slots (30 + 8 + 22 buffer)
- **ByteArray**: 45 slots (25 + 5 + 15 buffer)
- **Collection**: 75 slots (40 + 8 + 27 buffer)
- **Dictionary**: 65 slots (35 + 5 + 25 buffer)
- **SystemDictionary**: 45 slots (25 + 3 + 17 buffer)

## Notes
- These numbers are estimates based on manual inspection of key sections
- The Symbol count (23 instance + 8 class) was verified against the system image
- Buffer allows for future method additions without dictionary replacement
- MethodDictionary capacity should be set during Phase 1/2 creation