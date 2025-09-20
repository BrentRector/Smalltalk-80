import subprocess
import json

# Read the test cases
with open('compiler_tests.json', 'r') as f:
    data = json.load(f)

print("Testing bytecode execution with fixed interpreter:")
print("=" * 60)

# Test each expression
for i, expr in enumerate(data.get('initializationExpressions', [])[:5], 1):  # Test first 5 for now
    source = expr.get("source", "")
    bytecode = expr.get("bytecode", [])
    literals = expr.get("literals", [])
    
    print(f"\nTest {i}: {source}")
    print(f"  Bytecode: {bytecode}")
    
    # Determine expected behavior
    if bytecode and len(bytecode) > 0:
        first_byte = bytecode[0]
        
        if 32 <= first_byte <= 63:
            opcode_type = f"Push Literal Constant #{first_byte - 32}"
            if "nil" in str(literals[0]):
                expected = "Should push nil"
            else:
                expected = f"Should push {literals[0]}"
        elif 64 <= first_byte <= 95:
            opcode_type = f"Push Literal Variable #{first_byte - 64}"
            expected = f"Should push VALUE from Association: {literals[0]}"
        else:
            opcode_type = f"Bytecode {first_byte}"
            expected = "Special bytecode"
        
        print(f"  First opcode: {first_byte} = {opcode_type}")
        print(f"  Expected: {expected}")

print("\n" + "=" * 60)
print("Summary:")
print("- Tests 1-3: Push Literal Constant (32-63) for nil, symbol, string")
print("- Tests 4-6: Push Literal Variable (64-95) for Associations")
print("  These should push the VALUE (e.g., Object class), not the Association")