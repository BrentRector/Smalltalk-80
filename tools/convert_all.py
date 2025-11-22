#!/usr/bin/env python3
"""Batch convert all .form files to .bmp files."""

import os
import glob
from form_to_bmp import convert_form_to_bmp

def main():
    form_files = glob.glob('*.form')

    print(f"Found {len(form_files)} Form files to convert")

    for form_file in sorted(form_files):
        bmp_file = form_file.replace('.form', '.bmp')
        try:
            convert_form_to_bmp(form_file, bmp_file)
            print()
        except Exception as e:
            print(f"Error converting {form_file}: {e}")
            print()

if __name__ == '__main__':
    main()
