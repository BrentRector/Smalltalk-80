#!/usr/bin/env pwsh
# Fix selector spaces in Phase 2 JSON WITHOUT corrupting bytecode
# Uses text replacement instead of JSON parsing to preserve data integrity

Write-Host "Fixing Selector Spaces in Phase 2 JSON (Safe Version)" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""

Push-Location (Split-Path $PSScriptRoot -Parent)

try {
    $jsonPath = "Bootstrap/bootstrap_phase2.json"
    $backupPath = "Bootstrap/bootstrap_phase2_backup_$(Get-Date -Format 'yyyyMMdd_HHmmss').json"
    
    # Backup original
    Write-Host "Creating backup at $backupPath..." -ForegroundColor Yellow
    Copy-Item $jsonPath $backupPath
    
    # Read file as text
    Write-Host "Loading Phase 2 JSON as text..." -ForegroundColor Yellow
    $jsonText = Get-Content $jsonPath -Raw
    
    # Count occurrences before
    $beforeCount = ([regex]::Matches($jsonText, '"selector":\s*"[^"]*\s+[^"]*"')).Count
    Write-Host "Found $beforeCount selectors with spaces" -ForegroundColor Yellow
    
    # Fix selectors with spaces using regex
    # This specifically targets only the "selector" field values
    $fixedCount = 0
    
    # Pattern to match selector fields with spaces
    $pattern = '"selector":\s*"([^"]*?)\s+([^"]*?)"'
    
    # Common multi-word selectors that need fixing
    $selectorsToFix = @(
        @{From = '"selector": "at: put:"'; To = '"selector": "at:put:"'},
        @{From = '"selector": "basicAt: put:"'; To = '"selector": "basicAt:put:"'},
        @{From = '"selector": "instVarAt: put:"'; To = '"selector": "instVarAt:put:"'},
        @{From = '"selector": "perform: with:"'; To = '"selector": "perform:with:"'},
        @{From = '"selector": "perform: with: with:"'; To = '"selector": "perform:with:with:"'},
        @{From = '"selector": "perform: with: with: with:"'; To = '"selector": "perform:with:with:with:"'},
        @{From = '"selector": "perform: withArguments:"'; To = '"selector": "perform:withArguments:"'},
        @{From = '"selector": "broadcast: with:"'; To = '"selector": "broadcast:with:"'},
        @{From = '"selector": "tryPrimitive2: with:"'; To = '"selector": "tryPrimitive2:with:"'},
        @{From = '"selector": "tryPrimitive3: with: with:"'; To = '"selector": "tryPrimitive3:with:with:"'},
        @{From = '"selector": "tryPrimitive4: with: with: with:"'; To = '"selector": "tryPrimitive4:with:with:with:"'},
        @{From = '"selector": "literalAt: put:"'; To = '"selector": "literalAt:put:"'},
        @{From = '"selector": "objectAt: put:"'; To = '"selector": "objectAt:put:"'},
        @{From = '"selector": "needsStack: encoder:"'; To = '"selector": "needsStack:encoder:"'},
        @{From = '"selector": "putSource: class: category: inFile:"'; To = '"selector": "putSource:class:category:inFile:"'},
        @{From = '"selector": "putSource: inFile:"'; To = '"selector": "putSource:inFile:"'},
        @{From = '"selector": "setSourcePosition: inFile:"'; To = '"selector": "setSourcePosition:inFile:"'},
        @{From = '"selector": "newBytes: flags: nTemps: nStack: nLits:"'; To = '"selector": "newBytes:flags:nTemps:nStack:nLits:"'},
        @{From = '"selector": "newMethod: header:"'; To = '"selector": "newMethod:header:"'},
        @{From = '"selector": "fileOutOn: moveSource: toFile:"'; To = '"selector": "fileOutOn:moveSource:toFile:"'},
        @{From = '"selector": "poolHas: ifTrue:"'; To = '"selector": "poolHas:ifTrue:"'},
        @{From = '"selector": "subclass: instanceVariableNames: classVariableNames: poolDictionaries: category:"'; To = '"selector": "subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:"'},
        @{From = '"selector": "subclass: otherSupers: instanceVariableNames: classVariableNames: category:"'; To = '"selector": "subclass:otherSupers:instanceVariableNames:classVariableNames:category:"'},
        @{From = '"selector": "superclass: methodDict: format: name: organization: instVarNames: classPool: sharedPools:"'; To = '"selector": "superclass:methodDict:format:name:organization:instVarNames:classPool:sharedPools:"'},
        @{From = '"selector": "validateFrom: in: instanceVariableNames: methods:"'; To = '"selector": "validateFrom:in:instanceVariableNames:methods:"'},
        @{From = '"selector": "variableByteSubclass: instanceVariableNames: classVariableNames: poolDictionaries: category:"'; To = '"selector": "variableByteSubclass:instanceVariableNames:classVariableNames:poolDictionaries:category:"'},
        @{From = '"selector": "variableSubclass: instanceVariableNames: classVariableNames: poolDictionaries: category:"'; To = '"selector": "variableSubclass:instanceVariableNames:classVariableNames:poolDictionaries:category:"'},
        @{From = '"selector": "variableWordSubclass: instanceVariableNames: classVariableNames: poolDictionaries: category:"'; To = '"selector": "variableWordSubclass:instanceVariableNames:classVariableNames:poolDictionaries:category:"'}
    )
    
    # Add more patterns for other multi-word selectors
    $moreSelectors = @(
        "addSelector: withMethod:", "addSelectorUnchecked: withMethod:", "compile: notifying: trailer:",
        "compile: notifying: trailer: ifFail:", "format: variable: words: pointers:",
        "insertClass: selector: in:", "removeClass: selector: in:", "scopeHas: ifTrue:",
        "selectorAtMethod: setClass:", "sourceCodeForMethod: at:", "updateInheritanceTable: oldSelf:",
        "whichSelectorsReferTo: special: byte:", "printMethodChunk: on: moveSource: toFile:",
        "printSubclassesOn: callingSuperclass: level:", "recompile: from:", "showVariableMenu: collect:",
        "accumulateInstVarNames: traversedClasses:", "removeKey: ifAbsent:", "at: ifAbsent:",
        "associationAt: ifAbsent:", "declare: from:", "findKey: ifAbsent:", "keyAtValue: ifAbsent:",
        "remove: ifAbsent:", "removeAssociation: ifAbsent:", "ifFalse: ifTrue:", "ifTrue: ifFalse:",
        "between: and:", "key: value:", "replaceFrom: to: with: startingAt:", "hasInterned: ifTrue:",
        "displayOn: at:", "findString: startingAt:", "primReplaceFrom: to: with: startingAt:",
        "replaceFrom: to: withByteArray: startingAt:", "doubleWordAt: put:", "packBits: into:",
        "packBits: startBit: into:", "unpackBits: into:", "wordAt: put:", "detect: ifNone:",
        "inject: into:", "with: with:", "with: with: with:", "with: with: with: with:"
    )
    
    foreach ($selector in $moreSelectors) {
        $from = "`"selector`": `"$selector`""
        $to = "`"selector`": `"$($selector -replace '\s+', '')`""
        $selectorsToFix += @{From = $from; To = $to}
    }
    
    # Apply replacements
    foreach ($fix in $selectorsToFix) {
        if ($jsonText.Contains($fix.From)) {
            $jsonText = $jsonText.Replace($fix.From, $fix.To)
            $fixedCount++
            Write-Host "  Fixed: $($fix.From) -> $($fix.To)" -ForegroundColor Green
        }
    }
    
    # Additional pass to catch any remaining patterns
    $jsonText = $jsonText -replace '"selector":\s*"([^:]+):\s+([^"]+)"', '"selector": "$1:$2"'
    
    # Count occurrences after
    $afterCount = ([regex]::Matches($jsonText, '"selector":\s*"[^"]*\s+[^"]*"')).Count
    
    Write-Host ""
    Write-Host "Fixed $fixedCount selectors" -ForegroundColor Green
    Write-Host "Remaining selectors with spaces: $afterCount" -ForegroundColor $(if ($afterCount -eq 0) { "Green" } else { "Yellow" })
    
    # Verify JSON is still valid
    Write-Host ""
    Write-Host "Verifying JSON validity..." -ForegroundColor Yellow
    try {
        $testParse = $jsonText | ConvertFrom-Json
        Write-Host "✓ JSON is valid" -ForegroundColor Green
        
        # Check for [64, 124] pattern
        $problemCount = 0
        foreach ($class in $testParse.classes) {
            foreach ($method in $class.methods) {
                if ($method.bytecode.Count -eq 2 -and $method.bytecode[0] -eq 64 -and $method.bytecode[1] -eq 124) {
                    $problemCount++
                }
            }
        }
        
        if ($problemCount -gt 0) {
            Write-Host "✗ WARNING: Found $problemCount methods with [64, 124] bytecode!" -ForegroundColor Red
            Write-Host "  This file may be corrupted. Consider using the backup." -ForegroundColor Red
        } else {
            Write-Host "✓ No problematic [64, 124] bytecode patterns found" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "✗ JSON validation failed!" -ForegroundColor Red
        Write-Host "  Error: $_" -ForegroundColor Red
        Write-Host "  Restoring from backup..." -ForegroundColor Yellow
        Copy-Item $backupPath $jsonPath -Force
        throw "JSON validation failed"
    }
    
    # Save fixed JSON
    Write-Host ""
    Write-Host "Saving fixed JSON..." -ForegroundColor Yellow
    $jsonText | Set-Content $jsonPath -Encoding UTF8
    
    Write-Host "✓ Phase 2 JSON fixed successfully!" -ForegroundColor Green
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
} finally {
    Pop-Location
}