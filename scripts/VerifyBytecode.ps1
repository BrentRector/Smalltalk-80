# Verify specific methods have correct bytecode
param(
    [string]$Phase3Dir = "Bootstrap\phase3_final"
)

Write-Host "`n=== Bytecode Verification ===" -ForegroundColor Cyan

# Check key methods
$testCases = @(
    @{Class="UndefinedObject"; Method="isNil"; ExpectedBytecode=@(32, 124); Description="Should push true literal and return"},
    @{Class="UndefinedObject"; Method="notNil"; ExpectedBytecode=@(32, 124); Description="Should push false literal and return"},
    @{Class="True"; Method="not"; ExpectedBytecode=@(32, 124); Description="Should push false literal and return"},
    @{Class="False"; Method="not"; ExpectedBytecode=@(32, 124); Description="Should push true literal and return"},
    @{Class="True"; Method="&"; ExpectedBytecode=@(16, 124); Description="Should push first argument and return"},
    @{Class="False"; Method="&"; ExpectedBytecode=@(112, 124); Description="Should push self and return"},
    @{Class="Object"; Method="yourself"; ExpectedBytecode=@(120); Description="Should return self"},
    @{Class="Object"; Method="class"; ExpectedBytecode=@(64, 120); Description="Should have primitive 111"}
)

foreach ($test in $testCases) {
    $jsonFile = Join-Path $Phase3Dir "$($test.Class).json"
    if (Test-Path $jsonFile) {
        $json = Get-Content $jsonFile | ConvertFrom-Json
        
        # Find the method
        $method = $json.methods | Where-Object { $_.selector -eq $test.Method }
        
        if ($method) {
            $bytecodeMatch = $true
            if ($method.bytecode.Count -eq $test.ExpectedBytecode.Count) {
                for ($i = 0; $i -lt $method.bytecode.Count; $i++) {
                    if ($method.bytecode[$i] -ne $test.ExpectedBytecode[$i]) {
                        $bytecodeMatch = $false
                        break
                    }
                }
            } else {
                $bytecodeMatch = $false
            }
            
            if ($bytecodeMatch) {
                Write-Host "[OK] " -ForegroundColor Green -NoNewline
                Write-Host "$($test.Class)>>$($test.Method): $($method.bytecode -join ', ')"
                Write-Host "     $($test.Description)" -ForegroundColor DarkGray
            } else {
                Write-Host "[FAIL] " -ForegroundColor Red -NoNewline
                Write-Host "$($test.Class)>>$($test.Method)"
                Write-Host "     Expected: $($test.ExpectedBytecode -join ', ')" -ForegroundColor Yellow
                Write-Host "     Got: $($method.bytecode -join ', ')" -ForegroundColor Yellow
            }
            
            # Check literals if present
            if ($method.literals -and $method.literals.Count -gt 0) {
                Write-Host "     Literals: $($method.literals -join ', ')" -ForegroundColor DarkCyan
            }
            
            # Check primitive if present
            if ($method.primitive -and $method.primitive -ne 0) {
                Write-Host "     Primitive: $($method.primitive)" -ForegroundColor DarkMagenta
            }
        } else {
            Write-Host "[NOT FOUND] " -ForegroundColor Yellow -NoNewline
            Write-Host "$($test.Class)>>$($test.Method)"
        }
    } else {
        Write-Host "[NO FILE] $($test.Class).json not found" -ForegroundColor Red
    }
    Write-Host ""
}

# Summary of loaded methods
Write-Host "`n=== Method Count Summary ===" -ForegroundColor Cyan
$totalMethods = 0
Get-ChildItem $Phase3Dir -Filter "*.json" | Sort-Object Name | ForEach-Object {
    $json = Get-Content $_.FullName | ConvertFrom-Json
    $instanceCount = $json.methods.Count
    $classCount = $json.classMethods.Count
    $total = $instanceCount + $classCount
    $totalMethods += $total
    
    if ($total -gt 0) {
        Write-Host "$($json.className): " -NoNewline
        Write-Host "$instanceCount instance methods" -ForegroundColor Green -NoNewline
        if ($classCount -gt 0) {
            Write-Host ", $classCount class methods" -ForegroundColor Cyan -NoNewline
        }
        Write-Host ""
    }
}
Write-Host "`nTotal methods in Phase 3: $totalMethods" -ForegroundColor Yellow