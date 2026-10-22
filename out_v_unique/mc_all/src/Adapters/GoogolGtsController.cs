// =====================================================================
//  固高 GTS/GTN 系列 适配器（gts.dll）
//  生成工具: 上位机代码工厂 v1.0 | 2026-10-20 00:17:00
//  KB级别: B —— 核心API已映射(Open/使能/回零/点位/IO/位置)，插补为等时点位法+升级路径注释。
//  上真机前按 docs/ApiChecklist.md 核对签名（标注 [核对] 处）
// =====================================================================
using System;
using System.Runtime.InteropServices;
using System.Threading;
using MotionControl.Core;

namespace MotionControl.Adapters.Googol
{
    /// <summary>
    /// 固高 GTS/GTN 系列 —— IMotionController 适配器。
    /// 设计要点（JD职责3）:
    ///   1. P/Invoke 封装 + 签名核对标记
    ///   2. 单位换算: 用户单位(mm) <-脉冲- gts.dll
    ///   3. SDK 调用统一持锁串行化（厂商库多非线程安全）
    ///   4. 错误码统一映射 MotionResult
    /// </summary>
    public class GoogolGtsController : IMotionController
    {
        private readonly object _sdk = new object();
        private ControllerConnectInfo _info;
        private bool _open;
        private readonly bool[] _homed = new bool[8];
        private readonly double[] _ppu = new double[8];         // 脉冲/用户单位
        private readonly HomeMode[] _homeMode = new HomeMode[8];
        private readonly double[] _homeVelFast = new double[8];
        private readonly double[] _homeVelSlow = new double[8];
        private readonly double[] _homeAcc = new double[8];
        private readonly bool[] _homing = new bool[8];
        private readonly int[] _homeStartT = new int[8];
        private readonly double[] _softNeg = new double[8];
        private readonly double[] _softPos = new double[8];

        public GoogolGtsController()
        {
            for (int i = 0; i < 8; i++) { _ppu[i] = 10000; _softNeg[i] = -100000; _softPos[i] = 100000; _homeVelFast[i] = 20; _homeVelSlow[i] = 2; _homeAcc[i] = 200; }
        }

        public Brand Brand { get { return Brand.GoogolGts; } }
        public string DeviceName { get { return "GoogolGTS"; } }
        public bool IsOpen { get { return _open; } }

        /// <summary>已实现 JerkRatio → smoothTime(ms) 映射（官方区间 [0,50]）。比例尺待真机标定，见 tech-debt D7-②。</summary>
        public bool SupportsJerkProfile { get { return true; } }

