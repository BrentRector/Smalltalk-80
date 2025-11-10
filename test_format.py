format_val = 8600
print(f'Format 8600 = 0x{format_val:04X}')
instanceSpec = (format_val >> 12) & 0x03
instVars = (format_val >> 6) & 0x3F
isIndexable = (format_val & 0x10) != 0
hasPointers = (format_val & 0x08) != 0
print(f'instanceSpec=0x{instanceSpec:02X}, instVars={instVars}, isIndexable={isIndexable}, hasPointers={hasPointers}')

if instanceSpec == 0x02 and hasPointers and isIndexable:
    print("Should allocate as: ObjectFormat.PointerIndexed")
elif instanceSpec == 0x02 and hasPointers and not isIndexable:
    print("Should allocate as: ObjectFormat.Pointer")
