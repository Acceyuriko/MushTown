# MushTown crop helper. The client draws at 1366x768 and blits that picture up
# to the window, so every number in layouts/*.json and in the code is in game
# pixels while a --shot file holds the blitted size (3840x2160 here).
#
#   crop.ps1 -Mode crop -A <image> -Out <out.png> -N gx0,gy0,gx1,gy1   cut by GAME pixels
#   crop.ps1 -Mode box  -A <image> -Out <out.png> -N x0,y0,x1,y1      cut by FILE pixels
#   crop.ps1 -Mode diff -A <a.png> -B <b.png> [-Out <out.png>]      changed-pixel box
#
# crop and box take the top-left corner and the bottom-right corner, and the
# bottom-right corner is not part of the cut.
# diff prints the box of the pixels that differ (in file px), and -Out saves that
# region of picture A. It samples every 2nd pixel by default: a change on an odd
# row or column is invisible then, so use -Step 1 (about 90 s for a 4K pair).

param(
    [Parameter(Mandatory = $true)][string]$Mode,
    [Parameter(Mandatory = $true)][string]$A,
    [string]$B,
    [string]$Out,
    [object]$N,
    [int]$Step = 2
)

Add-Type -AssemblyName System.Drawing

$GameW = 1366
$GameH = 768
$Step = [Math]::Max(1, $Step)
$nums = @()
if ($N) {
    # a prompt call gives an array, a "pwsh -File" call gives one string
    $parts = if ($N -is [string]) { $N -split "," } else { $N }
    $nums = @($parts | ForEach-Object { [int]($_.ToString().Trim()) })
}

function Save-Png($bmp, [string]$path) {
    $dir = Split-Path -Parent $path
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

if ($Mode -eq "crop" -or $Mode -eq "box") {
    if (-not $Out -or $nums.Count -lt 4) {
        if ($Mode -eq "crop") { throw "usage: crop.ps1 -Mode crop -A <image> -Out <out.png> -N gx0,gy0,gx1,gy1  (game pixels)" }
        throw "usage: crop.ps1 -Mode box -A <image> -Out <out.png> -N x0,y0,x1,y1  (file pixels)"
    }
    $bmp = [System.Drawing.Bitmap]::FromFile((Resolve-Path $A -ErrorAction Stop).Path)
    if ($Mode -eq "crop") {
        # the same math as SceneMath.BlitRect in MushTown.Shared/Core/SceneMath.cs
        # the casts matter: PowerShell's [int] rounds and its "/" gives an Int32
        # when the division is exact, which would pick the wrong [Math]::Min overload
        $scale = [Math]::Min([double]($bmp.Width / $GameW), [double]($bmp.Height / $GameH))
        $vw = [int][Math]::Truncate($GameW * $scale)
        $vh = [int][Math]::Truncate($GameH * $scale)
        $x = [int][Math]::Round($nums[0] * $vw / $GameW + [Math]::Floor(($bmp.Width - $vw) / 2))
        $y = [int][Math]::Round($nums[1] * $vh / $GameH + [Math]::Floor(($bmp.Height - $vh) / 2))
        $w = [int][Math]::Round(($nums[2] - $nums[0]) * $vw / $GameW)
        $h = [int][Math]::Round(($nums[3] - $nums[1]) * $vh / $GameH)
    } else {
        $x = $nums[0]; $y = $nums[1]
        $w = $nums[2] - $nums[0]; $h = $nums[3] - $nums[1]
    }
    if ($w -le 0 -or $h -le 0) {
        $bmp.Dispose()
        throw ("the box is empty: the far corner must be bigger than the near one ({0},{1}) to ({2},{3})" -f $nums[0], $nums[1], $nums[2], $nums[3])
    }
    # clip the box to the picture
    $left = [Math]::Max(0, $x)
    $top = [Math]::Max(0, $y)
    $right = [Math]::Min($bmp.Width, $x + $w)
    $bottom = [Math]::Min($bmp.Height, $y + $h)
    if ($right -le $left -or $bottom -le $top) {
        $bmp.Dispose()
        throw "the box is outside the picture"
    }
    if ($left -ne $x -or $top -ne $y -or $right -ne ($x + $w) -or $bottom -ne ($y + $h)) { Write-Output "the box was clipped to the picture" }
    $x = $left; $y = $top; $w = $right - $left; $h = $bottom - $top
    Write-Output ("file rect ({0},{1}) {2}x{3} of {4}x{5}" -f $x, $y, $w, $h, $bmp.Width, $bmp.Height)
    $rect = New-Object System.Drawing.Rectangle $x, $y, $w, $h
    Save-Png ($bmp.Clone($rect, $bmp.PixelFormat)) $Out
    $bmp.Dispose()
    return
}

if ($Mode -eq "diff") {
    if (-not $B) { throw "usage: crop.ps1 -Mode diff -A <a.png> -B <b.png> [-Out <out.png>]" }
    $imgA = [System.Drawing.Bitmap]::FromFile((Resolve-Path $A -ErrorAction Stop).Path)
    $imgB = [System.Drawing.Bitmap]::FromFile((Resolve-Path $B -ErrorAction Stop).Path)
    if ($imgA.Width -ne $imgB.Width -or $imgA.Height -ne $imgB.Height) {
        $imgA.Dispose(); $imgB.Dispose()
        throw "the two pictures are not the same size"
    }
    $minX = $imgA.Width; $minY = $imgA.Height; $maxX = -1; $maxY = -1; $count = 0
    for ($y = 0; $y -lt $imgA.Height; $y += $Step) {
        for ($x = 0; $x -lt $imgA.Width; $x += $Step) {
            if ($imgA.GetPixel($x, $y).ToArgb() -ne $imgB.GetPixel($x, $y).ToArgb()) {
                $count++
                if ($x -lt $minX) { $minX = $x }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    $imgA.Dispose(); $imgB.Dispose()
    if ($maxX -lt 0) {
        Write-Output ("no differences in the sampled pixels (step {0}) - run it again with -Step 1 to be sure" -f $Step)
        return
    }
    Write-Output ("changed pixels (step {0}): {1}" -f $Step, $count)
    Write-Output ("bbox (file px): ({0},{1}) - ({2},{3})  size {4}x{5}" -f $minX, $minY, $maxX, $maxY, ($maxX - $minX + 1), ($maxY - $minY + 1))
    if ($Out) {
        $bmp = [System.Drawing.Bitmap]::FromFile((Resolve-Path $A -ErrorAction Stop).Path)
        $rect = New-Object System.Drawing.Rectangle $minX, $minY, ($maxX - $minX + 1), ($maxY - $minY + 1)
        Save-Png ($bmp.Clone($rect, $bmp.PixelFormat)) $Out
        $bmp.Dispose()
    }
    return
}

throw "unknown mode: $Mode"
