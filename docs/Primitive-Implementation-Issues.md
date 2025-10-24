# Primitive Implementation Issues - Action Plan

**Date**: 2025-01-24
**Source**: Comprehensive code review feedback
**Priority**: HIGH - Multiple blockers and correctness issues

## Summary of High-Impact Issues

1. **API Inconsistencies**: Helper APIs assumed but non-existent or inconsistent
2. **Semantic Errors**: Incorrect Smalltalk semantics (Mod/Quo/Div, Stream indexing)
3. **Stub Implementations**: Incomplete primitives without proper fallback behavior
4. **Contract Mismatches**: IPrimitiveContext vs actual usage divergence
5. **Error Handling**: Catch-all blocks hiding real errors
6. **Correctness Problems**: Duplicate implementations, primitive number conflicts

## Prioritized Action List

### IMMEDIATE (Blockers) - Must Fix Before Production

#### 1. Unify Nil Accessor (1-2 hours)
**Problem**: Mixed use of `SpecialObjectsArray.Instance.Nil` and `GlobalRegistry.NilOOP`
**Impact**: Inconsistent null handling, potential runtime errors
**Solution**:
- Choose canonical accessor: `SpecialObjectsArray.Instance.Nil`
- Global search/replace all `GlobalRegistry.NilOOP` usage
- Verify compilation

**Files to Audit**:
- All primitive files in `Core/VM/Primitives/`
- VirtualMachine.cs
- Any test files

**Command**:
```bash
grep -r "GlobalRegistry.NilOOP" --include="*.cs" Smalltalk80.Interpreter/Core/VM/
grep -r "SpecialObjectsArray.Instance.Nil" --include="*.cs" Smalltalk80.Interpreter/Core/VM/
```

#### 2. API Contract Alignment (2-3 hours)
**Problem**: Primitives call methods that don't exist or have wrong signatures
**Impact**: Compile errors, runtime failures during bootstrap
**Solution**:
- Audit IPrimitiveContext interface
- Document all method contracts
- Fix or remove invalid method calls
- Add missing helper methods if needed

**Key Methods to Verify**:
- `GetByteArray()` - exists? returns Memory<byte>?
- `AllocateObject()` - overloads? className vs classOOP?
- `SetClassOOP()` - exists? or use AllocateObject correctly?
- `GetObjectFormat()` - exists?
- `GetIndexedSize()`, `GetIndexedOOP()`, `SetIndexedOOP()` - exist?

**Action**:
1. Read IPrimitiveContext.cs completely
2. Grep for method calls in all primitive files
3. Document which exist, which don't
4. Create implementation plan

#### 3. Remove Duplicate/Inconsistent Implementations (3-4 hours)
**Problem**: Multiple versions of Mod/Quo/Div with different semantics
**Impact**: Wrong arithmetic results, unpredictable behavior
**Solution**:
- Identify all duplicate implementations
- Choose Blue Book semantics as canonical
- Remove/consolidate duplicates
- Add unit tests

**Arithmetic Semantics (Blue Book)**:
```
Mod:  remainder with sign of divisor
      5 mod 3 = 2
      -5 mod 3 = 1  (not -2)
      5 mod -3 = -1 (not 2)

Quo:  truncated division (toward zero)
      5 quo 3 = 1
      -5 quo 3 = -1
      5 quo -3 = -1

Div (//): floor division (toward -infinity)
      5 // 3 = 1
      -5 // 3 = -2  (not -1)
      5 // -3 = -2  (not -1)
```

