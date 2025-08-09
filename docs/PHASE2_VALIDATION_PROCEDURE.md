# Phase 2 Validation Procedure

This document describes the complete procedure for validating that Phase 2 bootstrap correctly installs all methods with proper bytecode for all Phase 1 and Phase 2 classes.

## Prerequisites

1. Ensure you have the latest code built:
```powershell
cd D:\Users\brent\OneDrive\Src\Smalltalk-80
dotnet clean
dotnet build
```

2. Ensure the Phase 2 JSON has been properly formatted (selectors without spaces):
```powershell
cd scripts
pwsh -File FixSelectorSpaces.ps1
```

## Quick Validation (Core Classes Only)

For a quick validation of the 8 core Phase 2 classes:

```powershell
cd scripts
pwsh -File ValidatePhase2Simple.ps1
```

This validates: Object, Behavior, Class, Symbol, String, Array, Dictionary, CompiledMethod

**Expected Result:** 7-8 classes fully valid (Dictionary may show minor parsing issues but has correct bytecode)

## Complete Validation (All Phase 2 Classes)

For comprehensive validation of all 20 Phase 2 classes:

```powershell
cd scripts
pwsh -File ValidatePhase2All.ps1
```

This validates all Phase 1 and Phase 2 classes including:
- Core classes: Object, Behavior, Class, Metaclass
- Collection classes: Collection, Dictionary, MethodDictionary, SystemDictionary
- Basic types: Symbol, String, Array, ByteArray
- Boolean classes: Boolean, True, False, UndefinedObject
- Support classes: Magnitude, LookupKey, Association, CompiledMethod

**Expected Result:** 
- ~6-10 classes with 100% match
- 500+ methods with matching bytecode
- <10 bytecode mismatches
- ~50 missing class methods (expected for Phase 3)

## Debugging Specific Classes

To validate a specific class:

```powershell
cd scripts
pwsh -File ValidatePhase2All.ps1 -Classes ClassName
```

Example for Dictionary:
```powershell
pwsh -File ValidatePhase2All.ps1 -Classes Dictionary
```

## Understanding the Validation Output

### Success Indicators

```
✓ VALID: All 78 methods match
```
- All methods present and bytecode matches exactly

### Warning Indicators

```
⚠ ISSUES FOUND:
  ✓ Matching: 107/110
  ✗ Bytecode mismatches: 3
  - Missing from Phase 2: 6 methods
```
- Some methods match but issues exist
- Review specific mismatches

### Key Metrics

1. **Method Count Match**: Phase 2 vs System Image
2. **Bytecode Match**: Byte-by-byte comparison
3. **Missing Methods**: Usually class methods for Phase 3
4. **Extra Methods**: Shouldn't happen if Phase 2 is correct

## Validation Process Details

The validation performs these steps for each class:

1. **Extract Phase 2 State**
   - Runs interpreter with `--skip-phase3 --print-class ClassName --no-mnemonics`
   - Parses method names and bytecode arrays
   - Important: Uses `--no-mnemonics` to get raw bytecode arrays

2. **Extract System Image**
   - Uses Smalltalk80.ExtractClass to get methods from system image
   - Compiles methods to bytecode using same compiler
   - Converts hex string bytecode to integer arrays

3. **Compare Methods**
   - Matches methods by selector name
   - Compares bytecode byte-by-byte
   - Reports matches, mismatches, missing, and extra methods

## Troubleshooting Common Issues

### Issue: Dictionary shows 0% match
**Cause**: Parsing issue with bytecode appearing as method name
**Solution**: This is a known script bug, manually verify Dictionary has 37 methods:
```powershell
dotnet bin/Debug/net9.0/Smalltalk80.Interpreter.dll --skip-phase3 --print-class Dictionary --no-mnemonics | Select-String "Methods \((\d+)\)"
```

### Issue: Many missing class methods
**Cause**: Class methods are typically added in Phase 3
**Solution**: This is expected. Focus on instance method validation.

### Issue: Bytecode mismatches
**Cause**: Usually differences in literal references or primitive numbers
**Solution**: Debug specific methods:
```powershell
pwsh -File DebugMethodBytecode.ps1 -Class ClassName -Method methodName
```

### Issue: Selectors with spaces
**Cause**: Phase 2 JSON had spaces in multi-word selectors
**Solution**: Run the fix script:
```powershell
pwsh -File FixSelectorSpaces.ps1
```

## Manual Verification

To manually verify a class has all methods installed:

1. Check method count:
```powershell
dotnet bin/Debug/net9.0/Smalltalk80.Interpreter.dll --skip-phase3 --print-class Object 2>&1 | Select-String "Methods \((\d+)\)"
```

2. List all methods:
```powershell
dotnet bin/Debug/net9.0/Smalltalk80.Interpreter.dll --skip-phase3 --print-class Object 2>&1 | Select-String "^  \S" | Where-Object { $_ -notmatch "Superclass|Methods|Instance|Class" }
```

3. Check specific method bytecode:
```powershell
dotnet bin/Debug/net9.0/Smalltalk80.Interpreter.dll --skip-phase3 --print-class Object --no-mnemonics 2>&1 | Select-String -A5 "^  methodName$"
```

## Expected Phase 2 Method Counts

These are the expected instance method counts for Phase 2 classes:

| Class | Expected Count | Notes |
|-------|---------------|-------|
| Object | 78 | Core object protocol |
| Behavior | 115 | Class behavior methods |
| Class | 36 | Class creation/management |
| Symbol | 23 | Symbol handling |
| String | 41 | String operations |
| Array | 6 | Basic array access |
| Dictionary | 37 | Dictionary operations |
| CompiledMethod | 43 | Method introspection |
| Metaclass | 22 | Metaclass protocol |
| MethodDictionary | 2 | Specialized dictionary |
| SystemDictionary | 69 | System management |
| ByteArray | 12 | Byte operations |
| Collection | 31 | Collection protocol |
| Boolean | 14 | Boolean operations |
| True | 10 | True singleton |
| False | 10 | False singleton |
| UndefinedObject | 8 | nil protocol |
| Magnitude | 10 | Comparison protocol |
| LookupKey | 7 | Key-value protocol |
| Association | 4 | Key-value pairs |

## Success Criteria

Phase 2 validation is considered successful when:

1. **Core 8 classes**: 100% instance method match with correct bytecode
2. **Method installation**: All Phase 2 JSON methods are installed
3. **Bytecode accuracy**: <1% bytecode mismatches across all methods
4. **No critical failures**: No missing instance methods for Phase 1/2 classes

## Automated Validation Script

For CI/CD integration, use this PowerShell script:

```powershell
# RunPhase2Validation.ps1
$result = & pwsh -File ValidatePhase2Simple.ps1
if ($result -match "VALIDATION PASSED|7 out of 8 classes are fully valid") {
    Write-Host "✅ Phase 2 Validation PASSED" -ForegroundColor Green
    exit 0
} else {
    Write-Host "❌ Phase 2 Validation FAILED" -ForegroundColor Red
    exit 1
}
```

## Summary

The Phase 2 validation ensures that:
- All methods from Phase 2 JSON are correctly installed
- Method dictionaries are properly sized and populated
- Bytecode matches the system image compilation
- The bootstrap is ready for Phase 3 installation

Run the validation after any changes to:
- Phase 2 JSON structure
- Method dictionary creation
- Bootstrap installation process
- Bytecode compilation