# All-objects design — plan (grilled 2026-10-16)

Status: U6 committed, S1 in progress (overnight autonomous run:
each slice ends with autonomous review-loop; user reviews in the
morning). UI input migration (W1-W5) is done and committed.
- U6 panel art 2 (done): quest/settings/shop/talk panels in-tree.
  New visuals: Fill (plain body), FlatButton (hover-aware, reads
  the legacy _hoverId: sibling scenes keep stale hover when a
  front panel wins, so gfx.HoverButton would lie), StepBox,
  ShopRow, LiveTint (live text + live ink), QuestDetail (+
  QuestLive snapshot; wrap cache extracted to QuestDetailLines),
  TalkBody. Shot staging runs after the scene build, so
  LoadContent re-syncs panel scenes after SetupShotMobs (shots
  skip Update; quest rows/shop/talk baked pre-staging state).
  6 code draws deleted. No new unit tests (shots are the proof).
  Gates 0/0 + 212/212. 20/20 shots match HEAD (settings screen
  added to the batch). Review R1: 0 P0/P1, 2 P2 fixed (quest-row
  guard back to legacy-exact `c!`, shop dys as `16 + n`). No R2
  (literal-level). Playtest pending.
- U5 panel art 1 (done): inv/stat/skill panels in-tree. New
  visuals: TitleBar (shared with U6), StatRow, RightLabel, SkillRow
  (+ SkillLive snapshot struct), InvSlot; Picture gains a Texture2D
  overload for the prebuilt navy backings. Panel scenes now build
  at load too (SyncPanelScenes at the end of BuildGameScene:
  shots skip Update, so the panels drew empty there; first Update
  no-ops). 8 code draws deleted. No new unit tests (all new code
  needs textures; shots are the proof). Gates 0/0 + 212/212.
  19/19 shots match HEAD (stat screen added to the batch).
  Review R1 (subagent; first reviewer stalled and was replaced,
  retry answered after a status ping): 0 P0/P1, 2 P2 (dead alias
  in SkillRow fixed; OwnBuild 3x accepted as trivial). No logic
  change from the fix, so no R2. Playtest pending.
- U4 menu art 2 (done): job, setup, log panel, cursor. New
  visuals: ImgButton, Border (2px; static reused by WhitePanel +
  UiBox — the U3 twin is dead, DrawBoxBorder deleted), UiBox,
  Text, LogPanel (bare root drawn by DrawLogOverlay in menu +
  game; plaque clicks + panel swallow stay code). Cursor brain
  extracted to Shared/CursorAnim (Select + Tick; OS hands stay;
  no fake object — the tree has no Update pass). 8 code draws
  deleted; DrawMenu is tree-only now. 9 new CursorTests (212
  total). Review R1 clean (0 P0/P1; 3 P2, 2 fixed + 1 keep).
  Gates 0/0 + 212/212. 17/17 shots match HEAD. Playtest pending
  (job pick border, setup typing, log panel in menu + game).
- U3 menu art 1 (done): saves, server, chars in-tree. New visuals:
  Sign, ServerRow, CancelRow, CenterText, PagerCell, Round, Dim,
  WhitePanel, Invoke (doll bridge); Picture gains Tint +
  HoverAlpha/PressAlpha; MenuAlpha holds the one 0.9/0.6 spelling;
  GameWidth/Height go internal for components. 12 code draws
  deleted (BtnAlpha stays: the log plaque still draws raw). No new
  unit tests (all new code needs textures; shots are the proof).
  Review R1: 0 P0, 3 P1 + 3 P2 fixed (shared alpha, internal
  consts, orphan comments, stretch note), 1 P2 rejected (empty
  bake = U2 precedent), 1 P2 to playtest (covered buttons no
  longer light under the delete modal: matches input). Gates 0/0
  + 203/203. 17/17 shots match HEAD. Playtest pending.
- U2 HUD art (done): plate, HP/MP, quickslot, tracker, boss bar,
  minimap in-tree. New visuals: Picture, LiveLabel, PlateLevel,
  BarNumber, Tracker, BossBar, Minimap; Label gains Tint (quickslot
  gray); GameObject gains NoHit (art skips hit tests, subtrees
  included). 7 code draws deleted. No new unit tests (all new code
  needs textures; shots are the proof). Review R1 clean (0 P0/P1;
  2 P2 fixed). Gates 0/0 + 203/203. 10/10 shots match HEAD.
  Playtest pending.
