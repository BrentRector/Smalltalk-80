@echo off
REM run-tests.bat
REM Batch file wrapper for PowerShell test runner

setlocal enabledelayedexpansion

REM Check if PowerShell is available
powershell -Command "exit 0" >nul 2>&1
if errorlevel 1 (
    echo Error: PowerShell is required but not found.
    exit /b 1
)

REM Get the directory of this batch file
set "SCRIPT_DIR=%~dp0"
set "PS_SCRIPT=%SCRIPT_DIR%Run-ComprehensiveTests.ps1"

REM Check if PowerShell script exists
if not exist "%PS_SCRIPT%" (
    echo Error: PowerShell script not found: %PS_SCRIPT%
    exit /b 1
)

REM Parse arguments
set "ARGS="
if "%1"=="" (
    set "ARGS=-TestCategory All"
) else if "%1"=="--help" (
    goto :show_help
) else if "%1"=="-h" (
    goto :show_help
) else (
    set "ARGS=%*"
)

echo Running Smalltalk-80 Comprehensive Tests...
echo.

REM Execute PowerShell script
powershell -ExecutionPolicy Bypass -File "%PS_SCRIPT%" %ARGS%
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if %EXIT_CODE%==0 (
    echo ✅ Tests completed successfully
) else (
    echo ❌ Tests failed
)

exit /b %EXIT_CODE%

:show_help
echo.
echo Smalltalk-80 Test Runner
echo.
echo Usage: run-tests.bat [options]
echo.
echo Options:
echo   -TestCategory [category]    Run specific test category (default: All)
echo   -Verbose                    Show detailed output
echo   -StopOnFirstFailure        Stop on first test failure
echo   -OutputPath [file]         Custom log file path
echo   --help, -h                 Show this help
echo.
echo Available test categories:
echo   All, ObjectTests, BooleanTests, NumberTests, CharacterTests,
echo   UndefinedObjectTests, CollectionTests, StringTests, BlockContextTests,
echo   StandardLibraryTestSuite
echo.
echo Examples:
echo   run-tests.bat
echo   run-tests.bat -TestCategory ObjectTests
echo   run-tests.bat -TestCategory All -Verbose
echo.
exit /b 0
