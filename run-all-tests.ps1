# Run ALL Smalltalk-80 test suites
param(
    [int]$TimeoutSeconds = 30,
    [switch]$StopOnError
)

Write-Host "Running ALL Smalltalk-80 Test Suites" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# First ensure everything is built
Write-Host "`nBuilding projects..." -ForegroundColor Yellow
dotnet build --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Define all test executables
$testExecutables = @(
    @{Name="Comprehensive Bytecode Tests"; Exe="Smalltalk80.Tests.exe"},
    @{Name="Compiler Tests"; Exe="Smalltalk80.CompilerTest.exe"},
    @{Name="Association Tests"; Exe="TestAssociation.exe"},
    @{Name="Bytecode Generation Tests"; Exe="TestBytecodeGen.exe"},
    @{Name="Direct Compilation Tests"; Exe="TestDirectCompile.exe"},
    @{Name="Extracted Compilation Tests"; Exe="TestExtractedCompile.exe"},
    @{Name="Extracted Bytecode Tests"; Exe="TestExtractedBytecode.exe"},
    @{Name="Get Text Tests"; Exe="TestGetText.exe"},
    @{Name="Jump Bytecode Tests"; Exe="TestJumpBytecodes.exe"},
    @{Name="Compile Debug Tests"; Exe="TestCompileDebug.exe"}
)

$totalTests = 0
$passedTests = 0
$failedTests = 0
$skippedTests = 0
$testResults = @()

foreach ($test in $testExecutables) {
    $testPath = Join-Path "bin\Debug\net9.0" $test.Exe
    
    if (Test-Path $testPath) {
        Write-Host "`n----------------------------------------" -ForegroundColor DarkGray
        Write-Host "Running: $($test.Name)" -ForegroundColor Yellow
        Write-Host "Executable: $($test.Exe)" -ForegroundColor DarkGray
        
        $totalTests++
        $startTime = Get-Date
        
        # Start the test process
        $testProcess = Start-Process -FilePath $testPath `
            -WorkingDirectory "bin\Debug\net9.0" `
            -NoNewWindow -PassThru `
            -RedirectStandardOutput "temp_output.txt" `
            -RedirectStandardError "temp_error.txt"
        
        # Wait for completion with timeout
        $completed = $testProcess.WaitForExit($TimeoutSeconds * 1000)
        $duration = ((Get-Date) - $startTime).TotalSeconds
        
        if ($completed) {
            if ($testProcess.ExitCode -eq 0) {
                Write-Host "✓ PASSED" -ForegroundColor Green -NoNewline
                Write-Host " (${duration}s)" -ForegroundColor DarkGray
                $passedTests++
                $testResults += @{Name=$test.Name; Status="PASSED"; Duration=$duration}
                
                # Show output if it's short
                if (Test-Path "bin\Debug\net9.0\temp_output.txt") {
                    $output = Get-Content "bin\Debug\net9.0\temp_output.txt" -Raw
                    $lines = ($output -split "`n").Count
                    if ($lines -le 10) {
                        Write-Host $output -ForegroundColor DarkGray
                    } else {
                        # Just show the last few lines with test results
                        $lastLines = ($output -split "`n") | Select-Object -Last 5
                        Write-Host ($lastLines -join "`n") -ForegroundColor DarkGray
                    }
                }
            } else {
                Write-Host "✗ FAILED" -ForegroundColor Red -NoNewline
                Write-Host " (exit code: $($testProcess.ExitCode), ${duration}s)" -ForegroundColor DarkGray
                $failedTests++
                $testResults += @{Name=$test.Name; Status="FAILED"; Duration=$duration; ExitCode=$testProcess.ExitCode}
                
                # Show error output
                if (Test-Path "bin\Debug\net9.0\temp_error.txt") {
                    $error = Get-Content "bin\Debug\net9.0\temp_error.txt" -Raw
                    if ($error) {
                        Write-Host "Error output:" -ForegroundColor Red
                        Write-Host $error -ForegroundColor DarkRed
                    }
                }
                
                if ($StopOnError) {
                    Write-Host "`nStopping on first error (use -StopOnError:$false to continue)" -ForegroundColor Yellow
                    break
                }
            }
        } else {
            Write-Host "⚠ TIMEOUT" -ForegroundColor Yellow -NoNewline
            Write-Host " (after ${TimeoutSeconds}s)" -ForegroundColor DarkGray
            $testProcess.Kill()
            $failedTests++
            $testResults += @{Name=$test.Name; Status="TIMEOUT"; Duration=$TimeoutSeconds}
            
            if ($StopOnError) {
                Write-Host "`nStopping on timeout (use -StopOnError:$false to continue)" -ForegroundColor Yellow
                break
            }
        }
        
        # Clean up temp files
        Remove-Item "bin\Debug\net9.0\temp_output.txt" -ErrorAction SilentlyContinue
        Remove-Item "bin\Debug\net9.0\temp_error.txt" -ErrorAction SilentlyContinue
    } else {
        Write-Host "`n⚠ SKIPPED: $($test.Name) - executable not found" -ForegroundColor Yellow
        $skippedTests++
        $testResults += @{Name=$test.Name; Status="SKIPPED"}
    }
}

# Summary
Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "Test Suite Summary" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

Write-Host "`nResults:" -ForegroundColor White
Write-Host "  Total:   $totalTests test suites" -ForegroundColor White
Write-Host "  Passed:  $passedTests" -ForegroundColor Green
Write-Host "  Failed:  $failedTests" -ForegroundColor Red
Write-Host "  Skipped: $skippedTests" -ForegroundColor Yellow

if ($testResults.Count -gt 0) {
    Write-Host "`nDetailed Results:" -ForegroundColor White
    foreach ($result in $testResults) {
        $color = switch ($result.Status) {
            "PASSED" { "Green" }
            "FAILED" { "Red" }
            "TIMEOUT" { "Yellow" }
            "SKIPPED" { "DarkGray" }
        }
        
        $line = "  {0,-40} {1,-10}" -f $result.Name, $result.Status
        if ($result.Duration) {
            $line += " ({0:F1}s)" -f $result.Duration
        }
        if ($result.ExitCode) {
            $line += " [Exit: $($result.ExitCode)]"
        }
        Write-Host $line -ForegroundColor $color
    }
}

# Overall status
Write-Host "`n" -NoNewline
if ($failedTests -eq 0 -and $passedTests -gt 0) {
    Write-Host "✓ ALL TESTS PASSED!" -ForegroundColor Green -BackgroundColor DarkGreen
    exit 0
} elseif ($passedTests -eq 0) {
    Write-Host "✗ NO TESTS PASSED" -ForegroundColor Red -BackgroundColor DarkRed
    exit 1
} else {
    Write-Host "⚠ SOME TESTS FAILED" -ForegroundColor Yellow -BackgroundColor DarkYellow
    exit 1
}