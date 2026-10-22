// =====================================================================
//  正运动 ZMC 系列 (zmcaux) 适配器（zmcaux.dll）
//  生成工具: 上位机代码工厂 v1.0 | 2026-10-20 00:17:00
//  KB级别: B —— 核心API已映射(EtherCAT网络连接/当量/点位/回零/IO)，ZAux_Direct系列为现行主力接口。
//  上真机前按 docs/ApiChecklist.md 核对签名（标注 [核对] 处）
// =====================================================================
using System;
using System.Runtime.InteropServices;
using System.Threading;
using MotionControl.Core;

namespace MotionControl.Adapters.ZMotion
{
    /// <summary>
    /// 正运动 ZMC 系列 (zmcaux) —— IMotionController 适配器。
    /// 设计要点（JD职责3）:
    ///   1. P/Invoke 封装 + 签名核对标记
    ///   2. 单位换算: 用户单位(mm) <-脉冲- zmcaux.dll
    ///   3. SDK 调用统一持锁串行化（厂商库多非线程安全）
    ///   4. 错误码统一映射 MotionResult
    /// </summary>
    public class ZMotionZmcController : IMotionController
    {
        private readonly object _sdk = new object();
        private ControllerConnectInfo _info;
        private bool _open;
        private readonly bool[] _homed = new bool[8];
        private readonly bool[] _jerkApplied = new bool[8];   // 各轴最近一次运动是否真的采纳了 JerkRatio
        private readonly double[] _ppu = new double[8];         // 脉冲/用户单位
        private readonly HomeMode[] _homeMode = new HomeMode[8];
        private readonly double[] _homeVelFast = new double[8];
        private readonly double[] _homeVelSlow = new double[8];
        private readonly double[] _homeAcc = new double[8];
        private readonly bool[] _homing = new bool[8];
        private readonly int[] _homeStartT = new int[8];
        private readonly double[] _softNeg = new double[8];
        private readonly double[] _softPos = new double[8];

        private IntPtr _h = IntPtr.Zero;

        public ZMotionZmcController()
        {
            for (int i = 0; i < 8; i++) { _ppu[i] = 10000; _softNeg[i] = -100000; _softPos[i] = 100000; _homeVelFast[i] = 20; _homeVelSlow[i] = 2; _homeAcc[i] = 200; }
        }

        public Brand Brand { get { return Brand.ZMotion; } }
        public string DeviceName { get { return "ZMotionZMC"; } }
        public bool IsOpen { get { return _open; } }

        /// <summary>
        /// 支持 S 曲线平滑 —— 走 <c>ZAux_Direct_SetSramp</c>（S 曲线平滑时间，**毫秒**）。
        /// 官方绑定注释逐字：「S曲线平滑时间设定值 MS」；RTBasic 手册给出范围 **0~250 ms**。
        /// 与固高 <c>smoothTime</c> 同量纲（时间/毫秒），仅满量程不同（固高 50 / 正运动 250）。
        /// 证据：evidence/JERK_UNITS_EVIDENCE.md。
        /// </summary>
        public bool SupportsJerkProfile { get { return true; } }

        #region 厂商 API 导入（[核对] = 签名需按手册确认）

        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern int ZAux_OpenEth([MarshalAs(UnmanagedType.LPStr)] string ip, out IntPtr handle);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Close(IntPtr handle);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetUnits(IntPtr h, int axis, float value);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetSpeed(IntPtr h, int axis, float value);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetAccel(IntPtr h, int axis, float value);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetDecel(IntPtr h, int axis, float value);
        /// <summary>设置 S 曲线平滑时间（**毫秒**，官方范围 0~250）。
        /// 官方绑定注释逐字：「S曲线平滑时间设定值 MS」。Jerk 与 SRAMP 互斥，不同时下发。
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetSramp(IntPtr h, int axis, float fValue);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_Single_MoveAbs(IntPtr h, int axis, float pos);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_Single_Move(IntPtr h, int axis, float dist);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_MoveAbs(IntPtr h, int imaxaxis, int[] axislist, float[] dpos);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_Single_Vmove(IntPtr h, int axis, int dir);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_Single_Cancel(IntPtr h, int axis, int option);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_Single_Datum(IntPtr h, int axis, int mode);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_GetDpos(IntPtr h, int axis, out float pPos);   // 官方注释: 读取轴命令位置坐标
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_GetMpos(IntPtr h, int axis, out float pPos);   // 官方注释: 读取编码轴反馈位置坐标
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_GetFe(IntPtr h, int axis, out float pPos);     // 官方注释: 读取轴随动误差
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_GetIfIdle(IntPtr h, int axis, out int pIdle);  // 官方注释: 读取轴运动完成状态 0-运动中 -1-停止
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_GetIn(IntPtr h, int ionum, out int pValue);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetOp(IntPtr h, int ionum, int value);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_GetAD(IntPtr h, int adnum, out int pValue);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetDA(IntPtr h, int danum, int value);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetAxisEnable(IntPtr h, int axis, int onoff);
        #endregion

