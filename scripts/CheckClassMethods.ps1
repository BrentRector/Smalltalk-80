# Check which Phase 3 files have class methods
Get-ChildItem Bootstrap\phase3_final\*.json | ForEach-Object {
    $json = Get-Content $_.FullName | ConvertFrom-Json
    if ($json.classMethods.Count -gt 0) {
        Write-Host "$($_.Name): $($json.classMethods.Count) class methods"
        $json.classMethods | ForEach-Object {
            Write-Host "  - $($_.selector)"
        }
    }
}