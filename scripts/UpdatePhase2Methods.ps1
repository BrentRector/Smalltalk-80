# Script to update Phase 2 JSON with complete method information from extracted classes

param(
    [string]$ExtractedDir = "..\extracted",
    [string]$Phase2File = "..\Bootstrap\bootstrap_phase2.json",
    [string]$OutputFile = "..\Bootstrap\bootstrap_phase2_updated.json"
)

# Load the current Phase 2 data
$phase2Data = Get-Content $Phase2File | ConvertFrom-Json

# Classes to update - includes all Phase 1 and Phase 2 classes
$phase2Classes = @(
    # Core hierarchy
    "Object",
    "Behavior",
    "Class",
    "Metaclass",
    # Method infrastructure
    "CompiledMethod",
    "MethodDictionary",
    # Essential data types
    "Symbol",
    "String", 
    "Array",
    "ByteArray",
    # Collection hierarchy
    "Collection",
    "Dictionary",
    "SystemDictionary",
    # Additional Phase 2 classes
    "UndefinedObject",
    "Boolean",
    "True",
    "False",
    "Magnitude",
    "LookupKey",
    "Association"
)

# Update each class with extracted method data
foreach ($className in $phase2Classes) {
    $extractedFile = Join-Path $ExtractedDir "$className.json"
    
    if (Test-Path $extractedFile) {
        Write-Host "Updating $className from extracted data..."
        $extractedData = Get-Content $extractedFile | ConvertFrom-Json
        
        # Find the class in Phase 2 data
        $phase2Class = $phase2Data.classes | Where-Object { $_.name -eq $className }
        
        if (-not $phase2Class) {
            # Add new class if it doesn't exist
            Write-Host "  Adding new class $className to Phase 2..."
            $phase2Class = @{
                name = $className
                superclass = $extractedData.Superclass
                instanceVariables = $extractedData.InstanceVariables
                methods = @()
                classMethods = @()
            }
            $phase2Data.classes += $phase2Class
            $phase2Class = $phase2Data.classes | Where-Object { $_.name -eq $className }
        }
        
        if ($phase2Class) {
            # Clear existing methods and class methods arrays
            $phase2Class.methods = @()
            if ($null -eq $phase2Class.classMethods) {
                $phase2Class | Add-Member -NotePropertyName classMethods -NotePropertyValue @()
            } else {
                $phase2Class.classMethods = @()
            }
            
            # Add instance methods with complete information
            foreach ($method in $extractedData.InstanceMethods) {
                # Convert bytecode string to integer array
                $bytecodeArray = @()
                if ($method.Bytecode) {
                    $bytes = $method.Bytecode -split ' ' | ForEach-Object { [int]"0x$_" }
                    # Ensure it's always an array, even for single byte
                    if ($bytes -is [int]) {
                        $bytecodeArray = @($bytes)
                    } else {
                        $bytecodeArray = $bytes
                    }
                }
                
                $phase2Method = @{
                    selector = $method.Selector
                    source = $method.Source
                    bytecode = $bytecodeArray
                    argumentCount = $method.ArgumentCount
                    temporaryCount = $method.TemporaryCount
                }
                
                # Only add literals if they exist and are strings
                if ($method.Literals -and $method.Literals.Count -gt 0) {
                    $stringLiterals = @()
                    foreach ($literal in $method.Literals) {
                        if ($literal.Value) {
                            $stringLiterals += $literal.Value.ToString()
                        } elseif ($literal -is [string]) {
                            $stringLiterals += $literal
                        } else {
                            $stringLiterals += $literal.ToString()
                        }
                    }
                    if ($stringLiterals.Count -gt 0) {
                        $phase2Method.literals = $stringLiterals
                    }
                }
                
                if ($method.IsPrimitive -and $method.PrimitiveNumber -gt 0) {
                    $phase2Method.primitive = $method.PrimitiveNumber
                }
                
                $phase2Class.methods += $phase2Method
            }
            
            # Add class methods with complete information
            foreach ($method in $extractedData.ClassMethods) {
                # Convert bytecode string to integer array
                $bytecodeArray = @()
                if ($method.Bytecode) {
                    $bytes = $method.Bytecode -split ' ' | ForEach-Object { [int]"0x$_" }
                    # Ensure it's always an array, even for single byte
                    if ($bytes -is [int]) {
                        $bytecodeArray = @($bytes)
                    } else {
                        $bytecodeArray = $bytes
                    }
                }
                
                $phase2Method = @{
                    selector = $method.Selector
                    source = $method.Source
                    bytecode = $bytecodeArray
                    argumentCount = $method.ArgumentCount
                    temporaryCount = $method.TemporaryCount
                }
                
                # Only add literals if they exist and are strings
                if ($method.Literals -and $method.Literals.Count -gt 0) {
                    $stringLiterals = @()
                    foreach ($literal in $method.Literals) {
                        if ($literal.Value) {
                            $stringLiterals += $literal.Value.ToString()
                        } elseif ($literal -is [string]) {
                            $stringLiterals += $literal
                        } else {
                            $stringLiterals += $literal.ToString()
                        }
                    }
                    if ($stringLiterals.Count -gt 0) {
                        $phase2Method.literals = $stringLiterals
                    }
                }
                
                if ($method.IsPrimitive -and $method.PrimitiveNumber -gt 0) {
                    $phase2Method.primitive = $method.PrimitiveNumber
                }
                
                $phase2Class.classMethods += $phase2Method
            }
            
            Write-Host "  Updated with $($phase2Class.methods.Count) instance methods and $($phase2Class.classMethods.Count) class methods"
        } else {
            Write-Host "  Warning: Class $className not found in Phase 2 JSON"
        }
    } else {
        Write-Host "Warning: Extracted file not found for $className"
    }
}

# Update the description
$phase2Data.description = "Phase 2 with complete compiled methods from system image"
$phase2Data.comments = @(
    "This Phase 2 contains all methods from the system image for Phase 2 classes",
    "Methods include full bytecode, literals, and metadata",
    "Generated from extracted system image classes"
)

# Save the updated Phase 2 data
$phase2Data | ConvertTo-Json -Depth 10 | Set-Content $OutputFile
Write-Host "`nPhase 2 JSON updated and saved to: $OutputFile"

# Summary
$totalMethods = 0
$totalClassMethods = 0
foreach ($class in $phase2Data.classes) {
    $totalMethods += $class.methods.Count
    if ($class.classMethods) {
        $totalClassMethods += $class.classMethods.Count
    }
}

Write-Host "`nSummary:"
Write-Host "  Total classes: $($phase2Data.classes.Count)"
Write-Host "  Total instance methods: $totalMethods"
Write-Host "  Total class methods: $totalClassMethods"