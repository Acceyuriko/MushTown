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
stand2 站姿动作（3 帧，body/arm/hand 按骨头合成），客户端 MonoGame 窗口按相机裁剪
绘制，角色站在出生点地面上。下一周（W3）：纸娃娃动画播放（walk/alert/jump + 帧延迟）。

## 常用命令

```sh
dotnet build MushTown.sln                                          # 全量编译
dotnet run --project MushTown.Exporter -- smoke                     # WzLib 冒烟测试（默认）
dotnet run --project MushTown.Exporter -- dump <路径> [深度]         # 看 WZ 节点结构
dotnet run --project MushTown.Exporter -- export                    # 导出 assets/
dotnet run --project MushTown.Client                                # 打开游戏窗口
dotnet run --project MushTown.Client -- --shot logs/w2_acceptance.png  # 截图后退出
git clone --recurse-submodules <仓库>                               # 克隆要带子模块
```

导出产物写到 `assets/`、账号数据写到 `data/`、日志写到 `logs/`，三者都在 `.gitignore` 里：
游戏素材是 Nexon 版权内容，不进仓库。
