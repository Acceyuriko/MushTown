# N4b-2 大地图收尾：tooltip、立绘、传送框

2026-11-02 用户验收 N4b 时提出，留到下一轮。这一轮只做这三块，不碰别的地图逻辑。

已做完，2026-11-03 通过验收，提交 `d3bf083`（5 轮审查，客户端测试 61 → 69）。

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
2. 新组件 `WorldMapTip`：算好宽度（取最长一行），用 `tip` 九宫当底，再按行贴 `Mob` / `Npc` 行图。
3. 内容顺序：地图名一行，然后怪物行、NPC 行。只有我们有的图有后两段。
4. 节点 hover 和左栏行 hover 共用这一个组件。

**已做（2026-11-03）**：三张素材导出完成，实测尺寸 `Line` 251×1 纯 `#777777`、`Mob`/`Npc` 各 9×9（彩色圆角方块 + 中间的黑色小图案）。9×9 的中间像素透明，**不能当九宫拉**，所以按「每行一枚 9×9 圆点」用：绿点 = 怪物，蓝点 = NPC，中间用 `Line` 画分隔。框沿用物品 tooltip 的 `tip` 九宫（官方没有给这个 tooltip 单独的框）。

节点 hover 用 `WorldMapNodeMark`（组件）把索引写进 `WorldMapState.HoverNode`，和点击读同一个 `gfx.Hovered`。

节点 tip 的标题只有一个地图名：`WorldMapUi.PickMap` 算出的那张（我们站的那张优先，否则第一张我们有的），和双击后确认框说的是同一张。上一版列的是这个节点下**全部**我方地图名，现在收成一个——一个节点代表五张图时（「进入冒险家训练场」），标题只写要去的那张，下面列的是那张图的怪和 NPC。

摆拍开关：`--shot ... --screen worldmap --hover <x,y>`，鼠标停在给定点，所有 tip 都按它算（通用，不只大地图）。

## 二、左栏行的 tooltip 和立绘（用户第 5 条后半）

现在左栏只画了名字，没有 tooltip，也没有立绘。

**已做（2026-11-03）**：没导新图。客户端本来就有这两张画：NPC 是 `assets/npc/<id>.png`（地图上画的就是它），怪物是 `assets/mob/<id>/<idle>/<i>.png`（stand 多于 1 帧才用 stand，否则 move；蓝蜗牛和蘑菇王的 stand 只有 1 帧）。tip 播放该动作的全部帧。

做法：`WorldMapUi.LivesOf(shown)` 只收集当前地图的 NPC 和怪物，开窗时加载这些行要用的帧；左栏每行是一个对象（`Rect + Picture + Text + LifeStand + LifeTip`），`LifeTip` 由 `Tips` 层统一画，不再往 `State.HoverLife` 写鼠标状态。地图名单按 (kind, id) 缓存。

地图名来自 `MapUi.Data.Maps`，只列我们导出的 15 张：蜗牛出现在 Snail Park、Inside the Small Forest、Inside the Dangerous Forest 三张。

## 三、输入框统一成一个组件（用户 2026-11-02 指出）

**已做（2026-11-03）**：`Ui/TextField.cs`，替换原来的 `CaretText`。

- 组件持有文本、光标、焦点和按键重复（按住 400ms 后每 40ms 一个）。`Read(kb, dtMs)` 返回 `FieldKey`：Enter = Submit、Esc = Leave，由窗口决定「发送」和「离开」是什么意思。
- 框是 Owner 的 Rect，只画文字和光标。聊天窗的官方输入条挪成 `PanelH`（三横切片）挂在同一层。
- 光标位置进布局（`caretDx/caretDy/caretH`，宽度缺省 1）：聊天窗白字，搜索框黑字（`Ink`）。摆拍比对：`chattype` 和 `worldmapsearch` 两张图和改之前逐像素相同。
- 字符表只剩一份（聊天窗原来那张比搜索框全，取全的）。
- `TextFieldTests` 钉住几条：按下的键进文本、按住 400ms 后每 40ms 一个、Enter/Escape 回给窗口、100 字上限、失焦不吃键、聚焦时按住的键不算新按键。（`--shot` 不跑 Update，重复只能靠单测。）

