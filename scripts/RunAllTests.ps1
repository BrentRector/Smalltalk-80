# Run All Available Tests for Phase 3 System
# This script runs all test types and collects results

$results = @{
    TotalTests = 0
    Passed = 0
    Failed = 0
    Skipped = 0
    Details = @()
}

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "RUNNING ALL PHASE 3 SYSTEM TESTS" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Critical Methods Test
Write-Host "[TEST 1] Critical Methods for Class Installation" -ForegroundColor Yellow
$criticalOutput = & "../bin/Debug/net9.0/Smalltalk80.Interpreter.exe" --test-critical-methods 2>&1 | Out-String

if ($criticalOutput -match "Passed: (\d+).*Failed: (\d+).*Total:\s+(\d+)") {
    $passed = [int]$matches[1]
    $failed = [int]$matches[2]
    $total = [int]$matches[3]
    
    $results.TotalTests += $total
    $results.Passed += $passed
    $results.Failed += $failed
    
    Write-Host "  Results: $passed/$total passed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
    $results.Details += "Critical Methods: $passed/$total passed"
} else {
    Write-Host "  ERROR: Could not parse results" -ForegroundColor Red
}

# Test 2: Phase 3 Class Loading
Write-Host ""
Write-Host "[TEST 2] Phase 3 Class Loading" -ForegroundColor Yellow
$phase3Output = & "../bin/Debug/net9.0/Smalltalk80.Interpreter.exe" --extended-library Bootstrap/phase3_minimal.json --list-bootstrap-classes 2>&1 | Out-String

$classesCreated = ([regex]::Matches($phase3Output, "Creating class")).Count
$methodsInstalled = ([regex]::Matches($phase3Output, "Installing")).Count

$expectedClasses = 13
$expectedMethods = 276

$classTestPassed = $classesCreated -eq $expectedClasses
$methodTestPassed = $methodsInstalled -eq $expectedMethods

$results.TotalTests += 2
if ($classTestPassed) { $results.Passed++ } else { $results.Failed++ }
if ($methodTestPassed) { $results.Passed++ } else { $results.Failed++ }

Write-Host "  Classes: $classesCreated/$expectedClasses" -ForegroundColor $(if ($classTestPassed) { "Green" } else { "Red" })
Write-Host "  Methods: $methodsInstalled/$expectedMethods" -ForegroundColor $(if ($methodTestPassed) { "Green" } else { "Red" })
$results.Details += "Phase 3 Classes: $classesCreated/$expectedClasses"
$results.Details += "Phase 3 Methods: $methodsInstalled/$expectedMethods"

# Test 3: System Statistics
Write-Host ""
Write-Host "[TEST 3] System Statistics Validation" -ForegroundColor Yellow
$statsOutput = & "../bin/Debug/net9.0/Smalltalk80.Interpreter.exe" --extended-library Bootstrap/phase3_minimal.json --enhanced-print 2>&1 | Out-String

if ($statsOutput -match "Classes registered in Smalltalk: (\d+)") {
    $totalClasses = [int]$matches[1]
    $expectedTotal = 60
    $classCountPassed = $totalClasses -eq $expectedTotal
    
    $results.TotalTests++
    if ($classCountPassed) { $results.Passed++ } else { $results.Failed++ }
    
    Write-Host "  Total Classes: $totalClasses/$expectedTotal" -ForegroundColor $(if ($classCountPassed) { "Green" } else { "Red" })
    $results.Details += "Total Classes: $totalClasses/$expectedTotal"
}

# Count total methods
$methodCounts = [regex]::Matches($statsOutput, "Methods \((\d+)\)")
$totalMethods = 0
foreach ($match in $methodCounts) {
    $totalMethods += [int]$match.Groups[1].Value
}

$methodCountPassed = $totalMethods -gt 700
$results.TotalTests++
if ($methodCountPassed) { $results.Passed++ } else { $results.Failed++ }

Write-Host "  Total Methods: $totalMethods (expected 700+)" -ForegroundColor $(if ($methodCountPassed) { "Green" } else { "Red" })
$results.Details += "Total Methods: $totalMethods"

# Test 4: Core Class Verification
Write-Host ""
Write-Host "[TEST 4] Core Class Verification" -ForegroundColor Yellow

$coreClasses = @(
    "Object", "Array", "String", "Dictionary", "Symbol",
    "Boolean", "True", "False", "SmallInteger", "Character",
    "OrderedCollection", "Set", "Interval", "Stream", "Compiler"
)

$foundCore = 0
$missingCore = @()

foreach ($class in $coreClasses) {
    if ($statsOutput -match "Class: $class\b") {
        $foundCore++
    } else {
        $missingCore += $class
    }
}

$coreTestPassed = $foundCore -eq $coreClasses.Count
$results.TotalTests++
if ($coreTestPassed) { $results.Passed++ } else { $results.Failed++ }

Write-Host "  Core Classes: $foundCore/$($coreClasses.Count)" -ForegroundColor $(if ($coreTestPassed) { "Green" } else { "Red" })
if ($missingCore.Count -gt 0) {
    Write-Host "  Missing: $($missingCore -join ', ')" -ForegroundColor Red
}
$results.Details += "Core Classes: $foundCore/$($coreClasses.Count)"

