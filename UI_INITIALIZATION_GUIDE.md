# UI Initialization Configuration Guide

## Overview

The Smalltalk-80 interpreter now includes enhanced error detection and configuration options for handling UI-related initialization failures. This is particularly useful when running in headless environments or when UI classes like `BinaryChoice` are not available.

## Features

### 1. Configuration Options

The `InitializationConfig` class provides the following options:

- **`SkipUIInitializations`**: When `true`, initialization failures due to missing UI classes will be logged but not block execution.
- **`ContinueOnError`**: When `true`, all initialization errors are logged but execution continues.
- **`KnownUIClasses`**: A set of UI class names to check for during initialization.

### 2. Enhanced Error Detection

The initialization executor now:
- Detects when specific UI classes are missing
- Identifies UI-related initialization failures
- Provides detailed error analysis
- Reports missing UI components at the end of initialization

### 3. Error Reporting

After initialization completes, the system reports:
- Total number of successful vs failed initializations
- List of missing UI classes detected
- Summary of all initialization errors with detailed messages

## Usage

### Command Line

Use the `--no-ui` or `--headless` flag when starting the interpreter:

```bash
Smalltalk80.Interpreter --no-ui
```

This will:
- Skip UI-related initializations
- Continue execution even if UI classes are missing
- Log all skipped initializations

### Programmatic Configuration

#### Method 1: Using Convenience Method

```csharp
var vm = new VirtualMachine(initialMemorySize: 262144);
vm.SetSkipUIInitializations(true);
vm.LoadBootstrapFromJson("system_image.json");
```

#### Method 2: Using Configuration Object

```csharp
var vm = new VirtualMachine(initialMemorySize: 262144);

var config = new InitializationConfig
{
    SkipUIInitializations = true,
    ContinueOnError = true
};

// Optionally add custom UI classes to check
config.KnownUIClasses.Add("MyCustomView");

vm.InitializationConfig = config;
vm.LoadBootstrapFromJson("system_image.json");
```

## Known UI Classes

The system automatically checks for these common UI classes:

- `BinaryChoice` / `BinaryChoiceView`
- `FillInTheBlank` / `FillInTheBlankView`
- `ActionMenu` / `PopUpMenu`
- `StandardSystemView` / `StandardSystemController`
- `ListView` / `SelectionInListView`
- `PluggableTextView` / `CodeView`
- `TextCollectorView` / `FormView`
- `DisplayTextView`

## Error Analysis

The system analyzes initialization errors to determine their cause:

1. **Missing Class References**: Detects when a class referenced in initialization doesn't exist
2. **UI-Related Failures**: Identifies failures related to UI components
3. **Stack Overflow**: Detects infinite recursion issues
4. **Primitive Failures**: Identifies primitive operation failures

## Example Output

When running with `--no-ui`:

```
[VM] Configured to skip UI-related initializations
UI-related initializations will be skipped.

[InitExpr] Executing 150 initialization expressions...
[InitExpr] Skipping UI-related initialization 'BinaryChoice initialize': Missing UI class: BinaryChoice. Class not found
[InitExpr] Skipping UI-related initialization 'PopUpMenu initialize': Missing UI class: PopUpMenu. Class not found
...
[InitExpr] Completed 145/150 initialization expressions.
[InitExpr] Missing UI classes detected:
  - BinaryChoice
  - PopUpMenu
  - StandardSystemView
  - FormView
  - DisplayTextView
[InitExpr] Initialization errors summary:
  BinaryChoice initialize:
    - Missing UI class: BinaryChoice. Class not found
  PopUpMenu initialize:
    - Missing UI class: PopUpMenu. Class not found
  ...
```

## Benefits

1. **Headless Operation**: Run Smalltalk-80 on servers without UI dependencies
2. **Partial System Loading**: Load and run non-UI parts of the system
3. **Better Debugging**: Clear identification of missing dependencies
4. **Graceful Degradation**: System continues to function despite missing UI components

## Implementation Details

The changes are implemented in:

1. **`InitializationExpressionExecutor.cs`** (line 297 and 101):
   - Added error analysis methods
   - Enhanced error handling in `ExecuteExpression`
   - Added UI class detection
   - Improved error reporting

2. **`VirtualMachine.cs`**:
   - Added `InitializationConfig` property
   - Added `SetSkipUIInitializations()` convenience method
   - Pass configuration to initialization executor

3. **`Program.cs`**:
   - Added `--no-ui`/`--headless` command-line options
   - Updated usage documentation

## Troubleshooting

If you encounter UI-related initialization failures:

1. Run with `--no-ui` flag to skip UI initializations
2. Check the error report for missing UI classes
3. If needed, add custom UI classes to the `KnownUIClasses` set
4. Consider implementing stub versions of critical UI classes

## Future Enhancements

Potential improvements could include:
- Automatic stub generation for missing UI classes
- Configurable initialization ordering
- Dependency analysis for selective initialization
- UI class lazy loading