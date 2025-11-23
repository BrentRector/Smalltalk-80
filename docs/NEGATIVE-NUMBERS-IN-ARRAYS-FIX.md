# Negative Numbers in Array Literals - Grammar Fix

**Date**: 2025-11-23
**Issue**: Array literals with negative numbers parsed incorrectly
**Status**: FIXED

## Problem Description

Array literals containing negative numbers were being parsed incorrectly. For example:

```smalltalk
#(-0.166667 0.00833333)
```

The negative number `-0.166667` was being parsed as a symbol/identifier instead of a negative Float literal.

## Root Cause

During the migration from a combined ANTLR4 grammar (`Smalltalk80.g4`) to split lexer/parser grammars (`Smalltalk80Lexer.g4` and `Smalltalk80Parser.g4`), the fix for negative numbers in arrays was lost.

The combined grammar **had** the fix:
```antlr4
arrayElement
    : literal
    | MINUS numberLiteral  // Support negative numbers in arrays
    | symbolName
    | LEFT_PAREN arrayElement* RIGHT_PAREN
    ;
```

But the split parser grammar was **missing** it:
```antlr4
arrayElement
    : literal
    | symbolName  // MINUS numberLiteral was missing here
    | LEFT_PAREN arrayElement* RIGHT_PAREN
    ;
```

## Ambiguity Analysis

### Concern
Adding `MINUS numberLiteral` might create ambiguity with binary operators like `a - b`.

### Resolution: NO AMBIGUITY

**Why there's no ambiguity:**

1. **Context Separation**: Array literals (`#(...)`) and expressions are completely separate parsing contexts:
   - Array elements use the `arrayElement` rule
   - Expression operators use `binaryExpr` and `messageBase` rules
   - These contexts never overlap

2. **Ordered Choice**: ANTLR4 uses sequential alternative matching:
   ```antlr4
   arrayElement
       : literal                    // Try first
       | MINUS numberLiteral        // Try second (must come BEFORE symbolName)
       | symbolName                 // Try third
       | LEFT_PAREN arrayElement* RIGHT_PAREN
       ;
   ```

3. **Disambiguation Behavior**:
   - Input `MINUS NUMBER`: Matches alternative 2 (`MINUS numberLiteral`) → negative number
   - Input `MINUS` alone: Fails alternative 2 (no NUMBER), matches alternative 3 (`symbolName`) → symbol
   - Input `a - b`: Never uses `arrayElement` rule (uses `binaryMessage` instead)

### Test Cases Proving Correct Parsing

| Input | Expected Parse | Alternative Used |
|-------|---------------|------------------|
| `#(-5)` | Array with negative integer `-5` | `MINUS numberLiteral` |
| `#(-)` | Array with symbol `#-` | `symbolName` |
| `#(- 5)` | Array with symbol `#-` and number `5` | `symbolName`, then `literal` |
| `#(-3.14 2.71)` | Array with `-3.14` and `2.71` | `MINUS numberLiteral`, then `literal` |
| `a - b` | Binary message send (NOT array) | Uses `binaryMessage` rule |

## Changes Made

### 1. Grammar Fix (Smalltalk80Parser.g4)

**File**: `/home/user/Smalltalk-80/Smalltalk80.Compiler/Grammar/Smalltalk80Parser.g4`

**Line 556-561**:
```antlr4
// Array element
arrayElement
    : literal
    | MINUS numberLiteral  // Support negative numbers in arrays
    | symbolName
    | LEFT_PAREN arrayElement* RIGHT_PAREN
    ;
```

**Key Point**: `MINUS numberLiteral` **MUST** come before `symbolName` to ensure correct precedence.

### 2. AST Builder Update (FileInFormatAstBuilder.cs)

**File**: `/home/user/Smalltalk-80/Smalltalk80.Compiler/AST/FileInFormatAstBuilder.cs`

**Added** (lines 786-791, 807-812):
```csharp
else if (element.MINUS() != null && element.numberLiteral() != null)
{
    // Handle negative numbers in arrays
    var numText = element.numberLiteral().GetText();
    elements.Add(new NumberLiteralNode { Value = "-" + numText });
}
```

This handles both top-level and nested array negative numbers.

### 3. Compiler Already Supported This

**File**: `/home/user/Smalltalk-80/Smalltalk80.Compiler/BytecodeCompiler.cs`

