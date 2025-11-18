<#
.SYNOPSIS
  Build Smalltalk-80 cold start bundle with BlockContext tests

.DESCRIPTION
  Builds the Compiler and generates coldstart_with_tests.json from Smalltalk-80.st + BlockContextTest_full.st
  Output: Smalltalk80.Compiler/bin/Debug/net9.0/coldstart_with_tests.json

.PARAMETER Configuration
  Build configuration (Debug or Release). Default: Debug

.EXAMPLE
  .\build-with-tests.ps1
  .\build-with-tests.ps1 -Configuration Release
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Smalltalk-80 Build with Tests ===" -ForegroundColor Cyan
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
Write-Host "Generating coldstart_with_tests.json with BlockContextTest..." -ForegroundColor Yellow

$CompilerExe = "Smalltalk80.Compiler/bin/$Configuration/net9.0/Smalltalk80.Compiler.dll"
$SourceFile1 = "Smalltalk-80 Source/Smalltalk-80.st"
$SourceFile2 = "Smalltalk-80 Source/Tests/BlockContextTest_full.st"
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

Write-Host
Write-Host "=== Build Complete ===" -ForegroundColor Green
Write-Host "Output: $OutputFile" -ForegroundColor Green
Write-Host
Write-Host "To run the interpreter with tests:" -ForegroundColor Cyan
Write-Host "  cd Smalltalk80.Interpreter" -ForegroundColor White
Write-Host "  dotnet run -- --coldstart ../Smalltalk80.Compiler/bin/$Configuration/net9.0/coldstart_with_tests.json" -ForegroundColor White
Write-Host
Write-Host "NOTE: This bundle includes BlockContextTest but does NOT run tests automatically" -ForegroundColor Yellow
Write-Host "      (BlockContextTest has no initialize method)" -ForegroundColor Yellow
Write-Host "      To run tests automatically, use build-with-runner.ps1 instead" -ForegroundColor Yellow
