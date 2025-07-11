# Contribution Guidelines

This project contains a prototype of a Smalltalk-80 interpreter written in C#. When making changes keep the following conventions in mind:

- ** Grammar changes**: Never make any grammar changes that do not support the full Smalltalk-80 language. Do not sacrifice full Smalltalk language support. 
- **Language version**: Target modern C# (v13 or later) where possible. Prefer features such as pattern matching enhancements, record types, and top-level statements when appropriate.
- **Documentation**: Every public class, property, method, and enum member must include thorough XML documentation comments describing its purpose and usage.
- **Generating the parser**: You can (re)generate the C# parser by running the GenerateIfNewer.ps1 Powershell script (located in the scripts folder) This script regenerates the parser when the grammar is newer than the generated code. You can always generate the parser by running the Invoke-Antlr4CSharp.ps1 script. Never allow warning during the ANTLR4 parser generation. A warning means the grammar must be adjusted to eliminate the warning.
- **Script working directory**: Run PowerShell from the `Smalltalk80/scripts` directory when invoking GenerateIfNewer.ps1 or Invoke-Antlr4CSharp.ps1 because all paths inside these scripts are relative to that folder.
- **Available tools**: The nano editor is available. You can use the 'dotnet script' functionality as necessary. However, you invoke the tool using the following command: dotnet-script
- **Testing**: Run `dotnet build --no-restore` and `dotnet test --no-build` before every commit to verify that the code compiles and all tests pass.
- **Code style**: Follow the existing formatting conventions. Use expression-bodied members when they improve readability and avoid unnecessary verbosity.
- **Repository structure**: Keep implementation code under the `Smalltalk80` directory and tests under `Smalltalk80.Tests`.
- **Grammar validation**: The repository contains `Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st` with thousands of lines of valid Smalltalk code. Use this file to validate grammar or parser changes and confirm syntax assumptions. Note that the WS lexer rule drops whitespace so there is never any need to add a whitespace production in the parser.
- **Installing powershell**: This will have been done when setting up the Environment:
1. Update package index
sudo apt update
2. Install prerequisites
sudo apt install -y wget apt-transport-https software-properties-common
3. Import Microsoft’s GPG key
wget -q https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
4. Update again to load the Microsoft repo
sudo apt update
5. Install PowerShell
sudo apt install -y powershell
6. Run PowerShell
Use the 'pwsh' command to run Powershell

These guidelines apply to all future contributions unless otherwise specified.
