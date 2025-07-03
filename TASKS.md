# Development Tasks

This file lists missing features and potential improvements for the Smalltalk-80 interpreter prototype. Each task references the specification lines that describe the feature.

## Missing or Incomplete Features

1. **Process Switching**
   - The specification describes invoking `checkProcessSwitch` at the start of each interpreter cycle so that processes can be swapped between bytecodes. Current implementation has no scheduler or process management.
   - Spec reference: lines 814–816 of the specification show `checkProcessSwitch` in the interpreter loop.
2. **BlockContext Semantics**
   - Blocks should be created via `blockCopy:` and executed using `value`/`value:` messages. Non-local returns from blocks must be handled according to the spec.
   - Spec reference: lines 368–387 describe how blocks are copied and executed.
3. **Object Memory and SmallInteger Representation**
   - The interpreter does not implement an object memory layer, reference counting or garbage collection, or the special SmallInteger pointer representation.
   - Spec reference: lines 461–468 explain automatic reclamation and SmallInteger pointers.
4. **Process Scheduler and Semaphores**
   - The spec defines primitives for process scheduling, semaphores and signaling that are entirely absent from the prototype.
   - Spec reference: lines 1564–1580 outline scheduler registers and primitives.

## Optimizations and Improvements

1. **Bytecode Dispatch**
   - Replace the long chain of `if`/`else` statements in `Interpreter.Dispatch` with a modern `switch` expression for clarity and potential performance gain.
2. **Context Allocation**
   - Use struct-based contexts or object pooling to reduce allocations when creating new contexts for sends and blocks.
3. **Parser and Compiler**
   - Expand the `SmalltalkParser` to support the full Smalltalk syntax, including unary, binary and keyword messages, and control structures.
4. **Garbage Collection Integration**
   - Implement either reference counting or a mark/sweep collector and integrate it with the interpreter per the specification.
5. **Comprehensive Test Coverage**
   - Expand tests to cover all interpreter features and primitives to prevent regressions.


## Parser and REPL Integration

1. **Store Compiled Methods**
   - Implement object memory support for persisting methods created by `SmalltalkParser` so that subsequent message sends can locate them.
   - Spec reference: `Virtual Machine - Object Memory Interface.md` lines 1–34 describe storing pointers and words in objects.
2. **Header Generation**
   - When compiling source, build method headers and literal frames exactly as outlined in `Virtual Machine - Compiled Methods.md` lines 1–24 and 44–66.
3. **VirtualMachine APIs**
   - Add routines for compiling source into a `CompiledMethod`, storing it on the appropriate class, and executing it.
4. **REPL Pipeline**
   - Update `Repl.Run` to feed user input through the new VirtualMachine APIs so each snippet is compiled, saved, and executed.
