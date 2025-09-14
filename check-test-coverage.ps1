# Bytecodes that are just marked as tested without actual tests
$markedNotTested = @{
    "ExtendedReturn" = 126..127
    "UnusedStackOp" = 138..143  
    "JumpOperation" = 144..175
    "SpecialSelector" = 192..207
    "SendLiteralSelector" = 208..223
    "ExtendedOperation" = 224..255
    "ExtendedSuperSend" = @(132)
}

# Check which bytecodes are used in the system
$json = Get-Content complete_system_final.json | ConvertFrom-Json
$allBytecodes = @()
foreach ($method in $json.methods) {
    if ($method.bytecode) {
        $allBytecodes += $method.bytecode
    }
}
$usedBytecodes = $allBytecodes | Sort-Object -Unique

Write-Host "=== Bytecode Test Coverage Analysis ==="
Write-Host ""

$totalMarked = 0
$totalUsedButNotTested = 0

foreach ($category in $markedNotTested.GetEnumerator() | Sort-Object Name) {
    $range = $category.Value
    $usedInRange = $usedBytecodes | Where-Object { $_ -in $range }
    
    if ($usedInRange.Count -gt 0) {
        Write-Host "$($category.Name): $($usedInRange.Count)/$($range.Count) bytecodes are USED in system but NOT TESTED"
        $totalMarked += $range.Count
        $totalUsedButNotTested += $usedInRange.Count
        
        if ($usedInRange.Count -le 10) {
            $hexCodes = $usedInRange | ForEach-Object { "0x{0:X2}" -f $_ }
            Write-Host "  Used bytecodes: $($hexCodes -join ', ')"
        }
    }
}

Write-Host ""
Write-Host "Summary:"
Write-Host "- Total bytecodes marked but not tested: $totalMarked"
Write-Host "- Of those, actually used in system: $totalUsedButNotTested"
Write-Host ""

# Bytecodes we do test
$actuallyTested = @(
    0..125     # Push, store, return operations
    128..134   # Extended operations (though they throw NotImplementedException)
    135..137   # Stack operations
    176..191   # Arithmetic operations
)

$testedAndUsed = $usedBytecodes | Where-Object { $_ -in $actuallyTested }
Write-Host "Bytecodes with actual tests: $($actuallyTested.Count)"
Write-Host "Of those, used in system: $($testedAndUsed.Count)"
Write-Host ""

# Show critical gaps
$criticalGaps = $usedBytecodes | Where-Object { 
    $_ -in 144..175 -or  # Jump operations
    $_ -in 192..223 -or  # Send operations
    $_ -in 224..255      # Extended send operations
}
Write-Host "CRITICAL: $($criticalGaps.Count) bytecodes are used for jumps/sends but have no real tests!"