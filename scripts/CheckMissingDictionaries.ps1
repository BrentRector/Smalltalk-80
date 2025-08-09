# Check which Phase 2 classes don't have method dictionaries in Phase 1

$phase1 = Get-Content "..\Bootstrap\bootstrap_phase1.json" | ConvertFrom-Json
$phase2 = Get-Content "..\Bootstrap\bootstrap_phase2.json" | ConvertFrom-Json

# Get all classes with createMethodDictionary in Phase 1
$phase1Classes = @()
foreach ($op in $phase1.operations) {
    if ($op.op -eq "createMethodDictionary") {
        # Remove "MethodDict" suffix and store just the class name
        $phase1Classes += $op.class
    }
}

# Get all classes in Phase 2
$phase2Classes = $phase2.classes | ForEach-Object { $_.name }

Write-Host "Phase 2 Classes Without Method Dictionaries in Phase 1:"
Write-Host "======================================================="
Write-Host ""

$missing = @()
foreach ($className in $phase2Classes) {
    # Check both instance and class side
    if ($className -notin $phase1Classes) {
        $class = $phase2.classes | Where-Object { $_.name -eq $className }
        $methodCount = 0
        if ($class.methods) {
            $methodCount = $class.methods.Count
        }
        Write-Host "$className - $methodCount instance methods"
        $missing += $className
    }
    
    # Check class side
    $metaclassName = $className + "Class"
    if ($metaclassName -notin $phase1Classes) {
        $class = $phase2.classes | Where-Object { $_.name -eq $className }
        $classMethodCount = 0
        if ($class.classMethods) {
            $classMethodCount = $class.classMethods.Count
        }
        if ($classMethodCount -gt 0) {
            Write-Host "$metaclassName - $classMethodCount class methods"
            $missing += $metaclassName
        }
    }
}

if ($missing.Count -eq 0) {
    Write-Host "(None - all Phase 2 classes have dictionaries in Phase 1)"
}

Write-Host ""
Write-Host "Total missing: $($missing.Count)"