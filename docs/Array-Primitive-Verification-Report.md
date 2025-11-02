# Array Primitive Fixed Slot Handling - Verification Report
## Date: 2025-11-02

## Executive Summary

✅ **VERIFIED CORRECT** - Array primitives (60-62) correctly handle fixed instance variable slots according to the Blue Book specification.

---

## Blue Book Specification

From "Formal Specification of the Primitive Methods.md" lines 734-748:

```smalltalk
primitiveAt
    | index array arrayClass result |
    index ← self positive16BitValueOf: self popStack.
    array ← self popStack.
    arrayClass ← memory fetchClassOf: array.
    self checkIndexableBoundsOf: index
         in: array.
    self success
        ifTrue: [index ← index + (self fixedFieldsOf: arrayClass).  ← KEY LINE
                result ← self subscript: array
                              with: index].
    self success
        ifTrue: [self push: result]
        ifFalse: [self unPop: 2]
```

**Key Requirement** (line 742): The index must be adjusted by adding the fixed fields count before accessing the underlying storage:
```smalltalk
index ← index + (self fixedFieldsOf: arrayClass).
```

This ensures that:
- Smalltalk `at: 1` accesses the first **indexed** field, not the class header or instance variables
- The class header (slot 0) and instance variables are invisible to `at:`/`at:put:`

---

## Our Implementation Analysis

### Primitive 60 (at:) - ObjectPrimitives.cs lines 15-77

```csharp
// Line 51: Get Smalltalk 1-based index
int index = args[0].AsSmallInteger();

// Line 52-56: Validate index >= 1
if (index < 1)
{
    ctx.PrimitiveSuccess = false;
    return SpecialObjectsArray.Instance.Nil;
}

// Line 59: Get indexed size (automatically excludes fixed slots)
int size = ctx.HeapManager.GetIndexedSize(receiver);

// Line 60-64: Bounds check
if (index > size)
{
    ctx.PrimitiveSuccess = false;
    return SpecialObjectsArray.Instance.Nil;
}

// Line 68: Convert to 0-based and access
var result = ctx.HeapManager.GetIndexedOOP(receiver, index - 1);
```

### HeapManager Implementation - SmalltalkHeapManager.cs

**GetIndexedOOP** (line 409) → **ReadIndexedOop** (line 642):

```csharp
private SmalltalkOOP ReadIndexedOop(ObjectTableEntry entry, int index)
{
    Debug.Assert(entry.FormatExplicit == ObjectFormat.PointerIndexed);

    var oopArray = entry.AsOopArray();

    // Calculate offset: header (1) + instance variables
    int offset = 1 + entry.InstanceVariableCountExplicit;  ← KEY LINE (652)
    int indexedSize = oopArray.Length - offset;

    // Bounds check on indexed portion only
    if (index < 0 || index >= indexedSize)
        throw new ArgumentOutOfRangeException(...);

    // Access: header + instance vars + 0-based indexed position
    return oopArray[offset + index];  ← ACCESS (659)
}
```

**GetIndexedSize** (line 825) → **GetIndexedSizeFromEntry** (line 387):

```csharp
private int GetIndexedSizeFromEntry(ObjectTableEntry entry)
{
    int totalWords = entry.SizeInWords;
    int headerWords = 1;
    int instanceVarCount = entry.InstanceVariableCount;

    // Calculate indexed portion: total - header - instance variables
    int indexedWords = totalWords - headerWords - instanceVarCount;  ← KEY LINE (394)

    return entry.Format switch
    {
        ObjectFormat.PointerIndexed => indexedWords,
        // ... other formats
    };
}
```

---

## Verification Example

### Scenario: OrderedCollection with 4 instance variables

**Object Layout**:
```
Slot 0: classOOP (header)
Slot 1: firstIndex (instance variable)
Slot 2: lastIndex (instance variable)
Slot 3: array (instance variable)
Slot 4: tally (instance variable)
Slot 5: element[0] (first indexed field)
Slot 6: element[1] (second indexed field)
...
```

**Smalltalk Code**: `collection at: 1`

**Execution Flow**:

1. **Primitive 60** receives `index = 1` (Smalltalk 1-based)
2. **Converts to 0-based**: `index - 1 = 0`
3. **GetIndexedSize** returns: `totalSlots - 1 (header) - 4 (instance vars) = N` (indexed elements only)
4. **Bounds check**: `1 <= N` ✓
5. **ReadIndexedOop** calculates:
   - `offset = 1 + 4 = 5` (skip header + 4 instance variables)
   - `arrayIndex = 5 + 0 = 5`
   - Returns `oopArray[5]` ← **First indexed element!** ✓

**Result**: ✅ Correctly accesses slot 5 (first indexed field), skipping header and instance variables.

---

## Blue Book Conformance

### primitiveAt Specification Mapping

