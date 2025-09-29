# Script to extract and install critical compilation methods from system image

$systemImagePath = "../system_image_compiler.json"
Write-Host "Loading system image..."
$json = Get-Content $systemImagePath | ConvertFrom-Json

# Critical methods we need to install
$criticalMethods = @(
    # Compiler methods
    @{Class="Compiler"; Selector="compile:in:notifying:ifFail:"; IsClassMethod=$false},
    @{Class="Compiler"; Selector="evaluate:in:to:notifying:ifFail:"; IsClassMethod=$false},
    @{Class="Compiler"; Selector="translate:noPattern:ifFail:"; IsClassMethod=$false},
    
    # Parser methods
    @{Class="Parser"; Selector="parse:class:noPattern:context:notifying:ifFail:"; IsClassMethod=$false},
    @{Class="Parser"; Selector="method:context:"; IsClassMethod=$false},
    @{Class="Parser"; Selector="expression"; IsClassMethod=$false},
    @{Class="Parser"; Selector="primaryExpression"; IsClassMethod=$false},
    @{Class="Parser"; Selector="temporaries"; IsClassMethod=$false},
    @{Class="Parser"; Selector="statements:innerBlock:"; IsClassMethod=$false},
    
    # Scanner methods
    @{Class="Scanner"; Selector="scan:"; IsClassMethod=$false},
    @{Class="Scanner"; Selector="scanToken"; IsClassMethod=$false},
    @{Class="Scanner"; Selector="xLetter"; IsClassMethod=$false},
    @{Class="Scanner"; Selector="xDigit"; IsClassMethod=$false},
    
    # MethodNode methods
    @{Class="MethodNode"; Selector="generate:"; IsClassMethod=$false},
    
    # Encoder methods
    @{Class="Encoder"; Selector="init:context:notifying:"; IsClassMethod=$false},
    @{Class="Encoder"; Selector="encodeVariable:"; IsClassMethod=$false},
    @{Class="Encoder"; Selector="encodeLiteral:"; IsClassMethod=$false},
    @{Class="Encoder"; Selector="bindTemp:"; IsClassMethod=$false},
    
    # Behavior compilation methods
    @{Class="Behavior"; Selector="compile:notifying:trailer:ifFail:"; IsClassMethod=$false},
    @{Class="Behavior"; Selector="addSelector:withMethod:"; IsClassMethod=$false},
    @{Class="Class"; Selector="compile:classified:"; IsClassMethod=$false},
    @{Class="ClassDescription"; Selector="compile:classified:notifying:"; IsClassMethod=$false}
)

Write-Host "`nExtracting critical methods for compilation..."
$output = @()

foreach ($methodSpec in $criticalMethods) {
    $class = $json.classes | Where-Object { $_.name -eq $methodSpec.Class }
    if ($class) {
        if ($methodSpec.IsClassMethod) {
            $method = $class.classMethods | Where-Object { $_.selector -eq $methodSpec.Selector }
        } else {
            $method = $class.methods | Where-Object { $_.selector -eq $methodSpec.Selector }
        }
        
        if ($method) {
            Write-Host "  Found: $($methodSpec.Class)>>$($methodSpec.Selector)"
            Write-Host "    Bytecode: [$($method.bytecode -join ', ')]"
            Write-Host "    Literals: $($method.literals.Count)"
            
            $output += [PSCustomObject]@{
                ClassName = $methodSpec.Class
                Selector = $methodSpec.Selector
                IsClassMethod = $methodSpec.IsClassMethod
                Bytecode = $method.bytecode
                Literals = $method.literals
                NumArgs = $method.numArgs
                NumTemps = $method.numTemps
                FrameSize = $method.frameSize
                Primitive = $method.primitive
            }
        } else {
            Write-Host "  WARNING: $($methodSpec.Class)>>$($methodSpec.Selector) not found!"
        }
    } else {
        Write-Host "  WARNING: Class $($methodSpec.Class) not found!"
    }
}

# Save the methods data
$output | ConvertTo-Json -Depth 10 | Set-Content "../compilation_methods.json"
Write-Host "`nSaved $($output.Count) methods to compilation_methods.json"

# Also check Scanner class initialization
$scanner = $json.classes | Where-Object { $_.name -eq "Scanner" }
if ($scanner) {
    $initMethod = $scanner.classMethods | Where-Object { $_.selector -eq "initialize" }
    if ($initMethod) {
        Write-Host "`nScanner class>>initialize found (initializes TypeTable):"
        Write-Host "  Bytecode length: $($initMethod.bytecode.Count)"
    }
}