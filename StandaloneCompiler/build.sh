#!/bin/bash
# Bash build script for StandaloneCompiler
# This script builds the compiler without requiring the solution file

echo "Building Standalone Smalltalk-80 Compiler..."

# Clean previous build
if [ -d "obj" ]; then
    echo "Cleaning obj directory..."
    rm -rf obj
fi

# Build the project
echo "Building project..."
dotnet build --configuration Release

if [ $? -eq 0 ]; then
    echo "Build successful!"
    echo "Executable location: bin/Release/net9.0/StandaloneCompiler"
else
    echo "Build failed!"
    exit 1
fi