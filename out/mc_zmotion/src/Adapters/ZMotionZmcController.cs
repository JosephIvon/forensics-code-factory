// =====================================================================
//  正运动 ZMC 系列 适配器（zauxdll.dll）
//  生成工具: 上位机代码工厂 v1.0 | 2026-10-20 12:43:08
//  KB级别: B —— 核心API已映射(EtherCAT网络连接/当量/点位/回零/IO/模拟量)。DLL名已按PE导出表改为 zauxdll.dll，ZAux_Direct 系列函数名与参数表已按官方C#绑定/C头文件逐条核对。
//  上真机前按 docs/ApiChecklist.md 核对签名（标注 [核对] 处）
// =====================================================================
using System;
using System.Runtime.InteropServices;
using System.Threading;
using MotionControl.Core;

namespace MotionControl.Adapters.ZMotion
{
    /// <summary>
    /// 正运动 ZMC 系列 —— IMotionController 适配器。
    /// 设计要点（JD职责3）:
    ///   1. P/Invoke 封装 + 签名核对标记
    ///   2. 单位换算: 用户单位(mm) <-脉冲- zauxdll.dll
    ///   3. SDK 调用统一持锁串行化（厂商库多非线程安全）
    ///   4. 错误码统一映射 MotionResult
    /// </summary>
    public class ZMotionZmcController : IMotionController
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

        private IntPtr _h = IntPtr.Zero;

        public ZMotionZmcController()
        {
            for (int i = 0; i < 8; i++) { _ppu[i] = 10000; _softNeg[i] = -100000; _softPos[i] = 100000; _homeVelFast[i] = 20; _homeVelSlow[i] = 2; _homeAcc[i] = 200; }
        }

        public Brand Brand { get { return Brand.ZMotion; } }
        public string DeviceName { get { return "ZMotionZMC"; } }
        public bool IsOpen { get { return _open; } }

        #region 厂商 API 导入（[核对] = 签名需按手册确认）
        // ── 真值链（每条 P/Invoke 上方：[ 源 | 核对 | 提取 ]）──
        //   数据源：mcopilot knowledge/sdks/zmotion.json（生成时实时读取，非复制）
        //   源=verified 这条结论从哪来 ｜ 核对=check 验到哪一步 ｜ 提取=extraction 签名对≠调用对时是否需人签字
        //   本套件 P/Invoke 20 条：知识库钉扎 20 / 未收录(显式降级) 0
        //   核对进度（钉扎条目）：manual 20 / hardware 0 / auto 0 / none 0
        //   复核：mcopilot 仓库跑 scripts/annotate_provenance.py --check；重新生成本套件即自动继承最新口径

        // [真值] DLL 名取自 dll_exports.csv(PE导出表)：官方是 zauxdll.dll，不存在 zmcaux.dll
        // [真值] 函数名/参数表取自 zmotion.csv(官方C#绑定) 与 zmotion_cpp.csv(官方C头文件)
        // [ 源 public-doc | 核对 manual | 提取 judgment ]  Zmcaux.cs:132 · 导出表✓
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern int ZAux_OpenEth([MarshalAs(UnmanagedType.LPStr)] string ipaddr, out IntPtr phandle);
        // [ 源 public-doc | 核对 manual | 提取 mechanical ]  Zmcaux.cs:159 · 导出表✓
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Close(IntPtr handle);
        // [ 源 official-binding | 核对 manual | 提取 judgment ]  Zmcaux.cs:1542 · 导出表✓
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetUnits(IntPtr h, int iaxis, float value);
        // [ 源 official-binding | 核对 manual | 提取 judgment ]  Zmcaux.cs:1442 · 导出表✓
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetSpeed(IntPtr h, int iaxis, float value);
        // [ 源 official-binding | 核对 manual | 提取 judgment ]  Zmcaux.cs:402 · 导出表✓
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetAccel(IntPtr h, int iaxis, float value);
        // [ 源 official-binding | 核对 manual | 提取 judgment ]  Zmcaux.cs:622 · 导出表✓
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetDecel(IntPtr h, int iaxis, float value);
        // [ 源 public-doc | 核对 manual | 提取 mechanical ]  Zmcaux.cs:2515 · 导出表✓
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_Single_MoveAbs(IntPtr h, int iaxis, float fdistance);
        // [ 源 public-doc | 核对 manual | 提取 mechanical ]  Zmcaux.cs:2505 · 导出表✓
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_Single_Move(IntPtr h, int iaxis, float fdistance);
        // [ 源 public-doc | 核对 manual | 提取 judgment ]  Zmcaux.cs:1645 · 导出表✓
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_MoveAbs(IntPtr h, int imaxaxises, int[] piAxislist, float[] pfDposlist);
        // [ 源 public-doc | 核对 manual | 提取 judgment ]  Zmcaux.cs:2475 · 导出表✓
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_Single_Vmove(IntPtr h, int iaxis, int idir);
        // [ 源 public-doc | 核对 manual | 提取 judgment ]  Zmcaux.cs:2465 · 导出表✓
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_Single_Cancel(IntPtr h, int iaxis, int imode);
        // [ 源 public-doc | 核对 manual | 提取 judgment ]  Zmcaux.cs:2485 · 导出表✓
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_Single_Datum(IntPtr h, int iaxis, int imode);
        // [ 源 public-doc | 核对 manual | 提取 judgment ]  Zmcaux.cs:1212 · 导出表✓
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_GetMpos(IntPtr h, int iaxis, out float pfValue);
        // [ 源 public-doc | 核对 manual | 提取 judgment ]  Zmcaux.cs:672 · 导出表✓
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_GetDpos(IntPtr h, int iaxis, out float pfValue);
        // [ 源 public-doc | 核对 manual | 提取 judgment ]  Zmcaux.cs:982 · 导出表✓
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_GetIfIdle(IntPtr h, int iaxis, out int piValue);
        // [ 源 official-binding | 核对 manual | 提取 judgment ]  Zmcaux.cs:208 · 导出表✓
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_GetIn(IntPtr h, int ionum, out uint piValue);
        // [ 源 public-doc | 核对 manual | 提取 judgment ]  Zmcaux.cs:218 · 导出表✓
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetOp(IntPtr h, int ionum, uint iValue);
        // [ 源 official-binding | 核对 manual | 提取 judgment ]  Zmcaux.cs:238 · 导出表✓
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_GetAD(IntPtr h, int ionum, out float pfValue);
        // [ 源 official-binding | 核对 manual | 提取 judgment ]  Zmcaux.cs:248 · 导出表✓
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetDA(IntPtr h, int ionum, float fValue);
        // [ 源 official-binding | 核对 manual | 提取 judgment ]  Zmcaux.cs:502 · 导出表✓
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetAxisEnable(IntPtr h, int iaxis, int iValue);
        #endregion

