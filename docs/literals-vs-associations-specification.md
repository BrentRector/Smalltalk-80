# Smalltalk-80 Literal vs Association Specification

## Overview
This document clarifies the distinction between literal constants and literal variables (Associations) in the Smalltalk-80 bytecode specification, as defined in the Blue Book.

## Bytecode Categories

### Literal Constants (Bytecodes 32-63)
- **Purpose**: Push direct literal values onto the stack
- **Contents**: Numbers, Strings, Symbols, Arrays, Blocks, and pseudo-variables
- **Pseudo-variables**: `nil`, `true`, `false` - These are constants, NOT variables
- **Behavior**: The literal is pushed as-is onto the stack
- **Example bytecode**: `pushLiteralConstant: 5` or `pushLiteralConstant: nil`

### Literal Variables (Bytecodes 64-95)  
- **Purpose**: Push the value of a variable binding onto the stack
- **Contents**: ONLY Association objects (key-value pairs)
- **Association structure**: 
  - Field 0: key (Symbol) - the variable name
  - Field 1: value (Object) - the current value
- **Used for**: Global variables, Class variables, Pool variables
- **Behavior**: The **value** field of the Association is pushed onto the stack
- **Example bytecode**: `pushLiteralVariable: Association(#Object -> Object class)`

## Critical Distinction

### What Gets an Association
- **Global variables**: Class names and other globals (e.g., `Object`, `Smalltalk`, `Transcript`)
- **Class variables**: Shared variables defined in a class
- **Pool variables**: Shared variables from pool dictionaries

### What Does NOT Get an Association
- **Pseudo-variables**: `nil`, `true`, `false` are constants, not variables
- **Literal symbols**: Selectors and symbol literals (e.g., `#at:put:`, `#symbol`)
- **Literal numbers**: SmallIntegers, LargeIntegers, Floats
- **Literal strings**: String constants
- **Literal arrays**: Array constants

## Implementation Notes

### Correct Literal Creation (JsonBootstrapExecutor.cs)
```csharp
if (literal == "nil" || literal == "true" || literal == "false")
{
    // Pseudo-variables are constants - store directly
    literalOop = vm.PrimitiveLookupGlobal(literal);
}
else if (vm.PrimitiveLookupGlobal(literal) >= 0)
{
    // True global variable - create Association
    var associationOop = vm.PrimitiveNewInstance(associationClassOop);
    vm.PrimitiveSetSlot(associationOop, 0, keySymbolOop);  // key
    vm.PrimitiveSetSlot(associationOop, 1, globalOop);      // value
    literalOop = associationOop;
}
else
{
    // Regular symbol literal
    literalOop = vm.PrimitiveInternSymbol(literal);
}
```

### Strict BytecodeDispatcher (BytecodeDispatcher.cs)
```csharp
private static void HandlePushLiteralVariable(Interpreter interpreter, int index)
{
    var literal = context.Method.Literals[index];
    
    // PushLiteralVariable REQUIRES an Association
    if (literal is Association assoc)
    {
        context.Stack.Push(assoc.Value);
    }
    else if (literal is SmalltalkObject obj && obj.Fields.Length == 2)
    {
        // SmalltalkObject Association
        context.Stack.Push(obj.Fields[1]); // Push value field
    }
    else
    {
        throw new InvalidOperationException(
            $"PushLiteralVariable requires Association, found {literal?.GetType()}");
    }
}
```

## Common Errors to Avoid

1. **Wrapping pseudo-variables in Associations**: `nil`, `true`, `false` should NEVER be in Associations
2. **Using workarounds in bytecode handlers**: If PushLiteralVariable finds a non-Association, it's a bug in literal creation
3. **Treating all globals the same**: Pseudo-variables are special constants, not true global variables

## Verification

To verify correct implementation:
1. No Association errors should occur during bootstrap
2. Pseudo-variables should appear as literal constants (bytecodes 32-63)
3. Class references should appear as literal variables (bytecodes 64-95) with Associations
4. The BytecodeDispatcher should have NO workarounds for malformed literals

## References
- Smalltalk-80: The Language and its Implementation (Blue Book), Chapter 26-28
- Bytecode specification: Pages 596-604
- Association class: Page 289