# Create Phase 3 manifest from extracted classes - Fixed version
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

Write-Host "Creating Phase 3 manifest (fixed)..." -ForegroundColor Cyan

# Build the manifest structure piece by piece
$manifestLines = @()
$manifestLines += '{'
$manifestLines += '  "$schema": "./bootstrap-schema.json",'
$manifestLines += '  "phase": "3",'
$manifestLines += '  "description": "Minimal self-hosting classes for Smalltalk-80 compilation and file-in",'
$manifestLines += '  "classes": ['

$classCount = 0
foreach ($className in $classOrder) {
    $classFile = Join-Path $extractedDir "$className.json"
    
    if (-not (Test-Path $classFile)) {
        Write-Warning "Class file not found: $classFile"
        continue
    }
    
    Write-Host "  Processing $className..." -ForegroundColor Gray
    
    # Read the extracted class
    $classJson = Get-Content $classFile -Raw
    $classData = $classJson | ConvertFrom-Json
    
    if ($classCount -gt 0) {
        $manifestLines += ','
    }
    
    # Build class definition
    $manifestLines += '    {'
    $manifestLines += "      `"name`": `"$className`","
    $manifestLines += "      `"superclass`": `"$($classData.superclass)`","
    
    # Instance variables
    if ($classData.instanceVariables -and $classData.instanceVariables.Count -gt 0) {
        $instVars = ($classData.instanceVariables | ForEach-Object { "`"$_`"" }) -join ", "
        $manifestLines += "      `"instanceVariables`": [$instVars],"
    } else {
        $manifestLines += '      "instanceVariables": [],'
    }
    
    # Class variables
    if ($classData.classVariables -and $classData.classVariables.Count -gt 0) {
        $classVars = ($classData.classVariables | ForEach-Object { "`"$_`"" }) -join ", "
        $manifestLines += "      `"classVariables`": [$classVars],"
    } else {
        $manifestLines += '      "classVariables": [],'
    }
    
    $manifestLines += '      "poolDictionaries": [],'
    $manifestLines += '      "category": "System-Compiler"'
    $manifestLines += '    }'
    
    $classCount++
}

$manifestLines += '  ],'
$manifestLines += '  "methods": ['

# Now add all methods
$methodCount = 0

foreach ($className in $classOrder) {
    $classFile = Join-Path $extractedDir "$className.json"
    
    if (-not (Test-Path $classFile)) {
        continue
    }
    
    $classJson = Get-Content $classFile -Raw
    $classData = $classJson | ConvertFrom-Json
    
    # Add instance methods
    foreach ($method in $classData.instanceMethods) {
        if ($methodCount -gt 0) {
            $manifestLines += ','
        }
        
        $manifestLines += '    {'
        $manifestLines += "      `"class`": `"$className`","
        $manifestLines += "      `"selector`": `"$($method.selector)`","
        
        # Convert bytecode to array format
        if ($method.bytecode -is [string]) {
            # If bytecode is a hex string like "70 7C", convert to array
            $bytes = $method.bytecode -split ' ' | ForEach-Object { [int]"0x$_" }
            $bytecodeArray = "[" + ($bytes -join ", ") + "]"
        } else {
            $bytecodeArray = "[" + (($method.bytecode | ForEach-Object { [int]$_ }) -join ", ") + "]"
        }
        $manifestLines += "      `"bytecode`": $bytecodeArray,"
        
        # Literals
        if ($method.literals -and $method.literals.Count -gt 0) {
            $literalStrings = @()
            foreach ($lit in $method.literals) {
                if ($lit -is [string]) {
                    $literalStrings += "`"$lit`""
                } elseif ($lit.Value) {
                    $literalStrings += "`"$($lit.Value)`""
                } else {
                    $literalStrings += "`"$lit`""
                }
            }
            $literalsArray = "[" + ($literalStrings -join ", ") + "]"
        } else {
            $literalsArray = "[]"
        }
        $manifestLines += "      `"literals`": $literalsArray,"
        
        $manifestLines += "      `"primitiveIndex`": $([int]$method.primitiveIndex),"
        $manifestLines += "      `"numArgs`": $([int]$method.numArgs),"
        $manifestLines += "      `"numTemps`": $([int]$method.numTemps),"
        $manifestLines += '      "isClassMethod": false'
        $manifestLines += '    }'
        
        $methodCount++
    }
    
    # Add class methods
    foreach ($method in $classData.classMethods) {
        if ($methodCount -gt 0) {
            $manifestLines += ','
        }
        
        $manifestLines += '    {'
        $manifestLines += "      `"class`": `"$className class`","
        $manifestLines += "      `"selector`": `"$($method.selector)`","
        
        # Convert bytecode to array format
        if ($method.bytecode -is [string]) {
            $bytes = $method.bytecode -split ' ' | ForEach-Object { [int]"0x$_" }
            $bytecodeArray = "[" + ($bytes -join ", ") + "]"
        } else {
            $bytecodeArray = "[" + (($method.bytecode | ForEach-Object { [int]$_ }) -join ", ") + "]"
        }
        $manifestLines += "      `"bytecode`": $bytecodeArray,"
        
        # Literals
        if ($method.literals -and $method.literals.Count -gt 0) {
            $literalStrings = @()
            foreach ($lit in $method.literals) {
                if ($lit -is [string]) {
                    $literalStrings += "`"$lit`""
                } elseif ($lit.Value) {
                    $literalStrings += "`"$($lit.Value)`""
                } else {
                    $literalStrings += "`"$lit`""
                }
            }
            $literalsArray = "[" + ($literalStrings -join ", ") + "]"
        } else {
            $literalsArray = "[]"
        }
        $manifestLines += "      `"literals`": $literalsArray,"
        
        $manifestLines += "      `"primitiveIndex`": $([int]$method.primitiveIndex),"
        $manifestLines += "      `"numArgs`": $([int]$method.numArgs),"
        $manifestLines += "      `"numTemps`": $([int]$method.numTemps),"
        $manifestLines += '      "isClassMethod": true'
        $manifestLines += '    }'
        
        $methodCount++
    }
}

