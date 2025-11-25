<#
.SYNOPSIS
  Build and run Smalltalk-80 with specified test file(s)

.DESCRIPTION
  Builds the Compiler, generates coldstart_with_tests.json from Smalltalk-80.st plus
  the specified test file(s), and runs the interpreter.

.PARAMETER TestFiles
  One or more test files to include (relative to project root or absolute paths).
  Defaults to "Smalltalk-80 Source/Tests/GraphicsTest.st"

.PARAMETER Configuration
  Build configuration (Debug or Release). Default: Debug

.EXAMPLE
  .\runtests.ps1
  .\runtests.ps1 "Smalltalk-80 Source/Tests/BlockContextTest_full.st"
  .\runtests.ps1 "Smalltalk-80 Source/Tests/BlockContextTest_full.st" "Smalltalk-80 Source/Tests/GraphicsTest.st"
  .\runtests.ps1 -Configuration Release
#>

param(
    [Parameter(Position=0, ValueFromRemainingArguments=$true)]
    [string[]]$TestFiles,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

# Default test file if none specified
if (-not $TestFiles -or $TestFiles.Count -eq 0) {
    $TestFiles = @("Smalltalk-80 Source/Tests/GraphicsTest.st")
}

Write-Host "=== Smalltalk-80 Test Runner ===" -ForegroundColor Cyan
Write-Host

# Build Compiler
Write-Host "Building Compiler..." -ForegroundColor Yellow
dotnet build "Smalltalk80.Compiler/Smalltalk80.Compiler.csproj" -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Error "Compiler build failed"
    exit 1
}

# Verify source files exist
$SourceFile = "Smalltalk-80 Source/Smalltalk-80.st"
if (-not (Test-Path $SourceFile)) {
    Write-Error "Source file not found: $SourceFile"
    exit 1
}

foreach ($TestFile in $TestFiles) {
    if (-not (Test-Path $TestFile)) {
        Write-Error "Test file not found: $TestFile"
        exit 1
    }
}

# Generate coldstart_with_tests.json
Write-Host
Write-Host "Generating coldstart_with_tests.json..." -ForegroundColor Yellow
Write-Host "  Source: $SourceFile"
foreach ($TestFile in $TestFiles) {
    Write-Host "  Test:   $TestFile"
}

$CompilerExe = "Smalltalk80.Compiler/bin/$Configuration/net9.0/Smalltalk80.Compiler.dll"
$OutputFile = "Smalltalk80.Compiler/bin/$Configuration/net9.0/coldstart_with_tests.json"

# Build compiler arguments: source file, test files, output file
$CompilerArgs = @($SourceFile) + $TestFiles + @($OutputFile)

dotnet $CompilerExe @CompilerArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "coldstart_with_tests.json generation failed"
    exit 1
}

# Copy to interpreter directory
Write-Host
Write-Host "Copying to interpreter directory..." -ForegroundColor Yellow
$InterpreterDir = "Smalltalk80.Interpreter/bin/$Configuration/net9.0-windows"
if (-not (Test-Path $InterpreterDir)) {
    New-Item -ItemType Directory -Path $InterpreterDir -Force | Out-Null
}
Copy-Item $OutputFile "$InterpreterDir/coldstart_with_tests.json" -Force

# Run interpreter
Write-Host
Write-Host "=== Running Interpreter with Tests ===" -ForegroundColor Green
Write-Host

Set-Location Smalltalk80.Interpreter
dotnet run -- --coldstart ../Smalltalk80.Compiler/bin/$Configuration/net9.0/coldstart_with_tests.json
$exitCode = $LASTEXITCODE
Set-Location ..

Write-Host
if ($exitCode -eq 0) {
    Write-Host "=== Tests Complete ===" -ForegroundColor Green
} else {
    Write-Host "=== Tests Failed (exit code: $exitCode) ===" -ForegroundColor Red
}

exit $exitCode
