// =====================================================================
//  ACS SPiiPlus (SPiiPlusNET) 适配器（ACS.SPiiPlusNET.dll）
//  生成工具: 上位机代码工厂 v1.0 | 2026-10-20 11:35:37
//  KB级别: B —— 基于官方.NET库(安装ACS软件后引用ACS.SPiiPlusNET.dll)，为高级EtherCAT平台(前瞻/力控强)。
//  上真机前按 docs/ApiChecklist.md 核对签名（标注 [核对] 处）
// =====================================================================
using System;
using System.Runtime.InteropServices;
using System.Threading;
using MotionControl.Core;
using ACS.SPiiPlusNET;

namespace MotionControl.Adapters.Acs
{
    /// <summary>
    /// ACS SPiiPlus (SPiiPlusNET) —— IMotionController 适配器。
    /// 设计要点（JD职责3）:
    ///   1. P/Invoke 封装 + 签名核对标记
    ///   2. 单位换算: 用户单位(mm) <-脉冲- ACS.SPiiPlusNET.dll
    ///   3. SDK 调用统一持锁串行化（厂商库多非线程安全）
    ///   4. 错误码统一映射 MotionResult
    /// </summary>
    public class AcsSpiiPlusController : IMotionController
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

        private readonly double[] _homeMethod = new double[8];

        public AcsSpiiPlusController()
        {
            for (int i = 0; i < 8; i++) { _ppu[i] = 10000; _softNeg[i] = -100000; _softPos[i] = 100000; _homeVelFast[i] = 20; _homeVelSlow[i] = 2; _homeAcc[i] = 200; }

            for (int i = 0; i < 8; i++) _homeMethod[i] = 21;   // [核对] HOME方法编号
        }

        public Brand Brand { get { return Brand.Acs; } }
        public string DeviceName { get { return "ACS-SPiiPlus"; } }
        public bool IsOpen { get { return _open; } }

        #region 厂商 API 导入（[核对] = 签名需按手册确认）
        // ── 真值链（每条 P/Invoke 上方：[ 源 | 核对 | 提取 ]）──
        //   ⚠ mcopilot 知识库无 ACS SPiiPlus (SPiiPlusNET) 的绑定口径
        //   本套件全部 P/Invoke 按 [ 源 未标注 | 核对 none | 提取 judgment ] 处理 —— 上线前必须逐条按官方手册核对

        // ACS 使用官方 .NET 托管库，无需 P/Invoke。
        // [部署] 从 ACS 软件安装目录引用 ACS.SPiiPlusNET.dll，
        //        build_net48.bat 需追加: /r:ACS.SPiiPlusNET.dll
        private CommCtrl _ctrl;
        private readonly Axis[] _axesArr = new Axis[8];
        #endregion

        public MotionResult Open(ControllerConnectInfo info)
        {
            lock (_sdk)
            {
                if (_open) return MotionResult.Success("already open");
                _info = info;

                _ctrl = new CommCtrl();
                _ctrl.OpenCommEthernetTCPSocket(info.Ip, (ushort)info.Port);   // [核对] 默认701
                for (int i = 0; i < 8; i++) _axesArr[i] = _ctrl.GetAxis((short)i);
                _open = true;
                return MotionResult.Success("ACS-SPiiPlus opened");
            }
        }

        public MotionResult Close()
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Success();

                if (_ctrl != null) { _ctrl.CloseComm(); _ctrl = null; }
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

