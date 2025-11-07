# Variable Indexing Architecture

## Definitive Specification for Smalltalk-80 Variable Access

**Version**: 2.0.0 - Single-Context Architecture
**Status**: AUTHORITATIVE
**Last Updated**: 2025-11-06

This document defines THE definitive algorithm for how the Smalltalk-80 compiler generates bytecode indices for variable access and how the interpreter resolves these indices at runtime.

**IMPORTANT**: This specification uses the **Single-Context Architecture** where MethodContext is used for both methods and blocks, with blocks having a `lexicalParent` field for scope chain traversal. See [Single-Context-Architecture.md](docs/Single-Context-Architecture.md) for complete architectural details.

This specification supersedes all other documentation and must be followed exactly by both compiler and interpreter.

---

## Table of Contents

1. [Scoping Hierarchy](#1-scoping-hierarchy)
2. [Compiler Index Generation Algorithm](#2-compiler-index-generation-algorithm)
3. [Interpreter Access Algorithm](#3-interpreter-access-algorithm)
4. [The Mapping Contract](#4-the-mapping-contract)
5. [Worked Examples](#5-worked-examples)
6. [Implementation Checklist](#6-implementation-checklist)
7. [Complete Variable Access Mechanisms](#7-complete-variable-access-mechanisms)
8. [Detailed Compiler Implementation Instructions](#8-detailed-compiler-implementation-instructions)
9. [Detailed Interpreter Implementation Instructions](#9-detailed-interpreter-implementation-instructions)
10. [Complete Bytecode Mapping Table](#10-complete-bytecode-mapping-table)
11. [Implementation Examples](#11-implementation-examples)

---

## 1. Scoping Hierarchy

### 1.1 Context Types

Smalltalk-80 has three types of execution contexts:

#### A. Instance Context
- **Scope**: receiver + instance variables
- **Access**: Through receiver (self)
- **Example**:
```smalltalk
Point >> distanceTo: aPoint
    "self x and self y access instance variables"
    ^((self x - aPoint x) squared + (self y - aPoint y) squared) sqrt
```

#### B. Method Context
- **Scope**: receiver + arguments + temporaries
- **Stack Layout**: `[arg0, arg1, ..., argN-1, temp0, temp1, ..., tempM-1]`
- **Example**:
```smalltalk
findKeyOrNil: key
    | location length probe pass |
    "Stack: [key, location, length, probe, pass]"
    "Indices: [0, 1, 2, 3, 4]"
```

#### C. Block Context
- **Scope**: Hierarchical access to all enclosing scopes + own arguments + own temporaries
- **Stack Layout**: `[blockArg0, ..., blockArgN-1, blockTemp0, ..., blockTempM-1]`
- **Parent Access**: Via homeContext chain

### 1.2 Nesting Levels

#### Level 0: Method
```smalltalk
method: arg
    | temp |
    "Direct access to arg and temp"
```

#### Level 1: Block in Method
```smalltalk
method: methodArg
    | methodTemp |
    [:blockArg | | blockTemp |
        "Can access: methodArg, methodTemp, blockArg, blockTemp"
    ]
```

#### Level 2: Nested Block
```smalltalk
method: methodArg
    | methodTemp |
    [| outerBlockTemp |
        [| innerBlockTemp |
            "Can access: methodArg, methodTemp, outerBlockTemp, innerBlockTemp"
        ]
    ]
```

#### Level 3: Triple-Nested Block
```smalltalk
method: methodArg
    | methodTemp |
    [| level1Temp |
        [| level2Temp |
            [| level3Temp |
                "Can access ALL temps from all levels"
            ]
        ]
    ]
```

---

## 2. Compiler Index Generation Algorithm

### 2.1 Core Principle: Hierarchical Indexing

The compiler generates **hierarchical indices** that encode the cumulative scope depth:

```
Index = LocalOffset + Σ(BlockLocalCounts of all child scopes)
```

### 2.2 Index Assignment Rules

#### A. Instance Variables
**Rule**: Instance variables always use PUSH_INST_VAR with field slot index.

```smalltalk
Point >> x
    ^x  "Generates: PUSH_INST_VAR 0"

Point >> y
    ^y  "Generates: PUSH_INST_VAR 1"
```

#### B. Method Arguments
**Rule**: Arguments are indexed 0..N-1 in method stack.

```smalltalk
copyFrom: start to: stop
    "start is at index 0, stop is at index 1"
    ^...
```

**Bytecode Generated**:
- Access to `start`: `PUSH_TEMP 0`
- Access to `stop`: `PUSH_TEMP 1`

#### C. Method Temporaries
**Rule**: Temporaries follow arguments at indices ArgCount..ArgCount+TempCount-1.

```smalltalk
findKeyOrNil: key
    | location length probe pass |
    "key at 0, location at 1, length at 2, probe at 3, pass at 4"
```

**Bytecode Generated**:
- Access to `key`: `PUSH_TEMP 0`
- Access to `location`: `PUSH_TEMP 1`
- Access to `length`: `PUSH_TEMP 2`

#### D. Block Arguments (1-Level)
**Rule**: Block accessing parent variables uses hierarchical index.

```smalltalk
select: aBlock
    | newCollection |
    newCollection _ self species new.
    self do: [:each |           "each is block arg at index 0"
        (aBlock value: each)     "aBlock accessed at index 0+1=1"
        ifTrue: [newCollection   "newCollection accessed at index 1+1=2"
                 add: each]].    "each accessed at index 0"
    ^newCollection
```

**Index Calculation for inner block**:
- `each` (block's own arg): index 0
- `aBlock` (method arg): index 0 + blockLocalCount(1) = 1
- `newCollection` (method temp): index 1 + blockLocalCount(1) = 2

#### E. Block Temporaries (1-Level)
**Rule**: Block temps follow block args in the block's stack.

```smalltalk
compute
    | outerTemp |
    outerTemp _ 10.
    ^[:arg | | blockTemp |
        blockTemp _ arg + outerTemp.
        "arg at 0, blockTemp at 1, outerTemp at 0+2=2"
    ] value: 5
```

**Index Calculation**:
- `arg` (block arg): index 0
- `blockTemp` (block temp): index 1
- `outerTemp` (method temp): index 0 + blockLocalCount(2) = 2

#### F. Nested Blocks (2-Level)
**Rule**: Each nesting level adds its blockLocalCount to parent indices.

```smalltalk
test2Level
    | methodTemp |
    methodTemp _ 10.
    ^[| outerBlockTemp |
        outerBlockTemp _ 20.
        [| innerBlockTemp |
            innerBlockTemp _ 30.
            methodTemp + outerBlockTemp + innerBlockTemp
        ] value
    ] value
```

**Index Calculation from innermost block**:
- `innerBlockTemp`: index 0 (own temp)
- `outerBlockTemp`: index 0 + innerLocalCount(1) = 1
- `methodTemp`: index 0 + innerLocalCount(1) + outerLocalCount(1) = 2

#### G. Nested Blocks (3-Level) - CRITICAL CASE
**Rule**: Accumulate ALL intermediate blockLocalCounts.

```smalltalk
m4t3l
    "Method with 4 temps accessed by 3-level nested blocks"
    | t0 t1 t2 t3 |
    t0 _ 10.
    t1 _ 20.
    t2 _ 30.
    t3 _ 40.
    ^[[[t0 + t3] value] value] value
```

**Index Calculation from innermost block**:
1. Innermost block has 0 args + 0 temps = blockLocalCount of 0
2. Middle block has 0 args + 0 temps = blockLocalCount of 0
3. Outer block has 0 args + 0 temps = blockLocalCount of 0
4. Method has 0 args + 4 temps

**Result**:
- `t0`: index 0 + 0 + 0 + 0 = 0
- `t3`: index 3 + 0 + 0 + 0 = 3

**BUT if blocks had locals**:
```smalltalk
m4t3l_complex
    | t0 t1 t2 t3 |
    ^[:a1 | | b1 |
        [:a2 | | b2 |
            [:a3 | | b3 |
                t0 + t3 + a1 + a2 + a3
            ] value: 30
        ] value: 20
    ] value: 10
```

**From innermost block**:
- `a3`: index 0 (own arg)
- `b3`: index 1 (own temp)
- `a2`: index 0 + innerLocalCount(2) = 2
- `b2`: index 1 + innerLocalCount(2) = 3
- `a1`: index 0 + innerLocalCount(2) + middleLocalCount(2) = 4
- `b1`: index 1 + innerLocalCount(2) + middleLocalCount(2) = 5
- `t0`: index 0 + innerLocalCount(2) + middleLocalCount(2) + outerLocalCount(2) = 6
- `t3`: index 3 + innerLocalCount(2) + middleLocalCount(2) + outerLocalCount(2) = 9

### 2.3 Compiler Algorithm (Pseudocode)

```csharp
int CalculateVariableIndex(string varName, Environment env)
{
    // Check current scope
    if (env.Arguments.Contains(varName))
        return env.Arguments[varName];

    if (env.Temporaries.Contains(varName))
        return env.Temporaries[varName];

    // If in block, check parent scopes with accumulation
    if (env.ScopeDepth > 0)
    {
        int accumulated = 0;
        var current = env;

        while (current.Parent != null)
        {
            // Add current block's locals to accumulator
            accumulated += current.ArgumentCount + current.TemporaryCount;

            // Check parent
            var parent = current.Parent;
            if (parent.Arguments.Contains(varName))
                return parent.Arguments[varName] + accumulated;

            if (parent.Temporaries.Contains(varName))
                return parent.Temporaries[varName] + accumulated;

            // Move up
            current = parent;
        }
    }

    // Check instance/class/pool variables...
}
```

---

## 3. Interpreter Access Algorithm

### 3.1 Core Principle: Hierarchical Traversal

The interpreter uses the hierarchical index to traverse the context chain, subtracting blockLocalCounts at each level.

### 3.2 PUSH_TEMP Algorithm (Single-Context Architecture)

**Note**: With the Single-Context Architecture, there is no `IsBlockContext()` check. Instead, we check if `lexicalParent == nil` to determine if we're in a method (nil) or block (non-nil).

```csharp
void PushTemp(int index)
{
    SmalltalkOOP contextOOP = _activeContextOOP;
    int remainingIndex = index;

    while (true)
    {
        // Get local count for this context (args + temps from method header)
        var methodOOP = GetInstanceVariable(contextOOP, CONTEXT_METHOD_INDEX);
        var header = ParseMethodHeader(methodOOP);
        int localCount = header.NumArgs + header.NumTemps;

        if (remainingIndex < localCount)
        {
            // Found it in this context!
            var value = GetIndexedSlot(contextOOP, remainingIndex);
            Push(value);
            return;
        }

        // Not here, traverse to lexical parent
        remainingIndex -= localCount;
        contextOOP = GetInstanceVariable(contextOOP, CONTEXT_LEXICALPARENT_INDEX);

        if (contextOOP == nil)
        {
            throw new InvalidOperationException(
                $"Variable index {index} out of bounds (traversed all contexts)");
        }
    }
}
```

**Key Changes**:
- No type checking (`IsBlockContext()`) - unified MethodContext for everything
- Traverse via `lexicalParent` field instead of `homeContext`
- Loop termination: `lexicalParent == nil` means we've reached the top (method)

### 3.3 STORE_TEMP Algorithm

Identical to PUSH_TEMP but writes instead of reads:

```csharp
void StoreTemp(int index)
{
    // Same traversal as PushTemp
    var context = FindContextForIndex(index);
    var adjustedIndex = CalculateAdjustedIndex(index);

    SetIndexedSlot(context, adjustedIndex, Pop());
}
```

### 3.4 PUSH_INST_VAR Algorithm

```csharp
void PushInstVar(int fieldIndex)
{
    var receiver = GetReceiver(_activeContext);
    var value = GetInstanceVariable(receiver, fieldIndex);
    Push(value);
}
```

---

## 4. The Mapping Contract

### 4.1 Selected Approach: Hierarchical Indices

**Decision**: We use **Hierarchical Indices** where the compiler generates indices that encode cumulative scope depth.

**Contract**:
1. **Compiler**: Generates index = localOffset + Σ(child blockLocalCounts)
2. **Interpreter**: Subtracts blockLocalCounts while traversing parent chain
3. **Invariant**: remainingIndex reaches 0 when correct context is found

### 4.2 Why Hierarchical Over Relative?

**Advantages**:
1. **Single index** encodes both scope and offset
2. **No metadata** needed about variable ownership
3. **Efficient** traversal - simple subtraction at each level
4. **Blue Book compatible** - matches original Smalltalk-80 design

**Alternative Rejected - Relative Indices**:
- Would require encoding which scope owns each variable
- Would need additional metadata in compiled methods
- Would complicate bytecode format

### 4.3 Critical Invariant

```
For any variable V accessed from block B:
  CompilerIndex(V) = InterpreterTraversal(B, V)
```

Where:
- `CompilerIndex(V)` = index generated by compiler
- `InterpreterTraversal(B, V)` = index after traversal from B to V's context

---

## 5. Worked Examples

### Example 1: Simple Method with Temps

```smalltalk
Dictionary >> findKeyOrNil: key
    | location length probe pass |
    location _ self basicSize.
    length _ self basicSize.
    probe _ key hash \\ length + 1.
    pass _ 1.
    ^location
```

**Compiler generates**:
```
PUSH_TEMP 0     ; key (arg at 0)
SEND hash
PUSH_TEMP 2     ; length (temp at 2)
SEND \\
PUSH_LIT 1
SEND +
STORE_TEMP 3    ; probe (temp at 3)
```

**Interpreter execution** (in MethodContext):
- `PUSH_TEMP 0`: Direct access to slot 0 → key
- `PUSH_TEMP 2`: Direct access to slot 2 → length
- `STORE_TEMP 3`: Direct store to slot 3 → probe

### Example 2: 1-Level Block Accessing Method Temp

```smalltalk
Collection >> select: aBlock
    | newCollection |
    newCollection _ self species new.
    self do: [:each |
        (aBlock value: each)
            ifTrue: [newCollection add: each]].
    ^newCollection
```

**For block `[:each | ...]`**:
- Block has 1 arg (each), 0 temps → blockLocalCount = 1
- Method has 1 arg (aBlock), 1 temp (newCollection)

**Compiler generates for block**:
```
PUSH_TEMP 1     ; aBlock (0 + blockLocalCount)
PUSH_TEMP 0     ; each (own arg)
SEND value:
```

**Interpreter execution** (in BlockContext):
- `PUSH_TEMP 1`: index 1 ≥ blockLocalCount(1) → traverse to parent
  - remainingIndex = 1 - 1 = 0
  - In method context, access slot 0 → aBlock
- `PUSH_TEMP 0`: index 0 < blockLocalCount(1) → local access
  - Direct access to slot 0 → each

### Example 3: 2-Level Block Accessing Method Temp

```smalltalk
test2Level
    | methodTemp |
    methodTemp _ 10.
    ^[| outerBlockTemp |
        outerBlockTemp _ 20.
        [| innerBlockTemp |
            innerBlockTemp _ 30.
            methodTemp + outerBlockTemp + innerBlockTemp
        ] value
    ] value
```

**For innermost block**:
- Inner block: 0 args, 1 temp → blockLocalCount = 1
- Outer block: 0 args, 1 temp → blockLocalCount = 1
- Method: 0 args, 1 temp

**Compiler generates for innermost block**:
```
PUSH_TEMP 2     ; methodTemp (0 + 1 + 1)
PUSH_TEMP 1     ; outerBlockTemp (0 + 1)
SEND +
PUSH_TEMP 0     ; innerBlockTemp (own)
SEND +
```

**Interpreter execution**:
- `PUSH_TEMP 2`:
  - index 2 ≥ blockLocalCount(1) → traverse
  - remainingIndex = 2 - 1 = 1
  - In outer block: 1 ≥ blockLocalCount(1) → traverse
  - remainingIndex = 1 - 1 = 0
  - In method context: access slot 0 → methodTemp

### Example 4: 3-Level Block (Complex Case)

```smalltalk
testComplex
    | m0 m1 |
    m0 _ 10.
    m1 _ 20.
    ^[:a1 | | t1 |
        t1 _ 100.
        [:a2 | | t2 |
            t2 _ 200.
            [:a3 | | t3 |
                t3 _ 300.
                m0 + m1 + a1 + t1 + a2 + t2 + a3 + t3
            ] value: 7
        ] value: 5
    ] value: 3
```

**For innermost block**:
- Level 3: 1 arg (a3), 1 temp (t3) → blockLocalCount = 2
- Level 2: 1 arg (a2), 1 temp (t2) → blockLocalCount = 2
- Level 1: 1 arg (a1), 1 temp (t1) → blockLocalCount = 2
- Method: 0 args, 2 temps (m0, m1)

**Compiler generates**:
```
PUSH_TEMP 6     ; m0 (0 + 2 + 2 + 2)
PUSH_TEMP 7     ; m1 (1 + 2 + 2 + 2)
SEND +
PUSH_TEMP 4     ; a1 (0 + 2 + 2)
SEND +
PUSH_TEMP 5     ; t1 (1 + 2 + 2)
SEND +
PUSH_TEMP 2     ; a2 (0 + 2)
SEND +
PUSH_TEMP 3     ; t2 (1 + 2)
SEND +
PUSH_TEMP 0     ; a3 (own arg)
SEND +
PUSH_TEMP 1     ; t3 (own temp)
SEND +
```

### Example 5: The Failing Case (findKeyOrNil:)

```smalltalk
findKeyOrNil: key
    | location length probe pass |
    ...
    [location < length] whileTrue: [
        probe _ probe + 1.
        ...
    ].
```

**BEFORE FIX** (Wrong):
```
Block [:| ... probe _ probe + 1 ...]
- Compiler stored: Temporaries["probe"] = 3 (absolute slot)
- Compiler generated: PUSH_TEMP 3, STORE_TEMP 3
- Interpreter expected: relative temp index (probe is temp 2)
- Result: Accessed wrong variable!
```

**AFTER FIX** (Correct):
```
Block [:| ... probe _ probe + 1 ...]
- Method has 1 arg, 4 temps
- Block has 0 args, 0 temps → blockLocalCount = 0
- probe is at method slot 3 (arg@0, temps@1-4)
- Compiler generates: PUSH_TEMP 3 (3 + 0)
- Interpreter: 3 ≥ 0 → traverse to method, access slot 3 → probe ✓
```

---

## 6. Implementation Checklist

### 6.1 Compiler Changes

- [x] **GetVariableIndex()** - Convert absolute to relative for parent temps
  - Lines 95-110: `parentRelativeTempIndex = parentTempIndex - Parent.ArgumentCount`
- [x] **PushVariable()** - Same conversion when pushing
  - Lines 2528-2530: Applied same fix
- [ ] **StoreVariable()** - Apply same fix for stores
- [ ] **PopVariable()** - Apply same fix for pop-and-store

### 6.2 Interpreter Changes

- [x] **Hierarchical traversal** implemented correctly
- [x] **blockLocalCount** calculation correct
- [x] **Context chain traversal** working
- [ ] **Validate** all context types handled (MethodContext, BlockContext, BlockClosure)

### 6.3 Test Cases

- [ ] Simple method with args and temps
- [ ] 1-level block accessing method variables
- [ ] 2-level nested blocks
- [ ] 3-level nested blocks
- [ ] Blocks with own args and temps
- [ ] Mixed access patterns (instance vars + temps)
- [ ] Loop blocks (whileTrue:, whileFalse:)
- [ ] Collection blocks (do:, select:, collect:)

### 6.4 Migration Strategy

**This is NOT a breaking change** - it's a bug fix that makes the system work correctly:

1. **Phase 1**: Fix compiler (DONE)
   - Updated GetVariableIndex() and PushVariable()
   - Regenerate coldstart.json with fixed bytecode

2. **Phase 2**: Verify interpreter (PENDING)
   - Confirm hierarchical traversal is correct
   - Add defensive validation

3. **Phase 3**: Test thoroughly
   - Run all init expressions
   - Verify Form initialize completes
   - Test complex block patterns

---

## Appendix: Debug Output Format

For debugging variable access issues, use this format:

```
[VARIABLE ACCESS] method=findKeyOrNil: var=probe
  Compiler: index=3, scope=method, slot=3
  Interpreter: traversal=[block(0) → method], final=3
  Value: OOP(12345) SmallInteger(1)
```

---

## 7. Complete Variable Access Mechanisms

This section covers ALL variable types in Smalltalk-80, not just temporaries and arguments.

### 7.1 Variable Categories in Smalltalk-80

Smalltalk-80 has **8 categories** of variables:

#### A. Instance Variables
**Storage**: Receiver object slots (0..N-1)
**Access Pattern**: Direct field lookup via receiver
**Bytecodes**:
- PUSH: `0x00-0x0F` (instances 0-15), `0x80 index` (extended)
- STORE: `0x60-0x67` (instances 0-7), `0x81 index` (extended)
- POP: `0x81 index` (extended store-pop)

**Example**:
```smalltalk
Point >> x
    ^x  "PUSH_INST_VAR 0"

Point >> x: newValue
    x _ newValue  "STORE_INST_VAR 0"
```

**Key Properties**:
- Fixed at class definition time
- Inherited from superclasses
- Always accessed via receiver (self)
- No scope traversal needed

#### B. Temporary Variables
**Storage**: Context indexed slots (after arguments)
**Access Pattern**: Hierarchical with scope traversal
**Bytecodes**:
- PUSH: `0x10-0x1F` (temps 0-15), `0x80 index` (extended)
- STORE: `0x68-0x6F` (temps 0-7), `0x81 index` (extended)
- POP: `0x87` (pop stack, discard)

**Example**:
```smalltalk
findKeyOrNil: key
    | location length probe pass |
    location _ self basicSize.   "STORE_TEMP 1"
    ^location                     "PUSH_TEMP 1"
```

**Key Properties**:
- Scoped to method or block
- Blocks inherit outer temps hierarchically
- Require context chain traversal for outer access
- Follow hierarchical indexing (Section 2)

#### C. Method Arguments
**Storage**: Context indexed slots (0..ArgCount-1)
**Access Pattern**: Same as temporaries (hierarchical)
**Bytecodes**: Same as temps (`PUSH_TEMP`, `STORE_TEMP`)

**Example**:
```smalltalk
copyFrom: start to: stop
    ^self copyFrom: start  "PUSH_TEMP 0 (start)"
```

**Key Properties**:
- Read-only in Smalltalk-80 (assignment not allowed)
- Indexed 0..N-1 at start of context stack
- Blocks access parent arguments via hierarchical indices

#### D. Class Variables
**Storage**: Class object's classPool dictionary (Association objects)
**Access Pattern**: Lookup by name via receiver's class
**Bytecodes**:
- PUSH: `0x40-0x5F` (literals 0-31), `0x82 index` (extended)
- STORE: Via Association (primitive write to value slot)

**Example**:
```smalltalk
ClassWithClassVar class >> initialize
    ClassVar _ 100.  "Store to Association value"

ClassWithClassVar >> accessClassVar
    ^ClassVar        "PUSH_LIT <association>, SEND value"
```

**Key Properties**:
- Shared across all instances of a class
- Stored as Association objects in classPool
- Access via literal reference to Association
- Modified via `Association value:` message

#### E. Pool Dictionary Variables
**Storage**: Shared pool dictionary (Association objects)
**Access Pattern**: Lookup by name in pool dictionary
**Bytecodes**: Same as class variables (literal associations)

**Example**:
```smalltalk
TextConstants initialize!
    CR _ Character cr.
    LF _ Character lf.

Text >> hasCR
    ^self includes: CR  "PUSH_LIT <CR association>, SEND value"
```

**Key Properties**:
- Shared across multiple classes
- Imported via `poolDictionaries` class method
- Stored as Association objects
- Read via `Association value`, write via `Association value:`

#### F. Global Variables
**Storage**: Smalltalk dictionary (Association objects)
**Access Pattern**: Lookup by name in Smalltalk global dictionary
**Bytecodes**: Same as class/pool variables (literal associations)

**Example**:
```smalltalk
Transcript show: 'Hello'  "PUSH_LIT <Transcript association>, SEND value"
```

**Key Properties**:
- Visible everywhere (system-wide scope)
- Stored in `Smalltalk` dictionary as Associations
- Includes classes (Point, Array, etc.)
- Modified via `Smalltalk at: #Name put: value`

#### G. Pseudo-Variables (Special Variables)
**Storage**: Context or fixed OOPs
**Access Pattern**: Special bytecodes or fixed references
**Bytecodes**:
- `self`: `PUSH_SELF` (0x70-0x77 depending on send optimization)
- `true`: `PUSH_TRUE` (0x71)
- `false`: `PUSH_FALSE` (0x72)
- `nil`: `PUSH_NIL` (0x73)
- `thisContext`: `PUSH_ACTIVE_CONTEXT` (0x89)
- `super`: Same as self but changes message lookup start

**Example**:
```smalltalk
Point >> = aPoint
    ^self class == aPoint class  "PUSH_SELF, SEND class"
        and: [self x = aPoint x]  "PUSH_SELF, SEND x"

Object >> isNil
    ^false  "PUSH_FALSE, RETURN"
```

**Key Properties**:
- Cannot be assigned to (read-only)
- Fixed semantics defined by VM
- `super` changes method lookup but uses self as receiver
- `thisContext` provides reflective access to execution context

#### H. Block Arguments
**Storage**: BlockContext indexed slots (0..BlockArgCount-1)
**Access Pattern**: Local to block (no traversal needed for own args)
**Bytecodes**: Same as temps (`PUSH_TEMP`, `STORE_TEMP`)

**Example**:
```smalltalk
select: aBlock
    self do: [:each |        "each is block arg at index 0"
        (aBlock value: each)  "PUSH_TEMP 0"
            ifTrue: [...]
    ]
```

**Key Properties**:
- Local to specific block
- Indexed 0..N-1 at start of block's stack
- Do not participate in hierarchical indexing for child blocks

### 7.2 Variable Resolution Priority

When the compiler encounters a variable name, it searches in this order:

1. **Block arguments** (if in block)
2. **Block temporaries** (if in block)
3. **Parent block arguments/temps** (hierarchical traversal)
4. **Method arguments**
5. **Method temporaries**
6. **Instance variables** (receiver fields)
7. **Class variables** (receiver's class)
8. **Pool dictionary variables** (imported pools)
9. **Global variables** (Smalltalk dictionary)
10. **Pseudo-variables** (self, super, true, false, nil, thisContext)

**Compile-time error if not found**.

### 7.3 Assignment Rules

| Variable Type | Can Assign? | Bytecode Pattern |
|---------------|-------------|------------------|
| Instance Variables | ✅ Yes | STORE_INST_VAR |
| Temporaries | ✅ Yes | STORE_TEMP |
| Arguments | ❌ No | Compile error |
| Class Variables | ✅ Yes | Via Association |
| Pool Variables | ✅ Yes | Via Association |
| Global Variables | ✅ Yes | Via Smalltalk at:put: |
| Pseudo-Variables | ❌ No | Compile error |
| Block Arguments | ✅ Yes | STORE_TEMP (pass-by-value) |

---

## 8. Detailed Compiler Implementation Instructions

This section provides step-by-step instructions for implementing variable access in the compiler.

### 8.1 Variable Resolution Algorithm

```csharp
public (BytecodeType type, int index) ResolveVariable(
    string varName,
    CompilationContext context)
{
    // Step 1: Check block-local variables (if in block)
    if (context.IsBlock)
    {
        // Check block arguments
        if (context.BlockArguments.TryGetIndex(varName, out int argIndex))
            return (BytecodeType.PUSH_TEMP, argIndex);

        // Check block temporaries
        if (context.BlockTemporaries.TryGetIndex(varName, out int tempIndex))
            return (BytecodeType.PUSH_TEMP, context.BlockArgCount + tempIndex);
    }

    // Step 2: Check parent scopes (hierarchical traversal)
    if (context.IsBlock)
    {
        int accumulatedOffset = context.BlockArgCount + context.BlockTempCount;
        var parentContext = context.Parent;

        while (parentContext != null)
        {
            // Check if parent is also a block
            if (parentContext.IsBlock)
            {
                // Check parent block arguments
                if (parentContext.BlockArguments.TryGetIndex(varName, out int pArgIndex))
                    return (BytecodeType.PUSH_TEMP, pArgIndex + accumulatedOffset);

                // Check parent block temporaries
                if (parentContext.BlockTemporaries.TryGetIndex(varName, out int pTempIndex))
                    return (BytecodeType.PUSH_TEMP,
                            parentContext.BlockArgCount + pTempIndex + accumulatedOffset);

                // Accumulate this parent's locals
                accumulatedOffset += parentContext.BlockArgCount + parentContext.BlockTempCount;
                parentContext = parentContext.Parent;
            }
            else
            {
                // Parent is method - check method arguments/temps
                break;
            }
        }
    }

    // Step 3: Check method arguments
    if (context.MethodArguments.TryGetIndex(varName, out int methodArgIndex))
    {
        if (context.IsBlock)
        {
            // Calculate hierarchical index for block accessing method arg
            int offset = CalculateBlockLocalOffset(context);
            return (BytecodeType.PUSH_TEMP, methodArgIndex + offset);
        }
        return (BytecodeType.PUSH_TEMP, methodArgIndex);
    }

    // Step 4: Check method temporaries
    if (context.MethodTemporaries.TryGetIndex(varName, out int methodTempIndex))
    {
        int baseIndex = context.MethodArgCount + methodTempIndex;
        if (context.IsBlock)
        {
            int offset = CalculateBlockLocalOffset(context);
            return (BytecodeType.PUSH_TEMP, baseIndex + offset);
        }
        return (BytecodeType.PUSH_TEMP, baseIndex);
    }

    // Step 5: Check instance variables
    var classLayout = GetClassLayout(context.TargetClass);
    if (classLayout.TryGetFieldIndex(varName, out int fieldIndex))
        return (BytecodeType.PUSH_INST_VAR, fieldIndex);

    // Step 6: Check class variables
    if (context.ClassVariables.Contains(varName))
    {
        var association = GetOrCreateAssociation(varName, context.ClassPool);
        int litIndex = AddLiteral(association);
        return (BytecodeType.PUSH_LIT_VAR, litIndex);
    }

    // Step 7: Check pool dictionary variables
    foreach (var pool in context.PoolDictionaries)
    {
        if (pool.Contains(varName))
        {
            var association = GetOrCreateAssociation(varName, pool);
            int litIndex = AddLiteral(association);
            return (BytecodeType.PUSH_LIT_VAR, litIndex);
        }
    }

    // Step 8: Check global variables
    if (GlobalDictionary.Contains(varName))
    {
        var association = GetOrCreateAssociation(varName, GlobalDictionary);
        int litIndex = AddLiteral(association);
        return (BytecodeType.PUSH_LIT_VAR, litIndex);
    }

    // Step 9: Check pseudo-variables
    switch (varName)
    {
        case "self": return (BytecodeType.PUSH_SELF, 0);
        case "super": return (BytecodeType.PUSH_SUPER, 0);
        case "true": return (BytecodeType.PUSH_TRUE, 0);
        case "false": return (BytecodeType.PUSH_FALSE, 0);
        case "nil": return (BytecodeType.PUSH_NIL, 0);
        case "thisContext": return (BytecodeType.PUSH_ACTIVE_CONTEXT, 0);
    }

    // Not found - compile error
    throw new CompileException($"Undefined variable: {varName}");
}

private int CalculateBlockLocalOffset(CompilationContext context)
{
    int offset = 0;
    var current = context;

    // Walk from current block up to method, accumulating local counts
    while (current.IsBlock)
    {
        offset += current.BlockArgCount + current.BlockTempCount;
        current = current.Parent;
    }

    return offset;
}
```

### 8.2 Bytecode Emission Rules

#### A. Push Variable
```csharp
public void EmitPushVariable(string varName)
{
    var (type, index) = ResolveVariable(varName, CurrentContext);

    switch (type)
    {
        case BytecodeType.PUSH_INST_VAR:
            if (index <= 15)
                EmitByte((byte)(0x00 + index));
            else
                EmitBytes(0x80, (byte)index);
            break;

        case BytecodeType.PUSH_TEMP:
            if (index <= 15)
                EmitByte((byte)(0x10 + index));
            else
                EmitBytes(0x80, (byte)index);
            break;

        case BytecodeType.PUSH_LIT_VAR:
            if (index <= 31)
                EmitByte((byte)(0x40 + index));
            else
                EmitBytes(0x82, (byte)index);
            break;

        case BytecodeType.PUSH_SELF:
            EmitByte(0x70);
            break;

        case BytecodeType.PUSH_TRUE:
            EmitByte(0x71);
            break;

        case BytecodeType.PUSH_FALSE:
            EmitByte(0x72);
            break;

        case BytecodeType.PUSH_NIL:
            EmitByte(0x73);
            break;

        case BytecodeType.PUSH_ACTIVE_CONTEXT:
            EmitByte(0x89);
            break;
    }
}
```

#### B. Store Variable
```csharp
public void EmitStoreVariable(string varName)
{
    var (type, index) = ResolveVariable(varName, CurrentContext);

    // Check assignment legality
    if (type == BytecodeType.PUSH_TEMP && IsArgument(varName))
        throw new CompileException($"Cannot assign to argument: {varName}");

    if (type == BytecodeType.PUSH_SELF || type == BytecodeType.PUSH_TRUE ||
        type == BytecodeType.PUSH_FALSE || type == BytecodeType.PUSH_NIL ||
        type == BytecodeType.PUSH_ACTIVE_CONTEXT)
        throw new CompileException($"Cannot assign to pseudo-variable: {varName}");

    switch (type)
    {
        case BytecodeType.PUSH_INST_VAR:
            if (index <= 7)
                EmitByte((byte)(0x60 + index));
            else
                EmitBytes(0x81, (byte)index);
            break;

        case BytecodeType.PUSH_TEMP:
            if (index <= 7)
                EmitByte((byte)(0x68 + index));
            else
                EmitBytes(0x81, (byte)index);
            break;

        case BytecodeType.PUSH_LIT_VAR:
            // Class/pool/global variables stored via Association
            EmitPushLiteral(index);  // Push association
            EmitSend("value:", 1);    // Send value: message
            break;
    }
}
```

#### C. Pop Variable (Store and Pop)
```csharp
public void EmitPopVariable(string varName)
{
    // Same as EmitStoreVariable but doesn't leave value on stack
    EmitStoreVariable(varName);
    // Pop is implicit in STORE bytecodes (they consume stack)
}
```

---

## 9. Detailed Interpreter Implementation Instructions

This section provides step-by-step instructions for implementing variable access in the interpreter.

### 9.1 Context Structure

The interpreter must maintain these context types:

```csharp
public class MethodContext
{
    public SmalltalkOOP Sender { get; set; }      // Calling context
    public SmalltalkOOP Receiver { get; set; }    // self
    public SmalltalkOOP Method { get; set; }      // CompiledMethod
    public int ProgramCounter { get; set; }       // Current bytecode index
    public SmalltalkOOP[] Stack { get; set; }     // Evaluation stack
    public int StackPointer { get; set; }         // Top of stack
    public SmalltalkOOP[] IndexedSlots { get; set; } // Args + Temps
}

public class BlockContext
{
    public SmalltalkOOP Sender { get; set; }      // Calling context
    public SmalltalkOOP HomeContext { get; set; } // Defining method context
    public SmalltalkOOP Method { get; set; }      // CompiledMethod (block literal)
    public int ProgramCounter { get; set; }
    public SmalltalkOOP[] Stack { get; set; }
    public int StackPointer { get; set; }
    public SmalltalkOOP[] IndexedSlots { get; set; } // Block args + temps
    public int ArgCount { get; set; }             // Number of block arguments
}
```

### 9.2 Bytecode Execution Algorithms

#### A. Push Instance Variable (0x00-0x0F, 0x80)
```csharp
public void ExecutePushInstanceVariable(byte opcode)
{
    int index;

    if (opcode >= 0x00 && opcode <= 0x0F)
    {
        // Short form: 0x00-0x0F push instance variables 0-15
        index = opcode - 0x00;
    }
    else if (opcode == 0x80)
    {
        // Extended form: next byte is index
        index = FetchNextByte();
    }
    else
    {
        throw new InvalidOpcodeException($"Invalid PUSH_INST_VAR opcode: {opcode:X2}");
    }

    // Get receiver from active context
    var receiver = GetReceiver(_activeContext);

    // Read instance variable
    var value = _heapManager.GetInstanceVariable(receiver, index);

    // Push to stack
    Push(value);
}
```

#### B. Push Temporary Variable (0x10-0x1F, 0x80)
```csharp
public void ExecutePushTemporaryVariable(byte opcode)
{
    int index;

    if (opcode >= 0x10 && opcode <= 0x1F)
    {
        // Short form: 0x10-0x1F push temps 0-15
        index = opcode - 0x10;
    }
    else if (opcode == 0x80)
    {
        // Extended form (when following PUSH_TEMP pattern)
        index = FetchNextByte();
    }
    else
    {
        throw new InvalidOpcodeException($"Invalid PUSH_TEMP opcode: {opcode:X2}");
    }

    // Execute hierarchical traversal
    var value = TraverseContextChainForRead(index);

    // Push to stack
    Push(value);
}

private SmalltalkOOP TraverseContextChainForRead(int index)
{
    var currentContext = _activeContext;
    var remainingIndex = index;

    while (true)
    {
        bool isBlock = IsBlockContext(currentContext);

        if (!isBlock)
        {
            // Method context - direct access
            return GetIndexedSlot(currentContext, remainingIndex);
        }

        // Block context - check if local or inherited
        int blockLocalCount = GetBlockLocalCount(currentContext);

        if (remainingIndex < blockLocalCount)
        {
            // Local to this block
            return GetIndexedSlot(currentContext, remainingIndex);
        }

        // Inherited from parent - traverse up
        remainingIndex -= blockLocalCount;
        currentContext = GetHomeContext(currentContext);

        if (currentContext == null)
            throw new RuntimeException("Context chain broken during variable access");
    }
}
```

#### C. Push Literal Variable (0x40-0x5F, 0x82)
```csharp
public void ExecutePushLiteralVariable(byte opcode)
{
    int literalIndex;

    if (opcode >= 0x40 && opcode <= 0x5F)
    {
        // Short form: 0x40-0x5F push literals 0-31
        literalIndex = opcode - 0x40;
    }
    else if (opcode == 0x82)
    {
        // Extended form: next byte is literal index
        literalIndex = FetchNextByte();
    }
    else
    {
        throw new InvalidOpcodeException($"Invalid PUSH_LIT opcode: {opcode:X2}");
    }

    // Get CompiledMethod from active context
    var method = GetMethod(_activeContext);

    // Read literal (Association object)
    var association = GetLiteral(method, literalIndex);

    // Read Association.value slot (index 1)
    var value = _heapManager.GetInstanceVariable(association, 1);

    // Push to stack
    Push(value);
}
```

#### D. Store Instance Variable (0x60-0x67, 0x81)
```csharp
public void ExecuteStoreInstanceVariable(byte opcode)
{
    int index;

    if (opcode >= 0x60 && opcode <= 0x67)
    {
        // Short form: 0x60-0x67 store to instance variables 0-7
        index = opcode - 0x60;
    }
    else if (opcode == 0x81)
    {
        // Extended form: next byte is index
        index = FetchNextByte();
    }
    else
    {
        throw new InvalidOpcodeException($"Invalid STORE_INST_VAR opcode: {opcode:X2}");
    }

    // Get receiver from active context
    var receiver = GetReceiver(_activeContext);

    // Pop value from stack (STORE consumes stack)
    var value = Pop();

    // Write instance variable
    _heapManager.SetInstanceVariable(receiver, index, value);
}
```

#### E. Store Temporary Variable (0x68-0x6F, 0x81)
```csharp
public void ExecuteStoreTemporaryVariable(byte opcode)
{
    int index;

    if (opcode >= 0x68 && opcode <= 0x6F)
    {
        // Short form: 0x68-0x6F store to temps 0-7
        index = opcode - 0x68;
    }
    else if (opcode == 0x81)
    {
        // Extended form
        index = FetchNextByte();
    }
    else
    {
        throw new InvalidOpcodeException($"Invalid STORE_TEMP opcode: {opcode:X2}");
    }

    // Pop value from stack
    var value = Pop();

    // Execute hierarchical traversal for write
    TraverseContextChainForWrite(index, value);
}

private void TraverseContextChainForWrite(int index, SmalltalkOOP value)
{
    var currentContext = _activeContext;
    var remainingIndex = index;

    while (true)
    {
        bool isBlock = IsBlockContext(currentContext);

        if (!isBlock)
        {
            // Method context - direct write
            SetIndexedSlot(currentContext, remainingIndex, value);
            return;
        }

        // Block context - check if local or inherited
        int blockLocalCount = GetBlockLocalCount(currentContext);

        if (remainingIndex < blockLocalCount)
        {
            // Local to this block
            SetIndexedSlot(currentContext, remainingIndex, value);
            return;
        }

        // Inherited from parent - traverse up
        remainingIndex -= blockLocalCount;
        currentContext = GetHomeContext(currentContext);

        if (currentContext == null)
            throw new RuntimeException("Context chain broken during variable write");
    }
}
```

#### F. Push Special Variables (0x70-0x73, 0x89)
```csharp
public void ExecutePushSpecialVariable(byte opcode)
{
    switch (opcode)
    {
        case 0x70: // PUSH_SELF
            var receiver = GetReceiver(_activeContext);
            Push(receiver);
            break;

        case 0x71: // PUSH_TRUE
            Push(SpecialObjectsArray.Instance.True);
            break;

        case 0x72: // PUSH_FALSE
            Push(SpecialObjectsArray.Instance.False);
            break;

        case 0x73: // PUSH_NIL
            Push(SpecialObjectsArray.Instance.Nil);
            break;

        case 0x89: // PUSH_ACTIVE_CONTEXT
            Push(_activeContext);
            break;

        default:
            throw new InvalidOpcodeException($"Invalid special variable opcode: {opcode:X2}");
    }
}
```

---

## 10. Complete Bytecode Mapping Table

### 10.1 Push Operations

| Range | Bytecode | Operation | Description | Index Encoding |
|-------|----------|-----------|-------------|----------------|
| 0x00-0x0F | 0x00+n | Push Receiver Variable n | Push instance variable 0-15 | Direct: n = opcode - 0x00 |
| 0x10-0x1F | 0x10+n | Push Temporary Variable n | Push temp/arg 0-15 | Direct: n = opcode - 0x10 |
| 0x20-0x3F | 0x20+n | Push Literal Constant n | Push literal object 0-31 | Direct: n = opcode - 0x20 |
| 0x40-0x5F | 0x40+n | Push Literal Variable n | Push class/pool/global var 0-31 | Direct: n = opcode - 0x40 |
| 0x70 | PUSH_SELF | Push self | Push receiver | N/A |
| 0x71 | PUSH_TRUE | Push true | Push true singleton | N/A |
| 0x72 | PUSH_FALSE | Push false | Push false singleton | N/A |
| 0x73 | PUSH_NIL | Push nil | Push nil singleton | N/A |
| 0x74 | PUSH_-1 | Push -1 | Push SmallInteger(-1) | N/A |
| 0x75 | PUSH_0 | Push 0 | Push SmallInteger(0) | N/A |
| 0x76 | PUSH_1 | Push 1 | Push SmallInteger(1) | N/A |
| 0x77 | PUSH_2 | Push 2 | Push SmallInteger(2) | N/A |

### 10.2 Store Operations

| Range | Bytecode | Operation | Description | Index Encoding |
|-------|----------|-----------|-------------|----------------|
| 0x60-0x67 | 0x60+n | Store Receiver Variable n | Store to instance var 0-7 | Direct: n = opcode - 0x60 |
| 0x68-0x6F | 0x68+n | Store Temporary Variable n | Store to temp 0-7 | Direct: n = opcode - 0x68 |

### 10.3 Extended Operations (0x80-0x8F)

| Bytecode | Extension Byte | Operation | Description |
|----------|----------------|-----------|-------------|
| 0x80 | index | Extended Push | Push any variable/literal/special |
| 0x81 | index | Extended Store | Store any instance var/temp |
| 0x82 | index | Extended Push Literal | Push literal object beyond 31 |
| 0x87 | N/A | Pop Stack Top | Discard top stack value |
| 0x88 | N/A | Duplicate Stack Top | Duplicate TOS |
| 0x89 | N/A | Push Active Context | Push thisContext |

**Extended Push (0x80) Format**:
- Next byte bits 7-6: Variable type (00=inst, 01=temp, 10=lit, 11=litVar)
- Next byte bits 5-0: Index (0-63)

**Extended Store (0x81) Format**:
- Next byte bit 7: Variable type (0=inst, 1=temp)
- Next byte bits 6-0: Index (0-127)

### 10.4 Send Message Operations

| Range | Bytecode | Operation | Description |
|-------|----------|-----------|-------------|
| 0x90-0x9F | 0x90+n | Send Literal Selector n With 0 Args | Fast send for common unary messages |
| 0xA0-0xAF | 0xA0+n | Send Literal Selector n With 1 Arg | Fast send for common binary messages |
| 0xB0-0xBF | 0xB0+n | Send Literal Selector n With 2 Args | Fast send for common keyword messages |
| 0xC0-0xDF | Extended | Send Literal Selector | General send with selector index + arg count |

### 10.5 Control Flow Operations

| Bytecode | Operation | Description |
|----------|-----------|-------------|
| 0xE0-0xE7 | Jump Forward Short | Jump forward 0-7 bytes |
| 0xE8-0xEF | Jump Forward Long | Jump forward with extension byte |
| 0xF0-0xF7 | Jump Backward Short | Jump backward 0-7 bytes |
| 0xF8 | Jump Forward If False | Conditional jump (pop and test) |
| 0xF9 | Jump Forward If True | Conditional jump (pop and test) |
| 0xFC-0xFF | Return | Return to sender (various forms) |

---

## 11. Implementation Examples

### 11.1 Complete Variable Access Example

```smalltalk
"Class definition"
Object subclass: #Example
    instanceVariableNames: 'instVar1 instVar2'
    classVariableNames: 'ClassVar'
    poolDictionaries: ''
    category: 'Examples'

"Method with all variable types"
Example >> complexMethod: arg1
    | temp1 temp2 |
    "Instance variable access"
    instVar1 _ 10.          "STORE_INST_VAR 0"
    temp1 _ instVar2.       "PUSH_INST_VAR 1, STORE_TEMP 1"

    "Class variable access"
    ClassVar _ 20.          "PUSH_LIT <ClassVar assoc>, PUSH_LIT 20, SEND value:"
    temp2 _ ClassVar.       "PUSH_LIT <ClassVar assoc>, SEND value, STORE_TEMP 2"

    "Pseudo-variable access"
    self doSomething.       "PUSH_SELF, SEND doSomething"
    ^nil                    "PUSH_NIL, RETURN"

"Block accessing outer variables"
Example >> blockExample: arg1
    | temp1 |
    temp1 _ 100.
    ^[:blockArg |
        | blockTemp |
        blockTemp _ blockArg + temp1 + arg1.
        instVar1 _ blockTemp.
        blockTemp
    ]
```

**Generated Bytecode for Block**:
```
; blockArg at index 0 (local)
; blockTemp at index 1 (local)
; temp1 at index 0+2=2 (parent)
; arg1 at index 0+2=2 (parent)
; instVar1 at instance slot 0

PUSH_TEMP 0        ; blockArg
PUSH_TEMP 2        ; temp1 (hierarchical)
SEND +
PUSH_TEMP 2        ; arg1 (hierarchical)
SEND +
STORE_TEMP 1       ; blockTemp
PUSH_TEMP 1        ; blockTemp
STORE_INST_VAR 0   ; instVar1
PUSH_TEMP 1        ; blockTemp
RETURN
```

### 11.2 Hierarchical Traversal Example

```smalltalk
"3-level nested block accessing method variable"
Example >> tripleNested
    | methodVar |
    methodVar _ 42.
    ^[
        [:innerArg |
            [methodVar + innerArg] value
        ] value: 10
    ] value
```

**Compiler Index Calculation**:
- Outer block: 0 args, 0 temps → localCount = 0
- Middle block: 1 arg (innerArg), 0 temps → localCount = 1
- Inner block: 0 args, 0 temps → localCount = 0
- Method: 0 args, 1 temp (methodVar)

**From innermost block**:
- `innerArg`: index 0 + innerLocalCount(0) = 0... wait, need parent
  - In middle block: innerArg at index 0
  - From inner block: index 0 + innerLocalCount(0) = 0
- `methodVar`:
  - Inner block locals: 0
  - Middle block locals: 1
  - Total offset: 0 + 1 = 1
  - methodVar at method index 0
  - Final: 0 + 1 = 1

**Interpreter Traversal**:
```
PUSH_TEMP 0 (innerArg):
  - Active context: inner block (localCount=0)
  - index 0 < 0? No
  - Traverse to parent: middle block (localCount=1)
  - remainingIndex = 0
  - 0 < 1? Yes → access middle block slot 0 → innerArg

PUSH_TEMP 1 (methodVar):
  - Active context: inner block (localCount=0)
  - index 1 < 0? No
  - Traverse to parent: middle block (localCount=1)
  - remainingIndex = 1 - 0 = 1
  - 1 < 1? No
  - Traverse to parent: method context
  - remainingIndex = 1 - 1 = 0
  - Access method slot 0 → methodVar
```

---

---

## 12. BlockClosure homeContext Architecture (CRITICAL)

### 12.1 The Chicken-and-Egg Problem

**Problem Statement**: BlockClosures are created eagerly (at pushLiteral time) but block execution contexts are created lazily (at invocation time). For nested blocks, the parent block's execution context doesn't exist when the child BlockClosure is created.

**Timeline of Events**:
```
1. Method executes, encounters outer block literal
   → Creates BlockClosure for outer block (homeContext = method context) ✓

2. Method continues, encounters NESTED block literal BEFORE outer block is invoked
   → Calls FindNearestBlockExecutionContext(_activeContextOOP)
   → Parent block execution context doesn't exist yet!
   → Returns method context (WRONG - should return parent BlockClosure)
   → Creates nested BlockClosure with homeContext = method context ✗

3. Later, outer block is invoked
   → Creates execution context for outer block
   → nested BlockClosure already has WRONG homeContext (can't fix it now)

4. Later, nested block is invoked
   → Uses wrong homeContext (method instead of parent block)
   → Variable access traverses to wrong context
   → Reads garbage/nil instead of correct values ✗
```

**Why This is Fundamental**:
- **Eager creation**: BlockClosures must be created when pushLiteral executes (during method/block body execution)
- **Lazy creation**: Block execution contexts only created when blocks are invoked (via primitive 207)
- **Result**: Parent block execution context literally doesn't exist when nested BlockClosure is created

**Example**:
```smalltalk
BlockContextTest>>test1a1b1i1p_1   "test case 1"
    | ma |                          "method arg at slot 0"
    ma _ 1.
    ^[:ba | | bt |                  "outer block: ba at 0, bt at 1"
        bt _ 2.
        [:ia |                      "nested block: ia at 0"
            | it |                  "nested temp: it at 1"
            it _ 3.
            ma + ba + bt + ia + it  "Accesses: ma(method), ba(parent), bt(parent), ia(own), it(own)"
        ] value: 4
    ] value: 9
```

**Diagnostic Evidence**:
```
[CREATE_BLOCKCLOSURE#5] closure=oop=89832, method=block:f55a217d3e6044a5b59d78bd892e52f6,
                        homeContext=oop=89826, numArgs=0
                                    ↑↑↑↑↑↑↑↑
                        WRONG: This is Integer>>+ method context, not parent block execution context!

[PUSH_TEMP READ] contextToReadFrom=oop=89826, actualIndex=0, value=OOP(oop=13968), class=UndefinedObject
                                   ↑↑↑↑↑↑↑↑
                 Reading from wrong context → gets nil instead of parent block's ba variable
```

### 12.2 Root Cause Analysis

**Current Implementation**:
```csharp
// VirtualMachine.cs:1340 (pushLiteral handler)
SmalltalkOOP homeContextOOP = FindNearestBlockExecutionContext(_activeContextOOP);
// PROBLEM: For nested blocks, parent execution context doesn't exist yet!

// VirtualMachine.cs:3954 (helper method)
private SmalltalkOOP FindNearestBlockExecutionContext(SmalltalkOOP startContextOOP)
{
    var currentContextOOP = startContextOOP;
    while (currentContextOOP != default && currentContextOOP != SpecialObjectsArray.Instance.Nil)
    {
        if (_blockContextMapping.ContainsKey(currentContextOOP))
        {
            return currentContextOOP;  // Found block execution context
        }
        // Walk sender chain...
    }
    return startContextOOP;  // Falls back to method context (WRONG for nested blocks!)
}
```

**Why This Fails**:
1. When nested block literal is pushed, we're executing in the METHOD context (or in a nested message send within the outer block)
2. Parent block's execution context hasn't been created yet (blocks are lazy - only created when invoked)
3. `FindNearestBlockExecutionContext` walks sender chain looking for execution contexts in `_blockContextMapping`
4. Doesn't find parent block execution context (because it doesn't exist yet!)
5. Returns method context as fallback
6. Nested BlockClosure created with wrong homeContext

### 12.3 Solution: Store Parent BlockClosure, Not Execution Context

**Key Insight**: BlockClosures form a stable lexical chain, while execution contexts form an ephemeral dynamic chain.

**Lexical Chain (Stable)**:
```
Method → Outer BlockClosure → Nested BlockClosure
         (created at line 5)    (created at line 7)
         homeContext = method   homeContext = outer BlockClosure (stable OOP)
```

**Execution Chain (Ephemeral)**:
```
At runtime:
Method context → Outer block execution context → Nested block execution context
(oop=1000)       (oop=2000 - created LATER)      (oop=3000 - created EVEN LATER)
```

**Recommended Solution**:

**Step 1: Change homeContext Semantics**
```csharp
// BEFORE (stores execution context OOP):
BlockClosure.homeContext = SmalltalkOOP (points to execution MethodContext)

// AFTER (stores parent BlockClosure OOP or method execution context OOP):
BlockClosure.homeContext = SmalltalkOOP (points to parent BlockClosure OR method execution context)
```

**Step 2: Modify CreateBlockClosure**
```csharp
// VirtualMachine.cs:5043 (CreateBlockClosure method)
private SmalltalkOOP CreateBlockClosure(SmalltalkOOP blockMethodOOP, int numArgs)
{
    SmalltalkOOP homeContextOOP;

    // Check if we're currently executing in a block
    if (_blockContextMapping.TryGetValue(_activeContextOOP, out var currentBlockContextOOP))
    {
        // We're in a block - find the BlockClosure that created this execution context
        // Option A: Store in _blockContextMapping as (executionContext → BlockClosure)
        // Option B: Add reverse mapping _executionContextToBlockClosure
        SmalltalkOOP parentBlockClosureOOP = LookupBlockClosureForExecutionContext(_activeContextOOP);
        homeContextOOP = parentBlockClosureOOP;  // Store parent BlockClosure (stable)
    }
    else
    {
        // We're in a method - store the method execution context
        homeContextOOP = FindMethodContext(_activeContextOOP);  // Store method context
    }

    // Create BlockClosure with correct homeContext
    var closureOOP = _heapManager.AllocateInstanceOf(blockClosureClassOOP);
    SetInstanceVariable(closureOOP, BLOCKCLOSURE_METHOD_INDEX, blockMethodOOP);
    SetInstanceVariable(closureOOP, BLOCKCLOSURE_HOMECONTEXT_INDEX, homeContextOOP);
    SetInstanceVariable(closureOOP, BLOCKCLOSURE_NUMARGS_INDEX, SmalltalkOOP.MakeSmallInteger(numArgs));

    return closureOOP;
}
```

**Step 3: Modify GetHomeContext to Resolve BlockClosure → Execution Context**
```csharp
// VirtualMachine.cs (new helper method)
private SmalltalkOOP GetHomeContext(SmalltalkOOP contextOOP)
{
    int homeIndex = VMConstants.BLOCKCONTEXT_HOME_INDEX;
    var homeContextOOP = _heapManager.GetInstanceVariable(contextOOP, homeIndex);

    // Check if homeContext is a BlockClosure (not an execution context)
    var homeClass = GetClass(homeContextOOP);
    if (homeClass == _vmMetadata.BlockClosureClassOOP)
    {
        // It's a BlockClosure - resolve to its execution context
        // Lookup in _blockClosureToExecutionContextMapping
        if (_blockClosureToExecutionContextMapping.TryGetValue(homeContextOOP, out var executionContextOOP))
        {
            return executionContextOOP;  // Return the execution context
        }
        else
        {
            throw new RuntimeException(
                $"BlockClosure {homeContextOOP} has no execution context (block not yet invoked?)");
        }
    }
    else
    {
        // It's already an execution context (method or block)
        return homeContextOOP;
    }
}
```

**Step 4: Update InvokeBlockInternal to Populate Mapping**
```csharp
// VirtualMachine.cs:3638 (InvokeBlockInternal method)
private SmalltalkOOP InvokeBlockInternal(SmalltalkOOP blockMethodOOP,
                                         SmalltalkOOP[] blockArgs,
                                         SmalltalkOOP homeContextOOP,
                                         SmalltalkOOP blockClosureOOP)
{
    // Create block execution context...
    var blockExecutionContextOOP = CreateMethodContext(blockMethodOOP, nilOOP, blockArgs);

    // CRITICAL: Register bidirectional mapping
    _blockContextMapping[blockExecutionContextOOP] = blockContextOOP;  // Existing mapping

    // NEW: Add reverse mapping from BlockClosure to execution context
    _blockClosureToExecutionContextMapping[blockClosureOOP] = blockExecutionContextOOP;

    // Execute block...
    return result;
}
```

### 12.4 Why This Solution Works

**Advantage 1: Stable References**
- BlockClosures are created once and never destroyed (until GC)
- BlockClosures form a stable lexical chain mirroring source code structure
- No timing issues - parent BlockClosure always exists when nested BlockClosure is created

**Advantage 2: Lazy Resolution**
- homeContext stores **what** to access (BlockClosure or method), not **where** (execution context OOP)
- Execution contexts created on-demand when blocks are invoked
- Resolution happens at variable access time (when execution contexts exist)

**Advantage 3: Correct Semantics**
- Lexical scoping: nested block accesses parent block's variables (via BlockClosure chain)
- Dynamic scoping: execution contexts form call stack (via sender chain)
- These are DIFFERENT chains with different purposes

**Advantage 4: Debugging**
- BlockClosure chain visible in debugger (stable OOPs)
- Execution context chain visible in stack trace (ephemeral OOPs)
- Clear separation of concerns

### 12.5 Implementation Algorithm

**Data Structures Required**:
```csharp
// Existing
private Dictionary<SmalltalkOOP, SmalltalkOOP> _blockContextMapping;
// Key: block execution context OOP → Value: BlockContext OOP (legacy data holder)

// NEW: Add reverse mapping
private Dictionary<SmalltalkOOP, SmalltalkOOP> _blockClosureToExecutionContextMapping;
// Key: BlockClosure OOP → Value: block execution context OOP
```

**Algorithm for Variable Access**:
```csharp
private SmalltalkOOP TraverseContextChainForRead(int index)
{
    var currentContext = _activeContextOOP;
    var remainingIndex = index;

    while (true)
    {
        bool isBlock = IsBlockContext(currentContext);

        if (!isBlock)
        {
            // Method context - direct access
            return GetIndexedSlot(currentContext, remainingIndex);
        }

        // Block context - check if local or inherited
        int blockLocalCount = GetBlockLocalCount(currentContext);

        if (remainingIndex < blockLocalCount)
        {
            // Local to this block
            return GetIndexedSlot(currentContext, remainingIndex);
        }

        // Inherited from parent - traverse up
        remainingIndex -= blockLocalCount;

        // CRITICAL: Resolve homeContext (might be BlockClosure or execution context)
        currentContext = ResolveHomeContext(currentContext);
        // ↑↑↑ This method handles both BlockClosure → execution context resolution
        //     and direct execution context references
    }
}

private SmalltalkOOP ResolveHomeContext(SmalltalkOOP blockExecutionContextOOP)
{
    // Get homeContext field from block execution context
    var homeContextOOP = GetInstanceVariable(blockExecutionContextOOP, BLOCKCONTEXT_HOME_INDEX);

    // Check if it's a BlockClosure or an execution context
    var homeClass = GetClass(homeContextOOP);

    if (homeClass == _vmMetadata.BlockClosureClassOOP)
    {
        // It's a BlockClosure - resolve to execution context
        if (_blockClosureToExecutionContextMapping.TryGetValue(homeContextOOP, out var executionContextOOP))
        {
            return executionContextOOP;
        }
        else
        {
            throw new RuntimeException(
                $"BlockClosure {homeContextOOP} not yet invoked (no execution context)");
        }
    }
    else
    {
        // Already an execution context (method or block)
        return homeContextOOP;
    }
}
```

### 12.6 Migration Path

**Phase 1: Add Reverse Mapping**
- Add `_blockClosureToExecutionContextMapping` field to VirtualMachine
- Update `InvokeBlockInternal` to populate mapping
- Test: Verify mapping is populated correctly

**Phase 2: Update CreateBlockClosure**
- Change logic to store parent BlockClosure (not execution context)
- Add `LookupBlockClosureForExecutionContext` helper
- Test: Verify nested BlockClosures have correct homeContext

**Phase 3: Update Variable Access**
- Modify `TraverseContextChainForRead` to call `ResolveHomeContext`
- Modify `TraverseContextChainForWrite` similarly
- Test: Verify nested block variable access works

**Phase 4: Update GetHomeContext Helper**
- Change to resolve BlockClosure → execution context
- Add error handling for unresolved BlockClosures
- Test: Verify all helper callers work correctly

**Phase 5: Comprehensive Testing**
- Run BlockContextTest (120 tests)
- Test 3-level nested blocks
- Test blocks passed between methods
- Verify all init expressions pass

### 12.7 Alternative Considered: Store Nothing, Search at Access Time

**Idea**: Don't store homeContext at all. At variable access time, search for parent block by walking the BlockClosure literal chain.

**Why Rejected**:
- **Performance**: Would require searching method literals every variable access
- **Complexity**: Would need to track which literal index created each BlockClosure
- **Fragility**: Breaks if blocks are passed between methods (literals in different methods)

**Conclusion**: Storing parent BlockClosure OOP is simpler, faster, and more robust.

### 12.8 Impact on Existing Code

**Files Requiring Changes**:
1. **VirtualMachine.cs**:
   - Add `_blockClosureToExecutionContextMapping` field
   - Modify `CreateBlockClosure` (lines 5043-5102)
   - Modify `InvokeBlockInternal` (lines 3638-3760)
   - Add `ResolveHomeContext` helper method
   - Modify `TraverseContextChainForRead` (lines 3693-3760)
   - Modify `TraverseContextChainForWrite` (similar pattern)

2. **BlockClosurePrimitives.cs**:
   - No changes required (homeContext semantics unchanged from Smalltalk perspective)

3. **VMConstants.cs**:
   - No changes required (field indices unchanged)

**Backward Compatibility**:
- **Breaking change**: Existing coldstart bundles won't work (homeContext OOP values change)
- **Solution**: Recompile Smalltalk-80.st source with fixed VM
- **Runtime impact**: None - this is a cold start architecture change only

### 12.9 Trade-offs and Considerations

**Advantages**:
✅ Solves chicken-and-egg problem completely
✅ Lexical chain stable and always available
✅ Clear separation of lexical scope (BlockClosure chain) vs execution stack (context sender chain)
✅ Debuggability improved (can inspect BlockClosure chain separately)
✅ Correct semantics for nested blocks

**Disadvantages**:
❌ Requires additional mapping dictionary (`_blockClosureToExecutionContextMapping`)
❌ Slight performance overhead for resolution (dictionary lookup at each parent access)
❌ More complex logic in `ResolveHomeContext` helper

**Performance Analysis**:
- **Cost**: One dictionary lookup per parent scope access
- **Benefit**: Eliminates incorrect behavior and enables nested blocks to work
- **Mitigation**: Could cache resolved contexts in local variable during traversal

**Memory Analysis**:
- **Cost**: Additional dictionary with N entries (one per active BlockClosure invocation)
- **Benefit**: BlockClosures themselves become smaller (no need to store stale execution context OOPs)
- **Net**: Roughly neutral (dictionary overhead ~= eliminated stale references)

---

## Revision History

- **v1.0.0** (2025-11-05): Initial specification based on bug fix for temp variable indexing
- **v2.0.0** (2025-11-05): Production-complete specification with all variable types, detailed compiler/interpreter instructions, and complete bytecode mapping
- **v2.1.0** (2025-11-05): Added Section 12 documenting BlockClosure homeContext chicken-and-egg problem and recommended solution architecture