- U1 art core (done): Bar visual (back + partial fill + cover,
  SceneMath.BarWidth int math, no clamp/guards: corrupt saves break
  like legacy) + EXP bar and menu/char buttons in-tree (Image art
  children; DrawExpBar/DrawHudButton deleted). Scene draws first
  (HP background hangs 3px over the EXP strip; all overlaps
  measured safe). Review R1 clean (0 P0/P1; 2 P2 comments fixed;
  reviewer mis-measured cover vs background, shots ruled). Gates
  0/0 + 203/203. 10/10 shots match HEAD. Playtest pending.
- C1 carry (done): take-1 CarryHand in Shared, double-click drink,
  wear/swap through hands, Esc-first, cursor icon + grab, save
  returns the hand. Per-scene funnel deleted (rebuilds would eat
  the item). Review R1 0 P0/P1 + 5 P2 fixed + 1 kept by choice
  (TakeOffEquip stays: tested shared rule), R2 clean. Gates 0/0 +
  202/202. 10/10 shots match HEAD (empty hands). Playtest pending.

## Goal

All game code runs as objects on the object-component core.
No stray systems. Defs stay data.

## Core split (Q2, agreed)

- Defs are JSON data rows. One row per kind. Dumb numbers and text.
  `ItemDef`, `SkillDef`, `QuestDef`, `DropTable`, layouts.
- Live things are objects. Each holds a def id and reads the row live.
- 10k rows of data is fine. 10k live objects is not.
- Live objects always read def rows live, never copy at spawn (Q12).
  Hot reload applies at once. Cost is one dict lookup per use.

## Authority (Q1, agreed)

- Server always wins. Client shows movement at once, then takes
  server fixes.
- Only the server rolls game numbers. Rolls ride with server events
  (hit, drop, crit). Client never rolls game numbers.
- Client may roll looks only (spark spread, drift).

## Server logic (Q4, agreed)

- All number-changing logic runs on the server: skill cost,
  damage, drops, quest kill counts, rewards.
- Client runs the same code to show at once, then takes fixes.
- Talk text stays client-only. Accept and turn-in call the server.
- Rule: if a number changes, the server ran it.

## Quest (agreed)

- `QuestDef` data: name, text, need items, kill needs, level gates,
  pre quests.
- `QuestLog` global object, lives in menu and game scenes.
- One `QuestState` component per quest taken. Holds kill counts,
  done flag.
- `Reward` component: exp, mesos, items (Q5: three fields only now;
  fame and skill points come with their systems).
- One `TurnIn` act runs all rewards.

## Skill (agreed)

- `SkillDef` data: name, max level, art keys.
- Player holds `SkillBar` (ids, levels, cooldowns).
- Shared components per cast, all read the def row:
  - `Cost`: HP, MP.
  - `Hitbox`: circles only (user ruled). Many circles per skill
    build other shapes.
  - `Damage`: multiplier, hit count.
  - `BuffFx`: stat, amount, time (buffs only).
- Paths (Q6, user ruled): fixed paths are point lists in data;
  logic paths (homing, meso-blast style) are mover code that reads
  live targets each tick. Data only names the mover plus numbers
  (speed, turn rate, blast range).

## Drop (agreed)

- `DropTable` data: chances, item list, meso range.
- Each mob object holds a `DropTable` component with a table id.
- One `Loot` act for all mobs and bosses (Q10). Reads the table,
  rolls once, spawns drops. Boss tables are bigger rows.
  Quest scripted drops go through the same act with bonus lines.

## Layout (Q3, agreed)

- Layout JSON nodes map to `GameObject` + `Rect` + children.
- Hot reload first: edit JSON, press a key, trees rebuild. No restart.
- In-game editor only if hot reload proves too slow. Not planned.

## Global objects (Q7, agreed)

- `SceneManager` holds the screen scene plus one globals scene that
  never drops: audio, quest log, save store, net client, clock.
- Plain `GameObject`s, no singletons. Tests build one global alone.
- Matches Unity: one scene, many root objects; globals match
  `DontDestroyOnLoad` managers.

## Factories (Q8, Q9, agreed)

- One small factory per kind: `MobFactory`, `DropFactory`,
  `FxFactory`. No god factory.
- Each holds its own id-to-builder map. Builders register at boot.
- Constructors stay dumb (store parts, set fields). Setup logic
  lives in the factory act, so pool reuse can run it.
- Unknown id fails loud with the id in the log.

## Pooling (Q11, agreed)

- Pool high-churn small things only: damage numbers, hit sparks,
  cast effects, flying projectiles.
- No pool for mobs, drops, quests, skills. Rare spawns with state;
  pooling them buys reset bugs.

## Services that stay services