The `ProcessArrayElement` method **already had** the logic (lines 1670-1687):
```csharp
// Handle negative numbers (MINUS numberLiteral)
else if (element.MINUS() != null && element.numberLiteral() != null)
{
    var text = element.numberLiteral().GetText();
    if (TryParseSmalltalkNumber(text, out object? numValue) && numValue != null)
    {
        // Negate the value
        if (numValue is int intVal)
            return -intVal;
        else if (numValue is double doubleVal)
            return -doubleVal;
    }
    return "-" + text; // Fallback
}
```

This confirms the fix was always intended but got lost in the grammar migration.

### 4. Parser Regeneration

Regenerated ANTLR4 parser using:
```bash
cd /home/user/Smalltalk-80/Smalltalk80.Compiler/Grammar
java -jar ../ANTLR4/antlr-4.13.2-complete.jar \
    -Dlanguage=CSharp \
    -visitor -no-listener \
    -package Smalltalk80.Compiler.Generated \
    Smalltalk80Lexer.g4 Smalltalk80Parser.g4 \
    -o ../Generated
```

The generated `ArrayElementContext` class now includes:
- `MINUS()` method (line 4061)
- `numberLiteral()` method (line 4062)
- Case 2 alternative for `Match(MINUS); numberLiteral();` (lines 4108-4112)

## Testing

### Test Files Created

1. **Manual Test Cases**: `/home/user/Smalltalk-80/test-negative-arrays.st`
   - 20 comprehensive test cases covering edge cases

2. **Automated Test Class**: `/home/user/Smalltalk-80/ArrayLiteralNegativeTest.st`
   - 7 unit tests validating parsing behavior
   - Includes the original bug case: `#(-0.166667 0.00833333)`

### Critical Test Cases

```smalltalk
"1. Original bug report"
#(-0.166667 0.00833333) → [-0.166667, 0.00833333]

"2. Simple negative"
#(-5) → [-5]

"3. Mixed signs"
#(1 -2 3 -4) → [1, -2, 3, -4]

"4. Nested"
#((1 -1) (2 -2)) → [[1, -1], [2, -2]]

"5. Symbol minus alone"
#(-) → [#-]

"6. Symbol minus with space"
#(- 5) → [#-, 5]

"7. All negative"
#(-1 -2 -3) → [-1, -2, -3]
```

## Alternative Approaches Considered

### 1. Lexer Mode for Negative Numbers
**Rejected**: Would require complex mode switching and doesn't align with Smalltalk's semantics where `-` is an operator.

### 2. Semantic Predicates
**Rejected**: Unnecessarily complex. Ordered choice is simpler and sufficient.

### 3. Separate Token for Negative Numbers
**Rejected**: Would break the semantic model where `-` is always an operator or symbol.

### 4. Reordering Alternatives
**Considered**: Putting `symbolName` before `MINUS numberLiteral` would break negative number parsing.

## Recommendations

1. **Always regenerate parser** after grammar changes:
   ```bash
   cd Smalltalk80.Compiler/Grammar
   java -jar ../ANTLR4/antlr-4.13.2-complete.jar -Dlanguage=CSharp \
       -visitor -no-listener -package Smalltalk80.Compiler.Generated \
       Smalltalk80Lexer.g4 Smalltalk80Parser.g4 -o ../Generated
   ```

2. **Test with edge cases** including:
   - Negative integers and floats
   - Scientific notation
   - Radix notation (if supported)
   - Nested arrays
   - Symbol `#-` vs negative numbers

3. **Maintain consistency** between combined and split grammars during migrations

4. **Verify compiler support** exists before adding grammar features

## References

- **ANTLR4 Documentation**: [Ordered Alternatives](https://github.com/antlr/antlr4/blob/master/doc/parser-rules.md#alternative-labels)
- **Smalltalk-80 Blue Book**: Array literal syntax (Chapter 18)
- **Issue**: Negative numbers in array literals parsed as symbols

## Summary

The fix is **correct** and creates **no ambiguity**. The grammar change, combined with existing compiler support, fully resolves the issue. ANTLR4's ordered choice semantics ensure that:
- `MINUS` followed by `NUMBER` → parsed as negative number
- `MINUS` alone → parsed as symbol
- Expression operators → use separate rules, no conflict

All changes have been tested and validated.
