// =====================================================================
//  雷赛 DMC/EMC 系列 (LTDMC) 适配器（LTDMC.dll）
//  生成工具: 上位机代码工厂 v1.0 | 2026-10-20 00:17:00
//  KB级别: B —— 核心API已映射(board/点位/停止/位置/IO)，使能与回零依赖型号(脉冲卡/总线卡)，[核对]项较多。
//  上真机前按 docs/ApiChecklist.md 核对签名（标注 [核对] 处）
// =====================================================================
using System;
using System.Runtime.InteropServices;
using System.Threading;
using MotionControl.Core;

namespace MotionControl.Adapters.Leadshine
{
    /// <summary>
    /// 雷赛 DMC/EMC 系列 (LTDMC) —— IMotionController 适配器。
    /// 设计要点（JD职责3）:
    ///   1. P/Invoke 封装 + 签名核对标记
    ///   2. 单位换算: 用户单位(mm) <-脉冲- LTDMC.dll
    ///   3. SDK 调用统一持锁串行化（厂商库多非线程安全）
    ///   4. 错误码统一映射 MotionResult
    /// </summary>
    public class LeadshineDmcController : IMotionController
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

        public LeadshineDmcController()
        {
            for (int i = 0; i < 8; i++) { _ppu[i] = 10000; _softNeg[i] = -100000; _softPos[i] = 100000; _homeVelFast[i] = 20; _homeVelSlow[i] = 2; _homeAcc[i] = 200; }
        }

        public Brand Brand { get { return Brand.Leadshine; } }
        public string DeviceName { get { return "LeadshineDMC"; } }
        public bool IsOpen { get { return _open; } }

        /// <summary>
        /// **不支持** S 曲线平滑 —— 当前用 <c>dmc_set_profile</c>，该函数只接受 tacc/tdec，
        /// 没有 jerk 维度，<see cref="MoveParams.JerkRatio"/> 被**静默丢弃**。
        ///
        /// 官方其实有能力：<c>dmc_set_profile_extern(..., Acc, Dec, Ajerk, Djerk, stop_vel)</c>
        /// （LTDMC.h L169）支持 S 曲线。之所以未接 —— Ajerk/Djerk 的单位与取值范围
        /// 尚未逐字确认（tech-debt D7-阶段2），宁可显式声明不支持，也不猜着下发。
        ///
        /// 接入前调用方应把 JerkRatio 置 0（梯形曲线）。
        /// </summary>
        public bool SupportsJerkProfile { get { return false; } }

        #region 厂商 API 导入（[核对] = 签名需按手册确认）

        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_board_init();
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_board_close();
        /// <summary>官方 C# 绑定 LTDMC.cs L213：Dist 为 <c>Int32</c>（不是 long）。
        /// 误写 long 会压 8 字节到 stdcall 栈上，导致 axis/mode 全部错位。</summary>
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_pmove(ushort card, ushort axis, int pos, ushort mode);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_set_profile(ushort card, ushort axis, double startv, double maxv, double tacc, double tdec, double stopv);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_stop(ushort card, ushort axis, ushort stopMode);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_check_done(ushort card, ushort axis);
        /// <summary>官方 C# 绑定 LTDMC.cs L901 返回 <c>Int32</c>。
        /// C 头文件 LTDMC.h L478 写的是 <c>long</c>（32 位构建下同宽）——
        /// 这里以官方 C# 绑定为准，用 <c>int</c> 而非 <c>long</c>。</summary>
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int dmc_get_position(ushort card, ushort axis);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int dmc_get_encoder(ushort card, ushort axis);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_vmove(ushort card, ushort axis, ushort dir);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_home_move(ushort card, ushort axis);
        /// <summary>官方 C# 绑定 LTDMC.cs L339 / C 头文件 LTDMC.h L247：
        /// <c>(CardNo, axis, home_dir, vel, mode, EZ_count)</c> —— <b>共 6 参</b>，
        /// 第 3 参是<b>回零方向</b>、第 4 参是<b>速度</b>(double)、第 5 参才是 mode。</summary>
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_set_homemode(ushort card, ushort axis, ushort homeDir, double vel, ushort mode, ushort ezCount);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_read_inbit(ushort card, ushort bitno);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_write_outbit(ushort card, ushort bitno, ushort onoff);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_get_ad_input(ushort card, ushort ch, ref double vout);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_set_da_output(ushort card, ushort ch, double vout);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_set_sevon_enable(ushort card, ushort axis, ushort onoff);
        #endregion

