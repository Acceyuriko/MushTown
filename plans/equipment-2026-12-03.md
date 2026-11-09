# 装备栏 / 装饰栏（总计划）

整体内容已经定完。周切块见 [周计划](cashshop-weeks-2026-12-03.md)。

2026-11-08 用户定：旧存档不兼容，开发阶段直接删档重开；下面所有“旧存档迁移/补齐”都不做。

## 为什么做这个

- 现在的背包 Equip 页把穿在身上的装备画在最前几格。这是错的。
- 背包里的格子都是平等的，只放“拥有的道具”。
- 每个道具记住自己的格子位置。玩家可以在同一个页签里把道具拖到别的格子。
- 每个页签有整理键。点一下把当前页签的道具按类型排好，并从第 1 格开始排满。
- 穿在身上的道具只放在新的装备栏 / 装饰栏里。
- 新的装备栏来自 `UI/UIEquip.img`。旧背包的布局不改，只改 Equip 页内容。
- 这里先把人物外观的两层数据打好：
  - 装备层：有属性，穿在 `Equips`。
  - Cash 层：只改外观，穿在 `CashWorn` / `Hair` / `Face`。

## WZ 实测结论（`UI/UIEquip.img`）

### 窗口

- `Equip/main/backgrnd` = 366×444。图 1 的 EQUIPMENT INVENTORY 底图。
- `Deco/main/backgrnd` = 366×444。图 2 的 DECORATION INVENTORY 底图。
- 两个窗口一样大。默认放在屏幕中间：x = (1366-366)/2 = 500，y = (768-444)/2 = 162。
- 窗口没有单独标题栏，标题已经画在底图上。
- `vector:windowLT = (12,38)`，`vector:windowRB = (461,440)`（Equip）和 `(366,515)`（Deco）。内容区从 (12,61) 开始。

### 顶部页签

- Equipment：`Equip/main/tab:detailTab`。
  - 两张 171×22。
  - 第 1 张 origin = (-11,-31)，画在 (11,31)。
  - 第 2 张 origin = (-184,-31)，画在 (184,31)。
  - `normal` 和 `selected` 各有 2 张。
- Decoration：`Deco/main/tab:detailTab`。
  - 第 1 张 113×22，origin = (-12,-31)，画在 (12,31)。
  - 第 2 张 114×22，origin = (-127,-31)，画在 (127,31)。
  - 第 3 张 113×22，origin = (-243,-31)，画在 (243,31)。
  - `normal` 和 `selected` 各有 3 张。

### 内容底图

- Equipment 页：`Equip/EquipTab/canvas:equip` = 342×349，origin = (-12,-61)，画在 (12,61)。
- Pet 页：`Equip/PetTab/canvas:pet` = 342×371，origin = (-12,-61)，画在 (12,61)。
- Outfits 页：`Deco/CoordiTab/canvas:coordi` = 342×349，origin = (-12,-61)，画在 (12,61)。
- Android 页：`Deco/AndroidTab/canvas:and` = 342×349，origin = (-12,-61)，画在 (12,61)。
- Damage Skins 页：`Deco/DamageSkinTab/canvas:and` = 342×349，origin = (-12,-61)，画在 (12,61)。

### 格子

- `EquipTab/SlotSize = 42`。
- 格子位置统一按 7×6 网格：`slotX=27`、`slotY=100`、`slotPitchX/Y=45`、`SlotSize=42`；当前所有槽都在这张网格上，具体槽表见 `HudEquip`。
- Equipment 页用到的格子：
  - `Slots/5` Top，origin = (-252,-145)，画在 (252,145)。
  - `Slots/6` Bottom，origin = (-252,-190)，画在 (252,190)。
  - `Slots/11` Weapon，origin = (-117,-280)，画在 (117,280)。
