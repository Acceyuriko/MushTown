# 装备栏 / 装饰栏 + 现金商店 周计划

状态：第 1、2、3 周已提交；3.5 / 3.6 已完成实现，待用户验收；下一步第 4 周。下面按 10 小时左右切周，一共 13 周，外加 2026-11-09 用户插队的 3.5 / 3.6（排在第四周之前）。
2026-11-09 用户定：伤害公式、战斗力公式、属性面板数值以后单独做一版，不在当前周范围。
2026-11-08 用户定：旧存档不兼容，开发阶段直接删档重开；下面所有“旧存档迁移/补齐”都不做。
2026-11-09 取消性别区分，皮肤见 `CharaSlot.Skin`，按 T-20261109180837 执行。
顺序：先装备栏 / 装饰栏，再现金商店。每周一个 commit，做完停下等验收，不顺手做下一周。

详细规则见：

- [装备栏 / 装饰栏总计划](equipment-2026-12-03.md)
- [现金商店总计划](cashshop-2026-12-03.md)

## 周表

| 周 | 主题 | 这周做什么 | 做完能验收 |
|---|---|---|---|
| 1 | 背包数据 + 6 页签 | 1. `CharaSlot` 加 `Skin` / `Hair` / `Face` / `CashBag` / `DecBag` / `CashWorn` / 四个 order list。<br>2. 新建角色默认值，默认头发和脸（旧存档不迁移）。<br>3. 新建 `Inventory.cs`：`Ensure` / `SlotId` / `Move` / `Sort` / `Remove`。<br>4. `HudInventory` 6 个页签都能点；Equip 页删掉“穿着的装备排在最前”；格子按位置显示；点击拿起、再点放下；整理键只排当前页签。<br>5. 导出 4 个默认头发 / 脸券的 `ItemData` 和图标。 | `--screen inv` 能出图。6 个页签都能点；Etc / Set-up 是空格；拖到空位、同 id 合并、不同 id 交换；整理键正常。新角色有默认头发和脸。build / test / format 全过。 |
| 2 | 通用 ScrollBar + 装备窗壳 | 1. 新建 `MushTown.Client/Ui/ScrollBar.cs`，做通用滚动块：轨道、滑块、`Grab` / `Drag`、normal / hover / pressed。<br>2. 导出背包小窗 / 宽窗滚动条美术。<br>3. 背包各页签按自己的道具行数上下滚（Etc / Set-up 没有存包，滚动条不出现）；小窗 4×8 可见、总 128 格；宽窗 16×8；每页各记滚动位置；滚轮一格 3 行。<br>4. 导出 `UIEquip.img` 的窗口、5 个页签和底图。<br>5. 新建 `EquipWindow` 壳，5 个页签都能切，`E` 开、X / Esc 关。 | `--screen invscroll` 能出图：小窗 40 件道具滚到底。`--screen equipui` 能打开装备窗，5 个页签都能点。滚动条能拖，滚轮能滚。build / test / format 全过。 |
| 3 | 装备 / 装饰内容 + Character Info | 1. Equipment 页显示 `Equips` 的武器和长袍。<br>2. Decoration 页 3 个小页签：Outfits / Android / Damage Skins；Outfits 页显示 `CashWorn`、`Hair`、`Face`。<br>3. Pet / Android / Damage Skins 页显示空白格。<br>4. Character Info 改成 `common/main` 上块 + `local/detail` 下块，删掉 EQUIP 页，`S` 打开。<br>5. 导出 Character Info 的底图和 STAT 条。<br>6. 装备窗能拖：按住标题条拖动，不越出屏幕。 | `--screen equipui` 能看到武器和长袍；`--screen decorations` 能看到默认头发和默认脸；`--screen stat` 上面有角色立绘，下面是属性；Pet / Android / Damage Skins 能点开空白格；装备窗能拖。build / test / format 全过。 |
| 3.5 | 插队（2026-11-09 用户定，排在第四周之前） | 1. 手里没有武器不能攻击：`Equips` 里没有 `Wp` 时按 Ctrl 不出攻击、不判伤害（客户端和服务端都拦）。<br>2. 创建角色送一套新手装备（剑 + 上衣 + 裤子 + 鞋），新建号进游戏身上就穿着；先补 Pants / Shoes 的导出、`BeginnerImgs`、`EquipSlots.Boxes`、`HudEquip` 的 `EquipBoxes` / `DecoBoxes` 和 `panel-equip.json` 的布局键。<br>3. 修 `Loopback_Attack_KillsMob`：测试地图加地板，别让怪掉出攻击高度。 | 空手按 Ctrl 不攻击；新建角色身上有新手一套；`dotnet test MushTown.sln` 全过。build / test / format 全过。 |
| 3.6 | 插队（2026-11-09 用户定，排在第四周之前） | 装备格子里每个格子显示自己的部位名字（参考图里有），素材从 WZ 的 `UIEquip.img` 找。本轮先做 Equipment / Outfits 两页；Pet / Android 页等有对应内容时再补。 | 打开装备栏 / 装饰栏，Equipment / Outfits 每个空格子上有部位名字。build / test / format 全过。 |
| 4 | 纸娃娃外观基础 | 1. 导出 6 套皮肤身体 / 头（`2000`…`2012` / `12000`…`12012`），`ExportCharacter` 加皮肤参数。<br>2. 导出全部 22 套头发 / 脸纸娃娃（11 个发型 + 11 个脸，含 4 个默认）。<br>3. `AvatarArt.MergeParts` 加 `skin` / `cashWorn` / `hair` / `face` 参数；`DrawAvatar` 和 `BodyCenterX` 都走完整外观；`HasLookBust` / `DrawLookBust` 按当前身体 / 头部件查，不写死 `00002000` / `00012000`。<br>4. 默认头发和脸替换出生图里的烤死部件。<br>5. 皮肤动作表接上。 | 6 种皮肤都能用默认身体、默认头发、默认脸正常站立、走、跳。`--screen equipui` / `stat` 的小人位置不歪。部件合并有单元测试。build / test / format 全过。 |
| 5 | 外观穿脱 + 装备 / 装饰联动 | 1. DEC 页双击 Fashion：从 `DecBag` 扣 1 件，进 `CashWorn`；再双击回 `DecBag`。<br>2. 同槽位新 Fashion 顶掉旧件，旧件回 `DecBag`。<br>3. 双击头发 / 脸券：新券出 `DecBag`，旧券回 `DecBag`，改 `Hair` / `Face`。<br>4. 双击装备栏里的装备：回 `EquipBag`；背包 Equip 页不再显示穿着的装备。<br>5. 显示优先 Cash，没 Cash 才显示装备。 | `--screen equipui`、`decorations`、`stat` 三张图里，穿脱和头发 / 脸都正确。穿着的东西不在背包；脱下来回背包。四张默认券都能看到、都能穿。build / test / format 全过。 |
| 6 | 现金商店框架 | 1. 写 `cashshop.json`：2 件 Utility + 8 件 Fashion。<br>2. 导出 `UICashShop.img` 的顶栏、列表、预览底图、底部导航、CHECK OUT 和 Beauty 底图，外加 2 件 Utility + 8 件 Fashion 的 `ItemData` 和图标。<br>3. 新建 `CashShopWindow`：全屏、X / Esc 关、MENU 能开。<br>4. 顶栏搜索 + meso；底部分组 Utility / Fashion / Beauty；子页签都注册点击。<br>5. 列表格子、卡片、图标、名字、价格、tooltip、`noResult`。<br>6. Utility 2 件和 Fashion 8 件都能出现在列表。 | `--screen cashshop` 能看到 Utility / Convenience 的 2 件道具；`--screen cashshopfashion` 能看到 8 件 Fashion；搜索 `reset` 只剩 SP Reset Scroll，搜 `zzz` 出 `noResult`；底部所有子页签能点。build / test / format 全过。 |
| 7 | 预览 + 购物车 | 1. 右侧预览用 `preview_back0` + `preview_bottom`，人物脚底 `(1095,354)`（底图草地线；WZ 的 330 在灌木里）。<br>2. 预览人物能左右走、Alt 跳、Ctrl 攻击、在梯子上上下爬，爬梯看背面。<br>3. `Remove All` 清预览和购物车，恢复真实外观。<br>4. 购物车 6 格；双击 Fashion 进购物车并预览；同槽位新件顶掉旧件，占旧格。<br>5. 双击同一件取消预览和购物车。<br>6. 购物车满 6 件时第 7 件弹 `The cart is full.`。 | `--screen cashshopcart` 有 2 件预览和 2 件购物车；`--screen cashshopladder` 人物在梯子上画背面；走、跳、攻击、爬梯都能操作；Remove All 能清空。build / test / format 全过。 |
| 8 | CHECK OUT + Utility | 1. 点价格弹单件 `CHECK OUT`；钱不够弹提示，不扣钱。<br>2. Buy All 弹多件 `CHECK OUT`，最多 3 行，超过滚动。<br>3. 买成功：Utility 进 `CashBag`，Fashion / 头发 / 脸进 `DecBag`；单件从购物车移除，Buy All 清空购物车，预览保留。<br>4. CASH 页双击 Character Name Change 改名，SP Reset Scroll 清技能退 SP。<br>5. 所有失败情况不扣道具、不改外观。 | `--screen cashshopcheckout`、`cashshopbuyall`、`cashshopempty` 都能出图。钱刚好够能买；钱少 1 不买；单件和 Buy All 的购物车规则对；改名 / 洗点都能用。build / test / format 全过。 |
| 9 | Beauty 发型 | 1. 接 Beauty 页的 9 张发型卡（22 套纸娃娃已在第 4 周导出）。<br>2. 每张卡有纸娃娃 + 价格按钮。<br>3. 点纸娃娃预览并进购物车；同类型新卡顶掉旧预览和购物车旧件。<br>4. 点价格买，买完进 `DecBag`，不自动换。<br>5. 所有款都显示，默认款也能选。 | `--screen cashshopbeauty` 有 9 张发型卡；点纸娃娃会预览和进购物车；点价格能买；买完去 DEC 页双击才换。build / test / format 全过。 |
| 10 | Beauty 脸 | 1. 接 Beauty / Faces 的 9 张脸卡（纸娃娃同样复用第 4 周）。<br>2. 每张卡有纸娃娃 + 价格按钮。<br>3. 点纸娃娃预览并进购物车；换脸会顶掉旧预览和购物车旧件。<br>4. 点价格买，买完进 `DecBag`，不自动换。<br>5. 默认脸和备用脸都能穿，位置不歪。 | `--screen cashshopface` 有 9 张脸卡；默认脸和备用脸都能穿，位置不歪；点纸娃娃会预览和进购物车；点价格能买；买完去 DEC 页双击才换，重启后还在。build / test / format 全过。 |
| 11 | 通用掉落 | 1. 玩家点 UI 外世界区域丢弃：单件直接丢，多件弹共用数量框。<br>2. 服务器 `Net.DropItem` 和 `MobWorld.DropItem`；单机直接生成。<br>3. `DropArt` 加出生 / 落地 / 拾取动画：0.4 秒下落、落地旋转停下、±2px 浮动、200ms 拾取。<br>4. 怪物多件掉落按图标宽度排一行，间距 0px，放不下整体平移。<br>5. 拾取按种类回 `EquipBag` / `Use` / `CashBag` / `DecBag`，补 order list。 | 丢 1 件直接到脚下；5 件选 3 丢，地上 1 个数量 3 的掉落；地上能捡回；怪物掉多件会排开；掉落有动画。build / test / format 全过。 |
| 12 | NPC 商店 6 页签 + 滚动 + 卖现金 | 1. NPC 商店右边背包改 6 个页签：Equip / Use / Etc / Set-up / Cash / Dec。<br>2. 导出 NPC 商店左右滚动条美术；左右列表都能滚：每页可见 10 行，每个页签各记滚动位置；滚轮一格 3 行，拖动滑块也能滚。<br>3. `Shop.Buy` / `Shop.Sell` 按页签选包；卖价半价；买回放回原包。<br>4. 卖现金道具用同款数量框。 | `--screen shopscroll`：左列表 12 行能滚到底，右边 6 个页签都能点。CASH / DEC 能卖，钱增加，道具回原包；买回放回原包。build / test / format 全过。 |
| 13 | 联机外观 + 总收尾 | 1. `Net.Appearance` 发 `Skin` / `Hair` / `Face` / `CashWorn` / `Equips`；服务器保存并在进图时下发。<br>2. `PeerArt` 缓存外观；`DrawAvatar` 用远程外观；装备 id 没美术就按没装备画。本周前远端只画普通身体（内衣），本周补外观同步。<br>3. 全部截图重跑：`cashshop`、`cashshopfashion`、`cashshopbeauty`、`cashshopface`、`cashshopcart`、`cashshopladder`、`cashshopcheckout`、`cashshopbuyall`、`cashshopempty`、`shopscroll`、`equipui`、`decorations`、`inv`、`invscroll`、`stat`。<br>4. 跑完测试、format、build、review，最后提交。 | 两个客户端互相看到皮肤、头发、脸、Cash 外观和装备一致；远程缺美术不崩。所有截图能出图。format 净 + build 0 错 + 测试全过 + review 通过。 |

