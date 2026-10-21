param(
  [Parameter(Mandatory = $true)][string]$In,     # a png file or a folder of png files
  [Parameter(Mandatory = $true)][string]$Out,    # the sheet png to write
  [int]$Cols = 8,                                # pictures per row
  [int]$Cell = 64,                               # the cell size in the sheet
  [string]$Label = ""                            # text under each cell (the file name)
)
# Puts many small pictures on one sheet so a human can look at them all at
# once. Use it after findsize.ps1 gives you a list of candidate nodes:
# export them with uipng, then make a sheet of the folder.
#
#   sheet.ps1 -In logs/wz/icons -Out logs/wz/icons_sheet.png -Cols 10
#
# The label under a cell is the file name, so keep the file names short.

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (Test-Path $In -PathType Leaf) { $files = @(Get-Item $In) }
else { $files = @(Get-ChildItem $In -Filter *.png | Sort-Object Name) }
if ($files.Count -eq 0) { throw "no png files in $In" }

$cell = $Cell
$labelH = 14
$rows = [math]::Ceiling($files.Count / $Cols)
$sheet = New-Object System.Drawing.Bitmap ($Cols * $cell), ($rows * ($cell + $labelH))
$g = [System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.Color]::FromArgb(255, 30, 30, 30))
$g.InterpolationMode = "NearestNeighbor"
$font = New-Object System.Drawing.Font("Consolas", 8)
$brush = [System.Drawing.Brushes]::White
$border = [System.Drawing.Pens]::DimGray

$i = 0
foreach ($f in $files) {
  $x = ($i % $Cols) * $cell
  $y = [math]::Floor($i / $Cols) * ($cell + $labelH)
  $bmp = [System.Drawing.Bitmap]::FromFile($f.FullName)
  # keep the art the same size, only scale it up when it is tiny
  $scale = [math]::Min(($cell - 8) / $bmp.Width, ($cell - 8) / $bmp.Height)
  if ($scale -gt 1) { $scale = [math]::Floor($scale) }
  $w = [int]($bmp.Width * $scale); $h = [int]($bmp.Height * $scale)
  $g.DrawRectangle($border, $x + 2, $y + 2, $cell - 4, $cell - 4)
  $g.DrawImage($bmp, $x + [int](($cell - $w) / 2), $y + [int](($cell - $h) / 2), $w, $h)
  $bmp.Dispose()
  $text = if ($Label) { $Label } else { $f.BaseName }
  if ($text.Length -gt 12) { $text = $text.Substring(0, 12) }
  $g.DrawString($text, $font, $brush, $x + 3, $y + $cell)
  $i++
}
$g.Dispose()
$font.Dispose()
$sheet.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$sheet.Dispose()
Write-Host "$Out : $($files.Count) pictures"
