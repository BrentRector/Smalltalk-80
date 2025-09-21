#!/bin/bash
#
# Build script for the Smalltalk-80 Compiler (Linux/macOS)
#

set -e

echo "Smalltalk-80 Compiler Build Script"
echo "==================================="
echo

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
CONFIGURATION="Debug"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --clean)
            CLEAN=true
            shift
            ;;
        --rebuild)
            REBUILD=true
            shift
            ;;
        --release)
            CONFIGURATION="Release"
            shift
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Clean if requested
if [[ "$CLEAN" == "true" ]] || [[ "$REBUILD" == "true" ]]; then
    echo "Cleaning build artifacts..."
    dotnet clean -c "$CONFIGURATION"
    
    # Clean generated files
    GENERATED_DIR="$SCRIPT_DIR/Generated"
    if [ -d "$GENERATED_DIR" ]; then
        rm -rf "$GENERATED_DIR"
        echo "Removed Generated directory"
    fi
fi

# Generate parser files
echo "Generating ANTLR parser files..."
JAR_PATH="$SCRIPT_DIR/ANTLR4/antlr-4.13.2-complete.jar"
GRAMMAR_FILE="$SCRIPT_DIR/Smalltalk80.g4"
OUTPUT_DIR="$SCRIPT_DIR/Generated"

if [ ! -f "$JAR_PATH" ]; then
    echo "Error: ANTLR JAR not found at: $JAR_PATH"
    exit 1
fi

if [ ! -f "$GRAMMAR_FILE" ]; then
    echo "Error: Grammar file not found at: $GRAMMAR_FILE"
    exit 1
fi

# Create output directory
mkdir -p "$OUTPUT_DIR"

# Run ANTLR
java -jar "$JAR_PATH" \
    -Dlanguage=CSharp \
    -no-listener -visitor \
    -package StandaloneCompiler \
    -o "$OUTPUT_DIR" \
    "$GRAMMAR_FILE"

if [ $? -ne 0 ]; then
    echo "Error: Failed to generate parser files"
    exit 1
fi

echo "Parser generation succeeded."

# Build the project
echo
echo "Building project ($CONFIGURATION)..."
dotnet build -c "$CONFIGURATION"

if [ $? -ne 0 ]; then
    echo "Error: Build failed"
    exit 1
fi

echo
echo "Build completed successfully!"
echo "Executable: bin/$CONFIGURATION/net9.0/Smalltalk80.Compiler"