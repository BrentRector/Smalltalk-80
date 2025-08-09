# Count methods in extracted classes
$totalInstance = 0
$totalClass = 0

Get-ChildItem Bootstrap\phase3_extracted\*.json | ForEach-Object {
    $json = Get-Content $_.FullName | ConvertFrom-Json
    $totalInstance += $json.instanceMethods.Count
    $totalClass += $json.classMethods.Count
}

Write-Host "Total instance methods: $totalInstance" -ForegroundColor Green
Write-Host "Total class methods: $totalClass" -ForegroundColor Cyan
Write-Host "Total methods: $($totalInstance + $totalClass)" -ForegroundColor Yellow