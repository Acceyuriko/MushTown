# 官方视频参考资料（2026-10-22）

视频：`C:\Users\zec_i\Downloads\2026-10-22 13-14-23.mp4`，1280×720，15 分 52 秒。
是**官方客户端（GMSR 私服）**，不是我们的游戏。拿它当"官方长什么样、怎么动"的参照。

重新截帧：

```
C:\Users\zec_i\Documents\ffmpeg-master-latest-win64-gpl-shared\bin\ffmpeg.exe -ss 00:07:20 -i "<视频>" -frames:v 1 -q:v 2 out.jpg
```

分辨率不同是正常的：视频 1280×720，我们内部固定 1366×768 再整幅放大。

## 图（`frames/`，不进 git）

图是 Nexon 版权，只留在本地（见 AGENTS.md 红线），本目录只有这份文字进 git。

| 文件 | 内容 |
|---|---|
| `menu440.jpg` | MENU 窗全貌：7 个蓝页签 Character / Item / Battle / Adventure / Community / Events-Maple Rewards / Other，每行右边有键帽徽章，底部 7 个按钮（Cash Shop / Auction House / CH. Change Channel / Switch Character / Key Bindings / Settings / Exit Game），右下角菱形 X |
| `key497.jpg` | 键位窗（KEYBOARD SHORTCUTS）：左边整个键盘、右边动作格子、底部 Combo Key / Quick Slots / Reset / Skill / Cancel / Save |
| `bili_hud.jpg` | 整屏 HUD：左上小地图（Empress' Road / Erewe + Quick Move 下拉）、右上 GUIDE 小窗、右中 QUEST HELPER 条、右下 MAPLE WORLD [0/5000] 频道条、左下聊天框 + Lv.200 名牌、底部 HP/MP/EXP 三条 + 快捷栏两排 |
| `bili_sheet.jpg` / `bili_sheet2.jpg` / `bili_sheet3.jpg` | 视频截图拼图：物品栏、角色页、事件面板、地图等（宽屏 1600×900 一张 4 格） |
| `cmp_ref_qslot.png` | 底部快捷栏特写：2 排 × 18 格，格上是键帽（Shift/Ins/Hm/Pup/Ctrl/Del/End/Pdn + 1..0 + K/L/Q/W/E/R/T/A/S/D/F/G/N/M），彩色格子 = 有绑定 |
| `cmp_ref_tr.png` | 任务追踪条特写 |
| `tip_hp.jpg` / `tip_mp.jpg` / `tip_hp_zoom.jpg` | 13:23–13:26 鼠标停在 MP 条、HP 条上：**深色圆角小框 + 浅灰细边 + 白字**，写 `HP (Health Point)` / `MP (Mana Point)`，框在光标右边、和光标同高。没有数字，只有名字 |
| `vid_05m00s.jpg` | 5:00 整帧：LINK SKILL 窗开着，底部聊天框、HP/MP/EXP 三条（HP 上方有 "O Aran in Memory" 地图标签），EXP 条下写 `10.10 [0.1071%]` |

## 时间点

| 时间 | 看到什么 |
|---|---|
| 0:21 | MENU 窗开着：鼠标划过条目整行浅蓝高亮；灰条目不变色、点了没反应；每行右边键帽徽章；底部 "Press the Tab or Arrow Keys to move between menus"；右上角 X |
| 0:50 | 窗关着时人物正常站、走 |
| 5:00 | LINK SKILL 窗；底部 HP/MP/EXP 三条和聊天框 |
| 7:20 | MENU 窗（`menu440.jpg`） |
| 8:00 / 9:03 | 世界地图：左边大图 + 地区标签，右边目的地列表；鼠标停在目的地上，地图上弹出地图名和说明；点 Go 才传送 |
| 8:15–8:17 | 键位窗（`key497.jpg`） |
| 10:50 | 拖物品时图标跟鼠标走；双击直接用（喝药） |
| 13:23–13:26 | 鼠标停在 MP / HP 条上：旁边弹出 `MP (Mana Point)` / `HP (Health Point)` 的小黑框（见 `tip_*.jpg`） |
| 全片 | 底部快捷栏多格、格下有键帽；小地图下有 Quick Move 下拉；聊天框 4 个页签 All/Buddy/Party/Friends；NPC 名字标签 + 头顶气泡 |

## 哪些东西 WZ 里没有（查过，别再做）

- **Quick Move 下拉**：整个 UI 树 + Etc + String 树都没有，只有字符串 `SID_QUICK_MOVE_DISABLE`。
- **左上那 4 个 NEW 图标**（灯泡 / 星星 / 铜铃+NEW / 本子+NEW）：把 `UI/_Canvas` 里 14×14 到 44×44 的画布逐个扫过，没有。
- **红色 M 钮**（MAPLE WORLD 面板右端）：2820 个红色候选里没有。
- **MENU 里我们没规则的行**（Legion / V Matrix / Familiar / 星之力 / Buddy / 活动…）：美术有，规则没有 → 画灰色 disabled。

## 结论落在哪

- **长得像**（美术、面板、布局）→ `plans/ui-gaps-2026-10-20.md`
- **用起来像**（窗口行为、键盘、鼠标反馈）→ `plans/interaction-2026-10-24.md`
- **已经做完的**：MENU 窗、QUICK MENU 条、键窗三块、小地图面板、频道条、快捷栏格子、任务追踪、物品 tooltip、窗口置顶、Esc 逐层关
- **细节记忆**：`.dsh/memory.md` 里带"视频"的条目
