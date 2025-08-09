#!/usr/bin/env pwsh
# Create Phase 2.5 bootstrap file from extracted classes

Write-Host "Creating Phase 2.5 Bootstrap" -ForegroundColor Cyan
Write-Host "============================" -ForegroundColor Cyan
Write-Host ""

$rootPath = Split-Path $PSScriptRoot -Parent
$extractedPath = Join-Path $rootPath "extracted"
$bootstrapPath = Join-Path $rootPath "Bootstrap"
$phase25Path = Join-Path $bootstrapPath "bootstrap_phase2_5.json"

# Classes needed for Phase 2.5 (in dependency order)
$phase25Classes = @(
    "Object",
    "ClassDescription", 
    "Metaclass",
    "ClassOrganizer",
    "SequenceableCollection",
    "ArrayedCollection",
    "Array",
    "ByteArray",
    "String",
    "Symbol"
)

# Initialize Phase 2.5 structure
$phase25 = @{
    description = "Phase 2.5 - Minimal classes for pure message-based class definition"
    version = "2.5"
    timestamp = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    classes = @()
    notes = @(
        "These classes enable pure message-based class creation",
        "Object provides root of hierarchy",
        "Metaclass enables class/metaclass duality",
        "Symbol provides selector internment",
        "Array/ByteArray provide data structures",
        "String provides name manipulation",
        "ClassOrganizer provides method categorization"
    )
}

# Process each class
foreach ($className in $phase25Classes) {
    $extractedFile = Join-Path $extractedPath "$className.json"
    
    if (Test-Path $extractedFile) {
        Write-Host "Processing $className..." -ForegroundColor Yellow
        $classData = Get-Content $extractedFile -Raw | ConvertFrom-Json
        
        # Create class entry
        $classEntry = @{
            name = $className
            superclass = $classData.Superclass
            instanceVariables = @()
            classVariables = @()
            methods = @()
            classMethods = @()
        }
        
        # Add instance variables if present
        if ($classData.InstanceVariables -and $classData.InstanceVariables.Count -gt 0) {
            $classEntry.instanceVariables = $classData.InstanceVariables
        }
        
        # Add class variables if present  
        if ($classData.ClassVariables -and $classData.ClassVariables.Count -gt 0) {
            $classEntry.classVariables = $classData.ClassVariables
        }
        
        # Process instance methods
        $instanceMethodCount = 0
        if ($classData.InstanceMethods) {
            foreach ($method in $classData.InstanceMethods) {
                # Convert bytecode from hex string to integer array
                $bytecode = @()
                if ($method.Bytecode) {
                    if ($method.Bytecode -is [string]) {
                        $bytes = $method.Bytecode -split '\s+' | Where-Object { $_ } | ForEach-Object {
                            [Convert]::ToInt32($_, 16)
                        }
                        $bytecode = @($bytes)
                    } else {
                        $bytecode = @($method.Bytecode)
                    }
                }
                
                # Convert literals to simple format
                $literals = @()
                if ($method.Literals) {
                    foreach ($lit in $method.Literals) {
                        if ($lit -is [PSCustomObject] -and $lit.Value) {
                            $literals += $lit.Value
                        } elseif ($lit -is [string] -or $lit -is [int]) {
                            $literals += $lit
                        }
                    }
                }
                
                $methodEntry = @{
                    selector = $method.Selector
                    bytecode = $bytecode
                    argumentCount = $method.ArgumentCount
                    temporaryCount = $method.TemporaryCount
                }
                
                if ($literals.Count -gt 0) {
                    $methodEntry.literals = $literals
                }
                
                if ($method.PrimitiveNumber -and $method.PrimitiveNumber -gt 0) {
                    $methodEntry.primitiveNumber = $method.PrimitiveNumber
                }
                
                $classEntry.methods += $methodEntry
                $instanceMethodCount++
            }
        }
        
        # Process class methods
        $classMethodCount = 0
        if ($classData.ClassMethods) {
            foreach ($method in $classData.ClassMethods) {
                # Convert bytecode from hex string to integer array
                $bytecode = @()
                if ($method.Bytecode) {
                    if ($method.Bytecode -is [string]) {
                        $bytes = $method.Bytecode -split '\s+' | Where-Object { $_ } | ForEach-Object {
                            [Convert]::ToInt32($_, 16)
                        }
                        $bytecode = @($bytes)
                    } else {
                        $bytecode = @($method.Bytecode)
                    }
                }
                
                # Convert literals to simple format
                $literals = @()
                if ($method.Literals) {
                    foreach ($lit in $method.Literals) {
                        if ($lit -is [PSCustomObject] -and $lit.Value) {
                            $literals += $lit.Value
                        } elseif ($lit -is [string] -or $lit -is [int]) {
                            $literals += $lit
                        }
                    }
                }
                
                $methodEntry = @{
                    selector = $method.Selector
                    bytecode = $bytecode
                    argumentCount = $method.ArgumentCount
                    temporaryCount = $method.TemporaryCount
                }
                
                if ($literals.Count -gt 0) {
                    $methodEntry.literals = $literals
                }
                
                if ($method.PrimitiveNumber -and $method.PrimitiveNumber -gt 0) {
                    $methodEntry.primitiveNumber = $method.PrimitiveNumber
                }
                
                $classEntry.classMethods += $methodEntry
                $classMethodCount++
            }
        }
        
        Write-Host "  ✓ Added $instanceMethodCount instance methods, $classMethodCount class methods" -ForegroundColor Green
        $phase25.classes += $classEntry
        
    } else {
        Write-Host "  ✗ Warning: $className not found in extracted/" -ForegroundColor Red
    }
}

