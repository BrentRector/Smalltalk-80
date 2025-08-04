# Smalltalk-80 Parser Test Cases

This directory contains systematic test cases for the Smalltalk-80 parser, organized by complexity level to ensure proper dependency management and comprehensive coverage.

## Test Structure

The test cases are organized into levels that build upon each other:

### Level 1: Basic Class Definitions
- `Level1-BasicClasses/` - Empty classes, classes with variables, inheritance
- Tests fundamental class definition parsing
- Validates instance variables, class variables, categories
- Ensures proper superclass relationships

### Level 2: Simple Methods  
- `Level2-SimpleMethods/` - Unary, binary, and keyword methods
- Tests method header parsing
- Validates method selector extraction
- Basic method body parsing (simple returns)

### Level 3: Methods with Temporaries
- `Level3-MethodsWithTemporaries/` - Methods with temporary variables
- Tests temporary variable declarations
- Validates scope and naming
- Assignment and usage patterns

### Level 4: Instance Variable Access
- `Level4-InstanceVariables/` - Methods accessing instance variables
- Tests instance variable references
- Validates assignment to instance variables
- Complex methods combining temporaries and instance variables

### Level 5: Primitive Methods
- `Level5-Primitives/` - Methods with primitive declarations
- Tests primitive syntax parsing
- Validates primitive index extraction
- Fallback method bodies

### Level 6: Class Methods (Metaclass)
- `Level6-ClassMethods/` - Class-side method definitions
- Tests class method syntax
- Validates metaclass method installation
- Class variable access patterns

## Test Philosophy

Each level depends only on features validated in previous levels:
- Level 2 requires Level 1 (classes must exist to hold methods)
- Level 3 builds on Level 2 (method structure + temporaries)
- Level 4 builds on Level 1 & 3 (instance variables + method structure)
- And so on...

## Negative Test Cases

Failure cases are stored in `../Parser.FailCases/Systematic/` and test:
- Syntax errors (missing bangs, unclosed temporaries, etc.)
- Semantic errors (references to undefined classes, etc.)
- Invalid method structures

## Integration with SystematicParserTests

These test cases are consumed by the `SystematicParserTests` class in the test project, which:
1. Validates positive parsing cases
2. Ensures runtime structures are correctly built
3. Verifies class and method installation
4. Tests the complete parse → compile → install pipeline

## Adding New Test Cases

When adding new test cases:
1. Place them in the appropriate level directory
2. Ensure they only depend on features from the same or lower levels
3. Add corresponding test methods to `SystematicParserTests.cs`
4. Include both positive and negative variants where appropriate
