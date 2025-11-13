# Smalltalk-80 Variable Access Requirements and Current Implementation Analysis

## Purpose

This document explains:
1. **Smalltalk-80 Requirements**: How variable access from blocks should work according to the language specification
2. **Current Implementation**: The unified temp frame architecture and two-context model
3. **Implementation Failure**: Why the current design fails for nested blocks accessing parent block arguments

This document is written to be consumed by an LLM with no prior Smalltalk knowledge.

---

## 1. Smalltalk-80 Language Requirements

### 1.1 What Are Blocks?

Blocks are **lexical closures** in Smalltalk-80. They are:
- Anonymous functions that capture variables from their defining scope
- First-class values that can be stored in variables, passed as arguments, and returned from methods
- Executed later, potentially in a completely different execution context

**Syntax Example:**
```smalltalk
[:x :y | x + y]
```
- `:x :y` are block arguments (parameters)
- `x + y` is the block body
- The block captures any variables from the enclosing scope that it references

### 1.2 Variable Scoping Rules

Smalltalk-80 has **lexical scoping** with the following variable types:

1. **Pseudo-variables** (read-only, cannot be shadowed):
   - `self` - the receiver object
   - `super` - the receiver, but method lookup starts in superclass
   - `true`, `false`, `nil` - boolean and null constants
   - `thisContext` - the current execution context

2. **Method arguments**: Parameters passed to a method (read-only)

3. **Method temporaries**: Local variables declared in a method (read-write)

4. **Block arguments**: Parameters passed to a block when it's executed (read-only)

5. **Block temporaries**: Local variables declared in a block (read-write)

6. **Instance variables**: Object fields (read-write)

7. **Class variables**: Variables shared across all instances of a class (read-write)

8. **Global variables**: Variables accessible from anywhere (read-write)

### 1.3 Critical Requirement: Nested Block Variable Access

**The core requirement that the current implementation fails:**

Inner blocks must be able to access:
- Their own arguments and temporaries
- Parent block arguments and temporaries (from any nesting level)
- Method arguments and temporaries
- Instance variables, class variables, and globals

**Example:**
```smalltalk
testMethod
    | methodTemp |
    methodTemp := 10.
    [:outerArg :outerTemp |          "Outer block with argument and temp"
        outerTemp := 20.
        [:innerArg |                  "Inner block nested inside outer block"
            "Inner block must access ALL of these:"
            self            "pseudo-variable (receiver)"
            methodTemp      "method temporary"
            outerArg        "parent block argument ← CRITICAL"
            outerTemp       "parent block temporary"
            innerArg        "own block argument"
        ] value: 30
    ] value: 40 value: 50
```

**Key Point:** The inner block can be **passed to another method** for execution:
```smalltalk
testMethod
    [:a :b |
        self someMethod: [:c | a + b + c]  "Block references a, b from parent"
    ] value: 1 value: 2
```

When `someMethod:` executes the inner block, it must still be able to access `a` and `b` from the outer block, even though the execution has moved to a different method.

---

## 2. Current Implementation Design

### 2.1 Two-Context Architecture

The implementation uses **two types of runtime contexts**:

1. **MethodContext**: Represents an activated method
   - Contains method arguments and temporaries in a **unified temp frame**
   - Has instance variable slots for: caller, IP, stack pointer, method reference, receiver
   - Has indexed slots for: unified temp frame (all method args/temps + block temps)

2. **BlockContext**: Represents an activated block closure
   - Contains **only block arguments** in indexed slots (NOT temporaries)
   - Has instance variable slots for: caller, IP, stack pointer, argument count, starting IP, **home**
   - The **home** field points to the MethodContext where the block was defined

### 2.2 Unified Temp Frame Model

**Key Design Decision:**
- All temporaries (method args/temps AND block temps) are stored in the **MethodContext's unified temp frame**
- Block **arguments** are stored separately in the **BlockContext's indexed slots**
- Block **temporaries** go in the MethodContext's unified frame, NOT in BlockContext

**Storage Layout:**

```
MethodContext:
  Fixed slots: [caller, IP, stackP, method, receiver, ...]
  Indexed slots (unified temp frame):
    [methodArg0, methodArg1, methodTemp0, methodTemp1, blockTemp0, blockTemp1, ...]

BlockContext:
  Fixed slots: [caller, IP, stackP, nargs, initialIP, home → MethodContext]
  Indexed slots (block arguments ONLY):
    [blockArg0, blockArg1, ...]
```

### 2.3 Variable Access Algorithm

When bytecode executes `PUSH_TEMP index`:

1. **If in BlockContext** (executing a block):
   - If `index < nargs`: Read from BlockContext indexed slot `index` (block argument)
   - If `index >= nargs`: Traverse `home` pointer to MethodContext, read from unified temp frame

2. **If in MethodContext** (executing a method):
   - Read directly from unified temp frame at `index`

**VM Implementation (simplified):**
```csharp
SmalltalkOOP GetTemporaryVariable(int index)
{
    if (activeContext.IsBlockContext)
    {
        int nargs = GetBlockArgumentCount(activeContext);
        if (index < nargs)
        {
            // Block argument: read from BlockContext indexed slot
            return ReadIndexedSlot(activeContext, index);
        }
        else
        {
            // Block temp or method variable: traverse to home MethodContext
            SmalltalkOOP homeContext = GetHomeContext(activeContext);
            return ReadIndexedSlot(homeContext, index);
        }
    }
    else
    {
        // Method context: read directly from unified frame
        return ReadIndexedSlot(activeContext, index);
    }
}
```

