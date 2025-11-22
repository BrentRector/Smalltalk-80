#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Converts Smalltalk-80 Form files to Windows BMP format.

.DESCRIPTION
    Reads Form files (as created by Extract-ArrowBitmaps.ps1) and outputs
    standard Windows bitmap (.bmp) files that can be viewed with any image viewer.

.PARAMETER FormFile
    Path to the input Form file

.PARAMETER BmpFile
    Path to the output BMP file (optional, defaults to same name with .bmp extension)

.PARAMETER InputDirectory
    Directory containing Form files to convert

.PARAMETER OutputDirectory
    Directory where BMP files will be written

.EXAMPLE
    ./Convert-FormToBmp.ps1 -FormFile arrow1.form -BmpFile arrow1.bmp

.EXAMPLE
    ./Convert-FormToBmp.ps1 -InputDirectory ./arrow_output -OutputDirectory ./bmp_output
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$FormFile,

    [Parameter(Mandatory=$false)]
    [string]$BmpFile,

    [Parameter(Mandatory=$false)]
    [string]$InputDirectory,

    [Parameter(Mandatory=$false)]
    [string]$OutputDirectory
)

# Helper function to read big-endian 16-bit word
function Read-Word {
    param([byte[]]$bytes, [int]$offset)
    return ([uint16]$bytes[$offset] -shl 8) -bor [uint16]$bytes[$offset + 1]
}

# Helper function to convert signed 16-bit (two's complement)
function ConvertTo-SignedInt16 {
    param([uint16]$value)
    if ($value -gt 32767) {
        return [int]$value - 65536
    }
    return [int]$value
}

# Read and parse Form file
function Read-FormFile {
    param([string]$path)

    $bytes = [System.IO.File]::ReadAllBytes($path)

    # Read header
    $fileCode = Read-Word -bytes $bytes -offset 0
    if ($fileCode -ne 1) {
        throw "Invalid Form file: fileCode = $fileCode, expected 1"
    }

    $width = Read-Word -bytes $bytes -offset 2
    $height = Read-Word -bytes $bytes -offset 4
    $offsetX = ConvertTo-SignedInt16 -value (Read-Word -bytes $bytes -offset 6)
    $offsetY = ConvertTo-SignedInt16 -value (Read-Word -bytes $bytes -offset 8)

    # Calculate bitmap size
    $wordWidth = [Math]::Floor(($width + 15) / 16)
    $expectedWords = $wordWidth * $height

    # Read bitmap data (column-major)
    $bitmapData = @()
    $pos = 10
    for ($i = 0; $i -lt $expectedWords; $i++) {
        if ($pos + 1 -lt $bytes.Length) {
            $word = Read-Word -bytes $bytes -offset $pos
            $bitmapData += $word
            $pos += 2
        }
    }

    return @{
        Width = $width
        Height = $height
        OffsetX = $offsetX
        OffsetY = $offsetY
        WordWidth = $wordWidth
        BitmapData = $bitmapData
    }
}

# Convert Form data to BMP format
function ConvertTo-Bmp {
    param($formData)

    $width = $formData.Width
    $height = $formData.Height
    $wordWidth = $formData.WordWidth
    $bitmapData = $formData.BitmapData

    # Convert column-major to row-major
    $pixels = @()
    for ($row = 0; $row -lt $height; $row++) {
        $rowPixels = @()
        for ($col = 0; $col -lt $wordWidth; $col++) {
            # Get the word for this column and row
            $wordIndex = $col * $height + $row
            if ($wordIndex -lt $bitmapData.Count) {
                $word = $bitmapData[$wordIndex]

                # Extract 16 bits (MSB first)
                for ($bit = 15; $bit -ge 0; $bit--) {
                    $pixelX = $col * 16 + (15 - $bit)
                    if ($pixelX -lt $width) {
                        # 1 = black, 0 = white in Form
                        $pixelValue = if (($word -band (1 -shl $bit)) -ne 0) { 1 } else { 0 }
                        $rowPixels += $pixelValue
                    }
                }
            }
        }
        $pixels += ,$rowPixels
    }

    # BMP format requirements
    $bytesPerRowUnpacked = [Math]::Floor(($width + 7) / 8)
    $bytesPerRow = ([Math]::Floor(($bytesPerRowUnpacked + 3) / 4)) * 4

    # Create BMP data (bottom-to-top)
    $bmpPixels = [System.Collections.Generic.List[byte]]::new()

    for ($row = $height - 1; $row -ge 0; $row--) {  # Bottom-to-top
        $rowData = $pixels[$row]

        # Pack bits into bytes
        $rowBytes = [System.Collections.Generic.List[byte]]::new()
        for ($byteIdx = 0; $byteIdx -lt $bytesPerRowUnpacked; $byteIdx++) {
            $byteVal = 0
            for ($bitIdx = 0; $bitIdx -lt 8; $bitIdx++) {
                $pixelIdx = $byteIdx * 8 + $bitIdx
                if ($pixelIdx -lt $rowData.Count) {
                    # BMP: 1 = white, 0 = black (opposite of Form)
                    if ($rowData[$pixelIdx] -eq 0) {  # Form white
                        $byteVal = $byteVal -bor (1 -shl (7 - $bitIdx))  # BMP white
                    }
                }
            }
            $rowBytes.Add([byte]$byteVal)
        }

        # Pad to 4-byte boundary
        while ($rowBytes.Count -lt $bytesPerRow) {
            $rowBytes.Add([byte]0)
        }

        $bmpPixels.AddRange($rowBytes)
    }

    return Create-BmpFile -width $width -height $height -pixelData $bmpPixels.ToArray()
}

