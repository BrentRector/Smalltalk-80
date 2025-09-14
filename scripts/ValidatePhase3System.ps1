# Validate Phase 3 System Completeness
# This script checks that all Phase 3 classes and methods are properly loaded

$interpreterPath = "../bin/Debug/net9.0/Smalltalk80.Interpreter.exe"
$phase3Manifest = "../bin/Debug/net9.0/Bootstrap/phase3_minimal.json"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "PHASE 3 SYSTEM VALIDATION" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Check Phase 3 loads without errors
Write-Host "Test 1: Loading Phase 3 Bootstrap..." -ForegroundColor Yellow
$output = & $interpreterPath --extended-library Bootstrap/phase3_minimal.json --enhanced-print 2>&1 | Out-String

if ($output -match "Classes registered in Smalltalk: (\d+)") {
    $classCount = $matches[1]
    if ($classCount -eq "60") {
        Write-Host "  ✓ Correct number of classes: $classCount" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Wrong number of classes: $classCount (expected 60)" -ForegroundColor Red
    }
} else {
    Write-Host "  ✗ Could not determine class count" -ForegroundColor Red
}

# Test 2: Check critical Phase 3 classes exist
Write-Host ""
Write-Host "Test 2: Verifying Phase 3 Classes..." -ForegroundColor Yellow

$requiredClasses = @(
    "OrderedCollection",
    "Set", 
    "Interval",
    "SortedCollection",
    "SequenceableCollection",
    "Stream",
    "PositionableStream",
    "ReadStream",
    "WriteStream",
    "ReadWriteStream",
    "Scanner",
    "Parser",
    "Compiler"
)

$foundClasses = 0
foreach ($class in $requiredClasses) {
    if ($output -match "Class: $class\s") {
        Write-Host "  ✓ Found: $class" -ForegroundColor Green
        $foundClasses++
    } else {
        Write-Host "  ✗ Missing: $class" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "  Phase 3 Classes: $foundClasses/$($requiredClasses.Count)" -ForegroundColor Cyan

# Test 3: Check critical methods work
Write-Host ""
Write-Host "Test 3: Testing Critical Methods..." -ForegroundColor Yellow

$criticalOutput = & $interpreterPath --test-critical-methods 2>&1 | Out-String

if ($criticalOutput -match "Passed: (\d+)") {
    $passed = $matches[1]
    Write-Host "  ✓ Critical methods passed: $passed" -ForegroundColor Green
} else {
    Write-Host "  ✗ Could not run critical method tests" -ForegroundColor Red
}

# Test 4: Count total methods
Write-Host ""
Write-Host "Test 4: Counting Methods..." -ForegroundColor Yellow

$methodCounts = [regex]::Matches($output, "Methods \((\d+)\)")
$totalMethods = 0
foreach ($match in $methodCounts) {
    $totalMethods += [int]$match.Groups[1].Value
}

Write-Host "  Total methods across all classes: $totalMethods" -ForegroundColor Cyan

# Test 5: Check Phase 3 manifest structure
Write-Host ""
Write-Host "Test 5: Validating Phase 3 Manifest..." -ForegroundColor Yellow

if (Test-Path $phase3Manifest) {
    $manifest = Get-Content $phase3Manifest -Raw | ConvertFrom-Json
    
    $manifestClasses = $manifest.classes.Count
    $manifestMethods = $manifest.methods.Count
    
    Write-Host "  Manifest contains:" -ForegroundColor Cyan
    Write-Host "    - Classes: $manifestClasses" -ForegroundColor Gray
    Write-Host "    - Methods: $manifestMethods" -ForegroundColor Gray
    
    if ($manifestClasses -eq 13 -and $manifestMethods -ge 270) {
        Write-Host "  ✓ Manifest structure correct" -ForegroundColor Green
    } else {
        Write-Host "  ⚠ Manifest may need updating" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ✗ Phase 3 manifest not found" -ForegroundColor Red
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "VALIDATION SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$testsRun = 5
$testsPassed = 0

if ($classCount -eq "60") { $testsPassed++ }
if ($foundClasses -eq $requiredClasses.Count) { $testsPassed++ }
if ($passed -ge 7) { $testsPassed++ }
if ($totalMethods -gt 500) { $testsPassed++ }
if ($manifestClasses -eq 13) { $testsPassed++ }

Write-Host ""
Write-Host "Tests Passed: $testsPassed/$testsRun" -ForegroundColor $(if ($testsPassed -eq $testsRun) { "Green" } else { "Yellow" })

if ($testsPassed -eq $testsRun) {
    Write-Host ""
    Write-Host "✅ PHASE 3 SYSTEM IS FULLY FUNCTIONAL!" -ForegroundColor Green
    Write-Host "The system has all required classes and methods for self-hosting." -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "⚠ PHASE 3 SYSTEM NEEDS ATTENTION" -ForegroundColor Yellow
    Write-Host "Review the failures above for details." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Key Statistics:" -ForegroundColor Cyan
Write-Host "  - Total Classes: $classCount" -ForegroundColor Gray
Write-Host "  - Total Methods: $totalMethods" -ForegroundColor Gray
Write-Host "  - Phase 3 Classes: $foundClasses" -ForegroundColor Gray
Write-Host "  - Critical Methods: $passed passing" -ForegroundColor Gray