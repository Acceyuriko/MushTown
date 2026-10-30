# 扩展性重构 — 2026-10-26

状态：K1 第 1、2 周已验收并提交；N5 键窗组件化已验收并提交；N0（`Box` + `Ui/AvatarArt.cs` + `AvatarPreview`）已验收并提交（6c521cc）；K2 已验收并提交（5348adb，两轮审查修了 5 个 P0 和一批 P1/P2）；W1（`Rect` 父相对，§3.5）已验收并提交（8b86b4a，5 轮审查，27 张 `--shot` 一致，客户端测试 32）；S1（世界对象自己走）已验收并提交（60fda5e）；S2（数字变成对象 + 皮肤字典）已验收并提交（1e182d0，两轮审查，测试 45）；S3（特效变成对象 + buff 循环挂玩家）已验收：27 张 `--shot` + 6 张 `--aiticks` + `--hit`/`--skill` 各 1 张全部逐字节一致，客户端测试 49。同一批还带一条输入修复：键窗不再挡任何东西（角色动作、面板键、喝药全照常，§8 #51）。**待提交。** 下一步 N1（背包）。按片做，每片 1-2 周，一次验收。

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

- **动作池 = 官方 `StatusBar3.img/KeyConfig/icon` 的全部 77 张图（2026-10-28 用户定）**：每张图一个格子，顺序按图标编号（0、1、2、3、4、5、6、7、8、9、10…300）。已实现的动作照旧开窗口；**没实现的动作（好友、公会、成就、现金商店、勋章、BOSS、战斗分析、怪物收藏、灵魂武器、换频道、截图、坐下、回城、仓库、表情 1-7、安全模式、静音、角色预设…）也能拖到键上，按了什么都不发生**——池子同时就是"还差什么功能"的清单，测试时一看就知道。
- 没实现的动作没有 `HandleHotkey` 分支，也不进 `UiHotkeyOrder`，所以按键连分发都不会进，副作用为零。
- **不进池**：`GuideCard`、`PartySearch`、`PartyAsk`（子窗口/服务端事件，官方图里没有它们自己的图标）；`Settings` 也不进池——官方没有"选项"这张图，选项是 MENU 窗口里的一行，没有默认键。
- Z 拾取是池里的 `Pickup` 动作（默认键 Z，2026-10-28 用户定）：`IsBindable` 不再排除 Z，拾取读 `_input.Fresh(HotkeyAction.Pickup)`。
- 技能目标（`Kind = Skill`）在表里，但没有默认键，从技能窗拖进来。
- 物品目标（`Kind = Item`）在表里：药水就是 `Item(id) → 键`。键名和 `"Item:<id>"` 都能解析，HUD 按表画格子，按 1..= 喝对应的药。
- 一个键最多一个目标，一个目标最多一个键；绑定时把目标从旧键删掉，把键上的旧目标删掉；组合键的每一段都算一个键。
- 不能绑的键：Esc、Enter、Tab、方向键（系统键）；Space 可以绑（默认 Talk），Z 也可以绑（默认 Pickup）。
- 键名用游戏里的枚举名（`D1`、`OemPipe`），不用裸数字 `"1"`；裸数字会被 `Enum.TryParse<Keys>` 当成枚举值，绑到错的键。

**HUD 右下 16 个快捷槽（16 个固定键）**：键是 Ctrl、Alt、I、C、1-9、0、-、=。每个槽显示表里这个键绑的目标（官方图标 + 键帽）；没有绑定就空着。默认只有 Ctrl→Attack、Alt→Jump、I→Inventory 有内容；C 槽空着（Stat 默认在 S，S 不在这 16 个键里）；1..= 空着。不另存 `QuickSlots`：药水就是 `Item` 目标，绑定在 1..= 上。16 个格子都按固定键查表：有目标就画它的图标（动作图标用 `StatusBar3.img/KeyConfig/icon`：Attack 52、Jump 53；物品画物品图；技能画技能图），键帽永远画。4 个提示格的键帽在左上（`hintcap`/`hinticon` 两个偏移），12 个物品格的键帽在右下角，和原版一样。格子只显示，点格子不绑定（编辑等 N2 的拖放）。

**键窗（K1 适配，K2 重做）**：

- K1 不改键窗 UI，只加一层适配：`HotkeyKey(action)` / `SaveHotkeyPreset(slot, map)` 读写当前角色的当前 preset；3 个按钮对应 3 套 preset。点 1/2/3 切换选中的 preset 并重读 draft（K1 的预览）；Save 把 draft 写进选中的 preset；Apply 设 `ActivePreset`。K1 的 Save 和 Copy 是同一个操作（K2 把 Copy 换成选来源的弹窗）。Save/Apply/Copy 都会存角色（位置也存，和退出时一样：只写 preset 也要重写整个存档文件）。没有角色（--host/--join）时 Save/Apply/Copy 不显示。K1 的键窗只有 8 个动作：被隐藏动作（Attack、Jump、Talk、Equip、Guide、KeyConfig）占着的键不能被抢，否则那个动作永久丢键；技能和物品的键到 K2 的新键窗再保护。K2 的动作池已把 16 个动作都列出来，`Reserved` 保护已删：技能和物品的键在 Save 时让出（`KeyBindings.RemoveKeyPart`）。
- 快捷槽页整个删了（用户 2026-10-27 验收时决定）：Quick Slots 按钮只画 `keys_qslot_n.png`，点了不动，不切页。`CustomDefaultKeyConfig` 的美术不再导出。
- K2 才做下面的新交互。

- 键格是固定的（官方键盘图），玩家把动作从下面的池子拖到键上；不是"点一个格子再按键"。
- 动作池 = 官方 `StatusBar3.img/KeyConfig/icon` 的全部 77 张图（见下面的动作集）。技能不在这里，技能从技能窗拖。Chat、Minimap 在 `UpdateHud` 里各有打开逻辑；Minimap 是常显窗口，只能 `ShowWindow`（它没有关闭路径，按了只是重建一次）。
- 快捷槽页（两行、每行 16 个固定键格；第一行 qwer1234567890up，第二行 asdf ctrl alt zxcvijklmn）属于以后"快捷槽可编辑"那片，K2 不做（页已删，要重做时照 `CustomDefaultKeyConfig` 重建）。
- **3 个 preset 是三套完整配置**（用户 2026-10-26，参考官方 UI）：
  - 打开：选中 `ActivePreset`，draft = 它的拷贝。
  - 点 1/2/3：**预览**那一套（draft 换成它），不生效。
  - Save：draft 写进当前选中的 preset；如果它就是 `ActivePreset`，live 立即跟着变。
  - Apply：`ActivePreset` = 当前选中的 preset，live 换成它。
  - Copy：弹窗选来源 preset，覆盖**当前选中的 preset**（连同 draft；如果当前是 active，live 跟着变），并立即存角色。
  - Reset：draft 回到默认表；Save 才写。
  - Cancel / X：丢掉 draft，不改 preset。
