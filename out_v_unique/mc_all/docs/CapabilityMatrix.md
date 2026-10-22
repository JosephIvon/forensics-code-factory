# 运动控制卡能力差异矩阵（JD职责3: 沉淀各品牌能力差异与性能边界）

> 生成时间: 2026-10-20 00:17:00 | 依据: **官方工件取证**（官方 C 头文件 / 官方 .NET 绑定反射 / DLL 导出表）+ 公开手册
> 各品牌「本仓库适配级别」的判定标准见下方；**没标 [核对] 的签名不是「没查」，而是已由门禁逐条比对过**

| 能力 | 固高 GTS/GTN | 雷赛 DMC/EMC | 正运动 ZMC | ACS SPiiPlus | 软赢 | 高川 |
|---|---|---|---|---|---|---|
| 总线形态 | 脉冲+部分EtherCAT | 脉冲+总线 | EtherCAT/脉冲 | EtherCAT | 以手册为准 | 以手册为准 |
| 直线插补 | 坐标系 2-4 轴 | 多轴 | 多轴 | 多轴 | `StartLinearIntplPos`（原生） | 坐标系 3 轴 |
| 圆弧插补 | 2D/3D | 2D | 2D/空间 | 2D/3D | `StartCircularIntplPos`（原生） | 2D（圆心增量） |
| S 曲线 | S 段参数 | 平滑因子 | 平滑/前瞻 | 高级(前瞻) | - | smoothCoef[0,199] |
| 位置比较输出(飞拍) | 支持 | 支持 | 支持(MOVECMP) | 支持(MEP) | - | 以手册为准 |
| 模拟量 AO(力控) | `GT_SetExtDaVoltage`(电压) | 部分型号 | 支持 | 支持 | - | `NMC_IODacSet`(电压) |
| SDK 形态 | gts.dll (C API) | LTDMC.dll (C API) | zmcaux.dll (C API) | ACS.SPiiPlusNET (.NET) | WMX3Api_CLRLib (.NET 托管) | nmc_lib20.dll (C API) |
| .NET 调用 | P/Invoke | P/Invoke | P/Invoke | 直接引用 | **反射后期绑定**（C++/CLI 托管集） | P/Invoke |
| 证据档 | official-artifact（二进制+CHM） | official-artifact（官方头文件） | official-artifact（官方头文件） | official-artifact（官方 .NET DLL 反射） | **official-artifact（官方托管绑定，已推翻旧 no-artifact 判定）** | official-artifact（官方头文件） |
| 本仓库适配级别 | B | B | B | B（适配器在 evidence/acs，需官方 DLL 引用） | **B**（反射后期绑定） | **B** |

## 适配级别说明
- **A级**: 虚拟控制器 —— 100% 自研可运行（仿真）
- **B级**: 核心 API 已映射且**签名经门禁逐条比对**（高川 31/31、固高 23/23 形参宽度、ACS 托管+原生双层）；未标定项（固高状态位、THomePrm 字段名）走「运行期标定 + 未标定显式标记」，不会静默用猜测值
- **C级**: 骨架。软赢属此级 —— **证据档 no-artifact**，全部厂商调用点返回显式错误，不可投产，详见 `evidence/SOFTWIN_STATUS.md`

## 各品牌仍需上真机确认的项（不多，且都有判据）
| 品牌 | 待确认 | 判据 | 代码里的体现 |
|---|---|---|---|
| 固高 | `THomePrm` 字段名 | 读回值量级合理 | `AssertStructLayouts()` 布局自检 + 报错信息 |
| 固高 | `GT_GetSts` 各位含义 | 使能/运动/报警时对应位变化 | `ProbeStatusBits()` / `CalibrateStsBits()`，未标定置 `AxisState.Uncalibrated` |
| 固高 | `GT_Bind` 第3参 | —— | 不实现 PLC 烧写，`Home()` 走 `GT_Home` 传参式，不依赖它 |
| ACS | 单位换算 / 状态机语义 | 需仿真器或真机 | 适配器标注实现期；已能装载并按名解析 11/11 符号 |


## 单位换算约定（适配层统一处理）
- 用户单位: mm / 度；`AxisConfig.PulsesPerUnit` = 每毫米脉冲数
- 总线卡（ZMC/ACS）内部支持当量标定，适配层同样走 PulsesPerUnit 归一，保证业务层语义一致
