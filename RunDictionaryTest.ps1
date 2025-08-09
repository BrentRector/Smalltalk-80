# PowerShell script to test Dictionary extraction and compilation

Write-Host "=== Dictionary Class Extraction and Compilation Test ===" -ForegroundColor Cyan
Write-Host ""

# Add required assemblies
Add-Type -Path "bin\Debug\net9.0\Smalltalk80.dll"
Add-Type -Path "bin\Debug\net9.0\Smalltalk80.Compiler.dll"

$systemImagePath = "D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80\Smalltalk-80 Source\Smalltalk-80.st"

try {
    # Step 1: Extract class structure
    Write-Host "Step 1: Extracting Dictionary class structure..." -ForegroundColor Yellow
    $simpleExtractor = New-Object Smalltalk80.Compiler.SimpleClassExtractor($systemImagePath)
    $simpleExtractor.ParseSystemImage()
    
    $dictClass = $simpleExtractor.GetClass("Dictionary")
    if ($null -eq $dictClass) {
        Write-Host "Error: Dictionary class not found!" -ForegroundColor Red
        return
    }
    
    Write-Host "Found Dictionary class:" -ForegroundColor Green
    Write-Host "  Superclass: $($dictClass.Superclass)"
    Write-Host "  Instance Variables: $($dictClass.InstanceVariables -join ', ')"
    Write-Host "  Class Variables: $($dictClass.ClassVariables -join ', ')"
    Write-Host ""
    
    # Step 2: Extract methods
    Write-Host "Step 2: Extracting Dictionary methods..." -ForegroundColor Yellow
    $methods = $simpleExtractor.ExtractMethodsForClass($systemImagePath, "Dictionary")
    Write-Host "Found $($methods.Count) methods for Dictionary" -ForegroundColor Green
    
    # Group by category
    $categories = $methods | Group-Object -Property Category | Sort-Object Name
    Write-Host "`nMethods by category:"
    foreach ($cat in $categories) {
        $catName = if ($cat.Name) { $cat.Name } else { "uncategorized" }
        Write-Host "  ${catName}: $($cat.Count) methods"
    }
    Write-Host ""
    
    # Step 3: Compile methods
    Write-Host "Step 3: Compiling methods with BytecodeCompiler..." -ForegroundColor Yellow
    $compiler = New-Object Smalltalk80.Compiler.BytecodeCompiler
    $compiledMethods = @()
    $failedMethods = @()
    
    foreach ($method in $methods) {
        try {
            Write-Host "  Compiling $($method.Selector)..." -NoNewline
            
            $compiledData = $compiler.CompileMethod(
                $method.Source,
                $dictClass.InstanceVariables
            )
            
            $compiledMethod = @{
                Selector = $method.Selector
                Source = $method.Source
                Category = $method.Category
                IsClassMethod = $method.IsClassMethod
                ArgumentCount = $compiledData.ArgumentCount
                TemporaryCount = $compiledData.TemporaryCount
                IsPrimitive = $compiledData.IsPrimitive
                PrimitiveNumber = $compiledData.PrimitiveNumber
                Literals = $compiledData.Literals
                Bytecode = $compiledData.Bytecode
                BytecodeHex = [BitConverter]::ToString($compiledData.Bytecode).Replace("-", " ")
            }
            
            $compiledMethods += $compiledMethod
            Write-Host " OK" -ForegroundColor Green
        }
        catch {
            Write-Host " FAILED: $_" -ForegroundColor Red
            $failedMethods += @{
                Selector = $method.Selector
                Error = $_.Exception.Message
            }
        }
    }
    
    Write-Host ""
    
    # Step 4: Generate JSON
    Write-Host "Step 4: Generating JSON output..." -ForegroundColor Yellow
    
    $output = @{
        className = "Dictionary"
        superclass = $dictClass.Superclass
        instanceVariables = $dictClass.InstanceVariables
        classVariables = $dictClass.ClassVariables
        poolDictionaries = $dictClass.PoolDictionaries
        category = $dictClass.Category
        statistics = @{
            totalMethods = $methods.Count
            compiledSuccessfully = $compiledMethods.Count
            compilationFailed = $failedMethods.Count
            instanceMethods = ($compiledMethods | Where-Object { -not $_.IsClassMethod }).Count
            classMethods = ($compiledMethods | Where-Object { $_.IsClassMethod }).Count
        }
        methods = $compiledMethods | Sort-Object Category, Selector
        failedMethods = $failedMethods
    }
    
    $jsonPath = "D:\Users\brent\OneDrive\Src\Smalltalk-80\Dictionary_compiled.json"
    $output | ConvertTo-Json -Depth 10 | Set-Content $jsonPath
    
    Write-Host "Output written to: $jsonPath" -ForegroundColor Green
    $fileInfo = Get-Item $jsonPath
    Write-Host "File size: $($fileInfo.Length.ToString('N0')) bytes"
    Write-Host ""
    
    # Summary
    Write-Host "=== Summary ===" -ForegroundColor Cyan
    Write-Host "Total methods: $($methods.Count)"
    Write-Host "Successfully compiled: $($compiledMethods.Count)" -ForegroundColor Green
    Write-Host "Failed: $($failedMethods.Count)" -ForegroundColor $(if ($failedMethods.Count -eq 0) { "Green" } else { "Yellow" })
    
    if ($failedMethods.Count -gt 0) {
        Write-Host "`nFailed methods:" -ForegroundColor Yellow
        $failedMethods | Select-Object -First 10 | ForEach-Object {
            Write-Host "  - $($_.Selector): $($_.Error)"
        }
        if ($failedMethods.Count -gt 10) {
            Write-Host "  ... and $($failedMethods.Count - 10) more"
        }
    }
    
    # Show sample
    if ($compiledMethods.Count -gt 0) {
        $sample = $compiledMethods[0]
        Write-Host "`nSample compiled method: $($sample.Selector)" -ForegroundColor Cyan
        Write-Host "  Arguments: $($sample.ArgumentCount)"
        Write-Host "  Temporaries: $($sample.TemporaryCount)"
        Write-Host "  Bytecode length: $($sample.Bytecode.Length) bytes"
        $hexSample = if ($sample.BytecodeHex.Length -gt 50) { 
            $sample.BytecodeHex.Substring(0, 50) + "..." 
        } else { 
            $sample.BytecodeHex 
        }
        Write-Host "  Bytecode (hex): $hexSample"
        if ($sample.Literals.Count -gt 0) {
            $litSample = ($sample.Literals | Select-Object -First 5 | ForEach-Object { 
                if ($null -eq $_) { "null" } else { $_.ToString() }
            }) -join ", "
            Write-Host "  Literals: $litSample"
        }
    }
}
catch {
    Write-Host "`nError: $_" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace
}