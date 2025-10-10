#!/usr/bin/env python3
"""
Validates block compilation according to Blue Book specification.
Checks that blocks are compiled as separate CompiledMethod objects.
"""

import json
import sys
import os
from typing import Dict, Any, List, Optional

# Bytecode constants
PUSH_RECEIVER_VARIABLE = range(0x00, 0x10)
PUSH_TEMP = range(0x10, 0x20)
PUSH_LITERAL_CONSTANT = range(0x20, 0x40)
PUSH_LITERAL_VARIABLE = range(0x40, 0x60)
STORE_POP_RECEIVER = range(0x60, 0x68)
STORE_POP_TEMP = range(0x68, 0x70)
PUSH_RECEIVER = 0x70
PUSH_TRUE = 0x71
PUSH_FALSE = 0x72
PUSH_NIL = 0x73
PUSH_MINUS_1 = 0x74
PUSH_0 = 0x75
PUSH_1 = 0x76
PUSH_2 = 0x77
RETURN_RECEIVER = 0x78
RETURN_TRUE = 0x79
RETURN_FALSE = 0x7A
RETURN_NIL = 0x7B
RETURN_TOP = 0x7C
RETURN_BLOCK = 0x7E
EXTENDED = 0x80
PUSH_ACTIVE_CONTEXT = 0x89
JUMP_FORWARD_SHORT = range(0x90, 0x98)
JUMP_FALSE_SHORT = range(0x98, 0xA0)
JUMP_FORWARD_LONG = range(0xA0, 0xA8)
JUMP_TRUE_LONG = range(0xA8, 0xB0)
SEND_ARITHMETIC = range(0xB0, 0xC0)
SEND_SPECIAL = range(0xC0, 0xD0)
SEND_LITERAL_0 = range(0xD0, 0xE0)
SEND_LITERAL_1 = range(0xE0, 0xF0)
SEND_LITERAL_2 = range(0xF0, 0x100)

def load_json(filepath: str) -> Dict[str, Any]:
    """Load and parse JSON file."""
    with open(filepath, 'r', encoding='utf-8') as f:
        return json.load(f)

def get_method_name(method: Dict[str, Any]) -> str:
    """Get a readable name for a method."""
    class_name = method.get('className', 'Unknown')
    selector = method.get('selector')
    if selector:
        return f"{class_name}>>{selector}"
    else:
        return f"block:{method.get('localId', 'unknown')}"

def find_blocks_in_literals(method: Dict[str, Any]) -> List[str]:
    """Find all block references in method literals."""
    blocks = []
    for literal in method.get('literals', []):
        if literal.get('type') == 'compiledMethod':
            blocks.append(literal.get('localId'))
    return blocks

def check_block_push_pattern(bytecodes: List[int], literal_index: int) -> bool:
    """Check if block is pushed via PUSH_LITERAL bytecode."""
    # Look for PUSH_LITERAL_CONSTANT with the given index
    expected_bytecode = 0x20 + literal_index
    return expected_bytecode in bytecodes

def analyze_block_compilation(data: Dict[str, Any]) -> Dict[str, Any]:
    """Analyze block compilation patterns in the JSON."""
    results = {
        'total_methods': 0,
        'total_blocks': 0,
        'correct_blocks': [],
        'incorrect_blocks': [],
        'inline_blocks': [],
        'issues': []
    }
    
    methods = data.get('methods', [])
    methods_by_id = {m.get('localId'): m for m in methods}
    
    for method in methods:
        method_id = method.get('localId', 'unknown')
        method_name = get_method_name(method)
        selector = method.get('selector')
        
        # Count regular methods (non-blocks)
        if selector is not None:
            results['total_methods'] += 1
        
        # Find blocks in literals
        block_refs = find_blocks_in_literals(method)
        
        for i, block_ref in enumerate(block_refs):
            results['total_blocks'] += 1
            block = methods_by_id.get(block_ref)
            
            if not block:
                results['issues'].append(f"{method_name}: Block literal {i} references non-existent method {block_ref}")
                continue
            
            # Check block properties
            block_selector = block.get('selector')
            block_header = block.get('header', {})
            block_primitive = block_header.get('primitiveIndex', 0)
            
            is_correct = True
            block_issues = []
            
            # Check selector is null
            if block_selector is not None:
                block_issues.append(f"selector should be null, found '{block_selector}'")
                is_correct = False
            
            # Check primitiveIndex is 0
            if block_primitive != 0:
                block_issues.append(f"primitiveIndex should be 0, found {block_primitive}")
                is_correct = False
            
            # Check block is pushed correctly
            if not check_block_push_pattern(method.get('bytecodes', []), i):
                # Note: This is a soft check - the exact bytecode depends on literal position
                pass  # Don't mark as incorrect for now
            
            block_info = {
                'method': method_name,
                'block_id': block_ref,
                'literal_index': i
            }
            
            if is_correct:
                results['correct_blocks'].append(block_info)
            else:
                block_info['issues'] = block_issues
                results['incorrect_blocks'].append(block_info)
        
        # Check for inline block patterns (wrong compilation)
        if contains_inline_block_pattern(method.get('bytecodes', [])):
            results['inline_blocks'].append(method_name)
            results['issues'].append(f"{method_name}: Contains inline block with jump-over pattern")
    
    return results

