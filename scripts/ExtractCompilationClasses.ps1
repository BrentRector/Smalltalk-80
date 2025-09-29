# Extract compilation classes from system image JSON and prepare them for cold start

$systemImagePath = "../system_image_compiler.json"
$outputPath = "../Smalltalk80.Interpreter/Bootstrap/CompilationClasses.cs"

Write-Host "Loading system image from $systemImagePath..."
$json = Get-Content $systemImagePath | ConvertFrom-Json

# Classes we need for compilation support
$compilationClasses = @(
    "Scanner",
    "ParseNode", 
    "LeafNode",
    "VariableNode",
    "LiteralNode",
    "MessageNode",
    "AssignmentNode", 
    "ReturnNode",
    "BlockNode",
    "CascadeNode",
    "MethodNode",
    "Encoder",
    "Parser",
    "Compiler",
    "SelectorNode",
    "SpecialLiteralNode",
    "SpecialSelectorNode"
)

Write-Host "`nExtracting compilation classes..."

$classData = @{}

foreach ($className in $compilationClasses) {
    $class = $json.classes | Where-Object { $_.name -eq $className }
    if ($class) {
        Write-Host "  Found: $className (superclass: $($class.superclass))"
        
        # Get instance methods
        $instanceMethods = $json.methods | Where-Object { 
            $_.className -eq $className -and -not $_.isClassMethod 
        }
        
        # Get class methods
        $classMethods = $json.methods | Where-Object { 
            $_.className -eq $className -and $_.isClassMethod 
        }
        
        $classData[$className] = @{
            Definition = $class
            InstanceMethods = $instanceMethods
            ClassMethods = $classMethods
        }
        
        Write-Host "    - Instance methods: $($instanceMethods.Count)"
        Write-Host "    - Class methods: $($classMethods.Count)"
    } else {
        Write-Host "  WARNING: $className not found in system image"
    }
}

Write-Host "`nGenerating summary..."
Write-Host "Total classes extracted: $($classData.Count)"

# Save extracted data as JSON for reference
$classData | ConvertTo-Json -Depth 10 | Set-Content "../compilation_classes_data.json"
Write-Host "Saved compilation class data to compilation_classes_data.json"

# Output key information for implementation
Write-Host "`n=== Implementation Summary ==="
Write-Host "Classes to add to cold start (in dependency order):"
Write-Host "1. ParseNode (base class)"
Write-Host "2. LeafNode -> VariableNode, LiteralNode, SelectorNode, SpecialLiteralNode"
Write-Host "3. Scanner"
Write-Host "4. Encoder (subclass of ParseNode)"
Write-Host "5. MessageNode, AssignmentNode, ReturnNode, BlockNode, CascadeNode, MethodNode"
Write-Host "6. Parser (subclass of Scanner)"
Write-Host "7. Compiler"

Write-Host "`nKey methods to implement:"
$criticalMethods = @(
    @{Class="Scanner"; Method="scanToken"},
    @{Class="Parser"; Method="parse:class:noPattern:context:notifying:ifFail:"},
    @{Class="Compiler"; Method="compile:in:notifying:ifFail:"},
    @{Class="MethodNode"; Method="generate:"},
    @{Class="Encoder"; Method="init:context:notifying:"}
)

foreach ($item in $criticalMethods) {
    $method = $json.methods | Where-Object { 
        $_.className -eq $item.Class -and 
        $_.selector -eq $item.Method -and 
        -not $_.isClassMethod 
    }
    if ($method) {
        Write-Host "  $($item.Class)>>$($item.Method) - bytecode length: $($method.bytecode.Count)"
    }
}