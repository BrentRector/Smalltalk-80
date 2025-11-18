# Phase 1: Critical Cleanup - Executable Commands

**Date**: 2025-11-17
**Status**: READY TO EXECUTE
**Estimated Time**: 30-45 minutes
**Risk Level**: LOW (compiler will catch any issues)

---

## Pre-Flight Checklist

Before starting, ensure:
- [ ] Current branch is clean (no uncommitted changes)
- [ ] All tests pass
- [ ] Build succeeds with 0 errors, 0 warnings

```bash
cd /home/user/Smalltalk-80/Smalltalk80.Interpreter
git status
dotnet build
dotnet test
```

---

## Step 1: Extract ObjectData from ObjectMemory

Create new file for ObjectData type that ObjectTable.cs uses:

```bash
cd /home/user/Smalltalk-80/Smalltalk80.Interpreter
```

Create `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/Memory/ObjectData.cs`:

```csharp
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.Memory;

/// <summary>
/// Data structure for a Smalltalk object.
/// Contains class reference, format, instance variables, and indexed data.
/// </summary>
public sealed class ObjectData
{
    /// <summary>Class reference for this object.</summary>
    public SmalltalkOOP ClassRef { get; set; }

    /// <summary>Storage format determining how indexed data is interpreted.</summary>
    public ObjectFormat Format { get; set; }

    /// <summary>Named instance variables (null for zero-length).</summary>
    public SmalltalkOOP[]? InstanceVariables { get; set; }

    /// <summary>Indexed data: byte[], ushort[], or SmalltalkOOP[] depending on Format.</summary>
    public object? IndexedData { get; set; }

    /// <summary>GC mark bit for mark-and-sweep collection.</summary>
    public bool Marked { get; set; }
}
```

---

## Step 2: Move ObjectFormat.cs

```bash
cd /home/user/Smalltalk-80/Smalltalk80.Interpreter

# Move ObjectFormat to Core/Memory
mv Core/Objects/ObjectFormat.cs Core/Memory/ObjectFormat.cs
```

Update namespace in `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/Memory/ObjectFormat.cs`:

Change line 1:
```csharp
namespace Smalltalk80.Interpreter.Core.Objects;
```

To:
```csharp
namespace Smalltalk80.Interpreter.Core.Memory;
```

---

## Step 3: Update ObjectTable.cs

Update `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/Memory/ObjectTable.cs`:

Change references from `ObjectMemory.ObjectData` to just `ObjectData`:

```bash
# This will be done via Edit tool, showing the changes needed:
# Line ~8: public ObjectData? Data { get; set; }  (was ObjectMemory.ObjectData)
# Line ~44: public int Allocate(ObjectData data)  (was ObjectMemory.ObjectData)
# Line ~63: public ObjectData? Get(int index)     (was ObjectMemory.ObjectData)
# Line ~74: public void Set(int index, ObjectData data)  (was ObjectMemory.ObjectData)
# Line ~159: private void TraceReferences(ObjectData data, ...)  (was ObjectMemory.ObjectData)
# And several more...
```

No need to change the `using` statement since ObjectData is now in same namespace.

---

## Step 4: Delete Unused Files

```bash
cd /home/user/Smalltalk-80/Smalltalk80.Interpreter

# Delete legacy/unused files
rm Core/Objects/ObjectTableEntry.cs    # Duplicate
rm Core/Objects/SObject.cs             # Unused stub
rm Core/Objects/SystemDictionary.cs    # Unused
rm Core/Objects/ObjectMemory.cs        # Stub (ObjectData extracted)

# Verify directory is empty except README if present
ls -la Core/Objects/

# If empty, delete directory
rmdir Core/Objects/
```

---

## Step 5: Update Using Statements (35 files)

Files that import `Core.Objects` need updates. Most only need ObjectFormat:

### Strategy:
1. Remove: `using Smalltalk80.Interpreter.Core.Objects;`
2. Add if needed: `using Smalltalk80.Interpreter.Core.Memory;`

### Files to Update:

**Will need Core.Memory (for ObjectFormat)**:
- All files in Core/HeapManager/ (5 files)
- All files in Engine/LoaderPhases/ (6 files)
- All files in Core/VM/Primitives/ (11 files)
- Engine/Loader.cs
- Engine/InitExecution/InitExpressionExecutor.cs
- Engine/ColdStartPrimitives.cs
- Core/VM/VirtualMachine.cs
- Core/VM/SymbolRegistry.cs
- Core/SemanticAllocator.cs
- Core/SnapshotWriter.cs
- Core/Memory/SpecialObjectsArray.cs
- Program.cs
- Tests/* (4 files)

**May not need any import** (transitively used):
- Some files may not directly reference ObjectFormat

### Batch Update Command:

```bash
cd /home/user/Smalltalk-80/Smalltalk80.Interpreter

# Find all files that import Core.Objects
grep -r "using Smalltalk80.Interpreter.Core.Objects;" --include="*.cs" | cut -d: -f1 | sort -u

# For each file, you'll need to:
# 1. Remove old using
# 2. Add new using if needed (for ObjectFormat)
# 3. Verify no other Core.Objects types were used
```

---

## Step 6: Build and Test

```bash
cd /home/user/Smalltalk-80/Smalltalk80.Interpreter

# Full rebuild
dotnet clean
dotnet build

# Expected: 0 errors, 0 warnings

# Run unit tests
dotnet test

# Run integration test
dotnet run

# Expected: Cold start succeeds, init expressions run
```

---

## Step 7: Validation

Run validation checks:

```bash
cd /home/user/Smalltalk-80/Smalltalk80.Interpreter

# 1. Verify Core/Objects deleted
if [ ! -d "Core/Objects" ]; then
    echo "✅ Core/Objects directory deleted"
else
    echo "❌ Core/Objects still exists"
    ls -la Core/Objects/
fi

# 2. Verify ObjectFormat moved
if [ -f "Core/Memory/ObjectFormat.cs" ]; then
    echo "✅ ObjectFormat moved to Core/Memory"
else
    echo "❌ ObjectFormat not found in Core/Memory"
fi

# 3. Verify ObjectData extracted
if [ -f "Core/Memory/ObjectData.cs" ]; then
    echo "✅ ObjectData extracted to Core/Memory"
else
    echo "❌ ObjectData not found in Core/Memory"
fi

# 4. Verify single ObjectTableEntry
count=$(find . -name "ObjectTableEntry.cs" -type f | grep -v "/obj/" | grep -v "/bin/" | wc -l)
if [ "$count" -eq 1 ]; then
    echo "✅ Single ObjectTableEntry.cs (in Core/HeapManager)"
else
    echo "❌ Found $count ObjectTableEntry.cs files (should be 1)"
    find . -name "ObjectTableEntry.cs" -type f | grep -v "/obj/" | grep -v "/bin/"
fi

# 5. Verify no references to Core.Objects namespace
count=$(grep -r "using Smalltalk80.Interpreter.Core.Objects;" --include="*.cs" 2>/dev/null | wc -l)
if [ "$count" -eq 0 ]; then
    echo "✅ No references to Core.Objects namespace"
else
    echo "❌ Found $count references to Core.Objects namespace"
    grep -r "using Smalltalk80.Interpreter.Core.Objects;" --include="*.cs"
fi

# 6. Verify build succeeds
if dotnet build --no-restore > /dev/null 2>&1; then
    echo "✅ Build succeeds"
else
    echo "❌ Build failed"
    dotnet build
fi
```

---

## Step 8: Commit Changes

```bash
cd /home/user/Smalltalk-80

git add -A
git commit -m "Phase 1: Clean up Core/Objects directory

- DELETE Core/Objects/ObjectTableEntry.cs (duplicate, superseded by Core/HeapManager version)
- DELETE Core/Objects/SObject.cs (unused stub class)
- DELETE Core/Objects/SystemDictionary.cs (unused, only string name referenced)
- DELETE Core/Objects/ObjectMemory.cs (stub methods, only nested type used)
- MOVE Core/Objects/ObjectFormat.cs -> Core/Memory/ObjectFormat.cs
- EXTRACT ObjectMemory.ObjectData -> Core/Memory/ObjectData.cs
- DELETE Core/Objects/ directory (now empty)
- UPDATE 35 files: Core.Objects -> Core.Memory namespace

All files now reference Core.Memory for ObjectFormat and ObjectData.
Core/HeapManager/ObjectTableEntry.cs is the single source of truth.

This cleanup removes legacy stub code and eliminates duplicate definitions
before refactoring into formal subsystems."

git log -1 --stat
```

---

## Rollback Plan (If Issues Occur)

If something goes wrong:

```bash
cd /home/user/Smalltalk-80

# Discard all changes
git reset --hard HEAD
git clean -fd

# Verify clean state
git status
dotnet build
```

---

## Expected Outcomes

After successful execution:

✅ Core/Objects/ directory deleted
✅ ObjectFormat.cs in Core/Memory/
✅ ObjectData.cs in Core/Memory/
✅ Single ObjectTableEntry.cs (in Core/HeapManager/)
✅ All 35 files updated to use Core.Memory
✅ Build succeeds with 0 errors, 0 warnings
✅ All tests pass
✅ Cold start succeeds
✅ Init expressions run successfully

---

## Files Modified Summary

**Deleted** (5 files):
- Core/Objects/ObjectTableEntry.cs
- Core/Objects/SObject.cs
- Core/Objects/SystemDictionary.cs
- Core/Objects/ObjectMemory.cs
- Core/Objects/ (directory)

**Created** (1 file):
- Core/Memory/ObjectData.cs

**Moved** (1 file):
- Core/Objects/ObjectFormat.cs → Core/Memory/ObjectFormat.cs

**Modified** (~36 files):
- Core/Memory/ObjectTable.cs (ObjectMemory.ObjectData → ObjectData)
- 35 files with updated using statements

**Total Impact**: ~43 files affected

---

## Time Estimates

- Step 1-2: Extract and move files - 5 min
- Step 3-4: Update ObjectTable.cs and delete - 5 min
- Step 5: Update 35 using statements - 15 min
- Step 6: Build and test - 5 min
- Step 7: Validation - 5 min
- Step 8: Commit - 5 min

**Total: 30-45 minutes**

---

## Next Phase

After Phase 1 completes successfully, proceed to Phase 2:
- Rename DebugOptions files
- Move misplaced files (WinFormsDisplayProvider, TestColdStart)
- Begin subsystem extraction planning

See `/docs/CRITICAL-NAMING-ISSUES.md` for Phase 2 details.

---

**Ready to Execute**: All commands are copy-paste ready. Review each step before executing.
