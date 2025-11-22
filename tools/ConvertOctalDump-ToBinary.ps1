#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Converts an octal dump (od output) to a binary file.

.DESCRIPTION
    Takes the output from 'od -t oC' command and reconstructs the original binary file.
    This allows extracting the ARROWS10.AL file from the octal dump provided.

.PARAMETER InputFile
    Path to the file containing the octal dump text

.PARAMETER OutputFile
    Path where the binary file will be written

.EXAMPLE
    ./ConvertOctalDump-ToBinary.ps1 -InputFile arrows_dump.txt -OutputFile ARROWS10.AL
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$InputFile,

    [Parameter(Mandatory=$true)]
    [string]$OutputFile
)

# Read the octal dump file
$lines = Get-Content $InputFile

# Parse octal dump and extract bytes
$bytes = @()

foreach ($line in $lines) {
    # Skip empty lines
    if ([string]::IsNullOrWhiteSpace($line)) {
        continue
    }

    # Parse line: "0000000   000 036 236 036 001 021 ..."
    # Split on whitespace and skip the first column (offset)
    $parts = $line -split '\s+' | Where-Object { $_ -ne '' }

    if ($parts.Length -gt 1) {
        # Skip the offset (first column)
        for ($i = 1; $i -lt $parts.Length; $i++) {
            $octal = $parts[$i]

            # Only process valid octal values (3 digits)
            if ($octal -match '^\d{3}$') {
                # Convert octal string to byte
                $value = [Convert]::ToByte($octal, 8)
                $bytes += $value
            }
        }
    }
}

Write-Host "Parsed $($bytes.Length) bytes from octal dump" -ForegroundColor Cyan

# Write binary file
[System.IO.File]::WriteAllBytes($OutputFile, [byte[]]$bytes)

Write-Host "Binary file written to: $OutputFile" -ForegroundColor Green
