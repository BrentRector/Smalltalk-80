# Fix numeric and boolean literals in Phase 3 JSON files to be strings
$files = @('Bootstrap/005_Number.json', 'Bootstrap/006_Integer.json')

foreach ($file in $files) {
    if (Test-Path $file) {
        Write-Host "Fixing literals in $file"
        $content = Get-Content $file -Raw
        
        # Use a more specific regex to fix numeric literals within the literals array
        # This matches number literals that are not already quoted within literal arrays
        $content = $content -replace '("literals":\s*\[[^\]]*?)(\d+)([^\]]*?\])', '$1"$2"$3'
        
        # Fix boolean and null values in literals arrays
        $content = $content -replace '("literals":\s*\[[^\]]*?)\btrue\b([^\]]*?\])', '$1"true"$2'
        $content = $content -replace '("literals":\s*\[[^\]]*?)\bfalse\b([^\]]*?\])', '$1"false"$2'
        $content = $content -replace '("literals":\s*\[[^\]]*?)\bnull\b([^\]]*?\])', '$1"null"$2'
        
        Set-Content $file $content -NoNewline
        Write-Host "Fixed $file"
    } else {
        Write-Host "File $file not found"
    }
}

Write-Host "Literal fixing complete."