## 每周固定门禁

每周做完都要跑：

1. `dotnet format MushTown.sln --exclude WzComparerR2`
2. `dotnet build MushTown.sln`
3. `dotnet test MushTown.sln`
4. 本周要求的 `--screen` 截图都出图
5. 自查一遍，再提交一个 commit

不满足就停下修，不把问题带到下周。

## 顺序理由

1. 先做数据底座，后面所有窗口和现金商店都靠它。
2. 先做背包，再做装备窗，最后做外观；这样装备 / 装饰能马上看到。
3. 先做现金商店框架，再做预览，再做结账；每一步都能单独看。
4. Fashion 复用已有装备美术，跟现金商店框架和预览一起做；Beauty 放后面，因为它依赖完整纸娃娃和购物车。
5. 掉落、NPC 商店、联机外观放最后，收尾一次做完。

## 如果某周超时

- 先把本周最后一个“能砍的小项”挪到下周，不硬塞。
- 不把整个一周的任务都挪走。
- 每周必须留下能跑、能看的结果。
- 13 周是估算，不是死线。提前做完就提前进下一周，不抢跑后面的功能。

## 后续待办

- 2026-11-09 服务端 `OnAttack` 没收到 `Build` 时回退 `Progression.Beginner`（攻击力 32），空手客户端在 `Build` 到达前仍能攻击。后面改：无 `Build` 按空手拒绝，或要求进房先发 `Build`；会动 CLI / 测试，单独做。