| Blue Book Step | Our Implementation | Status |
|----------------|-------------------|--------|
| Get index from stack | `int index = args[0].AsSmallInteger()` | ✅ |
| Get array from stack | `receiver` parameter | ✅ |
| Get array class | Implicit in `HeapManager.GetIndexedOOP` | ✅ |
| Check bounds | `if (index > size)` | ✅ |
| **Add fixed fields to index** | **`offset = 1 + instanceVarCount`** | ✅ |
| Subscript array | `oopArray[offset + index]` | ✅ |
| Push result | `return result` | ✅ |

### Key Conformance Point

**Blue Book**: `index ← index + (self fixedFieldsOf: arrayClass)`
**Our Code**: `offset = 1 + entry.InstanceVariableCountExplicit`

These are **semantically equivalent**:
- Blue Book adjusts the Smalltalk index before subscripting
- We calculate a permanent offset and add it during subscripting
- Both result in the same array position being accessed

---

## Additional Verification: Primitive 61 (at:put:) and 62 (size)

### Primitive 61 (at:put:) - ObjectPrimitives.cs lines 79-163

Uses the same pattern:
```csharp
// Line 93: Get index
int index = args[0].AsSmallInteger();

// Line 108: Get size (excludes fixed slots)
int size = ctx.HeapManager.GetIndexedSize(receiver);

// Line 123: Convert to 0-based and set
ctx.HeapManager.SetIndexedOOP(receiver, index - 1, value);
```

**SetIndexedOOP** → **WriteIndexedOop** (line 665):
```csharp
private void WriteIndexedOop(ObjectTableEntry entry, int index, SmalltalkOOP value)
{
    var oopArray = entry.AsOopArray();

    // Same offset calculation
    int offset = 1 + entry.InstanceVariableCountExplicit;
    int indexedSize = oopArray.Length - offset;

    // Bounds check
    if (index < 0 || index >= indexedSize)
        throw new ArgumentOutOfRangeException(...);

    // Write: header + instance vars + indexed position
    oopArray[offset + index] = value;
}
```

✅ **Correctly handles fixed slots**

### Primitive 62 (size) - ObjectPrimitives.cs lines 165-182

```csharp
public static SmalltalkOOP Size(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
{
    // ...validation...

    // Returns indexed size only (excludes header + instance variables)
    int size = ctx.HeapManager.GetIndexedSize(receiver);

    return SmalltalkOOP.FromSmallInteger(size);
}
```

Uses `GetIndexedSizeFromEntry` which calculates:
```csharp
int indexedWords = totalWords - headerWords - instanceVarCount;
```

✅ **Returns indexed element count only, correctly excluding fixed slots**

---

## Edge Cases Verified

### 1. Pure PointerIndexed Object (e.g., Array with no instance variables)

**Layout**:
```
Slot 0: classOOP (header only)
Slot 1: element[0]
Slot 2: element[1]
```

**Calculation**:
- `offset = 1 + 0 = 1`
- `at: 1` → `index = 0` → `array[1 + 0] = array[1]` ✓

### 2. Mixed Object (e.g., OrderedCollection with instance vars + indexed)

**Layout**:
```
Slot 0: classOOP
Slot 1-4: instance variables
Slot 5-N: indexed elements
```

**Calculation**:
- `offset = 1 + 4 = 5`
- `at: 1` → `index = 0` → `array[5 + 0] = array[5]` ✓

### 3. Pointer-only Object (e.g., Point with 2 instance vars, no indexed)

**GetIndexedSize**:
- `totalWords = 3` (header + x + y)
- `indexedWords = 3 - 1 - 2 = 0` ✓
- Returns 0, so `at:` will always fail bounds check ✓

---

## Conclusion

✅ **VERIFICATION PASSED**

Our array primitive implementation (60-62) **correctly handles fixed instance variable slots** according to the Blue Book specification.

**Evidence**:
1. ✅ `GetIndexedSize` correctly excludes header and instance variables
2. ✅ `ReadIndexedOop` correctly offsets by `1 + instanceVarCount`
3. ✅ `WriteIndexedOop` uses same offset calculation
4. ✅ Smalltalk `at: 1` correctly accesses first indexed element
5. ✅ `size` correctly returns indexed element count only
6. ✅ Edge cases (no instance vars, all instance vars) handled correctly

**No changes required** - the implementation is correct as-is.

---

## Testing Recommendations

While the implementation is correct, comprehensive tests would verify:

1. **Array with no instance variables**:
   ```smalltalk
   arr := #(10 20 30).
   arr at: 1  "→ 10"
   arr size   "→ 3"
   ```

2. **OrderedCollection with instance variables**:
   ```smalltalk
   oc := OrderedCollection new.
   oc add: 42.
   oc at: 1  "→ 42 (not firstIndex!)"
   ```

3. **Boundary conditions**:
   ```smalltalk
   arr at: 0    "→ error (index < 1)"
   arr at: 4    "→ error (index > size)"
   ```

These tests would confirm the correct offset behavior in practice.
