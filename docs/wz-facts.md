# WZ 实测数据

从 `E:\game\maplestory\appdata\Data` 实测记录。查美术尺寸、origin、资源路径、已导入内容来这里。规则和教训在 `.dsh/memory.md`。

## 1. WZ 数据结构（外部实测）

- 树：Base.wz 是总容器，Character/Map/Mob/Npc/Skill/Item/Quest/String/UI/Effect 都在 Base 下（`Base:Character\_Canvas\00002000.img`、`Base:Map\Map\Map1\100000000.img`）；分类 .wz 只放自己类型。`_Canvas` 下的同名 img 是小地图画布 → **真地图判据 = 分节里有 info/foothold**。
- 链接：真图在 `_Canvas` 树；树里的松散节点多是存根（带 origin/z/map 但 1×1）→ `_outlink` 是字符串路径（/ 分隔），`_inlink` 在同一 img，图片内容解出来后挂在 `Wz_Image.Node.Nodes`。**反向找原节点：`_Canvas` 路径去掉 `_Canvas/` 就是树里的路径**（`Mob/_Canvas/0100100.img/move/0` → `Mob/0100100.img/move/0`）。
- 地图 img 分节：info / back / life / 0~7 / reactor / foothold / ladderRope / miniMap / portal / ToolTip / effect / rectInfo / seat / skyWhale。资源路径形状：tile = `Map\Tile\{层 info.tS}.img\{tile.u}\{tile.no}`；obj = `Map\Obj\{oS}.img\{l0}\{l1}\{l2}`（动画帧是子节点 0/1/2，静态是本体 png）；back = `Map\Back\{bS}.img\back\{no}`（ani=1 时是目录，spine 不支持）。
- 角色基础动作树在 **Character.wz 分类文件（松散树）** 的 `00002000.img`（Base 树下只有 `_Canvas` 裸图，没有 origin/map）。动作名 stand1/walk1（单手剑系）/stand2/walk2（杆系）/swingO1/swingP1/jump/ladder/rope/prone。帧 = {body/arm/hand 分件 + face + delay}，分件带 origin/map(骨头)/z(zmap 名)/group。**stand1 = 3 帧、face 1 帧、delay 500**（1.5 秒一轮）；stand2/alert 也是 3 帧。
- 怪：动作树不在 `Data/Mob/Mob.wz`（192 字节的空壳）里，在 **`Data/Packs/Mob_00000.ms` ~ `Mob_00003.ms`**（4 个包，约 400 MB）。打开 `Data/Base/Base.wz` 后还要对 `Packs` 里每个 `.ms` 调 `LoadMsFile`，才看得到 `Mob/<id>.img`（官方查看器就是这么做的）。每帧是 1×1 存根，带 `origin`/`lt`/`rb`/`head`/`delay`/`_outlink`；真图在 `Mob/_Canvas/<id>.img/...`。所以**从 `_Canvas` 反查原节点 = 路径去掉 `_Canvas/`**（`Mob/_Canvas/0100100.img/move/0` → `Mob/0100100.img/move/0`），代码里用 `WzNodeUtil.ResolveLink` 顺着 `_outlink` 走。`info` 有 level/maxHP/exp/speed/PADamage/PDRate/MDRate 等官方数值，**导出直接当属性用**（MaxHp=maxHP、Exp=exp、Def=PDRate/100、Touch=PADamage、Speed=100+speed px/s、Boss=boss；只有 Aggro 追击范围 WZ 没有，保留自调）。life id 是零填充字符串（"0100000"）。本机只有 20000（8x 蜗牛）+ 1010000 有怪，其余 4 图 life 全 NPC。
- 怪帧时间：`Mob/<id>.img/<动作>/<帧>/delay`，每帧一个 ms，**不是固定帧率**。实测：蜗牛 move 5 帧全 180、hit1 300、die1 120；蓝蜗牛 120；史莱姆 move 200/50/150/150/100/100/300；猪 100/100/300；蘑菇妈 500/100/100/300/100/100。单帧动作（stand/0）没有 delay，导出缺省 180。maplestory.io 的 GIF 帧时间也来自这些 delay 节点（GMS 数据），两边对得上。
- 怪号规律：现代怪号（100000-100007）自己没有图 → 借官方**同名老怪**的图，**图名 = 老怪号补零到 7 位**（100000→0100100、100001→0100101、100003→0120100、100004→1210102）。老怪号：100100 蜗牛 / 100101 蓝蜗牛 / 120100 孢子 / 130100 树桩 / 130101 红蜗牛 / 210100 史莱姆 / 1210100 猪 / 1210102 橙蘑菇。`catdump String "Mob.img" 2` 一次看全部 12249 个怪名（名字后跟 ` (0 children)`，正则别用 `$` 收尾）。
- 伤害皮肤：`Effect\BasicEff.img` 下 NoRed0（黄普攻）/NoBlue0/NoGreen0/NoViolet0/NoCri0（红暴击），各 10 子为数字 0-9。数字 canvas 紧身（1=22×32、8=31×33），按宽推进，oy 对基线。皮肤 0 用 `effect/dmg.json`，皮肤 N 用 `dmg{N}.json`（搬文件名会让现有 assets 开不了游戏）。
- `String.wz` 的道具名用 **7 位 id**（去前导 0），img 文件名才是 8 位。地图名在 `Map.img/{世界名}/{图号}`，世界名是 `maple`/`victoria`/`ossyria`/`3rd`/`etc`（**不是数字**），另有 `mapName`/`streetName`/`mapDesc`。
- 官方地图路径：彩虹岛 `Map/Map0/000030000.img`（9 位补零）、维多利亚 `Map/Map1/100000000.img`（不补零）、南门街 `Map/Map2/200000000.img`。82 张官方彩虹岛图里 **16 张本机没有地图数据** → 别写"彩虹岛的图全在 Map/Map0"。
- 摄像机范围（VR）：`info` 里 4 个 VR 数字**要么全有要么全没有**；全没有的图用**所有地板线的包围盒**；只有 3 个或范围为空 → 导出失败。余量实测：`VRTop` = 最高地板线 − 360（11/15 张）、`VRLeft` = 地面左边 **+10**、`VRRight` = 地面右边 **−10**（10/15 张）、`VRBottom` 不固定（+190/+15/+3/+26/+75/−179）。
- 导出器风格：**fail loud，不猜不补**（缺节点整次导出失败；空发型列表打印 "no hair ids" 返回失败不崩；缺美术/缺配对的检查在烘焙前）。