**Implementation**:
```csharp
// Mod - remainder with sign of divisor
public static SmalltalkOOP Mod(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
{
    int a = receiver.SmallIntegerValue;
    int b = args[0].SmallIntegerValue;
    int rem = a % b;
    if (rem != 0 && ((a < 0) != (b < 0)))
        rem += b;
    return SmalltalkOOP.FromSmallInteger(rem);
}

// Quo - truncated division (toward zero)
public static SmalltalkOOP Quo(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
{
    int a = receiver.SmallIntegerValue;
    int b = args[0].SmallIntegerValue;
    return SmalltalkOOP.FromSmallInteger(a / b);  // C# / truncates toward zero
}

// Div (//) - floor division (toward -infinity)
public static SmalltalkOOP Div(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
{
    int a = receiver.SmallIntegerValue;
    int b = args[0].SmallIntegerValue;
    int quot = a / b;
    int rem = a % b;
    // Adjust if signs differ and remainder non-zero
    if (rem != 0 && ((a < 0) != (b < 0)))
        quot--;
    return SmalltalkOOP.FromSmallInteger(quot);
}
```

### HIGH Priority - Correctness Issues

#### 4. Stream and String Indexing (2-3 hours)
**Problem**: Inconsistent position/readLimit semantics, off-by-one errors
**Impact**: Stream operations fail or corrupt data
**Solution**:
- Document semantics: "readLimit is last allowed index; AtEnd => position > readLimit"
- Fix all implementations to use `position - 1` for 0-based array access
- Validate bounds consistently

**Canonical Implementation**:
```csharp
// AtEnd - check if position > readLimit
public static SmalltalkOOP AtEnd(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
{
    int position = ctx.GetFieldValue(receiver, "class:Stream", "position").SmallIntegerValue;
    int readLimit = ctx.GetFieldValue(receiver, "class:Stream", "readLimit").SmallIntegerValue;

    bool atEnd = position > readLimit;
    return atEnd ? SpecialObjectsArray.Instance.True : SpecialObjectsArray.Instance.False;
}

// Next - read element at position-1, then increment position
public static SmalltalkOOP Next(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
{
    int position = ctx.GetFieldValue(receiver, "class:Stream", "position").SmallIntegerValue;
    int readLimit = ctx.GetFieldValue(receiver, "class:Stream", "readLimit").SmallIntegerValue;

    if (position > readLimit)
    {
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }

    var arrayOOP = ctx.GetFieldValue(receiver, "class:Stream", "array");
    var element = ctx.HeapManager.GetIndexedOOP(arrayOOP, position - 1);  // 0-based access

    ctx.SetFieldValue(receiver, "class:Stream", "position", SmalltalkOOP.FromSmallInteger(position + 1));
    return element;
}
```

#### 5. LargeInteger Bootstrapping (3-4 hours)
**Problem**: Inconsistent class lookup, incomplete FromBigInteger
**Impact**: SmallInteger overflow fallback broken
**Solution**:
- Implement consistent class lookup via SpecialObjectsArray
- Consolidate FromBigInteger implementations
- Add fallback to SmallInteger if value fits

**Implementation Approach**:
```csharp
private static SmalltalkOOP FromBigInteger(IPrimitiveContext ctx, BigInteger value)
{
    // Check if fits in SmallInteger range
    if (value >= int.MinValue && value <= int.MaxValue)
    {
        return SmalltalkOOP.FromSmallInteger((int)value);
    }

    // Determine class
    bool isNegative = value < 0;
    if (isNegative)
        value = -value;

    string className = isNegative ? "class:LargeNegativeInteger" : "class:LargePositiveInteger";

    // Convert to byte array
    byte[] bytes = value.ToByteArray();

    // Allocate and populate
    var largeIntOOP = ctx.AllocateObject(className, bytes.Length);
    for (int i = 0; i < bytes.Length; i++)
    {
        ctx.HeapManager.SetIndexedByte(largeIntOOP, i, bytes[i]);
    }

    return largeIntOOP;
}
```

### MEDIUM Priority - Infrastructure Issues

#### 6. PrimitiveTable Conflicts (1-2 hours)
**Problem**: Primitive 103 registered twice (Beep and StringReplace)
**Impact**: One implementation shadows the other
**Solution**:
- Audit PrimitiveTable.cs registration
- Resolve conflicts per Blue Book mapping
- Add assertion for unique registration

**Blue Book Mapping (verify)**:
- 102: ?
- 103: StringReplace (historically)
- 104: ?
- 105: Beep?

