# MushTown 项目约定

只写全局约定（~/.dsh/AGENTS.md）之外、本项目特有的规则。
大头细节在 `.dsh/memory.md`，完整计划在 `plans/`，动手前先读。

## 架构设计
- 仿照 Unity 的 **Object-Component** 实现，编写和审查时需注意，代码**必须符合 Unity 架构要求**，不得有散乱的全局代码。

## 工作节奏

- 按周切块：一次只做一周（约 10 小时），范围以 `plans/` 里的周表为准。
- 每周结束必须达到周表验收标准（能跑、能看）；达不到就把下一个题砍半挪过来，不许烂尾。
- **一周做完就停下等用户验收**，不要顺手做下周。
- **每个 ticket 先走 review-loop，收敛后才能提交；提交后再呈递验收。**
- 每周一个 git commit；债不过周。

## 红线

- `assets/` `data/` `logs/` 永远不进 git（游戏素材是 Nexon 版权，仓库只放工具和文档）。
- 单 exe 双身份（本机开房 / 加入主机）：Server 是被客户端托管的类库，不写 Console 输出、不碰 UI，日志走回调写滚动文件。
- `WzComparerR2/` 是 git 子模块，锁定 commit；要改解析器先 fork，不要就地大改。
- 客户端**内部固定按 1366×768 渲染**（GameWidth/GameHeight），再整幅按比例放大到无边框全屏；不是扩大相机范围。窗口尺寸变化要跟着当前屏幕重新适配。
- 界面的定位数字、盒子尺寸、字号都放 `layouts/*.json`（菜单/选角/选职业/创建角色/HUD 各一个），不要写死在代码里；Debug 版存盘即热重载，Release 版只在启动时读一次。

## 常用命令

命令都在仓库根目录下运行（`assets/`、`logs/` 等相对路径都按当前目录解析）。

```sh
dotnet build MushTown.sln                                  # 全量编译
dotnet test MushTown.sln                                   # 跑单元测试
dotnet format MushTown.sln --exclude WzComparerR2          # 格式化（必须带排除：sln 含锁定的子模块工程）
dotnet run --project MushTown.Exporter -- smoke [Data路径]   # WzLib 冒烟测试
dotnet run --project MushTown.Exporter -- dump [路径] [深度]  # 看 Base 树的节点结构（缺路径=整棵树）
dotnet run --project MushTown.Exporter -- catdump <分类> <路径> [深度]  # 看分类 wz 的节点
dotnet run --project MushTown.Exporter -- cat <分类> <路径>  # 打印长字符串节点全文
dotnet run --project MushTown.Exporter -- raw <分类> <路径> <输出>  # 保存 raw 节点字节
dotnet run --project MushTown.Exporter -- spine <skel 文件> [bone]  # 看 .skel 的骨骼、槽位、时间轴
dotnet run --project MushTown.Exporter -- spinebake [id|all]  # 烘焙职业 Spine 到 assets/spine/（缺 id=全烘焙）
dotnet run --project MushTown.Exporter -- export [Data路径] [输出目录]  # 导出 assets/
dotnet run --project MushTown.Exporter -- sound [输出目录]  # 只导出声音（音效 + 每张图的 bgm + 登录曲）；拉到这个改动后必须重跑一次（解码器换了，所有 wav 重新编码）
dotnet run --project MushTown.Exporter -- panel <inventory|equip|stat|skill|quest|map|keys|menu|quickmenu|chat|shop|buff|guide|party|cashshop> [输出目录]  # 只导出一个面板的美术
dotnet run --project MushTown.Exporter -- find <节点名> [分类]  # 按名字定位 WZ 节点（带分类就在该分类里找，名字可以只写一部分）
dotnet run --project MushTown.Exporter -- questscan <npcid> [<npcid>...]  # 列出起止于这些 NPC 的任务
dotnet run --project MushTown.Exporter -- pick item <分类目录> <前缀>  # 列出该目录下的 img id + 名字 + 动作
dotnet run --project MushTown.Exporter -- pick hair <输出png> <id,id,...>  # 每个发型一张的对照图（选默认发型用）
dotnet run --project MushTown.Client                        # 打开游戏窗口（存档菜单）
dotnet run --project MushTown.Client -- --host               # 开房（无存档直跳）
dotnet run --project MushTown.Client -- --join <ip>           # 加入主机
dotnet run --project MushTown.Client -- --shot logs/x.png [--skin 0] [--map 10000] [--travel 2000000] [--action walk1] [--frame 2] [--facing left] [--screen saves|server|chars|chars2|job|job2|setup|delete|inv|use|invscroll|stat|equipui|decorations|hud|skill|quest|questask|questforfeit|questpick|talk|settings|shop|minimap|minimapbig|minimapnpc|minimapfilter|worldmap|worldmapsearch|worldmapask|worldmapcombo|keys|menu|guide|guidecard|party|partysearch|partyask|cashshop|cashshopfashion|cashshopfashionoutfits|cashshopreset|cashshopempty|cashshopcart|cashshopladder|cashshopbeauty] [--skilltab 0] [--talk <npc>] [--job <id>] [--hit | --skill <id> | --drop] [--wear] [--aiticks <n>] [--hover <x,y>]  # 截图后退出（验收用，frame 从 0 数，--skin 0..5 验收皮肤，--hit 摆拍打怪，--skill 摆拍技能，--skilltab 选技能页，--drop 摆拍掉落，--wear 穿满 demo 装备，--aiticks 跑 n 步 AI，quest 摆拍任务窗+助手，questask/questforfeit 摆拍传送/放弃确认框，questpick 摆拍选中任务详情，talk+--talk 摆拍 NPC 对话，settings 摆拍音量窗，shop+--talk 摆拍商店，stat 摆拍属性窗，equipui 摆拍新装备窗，decorations 摆拍装饰栏 Outfits 页，invscroll 摆拍小窗滚到底，minimap/minimapbig 摆拍小地图小布局和大布局（minimapnpc/minimapfilter 带 NPC 列表和过滤器），worldmap 摆拍大地图窗（worldmapsearch 带搜索窗，worldmapask 摆确认框，worldmapcombo 摆展开的下拉列表），keys 摆拍快捷键窗，menu 摆拍 MENU 大菜单窗，guide 摆拍 GUIDE 内容指南窗（guidecard 摆拍 GUIDE 小窗），party 摆拍组队窗（partysearch 摆拍组队搜索窗，partyask 摆拍组队邀请弹窗），cashshop 摆拍 Utility / Convenience，cashshopfashion 摆拍 Fashion / Weapons，cashshopfashionoutfits 摆拍 Fashion / Outfits，cashshopreset/cashshopempty 摆拍搜索结果，cashshopcart 摆拍预览购物车，cashshopladder 摆拍爬梯背面，cashshopbeauty 摆拍 Beauty / Hair Styles，job/job2+--job 摆拍指定职业，--frame 非0时覆盖脊柱冻结帧，创建角色页用 --frame 摆小人待机帧，--hover 把鼠标停在给定点（1366x768 游戏坐标，不是屏幕像素），tooltip 都按它算）
git clone --recurse-submodules <仓库>                       # 克隆要带子模块
```