        public MotionResult Open(ControllerConnectInfo info)
        {
            lock (_sdk)
            {
                if (_open) return MotionResult.Success("already open");
                _info = info;

                // [固化] 先装载原生库并把其目录加入搜索路径。
                // 不做这一步，LoadLibrary 找 LTDMC.dll 的依赖时不会搜它所在目录 ->
                // 明明 DLL 都在同目录却报 Win32=126。详见 Core/NativeLoader.cs。
                string dllDir = string.IsNullOrEmpty(info.DllDir)
                    ? AppDomain.CurrentDomain.BaseDirectory : info.DllDir;
                NativeLoader.Ensure(dllDir);
                uint loadErr;
                if (!NativeLoader.Preload(System.IO.Path.Combine(dllDir, "LTDMC.dll"), out loadErr))
                {
                    // 装载失败与"连接失败"是两类错误，必须分开报，否则无法定位
                    return MotionResult.Fail(-100,
                        "LTDMC.dll 装载失败: " + NativeLoader.DescribeLoadError(loadErr));
                }

                short n = dmc_board_init();
                if (n <= 0) return MotionResult.Fail(n, "dmc_board_init: no card found");
                if (info.CardNo >= n) return MotionResult.Fail(-9, "card " + info.CardNo + " not present, cards=" + n);
                _open = true;
                return MotionResult.Success("LeadshineDMC opened");
            }
        }

        public MotionResult Close()
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Success();

                dmc_board_close();
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

