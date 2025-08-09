# Extract all Phase 1/2 classes
$classes = @(
    'Object',
    'UndefinedObject', 
    'Boolean', 
    'True', 
    'False', 
    'SmallInteger',
    'Behavior', 
    'Class', 
    'Metaclass',
    'Magnitude', 
    'Number', 
    'Integer',
    'Collection', 
    'SequenceableCollection', 
    'ArrayedCollection',
    'Array', 
    'String', 
    'Symbol', 
    'Character', 
    'Association'
)

Write-Host "Extracting $($classes.Count) classes..." -ForegroundColor Cyan

foreach ($class in $classes) {
    Write-Host "`nExtracting $class..." -ForegroundColor Yellow
    $result = & ".\bin\Debug\net9.0\Smalltalk80.ExtractClass.exe" -o Bootstrap\phase3_extracted $class 2>&1
    
    # Show summary
    $result | Where-Object { $_ -match "(Instance methods:|Class methods:)" } | ForEach-Object {
        Write-Host "  $_" -ForegroundColor Green
    }
}

Write-Host "`nDone! Files saved to Bootstrap\phase3_extracted\" -ForegroundColor Cyan