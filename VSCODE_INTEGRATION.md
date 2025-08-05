# VS Code Integration Guide

## Automated Build and Test Workflow

Your VS Code project is now configured to automatically run all test cases after successful compilation.

## Quick Start

1. **Open VS Code**: Open this folder in VS Code
2. **Build with Tests**: Press `Ctrl+Shift+B` (or `Cmd+Shift+B` on Mac)
3. **View Results**: Test results appear in the Terminal panel

## What Happens During Build

When you run the default build task (`Ctrl+Shift+B`), the following sequence executes:

```
1. ANTLR Parser Generation
   ↓
2. .NET Solution Build
   ↓  
3. Comprehensive Test Suite Execution
   ↓
4. Test Results Summary
```

## Task Details

### Available Tasks
- **`build`** - Main build task with integrated testing (default)
- **`build-and-test`** - Build only (without tests)
- **`run-tests`** - Run test suite independently  
- **`generate-antlr`** - Generate ANTLR files only
- **`run`** - Start the interpreter
- **`repl`** - Start REPL mode

### Running Tasks Manually

#### Via Command Palette
1. Press `Ctrl+Shift+P` (or `Cmd+Shift+P`)
2. Type "Tasks: Run Task"
3. Select the desired task

#### Via Keyboard Shortcuts
- `Ctrl+Shift+B` - Default build (with tests)
- `F5` - Debug mode (builds first)

## Test Execution Details

### Automatic Test Categories
The build process runs all 9 test categories:
- ObjectTests
- BooleanTests  
- NumberTests
- CharacterTests
- UndefinedObjectTests
- CollectionTests
- StringTests
- BlockContextTests
- StandardLibraryTestSuite

### Test Output
- ✅ **Green checkmarks** - Passed tests
- ❌ **Red X marks** - Failed tests
- **Summary statistics** - Total/Passed/Failed counts
- **Log files** - Timestamped detailed results

### Viewing Test Results
Test output appears in the VS Code Terminal panel with:
- Real-time execution progress
- Color-coded pass/fail indicators
- Comprehensive summary with success rate
- Log file location for detailed analysis

## Manual Test Execution

If you want to run tests without building:

### Quick Test Run
```bash
# Run all tests
.\scripts\Run-ComprehensiveTests.ps1

# Run specific category
.\scripts\Run-ComprehensiveTests.ps1 -TestCategory "ObjectTests"

# Verbose output
.\scripts\Run-ComprehensiveTests.ps1 -Verbose
```

### Individual Tests
```bash
.\scripts\Run-QuickTests.ps1 -TestName "BooleanTests"
```

## Troubleshooting

### Build Fails
- Check the Terminal output for compilation errors
- Ensure .NET 9.0 SDK is installed
- Run `dotnet --version` to verify

### Tests Fail
- Review test output in Terminal panel
- Check log files in project root (format: `test-results-YYYYMMDD-HHMMSS.log`)
- Run tests manually to isolate issues

### Task Not Found
- Reload VS Code window (`Ctrl+Shift+P` → "Developer: Reload Window")
- Verify `.vscode/tasks.json` file exists
- Check VS Code Extensions (C# Dev Kit recommended)

## Customization

### Modify Test Categories
Edit `scripts/Run-ComprehensiveTests.ps1` to adjust which tests run during build.

### Change Build Behavior
Edit `.vscode/tasks.json` to modify the build workflow:
- Remove test execution from build
- Add additional post-build steps
- Modify task dependencies

### Test Configuration
Edit individual test files in `Testing/` folder to modify test behavior.

## Benefits

✅ **Immediate Feedback** - Know instantly if changes break tests
✅ **Consistent Testing** - Every build validates the entire system
✅ **Developer Productivity** - No manual test execution needed
✅ **Quality Assurance** - Catches regressions early
✅ **CI/CD Ready** - Same workflow works in automated environments

## Example Workflow

1. Make code changes in your Smalltalk-80 interpreter
2. Press `Ctrl+Shift+B` to build
3. Watch as the system:
   - Generates ANTLR parser
   - Compiles the solution
   - Runs all 180+ test methods
   - Reports results
4. Fix any issues if tests fail
5. Repeat until all tests pass

Your VS Code environment now provides a professional development experience with integrated testing!
