fmt = 8600
instanceSpec = (fmt >> 12) & 0x03
isIndexable = (fmt & 0x10) != 0
hasPointers = (fmt & 0x08) != 0

print(f"Format: {fmt} = 0x{fmt:04x} = {bin(fmt)}")
print(f"instanceSpec (bits 12-13): {instanceSpec} = 0x{instanceSpec:02x}")
print(f"isIndexable (bit 4): {isIndexable}")
print(f"hasPointers (bit 3): {hasPointers}")
print()
if instanceSpec == 0x02 and hasPointers and isIndexable:
    print("Result: PointerIndexed")
print()
print(f"Instance variable count (bits 8-11): {(fmt >> 8) & 0x0F}")
print(f"Full high byte: {(fmt >> 8) & 0xFF}")
