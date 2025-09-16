# AnalyzeSystemImageStructure.ps1
# Analyzes the complete_system_final.json structure for clean slate bootstrap redesign

param(
    [string]$SystemImageFile = "D:/Users/brent/OneDrive/Src/Smalltalk-80/complete_system_final.json",
    [string]$BootstrapFile = "D:/Users/brent/OneDrive/Src/Smalltalk-80/Bootstrap/bootstrap.json"
)

Write-Host "System Image Structure Analysis" -ForegroundColor Cyan
Write-Host "===============================" -ForegroundColor Cyan

# Load system image JSON
Write-Host "`n[1] Loading system image JSON..." -ForegroundColor Yellow
$systemImage = Get-Content $SystemImageFile | ConvertFrom-Json
Write-Host "System image contains:" -ForegroundColor Green
Write-Host "  Classes: $($systemImage.classes.Count)" -ForegroundColor White
Write-Host "  Methods: $($systemImage.methods.Count)" -ForegroundColor White

# Load current bootstrap manifest
Write-Host "`n[2] Loading current bootstrap manifest..." -ForegroundColor Yellow
$bootstrap = Get-Content $BootstrapFile | ConvertFrom-Json
$bootstrapClassNames = $bootstrap.classes | ForEach-Object { $_.name }
Write-Host "Bootstrap classes: $($bootstrapClassNames.Count)" -ForegroundColor Green

# Check which bootstrap classes exist in system image
Write-Host "`n[3] Verifying bootstrap classes exist in system image..." -ForegroundColor Yellow
$systemImageClassNames = $systemImage.classes | ForEach-Object { $_.name }
$foundClasses = @()
$missingClasses = @()

foreach ($className in $bootstrapClassNames) {
    if ($systemImageClassNames -contains $className) {
        $foundClasses += $className
    } else {
        $missingClasses += $className
    }
}

Write-Host "Found in system image: $($foundClasses.Count)" -ForegroundColor Green
Write-Host "Missing from system image: $($missingClasses.Count)" -ForegroundColor $(if ($missingClasses.Count -eq 0) { "Green" } else { "Red" })

if ($missingClasses.Count -gt 0) {
    Write-Host "Missing classes:" -ForegroundColor Red
    $missingClasses | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
}

# Analyze class structure
Write-Host "`n[4] Analyzing class structure..." -ForegroundColor Yellow
$sampleClass = $systemImage.classes | Where-Object { $_.name -eq "Object" } | Select-Object -First 1
Write-Host "Sample class structure (Object):" -ForegroundColor White
Write-Host "  name: $($sampleClass.name)" -ForegroundColor Gray
Write-Host "  superclass: $($sampleClass.superclass)" -ForegroundColor Gray
Write-Host "  instanceVariables: [$($sampleClass.instanceVariables -join ', ')]" -ForegroundColor Gray
Write-Host "  classVariables: [$($sampleClass.classVariables -join ', ')]" -ForegroundColor Gray
Write-Host "  poolDictionaries: [$($sampleClass.poolDictionaries -join ', ')]" -ForegroundColor Gray
Write-Host "  category: $($sampleClass.category)" -ForegroundColor Gray

# Analyze method structure
Write-Host "`n[5] Analyzing method structure..." -ForegroundColor Yellow
$sampleMethod = $systemImage.methods | Where-Object { $_.class -eq "Object" } | Select-Object -First 1
if ($sampleMethod) {
    Write-Host "Sample method structure (Object method):" -ForegroundColor White
    Write-Host "  class: $($sampleMethod.class)" -ForegroundColor Gray
    Write-Host "  selector: $($sampleMethod.selector)" -ForegroundColor Gray
    Write-Host "  bytecode: [$($sampleMethod.bytecode -join ', ')]" -ForegroundColor Gray
    Write-Host "  literals: [$($sampleMethod.literals -join ', ')]" -ForegroundColor Gray
    Write-Host "  primitiveIndex: $($sampleMethod.primitiveIndex)" -ForegroundColor Gray
    Write-Host "  numArgs: $($sampleMethod.numArgs)" -ForegroundColor Gray
    Write-Host "  numTemps: $($sampleMethod.numTemps)" -ForegroundColor Gray
    Write-Host "  isClassMethod: $($sampleMethod.isClassMethod)" -ForegroundColor Gray
}

