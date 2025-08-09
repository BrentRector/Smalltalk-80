# Phase 2 to Phase 3 Class Installation Analysis

This document analyzes the transition from Phase 2 to Phase 3 bootstrap, specifically focusing on the capability to install classes via pure message sends (without using `compile:`).

## Overview

**Phase 2 Current State:** Has all necessary classes but lacks the methods needed for message-based class installation.

**Phase 3 Goal:** Enable complete class installation via low-level message sends by adding the missing critical selectors.

## Complete Class Installation Requirements

Installing a class via message sends requires two high-level steps:

1. **Method Construction** - Build CompiledMethod objects
2. **Class Installation** - Create classes and install methods

## Minimal Selector Set Required

### Method Construction (5 selectors)

| Selector | Receiver | Purpose |
|----------|----------|---------|
| `new` | `CompiledMethod` | Allocate method object |
| `header:` | `CompiledMethod` | Set method header |
| `bytecodes:` | `CompiledMethod` | Set bytecode array |
| `literals:` | `CompiledMethod` | Set literal frame |
| `at:put:` | `MethodDictionary` | Install method under selector |

#### Method Construction Sequence

```smalltalk
"1. Allocate the CompiledMethod object"
CompiledMethod new

"2. Set the header word"
compiledMethod header: <SmallInteger>

"3. Set the bytecodes"
compiledMethod bytecodes: (ByteArray with: 176)

"4. Set the literals"
compiledMethod literals: (Array new: 0)

"5. Install into method dictionary"
Boolean methodDictionary at: #not put: compiledMethod
```

### Class Installation (3 selectors)

| Selector | Receiver | Purpose |
|----------|----------|---------|
| `subclass:instanceVariableNames:` | `Class` or `Behavior` | Create subclass with instance variables |
| `at:put:` | `SystemDictionary` | Register class globally |
| `methodDictionary` | `Class` | Access method dictionary |

#### Class Installation Sequence

```smalltalk
"Step 1: Create Boolean class"
Object subclass: #Boolean instanceVariableNames: ''

"Step 2: Register globally"
Smalltalk at: #Boolean put: Boolean

"Step 3: Install methods manually"
Boolean methodDictionary at: #and: put: <CompiledMethod for 'and:'>
Boolean methodDictionary at: #or: put: <CompiledMethod for 'or:'>
Boolean methodDictionary at: #not put: <CompiledMethod for 'not'>
```

## Phase 2 Current Status

**Total Required Selectors:** 8 critical selectors

**Phase 2 Analysis:**
- ✅ `new` - Present
- ✅ `at:put:` - Present (basic dictionary operations)
- ❌ `header:` - **MISSING**
- ❌ `bytecodes:` - **MISSING**
- ❌ `literals:` - **MISSING**
- ❌ `subclass:instanceVariableNames:` - **MISSING**
- ❌ `methodDictionary` - **MISSING**

**Current Capability:** 2 out of 8 selectors (25% complete)
**Missing:** 6 critical selectors (75% missing)

## What Phase 2 Cannot Do

Phase 2 **cannot** perform class installation via message sends because it lacks:
- All 3 CompiledMethod construction methods
- Both class creation methods  
- Method dictionary access

## Phase 3 Requirements

Phase 3 must add these 6 missing selectors to enable message-based class installation:

### CompiledMethod Enhancement
```json
{
  "name": "CompiledMethod", 
  "methods": [
    {
      "selector": "header:",
      "description": "Set method header word (literals, temps, stack depth, primitive)"
    },
    {
      "selector": "bytecodes:",
      "description": "Set method bytecode array"
    },
    {
      "selector": "literals:",
      "description": "Set method literal frame array"
    }
  ]
}
```

### Class Enhancement  
```json
{
  "name": "Class",
  "methods": [
    {
      "selector": "subclass:instanceVariableNames:",
      "description": "Create new subclass with instance variables"
    },
    {
      "selector": "methodDictionary",
      "description": "Access the class's method dictionary"
    }
  ]
}
```

### SystemDictionary Enhancement
```json
{
  "name": "SystemDictionary", 
  "methods": [
    {
      "selector": "at:put:",
      "description": "Install class as global variable"
    }
  ]
}
```

## Transition Point

**Phase 3 becomes operational for class installation when:**
- All 6 missing selectors are implemented with proper bytecode
- The bytecode correctly manipulates object memory fields
- Classes can be created and methods installed without dependencies on `compile:`

## Key Architectural Insight

The Phase 2→Phase 3 transition is **not** about adding new classes (Phase 2 already has all 33 necessary classes including CompiledMethod, Class, MethodDictionary, etc.). 

Instead, it's about **adding the missing construction methods** to existing classes to enable pure message-send based class installation.

## Bootstrap Implications

This analysis explains why:
1. Phase 2 provides the **object model** (classes and basic operations)
2. Phase 3 provides the **construction capabilities** (class installation methods)
3. The system becomes fully self-hosting when Phase 3 completes

The missing selectors represent the bridge between having the infrastructure (Phase 2) and being able to extend it dynamically (Phase 3).

---
*Generated: 2025-01-10*  
*Context: Phase 2 to Phase 3 bootstrap transition analysis*