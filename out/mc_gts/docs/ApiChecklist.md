# P/Invoke 签名核对清单（上真机前必做）

> 生成时间: 2026-10-20 09:12:01
> **同源声明（重要）**: 本清单由生成器内置知识库派生，与适配器代码同源。
> 它只能索引"去哪里核对"，不能证明签名正确——签名若有错，错误会被原样列进清单。
> 唯一有效依据是厂商官方手册/头文件（外部信息源），须逐条比对（含函数名、参数个数/类型/方向、轴号基数、IO 极性）。
> 比对工时是上真机工作的主体，请按实际手册篇幅估算，不要以本清单条数推断"半天即可"。

> **核对记录（verified_against）**: "核对记录"列由工程师填写，格式 `手册章节@手册版本 结论 日期`（例: `§4.2@LTDMC-2.11 一致 2026-10-20`；不一致须在"结论"写差异并停用对应方法）。
> **k=0 规则**: 只要"已核对 k / 总 n"的 k < n，本套件状态为**未核对**，不得标记可用/交付/上真机。
> **k 的口径**: k 只统计"函数名/参数表已在厂商官方真值（PE导出表 + 官方C#绑定/C头文件）里核对到"的条目；
> 标注「未实现」的条目（官方真值里没有等价函数，已从代码里删除该臆造声明）计入总 n、不计入 k。
> **持久化警告**: 重新生成会整表覆盖（核对记录随之下丢失）——核对前先把本文件提交版本库或移出生成目录，重生成后回填。

## 固高 GTS/GTN 系列 (gts.dll) —— 已核对 14 / 15

| API | 用途 | 核对要点 | 核对记录(章节@rev 结论 日期) |
|---|---|---|---|
| `GT_Open(channel, param)` | 打开卡 | 官方原型 GT_Open(short channel,short param)（gts.csv:326 / gts_hdr.csv:367）；第一参是通道(卡)号，第二参 param 按卡型取值。变体口径：这是【单卡库】签名；多卡库 gts.dll 的 GT_Open 是 (short cardNum, short channel, short param)——DLL 同名、导出名相同，混用会参数错位 | （待填） |
| `GT_Close()` | 关闭卡 | 官方原型 GT_Close()（gts.csv:79 / gts_hdr.csv:85） | （待填） |
| `GT_Reset()` | 复位控制器 | 官方原型 GT_Reset()（gts.csv:356 / gts_hdr.csv:398）；打开后必须复位一次 | （待填） |
| `GT_LoadConfig(pFile)` | 加载GTC文件配置 | 官方原型 GT_LoadConfig(string pFile)（gts.csv:322 / gts_hdr.csv:360）；配置文件由固高配置工具生成 | （待填） |
| `GT_AxisOn(axis) / GT_AxisOff(axis)` | 使能/下使能 | 官方原型 GT_AxisOn(short axis) / GT_AxisOff(short axis)（gts.csv:42 / gts_hdr.csv:45）；轴号1-based | （待填） |
| `GT_ClrSts(axis, count)` | 清报警 | 官方原型 GT_ClrSts(short axis,short count)（gts.csv:81 / gts_hdr.csv:87）；官方是两参，count 传 1 | （待填） |
| `GT_PrfTrap + GT_SetTrapPrm` | 梯形/S曲线点位 | 官方原型 GT_SetTrapPrm(short profile,ref TTrapPrm pPrm)（gts.csv:454 / gts_hdr.csv:513）；结构体 TTrapPrm 字段序 acc/dec/velStart/smoothTime(short)（gts_structs.csv） | （待填） |
| `GT_SetPos/GT_SetVel + GT_Update(mask)` | 设目标/速度并触发 | 官方原型 GT_SetPos(short profile,int pos) / GT_SetVel(short profile,double vel) / GT_Update(int mask)（gts.csv:439/459/470）；Update掩码bit=轴号-1 | （待填） |
| `GT_PrfJog + GT_SetJogPrm` | Jog | 官方原型 GT_SetJogPrm(short profile,ref TJogPrm pPrm)（gts.csv:422 / gts_hdr.csv:470）；TJogPrm 官方只有 acc/dec/smooth 三个字段，Jog 速度必须走 GT_SetVel（速度符号即方向） | （待填） |
| `GT_Stop(mask, option)` | 停止 | 官方原型 GT_Stop(int mask,int option)（gts.csv:465 / gts_hdr.csv:527）；option 0=减速 1=急停 | （待填） |
| `GT_GetStandardHomePrm + GT_ExecuteStandardHome` | 回零 | 官方原型 GT_GetStandardHomePrm(short axis,out TStandardHomePrm) / GT_ExecuteStandardHome(short axis,ref TStandardHomePrm)（gts.csv:241/119 / gts_hdr.csv:264/128）；官方 mode 宏全表在 gts_hdr_macros.csv:163-171：HOME_MODE_LIMIT=10 / HOME_MODE_LIMIT_HOME=11 / HOME_MODE_LIMIT_INDEX=12 / HOME_MODE_LIMIT_HOME_INDEX=13 / HOME_MODE_HOME=20 / HOME_MODE_HOME_INDEX=22 / HOME_MODE_INDEX=30 / HOME_MODE_FORCED_HOME=40 / HOME_MODE_FORCED_HOME_INDEX=41。本套件只映射其中 4 个（HomeMode 枚举只有 4 值）—— 需限位+原点+索引组合回零（11/12/13）或强制回零（40/41）时，官方有这个 mode，本套件没暴露，要自己扩 HomeMode 枚举再加 case | （待填） |
| `GT_GetSts(axis,...)` | 轴状态 | 官方原型 GT_GetSts(short axis,out int pSts,short count,out uint pClock)（gts.csv:244 / gts_hdr.csv:267）；使能/运动/报警/限位位定义按手册bit表核对 | （待填） |
| `GT_GetPrfPos/GT_GetEncPos` | 指令/反馈位置 | 官方原型 GT_GetPrfPos(short profile,out double pValue,short count,out uint pClock) / GT_GetEncPos(short encoder,...)（gts.csv:230/186）；单位脉冲 | （待填） |
| `GT_GetDi(diType) / GT_SetDoBit(doType,index,value)` | IO | 官方原型 GT_GetDi(short diType,out int pValue) / GT_SetDoBit(short doType,short doIndex,short value)（gts.csv:180/396）；官方宏表里有候选值 MC_GPI=4 / MC_GPO=12（gts_hdr_macros.csv:17,22），但真值未标注其与 diType/doType 的对应关系，故代码保留[核对]标记取值 1，待手册确认 | （待填） |
| `(未实现) 模拟量 AI/AO` | 力控模拟量 | 官方 C# 绑定与 PE 导出表均无 GT_GetAi/GT_SetAo，已删除该臆造声明；固高模拟量是通道型接口 GT_GetAdc / GT_SetDac / GT_PtAo（gts.csv:129/393/336），值域与单位换算须按手册确认后再接入 | （待填） |

## 总进度: 已核对 14 / 15（k=已对官方真值核对的条数；未实现项计入 n 不计入 k——见头部 k=0 规则）

## 通用核对流程（每品牌）

1. 用厂商 Demo 软件手动走一遍: 使能 -> 回零 -> 低速点位 -> 停止
2. 按本表逐行核对签名（参数个数/类型/方向），**当场填写核对记录列**并更新 k/n
3. 运行本工程 `bin/GlueDemo.exe`，观察 `[OK]` 日志与实际轴动作
4. 低速（<10mm/s）验证方向与当量，再逐步提速
5. 验证急停与限位后再进入工艺联调