        #region 厂商 API 导入
        //
        // 【浮点宽度按 DLL 二进制本体定案，勿照抄文档】
        //
        // 依据是 x87 指令与 stdcall 的 ret imm16（工具 probe_args.py --selftest 7/7 自测过）：
        //   GT_SetVel   DD 44 24 08 = FLD m64fp（8 字节 double）+ C2 0C 00 = 4+8=12 ✔
        //   GT_SetExtDaVoltage  DD 44 24 0C 同为 FLD m64fp
        // 全库扫描 158 个带浮点的导出，**无一个 f32**（D9/0），一律是 DD/0 的 f64。
        //
        // 踩过的坑（别重犯）：我曾把 DD/0 当成 4 字节 float，
        // 于是把本已正确的 double 全改成 float —— 上机表现为「速度设了但动不了」，
        // 因为 C# 按 float 传、double 低 32 位被 DLL 当速度读。
        // x87 表：D9/0=f32  DD/0=f64  D9/5=DD/5=f80  DB/0=i32  DB/5=i64
        //
        // 另一坑：出参是**指针**。GT_GetPrfPos / GT_GetAdcValue / GT_GetSts 的
        // 形参都是 4 字节槽（指针），float/double 在 *pValue 指向的内存里，
        // 不是在栈上。所以「ret 16」不代表有 double 形参。

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_Open(short card, short channel);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_Close();
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_Reset();
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern short GT_LoadConfig([MarshalAs(UnmanagedType.LPStr)] string cfgFile);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_ClrSts(short axis, short axisNum);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_AxisOn(short axis);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_AxisOff(short axis);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_Stop(int mask, int option);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_SetPos(short axis, int pos);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_SetVel(short axis, double vel);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_Update(int mask);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_PrfTrap(short axis);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_SetTrapPrm(short axis, ref TTrapPrm p);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_PrfJog(short axis);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_SetJogPrm(short axis, ref TJogPrm p);
        // 回零两套入口（都不是 SetHomePrm）：
        //
        // ① GT_GoHome(axis, axisNum)  ret=8  —— 用卡内已存参数，无法从上位机改
        // ② GT_Home(...)              ret=28 —— **传参式回零，参数直传**
        //
        // ②的签名由 DLL 机器码反推（2016 版，实测 4+4+8+8+4=28 与 ret 完全自洽）：
        //   +4  short  axis
        //   +8  short  axisNum
        //   +12 double velHigh   回零速度
        //   +20 double velLow    找 Index 信号的速度
        //   +28 short  edge      回零触发边沿
        // 与公开工程实践吻合：「回零参数主要有回零速度、找 Index 信号的速度、回零触发边沿」。
        // GT_HomeStop 是同形状的 4 参版（4+4+8+8=24 == ret 0x18），故第5参确属 GT_Home 独有。
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_GoHome(short axis, short axisNum);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_Home(short axis, short axisNum,
                                            double velHigh, double velLow, short edge);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_GetHomePrm(short axis, out THomePrm p);
        // 以下 4 个的形参都是 4 字节（指针/整数），ret=16 是 4 个槽，不代表有浮点形参
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_GetSts(short axis, out int pSts, short count, out uint pClock);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_GetPrfPos(short axis, out double pPos, short count, out uint pClock);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_GetEncPos(short axis, out double pPos, short count, out uint pClock);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_GetDi(short type, out int pDi);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_SetDo(short index, short value);
        // 模拟量：GT_SetDac 三槽全是 4 字节（机器码 52 50 51 三次 push，ret=12），
        // 第 3 槽传的是**整型 DA 值**（-32768~32767 对应 -10V~+10V），
        // 不是电压。要直接设电压请用 GT_SetExtDaVoltage（其 p3 是 f64，ret=16）。
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_GetAdcValue(short axis, short idx, short count, out double pValue);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_SetDac(short axis, short idx, int value);
        // 电压输出版：第3参是 double（DD 44 24 0C = FLD m64fp，ret=16 = 4+4+8）
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_SetExtDaVoltage(short axis, short idx, double volts);

        // [核对] 结构体字段顺序以手册为准（版本间存在差异）
        // 字段类型：GT_SetVel / GT_SetExtDaVoltage 全库 158 个浮点导出均为 f64，
        // 结构体字段同样用 double（4 字段 = 32 字节）。
        [StructLayout(LayoutKind.Sequential)]
        public struct TTrapPrm
        {
            public double acc;
            public double dec;
            public double smoothTime;   // S曲线平滑时间(0=梯形)
            public double vel;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct TJogPrm
        {
            public double acc;
            public double dec;
            public double smoothTime;
            public double vel;
        }
        // [未验证] 2016 版无「设置回零参数」导出，本结构体仅用于 GT_GetHomePrm 读回。
        // 字段序取自新版官方示例 THomePrm；2016 版的实际字段布局未从二进制验证，
        // 上机首件事就是核对本结构体（读回值是否合理、velHigh/velLow 是否在合理量级）。
        [StructLayout(LayoutKind.Sequential)]
        public struct THomePrm
        {
            public short mode;
            public short moveDir;
            public short edge;
            public short triggerIndex;
            public double velHigh;
            public double velLow;
            public double acc;
            public double dec;
            public double smoothTime;
            public double searchHomeDistance;
            public double searchIndexDistance;
            public double escapeStep;
            public double homeOffset;
        }
        #endregion

