<#
.SYNOPSIS
  Build Smalltalk-80 cold start bundle with BlockContextTestRunner

.DESCRIPTION
  Builds the Compiler and generates coldstart_with_runner.json from:
    - Smalltalk-80.st (base system)
    - BlockContextTest_full.st (test methods + 9 diagnostic methods)
    - BlockContextDiagnostics.st (single class with 9 test methods)

  Output: Smalltalk80.Compiler/bin/Debug/net9.0/coldstart_with_runner.json

.PARAMETER Configuration
  Build configuration (Debug or Release). Default: Debug

.EXAMPLE
  .\build-with-runner.ps1
  .\build-with-runner.ps1 -Configuration Release
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Smalltalk-80 Build with Test Runner ===" -ForegroundColor Cyan
Write-Host

# Build Compiler
Write-Host "Building Compiler..." -ForegroundColor Yellow
dotnet build "Smalltalk80.Compiler/Smalltalk80.Compiler.csproj" -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Error "Compiler build failed"
    exit 1
}

# Generate combined source file
Write-Host
Write-Host "Creating combined source file..." -ForegroundColor Yellow

$SourceDir = "Smalltalk-80 Source"
$BaseFile = "$SourceDir/Smalltalk-80.st"
$TestFile = "$SourceDir/Tests/BlockContextTest_full.st"
$DiagnosticsFile = "$SourceDir/Tests/BlockContextDiagnostics.st"
$CombinedFile = "$SourceDir/Smalltalk-80-with-runner.st"

if (-not (Test-Path $BaseFile)) {
    Write-Error "Base file not found: $BaseFile"
    exit 1
}

if (-not (Test-Path $TestFile)) {
    Write-Error "Test file not found: $TestFile"
    exit 1
}

if (-not (Test-Path $DiagnosticsFile)) {
    Write-Error "Diagnostics file not found: $DiagnosticsFile"
    exit 1
}

# Combine files
Get-Content $BaseFile, $TestFile, $DiagnosticsFile | Set-Content $CombinedFile
Write-Host "  Created: $CombinedFile" -ForegroundColor Green

# Compile to coldstart.json
Write-Host
Write-Host "Compiling Smalltalk source to coldstart.json..." -ForegroundColor Yellow

$CompilerExe = "Smalltalk80.Compiler/bin/$Configuration/net9.0/Smalltalk80.Compiler.dll"
dotnet $CompilerExe $CombinedFile
if ($LASTEXITCODE -ne 0) {
    Write-Error "Compilation failed"
    exit 1
}

# Copy to coldstart_with_runner.json
$ColdstartSrc = "Smalltalk80.Compiler/bin/$Configuration/net9.0/coldstart.json"
$ColdstartDst = "Smalltalk80.Compiler/bin/$Configuration/net9.0/coldstart_with_runner.json"

Write-Host
Write-Host "Copying to coldstart_with_runner.json..." -ForegroundColor Yellow
Copy-Item $ColdstartSrc $ColdstartDst -Force
Write-Host "  Created: $ColdstartDst" -ForegroundColor Green

Write-Host
Write-Host "=== Build Complete ===" -ForegroundColor Green
Write-Host "Output: $ColdstartDst" -ForegroundColor Green
Write-Host

Write-Host "To run the interpreter with test runner:" -ForegroundColor Cyan
Write-Host "  cd Smalltalk80.Interpreter" -ForegroundColor White
Write-Host "  dotnet run -- --coldstart ../Smalltalk80.Compiler/bin/$Configuration/net9.0/coldstart_with_runner.json" -ForegroundColor White
Write-Host

Write-Host "Expected output (9 diagnostic tests):" -ForegroundColor Yellow
Write-Host "  BlockContextDiagnostics testOa... Returned: 11" -ForegroundColor White
Write-Host "  BlockContextDiagnostics testOt1... Returned: 22" -ForegroundColor White
Write-Host "  BlockContextDiagnostics testOt2... Returned: 44" -ForegroundColor White
Write-Host "  BlockContextDiagnostics testMa... Returned: 88" -ForegroundColor White
Write-Host "  BlockContextDiagnostics testMt1... Returned: 166" -ForegroundColor White
Write-Host "  BlockContextDiagnostics testMt2... Returned: 322" -ForegroundColor White
Write-Host "  BlockContextDiagnostics testIa... Returned: 644" -ForegroundColor White
Write-Host "  BlockContextDiagnostics testIt1... Returned: 188" -ForegroundColor White
Write-Host "  BlockContextDiagnostics testIt2... Returned: 266" -ForegroundColor White
Write-Host
