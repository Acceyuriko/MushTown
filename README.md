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
- 本机装有 MapleStory 客户端（冒烟测试默认指向 `E:\游戏\maplestory\appdata\Data`，
  可用命令行参数指定别的路径）
- `git clone --recurse-submodules` 拉取子模块

## 当前状态

**W2 已完成：真实的冒险岛地图 + 角色进窗口。** 导出器从本机客户端的 WZ 数据里
导出冒险岛地图 100000000（背景 16 / 地块 1439 / 物件 206 / 落脚点 356），客户端 MonoGame
窗口按相机偏移绘制：背景按 `cx` 铺砖 + `rx` 视差、物件支持 `f=1` 左右翻，整幅画面
按比例放大到全屏（屏幕不是 1366×768 的形状时左右或上下留黑边），角色站在出生点地面上。

**W3 已完成：纸娃娃穿上装备。** 角色从裸体 4 件套变成 11 个 img
（身体/头/脸/头发 + 帽子/上衣/裤子/鞋/手套/披风/武器），stand2 站姿 3 帧、每帧 16 个
部件按 zmap 图层排序画对（披风在后、衣服盖住身体、武器在前）。客户端逻辑不用改（只改了一行注释）。

**W4 已完成：角色会动了。** 导出 stand2/walk2/jump 三个动作（每帧 16/16/17 个
部件），客户端按 delay 切帧：左右走（walk2）、Alt 跳（jump）、站立（stand2），
朝向左右翻转，相机跟随，平地跳跃落地。截图加 `--action/--frame/--facing` 参数。

**W5 已完成：双人同屏。** 一个开房（`--host`）、一个加入（`--join <ip>`），
互相看到对方跑/跳（TCP + 长度头二进制消息，服务端中转状态）。不带参数还是单机。

**W6 已完成：彩虹岛全境可走。** 6 张图闭集合：传送门（↑进，动画实机渲染）、
梯绳爬行、单向平台下落、趴下、按图隔离的多人同步。

**W7 已完成：账号大厅。** 本地账号（PBKDF2）+ 3 角色栏 + 位置存档
（`data/accounts.json`，原子写）：注册→建角→下线→重登，数据不丢。
无参数进登录菜单；`--host/--join/--shot` 照旧直跳（无存档）。

## 常用命令

命令都在仓库根目录下运行（`assets/`、`logs/` 等相对路径都按当前目录解析）。

```sh
dotnet build MushTown.sln                                          # 全量编译
dotnet test MushTown.sln                                           # 跑单元测试
dotnet run --project MushTown.Exporter -- smoke [Data路径]           # WzLib 冒烟测试（默认子命令）
dotnet run --project MushTown.Exporter -- dump [路径] [深度]         # 看 Base 树里的 WZ 节点结构（缺路径=整棵树）
dotnet run --project MushTown.Exporter -- catdump <分类> <路径> [深度] # 看某个分类 wz（如 Character）里的节点
dotnet run --project MushTown.Exporter -- find <节点名>              # 按名字定位节点
dotnet run --project MushTown.Exporter -- export [Data路径] [输出目录] # 导出 assets/
dotnet run --project MushTown.Client                                # 打开游戏窗口（账号登录菜单）
dotnet run --project MushTown.Client -- --host                       # 开房（本机当主机，无存档直跳）
dotnet run --project MushTown.Client -- --join <ip>                   # 加入主机（本机双开用 127.0.0.1）
dotnet run --project MushTown.Client -- --shot logs/x.png [--map 10000] [--action walk2] [--frame 2] [--facing left] [--screen login|server|chars|job|setup|register|delete|panel]  # 截图后退出（frame 从 0 数）
git clone --recurse-submodules <仓库>                               # 克隆要带子模块
```

Data路径参数只有 `smoke` 和 `export` 认（默认 `E:\游戏\maplestory\appdata\Data`）；
`dump`/`catdump`/`find` 固定用默认路径，第二个参数已经是它们自己的参数。

导出产物写到 `assets/`、账号数据写到 `data/`、日志写到 `logs/`，三者都在 `.gitignore` 里：
游戏素材是 Nexon 版权内容，不进仓库。