                try
                {
                    if (on) _axesArr[axis].Enable(); else _axesArr[axis].Disable();
                    return MotionResult.Success();
                }
                catch (Exception ex) { return MotionResult.Fail(-200, ex.Message); }
            }
        }

        public AxisState GetState(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return AxisState.None;
                CheckRange(axis);

                AxisState s = AxisState.None;
                try
                {
                    MotorStatus ms = _axesArr[axis].MotorStatus;
                    if ((ms & MotorStatus.Enable) != 0) s |= AxisState.Enabled;    // [核对] 位定义
                    if ((ms & MotorStatus.InMotion) != 0) s |= AxisState.Moving;   // [核对]
                    if (_homed[axis]) s |= AxisState.Homed;
                }
                catch { }
                return s;
            }
        }

        public double GetCommandPos(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return 0;
                CheckRange(axis);

                try { return _axesArr[axis].Position / _ppu[axis]; }
                catch { return 0; }
            }
        }

        public double GetFeedbackPos(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return 0;
                CheckRange(axis);

                try { return _axesArr[axis].Position / _ppu[axis]; }
                catch { return 0; }
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

                try
                {
                    _axesArr[axis].SetHomeMethod(_homeMethod[axis]);   // [核对] 方法编号
                    _axesArr[axis].GoHome();
                    return MotionResult.Success("homing started");
                }
                catch (Exception ex) { return MotionResult.Fail(-200, ex.Message); }
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
                    if (_homing[axis] && Environment.TickCount - _homeStartT[axis] > 200 && ((_axesArr[axis].MotorStatus & MotorStatus.HomeComplete) != 0)) { _homing[axis] = false; _homed[axis] = true; return MotionResult.Success(); }
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

                try
                {
                    _axesArr[axis].VelHigh = Math.Abs(p.Vel) * _ppu[axis];   // [核对] 属性名
                    _axesArr[axis].AccHigh = p.Acc * _ppu[axis];             // [核对]
                    _axesArr[axis].MoveAbs(to * _ppu[axis]);
                    return MotionResult.Success();
                }
                catch (Exception ex) { return MotionResult.Fail(-200, ex.Message); }
            }
        }

        public MotionResult MoveJog(int axis, double velUserPerSec, MoveParams p)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis);

                try
                {
                    _axesArr[axis].VelHigh = Math.Abs(velUserPerSec) * _ppu[axis];
                    _axesArr[axis].Jog(velUserPerSec >= 0 ? 1 : -1);   // [核对] Jog重载
                    return MotionResult.Success("jog");
                }
                catch (Exception ex) { return MotionResult.Fail(-200, ex.Message); }
            }
        }

        public MotionResult Stop(int axis, bool emergency)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis);

                try
                {
                    if (emergency) _axesArr[axis].Kill(); else _axesArr[axis].Halt();
                    return MotionResult.Success(emergency ? "kill" : "halt");
                }
                catch (Exception ex) { return MotionResult.Fail(-200, ex.Message); }
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
                    if (!((_axesArr[axis].MotorStatus & MotorStatus.InMotion) != 0)) return MotionResult.Success();
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

                // ACS 多轴插补: MP点/TP点 + _ctrl.MultiAxes? [升级路径] 使用 MSeg/TP运动
                // 此处等时点位法实现（各轴同速规划同到达）
                try
                {
                    double maxDelta = 1e-9;
                    double[] from = new double[axisList.Length];
                    double[] toArr = new double[axisList.Length];
                    for (int i = 0; i < axisList.Length; i++)
                    {
                        from[i] = GetCommandPos(axisList[i]);
                        toArr[i] = (mode == MoveMode.Absolute) ? targetUser[i] : from[i] + targetUser[i];
                        double d = Math.Abs(toArr[i] - from[i]);
                        if (d > maxDelta) maxDelta = d;
                    }
                    double duration = maxDelta / Math.Max(p.Vel, 0.001);
                    for (int i = 0; i < axisList.Length; i++)
                    {
                        int ax0 = axisList[i];
                        double vi = Math.Abs(toArr[i] - from[i]) / Math.Max(duration, 0.001);
                        _axesArr[ax0].VelHigh = vi * _ppu[ax0];
                        _axesArr[ax0].AccHigh = p.Acc * _ppu[ax0];
                        _axesArr[ax0].MoveAbs(toArr[i] * _ppu[ax0]);
                    }
                    return MotionResult.Success("equal-time line");
                }
                catch (Exception ex) { return MotionResult.Fail(-200, ex.Message); }
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
                // [升级路径] ACS 前瞻圆弧: 圆弧MSeg缓冲区
                return MotionResult.Success("arc done (32-seg linearization)");
            }
        }

        public MotionResult SetDo(int index, bool on)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");

                try { _ctrl.SetBit((ushort)index, on ? 1 : 0); return MotionResult.Success(); }
                catch (Exception ex) { return MotionResult.Fail(-200, ex.Message); }
            }
        }

        public bool GetDi(int index)
        {
            lock (_sdk)
            {
                if (!_open) return false;

                try { return _ctrl.GetBit((ushort)index) != 0; }
                catch { return false; }
            }
        }

        public double GetAi(int index)
        {
            lock (_sdk)
            {
                if (!_open) return 0;

                // [TODO] ACS 模拟量按型号补全（CommCtrl模拟量对象/属性）
                return 0;
            }
        }

        public MotionResult SetAo(int index, double volts)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");

                return MotionResult.Fail(-300, "[TODO] ACS AO 按型号补全(见ApiChecklist)");
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

    }
}
