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

## 雷赛 DMC/EMC 系列 (LTDMC) (LTDMC.dll) —— 已核对 12 / 13

| API | 用途 | 核对要点 | 核对记录(章节@rev 结论 日期) |
|---|---|---|---|
| `dmc_board_init()` | 初始化库并枚举卡 | 官方原型 dmc_board_init()（leisai.csv:4 / leisai_cpp.csv:32）；返回卡数，卡信息用官方 dmc_get_CardInfList(leisai.csv:10) | （待填） |
| `dmc_board_close()` | 关闭库 | 官方原型 dmc_board_close()（leisai.csv:6 / leisai_cpp.csv:30） | （待填） |
| `dmc_pmove(card,axis,dist,posi_mode)` | 点位运动 | 官方原型 dmc_pmove(UInt16 CardNo,UInt16 axis,Int32 Dist,UInt16 posi_mode)（leisai.csv:71 / leisai_cpp.csv:816）；posi_mode 0/1 绝对相对定义、脉冲单位 | （待填） |
| `dmc_set_profile(card,axis,...)` | 速度曲线 | 官方原型 dmc_set_profile(UInt16 CardNo,UInt16 axis,double Min_Vel,double Max_Vel,double Tacc,double Tdec,double stop_vel)（leisai.csv:61 / leisai_cpp.csv:1110）；参数序(min,max,tacc,tdec,stop) | （待填） |
| `dmc_stop(card,axis,stop_mode)` | 停止 | 官方原型 dmc_stop(UInt16 CardNo,UInt16 axis,UInt16 stop_mode)（leisai.csv:327 / leisai_cpp.csv:1236）；0=减速 1=立即 | （待填） |
| `dmc_check_done(card,axis)` | 运动完成判断 | 官方原型 dmc_check_done(UInt16 CardNo,UInt16 axis)（leisai.csv:321 / leisai_cpp.csv:61）；返回1=停止 | （待填） |
| `dmc_get_position(card,axis)` | 指令位置 | 官方C头文件 long dmc_get_position(WORD CardNo,WORD axis)（leisai_cpp.csv:552）；官方C#绑定返回 Int32（EMC系列开发头文件\C#\LTDMC.cs:891）——Windows LLP64 下 C 的 long 是 32 位，故 C# 侧必须用 int 而不是 long（用 long 会读到 RAX 未定义的高 32 位）；参数表 leisai.csv:314；单位脉冲 | （待填） |
| `dmc_get_encoder(card,axis)` | 反馈位置 | 官方C头文件 long dmc_get_encoder(WORD CardNo,WORD axis)（leisai_cpp.csv:406）；官方C#绑定返回 Int32（EMC系列开发头文件\C#\LTDMC.cs:859）——Windows LLP64 下 C 的 long 是 32 位，故 C# 侧必须用 int 而不是 long；参数表 leisai.csv:302；单位脉冲 | （待填） |
| `dmc_vmove(card,axis,dir) / dmc_change_speed(card,axis,vel,taccdec)` | Jog/变速 | 官方原型 dmc_vmove(UInt16 CardNo,UInt16 axis,UInt16 dir)（leisai.csv:81）/ dmc_change_speed(UInt16 CardNo,UInt16 axis,double Curr_Vel,double Taccdec)（leisai.csv:76）；change_speed 官方是四参 | （待填） |
| `dmc_set_homemode(...) + dmc_home_move(card,axis)` | 触发回零 | 官方原型 dmc_set_homemode(UInt16 CardNo,UInt16 axis,UInt16 home_dir,double vel,UInt16 mode,UInt16 EZ_count)（leisai.csv:115 / leisai_cpp.csv:1041）+ dmc_home_move(UInt16 CardNo,UInt16 axis)（leisai.csv:119）；mode/home_dir 编号按手册 | （待填） |
| `dmc_read_inbit(card,bitno) / dmc_write_outbit(card,bitno,on_off)` | 读DI/写DO | 官方原型 dmc_read_inbit(UInt16 CardNo,UInt16 bitno)（leisai.csv:259）/ dmc_write_outbit(UInt16 CardNo,UInt16 bitno,UInt16 on_off)（leisai.csv:260）；DI/DO 低有效极性按手册核对 | （待填） |
| `dmc_get_ad_input(card,ch) / dmc_set_da_output(card,ch,vout)` | AI/AO | 官方原型 dmc_get_ad_input(ushort CardNo,ushort channel,ref double Vout)（leisai.csv:442 / leisai_cpp.csv:319）/ dmc_set_da_output(UInt16 CardNo,UInt16 channel,double Vout)（leisai.csv:440 / leisai_cpp.csv:981）；输出参数官方即电压(Vout)，不再做码值换算 | （待填） |
| `(未实现) 总线卡软件使能` | 使能 | 官方 C# 绑定/C 头文件/PE 导出表均无 dmc_set_servo 或任何软件使能声明，已删除该臆造声明；雷赛脉冲卡为硬线使能，总线卡使能接口须按型号手册确认后再接入 | （待填） |

## 总进度: 已核对 12 / 13（k=已对官方真值核对的条数；未实现项计入 n 不计入 k——见头部 k=0 规则）

## 通用核对流程（每品牌）

1. 用厂商 Demo 软件手动走一遍: 使能 -> 回零 -> 低速点位 -> 停止
2. 按本表逐行核对签名（参数个数/类型/方向），**当场填写核对记录列**并更新 k/n
3. 运行本工程 `bin/GlueDemo.exe`，观察 `[OK]` 日志与实际轴动作
4. 低速（<10mm/s）验证方向与当量，再逐步提速
5. 验证急停与限位后再进入工艺联调
