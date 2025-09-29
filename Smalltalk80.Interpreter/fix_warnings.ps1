# PowerShell script to fix common warnings in the Smalltalk80.Interpreter project

Write-Host "Fixing warnings in Smalltalk80.Interpreter..." -ForegroundColor Green

# Get all C# files excluding bin and obj directories
$files = Get-ChildItem -Path . -Filter "*.cs" -Recurse | 
    Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" }

$totalChanges = 0

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content
    
    # Skip Program.cs as it needs to remain public
    if ($file.Name -eq "Program.cs") {
        continue
    }
    
    # Fix CA1515: Change public to internal for non-API types
    $content = $content -replace '\bpublic\s+((?:static\s+)?(?:class|interface|enum|struct)\s+)', 'internal $1'
    $content = $content -replace '\bpublic\s+(static\s+)?abstract\s+class\s+', 'internal $1abstract class '
    $content = $content -replace '\bpublic\s+(static\s+)?sealed\s+class\s+', 'internal $1sealed class '
    
    # Fix method visibility in internal classes (public methods in internal classes should be internal)
    if ($content -match 'internal\s+(?:static\s+)?class\s+') {
        $content = $content -replace '(\s+)public\s+((?:static|virtual|override|abstract|sealed)\s+)*([^(]+\([^)]*\))', '$1internal $2$3'
        $content = $content -replace '(\s+)public\s+((?:static\s+)?[A-Za-z<>?\[\]]+\s+[A-Za-z_][A-Za-z0-9_]*\s*\{)', '$1internal $2'
    }
    
    # Fix CA1805: Don't explicitly initialize to default values
    $content = $content -replace '\s*=\s*0;(\s*//[^\r\n]*)?$', ';$1' -replace '(?m)^'
    $content = $content -replace '\s*=\s*null;(\s*//[^\r\n]*)?$', ';$1' -replace '(?m)^'
    $content = $content -replace '\s*=\s*false;(\s*//[^\r\n]*)?$', ';$1' -replace '(?m)^'
    
    # Fix specific delegate naming (CA1711)
    $content = $content -replace '\bPrimitiveDelegate\b', 'PrimitiveHandler'
    
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        $totalChanges++
        Write-Host "  Updated: $($file.Name)" -ForegroundColor Yellow
    }
}

Write-Host "`nTotal files updated: $totalChanges" -ForegroundColor Cyan