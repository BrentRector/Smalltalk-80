# Final validation test for both compilers
Write-Host "Testing key conditional inlining patterns..." -ForegroundColor Cyan

$tests = @(
    @{Name="Simple ifTrue"; Source="test`n    ^true ifTrue: [42]"},
    @{Name="Simple ifFalse"; Source="test`n    ^false ifFalse: [99]"},
    @{Name="ifTrue:ifFalse:"; Source="test`n    ^5 > 3 ifTrue: [100] ifFalse: [200]"},
    @{Name="Nested"; Source="test`n    ^true ifTrue: [false ifFalse: [300]]"},
    @{Name="Block with args (no inline)"; Source="test`n    ^true ifTrue: [:x | x + 500]"},
    @{Name="Non-local return (no inline)"; Source="test`n    true ifTrue: [^600]. ^700"}
)

$results = @()

foreach ($test in $tests) {
    Write-Host "`n=== $($test.Name) ===" -ForegroundColor Yellow
    
    # Save test file
    Set-Content -Path "test_temp.st" -Value $test.Source
    
    # Test StandaloneCompiler
    Write-Host "StandaloneCompiler: " -NoNewline
    & ./bin/Debug/net9.0/StandaloneCompiler.exe test_temp.st test_temp.json 2>$null
    if (Test-Path test_temp.json) {
        $json = Get-Content test_temp.json | ConvertFrom-Json
        $bytecode = $json.bytecode -join ", "
        Write-Host "[$bytecode]" -ForegroundColor Green
        $results += @{Compiler="Standalone"; Test=$test.Name; Bytecode=$bytecode}
    } else {
        Write-Host "FAILED" -ForegroundColor Red
    }
    
    # Test Smalltalk80.Compiler
    Write-Host "Smalltalk80.Compiler: " -NoNewline
    & ./bin/Debug/net9.0/Smalltalk80.Compiler.exe test_temp.st test_temp.json 2>$null
    if (Test-Path test_temp.json) {
        $json = Get-Content test_temp.json | ConvertFrom-Json
        $bytecode = $json.bytecode -join ", "
        Write-Host "[$bytecode]" -ForegroundColor Green
        $results += @{Compiler="New"; Test=$test.Name; Bytecode=$bytecode}
    } else {
        Write-Host "FAILED" -ForegroundColor Red
    }
    
    # Cleanup
    Remove-Item -Path test_temp.st -ErrorAction SilentlyContinue
    Remove-Item -Path test_temp.json -ErrorAction SilentlyContinue
}

Write-Host "`n=== Summary ===" -ForegroundColor Cyan
$results | Format-Table -AutoSize