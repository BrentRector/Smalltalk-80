#!/usr/bin/env python3
"""Simple bytecode decoder for Smalltalk-80"""

# The bytecode sequence from the JSON
bytecodes = [
    16, 206, 97, 16, 207, 98, 18, 99, 65, 1, 35, 176, 36, 189, 2, 184,
    205, 96, 118, 17, 194, 137, 118, 200, 161, 0, 6, 0, 16, 17, 16, 192,
    193, 124, 240, 128, 18, 113, 124
]

i = 0
while i < len(bytecodes):
    b = bytecodes[i]
    print(f"{i:3d}: 0x{b:02X} ({b:3d}) - ", end="")

    # Decode bytecode
    if b < 16:  # 0-15: Push receiver var
        print(f"Push Receiver Var {b}")
        i += 1
    elif b < 32:  # 16-31: Push temp/arg
        print(f"Push Temp/Arg {b - 16}")
        i += 1
    elif b < 64:  # 32-63: Push literal constant
        print(f"Push Literal Constant {b - 32}")
        i += 1
    elif b < 96:  # 64-95: Push literal variable
        print(f"Push Literal Variable {b - 64}")
        i += 1
    elif b < 104:  # 96-103: Pop and store receiver var
        print(f"Pop and Store Receiver Var {b - 96}")
        i += 1
    elif b < 112:  # 104-111: Pop and store temp
        print(f"Pop and Store Temp/Arg {b - 104}")
        i += 1
    elif b < 120:  # 112-119: Push special
        specials = ["self", "true", "false", "nil", "-1", "0", "1", "2"]
        print(f"Push {specials[b - 112]}")
        i += 1
    elif b < 124:  # 120-123: Return
        returns = ["Return self from method", "Return top from method",
                   "Return self from block", "Return top from block"]
        print(returns[b - 120])
        i += 1
    elif b == 124:  # Duplicate top
        print("Pop stack")
        i += 1
    elif b == 125:  # Pop stack
        print("Duplicate top of stack")
        i += 1
    elif b == 128:  # Send message (extended)
        lit_idx = bytecodes[i+1]
        arg_count = bytecodes[i+2]
        print(f"Send Long: literal {lit_idx}, {arg_count} args")
        i += 3
    elif b == 137:  # 0x89
        print("Push Active Context")
        i += 1
    elif b == 161:  # 0xA1: Long jump
        offset = (bytecodes[i+1] << 8) | bytecodes[i+2]
        print(f"Long Jump Forward +{offset} (jump to bytecode {i+3+offset})")
        i += 3
    elif 176 <= b <= 191:  # Special sends
        ops = ["+", "-", "<", ">", "<=", ">=", "=", "~=", "*", "/", "\\\\",
               "@", "bitShift:", "//", "bitAnd:", "bitOr:"]
        print(f"Send special {ops[b - 176]}")
        i += 1
    elif 192 <= b <= 207:  # Send literal selector
        if b < 208:
            args = (b - 192) // 16
            lit = (b - 192) % 16
            print(f"Send Literal Selector {lit} with {args} arg(s)")
        i += 1
    elif 208 <= b <= 223:  # Send literal selector with 2 args
        print(f"Send Literal Selector {b - 208} with 2 args")
        i += 1
    elif b == 200:  # 0xC8
        print("Send Special blockCopy:")
        i += 1
    elif b == 240:  # 0xF0
        print("Block Return")
        i += 1
    else:
        print(f"Unknown bytecode")
        i += 1
