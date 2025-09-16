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

## Recent Fixes and Improvements

### Phase 2 Validation Achieved 100% (2025-01-10)

**Major Fixes Implemented:**

1. **Method Tracing Fixed**
   - Implemented proper method exit tracing with correct indentation
   - Added MethodTracer class to track method entry/exit
   - Fixed interpreter to trigger traces for all method dispatches

2. **Bytecode Dispatcher Corrected**
   - Fixed bytecode ranges to match Blue Book specification:
     - 176-191: Send literal selector with 0 args
     - 192-207: Send literal selector with 1 arg
     - 208-223: Send literal selector with 2 args
   - Previously had argument counts swapped

3. **Method Lookup Implemented**
   - Replaced stub Lookup method with full implementation
   - Properly searches through class hierarchy's method dictionaries
   - Critical for Phase 3 class creation via message sends

4. **Pretty Printer Enhanced**
   - Fixed ExtractMethodsFromDictionary to handle both formats:
     - MethodDictionary format: `[keysArray, valuesArray]`
     - Flat Dictionary format: `[tally, key1, value1, key2, value2, ...]`
   - Now correctly displays all methods (was only showing ~10 of 37 for Dictionary)

5. **Validation Script PowerShell Bug Fixed**
   - Discovered PowerShell quirk: `$hashtable.Keys` returns the value of entry "keys" if it exists
   - Changed to use `GetEnumerator()` to properly iterate through all entries
   - This fixed false positives about "missing" methods

**Bootstrap Status:**
- ✅ All 8 core classes validate at 100%
- ✅ 379 total methods match perfectly between bootstrap and system image
- ✅ Ready for extended class implementation via bytecode execution

### Extended Class Implementation Notes

**Bytecode-Based Class Creation:**
- Extended classes should be created via bytecode execution in method contexts
- This ensures all operations go through the interpreter and trigger proper method tracing
- Avoids bypassing the VM which would miss important initialization steps

**Extended JSON Structure:**
The extended manifest should use this structure:
```json
{
  "classes": [
    {"name": "ClassName", "superclass": "SuperclassName", ...}
  ],
  "methods": [
    {"class": "ClassName", "selector": "methodName", "bytecode": [...], ...}
  ]
}
```

## Bootstrap Process Overview

The Smalltalk-80 system bootstraps in a unified process:

### Bootstrap: Core System Classes
- Creates the essential classes needed to bootstrap: Object, Behavior, Class, Metaclass, CompiledMethod, MethodDictionary, Symbol, String, Array, ByteArray, Collection, Dictionary, SystemDictionary
- Establishes the class hierarchy and metaclass relationships
- Creates method dictionaries with pre-allocated sizes for all bootstrap methods
- Installs compiled methods with bytecode into all bootstrap classes
- Creates additional classes (UndefinedObject, Boolean, True, False, Magnitude, LookupKey, Association)
- Methods are separated into instance methods (installed on the class) and class methods (installed on the metaclass)
- Registers all classes in the global Smalltalk SystemDictionary

### Extended Classes: Additional System Classes
- Adds remaining Smalltalk-80 classes with their complete method implementations
- Enhances existing classes with additional methods
- Uses flat structure with separate classes and methods arrays
- Loaded via self-hosting message sends

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

### 2. Update Bootstrap
```powershell
cd scripts
pwsh -File UpdateBootstrapMethods.ps1
```
This script:
- Reads all extracted class JSON files
- Updates `bootstrap.json` with the latest bytecode
- Ensures bytecode is always an array (even single-byte)
- Handles literal conversion to proper format

### 3. Verify Dictionary Sizes
```powershell
cd scripts
pwsh -File AnalyzeDictionarySizes.ps1
```
This checks that bootstrap pre-allocated dictionary sizes match requirements.
Critical issues (TOO SMALL) must be fixed by updating bootstrap capacities.

### 4. Clean Rebuild
After significant changes:
```powershell
dotnet clean && dotnet build
```
This prevents issues with executable files being overwritten.

## Important Bootstrap Constraints

