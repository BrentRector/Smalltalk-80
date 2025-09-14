# Run the Smalltalk-80 test suite - quick tests only
param(
    [int]$TimeoutSeconds = 30,
    [switch]$All
)

if ($All) {
    Write-Host "Use run-all-tests.ps1 for comprehensive testing" -ForegroundColor Yellow
    & "$PSScriptRoot\run-all-tests.ps1" -TimeoutSeconds $TimeoutSeconds
    exit $LASTEXITCODE
}

Write-Host "Running Quick Smalltalk-80 Test Suite" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "(Use -All flag to run all tests)" -ForegroundColor DarkGray

# First ensure everything is built
Write-Host "`nBuilding projects..." -ForegroundColor Yellow
dotnet build --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Define quick-running test executables (ones that pass quickly)
$quickTests = @(
    @{Name="Comprehensive Bytecode Tests"; Exe="Smalltalk80.Tests.exe"},
    @{Name="Compiler Tests"; Exe="Smalltalk80.CompilerTest.exe"},
    @{Name="Extracted Bytecode Tests"; Exe="TestExtractedBytecode.exe"},
    @{Name="Get Text Tests"; Exe="TestGetText.exe"},
    @{Name="Jump Bytecode Tests"; Exe="TestJumpBytecodes.exe"},
    @{Name="Compile Debug Tests"; Exe="TestCompileDebug.exe"}
)

$totalTests = 0
$passedTests = 0
$failedTests = 0

Write-Host "`nRunning quick tests..." -ForegroundColor Yellow

foreach ($test in $quickTests) {
    $testPath = Join-Path "bin\Debug\net9.0" $test.Exe
    
    if (Test-Path $testPath) {
        Write-Host -NoNewline "  $($test.Name)... "
        $totalTests++
        
        # Run the test
        $testProcess = Start-Process -FilePath $testPath `
            -WorkingDirectory "bin\Debug\net9.0" `
            -NoNewWindow -PassThru `
            -RedirectStandardOutput "test_out.tmp" `
            -RedirectStandardError "test_err.tmp"
        
        # Wait for completion with timeout
        $completed = $testProcess.WaitForExit($TimeoutSeconds * 1000)
        
        if ($completed) {
            if ($testProcess.ExitCode -eq 0) {
                Write-Host "✓" -ForegroundColor Green
                $passedTests++
            } else {
                Write-Host "✗ (exit: $($testProcess.ExitCode))" -ForegroundColor Red
                $failedTests++
            }
        } else {
            Write-Host "⚠ timeout" -ForegroundColor Yellow
            $testProcess.Kill()
            $failedTests++
        }
        
        # Clean up temp files
        Remove-Item "bin\Debug\net9.0\test_out.tmp" -ErrorAction SilentlyContinue
        Remove-Item "bin\Debug\net9.0\test_err.tmp" -ErrorAction SilentlyContinue
    }
}

# Summary
Write-Host "`n----------------------------------------" -ForegroundColor DarkGray
Write-Host "Quick Test Summary: $passedTests/$totalTests passed" -ForegroundColor $(if ($failedTests -eq 0) { "Green" } else { "Yellow" })

if ($failedTests -eq 0) {
    Write-Host "✓ All quick tests passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "⚠ Some tests failed. Run with -All for detailed output." -ForegroundColor Yellow
    exit 1
}