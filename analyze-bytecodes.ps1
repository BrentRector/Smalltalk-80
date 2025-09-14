$json = Get-Content complete_system_final.json | ConvertFrom-Json
$allBytecodes = @()
foreach ($method in $json.methods) {
    if ($method.bytecode) {
        $allBytecodes += $method.bytecode
    }
}
$uniqueBytecodes = $allBytecodes | Sort-Object -Unique
Write-Host "Total unique bytecodes used in system: $($uniqueBytecodes.Count)"
Write-Host ""

# Group by ranges
$ranges = @{
    "0x00-0x0F" = @(0..15)    # Push receiver variable
    "0x10-0x1F" = @(16..31)   # Push temporary variable
    "0x20-0x3F" = @(32..63)   # Push literal constant
    "0x40-0x5F" = @(64..95)   # Push literal variable
    "0x60-0x6F" = @(96..111)  # Store and pop
    "0x70-0x7F" = @(112..127) # Push special/return
    "0x80-0x8F" = @(128..143) # Extended/unused
    "0x90-0xAF" = @(144..175) # Jump operations
    "0xB0-0xBF" = @(176..191) # Arithmetic
    "0xC0-0xCF" = @(192..207) # Special selectors
    "0xD0-0xDF" = @(208..223) # Send literal selector
    "0xE0-0xFF" = @(224..255) # Extended operations
}

foreach ($range in $ranges.GetEnumerator() | Sort-Object Name) {
    $used = $uniqueBytecodes | Where-Object { $_ -in $range.Value }
    $percentage = [Math]::Round(($used.Count / $range.Value.Count) * 100, 1)
    Write-Host ("{0}: {1}/{2} used ({3}%)" -f $range.Name, $used.Count, $range.Value.Count, $percentage)
    
    # Show which ones are NOT used
    $notUsed = $range.Value | Where-Object { $_ -notin $uniqueBytecodes }
    if ($notUsed.Count -gt 0 -and $notUsed.Count -le 8) {
        Write-Host "  Not used: $($notUsed -join ', ')"
    }
}

Write-Host ""
Write-Host "Summary:"
Write-Host "- Bytecodes 0-127: Core operations (push, store, return)"
Write-Host "- Bytecodes 128-143: Extended operations (mostly unimplemented)"
Write-Host "- Bytecodes 144-175: Jump operations"
Write-Host "- Bytecodes 176-191: Arithmetic operations"  
Write-Host "- Bytecodes 192-223: Send operations"
Write-Host "- Bytecodes 224-255: Extended send operations"