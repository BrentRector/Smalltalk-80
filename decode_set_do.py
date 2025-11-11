#!/usr/bin/env python3
"""Decode Set>>do: bytecode from coldstart.json"""

import json

# Load the coldstart bundle
with open('bin/Debug/net9.0/coldstart.json') as f:
    bundle = json.load(f)

# Find Set>>do: method
class_map = {c['localId']: c for c in bundle['classes']}

for method in bundle['methods']:
    class_obj = class_map[method['homeClassLocalId']]
    class_name = bundle['symbols'][class_obj['nameIndex']]
    selector = bundle['symbols'][method['selectorIndex']]

    if class_name == 'Set' and selector == 'do:':
        bytecodes = method['bytecodes']
        break
else:
    print("Method not found!")
    exit(1)

# Decode bytecodes
i = 0
while i < len(bytecodes):
    b = bytecodes[i]
    print(f"{i:3d}: 0x{b:02X} ({b:3d}) - ", end="")

    # Decode bytecode
    if b < 16:  # 0-15: Push receiver var
        print(f"pushReceiver {b}")
        i += 1
    elif b < 32:  # 16-31: Push temp/arg
        print(f"pushTemporary {b - 16}")
        i += 1
    elif b < 64:  # 32-63: Push literal constant
        print(f"pushLiteral {b - 32}")
        i += 1
    elif b < 96:  # 64-95: Push literal variable
        print(f"pushLiteralVariable {b - 64}")
        i += 1
    elif b < 104:  # 96-103: Pop and store receiver var
        print(f"storeReceiver {b - 96}")
        i += 1
    elif b < 112:  # 104-111: Pop and store temp
        print(f"storeTemporary {b - 104}")
        i += 1
    elif b < 120:  # 112-119: Push special
        specials = ["self", "true", "false", "nil", "-1", "0", "1", "2"]
        print(f"push {specials[b - 112]}")
        i += 1
    elif b < 124:  # 120-123: Return
        returns = ["returnSelf", "returnTop", "blockReturnSelf", "blockReturnTop"]
        print(returns[b - 120])
        i += 1
    elif b == 124:  # Pop stack
        print("popStack")
        i += 1
    elif b == 125:  # Duplicate top
        print("duplicate")
        i += 1
    elif b == 128:  # Send message (extended)
        lit_idx = bytecodes[i+1]
        arg_count = bytecodes[i+2]
        print(f"send literal {lit_idx} with {arg_count} args")
        i += 3
    elif b == 137:  # 0x89
        print("pushActiveContext")
        i += 1
    elif b == 161:  # 0xA1: Long jump
        offset = (bytecodes[i+1] << 8) | bytecodes[i+2]
        target = i + 3 + offset
        print(f"longJumpForward +{offset} (to offset {target})")
        i += 3
    elif 176 <= b <= 191:  # Special sends
        ops = ["+", "-", "<", ">", "<=", ">=", "=", "~=", "*", "/", "\\\\",
               "@", "bitShift:", "//", "bitAnd:", "bitOr:"]
        print(f"send special '{ops[b - 176]}'")
        i += 1
    elif 192 <= b <= 223:  # Send literal selector
        args = (b - 192) // 16
        lit = (b - 192) % 16
        print(f"send literal selector {lit} with {args} args")
        i += 1
    elif b == 200:  # 0xC8
        print("blockCopy:")
        i += 1
    elif b == 224:  # 0xE0
        print("send special 'ifTrue:'")
        i += 1
    elif b == 225:  # 0xE1
        print("send special 'ifFalse:'")
        i += 1
    elif b == 226:  # 0xE2
        print("send special 'whileTrue:'")
        i += 1
    elif b == 227:  # 0xE3
        print("send special '=='")
        i += 1
    elif b == 202:  # 0xCA
        print("send special 'value:'")
        i += 1
    elif b == 240:  # 0xF0
        print("blockReturn")
        i += 1
    elif b == 241:  # 0xF1
        print("stackReturn")
        i += 1
    else:
        print(f"UNKNOWN bytecode")
        i += 1

print("\nLiterals:")
for i, lit in enumerate(method['literals']):
    print(f"  [{i}]: {lit}")
