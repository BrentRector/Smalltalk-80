#!/usr/bin/env python3
"""
Script to analyze the remaining 851 unreferenced strings that aren't method sources.
"""

import json
import sys
from collections import defaultdict

def main():
    if len(sys.argv) != 2:
        print("Usage: python analyze_remaining_strings.py <path-to-coldstart.json>")
        sys.exit(1)
    
    filepath = sys.argv[1]
    
    with open(filepath, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    strings = data.get('strings', [])
    methods = data.get('methods', [])
    
    # Find all method sources
    method_sources = set()
    for method in methods:
        if 'source' in method and method['source']:
            method_sources.add(method['source'])
    
    # Find all referenced string indices
    referenced_strings = set()
    
    def track_literal_usage(literal):
        literal_type = literal.get('type')
        index = literal.get('index')
        
        if literal_type == 'string' and index is not None:
            referenced_strings.add(index)
        elif literal_type == 'array' and literal.get('elements') is not None:
            for element in literal.get('elements', []):
                track_literal_usage(element)
        elif literal_type == 'association':
            # Associations reference symbols, not strings
            pass
    
    for method in methods:
        for literal in method.get('literals', []):
            track_literal_usage(literal)
    
    # Find unreferenced non-source strings
    unreferenced_non_source = []
    for i, string_val in enumerate(strings):
        if i not in referenced_strings and string_val not in method_sources:
            unreferenced_non_source.append((i, string_val))
    
    print(f"Total unreferenced non-source strings: {len(unreferenced_non_source)}")
    
    # Categorize by type
    categories = defaultdict(list)
    
    for i, string_val in unreferenced_non_source:
        val_len = len(string_val)
        
        if val_len == 0:
            categories['empty'].append((i, string_val))
        elif val_len == 1:
            categories['single_char'].append((i, string_val))
        elif val_len <= 10:
            categories['short'].append((i, string_val))
        elif string_val.count('\r\n') > 2:  # Multi-line
            categories['multiline'].append((i, string_val))
        else:
            categories['other'].append((i, string_val))
    
    for category, items in categories.items():
        print(f"\n{category.upper()}: {len(items)} strings")
        for i, (idx, string_val) in enumerate(items[:10]):  # Show first 10
            if category == 'empty':
                print(f"  [{idx}]: (empty)")
            else:
                sample = string_val[:100] + "..." if len(string_val) > 100 else string_val
                print(f"  [{idx}]: {repr(sample)}")
        if len(items) > 10:
            print(f"  ... and {len(items) - 10} more")

if __name__ == "__main__":
    main()