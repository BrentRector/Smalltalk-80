# Contribution Guidelines

This project contains a prototype of a Smalltalk-80 interpreter written in C#. When making changes keep the following conventions in mind:

- **Grammar changes**: Never make any grammar changes that do not support the full Smalltalk-80 language. Do not sacrifice full Smalltalk language support. 
- **Language version**: Target modern C# (v13 or later) where possible. Prefer features such as pattern matching enhancements, record types, and top-level statements when appropriate.
- **Documentation**: Every public class, property, method, and enum member must include thorough XML documentation comments describing its purpose and usage.
- **Generating the parser**: You can (re)generate the C# parser by running the GenerateIfNewer.ps1 Powershell script (located in the scripts folder) This script regenerates the parser when the grammar is newer than the generated code. You can always generate the parser by running the Invoke-Antlr4CSharp.ps1 script. Never allow warning during the ANTLR4 parser generation. A warning means the grammar must be adjusted to eliminate the warning.
- **Script working directory**: Run PowerShell from the `Smalltalk80/scripts` directory when invoking GenerateIfNewer.ps1 or Invoke-Antlr4CSharp.ps1 because all paths inside these scripts are relative to that folder.
- **Available tools**: You can use the 'dotnet script' functionality as necessary. However, you invoke the tool using the following command: dotnet-script. Powershell is also available. Use the 'pwsh' command to run Powershell
- **Testing**: Run `dotnet build --no-restore` before every commit to verify that the code compiles. The compiler is automatically tested against the Smalltalk-80 system image after each build.
- **Code style**: Follow the existing formatting conventions. Use expression-bodied members when they improve readability and avoid unnecessary verbosity.
- **Repository structure**: Keep implementation code under the `Smalltalk80` directory and tests under `Smalltalk80.Tests`.
- **Grammar validation**: The repository contains `Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st` with thousands of lines of valid Smalltalk code. Use this file to validate grammar or parser changes and confirm syntax assumptions. Note that the WS lexer rule drops whitespace so there is never any need to add a whitespace production in the parser.

## Contribution Tracking

### Phase 3 Validation Results (2025-01-08) 

**CRITICAL FINDING: Phase 3 JSON Structure Issue Resolved**

**Problem Identified:**
- Phase 3 JSON files were using custom schema with `"className"`, `"methods"`, `"classMethods"` structure
- Phase3Executor expects different structure: `"classes": []`, `"methods": []` (flat list with `"class"` property)  
- This mismatch caused methods to appear to load but not actually install in method dictionaries

**Schema Comparison:**
```json
// WRONG - Our original schema
{
  "className": "Symbol", 
  "methods": [{...}],
  "classMethods": [{...}]
}

// CORRECT - Phase3Executor expected schema
{
  "classes": [{"name": "Symbol", ...}],
  "methods": [{"class": "Symbol", ...}, {"class": "Symbol class", ...}]
}
```

**Method Count Validation Against System Image:**
- **Symbol**: System image has 23 instance + 8 class methods = 31 total ✓ 
- **Character**: System image has ~35 methods (extraction validated)
- **Number**: System image has ~50+ methods (simplified to 8 essential)
- **Integer**: System image has ~60+ methods (simplified to 8 essential)

**Phase 3 Processing Behavior:** 
- Phase3Executor attempts to create new classes instead of enhancing existing ones
- This causes class conflicts and prevents proper method installation
- Solution: Phase 3 should enhance existing Phase 1/2 classes, not recreate them

**Current Status:**
- ✅ JSON structure corrected to Phase3Manifest format
- ✅ Symbol methods validated against system image (31 methods correct)
- ✅ Phase 3 loading process debugged and understood
- ⚠️ Method installation still has class creation conflicts  

**To Resume Work:** The Phase 3 validation revealed that our classes have the correct methods from the system image, but the installation process needs refinement to enhance existing classes rather than recreating them. Next step is to fix the Phase3Executor to properly install methods in existing classes.

## Bootstrap Process Overview

