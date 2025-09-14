$json = Get-Content complete_system_final.json | ConvertFrom-Json

Write-Host "=== Checking what bytecodes the compiler actually generates ==="
Write-Host ""

# Find methods that likely have conditionals (if/then)
$conditionalMethods = $json.methods | Where-Object { 
    $_.bytecode -contains 152 -or 
    $_.bytecode -contains 153 -or
    $_.bytecode -contains 154 -or
    $_.bytecode -contains 144 -or
    $_.bytecode -contains 145
} | Select-Object -First 5

Write-Host "Methods using bytecodes 144-154 (should be jumps per Blue Book):"
foreach ($method in $conditionalMethods) {
    Write-Host "  Class: $($method.class), Method: $($method.selector)"
    
    # Look for jump patterns
    $bytecode = $method.bytecode
    for ($i = 0; $i -lt $bytecode.Count; $i++) {
        $bc = $bytecode[$i]
        if ($bc -ge 144 -and $bc -le 175) {
            $context = ""
            if ($i -gt 0) { $context += "$($bytecode[$i-1]) " }
            $context += "[$bc]"
            if ($i -lt $bytecode.Count - 1) { $context += " $($bytecode[$i+1])" }
            Write-Host "    Position $i`: $context"
        }
    }
}

Write-Host ""
Write-Host "Checking bytecodes 224-255 (should be send with 1-2 args per Blue Book):"
$sendMethods = $json.methods | Where-Object { 
    $_.bytecode | Where-Object { $_ -ge 224 -and $_ -le 255 }
} | Select-Object -First 5

foreach ($method in $sendMethods) {
    Write-Host "  Class: $($method.class), Method: $($method.selector)"
    $bytecode = $method.bytecode
    for ($i = 0; $i -lt $bytecode.Count; $i++) {
        $bc = $bytecode[$i]
        if ($bc -ge 224 -and $bc -le 255) {
            Write-Host "    Bytecode $bc at position $i"
        }
    }
}

Write-Host ""
Write-Host "=== Analyzing bytecode 152 specifically (should be 'Jump forward #1') ==="
$method152 = $json.methods | Where-Object { $_.bytecode -contains 152 } | Select-Object -First 1
if ($method152) {
    Write-Host "Example: $($method152.class)>>$($method152.selector)"
    $bc = $method152.bytecode
    $index = $bc.IndexOf(152)
    
    Write-Host "Context around bytecode 152:"
    $start = [Math]::Max(0, $index - 3)
    $end = [Math]::Min($bc.Count - 1, $index + 3)
    
    for ($i = $start; $i -le $end; $i++) {
        if ($i -eq $index) {
            Write-Host "  -> [$($bc[$i])] <-- This should be a jump!"
        } else {
            Write-Host "     $($bc[$i])"
        }
    }
    
    Write-Host ""
    Write-Host "If 152 is a jump forward by 1 byte, the next byte would be skipped."
    Write-Host "If 152 is a send with 1 arg, it would pop an arg and receiver from stack."
}

Write-Host ""
Write-Host "=== CONCLUSION ==="
Write-Host "The compiler is generating Blue Book bytecodes (jumps at 144-175)"
Write-Host "But the interpreter is expecting different bytecodes (sends at 144-175)"
Write-Host "This is why control flow doesn't work!" -ForegroundColor Red