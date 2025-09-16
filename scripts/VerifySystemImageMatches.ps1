# VerifySystemImageMatches.ps1
# Performs complete verification between interpreter-loaded classes and system image reference
#
# Usage: pwsh VerifySystemImageMatches.ps1 [-SystemImagePath "complete_system_final.json"] [-DumpOutput "interpreter_class_dump.txt"]

param(
    [string]$SystemImagePath = "../complete_system_final.json",
    [string]$DumpOutput = "interpreter_class_dump.txt",
    [string]$ComparisonReport = "verification_report.txt"
)

Write-Host "System Image Verification" -ForegroundColor Green
Write-Host "========================" -ForegroundColor Green
Write-Host ""

# Verify system image exists
if (!(Test-Path $SystemImagePath)) {
    Write-Host "ERROR: System image not found at: $SystemImagePath" -ForegroundColor Red
    Write-Host "Run: dotnet bin/Debug/net9.0/StandaloneCompiler.dll 'Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st' complete_system_final.json" -ForegroundColor Yellow
    exit 1
}

Write-Host "Step 1: Loading all classes in interpreter..." -ForegroundColor Cyan
$interpreterResult = & dotnet ../bin/Debug/net9.0/Smalltalk80.Interpreter.dll --list-bootstrap-classes 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Interpreter failed to load classes" -ForegroundColor Red
    Write-Host $interpreterResult -ForegroundColor Red
    exit 1
}

Write-Host "Step 2: Dumping all loaded classes..." -ForegroundColor Cyan
$dumpResult = & dotnet ../bin/Debug/net9.0/Smalltalk80.Interpreter.dll --dump-all-classes $DumpOutput 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to dump classes" -ForegroundColor Red
    Write-Host $dumpResult -ForegroundColor Red
    exit 1
}

if (!(Test-Path $DumpOutput)) {
    Write-Host "ERROR: Class dump file not created: $DumpOutput" -ForegroundColor Red
    exit 1
}

Write-Host "Step 3: Loading system image reference..." -ForegroundColor Cyan
$systemImage = Get-Content $SystemImagePath | ConvertFrom-Json

Write-Host "Step 4: Parsing interpreter dump..." -ForegroundColor Cyan
$dumpContent = Get-Content $DumpOutput

# Parse interpreter dump (this will need to be adapted based on actual dump format)
$interpreterClasses = @{}
$currentClass = $null
$currentSection = $null

foreach ($line in $dumpContent) {
    if ($line -match "^CLASS: (.+)$") {
        $currentClass = $matches[1]
        $interpreterClasses[$currentClass] = @{
            Name = $currentClass
            InstanceVariables = @()
            Methods = @()
            FieldCount = 0
        }
        $currentSection = "class"
    }
    elseif ($line -match "^  Instance Variables: (.+)$") {
        $interpreterClasses[$currentClass].InstanceVariables = $matches[1] -split ", "
        $currentSection = "vars"
    }
    elseif ($line -match "^  Field Count: (\d+)$") {
        $interpreterClasses[$currentClass].FieldCount = [int]$matches[1]
        $currentSection = "fields"
    }
    elseif ($line -match "^  Method: (.+)$") {
        $interpreterClasses[$currentClass].Methods += $matches[1]
        $currentSection = "methods"
    }
}

Write-Host "Step 5: Comparing interpreter vs system image..." -ForegroundColor Cyan

$report = @()
$report += "System Image Verification Report"
$report += "================================"
$report += "Generated: $(Get-Date)"
$report += ""

$totalClasses = 0
$matchingClasses = 0
$mismatches = @()

# Core bootstrap classes to verify
$coreClasses = @(
    "Object", "Behavior", "Class", "Metaclass",
    "CompiledMethod", "MethodDictionary", 
    "Symbol", "String", "Array", "ByteArray",
    "Collection", "Dictionary", "SystemDictionary",
    "UndefinedObject", "Boolean", "True", "False",
    "Magnitude", "LookupKey", "Association"
)