- 拖放：现在（K2）是"按住拖、松手放"；用户 2026-10-28 改成**点击式**（单击拿起、再单击放下），归 K3，见 §3.6。三个可抓区（白色键盘、黑色动作池、HUD 16 槽）和三个可放区（白/槽 = 绑定，黑 = 取消）见 §3.6。放到 HUD 的 16 个快捷槽要等 N2（槽先合成一套）。
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
   - `Image(stem, on, onSuffix)`（已加，N5 用）：`on` 为真时换成 `stem + onSuffix`，默认后缀 `_p`；预设钮和 Combo Key 钮用它，PRESETS 1/2/3 传 `_c` 画绿勾。
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

**新增**：`GameObject.Dead`。`TreeUpdate` 跳过 `Dead` 和它的子树（结果段把绘制、命中、模态三条路也补上了，以结果段为准）。

**每类一个组件，自己 `Update`**：

| 对象 | 现在的循环 | 改后 |
|---|---|---|
| 怪 | MushTownGame 679-693 | `MobArt.Update`：推帧；`die1` 播完标 `Dead` |
| 传送门 | MushTownGame 674-676 | `PortalComp.Update` |
| 数字 | 694-700 | `FloatingNumber.Update`：计龄，到时标 `Dead`（已做） |
| 特效 | 701-722 | `SkillFx.Update`：推帧，播完标 `Dead`；buff 循环挂在玩家身上，由 buff 结束移除（已做） |

**回收**：`TreeUpdate` 之后，游戏扫自己的容器（`_mobs` / `_drops` / `_numbers` / `_fx`），把 `Dead` 的摘掉、进池。O(n)，n < 几百。数字和特效继续用两个 `Stack`；怪和掉落不池（Q11 已定：只池高频小东西）。

**树的顺序 = 现在 Update 的执行顺序**：玩家 → 地图家具 → 怪 → 掉落 → 数字 → 特效 → 音频。写在建树处，一行注释说明为什么是这个顺序。

**相位不变**：S6 当时把怪的 tick 留在原位，是为了不动"施法前 / 排空后"的相位。这次先做一次自检：同一帧里怪、数字、特效的先后和现在一致；再改。

**相机不搬（S4 砍掉）**：用户 2026-10-26 定。相机是单例，`Follow` 必须在玩家移动之后；现在的调用位置已经是确定顺序。屏幕震动、缩放、过场、边界都是相机内部的状态，不是树对象。S8 当时判"搬进树没收益"，不再翻案。

**验证**：`--shot` 不跑 Update，看不出动画。用 `--aiticks` 截图 + 新增头测（帧推进、播完回收、buff 循环跟人）。

**结果（S1，2026-10-29）**：已验收并提交（60fda5e）。

- `GameObject.Dead`；`TreeUpdate`、`TreeDraw`、`Scene.Hit`、`Scene.AnyModal` 都跳过死节点连同子树——死节点等于不存在，四条路语义一致，以后哪个对象改成走树画不用再想这件事。
- `MobArt` 拿 `Data` + 当前动作的 `Frames`，`SetAction` 一次换四样（动作名、帧表、帧号和计时器），两个字段的 setter 私有。`Update` 推帧，闪烁结束回 idle（保留溢出时间，和旧循环一样），`die1` 播完置 `Owner.Dead`。
- `MobArt.CollectDead(mobs, world)` 摘树 + 删 dict，游戏在 `TreeUpdate` 之后、`Draw` 之前调一次。**这一步不能漏也不能挪**：尸体停在 `Frame == Frames.Count`，漏了或挪到绘制之后，第一次击杀的那一帧绘制就越界。掉 `_mobs.Keys.ToList()` 的分配。
- `PortalComp.Update` 自己循环；主循环里那两段（传送门、怪）删掉。`_portals` 列表只剩绘制用。
- 相位自检：怪的 tick 从「travel 之后」提前到 `TreeUpdate` 里（travel 之前），数字/特效仍在后面；相对次序 怪 → 数字 → 特效 不变。地图切换那一帧，新图的怪和传送门第一次 tick 晚一帧（旧代码是先切图再 tick），看不出来。
- 时间源：`Component.Update` 收秒，怪按 `dt * 1000f` 算毫秒；旧循环用的是 `TotalMilliseconds`，两者差 2e-6 ms/帧，累计到 100ms 边界会早一帧翻页。截图不跑 Update，看不出来。
- 头测 9 条：推帧、闪烁回 idle（含帧表跟着换）、无 stand 回 move、播完标 Dead、死节点跳过子树、收尸（树和 dict 都少一个）、传送门循环（`WorldTests` 7 条）+ 命中跳过死节点、死模态不再吞点击（`SceneTests` 2 条）。客户端 32 → 41。
- 截图证明不了动画推进和回收（截图不跑 `Update`），那两条靠头测 + 一次手动击杀验收。

**结果（S2，2026-10-29）**：已验收并提交（1e182d0）。

- `FloatingNumber : Component`（`World/FloatingNumber.cs`）带 `Text` / `Skin` / `X` / `Y` / `Age`，`Update` 只做一件事：加龄，到 `LifeMs`（800ms）标 `Owner.Dead`。上升高度 `RisePx`（60）也归它，绘制读这两个常量，主循环里那两个常量删掉。
- 数字真正变成树上的节点：`TakeNumber` 从池里取组件（空池就 `new GameObject().Add(new FloatingNumber())`），`Reset()` 清 `Age`/`Skin` 并把 `Dead` 归 false，再 `_world.AddChild` **并 `_numbers.Add`**——进树和进列表是同一步，新的生成点不会只做一半。池子复用节点，不新建节点。
- `FloatingNumber.CollectDead(numbers, world, pool)` 在 `TreeUpdate` 之后和 `MobArt.CollectDead` 并排调：摘树、进池、出列表。`FloatingNumber.DropAll(numbers, world, pool)` 管换图清场，同样三样都做。两个都做成 public static 是为了能头测（理由同 §8 #41）。
- **换图清场必须摘树**：以前池里只有裸对象，现在节点还在树上，漏了 `RemoveChild` 就会留在 `_world` 里继续 tick，下次复用同一节点还会被加第二次。这是这次唯一的真实陷阱，`NumberDropAll_ClearsTheTreeAndTheList` 就是钉这一条。
- `_dmg` 从一份变 `Dictionary<int, DmgData>`，`LoadDmg(skin)` 按 key 缓存。**文件名不动**：皮肤 0 还是 `effect/dmg.json`，新皮肤是 `effect/dmg{key}.json`。这样不用重导 assets。
- 纹理仍按需载入，没做释放（§3.4 已记触发条件）。
- 相位自检：数字的 tick 从「主循环最后一段」挪进 `TreeUpdate`（travel 之前），比怪还晚一点；数字之间互相独立，先后无所谓。绘制仍读 `_numbers` 列表，不进 y-sort，所以画出来的顺序没变。
- 头测 4 条（`WorldTests`）：到点死（800ms 边界）、`Reset` 复活（含 `Skin` 归 0）后还能走、收尸（树少一个、列表少一个、池多一个）、换图清场（树清空、列表清空、池多两个）。客户端 41 → 45。
- 截图：27 张 `--shot` + 6 张 `--aiticks` 逐字节一致；另外补拍 `--hit` 和 `--skill`（这两张不进 27 张基线，因为要挑有怪的地图 20000），对着 S1 的 commit 也逐字节一致。

