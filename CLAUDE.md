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