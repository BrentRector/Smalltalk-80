# Smalltalk-80 Bytecode Reference

This document describes the bytecode values used in our Smalltalk-80 interpreter implementation, which follows the **canonical Blue Book (Goldberg & Robson, 1983) bytecode specification**.

## Push Instructions

### Instance Variables (0-15)
- `0`: PushReceiverVariable0 - Push instance variable 0 of receiver
- `1`: PushReceiverVariable1 - Push instance variable 1 of receiver
- ...
- `15`: PushReceiverVariable15 - Push instance variable 15 of receiver

### Temporaries/Arguments (16-31)
- `16`: PushTemporaryVariable0 - Push temporary/argument 0
- `17`: PushTemporaryVariable1 - Push temporary/argument 1
- ...
- `31`: PushTemporaryVariable15 - Push temporary/argument 15

### Literal Constants (32-63)
- `32`: PushLiteralConstant0 - Push literal constant at index 0
- `33`: PushLiteralConstant1 - Push literal constant at index 1
- ...
- `63`: PushLiteralConstant31 - Push literal constant at index 31

### Literal Variables (64-95)
- `64`: PushLiteralVariable0 - Push value from Association in literal 0
- ...
- `95`: PushLiteralVariable31 - Push value from Association in literal 31

## Store Instructions (DO NOT POP)

### Store Into Instance Variables (96-127)
- `96`: StoreIntoReceiverVariable0 - Store top of stack into instance variable 0 (no pop)
- `97`: StoreIntoReceiverVariable1 - Store top of stack into instance variable 1 (no pop)
- ...
- `127`: StoreIntoReceiverVariable31 - Store top of stack into instance variable 31 (no pop)

### Store Into Temporaries (128-159)
- `128`: StoreIntoTemporaryVariable0 - Store top of stack into temporary 0 (no pop)
- ...
- `159`: StoreIntoTemporaryVariable31 - Store top of stack into temporary 31 (no pop)

### Store Into Literal Variables (160-175)
- `160`: StoreIntoLiteralVariable0 - Store top of stack into literal variable 0 (no pop)
- ...
- `175`: StoreIntoLiteralVariable15 - Store top of stack into literal variable 15 (no pop)

## Return Instructions (176-180)
- `176`: ReturnReceiver - Return receiver (self)
- `177`: ReturnTrue - Return constant true
- `178`: ReturnFalse - Return constant false
- `179`: ReturnNil - Return constant nil
- `180`: ReturnTop - Return top of stack from method

## Unused (181-191)
- `181-191`: Reserved for future use

## Send Instructions (192-255)

### Send Message (192-223)
- `192`: SendMessage0 - Send message with selector at literal index 0
- `193`: SendMessage1 - Send message with selector at literal index 1
- ...
- `223`: SendMessage31 - Send message with selector at literal index 31

### Send Super Message (224-255)
- `224`: SendSuperMessage0 - Send super message with selector at literal index 0
- `225`: SendSuperMessage1 - Send super message with selector at literal index 1
- ...
- `255`: SendSuperMessage31 - Send super message with selector at literal index 31

## Important Notes

### Blue Book Canonical Specification
This implementation follows the exact bytecode specification from the Blue Book (Goldberg & Robson, 1983), which is the canonical Smalltalk-80 bytecode set.

### Key Differences from Modern Implementations
1. **Store Operations DO NOT Pop**: Unlike many modern implementations, Blue Book store operations (96-175) leave the value on the stack after storing.
2. **No Special Push Constants**: There are no dedicated bytecodes for pushing self, true, false, nil, -1, 0, 1, 2. These are handled through:
   - `self` is returned via bytecode 176 (ReturnReceiver)
   - `true`, `false`, `nil` are returned via bytecodes 177-179 or stored as literals
   - Small integers must be stored as literal constants
3. **Dense Encoding**: All 256 bytecode values are used (except 181-191 which are reserved).
4. **Send Operations Index Literals**: The send bytecodes (192-255) reference selectors stored in the method's literal frame.

## Example Bytecode Sequences

### Simple Accessor: `name ^name`
```
0   # PushReceiverVariable0 - push instance variable 'name'
180 # ReturnTop - return top of stack
```

### Simple Setter: `setName: aString  name := aString`
```
16  # PushTemporaryVariable0 - push argument aString
96  # StoreIntoReceiverVariable0 - store into instance variable 'name' (no pop)
176 # ReturnReceiver - return self
```

### Method with Literal: `greet ^'Hello, world!'`
```
32  # PushLiteralConstant0 - push literal 'Hello, world!'
180 # ReturnTop - return top of stack
```

### Method with Send: `double ^value * 2`
```
0   # PushReceiverVariable0 - push instance variable 'value'
33  # PushLiteralConstant1 - push literal 2
193 # SendMessage1 - send selector at literal index 1 (#*)
180 # ReturnTop - return result
```

### Method Using new: `Behavior>>new ^self basicNew`
For a primitive method like this, the header contains the primitive number (70 for basicNew).
If the primitive fails, the fallback bytecode would be:
```
176 # ReturnReceiver - return self (primitive should have already created the instance)
```