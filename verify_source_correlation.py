#!/usr/bin/env python3
"""
Script to verify if unreferenced strings correlate with method source fields.
"""

import json
import sys

def main():
    if len(sys.argv) != 2:
        print("Usage: python verify_source_correlation.py <path-to-coldstart.json>")
        sys.exit(1)
    
    filepath = sys.argv[1]
    
    with open(filepath, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    strings = data.get('strings', [])
    methods = data.get('methods', [])
    
    print(f"Total strings: {len(strings)}")
    print(f"Total methods: {len(methods)}")
    
    # Check how many methods have source fields
    methods_with_source = 0
    method_sources = []
    
    for method in methods:
        if 'source' in method and method['source']:
            methods_with_source += 1
            method_sources.append(method['source'])
    
    print(f"Methods with source field: {methods_with_source}")
    print(f"Unique method sources: {len(set(method_sources))}")
    
    # Check if method sources match strings in the strings array
    method_source_matches = 0
    string_indices_found = []
    
    for source in method_sources:
        try:
            index = strings.index(source)
            method_source_matches += 1
            string_indices_found.append(index)
        except ValueError:
            pass
    
    print(f"Method sources found in strings array: {method_source_matches}")
    print(f"String indices range: {min(string_indices_found) if string_indices_found else 'N/A'} to {max(string_indices_found) if string_indices_found else 'N/A'}")
    
    # Check comment sources too
    comments_with_source = 0
    comment_sources = []
    
    classes = data.get('classes', [])
    for cls in classes:
        if 'comment' in cls and cls['comment']:
            comments_with_source += 1
            comment_sources.append(cls['comment'])
    
    print(f"Classes with comment field: {comments_with_source}")
    
    comment_source_matches = 0
    for comment in comment_sources:
        try:
            index = strings.index(comment)
            comment_source_matches += 1
            string_indices_found.append(index)
        except ValueError:
            pass
    
    print(f"Class comments found in strings array: {comment_source_matches}")
    
    total_source_metadata = method_source_matches + comment_source_matches
    print(f"\nTotal source metadata strings: {total_source_metadata}")
    print(f"Previously found unreferenced strings: 5491")
    print(f"Difference: {5491 - total_source_metadata}")
    
    if total_source_metadata > 5000:
        print("\nCONFIRMED: The unreferenced strings are primarily method source code and class comments!")
        print("These are metadata fields that are not used as literals by the interpreter.")

if __name__ == "__main__":
    main()