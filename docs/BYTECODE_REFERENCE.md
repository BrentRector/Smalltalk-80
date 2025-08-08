# Smalltalk-80 Bytecode Reference

This document describes the bytecode values used in our Smalltalk-80 interpreter implementation.

## Push Instructions

### Instance Variables (0-15)
- `0`: PushReceiverVariable0 - Push instance variable 0
- `1`: PushReceiverVariable1 - Push instance variable 1
- ...
- `15`: PushReceiverVariable15 - Push instance variable 15

### Temporaries/Arguments (16-31)
- `16`: PushTemporaryVariable0 - Push temporary/argument 0
- `17`: PushTemporaryVariable1 - Push temporary/argument 1
- ...
- `31`: PushTemporaryVariable15 - Push temporary/argument 15

### Literal Constants (32-63)
- `32`: PushLiteralConstant0 - Push literal 0
- `33`: PushLiteralConstant1 - Push literal 1
- ...
- `63`: PushLiteralConstant31 - Push literal 31

### Literal Variables (64-95)
- `64`: PushLiteralVariable0 - Push value from Association in literal 0
- ...
- `95`: PushLiteralVariable31 - Push value from Association in literal 31

## Store Instructions

### Store and Pop Instance Variables (96-103)
- `96`: StoreAndPopReceiverVariable0 - Pop stack, store into instance variable 0
- `97`: StoreAndPopReceiverVariable1 - Pop stack, store into instance variable 1
- ...
- `103`: StoreAndPopReceiverVariable7 - Pop stack, store into instance variable 7

### Store and Pop Temporaries (104-111)
- `104`: StoreAndPopTemporaryVariable0 - Pop stack, store into temporary 0
- ...
- `111`: StoreAndPopTemporaryVariable7 - Pop stack, store into temporary 7

## Special Push Instructions (112-119)
- `112`: PushSelf - Push receiver (self)
- `113`: PushTrue - Push true
- `114`: PushFalse - Push false
- `115`: PushNil - Push nil
- `116`: PushMinusOne - Push -1
- `117`: PushZero - Push 0
- `118`: PushOne - Push 1
- `119`: PushTwo - Push 2

## Return Instructions (120-125)
- `120`: ReturnSelf - Return receiver (self)
- `121`: ReturnTrue - Return true
- `122`: ReturnFalse - Return false
- `123`: ReturnNil - Return nil
- `124`: ReturnTopFromMethod - Return top of stack from method
- `125`: ReturnTopFromBlock - Return top of stack from block

## Extended Instructions
- `128`: ExtendedPush - Extended push (followed by descriptor byte)
- `129`: ExtendedStore - Extended store (followed by descriptor byte)
- `130`: ExtendedStoreAndPop - Extended store and pop (followed by descriptor byte)

## Send Instructions

### Single Extended Send (131)
- `131`: SingleExtendedSend - Send with descriptor byte
  - Descriptor format: `aaaaalll` where `aaaa` = arg count, `lll` = literal index

### Double Extended Send (132)
- `132`: DoubleExtendedSend - Send with two bytes (arg count, literal index)

### Arithmetic Selectors (176-191)
- `176`: Send #+ (1 arg)
- `177`: Send #- (1 arg)
- `178`: Send #< (1 arg)
- `179`: Send #> (1 arg)
- `180`: Send #<= (1 arg)
- `181`: Send #>= (1 arg)
- `182`: Send #= (1 arg)
- `183`: Send #~= (1 arg)
- `184`: Send #* (1 arg)
- `185`: Send #/ (1 arg)
- `186`: Send #\\ (1 arg)
- `187`: Send #@ (1 arg)
- `188`: Send #bitShift: (1 arg)
- `189`: Send #// (1 arg)
- `190`: Send #bitAnd: (1 arg)
- `191`: Send #bitOr: (1 arg)

### Special Selectors (192-207)
- `192`: Send #at: (1 arg)
- `193`: Send #at:put: (2 args)
- `194`: Send #size (0 args)
- `195`: Send #next (0 args)
- `196`: Send #nextPut: (1 arg)
- `197`: Send #atEnd (0 args)
- `198`: Send #== (1 arg)
- `199`: Send #class (0 args)
- `200`: Send #blockCopy: (1 arg)
- `201`: Send #value (0 args)
- `202`: Send #value: (1 arg)
- `203`: Send #do: (1 arg)
- `204`: Send #new (0 args)
- `205`: Send #new: (1 arg)
- `206`: Send #x (0 args)
- `207`: Send #y (0 args)

### Send Literal Selector (208-255)
- `208-223`: SendLiteral0Args0 to SendLiteral0Args15 - Send literal selector 0-15 with 0 args
- `224-239`: SendLiteral1Args0 to SendLiteral1Args15 - Send literal selector 0-15 with 1 arg
- `240-255`: SendLiteral2Args0 to SendLiteral2Args15 - Send literal selector 0-15 with 2 args

## Jump Instructions

### Short Jumps (144-151)
- `144-151`: ShortUnconditionalJump1 to ShortUnconditionalJump8 - Jump forward 1-8 bytes

### Conditional Jumps (152-159)
- `152-159`: PopJumpFalseShort1 to PopJumpFalseShort8 - Pop, jump forward 1-8 if false

### Long Jumps (160-167)
- `160-167`: LongJump0 to LongJump7 - Long jump with offset byte

### Pop Jump True Long (168-171)
- `168-171`: PopJumpTrueLong0 to PopJumpTrueLong3 - Pop, long jump if true

### Pop Jump False Long (172-175)
- `172-175`: PopJumpFalseLong0 to PopJumpFalseLong3 - Pop, long jump if false

## Stack Operations
- `135`: PopStack - Pop and discard top of stack
- `136`: DuplicateTop - Duplicate top of stack
- `137`: PushActiveContext - Push the current context

## Example Bytecode Sequences

### Simple Accessor: `name ^name`
```
0   # PushReceiverVariable0 - push instance variable 'name'
124 # ReturnTopFromMethod - return it
```

### Simple Setter: `setName: aString  name := aString`
```
16  # PushTemporaryVariable0 - push argument aString
96  # StoreAndPopReceiverVariable0 - store into instance variable 'name'
120 # ReturnSelf - return self
```

### Method with Literal: `greet ^'Hello, world!'`
```
32  # PushLiteralConstant0 - push literal 'Hello, world!'
124 # ReturnTopFromMethod - return it
```

### Method with Send: `double ^value * 2`
```
0   # PushReceiverVariable0 - push instance variable 'value'
119 # PushTwo - push constant 2
184 # Send #* - multiply
124 # ReturnTopFromMethod - return result
```