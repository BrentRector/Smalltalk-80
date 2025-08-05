# Run-ComprehensiveTests.ps1
# Comprehensive test runner for Smalltalk-80 Standard Library
# Ensures proper dependency loading: Standard Library -> Test Framework -> Test Cases

param(
    [string]$TestCategory = "All",
    [switch]$Verbose,
    [switch]$StopOnFirstFailure,
    [string]$OutputPath = $null
)

# Set UTF-8 encoding for proper character display
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

# Configuration
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$SmalltalkProject = "Smalltalk80/Smalltalk80.csproj"
$TestLibrary = "Testing/TestLibrary.st"
$LogFile = if ($OutputPath) { $OutputPath } else { "test-results-$(Get-Date -Format 'yyyyMMdd-HHmmss').log" }

# Test categories and their corresponding files
$TestCategories = @{
    "ObjectTests"              = "Testing/ObjectTests.st"
    "BooleanTests"             = "Testing/BooleanTests.st"
    "NumberTests"              = "Testing/NumberTests.st"
    "CharacterTests"           = "Testing/CharacterTests.st"
    "UndefinedObjectTests"     = "Testing/UndefinedObjectTests.st"
    "CollectionTests"          = "Testing/CollectionTests.st"
    "StringTests"              = "Testing/StringTests.st"
    "BlockContextTests"        = "Testing/BlockContextTests.st"
    "StandardLibraryTestSuite" = "Testing/StandardLibraryTestSuite.st"
}

# Colors for output
$Green = [System.ConsoleColor]::Green
$Red = [System.ConsoleColor]::Red
$Yellow = [System.ConsoleColor]::Yellow
$Blue = [System.ConsoleColor]::Cyan
$White = [System.ConsoleColor]::White

function Write-ColorOutput {
    param([string]$Message, [System.ConsoleColor]$Color = $White)
    Write-Host $Message -ForegroundColor $Color
}

function Write-TestHeader {
    param([string]$TestName)
    Write-ColorOutput ("`n" + "=" * 80) $Blue
    Write-ColorOutput "RUNNING TEST: $TestName" $Blue
    Write-ColorOutput ("=" * 80) $Blue
}

function Write-TestResult {
    param([string]$TestName, [bool]$Success, [string]$Output)
    
    if ($Success) {
        Write-ColorOutput "✅ PASSED: $TestName" $Green
    }
    else {
        Write-ColorOutput "❌ FAILED: $TestName" $Red
    }
    
    # Always show output to capture individual test counts
    if ($Output) {
        Write-ColorOutput "--- Test Details ---" $Yellow
        Write-Host $Output
        Write-ColorOutput "--- End Details ---" $Yellow
    }
}

function Invoke-SmalltalkTest {
    param([string]$TestFile, [string]$TestName)
    
    Write-TestHeader $TestName
    
    # Build command with proper dependency order and test execution
    # 1. Standard Library (automatically loaded)
    # 2. Test Framework (TestLibrary.st)
    # 3. Test Case file
    # 4. Execute the test
    $TestClassName = $TestName
    $Command = "dotnet"
    $Arguments = @(
        "run",
        "--project", $SmalltalkProject,
        "--",
        "--source", $TestLibrary,
        "--source", $TestFile,
        "-e", "$TestClassName new performTest"
    )
    
    if ($Verbose) {
        Write-ColorOutput "Executing: $Command $($Arguments -join ' ')" $Yellow
    }
    
    try {
        # Execute the test with UTF-8 encoding and filter debug output
        $env:DOTNET_SYSTEM_GLOBALIZATION_INVARIANT = $false
        $AllOutput = & $Command @Arguments 2>&1
        $ExitCode = $LASTEXITCODE
        
        # Filter out debug and system messages, keep only actual test results
        $FilteredOutput = $AllOutput | Where-Object {
            $_ -notmatch '\[DEBUG_PARSER\]' -and
            $_ -notmatch '\[SYSTEM_IMAGE\]' -and
            $_ -notmatch '\[BOOTSTRAP\]' -and
            $_ -notmatch 'Smalltalk-80 System Boot' -and
            $_ -notmatch 'Class \w+' -and
            $_ -notmatch '#\w+' -and
            $_ -notmatch 'Successfully loaded' -and
            $_ -notmatch 'Loaded \d+ classes' -and
            $_ -notmatch 'Starting Smalltalk-80 REPL' -and
            $_ -notmatch 'Smalltalk>' -and
            $_ -notmatch 'Commands: LoadFile' -and
            $_.ToString().Trim() -ne ''
        }
        
        $Output = ($FilteredOutput | Out-String).Trim()
        
        # Log the results
        $Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        $LogEntry = @"
[$Timestamp] $TestName
Command: $Command $($Arguments -join ' ')
Exit Code: $ExitCode
Output:
$Output

"@
        Add-Content -Path $LogFile -Value $LogEntry
        
        # Determine success based on test output content
        $Success = $ExitCode -eq 0 -and 
        $Output -notmatch "Error|Exception|Failed" -and
        $Output -match "tests run|ALL PASSED|PASSED"
        
        Write-TestResult $TestName $Success $Output
        return $Success
        
    }
    catch {
        $ErrorMsg = "Exception occurred: $($_.Exception.Message)"
        Write-ColorOutput $ErrorMsg $Red
        Add-Content -Path $LogFile -Value "[$Timestamp] $TestName - ERROR: $ErrorMsg"
        return $false
    }
}

