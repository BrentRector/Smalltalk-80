# Build a precise line index for all classes in the system image

param(
    [string]$SystemImage = "..\Smalltalk80\Smalltalk-80 Source\Smalltalk-80.st",
    [string]$OutputFile = "..\CLASS_LINE_INDEX.txt"
)

$content = Get-Content $SystemImage
$index = @()

Write-Host "Building class line index from system image..."

for ($i = 0; $i -lt $content.Length; $i++) {
    $line = $content[$i]
    $lineNum = $i + 1
    
    # Match class definitions
    if ($line -match '^(\w+)\s+(subclass:|variableSubclass:|variableByteSubclass:|variableWordSubclass:)\s+#(\w+)') {
        $superclass = $matches[1]
        $classType = $matches[2]
        $className = $matches[3]
        
        # Find the end of class definition (!)
        $endLine = $i + 1
        while ($endLine -lt $content.Length -and $content[$endLine] -ne "!") {
            $endLine++
        }
        $endLineNum = $endLine + 1
        
        $index += [PSCustomObject]@{
            ClassName = $className
            Superclass = $superclass
            ClassType = $classType
            StartLine = $lineNum
            EndLine = $endLineNum
            LineCount = $endLineNum - $lineNum + 1
        }
        
        Write-Host "Found $className at line $lineNum-$endLineNum"
    }
    
    # Match class comments
    if ($line -match '^(\w+)\s+comment:') {
        $className = $matches[1]
        
        # Find end of comment
        $endLine = $i + 1
        while ($endLine -lt $content.Length -and -not ($content[$endLine] -match "'!$")) {
            $endLine++
        }
        $endLineNum = $endLine + 1
        
        # Update existing class entry if found
        $classEntry = $index | Where-Object { $_.ClassName -eq $className } | Select-Object -First 1
        if ($classEntry) {
            $classEntry | Add-Member -NotePropertyName CommentStart -NotePropertyValue $lineNum -Force
            $classEntry | Add-Member -NotePropertyName CommentEnd -NotePropertyValue $endLineNum -Force
        }
    }
}

# Save index
$index | Sort-Object ClassName | Format-Table -AutoSize | Out-String | Set-Content $OutputFile

# Also save as CSV for easy parsing
$index | Sort-Object ClassName | Export-Csv -Path ($OutputFile -replace '\.txt$', '.csv') -NoTypeInformation

Write-Host "`nIndex saved to $OutputFile"
Write-Host "Found $($index.Count) classes"

# Display summary of key classes
$keyClasses = @("Collection", "SequenceableCollection", "ArrayedCollection", "Array", "String", "Symbol",
                "ByteArray", "Set", "Dictionary", "OrderedCollection", "Stream", "PositionableStream",
                "ReadStream", "WriteStream", "LookupKey", "Association", "Interval", "LinkedList",
                "Process", "Semaphore", "CompiledMethod", "BlockContext", "MethodContext")

Write-Host "`nKey classes for headless system:"
foreach ($className in $keyClasses) {
    $classInfo = $index | Where-Object { $_.ClassName -eq $className } | Select-Object -First 1
    if ($classInfo) {
        Write-Host ("  {0,-30} Lines {1,6}-{2,6}" -f $className, $classInfo.StartLine, $classInfo.EndLine)
    }
}