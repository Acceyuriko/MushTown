# 扩展性重构 — 2026-10-26

状态：K1 第 1、2 周已验收并提交；N5 键窗组件化已验收并提交；N0（`Box` + `Ui/AvatarArt.cs` + `AvatarPreview`）已做、等验收。下一步 K2 第 2 周（动作池）；W1（`Rect` 父相对）重写在 §3.5，排在 K2 之后、N1 之前。按片做，每片 1-2 周，一次验收。

## 0. 一句话

三处从"手写清单"改成"按 id 注册、按对象走"：

1. **按键**：一个 `InputController` 每帧读一次键盘和鼠标；消费者自己问它"我这个目标刚按下吗 / 正按着吗"。每个角色 3 套完整 preset。技能可以进表，但**不做任何默认键**。
2. **画面**：窗口正文由组件画，删掉 `Invoke`。新窗口（星之力、潜能）用同一套组件。
3. **世界**：怪、伤害数字、特效、传送门自己 `Update`，自己标记死亡。主循环不再为每种东西写一段。

目标规模：神秘河几百种怪和 boss、几百种伤害皮肤、全职业几百种技能。

已知代价（用户 2026-10-26 接受）：技能没有默认键，拖放绑定是后期整体重做（技能/消耗品/椅子/面板按钮一起）。所以 K1 之后、拖放重做之前，**技能放不出来，只能普通攻击**。

已定不做：菜单 5 屏对象化；世界层 y 排序搬进树（画法保持现状）。

## 1. 现状（改动前，2026-10-26 的代码事实）

### 1.1 按键

