# Bytecode Mapping: Our Implementation vs Canonical Smalltalk-80

## Important Note
Our current implementation uses DIFFERENT bytecode values than the canonical Smalltalk-80 specification. This document shows the mapping between them.

## Comparison Table

| Operation | Canonical Value | Our Value | Status |
|-----------|----------------|-----------|---------|
| **Push Instance Variables** |
| pushReceiverVariable: 0-15 | 0-15 | 0-15 | ✅ Matches |
| **Push Temporaries/Arguments** |
| pushTemporaryVariable: 0-15 | 16-31 | 16-31 | ✅ Matches |
| **Push Literals** |
| pushLiteralConstant: 0-15 | 32-47 | 32-47 | ✅ Matches |
| pushLiteralConstant: 16-31 | 48-63 | 48-63 | ✅ Matches |
| **Push Literal Variables** |
| pushLiteralVariable: 0-15 | 64-79 | 64-79 | ✅ Matches |
| pushLiteralVariable: 16-31 | 80-95 | 80-95 | ✅ Matches |
| **Store and Pop** |
| popIntoReceiverVariable: 0-7 | 96-103 | 96-103 | ⚠️ Named differently |
| popIntoTemporaryVariable: 0-7 | 104-111 | 104-111 | ⚠️ Named differently |
| **Push Special Constants** |
| pushReceiver (self) | 112 (0x70) | 112 | ✅ Matches! |
| pushTrue | 113 (0x71) | 113 | ✅ Matches! |
| pushFalse | 114 (0x72) | 114 | ✅ Matches! |
| pushNil | 115 (0x73) | 115 | ✅ Matches! |
| pushMinusOne | 116 (0x74) | 116 | ✅ Matches! |
| pushZero | 117 (0x75) | 117 | ✅ Matches! |
| pushOne | 118 (0x76) | 118 | ✅ Matches! |
| pushTwo | 119 (0x77) | 119 | ✅ Matches! |
| **Return Instructions** |
| returnReceiver | 120 (0x78) | 120 | ✅ Matches! |
| returnTrue | 121 (0x79) | 121 | ✅ Matches! |
| returnFalse | 122 (0x7A) | 122 | ✅ Matches! |
| returnNil | 123 (0x7B) | 123 | ✅ Matches! |
| returnTop | 124 (0x7C) | 124 | ✅ Matches! |
| returnTopFromBlock | 125 (0x7D) | 125 | ✅ Matches! |

## Correction to Previous Analysis

I was wrong! After checking more carefully, our implementation DOES use the canonical Smalltalk-80 bytecode values. The confusion arose from:

1. Looking at the wrong enum values (I was seeing 88 in decimal when checking for 0x58)
2. Different naming conventions (we use "StoreAndPopReceiverVariable" instead of "popIntoReceiverVariable")

## Actual Implementation Status

Our interpreter correctly implements the canonical Smalltalk-80 bytecodes with these mappings:
- ✅ 0-15: Push instance variables
- ✅ 16-31: Push temporaries/arguments  
- ✅ 32-63: Push literal constants
- ✅ 64-95: Push literal variables
- ✅ 96-103: Pop into instance variables
- ✅ 104-111: Pop into temporaries
- ✅ 112-119: Push special constants (self, true, false, nil, -1, 0, 1, 2)
- ✅ 120-125: Return instructions
- ✅ 128-255: Extended and send instructions

The bytecode values are CORRECT and match the canonical Smalltalk-80 specification!