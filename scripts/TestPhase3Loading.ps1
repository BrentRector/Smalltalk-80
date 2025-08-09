# Test loading all Phase 3 files
param(
    [string]$Phase3Dir = "Bootstrap\phase3_final"
)

Write-Host "Testing Phase 3 Loading" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan
Write-Host ""

# Build the command with all JSON files
$jsonFiles = Get-ChildItem -Path $Phase3Dir -Filter "*.json" | ForEach-Object {
    "--json `"$($_.FullName)`""
}

$jsonArgs = $jsonFiles -join " "

# Create the full command
$cmd = ".\bin\Debug\net9.0\Smalltalk80.Interpreter.exe --skip-phase3 $jsonArgs"

Write-Host "Loading Phase 3 files:" -ForegroundColor Yellow
Get-ChildItem -Path $Phase3Dir -Filter "*.json" | ForEach-Object {
    Write-Host "  - $($_.Name)"
}
Write-Host ""

# Execute the command
Write-Host "Executing interpreter..." -ForegroundColor Yellow
Invoke-Expression $cmd