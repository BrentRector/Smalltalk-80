# HeapManager Tests - Quick Start Guide

## Issue: Tests Not Found with Category Filter

If you see this warning:
```
warning : No test matches the given testcase filter `Category=HeapManager`
Test summary: total: 0
```

**Solution**: Clean rebuild required to pick up the `[Trait]` attribute.

## Steps to Enable HeapManager Tests

### 1. Clean Build

```bash
cd Smalltalk80.Interpreter.Tests

# Clean previous builds
dotnet clean

# Rebuild test project
dotnet build
```

### 2. Run HeapManager Tests

```bash
# Run just HeapManager tests
dotnet test --filter "Category=HeapManager"
```

**Expected Output:**
```
Test summary: total: 85, failed: 0, succeeded: 85, skipped: 0
```

### 3. Run All Categorized Tests

```bash
# Run Primitives + HeapManager (343 tests total)
dotnet test --filter "Category=Primitives|Category=HeapManager"
```

**Expected Output:**
```
Test summary: total: 343, failed: 0, succeeded: 343, skipped: 0
(258 primitive tests + 85 heap manager tests)
```

## Verification

### Check Tests Are Discovered

```bash
# List all tests (verbose)
dotnet test --list-tests
```

Should show tests like:
- `Smalltalk80.Interpreter.Tests.HeapManager.SmalltalkHeapManagerTests.AllocateObject_PointerFormat_ReturnsValidOOP`
- `Smalltalk80.Interpreter.Tests.HeapManager.SmalltalkHeapManagerTests.AllocateObject_ByteIndexedFormat_ReturnsValidOOP`
- ... (85 total)

### Check Category Filter Works

```bash
# Filter should find 85 tests
dotnet test --filter "Category=HeapManager" --list-tests
```

## Test Categories Available

After clean build, these filters work:

```bash
# Primitives only (258 tests)
dotnet test --filter "Category=Primitives"

# HeapManager only (85 tests)
dotnet test --filter "Category=HeapManager"

# VM tests
dotnet test --filter "Category=VM"

# Context tests
dotnet test --filter "Category=Context"

# Compiler tests
dotnet test --filter "Category=Compiler"

# Multiple categories
dotnet test --filter "Category=Primitives|Category=HeapManager"
```

## Why Clean Build Is Needed

The `[Trait("Category", "HeapManager")]` attribute was added to categorize tests. This attribute is embedded in the compiled .dll at build time. If you:

1. Run tests without rebuilding → uses cached .dll without trait
2. Clean + rebuild → compiles fresh .dll with trait
3. Run tests → category filter now works!

## Troubleshooting

### Still Getting "No test matches" After Clean Build?

```bash
# Nuclear option: delete all bin/obj folders
rm -rf bin obj
dotnet build
dotnet test --filter "Category=HeapManager"
```

### Want to Run ALL Tests (No Filter)?

```bash
# Run everything (all categories + uncategorized tests)
dotnet test
```

This doesn't require categories to work - it runs every test method regardless of traits.
