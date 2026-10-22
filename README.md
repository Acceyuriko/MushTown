# MushTown

局域网复刻 MapleStory（冒险岛）的自用项目。.NET 8 + MonoGame，服务端与客户端在同一进程内
（星露谷式：本机开房 / 加入主机）。

完整技术选型、里程碑与按周切块计划见
[plans/MushTown-技术选型与里程碑-2026-10-11.md](plans/MushTown-技术选型与里程碑-2026-10-11.md)。

## 结构

| 项目 | 作用 |
|------|------|
| `MushTown.Shared` | 协议消息、数据模型 |
| `MushTown.Server` | 类库：账号、世界、怪物、战斗（无 UI、无 Console 输出） |
| `MushTown.Client` | MonoGame 主程序（窗口 / 渲染 / 输入；开房时托管 Server） |
| `MushTown.Exporter` | 开发工具：把 WZ 资源导出成 PNG + JSON |
| `MushTown.Tests` | MSTest 单元测试 |
| `WzComparerR2/` | git 子模块，MIT 协议，WZ 解析库（WzLib） |

## 前置条件

- .NET 8 SDK
- 本机装有 MapleStory 客户端（冒烟测试默认指向 `E:\game\maplestory\appdata\Data`，
  可用命令行参数指定别的路径）
- `git clone --recurse-submodules` 拉取子模块
- 选人背景图自备：`assets/back/charselect.png`（版权原因不进仓库，缺了进选人屏直接报错）。
  找一张 1366×768 或同比例的图放过去即可，`export` 不管它。

## 当前状态

**W2 已完成：真实的冒险岛地图 + 角色进窗口。** 导出器从本机客户端的 WZ 数据里
导出冒险岛地图 100000000（背景 16 / 地块 1439 / 物件 206 / 落脚点 356），客户端 MonoGame
窗口按相机偏移绘制：背景按 `cx` 铺砖 + `rx` 视差、物件支持 `f=1` 左右翻，整幅画面
按比例放大到全屏（屏幕不是 1366×768 的形状时左右或上下留黑边），角色站在出生点地面上。

**W3 已完成：纸娃娃穿上装备。** 角色从裸体 4 件套变成 8 个 img
（身体/头/脸/头发 + 裤子/上衣/鞋/武器），stand1 站姿 3 帧、每帧 12 个
部件按 zmap 图层排序画对（裤子在下、上衣盖住身体、武器在前）。客户端逻辑不用改（只改了一行注释）。

**W4 已完成：角色会动了。** 导出 stand1/walk1/jump 三个动作（每帧 12/12/14 个
部件），客户端按 delay 切帧：左右走（walk1）、Alt 跳（jump）、站立（stand1），
朝向左右翻转，相机跟随，平地跳跃落地。截图加 `--action/--frame/--facing` 参数。

**W5 已完成：双人同屏。** 一个开房（`--host`）、一个加入（`--join <ip>`），
互相看到对方跑/跳（TCP + 长度头二进制消息，服务端中转状态）。不带参数还是单机。

**W6 已完成：彩虹岛全境可走。** 6 张图闭集合：传送门（↑进，动画实机渲染）、
梯绳爬行、单向平台下落、趴下、按图隔离的多人同步。

**W7 已完成：存档大厅。** 本地存档（无账号无密码）+ 每档 3 角色栏 + 位置存档
（`data/saves.json`，原子写）：建角→下线→重登，数据不丢。
无参数进存档菜单；`--host/--join/--shot` 照旧直跳（无存档）。

## 界面布局文件

五个页面的位置、盒子尺寸、字号都在 `layouts/` 下的 json 里：`menu-layout.json`（主菜单）、
`charselect-layout.json`（选角）、`jobselect-layout.json`（选职业）、`setup-layout.json`（创建角色）、
`hud-layout.json`（游戏内面板）。
改完存盘就生效（Debug 版热重载，不用重编译）。键可以只留想改的那几个，其余保持现值。

## 常用命令

命令都在仓库根目录下运行（`assets/`、`logs/` 等相对路径都按当前目录解析）。

```sh
dotnet build MushTown.sln                                          # 全量编译
dotnet test MushTown.sln                                           # 跑单元测试
dotnet run --project MushTown.Exporter -- smoke [Data路径]           # WzLib 冒烟测试（默认子命令）
dotnet run --project MushTown.Exporter -- dump [路径] [深度]         # 看 Base 树里的 WZ 节点结构（缺路径=整棵树）
dotnet run --project MushTown.Exporter -- catdump <分类> <路径> [深度] # 看某个分类 wz（如 Character）里的节点
dotnet run --project MushTown.Exporter -- find <节点名>              # 按名字定位节点
dotnet run --project MushTown.Exporter -- pick item <目录> <前缀>  # 列出该目录下 img 的 id + 名字 + 动作
dotnet run --project MushTown.Exporter -- pick hair <输出png> <id,id,...>  # 发型对照图（选默认发型用）
dotnet run --project MushTown.Exporter -- export [Data路径] [输出目录] # 导出 assets/
dotnet run --project MushTown.Client                                # 打开游戏窗口（存档菜单）
dotnet run --project MushTown.Client -- --host                       # 开房（本机当主机，无存档直跳）
dotnet run --project MushTown.Client -- --join <ip>                   # 加入主机（本机双开用 127.0.0.1）
dotnet run --project MushTown.Client -- --shot logs/x.png [--map 10000] [--travel 2000000] [--action walk1] [--frame 2] [--facing left] [--screen saves|server|chars|chars2|job|job2|setup|delete|inv|use|stat|hud|skill|quest|talk|settings|shop]  # 截图后退出（frame 从 0 数）
git clone --recurse-submodules <仓库>                               # 克隆要带子模块
```

Data路径参数只有 `smoke` 和 `export` 认（默认 `E:\game\maplestory\appdata\Data`）；
`dump`/`catdump`/`find` 固定用默认路径，第二个参数已经是它们自己的参数。

导出产物写到 `assets/`、存档数据写到 `data/`、日志写到 `logs/`，三者都在 `.gitignore` 里：
游戏素材是 Nexon 版权内容，不进仓库。