**结果（S3，2026-10-30）**：已验收。

- `SkillFx : Component`（`World/SkillFx.cs`）带 `Skill` / `Loop` / `FacingRight` / `Frames` / `Frame` / `Timer` / `X` / `Y` / `Rider`。`Update` 三件事：有 `Rider` 就贴到玩家身上；按 `Delay`（100ms）推一帧，保留溢出；buff 循环取模绕回，一次性特效播完标 `Owner.Dead`。
- 帧表在生成时定好（施法 `Effect`、命中 `Hit`、buff 循环 `Affected`），绘制不再每帧按 `Loop/Hit` 三选一。
- `TakeFx` 和 `TakeNumber` 一样：从池取节点 → `Reset()` → `_world.AddChild` + `_fx.Add`，一步到位。
- 主循环里那段特效循环删掉，`TreeUpdate` 之后加一行 `PoolSweep.CollectDead(_fx, ...)`。
- buff 循环挂玩家：`Rider` 直接指向 `_mover`，组件每帧自己读位置。buff 结束/重挂走 `SkillFx.StripLoops`（static，可头测），它把节点同时从树、列表、池三边对好（以前只回池，留在树上的会被 tick 两次）。
- **`PoolSweep`（新）**：数字和特效的两把扫帚（`CollectDead` / `DropAll`）合成一个泛型工具，`FloatingNumber` 的那两个方法删掉。不持有池，只是无状态清扫。
- 头测 4 条：一次性特效推帧播完 + `Reset` 复活、buff 循环绕回不死、buff 循环跟着玩家走（换位置后下一帧跟上）、`StripLoops` 只摘那一个 buff 且摘树。客户端 45 → 49。
- 截图：27 张 `--shot` + 6 张 `--aiticks` + `--hit`/`--skill` 全部逐字节一致（`--skill` 那张画的就是新组件的帧）。
- 相位差三处（掉出地图、跨图、同图传送门），都是一帧，见 §8 #50。

### 3.4 扩展点

