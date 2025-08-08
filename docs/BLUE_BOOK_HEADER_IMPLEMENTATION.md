# Blue Book Method Header Implementation

## Summary

We have successfully implemented proper Blue Book method header encoding in the Smalltalk-80 VM according to the specification.

## Blue Book Header Format (16-bit word)

- **Bits 0-8**: Primitive number (0-511, where 0 means no primitive)
- **Bit 9**: Large context flag (1 if method needs > 16 temporaries)
- **Bits 10-15**: Number of temporaries/stack size (0-63)

## Implementation Changes

### 1. Phase3Executor.cs (lines 295-317)
Added proper Blue Book header encoding when installing methods:
```csharp
int primitiveNum = methodDef.Primitive & 0x1FF; // 9 bits for primitive
int largeContext = 0; // We don't have large contexts yet
int tempCount = methodDef.NumArgs; // For now, temps = args

// Blue Book header encoding
int headerValue = primitiveNum | (largeContext << 9) | (tempCount << 10);
```

### 2. VMPrimitiveAdapter.cs (lines 892-919)
Added proper Blue Book header decoding in ExtractCompiledMethod:
```csharp
// Decode Blue Book header format (16-bit word):
primitiveNumber = headerInt & 0x1FF;  // Lower 9 bits
bool largeContext = (headerInt & 0x200) != 0;  // Bit 9
tempCount = (headerInt >> 10) & 0x3F;  // Bits 10-15
```

### 3. MethodDef Structure
Added primitive field to MethodDef in Phase3Executor:
```csharp
[JsonPropertyName("primitive")]
public int Primitive { get; set; } = 0;
```

## Results

### Working Examples
- **Primitive 70 (basicNew)**: Header = 0x46 (binary: 0000000001000110)
  - Primitive = 70 (bits 0-8)
  - Large context = 0 (bit 9)
  - Temps = 0 (bits 10-15)
  
- **SmallInteger arithmetic methods**: All properly encode primitive numbers 1-18

### Remaining Issue
Phase 2 bootstrap methods still use simple argument counts as header values, which are now incorrectly interpreted as primitive numbers. For example:
- Method `at:put:` with 2 arguments has header value 2
- This is incorrectly interpreted as primitive 2 (subtract)

## Next Steps
1. Update Phase 2 bootstrap to use proper Blue Book header encoding
2. For methods without primitives, encode as: `(tempCount << 10)`
3. For methods with primitives, encode as: `primitive | (tempCount << 10)`

## Test Files Created
- `Bootstrap/bootstrap_phase3_smallinteger.json`: Creates SmallInteger class with primitive arithmetic methods
- `Bootstrap/bootstrap_phase3_test_primitive.json`: Initial test file for primitive methods

## Verification
After implementing the Blue Book header encoding:
- Primitive methods are correctly identified (e.g., primitive 70 for basicNew)
- Temporary counts are properly extracted
- The large context flag is recognized (though not yet used)