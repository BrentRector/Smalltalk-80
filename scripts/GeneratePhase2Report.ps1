# Generate Phase 2 Validation Report
# Summary of all Phase 2 validation results

Write-Host "Phase 2 Complete Validation Report" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan
Write-Host ""

$reportDate = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
Write-Host "Generated: $reportDate"
Write-Host ""

# Load Phase 2 bootstrap
$phase2 = Get-Content "..\Bootstrap\bootstrap_phase2.json" | ConvertFrom-Json

# Analyze the raw bytecode dump
$dumpFile = "..\temp\phase2_raw_bytecode.txt"
if (Test-Path $dumpFile) {
    $dumpContent = Get-Content $dumpFile -Raw
    
    # Count methods in dump per class
    $classMethodCounts = @{}
    $lines = $dumpContent -split "`n"
    $currentClass = $null
    
    foreach ($line in $lines) {
        if ($line -match '^Class:\s+(\w+)') {
            $currentClass = $matches[1]
        }
        elseif ($line -match '^\s+Methods\s+\((\d+)\):') {
            if ($currentClass) {
                $classMethodCounts[$currentClass] = [int]$matches[1]
            }
        }
    }
}

Write-Host "PHASE 2 BOOTSTRAP SUMMARY" -ForegroundColor Yellow
Write-Host "-------------------------" -ForegroundColor Yellow
Write-Host "Total Classes: $($phase2.classes.Count)"

$totalInstanceMethods = 0
$totalClassMethods = 0

foreach ($class in $phase2.classes) {
    if ($class.methods) {
        $totalInstanceMethods += $class.methods.Count
    }
    if ($class.classMethods) {
        $totalClassMethods += $class.classMethods.Count
    }
}

Write-Host "Total Instance Methods: $totalInstanceMethods"
Write-Host "Total Class Methods: $totalClassMethods"
Write-Host "Grand Total: $($totalInstanceMethods + $totalClassMethods)"
Write-Host ""

Write-Host "INTERPRETER DUMP SUMMARY" -ForegroundColor Yellow
Write-Host "------------------------" -ForegroundColor Yellow
Write-Host "Classes Found: $($classMethodCounts.Count)"
$totalDumpedMethods = ($classMethodCounts.Values | Measure-Object -Sum).Sum
Write-Host "Total Methods Shown: $totalDumpedMethods"
Write-Host ""

Write-Host "PER-CLASS COMPARISON" -ForegroundColor Yellow
Write-Host "--------------------" -ForegroundColor Yellow
Write-Host "{0,-20} {1,15} {2,15} {3,15}" -f "Class", "Expected", "Dumped", "Status"
Write-Host "{0,-20} {1,15} {2,15} {3,15}" -f "-----", "--------", "------", "------"

$allClasses = @{}
foreach ($class in $phase2.classes) {
    $instanceCount = if ($class.methods) { $class.methods.Count } else { 0 }
    $classCount = if ($class.classMethods) { $class.classMethods.Count } else { 0 }
    $allClasses[$class.name] = @{
        Expected = $instanceCount + $classCount
        InstanceMethods = $instanceCount
        ClassMethods = $classCount
    }
}

foreach ($className in $allClasses.Keys | Sort-Object) {
    $expected = $allClasses[$className].Expected
    $dumped = if ($classMethodCounts.ContainsKey($className)) { 
        $classMethodCounts[$className] 
    } else { 
        0 
    }
    
    $status = if ($dumped -eq 0) {
        "MISSING"
        $color = "Red"
    } elseif ($dumped -lt $expected) {
        "PARTIAL ($([Math]::Round(($dumped/$expected)*100, 0))%)"
        $color = "Yellow"
    } elseif ($dumped -eq $expected) {
        "COMPLETE"
        $color = "Green"
    } else {
        "EXTRA"
        $color = "Cyan"
    }
    
    Write-Host ("{0,-20} {1,15} {2,15} {3,15}" -f $className, $expected, $dumped, $status) -ForegroundColor $color
}

