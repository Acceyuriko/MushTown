# Unified Object-Component — plan (UI first, sim later)

Date: 2026-10-16. Status: W4 committed (7e89334), reviewed clean, W5 in progress. One commit per slice, only with approval. Standing rule: review-loop BEFORE commit, not after.

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
- W2: saves/server + chars (delete modal, pager, ghost silence). DONE,
  waiting for user acceptance. Menu scenes hold invisible Buttons
  (input only); row art, alpha states, glass underlays and conditional
  buttons stay legacy-drawn (zero z-order risk). Callbacks play their
  own click, mirroring legacy (Button autoplay waits for the W5 funnel
  cutover). Scene.Click returns the hit object now; Hover got the modal
  guard (W1 debt paid). _deleteOpen is dead (modal Enabled owns it).
  Known 16ms divergence: same-frame enter + new/delete/slot clicks
  resolve mouse-first (legacy interleaved enter between pager and the
  buttons; unified dispatch cannot split them). Pager-before-enter is
  exact (hit parent check). Gates 0/0 + 182/182; saves/server/chars/
  chars2/delete/hud shots pixel-identical vs HEAD.
- W3: job/setup + in-game menu bar. DONE, waiting for user
  acceptance. Job/setup join the invisible-Button pattern (name field
  is a silent Button; cell/make/cancel/confirm callbacks mirror
  legacy; enter confirms unless any tree click swallowed it, matching
  the legacy chains). Menu bar joins the game scene: menu/char are
  silent Buttons (the open/close funnel still plays the click),
  furniture is hit-rects with no Buttons; tree order menu > char >
  submenu > furniture matches the legacy chains (only overlap: the
  3px Exp strip under the buttons). Gates 0/0 + 182/182; job/hud/
  server/chars/chars2/delete shots pixel-identical vs HEAD. Known
  shot jitter (pre-existing, not W3): text antialiasing shimmers a
  few hundred px run-to-run (same binary differs from itself on
  saves/setup text). Debt (user picked A 2026-10-16): HUD hover
  ignores panel overlap by legacy design (sound follows the
  highlight); a panel covering the bar still hovers menu/char. Fix
  when panels migrate (W4/W5), not in W3.
- W4: inv/stat/skill. DONE, waiting for user acceptance. Three
  click-only panel scenes (hover stays legacy: the cross-panel
  interleave order is preserved exactly until W5). `Slot : Button`
  (kind + index) reports through Scene-level `CarrySlot.Resolve`;
  hands stay empty (legacy drink/wear/take-off run at once), so
  behavior is identical; the carry slice adds hold/drop/swap later
  (user ruled 2026-10-16: land carry now, behavior unchanged).
  `Draggable` NOT landed: title drags need no threshold (titles are
  Buttons + the legacy held-tracking). Panel z-arbitration stays
  legacy; Scene gains LastClick (title grab offsets). Trees are pure
  (pos, tab), synced per frame. Gates 0/0 + 182/182; inv/use/stat/
  skill/panel/hud/saves/chars shots pixel-identical vs HEAD.
- W5: quest/settings/shop/talk + cleanup. DONE, waiting for user
  acceptance. User ruled ABC: migrate + fix hover-z + autoplay, all
  in W5. Quest/settings/shop/talk scenes land (clicks); hover
  migrates to the trees front-to-back with swallow (the z-fix:
  covered buttons stay silent and dark; non-overlap ids match
  legacy exactly); autoplay by Button Kind via ClickTree (the flag
  funnel is gone; conditional sounds stay explicit: shopbuy, sell,
  talkshop, filled-gated chars buttons, slots-full new, keys, log).
  Slot.Kind renamed ItemKind (was colliding with sfx Kind).
  TalkHover/TalkClick/OnInvSlot deleted; remaining HitRect uses are
  principled (z-arbitration, log plaque, draws). Gates 0/0 + 182/182;
  17 shots pixel-identical vs HEAD (saves/server/delete text jitter
  is the known pre-existing shimmer). Review-loop R1 caught 5, all
  fixed: talkshop fresh-open lost click (now Kind.Click, no wrapper),
  draws followed live HitRect (now follow _hoverId so covered buttons
  stay dark), talk snapshot missed level-gated offers (entriesKey of
  quest ids), 2 stale comments. User ruled Kind must be an enum
  (ButtonKind Silent/Click, default Silent), not string literals.
  R2 clean (1 comment word fixed, 1 perf note rejected as
  negligible). Gates 0/0 + 182/182.

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
