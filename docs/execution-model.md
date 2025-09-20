# Smalltalk-80 Execution Model and Stack Architecture

## Overview

The Smalltalk-80 VM separates static method metadata from dynamic runtime state. This design allows efficient memory allocation and prevents stack overflow in deeply nested calls.

## Why Both a "Stack Size" in the CompiledMethod and a Real Stack in the MethodContext Exist

Smalltalk splits method metadata from per-call runtime state. The CompiledMethod carries only static information about a method's signature and resource needs, while each MethodContext carries the actual runtime buffers used when that method executes.

### 1. CompiledMethod's Stack Size (Static Metadata)

When you compile a method, the VM gathers:
- **numArgs**: The number of arguments the method expects.
- **numTemps**: The number of temporary variables declared in the source.
- **maxStackDepth**: The maximum depth the evaluation stack will ever reach when executing this method's bytecodes.

These three values are packed into the CompiledMethod's header so that at runtime the VM knows exactly how much space to allocate for new activations.

**Key point**: CompiledMethod does not hold a live stack—only the size hint (maxStackDepth) needed by any context that will run it.

### 2. MethodContext's Evaluation Stack (Runtime State)

Every time you invoke a CompiledMethod—whether via a normal send or during file-in—the VM:

1. Allocates a fresh MethodContext.
2. Uses the compiled method's metadata to size its buffers:
   - `temps = new Object[numTemps]`
   - `args = new Object[numArgs]`
   - `stack = new Object[maxStackDepth]`
3. Initializes other fields (receiver, method pointer, pc, sender).

During interpretation, the VM pushes and pops intermediate values on `context.stack[]`, advancing `context.sp`. That live array is unique to this invocation, preventing cross-talk between concurrent activations.

### 3. How They Work Together

**Example: Compiling "Object initialize"**

You compile into a CompiledMethod with:
- numArgs = 0, numTemps = 0, maxStackDepth = 1 (you only ever push one receiver)
- bytecodes = [72, 129, 124]
- literalFrame = [Association(#Object→Object), #initialize]

You create a MethodContext with:
- receiver = Object
- method = that CompiledMethod
- pc = 0
- temps = []
- args = []
- stack = new Object[1]

As the interpreter runs:
- 0x48 pushes the one receiver into stack[0]
- 0x81 pops it off
- 0x7C returns the result

Because the CompiledMethod told the VM exactly how big the stack should be, the MethodContext can safely allocate a small, fixed‐size array and avoid dynamic resizing or out-of-bounds checks.

## Why Nested Calls Don't Overflow a Smalltalk Context Stack

In Smalltalk-80 each method activation ("call frame") gets its own evaluation stack sized just for that method's bytecodes. You never cram nested calls onto your caller's stack—each send allocates a fresh MethodContext with its own array. Here's how it works step-by-step:

### 1. CompiledMethod Carries Only a Per-Method Stack Hint

When you compile a method, you compute its maxStackDepth: the maximum number of temporaries and intermediate operands its own bytecodes will ever push.

You store that number in the CompiledMethod (alongside numArgs and numTemps), but you do not allocate a live stack there.

### 2. MethodContext Gets Its Own Stack at Call Time

On every message send, the VM:
1. Allocates a new MethodContext.
2. Reads `context.method.maxStackDepth`.
3. Allocates `context.stack = new Object[maxStackDepth]`.
4. Sets `context.sp = 0`, `context.pc = 0`, links receiver, args, temps, and sender.

That stack is used only by this activation to evaluate its bytecodes.

### 3. Sending a Message Spawns a Separate Context

Given your first context has stackSize = 1, it can hold exactly one pushed value at a time.

When you execute the Send bytecode:
1. You pop the receiver (and any args) from its stack.
2. You look up the selector, then create a brand-new MethodContext for the callee.
3. That new context's stack is sized to the callee's own maxStackDepth, so it can grow larger if needed.
4. Control switches into the callee's context, and its evaluation never spills back into the caller's stack.

### 4. Returning Simply Tears Down the Callee Context

When the callee ends with a return opcode, the interpreter:
1. Pops its return value off the callee's stack.
2. Disposes (or recycles) the callee's MethodContext.
3. Pushes that return value onto its sender's stack.

The caller resumes with its original (and still correctly sized) evaluation stack.

## Visualizing the Call Chain

Initial state:
```
Process.activeContext
│
├── MethodContext A  (for CompiledMethod A; stackSize = 1)
│     [ stack: … ]
│     pc = …
│
└── sender = nil  (initial file-in context)
```

After A executes Send:
```
Process.activeContext
│
├── MethodContext B  (for CompiledMethod B; stackSize = N)
│     [ stack: … up to N entries ]
│     pc = …
│
└── sender = MethodContext A
```

When B returns, we pop it off, and A's tiny stack is still intact.

## Key Takeaways

- **Each context isolates its own evaluation stack.**
- **No single stackSize must anticipate all nested calls**—only the current method's own bytecode needs.
- **Deep call graphs become a chain of contexts**, not one huge shared stack.
- **Memory is allocated efficiently**: Each context gets exactly the stack space it needs.
- **Stack overflow is prevented**: Each method's stack is sized precisely for its own operations.

## Implementation in the Interpreter

The C# interpreter implements this model in several key classes:

### CompiledMethod Structure
- Header field encodes numArgs, numTemps, and maxStackDepth
- Bytecode array contains the actual instructions
- Literals array holds constants and symbols

### MethodContext Class
- Maintains its own evaluation stack (Stack property)
- Links to sender context for call chain
- Tracks PC (program counter) and SP (stack pointer)
- Holds receiver, arguments, and temporaries

### Execution Flow
1. `ExecuteMethod` creates new MethodContext with properly sized stack
2. `ExecuteBytecodes` interprets instructions using the context's stack
3. Send bytecodes trigger recursive `ExecuteMethod` calls
4. Return bytecodes unwind to sender context

## Related Topics

- [Method Lookup Algorithm](selector-lookup.md) - How methods are found in the class hierarchy
- Process scheduling and context switching
- Non-local returns and exception handling
- Block closures and their activation records