- Outfits 页用到的格子：
  - `Slots/105` Top，origin = (-252,-145)，画在 (252,145)。
  - `Slots/106` Bottom，origin = (-252,-190)，画在 (252,190)。
  - `Slots/111` Weapon，origin = (-252,-235)，画在 (252,235)。
  - `Slots/110` Secondary Weapon，origin = (-297,-235)，画在 (297,235)。
  - `Slots/hair`，origin = (-117,-280)，画在 (117,280)。
  - `Slots/face`，origin = (-162,-280)，画在 (162,280)。
- 其他格子先不画道具，但底图本身已经有空格子。

### 按钮

- `Equip/EquipTab/button:DecoUI/normal/0` = 89×22，origin = (-251,-72)，画在 (251,72)。文字是 `Decorations >`。
- `Deco/CoordiTab/button:EquipUI`：同尺寸，回到装备栏。
- `Equip/main/button:close`：关窗口。

## 数据

### `CharaSlot` 新增字段

```csharp
public int Skin { get; set; } // 0..5
public int Hair { get; set; } // 当前发型 cosmetic id，默认 30020
public int Face { get; set; } // 当前脸 cosmetic id，默认 20000
public Dictionary<int, int>? CashBag { get; set; } // 非装饰现金道具 itemId -> count
public Dictionary<int, int>? DecBag { get; set; } // 装饰现金道具 itemId -> count
public List<int>? CashWorn { get; set; } // 正在穿的装饰现金道具 item id：Fashion、当前发型券、当前脸券
public List<int>? UseOrder { get; set; } // USE 页每格的 item id，0 = 空
public List<int>? EquipOrder { get; set; } // EQUIP 页每格的 item id，0 = 空
public List<int>? CashOrder { get; set; } // CASH 页每格的 item id，0 = 空
public List<int>? DecOrder { get; set; } // DEC 页每格的 item id，0 = 空
```

- `Equips` / `EquipBag` 保持原样。它们只放有属性的装备。
- `CashBag` 放非装饰现金道具（Utility / Convenience）。
- `DecBag` 放没穿的装饰现金道具（Fashion、头发、脸）。四张默认券都要有：`5157257` / `5158151` 穿在 `CashWorn`，`5157258` / `5158152` 放 `DecBag`。
- `CashWorn` 放正在穿的装饰现金道具 item id：Fashion、当前发型券、当前脸券。穿上时从 `DecBag` 扣 1 件；脱下时加回 `DecBag`。穿在身上的东西不在背包里，不能卖、不能丢。
- 穿同槽位的新 Fashion 时，旧的那件自动从 `CashWorn` 移回 `DecBag`。
- 换头发 / 脸：新券从 `DecBag` 扣 1 件，`CashWorn` 加新券；旧券从 `CashWorn` 移回 `DecBag`。`Hair` / `Face` 写新外观 id。

### 背包格子位置

- `Use` / `EquipBag` / `CashBag` / `DecBag` 继续存 `itemId -> count`。格子位置另存 `UseOrder` / `EquipOrder` / `CashOrder` / `DecOrder`。
- 每个 order list 一格一个元素。元素是 item id，`0` 表示空格。
- 新角色创建时 order list 为空：`Inventory.Ensure` 按字典里现有道具补进前几格。
- 新道具：先进同样 id 的格子叠数量；没有就放第 1 个空格。
- 拖放：点一下拿起，再点同一个页签的其他格子放下，不用按住鼠标拖。空位移动；同 id 合并；不同 id 交换两个格子。
- 整理：只整理当前页签。按类型排序，再按 item id 从小到大，从第 1 格开始排满。
- `Etc` / `Set-up` 还没有道具，先只做空页签。`Use` / `Equip` 真正可拖放；`Cash` / `Dec` 的位置在现金商店接入时用。

### 默认头发和默认脸

- 默认头发：`5157257` Messy Hair（外观 30020），备用 `5157258`（外观 33390），免费。
- 默认脸：`5158151` Defiant Face（外观 20000），备用 `5158152`（外观 20002），免费。
- 四个都是 `Item/Cash` 里的真道具，有图标。
- 新角色：
  - `Skin = 0`、`Hair = 30020`、`Face = 20000`
  - `CashWorn` = `[5157257, 5158151]`（默认头发券和默认脸券）
  - `DecBag` = `{5157258:1, 5158152:1}`（备用两张，放在背包）
