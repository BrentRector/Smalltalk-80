<#
.SYNOPSIS
  Build standard Smalltalk-80 cold start bundle

.DESCRIPTION
  Builds the Compiler and generates coldstart.json from Smalltalk-80.st
  Output: Smalltalk80.Compiler/bin/Debug/net9.0/coldstart.json

.PARAMETER Configuration
  Build configuration (Debug or Release). Default: Debug

.EXAMPLE
  .\build-standard.ps1
  .\build-standard.ps1 -Configuration Release
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Smalltalk-80 Standard Build ===" -ForegroundColor Cyan
Write-Host

# Build Compiler
Write-Host "Building Compiler..." -ForegroundColor Yellow
dotnet build "Smalltalk80.Compiler/Smalltalk80.Compiler.csproj" -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Error "Compiler build failed"
    exit 1
}

# Generate coldstart.json
Write-Host
Write-Host "Generating standard coldstart.json..." -ForegroundColor Yellow

$CompilerExe = "Smalltalk80.Compiler/bin/$Configuration/net9.0/Smalltalk80.Compiler.dll"
$SourceFile = "Smalltalk-80 Source/Smalltalk-80.st"
$OutputFile = "Smalltalk80.Compiler/bin/$Configuration/net9.0/coldstart.json"

if (-not (Test-Path $SourceFile)) {
    Write-Error "Source file not found: $SourceFile"
    exit 1
}

dotnet $CompilerExe $SourceFile $OutputFile
if ($LASTEXITCODE -ne 0) {
    Write-Error "coldstart.json generation failed"
    exit 1
}

Write-Host
Write-Host "=== Build Complete ===" -ForegroundColor Green
Write-Host "Output: $OutputFile" -ForegroundColor Green
Write-Host
Write-Host "To run the interpreter:" -ForegroundColor Cyan
Write-Host "  cd Smalltalk80.Interpreter" -ForegroundColor White
Write-Host "  dotnet run -- --coldstart ../Smalltalk80.Compiler/bin/$Configuration/net9.0/coldstart.json" -ForegroundColor White
