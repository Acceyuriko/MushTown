---
name: wz-find
description: Find game art and data in the MapleStory WZ files. Use when you need a UI piece, a button, a window frame, an icon, a map picture or a string that the game already has, but MushTown does not. Covers searching the WZ by name, by picture size, by looking at a contact sheet, and reading the official position of a part from the WZ origin.
---

# Finding resources in the WZ

The WZ files are the source of every official picture in MushTown. Before you say
"we do not have this", look in the WZ. Most of the time the art is there under a
name nobody would guess.

The WZ lives at `E:\game\maplestory\appdata\Data` (see `ExporterPaths.DataDir` in
`MushTown.Exporter/Core/WzRoot.cs`). It holds 16 category trees: `Base`,
`Character`, `Effect`, `Etc`, `Item`, `Map`, `Mob`, `Morph`, `Npc`, `Packs`,
`Quest`, `Reactor`, `Skill`, `Sound`, `String`, `TamingMob`, `UI`.

**Our WZ is a modern GMS WZ.** The modern client parts are in it: `UIContentsGuide.img`,
`UI/UIMap.img/MiniMap`, `UI/StatusBar3.img/chat`, `UI/UIWindow2.img/UtilDlgEx`.
Old names from old tutorials often lead nowhere; the modern names are the good ones.

## The commands

All of them are `dotnet run --project MushTown.Exporter -- <command>`.
Run from the repo root.

| command | what it does |
|---|---|
| `catdump <Category> <path> [depth]` | print the tree under a path. Depth 0 = the node only, 1 = its children, 2 = grandchildren. **Depth 3 is where the `origin` lines show up.** |
| `find <name> [category] [partial]` | print every node with that name and its full path. Add `partial` to match a part of the name. Always pass the category - without it the whole Base tree is walked (4+ minutes, pegs one core, and it locks the exporter exe). |
| `uipng <Category> <path> <out.png>` | save one canvas as a png. Prints the size and the origin: `logs/x.png: 9x16 origin=-10,-45`. |
| `cat <Category> <path>` | print a string node in full (a name, a hint, a quest text). |
| `dump [path] [depth]` | the same as catdump but on the Base tree. |
| `pick item <folder> <prefix>` | list the ids and names of the items in a folder. |
| `pick hair <out.png> <id,id,...>` | a paper-doll sheet of the given hairs. |
| `questscan <npcid> [...]` | the quests that start or end at those NPCs. |
| `panel <name> [OutDir]` | export one panel's art (see the list in `AGENTS.md`). |

## The rule that saves the most time: the origin is the position

Every UI picture node has an `origin` (a `vec x,y`). **That is the anchor of the
picture, so the picture is drawn at minus the origin, on both axes.**

```
origin = -10,-45   -> the art sits at (10, 45) inside the window
origin = -116,-4   -> the art sits at (116, 4)
origin = 1,-90     -> the art sits at (-1, 90)   (the x sign flips too!)
```

So the official spot of a part is never a guess: read the origin and negate it.
The same rule holds for map art and for character frames.

Read the origin with depth 3:

```sh
dotnet run --project MushTown.Exporter -- catdump UI "UIContentsGuide.img/Minimum/button:Left" 3
```

```
  normal = - (1 children)
    0 = png 1x1 ARGB4444 (3 children)
      origin = vec -10,-45
      z = 1
      _outlink = [str 59] UI/_Canvas/UIContentsGuide.img/Minimum/button:Left
```

The `mouseOver` art of a button often has a **different** origin (the lit picture
is bigger and sits 2 pixels up and left). Check every state, not only `normal`.

## The tree shape you will meet

- The real picture bytes live in `UI/_Canvas/<Node>.img/...`. The node you see in
  the tree is a small stub (`png 1x1`) with an `origin` and an `_outlink` that
  points at the canvas. `uipng` follows the link for you.
- A button is `button:<Name>/normal|mouseOver|pressed|disabled/0`. The `0` is the
  picture index. `uipng` wants the path down to the `0`.
- A window frame is a nine-slice: `nw ne w e sw s se n s c`.
- A list row is a `layer:backgrnd` plus the items in it.

## The search workflow

### 1. Search by name

```sh
dotnet run --project MushTown.Exporter -- find BtTown UI
dotnet run --project MushTown.Exporter -- find Guide UI partial
```

`partial` is the useful one. `find` also searches **inside** the `.img` files, so
it finds names that only exist in the tree structure.

### 2. Search by size, when the name is unknown

You know what the picture looks like but not what it is called. Find every png
of that size in a category:

