$SystemImagePath = "../Bootstrap/bootstrap.json"
$SystemImagePath = [System.IO.Path]::GetFullPath($SystemImagePath)
Write-Host "Path: $SystemImagePath"
Write-Host "Exists: $(Test-Path $SystemImagePath)"
$content = Get-Content $SystemImagePath -Raw
Write-Host "Content length: $($content.Length)"
$json = $content | ConvertFrom-Json
Write-Host "Classes in JSON: $($json.classes.Count)"