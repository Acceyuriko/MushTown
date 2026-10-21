param(
  [string]$Category = "UI",
  [string]$Size = "",          # "13x13", "13x*", "*x21" - the size of the png to find
  [string]$Name = "",          # optional: only paths that contain this text
  [int]$Depth = 5,             # how deep to walk the category tree
  [string]$Out = ""            # optional: also write the matches to this file
)
# Finds png nodes in a WZ category by their pixel size, and prints the path
# of every hit. Use it when you know what a piece of art looks like (its
# size) but not its node name.
#
#   findsize.ps1 -Category UI -Size 13x13
#   findsize.ps1 -Category UI -Size "*x21" -Name Bt
#
# The printed path is the one to give to "uipng <Category> <path> <out.png>".
# The dump of the whole category takes a while on a big category (UI is
# about 6 seconds, 280 000 lines), so the tree is dumped once and then
# searched in memory.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$dump = Join-Path $env:TEMP ("wz_" + $Category + "_" + $Depth + ".txt")

Write-Host "dumping $Category (depth $Depth) ..." -ForegroundColor DarkGray
$lines = & dotnet run --project (Join-Path $root "MushTown.Exporter") -- catdump $Category "" $Depth 2>&1
if (-not $lines) { throw "catdump failed" }
$lines | Set-Content -Path $dump -Encoding UTF8

$stack = @()
$hits = New-Object System.Collections.Generic.List[string]
foreach ($line in $lines) {
  if ($line -notmatch '^(\s*)([^=]+?) = ') { continue }
  $indent = $Matches[1].Length
  $nodeName = $Matches[2].Trim()
  $level = [int]($indent / 2)
  $stack = $stack[0..([Math]::Max(0, $level - 1))]
  $stack += $nodeName
  if ($line -notmatch 'png (\d+)x(\d+)') { continue }
  $w = $Matches[1]; $h = $Matches[2]
  if ($Size) {
    $parts = $Size -split 'x'
    $wantW = $parts[0]; $wantH = $parts[1]
    if ($wantW -ne "*" -and $wantW -ne $w) { continue }
    if ($wantH -ne "*" -and $wantH -ne $h) { continue }
  }
  # the path the tools want: drop the .wz file and the _Canvas folder
  $path = @($stack[1..($stack.Count - 1)])
  $path = @($path | Where-Object { $_ -ne "_Canvas" })
  $path = $path -join "/"
  if ($Name -and $path -notlike "*$Name*") { continue }
  $hits.Add($path)
}

Write-Host ("found " + $hits.Count + " png nodes") -ForegroundColor Green
$hits | ForEach-Object { Write-Host $_ }
if ($Out) { $hits | Set-Content -Path $Out -Encoding UTF8 }
Write-Host "dump kept at $dump" -ForegroundColor DarkGray