# Add critical missing methods that aren't in the extraction
Write-Host ""
Write-Host "Adding critical missing methods..." -ForegroundColor Yellow

# Find CompiledMethod bytecode access methods
$compiledMethodAdditions = @{
    name = "CompiledMethod"
    methodAdditions = @(
        @{
            selector = "byteAt:"
            bytecode = @(112, 16, 247, 124)
            argumentCount = 1
            temporaryCount = 0
            primitiveNumber = 143
            comment = "Access bytecode at index"
        },
        @{
            selector = "byteAt:put:"
            bytecode = @(112, 16, 17, 248, 124)
            argumentCount = 2
            temporaryCount = 0
            primitiveNumber = 144
            comment = "Store byte at index"
        }
    )
    classMethodAdditions = @(
        @{
            selector = "newMethod:header:"
            bytecode = @(16, 17, 250, 124)
            argumentCount = 2
            temporaryCount = 0
            primitiveNumber = 79
            comment = "Create method with size and header"
        }
    )
}

# Add as a separate section for method additions to existing Phase 2 classes
$phase25.methodAdditions = @($compiledMethodAdditions)

Write-Host "  ✓ Added CompiledMethod byte access methods" -ForegroundColor Green

# Save Phase 2.5 bootstrap file
Write-Host ""
Write-Host "Saving Phase 2.5 bootstrap..." -ForegroundColor Yellow
$phase25 | ConvertTo-Json -Depth 10 | Set-Content $phase25Path -Encoding UTF8
Write-Host "✓ Saved to $phase25Path" -ForegroundColor Green

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Phase 2.5 Bootstrap Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$totalClasses = $phase25.classes.Count
$totalInstanceMethods = 0
$totalClassMethods = 0

foreach ($class in $phase25.classes) {
    $totalInstanceMethods += $class.methods.Count
    $totalClassMethods += $class.classMethods.Count
    Write-Host "$($class.name): $($class.methods.Count) instance, $($class.classMethods.Count) class methods" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Total Classes: $totalClasses" -ForegroundColor Yellow
Write-Host "Total Instance Methods: $totalInstanceMethods" -ForegroundColor Green
Write-Host "Total Class Methods: $totalClassMethods" -ForegroundColor Cyan
Write-Host "Grand Total Methods: $($totalInstanceMethods + $totalClassMethods)" -ForegroundColor Magenta

Write-Host ""
Write-Host "Key capabilities added:" -ForegroundColor Yellow
Write-Host "  • Object - root of class hierarchy" -ForegroundColor White
Write-Host "  • Metaclass - enables class/metaclass duality" -ForegroundColor White
Write-Host "  • Symbol intern: - selector creation from strings" -ForegroundColor White
Write-Host "  • Array/ByteArray - data structure support" -ForegroundColor White
Write-Host "  • String asSymbol - string to symbol conversion" -ForegroundColor White
Write-Host "  • ClassOrganizer - method categorization" -ForegroundColor White
Write-Host "  • CompiledMethod byteAt:put: - bytecode installation" -ForegroundColor White