## 2. origin 与找图

- **WZ 图片节点的 origin = 锚点**，画时两个轴都取负：图左上角 = (−origin.x, −origin.y)。例：`Minimum/button:Left/normal` origin −10,−45 → 画在 (10,45)；`Dot/1` origin 1,−90 → 画在 (−1,90)（探出窗口左边 1px，官方就是这样）。
- origin 是"画到哪"的**唯一真值**：新窗口的定位数字不要猜，去 WZ 读。mouseOver 的 origin 也会变（箭头 lit 图 origin 从 −10,−45 变 −8,−43，往左上 2 像素）。
- **backgrnd 的大小就是窗口大小**（guide_small_bg 135×105，下面 10 行透明 → 窗口高写 105）。
- **说"官方没有这个"前必须先看 wz-find skill**（`.agents/skills/wz-find/`，已被用户驳回两次）：按名字找、按图片尺寸找、拼大表看、读 origin。坑：树里的 png 常常是 1×1 存根（真图在 `_Canvas`）；png 可能有全透明行。
- 查过确实没有的：红色 M 钮、左边 NEW 图标列（灯泡/星星/铜铃/本子）、Quick Move 下拉及其地图列表。查法：UI 分类 191 个 img 按名字（精确+部分）、Etc、String 都查过，又对 `UI/_Canvas` 里 14×14~44×44 画布逐个扫像素找红（2820 个候选 / 33218 张图）。最接近的：`UIWindowBT.img/QuestBulb/BtBulbOpen` 15×36、`StatusBar2.img/mainBar/BtNotice` 27×21、`UITotalMenu.img/quickMenu/menu/*` 30×30 灰剪影、`UIEventList.img/icon/0..252`。只有字符串 `SID_QUICK_MOVE_DISABLE` / `SID_QUICKMOVE_ERR_ONLY_TOWN`。

## 3. 官方 UI 美术（实测）

### 3.1 登录 / 选角 / 选职业 / 创建角色

- ClassSelect = `UI/_Canvas/Login.img/ClassSelect`。`back/0/1/0` = 1366×768 蓝径向渐变（origin 0,0）；`back/1/<jobid>/0` = 每职业大立绘（110 origin −79,0）。同目录 login_hero*.png / skeleton*.png 是 Spine 图集页，不是动画帧。
- 列表：`list/backgrnd` 313×593 窄列表（2 列 × 7 行、格 152×74、首格 (0,35)、横距 161 纵距 81、origin −1040,−81）；`list/backgrnd1` 957×593 宽列表（origin 396,81）；`class/layer:classCover` 158×78 选区白框；`button:make` 200×57（origin −1040,−701）。**classEnabled 顺序：110 首、2700 在 2500 前（永不数字排序）**；`pagePrev/pageNext` 是动画帧 0..13，**不是状态**。另有 plus/minus 29×29、jobfilter 三段 120×29、`button:video` 39×39（视频本身是外部 .bik，客户端目录里没有）。
- 立绘 origin 每职业不同：110 = −79,0；130 = −246,0；12200 Saitama = 95,555；13100 = −374,−70。画布 713×679~1443×992。**单个全局偏移只在所有职业共用同一 origin 时才成立。**
- CustomizeChar = `UI/Login.img/CustomizeChar/<jobid>`（按职业分变体；12200 只填名字，没有 Gender 行）：`board/avatarTop` 366×202、`board/boardBottom` 366×91、BtYes/BtNo 166/167×43 三态；官方相对坐标 输入框 (37,104)、按钮 (12,231)/(186,231)。**卡片是半透明白（alpha 128..204），压在树丛上会透底，必须放干净天空上。**
- 发型：男性 = `Hair\0003xxxx`；000300 组 52 个 = 10 款式 × 颜色（0000 短毛 / 0010 尖刺 / 0020 刘海 / 0030 光头 / 0040 长白发 / 0050 短碎 / 0060 长发 / 0070 金色尖刺 / 0080 齐刘海 / 0090 橙尖刺），000301+ 是别的款式组。
- Spine：`back/1/<id>/` 下 `atlas`、`login_*.png` 图集页（2048×~2000，pma:true scale:0.8）、`skeleton.skel` = Spine **4.1.24** 二进制；23 个职业的 skeleton.json 是 **2.1.27 文本格式**。Hero(110) 一职业 17 页 ≈ 18.9MB PNG ≈ 211MB 显存；56 职业全导出 ≈ 1GB。

