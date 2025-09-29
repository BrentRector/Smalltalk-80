#!/usr/bin/env pwsh
# Script to replace all occurrences of 'new SCompiledMethod(' with 'SCompiledMethod.CreateCompiledMethod('

$files = @(
    "Bootstrap\KernelMethodsInstaller.cs",
    "Bootstrap\CompilationMethodsInstaller.cs",
    "VirtualMachine.cs"
)

foreach ($file in $files) {
    $fullPath = Join-Path $PSScriptRoot $file
    if (Test-Path $fullPath) {
        Write-Host "Processing $file..."
        $content = Get-Content $fullPath -Raw
        $newContent = $content -replace 'new SCompiledMethod\(', 'SCompiledMethod.CreateCompiledMethod('
        
        if ($content -ne $newContent) {
            Set-Content $fullPath $newContent -NoNewline
            Write-Host "  Updated $file" -ForegroundColor Green
        } else {
            Write-Host "  No changes needed in $file" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  File not found: $file" -ForegroundColor Red
    }
}

Write-Host "Done!" -ForegroundColor Green