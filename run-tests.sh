#!/bin/bash
# Run the Smalltalk-80 test suite on demand

TIMEOUT_SECONDS=${1:-120}

echo -e "\033[36mRunning Smalltalk-80 Test Suite\033[0m"
echo -e "\033[36m================================\033[0m"

# First ensure everything is built
echo -e "\033[33mBuilding projects...\033[0m"
dotnet build --no-restore

# Run the bytecode tests directly with timeout
echo -e "\n\033[33mRunning bytecode tests...\033[0m"

timeout $TIMEOUT_SECONDS dotnet run --project Smalltalk80.Tests --no-build

EXIT_CODE=$?

if [ $EXIT_CODE -eq 0 ]; then
    echo -e "\n\033[32mTest run complete!\033[0m"
elif [ $EXIT_CODE -eq 124 ]; then
    echo -e "\n\033[31mTest execution timed out after $TIMEOUT_SECONDS seconds!\033[0m"
    echo -e "\033[33mTest process terminated.\033[0m"
    exit 1
else
    echo -e "\n\033[31mTests failed with exit code: $EXIT_CODE\033[0m"
    exit $EXIT_CODE
fi