### 3.2 MENU 窗

- `UI/UITotalMenu.img/main/backgrnd` = 1022×698，一张图里是白面板 + 7 个蓝页签 + 底部 QUICK MENU 灰条；**7 列同时显示**。白面板 40% 白（alpha 102），官方在后面画模糊屏。
- 格位**不在 origin 里**：`main/menu/vector:startPos`=(23,82)、offsetX 140、offsetY 40。每行 art = `main/menu/buttonInfo/<col>/<row>/{normal,pressed,disabled,mouseOver}/0` = 136×40，**图标和文字都烤在 art 里**（别单独摆 16×16 图标，也别用字体画文字）。行数 4/8/8/9/6/4/4 = 43 行。页签 y 47..81。
- 底排 7 钮 = `main/button:{cashShop,mapleAuction,channelChange,characterSwitching,keySetting,option,gameQuit}` 136×40，**origin 是位置的负数**（x = 23..863 步 140，y = 521）。close 20×20 在 (982,12)：mouseOver 是白 X 蓝底板，官方原样**不要染色**。`layer:editModeGuide` 只有编辑模式才画；`vector:info`=(894,16) 是右上角频道名。
- 窗高用 **597**：白底到 576、投影到 596、597..610 全透明、QUICK MENU 从 611 开始（写 611 会多 14 行毛玻璃和拖动区）。位置 y ≤ 26 才不压住 HUD 菜单钮。
- 行尾键徽章 = `main/key/<编号>` 70 张 16×16 青底白字。编号是**官方键码不是 ASCII**（Esc=1、1..9=2..10、0=11、QWERTYUIOP=16..25、Enter=57、F1=59…）；内容 = 该功能当前绑定的键；没有徽章的键该行不画；组合键画第一个键的徽章；左右 Shift/Ctrl/Alt 共用一个码。
- **徽章表和键窗的键帽表不是一回事**：徽章 43=↑、57=↵；键帽 43="|"、57="Space"。两套各自都对，不要互抄。菜单键盘导航：左右换列（环形）、Tab = 右、Enter 打开；官方 hover 蓝 = `(123,204,224)`。

### 3.3 快捷键窗

- `UI/StatusBar3.img/KeyConfig`：`backgrnd` 660×624 = 整窗（标题、PRESETS、TIP 条、底部格子网格都烤在里面）；`backgrnd3` 620×231（origin −20,−100）= **官方键盘，所有键框都烤在里面**；`keyPos` = scancode→窗内坐标；`vector:keyOffset`=(0,33)；键 32×32。
- 对齐法：`keyPos + keyOffset` = 键框**左上角** → 画底图 + 键帽/图标即可，不用自己画键框；键框行顶 = 18/56/89/122/155/188。（旧"对不齐"的结论是比错了 backgrnd2。）
- 官方画法：没绑的键贴键帽字母，**绑了键的贴该动作的 32×32 官方图标**（不贴文字）。下半部动作网格 17 列 × 5 行、格 34×34、步距 36、首格 (25,374)，**一个操作占一格**。
- 绑定交互是官方的：按住拖（ghost 跟鼠标），放到键上 = 绑定，把动作拖出格子 = 解绑；"点一下再点一下"不是官方。
- 图标 = `KeyConfig/icon/<id>`（77 张 32×32，id 有跳号）。每张带 `category` 0..7，**官方下拉索引 = category + 1**。常用：1 ITEMS、2 CHAR. INFO、3 SKILL、7 MINI-MAP、8 QUESTS、19 PARTY、46 MENU、202 MUTE GAME（**别当设置图标**）。灰图标只有 51 SIT/52 ATTACK/53 JUMP/54 OPEN OTHER/55 TO TOWN，**没有灰 MENU**。46 号就是官方 MENU；Settings 也借 46 会一模一样 → 键窗里 Settings 用快捷菜单齿轮 `quickmenu_53_n.png`（唯一特例）。下拉 = `combo:filter`，外观 = `UI/Basic.img/ComboBox25/{normal|pressed}/{0,1,2}`；默认筛选 Character/Item，Menu/Settings/Party 属 category 2，要开下拉才看得到。
- Quick Slots 第二页 = `UI/StatusBar3.img/CustomDefaultKeyConfig`（backgrnd 660×735）：**这页烤的键盘比 Custom 页小且偏移不同 → 只能"点格子再按一个键"绑**。回 Custom 页的钮我们放 (280,33)（官方 (19,66) 会压住烤出来的键盘）。**这页已按用户要求整个删掉**（2026-10-27）：Quick Slots 按钮只画 `keys_qslot_n.png`，点了什么也不做；美术也不导了。以后做可编辑快捷槽时再建。
- 池子只列没绑的动作：`FilterShows` 第一行就是"已绑 → 不画"，任何过滤组都不画已绑动作；**格子位置固定，不往前补**（`Actions` 的下标就是格位）→ **绑上的动作被藏起来，键格本身必须能抓**，否则解绑和 Combo 第二段没有入口。键帽反查表在 `Build()` 开头建，别每帧遍历 77 个动作。

### 3.4 选项窗

- 选项窗 = `UI/StatusBar3.img/OptionMenu`（460×633）：`sound/layer:backgrnd` 301×507 在 (139,67)；`tab/enabled/2` = Sound 页签 118×37 在 (21,153)；音量轨道 art x=86..279（宽 194），行 y = 58 Master / 141 BGM / 206 Normal SFX / 271 Monster / 354 Skill SFX / 419 Skills；旋钮 20×22、Y 偏 −9。
- `UI/UIWindow.img/GameOpt` **不是**选项窗：只有 `backgrnd` 125×298 的聊天权限小面板，画不下音量行。

