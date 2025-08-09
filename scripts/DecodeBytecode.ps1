#!/usr/bin/env pwsh
# Decode what specific bytecode sequences mean

Write-Host "Bytecode Decoder" -ForegroundColor Cyan
Write-Host "=================" -ForegroundColor Cyan
Write-Host ""

# Common problem sequences
$sequences = @(
    @{Bytes = @(64, 124); Name = "Problem sequence 1"},
    @{Bytes = @(112, 124); Name = "Problem sequence 2"},
    @{Bytes = @(32, 136, 104, 105, 65, 124); Name = "Problem sequence 3"},
    @{Bytes = @(137, 0, 5, 66, 67, 198, 209, 125); Name = "Correct changeRequestFrom:"}
)

foreach ($seq in $sequences) {
    Write-Host "$($seq.Name): [$($seq.Bytes -join ', ')]" -ForegroundColor Yellow
    
    foreach ($byte in $seq.Bytes) {
        $instruction = ""
        
        # Decode based on Smalltalk-80 bytecode specification
        switch ($byte) {
            # Push receiver and arguments (0-15)
            { $_ -ge 0 -and $_ -le 15 } { $instruction = "Push receiver variable #$_" }
            
            # Push temporaries (16-31)
            { $_ -ge 16 -and $_ -le 31 } { $instruction = "Push temporary variable #$($_ - 16)" }
            
            # Push literal constants (32-63)
            { $_ -ge 32 -and $_ -le 63 } { $instruction = "Push literal constant #$($_ - 32)" }
            
            # Push literal variables (64-95)
            { $_ -ge 64 -and $_ -le 95 } { $instruction = "Push literal variable #$($_ - 64)" }
            
            # Pop and store (96-127)
            { $_ -ge 96 -and $_ -le 103 } { $instruction = "Pop and store receiver variable #$($_ - 96)" }
            { $_ -ge 104 -and $_ -le 111 } { $instruction = "Pop and store temporary variable #$($_ - 104)" }
            
            # Push special constants
            112 { $instruction = "Push receiver (self)" }
            113 { $instruction = "Push true" }
            114 { $instruction = "Push false" }
            115 { $instruction = "Push nil" }
            116 { $instruction = "Push -1" }
            117 { $instruction = "Push 0" }
            118 { $instruction = "Push 1" }
            119 { $instruction = "Push 2" }
            
            # Return
            120 { $instruction = "Return receiver (self)" }
            121 { $instruction = "Return true" }
            122 { $instruction = "Return false" }
            123 { $instruction = "Return nil" }
            124 { $instruction = "Return top of stack" }
            125 { $instruction = "Return top of stack from block" }
            
            # Extended operations
            { $_ -ge 128 -and $_ -le 137 } { 
                $type = $_ - 128
                $instruction = "Extended operation type $type (needs next byte)"
            }
            
            # Sends
            { $_ -ge 176 -and $_ -le 207 } { 
                $selector = ($_ - 176) % 16
                $argCount = [Math]::Floor(($_ - 176) / 16)
                $instruction = "Send selector #$selector with $argCount args"
            }
            
            { $_ -ge 208 -and $_ -le 223 } {
                $selector = $_ - 208
                $instruction = "Send special selector #$selector"
            }
            
            { $_ -ge 224 -and $_ -le 255 } {
                $selector = $_ - 224
                $instruction = "Send literal selector #$selector"
            }
            
            default { $instruction = "Unknown/Extended" }
        }
        
        Write-Host "  $byte : $instruction" -ForegroundColor Gray
    }
    Write-Host ""
}

Write-Host "Common problem patterns:" -ForegroundColor Cyan
Write-Host "  [64, 124] = Push literal variable #0, then return top" -ForegroundColor Red
Write-Host "           This is likely a placeholder/error bytecode!" -ForegroundColor Red
Write-Host ""
Write-Host "  [112, 124] = Push self, then return top" -ForegroundColor Yellow
Write-Host "           Simple 'return self' method" -ForegroundColor Yellow