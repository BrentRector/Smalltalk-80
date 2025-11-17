<#
.SYNOPSIS
  Build Smalltalk-80 cold start bundle with BlockContextTestRunner

.DESCRIPTION
  Builds the Compiler and generates coldstart_with_runner.json from:
    - Smalltalk-80.st (base system)
    - BlockContextTest_full.st (194 test methods)
    - BlockContextTestRunner.st (auto-runs tests via initialize)

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
$RunnerFile = "$SourceDir/Tests/BlockContextTestRunner.st"
$CombinedFile = "$SourceDir/Smalltalk-80-with-runner.st"

if (-not (Test-Path $BaseFile)) {
    Write-Error "Base file not found: $BaseFile"
    exit 1
}

if (-not (Test-Path $TestFile)) {
    Write-Error "Test file not found: $TestFile"
    exit 1
}

if (-not (Test-Path $RunnerFile)) {
    Write-Error "Runner file not found: $RunnerFile"
    exit 1
}

# Combine files
Get-Content $BaseFile, $TestFile, $RunnerFile | Set-Content $CombinedFile
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

Write-Host "Expected output:" -ForegroundColor Yellow
Write-Host "  [282] BlockContextTestRunner initialize... ~12,000 instructions  ✓ SUCCESS - Returned: 3503" -ForegroundColor White
Write-Host "  (Return value 3503 = all 194 BlockContext tests passed)" -ForegroundColor Yellow
Write-Host