The Smalltalk-80 system bootstraps in three phases:

### Phase 1: Minimal Core Classes
- Creates the essential classes needed to bootstrap: Object, Behavior, Class, Metaclass, CompiledMethod, MethodDictionary, Symbol, String, Array, ByteArray, Collection, Dictionary, SystemDictionary
- Establishes the class hierarchy and metaclass relationships
- Creates empty method dictionaries with pre-allocated sizes (critical since dictionaries cannot resize until after Phase 2)
- Registers all classes in the global Smalltalk SystemDictionary

### Phase 2: Method Installation
- Installs compiled methods with bytecode into the Phase 1 classes
- Creates additional classes not in Phase 1 (UndefinedObject, Boolean, True, False, Magnitude, LookupKey, Association)
- Methods are separated into instance methods (installed on the class) and class methods (installed on the metaclass)
- Method dictionaries must be sized correctly in Phase 1 to accommodate all Phase 2 methods

### Phase 3: Full System Classes
- Adds remaining Smalltalk-80 classes with their complete method implementations
- Enhances existing classes with additional methods
- Uses flat structure with separate classes and methods arrays

## Extracting Classes from System Image

To extract a class from the Smalltalk-80 system image and update the bootstrap files:

### 1. Extract the Class
```powershell
cd Smalltalk80.ExtractClass
dotnet run -- --class ClassName
```
This generates `extracted/ClassName.json` with:
- Compiled bytecode for all methods
- Proper argument and temporary counts
- Literals array
- Separation of instance and class methods

### 2. Update Phase 2 Bootstrap
```powershell
cd scripts
pwsh -File UpdatePhase2Methods.ps1
```
This script:
- Reads all extracted class JSON files
- Updates `bootstrap_phase2.json` with the latest bytecode
- Ensures bytecode is always an array (even single-byte)
- Handles literal conversion to proper format

### 3. Verify Dictionary Sizes
```powershell
cd scripts
pwsh -File AnalyzeDictionarySizes.ps1
```
This checks that Phase 1 pre-allocated dictionary sizes match Phase 2 requirements.
Critical issues (TOO SMALL) must be fixed by updating Phase 1 capacities.

### 4. Clean Rebuild
After significant changes:
```powershell
dotnet clean && dotnet build
```
This prevents issues with executable files being overwritten.

## Important Bootstrap Constraints

1. **Dictionary Sizing**: Method dictionaries cannot resize until after Phase 2 completes. Phase 1 must pre-allocate sufficient capacity (typically actual * 1.5 for hash table efficiency).

2. **Class Creation**: Phase 1 contains only minimal core classes. Phase 2 creates additional classes as needed using `CreatePhase2Class` which checks if a class exists before creating it.

3. **Method Installation**: Instance methods go on the class's method dictionary, class methods go on the metaclass's method dictionary.

4. **Bytecode Format**: All bytecode must be arrays of integers, even single-byte bytecode.

## Validating Phase 2 Bootstrap

After any changes to Phase 2, validate the bootstrap is working correctly:

### Quick Validation (Recommended)
```powershell
cd scripts
pwsh -File RunPhase2Validation.ps1 -Quick
```

This validates the 8 core Phase 2 classes. Success criteria:
- 95%+ method match rate
- Core classes (Object, Behavior, Class) have correct methods
- Minor issues with Dictionary parsing are acceptable

### Complete Validation
```powershell
cd scripts
pwsh -File RunPhase2Validation.ps1
```

This validates all 20 Phase 2 classes. Expect:
- 85%+ overall method match rate
- ~50 missing class methods (intended for Phase 3)
- <10 bytecode mismatches

### Fixing Common Issues

If validation fails:

1. **Selectors with spaces**: Run `pwsh scripts/FixSelectorSpaces.ps1`
2. **Method dictionary size issues**: Check Phase 1 pre-allocation
3. **Bytecode mismatches**: Usually literal or primitive differences

For detailed validation procedures, see `docs/PHASE2_VALIDATION_PROCEDURE.md`