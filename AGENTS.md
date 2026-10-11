# MushTown 项目约定

只写全局约定（~/.dsh/AGENTS.md）之外、本项目特有的规则。
大头细节在 `.dsh/memory.md`，完整计划在 `plans/`，动手前先读。

- 交流用中文（覆盖全局的英文规则）。

## 工作节奏

- 按周切块：一次只做一周（约 10 小时），范围以 `plans/` 里的周表为准。
- 每周结束必须达到周表验收标准（能跑、能看）；达不到就把下一个题砍半挪过来，不许烂尾。
- **一周做完就停下等用户验收**，不要顺手做下周。
- 每周一个 git commit；债不过周。

## 红线

- `assets/` `data/` `logs/` 永远不进 git（游戏素材是 Nexon 版权，仓库只放工具和文档）。
- 单 exe 双身份（本机开房 / 加入主机）：Server 是被客户端托管的类库，不写 Console 输出、不碰 UI，日志走回调写滚动文件。
- 快捷键一个都不分给调试工具，日志面板用鼠标点开。
- `WzComparerR2/` 是 git 子模块，锁定 commit；要改解析器先 fork，不要就地大改。

## 常用命令

```sh
dotnet build MushTown.sln                                # 全量编译
dotnet run --project MushTown.Exporter -- <Data路径>      # WzLib 冒烟测试
git clone --recurse-submodules <仓库>                     # 克隆要带子模块
```
