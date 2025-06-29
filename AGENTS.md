# Contribution Guidelines

This project contains a prototype of a Smalltalk-80 interpreter written in C#. When making changes keep the following conventions in mind:

- **Language version**: Target modern C# (v13 or later) where possible. Prefer features such as pattern matching enhancements, record types, and top-level statements when appropriate.
- **Documentation**: Every public class, property, method, and enum member must include XML documentation comments describing its purpose and usage.
- **Testing**: Run `dotnet build --no-restore` and `dotnet test --no-build` before every commit to verify that the code compiles and all tests pass.
- **Code style**: Follow the existing formatting conventions. Use expression-bodied members when they improve readability and avoid unnecessary verbosity.
- **Repository structure**: Keep implementation code under the `Smalltalk80` directory and tests under `Smalltalk80.Tests`.

These guidelines apply to all future contributions unless otherwise specified.