## 四、下拉框统一（用户 2026-11-02 记录）

**已做（2026-11-03）**：`WorldMapCombo` + `WorldMapComboList` 合成一个 `ComboBox`（盒子 + 展开列表 + hover 态都在里面），四个地方共用：顶部三个和搜索板的类型框都由 `WorldMapWindow.Combos` 一张表给出（位置、宽度、文字、选项、选中后做什么）。原来位置写了两遍（建对象一遍、`ComboBox(index)` 一遍），现在只有一遍。

- 行高进布局（`comboRowH`，20，等于美术高，不能改）。
- 列表行没有箭头，所以右边用左端盖镜像收口。官方三块图里没有「无箭头的右端盖」：盒子本身只有左边和上边的灰线（`n2` 右边是白、底边也是白），列表行镜像了左端盖，所以多了一条右边线，且每行自带圆角（行与行的交界处有 2px 的缝）、最后一行没有底下线。
- 搜索板的输入框和类型框共用 `sInX`/`sInW`（删掉 `sTypeX`/`sTypeW`）。
- 摆拍开关：`--screen worldmapcombo`，展开第二个框。

## 五、传送确认框换成官方素材（已完成）

现在用的是 NPC 对话框（`talk_dlg_*`，519×110），用户说是错的。官方是蓝色圆角框 + 白色文字 + Confirm/Cancel 两个按钮。

素材是 `Basic.img/sysPop`（用户给的路径）：`back/{top,mid,bottom}` + `button/Bt{Yes,No}/{normal,mouseOver,pressed}/0`，按钮位置在 `posInfo/button/YESNO_ASK`（73,14）和（162,14）。已经导完并接上。



## 验收

1. `--shot worldmap` 节点 hover 出官方多行 tooltip（要能摆拍：加一个 `--hover <x,y>` 之类的开关，或者直接把鼠标位置写进布局文件）。
2. 左栏 NPC 行 hover 出带立绘的 tooltip。
3. 点节点两次出蓝色确认框，Confirm 传送，Cancel 关闭。

## 六、验收后的五条修改（2026-11-03，用户看截图提的）

1. **小地图 NPC 列表的 tooltip 要跟大地图一样**（名字 + 立绘 + 绿色地图行）。做法：那个 tooltip 抽成 `Ui/LifeTip.cs`，左栏行和小地图列表行都挂这一个组件，由 HUD tips 层（Z 700）画；两个窗口自己都不画了。`HudMap.NpcListPanel` 原来那句「名字 + 第一句话」的 `NpcTip` 删掉。小地图窗口要补加载 `ui/tip_*` 和 `ui/wm_tip_line`。
2. **左栏行「文字有点偏上」**。原因：按贴图盒子（高 14）居中，字实际只占 10px 的墨迹带，盒子上下各多 2px → 字高 1px；很多行还手写 `+2`，更高。
3. **下拉 option 的文字也偏上**，同一个原因（`r.Y + 2`）。
4. **统一成一个 Text 组件**：`Ui/Text.cs`，`Value`/`Ink`/`X`/`W`/`Line`（Top/Middle/Bottom），按 `UiText.SmallInk` 的墨迹带居中。`Fit` 从 `WorldMapInfo.Fit` 搬过来。用到的行：左栏 NPC/怪物行、下拉闭合框和展开行、搜索结果行、小地图 NPC 列表行、`HoverTip`。
5. **`TipAnchor` 改成组件**（`Widgets.cs`），挂在 tips 层的对象上，`Tips`/`WorldMapTip` 用 `Owner.Get<TipAnchor>()` 取。不再是「全局函数」，符合 Object-Component。