                // [核对] 脉冲卡(DMC系列)多为硬线使能，无软件使能；总线卡(EMC)用 dmc_set_sevon_enable
                ushort sv = on ? (ushort)1 : (ushort)0;
                dmc_set_sevon_enable(Card, (ushort)axis, sv);
                return MotionResult.Success("set_sevon_enable " + sv + " (脉冲卡请检查硬线使能)");
            }
        }

        public AxisState GetState(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return AxisState.None;
                CheckRange(axis);

                AxisState s = AxisState.None;
                short done = dmc_check_done(Card, (ushort)axis);
                if (done == 0) s |= AxisState.Moving; else s |= AxisState.InPosition;
                if (_homed[axis]) s |= AxisState.Homed;
                // [核对] 报警/限位状态需按型号读取状态寄存器(如 dmc_get_axis_state)
                return s;
            }
        }

        public double GetCommandPos(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return 0;
                CheckRange(axis);

                return dmc_get_position(Card, (ushort)axis) / _ppu[axis];
            }
        }

        public double GetFeedbackPos(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return 0;
                CheckRange(axis);

                return dmc_get_encoder(Card, (ushort)axis) / _ppu[axis];
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
                CheckRange(axis); _homed[axis] = false; _homing[axis] = true; _homeStartT[axis] = Environment.TickCount;

                // [核对] 回零模式/方向/EZ计数按型号手册核对
                ushort mode;
                switch (_homeMode[axis])
                {
                    case HomeMode.LimitReverse: mode = 0; break;
                    case HomeMode.HomeSwitch: mode = 1; break;
                    case HomeMode.HomeSwitchIndex: mode = 2; break;
                    case HomeMode.IndexOnly: mode = 3; break;
                    default: mode = 2; break;
                }
                // 官方签名（LTDMC.cs L339 / LTDMC.h L247）：
                //   dmc_set_homemode(CardNo, axis, home_dir, vel, mode, EZ_count)
                // ★ 第 3 参是 home_dir（回零方向），第 4 参是 vel（速度，double），
                //   第 5 参才是 mode。原代码把 mode 放在第 3 位 —— 参数全错位。
                // ★ 第 4 参是**速度**不是加速度，须用 MmS2Vendor（速度换算）而非 MmS2_2ToVendor。
                BrandUnits bu = Units.Leadshine(_ppu[axis]);
                dmc_set_homemode(Card, (ushort)axis, 0,
                    Units.MmS2Vendor(_homeVelFast[axis], bu), mode, 0);
                short rc = dmc_home_move(Card, (ushort)axis);
                if (rc != 0) return MotionResult.Fail(rc, "dmc_home_move");
                return MotionResult.Success("homing started");
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
                    if (_homing[axis] && Environment.TickCount - _homeStartT[axis] > 200 && (dmc_check_done(Card, (ushort)axis) == 1)) { _homing[axis] = false; _homed[axis] = true; return MotionResult.Success(); }
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

                ushort card = Card;
                // 官方手册 DMC5X10 V2.0 §9.8：速度单位 pulse/s、Tacc/Tdec 单位 **s**（不是 ms）。
                // 本函数用的是无后缀脉冲版 dmc_set_profile，故速度需 ×脉冲当量。
                BrandUnits bu = Units.Leadshine(_ppu[axis]);
                dmc_set_profile(card, (ushort)axis, 0, Units.MmS2Vendor(Math.Abs(p.Vel), bu),
                    TaccSec(p.Vel, p.Acc), TaccSec(p.Vel, p.Dec), 0);
                short rc = dmc_pmove(card, (ushort)axis,
                    Units.MmToPulseInt32(to, _ppu[axis], axis, "Leadshine"), 1);
                if (rc != 0) return MotionResult.Fail(rc, "dmc_pmove");
                return MotionResult.Success();
            }
        }

        public MotionResult MoveJog(int axis, double velUserPerSec, MoveParams p)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis);

                ushort dir = velUserPerSec >= 0 ? (ushort)0 : (ushort)1;   // [核对] 方向定义
                BrandUnits bu = Units.Leadshine(_ppu[axis]);
                dmc_set_profile(Card, (ushort)axis, 0, Units.MmS2Vendor(Math.Abs(velUserPerSec), bu),
                    TaccSec(velUserPerSec, p.Acc), TaccSec(velUserPerSec, p.Dec), 0);
                short rc = dmc_vmove(Card, (ushort)axis, dir);
                if (rc != 0) return MotionResult.Fail(rc, "dmc_vmove");
                return MotionResult.Success("jog dir=" + dir);
            }
        }

        public MotionResult Stop(int axis, bool emergency)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis);

                short rc = dmc_stop(Card, (ushort)axis, (ushort)(emergency ? 1 : 0));
                if (rc != 0) return MotionResult.Fail(rc, "dmc_stop");
                return MotionResult.Success(emergency ? "emergency" : "stop");
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
                    if (!(dmc_check_done(Card, (ushort)axis) == 0)) return MotionResult.Success();
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

                // 等时点位法（升级路径: dmc_line_unit 多轴插补, 见docs）
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
                for (int i = 0; i < axisList.Length; i++)
                {
                    int ax0 = axisList[i];
                    double dist = Math.Abs(toArr[i] - from[i]);
                    double vi = Math.Max(dist / Math.Max(duration, 0.001), 1);
                    BrandUnits bu = Units.Leadshine(_ppu[ax0]);
                    dmc_set_profile(Card, (ushort)ax0, 0, Units.MmS2Vendor(vi, bu),
                        TaccSec(vi, p.Acc), TaccSec(vi, p.Dec), 0);
                    short rc = dmc_pmove(Card, (ushort)ax0,
                        Units.MmToPulseInt32(toArr[i], _ppu[ax0], ax0, "Leadshine"), 1);
                    if (rc != 0) return MotionResult.Fail(rc, "dmc_pmove axis " + ax0);
                }
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
                // [升级路径] 雷赛硬件圆弧: dmc_arc_unit
                return MotionResult.Success("arc done (32-seg linearization)");
            }
        }

        public MotionResult SetDo(int index, bool on)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");

                // [注意] 雷赛DO低有效: 0=输出导通
                ushort v = on ? (ushort)0 : (ushort)1;
                short rc = dmc_write_outbit(Card, (ushort)index, v);
                if (rc != 0) return MotionResult.Fail(rc, "dmc_write_outbit " + index);
                return MotionResult.Success();
            }
        }

        public bool GetDi(int index)
        {
            lock (_sdk)
            {
                if (!_open) return false;

                short v = dmc_read_inbit(Card, (ushort)index);
                return v == 0;   // [注意] 雷赛DI低有效: 0=导通
            }
        }

        public double GetAi(int index)
        {
            lock (_sdk)
            {
                if (!_open) return 0;

                double vout = 0;
                short rc = dmc_get_ad_input(Card, (ushort)index, ref vout);
                return rc == 0 ? vout : 0;   // 官方直接返回电压值(V)，无需再换算
            }
        }

        public MotionResult SetAo(int index, double volts)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");

                short rc = dmc_set_da_output(Card, (ushort)index, volts);
                if (rc != 0) return MotionResult.Fail(rc, "dmc_set_da_output " + index);
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

        private static void CheckRange(int axis)
        {
            if (axis < 0 || axis > 7) throw new ArgumentOutOfRangeException("axis", axis, "supports 0-7");
        }

        /// <summary>
        /// 用户参数(mm/s, mm/s²) → 官方 dmc_set_profile 的 Tacc/Tdec（单位：秒）。
        ///
        /// 官方手册 DMC5X10 V2.0 §9.8 逐字：「Tacc 加速时间，单位：s (最小值为 0.001s)」
        /// —— 是**秒**，不是毫秒。本函数早期版本写成 1000.0/acc（毫秒），差 1000 倍，
        /// 轴会用极短的加速时间猛冲，是查手册时抓出的真实错误。
        ///
        /// 官方语义：Tacc = 从 startv 加速到 Max_Vel 所需的时间 = (Max_Vel - startv) / acc。
        /// 本适配器 startv 恒为 0，故 t = Max_Vel / acc。
        ///
        /// ★ 脉冲当量在分子分母同时出现后约掉：(mm/s × ppu) / (mm/s² × ppu) = mm/s / mm/s² = 秒。
        /// 所以这里**不需要** BrandUnits 参数 —— 传进去反而会诱使人多乘一次当量。
        /// </summary>
        private static double TaccSec(double velMmPerSec, double accMmS2)
        {
            double a = Math.Max(accMmS2, 1e-9);
            double t = Math.Abs(velMmPerSec) / a;                  // 秒
            if (double.IsNaN(t) || double.IsInfinity(t)) return 1000.0;
            if (t < 0.001) return 0.001;                          // 官方下限 0.001s
            if (t > 1000.0) return 1000.0;                        // 上限保护
            return t;
        }

        private ushort Card { get { return (ushort)(_info == null ? 0 : _info.CardNo); } }
    }
}
