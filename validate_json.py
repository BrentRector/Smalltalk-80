#!/usr/bin/env python3
"""
Validate that a JSON file conforms to the bootstrap-schema.json structure.
This performs structural validation without requiring jsonschema library.
"""

import json
import sys
from typing import Any, Dict, List

def validate_structure(data: Dict[str, Any]) -> List[str]:
    """Validate the structure of the compiled JSON output."""
    errors = []
    
    # Check required top-level fields
    required_fields = ['classes', 'methods', 'initializationExpressions']
    for field in required_fields:
        if field not in data:
            errors.append(f"Missing required field: {field}")
    
    # Check optional but recommended fields
    if '$schema' not in data:
        errors.append("Missing recommended field: $schema")
    
    # Validate classes structure
    if 'classes' in data:
        if not isinstance(data['classes'], list):
            errors.append("Field 'classes' must be an array")
        else:
            for i, cls in enumerate(data['classes']):
                if not isinstance(cls, dict):
                    errors.append(f"Class at index {i} must be an object")
                    continue
                
                # Check required class fields
                if 'name' not in cls:
                    errors.append(f"Class at index {i} missing 'name'")
                if 'superclass' not in cls:
                    errors.append(f"Class at index {i} missing 'superclass'")
    
    # Validate methods structure
    if 'methods' in data:
        if not isinstance(data['methods'], list):
            errors.append("Field 'methods' must be an array")
        else:
            # Check first few methods as samples
            for i, method in enumerate(data['methods'][:5]):
                if not isinstance(method, dict):
                    errors.append(f"Method at index {i} must be an object")
                    continue
                
                # Check required method fields
                required_method_fields = ['class', 'selector', 'bytecode', 'literals']
                for field in required_method_fields:
                    if field not in method:
                        errors.append(f"Method at index {i} missing '{field}'")
                
                # Validate bytecode is array of integers
                if 'bytecode' in method:
                    if not isinstance(method['bytecode'], list):
                        errors.append(f"Method at index {i}: bytecode must be an array")
                    elif method['bytecode'] and not all(isinstance(b, int) and 0 <= b <= 255 
                                                        for b in method['bytecode']):
                        errors.append(f"Method at index {i}: bytecode must contain integers 0-255")
    
    # Validate initialization expressions
    if 'initializationExpressions' in data:
        if not isinstance(data['initializationExpressions'], list):
            errors.append("Field 'initializationExpressions' must be an array")
        else:
            for i, expr in enumerate(data['initializationExpressions'][:5]):
                if not isinstance(expr, dict):
                    errors.append(f"InitExpression at index {i} must be an object")
                    continue
                
                # Check required fields
                if 'bytecode' not in expr:
                    errors.append(f"InitExpression at index {i} missing 'bytecode'")
                if 'literals' not in expr:
                    errors.append(f"InitExpression at index {i} missing 'literals'")
    
    return errors

def main():
    if len(sys.argv) != 2:
        print("Usage: python validate_json.py <json_file>")
        sys.exit(1)
    
    json_file = sys.argv[1]
    
    try:
        with open(json_file, 'r', encoding='utf-8-sig') as f:
            data = json.load(f)
    except FileNotFoundError:
        print(f"Error: File '{json_file}' not found")
        sys.exit(1)
    except json.JSONDecodeError as e:
        print(f"Error: Invalid JSON in '{json_file}': {e}")
        sys.exit(1)
    
    # Validate structure
    errors = validate_structure(data)
    
    if errors:
        print(f"X Validation failed with {len(errors)} error(s):")
        for error in errors:
            print(f"  - {error}")
        sys.exit(1)
    else:
        print("OK: JSON structure is valid!")
        
        # Print statistics
        print(f"\nStatistics:")
        print(f"  Schema: {data.get('$schema', 'Not specified')}")
        print(f"  Classes: {len(data.get('classes', []))}")
        print(f"  Methods: {len(data.get('methods', []))}")
        print(f"  Initialization Expressions: {len(data.get('initializationExpressions', []))}")
        
        # Check for class methods
        if 'methods' in data:
            class_methods = sum(1 for m in data['methods'] if m.get('isClassMethod'))
            print(f"  Class Methods: {class_methods}")
            print(f"  Instance Methods: {len(data['methods']) - class_methods}")

if __name__ == '__main__':
    main()