# docs/JointEval.md — 共享行为评测集对照（接缝 3）

> 数据源：mcopilot `knowledge/shared_eval.json`（生成时实时读取，非复制）
> 实测对照：**断言本体在工厂侧生成代码内**（`[接缝3·BEH-xx]` 行为断言自检 ✓）；mcopilot 侧的共享评测集（`knowledge/shared_eval.json`）与对照脚本（`scripts/joint_eval.py`）**均已入库** ✓，故本表给出 mcopilot 侧验证点 ✓。
> 本表是**注册表**——回答「哪些行为断言、各自由谁负责验」✓；**实测**对照（两侧本次真跑的结果，含 BEH-19 的 SPC 机器比对）在 mcopilot 仓 `out/joint_eval.md` ✓，由 `scripts/joint_eval.py` 生成 ✓。注册表说「谁负责」，实测表说「本次验没验」✓，两者分工不混。

**规则**：工厂端到端 PASS 只有落在共享评测集的断言上才算有背书——空白项（本套件应验未验）是整改清单，n/a 项（接口无此概念）是能力矩阵差异，不许算 FAIL。

## 已验

| 断言 | 域 | 工厂侧验证点 | mcopilot 侧验证点 |
|---|---|---|---|
| BEH-01 跟随误差必须非零（抓假反馈） | 反馈真伪 | gen/core.js → 生成 Program.cs 的 BehaviorSelfCheck 内 [BEH-01] | knowledge/templates/SelfCheck.cs.tmpl → BEH-01（生成 out/csharp/MotionSelfCheck.cs 真跑） |
| BEH-03 点动方向必须与速度符号一致 | 运动方向 | gen/core.js → 生成 Program.cs 的 BehaviorSelfCheck 内 [BEH-03] | knowledge/templates/SelfCheck.cs.tmpl → BEH-03 |
| BEH-04 指令位置与反馈位置单位一致 | 位置口径 | gen/core.js → 生成 Program.cs 的 BehaviorSelfCheck 内 [BEH-04] | knowledge/templates/SelfCheck.cs.tmpl → BEH-04 |
| BEH-07 非法脉冲当量必须被拒绝 | 错误语义 | gen/core.js → 生成 Program.cs 的 BehaviorSelfCheck 内 [BEH-07] | knowledge/templates/SelfCheck.cs.tmpl → BEH-07 |
| BEH-08 越界轴号必须被拒绝 | 错误语义 | gen/core.js → 生成 Program.cs 的 RunProcess 参数守卫自检内 [BEH-08] | knowledge/templates/SelfCheck.cs.tmpl → BEH-08 |
| BEH-09 停止后不再处于运动中 | 状态机 | gen/core.js → 生成 Program.cs 的 BehaviorSelfCheck 内 [BEH-09] | knowledge/templates/SelfCheck.cs.tmpl → BEH-09 |
| BEH-12 直线插补必须各轴到位 | 插补 | gen/core.js → 生成 Program.cs 的 RunProcess 内 [BEH-12] | knowledge/templates/SelfCheck.cs.tmpl → BEH-12 |
| BEH-13 圆弧插补：不支持时必须显式失败 | 插补 | gen/core.js → 生成 Program.cs 的 RunProcess 内 [BEH-13] | knowledge/templates/SelfCheck.cs.tmpl → BEH-13（mcopilot 仿真器不实现圆弧，测「显式失败」） |
| BEH-14 相对定位增量正确 | 位置口径 | gen/core.js → 生成 Program.cs 的 BehaviorSelfCheck 内 [BEH-14] | knowledge/templates/SelfCheck.cs.tmpl → BEH-14 |
| BEH-15 回零后位置归零 | 回零 | gen/core.js → 生成 Program.cs 的 BehaviorSelfCheck 内 [BEH-15] | knowledge/templates/SelfCheck.cs.tmpl → BEH-15 |
| BEH-16 多轴工艺流程端到端（走胶→圆弧→DI 互锁→力控） | 工艺流程 | gen/core.js → 生成 Program.cs 的 RunProcess 末尾 [BEH-16] | — |
| BEH-17 DI 互锁等待（超时必须显式失败） | IO 时序 | gen/core.js → 生成 Program.cs 的 RunProcess 内 [BEH-17] | — |
| BEH-18 AO 力控设定与反馈读取 | 力控 | gen/core.js → 生成 Program.cs 的 RunProcess 内 [BEH-18] | — |
| BEH-19 SPC 判定（Cpk>=1.33 合格） | 统计判定 | gen/core.js → 生成 Program.cs 的 SpcReport 内 [BEH-19] | scripts/joint_eval.py 从工厂 glue_report.csv 原始样本独立重算 Cpk 再比对（机器比对） |

## 接口无此概念

| 断言 | 域 | 工厂侧验证点 | mcopilot 侧验证点 |
|---|---|---|---|
| BEH-02 开环轴必须报告无真实反馈 | 反馈真伪 | IMotionController 无 HasRealFeedback 一类接口（gen/core.js 接口定义） | knowledge/templates/SelfCheck.cs.tmpl → BEH-02 |
| BEH-05 到位后停止原因必须为 0 | 停止语义 | IMotionController 无 StopReason 一类接口（gen/core.js 接口定义） | knowledge/templates/SelfCheck.cs.tmpl → BEH-05 |
| BEH-06 停止原因必须能翻译成人话 | 停止语义 | IMotionController 无停止原因码与文本接口（gen/core.js 接口定义） | knowledge/templates/SelfCheck.cs.tmpl → BEH-06 |
| BEH-10 比较点端口越界必须被拒绝 | IO 边界 | IMotionController 无硬件比较输出口接口（gen/core.js 接口定义） | knowledge/templates/SelfCheck.cs.tmpl → BEH-10 |
| BEH-11 锁存端口越界必须被拒绝 | IO 边界 | IMotionController 无位置锁存口接口（gen/core.js 接口定义） | knowledge/templates/SelfCheck.cs.tmpl → BEH-11 |

## 合法行为差异（不是 FAIL）

| 议题 | mcopilot | 工厂 | 说明 |
|---|---|---|---|
| DIV-01 报错风格 | 仿真器抛 .NET 标准异常（ArgumentOutOfRangeException） | 接口返回 MotionResult.Fail + Message（Virtual 层的读数方法另有抛异常，与 BEH-08 守卫一致） | 同一语义两种表达：mcopilot 侧用异常表达「非法」，工厂侧用返回码表达「非法」。**不是 FAIL**——但真机适配器必须二选一说清楚，不能两套混着来。 |
| DIV-02 圆弧支持 | 仿真器不实现圆弧，测「不支持时必须显式失败」 | Virtual 实现 MoveArc2D，测「圆弧到位」 | 两边的断言**方向不同但都对**：一方验「明确拒绝」，一方验「真能走」。合起来才覆盖「不支持」与「支持」两种世界。 |
| DIV-03 停止原因 | 有 StopReason 码 + 文本翻译（现场报警要给人看） | 接口无此概念（工厂接口只有 MoveResult 语义，无停止原因码） | 能力矩阵差异：不是谁对谁错，是接口面不同。真机接入若需要停止原因翻译，必须在适配层补，不能指望工厂接口有。 |

---

本评测集与 P/Invoke 真值链同一来源纪律：数据在 mcopilot 仓库，工厂生成时实时读取，绝不另设副本。
