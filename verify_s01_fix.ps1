# Verify that s01 method compiles with correct bytecode
$ErrorActionPreference = "Stop"

Write-Host "Compiling s01 test..." -ForegroundColor Cyan
cd Smalltalk80.Compiler
dotnet run -- "..\Smalltalk-80 Source\Tests\BlockTest_s01.st" "..\bin\Debug\net9.0\test_s01.json" 2>&1 | Out-Null
cd ..

Write-Host "`nChecking compiled bytecode..." -ForegroundColor Cyan
$json = Get-Content "bin\Debug\net9.0\test_s01.json" | ConvertFrom-Json

# Find the s01 method
$s01Method = $json.methods | Where-Object { $_.methodLocalId -like "*>>s01" }

if ($s01Method) {
    Write-Host "Found s01 method: $($s01Method.methodLocalId)" -ForegroundColor Green
    Write-Host "  Argument count: $($s01Method.argumentCount)"
    Write-Host "  Temporary count: $($s01Method.temporaryCount)"
    Write-Host "  Bytecode length: $($s01Method.bytecode.Count)"
    $bytecodeStr = $s01Method.bytecode -join ', '
    Write-Host "  Bytecode: $bytecodeStr"

    if ($s01Method.temporaryCount -eq 1) {
        Write-Host ""
        Write-Host "SUCCESS: Method has 1 temporary (includes block temp)" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "FAIL: Expected 1 temporary, got $($s01Method.temporaryCount)" -ForegroundColor Red
    }
} else {
    Write-Host "FAIL: Could not find s01 method" -ForegroundColor Red
}

# Find the s00 method for comparison
$s00Method = $json.methods | Where-Object { $_.methodLocalId -like "*>>s00" }
if ($s00Method) {
    Write-Host ""
    Write-Host "Found s00 method (baseline): $($s00Method.methodLocalId)" -ForegroundColor Green
    Write-Host "  Temporary count: $($s00Method.temporaryCount)"
    $s00BytecodeStr = $s00Method.bytecode -join ', '
    Write-Host "  Bytecode: $s00BytecodeStr"
}
