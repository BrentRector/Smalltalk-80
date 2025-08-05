# Testing Directory

This directory contains all test-related files for the Smalltalk-80 interpreter project.

## Core Testing Framework

- **TestLibrary.st** - Main testing framework with TestCase base class and assertion methods
- **BasicTestMethods.st** - Comprehensive test suite for basic functionality (arithmetic, boolean, collections, strings)
- **TestClass_Proper.st** - Example test class demonstrating proper Smalltalk-80 syntax

## Test Execution

- **test_framework_execution.st** - Script for executing framework tests
- **run_test.st** / **run_tests.st** - Test execution scripts

## Additional Test Files

### Framework Variants
- **TestLibrary_working.st** - Working version of test library
- **TestLibrary_debug.st** - Debug version of test library  
- **TestSuite_Complete.st** - Complete test suite implementation

### Specific Test Cases
- **test_basic_bootstrap.st** - Bootstrap functionality tests
- **test_boolean_flow.st** - Boolean logic and flow control tests
- **test_cascade.st** - Cascade message syntax tests
- **test_class_methods.st** - Class method testing
- **test_comment.st** - Comment parsing tests
- **test_comprehensive.st** - Comprehensive functionality tests
- **test_minimal_standard.st** - Minimal standard library tests
- **test_primitive_fallback.st** - Primitive fallback mechanism tests
- **test_simple.st** / **test_simple_methods.st** - Simple test cases
- **test_with_character.st** - Character class tests
- **test_with_magnitude.st** - Magnitude class tests

### Legacy Test Files
- **BasicTestClass.st** - Legacy basic test class
- **SimpleTestClass.st** - Legacy simple test class
- **TestClass.st** - Legacy test class
- **simple_test.st** / **simple_test_case.st** - Legacy simple tests
- **SmalltalkTestExecution.st** - Legacy test execution
- **debug_test.st** / **test_temp.st** - Debug and temporary test files

## Usage

To load the testing framework:

```smalltalk
"From Smalltalk-80 REPL or source loading"
LoadFile Testing/TestLibrary.st

"To run basic functionality tests"
LoadFile Testing/BasicTestMethods.st
test := BasicFunctionalityTests new.
test performTest.
```

## Framework Architecture

The testing framework follows standard xUnit patterns with:

- `TestCase` - Base class for all test cases
- `TestResult` - Captures test execution results  
- `TestFailure` - Represents test failures
- `TestSuite` - Groups multiple test cases
- `TestRunner` - Executes test suites

## Assertion Methods

- `assertEqual:to:` - Verify two values are equal
- `assertTrue:` - Verify condition is true
- `assertFalse:` - Verify condition is false
- `assertNotNil:` - Verify object is not nil
- `fail:` - Force test failure with message

All tests use authentic Smalltalk-80 syntax with underscore assignment (`_`) instead of `:=`.
