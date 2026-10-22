# 技术债登记（截至 2026-10-21）

> 本文件只登记**仍然存在的债**。已还清的不在这里。
> 判定口径：能离线定案的债必须还清；只有「客观上必须上真机 / 需厂商资料」的才允许留，且必须写明判据。
>
> **本项目自身出过的错误**不记在这里，见 [FAILURE_MODES.md](../../evidence/FAILURE_MODES.md)
> （27 条，含分类法与分布统计；门禁 `evidence/verify_failure_modes.py` 保证「每条已修复的错都挂了防线」）。

## 一、本轮还清的

| 债 | 状态 | 证据 |
|---|---|---|
| **软赢被误判为 `no-artifact`** | ✅ **推翻**并实现 | 官方 10 个 `WMX3ApiCLR` 托管程序集确实存在。适配器已按官方绑定实现（反射后期绑定），反射层测试 ALL_PASS。详见 [SOFTWIN_STATUS.md](../../evidence/SOFTWIN_STATUS.md) |
| **ACS 端口用错（7010）** | ✅ 修正为 701 | 官方 [ACSC.h:118](../../evidence/acs_francdoc/ACSC.h#L118)：`ACSC_SOCKET_STREAM_PORT 701`。此前 7010 在 ACS 全部文档中查无此端口 |
| 高川适配器是空壳（`[SDK-STUB]`） | ✅ B 级 | 按官方 `mtn_lib20.h` 重写：`NMC_MtSetSvOn/Off`、`THomeSetting` 回零、真实状态位、`NMC_IOAdcGet`/`IODacSet` 电压版 AI/AO、坐标系插补。签名 31/31 |
| 固高 `GT_Bind` 第 3 参「数据页」 | ✅ **推翻**（无证据的推测） | 官方 CHM「运动程序编译器」章节证明是「控制器线程 ↔ C 函数」绑定，且本项目不实现 PLC 方案 → 判为**明确不适用** |
| 固高 `THomePrm` 字段名 | ✅ 大部分定案 | 官方 CHM「回零模块」给出参数集：速度 / 加减速度 / 原点偏置 / 回零方向 / Index 计数值。仅剩 2 个 short 位语义待真机 |
| 固高状态位写死猜的值 | ✅ 运行期标定 | `ProbeStatusBits()` / `CalibrateStsBits()`；未标定置 `AxisState.Uncalibrated` |
| 固高 `THomePrm` 结构体无人验 | ✅ 离线自检 | `AssertStructLayouts()`（总宽 + 偏移 + 跨度 + 值往返），负向 4/4 |
| SoftWin 编译不过（7 处） | ✅ 修复 | 全库 `csc` 编译通过 |
| `gen_load_plan.py` 无负向测试 | ✅ 5/5 | 并抓出 `EntryPoint` 真实 bug |
| 单位换算散落各处、无门禁 | ✅ 可证伪 | 新增 [Core/Units.cs](../src/Core/Units.cs) + [verify_units.py](../../evidence/verify_units.py) + 负向 7/7；抓出 1000 倍速度错、`dec` 误用 `Acc`、静默截断 |

## 二、仍然存在

### D1 · 单位换算 —— 已可证伪，6 家口径全部由官方一手制品定案（本轮最高风险项，已还清）

这是本项目**最高的静默错误源**：换算错了不崩、不抛异常，只会让轴以错误速度走偏，肉眼与异常都发现不了。

**已做的**：
- 抽出单一真值层 [Core/Units.cs](../src/Core/Units.cs)，位置/速度/加速度换算集中成纯函数
- 每家品牌的口径用 `BrandUnits` 整类封装、**构造后不可变**，杜绝运行期被改
- 目标位置越界**抛异常**而非静默截断（静默截断 = 轴少走一大截且无告警）
- 门禁 [verify_units.py](../../evidence/verify_units.py)：从官方头文件抽单位真值 + Python 独立闭式重算比对 C# 实现
- 负向测试 [neg_test_units.py](../../evidence/neg_test_units.py) **14/14 拦截**

**本轮因此抓出的真实错误**：
| 错误 | 后果 |
|---|---|
| **把正运动/软赢当成「脉冲/毫秒」，实际是「用户单位/秒」** | 系数差 **10000 倍**，不崩不抛 |
| `RateFactor` 里 `PerMsToPerSec(ppu)/1000` 两次 1000 抵消成 `ppu` | 速度大 1000 倍 |
| `AccFactor` 对所有脉冲类一律 `ppu/1000`，雷赛实为脉冲/秒 | 加速度小 1000 倍（轴加速极缓） |
| 雷赛 `tacc/tdec` 按毫秒算 `1000.0/acc`，官方手册明写「单位：s」 | 加速时间小 1000 倍，轴猛冲 |
| SoftWin `SetProfile` 的 `dec` 误用 `p.Acc` | 减速度按加速度下发 |
| 多处 `(int)Math.Round(mm * ppu)` 静默截断 | 越界时轴少走一大截且无告警 |
| 按 UTF-8 读 GBK 的 `LTDMC.h` | 搜「脉冲」命中 0 条 → 误判「官方没写单位」 |

**速率口径 3 种、加速度口径另算 3 种（官方一手制品逐条定案，任意两家混用都会错）**：

| 速率口径 | 速率系数 | 加速度系数 | 品牌 | 官方锚点 |
|---|---:|---:|---|---|
| 脉冲/毫秒 | × ppu/1000 | × ppu/1000 | 固高 | MCT2008 `Language.ini` L649「速度(pulse/ms)」L650「加速度(pulse/ms^2)」 |
| | | | 高川 | `mtn_lib20.h` L366「单位是 脉冲/ms」L534「脉冲/ms.ms」 |
| 脉冲/秒 | × ppu | × ppu | 雷赛 | 官方手册 DMC5X10 V2.0 §9.8「Max_Vel pulse/s」「Tacc 单位：s」「acc pulse/(s²)」 |
| 用户单位/秒 | × 1 | × 1 | 正运动 | 官方绑定 `Zmcaux.cs` L1435「单位为units/s」 |
| | | | 软赢 | 官方 doxygen「Unit: user unit / second」「user unit / second²」 |

> 固高/高川 与 正运动/软赢 之间差 **10000 倍**。
> **加速度口径必须跟着速度口径走**，不能想当然套用 —— 本轮就栽在这。
> 门禁 `verify_units.py` 从 4 份官方一手制品抽真值，逐家锁定两个系数；
> 负向测试 **14/14**（含 4 条专打分类错误与口径对调）。

**仍缺的**：无。6 家速率/加速度/时间口径全部定案，**全部 `NeedsVendorConfirm=false`**。
反向排除：三本雷赛官方手册合计 761 页检索「脉冲/毫秒」「pulse/ms」命中 **0 次**。
`_unit` 后缀版（脉冲当量/秒）与脉冲版量纲差一个脉冲当量，**必须认准选的是哪一套**——
本适配器全程用无后缀脉冲版，脉冲当量只在业务层参与换算，不下发控制器
（官方例 8.2：「仍希望以脉冲为单位，可以将脉冲当量值设置为 1」）。

### D2 · ACS —— 官方仿真器可得，只是缺账号

| 项 | 说明 |
|---|---|
| **结论** | **不要自写仿真器。** 官方 SPiiPlus Controller Simulator 免费、随 ADK Suite 分发，最多 128 轴 + 64 线程 |
| **自写为什么不可行** | ACS 的 TCP 报文是**闭源二进制**，官方从未公开 opcode 表 / 报文头 / 粘包规则，也没有 Wireshark dissector。自写等于逆向闭源 `ACSCL_x64.dll` 的网络层，无先例、无文档、无社区支持 |
| **自写的产出价值也低** | 即使成功，验出的只是「我写的模拟器符合我写的假设」，无法作为真实语义证据 |
| **门槛** | 需 ACS **企业邮箱**账号（申请表明确写「If you do NOT have a company email address, DO NOT fill this form」）。此前 `.com/.cn` 曾返回「暂不开放注册」，需备 Plan B：走 PI / motionusa / pikorea 经销商索取 |
| **工作量** | 账号审核 1–3 工作日（人工等待）+ 安装配置 2–4 小时 + 适配器联调 0.5–1 天 |
| **怎么连** | `acsc_OpenCommEthernetTCP(localIP, 701)` 即可，**无需改代码**（官方明说连本机 IP 就连仿真器）。另有专用入口 `acsc_OpenCommSimulator()`（`ACSC.h:2919`） |
| **已做到** | `ACSCL_x64.dll` 实测装载成功、`AddDllDirectory` 修复、11/11 符号按名解析、`acsc_OpenCommEthernetTCP` 真实调用并返回 |
| **适配器在哪** | [evidence/acs/AcsSpiiPlusController_v2.cs](../../evidence/acs/AcsSpiiPlusController_v2.cs)（编译需官方 `ACS.SPiiPlusNET.dll` 引用，故不在全品牌套件内） |

### D3 · 软赢 · 模拟量输出(AO) 函数名

- 输入侧官方为 `GetInAnalogData*`（已实现 `GetAi`）；**输出侧函数名未在公开文档中逐字确认**。
- 代码按显式错误返回（`[PENDING-VENDOR]`），**未猜**。拿到手册后一行补全。
- 同类待确认：`HomeParam` 字段名、`DeviceType` 枚举嵌套层级（代码两种候选都试）。

### D4 · 固高 · `THomePrm` 的 2 个 short 位语义

- 布局 / 对齐 / 宽度 / **字段名**已离线验掉。
- 仅剩：「取消 Home 回零过程」这个布尔量落在哪个 short 位、`edge` 的位含义 —— 二进制无语义信息，需真机。
- 判据见 [GOOGOL_STATUS.md §三-1](../../evidence/GOOGOL_STATUS.md)，上机 10 分钟可定。

### D5 · 固高 · `GT_GetSts` 各位含义

- 已改成运行期标定（`ProbeStatusBits` / `CalibrateStsBits`），未标定时置 `AxisState.Uncalibrated`，
  **不会把猜测值当真值用**。判据：使能前后只有使能位变；点动时只有运动位置 1。

### D6 · 雷赛 / 正运动 的型号相关分支

- 雷赛使能：`dmc_set_sevon_enable` 仅总线卡有，脉冲卡是硬线使能 —— 需按实际型号选分支。
- 固高：`gts.cs` 有单卡版/多卡版两套签名（`GT_Open` 参数不同、`TCrdPrm.originPos` 类型 long/int），
  本项目按单卡版声明 —— 若实际 DLL 是多卡版会崩溃。上机第一件事是核对。
- 判据已写在代码 `[核对]` 处与 ApiChecklist；按手册 + 实际卡型号一行内解决。

### D7 · S 曲线平滑参数（JerkRatio）—— 6 家量纲已定案，比例尺未标定，雷赛无法定案

`MoveParams.JerkRatio`（0~1，0 = 梯形）是 JD 职责 4「S 曲线加减速与轨迹平滑」的唯一入口。
经两轮取证（固高 `smoothTime`、六家 jerk 全量），它的问题分**三层**：

| 层 | 状态 | 涉及 |
|---|---|---|
| ① **量纲与区间** | ✅ 已定案（5/6 家） | 固高、高川、正运动、软赢 |
| ② **比例尺**（1 单位 = 多少时间） | ⚠ 部分定案 | 软赢**无需比例尺**；固高/高川/正运动待标定 |
| ③ **是否实现** | ✅ 4 家已接，1 家明确不支持 | 雷赛 `no-artifact` |

#### ① 量纲与区间：六家共 **4 种不同量纲**（任意两家直接换算都会错）

| 品牌 | 字段 | 量纲 | 范围 | `JerkRatio` 映射 | 证据层级 |
|---|---|---|---|---|---|
| 固高 | `TTrapPrm.smoothTime` | **时间（ms）** | `[0,50]` | `× 50` 线性 | `third_party_report` |
| 高川 | `smoothCoef` | **无量纲系数** | `[0,199]` | `× 199` 线性 | `official-artifact` |
| 正运动 | `ZAux_Direct_SetSramp` | **时间（ms）** | `[0,250]` | `× 250` 线性 | `official-artifact` |
| 软赢 | `Profile.jerkAccRatio` | **无量纲比率** | `[0,1]` | **直传，无需换算** | `official-artifact` |
| 雷赛 | `dmc_set_profile_extern.Ajerk` | **未知** | 未知 | **不接** | `no-artifact` |

证据链与逐字原文见 [JERK_UNITS_EVIDENCE.md](../../evidence/JERK_UNITS_EVIDENCE.md)。

> ★ **「系数」与「比率」虽都是无量纲，语义不同、数值不可互换**：
> 系数是"平滑强度"（越大越平滑，无上界语义）；比率是"加加速度变化段占加速段的时间比例"
> （0=梯形，1=S 曲线，官方明确定义两端含义）。
>
> ★ **固高 `smoothTime` 与正运动 `SRAMP` 是同一类量**（时间/毫秒），仅满量程不同（50 vs 250）。
> 这是六家里第二类可互参的量。

#### ② 比例尺：只有软赢不需要假设

- **软赢（唯一无需比例尺）**：官方 `jerkAccRatio` 定义为
  「the ratio of the time that the acceleration changes and the total acceleration time」，
  范围 `[0,1]`，且**官方明确定义两端**：0 = 等同 Trapezoidal，1 = 等同 SCurve。
  这与本项目 `JerkRatio` 的语义**逐字对齐** → 直传，不做任何换算。
- 固高 / 高川 / 正运动：官方给了区间上界，但**未定义「1ms（或 1 个系数单位）平滑」对应多大的加减速缓和程度**。
  两端（0 与满量程）有依据，**中间值是本项目自选的线性插值假设**。
- 需真机标定（阶段 3）。这与 D1 性质不同：D1 是**量纲错**（必然错），这里是**刻度未标定**（可能对）。

#### ③ 4 家已接，1 家明确不支持

| 品牌 | 状态 | 实现方式 |
|---|---|---|
| 固高 / 高川 | ✅ 已接 | `SmoothMs` / `Smooth` |
| **正运动** | ✅ **本轮接入** | `ApplySramp(axis, JerkRatio)` → `ZAux_Direct_SetSramp`，满量程 250ms |
| **软赢** | ✅ **本轮接入** | `jerkAccRatio`/`jerkDecRatio` 直传 + `type=ProfileType.JerkRatio` |
| 雷赛 | ❌ 明确不支持 | `dmc_set_profile_extern` 在 3400+ 页官方手册中**零文档**，不猜着下发 |

**软赢接入修掉一个隐藏问题**：原代码 `SetIfExists(prof, "type", 0)` 把曲线类型**硬编码为梯形**，
即使填了 jerk 字段也不生效 —— 这正是 JerkRatio 被静默丢弃的直接原因。

**正运动的互斥约束**：官方手册明确「设置 JERK 后会**替换** SRAMP」（`SRAMP = ACCEL*1000/JERK`）。
本项目走 SRAMP 路线，**因此绝不能同时下发 JERK**。已在代码注释中标注。

> **软赢仍有一处未取证**：doxygen 描述 C++ 原生头文件，**未描述 C++/CLI 的 CLR 包装**。
> `jerkAccRatio` 字段与 `ProfileType` 枚举在托管程序集里是否暴露、嵌套层级是否正确，
> 需反射 dump 确认。故代码里枚举名**不猜测**——按字段实际类型反查，查不到就退回梯形并置
> `JerkProfileApplied=false`，宁可保守不可静默失效。

---

## 三、修复方案

按「**离线可做 → 需厂商资料 → 需真机**」分三阶段，每阶段都有独立判据与止损点。

### 阶段 1（✅ 已完成）：把「静默丢弃」变成「显式声明」

`IMotionController` 新增 `SupportsJerkProfile`；软赢另加 `JerkProfileApplied`
（反射拿不到字段/枚举时为 `false`，让调用方知道平滑未生效）。
门禁 [verify_jerk_support.py](../../evidence/verify_jerk_support.py) 做反向检查 ——
**声明为 true 的必须真能在代码里找到映射触点**，防「空声明」；
四家逐一注入验证（4/4），且注入在临时目录沙箱内进行（FM-038 教训）。

### 阶段 2（✅ 已完成，2026-10-21）：逐字确认 jerk 字段 —— 结论是「4 种量纲 + 1 家无法定案」

原计划的判据是「三家的单位全部落到时间量且能互相换算」。**实际结果推翻了这个假设**：

| 目标 | 结论 | 证据层级 |
|---|---|---|
| 雷赛 `Ajerk`/`Djerk` | **无法定案**。`dmc_set_profile_extern` 在 **9 份官方 PDF 共 3400+ 页中零出现**；官方 DMC5X10 手册里「加加速度/Jerk」仅出现在**错误码表**（5 处）。另发现 C 头文件写 `Acc/Dec`、VB6 绑定写 `Tacc/Tdec`，**同一函数两套命名**且无官方说明 | `no-artifact` |
| 正运动 | `ZAux_Direct_SetJerk` 单位 = **`units/s³`**（加加速度）。**但它未进 PC 函数手册**（714 页全文 0 命中，398 个 `ZAux_*` 函数名里无 jerk）→ 这解释了本地绑定注释为何只写「设置jerk」三字。改用 `ZAux_Direct_SetSramp`（**ms，0~250**），本地官方绑定 L1459 注释逐字「S曲线平滑时间设定值 MS」 | `official-artifact` |
| 软赢 | **4 个 jerk 字段**：`jerkAcc`/`jerkDec` = `user unit/second³`；`jerkAccRatio`/`jerkDecRatio` = **无量纲 0~1，0=梯形、1=S 曲线**（官方明确定义两端）。另有 `accTimeMilliseconds`/`decTimeMilliseconds` = ms | `official-artifact` |

**判据修正**：原判据「都落到时间量」是错的。正确判据是「**每家的量纲必须被独立记录，且互不套用**」。
实际落出 **4 种量纲**：时间(ms) / 无量纲系数 / 无量纲比率 / 加加速度。

**已落地**：正运动 `ApplySramp`、软赢 `jerkAccRatio` 直传 + 曲线类型切换。
**止损**：雷赛维持阶段 1 的「显式不支持」—— 不猜着下发，也不因为「头文件里有这个符号」就认为可用。
待原厂工单书面确认三项（`Ajerk` 单位与范围 / `Acc` 与 `Tacc` 哪个对 / `extern`+`plan_mode` 配套用法）。

### 阶段 3（需真机）：标定比例尺

**前置**：阶段 1、2 完成，且至少有一家控制器能实际动。

**做法**：同一根轴、同一位移、同一速度档，`JerkRatio` 取 0 / 0.25 / 0.5 / 0.75 / 1.0 五档，
每档记录：
- 加速段实际时长（`GetCommandPos` 时序）
- 跟随误差峰值（`GetFollowingError` 六家都已实现，是现成的免费反馈源）
- 到位时间

**判据**：五档数据能拟合出一条单调曲线，则线性插值假设成立，`JerkRatio × 满量程` 可保留；
若明显非线性或某档无差异，说明满量程映射不成立，改查表或改为分档开关。

**止损**：任何一档出现跟随误差超限或异响，立即停机并回退 `JerkRatio=0`（梯形，最保守）。

### 三阶段都不做的事

**不引入自学习/自调参**。理由见上文 D7 的 ② 与 ③：
没有地面真值时，自适应只是在幻觉上调参；且运动控制参数学错的代价是撞机，不是点错推荐。
比例尺标定是**一次性的物理测量**，不是持续学习 —— 别把它包装成 AI。

## 四、维护规则

1. **改适配器后必须跑** `python evidence/run_all_verify.py`（当前 30 项）。
2. **新增校验器必须配负向测试**，且负向测试要证明「注入的缺陷被抓到」，不是证明「脚本能跑」。
   —— 本轮三处「全绿」里有两处是这样被推翻的。
3. **离线能验的不许留给真机**。留给真机的必须在 `CapabilityMatrix.md` 的待确认表里写明判据。
4. **反射 / 后期绑定代码必须配桩程序集测试**。软赢的反射层测试一次抓出 6 个只会在真机上炸的 bug
   （枚举层级、UInt32→Int32、`0xFFFFFFFF` 溢出、漏写 `_open=true`、`ErrorCode` 判定恒假、`object[]`→`double[]`）。
5. **判定「厂商没有某类工件」前，必须先穷举厂商自有渠道**（官网文档站 / 官方 doxygen / 中文官网）。
   —— 软赢的 `no-artifact` 就是因为漏查厂商 doxygen 而误判，代价是整个适配器被降级成骨架。
   —— **也要联网核实**：「本地没有制品」≠「不存在制品」。固高 `smoothTime` 本地无据，
   联网一搜就有官方文档转述，本轮据此抓出量纲错 20~50 倍（FM-034、FM-036）。
6. **新增真值层后必须加反向 grep 门禁**。正向比对「系数算得对不对」抓不到「绕过真值层」
   —— FM-035 三处下发点直接对 `_ppu` 做算术，正向全绿而实际速度差 1000 倍，
   其中 `GT_Home` 回零速度大 1000 倍会直接撞限位开关。反向检查见
   `verify_units.py` 的 D 段：「适配器不得绕过 Units 层」。
7. **桩程序集必须能返回错误码**。全返回成功值的桩无法验证错误处理 ——
   `IsOk` 写成恒真也能全绿，这是比 bug 本身更危险的结构性盲区。
8. **门禁的「缺失」分支不许只打印不计数**。否则负向测试复制门禁到临时目录运行时，
   会因同一个无关原因让基准与全部注入一起「失败」，整套负向测试失去参照（FM-037）。
9. **单位/量纲类换算必须有官方锚点 + 独立闭式重算**。门禁要能从官方文档抽到真值原文，
   而不是只验证「自己和自己一致」—— 两边都写错同一个式子时，只有外部锚点能发现。
10. **踩坑后立刻写进 `evidence/failure_modes.json` 并挂 countermeasures**。
   门禁会拒绝「已修复却没挂防线」「未关闭却声称有对策」这两种腐化。
   挂不上对策的坑 = 目前机器发现不了，值得单独讨论要不要做工具，而不是记一句「已知问题」。
11. **改任何被审过的源码后必须更新 `review_registry.json` 的哈希**。
    哈希对不上会自动退回 `unreviewed` —— 这是设计意图，不是误报。
12. **门禁的反向自检一律不得写入工作区文件**。注入必须在临时目录的副本上做。
    —— FM-038：自检直接改真实源码 + 关自检的环境变量名拼错 → 约 500 个 python 进程
    无限套娃、同时覆写同一个 `.cs`，把高川适配器的两条语句截断，源码当场编译不过。
13. **改完源码必须跑 `csc` 编译，不能只靠 grep 关键词计数判断完好**。
    套娃进程破坏源码时，`smoothCoef` / `JerkRatio` 等关键词全都还在 ——
    断的是语句结构，只有编译器抓得到。