        public MotionResult Open(ControllerConnectInfo info)
        {
            lock (_sdk)
            {
                if (_open) return MotionResult.Success("already open");
                _info = info;

                // [固化] 先装载原生库并把其目录加入搜索路径（依赖不搜 DLL 同目录 -> 误报 126）
                // 见 Core/NativeLoader.cs
                string dllDir = string.IsNullOrEmpty(info.DllDir)
                    ? AppDomain.CurrentDomain.BaseDirectory : info.DllDir;
                NativeLoader.Ensure(dllDir);
                uint loadErr;
                if (!NativeLoader.Preload(System.IO.Path.Combine(dllDir, "zauxdll.dll"), out loadErr))
                {
                    return MotionResult.Fail(-100,
                        "zauxdll.dll 装载失败: " + NativeLoader.DescribeLoadError(loadErr));
                }

                IntPtr h;
                int rc = ZAux_OpenEth(info.Ip, out h);
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_OpenEth " + info.Ip);
                _h = h;
                _open = true;
                return MotionResult.Success("ZMotionZMC opened");
            }
        }

        public MotionResult Close()
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Success();

                if (_h != IntPtr.Zero) { ZAux_Close(_h); _h = IntPtr.Zero; }
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

                if (_h != IntPtr.Zero) ZAux_Direct_SetUnits(_h, i, (float)_ppu[i]);   // 当量同步到控制器
                return MotionResult.Success(cfg + " configured");
            }
        }

        public MotionResult Enable(int axis, bool on)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis);

                int rc = ZAux_Direct_SetAxisEnable(_h, axis, on ? 1 : 0);   // [核对] 总线轴使能
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_Direct_Servo");
                return MotionResult.Success();
            }
        }

        public AxisState GetState(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return AxisState.None;
                CheckRange(axis);

                int idle;
                ZAux_Direct_GetIfIdle(_h, axis, out idle);
                AxisState s = AxisState.None;
                if (idle == 0) s |= AxisState.Moving; else s |= AxisState.InPosition;
                if (_homed[axis]) s |= AxisState.Homed;
                return s;
            }
        }

        public double GetCommandPos(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return 0;
                CheckRange(axis);

                float pos;
                ZAux_Direct_GetDpos(_h, axis, out pos);   // 命令位置（Dpos），不是反馈
                return pos;
            }
        }

        public double GetFeedbackPos(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return 0;
                CheckRange(axis);

                float pos;
                ZAux_Direct_GetMpos(_h, axis, out pos);   // 编码轴反馈位置（Mpos）
                return pos;
            }
        }

        public double GetFollowingError(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return 0;
                CheckRange(axis);

                float err;
                ZAux_Direct_GetFe(_h, axis, out err);     // 官方直接提供随动误差读取
                return err;
            }
        }

        public MotionResult Home(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis); _homed[axis] = false; _homing[axis] = true; _homeStartT[axis] = Environment.TickCount;

                int mode;
                switch (_homeMode[axis])
                {
                    case HomeMode.LimitReverse: mode = 3; break;
                    case HomeMode.HomeSwitch: mode = 4; break;      // [核对] DATUM模式表
                    case HomeMode.HomeSwitchIndex: mode = 5; break;
                    case HomeMode.IndexOnly: mode = 6; break;
                    default: mode = 4; break;
                }
                ZAux_Direct_SetSpeed(_h, axis, (float)(_homeVelFast[axis]));
                ZAux_Direct_SetAccel(_h, axis, (float)(_homeAcc[axis]));
                int rc = ZAux_Direct_Single_Datum(_h, axis, mode);
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_Direct_Datum mode=" + mode);
                return MotionResult.Success("homing started mode=" + mode);
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
                    if (_homing[axis] && Environment.TickCount - _homeStartT[axis] > 200 && (ZmotionIdle(axis))) { _homing[axis] = false; _homed[axis] = true; return MotionResult.Success(); }
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

                ZAux_Direct_SetSpeed(_h, axis, (float)Math.Abs(p.Vel));
                ZAux_Direct_SetAccel(_h, axis, (float)p.Acc);
                ZAux_Direct_SetDecel(_h, axis, (float)p.Dec);
                ApplySramp(axis, p.JerkRatio);
                int rc = (mode == MoveMode.Absolute)
                    ? ZAux_Direct_Single_MoveAbs(_h, axis, (float)to)
                    : ZAux_Direct_Single_Move(_h, axis, (float)(to - from));
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_Direct_Single_MoveAbs");
                return MotionResult.Success();
            }
        }

        public MotionResult MoveJog(int axis, double velUserPerSec, MoveParams p)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis);

                ZAux_Direct_SetSpeed(_h, axis, (float)Math.Abs(velUserPerSec));
                ZAux_Direct_SetAccel(_h, axis, (float)p.Acc);
                ApplySramp(axis, p.JerkRatio);
                int dir = velUserPerSec >= 0 ? 1 : -1;
                int rc = ZAux_Direct_Single_Vmove(_h, axis, dir);
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_Direct_Single_Vmove");
                return MotionResult.Success("jog");
            }
        }

        public MotionResult Stop(int axis, bool emergency)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis);

                int rc = ZAux_Direct_Single_Cancel(_h, axis, emergency ? 2 : 0);   // [核对] option 2=急停
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_Direct_Cancel");
                return MotionResult.Success();
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
                    if (!(!ZmotionIdle(axis))) return MotionResult.Success();
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

                int[] axes = axisList;
                float[] target = new float[axes.Length];
                for (int i = 0; i < axes.Length; i++)
                {
                    double fromI = GetCommandPos(axes[i]);
                    double toI = (mode == MoveMode.Absolute) ? targetUser[i] : fromI + targetUser[i];
                    if (toI < _softNeg[axes[i]] || toI > _softPos[axes[i]])
                        return MotionResult.Fail(-100, "soft limit axis " + axes[i]);
                    target[i] = (float)toI;
                    ZAux_Direct_SetSpeed(_h, axes[i], (float)Math.Abs(p.Vel));
                    ZAux_Direct_SetAccel(_h, axes[i], (float)p.Acc);
                }
                int rc = ZAux_Direct_MoveAbs(_h, axes.Length, axes, target);   // 硬件多轴直线插补
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_Direct_MoveAbs");
                return MotionResult.Success("hw linear interp, axes=" + axes.Length);
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
                // [升级路径] 硬件圆弧: ZAux_Direct_MoveClockwise/CounterClockwise
                return MotionResult.Success("arc done (32-seg linearization)");
            }
        }

        public MotionResult SetDo(int index, bool on)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");

                int rc = ZAux_Direct_SetOp(_h, index, on ? 1 : 0);
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_Direct_SetOp " + index);
                return MotionResult.Success();
            }
        }

        public bool GetDi(int index)
        {
            lock (_sdk)
            {
                if (!_open) return false;

                int v;
                ZAux_Direct_GetIn(_h, index, out v);
                return v != 0;
            }
        }

        public double GetAi(int index)
        {
            lock (_sdk)
            {
                if (!_open) return 0;

                int code;
                ZAux_Direct_GetAD(_h, index, out code);
                return code * 10.0 / 4096.0;   // [核对] 量程换算
            }
        }

        public MotionResult SetAo(int index, double volts)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");

                int code = (int)Math.Round(volts / 10.0 * 4095);   // [核对] 量程换算
                int rc = ZAux_Direct_SetDA(_h, index, code);
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_Direct_SetDa " + index);
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


        private bool ZmotionIdle(int axis)
        {
            int idle;
            ZAux_Direct_GetIfIdle(_h, axis, out idle);
            return idle != 0;
        }
        private static void CheckRange(int axis)
        {
            if (axis < 0 || axis > 7) throw new ArgumentOutOfRangeException("axis", axis, "supports 0-7");
        }

        /// <summary>
        /// 下发 S 曲线平滑时间：JerkRatio(0~1) → <c>ZAux_Direct_SetSramp</c>（**毫秒**）。
        ///
        /// 官方依据（两处一手）：
        ///   ① 官方 C# 绑定 <c>Zmcaux.cs</c> L1459 注释逐字：「S曲线平滑时间设定值 MS」
        ///   ② RTBasic 编程手册 V1.1.6：「smoothms：0-250 毫秒，设置后加减速过程会延长相应的
        ///      时间」「设置时间若超过 250 毫秒，按照 250 毫秒进行平滑」
        ///
        /// 满量程取 250ms（官方区间上界）。JerkRatio=0 → 0ms → 梯形（官方语义：0=梯形）。
        ///
        /// ★ 互斥约束：官方手册明确「设置 JERK 后会**替换** SRAMP」，换算式 SRAMP=ACCEL*1000/JERK。
        ///   本项目走 SRAMP 路线，**因此绝不能同时下发 JERK**，否则两者互相覆盖。
        ///
        /// ⚠ 比例尺（JerkRatio → ms）仍是本项目自选的线性假设，官方未定义「1ms 平滑」对应
        ///   多大的加减速缓和程度。需真机标定，见 tech-debt D7-②。
        ///
        /// 证据：evidence/JERK_UNITS_EVIDENCE.md
        /// </summary>
        private void ApplySramp(int axis, double jerkRatio)
        {
            if (_h == IntPtr.Zero) return;
            float ms;
            if (double.IsNaN(jerkRatio) || jerkRatio <= 0.0) ms = 0f;        // 0 = 梯形
            else if (jerkRatio >= 1.0) ms = SRAMP_MAX_MS;                    // 官方上界 250
            else ms = (float)(jerkRatio * SRAMP_MAX_MS);
            ZAux_Direct_SetSramp(_h, axis, ms);
            _jerkApplied[axis] = ms > 0f;
        }

        /// <summary>官方 SRAMP 上界（毫秒），来源 RTBasic 手册「0-250 毫秒」。</summary>
        private const float SRAMP_MAX_MS = 250f;

    }
}
