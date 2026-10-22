# docs/TruthChain.md — P/Invoke 真值链审计

> 数据源：mcopilot 仓库 `knowledge/sdks/{dmc,gts,zmotion}.json`（生成时实时读取，非复制）
> 根目录：D:\workspace\mcopilot　|　生成时间：2026-10-22 12:10:22

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

