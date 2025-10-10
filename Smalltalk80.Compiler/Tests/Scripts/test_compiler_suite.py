#!/usr/bin/env python3
"""
Main test suite runner for compiler validation.
Runs all validation checks on compiler output JSON files.
"""

import json
import sys
import os
import subprocess
from pathlib import Path
from typing import Dict, Any, List, Tuple

# Import validation modules
script_dir = Path(__file__).parent
sys.path.insert(0, str(script_dir))

def run_validator(script_name: str, json_files: List[str]) -> Tuple[bool, str]:
    """Run a validation script and capture output."""
    script_path = script_dir / script_name
    if not script_path.exists():
        return False, f"Script not found: {script_path}"
    
    cmd = [sys.executable, str(script_path)] + json_files
    
    try:
        result = subprocess.run(cmd, capture_output=True, text=True)
        success = result.returncode == 0
        output = result.stdout + result.stderr
        return success, output
    except Exception as e:
        return False, f"Failed to run {script_name}: {e}"

def check_expected_methods(json_file: str) -> List[str]:
    """Check that expected test methods are present."""
    issues = []
    
    with open(json_file, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    filename = Path(json_file).name
    methods = data.get('methods', [])
    
    # Define expected patterns for each test file
    expected_patterns = {
        'test_SimpleClass.json': {
            'selectors': ['value', 'value:', 'increment', 'double', 'addTo:', 
                         'isPositive', 'isZero', 'withValue:'],
            'min_methods': 8
        },
        'test_ControlStructures.json': {
            'selectors': ['whileTrueExample', 'whileFalseExample', 'toDoExample', 
                         'toByDoExample', 'timesRepeatExample', 'andExample:with:', 
                         'orExample:with:', 'notExample:', 'earlyReturnExample:'],
            'min_methods': 20  # Including blocks
        },
        'test_ComplexExpressions.json': {
            'selectors': ['cascadeExample', 'complexArithmetic', 'chainedOperations',
                         'nestedParentheses', 'nestedConditionals:', 'guardClauses:',
                         'blockComposition', 'recursiveBlock', 'blockWithMultipleParams'],
            'min_methods': 20  # Including blocks
        }
    }
    
    if filename in expected_patterns:
        expected = expected_patterns[filename]
        
        # Check minimum method count
        if len(methods) < expected['min_methods']:
            issues.append(f"Expected at least {expected['min_methods']} methods, found {len(methods)}")
        
        # Check for expected selectors (using selectorIndex to symbols array)
        symbols = data.get('symbols', [])
        found_selectors = set()
        for m in methods:
            sel_idx = m.get('selectorIndex')
            if sel_idx is not None and 0 <= sel_idx < len(symbols):
                found_selectors.add(symbols[sel_idx])
        
        for selector in expected['selectors']:
            if selector not in found_selectors:
                issues.append(f"Expected selector '{selector}' not found")
    
    return issues

def analyze_bytecode_patterns(json_file: str) -> Dict[str, Any]:
    """Analyze bytecode patterns in the generated JSON."""
    with open(json_file, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    stats = {
        'total_methods': 0,
        'total_blocks': 0,
        'total_bytecodes': 0,
        'primitive_methods': 0,
        'empty_methods': 0,
        'max_stack_depth': 0,
        'bytecode_distribution': {}
    }
    
    for method in data.get('methods', []):
        # Methods have selectorIndex, blocks don't
        if method.get('selectorIndex') is not None:
            stats['total_methods'] += 1
        else:
            stats['total_blocks'] += 1
        
        header = method.get('header', {})
        if header.get('primitiveIndex', 0) > 0:
            stats['primitive_methods'] += 1
        
        bytecodes = method.get('bytecodes', [])
        if not bytecodes:
            stats['empty_methods'] += 1
        
        stats['total_bytecodes'] += len(bytecodes)
        
        # Count bytecode usage
        for bc in bytecodes:
            category = categorize_bytecode(bc)
            stats['bytecode_distribution'][category] = stats['bytecode_distribution'].get(category, 0) + 1
    
    return stats

def categorize_bytecode(bc: int) -> str:
    """Categorize a bytecode for statistics."""
    if 0x00 <= bc < 0x10: return 'push_rcvr_var'
    if 0x10 <= bc < 0x20: return 'push_temp'
    if 0x20 <= bc < 0x40: return 'push_literal'
    if 0x40 <= bc < 0x60: return 'push_lit_var'
    if 0x60 <= bc < 0x70: return 'store_pop'
    if 0x70 <= bc < 0x78: return 'push_special'
    if 0x78 <= bc < 0x80: return 'return'
    if bc == 0x80: return 'extended'
    if 0x90 <= bc < 0xB0: return 'jump'
    if 0xB0 <= bc < 0xC0: return 'arithmetic'
    if 0xC0 <= bc < 0xD0: return 'special_send'
    if 0xD0 <= bc < 0x100: return 'literal_send'
    return 'unknown'

def main():
    """Main entry point."""
    if len(sys.argv) < 2:
        print("Usage: python test_compiler_suite.py <json_file> [json_file2 ...]")
        print("\nThis script runs comprehensive validation on compiler output:")
        print("  1. Schema v1.0.3 compliance")
        print("  2. Block compilation correctness")
        print("  3. Expected test methods presence")
        print("  4. Bytecode pattern analysis")
        sys.exit(1)
    
    json_files = sys.argv[1:]
    total_issues = 0
    
    print("="*70)
    print(" Smalltalk-80 Compiler Test Suite")
    print("="*70)
    
    # 1. Schema validation
    print("\n[1/4] Schema v1.0.3 Validation")
    print("-"*40)
    success, output = run_validator("validate_schema.py", json_files)
    if success:
        print("PASS: All files pass schema validation")
    else:
        print("FAIL: Schema validation failed")
        print(output)
        total_issues += 1
    
    # 2. Block compilation validation
    print("\n[2/4] Block Compilation Validation")
    print("-"*40)
    success, output = run_validator("validate_blocks.py", json_files)
    # Parse key results from output
    for line in output.split('\n'):
        if 'Correct blocks:' in line or 'Incorrect blocks:' in line or 'Total blocks:' in line:
            print(f"  {line.strip()}")
    if not success:
        total_issues += 1
    
    # 3. Expected methods check
    print("\n[3/4] Expected Test Methods")
    print("-"*40)
    for json_file in json_files:
        filename = Path(json_file).name
        print(f"\nChecking {filename}:")
        issues = check_expected_methods(json_file)
        if issues:
            for issue in issues:
                print(f"  FAIL: {issue}")
                total_issues += 1
        else:
            print(f"  PASS: All expected methods present")
    
    # 4. Bytecode analysis
    print("\n[4/4] Bytecode Statistics")
    print("-"*40)
    for json_file in json_files:
        filename = Path(json_file).name
        print(f"\n{filename}:")
        stats = analyze_bytecode_patterns(json_file)
        print(f"  Methods: {stats['total_methods']}, Blocks: {stats['total_blocks']}")
        print(f"  Total bytecodes: {stats['total_bytecodes']}")
        print(f"  Primitive methods: {stats['primitive_methods']}")
        
        if stats['bytecode_distribution']:
            print("  Bytecode usage:")
            for category, count in sorted(stats['bytecode_distribution'].items()):
                percentage = (count / stats['total_bytecodes'] * 100) if stats['total_bytecodes'] > 0 else 0
                print(f"    {category:15s}: {count:4d} ({percentage:5.1f}%)")
    
    # Final summary
    print("\n" + "="*70)
    print(" Test Results Summary")
    print("="*70)
    
    if total_issues == 0:
        print("\nSUCCESS: All validation checks passed!")
        print(f"   Tested {len(json_files)} file(s)")
        sys.exit(0)
    else:
        print(f"\nFAILED: {total_issues} issue(s) found")
        print(f"   Tested {len(json_files)} file(s)")
        sys.exit(1)

if __name__ == "__main__":
    main()