1. **Dictionary Sizing**: Method dictionaries cannot resize until after bootstrap completes. Bootstrap must pre-allocate sufficient capacity (typically actual * 1.5 for hash table efficiency).

2. **Class Creation**: Bootstrap contains all core classes with their complete method implementations in a single unified step.

3. **Method Installation**: Instance methods go on the class's method dictionary, class methods go on the metaclass's method dictionary.

4. **Bytecode Format**: All bytecode must be arrays of integers, even single-byte bytecode.

## Validating Bootstrap

After any changes to bootstrap, validate the bootstrap is working correctly:

### Run Validation
```powershell
cd scripts
pwsh -File ValidateBootstrapSimple.ps1
```

This validates the 8 core bootstrap classes. Success criteria:
- 100% method match rate (all 379 methods)
- All bytecode sequences match exactly
- No missing or extra methods

### Fixing Common Issues

If validation fails:

1. **PowerShell .Keys bug**: If you see bytecode strings as "missing" methods, the script may be hitting the PowerShell bug where `$hashtable.Keys` returns the value of key "keys". The script now uses `GetEnumerator()` to avoid this.

2. **Method dictionary display**: If the pretty printer shows fewer methods than expected, check that it handles both MethodDictionary formats:
   - Parallel arrays: `[keysArray, valuesArray]`
   - Flat format: `[tally, key1, value1, key2, value2, ...]`

3. **Bytecode mismatches**: Ensure bytecode is in hex format (e.g., "40 88 68 7C") and that the extractor and validator use consistent formatting.

4. **Dictionary sizing**: Run `AnalyzeDictionarySizes.ps1` to ensure Phase 1 pre-allocates sufficient capacity for Phase 2 methods.

## System Image Regeneration and Field Offset Correction

When changes are made to the standalone compiler that affect field offset calculation or bytecode generation, the entire system must be regenerated to ensure correctness. This is a multi-step process that must be completed in full to achieve 100% validation.

### 1. Regenerate System Image JSON

First, use the fixed standalone compiler to regenerate the system image JSON with corrected field offsets and bytecodes:

```bash
dotnet bin/Debug/net9.0/StandaloneCompiler.dll "Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st" complete_system_final.json
```

This step:
- Processes the original Smalltalk-80 source code with the corrected compiler
- Generates bytecode with proper field offsets for inherited instance variables
- Creates the canonical `complete_system_final.json` that all other processes depend on
- Must complete without errors to proceed

### 2. Update Bootstrap Configuration

Extract the core bootstrap classes from the corrected system image and update bootstrap files:

```powershell
cd scripts
pwsh UpdateBootstrapFromSystemImage.ps1
```

**Script: `UpdateBootstrapFromSystemImage.ps1`**
- **Purpose**: Updates bootstrap.json with corrected class definitions and method bytecodes from system image
- **Usage**: `pwsh UpdateBootstrapFromSystemImage.ps1 [-SystemImagePath "complete_system_final.json"] [-BootstrapPath "../Bootstrap/bootstrap.json"]`
- **Function**: 
  - Reads class definitions, instance variables, and method bytecodes from complete_system_final.json
  - Updates class instance variable counts with complete inheritance chains
  - Updates method dictionary sizes based on actual method counts  
  - Updates bytecode arrays with corrected field access instructions
  - Updates literal arrays with proper formatting
  - Generates bootstrap_updated.json with timestamp and change comments

Key classes that must be updated in bootstrap.json:
- Object, Behavior, Class, Metaclass (core hierarchy)
- CompiledMethod, MethodDictionary (method infrastructure) 
- Symbol, String, Array, ByteArray (essential data types)
- Collection, Dictionary, SystemDictionary (collection hierarchy)
- UndefinedObject, Boolean, True, False (fundamental objects)
- Magnitude, LookupKey, Association (supporting types)

### 3. Update Low-Level Class Installation

Process the system image JSON to update any low-level class installation data:

```bash
# Update extended class manifests and phase 3 data
# Ensure all class hierarchies have correct field layouts
# Verify that method bytecodes use proper field offsets
```

### 4. Complete Verification Process

