# 怪物掉落表

每个文件对应一只怪：`drops/{mobId}.json`。

- `MesoMin` / `MesoMax`：金币范围，按 W7 的设计值。
- `Items[].Chance`：**MushTown 的设计值，不是官方掉率**。wiki 没有公布掉率（见 `.dsh/research/drop-tables.md` 的 W3）。
  - 消耗品：10%
  - 装备：2%
- `Items[].Count`：每次掉落的数量，当前都是 1。

数据来源只有两个：`.dsh/research/drop-tables.md` 的「Wiki 掉落数据（2026-11-14）」和 WZ。WZ 本身没有掉落表。

`8641000`（Happy Erda）只写金币，`Items` 为空。神秘球的额外掉落归 ARC 题。
