# Generate Phase 1 dictionary size updates based on Phase 2 method counts

$phase2 = Get-Content "..\Bootstrap\bootstrap_phase2.json" | ConvertFrom-Json

# Growth factor for dictionary efficiency
$growthFactor = 1.5

Write-Host "Phase 1 Method Dictionary Updates"
Write-Host "=================================="
Write-Host ""

# First, fix the critical issue
Write-Host "CRITICAL FIX:"
Write-Host '{ "op": "createMethodDictionary", "class": "SystemDictionary", "capacity": 108, "result": "SystemDictionaryMethodDict" },'
Write-Host ""

Write-Host "SIZE OPTIMIZATIONS:"
Write-Host "{ ""op"": ""createMethodDictionary"", ""class"": ""Object"", ""capacity"": 120, ""result"": ""ObjectMethodDict"" },"
Write-Host "{ ""op"": ""createMethodDictionary"", ""class"": ""Behavior"", ""capacity"": 176, ""result"": ""BehaviorMethodDict"" },"
Write-Host "{ ""op"": ""createMethodDictionary"", ""class"": ""Metaclass"", ""capacity"": 40, ""result"": ""MetaclassMethodDict"" },"
Write-Host ""

Write-Host "NEW DICTIONARIES FOR PHASE 2 CLASSES:"
Write-Host "{ ""comment"": ""-- Additional Phase 2 classes method dictionaries --"" },"

# Classes that need dictionaries
$newClasses = @(
    @{Name="UndefinedObject"; Instance=8; Class=1},
    @{Name="Boolean"; Instance=14; Class=1},
    @{Name="True"; Instance=10; Class=0},
    @{Name="False"; Instance=10; Class=0},
    @{Name="Magnitude"; Instance=10; Class=0},
    @{Name="LookupKey"; Instance=7; Class=1},
    @{Name="Association"; Instance=4; Class=1}
)

foreach ($class in $newClasses) {
    $instanceSize = [Math]::Ceiling($class.Instance * $growthFactor)
    if ($instanceSize -lt 4) { $instanceSize = 4 }
    $instanceSize = [Math]::Ceiling($instanceSize / 4.0) * 4
    
    Write-Host "{ ""op"": ""createMethodDictionary"", ""class"": ""$($class.Name)"", ""capacity"": $instanceSize, ""result"": ""$($class.Name)MethodDict"" },"
    
    if ($class.Class -gt 0) {
        $classSize = [Math]::Ceiling($class.Class * $growthFactor)
        if ($classSize -lt 4) { $classSize = 4 }
        $classSize = [Math]::Ceiling($classSize / 4.0) * 4
        
        Write-Host "{ ""op"": ""createMethodDictionary"", ""class"": ""$($class.Name)Class"", ""capacity"": $classSize, ""result"": ""$($class.Name)ClassMethodDict"" },"
    }
}

Write-Host ""
Write-Host "Note: Insert these after the existing method dictionary definitions in Phase 1"
Write-Host ""
Write-Host "Also needed: Create the Phase 2-only classes in Phase 1:"
Write-Host "==========================================================="
Write-Host ""
Write-Host "{ ""comment"": ""-- Additional classes for Phase 2 support --"" },"
Write-Host "{ ""op"": ""createObject"", ""result"": ""UndefinedObject"" },"
Write-Host "{ ""op"": ""createObject"", ""result"": ""Boolean"" },"
Write-Host "{ ""op"": ""createObject"", ""result"": ""True"" },"
Write-Host "{ ""op"": ""createObject"", ""result"": ""False"" },"
Write-Host "{ ""op"": ""createObject"", ""result"": ""Magnitude"" },"
Write-Host "{ ""op"": ""createObject"", ""result"": ""LookupKey"" },"
Write-Host "{ ""op"": ""createObject"", ""result"": ""Association"" },"
Write-Host ""
Write-Host "{ ""op"": ""createObject"", ""result"": ""UndefinedObjectClass"" },"
Write-Host "{ ""op"": ""createObject"", ""result"": ""BooleanClass"" },"
Write-Host "{ ""op"": ""createObject"", ""result"": ""TrueClass"" },"
Write-Host "{ ""op"": ""createObject"", ""result"": ""FalseClass"" },"
Write-Host "{ ""op"": ""createObject"", ""result"": ""MagnitudeClass"" },"
Write-Host "{ ""op"": ""createObject"", ""result"": ""LookupKeyClass"" },"
Write-Host "{ ""op"": ""createObject"", ""result"": ""AssociationClass"" },"