# 扩展性重构 — 2026-10-26

状态：K1 第 1、2 周已验收并提交；N5 键窗组件化已验收并提交；N0（`Box` + `Ui/AvatarArt.cs` + `AvatarPreview`）已验收并提交（6c521cc）；K2 已验收并提交（5348adb，两轮审查修了 5 个 P0 和一批 P1/P2）；W1（`Rect` 父相对，§3.5）已验收并提交（8b86b4a，5 轮审查，27 张 `--shot` 一致，客户端测试 32）；S1（世界对象自己走）已验收并提交（60fda5e）；S2（数字变成对象 + 皮肤字典）已验收并提交（1e182d0，两轮审查，测试 45）；S3（特效变成对象 + buff 循环挂玩家）已验收：27 张 `--shot` + 6 张 `--aiticks` + `--hit`/`--skill` 各 1 张全部逐字节一致，客户端测试 49。同一批还带一条输入修复：键窗不再挡任何东西（角色动作、面板键、喝药全照常，§8 #51）。已提交（`69fc625`）；同批的输入修复也单独提交了。N1（HUD 条：气泡、手拿、tips、快捷栏格子）已提交。N2（快捷槽一套 + 设置旋钮）已验收并提交（41b50f4，11 轮审查）。K3（点击式拖放）已验收并提交（986c438，9 轮审查）。N3 已做（§3.9，`ChatLog` + `CaretText` + `Box` 框，按用户验收意见改了 10 轮，之后跑了 7 轮自主 review：修了 6 个 P0、12 个 P1），等验收。下一步：N3 收尾（等验收），然后 N9 聊天气泡样式（用户 2026-10-31 定）。按片做，每片 1-2 周，一次验收。

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
- 鼠标：`Mouse`（1366×768 游戏坐标）、`Click`、`Held`、`Wheel`。**K1 只把字段建好**；HUD/聊天/键窗的鼠标读取迁移和 K3 的拖放一起做，K1 的游戏、聊天、键窗仍各自读一次鼠标。
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

**HUD 右下 16 个快捷槽（16 个固定键）**：键是 Ctrl、Alt、I、C、1-9、0、-、=。每个槽显示表里这个键绑的目标（官方图标 + 键帽）；没有绑定就空着。默认只有 Ctrl→Attack、Alt→Jump、I→Inventory 有内容；C 槽空着（Stat 默认在 S，S 不在这 16 个键里）；1..= 空着。不另存 `QuickSlots`：药水就是 `Item` 目标，绑定在 1..= 上。16 个格子都按固定键查表：有目标就画它的图标（动作图标用 `StatusBar3.img/KeyConfig/icon`：Attack 52、Jump 53；物品画物品图；技能画技能图），键帽永远画。16 个格子画法一样：键帽在左上（`hud-layout.json` 的 `qslotcap` 偏移），图标居中（N2 定，见 §8 #54）。格子能拿起、能放下（K3，见 §3.6）。

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
- 拖放：用户 2026-10-28 把"按住拖、松手放"改成**点击式**（单击拿起、再单击放下），K3 已做，见 §3.6。三个可抓区（白色键盘、黑色动作池、HUD 16 槽）和三个可放区（白/槽 = 绑定，黑 = 取消）见 §3.6，N2 的 16 槽已经能放下。
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
   - 聊天片（N3）：`CaretText`（输入文字 + 光标闪烁）、`ChatLog`（折行复用 `UiText.Wrap`，日志 + 标签 + 滚动）。见 §3.9。
   - 任务片（N6）：`TextLines`（多行文字，每行可不同颜色）。
   - GUIDE 片（N7）：`ClipPlayer`（序列帧播放，喂 `UiClip`）。
   - 现有 `Picture / Label / LiveLabel / Bar / Image / Round / Fill / InvSlot` 继续用。
2. **共享画法**（`Ui/AvatarArt.cs`，**已建**）：`MergeParts` / `DrawAvatar` / `DrawAvatarOnScreen`（把相机压到 0,0 再画，UI 上的娃娃都是屏幕像素）+ `AvatarPreview` 组件（owner 矩形的左上角 = 脚）。创建角色预览已用它；Character Info / Equip 里的娃娃等那个窗加 `dollX/dollY` 布局键再接一行（那时 `AvatarPreview` 才需要 `worn` 参数）。选角格一页 12 个娃娃和石柱/传送门交织，留给 N8。
3. **专用组件**：只给一个窗口用、又要读那个窗口私有状态的，做成窗口文件里的 private 嵌套类，不做成通用件。例如 `KeyCap` / `ActionCell` / `FilterBox`（HudKeyConfig，N5 的 5 个）、`MinimapCanvas`（HudMap）、`ChatLog`（HudChat）。
   - **只靠构造参数就能活**（不读窗口私有状态）、或者两个以上窗口要用的，放 `Ui/` 做 public 组件：`Picture` / `Bar` / `LiveLabel` / `Slot` / `HoverTip` / `ItemTip` / `InvSlot`，以及 N1 的 `Balloon` / `HeldItem` / `Tips` / `QuickCell`（它们各自只有一处用，但取数全在构造时传进来，所以不绑在窗口文件上）。判断标准：构造时能不能只靠参数。
   - 专用组件清单（按窗口）：HUD 条 4 个（`Ui/`）、快捷栏 1 个（`Ui/`）、设置旋钮 1 个、聊天 3 个、小地图 1 个、大地图 1 个、键窗 5 个（嵌套）、任务 3 个、组队 2 个、GUIDE 1 个、选角/创建 3 个。
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
- **技能的改键 UI**：K2 已做（技能窗开在键窗上面）；K3 改成点技能行拿起、点键或槽放下。技能、消耗品、椅子、面板按钮的统一点击式设计还没做。
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
- 手画的地方要自己拿屏幕原点：`Draw`/`DrawMembers`/`DrawRow` 收一个屏幕矩形或原点参数；窗口加 `ScreenBox()`（直接用 `Pos`，不依赖遍历），设置面板加 `SettingsScreen()`（N2 删了，旋钮改成组件后没人手画）。
- 谈话框（`HudQuest.TalkBox/TalkRect`）跟着改成"根在屏幕、body 局部"；根上的 `Rect` 是复用的，不重复堆。
- 审查（5 轮，自主模式）修掉的真问题：`ReloadLayout` 没复位六个"建过一次"开关（面板钉在左上角）；`QuestHelperState` 在 Draw 里写 `Rect`（条子闪一帧）；复制对话框的全屏拦截块没抵消窗口偏移（左上两条带子的点击漏到 HUD，还会走 `NpcClick`）；任务助手条一行文本被写坏（整条不画）。删除弹窗改成"根不挂 `Rect`、`panel` 拿盒"，去掉了 `InDelete` 那层屏幕→局部的换算。
- 截图比对必须先删旧文件再逐张查退出码：`--shot` 崩了不写 png，旧文件留着，哈希照样一致（上面那个"整条不画"就是这么被放过的）。

**风险**：半改造会让所有窗口坐标错位 → 一片一次做完、一个 commit；命中测试要走父链，`Hovered` 的逻辑不变。

### 3.6 点击式拖放（K3，用户 2026-10-28 定）

**现状**：K3 之前 `KeyConfigWindow.UpdateDrag` 是"按住不放、移动、松手放下"。用户 2026-10-28 要改成**单击拿、单击放**；K3 已做，结果见本节末。

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
5. **技能和道具同理**：从技能窗/背包抓起，放到白色区域或快捷槽 = 绑定；放回黑色区域 = 取消。**道具这一半推迟**：K3 只接技能和动作，背包的拿起归背包窗那片（见本节「没做」）。
6. **技能窗拿起不看键窗**（用户 2026-10-30 定，K3 已做）：K2 时点技能行只在键窗开着时才算拿起（`KeyWindowOpen` 那道门槛）。有快捷槽之后，快捷槽也是可放区，所以拿起条件改成"有可放区"，不再依赖键窗；那道门槛已删。
7. 拿起后按 Esc 或右键 → 丢掉手上的东西，不改任何东西。

**和旧设计的关系**：这替代 §3.1 里的"按住拖、松手放"，也替代 K1 的"松手落在键上"。K3 把状态机从"按住/松开"改成"有没有拿着东西"（`Held`）。K3 之前 K2 先补了最小版本（键格可抓、拖到黑色池子取消、技能落键有门槛），所以那时就能解绑和做 Combo Key。

**依赖**：快捷槽要等 N2（槽先合成一套）；技能窗和背包的抓起要它们自己的窗口片。所以 K3 排在 N2 之后，白色区域和黑色区域的点击式拖放可以先做。

**结果**（2026-10-30 已做，review 9 轮后收敛，已验收并提交）：

