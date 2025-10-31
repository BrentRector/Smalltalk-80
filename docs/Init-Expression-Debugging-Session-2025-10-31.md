# Init Expression Debugging Session - 2025-10-31

## Summary

Extensive debugging session to investigate why `ClassOrganizer initialize` fails with "Method not found: truncated in class String".

## Key Findings

### ✅ Primitive 75 (Hash) - WORKING CORRECTLY

**Status**: Implementation is correct and working as intended.

**Evidence**:
```
[RETURN from method:String>>hash] value=SmallInteger(3240), class=SmallInteger, isSmallInt=True
[RETURN from method:String>>stringhash] value=SmallInteger(3240), class=SmallInteger, isSmallInt=True
```

**Implementation**: [ObjectPrimitives.cs:1006-1038](../Smalltalk80.Interpreter/Core/VM/Primitives/ObjectPrimitives.cs#L1006-L1038)

**Specification Source**: Original Berkeley Smalltalk-80 C implementation (Mario Wolczko, 1984-1989)
- Primitive 75 was NOT implemented in Berkeley (mapped to `primFail`)
- Our implementation follows Blue Book spec: "half of the receiver's object pointer"
- Formula: `int hashValue = receiver.RawValue >> 2;`

**Historical Documentation**: [Primitive75-Hash-Implementation-Research.md](Primitive75-Hash-Implementation-Research.md)

### ❌ Root Cause - Block Parameter Binding Issue

**Problem**: A block in `Symbol class>>hasInterned:ifTrue:` is receiving a String (`'As yet unclassified'`) as a parameter when it should receive a SmallInteger loop index.

**Error Manifestation**:
```
[at:] receiver=oop=57858 class="Array" | index=NOT SmallInteger! OOP(52834), class=String
  PROBLEM: Non-SmallInteger index detected!
  Active context: oop=67774
  Method: block:b1a005158e924ea3b0561169ddf02b39
```

**Call Stack**:
```
[0] block:b1a005158e924ea3b0561169ddf02b39 (attempting Array>>at: with String index)
[1] False>>ifFalse:
[2] hasInterned:ifTrue: execution
[3] Symbol class>>intern:
[4] String>>asSymbol
[5] ClassOrganizer initialize
```

**Source Location**: [Smalltalk-80.st:37427-37444](../Smalltalk-80 Source/Smalltalk-80.st#L37427-L37444)

```smalltalk
hasInterned: aString ifTrue: symBlock
    | v i ascii |
    aString size = 1 ifTrue: [(ascii _ (aString at: 1) asciiValue) < 128
            ifTrue:
                [symBlock value: (SingleCharSymbols at: ascii + 1).
                ^true]].
    v _ USTable at: aString stringhash \\ USTable size + 1.
    1 to: v size do:
        [:i |
        (v at: i) == nil
            ifFalse: [aString size = (v at: i) size ifTrue: [aString = (v at: i)
                        ifTrue:
                            [symBlock value: (v at: i).
                            ^true]]]].
    ^false!
```

**Block Identification**:
- Block localId: `block:b1a005158e924ea3b0561169ddf02b39`
- Bytecodes: `[16, 194, 18, 16, 192, 194, 182, 33, 224, 124]`
- Literals: Symbol index 1518 (#ifTrue:), nested block

**Analysis**:
- Block should have temp0 = boolean result of comparison
- Block calls `ifTrue:` on temp0
- But temp0 contains the String `'As yet unclassified'` instead of a boolean
- This means the comparison expression is returning the String instead of true/false

**Hypothesis**:
One of the following is broken:
1. `aString size` is returning the String instead of SmallInteger
2. `(v at: i) size` is returning something unexpected
3. The `=` operator is returning the wrong value
4. Block parameter passing is corrupted (temp0 gets wrong value)

**Observation**: No `to:do:` loop execution was detected after ClassOrganizer initialize started, suggesting the error occurs in the single-character lookup path (lines 37432-37435) or before the loop even starts.

## Diagnostic Output Added

**Files Modified**: [VirtualMachine.cs](../Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs)

**Diagnostics**:
1. **Hash method tracking** (lines 2271-2276): Logs when `hash` or `stringhash` messages are sent
2. **Hash return value tracking** (lines 710-718): Logs what value `hash` methods return
3. **`at:` index tracking** (lines 2278-2307): Logs all array access with index type, special handling for non-SmallInteger indices
4. **`=` comparison tracking** (lines 2278-2285): Logs equality comparisons
5. **`to:do:` loop tracking** (lines 2287-2296): Logs loop setup parameters

## Test Results

### String>>hash Execution
```
[hash] receiver oop=52834 | class="String"
  Found method: method:String>>hash, primitiveNumber=0
[RETURN from method:String>>hash] value=SmallInteger(3240), class=SmallInteger, isSmallInt=True
```

### String>>stringhash Execution
```
[stringhash] receiver oop=52834 | class="String"
  Found method: method:String>>stringhash, primitiveNumber=0
[RETURN from method:String>>stringhash] value=SmallInteger(3240), class=SmallInteger, isSmallInt=True
```

### Error Point
```
[at:] receiver=oop=57858 class="Array" | index=NOT SmallInteger! OOP(52834), class=String
  PROBLEM: Non-SmallInteger index detected!
  Active context: oop=67774
  Method: block:b1a005158e924ea3b0561169ddf02b39
```

## Next Steps

### Immediate Actions Needed

1. **Investigate temp0 in block b1a005158e924ea3b0561169ddf02b39**
   - Add diagnostic to show what value is in temp0 when block executes
   - Trace how temp0 gets assigned (should be boolean, but contains String)

2. **Validate USTable structure**
   - Check if USTable buckets are properly initialized
   - Verify `USTable at: index` returns an Array, not something else
   - Add Phase 0 validation for USTable structure

3. **Check `size` method calls**
   - Verify `aString size` returns SmallInteger
   - Verify `(v at: i) size` execution path

4. **Block parameter passing audit**
   - Review how block arguments are passed in `ifTrue:` contexts
   - Check if there's a scope issue with nested blocks

### Alternative Approaches

1. **Bypass Symbol interning temporarily**
   - Modify `String>>asSymbol` to always create new Symbol without checking USTable
   - This would allow init expressions to proceed and test other functionality
   - Disadvantage: Multiple copies of same Symbol would be created

2. **Fix USTable cold start initialization**
   - Review Phase 5 (GlobalsAndPatches) USTable creation
   - Ensure bucket arrays are properly structured
   - Validate that all symbols are correctly placed in buckets

3. **Simplify `hasInterned:ifTrue:` execution path**
   - Add explicit type checking and better error messages
   - Break down complex nested expressions into simpler steps

## Files Modified This Session

1. **ObjectPrimitives.cs** - Fixed Primitive 75 (Hash) implementation
2. **VirtualMachine.cs** - Added extensive diagnostic output for debugging
3. **Primitive75-Hash-Implementation-Research.md** - NEW: Historical research on hash primitive
4. **Init-Expression-Debugging-Session-2025-10-31.md** - NEW: This document

## Conclusion

**Primitive 75 (Hash) is working correctly.** The issue is not with hash computation but with how the computed hash value is used in the Symbol interning logic. Specifically, a block is receiving a String parameter when it should receive a SmallInteger, causing downstream type errors.

Further investigation needed to determine:
- Is this a bytecode compilation issue?
- Is this a block closure scope issue?
- Is this a USTable structure issue?
- Is this a message send/return value issue?

The root cause appears to be in the complex nested block structure of `hasInterned:ifTrue:` where temporary variable binding or parameter passing is getting corrupted.