**立绘会动**：NPC 的 `stand` 导出全部帧（`npc/{id}.png` + `npc/{id}_{i}.png`，延迟进 `StandDelays`），`GameMobs.LifeIdle(kind,id)` 一起给图和时间，`MobArt.Idle.At(delays, gfx.Time)` 选帧。怪物本来就有多帧。注意 `uol` 帧要解析（0011000 的 3~6 帧指回 1,2,1,2）。

**地图上的 NPC 也动，而且和 tooltip 是同一个组件**（用户 2026-11-03 追问）：`World/LifeStand.cs` = 「一个 NPC/怪物的 stand 动画」，有 `Name`、`Art`（`IdleArt`：图 + 延迟）、`ImageAt(ms)`。地图上每个 NPC 的对象挂 `NpcArt` + `LifeStand`，`GameRender.DrawNpc` 用 `LifeStand.ImageAt(_timeMs)` 取帧；左栏行和小地图列表行挂 `LifeStand` + `LifeTip`，`LifeTip` 从 `Owner.Get<LifeStand>()` 取名字和图。两边同一个钟（`_timeMs` 就是 `gfx.Time`），同一时刻是同一帧。所有多帧 NPC 的帧同尺寸，所以 origin 只存第一帧就够。

**怪物也要动**（用户 2026-11-03）：`MobArt.IdleOf` 原来「有 stand 就用 stand」，但蜗牛的 stand 只有 1 帧 → 怪和 tip 都不动。改成「stand 多于 1 帧才用，否则用 move」（100000 的爬行在 move，5 帧）。世界里的怪由 `MobArt.Update` 推进，tip 由 `LifeStand.ImageAt` 选帧，两边读同一份帧。

**名字牌**（用户 2026-11-03）：没有戒指牌的角色和 NPC 共用 `DrawTag`（`NameTag.cs`）：半透明黑板 `(0,0,0,120)` + `Text.Draw` 居中（`Text` 组件新加静态版，规则只有一份）。NPC 的牌子原来按 spawn 点居中、字也按贴图盒子摆，看着「没居中」；现在挂在小人立绘的中心，字按墨迹带居中。

**Text 的两个轴**（用户 2026-11-03）：`Text` 加 `TextAlign { Left, Center, Right }`（默认 Left），和原来的 `TextLine { Top, Middle, Bottom }` 组成完整的布局选项；静态 `Text.Draw` 同时收两个轴。名字牌用 Center。

**选中节点后左栏跟着换**（用户 2026-11-03）：左栏的 NPC/怪物行是 `Build` 时按 `WorldMapUi.ShownMapId` 建的（`Sel >= 0` 时就是 `SelMap`），但点节点只改了 `Sel`/`SelMap` 没重建，所以列表停在上一个地图。`Pick` 里补了 `Dirty = true`。

**怪帧时间**（用户 2026-11-03）：`ExportMob` 原来每帧写死 100ms，比原版快一倍（蜗牛看着「鬼畜」）。原版不是固定帧率，是每帧一个 ms。数据在 `Data/Packs/Mob_*.ms` 的 `Mob/<id>.img/<动作>/<帧>/delay`（用户指出后找到：`WzRoot.LoadBase` 现在会加载 `Packs/*.ms`），导出逐帧读，缺省 180。

**怪属性以 WZ 为准**（用户 2026-11-03）：`Mob/<id>.img/info` 里有全套官方的 maxHP/exp/speed/PADamage/PDRate/boss。导出映射：MaxHp=maxHP、Exp=exp、Def=PDRate/100、Touch=PADamage、Speed=100+speed px/s、Boss=boss；只有 Aggro（追击范围）WZ 没有，保留自调。之前写的「WZ 没有 hp/exp」是错的，数据在 Packs 里没加载。改完导出必须重跑 `export`；旧的 `assets/` 没有 `StandDelays` 会在启动时报错。

## 不做

- 书签、导航、传送冷却（用户之前定过「都不做」）。
- 等级范围过滤。