- 旧存档不兼容：开发阶段直接删档重建，不做默认条目补齐。
- 这样每个新角色都有四张默认券；装饰栏两张，背包两张。

### 皮肤身体和头

- 导出 6 套皮肤：身体 `2000` / `2001` / `2002` / `2003` / `2011` / `2012`，头 `12000` / `12001` / `12002` / `12003` / `12011` / `12012`。
- `ExportCharacter` 加皮肤参数，6 套都导到 `assets/chara/body/`，文件名按皮肤区分，例如 `stand1.json` / `stand1_s1.json`。
- `MushTownGame` 载入 6 套 `_charas`。
- `DrawAvatar` / `BodyCenterX` 多一个 `skin` 参数，由 `CharaSlot.Skin` 决定用哪套身体和头。
- 玩家自己、其他玩家、选角、菜单、创建角色都要传同一个 `Skin`。
- 联机同步要带整套外观（皮肤、头发、脸、CashWorn），见现金商店计划的「联机外观同步」。

### 装备槽映射

- `ItemData.Slot == "Wp"` → Equipment 页的 Weapon 格。
- `ItemData.Slot == "Ma"` → Equipment 页的 Top 格。
- `ItemData.Slot == "MaPn"`（连体装）→ Top 格；Bottom 格变红锁住，不能再穿下装。
- `ItemData.Slot == "Pn"` → Bottom 格；`ItemData.Slot == "So"` → Shoes 格。
- 其他槽先留空。槽位映射要通用，不能写死只支持 `Wp` / `Ma`。
- 后期要加：帽子、上衣、下装、鞋子、手套、披风、盾牌、饰品等所有装备槽。加槽时只补 `Slot → Slots/<id>` 映射和美术，不动窗口结构。

### Cash 外观槽映射

- `CashWorn` 里的 `Wp` → Outfits 页 Weapon 格。
- `CashWorn` 里的 `Ma` → Outfits 页 Top 格；`MaPn` → Top 格、Bottom 格变红锁住；`Pn` → Bottom 格；`So` → Shoes 格。
- `Hair` → Outfits 页 Hair 格；有纸娃娃时画上半身预览（`LookBust`），没有时退回券的 `ItemData.Image`。
- `Face` → Outfits 页 Face 格；有纸娃娃时画上半身预览（`LookBust`），没有时退回券的 `ItemData.Image`。
- 没有 Cash 外观时，Outfits 页对应格子留空。

## 背包修改

### 页签

- `TabCount = 6`，6 个页签都注册点击。
- Equip 列 `EquipBag`，Use 列 `Use`，Cash 列 `CashBag`（非装饰），Dec 列 `DecBag`（装饰）。
- Etc / Set-up 暂时没有道具，显示空格子。
- 空格子也要注册 Slot，能接收拖放；整理键对空页签是空操作。
- 当前页签用官方 selected 图，其他页签用 normal 图。
- 6 个页签的格子区都能上下滚动，每个页签各记各的滚动位置。

### 背包滚动

- 每页可见 8 行。
- 小窗 4 列 × 8 行 = 32 格可见，总容量 128 格 = 32 行；超出的行靠滚动看。
- 宽窗 16 列 × 8 行 = 128 格可见，总容量也是 128 格。
- 滚动位置按“行”记，`_invScroll[6]` 每页一个。
- 滚轮一格 = 3 行，只在当前页签的格子区生效。
- 拖动滑块也能滚。滚动条只在总行数超过 8 行时出现；不超过时画 disabled 轨道，不画滑块。
- 整理只排当前页签，排完滚回顶部。
- 数量变化后把滚动位置夹回合法范围，不能滚到空白。
- 拖放和双击用真实格子号：`(滚动行 + 可见行) × 列数 + 列`，不能用屏幕上的行号。
- 滚动条美术：
  - 小窗：`UIInventory.img/Inventory/AutoBuild/scroll:slot`，轨道 5×368，位置 (214,99)，滑块 5×28。
  - 宽窗：`UIInventory.img/Inventory/FullAutoBuild/scroll:avatarTab`，轨道 5×368，位置 (769,99)，滑块 5×28。
  - 两套都有 normal / hover / pressed 三种滑块图。
