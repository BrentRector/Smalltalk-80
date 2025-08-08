# Move .md files to docs folder except CLAUDE.md and README.md
$rootPath = "D:\Users\brent\OneDrive\Src\Smalltalk-80"
$docsPath = Join-Path $rootPath "docs"

# Create docs folder if it doesn't exist
if (!(Test-Path $docsPath)) {
    New-Item -ItemType Directory -Path $docsPath | Out-Null
}

# Get all .md files in root directory (not subdirectories)
Get-ChildItem -Path $rootPath -Filter "*.md" -File | 
    Where-Object { $_.Name -ne "CLAUDE.md" -and $_.Name -ne "README.md" } |
    ForEach-Object {
        Write-Host "Moving $($_.Name) to docs folder"
        Move-Item -Path $_.FullName -Destination $docsPath -Force
    }

Write-Host "Documentation files moved to docs folder successfully!"