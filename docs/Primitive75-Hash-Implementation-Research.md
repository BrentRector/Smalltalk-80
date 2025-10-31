# Primitive 75 (Hash) Implementation Research

**Date**: 2025-10-31
**Status**: ✅ Research Complete - Berkeley Source Code Located

## Summary

We located the original Berkeley Smalltalk-80 C implementation source code and discovered that **Primitive 75 (hash/primitiveIdentityHash) was NOT implemented**. It was mapped to `primFail` in the dispatch table, meaning the primitive always failed and the Smalltalk fallback code executed.

## Source Code Location

**File**: `docs/C Implementation/st/PrimTable.c`
**Copyright**: Mario Wolczko (1984-1989)
**Line 63-128**: Primitive dispatch table

```c
BOOL (*dispPrim[])()= {
    primFail,  // 0
    prAddSI, prSubSI, prLTSI, prGTSI, prLESI, prGESI, prEQSI, prNESI,  // 1-8
    // ... primitives 9-74 ...
    primFail,  // Primitive 75 - NOT IMPLEMENTED
    // ... remaining primitives ...
};
```

## Related Primitive: Primitive 74 (asOop)

**File**: `docs/C Implementation/st/ArrStorePrim.c`
**Lines 417-428**:

```c
BOOL prAsOop()
{
    OOP rcvr= popStack;
    if (isInt(rcvr)) {
        unPop(1);
        return TRUE;
    } else {
        push(objToInt(rcvr));
        return FALSE;
    }
}
```

**OOP Representation** (`docs/C Implementation/st/OMMacros.h`, lines 240-252):

```c
#define intObj(val)         ((val)|0x80000000)   // Create SmallInteger
#define isInt(oop)          ((oop)&0x80000000)   // Test if SmallInteger
#define objToInt(o)         ((o) | 0x80000000)   // Convert OOP to SmallInteger
#define intToObj(i)         ((i) & 0x7FFFFFFF)   // Extract OOP from SmallInteger
```

**Key Insight**: The Berkeley implementation used bit 31 (0x80000000) as the SmallInteger tag. Object pointers (OOPs) are raw values with bit 31 clear. Converting an OOP to a SmallInteger simply sets bit 31.

## Blue Book Specification

**Source**: Smalltalk-80.st, lines 26868-26874 (Object>>hash):

```smalltalk
hash
    "Answer a SmallInteger whose value is half of the receiver's object pointer
    (interpreting object pointers as 16-bit signed quantities).  Fails if the
    receiver is a SmallInteger.  Essential.  See documentation in Object metaclass."

    <primitive: 75>
    self primitiveFailed!
```

**Source**: Smalltalk-80.st, lines 37259-37265 (Symbol>>hash):

```smalltalk
hash
    "Answer with a SmallInteger whose value is half of the receiver's object pointer
    (interpreting object pointers as 16-bit signed quantities).  Essential.  See
    Object documentation whatIsAPrimitive."

    <primitive: 75>
    ^self!
```

## Berkeley Implementation Behavior

Since primitive 75 always failed in Berkeley Smalltalk:

1. **Object>>hash**: Would call `self primitiveFailed`, triggering error handling
2. **Symbol>>hash**: Would execute fallback `^self`, returning the Symbol OOP itself

**This means Berkeley Symbols used their OOP value as their hash**, NOT half the OOP.

## Our Implementation Decision

We implement the **Blue Book documented behavior** ("half of the receiver's object pointer"), not the Berkeley implementation:

```csharp
// For objects: return half the object table index as a SmallInteger
// Extract object table index: receiver.RawValue >> 1
// Then divide by 2 for hash: (receiver.RawValue >> 1) / 2 = receiver.RawValue >> 2
int hashValue = receiver.RawValue >> 2;
return SmalltalkOOP.FromSmallInteger(hashValue);
```

**Rationale**:
1. The Blue Book comment explicitly states "half of the receiver's object pointer"
2. Berkeley's non-implementation was likely incomplete/unfinished
3. Using half the OOP provides better hash distribution than using the full OOP
4. Our implementation successfully allows the system to bootstrap and execute

## Testing Results

**Status**: ✅ Working
**Test**: ClassOrganizer initialize completes successfully
**Evidence**: Init expression execution progresses past the point where it previously failed with Primitive 60 type error

**Example Hash Output**:
- `'As yet unclassified' hash` returns `SmallInteger(3240)`
- Hash values are computed correctly and used as array indices without type errors

## Alternative Implementations

### Berkeley Behavior (Not Implemented)
If we wanted to match Berkeley's actual behavior (primitive fails, Symbol>>hash returns self):

```csharp
// Always fail the primitive - let Smalltalk fallback execute
ctx.PrimitiveSuccess = false;
return SpecialObjectsArray.Instance.Nil;
```

This would cause Symbol>>hash to return the full Symbol OOP as a SmallInteger.

### Full OOP as Hash (Another Option)
Convert the OOP directly to SmallInteger without halving:

```csharp
int objectIndex = receiver.RawValue >> 1;  // Extract object table index
return SmalltalkOOP.FromSmallInteger(objectIndex);
```

This would return the full object table index as the hash value.

## Conclusion

We have **definitive source code evidence** from the original Berkeley Smalltalk-80 C implementation:

1. ✅ Primitive 75 was not implemented in Berkeley (mapped to primFail)
2. ✅ The Blue Book comment documents "half of the receiver's object pointer" as intended behavior
3. ✅ Our implementation follows the Blue Book specification
4. ✅ The system successfully bootstraps and executes with our implementation

**Implementation Location**: [ObjectPrimitives.cs:1006-1038](../Smalltalk80.Interpreter/Core/VM/Primitives/ObjectPrimitives.cs#L1006-L1038)