- Shared: GameData, QuestData, SkillData, Shop, Progression, Saves,
  Net, SoundKey, SceneMath, layout shapes, drop/manifest/discovery.
- Server: GameServer, NetClient, Damage math. MobWorld gets rebuilt
  on shared components in the sim phase (only service that changes
  shape).
- Client: Audio player, UiText cache, RollLog, content loaders,
  Exporter tool.

## Headless shots (user demand, before next slice)

- Shot mode hides the game window through the OS. Game still draws
  in memory. Shots stay the same. No window flash.

## Order (Q13, agreed)

Probe, carry, art, sim.

## Sprints (1 sprint = ~10h, gates + shots + review + live check each)

- P1 probe: one quest end to end (def, state, reward, turn-in) plus
  one skill end to end (cost, hitbox, damage). Tests data split,
  factory, server-wins, live reads. 1 sprint. DONE, waiting for
  review + user acceptance. QuestLog static becomes a QuestLog
  object (defs + states + acts); QuestState per taken quest via
  QuestStateFactory (default builder + per-id registry); TurnIn
  pays mesos/items and returns EXP (caller routes through GainExp);
  SkillCost/SkillDamage read defs live (math stays in Progression);
  Sync at StartGame rebuilds states from the save. Gates 0/0 +
  188/188 (6 new tests); 9 shots pixel-identical vs HEAD. Headless
  shots land in this slice (hide loop from ctor, proven 0 visible
  samples). Honest notes: quest server protocol deferred to S9-S10
  (no protocol exists); flying circle hitbox deferred to S3-S4 (no
  flying skills exist); new logic classes do not derive Component
  yet (core still Client-side, S1 moves it to Shared). Review-loop
  R1 clean (0 P0/P1; 2 P2 fixed: dead try/catch in hide loop cut,
  GetCurrentProcess().Id swapped for Environment.ProcessId, unused
  using dropped). User delegated both fixes. Gates 0/0 + 188/188.
- C1 carry acts: pick, drop, swap, wear through hands, Esc cancels.
  Behavior change: new shots plus live check. 1 sprint.
- U1 art core: slice-image and bar visuals, buttons get art
  children, one pilot screen. 1 sprint. Pilot = EXP bar + menu/char
  buttons (the only bar + the only in-tree buttons; plate/HP go to
  U2 with their live digits).
- U2 HUD art: plate, HP/MP, quickslot, tracker, boss bar,
  minimap. 1 sprint.
- U3 menu art 1: saves, server, chars. 1 sprint.
- U4 menu art 2: job, setup, log plaque and panel, cursor. 1 sprint.
  Must kill the WhitePanel/DrawBoxBorder twin (U3 review P1-3):
  delete DrawBoxBorder or make WhitePanel reuse it. Components:
  ImgButton (art + white-40 hover), Border (static color, job
  cell gold), UiBox (live border + text + hint), Text (big static),
  LogPanel (dark fill + baked block on a bare root drawn by
  DrawLogOverlay in menu + game); setup preview rides the Invoke
  bridge (2nd user); job:cell rebuilds the scene on pick (pager
  precedent) so the gold border is build-time. Plaque click order
  ("runs before the switch") and panel swallow stay code.
  Playtest: modal-covered buttons no longer light (U3 P2-5).
- U5 panel art 1: bag, stats, skill. 1 sprint.
- U6 panel art 2: quest, settings, shop, talk. 1 sprint.
- S1-S2 sim core: logic components in Shared, world object,
  spawn/despawn, player mover (walk, jump, ladder, portal). 2 sprints.
  Moves the headless core (GameObject/Component/Scene) to Shared;
  QuestLog/QuestState/SkillCost/SkillDamage gain the base for free.
- S3-S4 fight: swing, skills, buffs, cooldowns, regen, pooled
  numbers and fx. 2 sprints. Builds the flying circle hitbox here
  (first flying skills land here; building it in P1 would mean code
  with no user).
- S5-S6 mobs: AI to components, mob art to tree, drops and pickup.
  2 sprints.
- S7 NPCs, talk clicks, peers, name plates to tree. 1 sprint.
- S8 map layers and camera as objects. 1 sprint.
- S9-S10 quest/skill/drop full port: all def rows, all tables,
  quest log UI reads live states. 2 sprints. Adds the quest server
  protocol (new net messages plus server-side quest state per
  player; LAN trusts the client until here). Reward stays as def
  fields + TurnIn act unless a quest needs custom payout logic a
  row cannot hold.

Total: about 18 sprints.

## Risks

- U1-U6 stay pixel-same. S-phase can not. New baselines there.
- Shot mode must keep every flag alive through the sim phase.
