# Canonical Blue Book Bytecode Set

This document contains the complete canonical Smalltalk-80 bytecode set as defined in the Blue Book (Smalltalk-80: The Language and its Implementation).

## Bytecode Ranges

### Push Operations (0-95)

#### Push Receiver Variable (0-15)
- 0-15: pushReceiverVariable: 0-15

#### Push Temporary Variable (16-31)
- 16-31: pushTemporaryVariable: 0-15

#### Push Literal Constant (32-63)
- 32-63: pushLiteralConstant: 0-31

#### Push Literal Variable (64-95)
- 64-95: pushLiteralVariable: 0-31

### Store Operations (96-111)

#### Store and Pop Receiver Variable (96-103)
- 96-103: storeAndPopReceiverVariable: 0-7

#### Store and Pop Temporary Variable (104-111)
- 104-111: storeAndPopTemporaryVariable: 0-7

### Push Receiver and Constants (112-119)
- 112: pushReceiver (self)
- 113-119: **UNDEFINED** in canonical Blue Book

### Return Operations (120-125)
- 120: returnReceiver (return self)
- 121-123: **UNDEFINED** in canonical Blue Book
- 124: returnTop (return top of stack from method)
- 125: returnTop (return top of stack from block)

### Extended Operations (126-137)
- 126-127: **UNDEFINED** in canonical Blue Book
- 128: extendedPush
- 129: extendedStore
- 130: extendedStoreAndPop
- 131: singleExtendedSend
- 132: doubleExtendedSend
- 133: singleExtendedSuper
- 134: doubleExtendedSuper
- 135: popStack
- 136: duplicateTop
- 137: pushActiveContext

### Jump Operations (138-175)
- 138-143: **UNDEFINED** in canonical Blue Book
- 144-151: shortUnconditionalJump: 1-8
- 152-159: shortConditionalJump: 1-8 (pop and jump on false)
- 160-167: longUnconditionalJump
- 168-171: longConditionalJump (pop and jump on true)
- 172-175: longConditionalJump (pop and jump on false)

### Send Operations (176-255)
- 176-191: sendArithmeticSelector (special selectors 0-15)
- 192-207: sendCommonSelector (special selectors 16-31)
- 208-223: sendLiteralSelector with 0 args
- 224-239: sendLiteralSelector with 1 arg
- 240-255: sendLiteralSelector with 2 args

## Important Notes

1. **NO dedicated opcodes for true/false/nil**: These must be accessed via the literal frame (32-63)
2. **NO returnTrue/returnFalse/returnNil opcodes**: Only returnReceiver (120) and returnTop (124-125) exist
3. **NO pushTrue/pushFalse/pushNil opcodes**: Use pushLiteralConstant instead
4. Bytecodes 113-119, 121-123, 126-127, 138-143 are UNDEFINED in the canonical Blue Book

## Special Selectors (for bytecodes 176-207)

The first 32 special selectors that have optimized send bytecodes:

0. +
1. -
2. <
3. >
4. <=
5. >=
6. =
7. ~=
8. *
9. /
10. \\
11. @
12. bitShift:
13. //
14. bitAnd:
15. bitOr:
16. at:
17. at:put:
18. size
19. next
20. nextPut:
21. atEnd
22. ==
23. class
24. blockCopy:
25. value
26. value:
27. do:
28. new
29. new:
30. x
31. y

## Implementation Compliance

When implementing a Smalltalk-80 VM or bytecode compiler, strict adherence to this canonical set is required for compatibility with the original Blue Book specification. Any extensions or modifications should be clearly documented as non-canonical.