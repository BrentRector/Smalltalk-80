#!/usr/bin/env python3
import json

def load_json(filename):
    with open(filename, 'r', encoding='utf-8-sig') as f:
        return json.load(f)

def decode_bytecode(bytecode):
    """Decode Smalltalk-80 bytecode into human-readable format"""
    result = []
    i = 0
    while i < len(bytecode):
        byte = bytecode[i]
        
        # Push receiver/arguments/temps
        if 0 <= byte <= 15:
            result.append(f"push receiver variable #{byte}")
        elif 16 <= byte <= 31:
            result.append(f"push temporary location #{byte - 16}")
        elif 32 <= byte <= 63:
            result.append(f"push literal constant #{byte - 32}")
        elif 64 <= byte <= 95:
            result.append(f"push literal variable #{byte - 64}")
        elif byte == 112:
            result.append("push receiver (self)")
        elif byte == 113:
            result.append("push true")
        elif byte == 114:
            result.append("push false")
        elif byte == 115:
            result.append("push nil")
        elif byte == 116:
            result.append("push -1")
        elif byte == 117:
            result.append("push 0")
        elif byte == 118:
            result.append("push 1")
        elif byte == 119:
            result.append("push 2")
        elif byte == 120:
            result.append("return receiver (self)")
        elif byte == 121:
            result.append("return true")
        elif byte == 122:
            result.append("return false")
        elif byte == 123:
            result.append("return nil")
        elif byte == 124:
            result.append("return top of stack")
        elif byte == 125:
            result.append("return top of stack from block")
        elif 176 <= byte <= 191:
            result.append(f"send arithmetic message #{byte - 176}")
        elif 192 <= byte <= 207:
            result.append(f"send special message #{byte - 192}")
        elif 208 <= byte <= 223:
            result.append(f"send literal selector #{byte - 208} with 0 args")
        elif 224 <= byte <= 239:
            result.append(f"send literal selector #{byte - 224} with 1 arg")
        elif 240 <= byte <= 255:
            result.append(f"send literal selector #{byte - 240} with 2 args")
        elif byte == 131:
            # Double-byte extension
            i += 1
            if i < len(bytecode):
                ext = bytecode[i]
                result.append(f"extended send (131 {ext})")
        elif byte == 132:
            # Double-byte extension
            i += 1
            if i < len(bytecode):
                ext = bytecode[i]
                result.append(f"extended jump/special (132 {ext})")
        elif byte == 248:
            # Pop and push temp
            i += 1
            if i < len(bytecode):
                ext = bytecode[i]
                result.append(f"pop and push temp (248 {ext})")
        else:
            result.append(f"bytecode {byte}")
        
        i += 1
    
    return result

def compare_method(standalone_file, new_file, class_name, selector):
    standalone = load_json(standalone_file)
    new_comp = load_json(new_file)
    
    print(f"=== Comparing {class_name}>>{selector} ===\n")
    
    # Find method in each
    standalone_method = None
    new_method = None
    
    for m in standalone['methods']:
        if m['class'] == class_name and m['selector'] == selector:
            standalone_method = m
            break
    
    for m in new_comp['methods']:
        if m['class'] == class_name and m['selector'] == selector:
            new_method = m
            break
    
    if standalone_method and new_method:
        print("Standalone bytecode:")
        print(f"  Raw: {standalone_method['bytecode']}")
        print("  Decoded:")
        for instruction in decode_bytecode(standalone_method['bytecode']):
            print(f"    {instruction}")
        
        print("\nNew compiler bytecode:")
        print(f"  Raw: {new_method['bytecode']}")
        print("  Decoded:")
        for instruction in decode_bytecode(new_method['bytecode']):
            print(f"    {instruction}")
        
        print(f"\nLiterals (Standalone): {standalone_method.get('literals', [])}")
        print(f"Literals (New): {new_method.get('literals', [])}")

if __name__ == "__main__":
    # Compare Object>>class
    compare_method("standalone_clean_output.json", "new_clean_output.json", "Object", "class")
    print("\n" + "="*50 + "\n")
    # Compare Point class>>x:y:
    compare_method("standalone_clean_output.json", "new_clean_output.json", "Point class", "x:y:")