# Arithmetic Semantics - Blue Book Reference

**Date**: 2025-01-24
**Source**: Smalltalk-80 Blue Book (Goldberg & Robson)
**Purpose**: Canonical reference for primitive arithmetic

## Integer Division Operations

Smalltalk-80 has THREE division-related operations with distinct semantics:

### 1. `/` (Divide) - Primitive 10
**Selector**: `/`
**Semantics**: Integer division truncated toward zero
**Examples**:
```smalltalk
5 / 3    => 1
-5 / 3   => -1   (truncate toward zero)
5 / -3   => -1   (truncate toward zero)
-5 / -3  => 1
```

**C# Implementation**:
```csharp
int result = a / b;  // C# / truncates toward zero - CORRECT for quo:
```

### 2. `\\` (Mod) - Primitive 11
**Selector**: `\\`
**Semantics**: Remainder with sign of **divisor** (not dividend)
**Rule**: `(a \\ b)` has same sign as `b`
**Examples**:
```smalltalk
5 \\ 3   => 2
-5 \\ 3  => 1    (not -2, must have sign of divisor)
5 \\ -3  => -1   (not 2, must have sign of divisor)
-5 \\ -3 => -2
```

**C# Implementation**:
```csharp
int rem = a % b;
// C# % has sign of dividend, so adjust when signs differ
if (rem != 0 && ((a < 0) != (b < 0)))
    rem += b;
return rem;
```