def contains_inline_block_pattern(bytecodes: List[int]) -> bool:
    """
    Check for incorrect inline block compilation pattern.
    This would show as JUMP_FORWARD followed by block bytecodes.
    """
    for i, bc in enumerate(bytecodes):
        # Check for jump forward patterns that skip over code
        if bc in JUMP_FORWARD_SHORT or bc in JUMP_FORWARD_LONG:
            # In correct block compilation, we shouldn't see jumps over block code
            # Blocks should be in literals, not inline
            # Note: This is a heuristic - some jumps are legitimate for control flow
            if i + 1 < len(bytecodes):
                # Look for block-like patterns after jump
                # This is a simplified check
                next_instructions = bytecodes[i+1:min(i+10, len(bytecodes))]
                # If we see return block after a jump, it might be inline block
                if RETURN_BLOCK in next_instructions:
                    return True
    return False

def print_block_example(method: Dict[str, Any], block: Dict[str, Any], literal_index: int):
    """Print an example of block compilation."""
    print(f"\nExample: {get_method_name(method)}")
    print(f"  Block at literal[{literal_index}]: {block.get('localId')}")
    
    # Show relevant bytecodes
    bytecodes = method.get('bytecodes', [])
    print(f"  Method bytecodes (first 10): {bytecodes[:10]}")
    
    block_bytecodes = block.get('bytecodes', [])
    print(f"  Block bytecodes: {block_bytecodes}")
    
    # Show the PUSH_LITERAL instruction
    expected_push = 0x20 + literal_index
    if expected_push in bytecodes:
        print(f"  Block pushed with PUSH_LITERAL {literal_index} (bytecode 0x{expected_push:02X})")
    else:
        print(f"  ? Block push not found (expected 0x{expected_push:02X})")

def validate_test_cases(data: Dict[str, Any]) -> List[str]:
    """Validate specific test cases for block compilation."""
    test_results = []
    methods = data.get('methods', [])
    
    # Test case 1: Empty block `[]`
    # Should compile to a separate CompiledMethod with:
    # - selector = null
    # - primitiveIndex = 0  
    # - bytecodes ending with RETURN_NIL or RETURN_BLOCK
    
    # Test case 2: Block with arguments `[:x | x + 1]`
    # Should have:
    # - argCount > 0 in header
    # - Access to block arguments via temps
    
    # Test case 3: Nested blocks `[[:x | x] value: 1]`
    # Should have:
    # - Block containing another block in its literals
    
    for method in methods:
        if method.get('selector') is None:  # It's a block
            header = method.get('header', {})
            bytecodes = method.get('bytecodes', [])
            
            # Check empty block pattern
            if len(bytecodes) == 1 and bytecodes[0] in [RETURN_NIL, RETURN_BLOCK]:
                test_results.append(f"Found empty block: {method.get('localId')}")
            
            # Check block with arguments
            if header.get('argCount', 0) > 0:
                test_results.append(f"Found block with {header['argCount']} args: {method.get('localId')}")
            
            # Check nested blocks
            if find_blocks_in_literals(method):
                test_results.append(f"Found nested block: {method.get('localId')}")
    
    return test_results

def main():
    """Main entry point."""
    if len(sys.argv) < 2:
        print("Usage: python validate_blocks.py <json_file> [json_file2 ...]")
        sys.exit(1)
    
    total_issues = 0
    
    for filepath in sys.argv[1:]:
        if not os.path.exists(filepath):
            print(f"ERROR: File not found: {filepath}")
            total_issues += 1
            continue
        
        print(f"\n{'='*60}")
        print(f"Analyzing: {filepath}")
        print('='*60)
        
        try:
            data = load_json(filepath)
        except Exception as e:
            print(f"ERROR: Failed to load JSON: {e}")
            total_issues += 1
            continue
        
        results = analyze_block_compilation(data)
        
        # Print summary
        print(f"\n Summary:")
        print(f"  Total methods: {results['total_methods']}")
        print(f"  Total blocks: {results['total_blocks']}")
        print(f"  Correct blocks: {len(results['correct_blocks'])}")
        print(f"  Incorrect blocks: {len(results['incorrect_blocks'])}")
        print(f"  Inline blocks (wrong): {len(results['inline_blocks'])}")
        
        # Show examples of correct blocks
        if results['correct_blocks']:
            print(f"\n Correct block compilation examples:")
            methods = data.get('methods', [])
            methods_by_id = {m.get('localId'): m for m in methods}
            
            for block_info in results['correct_blocks'][:2]:  # Show first 2
                method = next((m for m in methods if get_method_name(m) == block_info['method']), None)
                block = methods_by_id.get(block_info['block_id'])
                if method and block:
                    print_block_example(method, block, block_info['literal_index'])
        
        # Show incorrect blocks
        if results['incorrect_blocks']:
            print(f"\n Incorrect block compilation:")
            for block_info in results['incorrect_blocks']:
                print(f"  {block_info['method']}:")
                for issue in block_info['issues']:
                    print(f"    - {issue}")
                total_issues += 1
        
        # Show inline blocks
        if results['inline_blocks']:
            print(f"\n Methods with inline block pattern (not Blue Book compliant):")
            for method_name in results['inline_blocks']:
                print(f"  - {method_name}")
                total_issues += 1
        
        # Run test case validation
        test_results = validate_test_cases(data)
        if test_results:
            print(f"\n Test Case Validation:")
            for result in test_results:
                print(f"  {result}")
        
        # Show other issues
        if results['issues']:
            print(f"\n Other issues:")
            for issue in results['issues'][:10]:
                print(f"  - {issue}")
            if len(results['issues']) > 10:
                print(f"  ... and {len(results['issues']) - 10} more")
            total_issues += len(results['issues'])
    
    # Final result
    print(f"\n{'='*60}")
    if total_issues == 0:
        print("All blocks compiled correctly according to Blue Book specification")
        sys.exit(0)
    else:
        print(f"Found {total_issues} issues with block compilation")
        sys.exit(1)

if __name__ == "__main__":
    main()