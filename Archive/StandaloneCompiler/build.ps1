# PowerShell build script for StandaloneCompiler
# This script builds the compiler without requiring the solution file

Write-Host "Building Standalone Smalltalk-80 Compiler..." -ForegroundColor Green

# Check if parser needs regeneration
Write-Host "Checking if parser needs regeneration..."
& $PSScriptRoot\GenerateIfNewer.ps1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Parser regeneration check failed!" -ForegroundColor Red
    exit 1
}

# Clean previous build
if (Test-Path "obj") {
    Write-Host "Cleaning obj directory..."
    Remove-Item -Recurse -Force obj
}

# Build the project
Write-Host "Building project..."
dotnet build --configuration Release

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build successful!" -ForegroundColor Green
    Write-Host "Executable location: bin\Release\net9.0\StandaloneCompiler.exe"
} else {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}