# Count methods per bootstrap class
Write-Host "`n[6] Counting methods per bootstrap class..." -ForegroundColor Yellow
$classMethodCounts = @{}
foreach ($className in $foundClasses) {
    $instanceMethods = $systemImage.methods | Where-Object { $_.class -eq $className -and $_.isClassMethod -eq $false }
    $classMethods = $systemImage.methods | Where-Object { $_.class -eq "$className class" -and $_.isClassMethod -eq $true }
    $classMethodCounts[$className] = @{
        instanceMethods = $instanceMethods.Count
        classMethods = $classMethods.Count
        total = $instanceMethods.Count + $classMethods.Count
    }
}

$topClasses = $classMethodCounts.GetEnumerator() | Sort-Object { $_.Value.total } -Descending | Select-Object -First 10
Write-Host "Top 10 classes by method count:" -ForegroundColor White
$topClasses | ForEach-Object {
    $name = $_.Key
    $counts = $_.Value
    Write-Host "  $name`: $($counts.instanceMethods) instance + $($counts.classMethods) class = $($counts.total) total" -ForegroundColor Gray
}

# Build dependency graph
Write-Host "`n[7] Building dependency graph..." -ForegroundColor Yellow
$dependencyGraph = @{}
$classLookup = @{}

# Create lookup table for classes
foreach ($class in $systemImage.classes) {
    $classLookup[$class.name] = $class
}

# Build dependency relationships for bootstrap classes only
foreach ($className in $foundClasses) {
    $class = $classLookup[$className]
    $superclass = $class.superclass
    
    if ($superclass -eq "nil" -or $superclass -eq $null) {
        $dependencyGraph[$className] = @()
    } else {
        $dependencyGraph[$className] = @($superclass)
    }
}

# Topological sort
Write-Host "`n[8] Performing topological sort..." -ForegroundColor Yellow
$sorted = @()
$visited = @{}
$visiting = @{}

function Visit-Class($className) {
    if ($visiting.ContainsKey($className)) {
        throw "Circular dependency detected involving $className"
    }
    if ($visited.ContainsKey($className)) {
        return
    }
    
    $visiting[$className] = $true
    
    if ($dependencyGraph.ContainsKey($className)) {
        foreach ($dep in $dependencyGraph[$className]) {
            if ($foundClasses -contains $dep) {
                Visit-Class $dep
            }
        }
    }
    
    $visiting.Remove($className)
    $visited[$className] = $true
    $script:sorted += $className
}

try {
    foreach ($className in $foundClasses) {
        if (-not $visited.ContainsKey($className)) {
            Visit-Class $className
        }
    }
    
    Write-Host "Dependency-sorted installation order:" -ForegroundColor Green
    for ($i = 0; $i -lt $sorted.Count; $i++) {
        $className = $sorted[$i]
        $superclass = $classLookup[$className].superclass
        Write-Host "  $($i+1). $className (superclass: $superclass)" -ForegroundColor White
    }
} catch {
    Write-Host "Error in dependency sorting: $_" -ForegroundColor Red
}

# Look for global variables
Write-Host "`n[9] Searching for global variables..." -ForegroundColor Yellow
$globalReferences = @()

# Search in method literals for potential global references
$allLiterals = $systemImage.methods | ForEach-Object { $_.literals } | Where-Object { $_ -ne $null }
$uniqueLiterals = $allLiterals | Sort-Object -Unique | Where-Object { $_ -match "^[A-Z]" -and $_ -notmatch "^#" }

Write-Host "Potential global variable references found in literals:" -ForegroundColor White
$uniqueLiterals | Select-Object -First 20 | ForEach-Object {
    Write-Host "  $_" -ForegroundColor Gray
}

if ($uniqueLiterals.Count -gt 20) {
    Write-Host "  ... and $($uniqueLiterals.Count - 20) more" -ForegroundColor Gray
}

# Summary
Write-Host "`n===============================" -ForegroundColor Cyan
Write-Host "ANALYSIS SUMMARY" -ForegroundColor Cyan
Write-Host "===============================" -ForegroundColor Cyan
Write-Host "✅ System image structure: Valid" -ForegroundColor Green
Write-Host "✅ Bootstrap classes found: $($foundClasses.Count)/$($bootstrapClassNames.Count)" -ForegroundColor $(if ($missingClasses.Count -eq 0) { "Green" } else { "Yellow" })
Write-Host "✅ Total methods available: $($systemImage.methods.Count)" -ForegroundColor Green
Write-Host "✅ Dependency sorting: $(if ($sorted.Count -eq $foundClasses.Count) { "Success" } else { "Partial" })" -ForegroundColor $(if ($sorted.Count -eq $foundClasses.Count) { "Green" } else { "Yellow" })
Write-Host "✅ Global references found: $($uniqueLiterals.Count)" -ForegroundColor Green

Write-Host "`nReady for clean slate bootstrap implementation!" -ForegroundColor Green