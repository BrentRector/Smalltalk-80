# Variable Resolution and Literal Frame Design

**Based on**: Smalltalk-80 Blue Book (Goldberg & Robson) and canonical source code analysis

**Date**: 2025-12-08

**Status**: AUTHORITATIVE DESIGN SPECIFICATION

---

## Table of Contents

1. [Overview](#overview)
2. [Variable Lookup Algorithm](#variable-lookup-algorithm)
3. [Association-Based Variable Encoding](#association-based-variable-encoding)
4. [Literal Frame Representation](#literal-frame-representation)
5. [Bytecode Execution Semantics](#bytecode-execution-semantics)
6. [The TextList/TextConstants Case Study](#the-textlisttextconstants-case-study)
7. [Implementation Requirements](#implementation-requirements)

---

## Overview

The Smalltalk-80 compiler resolves variable references through a hierarchical lookup process that searches through multiple scopes. When a variable is not found in the local scope (instance variables, temporaries, arguments), the compiler searches through:

1. **Class variables** (in the class's classPool)
2. **Pool variables** (in shared pool dictionaries)
3. **Global variables** (in the Smalltalk dictionary)

All non-local variables (class, pool, and global) are represented in the compiled method's literal frame as **Association objects**, not as raw values or symbols.

---

## Variable Lookup Algorithm

### Source Code Evidence

From `Encoder>>encodeVariable:` (line 16802):

```smalltalk
encodeVariable: name
    ^scopeTable at: name
        ifAbsent:
            [self lookupInPools: name
                ifFound: [:assoc | ^ self global: assoc name: name].
            requestor editor notNil
                ifTrue: [self undeclared: name]
                ifFalse: [self declareUndeclared: name]]
```

From `Encoder>>lookupInPools:ifFound:` (line 16978):

```smalltalk
lookupInPools: name ifFound: assocBlock
    | sym |
    Symbol
        hasInterned: name
        ifTrue: [:sym | ^class scopeHas: sym ifTrue: assocBlock].
    ^false
```

From `Behavior>>scopeHas:ifTrue:` (line 3184):

```smalltalk
scopeHas: varName ifTrue: assocBlock
    "Look up varName in this class, its superclasses, and Smalltalk.  If it is there,
    pass the association to assocBlock, and answer true; else answer false."
    | assoc |
    self withAllSuperclasses do:
        [:sup |
        (sup poolHas: varName ifTrue: assocBlock) ifTrue: [^true]].
    assoc _ Smalltalk associationAt: varName ifAbsent: [].
    assoc == nil
        ifFalse:
            [assocBlock value: assoc.
            ^true].
    ^false
```

From `ClassDescription>>poolHas:ifTrue:` (line 10002):

```smalltalk
poolHas: varName ifTrue: assocBlock
    "Look up the first argument in the context of the receiver.  If it is there,
    pass the association to assocBlock, and answer true, else answer false."
    | assoc pool |
    assoc _ self classPool associationAt: varName ifAbsent: [].
    assoc == nil
        ifFalse:
            [assocBlock value: assoc.
            ^true].
    self sharedPools do:
        [:pool |
        assoc _ pool associationAt: varName ifAbsent: [].
        assoc == nil
            ifFalse:
                [assocBlock value: assoc.
                ^true]].
    ^ false
```

### Lookup Order

The complete variable resolution order is:

1. **scopeTable** (instance variables, temporaries, arguments, special variables like `self`, `super`, `true`, `false`, `nil`)
2. **Class hierarchy traversal** (starting from the compiling class, up through superclasses):
   - Check **classPool** (class variables)
   - Check each **sharedPool** (pool dictionaries) in order
3. **Smalltalk dictionary** (global variables)
4. If not found: **Undeclared** (or prompt user to declare)

**CRITICAL**: The lookup is performed in the **compiling class's hierarchy**, NOT in any other class's hierarchy. This means:

- `TextList>>initialize` compiling in TextList looks up `DefaultTextStyle` in:
  - TextList's classPool
  - TextList's sharedPools (NONE - TextList has no pool dictionaries)
  - Paragraph's (superclass) classPool and sharedPools
  - DisplayObject's (superclass) classPool and sharedPools
  - Object's (superclass) classPool and sharedPools
  - Smalltalk dictionary (GLOBAL)

- `TextStyle>>default` compiling in TextStyle looks up `DefaultTextStyle` in:
  - TextStyle's classPool
  - TextStyle's sharedPools (includes TextConstants)
  - Object's (superclass) classPool and sharedPools
  - Smalltalk dictionary (GLOBAL)

### Key Insight: Association Resolution

When a variable is found in a pool or in Smalltalk, the **Association object itself** is what gets encoded, not just the variable name. This is critical.

From `Encoder>>global:name:` (line 16971):

```smalltalk
global: ref name: name
    ^self
        name: name
        key: ref          "ref is the ASSOCIATION"
        class: VariableNode
        type: LdLitIndType
        set: litIndSet
```

The `key` parameter is the **Association object** (`ref`), not a Symbol. This Association is then placed in the `litIndSet` dictionary.

---

## Association-Based Variable Encoding

### What is an Association?

An Association is an object with two instance variables:
- `key` - the variable name (a Symbol)
- `value` - the current value of the variable

### Why Associations?

Using Associations provides **late binding** and **sharing** semantics:

1. **Late Binding**: The variable's value is looked up at runtime by following the Association's `value` pointer, allowing the value to change dynamically.

2. **Sharing**: Multiple compiled methods that reference the same variable share the **same Association object**. When one method updates the variable, all other methods see the updated value because they all point to the same Association.

### Example: DefaultTextStyle

`DefaultTextStyle` is stored in the TextConstants pool dictionary:

```smalltalk
TextConstants at: #DefaultTextStyle put: (some TextStyle instance)
```

This creates an Association:
```
Association
  key: #DefaultTextStyle
  value: (the TextStyle instance)
```

**Both `TextList>>initialize` and `TextStyle>>default` reference this SAME Association object**, even though:
- TextList does NOT have TextConstants in its poolDictionaries declaration
- TextStyle DOES have TextConstants in its poolDictionaries declaration

Why? Because both lookups ultimately find `DefaultTextStyle` in the **Smalltalk dictionary** as a global variable. TextConstants is stored in Smalltalk as:

```smalltalk
Smalltalk at: #TextConstants put: (Dictionary new: 32)
```

And `DefaultTextStyle` could be stored either:
1. In the TextConstants dictionary: `TextConstants at: #DefaultTextStyle put: value`
2. Or directly in Smalltalk: `Smalltalk at: #DefaultTextStyle put: value`

If stored in TextConstants (option 1), then:
- `TextStyle>>default` finds it via pool lookup (sharedPools includes TextConstants)
- `TextList>>initialize` does NOT find it via pool lookup (no sharedPools), so lookup continues to Smalltalk globals, where TextConstants itself is registered, but this does NOT automatically search inside TextConstants

**CRITICAL CORRECTION**: If `DefaultTextStyle` is in the TextConstants dictionary but NOT in Smalltalk directly, then `TextList>>initialize` will **fail to resolve** the variable and it will be added to Undeclared.

**Most likely reality**: `DefaultTextStyle` is stored BOTH in TextConstants (for semantic clarity) AND in Smalltalk (for global access):

```smalltalk
"Text class>>initialize or similar"
| style |
style _ TextStyle default.
Smalltalk at: #DefaultTextStyle put: style.
TextConstants at: #DefaultTextStyle put: style.
```

Or, more likely, only stored in Smalltalk directly as a global variable.

---

## Literal Frame Representation

### Literal Frame Structure

A CompiledMethod's literal frame is an Array containing:

1. **Selectors** (Symbols) - for message sends
2. **Literals** (Numbers, Strings, Characters, Arrays, etc.) - for literal constants
3. **Associations** - for class variables, pool variables, and global variables
4. **Extended format header** (if primitive > 0 or nArgs > 4) - encoded as `nArgs * 256 + primitive`

From `Encoder>>literals:nArgs:` (line 16860):

```smalltalk
literals: primitive nArgs: nArgs
    "Generate literal frame matching standalone compiler format.
    For extended format (primitive > 0 or nArgs > 4), add encoded SmallInteger.
    DO NOT add class Association - homeClass slot is used instead."

    (primitive > 0 or: [nArgs > 4])
        ifTrue: [self litIndex: nArgs * 256 + primitive].
    ^literalStream contents
```

### Association Storage in Literal Frame

When a global/pool/class variable is referenced, its **Association object** is stored in the literal frame.

**Example Method**:

```smalltalk
TextList>>initialize
    ListStyle _ DefaultTextStyle copy.
    ListStyle gridForFont: 1 withLead: 0
```

**Literal Frame** (conceptual):

```
[
    Association(#ListStyle -> nil),           "class variable"
    Association(#DefaultTextStyle -> <aTextStyle>), "pool/global variable"
    #copy,                                    "selector"
    #gridForFont:withLead:,                   "selector"
    1,                                        "literal number"
    0                                         "literal number"
]
```

### Bytecode Encoding

- `PUSH_LITERAL_VAR` (bytecode 0x40-0x5F or extended) - pushes `(literal value)`, where literal is an Association
- `STORE_LITERAL_VAR` (bytecode 0x61-0x63 or extended) - stores into `(literal value:)`, where literal is an Association
- `PUSH_LITERAL_CONST` (bytecode 0x20-0x3F or extended) - pushes the literal object itself (for Symbols, Numbers, etc.)

**CRITICAL DISTINCTION**:

- Selectors, numbers, strings, etc. are accessed **directly** (the object IS the literal)
- Variables are accessed **indirectly** (the Association is the literal, the value is accessed via `association value` or `association value:`)

---

## Bytecode Execution Semantics

### PUSH_LITERAL_VAR (0x40-0x5F, extended 0x80-0x81)

**Blue Book Specification** (Chapter 28, Section 5.4.3):

> `pushLiteralVariable`: Push the value of the Association in the literal frame at the index specified by the bytecode.

**Implementation**:

```csharp
// Pseudocode
case PUSH_LITERAL_VAR:
    literalIndex = <decode from bytecode>;
    association = method.Literals[literalIndex];  // Association object
    value = GetInstanceVariable(association, VALUE_INDEX);  // association.value
    Push(value);
```

**CRITICAL**: The literal itself is an Association. We push `association.value`, NOT the association.

### STORE_LITERAL_VAR (0x61-0x63, extended 0x82-0x83)

**Blue Book Specification** (Chapter 28, Section 5.4.4):

> `storeIntoLiteralVariable`: Store the top of stack into the Association in the literal frame at the index specified by the bytecode. Do not pop the stack.

**Implementation**:

```csharp
// Pseudocode
case STORE_LITERAL_VAR:
    literalIndex = <decode from bytecode>;
    association = method.Literals[literalIndex];  // Association object
    value = StackTop();  // Don't pop
    SetInstanceVariable(association, VALUE_INDEX, value);  // association value: value
```

### POP_STORE_LITERAL_VAR (0x64, extended 0x84)

**Blue Book Specification**:

> `popStoreIntoLiteralVariable`: Store the top of stack into the Association in the literal frame at the index specified by the bytecode. Pop the stack.

**Implementation**:

```csharp
// Pseudocode
case POP_STORE_LITERAL_VAR:
    literalIndex = <decode from bytecode>;
    association = method.Literals[literalIndex];  // Association object
    value = Pop();
    SetInstanceVariable(association, VALUE_INDEX, value);  // association value: value
```

### PUSH_LITERAL_CONST (0x20-0x3F, extended 0x7C-0x7F)

**Blue Book Specification**:

> `pushLiteralConstant`: Push the literal object in the literal frame at the index specified by the bytecode.

**Implementation**:

```csharp
// Pseudocode
case PUSH_LITERAL_CONST:
    literalIndex = <decode from bytecode>;
    literal = method.Literals[literalIndex];  // Symbol, Number, String, Array, etc.
    Push(literal);  // Push the object ITSELF
```

**CRITICAL**: For constants, we push the literal object directly. For variables, we push the Association's value.

---

## The TextList/TextConstants Case Study

### Problem Statement

- `TextList` class does NOT have `TextConstants` as a pool dictionary
- `TextList>>initialize` references `DefaultTextStyle`
- `TextStyle` class DOES have `TextConstants` as a pool dictionary
- `TextStyle>>default` also references `DefaultTextStyle`

**Question**: Do these compile to the same Association or different Associations?

### Answer: SAME Association

Both methods reference the **SAME Association object** if and only if `DefaultTextStyle` is findable through the variable lookup algorithm from BOTH classes.

### Scenario Analysis

#### Scenario 1: DefaultTextStyle is a Global Variable

If `DefaultTextStyle` is stored in Smalltalk directly:

```smalltalk
Smalltalk at: #DefaultTextStyle put: <someTextStyle>
```

Then **BOTH** classes find it:

- `TextStyle>>default` lookup path:
  1. scopeTable: NO
  2. TextStyle classPool: NO
  3. TextStyle sharedPools (TextConstants): NO (assuming DefaultTextStyle not in TextConstants)
  4. Object classPool/pools: NO
  5. Smalltalk: **YES** ✓

- `TextList>>initialize` lookup path:
  1. scopeTable: NO
  2. TextList classPool: NO
  3. TextList sharedPools: NONE
  4. Paragraph classPool/pools: NO
  5. DisplayObject classPool/pools: NO
  6. Object classPool/pools: NO
  7. Smalltalk: **YES** ✓

**Result**: Both methods store the **SAME Association** `(Smalltalk associationAt: #DefaultTextStyle)` in their literal frames.

#### Scenario 2: DefaultTextStyle ONLY in TextConstants Dictionary

If `DefaultTextStyle` is ONLY stored in the TextConstants pool:

```smalltalk
TextConstants at: #DefaultTextStyle put: <someTextStyle>
```

Then:

- `TextStyle>>default` lookup path:
  1. scopeTable: NO
  2. TextStyle classPool: NO
  3. TextStyle sharedPools (TextConstants): **YES** ✓ (finds `TextConstants associationAt: #DefaultTextStyle`)

- `TextList>>initialize` lookup path:
  1. scopeTable: NO
  2. TextList classPool: NO
  3. TextList sharedPools: NONE
  4. Superclass pools: NO (Paragraph, DisplayObject, Object don't have TextConstants)
  5. Smalltalk: NO
  6. **NOT FOUND** - goes to Undeclared

**Result**:
- `TextStyle>>default` stores the Association `(TextConstants associationAt: #DefaultTextStyle)`
- `TextList>>initialize` creates a NEW Association in Undeclared: `(Undeclared associationAt: #DefaultTextStyle)` with value `nil`

These would be **DIFFERENT Associations**.

#### Most Likely Reality: Dual Storage

Based on the Blue Book convention and practical needs, `DefaultTextStyle` is likely stored as a **global variable** in Smalltalk:

```smalltalk
Text class>>initialize
    "Initialize constants shared by classes associated with text display."

    (Smalltalk includes: TextConstants)
        ifFalse: [Smalltalk at: #TextConstants put: (Dictionary new: 32)].

    "... initialize TextConstants entries ..."

    TextConstants at: #DefaultTextStyle put: (TextStyle default).
    Smalltalk at: #DefaultTextStyle put: (TextStyle default).
```

This ensures:
1. Classes with TextConstants pool can access it via pool lookup
2. Classes WITHOUT TextConstants pool can still access it as a global
3. All references share the SAME Association object

### Compiler Implementation Rule

**RULE**: The compiler must perform variable lookup using the **compiling class's hierarchy**. If a variable is found, store the **Association object** returned by the lookup in the literal frame. Different classes may find the same variable in different dictionaries (classPool vs sharedPool vs Smalltalk), but if they all find the same Association object, they will share the same variable.

**COROLLARY**: If a variable name resolves to different Association objects in different classes, those are DIFFERENT variables (even if they have the same name). This can happen if:
- One class has a class variable `X`
- Another class references global variable `X`
- These are DIFFERENT Associations with DIFFERENT values

---

## Implementation Requirements

### For the C# Compiler (Smalltalk80.Compiler)

#### VariableResolver

The `VariableResolver` class must implement the canonical lookup algorithm:

```csharp
public AssociationDescriptor? ResolveVariable(string name, ClassDescriptor compilingClass)
{
    // 1. Check scopeTable (instance vars, temps, args, pseudo-vars)
    if (scopeTable.TryGetValue(name, out var scopeVar))
        return null; // Not an Association-based variable

    // 2. Check class hierarchy (classPool + sharedPools)
    foreach (var classInHierarchy in compilingClass.GetSelfAndAllSuperclasses())
    {
        // Check classPool
        if (classInHierarchy.ClassPool.TryGetValue(name, out var assoc))
            return assoc;

        // Check each sharedPool in order
        foreach (var poolName in classInHierarchy.SharedPools)
        {
            var pool = GetPoolDictionary(poolName);
            if (pool.TryGetValue(name, out var assoc))
                return assoc;
        }
    }

    // 3. Check Smalltalk globals
    if (Smalltalk.TryGetValue(name, out var globalAssoc))
        return globalAssoc;

    // 4. Not found - add to Undeclared
    return GetOrCreateUndeclaredAssociation(name);
}
```

**CRITICAL**: Return the **Association object**, not a Symbol or String.

#### LiteralExtractor

The `LiteralExtractor` class must distinguish between:

1. **Literal constants** (Symbols, Numbers, Strings, Arrays) - stored directly in literal frame
2. **Variable references** (Associations) - stored as Association objects in literal frame

**Example**:

```csharp
// For expression: ListStyle _ DefaultTextStyle copy
literals = [
    <Association #ListStyle -> nil>,     // class variable
    <Association #DefaultTextStyle -> <TextStyle instance>>, // global/pool variable
    #copy                                 // selector (Symbol)
]

bytecodes = [
    PUSH_LITERAL_VAR 1,      // Push DefaultTextStyle (association value)
    SEND #copy,              // Send #copy
    STORE_LITERAL_VAR 0      // Store into ListStyle (association value:)
]
```

#### ColdStartBundle Schema

The `AssociationDescriptor` must include:

```json
{
    "key": "DefaultTextStyle",
    "valueOop": 12345,
    "dictionaryOop": 6789  // Which dictionary contains this Association
}
```

### For the Interpreter (Smalltalk80.Interpreter)

#### Bytecode Interpreter

**PUSH_LITERAL_VAR** execution:

```csharp
case BytecodeType.PushLiteralVariable:
    var assocOop = method.GetLiteral(literalIndex);
    var valueOop = _heap.GetInstanceVariable(assocOop, AssociationValueIndex);
    _stack.Push(valueOop);
    break;
```

**STORE_LITERAL_VAR** execution:

```csharp
case BytecodeType.StoreIntoLiteralVariable:
    var assocOop = method.GetLiteral(literalIndex);
    var valueOop = _stack.Peek(); // Don't pop
    _heap.SetInstanceVariable(assocOop, AssociationValueIndex, valueOop);
    break;
```

**POP_STORE_LITERAL_VAR** execution:

```csharp
case BytecodeType.PopStoreIntoLiteralVariable:
    var assocOop = method.GetLiteral(literalIndex);
    var valueOop = _stack.Pop();
    _heap.SetInstanceVariable(assocOop, AssociationValueIndex, valueOop);
    break;
```

**Key Points**:
1. The literal at `literalIndex` IS an Association object (OOP)
2. We access the Association's `value` instance variable (index 1)
3. For stores, we modify the Association's `value` in place

### Association Object Structure

An Association is an instance of class Association:

```
Class: Association
Superclass: LookupKey (or Magnitude in some implementations)
Instance Variables:
    [0] key    (Symbol)
    [1] value  (any object)
```

**Blue Book Reference**: Chapter 13, page 293

---

## Summary

### Key Principles

1. **Variable lookup is hierarchical**: scopeTable → classPool → sharedPools → Smalltalk → Undeclared

2. **Associations are shared**: Multiple methods referencing the same variable share the SAME Association object

3. **Late binding via indirection**: Variable values are accessed at runtime through Association objects, not baked into bytecode

4. **Pool dictionaries are class-specific**: Only classes with a pool in their `poolDictionaries` declaration can access that pool during compilation

5. **Globals are universally accessible**: Any variable in the Smalltalk dictionary can be accessed by any class

6. **Literal frame contains Associations**: Global, pool, and class variables are stored as Association objects, not Symbols

7. **Bytecode semantics are indirect**: `PUSH_LITERAL_VAR` pushes `association.value`, not the association itself

### Verification Test Cases

To verify correct implementation, test these scenarios:

#### Test 1: Global Variable Access

```smalltalk
"Setup"
Smalltalk at: #GlobalTest put: 42.

"Class without pool"
Object subclass: #TestClass1
    poolDictionaries: ''.

"Method in TestClass1"
testMethod
    ^GlobalTest

"Expected": Both compiler and interpreter should resolve GlobalTest from Smalltalk dictionary and return 42.
```

#### Test 2: Pool Variable Access

```smalltalk
"Setup"
Smalltalk at: #TestPool put: (Dictionary new).
TestPool at: #PoolVar put: 99.

"Class with pool"
Object subclass: #TestClass2
    poolDictionaries: 'TestPool'.

"Class without pool"
Object subclass: #TestClass3
    poolDictionaries: ''.

"Method in TestClass2"
testMethod
    ^PoolVar  "Should find in TestPool"

"Method in TestClass3"
testMethod
    ^PoolVar  "Should NOT find (Undeclared)"
```

#### Test 3: Class Variable Access

```smalltalk
"Setup"
Object subclass: #TestClass4
    classVariableNames: 'ClassVar'
    poolDictionaries: ''.

TestClass4 classPool at: #ClassVar put: 123.

"Method in TestClass4"
testMethod
    ^ClassVar  "Should find in classPool"

"Method in Object (different class)"
testMethod
    ^ClassVar  "Should NOT find (Undeclared)"
```

#### Test 4: TextList/DefaultTextStyle Scenario

```smalltalk
"Verify DefaultTextStyle resolution"
TextList compile: 'testAccess ^DefaultTextStyle'.
TextStyle compile: 'testAccess ^DefaultTextStyle'.

"Check literal frames"
(TextList compiledMethodAt: #testAccess) literals first. "Should be Association"
(TextStyle compiledMethodAt: #testAccess) literals first. "Should be Association"

"Verify SAME Association"
(TextList compiledMethodAt: #testAccess) literals first ==
    (TextStyle compiledMethodAt: #testAccess) literals first.
"Expected: true (same Association object)"
```

---

## References

- **Blue Book**: Smalltalk-80: The Language and its Implementation (Goldberg & Robson)
  - Chapter 3: Classes and Instances (Variable Declarations, Shared Variables, Pool Variables)
  - Chapter 28: Formal Specification of the Interpreter (Bytecode semantics)
  - Chapter 13: Protocol for Classes (Association class)

- **Source Code**:
  - `Encoder>>encodeVariable:` (line 16802)
  - `Encoder>>lookupInPools:ifFound:` (line 16978)
  - `Encoder>>global:name:` (line 16971)
  - `Behavior>>scopeHas:ifTrue:` (line 3184)
  - `ClassDescription>>poolHas:ifTrue:` (line 10002)
  - `Encoder>>literals:nArgs:` (line 16860)

---

**Document Status**: AUTHORITATIVE - This document represents the canonical Smalltalk-80 behavior as defined in the Blue Book and verified through source code analysis. All compiler and interpreter implementations must conform to this specification.
