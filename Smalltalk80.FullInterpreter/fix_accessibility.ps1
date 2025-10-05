#!/usr/bin/env pwsh

Write-Host "Fixing accessibility issues in Smalltalk80.Interpreter project..." -ForegroundColor Green

# Fix override methods that must be public
$filesToFix = @(
    "Core/SClass.cs",
    "Core/ProcessScheduler.cs",
    "Core/SLinkedList.cs",
    "Core/SMethodContext.cs",
    "Core/SProcess.cs",
    "Core/SBlockContext.cs",
    "Core/SArray.cs",
    "Memory/ArenaHandle.cs",
    "Memory/SnapshotWriter.cs"
)

foreach ($file in $filesToFix) {
    if (Test-Path $file) {
        Write-Host "Processing $file..."
        $content = Get-Content -Path $file -Raw
        
        # Fix ToString overrides
        $content = $content -replace 'internal override string ToString\(\)', 'public override string ToString()'
        
        # Fix Equals overrides
        $content = $content -replace 'internal override bool Equals\(object\?? obj\)', 'public override bool Equals(object? obj)'
        
        # Fix GetHashCode overrides
        $content = $content -replace 'internal override int GetHashCode\(\)', 'public override int GetHashCode()'
        
        # Fix operators that must be public
        $content = $content -replace 'internal static bool operator ==', 'public static bool operator =='
        $content = $content -replace 'internal static bool operator !=', 'public static bool operator !='
        
        Set-Content -Path $file -Value $content -NoNewline
        Write-Host "  Updated $file" -ForegroundColor Yellow
    }
}

# Fix enums in BytecodeDecoder that need to be internal
if (Test-Path "Execution/BytecodeDecoder.cs") {
    Write-Host "Processing Execution/BytecodeDecoder.cs..."
    $content = Get-Content -Path "Execution/BytecodeDecoder.cs" -Raw
    
    # Make enums internal
    $content = $content -replace 'enum BytecodeType', 'internal enum BytecodeType'
    $content = $content -replace 'enum StackOperation', 'internal enum StackOperation'
    $content = $content -replace 'enum ReturnOperation', 'internal enum ReturnOperation'
    $content = $content -replace 'enum SpecialConstant', 'internal enum SpecialConstant'
    $content = $content -replace 'enum ExtendedType', 'internal enum ExtendedType'
    
    Set-Content -Path "Execution/BytecodeDecoder.cs" -Value $content -NoNewline
    Write-Host "  Updated Execution/BytecodeDecoder.cs" -ForegroundColor Yellow
}

# Fix ClassFormat enum in ClassFormat.cs to be internal
if (Test-Path "Core/ClassFormat.cs") {
    Write-Host "Processing Core/ClassFormat.cs..."
    $content = Get-Content -Path "Core/ClassFormat.cs" -Raw
    
    $content = $content -replace 'public enum ClassFormat', 'internal enum ClassFormat'
    
    Set-Content -Path "Core/ClassFormat.cs" -Value $content -NoNewline
    Write-Host "  Updated Core/ClassFormat.cs" -ForegroundColor Yellow
}

# Fix IArenaObject implementations
$arenaFiles = @("Memory/ArenaCompiledMethod.cs", "Memory/ArenaContext.cs")
foreach ($file in $arenaFiles) {
    if (Test-Path $file) {
        Write-Host "Processing $file..."
        $content = Get-Content -Path $file -Raw
        
        # Make Reset() method public
        $content = $content -replace 'internal void Reset\(\)', 'public void Reset()'
        
        # Initialize method needs to be public
        $content = $content -replace 'internal void Initialize\(ArenaHandle handle, Memory<byte> memory\)', 'public void Initialize(ArenaHandle handle, Memory<byte> memory)'
        
        Set-Content -Path $file -Value $content -NoNewline
        Write-Host "  Updated $file" -ForegroundColor Yellow
    }
}

# Fix Receiver property in SBlockContext
if (Test-Path "Core/SBlockContext.cs") {
    Write-Host "Processing Core/SBlockContext.cs..."
    $content = Get-Content -Path "Core/SBlockContext.cs" -Raw
    
    # Change Receiver property to match base class accessibility
    $content = $content -replace 'public override object\? Receiver', 'internal override object? Receiver'
    
    Set-Content -Path "Core/SBlockContext.cs" -Value $content -NoNewline
    Write-Host "  Updated Core/SBlockContext.cs" -ForegroundColor Yellow
}

# Fix IEqualityComparer in SnapshotWriter
if (Test-Path "Memory/SnapshotWriter.cs") {
    Write-Host "Processing Memory/SnapshotWriter.cs..."
    $content = Get-Content -Path "Memory/SnapshotWriter.cs" -Raw
    
    # Make interface methods public
    $content = $content -replace 'internal bool Equals\(object\? x, object\? y\)', 'public bool Equals(object? x, object? y)'
    $content = $content -replace 'internal int GetHashCode\(object obj\)', 'public int GetHashCode(object obj)'
    
    # Fix Dispose methods
    $content = $content -replace 'internal void Dispose\(\)', 'public void Dispose()'
    
    Set-Content -Path "Memory/SnapshotWriter.cs" -Value $content -NoNewline
    Write-Host "  Updated Memory/SnapshotWriter.cs" -ForegroundColor Yellow
}

# Fix Equals method for ArenaHandle structs
if (Test-Path "Memory/ArenaHandle.cs") {
    Write-Host "Processing Memory/ArenaHandle.cs for IEquatable interface..."
    $content = Get-Content -Path "Memory/ArenaHandle.cs" -Raw
    
    # Make IEquatable.Equals public
    $content = $content -replace 'internal bool Equals\(ArenaHandle other\)', 'public bool Equals(ArenaHandle other)'
    $content = $content -replace 'internal bool Equals\(ArenaHandle<T> other\)', 'public bool Equals(ArenaHandle<T> other)'
    
    Set-Content -Path "Memory/ArenaHandle.cs" -Value $content -NoNewline
    Write-Host "  Updated Memory/ArenaHandle.cs" -ForegroundColor Yellow
}

Write-Host "`nAccessibility fixes completed!" -ForegroundColor Green