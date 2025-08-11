# Consolidate Phase 1 and Phase 2 into a single unified bootstrap
# This creates a new bootstrap_phase1.json that contains all classes and methods

Write-Host "Consolidating Bootstrap Phases..." -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Read current Phase 2
$phase2Path = "..\Bootstrap\bootstrap_phase2.json"
$phase2 = Get-Content $phase2Path -Raw | ConvertFrom-Json

# Define the complete class ordering (superclasses before subclasses)
$classOrder = @(
    # Core root classes
    "Object",
    
    # Behavior hierarchy (must come before any class/metaclass work)
    "Behavior",
    "ClassDescription", 
    "Class",
    "Metaclass",
    
    # Method infrastructure (needed for method installation)
    "CompiledMethod",
    
    # Collection hierarchy (needed for method dictionaries)
    "Collection",
    "Dictionary", 
    "MethodDictionary",
    "SystemDictionary",
    
    # Basic data types
    "Magnitude",
    "LookupKey",
    "Association",
    "SequenceableCollection",
    "ArrayedCollection",
    "Array",
    "ByteArray", 
    "String",
    "Symbol",
    
    # Boolean hierarchy
    "Boolean",
    "True", 
    "False",
    "UndefinedObject",
    
    # Additional Phase 2 classes
    "ClassOrganizer"
)

# Additional classes from Phase 2 that aren't in the explicit order
$phase2ClassNames = $phase2.classes | ForEach-Object { $_.name }
$additionalClasses = $phase2ClassNames | Where-Object { $_ -notin $classOrder }

if ($additionalClasses) {
    Write-Host "Found additional classes not in explicit order:" -ForegroundColor Yellow
    $additionalClasses | ForEach-Object { Write-Host "  - $_" }
    $classOrder += $additionalClasses
}

# Build ordered class list from Phase 2 data
$orderedClasses = @()
$classesDict = @{}

# Create lookup dictionary
foreach ($class in $phase2.classes) {
    $classesDict[$class.name] = $class
}

# Add classes in dependency order
foreach ($className in $classOrder) {
    if ($classesDict.ContainsKey($className)) {
        $orderedClasses += $classesDict[$className]
        Write-Host "✓ Added class: $className" -ForegroundColor Green
    }
    else {
        Write-Host "⚠ Class not found in Phase 2: $className" -ForegroundColor Yellow
    }
}

# Check for any missed classes
foreach ($class in $phase2.classes) {
    if ($class.name -notin $classOrder) {
        Write-Host "⚠ Class $($class.name) not in order list, adding at end" -ForegroundColor Yellow
        $orderedClasses += $class
    }
}

# Create the new consolidated bootstrap
$newBootstrap = @{
    phase = "1-unified"
    description = "Unified bootstrap containing all classes and methods (formerly Phase 1 + Phase 2)"
    comments = @(
        "This is the consolidated bootstrap that replaces the old Phase 1/Phase 2 split",
        "All classes are created with full structure and methods in a single phase",
        "Classes are ordered with superclasses before subclasses",
        "Generated from clean extraction of Smalltalk-80 system image"
    )
    timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    classes = $orderedClasses
}

# Save the new bootstrap
$outputPath = "..\Bootstrap\bootstrap_phase1_unified.json"
$newBootstrap | ConvertTo-Json -Depth 10 | Set-Content $outputPath -Encoding UTF8

Write-Host ""
Write-Host "=================================" -ForegroundColor Cyan
Write-Host "Consolidation Complete!" -ForegroundColor Green
Write-Host "Created: $outputPath" -ForegroundColor Green
Write-Host "Total classes: $($orderedClasses.Count)" -ForegroundColor Cyan

# Statistics
$totalMethods = 0
$orderedClasses | ForEach-Object {
    if ($_.methods) {
        $totalMethods += $_.methods.Count
    }
    if ($_.classMethods) {
        $totalMethods += $_.classMethods.Count
    }
}
Write-Host "Total methods: $totalMethods" -ForegroundColor Cyan