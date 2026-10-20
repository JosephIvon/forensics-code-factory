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

## 正运动 ZMC 系列 (zauxdll.dll) —— 已核对 13 / 13

| API | 用途 | 核对要点 | 核对记录(章节@rev 结论 日期) |
|---|---|---|---|
| `ZAux_OpenEth(ip,&h) / ZAux_Close(h)` | 网口连接/断开 | 官方原型 ZAux_OpenEth(string ipaddr,out IntPtr phandle)（zmotion.csv:374 / zmotion_cpp.csv:394）/ ZAux_Close(IntPtr handle)（zmotion.csv:35）；控制器默认IP 192.168.0.11 | （待填） |
| `ZAux_Direct_SetUnits(h,axis,v)` | 当量(脉冲/unit) | 官方原型 ZAux_Direct_SetUnits(IntPtr handle,int iaxis,float fValue)（zmotion.csv:314 / zmotion_cpp.csv:324） | （待填） |
| `ZAux_Direct_SetSpeed/SetAccel/SetDecel` | 速度/加减速 | 官方原型 ZAux_Direct_SetSpeed(IntPtr,int,float)（zmotion.csv:308）/SetAccel（:254）/SetDecel（:268）；用户单位/s | （待填） |
| `ZAux_Direct_Single_MoveAbs / ZAux_Direct_Single_Move` | 单轴绝对/相对点位 | 官方原型 ZAux_Direct_Single_MoveAbs(IntPtr,int,float fdistance)（zmotion.csv:328）/ ZAux_Direct_Single_Move(IntPtr,int,float fdistance)（zmotion.csv:327）；相对移动用 Single_Move | （待填） |
| `ZAux_Direct_MoveAbs(h,n,axislist[],dpos[])` | 多轴直线插补 | 官方原型 ZAux_Direct_MoveAbs(IntPtr handle,int imaxaxises,int[] piAxislist,float[] pfDposlist)（zmotion.csv:194 / zmotion_cpp.csv:201） | （待填） |
| `ZAux_Direct_Single_Vmove(h,axis,dir)` | Jog | 官方原型 ZAux_Direct_Single_Vmove(IntPtr handle,int iaxis,int idir)（zmotion.csv:329 / zmotion_cpp.csv:341） | （待填） |
| `ZAux_Direct_Single_Datum(h,axis,imode)` | 回零 | 官方原型 ZAux_Direct_Single_Datum(IntPtr handle,int iaxis,int imode)（zmotion.csv:326 / zmotion_cpp.csv:336）；imode 按手册DATUM模式表 | （待填） |
| `ZAux_Direct_Single_Cancel(h,axis,imode)` | 停止 | 官方原型 ZAux_Direct_Single_Cancel(IntPtr handle,int iaxis,int imode)（zmotion.csv:325 / zmotion_cpp.csv:335）；imode 2=急停 | （待填） |
| `ZAux_Direct_GetMpos / ZAux_Direct_GetDpos` | 指令/反馈位置 | 官方原型 ZAux_Direct_GetMpos(IntPtr,int,ref float pfValue)（zmotion.csv:124）/ ZAux_Direct_GetDpos(IntPtr,int,ref float pfValue)（zmotion.csv:82）；单位受UNITS影响 | （待填） |
| `ZAux_Direct_GetIfIdle(h,axis,&v)` | 运动空闲判断 | 官方原型 ZAux_Direct_GetIfIdle(IntPtr handle,int iaxis,ref int piValue)（zmotion.csv:106 / zmotion_cpp.csv:110）；0=运动中 | （待填） |
| `ZAux_Direct_GetIn / ZAux_Direct_SetOp` | IN/OUT | 官方原型 ZAux_Direct_GetIn(IntPtr,int ionum,ref UInt32 piValue)（zmotion.csv:108）/ ZAux_Direct_SetOp(IntPtr,int ionum,UInt32 iValue)（zmotion.csv:297）；编号0起 | （待填） |
| `ZAux_Direct_GetAD / ZAux_Direct_SetDA` | ADC/DAC | 官方原型 ZAux_Direct_GetAD(IntPtr,int ionum,ref float pfValue)（zmotion.csv:59 / zmotion_cpp.csv:63）/ ZAux_Direct_SetDA(IntPtr,int ionum,float fValue)（zmotion.csv:265 / zmotion_cpp.csv:275）；量程换算按手册 | （待填） |
| `ZAux_Direct_SetAxisEnable(h,axis,iValue)` | 使能 | 官方原型 ZAux_Direct_SetAxisEnable(IntPtr handle,int iaxis,int iValue)（zmotion.csv:258 / zmotion_cpp.csv:268） | （待填） |

## 总进度: 已核对 13 / 13（k=已对官方真值核对的条数；未实现项计入 n 不计入 k——见头部 k=0 规则）

## 通用核对流程（每品牌）

1. 用厂商 Demo 软件手动走一遍: 使能 -> 回零 -> 低速点位 -> 停止
2. 按本表逐行核对签名（参数个数/类型/方向），**当场填写核对记录列**并更新 k/n
3. 运行本工程 `bin/GlueDemo.exe`，观察 `[OK]` 日志与实际轴动作
4. 低速（<10mm/s）验证方向与当量，再逐步提速
5. 验证急停与限位后再进入工艺联调
