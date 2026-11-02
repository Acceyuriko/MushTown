# N4b-2 大地图收尾：tooltip、立绘、传送框

2026-11-02 用户验收 N4b 时提出，留到下一轮。这一轮只做这三块，不碰别的地图逻辑。

## 一、官方 tooltip 格式（用户第 9 条）

现在节点 tooltip 是游戏里通用的单行 `HoverTip`，不是官方的。官方是拼出来的多行框。

素材（`UI.wz`，都是 `_outlink`，导的时候跟着链接走）：

| 用途 | 节点 |
|---|---|
| 行底 | `UIWindow2.img/ToolTip/WorldMap/Line` |
| 怪物行 | `UIWindow2.img/ToolTip/WorldMap/Mob` |
| NPC 行 | `UIWindow2.img/ToolTip/WorldMap/Npc` |

做法：

1. 导出上面三张到 `ui/wm_tip_{line,mob,npc}.png`。
2. 新组件 `WorldMapTip : Component, IDrawable`：算好宽度（取最长一行），用 `Line` 拉成底，再按行贴 `Mob` / `Npc` 行图。
3. 内容顺序：地图名一行，然后怪物行、NPC 行。只有我们有的图有后两段。
4. 节点 hover 和左栏行 hover 共用这一个组件。

## 二、左栏行的 tooltip 和立绘（用户第 5 条后半）

现在左栏只画了名字，没有 tooltip，也没有立绘。

1. 立绘复用地图里的图：NPC 用 `Map.wz/Map/Map<n>/npc` 的立绘（和游戏里地图上画的是同一张），怪物用 `Mob.wz` 的 `stand/0`。
2. 导出到 `ui/wm_npc_<id>.png`、`ui/wm_mob_<id>.png`，只导我们 15 张图里出现过的 id。
3. 左栏每一行加 hit box，hover 时出上面那个 tooltip（名字 + 立绘 + 我们有的地图里出现它的那几张）。

要先确认两件事再动手：

- 地图 NPC 的立绘是不是已经在 `assets/` 里了（游戏里画 NPC 用的就是它，大概率能直接复用）。
- 怪物 `stand/0` 的导出代码已经有（打怪摆拍用过），确认能按 id 取单帧。

## 三、输入框统一成一个组件（用户 2026-11-02 指出）

两个问题：搜索框长按不重复输入；聊天和搜索各有一套输入代码。

1. 抽一个 `TextField` 组件：矩形 + 光标 + 文本 + 按键重复（按住 400ms 后每 40ms 一个字符）。聊天窗和搜索框都用它。
2. 搜索板的输入框和下拉框改成挂在同一个容器下，共用左右边（不再各写一套绝对坐标）。

## 四、下拉框统一（用户 2026-11-02 记录）

大地图里有四处下拉：顶部三个（世界 / 区域 / 地图）+ 搜索板的类型框。现在都各写一套坐标。

要做：一个 `ComboBox` 组件，盒子 + 展开列表 + hover 态都放在里面；四个地方共用。列表行的 hover 用 `Basic.img/ComboBox21/mouseOver`（已导出）。

## 五、传送确认框换成官方素材（已完成）

现在用的是 NPC 对话框（`talk_dlg_*`，519×110），用户说是错的。官方是蓝色圆角框 + 白色文字 + Confirm/Cancel 两个按钮。

素材是 `Basic.img/sysPop`（用户给的路径）：`back/{top,mid,bottom}` + `button/Bt{Yes,No}/{normal,mouseOver,pressed}/0`，按钮位置在 `posInfo/button/YESNO_ASK`（73,14）和（162,14）。已经导完并接上。



## 验收

1. `--shot worldmap` 节点 hover 出官方多行 tooltip（要能摆拍：加一个 `--hover <x,y>` 之类的开关，或者直接把鼠标位置写进布局文件）。
2. 左栏 NPC 行 hover 出带立绘的 tooltip。
3. 点节点两次出蓝色确认框，Confirm 传送，Cancel 关闭。

## 不做

- 书签、导航、传送冷却（用户之前定过「都不做」）。
- 等级范围过滤。