- 通用 `ScrollBar` 对象：
  - 新建 `MushTown.Client/Ui/ScrollBar.cs`。
  - 对象的 Rect 就是轨道盒子；负责画轨道和滑块，并处理 `Grab` / `Drag`。
  - 参数是当前位置 0..1、设置位置的回调、是否可用、轨道图、滑块图前缀。
  - NPC 商店左右列表和背包小 / 宽两套都用它，不各写一份。
  - 聊天框原来的 `ChatScrollBar` 先不动，继续用旧的三段轨道图。

### Equip 页

- 现在的 `HudInventory` 把 `_equips` 的每个 id 都摆一个 `"worn"` 格，再摆一个 `"bag"` 格。要删掉 `"worn"` 这一排。
- Equip 页只列 `CharaSlot.EquipBag` 里的道具，按 `EquipOrder` 的格子位置显示。
- 空格子也要能接收拖放。整个格子区都注册 Slot，不只是有道具的格子。
- 每个格子显示图标、数量、tooltip；空格子没有 tooltip。
- 拖放：点一下拿起，再点同一个页签的其他格子放下。空位移动；同 id 合并；不同 id 交换。
- 整理键：只整理当前页签。按类型排好，从第 1 格开始排满。
- 双击装备包里的装备 → 穿上。走现有 `CarryHand.Click` 的 `"bag"` 流程。
- 穿上后从装备包扣 1 件，加到 `Equips`。装备栏窗口立刻显示。
- 背包 CASH 页仍然是拥有的现金道具，等现金商店接入时再做。

### 丢弃

- 沿用点击抓取：手里拿着道具时，点到所有 UI 面板外面的世界区域，就把道具丢到地上。
- 丢到玩家脚下：落点 x = 玩家 x，y = 玩家脚下最近的地面。
- 只有 1 件：直接丢，不弹确认。
- 手里 1 件加背包剩余数量大于 1：弹 NPC 商店同款 `wm_pop` 数字输入框，居中屏幕。数量框抽成共用组件，NPC 商店买卖和丢弃都用同一份。
  - 标题 `How many do you want to drop?`
  - 默认 1，最小 1，最大 = 手里 1 件 + 背包剩余数量。
  - `MAX` 填最大数量。Enter / Yes 确认，Esc / No 取消。
  - 确认后按数量从背包和手里一起扣，地上生成一个同样数量的掉落。
  - 取消后东西仍在手里；Esc 先关数量框，不丢手里的东西。
- 点到任何 UI 面板（背包、装备栏、商店、聊天栏、快捷键等）不丢，走原来的 UI 点击。
- 只有世界地图能丢。现金商店、选角等全屏窗口不处理。

### 代码点

- 新建 `MushTown.Shared/Data/Inventory.cs`，统一管理格子位置：
  - `Ensure(chara)`：初始化四个 order list；把字典里有、order 里没有的 id 放进第 1 个空格；把 order 里字典已经没了的 id 清成 0。
  - `Move`：同页签空位移动、同 id 合并、不同 id 交换。
  - `Sort`：只排当前页签，按类型和 item id，从第 1 格开始排满。
  - `Remove(chara, tab, id, count)`：从 `Use` / `EquipBag` / `CashBag` / `DecBag` 扣数量，扣到 0 时把 order 里对应格子清成 0。卖和丢都走这里。
  - `SlotId(chara, tab, cell)`：读某一格的道具 id，`0` 表示空。
