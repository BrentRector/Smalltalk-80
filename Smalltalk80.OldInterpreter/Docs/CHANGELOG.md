# Changelog

All notable changes to the Smalltalk-80 Interpreter will be documented in this file.

## [Unreleased]

### Changed
- **BREAKING**: The interpreter now loads the complete Smalltalk-80 system image by default
  - Loads `complete_system_final.json` with all 223 classes and 4,493 methods
  - Default memory size increased from 64K to 256K objects to accommodate full system
  - Falls back to `bootstrap.json` (85 classes) if complete system not found
  - Removed `--test-complete-load` command line option (no longer needed)

### Technical Details
- Modified `Program.cs` to prioritize complete system image loading
- Updated `VirtualMachine` constructor to use 256K default memory size
- Deprecated `TestCompleteSystemLoad` class as functionality is now default
- System loads in ~0.12 seconds on modern hardware

### Benefits
- Full Smalltalk-80 environment available immediately
- Access to all standard library classes and methods
- Better compatibility with Smalltalk-80 code from books and examples
- No need for manual extended manifest loading

## [Previous Versions]

### Bootstrap-Only Version
- Loaded only 85 kernel classes from `bootstrap.json`
- Required manual loading of extended manifests for full system
- Memory size of 64K objects was sufficient