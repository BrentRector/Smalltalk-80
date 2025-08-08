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

## Compiler Testing

**Critical**: After any changes to the grammar or parser, you MUST test the compiler against the Smalltalk-80 system image:

```bash
dotnet run --project "Smalltalk80.Compiler/Smalltalk80.Compiler.csproj" -- --validate "Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st"
```

**Expected output for a successful test:**
- **0 errors**
- **0 warnings**
- **41,402 lines of code**
- Parsing time around 1 second

Any errors or warnings indicate a regression in the parser that must be fixed immediately. The Smalltalk-80 system image is the canonical reference and must always parse without errors.

## AI Assistant Development Guidelines

**These are critical directives for AI assistants working on this project:**

- **Testing Framework**: ALWAYS use the existing test case framework (`TestCase` class in `TestLibrary.st`) for new tests. Never create new testing infrastructure. The existing framework provides `assert:`, `assertEqual:to:`, `assertTrue:`, `assertFalse:` and other assertion methods.
- **Standard Library Preservation**: NEVER modify any class in `StandardLibrary.st` without explicit permission from the project owner. Each class should be identical to the version in the system image. There is one exception to this rule: the `File` class may be customized as required for platform compatibility. Always use the exact version from the system image for all other classes, without change.
- **Test File Organization**: Place all test classes in the `Testing/` directory using the `.st` extension. Test classes should inherit from `TestCase` and follow the naming convention `<ClassName>Tests`.
- **Parser Limitations**: Be aware that the parser may have issues with symbol arguments in method calls (shows "[DEBUG] KeywordSendExpression has null argument" warnings). Work around these limitations when creating tests.
- **File Loading Order**: When running tests, always load `TestLibrary.st` first, then the specific test files. Use the pattern: `--source Testing/TestLibrary.st --source Testing/YourTestFile.st`
- **PowerShell Commands**: The `tail` command does not exist in PowerShell. Use `Get-Content -Tail` or `Select-Object -Last` instead. For example, use `Get-Content file.txt -Tail 10` or `Get-Content file.txt | Select-Object -Last 10` instead of `tail -10 file.txt`. Similarly, `grep` and `sort` commands are not available - use `Select-String` and `Sort-Object` instead.
- **System Image Index**: Always use the system image index (`SYSTEM_IMAGE_INDEX.md`) to locate classes and methods in the system image. This index contains the complete mapping of all classes and their methods with line numbers in the `Smalltalk-80.st` file. Use this index before attempting to search or extract classes from the system image.
- **Documentation Updates**: After any changes to the `StandardLibrary.st` file (adding new classes, methods, or modifying existing ones), ALWAYS update the `STANDARD_LIBRARY_CONTENTS.md` documentation file. This includes:
  - Updating the total class count and method chunk count
  - Adding complete documentation for new classes with all their methods
  - Updating the class hierarchy diagram
  - Marking completion status in the expansion plan
  - Ensuring all class descriptions, instance variables, and key methods are documented
- **File System Implementation**: The file system has been completely implemented with modern File and FileDirectory classes that bridge to C# file system primitives (142-157). The FileStream class has been updated to work with these new classes instead of the original Alto-specific FilePage class. All file I/O operations now use primitive-based implementations.

## Folder Organization Guidelines

**Repository Structure and File Management:**

- **Core Implementation**: All interpreter code belongs in the `Smalltalk80/` directory
- **Test Files**: Test classes go in the `Testing/` directory with `.st` extensions
- **Documentation**: Keep all documentation files in the `docs/` directory
- **Build Artifacts**: The `bin/` and `obj/` directories contain build outputs - never commit these
- **Temporary Files**: Use the `temp/` directory for temporary files, logs, and test outputs:
  - **Log files**: All `*.log` files should be moved to `temp/` to keep the root clean
  - **Test outputs**: Files like `test_output.txt`, `test_output.log` belong in `temp/`
  - **Parse results**: Parser validation logs and system image parse outputs go in `temp/`
  - **Debug files**: Temporary debug files and experimental outputs belong in `temp/`
- **Archive Directory**: The `old_duplicates_backup/` directory contains archived duplicate files - avoid modifying
- **Scripts**: PowerShell and other utility scripts belong in the `scripts/` directory

**File Cleanup Protocol:**
- Move all log files and test outputs to the `temp/` directory immediately after creation
- Keep the repository root clean of temporary artifacts
- Use descriptive names for temporary files with timestamps when appropriate
- Regularly clean out obsolete files from the `temp/` directory

## PowerShell Installation (Reference)

- **Installing powershell**: This will have been done when setting up the Environment:
1. Update package index
   sudo apt update
2. Install prerequisites
   sudo apt install -y wget apt-transport-https software-properties-common
3. Import Microsoft's GPG key
   wget -q https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb
   sudo dpkg -i packages-microsoft-prod.deb
4. Update again to load the Microsoft repo
   sudo apt update
5. Install PowerShell
   sudo apt install -y powershell

These guidelines apply to all future contributions unless otherwise specified.