Write-Host ""
Write-Host "KEY FINDINGS" -ForegroundColor Yellow
Write-Host "------------" -ForegroundColor Yellow

# Check for method display limit
$methodLimits = $classMethodCounts.Values | Group-Object | Sort-Object -Property Name
$maxMethodsShown = ($classMethodCounts.Values | Measure-Object -Maximum).Maximum

if ($maxMethodsShown -eq 15) {
    Write-Host "⚠ WARNING: Interpreter appears to limit method display to 15 per class" -ForegroundColor Red
    Write-Host "  Many classes show exactly 15 methods when more are expected" -ForegroundColor Red
}

# Check completion rate
$completeClasses = 0
$partialClasses = 0
$missingClasses = 0

foreach ($className in $allClasses.Keys) {
    $expected = $allClasses[$className].Expected
    $dumped = if ($classMethodCounts.ContainsKey($className)) { 
        $classMethodCounts[$className] 
    } else { 
        0 
    }
    
    if ($dumped -eq 0) {
        $missingClasses++
    } elseif ($dumped -eq $expected) {
        $completeClasses++
    } else {
        $partialClasses++
    }
}

Write-Host ""
Write-Host "Class Coverage:" -ForegroundColor Green
Write-Host "  Complete: $completeClasses / $($allClasses.Count)"
Write-Host "  Partial: $partialClasses / $($allClasses.Count)"
Write-Host "  Missing: $missingClasses / $($allClasses.Count)"

$coveragePercent = [Math]::Round(($totalDumpedMethods / ($totalInstanceMethods + $totalClassMethods)) * 100, 2)
Write-Host ""
Write-Host "Method Coverage: $totalDumpedMethods / $($totalInstanceMethods + $totalClassMethods) ($coveragePercent%)"

Write-Host ""
Write-Host "VALIDATION RESULTS" -ForegroundColor Yellow
Write-Host "------------------" -ForegroundColor Yellow

# Run the bytecode validation and capture results
$validationOutput = & pwsh -File ValidatePhase2Bytecode.ps1 2>&1 | Out-String
if ($validationOutput -match "Correct Methods:\s+(\d+)") {
    $correctMethods = [int]$matches[1]
    Write-Host "✓ Bytecode-Verified Methods: $correctMethods" -ForegroundColor Green
}
if ($validationOutput -match "Incorrect Bytecode:\s+(\d+)") {
    $incorrectBytecode = [int]$matches[1]
    if ($incorrectBytecode -gt 0) {
        Write-Host "✗ Incorrect Bytecode: $incorrectBytecode" -ForegroundColor Red
    }
}
if ($validationOutput -match "Missing Methods:\s+(\d+)") {
    $missingMethods = [int]$matches[1]
    if ($missingMethods -gt 0) {
        Write-Host "✗ Missing Methods: $missingMethods" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "RECOMMENDATIONS" -ForegroundColor Yellow
Write-Host "---------------" -ForegroundColor Yellow

if ($maxMethodsShown -eq 15) {
    Write-Host "1. The interpreter's pretty printer appears to limit output to 15 methods per class."
    Write-Host "   Consider modifying the printer to show all methods or use a different dump mechanism."
}

if ($missingMethods -gt 0) {
    Write-Host "2. Many methods are missing from the dump. This could be due to:"
    Write-Host "   - Display limit in the interpreter"
    Write-Host "   - Methods not properly installed during Phase 2"
    Write-Host "   - Issue with method dictionary structure"
}

if ($incorrectBytecode -gt 0) {
    Write-Host "3. Some methods have incorrect bytecode. Check:"
    Write-Host "   - Bytecode compilation process"
    Write-Host "   - Method installation process"
    Write-Host "   - Bytecode array conversion"
}

Write-Host ""
Write-Host "Report complete." -ForegroundColor Cyan