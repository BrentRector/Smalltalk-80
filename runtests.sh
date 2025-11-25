#!/bin/bash
#
# Build and run Smalltalk-80 with specified test file(s)
#
# Usage: ./runtests.sh [test_file1.st] [test_file2.st] ...
#
# If no test files specified, defaults to "Smalltalk-80 Source/Tests/GraphicsTest.st"
#
# Examples:
#   ./runtests.sh
#   ./runtests.sh "Smalltalk-80 Source/Tests/BlockContextTest_full.st"
#   ./runtests.sh "Smalltalk-80 Source/Tests/BlockContextTest_full.st" "Smalltalk-80 Source/Tests/GraphicsTest.st"
#

set -e

CONFIGURATION="Debug"
TEST_FILES=()

# Parse arguments - collect all .st files as test files
for arg in "$@"; do
    if [[ "$arg" == *.st ]]; then
        TEST_FILES+=("$arg")
    elif [[ "$arg" == "Release" || "$arg" == "Debug" ]]; then
        CONFIGURATION="$arg"
    fi
done

# Default test file if none specified
if [ ${#TEST_FILES[@]} -eq 0 ]; then
    TEST_FILES=("Smalltalk-80 Source/Tests/GraphicsTest.st")
fi

echo "=== Smalltalk-80 Test Runner ==="
echo

# Build Compiler
echo "Building Compiler..."
dotnet build "Smalltalk80.Compiler/Smalltalk80.Compiler.csproj" -c "$CONFIGURATION"

# Verify source files exist
SOURCE_FILE="Smalltalk-80 Source/Smalltalk-80.st"
if [ ! -f "$SOURCE_FILE" ]; then
    echo "Error: Source file not found: $SOURCE_FILE"
    exit 1
fi

for TEST_FILE in "${TEST_FILES[@]}"; do
    if [ ! -f "$TEST_FILE" ]; then
        echo "Error: Test file not found: $TEST_FILE"
        exit 1
    fi
done

# Generate coldstart_with_tests.json
echo
echo "Generating coldstart_with_tests.json..."
echo "  Source: $SOURCE_FILE"
for TEST_FILE in "${TEST_FILES[@]}"; do
    echo "  Test:   $TEST_FILE"
done

COMPILER_EXE="Smalltalk80.Compiler/bin/$CONFIGURATION/net9.0/Smalltalk80.Compiler.dll"
OUTPUT_FILE="Smalltalk80.Compiler/bin/$CONFIGURATION/net9.0/coldstart_with_tests.json"

# Build compiler arguments: source file, test files, output file
dotnet "$COMPILER_EXE" "$SOURCE_FILE" "${TEST_FILES[@]}" "$OUTPUT_FILE"

# Copy to interpreter directory (both Linux and Windows paths)
echo
echo "Copying to interpreter directory..."
INTERPRETER_DIR_LINUX="Smalltalk80.Interpreter/bin/$CONFIGURATION/net9.0"
INTERPRETER_DIR_WINDOWS="Smalltalk80.Interpreter/bin/$CONFIGURATION/net9.0-windows"

mkdir -p "$INTERPRETER_DIR_LINUX" 2>/dev/null || true
mkdir -p "$INTERPRETER_DIR_WINDOWS" 2>/dev/null || true
cp "$OUTPUT_FILE" "$INTERPRETER_DIR_LINUX/coldstart_with_tests.json" 2>/dev/null || true
cp "$OUTPUT_FILE" "$INTERPRETER_DIR_WINDOWS/coldstart_with_tests.json" 2>/dev/null || true

# Run interpreter
echo
echo "=== Running Interpreter with Tests ==="
echo

cd Smalltalk80.Interpreter
dotnet run -- --coldstart "../Smalltalk80.Compiler/bin/$CONFIGURATION/net9.0/coldstart_with_tests.json"
EXIT_CODE=$?
cd ..

echo
if [ $EXIT_CODE -eq 0 ]; then
    echo "=== Tests Complete ==="
else
    echo "=== Tests Failed (exit code: $EXIT_CODE) ==="
fi

exit $EXIT_CODE
