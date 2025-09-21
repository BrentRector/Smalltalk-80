<#
.SYNOPSIS
  Build script for the Smalltalk-80 Compiler.
.DESCRIPTION
  Generates ANTLR parser files and builds the compiler project.
#>

param(
    [switch]$Clean,
    [switch]$Rebuild,
    [switch]$Release
)

$ErrorActionPreference = "Stop"

Write-Host "Smalltalk-80 Compiler Build Script" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan
Write-Host

$Configuration = if ($Release) { "Release" } else { "Debug" }

# Clean if requested
if ($Clean -or $Rebuild) {
    Write-Host "Cleaning build artifacts..." -ForegroundColor Yellow
    dotnet clean -c $Configuration
    
    # Clean generated files
    $GeneratedDir = Join-Path $PSScriptRoot "Generated"
    if (Test-Path $GeneratedDir) {
        Remove-Item $GeneratedDir -Recurse -Force
        Write-Host "Removed Generated directory" -ForegroundColor Yellow
    }
}

# Generate parser files
Write-Host "Generating ANTLR parser files..." -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "Invoke-Antlr4CSharp.ps1")
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to generate parser files"
    exit 1
}

# Build the project
Write-Host
Write-Host "Building project ($Configuration)..." -ForegroundColor Cyan
dotnet build -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

Write-Host
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "Executable: bin\$Configuration\net9.0\Smalltalk80.Compiler.exe" -ForegroundColor Green