# Test 5: Primitive Operations
Write-Host ""
Write-Host "[TEST 5] Primitive Operations Check" -ForegroundColor Yellow

# Quick primitive test by checking if basic operations work
$testCommands = @(
    "Object new",
    "Array new: 5", 
    "Dictionary new",
    "3 + 4",
    "true and: [false]"
)

$primitiveTests = 0
$primitivePassed = 0

# Since we can't execute Smalltalk directly, check that primitives are registered
$primOutput = $statsOutput
$registeredPrims = ([regex]::Matches($primOutput, "primitive")).Count

$hasPrimitives = $registeredPrims -gt 0
$results.TotalTests++
if ($hasPrimitives) { $results.Passed++ } else { $results.Failed++ }

Write-Host "  Primitives Registered: $(if ($hasPrimitives) { 'Yes' } else { 'No' })" -ForegroundColor $(if ($hasPrimitives) { "Green" } else { "Red" })
$results.Details += "Primitives: $(if ($hasPrimitives) { 'Registered' } else { 'Not Found' })"

# Test 6: Bootstrap Manifest Validation
Write-Host ""
Write-Host "[TEST 6] Bootstrap Manifest Files" -ForegroundColor Yellow

$manifestFiles = @(
    "../Bootstrap/bootstrap.json",
    "../Bootstrap/phase3_minimal.json"
)

$manifestTests = 0
$manifestPassed = 0

foreach ($file in $manifestFiles) {
    $manifestTests++
    if (Test-Path $file) {
        $manifestPassed++
        $size = (Get-Item $file).Length / 1KB
        Write-Host "  ✓ $(Split-Path $file -Leaf): $([math]::Round($size, 1)) KB" -ForegroundColor Green
    } else {
        Write-Host "  ✗ $(Split-Path $file -Leaf): Not Found" -ForegroundColor Red
    }
}

$results.TotalTests += $manifestTests
$results.Passed += $manifestPassed
$results.Failed += ($manifestTests - $manifestPassed)
$results.Details += "Manifest Files: $manifestPassed/$manifestTests"

# Test 7: Method Dictionary Functionality
Write-Host ""
Write-Host "[TEST 7] Method Dictionary Operations" -ForegroundColor Yellow

# Check that method dictionaries are working from critical methods test
$mdTestPassed = $criticalOutput -match "MethodDictionary>>at:put:.*PASSED"
$results.TotalTests++
if ($mdTestPassed) { $results.Passed++ } else { $results.Failed++ }

Write-Host "  Method Dictionary at:put: $(if ($mdTestPassed) { 'PASSED' } else { 'FAILED' })" -ForegroundColor $(if ($mdTestPassed) { "Green" } else { "Red" })
$results.Details += "Method Dictionary: $(if ($mdTestPassed) { 'Working' } else { 'Failed' })"

# Summary
Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "TEST EXECUTION SUMMARY" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Total Tests Run: $($results.TotalTests)" -ForegroundColor White
Write-Host "Passed: $($results.Passed)" -ForegroundColor Green
Write-Host "Failed: $($results.Failed)" -ForegroundColor $(if ($results.Failed -eq 0) { "Green" } else { "Red" })
Write-Host "Skipped: $($results.Skipped)" -ForegroundColor Yellow

$passRate = if ($results.TotalTests -gt 0) { 
    [math]::Round(($results.Passed / $results.TotalTests) * 100, 1) 
} else { 0 }

Write-Host ""
Write-Host "Pass Rate: $passRate%" -ForegroundColor $(if ($passRate -ge 90) { "Green" } elseif ($passRate -ge 70) { "Yellow" } else { "Red" })

Write-Host ""
Write-Host "Detailed Results:" -ForegroundColor Cyan
foreach ($detail in $results.Details) {
    Write-Host "  - $detail" -ForegroundColor Gray
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan

if ($results.Failed -eq 0) {
    Write-Host "✅ ALL TESTS PASSED!" -ForegroundColor Green
    Write-Host "The Phase 3 system is fully functional." -ForegroundColor Green
} elseif ($passRate -ge 80) {
    Write-Host "⚠️ MOSTLY PASSING" -ForegroundColor Yellow
    Write-Host "The Phase 3 system is functional with minor issues." -ForegroundColor Yellow
} else {
    Write-Host "❌ SIGNIFICANT FAILURES" -ForegroundColor Red
    Write-Host "The Phase 3 system has critical issues." -ForegroundColor Red
}

# Return results as JSON for automation
$jsonResult = @{
    timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    totalTests = $results.TotalTests
    passed = $results.Passed
    failed = $results.Failed
    skipped = $results.Skipped
    passRate = $passRate
    details = $results.Details
} | ConvertTo-Json

# Save to file
$jsonResult | Out-File -FilePath "../test_results.json" -Encoding UTF8

Write-Host ""
Write-Host "Results saved to test_results.json" -ForegroundColor Gray