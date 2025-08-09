# Verify Phase 3 methods are loaded with correct bytecode
param(
    [string]$Class = "Object",
    [string]$Method = "isNil"
)

Write-Host "Verifying Phase 3 Method Loading" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Create a test Smalltalk file that will inspect methods
$testCode = @"
| cls method |
cls := Smalltalk at: #$Class.
cls ifNil: [Transcript show: 'Class $Class not found'; cr. ^nil].

Transcript show: '=== Class: $Class ==='; cr.
Transcript show: 'Superclass: ', cls superclass printString; cr.
Transcript show: 'Instance methods: ', cls selectors size printString; cr.

"Show a specific method"
method := cls compiledMethodAt: #$Method.
method ifNil: [
    Transcript show: 'Method $Method not found in $Class'; cr
] ifNotNil: [
    Transcript show: ''; cr.
    Transcript show: 'Method: ', #$Method; cr.
    Transcript show: '  Arguments: ', method numArgs printString; cr.
    Transcript show: '  Temporaries: ', method numTemps printString; cr.
    Transcript show: '  Bytecode size: ', method size printString; cr.
    Transcript show: '  Bytecode: '.
    1 to: method size do: [:i |
        Transcript show: (method at: i) printString, ' '
    ].
    Transcript cr.
    
    "Show literals if any"
    method numLiterals > 0 ifTrue: [
        Transcript show: '  Literals: '; cr.
        1 to: method numLiterals do: [:i |
            Transcript show: '    ', i printString, ': ', (method literalAt: i) printString; cr
        ]
    ]
].

"List all instance methods"
Transcript show: ''; cr.
Transcript show: 'All instance methods:'; cr.
cls selectors do: [:sel |
    Transcript show: '  ', sel; cr
].

"Check class methods"
Transcript show: ''; cr.
Transcript show: '=== Metaclass: $Class class ==='; cr.
Transcript show: 'Class methods: ', cls class selectors size printString; cr.
cls class selectors isEmpty ifFalse: [
    Transcript show: 'All class methods:'; cr.
    cls class selectors do: [:sel |
        Transcript show: '  ', sel; cr
    ]
].
"@

# Save test code to a file
$testFile = "temp\verify_methods.st"
New-Item -ItemType Directory -Path "temp" -Force | Out-Null
$testCode | Set-Content -Path $testFile

Write-Host "Verifying class: $Class" -ForegroundColor Yellow
Write-Host "Looking for method: $Method" -ForegroundColor Yellow
Write-Host ""

# Run the interpreter with all Phase 3 files and the test code
$cmd = @"
.\bin\Debug\net9.0\Smalltalk80.Interpreter.exe --skip-phase3 ``
    --json Bootstrap\phase3_final\Object.json ``
    --json Bootstrap\phase3_final\Boolean.json ``
    --json Bootstrap\phase3_final\True.json ``
    --json Bootstrap\phase3_final\False.json ``
    --json Bootstrap\phase3_final\UndefinedObject.json ``
    --json Bootstrap\phase3_final\Array.json ``
    --json Bootstrap\phase3_final\String.json ``
    --json Bootstrap\phase3_final\Symbol.json ``
    --json Bootstrap\phase3_final\Collection.json ``
    --json Bootstrap\phase3_final\Dictionary.json ``
    --json Bootstrap\phase3_final\Behavior.json ``
    --json Bootstrap\phase3_final\Class.json ``
    --json Bootstrap\phase3_final\Metaclass.json ``
    --json Bootstrap\phase3_final\CompiledMethod.json ``
    --source $testFile
"@

Invoke-Expression $cmd