@echo off
echo Testing Smalltalk-80 Interpreter with automatic cold start...
echo.
echo Test 1: Simple arithmetic
dotnet run --project Smalltalk80.Interpreter -- eval "3 + 4"
echo.
echo Test 2: With cold start logging
dotnet run --project Smalltalk80.Interpreter -- eval "10 * 5" --cold-start-log 2>nul | findstr /C:"[COLD START]" | find /c "Created 56"
echo.
echo Test 3: Run command
dotnet run --project Smalltalk80.Interpreter -- run -e "100 / 4"
echo.
echo All tests complete!