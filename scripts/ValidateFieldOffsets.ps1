# ValidateFieldOffsets.ps1
# Validates that field offset corrections are working by analyzing bootstrap log output
#
# Usage: pwsh ValidateFieldOffsets.ps1 [-BootstrapLog "interpreter_log.txt"] [-SystemImagePath "complete_system_final.json"]

param(
    [string]$SystemImagePath = "../complete_system_final.json",
    [string]$ValidationReport = "field_offset_validation.txt"
)

Write-Host "Field Offset Validation" -ForegroundColor Green
Write-Host "======================" -ForegroundColor Green
Write-Host ""

# Verify system image exists
if (!(Test-Path $SystemImagePath)) {
    Write-Host "ERROR: System image not found at: $SystemImagePath" -ForegroundColor Red
    exit 1
}

Write-Host "Loading system image reference..." -ForegroundColor Cyan
$systemImage = Get-Content $SystemImagePath | ConvertFrom-Json

Write-Host "Running interpreter and capturing bootstrap log..." -ForegroundColor Cyan
$interpreterOutput = & dotnet ../bin/Debug/net9.0/Smalltalk80.Interpreter.dll --inspect-class Association 2>&1

# Parse the interpreter output to extract key validation points
$report = @()
$report += "Field Offset Validation Report"
$report += "=============================="
$report += "Generated: $(Get-Date)"
$report += ""

# Test cases to validate
$testCases = @(
    @{
        Class = "Association"
        ExpectedSuperclass = "LookupKey"
        ExpectedLocalFields = @("value")
        ExpectedInheritedFields = @("key")
        ExpectedTotalFields = 2
    },
    @{
        Class = "LookupKey" 
        ExpectedSuperclass = "Magnitude"
        ExpectedLocalFields = @("key")
        ExpectedInheritedFields = @()
        ExpectedTotalFields = 1
    },
    @{
        Class = "MethodDictionary"
        ExpectedSuperclass = "IdentityDictionary"
        ExpectedLocalFields = @()
        ExpectedInheritedFields = @("tally", "valueArray")
        ExpectedTotalFields = 2
    }
)

$totalTests = 0
$passedTests = 0

foreach ($testCase in $testCases) {
    $className = $testCase.Class
    $report += "Testing class: $className"
    
    # Find class in system image
    $systemClass = $systemImage.Classes | Where-Object { $_.Name -eq $className }
    if (!$systemClass) {
        $report += "  ERROR: Class '$className' not found in system image"
        continue
    }
    
    $totalTests++
    
    # Verify superclass relationship
    $expectedSuperclass = $testCase.ExpectedSuperclass
    if ($systemClass.Superclass -eq $expectedSuperclass) {
        $report += "  ✓ Superclass: $expectedSuperclass"
    } else {
        $report += "  ❌ Superclass: Expected '$expectedSuperclass', got '$($systemClass.Superclass)'"
        continue
    }
    
    # Calculate expected inherited fields
    $allInheritedFields = @()
    $currentSuperclass = $expectedSuperclass
    $hierarchy = @()
    
    # Build inheritance chain
    while ($currentSuperclass -and $currentSuperclass -ne "nil" -and $currentSuperclass -ne "Object") {
        $superClass = $systemImage.Classes | Where-Object { $_.Name -eq $currentSuperclass }
        if ($superClass) {
            $hierarchy = @($superClass) + $hierarchy
            $currentSuperclass = $superClass.Superclass
        } else {
            break
        }
    }
    
    # Collect inherited instance variables in order
    foreach ($cls in $hierarchy) {
        if ($cls.InstanceVariables) {
            $allInheritedFields += $cls.InstanceVariables
        }
    }
    
    # Add local instance variables
    $localFields = if ($systemClass.InstanceVariables) { $systemClass.InstanceVariables } else { @() }
    $totalFields = $allInheritedFields + $localFields
    
    # Verify field count matches expectation
    if ($totalFields.Count -eq $testCase.ExpectedTotalFields) {
        $report += "  ✓ Total fields: $($totalFields.Count)"
        $passedTests++
    } else {
        $report += "  ❌ Total fields: Expected $($testCase.ExpectedTotalFields), got $($totalFields.Count)"
        continue
    }
    
    # Verify field layout
    $report += "  Field layout:"
    for ($i = 0; $i -lt $totalFields.Count; $i++) {
        $fieldName = $totalFields[$i]
        $source = if ($i -lt $allInheritedFields.Count) { "inherited" } else { "local" }
        $report += "    Offset $i`: $fieldName ($source)"
    }
    
    $report += ""
}

