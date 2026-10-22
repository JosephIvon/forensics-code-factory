// =====================================================================
//  雷赛 DMC/EMC 系列 (LTDMC) 适配器（LTDMC.dll）
//  生成工具: 上位机代码工厂 v1.0 | 2026-10-20 00:15:59
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

        #region 厂商 API 导入（[核对] = 签名需按手册确认）

        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_board_init();
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_board_close();
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_pmove(ushort card, ushort axis, long pos, ushort mode);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_set_profile(ushort card, ushort axis, double startv, double maxv, double tacc, double tdec, double stopv);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_stop(ushort card, ushort axis, ushort stopMode);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_check_done(ushort card, ushort axis);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern long dmc_get_position(ushort card, ushort axis);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern long dmc_get_encoder(ushort card, ushort axis);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_vmove(ushort card, ushort axis, ushort dir);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_change_speed(ushort card, ushort axis, double newVel);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_home_move(ushort card, ushort axis);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_set_homemode(ushort card, ushort axis, ushort mode, ushort velMode, ushort originDir, ushort ezCount, double lowVel, double highVel);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_read_inbit(ushort card, ushort bitno);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_write_outbit(ushort card, ushort bitno, ushort onoff);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_read_ad(ushort card, ushort ch);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_write_da(ushort card, ushort ch, ushort value);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_set_servo(ushort card, ushort axis, ushort onoff);
        #endregion

        public MotionResult Open(ControllerConnectInfo info)
        {
            lock (_sdk)
            {
                if (_open) return MotionResult.Success("already open");
                _info = info;

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

                // [核对] 脉冲卡(DMC系列)多为硬线使能，无软件使能；总线卡(EMC)用 dmc_set_servo
                ushort sv = on ? (ushort)1 : (ushort)0;
                dmc_set_servo(Card, (ushort)axis, sv);
                return MotionResult.Success("set_servo " + sv + " (脉冲卡请检查硬线使能)");
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
                dmc_set_homemode(Card, (ushort)axis, mode, 0, 0, 1,
                    _homeVelSlow[axis] * _ppu[axis], _homeVelFast[axis] * _ppu[axis]);
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
                dmc_set_profile(card, (ushort)axis, 0, Math.Abs(p.Vel) * _ppu[axis],
                    1000.0 / Math.Max(p.Acc, 1), 1000.0 / Math.Max(p.Dec, 1), 0);   // [核对] tacc/tdec单位ms
                short rc = dmc_pmove(card, (ushort)axis, (long)Math.Round(to * _ppu[axis]), 1);  // [核对] mode 1=绝对(或相反)
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
                dmc_set_profile(Card, (ushort)axis, 0, Math.Abs(velUserPerSec) * _ppu[axis], 50, 50, 0);
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
                    dmc_set_profile(Card, (ushort)ax0, 0, vi * _ppu[ax0], 50, 50, 0);
                    short rc = dmc_pmove(Card, (ushort)ax0, (long)Math.Round(toArr[i] * _ppu[ax0]), 1);
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

                short code = dmc_read_ad(Card, (ushort)index);
                return code * 10.0 / 4096.0;   // [核对] 12bit/10V量程换算
            }
        }

        public MotionResult SetAo(int index, double volts)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");

                ushort code = (ushort)Math.Round(volts / 10.0 * 4095);   // [核对] 12bit/10V量程
                short rc = dmc_write_da(Card, (ushort)index, code);
                if (rc != 0) return MotionResult.Fail(rc, "dmc_write_da " + index);
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

        private ushort Card { get { return (ushort)(_info == null ? 0 : _info.CardNo); } }
    }
}