- `HudInventory.BuildInvScene` 里 `_invTab == 0` / `1` 改成遍历整个格子区：
  - 小窗可见 4×8 = 32 格，大窗可见 16×8 = 128 格；总容量都是 128 格。
  - 每一格都 `PanelSlot`，空格也注册 Slot；格子号用滚动后的真实序号。
  - 加一个 `ScrollBar`，滚轮改 `_invScroll[_invTab]`，拖动滑块走 `Grab` / `Drag`；`HudInventory` 每帧把鼠标按住状态和 y 转给它。
  - 页签 0 = Equip 用 `EquipOrder` / `EquipBag`，1 = Use 用 `UseOrder` / `Use`，4 = Cash 用 `CashOrder` / `CashBag`，5 = Dec 用 `DecOrder` / `DecBag`。2 / 3 只有空格。
  - `ItemId` 按当前页签返回该格 id 或 0。
- `ClickSlot` 增加格子号，传给 `CarryHand.Click`。
- `CarryHand`：
  - `Kind` 用页签名 `use` / `equip` / `cash` / `dec`，不再用 `bag` / `worn`。
  - 多记来源页签和来源格子号。
  - 同页签同一格双击：装备就穿，药水就喝，Cash Utility 就用，Cash Fashion 就穿，头发 / 脸就换。
  - 放到别的格子：走 `Inventory.Move`，空位移动、同 id 合并、不同 id 交换。
  - 跨页签放置：不接受。
  - `Return`：按来源页签放回对应的包。
- `InvWornRows`、`"worn"` kind 不再用于背包。
- `button:sort` 点击：调用 `Inventory.Sort`，只排当前页签，并把该页 `_invScroll` 归零。

## 客户端窗口

### `EquipWindow`

- 新建 `MushTown.Client/Panels/HudEquip.cs`，类 `EquipWindow : UiWindow`。
- `Action = HotkeyAction.Equip`。
- `LayoutPos() = (500,162)`，`Rect() = (0,0,366,444)`，`TitleRect() = Rectangle.Empty`。
- 一个窗口，五个页面：
  ```csharp
  enum EquipPage { Equipment, Pet, Outfits, Android, DamageSkins }
  ```
- 默认 `Equipment`。
- 页面切换只换底图、页签和内容，不重建新窗口。
- 关闭：右上 X、Esc。
- 拖动（第 3 周）：`TitleRect()` 改成 `(0,0,366,31)`，加 `MapWindowParts.TitleDrag`；`titleH` 进 `panel-equip.json`。第 2 周只做空实现。

### Equipment / Pet 页

- 画 `Equip/main/backgrnd`。
- Equipment 页：画 Equipment 页签 selected 图、Pet 页签 normal 图；画 `EquipTab/canvas:equip`。
- Pet 页：画 Pet 页签 selected 图、Equipment 页签 normal 图；画 `PetTab/canvas:pet`。
- Equipment 页遍历 `Hero.Equips`：
  - 有 `Wp`：在 Weapon 格画图标。
  - 有 `Ma`：在 Top 格画图标。
  - 有 `MaPn`：在 Top 格画图标，Bottom 格画红色锁。
  - 有 `Pn`：在 Bottom 格画图标；`So`：在 Shoes 格画图标。
  - 其他槽忽略。
- Pet 页只画空白格，不显示道具。
- 每个图标挂 `ItemTip`，tooltip 和普通游戏一样。
- 双击已穿装备 → 从 `Equips` 移除，回到 `EquipBag`。长袍双击 Top 格脱整件。
- Equipment 右上 `Decorations` 按钮：切到 `Outfits`。
- 关闭按钮：关窗口。
- 底部 PRESETS、3 个 preset、Apply 不注册点击。底图已经画出来，不额外补图。

### Decoration 页

- 画 `Deco/main/backgrnd`。
- Outfits / Android / Damage Skins 三个页签都注册点击，按当前页画 selected / normal 图。
- Outfits 页：画 `CoordiTab/canvas:coordi`。遍历 `Hero.CashWorn`：
  - `ItemData.Slot == "Wp"` → Weapon 格。
  - `ItemData.Slot == "Ma"` → Top 格；`MaPn` → Top 格、Bottom 格红色锁；`Pn` → Bottom 格；`So` → Shoes 格。
  - `Hairs` 里 `Cosmetic == Hero.Hair` 的 ItemId → Hair 格。
  - `Faces` 里 `Cosmetic == Hero.Face` 的 ItemId → Face 格。
