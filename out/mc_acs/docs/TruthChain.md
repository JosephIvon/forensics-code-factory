# docs/TruthChain.md — P/Invoke 真值链审计

> 数据源：mcopilot 仓库 `knowledge/sdks/{dmc,gts,zmotion}.json`（生成时实时读取，非复制）
> 根目录：C:\Users\admin\.minimax\sessions\mvs_838bec01bb00476594f48711e6b172d0\workspace\mcopilot　|　生成时间：2026-10-20 11:35:37

**三根轴**：源 = verified（这条结论从哪来）｜核对 = check（验到哪一步：manual 人工 / hardware 真机 / auto 机器比对 / none 未核对）｜提取 = extraction（签名对是否等于调用对；judgment = 有语义陷阱，必须人签字）。

**纪律（R2）**：judgment 条目必须 manual/hardware 签字才能当事实用；`check=none` 的条目生成代码里已标 TODO[需核对厂商手册]。
**接缝说明**：本工厂的知识库不另设真值副本 —— mcopilot 侧 `scripts/annotate_provenance.py --check` 复核后，重新生成本套件即自动继承最新口径。

## ACS SPiiPlus (SPiiPlusNET)

- 数据源：mcopilot 知识库无 ACS SPiiPlus (SPiiPlusNET) 的绑定口径
- P/Invoke 0 条：钉扎 0 / 未收录(降级) 0 / 口径不一致 0；核对进度 manual 0 / hardware 0 / auto 0 / none 0

（本品牌无 P/Invoke extern：托管库模式，引用官方 .NET DLL）

