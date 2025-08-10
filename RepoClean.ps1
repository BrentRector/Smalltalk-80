[CmdletBinding(SupportsShouldProcess)]
param()

# 1. Remove 0‑byte C# stubs
Get-ChildItem -Path . -Filter *.cs -File -Recurse |
  Where-Object { $_.Length -eq 0 } |
  ForEach-Object {
    if ($PSCmdlet.ShouldProcess($_.FullName, "Remove 0-byte stub")) {
      Remove-Item -LiteralPath $_.FullName -Force
    }
  }

# 2. Archive old bootstrap JSON variants (manual keep list here)
$archiveDir = Join-Path (Get-Location) "archive_bootstrap"
$toArchive = @(
  "Bootstrap/bootstrap_phase2_original.json",
  "Bootstrap/bootstrap_phase2_5.json"
)

if (-not (Test-Path $archiveDir)) { New-Item -ItemType Directory -Path $archiveDir | Out-Null }

foreach ($relPath in $toArchive) {
  if (Test-Path $relPath) {
    if ($PSCmdlet.ShouldProcess($relPath, "Move to archive")) {
      Move-Item -LiteralPath $relPath -Destination $archiveDir -Force
    }
  }
}