### 3.5 面板

- 背包 = `UIInventory.img/Inventory`：`backgrnd` 242×593 小窗、`FullBackgrnd` 800×546 宽窗；**官方格子 36px、间距 46、首格 (31,101)**；小窗 4 列、宽窗 16 列；6 页签。
- 属性 = `UICharacterInfo.img`（STAT/EQUIP 两页），正文 472 宽；Combat Power 官方位留着但没公式，恒 0。小 X 官方出处 = `common/main/button:close/normal/0` 11×11（技能窗借它；任务窗用自己的 `UI/Quest.img/Main/questList|questInfo/button:close`）；`Basic.img/BtClose` 12×12 蓝、`UIWindow.img/BtUIClose` 32×15。
- 技能 = `UIWindow2.img/Skill/main`（**不是**旧的 `UIWindow.img/Skill`）：没有详情框 → 说明框用 `UIToolTip.img/Skill/Frame`（九块 14~15px 边框的深色面板）画九宫。没有关闭钮 → 借小 X。蓝条 304×45；行图 140×35，**行距用 32**（裁到 32 高，8 行才放得进官方正文 y355）。
- 任务 = `UI/Quest.img/Main`：左 `questList/backgrnd` 420×550 + 右 `questInfo/backgrnd` 322×550，并排 742×550。左列表三组 `questList/bottom/state/0..2`（0 Available / 1 In Progress / 2 Completed，377×27，opened/closed 带正常/悬停/按下），行选中 `bottom/selected` 367×24（origin 5,1）；WZ 没有单行鼠标悬停图，悬停也用这张。右详情空状态 `questInfo/layer:empty` 306×485 在 (8,30)，选中底 `layer:selected`；宽按钮 start/end 292×36 在 (15,471)，giveUp 86×24 在 (225,519)；奖励 EXP 33×26、meso 32×32，box 290×51，reward 标签 32×10。
- 商店 = `UI/UINpcShop.img` 左右两窗：左 `Shop/backgrnd` 380×681（NPC 名/立绘、Sell/Buy Back 两页签；`pos/vector:title`=(139,55) 左对齐，页签 70×22 在 x=11+72*i,y=103；行底 336×45，行距 50，`itemList/pos/itemIcon`=(7,7)、`itemName`=(56,6)、`itemPrice`=(72,24)、`pointIcon`=(55,27)）；右 `Inventory/Normal/backgrnd` 324×681（角色预览 (58,88)、金币图标 (115,71)、金币数字 (293,79)、四个页签 74×22 在 x=11+76*i,y=103；行底 280×45，行距 50）。两窗整张画；右窗的屏幕 x 必须是**左窗宽 380**，不是自身宽 324。行底都有 normal/selected 两态；selected 图的图标格仍是白底，客户端要把这 48px 也填成选中蓝。`layer:meso` 200×21 origin (-106,-66) → 画在 (106,66)。右侧关闭钮借 `Inventory/Normal/button:close` 27×27。官方字体：标题 GyeokdongRg 14 淡蓝 (ffbecbd9)、页签 Arial 11 白、名字 Arial 12 黑、价格 Arial 12 粗黑。空列表不显示文字。

### 3.6 地图类窗口

