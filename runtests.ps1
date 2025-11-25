<#
.SYNOPSIS
  Build and run Smalltalk-80 with tests

.DESCRIPTION
  Builds the Compiler, generates coldstart_with_tests.json from Smalltalk-80.st + Smalltalk-80-with-tests.st,
  copies to interpreter directory, and runs the interpreter.

.PARAMETER Configuration
  Build configuration (Debug or Release). Default: Debug

.EXAMPLE
  .\runtests.ps1
  .\runtests.ps1 -Configuration Release
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Smalltalk-80 Test Runner ===" -ForegroundColor Cyan
Write-Host

# Build Compiler
Write-Host "Building Compiler..." -ForegroundColor Yellow
dotnet build "Smalltalk80.Compiler/Smalltalk80.Compiler.csproj" -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Error "Compiler build failed"
    exit 1
}

# Generate coldstart_with_tests.json
Write-Host
Write-Host "Generating coldstart_with_tests.json..." -ForegroundColor Yellow

$CompilerExe = "Smalltalk80.Compiler/bin/$Configuration/net9.0/Smalltalk80.Compiler.dll"
$SourceFile1 = "Smalltalk-80 Source/Smalltalk-80.st"
$SourceFile2 = "Smalltalk-80 Source/Smalltalk-80-with-tests.st"
$OutputFile = "Smalltalk80.Compiler/bin/$Configuration/net9.0/coldstart_with_tests.json"

if (-not (Test-Path $SourceFile1)) {
    Write-Error "Source file not found: $SourceFile1"
    exit 1
}

if (-not (Test-Path $SourceFile2)) {
    Write-Error "Test file not found: $SourceFile2"
    exit 1
}

dotnet $CompilerExe $SourceFile1 $SourceFile2 $OutputFile
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