---

## 3. The Critical Failure

### 3.1 The Problem Scenario

**When nested blocks try to access parent block arguments, the system fails.**

**Failing Example:**
```smalltalk
testMethod
    [:a :b |              "Outer block with arguments a, b"
        [:c |             "Inner block with argument c"
            a + b + c     "Inner block tries to access outer block's arguments"
        ] value: 3
    ] value: 1 value: 2
```

### 3.2 What Happens at Runtime

**Step 1: Outer Block Creation**
- Compiler creates BlockContext for outer block
- Outer BlockContext's `home` points to `testMethod`'s MethodContext
- Arguments `a`, `b` stored in outer BlockContext indexed slots [0], [1]

**Step 2: Outer Block Execution** (`value: 1 value: 2`)
- Primitive 81 activates outer BlockContext
- Stores argument values into BlockContext indexed slots:
  - Slot [0] = 1 (for `a`)
  - Slot [1] = 2 (for `b`)

**Step 3: Inner Block Creation**
- Compiler creates BlockContext for inner block
- **CRITICAL BUG:** Inner BlockContext's `home` points to `testMethod`'s MethodContext
  - It does NOT point to outer BlockContext!
  - The `home` chain skips over BlockContexts and goes straight to MethodContext
- Argument `c` will be stored in inner BlockContext indexed slot [0]

**Step 4: Inner Block Execution** (`value: 3`)
- Primitive 81 activates inner BlockContext
- Stores argument value into BlockContext indexed slot:
  - Slot [0] = 3 (for `c`)

**Step 5: Inner Block Tries to Access `b`**
- Compiler emitted `PUSH_TEMP index` for variable `b`
- VM's `GetTemporaryVariable` executes:
  1. Check if `b` is local argument in inner BlockContext - **NO** (only `c` is)
  2. Traverse `home` pointer → Goes to `testMethod`'s MethodContext
  3. Try to read `b` from MethodContext's unified temp frame
  4. **FAIL:** `b` is NOT in the unified temp frame!
     - `b` is in the outer BlockContext's indexed slots
     - But the `home` chain skipped over the outer BlockContext entirely

### 3.3 Root Cause Analysis

**The fundamental architectural flaw:**

The `home` pointer creates a **single-level chain**: BlockContext → MethodContext

This works for:
- ✅ Accessing method arguments/temps (they're in MethodContext unified frame)
- ✅ Accessing block temporaries (they're in MethodContext unified frame)
- ❌ **Accessing parent block arguments** (they're in parent BlockContext indexed slots, which are unreachable)

**Why is it unreachable?**
- Inner block's `home` points to MethodContext, not outer BlockContext
- There is no traversal mechanism to search intermediate BlockContexts
- The VM only checks: "Is it my local argument? If not, go to `home` (MethodContext)"
- Parent BlockContext is completely bypassed in the chain

### 3.4 Architectural Contradiction

The design has an inherent contradiction:

1. **Block arguments** are stored in BlockContext indexed slots (NOT in unified frame)
2. **The `home` chain** skips BlockContexts and goes straight to MethodContext
3. **Therefore:** Parent block arguments are architecturally unreachable from nested blocks

**This violates the fundamental Smalltalk-80 requirement that nested blocks must access parent block arguments.**

---

## 4. Summary

### 4.1 Smalltalk-80 Requirement
Nested blocks must access all enclosing scope variables, including parent block arguments, even when the block is passed to and executed by another method.

### 4.2 Current Implementation
- Two-context model: MethodContext (stores unified temp frame) and BlockContext (stores block arguments)
- Block arguments stored in BlockContext indexed slots
- Block temporaries stored in MethodContext unified temp frame
- `home` pointer chains BlockContext → MethodContext (skips intermediate BlockContexts)

### 4.3 The Failure
- Inner blocks cannot access outer block arguments
- The `home` chain bypasses parent BlockContexts where the arguments are stored
- This is a **fundamental architectural bug**, not a minor implementation error

### 4.4 Impact
Any code with nested blocks that access parent block arguments will fail at runtime with:
- Variable not found errors
- Wrong values being read (if variable names collide with method variables)
- System instability if the code is critical (like `Character initialize`)

---

## Appendix: Key Files and References

**Architecture Documentation:**
- `docs/BlueBook-Context-Architecture.md` - Complete specification of the current design

**Implementation Files:**
- `Smalltalk80.Compiler/VariableResolver.cs` - Compiler variable resolution
- `Smalltalk80.Compiler/Analysis/TempFrameAllocator.cs` - Unified temp frame allocation
- `Smalltalk80.Interpreter/VirtualMachine.cs` - Runtime variable access (GetTemporaryVariable)

**Example Failing Code:**
```smalltalk
"Character class>>initialize"
[:i |
    "... code that assigns to i ..."
    i := i + 1  "FAILS: Attempts to assign to read-only block argument"
] value: 0
```

This code fails because:
1. `i` is a block argument (read-only per Smalltalk-80 spec)
2. The compiler incorrectly allowed assignment to block argument
3. At runtime, the assignment corrupts the execution state