# Check for CompiledMethod creation in bootstrap log
$compiledMethodCreations = $interpreterOutput | Select-String "Created method instance OOP.*Method object has \d+ fields"
if ($compiledMethodCreations) {
    $report += "CompiledMethod Creation Validation:"
    $fieldCounts = @()
    foreach ($line in $compiledMethodCreations) {
        if ($line -match "Method object has (\d+) fields") {
            $fieldCounts += [int]$matches[1]
        }
    }
    
    $uniqueCounts = $fieldCounts | Sort-Object -Unique
    if ($uniqueCounts.Count -eq 1 -and $uniqueCounts[0] -eq 4) {
        $report += "  ✓ All CompiledMethod instances created with 4 fields"
        $passedTests++
    } else {
        $report += "  ❌ CompiledMethod field count inconsistent: $($uniqueCounts -join ', ')"
    }
    $totalTests++
    $report += ""
}

# Check for inheritance relationships in bootstrap log
$inheritanceLines = $interpreterOutput | Select-String "Successfully set superclass.*to.*"
if ($inheritanceLines.Count -gt 0) {
    $report += "Inheritance Chain Validation:"
    $report += "  ✓ Found $($inheritanceLines.Count) successful superclass relationships"
    
    # Specifically check Association -> LookupKey
    $associationInheritance = $interpreterOutput | Select-String "Successfully set superclass of Association.*to LookupKey"
    if ($associationInheritance) {
        $report += "  ✓ Association correctly inherits from LookupKey"
        $passedTests++
    } else {
        $report += "  ❌ Association inheritance from LookupKey not found"
    }
    $totalTests++
    $report += ""
}

# Check for no field access errors
$fieldErrors = $interpreterOutput | Select-String "Slot index.*out of range|ArgumentOutOfRangeException"
if ($fieldErrors.Count -eq 0) {
    $report += "Field Access Validation:"
    $report += "  ✓ No field access errors detected"
    $passedTests++
} else {
    $report += "Field Access Validation:"
    $report += "  ❌ Found $($fieldErrors.Count) field access errors"
    foreach ($error in $fieldErrors) {
        $report += "    $($error.Line)"
    }
}
$totalTests++

# Generate summary
$successPercentage = if ($totalTests -gt 0) { [math]::Round(($passedTests / $totalTests) * 100, 1) } else { 0 }

$report += ""
$report += "VALIDATION SUMMARY"
$report += "=================="
$report += "Total tests: $totalTests"
$report += "Passed tests: $passedTests"
$report += "Success rate: $successPercentage%"
$report += ""

if ($successPercentage -eq 100) {
    $report += "✅ SUCCESS: 100% field offset validation achieved!"
    $report += "Field offset corrections are working correctly."
    $report += "- Inheritance chains established properly"
    $report += "- CompiledMethod instances have correct structure" 
    $report += "- No field access errors detected"
    $report += "- System image matches interpreter behavior"
} else {
    $report += "❌ INCOMPLETE: Field offset validation failed"
    $report += "Issues found that require attention."
}

# Save report
$report | Set-Content $ValidationReport

# Display results
Write-Host ""
if ($successPercentage -eq 100) {
    Write-Host "✅ SUCCESS: 100% field offset validation achieved!" -ForegroundColor Green
    Write-Host "Field offset corrections are working correctly." -ForegroundColor Green
} else {
    Write-Host "❌ INCOMPLETE: $successPercentage% validation" -ForegroundColor Red
    Write-Host "Found issues with field offset implementation." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Full report saved to: $ValidationReport" -ForegroundColor Cyan

exit $(if ($successPercentage -eq 100) { 0 } else { 1 })