# Add enhanced methods for existing Phase 2 classes
Write-Host "  Adding enhanced methods for existing classes..." -ForegroundColor Gray

$enhancedMethods = @(
    @{
        class = "CompiledMethod"
        selector = "header:"
        bytecode = "[16, 96, 124]"
        numArgs = 1
        numTemps = 0
    },
    @{
        class = "CompiledMethod"
        selector = "bytecodes:"
        bytecode = "[16, 97, 124]"
        numArgs = 1
        numTemps = 0
    },
    @{
        class = "CompiledMethod"
        selector = "literals:"
        bytecode = "[16, 98, 124]"
        numArgs = 1
        numTemps = 0
    },
    @{
        class = "Class"
        selector = "methodDictionary"
        bytecode = "[2, 124]"
        numArgs = 0
        numTemps = 0
    },
    @{
        class = "Behavior"
        selector = "addSelector:withMethod:"
        bytecode = "[2, 16, 17, 193, 124]"
        literals = '["at:put:"]'
        numArgs = 2
        numTemps = 0
    }
)

foreach ($method in $enhancedMethods) {
    if ($methodCount -gt 0) {
        $manifestLines += ','
    }
    
    $manifestLines += '    {'
    $manifestLines += "      `"class`": `"$($method.class)`","
    $manifestLines += "      `"selector`": `"$($method.selector)`","
    $manifestLines += "      `"bytecode`": $($method.bytecode),"
    $manifestLines += "      `"literals`": $(if ($method.literals) { $method.literals } else { '[]' }),"
    $manifestLines += "      `"primitiveIndex`": 0,"
    $manifestLines += "      `"numArgs`": $($method.numArgs),"
    $manifestLines += "      `"numTemps`": $($method.numTemps),"
    $manifestLines += '      "isClassMethod": false'
    $manifestLines += '    }'
    
    $methodCount++
}

$manifestLines += '  ]'
$manifestLines += '}'

# Write the manifest
$manifestLines -join "`n" | Set-Content $outputPath -Encoding UTF8

Write-Host "`nPhase 3 Manifest Summary:" -ForegroundColor Green
Write-Host "  Classes: $classCount" -ForegroundColor Yellow
Write-Host "  Methods: $methodCount" -ForegroundColor Yellow

Write-Host "`nPhase 3 manifest saved to: $outputPath" -ForegroundColor Green
Write-Host "File size: $((Get-Item $outputPath).Length / 1KB) KB" -ForegroundColor Gray