foreach ($className in $coreClasses) {
    $totalClasses++
    $report += "Checking class: $className"
    
    # Find in system image
    $systemClass = $systemImage.Classes | Where-Object { $_.Name -eq $className }
    $interpreterClass = $interpreterClasses[$className]
    
    if (!$systemClass) {
        $mismatches += "  ERROR: $className not found in system image"
        $report += "  ERROR: Not found in system image"
        continue
    }
    
    if (!$interpreterClass) {
        $mismatches += "  ERROR: $className not loaded in interpreter"
        $report += "  ERROR: Not loaded in interpreter" 
        continue
    }
    
    # Calculate expected field count from inheritance
    $expectedFields = 0
    $currentClass = $systemClass
    $hierarchy = @()
    
    # Build inheritance chain
    while ($currentClass -and $currentClass.Name -ne "Object") {
        $hierarchy = @($currentClass) + $hierarchy
        $superclassName = $currentClass.Superclass
        if ($superclassName -and $superclassName -ne "nil") {
            $currentClass = $systemImage.Classes | Where-Object { $_.Name -eq $superclassName }
        } else {
            break
        }
    }
    
    # Add Object if we're not Object
    if ($className -ne "Object") {
        $objectClass = $systemImage.Classes | Where-Object { $_.Name -eq "Object" }
        if ($objectClass) {
            $hierarchy = @($objectClass) + $hierarchy
        }
    }
    
    # Count inherited instance variables
    foreach ($cls in $hierarchy) {
        if ($cls.InstanceVariables) {
            $expectedFields += $cls.InstanceVariables.Count
        }
    }
    
    # Compare field counts
    if ($interpreterClass.FieldCount -ne $expectedFields) {
        $mismatches += "  MISMATCH: $className field count - Expected: $expectedFields, Actual: $($interpreterClass.FieldCount)"
        $report += "  MISMATCH: Field count - Expected: $expectedFields, Actual: $($interpreterClass.FieldCount)"
    }
    
    # Compare method counts
    $expectedMethods = if ($systemClass.Methods) { $systemClass.Methods.Count } else { 0 }
    $actualMethods = $interpreterClass.Methods.Count
    
    if ($actualMethods -ne $expectedMethods) {
        $mismatches += "  MISMATCH: $className method count - Expected: $expectedMethods, Actual: $actualMethods"
        $report += "  MISMATCH: Method count - Expected: $expectedMethods, Actual: $actualMethods"
    }
    
    if ($interpreterClass.FieldCount -eq $expectedFields -and $actualMethods -eq $expectedMethods) {
        $matchingClasses++
        $report += "  ✓ MATCH: All checks passed"
    }
    
    $report += ""
}

# Generate summary
$matchPercentage = if ($totalClasses -gt 0) { [math]::Round(($matchingClasses / $totalClasses) * 100, 1) } else { 0 }

$report += "VERIFICATION SUMMARY"
$report += "==================="
$report += "Total classes checked: $totalClasses"
$report += "Matching classes: $matchingClasses"
$report += "Match percentage: $matchPercentage%"
$report += ""

if ($mismatches.Count -gt 0) {
    $report += "MISMATCHES FOUND:"
    $report += $mismatches
    $report += ""
}

if ($matchPercentage -eq 100) {
    $report += "✅ SUCCESS: 100% verification achieved!"
    $report += "All classes match the system image specification."
} else {
    $report += "❌ INCOMPLETE: Verification failed"
    $report += "Must achieve 100% match before system is considered correct."
    $report += "Return to Step 1 and regenerate system image."
}

# Save report
$report | Set-Content $ComparisonReport

# Display results
Write-Host ""
if ($matchPercentage -eq 100) {
    Write-Host "✅ SUCCESS: 100% verification achieved!" -ForegroundColor Green
} else {
    Write-Host "❌ INCOMPLETE: $matchPercentage% verification" -ForegroundColor Red
    Write-Host "Found $($mismatches.Count) mismatches:" -ForegroundColor Yellow
    foreach ($mismatch in $mismatches) {
        Write-Host $mismatch -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Full report saved to: $ComparisonReport" -ForegroundColor Cyan

exit $(if ($matchPercentage -eq 100) { 0 } else { 1 })