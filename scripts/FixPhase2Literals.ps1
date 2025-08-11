# Fix Phase 2 bootstrap to use proper SmalltalkObject references for literals
param(
    [string]$InputFile = "..\Bootstrap\bootstrap_phase2.json",
    [string]$OutputFile = "..\Bootstrap\bootstrap_phase2_fixed.json"
)

Write-Host "Loading Phase 2 bootstrap file..." -ForegroundColor Cyan
$phase2 = Get-Content $InputFile -Raw | ConvertFrom-Json

# Create objects section for all unique symbols and associations
$objects = @()
$symbolMap = @{}
$nextId = 1000  # Start IDs at 1000 to avoid conflicts

Write-Host "Processing literals to extract symbols and associations..." -ForegroundColor Cyan

# Process all methods to collect unique literals
foreach ($class in $phase2.classes) {
    foreach ($method in $class.methods) {
        if ($method.literals) {
            $newLiterals = @()
            foreach ($literal in $method.literals) {
                if ($literal -is [string]) {
                    # Parse the literal string to determine its type
                    if ($literal -match '^Association(.+)$') {
                        # It's an Association - we'll handle these specially
                        # For now, treat as a symbol
                        $symbolId = "symbol_$nextId"
                        if (-not $symbolMap.ContainsKey($literal)) {
                            $symbolMap[$literal] = $symbolId
                            $objects += @{
                                id = $symbolId
                                className = "Symbol"
                                fields = @{
                                    value = $literal
                                }
                            }
                            $nextId++
                        }
                        $newLiterals += $symbolMap[$literal]
                    }
                    else {
                        # It's a regular symbol
                        $symbolId = "symbol_$nextId"
                        if (-not $symbolMap.ContainsKey($literal)) {
                            $symbolMap[$literal] = $symbolId
                            $objects += @{
                                id = $symbolId
                                className = "Symbol"
                                fields = @{
                                    value = $literal
                                }
                            }
                            $nextId++
                        }
                        $newLiterals += $symbolMap[$literal]
                    }
                }
                elseif ($literal -is [int] -or $literal -is [long]) {
                    # SmallInteger - create as object
                    $intId = "int_$nextId"
                    $objects += @{
                        id = $intId
                        className = "SmallInteger"
                        value = $literal
                    }
                    $newLiterals += $intId
                    $nextId++
                }
                else {
                    # Keep as-is for now
                    $newLiterals += $literal
                }
            }
            $method.literals = $newLiterals
        }
    }
}

# Also process class methods if they exist
foreach ($class in $phase2.classes) {
    if ($class.classMethods) {
        foreach ($method in $class.classMethods) {
            if ($method.literals) {
                $newLiterals = @()
                foreach ($literal in $method.literals) {
                    if ($literal -is [string]) {
                        $symbolId = "symbol_$nextId"
                        if (-not $symbolMap.ContainsKey($literal)) {
                            $symbolMap[$literal] = $symbolId
                            $objects += @{
                                id = $symbolId
                                className = "Symbol"
                                fields = @{
                                    value = $literal
                                }
                            }
                            $nextId++
                        }
                        $newLiterals += $symbolMap[$literal]
                    }
                    elseif ($literal -is [int] -or $literal -is [long]) {
                        $intId = "int_$nextId"
                        $objects += @{
                            id = $intId
                            className = "SmallInteger"
                            value = $literal
                        }
                        $newLiterals += $intId
                        $nextId++
                    }
                    else {
                        $newLiterals += $literal
                    }
                }
                $method.literals = $newLiterals
            }
        }
    }
}

Write-Host "Created $($objects.Count) objects for literals" -ForegroundColor Green

# Create the new Phase 2 structure with objects
$newPhase2 = @{
    phase = $phase2.phase
    description = $phase2.description + " (with SmalltalkObject literals)"
    comments = $phase2.comments + @(
        "Modified to use SmalltalkObject references for all literals",
        "All symbols and integers are now proper objects"
    )
    objects = $objects
    classes = $phase2.classes
}

# Save the modified Phase 2
Write-Host "Saving modified Phase 2 to $OutputFile..." -ForegroundColor Cyan
$newPhase2 | ConvertTo-Json -Depth 100 | Set-Content $OutputFile -Encoding UTF8

Write-Host "Phase 2 bootstrap file has been fixed!" -ForegroundColor Green
Write-Host "Objects created: $($objects.Count)" -ForegroundColor Yellow
Write-Host "Output saved to: $OutputFile" -ForegroundColor Yellow