```sh
& .\.agents\skills\wz-find\findsize.ps1 -Category UI -Size 13x13
& .\.agents\skills\wz-find\findsize.ps1 -Category UI -Size "*x21" -Name Bt
& .\.agents\skills\wz-find\findsize.ps1 -Category Etc -Size 16x16 -Depth 6
```

The script dumps the category once (UI is about 6 seconds and 280 000 lines) and
then searches the dump in memory. The paths it prints are ready for `uipng`.

**If it finds nothing, the depth is too shallow.** The default is 5; the pictures
in `UI` are mostly deeper than that, and some categories need 6 or 7. A bigger
`-Depth` costs time but not much.

Sizes are a strong filter. A 21x21 picture in `UIMap.img` is a minimap button.
A 13x13 in a `button:` node is a close button. A 4x4 or 6x6 next to a `Dot`
node is a page dot.

### 3. Look at the candidates

Export the candidates and put them on one sheet:

```sh
dotnet run --project MushTown.Exporter -- uipng UI "UIContentsGuide.img/Minimum/Dot/0" logs/wz/01.png
& .\.agents\skills\wz-find\sheet.ps1 -In logs/wz -Out logs/wz/sheet.png -Cols 10
```

The sheet shows every picture at once, with the file name under it. Name the
exported files `01.png`, `02.png` so the sheet order matches the list order.
**Look at the sheet before you pick.** It is the fastest way to spot the right one.

### 4. Check the neighbours

The part you want is often next to its siblings in the tree. Once you find the
window, list the whole node at depth 2-3 and see everything it holds: the frame,
the buttons, the tabs, the list rows, the dots. One `catdump` tells you more
than ten `find` calls.

## Before you say "it is not in the WZ"

The user pushed back on this twice, so the bar is high. Do all of these before
you report a piece as missing:

1. `find` with the name, exact and partial, in every category that could hold it
   (`UI` for interface art, `Etc` for shared art, `String` for text).
2. `findsize` with the size, in `UI` and in `Etc`.
3. `catdump` the parent node and look at all the children.
4. Look at the sheet of the candidates. Look at the colours and the shape.
5. Check the old client names too (`UIWindow.img`, `UIWindow2.img`, `StatusBar.img`).

If it is really not there, say what you searched and what you found instead.

## Bringing the art into the game

The rules of the project (see `AGENTS.md`):

1. **The exporter writes the png, the client loads it by the same name.**
   - Add the export to the panel file in `MushTown.Exporter/Export/` (for example
     `ExportGuideUi.cs`).
   - The client loads it with `_game.LoadTexture("ui/<file>.png")`. The name must
     match the exported file name, letter for letter. A missing file throws at
     startup, so check the name before you run the game.
   - `assets/` is never committed (it is Nexon's art). The exporter is the
     thing that is committed.
2. **Positions and sizes go in `layouts/*.json`, not in the code.** One json per
   panel: `layouts/panel-<name>.json`. The window size stays in the code when it
   has to match the art (the art size is the truth).
3. **Never recolour, rescale or bake the official art.** Draw it as it is. If a
   part has no function in our game, draw it with the official disabled art and
   do not make it clickable.
4. **The official art is not stretched.** The `Image` widget stretches the
   picture into its rectangle, so the rectangle must be the art's own size. When
   the states have different sizes (a lit button is bigger), draw the picture
   yourself at its own size.
5. **A button that cannot work is drawn with the disabled art.** The
   `disabled` state of the button is the official way to say "not yet".

## The traps

- `find` without a category walks the whole Base tree: 4+ minutes, one core at
  100%, and it locks `MushTown.Exporter.exe` so nothing else can run.
- `catdump` on a big category prints a lot. Pipe it through `Select-String` or
  write it to a file; do not read it in the console.
- A png node in the tree is often a 1x1 stub. The size in the tree is **not**
  the size of the picture. `uipng` prints the real size.
- A png can have transparent rows. `guide_small_bg.png` is 135x115 but the last
  10 rows are empty, so the window is 135x105. Check the picture, not the header.
- `find` prints the same node twice when it is reachable twice (Base tree and
  the category file). That is normal, not a bug.
- The name of a node is not the name of the game feature. `BtClose` is used by
  40 different windows.

## What is in the WZ now (checked 2026-10-21)

Read `.dsh/memory.md` for the full list (sections 28, 33, 35, 37). The short
version: the minimap frame and its five buttons, the chat frame and its scroll
bar, the NPC talk box, the big and the small guide window, the quest helper bar
and the buff bar are all in the WZ and all in the game now.

Still not in the WZ after a full search: the Quick Move button, the red "M"
community button, the NEW icon column on the left edge.
