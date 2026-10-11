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

**W1 已完成：WzLib 冒烟测试。** 15 个数据目录全部解析成功（KMST 1205+ 新加密格式），
角色 / 地图 / 怪物图片均可解密。下一周（W2）：导出器把第一张地图和第一个角色角色导出到
PNG + JSON，并在客户端窗口里画出来。

## 运行冒烟测试

```sh
dotnet run --project MushTown.Exporter -- <你的 MapleStory\appdata\Data 路径>
```

导出产物写到 `assets/`、账号数据写到 `data/`、日志写到 `logs/`，三者都在 `.gitignore` 里：
游戏素材是 Nexon 版权内容，不进仓库。
