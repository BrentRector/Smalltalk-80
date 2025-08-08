# PowerShell script to move unnecessary .cs files to temp/old_cs_files
# These files are not referenced in any project and appear to be development artifacts

$rootPath = "D:\Users\brent\OneDrive\Src\Smalltalk-80"
$tempPath = Join-Path $rootPath "temp\old_cs_files"

# Create destination folder if it doesn't exist
if (!(Test-Path $tempPath)) {
    New-Item -ItemType Directory -Path $tempPath | Out-Null
    Write-Host "Created directory: $tempPath"
}

# List of test/experimental files that are safe to move
$testFiles = @(
    "ParserTest.cs",
    "test_constructor.cs",
    "test_debug_token.cs",
    "test_metaclass_system.cs",
    "test_parser.cs",
    "test_visitor_debug.cs",
    "TestMinimalBootstrap.cs",
    "TestStandardLibraryLoad.cs",
    "TestSystemImageLoad.cs",
    "TestSmalltalkLoaderProgram.cs"
)

# List of utility files that might be worth keeping for reference
$utilityFiles = @(
    "ExtractHeadlessClasses.cs",
    "GenerateBootstrapTool.cs",
    "RunBootstrapGenerator.cs",
    "SystemImageLoader.cs",
    "InspectMetaclass.cs",
    "MetaclassSystemFix.cs"
)

Write-Host "`nMoving test/experimental .cs files to temp folder..."
foreach ($file in $testFiles) {
    $sourcePath = Join-Path $rootPath $file
    if (Test-Path $sourcePath) {
        Move-Item -Path $sourcePath -Destination $tempPath -Force
        Write-Host "  Moved: $file"
    }
}

Write-Host "`nMoving utility .cs files to temp folder..."
foreach ($file in $utilityFiles) {
    $sourcePath = Join-Path $rootPath $file
    if (Test-Path $sourcePath) {
        Move-Item -Path $sourcePath -Destination $tempPath -Force
        Write-Host "  Moved: $file"
    }
}

Write-Host "`nCleanup complete! All identified .cs files have been moved to:"
Write-Host "  $tempPath"
Write-Host "`nYou can review these files and delete the folder if they're not needed."