- 小地图框 = `UI/UIMap.img/MiniMap/MinMap` 九宫：nw/ne 15×36、sw/se 15×16、n 1×30、s 1×10、w/e 9×1 → **内边距 侧 9 / 顶 30 / 底 10**，地图井 = 窗口减这些。窗口尺寸 = `左右边框 + max(167, 底图宽)` / `上下边框 + max(50, 底图高)`，167×50 是最小地图区域（＝最小窗宽 185）。底图按**原尺寸**画、居中、不缩放；比最小值小时四周铺 70% 黑（`Black * 0.7f`，alpha 178）。九宫不画中间 `c`（官方 `C = null`）。角 nw/ne 36 高、多出的 6 行压在地图上面。
- 小地图钮 = `UIMap.img/MiniMap/{BtMap,BtNpc,BtFilter,button:min,button:max}` 21×21 三态。town / navi 两个钮没功能，用户 2026-11-01 让删了，美术也不再导出。`UI/UIWindow2.img/MiniMap` 是 v0.83 绿字老版，不要用。
- 大地图框 = `UIMap.img/MiniMap/MaxMap` 九宫：nw 44×76、ne 15×76、**n 1×70**、s 1×10、w/e 9×1、sw/se 15×16（白 alpha 119）。井顶取 **n 的高 70**（不是角块的 76，角块多出的 6 行压在地图上面）；nw 里还有 36×36 白方块；地图名 (50,48)、街道名 (50,30)。
- 世界地图数据在 **`Map/WorldMap/WorldMap.img`（不是 UI 分类）**：`BaseImg/0` 640×470（origin 320,235）；`MapList/<i>` = title + spot（图上像素 = 320+spot.X, 235+spot.Y）；`MapLink/<i>` 的 `link/linkMap` 是区域→区域图，`toolTip` 是链接名。**配对靠名字不按序号**（MapLink 15 条 vs MapList 13 个）；只读 MapList 会漏 1020000（Split Road of Destiny）→ 把区域图的城镇号并进去。根图 `WorldMap` 没有 `info/parentMap`（导出成 `""`，不是 null）。窗框 = `UI/UIMap.img/WorldMap/backgrnd` 852×561。
- MAP INFO 栏只有 180 art 像素宽 → 长名要换行；宽度用官方 146（lifeRB.x − lifeLT.x）。`mapInfo` 自带 "Cooldown" 牌子（y≈477-495）→ 列表不能压到，要能滚；列表 14 行。滚动条轨道 x=165、长 296、滑块 67、宽 5，美术 = `Basic.img/VScr111`（滑块图只有 5×26）→ **按美术自己的尺寸画，不拉长**。官方滚轮一格 3 行。
- 大地图顶部三个下拉 = `combo_{n,h,p}0..2`（4×20 / 77×20 / 17×20，盒 98×21，pitch 101）。四个状态 `normal/mouseOver/pressed/selected` 里**只有 `2`（右端盖的箭头）变颜色**（h 和 s 的箭头是青色 4,199,222，p 是白色），`0`/`1` 三种状态逐像素相同；`selected` 和 `mouseOver` 完全一样。列表行没有箭头，所以官方下拉列表的悬停态在这套图里**没有对应件**，要在行上自己加底色。确认框 = `Basic.img/sysPop`（top 318×30 / mid 318×17 / bottom 318×59，BtYes/BtNo 83×26 在 (73,14)/(162,14)）。`button:teleport` 是 Hyper Teleport Rock、`button:navigationStart` 是导航，都只有 disabled 图。
- 下拉三块图的分工：0 = 左端盖（第一列灰边、圆角），1 = 中段（**只有最上一行是灰的**，左右两边都没边），2 = 右端盖 + 灰色的 ▼。**没有「无箭头的右端盖」**，所以列表行要收口只能拿 0 镜像。`selected` 那套和 `normal` 一样只有三块。
- 大地图 tooltip 三张 = `UIWindow2.img/ToolTip/WorldMap/{Line,Mob,Npc}`：Line 251×1 纯 `#777777`；Mob / Npc 各 9×9（彩色圆角块 + 中间 M 形镂空，A=0；画在深色框上看着像黑图案）。**中间是透明的，不能九宫拉**。
- 立绘直接有现成的，不用导：NPC = `assets/npc/<id>.png`（第 0 帧；多帧是 `npc/<id>_<i>.png`，每帧 origin 在 json 里）；怪物 = `assets/mob/<id>/<动作>/<i>.png`，每帧 json 带 `OriginX/Y`（WZ origin），客户端把该像素贴到怪的脚点（X/Y 都用 origin）。**不是每只怪都有多帧 stand**（100001/100002/100005 和 6130101 的 stand 都只有 1 帧，100000 也只有 1 帧），单帧 stand 不能动 → 取 idle 要按「stand 多于 1 帧才用 stand，否则用 move」；tip 播放该动作的全部帧。
- NPC 的 stand 常有多帧：`Npc/<id>.img/stand/<i>` 每帧一张图 + `delay`，导出成 `npc/{id}.png`（第 0 帧）、`npc/{id}_{i}.png`，延迟进 json 的 `StandDelays`。导出容忍空号（当前 17 个 NPC 的 stand 都只有数字子节点、序号连续）；按实际存在的序号排序后重新写成 0,1,2…，客户端只按数量找文件。多帧的 origin 不一定相同：10201 的 stand 四帧是 38,150 / 38,153 / 38,157 / 38,153，导出逐帧写进 `StandOrigins`；客户端每帧按自己的 origin 摆（锚点、点击框、头顶、名字牌、tooltip）。帧可以是 **`uol` 指针**指回前面的帧（0011000 的 3~6 帧指回 1,2,1,2），所以导出要解析 uol，否则帧数是 7 但图只有 2 张。多帧例：0011000 7 帧（1200/120×6）、0022000 10 帧、0010204 3 帧；多数 NPC 只有 1 帧（如 1541000 Sugar）。

### 3.7 聊天 / 对话 / 组队 / GUIDE / 任务条 / 气泡 / buff / 快捷栏

