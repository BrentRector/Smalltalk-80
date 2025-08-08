# Bytecode Discrepancy Issue

## Problem
Our interpreter implementation uses DIFFERENT bytecode values than the canonical Smalltalk-80 specification.

## Key Differences

### Push Special Constants (MISMATCH!)
| Operation | Canonical | Our Implementation | Difference |
|-----------|-----------|-------------------|------------|
| pushReceiver (self) | 88 (0x58) | 112 (0x70) | +24 |
| pushTrue | 89 (0x59) | 113 (0x71) | +24 |
| pushFalse | 90 (0x5A) | 114 (0x72) | +24 |
| pushNil | 91 (0x5B) | 115 (0x73) | +24 |
| pushMinusOne | 92 (0x5C) | 116 (0x74) | +24 |
| pushZero | 93 (0x5D) | 117 (0x75) | +24 |
| pushOne | 94 (0x5E) | 118 (0x76) | +24 |
| pushTwo | 95 (0x5F) | 119 (0x77) | +24 |

### Return Instructions (MISMATCH!)
| Operation | Canonical | Our Implementation | Difference |
|-----------|-----------|-------------------|------------|
| returnReceiver | 95 (0x5F) | 120 (0x78) | +25 |
| returnTrue | 96 (0x60) | 121 (0x79) | +25 |
| returnFalse | 97 (0x61) | 122 (0x7A) | +25 |
| returnNil | 98 (0x62) | 123 (0x7B) | +25 |
| returnTop | 99 (0x63) | 124 (0x7C) | +25 |
| returnTopFromBlock | 100 (0x64) | 125 (0x7D) | +25 |

### What's at 88-95 in Our Implementation?
In canonical Smalltalk-80, 88-95 are push constants.
In our implementation, 88-95 appear to be part of the literal variable range (64-95).

### What's at 96-103 in Our Implementation?
| Our Implementation | Canonical Meaning |
|-------------------|-------------------|
| 96-103: StoreAndPopReceiverVariable0-7 | Should be return instructions |

## Impact
This discrepancy means:
1. **Bytecode compiled for standard Smalltalk-80 will NOT work** with our interpreter
2. **Our bytecodes are incompatible** with standard Smalltalk tools
3. **Any external Smalltalk code** needs bytecode translation to run

## Recommendation
We should consider fixing our Bytecode enum to match the canonical values. This would require:
1. Updating the Bytecode.cs enum values
2. Adjusting the interpreter's dispatch logic
3. Updating any existing bytecode in our tests

## Current Workaround
For now, when creating bytecode for our interpreter, we must use our non-standard values:
- Use 112 instead of 88 for push self
- Use 120 instead of 95 for return self
- Use 124 instead of 99 for return top
- etc.

This is documented in BYTECODE_REFERENCE.md for our implementation.