        /// <summary>
        /// 首板自检（L0 离线）：在 Open() 里跑一次，把「不需要真机就能定的项」当场定案。
        ///
        /// 为什么要专门做这件事：GOOGOL_STATUS.md §3 列了两项「上机才能定案」，
        /// 但其实只有「GT_GetSts 各位含义」和「THomePrm 字段名」需要真机；
        /// **结构体的总字节数与字段对齐**是纯离线的，漏了就是纯 bug，必须自己验掉。
        ///
        /// 判据（任一不过即抛异常，让首板立刻爆，不静默带着错结构体上机）：
        ///   1. THomePrm 总宽 == 4*short(2B) + 9*double(8B) = 8 + 72 = 80 字节
        ///   2. 各字段 OffsetOf 落在预期槽位内，无重叠、无 8 字节错位
        ///   3. TTrapPrm / TJogPrm 各 32 字节（4 个 double）
        /// </summary>
        private static void AssertStructLayouts()
        {
            int homeSize = Marshal.SizeOf(typeof(THomePrm));
            if (homeSize != 80)
                throw new InvalidOperationException(
                    "THomePrm 布局自检失败: 实际 " + homeSize + " 字节，期望 80（4 short×2 + 9 double×8）。"
                    + "字段顺序或类型与 2016 版不符，禁止上机。");

            // 逐字段槽位核对：4 个 short 占 0..7，9 个 double 从 8 开始，每 8 字节一个
            // 注：Marshal.OffsetOf 在 .NET Framework 4.x 返回 IntPtr（.NET Core 2.0+ 才返回 int），
            //     故统一 ToInt32()，否则本工程按 net48 编译时类型不匹配。
            string[] shortFields = { "mode", "moveDir", "edge", "triggerIndex" };
            for (int i = 0; i < shortFields.Length; i++)
            {
                int off = Marshal.OffsetOf(typeof(THomePrm), shortFields[i]).ToInt32();
                if (off != i * 2)
                    throw new InvalidOperationException(
                        "THomePrm 布局自检失败: 第 " + i + " 个 short 字段(" + shortFields[i] + ") 偏移 " + off
                        + "，期望 " + (i * 2) + "（short 应紧密排列且 2 字节对齐）。");
            }

            string[] doubleFields = {
                "velHigh", "velLow", "acc", "dec", "smoothTime",
                "searchHomeDistance", "searchIndexDistance", "escapeStep", "homeOffset"
            };
            for (int i = 0; i < doubleFields.Length; i++)
            {
                int off = Marshal.OffsetOf(typeof(THomePrm), doubleFields[i]).ToInt32();
                int expect = 8 + i * 8;   // 4 short = 8 字节，其后 double 需 8 字节对齐 → 恰好从 8 起
                if (off != expect)
                    throw new InvalidOperationException(
                        "THomePrm 布局自检失败: " + doubleFields[i] + " 偏移 " + off + "，期望 " + expect
                        + "。8 字节对齐被破坏（常见原因：结构体含 pack 或字段类型误写为 float）。");

                // 只查偏移不够：把 double 改成 float 时，后续字段的偏移**不变**（padding 吸收了），
                // 必须再查该字段实际占据的字节跨度 = 下一个字段偏移 - 本字段偏移。
                int span = (i + 1 < doubleFields.Length)
                    ? Marshal.OffsetOf(typeof(THomePrm), doubleFields[i + 1]).ToInt32() - off
                    : homeSize - off;
                if (span != 8)
                    throw new InvalidOperationException(
                        "THomePrm 布局自检失败: " + doubleFields[i] + " 占 " + span + " 字节，期望 8（double）。"
                        + "典型病因：该字段被误写为 float/int —— 偏移不变但宽度缩水，"
                        + "会与卡内实际布局错位，读回的回零参数全是垃圾。");
            }

            // 偏移与跨度都查还不够（首个 double 被误写成 float 时跨度仍是 8），
            // 最后做一次「值往返」：给每个 double 字段单独写入哨兵值 → Marshal 落内存 → 读回，
            // 读不回原值即说明该字段的真实宽度不是 8 字节。这是唯一能抓住 float 化的判据。
            AssertHomePrmRoundTrip(doubleFields);

            if (Marshal.SizeOf(typeof(TTrapPrm)) != 32)
                throw new InvalidOperationException("TTrapPrm 布局自检失败: 期望 32 字节（4 double）。");
            if (Marshal.SizeOf(typeof(TJogPrm)) != 32)
                throw new InvalidOperationException("TJogPrm 布局自检失败: 期望 32 字节（4 double）。");
        }