# Create BMP file
function Create-BmpFile {
    param([int]$width, [int]$height, [byte[]]$pixelData)

    # Calculate sizes
    $bytesPerRowUnpacked = [Math]::Floor(($width + 7) / 8)
    $bytesPerRow = ([Math]::Floor(($bytesPerRowUnpacked + 3) / 4)) * 4
    $pixelDataSize = $bytesPerRow * $height

    $fileHeaderSize = 14
    $dibHeaderSize = 40
    $colorTableSize = 2 * 4
    $headersSize = $fileHeaderSize + $dibHeaderSize + $colorTableSize
    $fileSize = $headersSize + $pixelDataSize

    $ms = [System.IO.MemoryStream]::new()
    $bw = [System.IO.BinaryWriter]::new($ms)

    # BMP File Header (14 bytes)
    $bw.Write([byte]0x42)  # 'B'
    $bw.Write([byte]0x4D)  # 'M'
    $bw.Write([uint32]$fileSize)
    $bw.Write([uint16]0)   # Reserved 1
    $bw.Write([uint16]0)   # Reserved 2
    $bw.Write([uint32]$headersSize)

    # DIB Header (BITMAPINFOHEADER, 40 bytes)
    $bw.Write([uint32]$dibHeaderSize)
    $bw.Write([int32]$width)
    $bw.Write([int32]$height)
    $bw.Write([uint16]1)   # Planes
    $bw.Write([uint16]1)   # Bits per pixel
    $bw.Write([uint32]0)   # Compression
    $bw.Write([uint32]$pixelDataSize)
    $bw.Write([int32]2835) # X pixels per meter
    $bw.Write([int32]2835) # Y pixels per meter
    $bw.Write([uint32]2)   # Colors in palette
    $bw.Write([uint32]2)   # Important colors

    # Color Table (RGBQUAD)
    # Color 0 = Black
    $bw.Write([byte]0)     # Blue
    $bw.Write([byte]0)     # Green
    $bw.Write([byte]0)     # Red
    $bw.Write([byte]0)     # Reserved

    # Color 1 = White
    $bw.Write([byte]255)   # Blue
    $bw.Write([byte]255)   # Green
    $bw.Write([byte]255)   # Red
    $bw.Write([byte]0)     # Reserved

    # Pixel data
    $bw.Write($pixelData)

    $result = $ms.ToArray()
    $bw.Close()
    $ms.Close()

    return $result
}

# Convert single Form file to BMP
function Convert-SingleFormToBmp {
    param([string]$formPath, [string]$bmpPath)

    Write-Host "Reading Form file: $formPath" -ForegroundColor Cyan
    $formData = Read-FormFile -path $formPath

    Write-Host "  Size: $($formData.Width)x$($formData.Height)" -ForegroundColor White
    Write-Host "  Offset: ($($formData.OffsetX), $($formData.OffsetY))" -ForegroundColor White

    Write-Host "Converting to BMP..." -ForegroundColor Yellow
    $bmpData = ConvertTo-Bmp -formData $formData

    Write-Host "Writing BMP file: $bmpPath" -ForegroundColor Cyan
    [System.IO.File]::WriteAllBytes($bmpPath, $bmpData)

    Write-Host "Done! Created $($bmpData.Length) byte BMP file" -ForegroundColor Green
}

# Convert directory of Form files
function Convert-DirectoryFormToBmp {
    param([string]$inputDir, [string]$outputDir)

    if (-not (Test-Path $inputDir)) {
        Write-Host "Error: Input directory not found: $inputDir" -ForegroundColor Red
        exit 1
    }

    if (-not (Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir | Out-Null
    }

    $formFiles = Get-ChildItem -Path $inputDir -Filter "*.form" | Sort-Object Name

    if ($formFiles.Count -eq 0) {
        Write-Host "No .form files found in $inputDir" -ForegroundColor Yellow
        return
    }

    Write-Host "Found $($formFiles.Count) Form file(s)" -ForegroundColor Cyan
    Write-Host ""

    foreach ($formFile in $formFiles) {
        $bmpFileName = $formFile.Name -replace '\.form$', '.bmp'
        $bmpPath = Join-Path $outputDir $bmpFileName

        try {
            Convert-SingleFormToBmp -formPath $formFile.FullName -bmpPath $bmpPath
            Write-Host ""
        } catch {
            Write-Host "Error converting $($formFile.Name): $_" -ForegroundColor Red
            Write-Host ""
        }
    }
}

# Main script logic
if ($InputDirectory -and $OutputDirectory) {
    Convert-DirectoryFormToBmp -inputDir $InputDirectory -outputDir $OutputDirectory
}
elseif ($FormFile) {
    if (-not (Test-Path $FormFile)) {
        Write-Host "Error: Form file not found: $FormFile" -ForegroundColor Red
        exit 1
    }

    if (-not $BmpFile) {
        $BmpFile = $FormFile -replace '\.form$', '.bmp'
    }

    Convert-SingleFormToBmp -formPath $FormFile -bmpPath $BmpFile
}
else {
    Write-Host "Usage:" -ForegroundColor Yellow
    Write-Host "  Convert-FormToBmp.ps1 -FormFile <form_file> [-BmpFile <bmp_file>]"
    Write-Host "    Convert a single Form file to BMP"
    Write-Host ""
    Write-Host "  Convert-FormToBmp.ps1 -InputDirectory <input_dir> -OutputDirectory <output_dir>"
    Write-Host "    Convert all .form files in a directory"
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Yellow
    Write-Host "  pwsh ./Convert-FormToBmp.ps1 -FormFile arrow1.form -BmpFile arrow1.bmp"
    Write-Host "  pwsh ./Convert-FormToBmp.ps1 -InputDirectory ./arrow_output -OutputDirectory ./bmp_output"
    exit 1
}
