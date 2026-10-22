# docs/TruthChain.md — P/Invoke 真值链审计

> 数据源：mcopilot 仓库 `knowledge/sdks/{dmc,gts,zmotion}.json`（生成时实时读取，非复制）
> 根目录：D:\workspace\mcopilot　|　生成时间：2026-10-22 12:00:23

**三根轴**：源 = verified（这条结论从哪来）｜核对 = check（验到哪一步：manual 人工 / hardware 真机 / auto 机器比对 / none 未核对）｜提取 = extraction（签名对是否等于调用对；judgment = 有语义陷阱，必须人签字）。

**纪律（R2）**：judgment 条目必须 manual/hardware 签字才能当事实用；`check=none` 的条目生成代码里已标 TODO[需核对厂商手册]。
**接缝说明**：本工厂的知识库不另设真值副本 —— mcopilot 侧 `scripts/annotate_provenance.py --check` 复核后，重新生成本套件即自动继承最新口径。

## 雷赛 DMC/EMC 系列 (LTDMC)

- 数据源：mcopilot knowledge/sdks/dmc.json
- P/Invoke 16 条：钉扎 13 / 未收录(降级) 3 / 口径不一致 0；核对进度 manual 0 / hardware 0 / auto 0 / none 13

| P/Invoke | 本套件口径 | mcopilot 口径 | 状态 | 来源 |
|---|---|---|---|---|
| dmc_board_init | short(0参) | short(0参) | 已钉扎·未核对 | — · 导出表无背书 |
| dmc_board_close | short(0参) | short(0参) | 已钉扎·未核对 | — · 导出表无背书 |
| dmc_pmove | short(4参) | short(4参) | 已钉扎·未核对 | — · 导出表无背书 |
| dmc_set_profile | short(7参) | short(7参) | 已钉扎·未核对 | — · 导出表无背书 |
| dmc_stop | short(3参) | short(3参) | 已钉扎·未核对 | — · 导出表无背书 |
| dmc_check_done | short(2参) | short(2参) | 已钉扎·未核对 | — · 导出表无背书 |
| dmc_get_position | int(2参) | int(2参) | 已钉扎·未核对 | — · 导出表无背书 |
| dmc_get_encoder | int(2参) | int(2参) | 已钉扎·未核对 | — · 导出表无背书 |
| dmc_vmove | short(3参) | short(3参) | 已钉扎·未核对 | — · 导出表无背书 |
| dmc_change_speed | short(4参) | — | 未收录·已降级 | — |
| dmc_home_move | short(2参) | short(2参) | 已钉扎·未核对 | — · 导出表无背书 |
| dmc_set_homemode | short(6参) | short(6参) | 已钉扎·未核对 | — · 导出表无背书 |
| dmc_read_inbit | short(2参) | short(2参) | 已钉扎·未核对 | — · 导出表无背书 |
| dmc_write_outbit | short(3参) | short(3参) | 已钉扎·未核对 | — · 导出表无背书 |
| dmc_get_ad_input | short(3参) | — | 未收录·已降级 | — |
| dmc_set_da_output | short(3参) | — | 未收录·已降级 | — |

**未收录（已显式降级 TODO）**：dmc_change_speed, dmc_get_ad_input, dmc_set_da_output

## 固高 GTS/GTN 系列

- 数据源：mcopilot knowledge/sdks/gts.json
- P/Invoke 22 条：钉扎 19 / 未收录(降级) 3 / 口径不一致 0；核对进度 manual 0 / hardware 0 / auto 0 / none 19

| P/Invoke | 本套件口径 | mcopilot 口径 | 状态 | 来源 |
|---|---|---|---|---|
| GT_Open | short(2参) | short(2参) | 已钉扎·未核对 | — · 导出表无背书 |
| GT_Close | short(0参) | short(0参) | 已钉扎·未核对 | — · 导出表无背书 |
| GT_Reset | short(0参) | short(0参) | 已钉扎·未核对 | — · 导出表无背书 |
| GT_LoadConfig | short(1参) | short(1参) | 已钉扎·未核对 | — · 导出表无背书 |
| GT_ClrSts | short(2参) | short(2参) | 已钉扎·未核对 | — · 导出表无背书 |
| GT_AxisOn | short(1参) | short(1参) | 已钉扎·未核对 | — · 导出表无背书 |
| GT_AxisOff | short(1参) | short(1参) | 已钉扎·未核对 | — · 导出表无背书 |
| GT_Stop | short(2参) | short(2参) | 已钉扎·未核对 | — · 导出表无背书 |
| GT_SetPos | short(2参) | short(2参) | 已钉扎·未核对 | — · 导出表无背书 |
| GT_SetVel | short(2参) | short(2参) | 已钉扎·未核对 | — · 导出表无背书 |
| GT_Update | short(1参) | short(1参) | 已钉扎·未核对 | — · 导出表无背书 |
| GT_PrfTrap | short(1参) | short(1参) | 已钉扎·未核对 | — · 导出表无背书 |
| GT_SetTrapPrm | short(2参) | short(2参) | 已钉扎·未核对 | — · 导出表无背书 |
| GT_PrfJog | short(1参) | short(1参) | 已钉扎·未核对 | — · 导出表无背书 |
| GT_SetJogPrm | short(2参) | short(2参) | 已钉扎·未核对 | — · 导出表无背书 |
| GT_GetStandardHomePrm | short(2参) | — | 未收录·已降级 | — |
| GT_ExecuteStandardHome | short(2参) | — | 未收录·已降级 | — |
| GT_GetSts | short(4参) | short(4参) | 已钉扎·未核对 | — · 导出表无背书 |
| GT_GetPrfPos | short(4参) | short(4参) | 已钉扎·未核对 | — · 导出表无背书 |
| GT_GetEncPos | short(4参) | — | 未收录·已降级 | — |
| GT_GetDi | short(2参) | short(2参) | 已钉扎·未核对 | — · 导出表无背书 |
| GT_SetDoBit | short(3参) | short(3参) | 已钉扎·未核对 | — · 导出表无背书 |

