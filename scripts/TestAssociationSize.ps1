# Test script to check Association instance size calculation

Write-Host "Testing Association instance size calculation..." -ForegroundColor Cyan
Write-Host ""

# Run the interpreter to check Association objects
$interpreterPath = "../bin/Debug/net9.0/Smalltalk80.Interpreter.exe"

if (Test-Path $interpreterPath) {
    Write-Host "Running interpreter to inspect Association objects..."
    & $interpreterPath | Select-String -Pattern "Association" -Context 2,2
} else {
    Write-Host "Interpreter not found at: $interpreterPath" -ForegroundColor Red
    Write-Host "Building project first..."
    
    Push-Location ..
    dotnet build --no-restore
    Pop-Location
    
    if (Test-Path $interpreterPath) {
        Write-Host "Running interpreter to inspect Association objects..."
        & $interpreterPath | Select-String -Pattern "Association" -Context 2,2
    }
}