#### 7. Remove Catch-All Blocks (2-3 hours)
**Problem**: Broad catch blocks hide real errors
**Impact**: Bootstrap failures invisible, hard to debug
**Solution**:
- Replace with targeted validation
- Only catch expected exceptions
- Log unexpected exceptions
- Set PrimitiveSuccess explicitly

**Pattern**:
```csharp
// BEFORE (bad):
try {
    // ... operations ...
    return result;
}
catch {
    ctx.PrimitiveSuccess = false;
    return SpecialObjectsArray.Instance.Nil;
}

// AFTER (good):
// Validate arguments
if (args.Length != 1)
{
    ctx.PrimitiveSuccess = false;
    return SpecialObjectsArray.Instance.Nil;
}

if (!args[0].IsSmallInteger)
{
    ctx.PrimitiveSuccess = false;
    return SpecialObjectsArray.Instance.Nil;
}

try {
    // ... operations ...
    ctx.PrimitiveSuccess = true;  // Explicit on success
    return result;
}
catch (DivideByZeroException)
{
    // Expected failure - let Smalltalk handle
    ctx.PrimitiveSuccess = false;
    return SpecialObjectsArray.Instance.Nil;
}
catch (OverflowException)
{
    // Expected failure - overflow to LargeInteger
    ctx.PrimitiveSuccess = false;
    return SpecialObjectsArray.Instance.Nil;
}
// Let other exceptions propagate - they indicate bugs
```

#### 8. BlockClosure Contract (2-3 hours)
**Problem**: Multiple InvokeBlock signatures, inconsistent usage
**Impact**: Nested blocks may fail
**Solution**:
- Consolidate to single IPrimitiveContext.InvokeBlock signature
- Document contract (preconditions, ownership, exceptions)
- Test nested block with lexical capture

### LOW Priority - Future Improvements

#### 9. Process/Control Primitives (as needed)
- Mark stubs clearly with comments
- Return PrimitiveSuccess=false consistently
- Document unimplemented behavior

#### 10. Float Byte Order (when Float support needed)
- Document byte order (BitConverter.IsLittleEndian)
- Ensure cross-platform consistency

#### 11. Unit Tests (ongoing)
- SmallInteger arithmetic matrix
- Mod/Quo/Div sign combinations
- Stream boundary conditions
- BlockClosure lexical capture
- StringReplace overlapping copy

## Implementation Strategy

### Phase 1: Immediate Blockers (1 day)
1. Unify Nil accessor
2. Audit and document IPrimitiveContext API
3. Fix immediate compile errors

### Phase 2: Correctness (2-3 days)
4. Consolidate arithmetic implementations
5. Fix Stream indexing
6. Fix LargeInteger bootstrapping
7. Resolve primitive number conflicts

### Phase 3: Quality (1-2 days)
8. Improve error handling
9. BlockClosure contract
10. Add unit tests

### Phase 4: Future (as needed)
11. Process primitives
12. Float support
13. Comprehensive test suite

## Success Criteria

- ✅ Zero compile errors in primitive implementations
- ✅ All primitive numbers unique in PrimitiveTable
- ✅ Single canonical Nil accessor used everywhere
- ✅ Arithmetic operations match Blue Book semantics
- ✅ Stream operations handle boundaries correctly
- ✅ LargeInteger overflow fallback works
- ✅ Unit tests cover edge cases
- ✅ Bootstrap completes without primitive failures

## Risk Assessment

**Risk**: HIGH without fixes
- Multiple blockers prevent compilation
- Correctness issues cause wrong results
- Bootstrap may fail unpredictably

**Risk**: LOW with fixes
- Clear fix paths for all issues
- Can be done incrementally
- Testing validates correctness

## Next Steps

1. **Start with Phase 1 (Immediate Blockers)**
2. **Document findings as you go**
3. **Test after each major fix**
4. **Add unit tests incrementally**

Would you like me to start with any specific issue? I recommend:
1. **Unify Nil accessor** (quick, high impact)
2. **Audit IPrimitiveContext** (foundational)
3. **Fix arithmetic** (correctness critical)
