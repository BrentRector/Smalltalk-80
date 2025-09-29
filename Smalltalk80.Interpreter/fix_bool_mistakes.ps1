#!/usr/bin/env pwsh

Write-Host "Fixing boolean mistakes in Smalltalk80.Interpreter project..." -ForegroundColor Green

$files = Get-ChildItem -Path "." -Include "*.cs" -Recurse

foreach ($file in $files) {
    $content = Get-Content -Path $file.FullName -Raw
    $originalContent = $content
    
    # Fix disposed = STrue/SFalse -> true/false
    $content = $content -replace 'disposed = STrue', 'disposed = true'
    $content = $content -replace 'disposed = SFalse', 'disposed = false'
    
    # Fix _disposed = STrue/SFalse -> true/false
    $content = $content -replace '_disposed = STrue', '_disposed = true'
    $content = $content -replace '_disposed = SFalse', '_disposed = false'
    
    # Fix Dispose(STrue) -> Dispose(true)
    $content = $content -replace 'Dispose\(STrue\)', 'Dispose(true)'
    $content = $content -replace 'Dispose\(SFalse\)', 'Dispose(false)'
    
    # Fix EnablePooling = STrue -> true
    $content = $content -replace 'EnablePooling { get; set; } = STrue', 'EnablePooling { get; set; } = true'
    $content = $content -replace 'EnableGCIntegration { get; set; } = STrue', 'EnableGCIntegration { get; set; } = true'
    
    # Fix comparison results that should not be STrue/SFalse
    $content = $content -replace '== STrue \? STrue\.Instance : SFalse\.Instance', '== true ? STrue.Instance : SFalse.Instance'
    
    # Fix bool returns that are incorrectly changed
    $content = $content -replace 'return STrue;', 'return true;'  
    $content = $content -replace 'return SFalse;', 'return false;'
    
    # Fix comment mistakes
    $content = $content -replace 'otherwise, SFalse', 'otherwise, false'
    $content = $content -replace 'True if', 'true if'
    $content = $content -replace 'SFalse if', 'false if'
    
    # Save only if changed
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        Write-Host "  Fixed $($file.Name)" -ForegroundColor Yellow
    }
}

Write-Host "`nBoolean fixes completed!" -ForegroundColor Green