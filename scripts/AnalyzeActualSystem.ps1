# Analyze what's actually loaded in the interpreter
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SYSTEM ANALYSIS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Run interpreter and get output
Write-Host "Running interpreter..." -ForegroundColor Yellow
$interpreterExe = Resolve-Path "../bin/Debug/net9.0/Smalltalk80.Interpreter.exe"
& $interpreterExe --verbose-level 3 --show-bytecode --no-mnemonics > system_full.txt 2>&1

# Count classes and methods
$content = Get-Content system_full.txt -Raw
$classes = [regex]::Matches($content, 'Class: (.+?)$', 'Multiline') | ForEach-Object { $_.Groups[1].Value }
$methods = [regex]::Matches($content, '^\s+(\w+(?::\w*)*)\s*$', 'Multiline')
$bytecodes = [regex]::Matches($content, '^\s+Bytecode:\s+([0-9A-Fa-f]{2})', 'Multiline')

Write-Host ""
Write-Host "System Statistics:" -ForegroundColor Yellow
Write-Host "  Total Classes: $($classes.Count)" -ForegroundColor Cyan
Write-Host "  Total Methods: $($methods.Count)" -ForegroundColor Cyan
Write-Host "  Methods with Bytecode: $($bytecodes.Count)" -ForegroundColor Cyan

# Group by class type
$regularClasses = $classes | Where-Object { $_ -notmatch 'Class$' -and $_ -notmatch ' class$' }
$metaclasses = $classes | Where-Object { $_ -match 'Class$' -or $_ -match ' class$' }

Write-Host ""
Write-Host "Class Breakdown:" -ForegroundColor Yellow
Write-Host "  Regular Classes: $($regularClasses.Count)" -ForegroundColor Gray
Write-Host "  Metaclasses: $($metaclasses.Count)" -ForegroundColor Gray

# Compare with bootstrap sources
Write-Host ""
Write-Host "Bootstrap Sources:" -ForegroundColor Yellow

$bootstrap = Get-Content "../Bootstrap/bootstrap.json" -Raw | ConvertFrom-Json
$bootstrapMethods = 0
foreach ($class in $bootstrap.classes) {
    $bootstrapMethods += ($class.instanceMethods ?? @()).Count
    $bootstrapMethods += ($class.classMethods ?? @()).Count
}
Write-Host "  bootstrap.json: $($bootstrap.classes.Count) classes, $bootstrapMethods methods" -ForegroundColor Gray

if (Test-Path "../Bootstrap/phase3_minimal.json") {
    $phase3 = Get-Content "../Bootstrap/phase3_minimal.json" -Raw | ConvertFrom-Json
    $phase3Classes = ($phase3.classes ?? @()).Count
    $phase3Methods = ($phase3.methods ?? @()).Count
    Write-Host "  phase3_minimal.json: $phase3Classes classes, $phase3Methods methods" -ForegroundColor Gray
}

# Sample some classes to see what's there
Write-Host ""
Write-Host "Sample Classes:" -ForegroundColor Yellow
$sampleClasses = $regularClasses | Select-Object -First 10
foreach ($class in $sampleClasses) {
    $classMethods = [regex]::Matches($content, "Class: $class\s*\n[^\n]*\n[^\n]*Methods \((\d+)\)") 
    if ($classMethods.Count -gt 0) {
        $methodCount = $classMethods[0].Groups[1].Value
        Write-Host "  $class : $methodCount methods" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "Analysis complete. Full output saved to system_full.txt" -ForegroundColor Green