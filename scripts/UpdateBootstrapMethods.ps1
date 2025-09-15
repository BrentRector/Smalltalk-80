# Script to update bootstrap JSON with complete method information from extracted classes

param(
    [string]$ExtractedDir = "..\extracted",
    [string]$BootstrapFile = "..\Bootstrap\bootstrap.json",
    [string]$OutputFile = "..\Bootstrap\bootstrap_updated.json"
)

# Load the current bootstrap data
$bootstrapData = Get-Content $BootstrapFile | ConvertFrom-Json

# Classes to update - includes all bootstrap classes
$bootstrapClasses = @(
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
foreach ($className in $bootstrapClasses) {
    $extractedFile = Join-Path $ExtractedDir "$className.json"
    
    if (Test-Path $extractedFile) {
        Write-Host "Updating $className from extracted data..."
        $extractedData = Get-Content $extractedFile | ConvertFrom-Json
        
        # Find the class in bootstrap data
        $bootstrapClass = $bootstrapData.classes | Where-Object { $_.name -eq $className }
        
        if (-not $bootstrapClass) {
            # Add new class if it doesn't exist
            Write-Host "  Adding new class $className to bootstrap..."
            $bootstrapClass = @{
                name = $className
                superclass = $extractedData.Superclass
                instanceVariables = $extractedData.InstanceVariables
                methods = @()
                classMethods = @()
            }
            $bootstrapData.classes += $bootstrapClass
            $bootstrapClass = $bootstrapData.classes | Where-Object { $_.name -eq $className }
        }
        
        if ($bootstrapClass) {
            # Clear existing methods and class methods arrays
            $bootstrapClass.methods = @()
            if ($null -eq $bootstrapClass.classMethods) {
                $bootstrapClass | Add-Member -NotePropertyName classMethods -NotePropertyValue @()
            } else {
                $bootstrapClass.classMethods = @()
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
                
                $bootstrapMethod = @{
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
                        $bootstrapMethod.literals = $stringLiterals
                    }
                }
                
                if ($method.IsPrimitive -and $method.PrimitiveNumber -gt 0) {
                    $bootstrapMethod.primitive = $method.PrimitiveNumber
                }
                
                $bootstrapClass.methods += $bootstrapMethod
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
                
                $bootstrapMethod = @{
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
                        $bootstrapMethod.literals = $stringLiterals
                    }
                }
                
                if ($method.IsPrimitive -and $method.PrimitiveNumber -gt 0) {
                    $bootstrapMethod.primitive = $method.PrimitiveNumber
                }
                
                $bootstrapClass.classMethods += $bootstrapMethod
            }
            
            Write-Host "  Updated with $($bootstrapClass.methods.Count) instance methods and $($bootstrapClass.classMethods.Count) class methods"
        } else {
            Write-Host "  Warning: Class $className not found in bootstrap JSON"
        }
    } else {
        Write-Host "Warning: Extracted file not found for $className"
    }
}

# Update the description
$bootstrapData.description = "Bootstrap with complete compiled methods from system image"
$bootstrapData.comments = @(
    "This bootstrap contains all methods from the system image for bootstrap classes",
    "Methods include full bytecode, literals, and metadata",
    "Generated from extracted system image classes"
)

# Save the updated bootstrap data
$bootstrapData | ConvertTo-Json -Depth 10 | Set-Content $OutputFile
Write-Host "`nBootstrap JSON updated and saved to: $OutputFile"

# Summary
$totalMethods = 0
$totalClassMethods = 0
foreach ($class in $bootstrapData.classes) {
    $totalMethods += $class.methods.Count
    if ($class.classMethods) {
        $totalClassMethods += $class.classMethods.Count
    }
}

Write-Host "`nSummary:"
Write-Host "  Total classes: $($bootstrapData.classes.Count)"
Write-Host "  Total instance methods: $totalMethods"
Write-Host "  Total class methods: $totalClassMethods"