        public MotionResult Open(ControllerConnectInfo info)
        {
            lock (_sdk)
            {
                if (_open) return MotionResult.Success("already open");
                _info = info;

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

                // [真值] 官方使能接口 ZAux_Direct_SetAxisEnable(IntPtr handle,int iaxis,int iValue)
                int rc = ZAux_Direct_SetAxisEnable(_h, axis, on ? 1 : 0);
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_Direct_SetAxisEnable");
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
                ZAux_Direct_GetMpos(_h, axis, out pos);
                return pos;
            }
        }

        public double GetFeedbackPos(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return 0;
                CheckRange(axis);

                // [真值] 反馈位置用官方 ZAux_Direct_GetDpos（指令位置是 GetMpos）
                float pos;
                ZAux_Direct_GetDpos(_h, axis, out pos);
                return pos;
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

                // [真值] 官方回零接口 ZAux_Direct_Single_Datum(IntPtr handle,int iaxis,int imode)
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
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_Direct_Single_Datum mode=" + mode);
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
                int rc = (mode == MoveMode.Absolute)
                    ? ZAux_Direct_Single_MoveAbs(_h, axis, (float)to)
                    : ZAux_Direct_Single_Move(_h, axis, (float)(to - from));
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_Direct_Single_Move");
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

                // [真值] 官方停止接口 ZAux_Direct_Single_Cancel(IntPtr handle,int iaxis,int imode)
                int rc = ZAux_Direct_Single_Cancel(_h, axis, emergency ? 2 : 0);   // [核对] imode 2=急停
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_Direct_Single_Cancel");
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
                // [升级路径] 硬件圆弧: 官方 ZAux_Direct_MoveCircAbs / ZAux_Direct_MoveCirc（zmotion.csv:203/198）
                return MotionResult.Success("arc done (32-seg linearization)");
            }
        }

        public MotionResult SetDo(int index, bool on)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");

                int rc = ZAux_Direct_SetOp(_h, index, on ? 1u : 0u);
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_Direct_SetOp " + index);
                return MotionResult.Success();
            }
        }

        public bool GetDi(int index)
        {
            lock (_sdk)
            {
                if (!_open) return false;

                uint v;
                ZAux_Direct_GetIn(_h, index, out v);
                return v != 0;
            }
        }

        public double GetAi(int index)
        {
            lock (_sdk)
            {
                if (!_open) return 0;

                // [真值] 官方 ZAux_Direct_GetAD(IntPtr,int ionum,ref float pfValue)
                float code;
                ZAux_Direct_GetAD(_h, index, out code);
                return code * 10.0 / 4096.0;   // [核对] 量程换算：真值未给出AD量程，按手册确认
            }
        }

        public MotionResult SetAo(int index, double volts)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");

                // [真值] 官方 ZAux_Direct_SetDA(IntPtr,int ionum,float fValue)
                int code = (int)Math.Round(volts / 10.0 * 4095);   // [核对] 量程换算：真值未给出DA量程，按手册确认
                int rc = ZAux_Direct_SetDA(_h, index, code);
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_Direct_SetDA " + index);
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

    }
}
