# Sanity.ps1 - Compile and run BlockContext regression tests
# Expected result: Returns 1191 (120 tests pass)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$sourceDir = Join-Path $repoRoot "Smalltalk-80 Source"
$compilerProj = Join-Path $repoRoot "Smalltalk80.Compiler/Smalltalk80.Compiler.csproj"
$compilerOutput = Join-Path $repoRoot "Smalltalk80.Compiler/bin/Debug/net9.0/Data/coldstart.json"
$interpreterData = Join-Path $scriptDir "bin/Debug/net9.0-windows/Data/coldstart_with_runner.json"

Write-Host "=== Compiling Smalltalk source with test files ===" -ForegroundColor Cyan

Push-Location $sourceDir
try {
    dotnet run --project $compilerProj -- Smalltalk-80.st Tests/BlockContextTest.st Tests/BlockContextTestRunner.st
    if ($LASTEXITCODE -ne 0) {
        throw "Compiler failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

Write-Host "`n=== Copying coldstart bundle ===" -ForegroundColor Cyan
Copy-Item $compilerOutput $interpreterData -Force
Write-Host "Copied to: $interpreterData"

Write-Host "`n=== Running interpreter regression test ===" -ForegroundColor Cyan
Write-Host "Expected result: 1191 (120 tests pass)`n" -ForegroundColor Yellow

Push-Location $scriptDir
try {
    dotnet run -- --coldstart $interpreterData
    if ($LASTEXITCODE -ne 0) {
        throw "Interpreter failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

Write-Host "`n=== Sanity check complete ===" -ForegroundColor Green
