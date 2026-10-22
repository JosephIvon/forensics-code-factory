# docs/TruthChain.md — P/Invoke 真值链审计

> 数据源：mcopilot 仓库 `knowledge/sdks/{dmc,gts,zmotion}.json`（生成时实时读取，非复制）
> 根目录：C:\Users\admin\.minimax\sessions\mvs_838bec01bb00476594f48711e6b172d0\workspace\mcopilot　|　生成时间：2026-10-20 12:43:08

**三根轴**：源 = verified（这条结论从哪来）｜核对 = check（验到哪一步：manual 人工 / hardware 真机 / auto 机器比对 / none 未核对）｜提取 = extraction（签名对是否等于调用对；judgment = 有语义陷阱，必须人签字）。

**纪律（R2）**：judgment 条目必须 manual/hardware 签字才能当事实用；`check=none` 的条目生成代码里已标 TODO[需核对厂商手册]。
**接缝说明**：本工厂的知识库不另设真值副本 —— mcopilot 侧 `scripts/annotate_provenance.py --check` 复核后，重新生成本套件即自动继承最新口径。

## 固高 GTS/GTN 系列

- 数据源：mcopilot knowledge/sdks/gts.json
- P/Invoke 22 条：钉扎 20 / 未收录(降级) 2 / 口径不一致 0；核对进度 manual 20 / hardware 0 / auto 0 / none 0

| P/Invoke | 本套件口径 | mcopilot 口径 | 状态 | 来源 |
|---|---|---|---|---|
| GT_Open | short(2参) | short(2参) | 已钉扎 | gts.cs:406 · 导出表✓ |
| GT_Close | short(0参) | short(0参) | 已钉扎 | gts.cs:408 · 导出表✓ |
| GT_Reset | short(0参) | short(0参) | 已钉扎 | gts.cs:541 · 导出表✓ |
| GT_LoadConfig | short(1参) | short(1参) | 已钉扎 | gts.cs:411 · 导出表✓ |
| GT_ClrSts | short(2参) | short(2参) | 已钉扎 | gts.cs:550 · 导出表✓ |
| GT_AxisOn | short(1参) | short(1参) | 已钉扎 | gts.cs:552 · 导出表✓ |
| GT_AxisOff | short(1参) | short(1参) | 已钉扎 | gts.cs:554 · 导出表✓ |
| GT_Stop | short(2参) | short(2参) | 已钉扎 | gts.cs:556 · 导出表✓ |
| GT_SetPos | short(2参) | short(2参) | 已钉扎 | gts.cs:645 · 导出表✓ |
| GT_SetVel | short(2参) | short(2参) | 已钉扎 | gts.cs:649 · 导出表✓ |
| GT_Update | short(1参) | short(1参) | 已钉扎 | gts.cs:643 · 导出表✓ |
| GT_PrfTrap | short(1参) | short(1参) | 已钉扎 | gts.cs:654 · 导出表✓ |
| GT_SetTrapPrm | short(2参) | short(2参) | 已钉扎 | gts.cs:656 · 导出表✓ |
| GT_PrfJog | short(1参) | short(1参) | 已钉扎 | gts.cs:661 · 导出表✓ |
| GT_SetJogPrm | short(2参) | short(2参) | 已钉扎 | gts.cs:663 · 导出表✓ |
| GT_GetStandardHomePrm | short(2参) | — | 未收录·已降级 | — |
| GT_ExecuteStandardHome | short(2参) | — | 未收录·已降级 | — |
| GT_GetSts | short(4参) | short(4参) | 已钉扎 | gts.cs:548 · 导出表✓ |
| GT_GetPrfPos | short(4参) | short(4参) | 已钉扎 | gts.cs:595 · 导出表✓ |
| GT_GetEncPos | short(4参) | short(4参) | 已钉扎 | gts.cs:509 · 导出表✓ |
| GT_GetDi | short(2参) | short(2参) | 已钉扎 | gts.cs:488 · 导出表✓ |
| GT_SetDoBit | short(3参) | short(3参) | 已钉扎 | gts.cs:475 · 导出表✓ |

**未收录（已显式降级 TODO）**：GT_GetStandardHomePrm, GT_ExecuteStandardHome

