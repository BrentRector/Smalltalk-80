<#
CreateRepoManifest.ps1
Generates:
  - repo.manifest.json: structured inventory of the repository
  - chunk_XX.txt files: chunked, labeled code blocks for paste-based analysis

Compatible with:
  - Windows PowerShell 5.1
  - PowerShell 7.x

Avoids:
  - ternary operator
  - inline-if expressions
  - backtick-newline parsing issues
#>

[CmdletBinding()]
param(
  [string]$RepoName = (Split-Path -Leaf (Get-Location)),
  [string]$Commit   = $(try { git rev-parse HEAD 2>$null } catch { "" }),
  [string]$Branch   = $(try { git rev-parse --abbrev-ref HEAD 2>$null } catch { "" }),
  [int]$MaxChunkChars = 100000,
  [int]$TopNLargest   = 20
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$NL = [Environment]::NewLine

function Normalize-Path([string]$Path) { ($Path -replace '\\','/') }

function Get-SHA256Hex([string]$Path) {
  $sha = [System.Security.Cryptography.SHA256]::Create()
  $fs = [System.IO.File]::Open(
    $Path,
    [System.IO.FileMode]::Open,
    [System.IO.FileAccess]::Read,
    [System.IO.FileShare]::ReadWrite
  )
  try { $hash = $sha.ComputeHash($fs) } finally { $fs.Dispose(); $sha.Dispose() }
  ($hash | ForEach-Object { $_.ToString('x2') }) -join ''
}

function Get-FileText([string]$Path) {
  [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
}

function Get-LineCountFromText([string]$Text) {
  if ([string]::IsNullOrEmpty($Text)) { return 0 }
  return ($Text -split "`n").Count
}

function Get-RepositoryFiles {
  $exts = @('*.cs','*.csproj','*.sln','*.json','*.props','*.targets')
  $files = foreach ($pattern in $exts) {
    Get-ChildItem -Recurse -File -Filter $pattern -ErrorAction SilentlyContinue |
      Where-Object { $_.FullName -notmatch '[\\\/]\.git([\\\/]|$)' }
  }
  $files | Sort-Object FullName -Unique
}

function Get-FileInventory($Files) {
  $repoRoot = (Get-Location).Path
  foreach ($f in $Files) {
    $rel = Normalize-Path ($f.FullName.Replace($repoRoot, '').TrimStart('\','/'))
    $pathForManifest = if ($rel) { $rel } else { Normalize-Path $f.FullName }
    $text = Get-FileText $f.FullName
    [pscustomobject]@{
      path   = $pathForManifest
      ext    = [System.IO.Path]::GetExtension($f.FullName)
      size   = $f.Length
      lines  = Get-LineCountFromText $text
      sha256 = Get-SHA256Hex $f.FullName
    }
  }
}

function Parse-SolutionProjects([string]$SlnPath) {
  $content = Get-Content -Raw -LiteralPath $SlnPath
  ($content -split "`r?`n" | Where-Object { $_ -match '^\s*Project\(' } |
    ForEach-Object {
      if ($_ -match '=\s*"[^"]*"\s*,\s*"([^"]+)"') { $Matches[1] -replace '\\','/' }
    }) | Sort-Object -Unique
}

function Get-SolutionsInfo($SolutionFiles) {
  $repoRoot = (Get-Location).Path
  foreach ($sln in $SolutionFiles) {
    $rel = Normalize-Path ($sln.FullName.Replace($repoRoot, '').TrimStart('\','/'))
    [pscustomobject]@{
      path     = if ($rel) { $rel } else { Normalize-Path $sln.FullName }
      projects = Parse-SolutionProjects $sln.FullName
    }
  }
}

function Get-ProjectsInfo($ProjectFiles) {
  $repoRoot = (Get-Location).Path
  foreach ($proj in $ProjectFiles) {
    $rel = Normalize-Path ($proj.FullName.Replace($repoRoot, '').TrimStart('\','/'))
    $pathOut = if ($rel) { $rel } else { Normalize-Path $proj.FullName }
    $nameOut = [System.IO.Path]::GetFileNameWithoutExtension($proj.Name)
    $tfm = @(); $lang = $null; $nullable = $null
    $pkgRefs = @(); $projRefs = @(); $analyzers = @()
    $compileCount = 0; $resourceCount = 0
    try {
      [xml]$xml = Get-Content -Raw -LiteralPath $proj.FullName
      foreach ($node in @($xml.Project.PropertyGroup)) {
        if ($node.TargetFramework)  { $tfm += $node.TargetFramework }
        if ($node.TargetFrameworks) { $tfm += ($node.TargetFrameworks -split ';') }
        if (-not $lang -and $node.LangVersion) { $lang = $node.LangVersion }
        if (-not $nullable -and $node.Nullable) { $nullable = $node.Nullable }
      }
      $tfm = $tfm | Where-Object { $_ } | Select-Object -Unique
      foreach ($ig in @($xml.Project.ItemGroup)) {
        foreach ($pkg in @($ig.PackageReference)) {
          if ($pkg) { $pkgRefs += [pscustomobject]@{ id=$pkg.Include; version=$pkg.Version } }
        }
        foreach ($pr in @($ig.ProjectReference)) {
          if ($pr.Include) { $projRefs += (Normalize-Path $pr.Include) }
        }
        foreach ($an in @($ig.Analyzer)) {
          if ($an.Include) { $analyzers += (Normalize-Path $an.Include) }
        }
        $resourceCount += (@($ig.EmbeddedResource) | Measure-Object).Count
      }
      $compileCount = (Get-ChildItem -Recurse -Path (Split-Path $proj.FullName) -Filter *.cs -File | Measure-Object).Count
    } catch {}
    [pscustomobject]@{
      name              = $nameOut
      path              = $pathOut
      targetFrameworks  = $tfm
      langVersion       = $lang
      nullable          = $nullable
      packageReferences = $pkgRefs
      projectReferences = $projRefs
      analyzers         = $analyzers
      compileItems      = $compileCount
      resources         = $resourceCount
    }
  }
}

function Get-Stats($Inventory) {
  $byExt = @{}
  foreach ($item in $Inventory) {
    $ext = $item.ext; if (-not $ext) { $ext = "" }
    if (-not $byExt.ContainsKey($ext)) { $byExt[$ext] = 0 }
    $byExt[$ext] += 1
  }
  [pscustomobject]@{
    totalFiles   = $Inventory.Count
    byExtension  = $byExt
    largestFiles = $Inventory | Sort-Object size -Descending | Select-Object -First $TopNLargest
  }
}

function Write-Json($Object, $Path) {
  Set-Content -LiteralPath $Path -Value ($Object | ConvertTo-Json -Depth 20) -Encoding utf8
}

function Get-LanguageTag([string]$Ext) {
  if (-not $Ext) { return '' }
  switch ($Ext.ToLowerInvariant()) {
    '.cs'      { 'csharp' }
    '.csproj'  { 'xml' }
    '.sln'     { '' }
    '.json'    { 'json' }
    '.props'   { 'xml' }
    '.targets' { 'xml' }
    default    { '' }
  }
}

function Build-FileBlock($FileMeta) {
  $path  = $FileMeta.path
  $sha   = $FileMeta.sha256
  $lines = $FileMeta.lines
  $lang  = Get-LanguageTag $FileMeta.ext
  $absPath = if ([System.IO.Path]::IsPathRooted($path)) { $path } else { Join-Path (Get-Location).Path $path }
  $fileText = Get-FileText $absPath
  $header = "FILE path={0} sha256={1} lines={2}" -f $path, $sha, $lines
  $codeHere = @"
```$lang
// file: $path
$fileText
```
"@
  $header + $NL + "---" + $NL + $codeHere
}

function Build-Chunks($Inventory, $MaxChars) {
  $ordered = $Inventory | Sort-Object path
  $chunks = @()
  $current = ""
  foreach ($item in $ordered) {
    $block = Build-FileBlock $item

    if ($block.Length -ge $MaxChars) {
      # Flush current if any
      if ($current.Length -gt 0) {
        $chunks += $current.TrimEnd()
        $current = ""
      }
      # Split oversized block into slices
      $start = 0
      while ($start -lt $block.Length) {
        $len = [Math]::Min($MaxChars, $block.Length - $start)
        $chunks += $block.Substring($start, $len)
        $start += $len
      }
      continue
    }

    if (($current.Length + $block.Length + 2) -gt $MaxChars) {
      $chunks += $current.TrimEnd()
      $current = ""
    }

    if ($current.Length -gt 0) { $current += ($NL + $NL) }
    $current += $block
  }

  if ($current.Length -gt 0) {
    $chunks += $current.TrimEnd()
  }

  return ,$chunks
}

function Write-Chunks($Chunks) {
  $total = $Chunks.Count
  for ($i = 0; $i -lt $total; $i++) {
    $idx = $i + 1
    $name = "chunk_{0:D2}.txt" -f $idx
    $prefix = "CHUNK {0}/{1} repo={2} branch={3} commit={4}" -f $idx, $total, $RepoName, $Branch, $Commit
    $content = $prefix + $NL + "---" + $NL + $Chunks[$i]
    Set-Content -LiteralPath $name -Value $content -Encoding utf8
  }
}

# ----- main -----
$files = Get-RepositoryFiles
$inventory = Get-FileInventory $files
$solutionFiles = $files | Where-Object { $_.Extension -eq '.sln' }
$projectFiles  = $files | Where-Object { $_.Extension -eq '.csproj' }

$manifest = [pscustomobject]@{
  repo        = $RepoName
  commit      = $Commit
  branch      = $Branch
  generatedAt = (Get-Date).ToString('o')
  stats       = Get-Stats $inventory
  solutions   = @(Get-SolutionsInfo $solutionFiles)
  projects    = @(Get-ProjectsInfo  $projectFiles)
  files       = $inventory
}

Write-Json $manifest "repo.manifest.json"

$chunks = Build-Chunks $inventory $MaxChunkChars
Write-Chunks $chunks

Write-Host ("Done. Files: {0}, Chunks: {1}" -f $inventory.Count, $chunks.Count)