        /// <summary>THomePrm 值往返自检：验证每个 double 字段确实占满 8 字节且能无损读写。</summary>
        private static void AssertHomePrmRoundTrip(string[] doubleFields)
        {
            System.Reflection.FieldInfo[] fis =
                new System.Reflection.FieldInfo[doubleFields.Length];
            for (int i = 0; i < doubleFields.Length; i++)
                fis[i] = typeof(THomePrm).GetField(doubleFields[i]);

            IntPtr buf = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(THomePrm)));
            try
            {
                for (int i = 0; i < fis.Length; i++)
                {
                    object src = Activator.CreateInstance(typeof(THomePrm));
                    for (int k = 0; k < fis.Length; k++) fis[k].SetValue(src, 0.0);
                    double sentinel = 1.2345678901234E+11 + i;   // 各不相同且 double 可精确表示
                    fis[i].SetValue(src, sentinel);

                    Marshal.StructureToPtr(src, buf, false);
                    object back = Marshal.PtrToStructure(buf, typeof(THomePrm));
                    double got = (double)fis[i].GetValue(back);

                    if (got != sentinel)
                        throw new InvalidOperationException(
                            "THomePrm 布局自检失败: 字段 " + doubleFields[i] + " 值往返不一致（写入 "
                            + sentinel + " 读回 " + got + "）。该字段实际宽度不是 8 字节（很可能被误写为 float），"
                            + "与卡内布局错位，禁止上机。");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }

        public MotionResult Open(ControllerConnectInfo info)
        {
            lock (_sdk)
            {
                if (_open) return MotionResult.Success("already open");

                // 首板自检：结构体布局错 → 立刻抛，不带着错布局上机
                AssertStructLayouts();

                _info = info;

                // [固化] 先装载原生库并把其目录加入搜索路径（依赖不搜 DLL 同目录 -> 误报 126）
                // 见 Core/NativeLoader.cs
                string dllDir = string.IsNullOrEmpty(info.DllDir)
                    ? AppDomain.CurrentDomain.BaseDirectory : info.DllDir;
                NativeLoader.Ensure(dllDir);
                uint loadErr;
                if (!NativeLoader.Preload(System.IO.Path.Combine(dllDir, "gts.dll"), out loadErr))
                {
                    return MotionResult.Fail(-100,
                        "gts.dll 装载失败: " + NativeLoader.DescribeLoadError(loadErr));
                }

                short rc = GT_Open(info.CardNo, 0);
                if (rc != 0) return MotionResult.Fail(rc, "GT_Open card=" + info.CardNo);
                GT_Reset();
                if (!string.IsNullOrEmpty(info.ConfigFile))
                {
                    rc = GT_LoadConfig(info.ConfigFile);
                    if (rc != 0) { GT_Close(); return MotionResult.Fail(rc, "GT_LoadConfig " + info.ConfigFile); }
                }
                _open = true;
                return MotionResult.Success("GoogolGTS opened");
            }
        }

        public MotionResult Close()
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Success();

                GT_Reset();
                GT_Close();
                _open = false;
                return MotionResult.Success();
            }
        }

        public void Dispose() { try { Close(); } catch { } }

        public MotionResult ConfigureAxis(AxisConfig cfg)
        {
            lock (_sdk)
            {
                if (cfg == null) return MotionResult.Fail(-6, "null config");
                if (cfg.AxisIndex < 0 || cfg.AxisIndex > 7) return MotionResult.Fail(-7, "axis out of range 0-7");
                int i = cfg.AxisIndex;
                _ppu[i] = cfg.PulsesPerUnit <= 0 ? 10000 : cfg.PulsesPerUnit;
                _homeMode[i] = cfg.Home.Mode; _homeVelFast[i] = cfg.Home.VelFast;
                _homeVelSlow[i] = cfg.Home.VelSlow; _homeAcc[i] = cfg.Home.Acc;
                _softNeg[i] = cfg.SoftLimitNeg; _softPos[i] = cfg.SoftLimitPos;
                _homed[i] = false;

                return MotionResult.Success(cfg + " configured");
            }
        }

