#!/usr/bin/env python3
import re

# Read VirtualMachine.cs
with open(r'Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()
    content = ''.join(lines)

dead_methods = [
    (2256, "DumpMethodStateOnError"),
    (2677, "ResolveOuterContext"),
    (2693, "GetHomeContextReceiver"),
    (2704, "GetBlockHomeReceiver"),
    (2773, "FindNearestBlockExecutionContext"),
    (2779, "CleanupBlockContextMappings"),
    (2785, "LookupBlockClosureForExecutionContext"),
    (2791, "ResolveHomeContext"),
    (2804, "FindMethodContextForMethod"),
    (3883, "AllocateEmptyClassPoolDictionary"),
    (4003, "GetBlockArgCount"),
    (4217, "DumpBytecodes"),
]

print("=== DEAD CODE ANALYSIS ===\n")

print("## Category 1: Debug/Diagnostic Methods (Keep or Remove?)\n")
for line_num, name in dead_methods:
    if 'Dump' in name or 'Debug' in name:
        # Read a few lines around the method
        start = max(0, line_num - 1)
        end = min(len(lines), line_num + 10)
        snippet = ''.join(lines[start:end])
        
        # Check for NotImplementedException
        has_notimpl = 'NotImplementedException' in snippet or 'throw new' in snippet
        
        print(f"Line {line_num}: {name}")
        if has_notimpl:
            print(f"  → Throws NotImplementedException - SAFE TO DELETE")
        else:
            print(f"  → Diagnostic utility - CONSIDER KEEPING FOR DEBUGGING")
        print()

print("\n## Category 2: Obsolete BlockContext Methods (DELETE)\n")
for line_num, name in dead_methods:
    if any(x in name for x in ['Block', 'Home', 'Outer', 'Closure']):
        start = max(0, line_num - 1)
        end = min(len(lines), line_num + 10)
        snippet = ''.join(lines[start:end])
        
        has_notimpl = 'NotImplementedException' in snippet or 'obsolete' in snippet.lower()
        
        print(f"Line {line_num}: {name}")
        if has_notimpl:
            print(f"  → Marked obsolete or throws exception - SAFE TO DELETE")
        else:
            print(f"  → Obsolete BlockContext logic - DELETE")
        print()

print("\n## Category 3: Other Dead Methods\n")
for line_num, name in dead_methods:
    if not ('Dump' in name or 'Debug' in name) and not any(x in name for x in ['Block', 'Home', 'Outer', 'Closure']):
        print(f"Line {line_num}: {name}")
        start = max(0, line_num - 1)
        end = min(len(lines), line_num + 10)
        snippet = ''.join(lines[start:end])
        if 'NotImplementedException' in snippet:
            print(f"  → Stub - SAFE TO DELETE")
        print()