- 手上的东西从窗口搬到游戏：`Core/Hotkeys.cs` 的 `Held`（`BindTarget?`，只装 Ui/Skill）/ `Holding` / `Hold` / `TakeHeld` / `ClearHeld`（替掉 `HeldSkillAt` 和 `ClearHeldSkill`）。一个字段而不是"动作 + 技能"两个，所以"两个都设"这种状态在类型上不存在。**拿起不改 draft**，只有放下才改，所以 Esc 和右键什么都不改。`BindLive(Keys, BindTarget)` 是"键窗没开时直接绑当前 preset 并存盘"；条带读实时表，所以新绑定下一帧才画出来（`InstallPendingKeys` 仍然只在 `Poll` 前跑，不在点击中途重装输入表）。
- `KeyConfigWindow` 的按住拖拽整个删掉：`_held`、`_pickAt`、`DragSlop`、`UpdateDrag`、`Drop`、`DropSkill`、`HeldIcon`、`BuildDrag`、`ActionFor`。新的入口四个：`Pick`（拿起动作）、`PickKey`（拿起键上的东西）、`TryDropAt`（这个点是可放区就放下）、`ApplyDrop`（真正改 draft），加一个 `DraftOn`（draft 在这个键上放了什么，给键帽和条带画）。
- `Ui/Widgets.cs` 加 `Grab`：`Grab(Func<bool> can)`，答案每帧现算，所以槽里绑上或解绑不用重建。`HudBar.UpdateHudHover` 沿父链找它决定抓手光标（`Slot` 仍然直接给抓手），`Menu.cs` 的 `_hoverItem` 跟着改名 `_hoverGrab`。
- 三个可放区命中方式：键格 = `Spots.Keys` 的屏幕矩形；动作池 = `PoolRect()`（放到这里 = 解绑，combo 模式也一样）；快捷槽 = 先 `QslotRect()` 粗筛，再一次 `Array.IndexOf(_cells, _uiScene.TopAt(gx, gy))`（-1 = 不在条带上或被盖住，一次就同时回答了"第几格"和"被没被盖住"）。三处都要过"最上面那个对象是不是我的"：键格和池子用 `OnKeyWindow`（走父链到窗口的 `Host`），快捷槽靠 `TopAt` 返回的就是那一格本身。被别的窗口盖住的点不收，点击归那个窗口。
- **键窗开着时条带画的是 draft**（`SlotTarget` → `DraftOn`）：放到槽上和放到那个键上一样，放下立刻能看到。副作用：键窗停在别的 preset（1/2/3 页）时，条带画的是那一页的 draft，不是当前生效的那套——这是"条带显示你要保存的东西"的代价。道具例外：draft 里没有道具表，道具永远读实时表。键窗关着时，放到槽上直接绑实时表（`BindLive`），技能窗抓起技能可以直接放槽。
- 技能窗：点技能行 = 拿起 + 选中，不再要求键窗开着（第 6 条）。拿起后那一行的图标不画，和池子格、键帽一样。
- **空出原位置只在画的时候判断**（`Held == target`），不重建树：池子格、键帽、技能行、条带格四处同一个写法（键盘的键整格空，连字母都不画；条带格只空图标、键帽留着，看得出是哪个键）。所以拿起和放回都不 `Build()`；只有真正改了 draft 的放下才重建整窗（`Bind`/`AddKey` 返回"改了没有"，`ApplyDrop` 按它决定要不要 `Build()`）。
- Esc 和右键：只 `ClearHeld()`（`MushTownGame.Update` 里两处），什么都不改，也不用重建。拿着东西时 Talk 键不打开对话（`!Holding`）：一是对话窗是模态，开着的时候三个可放区都会被它的空白拦掉（手上的东西放不下去）；二是图标画在对话上面，难看。
- **热键关窗不丢手上的东西**：用 `\` 关键窗、用 K 关技能窗都不清 `Held`。点窗上的 X 是一次普通点击，`UpdateHudClick` 先 `ClearHeld()` 再执行关闭，所以那条路会先把手上的东西放回去，和"点别处"一样。只有 Esc、右键、点到别处、退回选角（`ToChars`）会清。
- 光标：池子格、键帽、技能行、有动作或技能的槽都有 `Grab`，移上去都是抓手。

**没做**：道具拖放（第 5 条的道具一半）。背包窗的拿起是背包窗那片的事，K3 只接了动作和技能；槽里的道具点不动，`Pickable` 只认 `Ui`/`Skill`。§3.4 里一起提到的椅子、面板按钮的统一点击式设计也没做。

**验收**：31 张截图（`--aiticks` 3 张）和改动前**逐字节一致**（`logs/k3_before` / `logs/k3_after3`）——K3 只改点击处理，没改一个像素；`keys` 那张也一致：draft 和实时表在动作和技能上相同，demo 的 D1/D3 绑着两瓶药（draft 里没有道具，落回实时表），画的还是那两瓶药。手测清单见 §5 第 12 条。

### 3.7 HUD 条（N1，2026-10-30）

HUD 条上还剩 4 处 `Invoke`：气泡、手拿、tips、快捷栏的格子。它们要的数据本来就都在 `MushTownGame` 上，所以拆成四个组件，构造时把「怎么取数」传进去，绘制全在组件里：

- **`Balloon`**（`Ui/Balloon.cs`）：消息（`Func<string>`）、脚在屏幕上的位置（`Func<Point>`）、`panel-balloon.json`。换行缓存跟着组件走，只在消息或宽度变化时重算。
- **`HeldItem`**（`Ui/HeldItem.cs`）：手拿的东西画在鼠标上，取的是「手上那件的图，空手就空串」。
- **`Tips`**（`Ui/Tips.cs`）：文字 tip（`HoverTip`）+ 物品 tooltip（`ItemTip`）。tooltip 的正文行用 `ItemLines.Detail`（`Ui/ItemLines.cs`），属性窗的详情框也调同一个函数：以前它在 `HudStat` 里是 private，`HudBar` 靠同一个 partial class 直接调；现在搬到 `Ui/` 公开，两处都能用，也能头测。
- **`QuickCell`**（`Ui/QuickCell.cs`）：一格快捷栏 = 绑定目标的图标 + 官方键帽。目标每帧现读，改绑不用重建；N2 之前四个提示格用 `Layout.HintIcon/HintCap`、十二个物品格图标居中键帽在右下，N2 已改成 16 格一个画法（那两个字段也删了，见 §3.8）。纹理要按需加载，组件不能往游戏的缓存里加，所以把 `LoadArt(name)` 传进去。

**结果**：`HudBar.cs` / `HudQuickSlots.cs` 的 `new Invoke` 都是 0，全仓 18 → 14。头测 2 条（`ItemLines` 的顺序、没有的项不写），客户端 49 → 51。27 张 `--shot` + 6 张 `--aiticks` + `--hit`/`--skill` 全部逐字节一致。

**截图怎么验这三条绘制路径**：`--shot` 不跑 `Update`，所以悬停要自己补。加了两条按文件名摆拍的规则（和已有的 `*balloon*` 一样）：名字含 `hptip` → 鼠标停在 HP 条上（文字 tip），含 `itemtip` → 停在背包里那枚戒指上（物品 tooltip），画之前补一次 `Layout()` 和 `Hover()`（`MushTownGame.ShotMouse`）。三张对照图在 `logs/n1_before` / `logs/n1_after`：旧代码和新代码逐字节一致。手拿那 4 行没有摆拍（要真的拿起一件东西），只能手测（§5 第 14 条）。背包窗（`HudInventory.cs`）本来就没有 `Invoke`；K3 只接了动作和技能，道具的拿起推迟到背包窗那片。

### 3.8 快捷槽一套 + 设置旋钮（N2，2026-10-30 已验收并提交 41b50f4）

**现状**：16 个槽已经是同一个 `SlotKeys` 数组（Ctrl、Alt、I、C、1-9、0、-、=），但「一套」只到键为止，其余是两份：

- 格子几何写死在代码里：`QslotCell(i)` 的 35px 步长和 32px 格子（`HudBar.cs`），只有条带的左上角来自 `hud-layout.json` 的 `qslot`。同一条带的位置在 json、尺寸在代码，改一个要改两处。
- 「4 个键提示 + 12 个物品格」是代码里的分支：`QuickCell` 里 `index < HintCells` 决定键帽画左上还是右下、图标要不要偏移，偏移读 `Layout.HintIcon/HintCap`，`CapPad/CapShadow` 又是组件里的常量。第 5 个槽想换一种画法只能改代码。
- 设置窗的音量旋钮还是 `new Invoke(gfx => DrawVolumeKnob(...))`（`HudSettings.cs:118`），是那个文件最后一处。

**目标**：16 个槽的**画法只有一份**（没有「提示格」这种区分）；旋钮变成组件。内容不动（键不变、槽里绑什么不变），不加编辑、不加拖放——拖放归 K3（§3.6），那时三个可放区都按同一套格子命中。

**设计**（用户定：16 格画法完全一样，没有「提示格」这个概念）：

1. **16 个键留在 `HudQuickSlots` 的私有 `SlotKeys`**。中间试过搬成 `Ui/QuickSlotKeys.cs` 以便头测，但那条测试只是把常量抄一遍、抓不到错，删测试后新文件就只剩一个调用点，属于为抽象而抽象，撤回。
2. **`QuickCell`** 一个画法：键帽画在格子左上角（`Layout.QslotCap`），图标居中。构造参数从 `int index` 缩到 `(cap, iconArt, load)`，`HintCells` 常量和「第几格」的判断整个删掉。`hud-layout.json` 的 `hintcap` / `hinticon` 合成一个 `qslotcap`（图标居中，不再有偏移）。
3. **`Ui/Slider.cs`**（新）：一个对象 = 轨道矩形 + `Button` + `Slider` 组件。`Slider(Func<int> volume, Action<int> set, Action released)`（轨道宽度只从自己的 `Rect` 读）自己画旋钮（`ui/opt_knob.png`，`Screen.X + volume * travel / 100`），自己管拖动（`Grab(gx)` 抓住、`Drag(held, gx)` 跟手、`Release()` 放掉——松手和关窗都走它，关窗那次不跟手），状态 `_dragging` 在组件里，游戏里没有全局的「正在拖哪一条」。`VolumeAt(clickX, trackW)`（clickX 相对轨道左边缘）是 static 纯函数，能头测；`released` 在松手时回调一次，让游戏只存一次盘。`HudSettings` 删掉 `DrawVolumeKnob`、`VolumeAt`、`SliderKnobW` 和那处 `Invoke`，只留一个 `List<Slider>`；`UpdateHud` 里一行 `foreach (var slider in _sliders) slider.Drag(held, gx);` 把指针递给每个 slider，各自判断是不是自己；`CloseSettings` 里对每个调 `Release()`，窗口关掉时拖动不会残留。

**不做**：不加编辑（16 个键固定，见 §8 #4）、不做快捷槽页（#12）、不接拖放（K3）、不动槽里现在绑的东西。

**原计划的 `layouts/panel-qslot.json` 没做**：格子的 32/35 和条带的 557/67 已经在 `QslotCell` / `QslotRect` 一处，`hud-layout.json` 的说明写着「尺寸留在代码，必须跟美术对齐」。为 4 个常量开一个新文件是配置膨胀，不是这一片要解决的问题。真要挪，等改条带美术时一起挪。

**结果**：`HudSettings.cs` 的 `new Invoke` = 0，全仓 14 → 13。客户端 51 → 54（`SliderTests` 3 条；没有为 16 个键加测试：它只是把常量抄一遍，抓不到错）。`MushTown.Tests/LayoutTests` 的 `Full`/保存/恢复也跟着加了 `qslotcap`（条数不变）。画面变了：12 个数字从右下角移到左上角，4 个动作格的键帽也跟着从 (2,2) 挪到 (2,3)（用户后来把 json 调成 (2,3)）；12 个物品格的键帽从 200 灰改成白色。键帽没有阴影（参考图也没有，用户 2026-10-30 定）。对比图 `logs/n2_strip_before.png` / `logs/n2_strip_after.png`。截图核对：8 张（选角/存档/服务器等不带 HUD 的）逐字节一致；22 张带 HUD 的只有条带那一块变（条带以上 y<1940 逐字节一致）；`settings` 另有一处不同，是用户点过音量条（存档 41/86 → 90/37），同一个音量下新旧代码 A/B 逐字节一致。顺带修了旋钮点击偏移：原来按方块左边缘算，点中心每点一次右移 10px；现在按中心算，点方块中心数值不变（101 个值全验过）。

**验收**：31 张截图（`--aiticks` 3 张：10/40/100），条带以外的像素逐字节一致；`grep "new Invoke" MushTown.Client/Panels/HudSettings.cs` = 0，全仓 14 → 13；头测覆盖 `Slider.VolumeAt`（点方块中心不动、两端夹住）和 `Slider.Drag`/`Release`（只动被按的那个、松手存一次、值没变不存）；手测：对着音量方块中心连点，方块不动；点轨道别处，方块跳过去；按住方块左右拖，音量跟着走，松手就停。

### 3.9 聊天窗（N3 第 1 周，2026-10-31）

**现状**：`HudChat.cs`（497 行）只有外壳是对象树：`Build()` 给框、标签、滚动条、输入行、按钮各建了 `Rect` + `Button`，但文字和状态都在窗口自己身上，`Invoke(Draw)` 一口气画框、标签、日志、输入行，`UpdateKeys` 自己读键盘和鼠标。

- 日志：`_lines` / `_visible` / `_scroll` / `_tab` 四个字段在窗口里，`FillVisible` / `LogRows` / `MaxScroll` / `ScrollPos` 四个纯计算也是；滚动条反过来用两个 `Func` 问窗口。
- 日志一行只画一遍，**不折行**：长句直接冲出框架。`Balloon` 早就在用 `gfx.Ui.Wrap`，聊天没用。
- 输入行：`_input` 字符串和 `_focused` 在窗口里；光标**常亮**，不闪。
- 框：`DrawFrame` 直接调 `NineSlice.Draw("chat_frame", ...)`，是 §3.2 说"组件化时再换 `Box`"的最后一处之一。

**目标**：日志和输入行各自变成一个组件，窗口只留框、标签、按钮和键盘。像素不变（聊天在主界面上，31 张截图都得逐字节一致）。

**设计**：
1. **`Ui/ChatLog.cs`**（新）：一个对象 = `Rect(LogRect())` + `Button` + `Box("chat_frame")` + `ChatLog`。它持有 `_lines` / `_visible` / `_rows` / `_scroll` / `Current`（标签），对外 `AddLine` / `AddSystem` / `Scroll(rows)` / `ScrollPos()` / `Scrollable`。`Draw` 先按 `gfx.Ui.Wrap` 把每行折成若干 `Row`（具名行折在名字后面，后续行缩进对齐名字），再按 `Fits(高度)` 从最新的往上画。滚动条的两个 `Func` 改成读 `ChatLog`。
2. **`Ui/CaretText.cs`**（新）：持有 `_text` / `Focused`，`Type(char)` / `Backspace()` / `Clear()`；`Draw` 画官方三段式输入条、文字和**闪烁**的光标（`gfx.Time` 每 500ms 亮灭一次）。100 字上限跟着组件走。
3. **`UiGfx.Time`**（新字段）：`DrawHud` 每帧从 `_timeMs` 填。`--shot` 下 `_timeMs` 是 0（静态画面），所以摆拍不会出现随机相位的光标。
4. **框换成 `Box("chat_frame")`**：日志对象加一个 `Box` 子组件。`Draw` 只剩滚轮读取和标签（标签要按 hover 换三张图，`Box` 是固定 stem，留给下一步）。
5. 窗口删掉 `_lines` / `_visible` / `_input` / `_scroll` / `_tab` / `_focused` / `MaxLines` / `MaxInput` / `DrawLog` / `DrawInput` / `DrawFrame` / `FillVisible` / `LogRows` / `MaxScroll` / `ScrollPos`；`AddLine` / `AddSystem` 转给 `ChatLog`，`Focused` 转给 `CaretText`。

6. **鼠标读取只剩一份**：`_input.Poll` 从「聊天之后」挪到「聊天之前」，`Update` 里一开头就读一次鼠标（`ClickEdge` / `MouseGamePos` / 按键状态），聊天读 `_game.Input.Mouse/Click/Wheel`。聊天删掉 `_mouseDown`。
7. **`Capture` 落地**（§3.1 的设计）：`InputController.Capture` 为真时，`Poll` 把每个动作的 `Fresh`/`Down` 直接置假，原始键盘照常返回。游戏每帧写 `_input.Capture = _chatWindow.Focused`，不再依赖「先把 `kb` 清空再 Poll」——清空 `kb` 仍然保留，因为方向键和 Up/Down 是直接读 `kb.IsKeyDown` 的（那条路不走 action）。

8. **标签也是组件**：两个标签各是 `Rect` + `Button` + `Image("ui/chat_taball")`，`Image` 自己按 hover/pressed 换 `_n`/`_h`/`_p` 三张图，`DrawTabs` 和 `_tabObjs` 删掉。

**结果**：`HudChat.cs` 497 → 376 行，`Ui/ChatLog.cs` 129 行、`Ui/CaretText.cs` 49 行；`Input.cs` +9 行。窗口自己画的东西只剩折叠状态下那个会随 hover 换图的小框。31 张截图（`--aiticks` 3 张）和改前逐字节一致：短句折行后还是自己那一行，日志、输入条、框和标签的位置与画法没动。客户端 55 条、共享/服务端 296 条不变。

**用户验收提的三条（2026-10-31，当场改）**：
1. **输入框内容会溢出**：`CaretText.Draw` 先算可用宽度（`BarW - fieldX - capW`），超长就按源矩形裁成尾巴（`src = (t.Width - w, 0, w, h)`），光标跟着可见的尾端走，不再盖住右边的按钮。
2. **按住字母只打一个**：`UpdateKeys` 收 `dtMs`，加键盘重复——按下先打一个，按住 400ms 后每 40ms 再打一个（Backspace 同样会重复）；`FocusInput` 和 `Send` 都把重复状态清掉，所以开关输入不会误触发。
3. **折叠态不对**：按参考图改成一行——`chat_frame_min` 九宫格 + 最近一行（超长同样裁尾巴）+ 右端一个白色 `+`；单击整条折叠框展开（`minW` 60 → 315，新增 `minPad`）。同时删掉窗口上不需要的第三个标签格（官方那张空标签图，`chat_tab_n.png` 不再加载，`EmptyTabRect` 删掉），标签只剩 All / Etc。

4. **输入框平时隐藏**：照参考图改成"日志常在、输入行临时"——没在打字时只画标签 + 日志框 + 右边那一排小按钮，`To All` 和输入条都不画（`Build` 里按 `_caret.Focused` 才挂这两个对象）。回车（或聊天键）开始一行，回车发送、Esc 取消、点日志/点别处都会收起输入行（`Unfocus` 里置 `Dirty` 重建）。判断回车的位置放在聊天处理之前（`MushTownGame.Update`，Poll 之后），否则"回车发送"和"回车开始新行"会在同一帧撞车。

5. **按钮全删、只留一个 chevron、窗口可拖**（用户 2026-10-31 第二轮）：`chat_btn_set` / `chat_btaddtab` / `chat_btmax` / `chat_btoutchat` 四个按钮和它们的贴图加载一起删掉。只留 `chat_btmin` 那个 chevron，摆到标签行右上角（`chevGap`），点它折起。折起那条仍是最近一句 + 右端 `+`，但**只有 `+` 那 18px（`plusW`）能展开**，其余部分改成拖动条。聊天窗可拖：展开时拖标签行中间那段空白（`TitleRect()` = 第二个标签右边到 chevron 左边），折起时拖整条（`+` 除外），走 `UiWindow.StartDrag` + `DragWindows` 那套，拖动只改 `Pos` 不重建。没在打字时窗口高度只到日志框底边（`Rect()` 取 `LogRect().Bottom`），输入行出现时才长到 150，所以展开态平时就是"标签 + 日志"。

6. **折叠往下收、展开往上长 + 标签行可整排平移**（用户 2026-10-31 第三轮）：折起来的那一条改成占**开窗盒子的最下面一行**（`Rect()` 折叠态 = `(0, h - minH, minW, minH)`），所以收起是"往下收"，展开是"往上长"，底边不动。为此开窗高度固定为 `h`，不再按有没有输入行变高（空闲时日志框下面那 28px 不画东西，但窗口照样吃点击）。根节点的 rect 仍在盒子左上角，折叠时子节点的 rect 从 0 起算。`DragWindows` 的夹取改用 `Rect().Bottom`，这样 rect 有纵向偏移的窗口（折叠的聊天）不会被拖出屏幕。标签行新增 `tabX`：整排标签一起左右平移，不用逐个改。

7. **收起条的 hover、回车自动收回、字体颜色、layout 位置键**（用户 2026-10-31 第四轮）：
   - hover：`chat_frame_min` / `chat_frame_min1` 是官方两张（`view/backgrnd_min`、`min1`），"hover 换第二张"是我加的，已删——收起条不再随鼠标变样（它现在是拖动条，冒 hover 反而误导）。
   - 回车自动收回：`FocusInput` 在"从收起状态展开"时置 `_foldWhenDone`，`Unfocus` 里据此折回去；`Minimise`/`Restore` 清掉这个标记，所以用 `+` 打开不会自己收。
   - 字体颜色：`ChatLog` 把名字色/系统句色提成 `NameColor`/`SystemColor`，新增 `NewestRuns`（`Run(Text, Color, Gap)`），收起条按日志的排法画整条最新消息——蓝名字 + `nameGap` + 白正文，系统句是一整段金色，从左往右画、放不下在结尾截断；顺带去掉了原来拼在中间的冒号。超长仍从左裁。
   - layout：`tabW`/`tabH` 进过代码常量又回到 json（第 3 轮审查指出 `Image` 会把贴图拉伸到框，尺寸改了看得出来）；现在 `tabW/tabH/chevW/chevH/targetW/targetH/scrollW` 都在 json。实测把 `tabX` 改成 40，两个标签整体右移 40 ✓。

8. **layout 改了不生效的真相**（用户 2026-10-31 第五轮）：我上一轮动 `_help` 时把 `the "+" box` 里的引号直接写进了 JSON 字符串，整个 `panel-chat.json` 变成非法 → `PanelLayout.Read` 的 `catch (JsonException)` 把错误吞掉，游戏一直用内置默认值，**所以用户改的任何键都不会生效**（我的 `tabX: 40` 自测也没动，当时看错了截图）。修法三条：
   - 重写 `panel-chat.json`，`_help` 里不再出现裸引号，`ConvertFrom-Json` 验过；`tabY` 设 4（标签贴到日志框上沿）、`targetX` 保留用户改的 0（`To All` 贴最左）。
   - `PanelLayout.Load` 仍吞解析错误（启动必须能起来），但 `Read` 不再吞：热重载时抛给 `ReloadLayout`，由 `TryReloadLayout` 写一行 `layout: ...` 到 `logs/net-*.log`。配 `Load_BrokenJson_KeepsTheDefaults` / `ReloadAll_BrokenFile_KeepsItsNumbersAndReadsTheRest` 两个测试。
   - 热重载撞上"编辑器还占着文件"（`IOException`）时不再放弃：`TryReloadLayout` 返回 bool，失败 300ms 重试，最多 5 次（永久坏掉的文件只留一行日志，不死循环）。

9. **chevron 跟着标签行走**（用户 2026-10-31 第六轮）：用户把 `tabY` 改成 2，chevron 停在第 0 行，两者错开。`ChevRect()` 的 Y 改成 `_lay.Get("chevY", _lay.Get("tabY", 0))`——没写 `chevY` 时自动跟标签行对齐，要单独挪就加一个 `chevY`。拖动条（`TitleRect()`）的 Y 也跟着改成 `tabY`、高改成固定的 `TabH`（原来还在读已经删掉的 `tabH`）。

10. **收起条显示"最后一整行"**（用户 2026-10-31 第七轮）：之前收起条画的是"最新一条消息整段塞进一行"，一条消息在日志里占两行时，收起后两行被并成一行。现在改成画日志**最后一行（wrap 后的最后一行）**：`ChatLog.NewestRowRuns` 复用 `FillVisible` + `WrapRows` 拿 `_rows[^1]`，颜色和间距跟日志一致，仍从左裁。摆拍 `*chatmin*` 顺手加了一条会折行的长句，好核对。

11. **收起条把同一行叠着画**（用户 2026-10-31 第八轮）：`ChatLog.NewestRowRuns` 往调用方传进来的 `List<Run>` 里**只加不清**，而那个 list 是窗口的字段、每帧复用——跑几秒后同一行被重复追加，`skip` 只留尾部 `maxW` 像素，于是尾部窗口里出现"上一份的尾 + 间隙 + 下一份"，看起来像中间被截断。修法：`NewestRowRuns` 开头 `runs.Clear()`。**这类每帧累积的 bug 用 `--shot` 抓不到**（摆拍只渲染一帧），以后碰到"跑一会儿才出现"的显示问题，要按帧累积的思路查。

12. **收起条左端留了一大段空白**（用户 2026-10-31 第九轮）：`skip = total - maxW` 在文字比收起条短时是**负数**，而绘制循环里 `else { x += run.Gap - skip; }` 把这个负数当成"要跳过的量"反向加到了 x 上，于是短文本被整段推到右边（看着像居中/右对齐）。修法：`skip` 用 `Math.Max(..., 0)` 夹住，放得下就从左端 `pad` 开始画。验证：短的一行折起后紧贴左端（截图 `logs/f_chatmin.png`）。

13. **收起条改成"最后一条消息从头显示、尾巴截断"**（用户 2026-10-31 第十轮，定稿）：上一版画"日志最后一行（wrap 之后）"，看着很怪（只剩半句 `dddd…`）。现在 `ChatLog.NewestRuns` 给整条最新消息——蓝名字 + `nameGap` + 正文（系统句一整段金色），收起条从 `pad` 开始从左往右画，宽度用完就把最后一个字**从中间截断**，不再从左边裁。上一版的 `NewestRowRuns` 删掉。

**新增摆拍**：`--shot *chatmin*` 折起聊天，`--shot *chattype*` 在输入行打一串超长文字（`PoseShotChat`，和 `*hptip*` / `*itemtip*` 同一套按文件名摆拍）。

14. **审查修的一批**（2026-10-31，自主 review-loop 第 1 轮）：
    - **P0 坏一个 json 就冻住整次热重载**：我上一轮把 `PanelLayout.Read` 改成抛异常，但 `ReloadAll` 没逐文件兜住，而且 `layouts/panel-party.json` 的 `_help` 里本来就有裸引号（`the "not in a party" layer`），从 10-22 起就没解析过。结果：改 `panel-chat.json` 时重载在 party 那里抛出，后面的 `BuildScreenScene()` 不跑 → 界面完全不动。修法：`ReloadAll` 逐文件 `try/catch`（`JsonException` / `InvalidOperationException`），坏文件保旧值继续读别的，错误以 `List<string>` 返回；`TryReloadLayout` 把每条写进日志但返回 true（解析错误重试没用），只有 `IOException` / `UnauthorizedAccessException` 才重试。party 文件的裸引号清掉。测试 `ReloadAll_BrokenFile_KeepsItsNumbersAndReadsTheRest` 改成断言"坏文件保旧值 + 别的文件照读 + 返回 1 条错误"。
    - **P0 `_foldWhenDone` 没清**：`Minimise` / `Restore` 都补上清标记，否则"回车展开 → chevron 折起 → `+` 展开 → 发送"会把窗口自己折回去。
    - **P1 看不见的点击吞区**：`UiWindow.Sync` 原来 `Place(Host, Pos, Rect().Size)`，折叠时 `Rect()` 的 y 偏移 130 没算进去，宿主命中框落在展开盒左上角。改成 `Pos + box.Location`。
    - **P1 折行宽度不跟热重载**：`ChatLog._wrapW` 是构造参数，改 `w`/`logPadX` 不生效。改成 `WrapW` 属性，`Build()` 里按 `LogTextW()` 重设。
    - **P1 `_repeatKey` 失焦不清**：点输入框外面 / Esc 之后重复块还会再打一个字。`Unfocus()` 里清 `_repeatKey`/`_repeatMs`。
    - **P1 `_wrapCache` 无上限**：聊天每发一句就多一条永久缓存，给到和贴图缓存一样的 `CacheCap` 上限、满了整体清空。
    - 清理：`TitleRect()` 的折叠分支（死代码）、输入行的死按钮、四处过期注释。

15. **第 2 轮审查修的一批**（2026-10-31）：
    - **P0 收起后加号点不开、整条拖不动**：上一轮给 `UiWindow.Sync` 加 `Pos + Rect().Location` 之后，折叠窗的根节点还带着 `Rect()` 的 y=130，`Scene.Layout` 又加一次父偏移，命中框落到 `Pos.Y + 260`（屏幕外）；画的时候用 `OnScreen(Rect())`，所以截图完全正常。修法：根节点的 `Rect` 改成从 `(0,0)` 起，偏移只由宿主带一次。补了测试 `Sync_OffsetRect_PutsTheContentAtTheOffset`（带偏移 `Rect()` 的窗口 + `TopAt` 命中）。
    - **P1 点聊天框自己的地方会漏到世界**：`UpdateKeys` 在 HUD 分发点击之前就把窗口折起，点击点落空 → `NpcClick` 跑世界点击。改成"延后一帧"：`Unfocus` 只置 `_foldPending`，真正的折叠在 `Draw` 里做（那时点击已经分发完）。
    - **P1 收起条跟随页签**：`NewestRuns` 改用 `_lines[^1]`（全局最新），不管当前停在哪个页签。
    - **P1 语法错被当成可重试**：`TryReloadLayout` 拆成两个 catch——IO/权限错返回 false 重试，JSON/根不是对象返回 true 只记一行。
    - 小项：`logGap` 进 json（替掉 `LogTextW()` 里写死的 `-2`）；`WrapW` 去掉重复的 40 下限；按住连发只对能打出字符的键生效（按一下方向键不再打断字母连发）；`ReloadAll` 去掉多余 `ToList()`；7 处过期注释；补 3 条测试（`Sync` 偏移、`NewestRuns` 三次调用、**所有 `layouts/*.json` 必须能解析**——party 那个坏文件挂了 9 天没人发现）。

16. **第 3 轮审查修的一批**（2026-10-31）：
    - **P0 IO 错会让游戏退出**：上一轮把 catch 收窄到 JSON 两类，结果保存时文件被占（`IOException`/`FileNotFoundException`，仓库在 Documents 下，OneDrive 也会锁）会从 `Update` 抛出去 → `Program` 写 crash 文件退出；同时计划里写的"300ms 重试 5 次"变成死代码。现在分两个 catch：IO/权限 → 记一行、返回 false 走重试；JSON/根不是对象 → 记一行、返回 true 不重试。
    - **P0 盒子尺寸又写死在代码里**：上一轮把 `tabW/tabH` 从 json 删掉换成常量，理由是"图按原尺寸画"，这个理由是错的（`Image` 会把贴图拉伸到 Rect）。现在 `tabW/tabH/chevW/chevH/targetW/targetH/scrollW` 都回到 json，代码全走 `_lay.Get`。
    - **P1 在 `Draw` 里改状态**：折叠挪到下一帧 `Update` 开头的 `ChatWindow.Tick()`，`Draw` 变纯；顺带消掉"发送那一帧折叠条闪一下"。
    - **P1 `Capture` 没测试**：补 `Poll_CaptureBlanksTheActions_AndKeepsTheKeys`（捕获时空动作表、按键边沿仍在、放开后不算新按下）。
    - 小项：`Math.Max(..., 40)` 改 1（只为不让宽度为 0）、`WrapW` 改自动属性、`Send` 去掉重复清重复态、`NewestRuns`/折叠相关注释、双空行。

17. **第 4 轮审查修的一批**（2026-10-31）：
    - **P1 Esc 那一帧会吃掉输入**：上一轮为了读取点击把 `Poll` 挪到了 Esc 早退之前，结果按 Esc 关窗的那一帧，同帧按下的键/点击被消费掉（`Poll` 更新了按下表，下一帧不再算新按下）。现在 Esc 那层回到 `Poll` 之前，条件是 `!_chatWindow.Focused`（聊天有自己的 Esc）。
    - **P1 全局 layout 坏文件仍会冻住整次重载**：`Layout/MenuLayout/CharLayout/JobLayout/SetupLayout` 五个 `Load` 之前没有逐文件兜底。现在统一走 `Guard(errors, path, () => Load(...))`，坏文件记一行、继续读别的，最后照常 `BuildScreenScene()`。
    - **启动期 IO 错不再崩**：`PanelLayout.Load` 也吞 `IOException`/`UnauthorizedAccessException`（文件被 OneDrive 或编辑器锁住时保默认值，游戏必须能起来）。
    - 小项：`Minimise` 改走 `Unfocus()`（重复态只在一处清）、连发遇到长帧不再一口气补按一长串（把 `_repeatMs` 夹在 `RepeatDelayMs - RepeatRateMs`）、`WrapW` 去掉推测的默认 40、`Scroll`/`WrapRows` 的注释按真实行为重写、"一次鼠标读取"那句改成实际情况、`TheShippedLayouts_Parse` 只扫 `panel-*.json` 和 `*-layout.json`（别人的备份文件不该让它红）。

18. **第 5 轮审查修的一批**（2026-10-31，无 P0）：
    - **死 catch 删掉**：所有布局解析都在 `Guard` / `PanelLayout.ReloadAll` 里逐文件兜住了，`TryReloadLayout` 第二个 catch 永远命中不了；留着还会把别处真正的 `InvalidOperationException` 当成"布局写错"吞掉。
    - **导出器对齐**：删掉已不加载的 `chat_btn_set` / `chat_frame2` / `chat_frame_min1` / `chat_tab` / `chat_btaddtab` / `chat_btmax` / `chat_btoutchat` 七组导出，只留 `chat_btmin`（折叠箭头）；头注释和完成行改成实际内容。
    - **重试预算按每次保存算**：watcher 的 Changed/Renamed/Created 都清 `_layoutTries`，连续保存不会再提前用完 5 次。
    - **折叠条宽度只用一个键**：`minW` 删掉，折叠条跟 `w` 同宽（原来两个键默认相同，改一个忘另一个就对不齐）。
    - **滚轮挪出 Draw**：`ReadWheel()` 进 `Tick()`，`Draw` 只画；滚动条 `_enabled()` 只读一次；`TheShippedLayouts_Parse` 在没有仓库时 `Assert.Inconclusive`。
    - 挂账（P2，已记）：`ReloadLayout` 可能半提交（后面的文件 IO 失败时，前面的已经写进静态对象，靠重试或下次保存收敛）；别的窗口盖住输入行时 Enter 仍能开聊天；超长名字不裁剪会出框；满格时光标压右端帽 1px；字号仍是全局 12px 没进 json；折叠/发送/连发的状态机没有单测（要 `MushTownGame` 实例，靠手测）。

19. **第 6 轮审查修的一批**（2026-10-31，无 P0）：
    - **P1 展开态多出 28px 看不见的吞点击带**：根节点命中框用 `Rect()` 全高（150），但没打字时输入行不画（日志框到 122），那片空白会吃掉下沿的点击。改成按状态取高：折叠 `minH`、打字时到 `inputY+inputH`、平时到 `logY+logH`。
    - **P1 启动时的 layout 错误静默**：`GameLog` 只写 `_rollLog`，而它在构造函数第 169 行那次 `TryReloadLayout()` 之后才建。现在建日志提前到那次 reload 之前（截图路径仍不写日志）。
    - **P1 手写 `CharOf` + 手写连发**（建议改用 `Window.TextInput`）：**驳回**。理由：`TextInput` 是否会在按住时重复依赖 SDL 与系统设置，这个环境没法验证，删掉连发就是拿需求 2（长按连发）去赌；而且走 `TextInput` 会顺带支持中文/大写输入，用户 2026-10-31 明确说中文输入不做。等哪天真机确认 `TextInput` 会重复，再删 `CharOf` 和整套连发。
    - 小项：`TitleRect()` 折叠态返回 `Rect()`（折叠条没有标题条）、`FoldForShot` 不再改数据（只摆状态，长句折叠：文件名同时含 `chatlong` 和 `chatmin`（如 `chatlong_chatmin.png`）两条摆拍都会触发）、`Tick` 改名 `UpdateFrame`、导出器去掉没人加载的 `prev*/next*` 滚动箭头、`Math.Max(end - x, 0)` 删掉、`Guard` 的日志改成全路径、`TheShippedLayouts_Parse` 找不到仓库时 `Assert.Inconclusive`。

20. **第 7 轮审查修的一批**（2026-10-31，最后一条 P1）：
    - **宿主命中框和内容高度对齐**：第 19 条只把根节点改矮了，宿主（`UiWindow.Sync` 里 `Place` 的那个 Rect）还是整个 150 高，日志框下面那 28px 空白仍然吞点击。现在 `UiWindow` 加 `HitSize()`（默认 `Rect().Size`），`Sync` 用它放宿主；`ChatWindow` 覆写成"折叠 = minH / 打字 = 到输入行底 / 平时 = 到日志框底"，根节点也读同一个值，两处不会再走偏。补测试 `Sync_HitSize_LeavesTheEmptyPartToTheWorld`（窗口盒里没画东西那段 `TopAt` 必须是 null）。
    - `_layoutTries` 改 `volatile`（watcher 线程会清它）。

**结果（含上面十三条验收意见 + 六轮审查修复）：`HudChat.cs` 497 → 465 行（`ChatLog.cs` 129 → 157 行、`CaretText.cs` 49 → 55 行）。收起条、输入行、chevron 这些行为改动都发生在聊天窗自己的区域里，画面其它地方没动；最后两版收起条的样子看 `logs/crop/h_min.png`（名字 + 正文 + `+`）和 `logs/crop/h_open.png`（展开、未打字时没有输入行）。客户端 60 条、共享/服务端 299 条全过。

**没做**：官方那几个没功能的窗口按钮（whisper/link/command/emoticon）的行为；私聊/表情那一排官方 art 的行为。**下一片就是气泡样式**（用户 2026-10-31 定）：角色头顶的说话气泡现在画的是官方 `UI/ChatBalloon.img/0` 九宫 + `balloon_arrow` 尾巴 + 黑字居中，最多 3 行（`Ui/Balloon.cs`、`layouts/panel-balloon.json`）；用户说样式不对，具体要改哪里等开工时确认。
## 4. 分片

工期按每周约 10 小时估。每周一个 commit（用户规矩）；2 周的片第 1 周提交能 build + test 的中间态，第 2 周完成；每片结束停下验收，先 review 后提交。

### 输入线

| 片 | 内容 | 工期 |
|---|---|---|
| K1 (P0) | **第 1 周**：`InputController` + `BoundAction`；绑定进角色（3 套 `KeyPresets` + `ActivePreset`）；KMS 默认键（10 个菜单动作 + 攻击/跳跃/Talk）；技能进表但**无默认键**；`CastSkill`；旧键窗适配到当前 preset（8 个可见动作）；技能窗去掉固定键文字（旧 Q/R/W… 已失效）。**第 2 周**（已做）：HUD 16 槽改成键槽（C 空）；Item 目标；删 `QuickSlots`/`QuickSlotKey`/demo QuickSlots 种子；旧键窗的快捷槽页整个删掉，Quick Slots 按钮只画图、点了不动 | 2 周 |
| K2 (P0) | 键窗：每角色 preset、动作池、组件化（与 N5 合并）、键格固定、动作拖动、Copy 弹窗、技能从技能窗拖；拖到 HUD 槽随 K3；不做快捷槽页。**已做（2026-10-28）**：组件化（N5）；动作池 = 官方 77 张图标全放进去（没做的按了没反应），默认过滤组 = Not Added；Chat/Minimap 接进 `UpdateHud`；删掉 K1 的"隐藏动作的键不许抢"保护；Copy 换成官方 `presetCopy` 弹窗（3 个来源 preset + checked 高亮）；PRESETS 1/2/3 用 checked 图（选中绿）；Skill 钮不再关键窗，技能窗开在上面，从技能行拖到键格写成 `Skill:<id>`（draft 带技能，键格画技能图标）。**审查后补（2026-10-29）**：键格可抓（解绑和 Combo Key 才有入口）、技能落键加拖动门槛和顶层判断、Copy 默认来源改成当前 preset、Default 真的清掉技能、Chat 键不再把自己的字符打进输入框、键帽反查表。**剩下**：无（点击式拖放归 K3） | 2 周 |
| K3 (P1) | 点击式拖放（§3.6，**已提交 986c438**）：hover 抓手（`Cursor/5`）、单击拿、单击放；白色键盘/黑色池子/HUD 槽三区互通；放到黑色区 = 取消绑定；池子固定格位、只列没绑的动作。道具推迟到背包窗那片 | 1 周 |

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
| N1 (P1) | HUD 条：气泡、快捷栏角标、手拿、tips。**已做（2026-10-30，已提交）**：四个组件（`Balloon` / `HeldItem` / `Tips` / `QuickCell`），`ItemLines` 由 tooltip 和属性窗共用；HudBar/HudQuickSlots 的 `Invoke` 归零，全仓 18 → 14；测试 49 → 51；35 张截图逐字节一致。详见 §3.7 | 1 周 |
| N2 (P1) | 快捷槽：4 个键提示 + 12 个物品格合成一套 16 个等价槽（内容先固定）+ 设置旋钮。设计见 §3.8：16 个键留在 `HudQuickSlots.SlotKeys`，`QuickCell` 不再认「第几格」，旋钮成组件 | 1 周 |
| N3 (P1) | 聊天窗（整块，最大）；第 1 周已做（§3.9）：`ChatLog` + `CaretText` + `Box` 框，31 张截图逐字节一致 | 2 周 |
| N4 (P1) | 小地图 + 大地图 | 2 周 |
| N6 (P2) | 任务窗 | 1 周 |
| N7 (P2) | 组队 + GUIDE | 1.5 周 |
| N9 (P1) | 聊天气泡样式：官方 `UI/ChatBalloon.img/0` 九宫 + 尾巴 + 黑字，最多 3 行；用户 2026-10-31 说样式不对，**排在 N3 之后**，具体改哪里开工时确认 | 0.5 周 |
| N8 (P2) | 选角 + 创建角色 | 2 周 |

合计约 19 周（N0 缩到 0.5 周，S4 砍掉，W1 加 1 周）。三块互不依赖，顺序可以换。

**顺序（我定的）**：K1 → N0 → K2+N5 → W1 → S1 → S2 → S3 → N1 → N2 → **K3** → N3 → **N9（气泡样式，2026-10-31 用户插到 N3 之后）** → N4 → N6 → N7 → N8。

理由：先把输入这条线走完（技能解耦 + 每角色绑定是一件事）；再做组件基础，紧接着把键窗改组件；然后**先把 `Rect` 改成父相对**，再动世界和窗口——W1 要改每个窗口的坐标写法，放在 N1/N2/N3/N6/N7/N8 之前，那些窗口就只写一次新写法。**K3 从原来的位置挪到 N2 之后**（2026-10-30 用户定）：点击式拖放的三个可放区里，HUD 16 槽要等 N2 先合成一套，背包要等 N1。

## 5. 每片的验收

1. `dotnet format` 干净；`dotnet build` 0 警告 0 错误。
2. 测试全过（K1 新增 16 条；当前 347）。
3. 受影响的 `--shot` 截图与改动前逐字节一致；**例外**：输入片换了默认键（Stat C→S、Quest V→Q 等），**所有带 HUD 的游戏截图**（第 4 个提示格、MENU 角标）、键窗和**技能窗**（固定键文字已删）按新默认重拍，人工对照官方布局验收。
4. 世界片：`--aiticks 40` 截图一致；新增头测覆盖"推帧 / 播完 / 回收"。S1 已做：6 张 `--aiticks` 图（0/10/40/100 步、1000000 地图 0/40 步）逐字节一致，27 张 `--shot` 也一致；`WorldTests` 7 条 + `SceneTests` 2 条覆盖推帧、闪烁、播完、死节点（step 和命中都跳）、收尸、传送门。**注意**：截图不跑 `Update`，`--aiticks` 只推怪的走位和刷新，所以这些图只证明"绘制查表没变"，证明不了动画推进和回收。回收这一步要靠**手动验收**：进游戏打一只怪，尸体播完消失，不崩。S2 已做：`WorldTests` 4 条（到点死、`Reset` 复活、收尸、换图清场），27 张 `--shot` + 6 张 `--aiticks` 一致，另加 `--hit`/`--skill`（地图 20000）两张在改动前后逐字节一致。手动验收：①打怪时数字上升淡出、自己消失，不崩；②数字还在屏幕上时走进传送门换图，数字立刻消失，换图后新数字的上升速度不变（这条验的是清场时有没有把节点摘出树）。S3 已做：`WorldTests` 4 条（一次性特效推帧播完 + `Reset` 复活、buff 循环绕回不死、buff 循环跟着玩家走、`StripLoops` 只摘那一个 buff 并摘树），27 张 `--shot` + 6 张 `--aiticks` 一致，`--hit`/`--skill` 两张也逐字节一致。手动验收：①放一个带图的 buff（例如 Spirit Blade 那类有 `Affected` 图的），循环图案贴着人走，buff 结束时图案消失；②连续放同一个 buff，图案不会叠成两层；③放一个伤害技能，特效播一次就消失，不崩；④放 buff 后走进传送门换图，图案重新挂在人身上。
5. 输入片第 1 周：手测 10 个菜单键、攻击、跳跃、Talk；换角色后按键跟着换；旧键窗的 Save/Apply/Copy/Default 在 3 套 preset 上生效（1/2/3 切换选中的 preset 并重读 draft，键格跟着换；新 UI 的预览是 K2）；`settings.json` 没有按键字段。
6. 输入片第 2 周：3 套 preset 预览 / Apply / Copy / Save 后重开仍生效；HUD 16 槽按表重画（Ctrl/Alt/I 有内容，C 空，12 个物品格按表）。技能头测（已加，`Fresh_ASkillTargetFiresFromItsKey`：塞一条合成绑定，验证按下那个键时技能目标 Fresh）覆盖输入那一半；`CastSkill` 本身要 GraphicsDevice，只能靠 `--skill` 截图手测。实际游玩要等拖放片，这是已知代价。
7. 代码检查：`grep "new Invoke"` 该文件为 0；**这 16 个**游戏输入字段为 0（`_altWasDown`、`_upWasDown`、`_ctrlWasDown` + 12 个技能键 + `_zWasDown` 拾取）；菜单的 `_escWasDown`、`_menuUpWasDown` 等系统键字段保留（不是绑定键）；`_qsWasDown` 已随快捷栏删掉；`settings.json` 里没有按键。
8. 窗口片（N5 已做）：`grep "new Invoke" MushTown.Client/Panels/HudKeyConfig.cs` = 0；`--shot keys` 和改动前逐字节一致（组件化不许改像素）；手动拖一次动作到键上，图标跟手、松手落在那个键、拖到黑色动作池上把键清掉。
9. N0（已做）：`--shot chars/chars2/setup/equip/minimap/hud` 与改动前逐字节一致（`Box` 的 `mini_frame` 由 minimap 覆盖、`tip` 由 equip 页的物品详情框覆盖；`AvatarPreview` 由 setup 覆盖；`DrawAvatar` 由 hud 和 chars 覆盖）。
10. W1（`Rect` 父相对）：全部 `--shot` 屏幕（27 个）逐字节一致；拖标题栏、拖任务窗不触发 `Build()`（临时计数验证后删）；`grep "Pos.X\|Pos.Y"` 只剩 `OnScreen`、`StartDrag` 和复制对话框的全屏拦截块；一帧里的节点遍历最多 3 次，理由见 §8 第 35 条。
11. K2 动作池（已做）：键窗打开时默认是 Not Added 组，列出 63 个没绑键的动作（官方 icon 全部 77 张图，一张一格；默认表占 14 个）；9 个过滤项（Not Added + 8 个分类）逐组点开都能看到对应图标；键盘上 E 画 EQUIP、U 画 MAPLE GUIDE、`\` 画 KEY BINDINGS、Z 画 PICK UP、Ctrl/Alt 画 ATTACK/JUMP；Chat 键点开聊天输入、Minimap 键只是重建小地图；把 Attack 从键上抓起来、拖到黑色动作池上松手、Save 后攻击真的没键了（K1 的 `Reserved` 保护已删，键格本身可以抓；松手在标题栏、按钮或键缝上什么都不改）；把 `Legion`（没实现）拖到键上 Save，按键什么都不发生；Combo Key 模式下点一下第一个已绑的键拿起、点第二个键放下，做成 `I+K`（K3 之前是按住拖，点一下拿不起来）。`--shot keys` 的像素变化是预期的（池子变大），不用和旧基线比。
12. K3 点击式拖放（已做）：31 张截图（`--aiticks` 3 张）逐字节一致（`logs/k3_before` / `logs/k3_after3`，`keys` 也在内）。手测：①键窗里点动作池的图标 → 图标跟手、那一格空出；②点键盘上的一个键 → 动作落上去、池子里那一格变空、其余格子不动；③再把它从键盘抓回池子 → 键变空、动作回到原来那一格；④点槽里绑着动作或技能的格子 → 跟手、那一格空出；键窗开着时点键盘的键或池子能放下，键窗关着时点另一个槽能放下（存盘，重进还在）；⑤技能窗点一行技能 → 跟手，键窗开着放键盘、关着放槽；⑥拿起后按 Esc 或右键 → 手上没了，什么都没改；⑦把默认的 Ctrl 槽（`LeftControl+RightControl` = 攻击）拿起再放回原槽 → 绑定不变，RightControl 还能攻击（原地放回是空操作，不许把组合键压成单键）；键窗里点 Ctrl 键帽两次也一样；⑧`--host` / `--join` 没有角色时点槽 → 不存盘、不报错，手上的东西放回去、改成拿起被点的那一格（条带在这种会话里是只读的）；⑨拿着东西点窗上的 X → 手上的东西先放回去，再关窗；⑩拿着东西按 Space → 不开对话；⑪过滤列表展开时点心那个键格 → 绑定不变，手上的东西放回去、改成拿起那一格（和点别处一样）。
13. 键窗不挡（S3 同批，已做）：键窗开着时走路、攻击、跳跃、拾取、对话、喝药、7 个面板键全部生效，用的是**已保存**的 preset；在键窗里改键但**不 Save**，关窗后按旧键还是老效果；Save 后**下一帧**新键生效，不用重开；MENU 窗口仍然挡角色。这几处是 `Update` / `UpdateHud` 里的条件，要 `GraphicsDevice`，没有头测，只能手测。
14. N1 HUD 条（已做）：`grep "new Invoke"` 在 `HudBar.cs` 和 `HudQuickSlots.cs` 里都是 0；27 张 `--shot` + 6 张 `--aiticks` + `--hit`/`--skill` 逐字节一致；`ItemLines` 2 条头测；气泡和两条 tip 用 `*balloon*` / `*hptip*` / `*itemtip*` 摆拍，`logs/n1_before` 和 `logs/n1_after` 三对图逐字节一致。手测（截图拍不到手拿）：①鼠标停在 HP/MP/EXP 条上出深色文字 tip；②停在背包或快捷栏的物品上出物品 tooltip（图标、名字、加成、描述），手里拿着东西时不出；③在聊天里发一句话，头顶出气泡，几秒后消失，走开时跟着走；④从背包拿起一件东西，图标跟着鼠标走，放下后回到格子。
15. N2 快捷槽一套 + 设置旋钮（已做）：31 张截图（`--aiticks` 3 张），条带以外的像素逐字节一致（8 张不带 HUD 的整张一致，22 张只有条带变）；`settings` 另因音量状态变化，同音量下新旧 A/B 一致；`grep "new Invoke" MushTown.Client/Panels/HudSettings.cs` = 0，全仓 14 → 13；头测 3 条（`Slider.VolumeAt` 点中心不动和两端夹住、`Drag` 只动被按的那个且松手存一次）；手测：对着音量方块中心连点，方块不动；点轨道别处，方块跳过去；按住方块左右拖，音量跟着走，松手就停。

16. N3 聊天窗（已做，跑了 7 轮自主 review：修了 6 个 P0、12 个 P1，其余记在 §3.9）：`HudChat.cs` 497 → 465 行，`grep "new Invoke" MushTown.Client/Panels/HudChat.cs` 只剩折叠框那一处；行为改动只在聊天窗自己的区域（收起条 / 输入行 / chevron），31 张整图不再逐字节一致是预期的；客户端 60 条 + 共享/服务端 299 条。手测：①按回车 → 展开并出现输入行和光标（平时输入行不画）；②打一句比一行宽的话回车 → 日志里折成多行，第二行缩进对齐名字；③日志行多到超出框 → 滚轮能上下滚，滑块跟着动；④打字时 WASD/方向键不动角色，Enter 发出去之后角色又能走；⑤打字时按绑定的动作键（比如 Ctrl 攻击）不出招；⑥点日志或别处 → 输入失焦，输入行消失；⑦点 All / Etc 两个标签 → 日志换成对应内容；⑧右上角只有一个 chevron，点它折成一行（那一行贴在窗口最下面，展开时往上长，底边不动）；⑨折起那一行显示最新一条的"名字 + 正文"，放不下就在结尾截断；只有右端 `+` 能展开，拖这一行的其它地方能移动聊天窗；⑩收起时按回车 → 展开并进入输入，发送完成或失焦自己收回；⑪按住一个字母不放 → 隔约 0.4 秒开始连续输入（Backspace 也会连续删）；⑫打一句比输入框宽的话 → 只显示末尾那一截；⑬展开时拖标签行中间的空白也能移动。挂账（已知、未改）：收起状态下拖不到窗口顶部（`ClampPanel` 夹的是展开盒的顶边，折叠条最低只能到 y=130）；Esc / 折起 / 失焦不清输入框里的半句话（下次回车会带出来，等用户决定）；点 `+` 展开后打字发送**不会**自动收回（只有回车从收起态打开的那种才收回，用户第 7 轮定的）。

## 6. 风险

| 风险 | 对策 |
|---|---|
| 输入一次改全，所有面板键都可能坏 | K1 只换读取，分发逻辑照旧；截图 + 手测逐键过 |
| 老存档没有绑定字段 | 缺字段 = 默认表；用户已说明旧档删掉重建，不做迁移 |
| 技能在拖放重做前不可用 | 用户在计划里接受；K1 只保证 `CastSkill` 的 id 校验和旧分支不变，实际触发等 K2 绑定 |
| 默认键改了，截图基线变 | 输入片的**所有带 HUD 的游戏截图**、键窗和技能窗明确重拍；其余截图仍逐字节比 |
| 键窗拖到 HUD 槽要跨两套槽 | 排到 K3：N2 已先把 4+12 合成 16 个等价槽，K3 再接拖放 |
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
13. 键窗拖动作到 HUD 16 槽：K3 已做（N2 先把槽合成了一套）。
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
24. 键窗的 Skill 钮不关键窗（2026-10-28；K3 改过）：技能窗直接开在上面（`_raiseRow` 行尾），点技能行就拿起技能（不再要求键窗开着），再点键格或快捷槽放下；关技能窗不丢手上的东西。
25. 主窗口的 PRESETS 1/2/3 用 `button:presetPage1/2/3` 的 `checked` 图（2026-10-28 用户定）：选中的那页是绿色，没选中的是灰色；hover/pressed 用 `_h`/`_p`。
26. 键窗打开时默认过滤组 = Not Added（2026-10-28 用户定）：一开窗就看到还没绑键的动作。
27. 拖放改成**点击式**（2026-10-28 用户定，见 §3.6）：hover 抓手（`Basic.img/Cursor/5`）、单击拿、单击放；白色键盘/黑色池子/HUD 槽三个区都能抓、都能放；放到黑色区 = 取消绑定；池子只列没绑的动作，**格子位置固定、不往前补**。技能同理；**道具推迟到背包窗那片**（2026-10-30 K3 做完时定的）。这替代"按住拖、松手放"。
28. 池子的隐藏规则（2026-10-28 用户定）：`_draft` 里有键的动作一律不画（不管当前是哪个过滤组），所以绑好一个动作它就从池子里消失，取消绑定后回到它原来那一格。
29. 技能组合技（上/下+攻击、右+右、单键先）留给技能大改，K1 不做；K1 只提供原始键、动作边、一帧多动作。
30. 键格也能抓（2026-10-29 审查 P0）：键上已经有动作时，按那个键把动作拿起来，可以移到别的键，或拖到黑色动作池上取消绑定。没有这条，绑上的动作就永远拿不下来（池子只画没绑的），Combo Key 也永远做不出第二个键。
31. 池子按过滤组里的所有动作算格位，绑了的格子留空（2026-10-29）：`Actions` 的下标就是格位，所以「不往前补」对所有过滤组都成立。
32. 取消区只有黑色动作池（2026-10-29 审查 P0-2；K3 改成点击式后 combo 模式也能取消）：点池子才解绑，点在标题栏、按钮、PRESETS 行或键缝上什么都不改。键盘图上的 Tab / Enter / 方向键不在 `keys.json` 里，按「不是键格就解绑」会把动作静默丢掉。过滤下拉列表展开时不接受取消（列表盖在池子上，点它算「什么都不改」）。
33. 键上的技能拿不起来（2026-10-29 审查 P1-2）：`_held` 是 `HotkeyAction?`，装不下技能。**K3 已修**：手上的东西换成 `BindTarget?`，键上的技能也能拿起、换键或放回池子。
34. 老存档的 preset 表没有 `Z -> Pickup`（2026-10-29 审查 P0）：拾取从硬编码 `Keys.Z` 改成查表，`Ensure` 只在 `KeyPresets == null` 时播种，所以 K1 时期建的表里没有这条，切到那套 preset 后 Z 不捡东西。**按 §8 的既有决定不写迁移代码**：删档重建，或在键窗里按一次 Default。理由：自动补种会让「解绑拾取」在重开游戏后失效，那是新功能的真 bug。窗口里能看见 Z 格是空的、Not Added 组里列着 Pickup，不是静默坏掉。
35. W1 的 Scene.Layout() 由 TopAt 和 Draw 各自兜底调用（2026-10-29 审查 P1-1 驳回）：一帧里会跑好几次，每次都是几百个节点的整数加法。K3 让拿着东西的点击再多 1-2 次（先问键格/池子是不是自己的，再问条带），放下成功就直接 return，后面那次不再跑。驳回收成一处的理由：命中要读到本帧刚 Build 出来的树（按一次按钮就重建面板，那一帧的点击必须落在新树上），而 Draw 在 Update 之后跑，只留 Draw 里那一次会让命中永远落后一帧。遍历是几百个节点的整数加法，比这个风险便宜。
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
53. N2 的 json 没做（2026-10-30）：原计划给条带开 layouts/panel-qslot.json 装格子尺寸和键帽偏移。做的时候发现格子的 32/35 和条带的 557/67 已经在 QslotCell/QslotRect 一处，hud-layout.json 的说明也写着「尺寸留在代码，必须跟美术对齐」。为 4 个常量开新文件是配置膨胀，砍掉。这一片只做「组件不再认第几格」+ 旋钮成组件。
54. 16 格画法完全一样（2026-10-30 用户定）：N2 做的时候先按「前 4 格是提示格」留了一个 hint 分支，用户看到后说「quickslots 所有格子都一样，不存在区分」，选了「键帽全部放左上角」。于是删掉 hint、HintCells、hinticon，hintcap 改名 qslotcap，16 格统一：键帽左上 + 图标居中。代价是 12 个数字从右下角挪到左上角，压在物品图上（不加阴影，参考图也没有）。
55. 热重载打断拖动（2026-10-30，review 定的取舍）：DEBUG 下改 json 会重建设置窗，正在拖的音量条被 _sliders.Clear() 丢掉，这次拖动不存盘（音量本身已经生效，下一次新拖动松手或 Cancel 会存；退出游戏时正拖着的那次同理）。BuildSettingsScene 可能从 Draw 里的 SyncLayers 跑，为这个 DEBUG 窄分支在 build 里写盘不值得；和既有的「reload 丢抓取」一致。
56. 坏布局直接崩，不兜底（2026-10-30，review 定的取舍）：`Slider.VolumeAt` 试过 `if (travel <= 0) return 0;`，又删了。理由是仓库对坏布局的既有做法是抛（`Layout.Load` 的 `Load_BadValue_ThrowsAndKeepsOld`），静默夹到 0 反而查不出来。`bgmW/sfxW` 的要求写在 `panel-settings.json` 的 `_help` 里：要宽于 120（旋钮 20 + travel > 100）。宽 ≤ 20 时除零；21..120 不崩，只是点方块中心会偏。
57. K3 的三条做法（2026-10-30）：①手上的东西存在游戏上，类型就是 `BindTarget?`（`Held`，只装 Ui/Skill，由 `Hold` 的调用方保证），不放键窗里——技能窗和快捷槽都要用它；②拿起不改 draft，只有放下才改，所以 Esc 和右键永远"什么都没变"；③键窗开着时条带画 draft（`SlotTarget` → `DraftOn`），关着时画实时表。第三条是为了"放到槽上立刻能看到"：条带和键盘是同一批键，draft 是它们唯一的真相。draft 没覆盖某个键时（比如 demo 的 D1/D3 绑着药水），条带仍画实时表上的那个道具，tooltip 也跟着图标走。
58. 道具还不能抓（2026-10-30）：§3.6 第 5 条只做了技能。`Pickable` 只认 `Ui` 和 `Skill` 两种目标，槽里的道具点不动，也没法从背包抓起放到槽上。背包拿起归背包窗那片。
59. 三个可放区都先问"最上面那个对象是不是我的"（2026-10-30，review 补）：键格和动作池用 `OnKeyWindow`（走父链到窗口的 `Host`），快捷槽用 `Array.IndexOf(_cells, TopAt)`（-1 = 没点到或被盖住）。被别的窗口或面板盖住的点不收，点击归那个窗口——否则开键窗再开技能窗，技能窗底边压在池子上，点那里想放回去会把绑定清掉，而且点击被吞。只挡放下；点一下拿起走 `ClickTree`，本来就落在最上面的对象上。
