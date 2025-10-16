# PowerShell script to fix remaining GlobalRegistry conversions

$files = Get-ChildItem -Path "D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter" -Filter "*.cs" -Recurse

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content
    
    # Fix patterns where LookupClassOOP result is wrapped
    $content = $content -replace 'SmalltalkOOP (\w+) = GlobalRegistry\.LookupClassOOP\((.*?)\);\s*var (\w+) = SmalltalkOOP\.FromObjectTableIndex\(\1\);', 'var $3 = GlobalRegistry.LookupClassOOP($2);'
    
    # Fix SmalltalkOOP.FromObjectTableIndex wrapping a variable that's already SmalltalkOOP
    $content = $content -replace 'SmalltalkOOP\.FromObjectTableIndex\((\w+Id)\)', '$1'
    $content = $content -replace 'SmalltalkOOP\.FromObjectTableIndex\((\w+OOP)\)', '$1'
    
    # Fix patterns where .ToObjectTableIndex() result is used directly
    $content = $content -replace 'int (\w+) = GlobalRegistry\.(\w+OOP)\.ToObjectTableIndex\(\)', 'var $1 = GlobalRegistry.$2'
    
    # Save if changed
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        Write-Host "Updated: $($file.Name)"
    }
}

Write-Host "Script complete!"