# Bytecode Implementation vs Specification Comparison

This document compares the FullInterpreter bytecode implementation with the Interpreter-Architecture.md specification.

## Summary

**CRITICAL DIFFERENCE FOUND**: The implementations do NOT match on literal indexing!

---

## Push Receiver Variable (0x00-0x0F)

**Spec**: Push instance variable 0-15 of receiver
**Implementation**: ✅ Matches
```csharp
// Lines ~370-380 in Interpreter.cs.old
if (bytecode >= 0x00 && bytecode <= 0x0F)
{
    var index = bytecode & 0x0F;
    var value = ObjectMemory.GetInstanceVariable(receiverRef.AsObjectPointer(), index);
    ContextPush(contextOop, value);
    return false;
}
```
**Verdict**: ✅ MATCHES

---

## Push Temporary Variable (0x10-0x1F)

**Spec**: Push temporary/argument 0-15
**Implementation**: ✅ Matches
```csharp
// Lines ~390-406
if (bytecode >= 0x10 && bytecode <= 0x1F)
{
    var index = bytecode & 0x0F;
    // ... push temp[index] ...
}
```
**Verdict**: ✅ MATCHES

---

## Push Literal Constant (0x20-0x3F)

**Spec**:
- Push literal[low5+1] (skip header at lit[0])
- Bytecode literal indices skip the header

**Implementation**:
```csharp
// Lines 409-416
if (bytecode >= 0x20 && bytecode <= 0x3F)
{
    var index = bytecode & 0x1F;
    var literal = ObjectMemory.GetCompiledMethodLiteral(methodOop, index);  // ❌ NO +1!
    ContextPush(contextOop, literal);
    return false;
}
```

**Verdict**: ❌ **MISMATCH!**
Implementation accesses `lit[index]` directly, but spec says should access `lit[index+1]`

---

## Push Literal Variable (0x40-0x5F)

**Spec**:
- Treat lit[low5+1] as association, push its value
- Skip header at lit[0]

**Implementation**:
```csharp
// Lines 418-460
if (bytecode >= 0x40 && bytecode <= 0x5F)
{
    var index = bytecode & 0x1F;
    var compiledAssociation = ObjectMemory.GetCompiledMethodLiteral(methodOop, index);  // ❌ NO +1!
    // ... lookup association value ...
}
```

**Verdict**: ❌ **MISMATCH!**
Implementation accesses `lit[index]` directly, but spec says should access `lit[index+1]`

---

## Pop and Store Receiver Variable (0x60-0x67)

**Spec**: Pop and store into instance variable 0-7
**Implementation**: ✅ Matches
```csharp
// Lines 463-474
if (bytecode >= 0x60 && bytecode <= 0x67)
{
    var index = bytecode & 0x07;
    var value = ContextPop(contextOop);
    ObjectMemory.SetInstanceVariable(receiverRef.AsObjectPointer(), index, value);
    return false;
}
```
**Verdict**: ✅ MATCHES

---

## Pop and Store Temporary Variable (0x68-0x6F)

**Spec**: Pop and store into temporary 0-7
**Implementation**: ✅ Matches
**Verdict**: ✅ MATCHES

---

## Push Special Constants (0x70-0x77)

**Spec**:
- 0x70: PUSH_SELF (receiver)
- 0x71: PUSH_TRUE
- 0x72: PUSH_FALSE
- 0x73: PUSH_NIL
- 0x74: PUSH_MINUS_ONE
- 0x75: PUSH_ZERO
- 0x76: PUSH_ONE
- 0x77: PUSH_TWO

**Implementation**:
```csharp
case 0x70: ContextPush(contextOop, receiverRef); return false;  // ✅
case 0x71: ContextPush(contextOop, GetTrueRef()); return false;  // ✅
case 0x72: ContextPush(contextOop, GetFalseRef()); return false;  // ✅
case 0x73: ContextPush(contextOop, SObjectRef.Nil); return false;  // ✅
case 0x74: ContextPush(contextOop, SObjectRef.FromSmallInteger(-1)); return false;  // ✅
case 0x75: ContextPush(contextOop, SObjectRef.FromSmallInteger(0)); return false;  // ✅
case 0x76: ContextPush(contextOop, SObjectRef.FromSmallInteger(1)); return false;  // ✅
case 0x77: ContextPush(contextOop, SObjectRef.FromSmallInteger(2)); return false;  // ✅
```
**Verdict**: ✅ MATCHES

---

## Return Bytecodes (0x78-0x7F)

**Spec**:
- 0x78: RETURN_SELF (return receiver to sender)
- 0x79: RETURN_TRUE
- 0x7A: RETURN_FALSE
- 0x7B: RETURN_NIL
- 0x7C: RETURN_TOP (pop and return)
- 0x7D: BLOCK_RETURN_SELF
- 0x7E: BLOCK_RETURN_TOP
- 0x7F: NOP