- 聊天框：WZ 里三代，最新一代 = `UI/StatusBar3.img/chat/ingame/view/backgrnd`（九宫，中间 1×1 alpha 204）= 我们在用的；旧代 = `StatusBar2.img/chat`、`StatusBar.img/base/chat`。`view/scroll` 的 prev/next **全透明** → 只画轨道和滑块。窗口钮只有最小化有功能（其余只有美术）。聊天目标钮盖在输入条左端（官方 x=6），输入文字从 x=71 起。`outside` 410×806 是频道聊天大窗（不透明，不用）。系统行 = 聊天帧里名字是空字符串（黄字，归 Etc. 页签；All 显示所有行）。
- 对话窗 = `UI/UIWindow2.img/UtilDlgEx`：t 519×28 / c 519×13 / s 519×44 是 **portrait-left**（灰头像区约 0..145，白字区约 147..506，蓝条高 44）；`it/ic/is` 是 portrait-right，不用。名字牌 `bar` = 109×19（官方灰条，客户端按原色画白字）。按钮 `BtPrev`/`BtNext` 40×16、`BtClose` 91×16（END CHAT）、`BtOK` 40×16、`BtYes`/`BtNo` 57×16，**标签烤在图里** → 组队弹窗 label 传 ""。`notice` 305×157（"Space = Talk to NPCs"）。UI 分类整棵树里**没有**叫 Talk/Dialog/Conversation/NPC 的节点（`find` 只查 Base 树，要用 `catdump UI`）。公告弹窗 = `UI/UIWindow2.img/Notice/13`（390×151，同族 0..18 多数 300×131）；`UI/Basic.img/Monologue/0/{0..3}` 不是框，是 23×22 金色箭头。
- NPC 任务气泡 = `UI/UIWindow2.img/QuestIcon`：0 = 可接，8 帧（4 种图循环）；1 = 进行中，4 帧；2 = 可交，8 帧。每帧 delay 已导出，WZ 是 150ms；客户端 `panel-balloon.json` 的 `npcMarkDelayScale` 现在是 1（150ms/帧，WZ 原速），2 = 半速（300ms/帧）。尾巴位置逐帧不同。
- 组队窗 = `UIParty.img/mainUI`：`backgrnd` 528×578；`partyWnd/backgrnd` 504×463 画在 **(12,63)**；`entity/back/normal` 476×39 = 成员行。队名画在 **`partyWnd/vector:partyName` (52,18)**（WZ 的 `edit:partyName`=35 会被队伍图标压住）。**行距 42 不是 39**（行图 39 + `entityMargin` 3）。`partyNum` 白框里 "1/6" 的斜杠是画好的。搜索窗 = `mainUI/searchWnd/backgrnd` 504×504 + `partyMemberWnd/backgrnd` 476×372 在 (14,116)、搜索行 468×39、行内邀请钮 27×28 在 (430,5)；最多 8 行（面板 372 / 行 39，第 9 行出框；官方有滚动条我们没做）。
- `UIParty.img` 还没用的：`partyHP` 九宫 = 别人头顶队伍血条；`GaugeBar` 半透明白九宫；`partyInfo` 成员信息窗；`partyMake` 官方建队窗；`submenu` 右键菜单（v1 不做）。
- **没有"收到邀请"专用弹窗**（Notice、CancelRequests、FadeYesNo、UIParty 全部子节点都查过）→ 用 UtilDlgEx。`UserList/PopupInvite` 是"按名字邀请"的表单，不是收到邀请的框。
- GUIDE 大窗 = `UIContentsGuide.img/Maximum`（在根目录）：backgrnd 642×627；`tab:mainTab` lt (20,74) rb (625,101)。**min/help 两个钮官方只有灰色图** → 画成不拉伸的 Picture。列表名字位置：boss x=45 起每行 +90；special x=40 起每列 +143、y=177；field 图标 x=127/236/323/410、y=130、名字在图标下 66。小窗 = `UIContentsGuide.img/Minimum`：backgrnd 135×115（可见只有 105，下面 10 行透明）；BtMainQuest/BtJob 84×67；Max 11×11、Close 13×13。卡片能点（主线开任务窗、职业开属性窗），绿加号开大窗。
- 任务条 = `UI/Quest.img/Helper`：`topClosed` 222×28（标题烤在图里）、`topOpened` 222×26、`mid` 222×1、`bottom` 222×12（列表体，已用）；数字 `qNum/0..9` 6×5。条上没任务时不画（官方如此）；条上的 art 不吃点击，展开后的 body 整块吞点击、防止漏到世界，只有 bar 的两个钮和每行的 X 有动作；我们另在条顶部加拖动区，按住可移动整个助手。
- 任务助手行 = `UI/Quest.img/Helper/questTitle`：`canvas:top|mid|bottom` 206 宽（6/1/6 高，竖向三段）、`button:close` 9×10 在行内 (192,4)；`vector:tag` (5,6)、`vector:name` (60,6)、`vector:detail` (19,26)。官方 questTitle 底板是黑框，我们不用；多任务直接一行接一行排，文字和 X 都画在 helper 的 body 上。
- 任务窗的 "Add to Notifier" = `UI/Quest.img/Main/questInfo/helper/layer:helper`（97×13，origin −17,−524）和 `helper/check`（13×12）；勾选态就是 check 盖在空框上。
- 说话气泡 = `UI/ChatBalloon.img/0` 九宫 + arrow 尾巴：宽 130、最多 3 行、4000ms、aboveFeet 74。`UI/ChatBalloon.img` 有 472 套样式，`Character/Ring/0111xxxx.img/info/chatBalloon` 是戒指→样式的官方字段（455 枚戒指有）。`popupSayEx/chatBalloon` 是深灰的说话弹窗，**不是** NPC 白气泡。
- buff 栏 = `UI/StatusBar3.img/BuffSetting/favoriteBuff` 白半透九宫。`UI/BuffIcon.img/buff/0..8` 只是 1×1 占位（名字是 "Physical Attack" 之类），**不能当图标** → 用技能图标；时间 m:ss 白字四角黑描边压在图标右下（12px 放不进框里，官方也压）。
- 快捷栏 = `UI/UIQuickSlot.img/quickSlot/backgrnd` 557×67 = **两排 16 格**（每格 35×33、pitch 35），条在官方 HUD 的 (810,691)；左边 4 格是 Ctrl/Alt/I/C 提示，剩 **12 格 = 快捷栏**（键 1-9、0、−、=）。**用这张（平格子、无边框）**；`StatusBar3.img/mainBar/quickSlot/backgrnd` 是同一张图加了亮边框，配套的 `layer:cover`（562×73，origin −10,0）要按原尺寸画在 −2,−3 才对，拉伸铺满会变成粗双线 —— 已弃用。
- 快捷菜单图标 = `UITotalMenu.img/quickMenu/menu/*` 30×30；**WZ 节点名不是 0..54**（0..45,101,102,1001,1002,1004-1008，共 55 个）→ 导出时按官方顺序改名。官方间距 33。汉堡钮 = `quickMenu/button:menu` 30×30，放进第 10 格会和血条重叠，所以删掉了原来的电源键（Exit Game 仍在 MENU 底排）。
- 键帽 art（`KeyConfig/key/<scancode>`）宽高随字变化（字母 10×9、Space 34×11、Shift 28×9）→ 要居中；宽键帽**往左长**。没有键帽 art 的键画键名并缩进格子（RightShift 67px）。真图在 `_Canvas/battlesimulationreplay.img/player/virtualKeyboard/key/<scancode>`；`StatusBar3.img/KeyConfig/key/<code>` 只是 `_outlink`（1×1 链接节点），所以在 `StatusBar3.img` 下看不到图。
- 快捷栏格子里的名字是我们自己写的（官方格子里没有名字），文字要缩到格宽，最右格会被切掉；官方叫 SETTINGS。K1 起 4 个提示格改画官方图标（52 ATTACK、53 JUMP、1 ITEMS、2 CHAR.INFO）+ 固定键帽，不写名字。
- **HUD 16 格 = 16 个固定键查表**：键是 Ctrl、Alt、I、C、1-9、0、-、=，常量在 `HudQuickSlots.SlotKeys`（私有，**不要再手写 scancode 表**）。键帽图名唯一来源是 `assets/ui/keys.json` 的 `Cap` + `KeyNames`，结果缓存，每帧不拼字符串。`InputController.TargetOf(Keys)` 给这个键上的目标（组合键任一段都算，所以 Ctrl 格能查到 `LeftControl+RightControl` 的 Attack）。画法：有目标就画它的图（动作图标 / 物品图 / 技能图），键帽永远画，图标居中、键帽在左上吃 `qslotcap`，没有阴影。`keys_cap_<scancode>.png` 和旧的 `hud_key_*.png` 是同一批字节，所以 HUD 直接用 `keys_cap_*`。格子只显示，点格子不绑定物品。
- **药水绑定 = 表里的 `Item:<id>` 目标**：`CharaSlot.QuickSlots` 已删。喝药是 `ItemEdges`（`_input.Actions` 里 `Kind==Item` 且 `Fresh`）。demo 角色的两瓶药改成 preset 1 的 `D1`/`D3`。
- **键窗是组件化的**：键格/动作格/过滤框/列表行/跟手图标各自成组件，窗口里没有一个 `new Invoke`。松手边沿从 Draw 轮询改成 `UpdateHud` 传进来（`UiGfx.Mouse` 和 `gx,gy` 同一坐标，截图逐字节不变）。
- 键窗 Copy 弹窗：官方 `presetCopy` 背图 332×256（origin 0,−1，标题和说明文字烤在图里，"Preset" 后的空在 x=213..229、y=83..92），按钮 copy(81,207)/cancel(169,207) 各 83×26，preset 页 18×18 在 (207/240/273,152)，选中态用 `_c.png`。**默认来源 = 当前 preset**（自拷贝不写盘）。靠一个盖满**整个屏幕**（1366×768，不只是键窗）的 `Silent` 按钮吞掉外部点击。
- 键窗 PRESETS 1/2/3 的页按钮是 `button:presetPageN` 的 normal/hover/pressed（12×12），`checked` 导出成 `_c`（绿色）→ 选中的页是绿的。Copy 弹窗里另有一套 18×18 的 `keys_pcopy_pg{1,2,3}`（同样带 `_c`），别混。
- 抓手光标 = `UI/_Canvas/Basic.img/Cursor/5`（用户 2026-10-28 指定）：已导出成 `cursor_item.json` + `cursor_item_{0..3}.png`（4 帧，带热点和 Delay）。HUD 里要做抓手得自己建 `MouseCursor`（菜单那套是参考，注意 HUD 现在是系统光标）。
- 键窗动作池 = 官方 `StatusBar3.img/KeyConfig/icon` **全部 77 张图**，一张一格，顺序按图标编号。`HotkeyAction` 枚举同步 81 个值（16 实现 + 61 空动作 + 4 不进池的 PartySearch/PartyAsk/GuideCard/Settings）。**Settings 不进池**：官方没有"选项"图标，选项是 MENU 的一行。空动作按了没反应（不在 `UiHotkeyOrder` 里）→ 池子就是"还差哪些功能"的清单。官方 `iconPlaced` 76 格（起点 `paletteOffset`=26,375、`interval`=36），但 pid 里有一批没有图，所以顺序改用图标编号。过滤分组 = `keys_filter.json` 的 `Categories[图标号]`，共 9 项（Not Added + 8 类）。
- 键窗完全不挡其他操作（2026-10-30 用户定，参考原版"应该没限制"）：走路/攻击/跳跃/拾取/对话/喝药/面板键全照常，用的是**已保存**的 preset（草稿在窗口里，Save/Apply 才写存档，下一帧重建输入表）。MENU 仍然挡角色。
- 取消区只有黑色动作池：松手在标题栏、按钮、PRESETS 行、键缝上**什么都不改**（Tab/Enter/方向键不在 `keys.json` 的 73 个键里，按"不是键格就解绑"会把动作静默丢掉）。过滤列表展开时也不接受取消。
- 老存档没有 `Z -> Pickup`：**不写迁移**（删档重建）；自动补种会让"解绑拾取"重开游戏后失效。验收单写明"老档按一次 Default"。
- 键窗技能拖放：技能窗行按下时 `HoldSkill(id, point)`（只在键窗开着时），键窗松手写 draft；`Save` 传 draft，`Default` 把每个技能写成 `""`（不是 `Clear()`），这样 Save 才会真的删掉。**物品不在 draft 里**，所以 `Default` 另置标记，`Save` 时统一 `RemoveKind(table, BindKind.Item)`。键上的技能拿不起来。

