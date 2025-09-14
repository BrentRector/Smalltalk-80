# Create Phase 3 manifest from extracted classes
# This script combines all Phase 3 class extractions into a single manifest

$outputPath = "../Bootstrap/phase3_minimal.json"
$extractedDir = "../Smalltalk80.ExtractClass/extracted/phase3"

# Define classes in dependency order
$classOrder = @(
    "SequenceableCollection",  # Parent of OrderedCollection
    "Interval",                 # Another SequenceableCollection
    "OrderedCollection",        # Parent of SortedCollection
    "SortedCollection",         # Specialized OrderedCollection
    "Set",                      # Used by compiler
    "Stream",                   # Base stream class
    "PositionableStream",       # Parent of read/write streams
    "ReadStream",               # Read from collections
    "WriteStream",              # Write to collections
    "ReadWriteStream",          # Both read and write
    "Scanner",                  # Tokenizes source
    "Parser",                   # Creates AST (extends Scanner)
    "Compiler"                  # Compiles to bytecode
)

# Initialize the manifest structure
$manifest = @{
    '$schema' = "./bootstrap-schema.json"
    phase = "3"
    description = "Minimal self-hosting classes for Smalltalk-80 compilation and file-in"
    classes = @()
    methods = @()
}

Write-Host "Creating Phase 3 manifest..." -ForegroundColor Cyan

foreach ($className in $classOrder) {
    $classFile = Join-Path $extractedDir "$className.json"
    
    if (-not (Test-Path $classFile)) {
        Write-Warning "Class file not found: $classFile"
        continue
    }
    
    Write-Host "  Processing $className..." -ForegroundColor Gray
    
    # Read the extracted class
    $classData = Get-Content $classFile -Raw | ConvertFrom-Json
    
    # Add class definition
    $classDef = @{
        name = $classData.name
        superclass = $classData.superclass
        instanceVariables = $classData.instanceVariables
        classVariables = $classData.classVariables
        poolDictionaries = @()
        category = "System-Compiler"
    }
    $manifest.classes += $classDef
    
    # Add instance methods
    foreach ($method in $classData.instanceMethods) {
        $methodDef = @{
            class = $classData.name
            selector = $method.selector
            bytecode = $method.bytecode
            literals = $method.literals
            primitiveIndex = $method.primitiveIndex
            numArgs = $method.numArgs
            numTemps = $method.numTemps
            isClassMethod = $false
        }
        $manifest.methods += $methodDef
    }
    
    # Add class methods
    foreach ($method in $classData.classMethods) {
        $methodDef = @{
            class = "$($classData.name) class"
            selector = $method.selector
            bytecode = $method.bytecode
            literals = $method.literals
            primitiveIndex = $method.primitiveIndex
            numArgs = $method.numArgs
            numTemps = $method.numTemps
            isClassMethod = $true
        }
        $manifest.methods += $methodDef
    }
}

# Add enhanced methods for existing Phase 2 classes
Write-Host "  Adding enhanced methods for existing classes..." -ForegroundColor Gray

# CompiledMethod enhancements
$enhancedMethods = @(
    @{
        class = "CompiledMethod"
        selector = "header:"
        bytecode = @(16, 96, 124)  # temp0, store in inst var 0, return self
        literals = @()
        primitiveIndex = 0
        numArgs = 1
        numTemps = 0
        isClassMethod = $false
    },
    @{
        class = "CompiledMethod"
        selector = "bytecodes:"
        bytecode = @(16, 97, 124)  # temp0, store in inst var 1, return self
        literals = @()
        primitiveIndex = 0
        numArgs = 1
        numTemps = 0
        isClassMethod = $false
    },
    @{
        class = "CompiledMethod"
        selector = "literals:"
        bytecode = @(16, 98, 124)  # temp0, store in inst var 2, return self
        literals = @()
        primitiveIndex = 0
        numArgs = 1
        numTemps = 0
        isClassMethod = $false
    },
    @{
        class = "Class"
        selector = "methodDictionary"
        bytecode = @(2, 124)  # inst var 2, return
        literals = @()
        primitiveIndex = 0
        numArgs = 0
        numTemps = 0
        isClassMethod = $false
    },
    @{
        class = "Behavior"
        selector = "addSelector:withMethod:"
        bytecode = @(2, 16, 17, 193, 124)  # methodDict, selector, method, at:put:, return
        literals = @("at:put:")
        primitiveIndex = 0
        numArgs = 2
        numTemps = 0
        isClassMethod = $false
    }
)

foreach ($method in $enhancedMethods) {
    $manifest.methods += $method
}

# Summary statistics
$totalClasses = $manifest.classes.Count
$totalMethods = $manifest.methods.Count
$instanceMethods = ($manifest.methods | Where-Object { -not $_.isClassMethod }).Count
$classMethods = ($manifest.methods | Where-Object { $_.isClassMethod }).Count

Write-Host "`nPhase 3 Manifest Summary:" -ForegroundColor Green
Write-Host "  Classes: $totalClasses" -ForegroundColor Yellow
Write-Host "  Methods: $totalMethods" -ForegroundColor Yellow
Write-Host "    Instance methods: $instanceMethods" -ForegroundColor Gray
Write-Host "    Class methods: $classMethods" -ForegroundColor Gray

# Convert to JSON with proper formatting
$jsonOptions = @{
    Depth = 10
    Compress = $false
}

$json = $manifest | ConvertTo-Json @jsonOptions

# Ensure arrays are properly formatted even for single items
$json = $json -replace '(?<="instanceVariables":\s*)"([^"]*)"', '[$1]'
$json = $json -replace '(?<="classVariables":\s*)"([^"]*)"', '[$1]'
$json = $json -replace '(?<="literals":\s*)"([^"]*)"', '[$1]'
$json = $json -replace '(?<="bytecode":\s*)(\d+)(?=[,\s\}])', '[$1]'

# Write the manifest
$json | Set-Content $outputPath -Encoding UTF8

Write-Host "`nPhase 3 manifest saved to: $outputPath" -ForegroundColor Green
Write-Host "File size: $((Get-Item $outputPath).Length / 1KB) KB" -ForegroundColor Gray