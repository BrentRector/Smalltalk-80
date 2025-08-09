# Script to fix literal format in Phase 3 JSON files
# Converts literals from objects with "Value" property to plain strings

param(
    [string[]]$Files = @(
        "..\Bootstrap\003_Symbol.json",
        "..\Bootstrap\004_Character.json", 
        "..\Bootstrap\005_Number.json",
        "..\Bootstrap\006_Integer.json"
    )
)

foreach ($file in $Files) {
    if (Test-Path $file) {
        Write-Host "Processing $file..." -ForegroundColor Cyan
        
        # Read the JSON file
        $json = Get-Content $file -Raw | ConvertFrom-Json
        
        # Process each method's literals
        foreach ($method in $json.methods) {
            if ($method.literals -and $method.literals.Count -gt 0) {
                $newLiterals = @()
                foreach ($literal in $method.literals) {
                    if ($literal -is [PSCustomObject] -and $literal.PSObject.Properties['Value']) {
                        # Extract the Value property
                        $newLiterals += $literal.Value
                    } else {
                        # Keep as is
                        $newLiterals += $literal
                    }
                }
                $method.literals = $newLiterals
            }
        }
        
        # Process class methods too
        foreach ($method in $json.classMethods) {
            if ($method.literals -and $method.literals.Count -gt 0) {
                $newLiterals = @()
                foreach ($literal in $method.literals) {
                    if ($literal -is [PSCustomObject] -and $literal.PSObject.Properties['Value']) {
                        # Extract the Value property
                        $newLiterals += $literal.Value
                    } else {
                        # Keep as is
                        $newLiterals += $literal
                    }
                }
                $method.literals = $newLiterals
            }
        }
        
        # Convert back to JSON with proper formatting
        $jsonString = $json | ConvertTo-Json -Depth 10
        
        # Write back to file
        Set-Content $file -Value $jsonString
        Write-Host "✓ Fixed $file" -ForegroundColor Green
    } else {
        Write-Host "✗ File not found: $file" -ForegroundColor Red
    }
}

Write-Host "`nAll files processed!" -ForegroundColor Green