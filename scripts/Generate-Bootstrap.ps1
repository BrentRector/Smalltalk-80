# Generate-Bootstrap.ps1
# Generates C# bootstrap code from Bootstrap.st

param(
    [string]$ProjectRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

Write-Host "Bootstrap Generator Script" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan

# Paths
$bootstrapSt = Join-Path $ProjectRoot "Bootstrap.st"
$generatedCs = Join-Path $ProjectRoot "Smalltalk80\GeneratedBootstrap.cs"
$generatorCs = Join-Path $ProjectRoot "Smalltalk80\BootstrapGenerator.cs"

# Check if Bootstrap.st exists
if (-not (Test-Path $bootstrapSt)) {
    Write-Error "Bootstrap.st not found at: $bootstrapSt"
    exit 1
}

# Check if we need to regenerate (Bootstrap.st is newer than GeneratedBootstrap.cs)
$regenerate = $false
if (-not (Test-Path $generatedCs)) {
    Write-Host "GeneratedBootstrap.cs does not exist, will generate" -ForegroundColor Yellow
    $regenerate = $true
} else {
    $bootstrapTime = (Get-Item $bootstrapSt).LastWriteTime
    $generatedTime = (Get-Item $generatedCs).LastWriteTime
    
    if ($bootstrapTime -gt $generatedTime) {
        Write-Host "Bootstrap.st is newer than GeneratedBootstrap.cs, will regenerate" -ForegroundColor Yellow
        $regenerate = $true
    } else {
        Write-Host "GeneratedBootstrap.cs is up to date" -ForegroundColor Green
    }
}

if ($regenerate) {
    Write-Host "Building BootstrapGenerator tool..." -ForegroundColor Cyan
    
    # Build the entire project first to ensure all dependencies are available
    dotnet build --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build project"
        exit 1
    }
    
    # Run the generator using dotnet run
    Write-Host "Running BootstrapGenerator..." -ForegroundColor Cyan
    Push-Location (Join-Path $ProjectRoot "Smalltalk80")
    try {
        # Create a temporary project file for the generator
        $tempProject = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <StartupObject>Smalltalk80.BootstrapGenerator</StartupObject>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="BootstrapGenerator.cs" />
    <Compile Include="**\*.cs" Exclude="Program.cs;BootstrapGenerator.cs;GeneratedBootstrap.cs" />
  </ItemGroup>
</Project>
"@
        $tempProjectFile = "BootstrapGenerator.csproj"
        $tempProject | Out-File -FilePath $tempProjectFile -Encoding UTF8
        
        dotnet run --project $tempProjectFile -- "..\Bootstrap.st" "GeneratedBootstrap.cs"
        $exitCode = $LASTEXITCODE
        
        # Clean up temp project
        Remove-Item $tempProjectFile -ErrorAction SilentlyContinue
        
        if ($exitCode -ne 0) {
            Write-Error "BootstrapGenerator failed with exit code $exitCode"
            exit 1
        }
    }
    finally {
        Pop-Location
    }
    
    Write-Host "Successfully generated GeneratedBootstrap.cs" -ForegroundColor Green
} else {
    Write-Host "Skipping generation - GeneratedBootstrap.cs is up to date" -ForegroundColor Green
}

Write-Host "Bootstrap generation complete!" -ForegroundColor Green