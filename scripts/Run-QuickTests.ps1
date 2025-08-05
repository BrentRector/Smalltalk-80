# Run-QuickTests.ps1
# Quick test runner for individual Smalltalk-80 tests
# Usage: .\scripts\Run-QuickTests.ps1 -TestName "ObjectTests"

param(
    [Parameter(Mandatory=$true)]
    [string]$TestName,
    [switch]$VerboseOutput
)

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$SmalltalkProject = "Smalltalk80/Smalltalk80.csproj"
$TestLibrary = "Testing/TestLibrary.st"
$TestFile = "Testing/$TestName.st"

Push-Location $ProjectRoot

try {
    Write-Host "Running $TestName..." -ForegroundColor Cyan
    
    # Check if test file exists
    if (-not (Test-Path $TestFile)) {
        Write-Host "Error: Test file not found: $TestFile" -ForegroundColor Red
        exit 1
    }
    
    # Build command with proper dependency order
    $Command = "dotnet"
    $Arguments = @(
        "run",
        "--project", $SmalltalkProject,
        "--",
        "--source", $TestLibrary,
        "--source", $TestFile
    )
    
    if ($VerboseOutput) {
        Write-Host "Executing: $Command $($Arguments -join ' ')" -ForegroundColor Yellow
    }
    
    # Execute the test
    & $Command @Arguments
    
    $ExitCode = $LASTEXITCODE
    if ($ExitCode -eq 0) {
        Write-Host "✅ Test completed successfully" -ForegroundColor Green
    } else {
        Write-Host "❌ Test failed with exit code: $ExitCode" -ForegroundColor Red
    }
    
    exit $ExitCode
    
} finally {
    Pop-Location
}
