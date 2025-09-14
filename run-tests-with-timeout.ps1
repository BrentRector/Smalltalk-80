# Run the Smalltalk-80 test suite with timeout
param(
    [int]$TimeoutSeconds = 60
)

Write-Host "Running Smalltalk-80 Test Suite with $TimeoutSeconds second timeout" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan

# Run the bytecode tests directly with timeout
$job = Start-Job -ScriptBlock {
    Set-Location $using:PWD
    dotnet run --project Smalltalk80.Tests --no-build
}

Write-Host "Starting test execution..." -ForegroundColor Yellow

# Wait for completion or timeout
$completed = Wait-Job $job -Timeout $TimeoutSeconds

if ($completed) {
    $result = Receive-Job $job
    Write-Host $result
    $exitCode = $job.ChildJobs[0].ExitCode
    
    if ($exitCode -eq 0) {
        Write-Host "`nTests completed successfully!" -ForegroundColor Green
    } else {
        Write-Host "`nTests failed with exit code: $exitCode" -ForegroundColor Red
    }
} else {
    Write-Host "`nTest execution timed out after $TimeoutSeconds seconds!" -ForegroundColor Red
    Write-Host "Stopping test job..." -ForegroundColor Yellow
    Stop-Job $job -Force
    Remove-Job $job -Force
    
    Write-Host "`nPossible infinite loop detected in tests." -ForegroundColor Red
    Write-Host "Try running with tracing to identify the problematic test:" -ForegroundColor Yellow
    Write-Host "  dotnet run --project Smalltalk80.Tests" -ForegroundColor White
    
    exit 1
}

Remove-Job $job -Force -ErrorAction SilentlyContinue