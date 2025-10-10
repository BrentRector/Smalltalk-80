#!/usr/bin/env pwsh
#
# Smalltalk-80 Build and Validation Script
# =========================================
#
# Automates the full build pipeline:
# 1. Build Compiler
# 2. Generate coldstart.json from Smalltalk-80.st
# 3. Run Standalone Validator
# 4. Report Results
#
# Usage:
#   pwsh scripts/build-and-validate.ps1
#

param(
    [switch]$SkipBuild = $false,
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot/..

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Smalltalk-80 Build & Validation Pipeline" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build Compiler
if (-not $SkipBuild) {
    Write-Host "[1/4] Building Compiler..." -ForegroundColor Yellow
    dotnet build Smalltalk80.Compiler/Smalltalk80.Compiler.csproj --nologo --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Compiler build failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "  ✓ Compiler built successfully" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "[1/4] Skipping compiler build (--SkipBuild)" -ForegroundColor Gray
    Write-Host ""
}

# Step 2: Generate coldstart.json
Write-Host "[2/4] Generating coldstart.json..." -ForegroundColor Yellow
$sourceFile = "Smalltalk-80 Source/Smalltalk-80.st"
$outputFile = "bin/Debug/net9.0/coldstart.json"

if (-not (Test-Path $sourceFile)) {
    Write-Host "✗ Source file not found: $sourceFile" -ForegroundColor Red
    exit 1
}

Push-Location "Smalltalk-80 Source"
try {
    $compilerOutput = dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj --no-build -- Smalltalk-80.st 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Compiler failed to generate JSON!" -ForegroundColor Red
        Write-Host $compilerOutput
        exit 1
    }

    if ($Verbose) {
        Write-Host $compilerOutput
    }

    # Extract stats from compiler output
    $symbolCount = ($compilerOutput | Select-String "Symbols: (\d+)").Matches.Groups[1].Value
    $classCount = ($compilerOutput | Select-String "Classes: (\d+)").Matches.Groups[1].Value
    $methodCount = ($compilerOutput | Select-String "Methods: (\d+)").Matches.Groups[1].Value

    Write-Host "  ✓ Generated coldstart.json" -ForegroundColor Green
    Write-Host "    Symbols: $symbolCount | Classes: $classCount | Methods: $methodCount" -ForegroundColor Gray
    Write-Host ""
} finally {
    Pop-Location
}

# Step 3: Build & Run Standalone Validator
Write-Host "[3/4] Running Standalone Validator..." -ForegroundColor Yellow

if (-not $SkipBuild) {
    dotnet build Smalltalk80.Validator/Smalltalk80.Validator.csproj --nologo --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Validator build failed!" -ForegroundColor Red
        exit 1
    }
}

$validatorArgs = @($outputFile)
if ($Verbose) {
    $validatorArgs += "--verbose"
}

$validatorOutput = dotnet run --project Smalltalk80.Validator/Smalltalk80.Validator.csproj --no-build -- @validatorArgs 2>&1
$validatorExitCode = $LASTEXITCODE

if ($Verbose) {
    Write-Host $validatorOutput
}

# Step 4: Parse and Report Results
Write-Host ""
Write-Host "[4/4] Validation Results:" -ForegroundColor Yellow
Write-Host ""

# Extract validation summary
$validatorOutput | Select-String -Pattern "MethodHeaderValidator|BytecodeValidator|LiteralFrameValidator|PatchValidator|InstallOrderValidator|ObjectFormatValidator" | ForEach-Object {
    $line = $_.Line
    if ($line -match "✓") {
        Write-Host "  $line" -ForegroundColor Green
    } elseif ($line -match "✗") {
        Write-Host "  $line" -ForegroundColor Red
    } else {
        Write-Host "  $line"
    }
}

Write-Host ""

# Check if validation passed
if ($validatorExitCode -eq 0) {
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "✓ BUILD AND VALIDATION SUCCESSFUL" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    exit 0
} else {
    # Extract error summary
    $errorLine = $validatorOutput | Select-String -Pattern "Validation FAILED \((\d+) errors\)"
    if ($errorLine) {
        $errorCount = $errorLine.Matches.Groups[1].Value
        Write-Host "========================================" -ForegroundColor Red
        Write-Host "✗ VALIDATION FAILED ($errorCount errors)" -ForegroundColor Red
        Write-Host "========================================" -ForegroundColor Red
        Write-Host ""
        Write-Host "Run with full output to see details:" -ForegroundColor Yellow
        Write-Host "  dotnet run --project Smalltalk80.Validator/Smalltalk80.Validator.csproj -- $outputFile" -ForegroundColor Gray
    } else {
        Write-Host "========================================" -ForegroundColor Red
        Write-Host "✗ VALIDATION FAILED" -ForegroundColor Red
        Write-Host "========================================" -ForegroundColor Red
    }
    exit 1
}
