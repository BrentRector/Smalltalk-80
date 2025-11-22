#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Extracts individual cursor bitmaps from ARROWS10.AL file and outputs them in Form format.

.DESCRIPTION
    Reads the Alto bitmap collection file (ARROWS10.AL) and extracts each cursor/arrow
    bitmap, outputting them in the Smalltalk-80 Form file format:
    - Word 0: fileCode (1)
    - Word 1: width
    - Word 2: height
    - Word 3: offsetX (signed)
    - Word 4: offsetY (signed)
    - Word 5+: bitmap data (column-major order)

.PARAMETER InputFile
    Path to the ARROWS10.AL file

.PARAMETER OutputDirectory
    Directory where individual Form files will be written (default: current directory)

.EXAMPLE
    ./Extract-ArrowBitmaps.ps1 -InputFile ARROWS10.AL -OutputDirectory ./forms/
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$InputFile,

    [Parameter(Mandatory=$false)]
    [string]$OutputDirectory = "."
)

# Ensure output directory exists
if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
}

# Read the entire file as bytes
$bytes = [System.IO.File]::ReadAllBytes($InputFile)
Write-Host "File size: $($bytes.Length) bytes" -ForegroundColor Cyan

# Helper function to read big-endian 16-bit word
function Read-Word {
    param([byte[]]$bytes, [int]$offset)
    return ([uint16]$bytes[$offset] -shl 8) -bor [uint16]$bytes[$offset + 1]
}

# Helper function to write big-endian 16-bit word
function Write-Word {
    param([System.IO.BinaryWriter]$writer, [uint16]$value)
    $writer.Write([byte]($value -shr 8))
    $writer.Write([byte]($value -band 0xFF))
}

# Helper function to convert signed 16-bit (two's complement)
function ConvertTo-SignedInt16 {
    param([uint16]$value)
    if ($value -gt 32767) {
        return [int]$value - 65536
    }
    return [int]$value
}

# Parse the header - appears to be a table of offsets
Write-Host "`nParsing header..." -ForegroundColor Yellow

# First few bytes look like a signature
$sig1 = $bytes[0]
$sig2 = Read-Word -bytes $bytes -offset 1

Write-Host "Signature: 0x$($sig1.ToString('X2')) 0x$($sig2.ToString('X4'))" -ForegroundColor Gray

# The file appears to have an offset table at the beginning
# Let's scan for bitmap metadata patterns (width, height values)

$bitmapCount = 0
$bitmaps = @()

# Starting after the header, look for bitmap definitions
# Each bitmap should have: width, height, offset data, then bitmap bits
$pos = 0x220  # Starting position after offset table (approximate from octal dump)

Write-Host "`nScanning for bitmaps..." -ForegroundColor Yellow

while ($pos -lt $bytes.Length - 20) {
    # Try to read potential bitmap header
    $word1 = Read-Word -bytes $bytes -offset $pos
    $word2 = Read-Word -bytes $bytes -offset ($pos + 2)
    $width = Read-Word -bytes $bytes -offset ($pos + 4)
    $height = Read-Word -bytes $bytes -offset ($pos + 6)
    $offsetX = Read-Word -bytes $bytes -offset ($pos + 8)
    $offsetY = Read-Word -bytes $bytes -offset ($pos + 10)

    # Check if this looks like a valid bitmap header
    # Reasonable dimensions for cursor: 1-64 pixels, offsets -64 to 64
    if ($width -gt 0 -and $width -le 64 -and $height -gt 0 -and $height -le 64) {
        # Calculate expected bitmap size
        $wordWidth = [Math]::Floor(($width + 15) / 16)
        $bitmapWords = $wordWidth * $height
        $bitmapBytes = $bitmapWords * 2

        # Check if we have enough data
        if (($pos + 12 + $bitmapBytes) -le $bytes.Length) {
            $bitmapCount++

            $signedOffsetX = ConvertTo-SignedInt16 -value $offsetX
            $signedOffsetY = ConvertTo-SignedInt16 -value $offsetY

            Write-Host "Bitmap $bitmapCount found at offset 0x$($pos.ToString('X4')):" -ForegroundColor Green
            Write-Host "  Size: ${width}x${height}" -ForegroundColor White
            Write-Host "  Offset: $signedOffsetX,$signedOffsetY" -ForegroundColor White
            Write-Host "  Bitmap data: $bitmapWords words ($bitmapBytes bytes)" -ForegroundColor White

            # Store bitmap info
            $bitmaps += @{
                Number = $bitmapCount
                Position = $pos
                Width = $width
                Height = $height
                OffsetX = $offsetX
                OffsetY = $offsetY
                BitmapStart = $pos + 12
                BitmapWords = $bitmapWords
            }

            # Move to next potential bitmap
            $pos += 12 + $bitmapBytes
        } else {
            $pos += 2
        }
    } else {
        $pos += 2
    }

    # Safety limit
    if ($bitmapCount -ge 50) {
        break
    }
}