- [Hotkeys.cs:7](../MushTown.Client/Core/Hotkeys.cs#L7) 的 `HotkeyAction` 只有 15 个 UI 动作。
- [Hotkeys.cs:57](../MushTown.Client/Core/Hotkeys.cs#L57) 的 `FreshHotkey` 一帧只返回一个动作（第一个命中的）。
- 绑定表和 3 个 preset 现在存在 **`data/settings.json`**（全局）：[Hotkeys.cs:110-114](../MushTown.Client/Core/Hotkeys.cs#L110-L114)，由 [Audio.cs:38-42](../MushTown.Client/Core/Audio.cs#L38-L42) 读写。存档 [Saves.cs:153-177](../MushTown.Shared/Data/Saves.cs#L153-L177) 的 `CharaSlot` 里没有任何按键字段。
- 技能键写死在 [MushTownGame.cs:72-73](../MushTown.Client/Game/MushTownGame.cs#L72-L73)：`_qWasDown` 到 `_zWasDown` 共 16 个字段。
- 读取在 [MushTownGame.cs:506-529](../MushTown.Client/Game/MushTownGame.cs#L506-L529)：13 次 `FreshKey`，每帧都读。
- 菜单开合后补边沿在 [GameCombat.cs:198-217](../MushTown.Client/Game/GameCombat.cs#L198-L217)：再手写 17 行。
- 键窗只列 8 个动作（[HudKeyConfig.cs:52-62](../MushTown.Client/Panels/HudKeyConfig.cs#L52-L62)）。
- 快捷栏 12 格用固定键 1-9、0、-、=（[HudQuickSlots.cs:14-18](../MushTown.Client/Panels/HudQuickSlots.cs#L14-L18)），键窗还要拦着不让它们被绑（[HudQuickSlots.cs:98](../MushTown.Client/Panels/HudQuickSlots.cs#L98)）。
- 普通攻击 Ctrl、跳跃 Alt 也是写死的（[MushTownGame.cs:506-515](../MushTown.Client/Game/MushTownGame.cs#L506-L515)）。

结论：**只有 UI 动作在绑定表里，且表是全局的**。技能、攻击、跳跃、快捷栏都不在；换角色共用一套键。

### 1.2 画面

- 28 处 `new Invoke(...)`，13 个文件。整块窗口正文靠一个方法画。

| 文件 | 处数 | 画什么 |
|---|---|---|
| [HudKeyConfig.cs](../MushTown.Client/Panels/HudKeyConfig.cs) | 8 | 键帽、动作格、快捷栏格、手拿、预设点、下拉（202/216/239/286/318/339/357/378） |
| [HudBar.cs](../MushTown.Client/Panels/HudBar.cs) | 4 | 气泡、快捷栏角标、手拿、tips（267/311/333/336） |
| [HudQuest.cs](../MushTown.Client/Panels/HudQuest.cs) | 4 | 任务图标、进度条、正文（99/104/117/122） |
| [MenuSetup.cs](../MushTown.Client/Screens/MenuSetup.cs) | 3 | 小人预览、名字框、错误行（54/79/88） |
| [HudMap.cs](../MushTown.Client/Panels/HudMap.cs) | 2 | 小地图、大地图（119/289） |
| [HudParty.cs](../MushTown.Client/Panels/HudParty.cs) | 2 | 成员行、搜索行（97/163） |
| [MenuChars.cs](../MushTown.Client/Screens/MenuChars.cs) | 1 | 选角格里的纸娃娃（67） |
| [HudChat.cs](../MushTown.Client/Panels/HudChat.cs) | 1 | 聊天窗整块（267） |
| [HudGuide.cs](../MushTown.Client/Panels/HudGuide.cs) | 1 | GUIDE 正文（55） |
| [HudSettings.cs](../MushTown.Client/Panels/HudSettings.cs) | 1 | 音量旋钮（119） |
| [HudQuickSlots.cs](../MushTown.Client/Panels/HudQuickSlots.cs) | 1 | 快捷栏格子（38） |

- 新增窗口（星之力、潜能）照这个写法，每个窗口要再写一套画法，不能复用。

### 1.3 世界

- [MushTownGame.cs:679-693](../MushTown.Client/Game/MushTownGame.cs#L679-L693)：怪的帧推进和死亡移除。
- [MushTownGame.cs:694-700](../MushTown.Client/Game/MushTownGame.cs#L694-L700)：伤害数字的计时和回收。
- [MushTownGame.cs:701-722](../MushTown.Client/Game/MushTownGame.cs#L701-L722)：技能特效的播放和回收。
- 对象已经在树上（MobArt、DropArt 都是 Component，[GameMobs.cs:71-77](../MushTown.Client/Game/GameMobs.cs#L71-L77)），但动画、寿命、回收不由对象自己走。
- 数字和特效各有一个嵌套类 + 一个栈池（[GameCombat.cs:19-60](../MushTown.Client/Game/GameCombat.cs#L19-L60)）。
- 伤害皮肤只有一份：[GameCombat.cs:115-120](../MushTown.Client/Game/GameCombat.cs#L115-L120) 的 `LoadDmg` 读 `effect/dmg.json`。
- 结论：每加一种"会自己消失、会播放"的东西，都要在 Update 加一段循环和两个池。

## 2. 目标

- **一个输入入口**：键盘和鼠标每帧只读一次；绑定表按角色走；加技能/新动作不改读取代码。
- **一个窗口写法**：窗口 = 对象树 + 组件。仓库里 `Invoke` 为 0。
- **一个世界写法**：活的东西自己 `Update`，自己标记死亡。游戏只负责建和收。
- **三处按 id 扩展**：怪、皮肤、技能只加数据或注册一行。
- 每一片都能单独交付；中途停下也不留半套。

## 3. 设计

### 3.1 InputController + 每角色绑定

**流程**：每帧读一次设备；控制器把 `键 → 目标` 表变成一组 `BoundAction`（每个目标一个，自带键）；消费者拿 action 问 `Fresh` / `Down`。表最多和键数一样大，和技能总数无关。

**InputController**（`Client/Core/Input.cs`，普通类，不进 Shared、不挂树）：

- 一帧调一次 `Poll(keyboard, mouse)`：读设备，更新原始键边沿；再更新所有 `BoundAction` 的 `Fresh` / `Down`。
- `BoundAction { BindTarget Target; Keys[] Keys; bool Fresh; bool Down; }`：一个目标一个；`Keys` 是它的键或组合键拆开后的段。两段的组合键按**任一段**触发（别名），不是两段同时按。
- `Fresh`：任一键这一帧刚按下（上一帧没按）；`Down`：任一键正按着（跳跃高度要用）。
- `Fresh(Keys)`：原始键查询。菜单、捕获模式用。
- 鼠标：`Mouse`（1366×768 游戏坐标）、`Click`、`Held`、`Wheel`。**K1 只把字段建好**；HUD/聊天/键窗的鼠标读取迁移和 N2 的拖放一起做，K1 的游戏、聊天、键窗仍各自读一次鼠标。
- "激活的绑定表" = 当前角色的 live 表（`KeyPresets[ActivePreset]`）。菜单没有角色，只用原始键，不进绑定。
- 装表：遍历 `键 → 目标`，建 action 组；**不建**"目标 → 键"的反查字典。表最多约 100 条，通常 20-40 条。
- 捕获模式：聊天、创建角色打字、键窗组合键采集时设 `Capture`。捕获时所有 action 的 `Fresh` / `Down` 为 false，原始键照常返回。谁设的谁清。**K1 的旧键窗不用 Capture（旧行为照旧，K2 的新键窗和聊天再用）**。
- 消费者顺序 = 代码顺序。一个目标只能有一个主人；两个地方问同一个目标会双触发，这是约定。

**技能**：只为表里已绑定的技能建 action。技能系统拿着这组 action 循环：

    foreach (var a in boundSkillActions) if (a.Fresh) CastSkill(a.Target);

未绑定的技能不建 action；技能数据几百个，action 组最多和键数一样大。

**技能同帧顺序**：K2 绑技能时按技能窗的固定 id 顺序遍历，不按绑定表的插入顺序；否则同帧两个带 `AttackLock` 的技能谁先放取决于绑的先后。K1 没有技能绑定，循环空转，不用改。

**绑定目标**：`BindTarget = (Kind, Id)`

- `Kind = Ui`：Id 是 `HotkeyAction`。
- `Kind = Skill`：Id 是技能 id。
- `Kind = Item`：Id 是物品 id。
- 存档里目标写成字符串：`"Inventory"`、`"Attack"`、`"Skill:1001005"`、`"Item:2000000"`。
- 表的方向是 **键名 → 目标**：`"D1" → "Item:2000000"`、`"I" → "Inventory"`。键名用游戏枚举名，不用裸数字。
- 组合键的键名是 `"I+K"`，一段或两段；**两段按任一段触发（别名），不是同时按**；一个键最多一个目标，一个目标最多一个键。技能组合技不在这里，见 3.4。

**数据（跟角色走）**：

- `CharaSlot` 新增两个字段：
  - `List<Dictionary<string,string>>? KeyPresets`：3 套**完整配置**，不是备份；每套是 `键名 → 目标`。
  - `int ActivePreset`：当前生效的是第几套（0..2）。
- 删掉 `CharaSlot.QuickSlots`（12 个物品 id 的列表）：药水改存成表中的 `Item` 目标。
- 没有单独的 live 字段：live 表 = `KeyPresets[ActivePreset]`。
- 角色没有 `KeyPresets` 时，3 套都填默认表，`ActivePreset = 0`。
- `data/settings.json` 只留音量等全局项，删掉 `Keys` / `Presets` / `KeyVersion`。
- **不做迁移**（用户 2026-10-26：旧数据不维护，删档重建）。角色没有 `KeyPresets` 时用默认表。
- **默认表（KMS 现行默认，用户 2026-10-26 查证）**：

| 键 | 目标 |
|---|---|
| S | Stat |
| E | Equip（新动作） |
| I | Inventory |
| K | Skill |
| Q | Quest |
| W | WorldMap |
| U | Guide |
| \ | KeyConfig |
| P | Party |
| L | Menu |
| Ctrl | Attack |
| Alt | Jump |
| Space | Talk（和 NPC 对话） |

没有默认键：设置 Settings、小地图 Minimap、聊天 Chat（官方是 Esc / N / C，这批不做）；技能全部不写默认键。

来源：韩国官方指南的默认键盘图；GMS 的对照见 [Grandis Library](https://grandislibrary.com/content/keyboard-shortcuts)（那里 GMS 的快捷键设置是 F11，韩服是 `\`）。我们的数据是 KMST，用韩服这套。

- 进游戏装表，切角色换表；键窗 Save/Apply/Copy 改表后，控制器重建一次内存表（键名只在这一刻拆）。
- `KeyVersion` 不再需要：缺字段用默认，读不认识的键跳过。
- K1 第 1 周的旧键窗只列出 8 个可见动作；`Equip(E)`、`Guide(U)`、`KeyConfig(\)`、攻击/跳跃/Talk 能生效但看不到，也不能改。静默抢键由“一键一目标”保证一致，K2 的新键窗才全部列出。

**动作集**：

- 现有 15 个 UI 动作 + 新增 `Equip`、`Attack`、`Jump`、`Talk`。
- Z 拾取仍是固定键，不在表里（计划外；要进表需加 `Pickup` 动作，数据模型小改，等用户定）。
- 技能目标（`Kind = Skill`）在表里，但没有默认键，键窗也不列。
- 物品目标（`Kind = Item`）在表里：药水就是 `Item(id) → 键`。键名和 `"Item:<id>"` 都能解析，HUD 按表画格子，按 1..= 喝对应的药。
- 一个键最多一个目标，一个目标最多一个键；绑定时把目标从旧键删掉，把键上的旧目标删掉；组合键的每一段都算一个键。
- 不能绑的键：Esc、Enter、Tab、方向键（系统键）、Z（拾取固定，见上）；Space 可以绑（默认 Talk）。
- 键名用游戏里的枚举名（`D1`、`OemPipe`），不用裸数字 `"1"`；裸数字会被 `Enum.TryParse<Keys>` 当成枚举值，绑到错的键。

**HUD 右下 16 个快捷槽（16 个固定键）**：键是 Ctrl、Alt、I、C、1-9、0、-、=。每个槽显示表里这个键绑的目标（官方图标 + 键帽）；没有绑定就空着。默认只有 Ctrl→Attack、Alt→Jump、I→Inventory 有内容；C 槽空着（Stat 默认在 S，S 不在这 16 个键里）；1..= 空着。不另存 `QuickSlots`：药水就是 `Item` 目标，绑定在 1..= 上。16 个格子都按固定键查表：有目标就画它的图标（动作图标用 `StatusBar3.img/KeyConfig/icon`：Attack 52、Jump 53；物品画物品图；技能画技能图），键帽永远画。4 个提示格的键帽在左上（`hintcap`/`hinticon` 两个偏移），12 个物品格的键帽在右下角，和原版一样。格子只显示，点格子不绑定（编辑等 N2 的拖放）。

**键窗（K1 适配，K2 重做）**：

- K1 不改键窗 UI，只加一层适配：`HotkeyKey(action)` / `SaveHotkeyPreset(slot, map)` 读写当前角色的当前 preset；3 个按钮对应 3 套 preset。点 1/2/3 切换选中的 preset 并重读 draft（K1 的预览）；Save 把 draft 写进选中的 preset；Apply 设 `ActivePreset`。K1 的 Save 和 Copy 是同一个操作（K2 把 Copy 换成选来源的弹窗）。Save/Apply/Copy 都会存角色（位置也存，和退出时一样：只写 preset 也要重写整个存档文件）。没有角色（--host/--join）时 Save/Apply/Copy 不显示。K1 的键窗只有 8 个动作：被隐藏动作（Attack、Jump、Talk、Equip、Guide、KeyConfig）占着的键不能被抢，否则那个动作永久丢键；技能和物品的键到 K2 的新键窗再保护。K2 新键窗放开。
- 快捷槽页整个删了（用户 2026-10-27 验收时决定）：Quick Slots 按钮只画 `keys_qslot_n.png`，点了不动，不切页。`CustomDefaultKeyConfig` 的美术不再导出。
- K2 才做下面的新交互。

- 键格是固定的（官方键盘图），玩家把动作从下面的池子拖到键上；不是"点一个格子再按键"。
- 动作池 = **所有系统动作**：面板动作、攻击、跳跃、装备、向导、快捷键、设置、MENU、聊天……（过滤分类照旧）。技能不在这里，技能从技能窗拖。Chat、Minimap、GuideCard 也要在 `UpdateHud` 里各有打开逻辑；Minimap 是常显窗口，只能 `ShowWindow`。
- 快捷槽页（两行、每行 16 个固定键格；第一行 qwer1234567890up，第二行 asdf ctrl alt zxcvijklmn）属于以后"快捷槽可编辑"那片，K2 不做（页已删，要重做时照 `CustomDefaultKeyConfig` 重建）。
- **3 个 preset 是三套完整配置**（用户 2026-10-26，参考官方 UI）：
  - 打开：选中 `ActivePreset`，draft = 它的拷贝。
  - 点 1/2/3：**预览**那一套（draft 换成它），不生效。
  - Save：draft 写进当前选中的 preset；如果它就是 `ActivePreset`，live 立即跟着变。
  - Apply：`ActivePreset` = 当前选中的 preset，live 换成它。
  - Copy：弹窗选来源 preset，覆盖**当前选中的 preset**（连同 draft；如果当前是 active，live 跟着变），并立即存角色。
  - Reset：draft 回到默认表；Save 才写。
  - Cancel / X：丢掉 draft，不改 preset。
- 拖放：从动作池拖到键盘键格 = 改 draft。拖到 HUD 的 16 个快捷槽**放到 N2**（槽先合成一套再接受拖放）。
- 键帽照旧用 `CapArt`；UI 动作画现有图标。

**删除**：

- `Hotkeys.cs` 的 `_hotkeys` / `_hotkeyPresets` / `FreshHotkey` / `SeedHotkeys` / 存取方法删掉；绑定改成角色的 3 套 preset；`settings.json` 的 Keys/Presets/KeyVersion 字段删掉。
- 16 个 `_xWasDown`、13 次 `FreshKey`、技能 if 分支（进 `CastSkill`）；`SeedHotkeys` 换成控制器的 `Seed(kb)`（进游戏时灌一次上一帧集合）。
- 16 个槽键的常量（4 + 12）留下，它们现在是查表的键；`QuickSlots` 物品列表、`QuickSlotKey` 拦截（1..= 现在是正常的可绑键）、`ClickQuickSlot`（点格子绑定物品）、`SeedGameKeys` 都删了；喝药改成 `Item` 目标的消费者（`ItemEdges`）。
- [Menu.cs:235](../MushTown.Client/Screens/Menu.cs#L235) 的 demo `QuickSlots` 种子删掉：两瓶药改成 preset 1 的 `D1`/`D3` 两条 `Item:` 绑定（截图仍能看到两个格子），preset 2/3 是默认表。

### 3.2 窗口 = 对象树 + 组件

**原则**：组件只做一件事。分三层：

1. **通用组件**（`Ui/Widgets.cs` 追加，**用到才加**）：
   - `Box`（九宫格框，包 `NineSlice.Draw`，**已加**）：小地图框（`mini_frame`）和装备页的物品详情框（`tip`）已经用它；HUD 跟随鼠标的 tip、气泡、buff 框、聊天框还在自己的画法里直接调 `NineSlice.Draw`，等它们组件化时再换。
   - `Image(stem, on)`（已加，N5 用）：`on` 为真时常亮成 `_p`，预设钮和 Combo Key 钮用它。
   - 聊天片（N3）：`CaretText`（输入文字 + 光标闪烁）、`WrapText`（按宽度折行，复用 `UiText.Wrap`）。
   - 任务片（N6）：`TextLines`（多行文字，每行可不同颜色）。
   - GUIDE 片（N7）：`ClipPlayer`（序列帧播放，喂 `UiClip`）。
   - 现有 `Picture / Label / LiveLabel / Bar / Image / Round / Fill / InvSlot` 继续用。
2. **共享画法**（`Ui/AvatarArt.cs`，**已建**）：`MergeParts` / `DrawAvatar` / `DrawAvatarOnScreen`（把相机压到 0,0 再画，UI 上的娃娃都是屏幕像素）+ `AvatarPreview` 组件（owner 矩形的左上角 = 脚）。创建角色预览已用它；Character Info / Equip 里的娃娃等那个窗加 `dollX/dollY` 布局键再接一行（那时 `AvatarPreview` 才需要 `worn` 参数）。选角格一页 12 个娃娃和石柱/传送门交织，留给 N8。
3. **专用组件**：放窗口自己的文件，不做成通用件。例如 `MinimapCanvas`（HudMap）、`KeyCap`（HudKeyConfig）、`ChatLog`（HudChat）、`Balloon`（HudBalloon）。
   - 专用组件清单（按窗口）：HUD 条 4 个、快捷栏 1 个、设置旋钮 1 个、聊天 3 个、小地图 1 个、大地图 1 个、键窗 5 个、任务 3 个、组队 2 个、GUIDE 1 个、选角/创建 3 个。
   - 不做"万能组件"：两个窗口画法不同就各留一个。

**顺序**：一片一个窗口，每片截图逐字节对比。

**结束条件**：全仓 `grep "new Invoke"` = 0，删掉 `Invoke` 类和它的注释。

### 3.3 世界对象自己走

**新增**：`GameObject.Dead`。`TreeUpdate` 跳过 `Dead` 和它的子树。

**每类一个组件，自己 `Update`**：

| 对象 | 现在的循环 | 改后 |
|---|---|---|
| 怪 | MushTownGame 679-693 | `MobArt.Update`：推帧；`die1` 播完标 `Dead` |
| 传送门 | MushTownGame 674-676 | `PortalComp.Update` |
| 数字 | 694-700 | `FloatingNumber.Update`：计龄，到时标 `Dead` |
| 特效 | 701-722 | `SkillFx.Update`：推帧，播完标 `Dead`；buff 循环挂在玩家身上，由 buff 结束移除 |

**回收**：`TreeUpdate` 之后，游戏扫自己的容器（`_mobs` / `_drops` / `_numbers` / `_fx`），把 `Dead` 的摘掉、进池。O(n)，n < 几百。数字和特效继续用两个 `Stack`；怪和掉落不池（Q11 已定：只池高频小东西）。

**树的顺序 = 现在 Update 的执行顺序**：玩家 → 地图家具 → 怪 → 掉落 → 数字 → 特效 → 音频。写在建树处，一行注释说明为什么是这个顺序。

**相位不变**：S6 当时把怪的 tick 留在原位，是为了不动"施法前 / 排空后"的相位。这次先做一次自检：同一帧里怪、数字、特效的先后和现在一致；再改。

**相机不搬（S4 砍掉）**：用户 2026-10-26 定。相机是单例，`Follow` 必须在玩家移动之后；现在的调用位置已经是确定顺序。屏幕震动、缩放、过场、边界都是相机内部的状态，不是树对象。S8 当时判"搬进树没收益"，不再翻案。

**验证**：`--shot` 不跑 Update，看不出动画。用 `--aiticks` 截图 + 新增头测（帧推进、播完回收、buff 循环跟人）。

### 3.4 扩展点

- **怪**：数据驱动，几种怪就几个 json。不做池。纹理缓存现在是永不清（[GameRender.cs:178-188](../MushTown.Client/Game/GameRender.cs#L178-L188)），几百张图会吃内存 → 这次不做，记触发条件：单图怪贴图占用明显变大时，单开"按图卸载"计划。
- **伤害皮肤**：`_dmg`（一份）改成按 key 的字典；数字对象存皮肤 key，默认 0。几百种皮肤只加数据 + 导出。选择皮肤的设置 UI 这次不做。
- **技能**：`CastSkill(id)` + 覆盖表；特效按 `SkillData.Effect/Hit/Affected` 的 key 读；几百个技能不改循环。
- **技能的改键 UI**：后期整体重做。技能、消耗品、椅子、面板按钮都从各自窗口拖到快捷键，统一设计。这次只把数据结构和读取路径铺好，**不初始化技能键**。
- **技能组合技**：不做，留给技能大改。战神的"上/下 + 攻击"、"右 + 右"、"先趴下再攻击出巨熊"都归那一层。它拿控制器给的原始方向键和动作边，记几百毫秒的输入历史：攻击按下时看当前按住的方向和刚按过的方向，匹配到组合技就放组合技，否则普攻；一次攻击只出一个技能。单键（趴下）是移动键，按下立即生效，不等攻击。K1 只保证原始键可读、动作有 `Fresh`/`Down`、一帧能触发多个动作。
- **窗口**：新窗口（星之力、潜能）用 3.2 的组件，缺什么加什么专用组件，不写 `Invoke`。

### 3.5 `Rect` 改成父相对（W1，用户 2026-10-27 定）

**现状**：`Rect : Component` 存的是**绝对屏幕矩形**（`Rectangle Box`）。于是：

- 窗口一移动就是 `Pos` 变 → `UiWindow.Sync()` 调 `Build()` 把整棵树重建一次（拖标题栏时每帧重建几十个节点）。
- 子节点不能跟着父节点走，窗口之间也不能嵌：娃娃预览塞进 Character Info，得自己算绝对坐标。
- 布局文件（`layouts/*.json`）里的数字本来就是窗口局部的，代码却在每处 `Pos.X + x` 加回来：10 个窗口文件、`Pos.X +` 48 处、`Pos.Y +` 51 处。

**改法**（照 Unity 的 `RectTransform`，只取有用的部分）：

1. `Rect` 只存局部 `Offset` + `Size`（相对父节点；窗口的根节点原点 = 窗口 `Pos`，菜单/HUD 的根 = 屏幕原点）。
2. 每帧开头 `Scene.Layout()` 走一次树，把算好的绝对屏幕矩形写进 `Rect.Screen`。命中（`Scene.TopAt`）、绘制（`TreeDraw.DrawObject`）、HUD 的悬停和格子查找（`UpdateHudHover`、`QslotCell`、键窗的 `KeyRect`/`CellRect`）统一读 `Screen`，不再各算一份。
3. `UiWindow.Sync()` 只在 `Dirty` 时重建；`Pos` 变化只改根节点偏移，子节点自动跟。
4. `Pos.X +` 48 处、`Pos.Y +` 51 处变回纯偏移；115 处 `new Rect(...)` 基本不动，只是不再加 `Pos`。
5. `Picture(key, dx, dy)`、`Label`、`Image` 这类"owner 原点 + 偏移"的写法不变，原点由遍历给出。

**不做**：anchors、pivot、layout group、缩放。画布固定 1366×768、布局数字手调，这些用不上。

**位置**：K2 之后、N1 之前。W1 要改每个窗口的坐标写法，先改完，后面的窗口片（N1/N2/N3/N6/N7/N8）就只写一遍新写法。

**验收**：全部 `--shot` 屏幕（27 个）逐字节一致；拖标题栏、拖任务窗时树不重建（临时在 `Build()` 里加计数验证后删掉）；`grep "Pos.X +"` 只剩"根就是屏幕"的菜单/HUD，或为 0。

**风险**：半改造会让所有窗口坐标错位 → 一片一次做完、一个 commit；命中测试要走父链，`Hovered` 的逻辑不变。

## 4. 分片

工期按每周约 10 小时估。每周一个 commit（用户规矩）；2 周的片第 1 周提交能 build + test 的中间态，第 2 周完成；每片结束停下验收，先 review 后提交。

### 输入线

| 片 | 内容 | 工期 |
|---|---|---|
| K1 (P0) | **第 1 周**：`InputController` + `BoundAction`；绑定进角色（3 套 `KeyPresets` + `ActivePreset`）；KMS 默认键（10 个菜单动作 + 攻击/跳跃/Talk）；技能进表但**无默认键**；`CastSkill`；旧键窗适配到当前 preset（8 个可见动作）；技能窗去掉固定键文字（旧 Q/R/W… 已失效）。**第 2 周**（已做）：HUD 16 槽改成键槽（C 空）；Item 目标；删 `QuickSlots`/`QuickSlotKey`/demo QuickSlots 种子；旧键窗的快捷槽页整个删掉，Quick Slots 按钮只画图、点了不动 | 2 周 |
| K2 (P0) | 键窗：每角色 preset、动作池 = 所有系统动作（含攻击/跳跃）、组件化（与 N5 合并）；键格固定、动作拖动；拖到 HUD 槽随 N2；不做快捷槽页 | 2 周 |

### 世界线

| 片 | 内容 | 工期 |
|---|---|---|
| S1 (P0) | `Dead` + 回收 + 怪动画进 `MobArt.Update` + 传送门 | 1 周 |
| S2 (P0) | 数字变成对象 + 皮肤字典（默认 1 种） | 1 周 |
| S3 (P0) | 特效变成对象 + buff 循环挂玩家 | 1 周 |

### 窗口线

| 片 | 内容 | 工期 |
|---|---|---|
| N0 (P0) | `Box` + `Ui/AvatarArt.cs` + `AvatarPreview`：**已做**（`Box` 用在小地图框和物品 tip 框；`AvatarPreview` 用在创建角色预览；`MergeParts`/`DrawAvatar` 搬进新文件并加了 `DrawAvatarOnScreen`） | 0.5 周 |
| N5 (P0) | 键窗 7 处 `Invoke` 已删（和 K2 合并）：`KeyCap` / `ActionCell` / `FilterBox` / `FilterItem` / `HeldIcon` 五个窗口内私有组件，预设钮和 Combo Key 钮改用 `Image(stem, on)`；拖放松手改由 `UpdateHud` 的鼠标边沿驱动（`KeyConfigWindow.UpdateDrag`），Draw 不再轮询鼠标 | 已做 |
| W1 (P0) | **`Rect` 改成父相对（Unity RectTransform 的形状）**：`Rect` 只存局部 offset + size；每帧开头 `Scene.Layout()` 走一次树把屏幕矩形写进 `Rect.Screen`；命中、绘制、HUD 悬停都读 `Screen`；`UiWindow.Sync()` 不再因为 `Pos` 变化重建；48 处 `Pos.X +`、51 处 `Pos.Y +` 变回纯偏移（10 个窗口文件）。不做 anchors / pivot / layout group。详见 §3.5 | 1 周 |
| N1 (P1) | HUD 条：气泡、快捷栏角标、手拿、tips | 1 周 |
| N2 (P1) | 快捷槽：4 个键提示 + 12 个物品格合成一套 16 个等价槽（内容先固定）+ 设置旋钮 | 1 周 |
| N3 (P1) | 聊天窗（整块，最大） | 2 周 |
| N4 (P1) | 小地图 + 大地图 | 2 周 |
| N6 (P2) | 任务窗 | 1 周 |
| N7 (P2) | 组队 + GUIDE | 1.5 周 |
| N8 (P2) | 选角 + 创建角色 | 2 周 |

合计约 19 周（N0 缩到 0.5 周，S4 砍掉，W1 加 1 周）。三块互不依赖，顺序可以换。

**顺序（我定的）**：K1 → N0 → K2+N5 → W1 → S1 → S2 → S3 → N1 → N2 → N3 → N4 → N6 → N7 → N8。

理由：先把输入这条线走完（技能解耦 + 每角色绑定是一件事）；再做组件基础，紧接着把键窗改组件；然后**先把 `Rect` 改成父相对**，再动世界和窗口——W1 要改每个窗口的坐标写法，放在 N1/N2/N3/N6/N7/N8 之前，那些窗口就只写一次新写法。

## 5. 每片的验收

1. `dotnet format` 干净；`dotnet build` 0 警告 0 错误。
2. 测试全过（K1 新增 16 条；当前 319）。
3. 受影响的 `--shot` 截图与改动前逐字节一致；**例外**：输入片换了默认键（Stat C→S、Quest V→Q 等），**所有带 HUD 的游戏截图**（第 4 个提示格、MENU 角标）、键窗和**技能窗**（固定键文字已删）按新默认重拍，人工对照官方布局验收。
4. 世界片：`--aiticks 40` 截图一致；新增头测覆盖"推帧 / 播完 / 回收"。
5. 输入片第 1 周：手测 10 个菜单键、攻击、跳跃、Talk；换角色后按键跟着换；旧键窗的 Save/Apply/Copy/Default 在 3 套 preset 上生效（1/2/3 切换选中的 preset 并重读 draft，键格跟着换；新 UI 的预览是 K2）；`settings.json` 没有按键字段。
6. 输入片第 2 周：3 套 preset 预览 / Apply / Copy / Save 后重开仍生效；HUD 16 槽按表重画（Ctrl/Alt/I 有内容，C 空，12 个物品格按表）。技能头测（塞一条合成绑定，验证能触发 `CastSkill`）随 K2 的拖放和绑定一起做；实际游玩要等拖放片，这是已知代价。
7. 代码检查：`grep "new Invoke"` 该文件为 0；**这 16 个**游戏输入字段为 0（`_altWasDown`、`_upWasDown`、`_ctrlWasDown` + 12 个技能键 + `_zWasDown` 拾取）；菜单的 `_escWasDown`、`_menuUpWasDown` 等系统键字段保留（不是绑定键）；`_qsWasDown` 已随快捷栏删掉；`settings.json` 里没有按键。
8. 窗口片（N5 已做）：`grep "new Invoke" MushTown.Client/Panels/HudKeyConfig.cs` = 0；`--shot keys` 和改动前逐字节一致（组件化不许改像素）；手动拖一次动作到键上，图标跟手、松手落在那个键、拖出键盘外把键清掉。
9. N0（已做）：`--shot chars/chars2/setup/equip/minimap/hud` 与改动前逐字节一致（`Box` 的 `mini_frame` 由 minimap 覆盖、`tip` 由 equip 页的物品详情框覆盖；`AvatarPreview` 由 setup 覆盖；`DrawAvatar` 由 hud 和 chars 覆盖）。
10. W1（`Rect` 父相对）：全部 `--shot` 屏幕（27 个）逐字节一致；拖标题栏、拖任务窗不触发 `Build()`（临时计数验证后删）；`grep "Pos.X +"` 为 0 或只剩菜单/HUD 这类"根就是屏幕"的地方；那一帧的节点遍历只跑一次（`Scene.Layout`）。

## 6. 风险

| 风险 | 对策 |
|---|---|
| 输入一次改全，所有面板键都可能坏 | K1 只换读取，分发逻辑照旧；截图 + 手测逐键过 |
| 老存档没有绑定字段 | 缺字段 = 默认表；用户已说明旧档删掉重建，不做迁移 |
| 技能在拖放重做前不可用 | 用户在计划里接受；K1 只保证 `CastSkill` 的 id 校验和旧分支不变，实际触发等 K2 绑定 |
| 默认键改了，截图基线变 | 输入片的**所有带 HUD 的游戏截图**、键窗和技能窗明确重拍；其余截图仍逐字节比 |
| 键窗拖到 HUD 槽要跨两套槽 | 排到 N2：先把 4+12 合成 16 个等价槽，再接拖放 |
| 截图模式没有真实角色 | 截图用 demo 角色，也要灌默认表；`--shot keys` 走 `Open()`，能开能画就是自检 |
| 键窗拖放逻辑复杂（组合键、拿走） | K2 保留现有路径，只换目标类型；手测覆盖 |
| 组件化改变像素 | 每片一张截图逐字节比；不顺手改数字 |
| 世界相位变化 | 固定树序 + aiticks 对比 + 头测 |
| 对象数增加（数字、特效各一个对象） | 目标 < 1000 个；池复用；树遍历 O(n) |
| 19 周太长，中途插内容块 | 每片独立可交付；P2 可砍；每周一个 commit |

## 7. 不做（以及两个方向的说明）

已定不做：菜单屏对象化、世界 y 排序搬树。

### 7.1 菜单 5 屏改成 Scene 对象（讲解）

**现在**：一个枚举 `Screen`（[Menu.cs:14](../MushTown.Client/Screens/Menu.cs#L14)），两个 switch：

- [Menu.cs:602](../MushTown.Client/Screens/Menu.cs#L602)：按屏调 `UpdateSaves` / `UpdateServer` / ...
- [Menu.cs:642](../MushTown.Client/Screens/Menu.cs#L642)：按屏调 `BuildSavesScene` / ...

每个屏的代码在自己的文件里，但"现在哪个屏"是枚举值，切换靠 switch。

**改成对象**：定义一个 `MenuScreen` 基类（Build / Update / Enter / Leave），5 个屏各一个类；`SceneManager` 存当前屏；切屏 = `_scenes.Show(new CharsScreen(...))`。

**好处**：加屏不改枚举和 switch；每屏状态自己收着。**坏处**：这 5 个屏是官方固定集合；它们共用存档、光标、错误文字、hover id 等一堆状态，拆对象要传共享 context，代码不会变少；计划已经定了保留 switch。

**结论**：不做。以后真加第 6 个屏再谈。

### 7.2 世界层 y 排序搬进树（讲解）

**现在**：画世界时每帧建一个临时列表（[GameRender.cs:23-57](../MushTown.Client/Game/GameRender.cs#L23-L57)）：玩家、其他玩家、怪、掉落、NPC 的"脚底 Y + 画法"塞进去，`OrderBy(Y)` 后依次画。树只说明"谁存在"，不决定画的顺序。

**搬进树**：给世界对象一个每帧算的 Z（Z = 脚底 Y），让根列表按 Z 排；或者每帧按 Y 重排孩子。这样画的时候只走树。

**好处**：只有一套顺序。**坏处**：每帧重排树或每帧改 Z；Y 相同时的先后（玩家 → 其他玩家 → 怪 → 掉落 → NPC）要另外记住；命中测试也会受影响；现在这个列表只有几十项，排序成本可忽略。

**结论**：不动。树只负责 Update 顺序，画法保持现在的 Y 排序列表。

## 8. 已定（2026-10-26，用户）

1. 攻击 / 跳跃进绑定表。
2. 绑定跟角色走，3 套完整 preset；`ActivePreset` 决定生效的那套。Save 写当前 preset，点 1/2/3 是预览，Apply 才切换生效，Copy 覆盖当前 preset。
3. 键窗不列技能；技能改键属于后期整体重做（技能/消耗品/椅子/面板按钮拖到快捷键）。
4. HUD 右下 16 个快捷槽**完全等同**：可以放面板动作、技能、消耗品、椅子。这批先固定内容和键（Ctrl 攻击、Alt 跳跃、I 背包、C 空着 + 12 个物品格 1-9/0/-/=），不做编辑；以后要支持编辑。
5. 分片顺序由代理定：K1 → N0 → K2+N5 → S1 → S2 → S3 → N1..N8。
6. 菜单屏不重构；世界层 y 排序不改。
7. 技能进绑定表，但**不做默认键、不初始化数据**，由玩家以后自己定义；接受"拖放重做前技能不可用"的空窗。
8. 旧存档/设置不迁移，删档重建。
9. 快捷菜单的默认键用 KMS 现行默认：Stat S、Equip E、Inventory I、Skill K、Quest Q、WorldMap W、Guide U、KeyConfig \、Party P、Menu L；攻击 Ctrl、跳跃 Alt。Settings / Minimap / Chat 默认无键。
10. 绑定表方向是**键 → 目标**（`"1" → "Item:2000000"`）；一个键最多一个目标，一个目标最多一个键。药水不单独存，改成 `Item` 目标进表。
11. 键窗的键格固定，玩家把动作拖到键上；动作池提供**所有系统动作**（含攻击/跳跃）。不是"点格再按键"。
12. 快捷槽页的 32 个固定键格：第一行 qwer1234567890up，第二行 asdf ctrl alt zxcvijklmn。以后做可编辑快捷槽时才实现。
13. 键窗拖动作到 HUD 16 槽，放到 N2（先把槽合成一套）。
14. 表的大小 = 键数（最多约 100 条）；只为已绑定的目标建 `BoundAction`，技能数据几百个不进表。
15. Space 可以绑（默认 Talk）；Esc、Enter、Tab、方向键不能绑。
16. K1 就把 HUD 16 槽改成键槽：4 个提示格 + 12 个物品格都按固定键查表；C 空着。
17. N0 已做（用户 2026-10-27 改定）：`Box` + `Ui/AvatarArt.cs` + `AvatarPreview`。`TextLines` / `WrapText` / `CaretText` / `ClipPlayer` 仍在第一个用它的窗口片加。
18. S4 砍掉：相机不搬进树。
19. K1 旧键窗继续能用，只把读写指向当前 preset；新 UI（拖放、Copy 弹窗、预览）在 K2。
20. 同一帧按下多个绑定键，动作**都触发**（用户 2026-10-26：很常见）。
21. `I+K` 是**别名**（按任一个都触发），不是两个键一起按；技能组合技不进绑定表。
22. 技能组合技（上/下+攻击、右+右、单键先）留给技能大改，K1 不做；K1 只提供原始键、动作边、一帧多动作。
