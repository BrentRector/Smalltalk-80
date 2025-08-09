# Fix JSON literals in Phase 3 files by parsing as JSON and fixing the literals arrays
param(
    [string[]]$Files = @('Bootstrap/005_Number.json', 'Bootstrap/006_Integer.json')
)

function Fix-JsonLiterals {
    param([string]$FilePath)
    
    if (-not (Test-Path $FilePath)) {
        Write-Host "File not found: $FilePath"
        return
    }
    
    Write-Host "Processing $FilePath..."
    
    try {
        # Read and parse the JSON
        $jsonContent = Get-Content $FilePath -Raw
        $jsonObject = $jsonContent | ConvertFrom-Json
        
        # Fix literals in methods
        foreach ($method in $jsonObject.methods) {
            if ($method.literals) {
                for ($i = 0; $i -lt $method.literals.Count; $i++) {
                    $literal = $method.literals[$i]
                    # Convert all literals to strings
                    if ($literal -is [int] -or $literal -is [double] -or $literal -is [bool]) {
                        $method.literals[$i] = $literal.ToString()
                    }
                    elseif ($literal -eq $null) {
                        $method.literals[$i] = "null"
                    }
                }
            }
        }
        
        # Convert back to JSON and save
        $fixedJson = $jsonObject | ConvertTo-Json -Depth 10 -Compress:$false
        Set-Content $FilePath $fixedJson -NoNewline
        
        Write-Host "Fixed literals in $FilePath"
    }
    catch {
        Write-Error "Failed to process ${FilePath}: $($_.Exception.Message)"
    }
}

# Process each file
foreach ($file in $Files) {
    Fix-JsonLiterals -FilePath $file
}

Write-Host "JSON literal fixing complete."