The update process is **NOT COMPLETE** until 100% verification is achieved:

#### 4.1 Complete Verification Process
```powershell
cd scripts  
pwsh VerifySystemImageMatches.ps1
```

**Script: `VerifySystemImageMatches.ps1`**
- **Purpose**: Performs complete verification between interpreter-loaded classes and system image reference
- **Usage**: `pwsh VerifySystemImageMatches.ps1 [-SystemImagePath "complete_system_final.json"] [-DumpOutput "interpreter_class_dump.txt"]`
- **Function**:
  - Loads all classes in interpreter with `--list-bootstrap-classes`
  - Dumps all loaded classes with `--dump-all-classes interpreter_class_dump.txt`
  - Parses interpreter dump to extract class information
  - Loads system image JSON as reference specification
  - Compares interpreter state vs system image for all core classes
  - Generates verification_report.txt with detailed analysis
  - Returns exit code 0 for 100% match, 1 for any mismatches

This creates a detailed comparison of:
- Class hierarchies and superclass relationships
- Instance variable counts and field layouts  
- Method dictionary contents and counts
- Inherited field offset calculations
- Object memory layout verification

#### 4.4 Validation Success Criteria

The update process is complete ONLY when:
- ✅ All bootstrap classes load without errors
- ✅ All extended classes load without errors  
- ✅ Field access bytecodes use correct offsets
- ✅ Instance sizes match calculated inheritance totals
- ✅ Method dictionaries have proper capacity
- ✅ 100% match between interpreter dump and system image JSON
- ✅ No field offset mismatches in any class hierarchy
- ✅ All bytecode sequences match reference specification

Any mismatch requires returning to step 1 and regenerating the system image.

### Field Offset Calculation Details

The corrected field offset calculation follows Smalltalk-80 Blue Book specification:

```
For class hierarchy: Object -> A -> B -> C
Where: A has fields [a1, a2]
       B has fields [b1]  
       C has fields [c1, c2]

Instance of C has layout:
  Offset 0: a1 (inherited from A)
  Offset 1: a2 (inherited from A)
  Offset 2: b1 (inherited from B)  
  Offset 3: c1 (declared in C)
  Offset 4: c2 (declared in C)
```

When compiling methods in class C:
- `a1` access generates `pushInstVar: 0` 
- `a2` access generates `pushInstVar: 1`
- `b1` access generates `pushInstVar: 2`
- `c1` access generates `pushInstVar: 3` 
- `c2` access generates `pushInstVar: 4`

The standalone compiler now correctly computes this full inheritance chain when generating bytecode.

## Complete System Image Regeneration Workflow

### Full Process Summary

When field offset corrections or compiler changes are made, follow this complete workflow:

```powershell
# Step 1: Regenerate system image with corrected compiler
dotnet bin/Debug/net9.0/StandaloneCompiler.dll "Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st" complete_system_final.json

# Step 2: Update bootstrap from corrected system image  
cd scripts
pwsh UpdateBootstrapFromSystemImage.ps1

# Step 3: Copy updated bootstrap if verification succeeds
cp ../Bootstrap/bootstrap_updated.json ../Bootstrap/bootstrap.json

# Step 4: Rebuild with updated bootstrap
cd ..
dotnet build --no-restore

# Step 5: Verify 100% compatibility 
cd scripts
pwsh ValidateFieldOffsets.ps1
```

### Required Scripts

All scripts are located in the `scripts/` directory:

1. **`UpdateBootstrapFromSystemImage.ps1`** - Updates bootstrap.json from system image
2. **`VerifySystemImageMatches.ps1`** - Verifies 100% interpreter/system image match
3. **`ValidateFieldOffsets.ps1`** - Validates field offset corrections are working correctly

### Validation Requirements

The process must achieve **100% verification** or the system is incomplete:
- All bootstrap classes must load without errors
- Field counts must match calculated inheritance totals  
- Method counts must match system image specification
- No field offset mismatches in any class hierarchy
- Interpreter dump must match system image JSON exactly

Any mismatch requires returning to Step 1 and regenerating the entire system.