## 4. 已导入的数据与内容

- 药：官方 `Consume/0200.img` 的 spec 有 hp/mp、`info/price`、`info/icon`，名字在 `String.wz`。2000001 橙药 +150HP / 24、2000002 白药 +300HP / 48、2000006 魔力精灵 +300MP / 93；2000004 精灵药按百分比回血（不支持）。
- 装备：只导 incPAD/incPDD/incSTR/incDEX（别的 inc 导出器报错；官方很多外套带 incMDD/incMHP 不进）。槽看 `islot`（**游戏只认 islot**，查过的装备 islot = vslot）；性别看号（女款不进清单）；等级要求导出但 **v1 不拦截穿戴** → 店里用价格分层。
- 装备清单 8 件：武器 01302007 Long Sword（攻 27 / Lv10 / 300）、01302005 Sabre（32 / 15 / 600）、01302004 Cutlass（52 / 35 / 5500）、01302015 Hero's Gladius（63 + 力量 1 / 45 / 11000）；防具 01040015 Brown Lolico Armor（防 15 / 10 / 300）、01040007 Green Leather Hoodwear（14 + 敏捷 1 / 15 / 500）、01040012 Blue Sergeant（25 + 力量 1 / 20 / 800）、01040022 Green Bennis Chainmail（22 + 敏捷 2 / 25 / 1300）。
- 纸娃娃美术：导出器里"这件装备换掉的新手零件"（外套→长外套、武器→剑、戒指 null）由导出器和烘焙共用，角色身上真的换了。戒指无美术且必须是 Ri。
- 掉落表 = 仓库手写 `drops/{怪号}.json`（进 git），少了报错；货单 = `shops/{npc号}.json`（皮奥 10000）。红蜗牛掉橙药 0.05。店主：西德 11000（蘑菇镇 1000000，卖武器）、潘姆 21000（南门街 2000000，卖防具）。
- 地图清单 = 11 张（30000 蜗牛花园、40000 小森林里面、50000 危险森林里面、1000002 Amherst 镇街、2000100 港口、20000、1010000 等）。**加不进去的**（导出器 fail loud）：30001（其门 out00 指向 30000 的 in01，官方 30000 没有 in01）；1010100-1010400（训练假人我们没有）；1000001/1000003/2000001（商店假人）；100000000 Henesys（传送门闭环太大）。官方彩虹岛共 19 个图号（区域清单里还有一个占位的 0 没名字）。训练中心 4 张（1010100/1010200/1010300/1010400）：门都是 out00 → 1010000 in00（我们有了 → 闭环），音乐 `Bgm34/MapleLeaf`，怪 = 树桩 15 / 红蜗牛 16 / 史莱姆 15 / 猪 13。
- 传送门：`tm = <图号>`，`tm = 999999999` 是没有目标的门。闭环：30000→40000、30001→30000、40000→50000+20000、50000→1000000+40000、1010100→1010000、2000100→自己。
- 升级曲线 = 20×等级+15 → 1-10 级共 1035 经验；1010000 清一遍 945。
- 任务与怪号**对不上**（以后做任务块要修）：官方任务目标用**老怪号**（100100/100101/120100），我们的怪是**新号**（100000/100001/100003）→ 任务 1016 永远完不成，文本 `#o100100#` 显示 "mob 100100"。任务导出要加一张老号→新号表。1016 的前置 1019 要 NPC 2005（山姆，不在彩虹岛任何一张图）→ 没有维多利亚岛就接不上。36301（糖，NPC 1541000，地图 10000）是送东西的，不受影响。
- 任务可行性：1041-1044（迈，NPC 12100）的杀怪目标**已经是新号**（100005/100002/100006/100007）→ 不用翻译；1041 要 3 个 4000003、1043 要 1 个 4000004（树桩/史莱姆掉）；1042 奖励 10 蛋 + 10 橙子、1044 奖励 15 肉 + 15 柠檬（2010001-4 在 `Item/Consume/0201.img`，有 hp/mp）；1041 的前置 1040 是进行中且要 1037、1037 要 NPC 2005 → 不导出 1040/1037，1041 直接接。1007（毕格斯）要 5 个 4000000 + 1 个 4000001，奖励两把匕首（1332007 攻 22、1332005 攻 20，都是 Wp，只加 incPAD）。做不了：1008/1020 要 4031161/4031162（箱子货）、8000 要 1042003（帽子，纸娃娃没帽子槽）、36303 前置 36302 没有 npc0、1009-1015（雨的书）对话是**选择题**（我们的对话没有选项按钮）。
- 音乐：登录曲 = `Sound/BgmUI.img/Title`（111 秒 44.1k 立体声）→ `bgm_login.wav`（`Title_old2` 是同一首）。**我们导出的 6 张图在 WZ 里共用同一首** `Bgm58/The Beginnig of The Adventure`（查 `Map/Map0/<id>.img/info/bgm` 确认）→ "每张图一首"本来就是这样，不是导出漏了。
- 死亡音：借图号 1210100 有自己的 Die（130101、130100 也有；210100 没有，继续用蜗牛的）→ key `die_pig`。

