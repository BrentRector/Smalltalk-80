import json

with open('compiler_tests.json', 'r') as f:
    data = json.load(f)
    
print("Compiler Test Results:")
print("=" * 60)

for i, expr in enumerate(data.get('initializationExpressions', []), 1):
    print(f'Test {i}: {expr.get("source", "")}')
    bytecode = expr.get("bytecode", [])
    literals = expr.get("literals", [])
    
    print(f'  Bytecode: {bytecode}')
    print(f'  Literals: {literals}')
    
    # Analyze the bytecode
    if len(bytecode) > 0:
        first_byte = bytecode[0]
        if 32 <= first_byte <= 63:
            print(f'  First opcode: {first_byte} (0x{first_byte:02X}) = Push Literal Constant #{first_byte - 32}')
        elif 64 <= first_byte <= 95:
            print(f'  First opcode: {first_byte} (0x{first_byte:02X}) = Push Literal Variable #{first_byte - 64}')
        elif first_byte == 115:
            print(f'  First opcode: {first_byte} (0x{first_byte:02X}) = Push nil')
        elif first_byte == 116:
            print(f'  First opcode: {first_byte} (0x{first_byte:02X}) = Push true')
        elif first_byte == 117:
            print(f'  First opcode: {first_byte} (0x{first_byte:02X}) = Push false')
        elif first_byte == 118:
            print(f'  First opcode: {first_byte} (0x{first_byte:02X}) = Push 1')
        elif first_byte == 119:
            print(f'  First opcode: {first_byte} (0x{first_byte:02X}) = Push 2')
        else:
            print(f'  First opcode: {first_byte} (0x{first_byte:02X})')
    
    # Check if it matches expected
    if i == 1:  # nil
        expected = "Should use Push nil (115) or Push Literal Constant if nil is in literals"
    elif i in [2, 3]:  # symbol, string
        expected = "Should use Push Literal Constant (32-63)"
    elif i in [4, 5, 6]:  # Object, Object initialize, Array new
        expected = "Should use Push Literal Variable (64-95) for Association literals"
    elif i == 7:  # 1 + 2
        expected = "Should use special push for small integers or literal constants"
    else:
        expected = "Complex bytecode for blocks"
    
    print(f'  Expected: {expected}')
    print()