**未收录（已显式降级 TODO）**：GT_GetStandardHomePrm, GT_ExecuteStandardHome, GT_GetEncPos

## 正运动 ZMC 系列

- 数据源：mcopilot knowledge/sdks/zmotion.json
- P/Invoke 20 条：钉扎 17 / 未收录(降级) 3 / 口径不一致 0；核对进度 manual 0 / hardware 0 / auto 0 / none 17

| P/Invoke | 本套件口径 | mcopilot 口径 | 状态 | 来源 |
|---|---|---|---|---|
| ZAux_OpenEth | int(2参) | int(2参) | 已钉扎·未核对 | — · 导出表无背书 |
| ZAux_Close | int(1参) | int(1参) | 已钉扎·未核对 | — · 导出表无背书 |
| ZAux_Direct_SetUnits | int(3参) | int(3参) | 已钉扎·未核对 | — · 导出表无背书 |
| ZAux_Direct_SetSpeed | int(3参) | int(3参) | 已钉扎·未核对 | — · 导出表无背书 |
| ZAux_Direct_SetAccel | int(3参) | int(3参) | 已钉扎·未核对 | — · 导出表无背书 |
| ZAux_Direct_SetDecel | int(3参) | int(3参) | 已钉扎·未核对 | — · 导出表无背书 |
| ZAux_Direct_Single_MoveAbs | int(3参) | int(3参) | 已钉扎·未核对 | — · 导出表无背书 |
| ZAux_Direct_Single_Move | int(3参) | int(3参) | 已钉扎·未核对 | — · 导出表无背书 |
| ZAux_Direct_MoveAbs | int(4参) | int(4参) | 已钉扎·未核对 | — · 导出表无背书 |
| ZAux_Direct_Single_Vmove | int(3参) | int(3参) | 已钉扎·未核对 | — · 导出表无背书 |
| ZAux_Direct_Single_Cancel | int(3参) | int(3参) | 已钉扎·未核对 | — · 导出表无背书 |
| ZAux_Direct_Single_Datum | int(3参) | int(3参) | 已钉扎·未核对 | — · 导出表无背书 |
| ZAux_Direct_GetMpos | int(3参) | int(3参) | 已钉扎·未核对 | — · 导出表无背书 |
| ZAux_Direct_GetDpos | int(3参) | int(3参) | 已钉扎·未核对 | — · 导出表无背书 |
| ZAux_Direct_GetIfIdle | int(3参) | int(3参) | 已钉扎·未核对 | — · 导出表无背书 |
| ZAux_Direct_GetIn | int(3参) | int(3参) | 已钉扎·未核对 | — · 导出表无背书 |
| ZAux_Direct_SetOp | int(3参) | int(3参) | 已钉扎·未核对 | — · 导出表无背书 |
| ZAux_Direct_GetAD | int(3参) | — | 未收录·已降级 | — |
| ZAux_Direct_SetDA | int(3参) | — | 未收录·已降级 | — |
| ZAux_Direct_SetAxisEnable | int(3参) | — | 未收录·已降级 | — |

**未收录（已显式降级 TODO）**：ZAux_Direct_GetAD, ZAux_Direct_SetDA, ZAux_Direct_SetAxisEnable

## 软赢 (SoftWin)

- 数据源：mcopilot 知识库无 软赢 (SoftWin) 的绑定口径
- P/Invoke 0 条：钉扎 0 / 未收录(降级) 0 / 口径不一致 0；核对进度 manual 0 / hardware 0 / auto 0 / none 0

（本品牌无 P/Invoke extern：骨架品牌，待官方一手件）

## 高川

- 数据源：mcopilot 知识库无 高川 的绑定口径
- P/Invoke 0 条：钉扎 0 / 未收录(降级) 0 / 口径不一致 0；核对进度 manual 0 / hardware 0 / auto 0 / none 0

（本品牌无 P/Invoke extern：骨架品牌，待官方一手件）

