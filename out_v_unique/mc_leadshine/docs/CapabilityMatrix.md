# 运动控制卡能力差异矩阵（JD职责3: 沉淀各品牌能力差异与性能边界）

> 生成时间: 2026-10-20 00:15:59 | 依据: 厂商公开手册常识整理 + 行业经验，**上真机前以官方最新手册为准**

| 能力 | 固高 GTS/GTN | 雷赛 DMC/EMC | 正运动 ZMC | ACS SPiiPlus | 软赢 | 高川 |
|---|---|---|---|---|---|---|
| 总线形态 | 脉冲+部分EtherCAT | 脉冲+总线 | EtherCAT/脉冲 | EtherCAT | 以手册为准 | 以手册为准 |
| 直线插补 | 坐标系 2-4 轴 | 多轴 | 多轴 | 多轴 | - | - |
| 圆弧插补 | 2D/3D | 2D | 2D/空间 | 2D/3D | - | - |
| S 曲线 | S 段参数 | 平滑因子 | 平滑/前瞻 | 高级(前瞻) | - | - |
| 位置比较输出(飞拍) | 支持 | 支持 | 支持(MOVECMP) | 支持(MEP) | - | - |
| 模拟量 AO(力控) | 部分型号 | 部分型号 | 支持 | 支持 | - | - |
| SDK 形态 | gts.dll (C API) | LTDMC.dll (C API) | zmcaux.dll (C API) | ACS.SPiiPlusNET (.NET) | - | - |
| .NET 调用 | P/Invoke | P/Invoke | P/Invoke | 直接引用 | - | - |
| 调试工具 | GTN Demo | 雷赛调试软件 | ZDevelop | ACS C++/SPiiPlus MMI | - | - |
| 本仓库适配级别 | B | B | B | B(需厂商DLL,不在全品牌套件) | C(骨架) | C(骨架) |

## 适配级别说明
- **A级**: 虚拟控制器 —— 100% 自研可运行（仿真）
- **B级**: 核心 API 已映射（Open/使能/回零/点位/IO/位置），插补给出调用结构；**SDK 签名来自生成器内置知识（未经厂商手册比对，可能有错）**，须按 ApiChecklist.md 的同源声明对照手册逐条核实。注意：全品牌套件不含 ACS（编译需官方 ACS.SPiiPlusNET.dll）；要用 ACS 请单独生成 ACS 套件并安装 ACS 软件
- **C级**: 骨架 + 完整 TODO 清单，用于二次开发起点

## 单位换算约定（适配层统一处理）
- 用户单位: mm / 度；`AxisConfig.PulsesPerUnit` = 每毫米脉冲数
- 总线卡（ZMC/ACS）内部支持当量标定，适配层同样走 PulsesPerUnit 归一，保证业务层语义一致
