#!/usr/bin/env python3
"""
Script to analyze coldstart.json for unreferenced literals.
This will identify string/array/float/character indices that are allocated but never referenced.
"""

import json
import sys
from collections import defaultdict, Counter

def load_coldstart_json(filepath):
    """Load and parse the coldstart.json file."""
    with open(filepath, 'r', encoding='utf-8') as f:
        return json.load(f)

def analyze_literal_usage(data):
    """Analyze which literal indices are actually used by methods."""
    
    # Track which indices are referenced
    referenced_strings = set()
    referenced_symbols = set()
    referenced_arrays = set()  # Array literals from methods
    referenced_floats = set()
    referenced_characters = set()
    
    # Count total literals available
    total_strings = len(data.get('strings', []))
    total_symbols = len(data.get('symbols', []))
    
    print(f"Total strings in JSON: {total_strings}")
    print(f"Total symbols in JSON: {total_symbols}")
    print(f"Total methods: {len(data.get('methods', []))}")
    
    # Scan all method literals
    method_count = 0
    literal_count = 0
    
    def track_literal_usage(literal):
        """Recursively track usage of a literal and its nested elements."""
        nonlocal literal_count
        literal_count += 1
        
        literal_type = literal.get('type')
        index = literal.get('index')
        
        if literal_type == 'string' and index is not None:
            referenced_strings.add(index)
        elif literal_type == 'symbol' and index is not None:
            referenced_symbols.add(index)
        elif literal_type == 'float' and literal.get('value') is not None:
            # Floats don't have indices, they have values - track the value
            referenced_floats.add(literal.get('value'))
        elif literal_type == 'character' and literal.get('codePoint') is not None:
            referenced_characters.add(literal.get('codePoint'))
        elif literal_type == 'array' and literal.get('elements') is not None:
            # Recursively process array elements
            for element in literal.get('elements', []):
                track_literal_usage(element)
        elif literal_type == 'association':
            # Associations reference symbols by keyIndex
            key_index = literal.get('keyIndex')
            if key_index is not None:
                referenced_symbols.add(key_index)
    
    # Process all methods
    for method in data.get('methods', []):
        method_count += 1
        for literal in method.get('literals', []):
            track_literal_usage(literal)
    
    print(f"Processed {method_count} methods with {literal_count} total literal references")
    
    # Find unreferenced indices
    unreferenced_strings = set(range(total_strings)) - referenced_strings
    unreferenced_symbols = set(range(total_symbols)) - referenced_symbols
    
    print(f"\nLiteral Usage Analysis:")
    print(f"Referenced strings: {len(referenced_strings)}/{total_strings}")
    print(f"Referenced symbols: {len(referenced_symbols)}/{total_symbols}")
    print(f"Referenced floats: {len(referenced_floats)}")
    print(f"Referenced characters: {len(referenced_characters)}")
    
    print(f"\nUnreferenced strings: {len(unreferenced_strings)}")
    print(f"Unreferenced symbols: {len(unreferenced_symbols)}")
    
    # Show some examples of unreferenced strings
    if unreferenced_strings:
        print(f"\nFirst 10 unreferenced string indices: {sorted(list(unreferenced_strings))[:10]}")
        print("Sample unreferenced strings:")
        strings = data.get('strings', [])
        for i, idx in enumerate(sorted(unreferenced_strings)[:5]):
            if idx < len(strings):
                sample = strings[idx][:100] + "..." if len(strings[idx]) > 100 else strings[idx]
                print(f"  [{idx}]: {repr(sample)}")
    
    if unreferenced_symbols:
        print(f"\nFirst 10 unreferenced symbol indices: {sorted(list(unreferenced_symbols))[:10]}")
        print("Sample unreferenced symbols:")
        symbols = data.get('symbols', [])
        for i, idx in enumerate(sorted(unreferenced_symbols)[:5]):
            if idx < len(symbols):
                print(f"  [{idx}]: {repr(symbols[idx])}")
    
    return {
        'unreferenced_strings': unreferenced_strings,
        'unreferenced_symbols': unreferenced_symbols,
        'referenced_strings': referenced_strings,
        'referenced_symbols': referenced_symbols,
        'referenced_floats': referenced_floats,
        'referenced_characters': referenced_characters,
        'total_strings': total_strings,
        'total_symbols': total_symbols
    }

def analyze_duplicate_allocations(data):
    """Analyze if the same literal values are being allocated multiple times."""
    
    # Track string content frequency
    string_content_count = Counter()
    string_indices_by_content = defaultdict(list)
    
    strings = data.get('strings', [])
    for idx, content in enumerate(strings):
        string_content_count[content] += 1
        string_indices_by_content[content].append(idx)
    
    # Find duplicate string contents
    duplicates = {content: indices for content, indices in string_indices_by_content.items() 
                 if len(indices) > 1}
    
    print(f"\nDuplicate String Analysis:")
    print(f"Total unique string contents: {len(string_content_count)}")
    print(f"String contents with duplicates: {len(duplicates)}")
    
    if duplicates:
        print("Sample duplicate strings:")
        for i, (content, indices) in enumerate(list(duplicates.items())[:5]):
            sample = content[:80] + "..." if len(content) > 80 else content
            print(f"  '{sample}' appears at indices: {indices}")
    
    return duplicates

def main():
    if len(sys.argv) != 2:
        print("Usage: python analyze_literals.py <path-to-coldstart.json>")
        sys.exit(1)
    
    filepath = sys.argv[1]
    
    try:
        print(f"Loading {filepath}...")
        data = load_coldstart_json(filepath)
        
        print("Analyzing literal usage...")
        usage_results = analyze_literal_usage(data)
        
        print("\nAnalyzing duplicate strings...")
        duplicates = analyze_duplicate_allocations(data)
        
        # Summary
        print(f"\n" + "="*60)
        print("SUMMARY")
        print("="*60)
        print(f"Total unreferenced strings: {len(usage_results['unreferenced_strings'])}")
        print(f"Total unreferenced symbols: {len(usage_results['unreferenced_symbols'])}")
        print(f"Total duplicate string contents: {len(duplicates)}")
        
        if usage_results['unreferenced_strings']:
            print(f"\nISSUE IDENTIFIED: {len(usage_results['unreferenced_strings'])} strings in the JSON are never referenced by any method literal!")
            print("This could explain the 61 unreachable String objects during GC.")
        
        if usage_results['unreferenced_symbols']:
            print(f"\nISSUE IDENTIFIED: {len(usage_results['unreferenced_symbols'])} symbols in the JSON are never referenced by any method literal!")
        
        if duplicates:
            print(f"\nPOTENTIAL OPTIMIZATION: {len(duplicates)} string contents appear multiple times.")
            print("The compiler could deduplicate these to reduce JSON size.")
        
    except Exception as e:
        print(f"Error: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()