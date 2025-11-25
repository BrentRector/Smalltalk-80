#!/bin/bash
#
# Build and run Smalltalk-80 with tests
#
# Usage: ./runtests.sh [Debug|Release]
#

set -e

CONFIGURATION="${1:-Debug}"

echo "=== Smalltalk-80 Test Runner ==="
echo

# Build Compiler
echo "Building Compiler..."
dotnet build "Smalltalk80.Compiler/Smalltalk80.Compiler.csproj" -c "$CONFIGURATION"

# Generate coldstart_with_tests.json
echo
echo "Generating coldstart_with_tests.json..."

COMPILER_EXE="Smalltalk80.Compiler/bin/$CONFIGURATION/net9.0/Smalltalk80.Compiler.dll"
SOURCE_FILE1="Smalltalk-80 Source/Smalltalk-80.st"
SOURCE_FILE2="Smalltalk-80 Source/Smalltalk-80-with-tests.st"
OUTPUT_FILE="Smalltalk80.Compiler/bin/$CONFIGURATION/net9.0/coldstart_with_tests.json"

if [ ! -f "$SOURCE_FILE1" ]; then
    echo "Error: Source file not found: $SOURCE_FILE1"
    exit 1
fi

if [ ! -f "$SOURCE_FILE2" ]; then
    echo "Error: Test file not found: $SOURCE_FILE2"
    exit 1
fi

dotnet "$COMPILER_EXE" "$SOURCE_FILE1" "$SOURCE_FILE2" "$OUTPUT_FILE"

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
