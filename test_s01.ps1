# Test s00 and s01 methods
$ErrorActionPreference = "Stop"

Write-Host "Building test bundle..." -ForegroundColor Cyan
cd Smalltalk80.Compiler
dotnet run -- "..\Smalltalk-80 Source\Smalltalk-80.st" "..\Smalltalk-80 Source\Tests\BlockTest_s01.st" "..\bin\Debug\net9.0\coldstart_test_s01.json" | Out-Null
cd ..

Write-Host "`nTesting s00 (working baseline)..." -ForegroundColor Cyan
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_test_s01.json --skip-init-expressions --execute "BlockContextTest new s00" 2>&1 | Select-String -Pattern "Result|Error|Exception" -Context 0,3

Write-Host "`nTesting s01 (previously buggy)..." -ForegroundColor Cyan
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_test_s01.json --skip-init-expressions --execute "BlockContextTest new s01" 2>&1 | Select-String -Pattern "Result|Error|Exception" -Context 0,3

cd ..
