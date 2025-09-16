#!/usr/bin/env python3
import subprocess
import os
import json

print("Running VM comparison with updated bootstrap...")

# Change to the bin directory
os.chdir("bin/Debug/net9.0")

# Run the VM with full class loading and dump memory
print("1. Running interpreter to dump VM memory...")
result = subprocess.run(["./TestLowLevelClasses.exe"], capture_output=True, text=True)

# Change back to original directory
os.chdir("../../..")

if result.returncode != 0:
    print(f"Error running interpreter: {result.stderr}")
    exit(1)

print("2. Running comparison analysis...")
result = subprocess.run(["python", "vm_system_comparison.py"], capture_output=True, text=True)

if result.returncode != 0:
    print(f"Error running comparison: {result.stderr}")
    print(f"Output: {result.stdout}")
    exit(1)

print(result.stdout)

# Try to read the generated reports
try:
    with open("comparison_report.json", "r") as f:
        report = json.load(f)
        
    print(f"\nSummary of improvements:")
    print(f"Perfect matches: {report['summary']['perfect_matches']}")
    print(f"Structural matches: {report['summary']['structural_matches']}") 
    print(f"Complete mismatches: {report['summary']['complete_mismatches']}")
    
    if report['perfect_matches']:
        print(f"\nPerfect matches: {[cls['class'] for cls in report['perfect_matches']]}")
    
    if report['structural_matches']:
        print(f"\nStructural matches: {[cls['class'] for cls in report['structural_matches']]}")
        
    if report['complete_mismatches']:
        print(f"\nRemaining mismatches: {[cls['class'] for cls in report['complete_mismatches']]}")
        
except FileNotFoundError:
    print("Comparison report not found")
except Exception as e:
    print(f"Error reading comparison report: {e}")