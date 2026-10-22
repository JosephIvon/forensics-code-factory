# docs/TruthChain.md — P/Invoke 真值链审计

> 数据源：mcopilot 仓库 `knowledge/sdks/{dmc,gts,zmotion}.json`（生成时实时读取，非复制）
> 知识库根目录：<mcopilot 仓根>（生成时按 MCOPILOT_ROOT 或同级兄弟目录发现，不写入本机绝对路径）　|　生成时间：2026-10-22 15:51:27

**三根轴**：源 = verified（这条结论从哪来）｜核对 = check（验到哪一步：manual 人工 / hardware 真机 / auto 机器比对 / none 未核对）｜提取 = extraction（签名对是否等于调用对；judgment = 有语义陷阱，必须人签字）。

**纪律（R2）**：judgment 条目必须 manual/hardware 签字才能当事实用；`check=none` 的条目生成代码里已标 TODO[需核对厂商手册]。
**接缝说明**：本工厂的知识库不另设真值副本 —— mcopilot 侧 `scripts/verify_traekb_citations.py` 复核后，重新生成本套件即自动继承最新口径。

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

