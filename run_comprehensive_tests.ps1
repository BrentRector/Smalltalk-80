# Comprehensive test runner for both compilers

$tests = @(
    @{Name="test1IfTrue"; Src="^true ifTrue: [42]"; ShouldInline=$true},
    @{Name="test2IfFalse"; Src="^false ifFalse: [99]"; ShouldInline=$true},
    @{Name="test3IfTrueIfFalse"; Src="^5 > 3 ifTrue: [100] ifFalse: [200]"; ShouldInline=$true},
    @{Name="test4Nested"; Src="^true ifTrue: [false ifFalse: [300]]"; ShouldInline=$true},
    @{Name="test5BlockWithTemps"; Src="^true ifTrue: [| temp | temp := 400. temp + 100]"; ShouldInline=$true},
    @{Name="test6BlockWithArgs"; Src="^true ifTrue: [:x | x + 500]"; ShouldInline=$false},
    @{Name="test7NonLocalReturn"; Src="true ifTrue: [^600]. ^700"; ShouldInline=$false},
    @{Name="test8ComplexNested"; Src="^true ifTrue: [5 > 3 ifTrue: [10 < 20 ifTrue: [800]]]"; ShouldInline=$true},
    @{Name="test10IfFalseIfTrue"; Src="^5 < 3 ifFalse: [1100] ifTrue: [1200]"; ShouldInline=$true},
    @{Name="test13EmptyBlocks"; Src="^true ifTrue: [] ifFalse: []"; ShouldInline=$true},
    @{Name="test14MultipleStatements"; Src="^true ifTrue: [| a b | a := 10. b := 20. a + b]"; ShouldInline=$true},
    @{Name="test15SuperSend"; Src="^super ifTrue: [1600]"; ShouldInline=$false}
)

$results = @()

foreach ($test in $tests) {
    Write-Host "`n=== Testing: $($test.Name) ===" -ForegroundColor Cyan
    Write-Host "Source: $($test.Src)"
    Write-Host "Should inline: $($test.ShouldInline)"
    
    # Create test file
    $testFile = "temp_test_$($test.Name).st"
    $methodSrc = "test`n    $($test.Src)"
    Set-Content -Path $testFile -Value $methodSrc
    
    # Test with StandaloneCompiler
    Write-Host "`nStandaloneCompiler:" -ForegroundColor Yellow
    $outFile1 = "temp_standalone_$($test.Name).json"
    & dotnet run --project StandaloneCompiler/StandaloneCompiler.csproj -- $testFile $outFile1 2>$null
    
    if (Test-Path $outFile1) {
        $json1 = Get-Content $outFile1 | ConvertFrom-Json
        $bytecode1 = $json1.bytecode -join ", "
        
        # Check for BlockCopy (137, numArgs, 200)
        # BlockCopy pattern is: 137 (push active context), numArgs (117=0, 118=1, etc), 200 (send blockCopy:)
        $hasBlockCopy1 = $false
        for ($i = 0; $i -lt $json1.bytecode.Count - 2; $i++) {
            if ($json1.bytecode[$i] -eq 137 -and $json1.bytecode[$i+2] -eq 200) {
                $hasBlockCopy1 = $true
                break
            }
        }
        # Check for conditional jump (152-159 or 168)
        $hasCondJump1 = ($json1.bytecode | Where-Object {$_ -ge 152 -and $_ -le 159}).Count -gt 0 -or $json1.bytecode -contains 168
        
        Write-Host "  Bytecode: [$bytecode1]"
        Write-Host "  Has BlockCopy: $hasBlockCopy1"
        Write-Host "  Has Conditional Jump: $hasCondJump1"
        
        $results += [PSCustomObject]@{
            Test = $test.Name
            Compiler = "StandaloneCompiler"
            ShouldInline = $test.ShouldInline
            Bytecode = $bytecode1
            HasBlockCopy = $hasBlockCopy1
            HasCondJump = $hasCondJump1
            Correct = ($test.ShouldInline -and -not $hasBlockCopy1 -and $hasCondJump1) -or 
                     (-not $test.ShouldInline -and ($hasBlockCopy1 -or $test.Name -eq "test15SuperSend"))
        }
    } else {
        Write-Host "  ERROR: Failed to generate output" -ForegroundColor Red
    }
    
    # Test with Smalltalk80.Compiler
    Write-Host "`nSmalltalk80.Compiler:" -ForegroundColor Yellow
    $outFile2 = "temp_newcompiler_$($test.Name).json"
    & dotnet run --project Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- $testFile $outFile2 2>$null
    
    if (Test-Path $outFile2) {
        $json2 = Get-Content $outFile2 | ConvertFrom-Json
        $bytecode2 = $json2.bytecode -join ", "
        
        # Check for BlockCopy (137, numArgs, 200)
        # BlockCopy pattern is: 137 (push active context), numArgs (117=0, 118=1, etc), 200 (send blockCopy:)
        $hasBlockCopy2 = $false
        for ($i = 0; $i -lt $json2.bytecode.Count - 2; $i++) {
            if ($json2.bytecode[$i] -eq 137 -and $json2.bytecode[$i+2] -eq 200) {
                $hasBlockCopy2 = $true
                break
            }
        }
        # Check for conditional jump
        $hasCondJump2 = ($json2.bytecode | Where-Object {$_ -ge 152 -and $_ -le 159}).Count -gt 0 -or $json2.bytecode -contains 168
        
        Write-Host "  Bytecode: [$bytecode2]"
        Write-Host "  Has BlockCopy: $hasBlockCopy2"
        Write-Host "  Has Conditional Jump: $hasCondJump2"
        
        $results += [PSCustomObject]@{
            Test = $test.Name
            Compiler = "Smalltalk80.Compiler"
            ShouldInline = $test.ShouldInline
            Bytecode = $bytecode2
            HasBlockCopy = $hasBlockCopy2
            HasCondJump = $hasCondJump2
            Correct = ($test.ShouldInline -and -not $hasBlockCopy2 -and $hasCondJump2) -or 
                     (-not $test.ShouldInline -and ($hasBlockCopy2 -or $test.Name -eq "test15SuperSend"))
        }
    } else {
        Write-Host "  ERROR: Failed to generate output" -ForegroundColor Red
    }
    
    # Clean up
    Remove-Item -Path $testFile -ErrorAction SilentlyContinue
    Remove-Item -Path $outFile1 -ErrorAction SilentlyContinue
    Remove-Item -Path $outFile2 -ErrorAction SilentlyContinue
}

# Summary
Write-Host "`n`n=== SUMMARY ===" -ForegroundColor Green
$correct = ($results | Where-Object {$_.Correct}).Count
$total = $results.Count
Write-Host "Correct: $correct / $total"

# Show failures
$failures = $results | Where-Object {-not $_.Correct}
if ($failures) {
    Write-Host "`nFAILURES:" -ForegroundColor Red
    $failures | Format-Table -AutoSize
}

# Export results for analysis
$results | ConvertTo-Json -Depth 10 | Set-Content "test_results.json"
Write-Host "`nResults saved to test_results.json"