**Verification**:
- `-5 % 3 = -2` (C#), but we want `1` → `-2 + 3 = 1` ✓
- `5 % -3 = 2` (C#), but we want `-1` → `2 + (-3) = -1` ✓

### 3. `//` (Quo) - Primitive 12
**Selector**: `//`
**Semantics**: Truncated division (toward zero) - same as `/`
**Note**: In Smalltalk-80, `quo:` and `/` are identical for integers
**Examples**:
```smalltalk
5 // 3   => 1
-5 // 3  => -1   (truncate toward zero)
5 // -3  => -1   (truncate toward zero)
-5 // -3 => 1
```

**C# Implementation**:
```csharp
int result = a / b;  // C# / truncates toward zero - CORRECT
```

### 4. `div:` (Div) - Primitive 17
**Selector**: `div:`
**Semantics**: Floor division (toward -infinity)
**Rule**: Always rounds DOWN (toward -infinity)
**Examples**:
```smalltalk
5 div: 3   => 1
-5 div: 3  => -2   (not -1, must floor toward -infinity)
5 div: -3  => -2   (not -1, must floor toward -infinity)
-5 div: -3 => 1
```

**C# Implementation**:
```csharp
int quot = a / b;  // Truncated quotient
int rem = a % b;
// Adjust quotient if signs differ and remainder non-zero
if (rem != 0 && ((a < 0) != (b < 0)))
    quot--;
return quot;
```

**Verification**:
- `-5 / 3 = -1` (C#), but we want `-2` → `-1 - 1 = -2` ✓
- `5 / -3 = -1` (C#), but we want `-2` → `-1 - 1 = -2` ✓

## Relationship Between Operations

**Mathematical Identity**:
```
a = (a // b) * b + (a \\ b)
```

**For all integers a, b (b ≠ 0)**:
- `a // b` gives quotient
- `a \\ b` gives remainder
- Quotient * divisor + remainder = dividend

**Examples**:
```smalltalk
"5 = 1*3 + 2"
5 // 3  => 1
5 \\ 3  => 2
(5 // 3) * 3 + (5 \\ 3) => 5  ✓

"-5 = -2*3 + 1"  (Note: remainder has sign of divisor!)
-5 div: 3  => -2
-5 \\ 3    => 1
(-5 div: 3) * 3 + (-5 \\ 3) => -6 + 1 => -5  ✓

"-5 = -1*3 + -2" (Using quo: instead of div:)
-5 // 3  => -1
-5 \\ 3  => 1
Wait, this breaks! (-1 * 3) + 1 = -2 ≠ -5
```

**IMPORTANT**: The identity holds with `div:` (floor division), NOT with `quo:` (truncated division).

## Comparison with Other Languages

### C# / Java / C++
```csharp
/  : Truncated division (toward zero) = Smalltalk quo:
%  : Remainder with sign of dividend ≠ Smalltalk \\
```

### Python 3
```python
//  : Floor division (toward -infinity) = Smalltalk div:
%   : Modulo with sign of divisor = Smalltalk \\
```

### Ruby
```ruby
/   : Floor division (toward -infinity) = Smalltalk div:
%   : Modulo with sign of divisor = Smalltalk \\
```

## Test Cases (Edge Cases)

### Positive / Positive
```smalltalk
7 // 3   => 2    (quo)
7 div: 3 => 2    (div - same)
7 \\ 3   => 1    (mod)
```

### Negative / Positive
```smalltalk
-7 // 3   => -2   (quo - truncate)
-7 div: 3 => -3   (div - floor)
-7 \\ 3   => 2    (mod - sign of divisor)
```

### Positive / Negative
```smalltalk
7 // -3   => -2   (quo - truncate)
7 div: -3 => -3   (div - floor)
7 \\ -3   => -2   (mod - sign of divisor)
```

### Negative / Negative
```smalltalk
-7 // -3   => 2    (quo - truncate)
-7 div: -3 => 2    (div - floor, same)
-7 \\ -3   => -1   (mod - sign of divisor)
```

### Zero Cases
```smalltalk
0 // 5   => 0
0 div: 5 => 0
0 \\ 5   => 0

5 // 0   => Error (division by zero)
5 div: 0 => Error (division by zero)
5 \\ 0   => Error (division by zero)
```

## Current Implementation Status

### ✅ Mod (Primitive 11) - CORRECT
Lines 277-318 in ArithmeticPrimitives.cs
```csharp
int rem = a % b;
if (rem != 0 && ((a < 0) != (b < 0)))
    rem += b;
```
**Status**: Implements Blue Book semantics correctly

### ✅ Quo (Primitive 12) - CORRECT
Lines 321-354 in ArithmeticPrimitives.cs
```csharp
int result = a / b;
```
**Status**: C# / truncates toward zero, which matches quo: semantics

### ❌ Div (Primitive 17) - WRONG
Lines 464-497 in ArithmeticPrimitives.cs
```csharp
int result = a / b;  // Same as Quo - WRONG!
```
**Status**: Should be floor division, currently truncates toward zero

### ❌ Divide (Primitive 10) - AMBIGUOUS
Lines 241-274 in ArithmeticPrimitives.cs
```csharp
int result = a / b;
```
**Status**: Historically, SmallInteger / produces integer result. Blue Book is ambiguous on whether it's quo or div semantics. Recommend: Use quo: semantics (truncate toward zero) for consistency with Quo primitive.

## Recommended Fixes

### Fix Div (Primitive 17)
```csharp
// Primitive 17: SmallInteger div:
public static SmalltalkOOP Div(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
{
    if (args.Length != 1)
    {
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }

    if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
    {
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }

    int a = receiver.AsSmallInteger();
    int b = args[0].AsSmallInteger();

    if (b == 0)
    {
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }

    try
    {
        // Floor division (toward -infinity)
        int quot = a / b;
        int rem = a % b;

        // Adjust if signs differ and remainder non-zero
        if (rem != 0 && ((a < 0) != (b < 0)))
            quot--;

        return SmalltalkOOP.FromSmallInteger(quot);
    }
    catch
    {
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }
}
```

### Document Divide (Primitive 10)
Add comment clarifying it uses truncated division (quo: semantics):
```csharp
// Primitive 10: SmallInteger /
// Blue Book: For integers, / produces truncated integer result (same as quo:)
public static SmalltalkOOP Divide(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
{
    // ... existing implementation is correct ...
}
```

## Verification Matrix

| a | b | a/b (/) | a//b (quo:) | a div: b | a\\b (mod) | Check: (a div: b)*b + (a\\b) == a |
|---|---|---------|-------------|----------|------------|------------------------------------|
| 7 | 3 | 2 | 2 | 2 | 1 | 2*3 + 1 = 7 ✓ |
| -7 | 3 | -2 | -2 | **-3** | **2** | -3*3 + 2 = -7 ✓ |
| 7 | -3 | -2 | -2 | **-3** | **-2** | -3*-3 + -2 = 9 - 2 = 7 ✓ |
| -7 | -3 | 2 | 2 | 2 | -1 | 2*-3 + -1 = -7 ✓ |
| 5 | 3 | 1 | 1 | 1 | 2 | 1*3 + 2 = 5 ✓ |
| -5 | 3 | -1 | -1 | **-2** | **1** | -2*3 + 1 = -5 ✓ |
| 5 | -3 | -1 | -1 | **-2** | **-1** | -2*-3 + -1 = 6 - 1 = 5 ✓ |
| -5 | -3 | 1 | 1 | 1 | -2 | 1*-3 + -2 = -5 ✓ |

**Bold** = Differs from C# native `/` and `%` operators

## References

- Smalltalk-80: The Language and its Implementation (Blue Book), Goldberg & Robson, 1983
- Chapter on Number Protocol, pages 306-307
- Primitive descriptions in implementation chapters
