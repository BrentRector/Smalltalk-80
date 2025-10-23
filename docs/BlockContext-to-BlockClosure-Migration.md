# BlockContext to BlockClosure Migration Path

## Current State (2025-01-23)

The Smalltalk-80 system currently has **TWO block implementations coexisting**:

### 1. **BlockContext** (Original Blue Book Model)
- **Class**: BlockContext (subclass of ContextPart)
- **Primitives**: 80 (blockCopy:), 81 (value), 82 (value:)
- **Used by**: Existing Smalltalk-80.st source code (33 references)
- **Creation**: Compiler emits `blockCopy:` bytecode, VM creates BlockContext
- **Status**: ✅ Fully functional in original image

### 2. **BlockClosure** (New Modern Model)
- **Class**: BlockClosure (subclass of Object)
- **Primitive**: 207 (valueWithArguments:)
- **Used by**: New blocks compiled with our compiler
- **Creation**: Compiler emits block as CompiledMethod literal, VM wraps in BlockClosure at runtime
- **Status**: 🚧 Implemented but being tested

## Key Differences

| Aspect | BlockContext | BlockClosure |
|--------|--------------|--------------|
| **Structure** | Context object with PC, args, temps | Wrapper around CompiledMethod + homeContext |
| **Creation** | Dynamic (at blockCopy: execution) | Hybrid (CompiledMethod at compile time, wrapper at runtime) |
| **Lexical Scope** | Captured via homeContext | Captured via homeContext reference |
| **Performance** | Heavier (full context allocation) | Lighter (shares CompiledMethod) |
| **GC Pressure** | Higher (creates context per block) | Lower (BlockClosure is small wrapper) |

## Migration Strategy (Future Work)

### Phase 1: Coexistence (CURRENT)
- ✅ Both models work side-by-side
- ✅ BlockContext used by existing code
- ✅ BlockClosure used by new compiled code
- ✅ No breaking changes to existing image

### Phase 2: Gradual Migration (FUTURE)
1. **Identify all blockCopy: usage** in Smalltalk-80.st
2. **Rewrite critical methods** to use block literals instead of blockCopy:
3. **Test extensively** - blocks are fundamental to control flow
4. **Update compiler** to emit BlockClosure for all blocks
5. **Deprecate BlockContext** (mark as obsolete but keep for compatibility)

### Phase 3: BlockContext Removal (FAR FUTURE)
1. **Remove BlockContext class** from image
2. **Remove primitives 80-82** from VM
3. **Update documentation** to only reference BlockClosure
4. **Performance testing** to verify improvements

## Technical Considerations

### Why Migrate?

**Advantages of BlockClosure**:
- **Simpler VM**: No special blockCopy: bytecode handling
- **Better performance**: Blocks are literals, no runtime context creation
- **Modern semantics**: Matches Smalltalk-80 V2+ and modern Smalltalks
- **Cleaner implementation**: Separation of compilation and execution

**Challenges**:
- **Compatibility**: Must ensure all existing code works
- **Testing burden**: Blocks are used everywhere (control flow, iterators, etc.)
- **Image size**: Recompiling entire image increases initial size
- **Debugging**: Need to ensure debugger works with new model

### Migration Risks

1. **Breaking changes**: Subtle semantic differences could break existing code
2. **Performance regression**: Need to verify new model is actually faster
3. **Debugger compatibility**: Tools may expect BlockContext
4. **Third-party code**: External packages may depend on BlockContext

## Recommendation

**DO NOT migrate immediately**. The current coexistence model is:
- ✅ Safe (no breaking changes)
- ✅ Flexible (both models work)
- ✅ Testable (can compare behaviors)
- ✅ Incremental (can migrate one method at a time)

**Future migration** should be:
1. **Data-driven**: Measure actual performance benefits
2. **Incremental**: Migrate one subsystem at a time
3. **Reversible**: Keep BlockContext as fallback
4. **Well-tested**: Extensive test suite before migration

## References

- Blue Book (Smalltalk-80: The Language and its Implementation) - Chapter 30, BlockContext
- Modern Smalltalk implementations (Pharo, Squeak) - Use BlockClosure exclusively
- Our implementation: `VirtualMachine.cs:CreateBlockClosure()` and `Primitive 207`

## Status: 2025-01-23

- [x] BlockClosure class implemented in Smalltalk-80.st
- [x] Primitive 207 implemented in VM
- [x] Runtime BlockClosure wrapping in pushLiteral
- [ ] Full testing of BlockClosure evaluation
- [ ] Performance comparison BlockContext vs BlockClosure
- [ ] Migration tooling for rewriting blockCopy: to literals
