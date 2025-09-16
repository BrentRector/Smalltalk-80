# UpdateBootstrapFromSystemImage.ps1
# Updates bootstrap.json with corrected class definitions and method bytecodes from the system image JSON
#
# Usage: pwsh UpdateBootstrapFromSystemImage.ps1 [-SystemImagePath "complete_system_final.json"] [-BootstrapPath "../Bootstrap/bootstrap.json"]

param(
    [string]$SystemImagePath = "../complete_system_final.json",
    [string]$BootstrapPath = "../Bootstrap/bootstrap.json",
    [string]$OutputPath = "../Bootstrap/bootstrap_updated.json"
)

Write-Host "Bootstrap Update from System Image" -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Green
Write-Host ""

# Verify input files exist
if (!(Test-Path $SystemImagePath)) {
    Write-Host "ERROR: System image not found at: $SystemImagePath" -ForegroundColor Red
    Write-Host "Run: dotnet bin/Debug/net9.0/StandaloneCompiler.dll 'Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st' complete_system_final.json" -ForegroundColor Yellow
    exit 1
}

if (!(Test-Path $BootstrapPath)) {
    Write-Host "ERROR: Bootstrap file not found at: $BootstrapPath" -ForegroundColor Red
    exit 1
}

Write-Host "Loading system image: $SystemImagePath"
$systemImage = Get-Content $SystemImagePath | ConvertFrom-Json

Write-Host "Loading current bootstrap: $BootstrapPath"
$bootstrap = Get-Content $BootstrapPath | ConvertFrom-Json

# Core bootstrap classes that must be updated
$coreClasses = @(
    "Object", "Behavior", "Class", "Metaclass",
    "CompiledMethod", "MethodDictionary", 
    "Symbol", "String", "Array", "ByteArray",
    "Collection", "Dictionary", "SystemDictionary",
    "UndefinedObject", "Boolean", "True", "False",
    "Magnitude", "LookupKey", "Association",
    "SequenceableCollection", "ArrayedCollection", "IdentityDictionary"
)

Write-Host "Updating $($coreClasses.Count) core bootstrap classes..."

foreach ($className in $coreClasses) {
    Write-Host "  Processing: $className" -ForegroundColor Cyan
    
    # Find class in system image
    $systemClass = $systemImage.Classes | Where-Object { $_.Name -eq $className }
    if (!$systemClass) {
        Write-Host "    WARNING: Class '$className' not found in system image" -ForegroundColor Yellow
        continue
    }
    
    # Find class in bootstrap
    $bootstrapClass = $bootstrap.classes | Where-Object { $_.name -eq $className }
    if (!$bootstrapClass) {
        Write-Host "    WARNING: Class '$className' not found in bootstrap" -ForegroundColor Yellow
        continue
    }
    
    # Update instance variables with inherited chain
    $allInstanceVars = @()
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
    
    # Collect all instance variables in inheritance order
    foreach ($cls in $hierarchy) {
        if ($cls.InstanceVariables) {
            $allInstanceVars += $cls.InstanceVariables
        }
    }
    
    # Update bootstrap class definition
    $bootstrapClass.instanceVariables = $allInstanceVars
    if ($systemClass.ClassVariables) {
        $bootstrapClass.classVariables = $systemClass.ClassVariables
    }
    
    Write-Host "    Updated instance vars: [$($allInstanceVars -join ', ')]" -ForegroundColor Gray
    
    # Update instance methods
    if ($systemClass.Methods) {
        $bootstrapClass.methods = @()
        foreach ($method in $systemClass.Methods) {
            $bootstrapMethod = @{
                selector = $method.Selector
                bytecode = $method.Bytecode
                literals = $method.Literals
                numArgs = $method.NumArgs
                numTemps = $method.NumTemps
                primitiveIndex = $method.PrimitiveIndex
                source = if ($method.Source) { $method.Source } else { "" }
                isClassMethod = $false
            }
            $bootstrapClass.methods += $bootstrapMethod
        }
        Write-Host "    Updated $($systemClass.Methods.Count) instance methods" -ForegroundColor Gray
    }
    
    # Update class methods
    if ($systemClass.ClassMethods) {
        $bootstrapClass.classMethods = @()
        foreach ($method in $systemClass.ClassMethods) {
            $bootstrapMethod = @{
                selector = $method.Selector
                bytecode = $method.Bytecode
                literals = $method.Literals
                numArgs = $method.NumArgs
                numTemps = $method.NumTemps
                primitiveIndex = $method.PrimitiveIndex
                source = if ($method.Source) { $method.Source } else { "" }
                isClassMethod = $true
            }
            $bootstrapClass.classMethods += $bootstrapMethod
        }
        Write-Host "    Updated $($systemClass.ClassMethods.Count) class methods" -ForegroundColor Gray
    }
}

# Update timestamp and comments
$bootstrap.timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$bootstrap.comments = @(
    "This is the consolidated bootstrap updated from corrected system image",
    "All classes have proper inherited instance variable layouts", 
    "Method bytecodes use correct field offsets for inheritance",
    "Generated from complete_system_final.json with field offset corrections",
    "Updated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
)

Write-Host ""
Write-Host "Saving updated bootstrap to: $OutputPath" -ForegroundColor Green
$bootstrap | ConvertTo-Json -Depth 10 | Set-Content $OutputPath

Write-Host ""
Write-Host "Bootstrap update completed successfully!" -ForegroundColor Green
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Review the updated bootstrap file" -ForegroundColor Yellow
Write-Host "2. Copy $OutputPath to $BootstrapPath if correct" -ForegroundColor Yellow
Write-Host "3. Run verification: dotnet bin/Debug/net9.0/Smalltalk80.Interpreter.dll --list-bootstrap-classes" -ForegroundColor Yellow