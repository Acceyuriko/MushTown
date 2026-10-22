# 世界地图传送（W19.3，2026-11-05 这一周）

组队 v1（W19.1）和共享经验（W19.2）做完了。这周回到**单机**：把世界地图变成
真的能用的地图——**点一个区域 → 列出那个区域的地图 → 点地图传送过去**。

## 为什么做这个

- 现在世界地图窗（`HudMap.cs` 的 `WorldMapWindow`）只是**一张图**：13 个区域
  画成小方点，MAP INFO 只写当前区域的标题和地图名 ✗ 不能去任何地方。
- 官方就是能传送的（点区域 → 城镇 → Move 按钮）。
- 传送这件事客户端已经会：`SwitchMap(tm, tn)`（走传送门那条路），
  服务端也认客户端换图（`OnState` 里 `switched` → 重发怪 ✓）。

## 这周做什么

1. **客户端：世界地图窗能点**（`MushTown.Client/Panels/HudMap.cs`）
   - 每个区域的小方点加一个看不见的点击框（`Button` + `HoverFill = false` ✓）。
     我们没有地图的区域不加点击框（点了也没东西可列 ✗ 死按钮）。
   - 点一个区域 → MAP INFO 那边列出**这个区域里我们有的地图**（名字来自
     `assets/ui/mapui.json` 的 `Maps` ✓）→ 点一个名字 → 传送。
   - 传送之后窗口关掉（`Show = false` ✓）：传送完地图没了，
     留在屏幕上没意义 ✓。
   - 玩家选的那个区域留着 ✓：玩家走到别的区域以后列表才回到玩家所在的区域 ✓
     （`OnMapChanged` ✓ 只让窗口重建 ✓，`Build` 里比一下区域 ✓）。
   - 长名字会**换行**（`WrapText` ✓，宽度用官方的 146 像素 ✓），
     一行放不下就占两行三行，行高跟着变 ✓。
   - 数字（点的点击框大小、列表的行距、每行的位置）进 `layouts/panel-map.json` ✓。
2. **传送**（`MushTown.Client/Game/MushTownGame.cs`）
   - 新方法 `TravelTo(int mapId)`：换图（`EnterMap` ✓），
     落在新地图的出生点（用地图数据算好的出生点地面 `Spawn()` ✓，
     不用 `ArrivalGround` ✗ 那是传送门用的，会站到上面的平台 ✓）。
   - 联机时照旧靠状态帧告诉服务端（服务端认 ✓），和走传送门一样。
3. **导出器：区域的地图清单**（`MushTown.Exporter/Export/ExportMapUi.cs`）
   - 官方有两份数据：`WorldMap.img/MapList`（区域，只有老图号）和
     `WorldMap.img/MapLink`（区域 → 区域图 `WorldMap0NN.img`，图里有城镇，
     城镇里有地图号 ✓）。
   - 之前只读第一份 → 1020000（Split Road of Destiny）不在清单里 ✗ 去不了。
   - 现在两份合并 ✓ → 重跑 `dotnet run --project MushTown.Exporter -- panel map` ✓。
4. **截图**：`--screen worldmap`（打开世界地图窗 ✓，
   MAP INFO 那边列着当前区域的地图 ✓ = 点开区域以后的样子 ✓）。
   新选项 `--travel <图号>`（`--shot` 用 ✓）：先传送再截图，
   证明"点名字 → 传送过去 → 站在新地图的出生点" ✓（`logs/wm_travel.png` ✓）。
5. **门禁**：format 净 + build 0 错 + 测试全过 + 审完提交。

## 这周不做什么

- 官方的区域下拉框（`UIMap.img/WorldMap/combo:worldSearch0` ✓ 有，但我们用
  直接点地图上的点，更简单 ✓）。
- 官方的区域说明（`desc`/`townDesc` ✓ 有）→ 下周做悬停提示 ✓。
- 官方城镇在地图上的位置（WZ 里没有我们的这份数据 ✗ → 用列表代替 ✓）。
- 传送的"解锁"规矩（官方按任务解锁城镇 ✗ → 我们的地图都能去 ✓）。
- 更多地图（导出清单里加图号 + 重跑导出 ✓ → 以后单独一块）。
- 队伍血条（`plans/party-hp-2026-11-12.md` ✓）。

## 这周顺手改的

- WZ 数据目录搬家了：`E:\游戏\maplestory\appdata\Data` →
  `E:\game\maplestory\appdata\Data` ✓（`ExporterPaths.DataDir` ✓、
  `AGENTS.md` ✓、`README.md` ✓）。

## 验收

- 打开世界地图 → 点 Maple Island → 左边列出我们的地图（含 Split Road of
  Destiny ✓）→ 点 Southperry → 角色出现在 Southperry 的出生点。
- 门禁：format 净 + build 0 错 + 测试全过。
- 审完提交（每块一个 commit）。
