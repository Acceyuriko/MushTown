---
name: crop
description: Crop MushTown screenshots correctly. Use when you need a close-up of a part of a --shot picture or a live screen grab (a character, a UI panel, a text label), or when you need to find which pixels changed between two pictures.
---

# Cropping MushTown pictures

The client draws at 1366x768 and blows the picture up to the window. The positions in
`layouts/*.json` and the HUD are in **game pixels**. A `--shot` file holds the blown-up
size (3840x2160 on this screen). Most wrong crops come from mixing these two.

## Three spaces. Name the one you are using.

| space | size | where it comes from |
|---|---|---|
| game px | 1366x768 | the layout json, the HUD. World coordinates (where the character stands) are not game px, the camera moves |
| file px | the png's own size (3840x2160 here) | the `--shot` png |
| preview px | whatever the image tool shows me (it prints the size) | only for looking, never for cutting |

game px -> file px, same math as `SceneMath.BlitRect` in `MushTown.Shared/Core/SceneMath.cs`:

```
scale = min(fileW / 1366, fileH / 768)
vw = trunc(1366 * scale)      # the blit width
vh = trunc(768 * scale)       # the blit height
X = (fileW - vw) \ 2          # the blit is centred
Y = (fileH - vh) \ 2
file_x = round(game_x * vw / 1366 + X)
file_y = round(game_y * vh / 768  + Y)
```

The blit size (vw, vh) and the centring (X, Y) are truncated, the cut corners are
rounded to the nearest pixel. In PowerShell `[int]` rounds (2158.946 becomes 2159) and
`/` gives an Int32 when the division is exact. For a 3840x2160 file: scale 2.81113,
vw 3840, vh 2158, X 0, Y 1 (one empty pixel row at the top and one at the bottom).
file px -> game px: the same formula backwards.

preview px -> file px: multiply by `file_width / preview_width`. The tool prints a
rounded factor ("multiply coordinates by 1.41"); 3840/2730 is 1.40659, and using the
printed factor is about 9 px of error at the right edge of a 4K file. Compute it from
the printed sizes.

## Rules

1. **Never read a cut box off the preview.** The preview is scaled and I read the
   scale wrong every time. If the only source is the preview, multiply by the factor
   you computed from the printed sizes and treat the result as file px.
2. **Do not mix spaces.** Write down which space the numbers are in before cutting.
   Every number the helper prints is in file px.
3. **Find the region with numbers, not with eyes.**
   - `crop.ps1 -Mode diff -A a.png -B b.png` prints the box of the pixels that
     changed, in file px. Turn it into game px with the formula above.
   - the layout json gives the numbers, but read the file's own `_help` line first:
     - `setup-layout.json`: `panel` (960,220, the board top-left) and `avatar`
       (515,420, the doll's feet) are screen spots; every other position is an offset
       inside the board, so the screen spot is `panel` + offset.
     - `menu-layout.json`: the board is drawn at `scale` (0.7); in X around the screen
       centre, in Y around the panel top (`MSX`/`MSY` in `Menu.cs`).
       `textMargin`/`textGap`/`textDown`/`cancelDown` are multiplied by that scale too.
   - a known game position.
4. **Cut with the helper** and read the rect it prints.
5. **Look at the cut** (`read_image` on the new file) and check it shows the thing you
   wanted. If it shows something else, the box is wrong - fix the box, do not re-guess
   from the preview.
6. **Live screen grabs are physical pixels.** A DPI-unaware process sees the screen as
   1920x1080 and grabs only the top-left quarter. Make the process DPI-aware
   (`SetProcessDPIAware`) or grab 3840x2160, then check the png really is that size.
   After the fullscreen client has the focus, `CopyFromScreen` can fail with
   "invalid handle": take stills with `--shot`, or grab before clicking the client.

## Helper

`.agents/skills/crop/crop.ps1` (PowerShell, no packages). `crop` and `box` take the
top-left corner and the bottom-right corner; the bottom-right corner is not part of
the cut.

```sh
# cut by game pixels
& .\.agents\skills\crop\crop.ps1 -Mode crop -A logs/shot.png -Out logs/cut.png -N 440,300,600,560

# cut by file pixels
& .\.agents\skills\crop\crop.ps1 -Mode box  -A logs/shot.png -Out logs/cut.png -N 1280,950,1580,1350

# what changed between two pictures; -Out saves that region of picture A
& .\.agents\skills\crop\crop.ps1 -Mode diff -A logs/a.png -B logs/b.png -Out logs/changed_area.png
```

`diff` samples every 2nd pixel (`GetPixel` on a 4K picture is slow): about 20 s for a
4K pair, about 90 s with `-Step 1`. A change on an odd row or column is invisible at
the default step, so use `-Step 1` when the answer matters.
