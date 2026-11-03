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
2. 新组件 `WorldMapTip`：算好宽度（取最长一行），用 `tip` 九宫当底，再按行贴 `Mob` / `Npc` 行图。
3. 内容顺序：地图名一行，然后怪物行、NPC 行。只有我们有的图有后两段。
4. 节点 hover 和左栏行 hover 共用这一个组件。

**已做（2026-11-03）**：三张素材导出完成，实测尺寸 `Line` 251×1 纯 `#777777`、`Mob`/`Npc` 各 9×9（彩色圆角方块 + 中间的黑色小图案）。9×9 的中间像素透明，**不能当九宫拉**，所以按「每行一枚 9×9 圆点」用：绿点 = 怪物，蓝点 = NPC，中间用 `Line` 画分隔。框沿用物品 tooltip 的 `tip` 九宫（官方没有给这个 tooltip 单独的框）。

节点 hover 用 `WorldMapNodeMark`（组件）把索引写进 `WorldMapState.HoverNode`，和点击读同一个 `gfx.Hovered`。

节点 tip 的标题只有一个地图名：`WorldMapUi.PickMap` 算出的那张（我们站的那张优先，否则第一张我们有的），和双击后确认框说的是同一张。上一版列的是这个节点下**全部**我方地图名，现在收成一个——一个节点代表五张图时（「进入冒险家训练场」），标题只写要去的那张，下面列的是那张图的怪和 NPC。

摆拍开关：`--shot ... --screen worldmap --hover <x,y>`，鼠标停在给定点，所有 tip 都按它算（通用，不只大地图）。

## 二、左栏行的 tooltip 和立绘（用户第 5 条后半）

现在左栏只画了名字，没有 tooltip，也没有立绘。

**已做（2026-11-03）**：没导新图。客户端本来就有这两张画：NPC 是 `assets/npc/<id>.png`（地图上画的就是它），怪物是 `assets/mob/<id>/<idle>/0.png`（`MobArt.IdleOf` 就是「有 stand 用 stand，没有用 move」，蓝蜗牛和蘑菇王没有 stand）。导一份 `ui/wm_npc_*` 只是把同样的像素复制一遍，多 26 个文件和一段导出代码。

做法：`WorldMapUi.Lives()` 收集 15 张图里出现过的全部 NPC 和怪物（17 + 9 个），开窗时一次加载；`WorldMapInfo.Draw` 把鼠标所在行写进 `State.HoverLife`（行没有点击，所以不需要 hit box 对象）；`WorldMapTip` 画名字、灰线、立绘、「我们有的、有它的地图」名字列表。地图名单按 (kind, id) 缓存。

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

## 不做

- 书签、导航、传送冷却（用户之前定过「都不做」）。
- 等级范围过滤。
