#!/usr/bin/env pwsh

Write-Host "Fixing singleton names in Smalltalk80.Interpreter project..." -ForegroundColor Green

$files = Get-ChildItem -Path "." -Include "*.cs" -Recurse

foreach ($file in $files) {
    Write-Host "Processing $($file.FullName)..."
    $content = Get-Content -Path $file.FullName -Raw
    
    # Replace Nil with SNil (but not SNil with SSNil!)
    $content = $content -replace '(?<![S])Nil\.Instance', 'SNil.Instance'
    $content = $content -replace '(?<![S])Nil\.', 'SNil.'
    $content = $content -replace ' Nil ', ' SNil '
    $content = $content -replace '\(Nil\)', '(SNil)'
    $content = $content -replace '= Nil', '= SNil'
    $content = $content -replace ', Nil', ', SNil'
    
    # Replace True with STrue
    $content = $content -replace '(?<![S])True\.Instance', 'STrue.Instance'
    $content = $content -replace '(?<![S])True\.', 'STrue.'
    $content = $content -replace ' True ', ' STrue '
    $content = $content -replace '\(True\)', '(STrue)'
    $content = $content -replace '= True', '= STrue'
    $content = $content -replace ', True', ', STrue'
    
    # Replace False with SFalse
    $content = $content -replace '(?<![S])False\.Instance', 'SFalse.Instance'
    $content = $content -replace '(?<![S])False\.', 'SFalse.'
    $content = $content -replace ' False ', ' SFalse '
    $content = $content -replace '\(False\)', '(SFalse)'
    $content = $content -replace '= False', '= SFalse'
    $content = $content -replace ', False', ', SFalse'
    
    # Save only if changed
    $originalContent = Get-Content -Path $file.FullName -Raw
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        Write-Host "  Updated $($file.Name)" -ForegroundColor Yellow
    }
}

Write-Host "`nSingleton name fixes completed!" -ForegroundColor Green