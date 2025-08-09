# Critical Methods for Message-Based Class Installation

## Summary
To install classes via message sends (not compile:), the following 8 critical selectors must be present:

## 1. CompiledMethod Methods

### CompiledMethod class>>new
```smalltalk
new
    <primitive: 70>
    ^self basicNew
```
- **Purpose**: Allocate a new CompiledMethod instance
- **Required**: YES - Essential for creating method objects
- **Status**: MISSING (CompiledMethod has no methods)

### CompiledMethod>>header:
```smalltalk
header: aHeader
    header := aHeader
```
- **Purpose**: Set the method header (contains arg/temp counts and primitive number)
- **Required**: YES - Essential for method configuration
- **Status**: MISSING

### CompiledMethod>>bytecodes:
```smalltalk
bytecodes: aByteArray
    bytecode := aByteArray
```
- **Purpose**: Set the bytecode array
- **Required**: YES - Essential for method code
- **Status**: MISSING

### CompiledMethod>>literals:
```smalltalk
literals: anArray
    literals := anArray
```
- **Purpose**: Set the literal frame (symbols, blocks, etc.)
- **Required**: YES - Essential for method references
- **Status**: MISSING

## 2. MethodDictionary Methods

### MethodDictionary>>at:put:
```smalltalk
at: aSymbol put: aCompiledMethod
    <primitive: 61>
    ^super at: aSymbol put: aCompiledMethod
```
- **Purpose**: Install a compiled method under a selector
- **Required**: YES - Essential for method installation
- **Status**: MISSING (MethodDictionary has no methods)

## 3. Class/Behavior Methods

### Class>>subclass:instanceVariableNames:
```smalltalk
subclass: className instanceVariableNames: instVarString
    "Create a new subclass - simplified version"
    | newClass |
    newClass := self basicNew.
    newClass superclass: self.
    newClass setName: className.
    newClass instanceVariables: (instVarString subStrings).
    Smalltalk at: className put: newClass.
    ^newClass
```
- **Purpose**: Create a new subclass
- **Required**: YES - Essential for class creation
- **Status**: MISSING (Class has no methods)

### Class>>methodDictionary
```smalltalk
methodDictionary
    ^methodDict
```
- **Purpose**: Access the method dictionary for installation
- **Required**: YES - Essential for accessing where to install methods
- **Status**: MISSING

## 4. SystemDictionary Methods

### SystemDictionary>>at:put:
```smalltalk
at: aSymbol put: anObject
    <primitive: 61>
    ^super at: aSymbol put: anObject
```
- **Purpose**: Register class globally in Smalltalk dictionary
- **Required**: YES - Essential for global class registration
- **Status**: MISSING (SystemDictionary has no methods)

## Action Plan

### Phase 1 Enhancement Requirements
Phase 1 must be enhanced to include methods for these classes:
1. **CompiledMethod**: Add new, header:, bytecodes:, literals:
2. **MethodDictionary**: Add at:put:
3. **Class**: Add subclass:instanceVariableNames:, methodDictionary
4. **SystemDictionary**: Add at:put:

### Implementation Strategy
1. These methods must be added to Phase 1 JSON as primitive bootstrap methods
2. They can be minimal implementations - just enough to enable message-based installation
3. Once these are present, Phase 3 can use message sends to install all other methods

### Bytecode for Essential Methods

#### CompiledMethod class>>new
```
Bytecode: [112, 208, 124]  // pushReceiver, send: basicNew, returnTop
```

#### CompiledMethod>>header:
```
Bytecode: [16, 99, 124]  // pushTemp: 0, popIntoRcvrVariable: 3, returnTop
```

#### CompiledMethod>>bytecodes:
```
Bytecode: [16, 97, 124]  // pushTemp: 0, popIntoRcvrVariable: 1, returnTop  
```

#### CompiledMethod>>literals:
```
Bytecode: [16, 98, 124]  // pushTemp: 0, popIntoRcvrVariable: 2, returnTop
```

#### MethodDictionary>>at:put:
```
Bytecode: [112, 16, 17, 243, 124]  // pushReceiver, pushTemp: 0, pushTemp: 1, send: at:put: (super), returnTop
With primitive: 61
```

#### Class>>methodDictionary
```
Bytecode: [1, 124]  // pushRcvrVariable: 1, returnTop
```

#### SystemDictionary>>at:put:
```
Bytecode: [112, 16, 17, 243, 124]  // pushReceiver, pushTemp: 0, pushTemp: 1, send: at:put: (super), returnTop
With primitive: 61
```

## Status Summary
- **Total Critical Methods**: 8
- **Currently Available**: 0
- **Missing**: 8 (100%)

All critical methods are missing because they weren't included in Phase 1 or Phase 2. These must be added to Phase 1 to enable message-based class installation in Phase 3.