- Android 页：画 `AndroidTab/canvas:and`，只画空白格。
- Damage Skins 页：画 `DamageSkinTab/canvas:and`，只画空白格。
- 图标都挂 `ItemTip`。
- 双击已穿 Cash Fashion → 从 `CashWorn` 移除，加回 `DecBag`。长袍双击 Top 格脱整件。
- 头发 / 脸格双击不做事；改发型和脸去 Beauty 页或背包 Cash 页。
- Outfits 页右上 `EquipUI` 按钮：切回 `Equipment`。按钮在 `CoordiTab` 下，Android / Damage Skins 页没有。
- 关闭按钮：关窗口。
- 底部 3 个纸娃娃 preset、Default Weapon Motion、Effects、Beauty Salon 不注册点击。

### 入口

- `HotkeyAction.Equip` 从“打开 Stat 窗口的装备页”改成“打开新装备栏”。
- `HudBar` 的 `E` 图标同理。
- `Feature.Equip` 改为 `ShowWindow(HotkeyAction.Equip)`。
- Stat 窗口改成新的 Character Info 页，详见下一节。

### Character Info（S）

- `S` 打开的窗口改成上下两块：
  - 上：`UICharacterInfo.img/common/main/backgrnd`，472×230。
  - 下：`UICharacterInfo.img/local/detail/backgrnd`，472×479，接在上图下面。
- 整个窗口 472×709，默认居中：x=(1366-472)/2=447，y=(768-709)/2=29。
- 上半画角色立绘：脚底初值 x=236，y=159（实现时按不压名字牌调整）。具体数字进 `panel-stat.json`，可热重载调整。
- 立绘用真实外观：装备 + 默认头发 / 默认脸；现金外观接入后自动带 Cash 外观。
- 立绘下面写名字和 `Lv.N`。等级用现有 `CharaSlot.Level`。
- 下半只放 STAT 属性页。删除 EQUIP 页签和装备格页面，不再从属性窗进装备。
- 下半保留 Combat Power 和现有属性行。
- 标题 `DETAIL` / `STAT` 用官方图 `local/detail/layer:stat`（450×20）和现有 `stat_page`。
- 关闭：右上 X；拖动：标题区域。
- 改 `layouts/panel-stat.json`：窗口 472×709，两块底图位置、立绘脚底、名字 / 等级位置、属性页位置。

### 布局

新建 `layouts/panel-equip.json`。所有位置、尺寸、字号放这里，代码不写死。
至少包含：

- 窗口位置 x / y。
- 两个底图的位置。
- 两组页签的位置和间距。
- Equipment / Outfits 内容底图位置。
- 需要填充的格子位置、格子大小（第 3 周做内容时加）。
- 图标缩放上限（第 3 周定：现有装备图标最大 33，42 格里原尺寸居中，不做缩放）。
- Decorations / EquipUI / close 按钮位置。
- tooltip 沿用现有 `Tips`，不新增位置。

## 导出器

- 新建 `MushTown.Exporter/Export/ExportEquipUi.cs`。
- 导出到 `assets/ui/`，文件名统一 `equip_...` / `deco_...`：
  - `Equip/main/backgrnd` → `equip_bg.png`
  - `Deco/main/backgrnd` → `deco_bg.png`
  - Equipment 页签 normal/selected 0..1 → `equip_tab_n0.png`、`equip_tab_s0.png` 等
  - Decoration 页签 normal/selected 0..2 → `deco_tab_n0.png`、`deco_tab_s0.png` 等
  - `EquipTab/canvas:equip` → `equip_panel.png`
  - `PetTab/canvas:pet` → `equip_pet.png`
  - `CoordiTab/canvas:coordi` → `deco_panel.png`
  - `AndroidTab/canvas:and` → `deco_android.png`
  - `DamageSkinTab/canvas:and` → `deco_damage.png`
  - `button:DecoUI` normal → `equip_deco.png`
  - `button:EquipUI` normal → `deco_equip.png`
  - `button:close` normal → `equip_close.png`
