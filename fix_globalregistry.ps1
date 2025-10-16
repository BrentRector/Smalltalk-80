# PowerShell script to fix GlobalRegistry API changes across all files

# Fix patterns in all .cs files
$files = Get-ChildItem -Path "D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter" -Filter "*.cs" -Recurse

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content
    
    # Pattern 1: Replace GlobalRegistry.NilObjectId with GlobalRegistry.NilOOP
    $content = $content -replace 'SmalltalkOOP\.FromObjectTableIndex\(GlobalRegistry\.NilObjectId\)', 'GlobalRegistry.NilOOP'
    $content = $content -replace '\.ToObjectTableIndex\(\)\s*==\s*GlobalRegistry\.NilObjectId', ' == GlobalRegistry.NilOOP'
    $content = $content -replace '\.ToObjectTableIndex\(\)\s*!=\s*GlobalRegistry\.NilObjectId', ' != GlobalRegistry.NilOOP'
    $content = $content -replace 'GlobalRegistry\.NilObjectId', 'GlobalRegistry.NilOOP.ToObjectTableIndex()'
    
    # Pattern 2: Replace GlobalRegistry.TrueObjectId with GlobalRegistry.TrueOOP
    $content = $content -replace 'SmalltalkOOP\.FromObjectTableIndex\(GlobalRegistry\.TrueObjectId\)', 'GlobalRegistry.TrueOOP'
    $content = $content -replace '\.ToObjectTableIndex\(\)\s*==\s*GlobalRegistry\.TrueObjectId', ' == GlobalRegistry.TrueOOP'
    $content = $content -replace '\.ToObjectTableIndex\(\)\s*!=\s*GlobalRegistry\.TrueObjectId', ' != GlobalRegistry.TrueOOP'
    $content = $content -replace 'GlobalRegistry\.TrueObjectId', 'GlobalRegistry.TrueOOP.ToObjectTableIndex()'
    
    # Pattern 3: Replace GlobalRegistry.FalseObjectId with GlobalRegistry.FalseOOP
    $content = $content -replace 'SmalltalkOOP\.FromObjectTableIndex\(GlobalRegistry\.FalseObjectId\)', 'GlobalRegistry.FalseOOP'
    $content = $content -replace '\.ToObjectTableIndex\(\)\s*==\s*GlobalRegistry\.FalseObjectId', ' == GlobalRegistry.FalseOOP'
    $content = $content -replace '\.ToObjectTableIndex\(\)\s*!=\s*GlobalRegistry\.FalseObjectId', ' != GlobalRegistry.FalseOOP'
    $content = $content -replace 'GlobalRegistry\.FalseObjectId', 'GlobalRegistry.FalseOOP.ToObjectTableIndex()'
    
    # Pattern 4: Replace GlobalRegistry.SystemDictionaryId with GlobalRegistry.SystemDictionaryOOP
    $content = $content -replace 'SmalltalkOOP\.FromObjectTableIndex\(GlobalRegistry\.SystemDictionaryId\)', 'GlobalRegistry.SystemDictionaryOOP'
    $content = $content -replace '\.ToObjectTableIndex\(\)\s*==\s*GlobalRegistry\.SystemDictionaryId', ' == GlobalRegistry.SystemDictionaryOOP'
    $content = $content -replace '\.ToObjectTableIndex\(\)\s*!=\s*GlobalRegistry\.SystemDictionaryId', ' != GlobalRegistry.SystemDictionaryOOP'
    $content = $content -replace 'GlobalRegistry\.SystemDictionaryId', 'GlobalRegistry.SystemDictionaryOOP.ToObjectTableIndex()'
    
    # Pattern 5: Replace GlobalRegistry.LookupClassObjectId with GlobalRegistry.LookupClassOOP
    $content = $content -replace 'int\s+(\w+)\s*=\s*GlobalRegistry\.LookupClassObjectId\((.*?)\)', 'SmalltalkOOP $1 = GlobalRegistry.LookupClassOOP($2)'
    $content = $content -replace 'GlobalRegistry\.LookupClassObjectId\((.*?)\)', 'GlobalRegistry.LookupClassOOP($1).ToObjectTableIndex()'
    
    # Pattern 6: Fix TryGet calls
    $content = $content -replace 'GlobalRegistry\.TryGet\((.*?),\s*out\s+int\s+(\w+)\)', 'GlobalRegistry.TryGet($1, out SmalltalkOOP $2)'
    
    # Pattern 7: Fix Get calls wrapped in FromObjectTableIndex
    $content = $content -replace 'SmalltalkOOP\.FromObjectTableIndex\(GlobalRegistry\.Get\((.*?)\)\)', 'GlobalRegistry.Get($1)'
    
    # Pattern 8: Fix Register calls taking .ToObjectTableIndex()
    $content = $content -replace 'GlobalRegistry\.Register\((.*?),\s*(\w+)\.ToObjectTableIndex\(\)\)', 'GlobalRegistry.Register($1, $2)'
    $content = $content -replace 'GlobalRegistry\.RegisterClass\((.*?),\s*(\w+)\.ToObjectTableIndex\(\)\)', 'GlobalRegistry.RegisterClass($1, $2)'
    
    # Save if changed
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        Write-Host "Updated: $($file.Name)"
    }
}

Write-Host "Script complete!"