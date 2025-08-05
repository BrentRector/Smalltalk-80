# Update-TestTracking.ps1
# Script to update remaining test files with individual test tracking

$TestFiles = @(
    "Testing/CharacterTests.st",
    "Testing/StringTests.st", 
    "Testing/BlockContextTests.st",
    "Testing/UndefinedObjectTests.st"
)

foreach ($File in $TestFiles) {
    Write-Host "Updating $File..."
    
    # Read the file content
    $Content = Get-Content $File -Raw
    
    # Find performTest method and update it
    # This is a simple pattern replacement - may need manual adjustment
    $UpdatedContent = $Content -replace 
    '(performTest\s*\n\s*"[^"]*"\s*\n\s*)(self test[^!]+!)(!)', 
    '$1self resetTestCounts.$2self printTestResults$3'
    
    # Replace individual test calls with runIndividualTest calls
    $UpdatedContent = $UpdatedContent -replace 'self (test\w+)\.', 'self runIndividualTest: #$1.'
    
    # Write back to file
    Set-Content -Path $File -Value $UpdatedContent
}

Write-Host "Test tracking updates completed!"