- 背包新增整理按钮：`UIInventory.img/Inventory/AutoBuild/button:sort` 的 normal / mouseOver / pressed → `inv_sort_n/h/p.png`；宽窗 `FullAutoBuild` 同样导出为 `invw_sort_n/h/p.png`。
- 背包滚动条新增：
  - `AutoBuild/scroll:slot` 的 enabled/disabled base（实测同一张图，只导 enabled）→ `inv_scroll_base.png`，thumb0/1/2 → `inv_scroll_thumb0/1/2.png`。
  - `FullAutoBuild/scroll:avatarTab` 的同样节点 → `invw_scroll_base.png`、`invw_scroll_thumb0/1/2.png`。
- NPC 商店滚动条新增：
  - `UINpcShop.img/Shop/scroll:shop` → `shop_scroll_base.png`、`shop_scroll_thumb0/1/2.png`。
  - `UINpcShop.img/Inventory/Normal/scroll:inventory` → `shop_inv_scroll_base.png`、`shop_inv_scroll_thumb0/1/2.png`。
- Character Info 新增 / 替换：
  - `UICharacterInfo.img/common/main/backgrnd` → `charinfo_main.png`
  - `UICharacterInfo.img/local/detail/backgrnd` → `stat_win.png`（从现在的 `remote/detail` 换成 `local/detail`）
  - `UICharacterInfo.img/local/detail/layer:stat` → `stat_statbar.png`
- `Panel` 命令加 `equip`：`dotnet run --project MushTown.Exporter -- panel equip`。
- 四张默认头发 / 脸券的 `ItemData` + 图标先导出到 `assets/cashitem/`，现金商店可直接复用。

## 截图

`--screen` 新增：

- `equipui`：打开装备栏 Equipment 页，身上有新手一套。
- `decorations`：打开装饰栏 Outfits 页，显示默认头发、默认脸。
- `inv` 仍然要能出图，用来确认 Equip 页不再把穿着的装备放在最前面。
- `invscroll`：小窗 Use 页塞 40 格道具（消耗品图标不足 40，循环使用），滚动条出现，滚到第 2 行。
- `stat`：Character Info 页，上面有角色立绘，下面是属性。

## 测试

- 背包：
  - 6 个页签都能点；Etc / Set-up 显示空格子，Cash / Dec 有对应现金道具时显示道具。
  - 空 `EquipOrder` → `Inventory.Ensure` 后 3 件装备补到第 1、2、3 格。
  - 把第 1 格的装备拖到第 5 格 → 第 1 格空，第 5 格有它，数量不变。
  - 同 id 拖到同 id 格 → 两格数量合并，来源格空。
  - 不同 id 拖到有道具的格 → 两格交换。
  - 整理 → 当前页签按类型排好，从第 1 格开始排满；滚动位置回到顶部。
  - 小窗 Use 页 40 格道具（图标可循环），滚轮一格 3 行 → 滚到最底，第一行是第 9 格，最后一行是第 40 格。
  - 小窗 Use 页切到别的页签再回来 → 滚动位置还在。
  - 小窗塞满 128 件 → 最多滚到第 25 行（第 97 件到第 128 件），不会出现全空。
  - 滚动后点第 1 个可见格 → 拿起的是真实格子号对应的道具，不是第 1 格。
  - `Equips` 里有装备 → 不占背包格子。
  - 双击装备包里的装备 → `Equips` 多一件，`EquipBag` 少一件。
  - 丢弃 1 件道具 → 背包少 1 件，地上多 1 个掉落，数量不变。
  - 持有 5 件道具时丢弃并选 3 → 背包少 3 件，地上 1 个数量 3 的掉落；取消则背包不变、东西还在手里。
  - 卖 / 丢到数量 0 → `UseOrder` / `EquipOrder` / `CashOrder` / `DecOrder` 对应格清空。
