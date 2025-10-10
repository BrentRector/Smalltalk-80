#!/usr/bin/env python3
"""
Validates compiler-generated JSON against the coldstart v1.0.3 schema.
"""

import json
import sys
import os
from pathlib import Path
from typing import Dict, Any, List, Tuple

def load_json(filepath: str) -> Dict[str, Any]:
    """Load and parse JSON file."""
    with open(filepath, 'r', encoding='utf-8') as f:
        return json.load(f)

def validate_metadata(data: Dict[str, Any]) -> List[str]:
    """Validate metadata section."""
    errors = []
    metadata = data.get('metadata', {})
    
    required_fields = ['version', 'generator', 'generatedAt', 'sourceFile', 
                       'symbolCount', 'stringCount', 'classCount', 'methodCount']
    
    for field in required_fields:
        if field not in metadata:
            errors.append(f"Missing required metadata field: {field}")
    
    if metadata.get('version') != '1.0.3':
        errors.append(f"Invalid schema version: {metadata.get('version')} (expected 1.0.3)")
    
    return errors

def validate_method_header(method: Dict[str, Any], method_id: str) -> List[str]:
    """Validate method header fields."""
    errors = []
    header = method.get('header', {})
    
    required_header_fields = ['word', 'literalCount', 'tempCount', 
                              'argCount', 'primitiveIndex', 'largeContext']
    
    for field in required_header_fields:
        if field not in header:
            errors.append(f"Method {method_id}: Missing header field '{field}'")
    
    # Validate literal count matches
    literal_count = header.get('literalCount', 0)
    actual_literals = len(method.get('literals', []))
    if literal_count != actual_literals:
        errors.append(f"Method {method_id}: header.literalCount ({literal_count}) != actual literals ({actual_literals})")
    
    return errors

def validate_blocks(data: Dict[str, Any]) -> List[str]:
    """Validate block compilation according to Blue Book specification."""
    errors = []
    methods = data.get('methods', [])
    
    for method in methods:
        method_id = method.get('localId', 'unknown')
        
        # Check blocks in literals
        for i, literal in enumerate(method.get('literals', [])):
            if literal.get('type') == 'compiledMethod':
                block_ref = literal.get('localId')
                # Find the block method
                block_method = next((m for m in methods if m.get('localId') == block_ref), None)
                
                if not block_method:
                    errors.append(f"Method {method_id}: Block literal {i} references non-existent method {block_ref}")
                    continue
                
                # Validate block has null selector
                if block_method.get('selector') is not None:
                    errors.append(f"Block {block_ref}: Must have selector=null, found '{block_method.get('selector')}'")
                
                # Validate block has primitiveIndex=0
                header = block_method.get('header', {})
                if header.get('primitiveIndex', 0) != 0:
                    errors.append(f"Block {block_ref}: Must have primitiveIndex=0, found {header.get('primitiveIndex')}")
                
                # Check for inline block bytecodes (wrong pattern)
                bytecodes = method.get('bytecodes', [])
                if contains_jump_over_block(bytecodes):
                    errors.append(f"Method {method_id}: Contains inline block with jump-over pattern (not Blue Book compliant)")
    
    return errors

def contains_jump_over_block(bytecodes: List[int]) -> bool:
    """Check if bytecodes contain the wrong jump-over-block pattern."""
    # Look for JUMP_FORWARD (0x90-0x97) followed by block bytecodes
    for i, bc in enumerate(bytecodes):
        if 0x90 <= bc <= 0x97:  # Short jump forward
            # This could indicate jump-over-block pattern
            # In correct compilation, blocks are in literals, not inline
            return True
        if bc == 0x80 and i + 1 < len(bytecodes):
            next_bc = bytecodes[i + 1]
            if next_bc in [0x0A, 0x0B, 0x0C]:  # Extended jumps
                return True
    return False

def validate_bytecodes(method: Dict[str, Any]) -> List[str]:
    """Validate bytecode array."""
    errors = []
    method_id = method.get('localId', 'unknown')
    bytecodes = method.get('bytecodes', [])
    
    if not isinstance(bytecodes, list):
        errors.append(f"Method {method_id}: bytecodes must be an array")
        return errors
    
    for i, bc in enumerate(bytecodes):
        if not isinstance(bc, int):
            errors.append(f"Method {method_id}: bytecode[{i}] must be integer, found {type(bc).__name__}")
        elif not (0 <= bc <= 255):
            errors.append(f"Method {method_id}: bytecode[{i}] = {bc} out of range [0-255]")
    
    return errors

def validate_json_file(filepath: str) -> Tuple[bool, List[str]]:
    """Validate a single JSON file against schema v1.0.3."""
    errors = []
    
    try:
        data = load_json(filepath)
    except Exception as e:
        return False, [f"Failed to load JSON: {e}"]
    
    # Validate structure
    if not isinstance(data, dict):
        errors.append("Root must be an object")
        return False, errors
    
    # Validate metadata
    errors.extend(validate_metadata(data))
    
    # Validate methods
    methods = data.get('methods', [])
    if not isinstance(methods, list):
        errors.append("'methods' must be an array")
    else:
        for method in methods:
            errors.extend(validate_method_header(method, method.get('localId', 'unknown')))
            errors.extend(validate_bytecodes(method))
    
    # Validate block compilation
    errors.extend(validate_blocks(data))
    
    # Check symbols and strings are arrays
    if not isinstance(data.get('symbols', []), list):
        errors.append("'symbols' must be an array")
    if not isinstance(data.get('strings', []), list):
        errors.append("'strings' must be an array")
    
    return len(errors) == 0, errors

def main():
    """Main entry point."""
    if len(sys.argv) < 2:
        print("Usage: python validate_schema.py <json_file> [json_file2 ...]")
        sys.exit(1)
    
    total_errors = 0
    
    for filepath in sys.argv[1:]:
        if not os.path.exists(filepath):
            print(f"ERROR: File not found: {filepath}")
            total_errors += 1
            continue
        
        print(f"\nValidating: {filepath}")
        print("-" * 60)
        
        valid, errors = validate_json_file(filepath)
        
        if valid:
            print("PASSED - Schema validation successful")
        else:
            print(f"FAILED - {len(errors)} errors found:")
            for error in errors[:10]:  # Show first 10 errors
                print(f"  - {error}")
            if len(errors) > 10:
                print(f"  ... and {len(errors) - 10} more errors")
            total_errors += len(errors)
    
    if total_errors == 0:
        print("\nSUCCESS: All files passed validation")
        sys.exit(0)
    else:
        print(f"\nFAILED: Total errors: {total_errors}")
        sys.exit(1)

if __name__ == "__main__":
    main()