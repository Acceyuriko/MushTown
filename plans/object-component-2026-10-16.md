# Unified Object-Component — plan (UI first, sim later)

Date: 2026-10-16. Status: W1 done, waiting for user acceptance. One commit per slice, only with approval.

## W1 done (core + submenu)

- New: Scene.cs (UiGfx/GameObject/Component/Scene/SceneManager),
  Widgets.cs (Rect/Panel/Button/Image/Label).
- Submenu migrated: 6 rows are Buttons (art rows 0-1, text rows 2-5),
  popup is a Panel; old branches/hover/DrawSubmenu/_showSubmenu gone.
- Scene rebuilds on layout reload (popup height reads textures, so the
  ctor reload skips the build; LoadMenuContent builds + shows).
- Gates: format clean, 0/0, 182/182. Shots pixel-identical vs HEAD
  (hud + saves, worktree baseline; logs/ fx5 refs are stale, ignore).
- Live check needed: submenu open/close, row clicks, hover sound,
  hover cursor (invisible to shots).

## Why

- UI buttons are loose rects: PlaySfx, hover ids, and hit tests are each
  written 3 times (draw/click/hover). Every new button repeats it.
- Future maps hold hundreds of mobs; bosses and skills add bullets and
  projectiles. They need one uniform object model, not one-off code each.

## Decisions (user picked 2026-10-16)

- A: one Object-Component core for UI and world objects (Unity-like).
- No ECS (hundreds of objects do not need it; rewrite cost too high).
- UI migrates first (proves the core, fixes the scattered sfx now);
  sim objects (mob/bullet/projectile) migrate in a later phase.
- Strangler migration: one screen per slice, old path deleted in the
  same slice. No big bang, no two systems left behind at the end.

## Core design (grilled 2026-10-16, user picked all A/B as noted)

- Scene = one screen (Unity-like). Menu screens build on enter and
  drop on leave; the game owns one persistent scene (panel positions
  survive). `SceneManager` owns the current scene and switching (B:
  keep the extension point).
- `MushTownGame` keeps its screen switch; each case delegates to its
  scene's Update/Draw. No other manager.
- `GameObject`: children, enabled flag, modal flag, component list,
  Update/Draw. No game logic; it only owns the tree.
- `Component`: back-ref to its object, virtual Update/Draw. One job
  each. Headless rule (iron): base classes never reference
  Texture2D/SpriteBatch/GraphicsDevice; drawing lives in visual
  components only, so logic components later run on Server as-is.
- UI components: `Rect` (pos/size, numbers copied as-is for pixel
  parity), `Button` (kind + OnClick + visual child; sfx/cursor fully
  automatic by kind, screens never call PlaySfx), `Label`, `Panel`,
  `Image`, `Slot` (cursor-only item cell, still silent), `Draggable`
  (press-move over a threshold; unmoved release = click).
- Input: Scene walks the tree topmost-first once per frame; first hit
  takes hover/click. Modal objects (talk/log/delete) swallow below.
  Click-to-front = Scene reorders children. Hover change plays over.
- Cursor: Scene maps hovered kind (button/slot/none) + pressed to the
  existing 0-4 states (ApplyCursor reused). `_hoverId`/`_hoverItem`
  strings go away.
- Carry (MapleStory pick-up/drop, not press-drag): Scene-level
  `CarrySlot` (one item max). Slot clicks only report; Scene decides
  pick/drop/swap by kind match (items/slots carry kinds; mismatch =
  reject back to hand, silent). Wear/TakeOff reused, not rewritten.
  Carried icon drawn by Scene at the cursor. Esc cancels carry first
  (item back), then closes panels; outside clicks do nothing (ground
  drop is a later feature).
- Coexistence: input hits the Scene first, falls through to the
  legacy chain for unmigrated parts. Legacy deleted slice by slice.
- Server rule (red line stands): sim phase puts logic components in
  Shared, draw/sfx in Client. This plan moves UI only.

## Migration weeks (UI, user picked)

- W1: core (Scene/SceneManager/GameObject/Component/Rect/Button/Label/
  Panel/Image) + submenu migration. Delete old submenu code.
- W2: saves/server + chars (delete modal, pager, ghost silence).
  Debt from W1 review: Hover needs the modal guard Click has (an open
  modal must swallow hover-highlights + over sfx below it, not just
  clicks); land it with the first modal (delete).
- W3: job/setup + in-game menu bar.
- W4: inv/stat/skill (`Slot`/`CarrySlot`/`Draggable` land here: first
  users, not earlier).
- W5: quest/settings/shop/talk + cleanup (SfxOnHover, old hover/click
  chains, dead rect helpers; grep must find no stragglers).

## Sim phase (later, separate plan)

- Mob/bullet/projectile/NPC onto the same core when large maps land.
- Pooling lands with bullets, not before.

## Out of scope

- ECS, pooling now, framework features with no user yet.
- Sim migration (this plan ends when UI is fully migrated).
- Visual redesign: every slice is behavior-identical, pixel-identical.

## Accept (per slice)

- Shots pixel-identical vs pre-slice (submenu/saves/chars/panels).
- Gates: build 0/0, 182/182 tests, format clean.
- Live check by user (sound + cursor are invisible to shots).