- 装备栏：
  - `Equips` 有 `Wp` → Weapon 格出现图标。
  - `Equips` 有 `Ma` → Top 格出现图标；`Equips` 有 `Pn` → Bottom 格出现图标。
  - `Equips` 有 `MaPn` → Top 格出现图标，Bottom 格变红锁住。
  - 双击已穿装备 → `Equips` 少一件，`EquipBag` 多一件。
- 装饰栏：
  - `CashWorn` 有 `Wp` → Weapon 格出现图标。
  - `CashWorn` 有 `Ma` → Top 格出现图标；`MaPn` → Top 格出现图标、Bottom 格变红锁住。
  - `Skin=0`、`Hair=30020`、`Face=20000` → Hair / Face 格有图标。
  - `Skin=2`、`Hair=33390`、`Face=20002` → 用皮肤 2 的身体和头画，Hair / Face 格有图标。
  - 空 `CashWorn` → 其余格子空。
  - 双击已穿 Cash Fashion → `CashWorn` 少一件，`DecBag` 多一件。
- 存档：
  - 旧存档不迁移：缺少四个 order list（或长度不是 128）的旧档直接删档重建。
  - 新角色一开始 `CashWorn` 有两张默认券，`DecBag` 有两张备用券。
- 窗口：
  - Equipment / Pet / Outfits / Android / Damage Skins 五个页签都能切换，底图和页签高亮正确。
  - Pet / Android / Damage Skins 页显示空白格，不显示道具。
  - Equipment 的 Decorations 能切到 Outfits，Decoration 的 EquipUI 能切回。
  - X 和 Esc 能关。
- Character Info：
  - `S` 打开后上块有角色立绘，名字 / 等级正确，下块有属性。
  - 装备页签不再存在。

## 验收

- 背包 6 个页签都能点；Etc / Set-up 显示空格子，Cash / Dec 有对应现金道具时显示道具。
- 背包 Equip 页不再有“穿着的装备排在最前”的格子。所有格子只放拥有的装备。
- 装备显示在自己保存的格子里。拖到同页签其他格子能移动；整理键能按类型排满。
- 背包每个页签都能滚：滚轮和拖动滑块都行，每页滚动位置分开记。
- 手里拿起道具点 UI 外世界区域能丢到脚下；单件直接丢，多件弹数量框，地上能捡回。
- `E` 能打开新装备栏，X / Esc 能关。
- `S` 打开的 Character Info 上面有角色立绘，下面是属性。
- Equipment 页能看到身上的武器和新手一套。长袍显示在 Top 格，Bottom 格变红锁住。
- 双击装备栏里的装备能脱下，回到装备包。
- Pet、Android、Damage Skins 页能点开，显示各自的空白格。
- 点 `Decorations` 进入装饰栏。Outfits 页能看到默认头发、默认脸。
- 双击装饰栏里的 Cash Fashion 能脱下。
- 点 `EquipUI` 能回装备栏。
- 新角色创建后有默认头发和默认脸。
- 截图 `equipui`、`decorations`、`inv`、`invscroll`、`stat` 都能出图。
- 门禁：format 净 + build 0 错 + 测试全过。

## 这次不做什么

- Pet、Android、Damage Skins 页里的道具。页面能开，但只显示空白格。
- 装备栏的 3 个预设、Default Weapon Motion、Effects、Beauty Salon。
- 现金商店和背包 CASH 页。现金商店单独做。
- 帽子、上衣、下装、鞋子、手套、披风、饰品。
- 换发型和换脸的纸娃娃部件。现金商店的 Beauty 页做。
- 从 Cash 包双击穿戴。现金商店接入时做。
- 把 Cash 外观放进世界人物。这里只做数据字段和窗口显示；世界渲染在现金商店接入时做。
- 更多皮肤 / 发型 / 脸的购买和切换。