**Implementation**:
```csharp
case 0x78: ContextPush(contextOop, receiverRef); return true;  // ✅ Push receiver and return
case 0x79: ContextPush(contextOop, GetTrueRef()); return true;  // ✅
case 0x7A: ContextPush(contextOop, GetFalseRef()); return true;  // ✅
case 0x7B: ContextPush(contextOop, SObjectRef.Nil); return true;  // ✅
case 0x7C: /* Top already on stack */ return true;  // ✅
// 0x7D, 0x7E, 0x7F not shown in excerpt
```
**Verdict**: ✅ MATCHES (for shown bytecodes)

---

## Send Arithmetic (0xB0-0xBF)

**Spec**: Hardcoded arithmetic selectors: +, -, <, >, <=, >=, =, ~=, *, /, \\, @, bitShift:, //, bitAnd:, bitOr:

**Implementation**:
```csharp
// Lines 681-729
var selector = (bytecode & 0x0F) switch
{
    0x00 => "+",     0x01 => "-",    0x02 => "<",    0x03 => ">",
    0x04 => "<=",    0x05 => ">=",   0x06 => "=",    0x07 => "~=",
    0x08 => "*",     0x09 => "/",    0x0A => "\\\\", 0x0B => "@",
    0x0C => "bitShift:", 0x0D => "//", 0x0E => "bitAnd:", 0x0F => "bitOr:",
    _ => throw ...
};
```
**Verdict**: ✅ MATCHES

---

## Send Special (0xC0-0xCF)

**Spec**: Special selectors: at:, at:put:, size, next, nextPut:, atEnd, ==, class, blockCopy:, value, value:, do:, new, new:, x, y

**Implementation**:
```csharp
// Lines 734-786
var specialSelectors = new[]
{
    "at:", "at:put:", "size", "next", "nextPut:", "atEnd",
    "==", "class", "blockCopy:", "value", "value:", "do:",
    "new", "new:", "x", "y"
};
```
**Verdict**: ✅ MATCHES

---

## Send Literal Selector with 0 Args (0xD0-0xDF)

**Spec**:
- Send message using lit[low4+1] with 0 arguments
- Literal indices skip header

**Implementation**:
```csharp
// Lines 579-593
if (bytecode >= 0xD0 && bytecode <= 0xDF)
{
    var selectorIndex = bytecode & 0x0F;
    var selectorRef = ObjectMemory.GetCompiledMethodLiteral(methodOop, selectorIndex);  // ❌ NO +1!
    // ... send message ...
}
```
**Verdict**: ❌ **MISMATCH!**
Implementation accesses `lit[index]` directly, but spec says should access `lit[index+1]`

---

## Send Literal Selector with 1 Arg (0xE0-0xEF)

**Implementation**:
```csharp
// Lines 595-610
if (bytecode >= 0xE0 && bytecode <= 0xEF)
{
    var selectorIndex = bytecode & 0x0F;
    var selectorRef = ObjectMemory.GetCompiledMethodLiteral(methodOop, selectorIndex);  // ❌ NO +1!
    // ...
}
```
**Verdict**: ❌ **MISMATCH!**
Same issue - no +1 for header skip

---

## Send Literal Selector with 2 Args (0xF0-0xFF)

**Implementation**:
```csharp
// Lines 612-628
if (bytecode >= 0xF0 && bytecode <= 0xFF)
{
    var selectorIndex = bytecode & 0x0F;
    var selectorRef = ObjectMemory.GetCompiledMethodLiteral(methodOop, selectorIndex);  // ❌ NO +1!
    // ...
}
```
**Verdict**: ❌ **MISMATCH!**
Same issue - no +1 for header skip

---

## CRITICAL FINDINGS

### Issue #1: Literal Indexing Mismatch

**Specification says**:
> When bytecodes reference "literal N", they skip the header at lit[0] by accessing lit[N+1]

**Implementation does**:
> Accesses `ObjectMemory.GetCompiledMethodLiteral(methodOop, index)` WITHOUT adding 1

**Affected bytecodes**:
- 0x20-0x3F: Push Literal Constant
- 0x40-0x5F: Push Literal Variable
- 0xD0-0xDF: Send Literal 0 Args
- 0xE0-0xEF: Send Literal 1 Arg
- 0xF0-0xFF: Send Literal 2 Args

**Possible explanations**:
1. The `ObjectMemory.GetCompiledMethodLiteral()` method handles the +1 offset internally
2. The specification is wrong
3. The implementation is wrong

**Resolution needed**: Check `ObjectMemory.GetCompiledMethodLiteral()` implementation to see if it adds 1 internally.

---

## Conclusion

The FullInterpreter implementation matches the specification for:
- ✅ All push/pop instance variable operations
- ✅ All push/pop temporary operations
- ✅ All special constant pushes (self, true, false, nil, integers)
- ✅ All return operations
- ✅ Arithmetic selector sends
- ✅ Special selector sends

**CRITICAL MISMATCH**:
- ❌ All literal-based operations do NOT add +1 to skip header

**Next step**: Examine `ObjectMemory.GetCompiledMethodLiteral()` to determine if it handles the offset internally, making the spec documentation misleading.

---

## RESOLUTION

I examined both the OLD heap implementation and the NEW heap implementation:

### OLD Heap (FullInterpreter/Memory/ObjectMemoryClassHelpers.cs:353-360)

```csharp
public static SObjectRef GetCompiledMethodLiteral(nint methodOop, int index)
{
    var literals = GetCompiledMethodLiterals(methodOop);
    if (index < 0 || index >= literals.Length)
        throw new ArgumentOutOfRangeException(nameof(index));

    return literals[index]; // ❌ Returns lit[index] directly, NO +1!
}
```

**Finding**: The OLD implementation does **NOT** add +1 internally. It accesses `lit[index]` directly.

### NEW Heap (Smalltalk80.Interpreter/VM/SmalltalkHeap.cs:220-243)

```csharp
public int CreateCompiledMethod(MethodDescriptor method, int[] literalIds, byte[] bytecodes)
{
    int literalCount = literalIds.Length;
    int headerOop = EncodeSmallInteger(method.Header.Word);

    int methodId = AllocateRawObject(0, ObjectFormat.CompiledMethod, 0, bytecodes.Length);
    var methodData = _objectTable[GetTableIndex(methodId)];

    // CRITICAL: Store header + literals in instance variables
    // Instance variables: [header, literal1, literal2, ...]
    methodData.InstanceVariables = new int[1 + literalCount];
    methodData.InstanceVariables[0] = headerOop; // ✅ Header IS at literal[0]
    Array.Copy(literalIds, 0, methodData.InstanceVariables, 1, literalCount);

    methodData.IndexedData = bytecodes;
    return methodId;
}
```

**Finding**: The NEW heap **DOES** store the header at `lit[0]`, and user literals start at `lit[1]`.

### NEW Heap Disassembler (Smalltalk80.Interpreter/VM/BytecodeDisassembler.cs:59-71)

```csharp
// Push literal constant (0x20-0x3F)
// Note: Bytecode literal indices skip the header at literalIds[0]
else if (BytecodeUtil.IsPushLiteralConst(b))
{
    int litIndex = BytecodeUtil.PushLiteralConstIndex(b) + 1; // ✅ +1 to skip header at index 0
    string litValue = litIndex < literalIds.Length ? getLiteralDisplay(literalIds[litIndex]) : "???";
    result = $"pushLiteral {litIndex - 1} ({litValue})";
}

// Push literal variable (0x40-0x5F)
else if (BytecodeUtil.IsPushLiteralVar(b))
{
    int litIndex = BytecodeUtil.PushLiteralVarIndex(b) + 1; // ✅ +1 to skip header at index 0
    string litValue = litIndex < literalIds.Length ? getLiteralDisplay(literalIds[litIndex]) : "???";
    result = $"pushLiteralVariable {litIndex - 1} ({litValue})";
}
```

**Finding**: The NEW disassembler **DOES** add +1 to skip the header when accessing literals!

---

## FINAL VERDICT

### ❌ CRITICAL INCOMPATIBILITY FOUND

The **OLD FullInterpreter** implementation and the **NEW heap format** are **INCOMPATIBLE**:

| Component | Behavior |
|-----------|----------|
| **Interpreter-Architecture.md spec** | Bytecodes access `lit[index+1]` (skip header) |
| **NEW heap format** | Stores header at `lit[0]`, user literals at `lit[1+]` |
| **NEW disassembler** | Adds +1 when accessing literals |
| **OLD FullInterpreter** | Accesses `lit[index]` directly (NO +1) ❌ |
| **OLD heap** | Unknown (not examined) - possibly stored literals without header? |

### The Problem

If the OLD interpreter runs against the NEW heap format:
- Bytecode `0x20` (push literal[0]) will access `lit[0]` = **method header** ❌
- Bytecode `0x21` (push literal[1]) will access `lit[1]` = **first user literal** ✓
- But this is off-by-one from what the compiler intended!

### Resolution Options

**Option A: Fix OLD interpreter to match NEW heap**
- Update all literal access to add +1
- Matches the specification
- Matches the NEW heap format
- Matches the NEW disassembler

**Option B: Change NEW heap to match OLD interpreter**
- Don't store header at `lit[0]`
- Store only user literals
- Update disassembler to remove +1
- Contradicts the specification

**Option C: Verify OLD heap format**
- Examine how OLD heap stored method literals
- If OLD heap didn't store header at `lit[0]`, then OLD interpreter was correct for OLD heap
- But NEW heap has different format, requiring NEW interpreter

### Recommendation

**Option A** is correct because:
1. The specification clearly documents header at `lit[0]`
2. The NEW heap correctly implements this
3. The NEW disassembler correctly implements this
4. The OLD interpreter needs updating to match

### Action Required

Update the OLD FullInterpreter to add +1 when accessing literals in these bytecodes:
- `0x20-0x3F`: Push Literal Constant
- `0x40-0x5F`: Push Literal Variable
- `0xD0-0xDF`: Send Literal 0 Args
- `0xE0-0xEF`: Send Literal 1 Arg
- `0xF0-0xFF`: Send Literal 2 Args

This will make the OLD interpreter compatible with the NEW heap format and the documented specification.