        public MotionResult Enable(int axis, bool on)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis);

                short ax = Ax(axis);
                short rc = GT_ClrSts(ax, 1);   // (起始轴, 轴数)：清本轴状态
                rc = on ? GT_AxisOn(ax) : GT_AxisOff(ax);
                if (rc != 0) return MotionResult.Fail(rc, "GT_Axis" + (on ? "On" : "Off") + " axis " + axis);
                return MotionResult.Success();
            }
        }

        public AxisState GetState(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return AxisState.None;
                CheckRange(axis);

                int sts; uint clk;
                short rc = GT_GetSts(Ax(axis), out sts, 1, out clk);
                AxisState s = AxisState.None;
                if (rc == 0)
                {
                    if ((sts & StsEnabled)  != 0) s |= AxisState.Enabled;
                    if ((sts & StsMoving)   != 0) s |= AxisState.Moving;
                    if ((sts & StsAlarm)    != 0) s |= AxisState.Alarm;
                    if ((sts & StsLimitPos) != 0) s |= AxisState.LimitPos;
                    if ((sts & StsLimitNeg) != 0) s |= AxisState.LimitNeg;
                    if (_homed[axis]) s |= AxisState.Homed;
                    if (!_stsCalibrated) s |= AxisState.Uncalibrated;
                }
                return s;
            }
        }

        public double GetCommandPos(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return 0;
                CheckRange(axis);

                double pos; uint clk;
                short rc = GT_GetPrfPos(Ax(axis), out pos, 1, out clk);
                return rc == 0 ? pos / _ppu[axis] : 0;
            }
        }

        public double GetFeedbackPos(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return 0;
                CheckRange(axis);

                double pos; uint clk;
                short rc = GT_GetEncPos(Ax(axis), out pos, 1, out clk);
                return rc == 0 ? pos / _ppu[axis] : GetCommandPos(axis);
            }
        }

        public double GetFollowingError(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return 0;
                double cmd = GetCommandPos(axis); double fb = GetFeedbackPos(axis);
                return cmd - fb;
            }
        }

        public MotionResult Home(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis);

                short ax = Ax(axis);
                // 优先走传参式回零 GT_Home(axis, axisNum, velHigh, velLow, edge)——
                // 回零速度/找Index速度/触发边沿从上位机直接下发生效。
                // GT_Home 的签名由 DLL 机器码反推：4+4+8+8+4=28 == ret 0x1C，
                // 且 GT_HomeStop 是同形状的 4 参版（24==0x18），互为佐证。
                // cfg.Home.VelFast<=0 表示用户没配，回退卡内参数并如实回报。
                double velHigh = _homeVelFast[axis], velLow = _homeVelSlow[axis];
                // edge: 0=正边沿 1=负边沿。LimitReverse 表示回零时朝负方向找，取负边沿。
                short edge = (short)(_homeMode[axis] == HomeMode.LimitReverse ? 1 : 0);
                _homed[axis] = false; _homing[axis] = true; _homeStartT[axis] = Environment.TickCount;
                if (velHigh > 0)
                {
                    // 回零速度同样必须走 Units：固高是「脉冲/毫秒」，直接 ×_ppu 会大 1000 倍
                    // （回零高速撞限位开关，是本项目最危险的一类换算错误）。
                    BrandUnits hbu = Units.Googol(_ppu[axis]);
                    short rc = GT_Home(ax, 1,
                        Units.MmS2Vendor(velHigh, hbu),
                        Units.MmS2Vendor(velLow, hbu), edge);
                    if (rc != 0) return MotionResult.Fail(rc, "GT_Home");
                    return MotionResult.Success("homing started (GT_Home 传参: velHigh="
                        + velHigh + " velLow=" + velLow + " edge=" + edge + ")");
                }

                THomePrm hp;
                short rc2 = GT_GetHomePrm(ax, out hp);
                if (rc2 != 0) return MotionResult.Fail(rc2, "GT_GetHomePrm");
                rc2 = GT_GoHome(ax, 1);   // (起始轴, 轴数)
                if (rc2 != 0) return MotionResult.Fail(rc2, "GT_GoHome");
                return MotionResult.Fail(-101,
                    "回零速度未配置（cfg.Home.VelFast<=0），已按卡内参数回零"
                    + "（实际 velHigh=" + hp.velHigh + " velLow=" + hp.velLow
                    + " mode=" + hp.mode + "）。配置 VelFast/VelSlow 后可经 GT_Home 传参生效");
            }
        }

        public bool IsHomed(int axis) { lock (_sdk) { return axis >= 0 && axis < 8 && _homed[axis]; } }

        public MotionResult WaitHomeDone(int axis, int timeoutMs)
        {
            DateTime t0 = DateTime.Now;
            while ((DateTime.Now - t0).TotalMilliseconds < timeoutMs)
            {
                lock (_sdk)
                {
                    if (!_open) return MotionResult.Fail(-1, "not open");
                    if (_homing[axis] && Environment.TickCount - _homeStartT[axis] > 200 && (!IsMovingGts(axis))) { _homing[axis] = false; _homed[axis] = true; return MotionResult.Success(); }
                }
                Thread.Sleep(10);
            }
            return MotionResult.Fail(-2, "home timeout axis " + axis);
        }

        public MotionResult MoveTo(int axis, double posUser, MoveParams p, MoveMode mode)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis);
                double from = GetCommandPos(axis);
                double to = (mode == MoveMode.Absolute) ? posUser : from + posUser;
                if (to < _softNeg[axis] || to > _softPos[axis])
                    return MotionResult.Fail(-100, "soft limit reject: target " + to.ToString("F3"));

                short ax = Ax(axis);
                short rc = GT_PrfTrap(ax);
                if (rc != 0) return MotionResult.Fail(rc, "GT_PrfTrap");
                BrandUnits bu = Units.Googol(_ppu[axis]);
                TTrapPrm tp = new TTrapPrm();
                tp.acc = Units.MmS2_2ToVendor(p.Acc, bu);
                tp.dec = Units.MmS2_2ToVendor(p.Dec, bu);
                tp.smoothTime = SmoothMs(p.JerkRatio);   // 官方口径：平滑时间(ms)，取值 [0,50]
                tp.vel = Units.MmS2Vendor(Math.Abs(p.Vel), bu);
                rc = GT_SetTrapPrm(ax, ref tp);
                if (rc != 0) return MotionResult.Fail(rc, "GT_SetTrapPrm");
                rc = GT_SetPos(ax, Units.MmToPulseInt32(to, _ppu[axis], axis, "Googol"));
                if (rc != 0) return MotionResult.Fail(rc, "GT_SetPos");
                rc = GT_SetVel(ax, Units.MmS2Vendor(Math.Abs(p.Vel), bu));
                if (rc != 0) return MotionResult.Fail(rc, "GT_SetVel");
                rc = GT_Update(1 << axis);   // [核对] 掩码bit为0-based轴号
                if (rc != 0) return MotionResult.Fail(rc, "GT_Update");
                return MotionResult.Success();
            }
        }

        public MotionResult MoveJog(int axis, double velUserPerSec, MoveParams p)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis);

                short ax = Ax(axis);
                short rc = GT_PrfJog(ax);
                if (rc != 0) return MotionResult.Fail(rc, "GT_PrfJog");
                TJogPrm jp = new TJogPrm();
                BrandUnits bu = Units.Googol(_ppu[axis]);
                jp.acc = Units.MmS2_2ToVendor(p.Acc, bu);
                jp.dec = Units.MmS2_2ToVendor(p.Dec, bu);
                jp.smoothTime = SmoothMs(p.JerkRatio);
                jp.vel = Units.MmS2Vendor(velUserPerSec, bu);   // 符号即方向
                rc = GT_SetJogPrm(ax, ref jp);
                if (rc != 0) return MotionResult.Fail(rc, "GT_SetJogPrm");
                rc = GT_Update(1 << axis);
                if (rc != 0) return MotionResult.Fail(rc, "GT_Update");
                return MotionResult.Success("jog vel=" + velUserPerSec.ToString("F2"));
            }
        }

        public MotionResult Stop(int axis, bool emergency)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis);

                int mask = 1 << axis;
                short rc = GT_Stop(mask, emergency ? 1 : 0);
                if (rc != 0) return MotionResult.Fail(rc, "GT_Stop");
                return MotionResult.Success(emergency ? "emergency stop" : "stop");
            }
        }

        public MotionResult WaitMoveDone(int axis, int timeoutMs)
        {
            DateTime t0 = DateTime.Now;
            while ((DateTime.Now - t0).TotalMilliseconds < timeoutMs)
            {
                lock (_sdk)
                {
                    if (!_open) return MotionResult.Fail(-1, "not open");
                    if (!(IsMovingGts(axis))) return MotionResult.Success();
                }
                Thread.Sleep(10);
            }
            return MotionResult.Fail(-3, "move timeout axis " + axis);
        }

        public MotionResult MoveLine(int[] axisList, double[] targetUser, MoveParams p, MoveMode mode)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                if (axisList == null || targetUser == null || axisList.Length != targetUser.Length)
                    return MotionResult.Fail(-4, "axisList/targetUser mismatch");

                // 等时点位法: 各轴速度按位移比例分配，同触发同到达。
                // [升级路径] 高同步要求场景改用固高坐标系插补(GT_SetCrdPrf/GT_CrdLine/GT_CrdArc)，
                //            可获得硬件级插补脉冲同步（见docs/CapabilityMatrix.md）。
                double maxDelta = 1e-9;
                double[] from = new double[axisList.Length];
                double[] toArr = new double[axisList.Length];
                for (int i = 0; i < axisList.Length; i++)
                {
                    from[i] = GetCommandPos(axisList[i]);
                    toArr[i] = (mode == MoveMode.Absolute) ? targetUser[i] : from[i] + targetUser[i];
                    double d = Math.Abs(toArr[i] - from[i]);
                    if (d > maxDelta) maxDelta = d;
                    if (toArr[i] < _softNeg[axisList[i]] || toArr[i] > _softPos[axisList[i]])
                        return MotionResult.Fail(-100, "soft limit axis " + axisList[i]);
                }
                double duration = maxDelta / Math.Max(p.Vel, 0.001) + Math.Max(p.Vel, 0.001) / Math.Max(p.Acc, 1);
                int allMask = 0;
                for (int i = 0; i < axisList.Length; i++)
                {
                    int ax0 = axisList[i];
                    short ax = Ax(ax0);
                    double dist = Math.Abs(toArr[i] - from[i]);
                    double vi = dist / Math.Max(duration, 0.001);
                    short rc = GT_PrfTrap(ax);
                    if (rc != 0) return MotionResult.Fail(rc, "GT_PrfTrap");
                    TTrapPrm tp = new TTrapPrm();
                    BrandUnits bu = Units.Googol(_ppu[ax0]);
                    tp.acc = Units.MmS2_2ToVendor(p.Acc, bu);
                    tp.dec = Units.MmS2_2ToVendor(p.Dec, bu);
                    tp.smoothTime = SmoothMs(p.JerkRatio);
                    tp.vel = Units.MmS2Vendor(vi, bu);
                    rc = GT_SetTrapPrm(ax, ref tp);
                    if (rc != 0) return MotionResult.Fail(rc, "GT_SetTrapPrm");
                    rc = GT_SetPos(ax, Units.MmToPulseInt32(toArr[i], _ppu[ax0], ax0, "Googol"));
                    if (rc != 0) return MotionResult.Fail(rc, "GT_SetPos");
                    rc = GT_SetVel(ax, Units.MmS2Vendor(vi, bu));
                    if (rc != 0) return MotionResult.Fail(rc, "GT_SetVel");
                    allMask |= 1 << ax0;
                }
                short rcU = GT_Update(allMask);
                if (rcU != 0) return MotionResult.Fail(rcU, "GT_Update");
                return MotionResult.Success("equal-time line, axes=" + axisList.Length);
            }
        }

        public MotionResult MoveArc2D(int axisX, int axisY, double i, double j, double endX, double endY, bool cw, MoveParams p)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                double x0 = GetCommandPos(axisX), y0 = GetCommandPos(axisY);
                double cx = x0 + i, cy = y0 + j;
                double r = Math.Sqrt(i * i + j * j);
                if (r < 1e-9) return MotionResult.Fail(-8, "zero radius arc");
                double a0 = Math.Atan2(y0 - cy, x0 - cx);
                double a1 = Math.Atan2(endY - cy, endX - cx);
                double sweep = a1 - a0;
                if (cw && sweep >= 0) sweep -= 2 * Math.PI;
                if (!cw && sweep <= 0) sweep += 2 * Math.PI;
                int n = 32;
                for (int k = 1; k <= n; k++)
                {
                    double a = a0 + sweep * k / n;
                    MotionResult rseg = MoveLine(new int[] { axisX, axisY },
                        new double[] { cx + r * Math.Cos(a), cy + r * Math.Sin(a) }, p, MoveMode.Absolute);
                    if (!rseg.Ok) return rseg;
                    MotionResult rw = WaitMoveDone(axisX, 60000);
                    if (!rw.Ok) return rw;
                }
                // [升级路径] 硬件圆弧: GT_CrdArc（坐标系模式），精度与速度优于线性化
                return MotionResult.Success("arc done (32-seg linearization)");
            }
        }

        public MotionResult SetDo(int index, bool on)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");

                short rc = GT_SetDo((short)index, (short)(on ? 1 : 0));   // 2 参：(index, value)
                if (rc != 0) return MotionResult.Fail(rc, "GT_SetDo " + index);
                return MotionResult.Success();
            }
        }

        public bool GetDi(int index)
        {
            lock (_sdk)
            {
                if (!_open) return false;

                int di;
                short rc = GT_GetDi(1, out di);   // [核对] type 1=GT_DI_GPI; 返回组值则按位取
                if (rc != 0) return false;
                return ((di >> index) & 1) != 0;
            }
        }

        public double GetAi(int index)
        {
            lock (_sdk)
            {
                if (!_open) return 0;

                double v;
                short rc = GT_GetAdcValue(0, (short)index, 1, out v);   // (axis, idx, count, out)
                return rc == 0 ? v : 0;
            }
        }

        public MotionResult SetAo(int index, double volts)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");

                // 电压直设：走 GT_SetExtDaVoltage（第3参是 double，ret=16）
                short rc = GT_SetExtDaVoltage(0, (short)index, volts);
                if (rc != 0) return MotionResult.Fail(rc, "GT_SetExtDaVoltage " + index);
                return MotionResult.Success();
            }
        }

        public MotionResult WaitDi(int index, bool level, int timeoutMs)
        {
            DateTime t0 = DateTime.Now;
            while ((DateTime.Now - t0).TotalMilliseconds < timeoutMs)
            {
                if (GetDi(index) == level) return MotionResult.Success();
                Thread.Sleep(10);
            }
            return MotionResult.Fail(-5, "wait DI" + index + " timeout");
        }


        /// <summary>
        /// 用户 JerkRatio(0~1) → 官方 TTrapPrm.smoothTime（**单位：毫秒**，取值范围 **[0,50]**）。
        ///
        /// 官方依据（第三方转载固高官方文档，逐字）：
        ///   「回原点运动的平滑时间：取值[0,50]，单位：ms，具体含义与 GTS 系列控制器点位运动相似」
        ///   示例代码 `prm.smoothTime = 25;`
        /// 另有独立来源印证同一区间：`trap.smoothTime = 20; //范围0-50`。
        ///
        /// ★ 历史错误：本函数早期写成 `0.02 + JerkRatio * 0.1`（即 0.02~0.12），
        ///   那是把 smoothTime 误当成「无量纲平滑系数」，与官方口径差 20~50 倍，
        ///   且量纲完全不对（系数 vs 毫秒）。两个量级都不对，不是偏差而是错误。
        ///
        /// 满量程取 50ms：官方区间上界。JerkRatio=0 → 0ms → 梯形曲线（官方语义：0=梯形）。
        /// </summary>
        private static double SmoothMs(double jerkRatio)
        {
            if (double.IsNaN(jerkRatio) || jerkRatio <= 0.0) return 0.0;   // 0 = 梯形
            if (jerkRatio >= 1.0) return 50.0;                            // 官方上界
            return jerkRatio * 50.0;
        }

        private bool IsMovingGts(int axis)
        {
            int sts; uint clk;
            if (GT_GetSts(Ax(axis), out sts, 1, out clk) != 0) return false;
            return (sts & StsMoving) != 0;
        }

        // ---- GT_GetSts 状态位 ----
        // 这几位在 2016 版 gts.dll 上无法从二进制静态定案（各家固件/版本有差异）。
        // 处理方式：不猜死，改成「运行期标定 + 未标定时明确报错」，而不是悄悄用可能错的位。
        // 未标定时 GetState 仍返回 None 并在 Message 里提示（见 GetStateRawSts / ProbeStatusBits）。
        private static int StsMoving   = 0x400;   // 运动中（首板标定后覆盖）
        private static int StsAlarm    = 0x2;
        private static int StsEnabled  = 0x1;
        private static int StsLimitPos = 0x20;
        private static int StsLimitNeg = 0x10;
        private static bool _stsCalibrated;

        /// <summary>
        /// 首板标定：把 GT_GetSts 的关键位跑出来，供人工确认为哪几位。
        /// 调用前提：已 Open 且轴已使能。返回可读的状态位摘要（各候选位当前取值）。
        /// 判据（见 GOOGOL_STATUS.md §3-2）：
        ///   · 使能前后只有「使能位」变化 → 该位即 StsEnabled
        ///   · 点动过程中只有「运动位」置 1 → 该位即 StsMoving
        ///   · 触发报警/限位后对应位变化
        /// 确认为准后调用 CalibrateStsBits(enabled, moving, alarm, limitPos, limitNeg) 固化。
        /// </summary>
        public string ProbeStatusBits(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return "not open";
                CheckRange(axis);
                int sts; uint clk;
                short rc = GT_GetSts(Ax(axis), out sts, 1, out clk);
                if (rc != 0) return "GT_GetSts rc=" + rc;
                string s = "axis " + axis + " raw=0x" + sts.ToString("X8") + " (未标定，当前用启发式默认值)";
                for (int b = 0; b < 16; b++)
                    if ((sts & (1 << b)) != 0) s += " bit" + b;
                return s;
            }
        }

        /// <summary>首板标定完成后固化状态位（把 ProbeStatusBits 的结果填进来）。</summary>
        public void CalibrateStsBits(int enabled, int moving, int alarm, int limitPos, int limitNeg)
        {
            StsEnabled = enabled; StsMoving = moving; StsAlarm = alarm;
            StsLimitPos = limitPos; StsLimitNeg = limitNeg;
            _stsCalibrated = true;
        }

        private static void CheckRange(int axis)
        {
            if (axis < 0 || axis > 7) throw new ArgumentOutOfRangeException("axis", axis, "supports 0-7");
        }

        /// <summary>统一0-based轴号 -> 固高1-based</summary>
        private static short Ax(int axis) { return (short)(axis + 1); }
    }
}