function Test-Dependencies {
    Write-ColorOutput "Checking dependencies..." $Yellow
    
    # Check if dotnet is available
    try {
        $DotnetVersion = & dotnet --version 2>&1
        Write-ColorOutput "✅ .NET CLI available: $DotnetVersion" $Green
    }
    catch {
        Write-ColorOutput "❌ .NET CLI not found. Please install .NET CLI." $Red
        return $false
    }
    
    # Check if project file exists
    if (-not (Test-Path $SmalltalkProject)) {
        Write-ColorOutput "❌ Project file not found: $SmalltalkProject" $Red
        return $false
    }
    Write-ColorOutput "✅ Project file found: $SmalltalkProject" $Green
    
    # Check if test library exists
    if (-not (Test-Path $TestLibrary)) {
        Write-ColorOutput "❌ Test library not found: $TestLibrary" $Red
        return $false
    }
    Write-ColorOutput "✅ Test library found: $TestLibrary" $Green
    
    return $true
}

function Start-ComprehensiveTests {
    Write-ColorOutput "Smalltalk-80 Comprehensive Test Runner" $Blue
    Write-ColorOutput "Project Root: $ProjectRoot" $White
    Write-ColorOutput "Log File: $LogFile" $White
    Write-ColorOutput "Test Category: $TestCategory" $White
    
    # Initialize log file
    $InitialLog = @"
Smalltalk-80 Comprehensive Test Run
Started: $(Get-Date)
Project: $ProjectRoot
Test Category: $TestCategory
Arguments: $($MyInvocation.BoundParameters | ConvertTo-Json)

"@
    Set-Content -Path $LogFile -Value $InitialLog
    
    # Check dependencies
    if (-not (Test-Dependencies)) {
        Write-ColorOutput "❌ Dependency check failed. Exiting." $Red
        return $false
    }
    
    # Determine which tests to run
    $TestsToRun = @()
    if ($TestCategory -eq "All") {
        $TestsToRun = $TestCategories.GetEnumerator() | Sort-Object Key
        Write-ColorOutput "Running all $($TestsToRun.Count) test categories" $Yellow
    }
    elseif ($TestCategories.ContainsKey($TestCategory)) {
        $TestsToRun = @(@{Key = $TestCategory; Value = $TestCategories[$TestCategory] })
        Write-ColorOutput "Running single test category: $TestCategory" $Yellow
    }
    else {
        Write-ColorOutput "❌ Unknown test category: $TestCategory" $Red
        Write-ColorOutput "Available categories: $($TestCategories.Keys -join ', ')" $Yellow
        return $false
    }
    
    # Run tests
    $TotalTests = $TestsToRun.Count
    $PassedTests = 0
    $FailedTests = 0
    $Results = @()
    
    Write-ColorOutput "`nRunning $TotalTests test(s)..." $Blue
    
    foreach ($Test in $TestsToRun) {
        $TestName = $Test.Key
        $TestFile = $Test.Value
        
        Write-ColorOutput "Processing test: $TestName -> $TestFile" $Yellow
        
        if (-not (Test-Path $TestFile)) {
            Write-ColorOutput "❌ Test file not found: $TestFile" $Red
            $FailedTests++
            $Results += @{Name = $TestName; Success = $false; Reason = "File not found" }
            continue
        }
        
        $Success = Invoke-SmalltalkTest $TestFile $TestName
        
        if ($Success) {
            $PassedTests++
        }
        else {
            $FailedTests++
        }
        
        $Results += @{Name = $TestName; Success = $Success }
        
        if ($StopOnFirstFailure -and -not $Success) {
            Write-ColorOutput "❌ Stopping on first failure as requested." $Red
            break
        }
    }
    
    # Summary
    Write-ColorOutput ("`n" + "=" * 80) $Blue
    Write-ColorOutput "TEST SUMMARY" $Blue
    Write-ColorOutput ("=" * 80) $Blue
    Write-ColorOutput "Total Tests: $TotalTests" $White
    Write-ColorOutput "Passed: $PassedTests" $Green
    Write-ColorOutput "Failed: $FailedTests" $Red
    Write-ColorOutput "Success Rate: $([math]::Round(($PassedTests / $TotalTests) * 100, 2))%" $White
    Write-ColorOutput "Log File: $LogFile" $Yellow
    
    # Detailed results
    if ($Results.Count -gt 0) {
        Write-ColorOutput "`nDetailed Results:" $Yellow
        foreach ($Result in $Results) {
            $Status = if ($Result.Success) { "✅ PASS" } else { "❌ FAIL" }
            $Color = if ($Result.Success) { $Green } else { $Red }
            Write-ColorOutput "  $Status $($Result.Name)" $Color
        }
    }
    
    # Final status
    $OverallSuccess = $FailedTests -eq 0
    if ($OverallSuccess) {
        Write-ColorOutput "`n🎉 All tests passed!" $Green
    }
    else {
        Write-ColorOutput "`n❌ Some tests failed. Check the log file for details." $Red
    }
    
    return $OverallSuccess
}

# Change to project root directory
Push-Location $ProjectRoot

try {
    # Run the comprehensive tests
    $Success = Start-ComprehensiveTests
    exit $(if ($Success) { 0 } else { 1 })
}
finally {
    Pop-Location
}
