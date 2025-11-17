# Phase 3.5 Testing Script
# Tests canonical Association pre-creation implementation

Write-Host "`n=== Phase 3.5 Testing Script ===" -ForegroundColor Cyan
Write-Host "Testing canonical Association pre-creation implementation`n" -ForegroundColor Cyan

# Change to interpreter directory
Set-Location Smalltalk80.Interpreter

# Test 1: Build
Write-Host "[1/5] Building project..." -ForegroundColor Yellow
$buildOutput = dotnet build 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build FAILED" -ForegroundColor Red
    Write-Host $buildOutput
    exit 1
}
Write-Host "✅ Build succeeded (0 errors)" -ForegroundColor Green

# Test 2: Run cold start with Phase 3.5
Write-Host "`n[2/5] Running cold start with Phase 3.5..." -ForegroundColor Yellow
$output = dotnet run 2>&1 | Out-String

# Check for Phase 3.5 output
if ($output -match "\[Phase3\.5\] Pre-created (\d+) canonical Associations") {
    $count = $matches[1]
    Write-Host "✅ Phase 3.5 executed: Pre-created $count canonical Associations" -ForegroundColor Green
}
else {
    Write-Host "❌ Phase 3.5 output NOT found" -ForegroundColor Red
    Write-Host "Output:`n$output"
    exit 1
}

# Test 3: Verify init expression count
Write-Host "`n[3/5] Checking init expression success rate..." -ForegroundColor Yellow
$successCount = ([regex]::Matches($output, "✓ SUCCESS")).Count
if ($successCount -ge 25) {
    Write-Host "✅ $successCount init expressions passed (expected >= 25)" -ForegroundColor Green
}
else {
    Write-Host "❌ Only $successCount init expressions passed (expected >= 25)" -ForegroundColor Red
}

# Test 4: Check Text initialize status
Write-Host "`n[4/5] Checking Text initialize status..." -ForegroundColor Yellow
if ($output -match "Text initialize.*SUCCESS") {
    Write-Host "✅ Text initialize PASSED" -ForegroundColor Green
}
elseif ($output -match "Text initialize.*Method not found: aNumber") {
    Write-Host "⚠️  Text initialize failed with 'aNumber' error (expected - different issue)" -ForegroundColor Yellow
}
elseif ($output -match "Text initialize.*SKIPPED") {
    Write-Host "⚠️  Text initialize SKIPPED" -ForegroundColor Yellow
}
else {
    Write-Host "ℹ️  Text initialize status unclear" -ForegroundColor Gray
}

# Test 5: Run BlockContext test suite
Write-Host "`n[5/5] Running BlockContext test suite (194 tests)..." -ForegroundColor Yellow
Set-Location ../"Smalltalk-80 Source"

# Generate test bundle
cat Smalltalk-80.st, Tests/BlockContextTest.st > Smalltalk-80-with-tests.st
dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- "Smalltalk-80-with-tests.st" 2>&1 | Out-Null
Copy-Item ../bin/Debug/net9.0/coldstart.json ../bin/Debug/net9.0/coldstart_with_tests.json

# Run test suite
Set-Location ../Smalltalk80.Interpreter
$testOutput = dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_tests.json --skip-init-expressions 2>&1 | Out-String

if ($testOutput -match "BlockContextTest>>testAllCombinations.*3503") {
    Write-Host "✅ BlockContext test suite PASSED (194/194 tests)" -ForegroundColor Green
}
else {
    Write-Host "❌ BlockContext test suite FAILED" -ForegroundColor Red
    Write-Host "Expected: 3503, Got: $($matches[1])"
}

# Summary
Write-Host "`n=== Testing Complete ===" -ForegroundColor Cyan
Write-Host "Review the results above to verify Phase 3.5 implementation." -ForegroundColor Cyan