- **怪**：数据驱动，几种怪就几个 json。不做池。纹理缓存现在是永不清（[GameRender.cs:178-188](../MushTown.Client/Game/GameRender.cs#L178-L188)），几百张图会吃内存 → 这次不做，记触发条件：单图怪贴图占用明显变大时，单开"按图卸载"计划。
- **伤害皮肤**：`_dmg`（一份）改成按 key 的字典；数字对象存皮肤 key，默认 0。几百种皮肤只加数据 + 导出。选择皮肤的设置 UI 这次不做。**加第 1 个真皮肤时导出器要一起改**：皮肤 0 是 `effect/dmg.json` + `effect/dmg/{d}.png`；皮肤 N 要写 `effect/dmg{N}.json`，10 张图**必须放独立目录**（例如 `effect/dmg{N}/{d}.png`），照抄现在的路径会把皮肤 0 的图覆盖掉，两份 json 指向同一批 png，皮肤 0 的样子被静默换掉。客户端是第一次画到那个皮肤时才读 json，缺文件在那时抛 `FileNotFoundException`，不是启动时。
- **技能**：`CastSkill(id)` + 覆盖表；特效按 `SkillData.Effect/Hit/Affected` 的 key 读；几百个技能不改循环。
- **技能的改键 UI**：K2 已做（技能窗开在键窗上面，从技能行拖到键格）。剩下的点击式拖放归 K3。技能、消耗品、椅子、面板按钮的统一点击式设计在 K3。
- **技能组合技**：不做，留给技能大改。战神的"上/下 + 攻击"、"右 + 右"、"先趴下再攻击出巨熊"都归那一层。它拿控制器给的原始方向键和动作边，记几百毫秒的输入历史：攻击按下时看当前按住的方向和刚按过的方向，匹配到组合技就放组合技，否则普攻；一次攻击只出一个技能。单键（趴下）是移动键，按下立即生效，不等攻击。K1 只保证原始键可读、动作有 `Fresh`/`Down`、一帧能触发多个动作。
- **窗口**：新窗口（星之力、潜能）用 3.2 的组件，缺什么加什么专用组件，不写 `Invoke`。

### 3.5 `Rect` 改成父相对（W1，用户 2026-10-27 定）

**现状**：`Rect : Component` 存的是**绝对屏幕矩形**（`Rectangle Box`）。于是：

- 窗口一移动就是 `Pos` 变 → `UiWindow.Sync()` 调 `Build()` 把整棵树重建一次（拖标题栏时每帧重建几十个节点）。
- 子节点不能跟着父节点走，窗口之间也不能嵌：娃娃预览塞进 Character Info，得自己算绝对坐标。
- 布局文件（`layouts/*.json`）里的数字本来就是窗口局部的，代码却在每处 `Pos.X + x` 加回来：10 个窗口文件、`Pos.X +` 48 处、`Pos.Y +` 51 处。

**改法**（照 Unity 的 `RectTransform`，只取有用的部分）：

1. `Rect` 只存局部 `Offset` + `Size`（相对父节点；窗口的根节点原点 = 窗口 `Pos`，菜单/HUD 的根 = 屏幕原点）。
2. 每帧开头 `Scene.Layout()` 走一次树，把算好的绝对屏幕矩形写进 `Rect.Screen`。命中（`Scene.TopAt`）、绘制（`TreeDraw.DrawObject`）、HUD 的悬停统一读 `Screen`，不再各算一份；`QslotCell`/`KeyRect`/`CellRect` 这类是**建树时**算局部盒子的函数（HUD 根没有 `Rect`，所以它们的值正好等于屏幕坐标），命中仍然走节点的 `Screen`。
3. `UiWindow.Sync()` 只在 `Dirty` 时重建；`Pos` 变化只改根节点偏移，子节点自动跟。
4. `Pos.X +` 48 处、`Pos.Y +` 51 处变回纯偏移；115 处 `new Rect(...)` 基本不动，只是不再加 `Pos`。
5. `Picture(key, dx, dy)`、`Label`、`Image` 这类"owner 原点 + 偏移"的写法不变，原点由遍历给出。

**不做**：anchors、pivot、layout group、缩放。画布固定 1366×768、布局数字手调，这些用不上。

**位置**：K2 之后、N1 之前。W1 要改每个窗口的坐标写法，先改完，后面的窗口片（N1/N2/N3/N6/N7/N8）就只写一遍新写法。

**验收**：全部 `--shot` 屏幕（27 个）逐字节一致；拖标题栏、拖任务窗时树不重建（临时在 `Build()` 里加计数验证后删掉）；`grep "Pos.X\|Pos.Y"` 只剩三处：窗口基类的 `OnScreen`、`StartDrag`，和复制对话框的全屏拦截块（它必须抵消窗口偏移才能盖满屏幕）。

**结果（2026-10-29）**：已验收并提交（8b86b4a）。

- `Rect` 只存 `Offset` + `Size`，`Scene.Layout()` 每帧填 `Screen`，`Hit`/`Draw`/各组件都读 `Screen`。
- `UiWindow.Sync()` 只在没建过或 `Dirty` 时重建，`Pos` 变化只改根节点 `Offset`；HUD 面板同理（`PlaceHost` + 六个"建过一次"的开关）。
- 27 个 `--shot` 屏幕逐字节一致（基线在 `logs/w1_before/`）。
- `grep "Pos.X\|Pos.Y"` 只剩三处：`UiWindow.OnScreen()`、`UiWindow.StartDrag()`，和复制对话框的全屏拦截块（块的父节点在窗口里，要写成负的窗口点才盖满屏幕）。窗口自己画的/自己命中的地方（聊天窗、键窗、世界地图、组队行、任务正文）都用 `OnScreen` 把局部矩形换成屏幕矩形。
- 新加 2 个测试（`SceneTests`）：`Layout_NestsTheOffsets`、`Sync_MoveDoesNotRebuild`（后者就是"拖窗口不重建"的常驻验证，替掉临时计数）。客户端测试 30 → 32。
- 热重载（`ReloadLayout`）要把六个"建过一次"的开关一起复位，否则面板被钉在左上角（`PlaceFirst` 不跑）。
- 一帧的遍历次数不是 1：`TopAt` 和 `Draw` 各自兜底调一次（普通帧 2 次、点击帧 3 次）。取舍见 §8。
- 手画的地方要自己拿屏幕原点：`Draw`/`DrawMembers`/`DrawRow` 收一个屏幕矩形或原点参数；窗口加 `ScreenBox()`（直接用 `Pos`，不依赖遍历），设置面板加 `SettingsScreen()`。
- 谈话框（`HudQuest.TalkBox/TalkRect`）跟着改成"根在屏幕、body 局部"；根上的 `Rect` 是复用的，不重复堆。
- 审查（5 轮，自主模式）修掉的真问题：`ReloadLayout` 没复位六个"建过一次"开关（面板钉在左上角）；`QuestHelperState` 在 Draw 里写 `Rect`（条子闪一帧）；复制对话框的全屏拦截块没抵消窗口偏移（左上两条带子的点击漏到 HUD，还会走 `NpcClick`）；任务助手条一行文本被写坏（整条不画）。删除弹窗改成"根不挂 `Rect`、`panel` 拿盒"，去掉了 `InDelete` 那层屏幕→局部的换算。
- 截图比对必须先删旧文件再逐张查退出码：`--shot` 崩了不写 png，旧文件留着，哈希照样一致（上面那个"整条不画"就是这么被放过的）。

**风险**：半改造会让所有窗口坐标错位 → 一片一次做完、一个 commit；命中测试要走父链，`Hovered` 的逻辑不变。

### 3.6 点击式拖放（K3，用户 2026-10-28 定）

**现状**：`KeyConfigWindow.UpdateDrag` 是"按住不放、移动、松手放下"。用户要改成**单击拿、单击放**。

1. **拿起**：鼠标移到可抓的图标上，光标变成抓手（**`UI/_Canvas/Basic.img/Cursor/5`**，用户 2026-10-28 指定；素材已经在用——`assets/ui/cursor_item.json` 是它的 4 帧和热点，`Menu.cs` 拿它当背包抓取的光标）。单击 → 图标跟着鼠标走，原位置空出来。
2. **放下**：再单击一次。落在哪个区，就做那个区的事：
   - **白色区域**（官方键盘上的键格）→ 绑定到那个键。
   - **HUD 16 个快捷槽** → 绑定到那个槽的固定键。
   - **黑色区域**（键盘下方的动作池）→ 取消绑定。
3. **三个区都能抓、都能放**：
   - 白色区域：抓住一个已绑的键，拿走（键变空）。
   - 黑色区域：抓住一个没绑的动作。
   - 快捷槽：抓住槽里的目标。
4. **池子只列没绑的动作，格子位置不动**（用户 2026-10-28 定）：绑定的动作从黑色区域消失，**后面的格子不往前补**——每个 action 的格子位置是固定的（`Actions` 的下标），空出来的格子就是空的。从白色或快捷槽抓回黑色区域 = 取消绑定，动作回到它原来那一格。所以池子是"固定格位 + 按 draft 隐藏已绑的"，不是动态重排。
5. **技能和道具同理**：从技能窗/背包抓起，放到白色区域或快捷槽 = 绑定；放回黑色区域 = 取消。
6. **技能窗拿起不看键窗**（用户 2026-10-30 定，归 K3）：现在点技能行只在键窗开着时才算拿起（`HudSkill.KeyWindowOpen` 那道门槛）。有快捷槽之后，快捷槽也是可放区，所以拿起条件改成"有可放区"，不再依赖键窗。
7. 拿起后按 Esc 或右键 → 丢掉手上的东西，不改任何东西。

**和旧设计的关系**：这替代 §3.1 里的"按住拖、松手放"，也替代 K1 的"松手落在键上"。`UpdateDrag` 的状态机要从"按住/松开"改成"有没有拿着东西"。K3 做之前，K2 先补了最小版本（键格可抓、拖到黑色池子取消、技能落键有门槛），所以现在就能解绑和做 Combo Key。

**依赖**：快捷槽要等 N2（槽先合成一套）；技能窗和背包的抓起要它们自己的窗口片。所以 K3 排在 N2 之后，白色区域和黑色区域的点击式拖放可以先做。

**验收**：单击动作池的图标 → 图标跟手、原格空出；单击键盘的键 → 动作落在那个键上、池子里少一格（其余格子不动）；再把它从键盘抓回池子 → 键变空、动作回到原来那一格；拿起时按 Esc → 什么都没变；HUD 槽的抓放同 N2 的验收。

## 4. 分片

工期按每周约 10 小时估。每周一个 commit（用户规矩）；2 周的片第 1 周提交能 build + test 的中间态，第 2 周完成；每片结束停下验收，先 review 后提交。

### 输入线

| 片 | 内容 | 工期 |
|---|---|---|
| K1 (P0) | **第 1 周**：`InputController` + `BoundAction`；绑定进角色（3 套 `KeyPresets` + `ActivePreset`）；KMS 默认键（10 个菜单动作 + 攻击/跳跃/Talk）；技能进表但**无默认键**；`CastSkill`；旧键窗适配到当前 preset（8 个可见动作）；技能窗去掉固定键文字（旧 Q/R/W… 已失效）。**第 2 周**（已做）：HUD 16 槽改成键槽（C 空）；Item 目标；删 `QuickSlots`/`QuickSlotKey`/demo QuickSlots 种子；旧键窗的快捷槽页整个删掉，Quick Slots 按钮只画图、点了不动 | 2 周 |
| K2 (P0) | 键窗：每角色 preset、动作池、组件化（与 N5 合并）、键格固定、动作拖动、Copy 弹窗、技能从技能窗拖；拖到 HUD 槽随 N2；不做快捷槽页。**已做（2026-10-28）**：组件化（N5）；动作池 = 官方 77 张图标全放进去（没做的按了没反应），默认过滤组 = Not Added；Chat/Minimap 接进 `UpdateHud`；删掉 K1 的"隐藏动作的键不许抢"保护；Copy 换成官方 `presetCopy` 弹窗（3 个来源 preset + checked 高亮）；PRESETS 1/2/3 用 checked 图（选中绿）；Skill 钮不再关键窗，技能窗开在上面，从技能行拖到键格写成 `Skill:<id>`（draft 带技能，键格画技能图标）。**审查后补（2026-10-29）**：键格可抓（解绑和 Combo Key 才有入口）、技能落键加拖动门槛和顶层判断、Copy 默认来源改成当前 preset、Default 真的清掉技能、Chat 键不再把自己的字符打进输入框、键帽反查表。**剩下**：无（点击式拖放归 K3） | 2 周 |
| K3 (P1) | 点击式拖放（§3.6）：hover 抓手（`Cursor/5`）、单击拿、单击放；白色键盘/黑色池子/HUD 槽三区互通；放到黑色区 = 取消绑定；池子固定格位、只列没绑的动作。白+黑两区先做，HUD 槽等 N2，技能/道具等各自窗口片 | 1 周 |

### 世界线

| 片 | 内容 | 工期 |
|---|---|---|
| S1 (P0) | `Dead` + 回收 + 怪动画进 `MobArt.Update` + 传送门。**已做（2026-10-29，提交 60fda5e）**：`GameObject.Dead`，`TreeUpdate`/`TreeDraw`/`Scene.Hit`/`Scene.AnyModal` 都跳过死节点（用户要求「满足 object-component 架构以便之后扩展」，所以让死节点在步进、绘制、命中、模态四条路上语义一致，不只 step）；`MobArt.Update` 推帧（闪烁回 idle、die1 播完标 `Dead`），动作改由 `SetAction` 同时换帧表；`PortalComp.Update` 自己循环；游戏在 `TreeUpdate` 之后 `MobArt.CollectDead` 收尸（摘树 + 删 dict）。数字/特效仍是主循环，归 S2/S3 | 1 周 |
| S2 (P0) | 数字变成对象 + 皮肤字典（默认 1 种）。**已验收并提交（1e182d0）**：`FloatingNumber` 组件自己加龄、到点标 `Dead`；数字节点进 `_world`，池子复用节点（`Reset` 清 `Age`/`Skin`/`Dead`）；`FloatingNumber.CollectDead` 收尸，`FloatingNumber.DropAll` 换图清场；`_dmg` 改成按 key 的字典，皮肤 0 沿用 `effect/dmg.json`，新皮肤 `effect/dmg{key}.json`。测试 41 → 45，27 张 `--shot` + 6 张 `--aiticks` + `--hit`/`--skill` 全部一致 | 1 周 |
| S3 (P0) | 特效变成对象 + buff 循环挂玩家。**已验收（2026-10-30）**：`SkillFx` 组件自己推帧、播完标 `Dead`、buff 循环跟着 `Rider`（玩家）走；`TakeFx` 进树 + 进列表一步；主循环那段删掉；`PoolSweep` 统一数字和特效的收尸/清场；`SkillFx.StripLoops` 补上摘树。测试 45 → 49，27 张 `--shot` + 6 张 `--aiticks` + `--hit`/`--skill` 全部一致 | 1 周 |

### 窗口线

| 片 | 内容 | 工期 |
|---|---|---|
| N0 (P0) | `Box` + `Ui/AvatarArt.cs` + `AvatarPreview`：**已做**（`Box` 用在小地图框和物品 tip 框；`AvatarPreview` 用在创建角色预览；`MergeParts`/`DrawAvatar` 搬进新文件并加了 `DrawAvatarOnScreen`） | 0.5 周 |
| N5 (P0) | 键窗 7 处 `Invoke` 已删（和 K2 合并）：`KeyCap` / `ActionCell` / `FilterBox` / `FilterItem` / `HeldIcon` 五个窗口内私有组件，预设钮和 Combo Key 钮改用 `Image(stem, on)`；拖放松手改由 `UpdateHud` 的鼠标边沿驱动（`KeyConfigWindow.UpdateDrag`），Draw 不再轮询鼠标 | 已做 |
| W1 (P0) | **`Rect` 改成父相对（Unity RectTransform 的形状）**：`Rect` 只存局部 offset + size；每帧开头 `Scene.Layout()` 走一次树把屏幕矩形写进 `Rect.Screen`；命中、绘制、HUD 悬停都读 `Screen`；`UiWindow.Sync()` 不再因为 `Pos` 变化重建；48 处 `Pos.X +`、51 处 `Pos.Y +` 变回纯偏移（10 个窗口文件）。不做 anchors / pivot / layout group。详见 §3.5 | 1 周（已验收，8b86b4a） |
| N1 (P1) | HUD 条：气泡、快捷栏角标、手拿、tips | 1 周 |
| N2 (P1) | 快捷槽：4 个键提示 + 12 个物品格合成一套 16 个等价槽（内容先固定）+ 设置旋钮 | 1 周 |
| N3 (P1) | 聊天窗（整块，最大） | 2 周 |
| N4 (P1) | 小地图 + 大地图 | 2 周 |
| N6 (P2) | 任务窗 | 1 周 |
| N7 (P2) | 组队 + GUIDE | 1.5 周 |
| N8 (P2) | 选角 + 创建角色 | 2 周 |

合计约 19 周（N0 缩到 0.5 周，S4 砍掉，W1 加 1 周）。三块互不依赖，顺序可以换。

**顺序（我定的）**：K1 → N0 → K2+N5 → W1 → S1 → S2 → S3 → N1 → N2 → **K3** → N3 → N4 → N6 → N7 → N8。

理由：先把输入这条线走完（技能解耦 + 每角色绑定是一件事）；再做组件基础，紧接着把键窗改组件；然后**先把 `Rect` 改成父相对**，再动世界和窗口——W1 要改每个窗口的坐标写法，放在 N1/N2/N3/N6/N7/N8 之前，那些窗口就只写一次新写法。**K3 从原来的位置挪到 N2 之后**（2026-10-30 用户定）：点击式拖放的三个可放区里，HUD 16 槽要等 N2 先合成一套，背包要等 N1。

## 5. 每片的验收

1. `dotnet format` 干净；`dotnet build` 0 警告 0 错误。
2. 测试全过（K1 新增 16 条；当前 345）。
3. 受影响的 `--shot` 截图与改动前逐字节一致；**例外**：输入片换了默认键（Stat C→S、Quest V→Q 等），**所有带 HUD 的游戏截图**（第 4 个提示格、MENU 角标）、键窗和**技能窗**（固定键文字已删）按新默认重拍，人工对照官方布局验收。
4. 世界片：`--aiticks 40` 截图一致；新增头测覆盖"推帧 / 播完 / 回收"。S1 已做：6 张 `--aiticks` 图（0/10/40/100 步、1000000 地图 0/40 步）逐字节一致，27 张 `--shot` 也一致；`WorldTests` 7 条 + `SceneTests` 2 条覆盖推帧、闪烁、播完、死节点（step 和命中都跳）、收尸、传送门。**注意**：截图不跑 `Update`，`--aiticks` 只推怪的走位和刷新，所以这些图只证明"绘制查表没变"，证明不了动画推进和回收。回收这一步要靠**手动验收**：进游戏打一只怪，尸体播完消失，不崩。S2 已做：`WorldTests` 4 条（到点死、`Reset` 复活、收尸、换图清场），27 张 `--shot` + 6 张 `--aiticks` 一致，另加 `--hit`/`--skill`（地图 20000）两张在改动前后逐字节一致。手动验收：①打怪时数字上升淡出、自己消失，不崩；②数字还在屏幕上时走进传送门换图，数字立刻消失，换图后新数字的上升速度不变（这条验的是清场时有没有把节点摘出树）。S3 已做：`WorldTests` 4 条（一次性特效推帧播完 + `Reset` 复活、buff 循环绕回不死、buff 循环跟着玩家走、`StripLoops` 只摘那一个 buff 并摘树），27 张 `--shot` + 6 张 `--aiticks` 一致，`--hit`/`--skill` 两张也逐字节一致。手动验收：①放一个带图的 buff（例如 Spirit Blade 那类有 `Affected` 图的），循环图案贴着人走，buff 结束时图案消失；②连续放同一个 buff，图案不会叠成两层；③放一个伤害技能，特效播一次就消失，不崩；④放 buff 后走进传送门换图，图案重新挂在人身上。
5. 输入片第 1 周：手测 10 个菜单键、攻击、跳跃、Talk；换角色后按键跟着换；旧键窗的 Save/Apply/Copy/Default 在 3 套 preset 上生效（1/2/3 切换选中的 preset 并重读 draft，键格跟着换；新 UI 的预览是 K2）；`settings.json` 没有按键字段。
6. 输入片第 2 周：3 套 preset 预览 / Apply / Copy / Save 后重开仍生效；HUD 16 槽按表重画（Ctrl/Alt/I 有内容，C 空，12 个物品格按表）。技能头测（已加，`Fresh_ASkillTargetFiresFromItsKey`：塞一条合成绑定，验证按下那个键时技能目标 Fresh）覆盖输入那一半；`CastSkill` 本身要 GraphicsDevice，只能靠 `--skill` 截图手测。实际游玩要等拖放片，这是已知代价。
7. 代码检查：`grep "new Invoke"` 该文件为 0；**这 16 个**游戏输入字段为 0（`_altWasDown`、`_upWasDown`、`_ctrlWasDown` + 12 个技能键 + `_zWasDown` 拾取）；菜单的 `_escWasDown`、`_menuUpWasDown` 等系统键字段保留（不是绑定键）；`_qsWasDown` 已随快捷栏删掉；`settings.json` 里没有按键。
8. 窗口片（N5 已做）：`grep "new Invoke" MushTown.Client/Panels/HudKeyConfig.cs` = 0；`--shot keys` 和改动前逐字节一致（组件化不许改像素）；手动拖一次动作到键上，图标跟手、松手落在那个键、拖到黑色动作池上把键清掉。
9. N0（已做）：`--shot chars/chars2/setup/equip/minimap/hud` 与改动前逐字节一致（`Box` 的 `mini_frame` 由 minimap 覆盖、`tip` 由 equip 页的物品详情框覆盖；`AvatarPreview` 由 setup 覆盖；`DrawAvatar` 由 hud 和 chars 覆盖）。
10. W1（`Rect` 父相对）：全部 `--shot` 屏幕（27 个）逐字节一致；拖标题栏、拖任务窗不触发 `Build()`（临时计数验证后删）；`grep "Pos.X\|Pos.Y"` 只剩 `OnScreen`、`StartDrag` 和复制对话框的全屏拦截块；一帧里的节点遍历最多 3 次，理由见 §8 第 35 条。
11. K2 动作池（已做）：键窗打开时默认是 Not Added 组，列出 63 个没绑键的动作（官方 icon 全部 77 张图，一张一格；默认表占 14 个）；9 个过滤项（Not Added + 8 个分类）逐组点开都能看到对应图标；键盘上 E 画 EQUIP、U 画 MAPLE GUIDE、`\` 画 KEY BINDINGS、Z 画 PICK UP、Ctrl/Alt 画 ATTACK/JUMP；Chat 键点开聊天输入、Minimap 键只是重建小地图；把 Attack 从键上抓起来、拖到黑色动作池上松手、Save 后攻击真的没键了（K1 的 `Reserved` 保护已删，键格本身可以抓；松手在标题栏、按钮或键缝上什么都不改）；把 `Legion`（没实现）拖到键上 Save，按键什么都不发生；Combo Key 模式下按住第一个已绑的键、拖到第二个键上松手，做成 `I+K`（点一下拿不起来，那是 K3）。`--shot keys` 的像素变化是预期的（池子变大），不用和旧基线比。
12. K3 点击式拖放：见 §3.6 的验收段。拿起时按 Esc 丢掉手上的东西、不改任何东西；池子只列没绑的动作，绑一个少一格、取消一个多一格。
13. 键窗不挡（S3 同批，已做）：键窗开着时走路、攻击、跳跃、拾取、对话、喝药、7 个面板键全部生效，用的是**已保存**的 preset；在键窗里改键但**不 Save**，关窗后按旧键还是老效果；Save 后**下一帧**新键生效，不用重开；MENU 窗口仍然挡角色。这几处是 `Update` / `UpdateHud` 里的条件，要 `GraphicsDevice`，没有头测，只能手测。

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
22. 动作池 = 官方 icon 的 77 张图（2026-10-28 用户定）：**全部放进去，没做的功能按了没反应**——这样测试时一眼能看出哪些还没做。77 张图对应 77 个格子；格子顺序按图标编号；`GuideCard` / `PartySearch` / `PartyAsk` 不进池（子窗口和服务端事件，官方没有它们的图标）；`Settings` 也不进池（官方没有选项的图标，选项在 MENU 里）；Z 拾取作为 `Pickup` 进池。Chat 和 Minimap 进池后要真的能用：Chat 键 = 点聊天输入行，Minimap 键 = 只 `ShowWindow`（常显窗，关不掉）。
23. Copy 用官方 `presetCopy` 弹窗（2026-10-28）：背图自带标题、说明和 PRESET 条，"currently selected Preset [?]" 的空只填当前 preset 号；三个 preset 钮用 checked 图；Copy 把来源整表覆盖到当前选中的 preset（技能和物品一起），Cancel 什么都不改；弹窗是 modal，点外面无效。
24. 键窗的 Skill 钮不关键窗（2026-10-28）：技能窗直接开在上面（`_raiseRow` 行尾），点技能行拿起技能（只有键窗开着时才拿），拖到键格松手写 `Skill:<id>`；技能窗关掉时手上的技能一起丢掉。
25. 主窗口的 PRESETS 1/2/3 用 `button:presetPage1/2/3` 的 `checked` 图（2026-10-28 用户定）：选中的那页是绿色，没选中的是灰色；hover/pressed 用 `_h`/`_p`。
26. 键窗打开时默认过滤组 = Not Added（2026-10-28 用户定）：一开窗就看到还没绑键的动作。
27. 拖放改成**点击式**（2026-10-28 用户定，见 §3.6）：hover 抓手（`Basic.img/Cursor/5`）、单击拿、单击放；白色键盘/黑色池子/HUD 槽三个区都能抓、都能放；放到黑色区 = 取消绑定；池子只列没绑的动作，**格子位置固定、不往前补**。技能和道具同理。这替代"按住拖、松手放"。
28. 池子的隐藏规则（2026-10-28 用户定）：`_draft` 里有键的动作一律不画（不管当前是哪个过滤组），所以绑好一个动作它就从池子里消失，取消绑定后回到它原来那一格。
29. 技能组合技（上/下+攻击、右+右、单键先）留给技能大改，K1 不做；K1 只提供原始键、动作边、一帧多动作。
30. 键格也能抓（2026-10-29 审查 P0）：键上已经有动作时，按那个键把动作拿起来，可以移到别的键，或拖到黑色动作池上取消绑定。没有这条，绑上的动作就永远拿不下来（池子只画没绑的），Combo Key 也永远做不出第二个键。
31. 池子按过滤组里的所有动作算格位，绑了的格子留空（2026-10-29）：`Actions` 的下标就是格位，所以「不往前补」对所有过滤组都成立。
32. 取消区只有黑色动作池（2026-10-29 审查 P0-2）：松手在标题栏、按钮、PRESETS 行或键缝上什么都不改。键盘图上的 Tab / Enter / 方向键不在 `keys.json` 里，按「不是键格就解绑」会把动作静默丢掉。过滤下拉列表展开时不接受取消（列表盖在池子上，松开算「什么都不改」）。
33. 键上的技能拿不起来（2026-10-29 审查 P1-2）：`_held` 是 `HotkeyAction?`，装不下技能。要单独清掉一个技能键，现在只能 Default。放进 K3，和 `Cursor/5` 抓手一起改（`_held` 换成「动作或技能」的联合体）。
34. 老存档的 preset 表没有 `Z -> Pickup`（2026-10-29 审查 P0）：拾取从硬编码 `Keys.Z` 改成查表，`Ensure` 只在 `KeyPresets == null` 时播种，所以 K1 时期建的表里没有这条，切到那套 preset 后 Z 不捡东西。**按 §8 的既有决定不写迁移代码**：删档重建，或在键窗里按一次 Default。理由：自动补种会让「解绑拾取」在重开游戏后失效，那是新功能的真 bug。窗口里能看见 Z 格是空的、Not Added 组里列着 Pickup，不是静默坏掉。
35. W1 的 Scene.Layout() 由 TopAt 和 Draw 各自兜底调用（2026-10-29 审查 P1-1 驳回）：普通帧 2 次、点击帧 3 次。驳回收成一处的理由：命中要读到本帧刚 Build 出来的树（按一次按钮就重建面板，那一帧的点击必须落在新树上），而 Draw 在 Update 之后跑，只留 Draw 里那一次会让命中永远落后一帧。遍历是几百个节点的整数加法，比这个风险便宜。
36. 面板/窗口的「根在屏幕、子节点局部」是 W1 的唯一写法（2026-10-29）：手画的部件拿不到遍历给的原点，统一用 UiWindow.OnScreen(local)（Pos 始终是最新的，不需要遍历）。
37. 任务助手的条和 chevron 不在 Draw 里改 Rect（2026-10-29 审查 P1-2）：Draw 跑在 Layout 之后，改了本帧读到的 Screen 还是旧值，条子会闪一帧。位置改在 Build 和 chevron 点击时写。
38. 怪的帧表跟着动作走（S1，2026-10-29）：`MobArt` 存 `MobData` + 当前动作的 `Frames`，`SetAction` 一次换动作名、帧表、帧号和计时器，两个字段的 setter 是私有的。外部换动作只有三处（spawn、挨打、走起来），第四处是 `Update` 里闪烁结束回 idle——那一处要保留溢出时间，所以手写两行而不走 `SetAction`。理由：`Update` 和 `Draw` 都要按当前动作取帧，每次查一遍 `Data.Actions` 是白查；私有 setter 让"动作名换了、帧表没换"编译不过。
39. 时间源不统一（S1，2026-10-29）：`Component.Update` 收秒（玩家 mover 的契约），怪和传送门要毫秒，用 `dt * 1000f`。旧循环用的是 `TotalMilliseconds`，两者差 2e-6 ms/帧；按 16.666667 累积，100ms 的帧会早一个 tick 翻页。**接受**：只有真人看得出的一帧动画差别，截图（不跑 Update）看不出来；换成传毫秒要改 mover 的契约和测试，不划算。S3 起特效（`SkillFx`）也从 `dtMs` 换成了 `dt * 1000f`，同样的量级，同样接受。
40. 地图切换那一帧的相位（S1，2026-10-29）：`SwitchMap`（踩传送门/换频道）在 `TreeUpdate` 之后，所以那条路径上新图的怪和传送门第一次 tick 晚一帧（旧代码是先切图再 tick）。**接受**：新旧图都从第 0 帧起，晚一帧看不出来；相对次序 怪 → 数字 → 特效 没变。大地图传送（`TravelTo`）走的是 `UpdateHud`，在 `TreeUpdate` **之前**，那条路径新图的怪同帧就 tick，和旧代码一样。
41. 收尸放在 `MobArt` 上并公开（S1，2026-10-29）：`MobArt.CollectDead(mobs, world)` 摘树 + 删 dict。放在游戏里的私有方法测不到（要 GraphicsDevice），而「播完 → 摘树 → 删 dict」正是这片要验的东西。方法本身只依赖 `Dead` 和 `GameObject`，头测直接调。
42. 皮肤文件名不搬家（S2，2026-10-29）：皮肤 0 仍是 `effect/dmg.json`，皮肤 N 是 `effect/dmg{N}.json`，`LoadDmg(skin)` 里就一个三元。另一条路是全部改成 `dmg{key}.json`，但那样现有的 `assets/` 得重导一遍才开得了游戏。字典本来就是这片的目的，文件名不是。
43. 数字池复用的是**节点**，不是裸组件（S2，2026-10-29）：`TakeNumber` 从池里取组件，`Reset()` 把 `Age`/`Skin`/`Dead` 清干净，再 `_world.AddChild` 挂回树上；收尸时 `RemoveChild` 再进池。少了 `Reset` 里那行 `Dead = false`，复用的数字一出生就是死的：树上不更新，`CollectDead` 当帧把它收回池子，所以永远看不见。少了 `RemoveChild`，换图清场后节点留在 `_world` 里继续 tick，下次复用同一节点时被 `AddChild` 加第二次 → 一帧走两次（上升变快、寿命减半），树里节点越积越多。两条路都有头测（`NumberReset_...`、`NumberDropAll_...`）。
44. 数字的 tick 挪进 `TreeUpdate`，绘制仍读 `_numbers`（S2，2026-10-29）：数字之间没有依赖，谁先走无所谓，所以不为了保持"怪 → 数字 → 特效"的顺序去另开一个桶。绘制读列表是因为数字不进 y-sort，也不该进——它永远画在最上层。
45. `FloatingNumber` 自己带 `LifeMs`/`RisePx`（S2，2026-10-29）：寿命和上升高度是这个对象自己的样子，主循环里的两个常量删掉，绘制读 `FloatingNumber.LifeMs` / `RisePx`。以后换皮肤如果连消失时间也不同，字段就在组件上改，不用再找主循环。
46. 树步进期间不能增删子节点（S2 审查，2026-10-30）：`TreeUpdate` 的 `foreach (o.Children)` 是活遍历，组件 `Update` 里 `AddChild`/`RemoveChild` 会抛 `Collection was modified`。今天的生成点都在步进之外（攻击/技能在步进前，网络/AI 在步进后），所以只写在注释里，没有改成"遍历前拷一份子列表"。以后真要在步进里生成（比如怪死时掉东西），先改遍历方式。
47. 数字的"进树 + 进列表"合成一步（S2 审查，2026-10-30）：`TakeNumber` 同时做 `_world.AddChild` 和 `_numbers.Add`。分开写时，新生成点漏掉列表那一半，节点会在树里一直 tick 却永远收不到。`TakeFx` 照同一个写法（S3）。
48. buff 循环自己跟着玩家（S3，2026-10-30）：`SkillFx.Rider` 直接指向 `PlayerMover`，组件每帧读它的 `X/Y` 并减去 `CastDy`。另一条路是让节点成为玩家的子节点，但世界树没有坐标（`Screen` 只给 UI 用），那样得先做一套世界坐标，超出这一片。组件持一个玩家引用是最小的做法。
49. `PoolSweep` 统一两把扫帚（S3，2026-10-30）：数字那一份（`FloatingNumber` 上的 `CollectDead`/`DropAll`）和特效那一份（`GameCombat` 里的倒序循环 + `DropFx` 的 foreach）做的是同一件事，合成泛型工具，`FloatingNumber` 上那两个方法删掉。名字不叫 Pool：它不持有池，只是无状态清扫，池在游戏里。`MobArt.CollectDead` 不并进来：它还要删 dict，签名不同。
50. 传送/掉出地图那一帧的相位（S3，2026-10-30）：旧代码在 `TreeUpdate` 之后（`SwitchMap`/`Spawn()` 之后）才给 buff 循环贴位置，现在循环在 `TreeUpdate` 里、玩家节点之后跑。三处差异，都是一帧：①掉出地图，`Spawn()` 在 `TreeUpdate` 之后传送玩家，循环这一帧还在旧位置；②跨图，`EnterMap` 里 `DropFx` + `RespawnBuffLoops` 先跑，那时 `_mover.X/Y` 还是旧图坐标，新循环生成在旧位置，`_loadBeat` 的 10 帧黑屏盖住；③同图传送门，不调 `EnterMap`、没有黑屏，循环在旧位置画一帧。**接受**：跨图和掉出地图都有黑屏或瞬移盖住；同图传送门可能看见一帧旧图案（传送门离得近、旧位置还在屏幕内时），接受这一帧。真要修就在 `SwitchMap` 设完 `_mover.X/Y` 后把带 `Rider` 的循环贴一次位置（和 `SkillFx.Update` 同样两行）。
51. 键窗完全不挡（2026-10-30 用户定，参考 MapleStory）：先是面板键照常（不然按 K 打不开技能窗、没法把技能拖到键上），然后用户要求角色也不挡——「MapleStory 里面应该是没限制的」。现在 `blocked` 只看 MENU 窗口；键窗开着时走路、攻击、跳跃、拾取、对话、喝药、7 个面板键全部照常，用的是**已保存**的 preset。草稿只在键窗里，Save/Apply 才写存档，下一帧 `InstallPendingKeys` 在 `Poll` 前重建输入表，所以没保存就一直是旧键、保存后下一帧生效。MENU 仍然挡角色（全屏菜单）。
52. K3 挪到 N2 之后（2026-10-30 用户定）：点击式拖放的三块可放区里，HUD 16 槽要等 N2 合成一套、背包要等 N1。原顺序里漏了 K3，现在补在 N2 后面。
