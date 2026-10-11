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
| `WzComparerR2/` | git 子模块，MIT 协议，WZ 解析库（WzLib） |

## 前置条件

- .NET 8 SDK
- 本机装有 MapleStory 客户端（冒烟测试默认指向 `E:\游戏\maplestory\appdata\Data`，
  可用命令行参数指定别的路径）
- `git clone --recurse-submodules` 拉取子模块

## 当前状态

**W2 已完成：真实的冒险岛地图 + 角色进窗口。** 导出器从本机客户端的 WZ 数据里
导出冒险岛地图 100000000（背景 16 / 地块 1439 / 物件 206 / 落脚点 356）和角色
stand2 站姿动作（3 帧，身体/手臂/手/头/耳/脸/头发 7 个部件按骨头合成），客户端 MonoGame
窗口按相机偏移绘制：背景按 `cx` 铺砖 + `rx` 视差、物件支持 `f=1` 左右翻，整幅画面
按比例放大到全屏（屏幕不是 1366×768 的形状时左右或上下留黑边），角色站在出生点地面上。
下一周（W3）：纸娃娃数据侧——骨骼 + 帧图层全量导出（18 槽位），动画播放留 W4。

## 常用命令

命令都在仓库根目录下运行（`assets/`、`logs/` 等相对路径都按当前目录解析）。

```sh
dotnet build MushTown.sln                                          # 全量编译
dotnet run --project MushTown.Exporter -- smoke [Data路径]           # WzLib 冒烟测试（默认子命令）
dotnet run --project MushTown.Exporter -- dump [路径] [深度]         # 看 Base 树里的 WZ 节点结构（缺路径=整棵树）
dotnet run --project MushTown.Exporter -- catdump <分类> <路径> [深度] # 看某个分类 wz（如 Character）里的节点
dotnet run --project MushTown.Exporter -- find <节点名>              # 按名字定位节点
dotnet run --project MushTown.Exporter -- export [Data路径] [输出目录] # 导出 assets/
dotnet run --project MushTown.Client                                # 打开游戏窗口
dotnet run --project MushTown.Client -- --shot logs/w2_acceptance.png  # 截图后退出
git clone --recurse-submodules <仓库>                               # 克隆要带子模块
```

Data路径参数只有 `smoke` 和 `export` 认（默认 `E:\游戏\maplestory\appdata\Data`）；
`dump`/`catdump`/`find` 固定用默认路径，第二个参数已经是它们自己的参数。

导出产物写到 `assets/`、账号数据写到 `data/`、日志写到 `logs/`，三者都在 `.gitignore` 里：
游戏素材是 Nexon 版权内容，不进仓库。