Write-Host "`nFound $bitmapCount bitmaps total" -ForegroundColor Cyan

# Output each bitmap in Form format
Write-Host "`nGenerating Form files..." -ForegroundColor Yellow

foreach ($bitmap in $bitmaps) {
    $formFile = Join-Path $OutputDirectory "arrow$($bitmap.Number).form"

    try {
        $stream = [System.IO.File]::Create($formFile)
        $writer = [System.IO.BinaryWriter]::new($stream)

        # Write Form header
        Write-Word -writer $writer -value 1  # fileCode
        Write-Word -writer $writer -value $bitmap.Width
        Write-Word -writer $writer -value $bitmap.Height
        Write-Word -writer $writer -value $bitmap.OffsetX
        Write-Word -writer $writer -value $bitmap.OffsetY

        # Write bitmap data
        $bitmapStart = $bitmap.BitmapStart
        $bitmapEnd = $bitmapStart + ($bitmap.BitmapWords * 2)

        for ($i = $bitmapStart; $i -lt $bitmapEnd; $i += 2) {
            $word = Read-Word -bytes $bytes -offset $i
            Write-Word -writer $writer -value $word
        }

        $writer.Close()
        $stream.Close()

        Write-Host "  Created: $formFile ($($bitmap.Width)x$($bitmap.Height))" -ForegroundColor Green

    } catch {
        Write-Host "  Error creating $formFile : $_" -ForegroundColor Red
    }
}

# Also output a text representation of each bitmap
$textFile = Join-Path $OutputDirectory "arrows_visual.txt"
$output = @()
$output += "ARROWS10.AL Bitmap Visualization"
$output += "=" * 80
$output += ""

foreach ($bitmap in $bitmaps) {
    $output += ""
    $output += "Bitmap $($bitmap.Number): $($bitmap.Width)x$($bitmap.Height) at offset ($($bitmap.OffsetX),$($bitmap.OffsetY))"
    $output += "-" * 80

    # Read bitmap data
    $bitmapStart = $bitmap.BitmapStart
    $wordWidth = [Math]::Floor(($bitmap.Width + 15) / 16)

    # Build the bitmap as a 2D array
    $pixels = @()
    for ($row = 0; $row -lt $bitmap.Height; $row++) {
        $rowBits = ""
        for ($col = 0; $col -lt $wordWidth; $col++) {
            $wordOffset = $bitmapStart + (($col * $bitmap.Height + $row) * 2)
            if ($wordOffset + 1 -lt $bytes.Length) {
                $word = Read-Word -bytes $bytes -offset $wordOffset

                # Convert word to binary string (16 bits)
                for ($bit = 15; $bit -ge 0; $bit--) {
                    if (($col * 16 + (15 - $bit)) -lt $bitmap.Width) {
                        if (($word -band (1 -shl $bit)) -ne 0) {
                            $rowBits += "█"
                        } else {
                            $rowBits += "·"
                        }
                    }
                }
            }
        }
        $pixels += $rowBits
    }

    $output += $pixels
    $output += ""
}

$output | Out-File -FilePath $textFile -Encoding UTF8
Write-Host "`nVisual representation saved to: $textFile" -ForegroundColor Cyan

Write-Host "`nDone! Extracted $bitmapCount bitmaps." -ForegroundColor Green
