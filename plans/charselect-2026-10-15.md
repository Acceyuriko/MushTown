# Char select rebuild (fig1 look) — week plan

Date: 2026-10-15. Status: done, waiting for user playtest. No commit until approved.
Gates: format clean, build 0/0, 178 tests pass. Shots: logs/cs3.png (page 1),
logs/cs4.png (page 2 empty), logs/cs5.png (delete modal).

## Decisions (user picked 2026-10-15)

- Pillar: adventure (matches our adventurer warrior path).
- Slots: 60 = 12 per page x 5 pages, full fig1 replica with working pager.
- Motion: full WZ timing + position + fade for ghost, portal, particles.

## Export (all under UI/Login.img/CharSelect unless noted)

- 6 buttons x 3 states (normal/mouseOver/pressed): BtSelect, BtNew,
  BtDelete, BtDeleteConfirm, BtDeleteCancel, Common/BtPreview (back).
- Pager: backgrnd + sprite:base black glass + page1-5 + L/R; blue
  thumb drawn (no WZ art).
- Board: slotJobBoard/closed backgrnd only. Counts drawn as MapleStory text.
- adventure pillar (1 static PNG).
- Frames + one JSON each (image, origin, delay, fade): ghost 16,
  portal 6, particles 9.
- charDeleteMsg box: dropped (158x55 strip, unusable); white box kept.
- BG: user screenshot fig2 -> assets/back/charselect.png (gitignored,
  drawn scaled to 1366x768; aspect 1.777 vs 1.779, stretch invisible).

## Client

- Frames player: reads frames JSON, loops, honors origin/delay/fade.
  First user of per-frame origin/fade; skill effects stay bare for now.
- New DrawChars: bg, 12 slots on shelves, ghost on empty, avatar on
  filled, name tags reused, board top-right with n/60 text, pager,
  BtNew/BtDelete/BtSelect/BtPreview.
- Select: click filled slot -> char walks in place (walk frame timer),
  pillar + portal + particles behind. Draw order: pillar, portal,
  particles, char, tag (particles behind: the flash frame would
  white out the char).
- Delete modal: white box kept + official confirm/cancel buttons.
- Dropped: side info panel. Kept: world pill (server name).
- Saves: MaxHeroes 3 -> 60; Save trims trailing null slots; create
  flow remembers picked slot; tests updated.

## Out of scope

- Job screen rebuild (ClassSelect art exists, later week).
- Board opened state, job progression, selectworld.
- More jobs / more pillars (when the real job system lands).

## Accept (shots)

- Page 1 empty (12 ghosts), page 1 filled (chars + tags), selected
  char with pillar + portal, delete modal, page 2, pager click.
- Gates: build 0/0, tests pass, format clean.
