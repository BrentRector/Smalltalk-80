# Contribution Guidelines

This project contains a prototype of a Smalltalk-80 interpreter written in C#. When making changes keep the following conventions in mind:

- **Grammar changes**: Never make any grammar changes that do not support the full Smalltalk-80 language. Do not sacrifice full Smalltalk language support. 
- **Language version**: Target modern C# (v13 or later) where possible. Prefer features such as pattern matching enhancements, record types, and top-level statements when appropriate.
- **Documentation**: Every public class, property, method, and enum member must include thorough XML documentation comments describing its purpose and usage.
- **Generating the parser**: You can (re)generate the C# parser by running the GenerateIfNewer.ps1 Powershell script (located in the scripts folder) This script regenerates the parser when the grammar is newer than the generated code. You can always generate the parser by running the Invoke-Antlr4CSharp.ps1 script. Never allow warning during the ANTLR4 parser generation. A warning means the grammar must be adjusted to eliminate the warning.
- **Script working directory**: Run PowerShell from the `Smalltalk80/scripts` directory when invoking GenerateIfNewer.ps1 or Invoke-Antlr4CSharp.ps1 because all paths inside these scripts are relative to that folder.
- **Available tools**: You can use the 'dotnet script' functionality as necessary. However, you invoke the tool using the following command: dotnet-script. Powershell is also available. Use the 'pwsh' command to run Powershell
- **Testing**: Run `dotnet build --no-restore` and `dotnet test --no-build` before every commit to verify that the code compiles and all tests pass. The solution contains a `GrammarTests` project that executes regression tests against the `Smalltalk80.g4` grammar. These tests run after every build and should remain part of the solution.
- **Code style**: Follow the existing formatting conventions. Use expression-bodied members when they improve readability and avoid unnecessary verbosity.
- **Repository structure**: Keep implementation code under the `Smalltalk80` directory and tests under `Smalltalk80.Tests`.
- **Grammar validation**: The repository contains `Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st` with thousands of lines of valid Smalltalk code. Use this file to validate grammar or parser changes and confirm syntax assumptions. Note that the WS lexer rule drops whitespace so there is never any need to add a whitespace production in the parser.

## AI Assistant Development Guidelines

**These are critical directives for AI assistants working on this project:**

- **Testing Framework**: ALWAYS use the existing test case framework (`TestCase` class in `TestLibrary.st`) for new tests. Never create new testing infrastructure. The existing framework provides `assert:`, `assertEqual:to:`, `assertTrue:`, `assertFalse:` and other assertion methods.
- **Standard Library Preservation**: NEVER generate new standard library code. ALWAYS use the version from the system image, without change. The `StandardLibrary.st` file contains the complete, tested implementation with 34 classes and 177 method chunks. Do not modify or recreate any part of it.
- **Test File Organization**: Place all test classes in the `Testing/` directory using the `.st` extension. Test classes should inherit from `TestCase` and follow the naming convention `<ClassName>Tests`.
- **Parser Limitations**: Be aware that the parser may have issues with symbol arguments in method calls (shows "[DEBUG] KeywordSendExpression has null argument" warnings). Work around these limitations when creating tests.
- **File Loading Order**: When running tests, always load `TestLibrary.st` first, then the specific test files. Use the pattern: `--source Testing/TestLibrary.st --source Testing/YourTestFile.st`
- **Command Line Parameters**: Use `--source <file>` to load Smalltalk source files (not `--file`, which doesn't exist). The `--source` parameter is the correct way to load additional Smalltalk files into the interpreter.
- **Temporary Files**: All temporary files, debug files, test logs, and experimental code should be placed in the `temp/` directory. This makes cleanup easier and keeps the project root organized. Examples include: debug test files (`temp/debug_*.st`), test output logs (`temp/test_*.log`), experimental code (`temp/experiment_*.st`), and backup files (`temp/*_backup.*`).
- **PowerShell Commands**: The `tail` command does not exist in PowerShell. Use `Get-Content -Tail` or `Select-Object -Last` instead. For example, use `Get-Content file.txt -Tail 10` or `Get-Content file.txt | Select-Object -Last 10` instead of `tail -10 file.txt`. Similarly, `grep` and `sort` commands are not available - use `Select-String` and `Sort-Object` instead.
- **System Image Index**: Always use the system image index (`SYSTEM_IMAGE_INDEX.md`) to locate classes and methods in the system image. This index contains the complete mapping of all classes and their methods with line numbers in the `Smalltalk-80.st` file. Use this index before attempting to search or extract classes from the system image.
- **Documentation Updates**: After any changes to the `StandardLibrary.st` file (adding new classes, methods, or modifying existing ones), ALWAYS update the `STANDARD_LIBRARY_CONTENTS.md` documentation file. This includes:
  - Updating the total class count and method chunk count
  - Adding complete documentation for new classes with all their methods
  - Updating the class hierarchy diagram
  - Marking completion status in the expansion plan
  - Ensuring all class descriptions, instance variables, and key methods are documented
- **File System Implementation**: The file system has been completely implemented with modern File and FileDirectory classes that bridge to C# file system primitives (142-157). The FileStream class has been updated to work with these new classes instead of the original Alto-specific FilePage class. All file I/O operations now use primitive-based implementations.

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
