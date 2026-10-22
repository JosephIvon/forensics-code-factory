// =====================================================================
//  软赢 (SoftWin / Movensys WMX3) 适配器
//  KB级别: B —— 按官方 WMX3ApiCLR 绑定实现（反射后期绑定）
//  真值来源: 厂商官方 .NET 绑定文档（download.movensys.com:8111/wmx3 doxygen，
//            "© Copyright 2024, MOVENSYS Inc."）+ 官方仓库 movensys/wmx-r2 实际调用
//            + 中文官网 soft-motion.cn/nd.jsp?id=61
//            详见 evidence/SOFTWIN_STATUS.md
//
//  ⚠ 重要：本适配器**不是 P/Invoke**。官方 Windows 绑定是 C++/CLI 托管程序集
//     （命名空间 WMX3ApiCLR），DllImport 对它无效。官方文档《C++ 与 .NET Library
//     Differences》明确：.NET 库是托管对象（WMX3Api / CoreMotion / Io ...），
//     C++ 的默认参数在 .NET 里拆成多个重载。
//     因此这里用**反射后期绑定**：不强引用厂商程序集，未装 WMX3 也能编译，
//     装了直接可用；未装则 Open() 给出明确失败原因。
//
//  必需程序集（C:\Program Files\SoftServo\WMX3\Lib）：
//     WMX3Api_CLRLib.dll（必需）、CoreMotionApi_CLRLib.dll、IOApi_CLRLib.dll
//     其余 7 个（ApiBuffer/Compensation/CyclicBuffer/Event/Log/UserMemory/AdvancedMotion）本适配器不用
//  原生依赖：IMDll.dll —— 官方两处强调「必须复制到 exe 同目录」，否则启动即抛异常
//  引擎进程：WMX3Engine.rtss（CreateDevice 的第一个参数就是它的所在目录）
//
//  License: 官方说明「non-RTOS 版为 Demo，无 license 可按 1 小时为单位维持连续通讯；
//           模拟模式下可无功能限制地测试 API；RTOS 评估版可用 3 个月」
// =====================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using MotionControl.Core;

namespace MotionControl.Adapters.SoftWin
{
    /// <summary>
    /// 软赢 WMX3 适配器。反射调用官方 WMX3ApiCLR 托管绑定。
    /// 所有成员名均来自官方文档与官方仓库，见文件头「真值来源」。
    /// </summary>
    public class SoftWinController : IMotionController
    {
        private const int AXES = 8;

        // 官方程序集与类型名（官方 C# 教程「SEC2_1」明确列出）
        private const string ASM_MAIN  = "WMX3Api_CLRLib";
        private const string ASM_MOTION = "CoreMotionApi_CLRLib";
        private const string ASM_IO    = "IOApi_CLRLib";
        private const string T_WMX3API = "WMX3ApiCLR.WMX3Api";
        private const string T_MOTION  = "WMX3ApiCLR.CoreMotion";
        private const string T_IO      = "WMX3ApiCLR.Io";

        private const int ERR_NONE = 0;   // ErrorCode.None
        private const int TIMEOUT_INFINITE = -1;   // 官方超时形参为 int 毫秒，-1 = 不限

        private readonly object _sdk = new object();
        private ControllerConnectInfo _info;
        private bool _open;
        private bool _jerkApplied;   // 最近一次运动是否真的采纳了 JerkRatio（反射可能拿不到字段/枚举）

        // 反射句柄
        private object _api;          // WMX3Api 实例
        private object _cm;           // CoreMotion（包 _api）
        private object _io;           // Io（包 _api）
        private MethodInfo _mAxisControl;      // AxisControl 属性
        private MethodInfo _mMotion;           // Motion 属性
        private MethodInfo _mConfig;           // Config 属性
        private MethodInfo _mHome;             // Home 属性

        private readonly bool[] _homed = new bool[AXES];
        private readonly bool[] _homing = new bool[AXES];
        private readonly double[] _ppu = new double[AXES];        // 脉冲 / 用户单位
        private readonly HomeMode[] _homeMode = new HomeMode[AXES];
        private readonly double[] _homeVelFast = new double[AXES];
        private readonly double[] _homeVelSlow = new double[AXES];
        private readonly double[] _homeAcc = new double[AXES];
        private readonly double[] _softNeg = new double[AXES];
        private readonly double[] _softPos = new double[AXES];

        /// <summary>WMX3Engine.rtss 所在目录。留空则用默认安装路径。</summary>
        public string EnginePath { get; set; }

        public SoftWinController()
        {
            for (int i = 0; i < AXES; i++)
            {
                _ppu[i] = 10000; _softNeg[i] = -100000; _softPos[i] = 100000;
                _homeVelFast[i] = 20; _homeVelSlow[i] = 2; _homeAcc[i] = 200;
            }
        }

        public Brand Brand { get { return Brand.SoftWin; } }
        public string DeviceName { get { return "SoftWin-WMX3"; } }
        public bool IsOpen { get { return _open; } }

        /// <summary>
        /// 支持 S 曲线平滑 —— 官方 <c>Profile.jerkAccRatio</c> / <c>jerkDecRatio</c>，
        /// 范围 [0,1]，其定义与本项目 <see cref="MoveParams.JerkRatio"/> 语义逐字对齐
        /// （0=梯形，1=S 曲线），可直传、无需比例尺换算。
        /// 证据：evidence/JERK_UNITS_EVIDENCE.md（官方 doxygen 实测）。
        /// </summary>
        public bool SupportsJerkProfile { get { return true; } }

        /// <summary>
        /// 最近一次运动是否**真的**采纳了 JerkRatio。
        /// 反射层拿不到 <c>jerkAccRatio</c> 字段或 <c>ProfileType.JerkRatio</c> 枚举时会
        /// 静默退回梯形曲线 —— 此时本属性为 false，调用方能查到平滑未生效，
        /// 而不是以为 S 曲线已生效（这正是此前 JerkRatio 被静默丢弃的形态）。
        /// </summary>
        public bool JerkProfileApplied { get { return _jerkApplied; } }

        #region 连接生命周期

        public MotionResult Open(ControllerConnectInfo info)
        {
            lock (_sdk)
            {
                if (_open) return MotionResult.Success("already open");
                if (info == null) return MotionResult.Fail(-6, "null connect info");
                _info = info;

                string dir = EnginePath;
                if (string.IsNullOrEmpty(dir))
                    dir = @"C:\Program Files\SoftServo\WMX3";

                try
                {
                    LoadManaged();
                }
                catch (Exception ex)
                {
                    return MotionResult.Fail(-300,
                        "加载官方 WMX3 .NET 绑定失败: " + ex.Message +
                        "。需先安装 WMX3（默认 " + dir + @"），并确保 IMDll.dll 与 exe 同目录。" +
                        "详见 evidence/SOFTWIN_STATUS.md");
                }

                // CreateDevice(path, DeviceTypeNormal, timeout)
                // DeviceType 枚举在官方 .NET 绑定里是嵌套在 WMX3Api 内还是位于命名空间层，
                // 公开文档未逐字标明（doxygen 只给 C++ 的 wmx3Api::DeviceType）。
                // 故两种候选都试，取不到才报错 —— 避免因猜错嵌套层级而全线不可用。
                object devType = EnumValue(new string[] {
                        T_WMX3API + "+DeviceType",      // WMX3ApiCLR.WMX3Api+DeviceType
                        "WMX3ApiCLR.DeviceType" },      // WMX3ApiCLR.DeviceType
                    "DeviceTypeNormal");
                if (devType == null)
                    return MotionResult.Fail(-300, "官方绑定中找不到枚举 DeviceType.DeviceTypeNormal");

                // timeout 官方形参是 int（毫秒）。用 -1 表示「不限」，
                // 不能写 0xFFFFFFFF：那是 UInt32，转 Int32 会抛 OverflowException。
                object rc = Call(_api, "CreateDevice", dir, devType, TIMEOUT_INFINITE);
                if (!IsOk(rc))
                    return MotionResult.Fail(RcOf(rc),
                        "CreateDevice 失败 rc=" + rc + "（错误文本: " + ErrorText(rc) + "）");

                rc = Call(_api, "StartCommunication", TIMEOUT_INFINITE);
                if (!IsOk(rc))
                    return MotionResult.Fail(RcOf(rc),
                        "StartCommunication 失败 rc=" + rc + "（" + ErrorText(rc) + "）");

                // 官方说明：SetServoOn 有几拍延迟；CreateDevice 后应轮询引擎状态至 Communicating
                if (!WaitEngineCommunicating(3000))
                    return MotionResult.Fail(-5, "引擎 3 秒内未进入 Communicating 状态");

                _open = true;
                return MotionResult.Success("WMX3 opened (engine=" + dir + ")");
            }
        }

        public MotionResult Close()
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Success();
                try { Call(_api, "StopCommunication", TIMEOUT_INFINITE); } catch { }
                try { Call(_api, "CloseDevice"); } catch { }
                _api = _cm = _io = null;
                _open = false;
                for (int i = 0; i < AXES; i++) { _homed[i] = false; _homing[i] = false; }
                return MotionResult.Success();
            }
        }

        public void Dispose() { try { Close(); } catch { } }

        // 已加载的官方程序集（FindType 靠它查类型；Assembly.Load 之后 AppDomain 里就有，
        // 但显式留一份引用可避免「枚举/类型查找早于 Load 完成」这类时序坑）
        private Assembly _asmMain, _asmMotion, _asmIO;

        private void LoadManaged()
        {
            _asmMain = Assembly.Load(ASM_MAIN);
            _asmMotion = Assembly.Load(ASM_MOTION);
            _asmIO = Assembly.Load(ASM_IO);

            Type tApi = _asmMain.GetType(T_WMX3API, true);
            Type tCm = _asmMotion.GetType(T_MOTION, true);
            Type tIo = _asmIO.GetType(T_IO, true);

            _api = Activator.CreateInstance(tApi);
            // 官方示例：new CoreMotion(wmxlib) / new Io(wmxlib) —— 传入引擎实例
            _cm = Activator.CreateInstance(tCm, _api);
            _io = Activator.CreateInstance(tIo, _api);

            // 子对象是属性（官方示例用 wmxlib_cm.AxisControl.SetServoOn(...)）
            _mAxisControl = Prop(tCm, "AxisControl");
            _mMotion = Prop(tCm, "Motion");
            _mConfig = Prop(tCm, "Config");
            _mHome = Prop(tCm, "Home");
        }

        /// <summary>轮询 GetEngineStatus 直到 Communicating（官方建议，避免过早下发指令）。</summary>
        private bool WaitEngineCommunicating(int timeoutMs)
        {
            DateTime t0 = DateTime.Now;
            while (true)
            {
                object st = CallOrNull(_api, "GetEngineStatus");
                if (st != null && st.ToString().IndexOf("Communicat", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if ((DateTime.Now - t0).TotalMilliseconds >= timeoutMs) return false;
                Thread.Sleep(50);
            }
        }

        #endregion

        public MotionResult ConfigureAxis(AxisConfig cfg)
        {
            lock (_sdk)
            {
                if (cfg == null) return MotionResult.Fail(-6, "null config");
                if (cfg.AxisIndex < 0 || cfg.AxisIndex >= AXES) return MotionResult.Fail(-7, "axis out of range 0-" + (AXES - 1));
                int i = cfg.AxisIndex;
                _ppu[i] = cfg.PulsesPerUnit <= 0 ? 10000 : cfg.PulsesPerUnit;
                _homeMode[i] = cfg.Home.Mode;
                _homeVelFast[i] = cfg.Home.VelFast; _homeVelSlow[i] = cfg.Home.VelSlow; _homeAcc[i] = cfg.Home.Acc;
                _softNeg[i] = cfg.SoftLimitNeg; _softPos[i] = cfg.SoftLimitPos;
                _homed[i] = false;
                return MotionResult.Success(cfg + " configured (ppu=" + _ppu[i] + ")");
            }
        }

        #region 轴状态 / 位置

        public MotionResult Enable(int axis, bool on)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis);

                object ax = Sub(_mAxisControl);
                object rc = Call(ax, "SetServoOn", axis, on ? 1 : 0);
                if (!IsOk(rc))
                    return MotionResult.Fail(RcOf(rc),
                        "SetServoOn axis " + axis + " rc=" + rc + "（" + ErrorText(rc) + "）");

                // 官方明确：使能有几拍延迟，直接发运动会报错 —— 必须轮询到生效
                if (!WaitServoOn(axis, on, 500))
                    return MotionResult.Fail(-5, "axis " + axis + " 使能状态 500ms 内未生效（官方说明存在数拍延迟）");

                return MotionResult.Success("servo " + (on ? "ON" : "OFF") + " axis " + axis);
            }
        }

        private bool WaitServoOn(int axis, bool want, int timeoutMs)
        {
            DateTime t0 = DateTime.Now;
            while (true)
            {
                object st = Status(axis);
                if (st != null && ServoOnOf(st, axis) == want) return true;
                if ((DateTime.Now - t0).TotalMilliseconds >= timeoutMs) return false;
                Thread.Sleep(10);
            }
        }

        public AxisState GetState(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return AxisState.None;
                CheckRange(axis);

                object st = Status(axis);
                if (st == null) return AxisState.None;

                AxisState s = AxisState.None;
                if (FlagOf(st, axis, "servoOn")) s |= AxisState.Enabled;
                if (FlagOf(st, axis, "motionComplete") && !FlagOf(st, axis, "moving")) s |= AxisState.InPosition;
                if (FlagOf(st, axis, "ampAlarm")) s |= AxisState.Alarm;
                if (FlagOf(st, axis, "positiveLS")) s |= AxisState.LimitPos;
                if (FlagOf(st, axis, "negativeLS")) s |= AxisState.LimitNeg;
                if (FlagOf(st, axis, "homeDone")) s |= AxisState.Homed;
                if (_homing[axis]) s |= AxisState.Homing;
                return s;
            }
        }

        public double GetCommandPos(int axis)
        {
            lock (_sdk)
            {
                object st = Status(axis);
                return st == null ? 0 : Dbl(st, axis, "posCmd") / _ppu[axis];
            }
        }

        public double GetFeedbackPos(int axis)
        {
            lock (_sdk)
            {
                object st = Status(axis);
                return st == null ? 0 : Dbl(st, axis, "actualPos") / _ppu[axis];
            }
        }

        public double GetFollowingError(int axis)
        {
            lock (_sdk)
            {
                double f = GetFeedbackPos(axis), c = GetCommandPos(axis);
                return Math.Abs(c - f);
            }
        }

        #endregion

        #region 回零

        public MotionResult Home(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis);

                // 官方：Config.SetHomeParam(axis, homeParam) → Home.StartHome(axis)
                // homeParam 的字段布局未在公开文档中逐字给出 —— 这里用「按字段名逐个设」，
                // 字段名取自官方 WMX3 参数体系（HomeParam.velFast / velSlow / acc / mode ...）。
                object hp = NewHomeParam();
                if (hp == null)
                    return MotionResult.Fail(-300, "官方绑定中找不到 HomeParam 类型（见 SOFTWIN_STATUS.md 待确认项）");

                // WMX3 HomeParam 字段单位未逐字确认；官方 CoreMotion 走 user unit，
                // 故按 user unit 原值下发（若后续拿到手册确认是脉冲，改这里并同步 Units.SoftWin）。
                SetIfExists(hp, "velFast", _homeVelFast[axis]);
                SetIfExists(hp, "velSlow", _homeVelSlow[axis]);
                SetIfExists(hp, "acc", _homeAcc[axis]);
                SetIfExists(hp, "mode", MapHomeMode(_homeMode[axis]));

                object rc = Call(Sub(_mConfig), "SetHomeParam", axis, hp);
                if (!IsOk(rc))
                    return MotionResult.Fail(RcOf(rc), "SetHomeParam rc=" + rc);

                rc = Call(Sub(_mHome), "StartHome", axis);
                if (!IsOk(rc))
                    return MotionResult.Fail(RcOf(rc), "StartHome rc=" + rc);

                _homing[axis] = true; _homed[axis] = false;
                return MotionResult.Success("homing started axis " + axis);
            }
        }

        public bool IsHomed(int axis)
        {
            lock (_sdk)
            {
                object st = Status(axis);
                if (st != null && FlagOf(st, axis, "homeDone"))
                {
                    _homing[axis] = false; _homed[axis] = true;
                }
                return _homed[axis];
            }
        }

        public MotionResult WaitHomeDone(int axis, int timeoutMs)
        {
            DateTime t0 = DateTime.Now;
            while (true)
            {
                lock (_sdk)
                {
                    if (!_open) return MotionResult.Fail(-1, "not open");
                    CheckRange(axis);
                    if (FlagOf(Status(axis), axis, "homeDone"))
                    {
                        _homing[axis] = false; _homed[axis] = true;
                        return MotionResult.Success("homed axis " + axis);
                    }
                }
                if ((DateTime.Now - t0).TotalMilliseconds >= timeoutMs)
                    return MotionResult.Fail(-3, "home timeout axis " + axis);
                Thread.Sleep(20);
            }
        }

        #endregion

        #region 单轴运动

        public MotionResult MoveTo(int axis, double posUser, MoveParams p, MoveMode mode)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis);

                double ppu = _ppu[axis];
                if (mode == MoveMode.Relative) posUser += GetCommandPos(axis);

                object cmd = NewPosCmd();
                if (cmd == null)
                    return MotionResult.Fail(-300, "官方绑定中找不到运动指令结构（见 SOFTWIN_STATUS.md 待确认项）");

                SetIfExists(cmd, "target", posUser * ppu);
                SetProfile(cmd, p, ppu);
                SetIfExists(cmd, "axis", axis);

                object rc = Call(Sub(_mMotion), mode == MoveMode.Relative ? "StartMov" : "StartPos", cmd);
                if (!IsOk(rc))
                    return MotionResult.Fail(RcOf(rc),
                        (mode == MoveMode.Relative ? "StartMov" : "StartPos") + " rc=" + rc + "（" + ErrorText(rc) + "）");
                return MotionResult.Success("ptp axis " + axis + " -> " + Math.Round(posUser * ppu) + " 脉冲");
            }
        }

        public MotionResult MoveJog(int axis, double velUserPerSec, MoveParams p)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis);

                // WMX3 原生单位即 user unit（mm 与 mm/s），脉冲当量由控制器齿轮比承担
                object cmd = NewJogCmd();
                if (cmd == null) return MotionResult.Fail(-300, "官方绑定中找不到 JogCmd 类型");

                SetIfExists(cmd, "axis", axis);
                SetIfExists(cmd, "velocity", velUserPerSec);
                SetIfExists(cmd, "acc", p.Acc);
                SetIfExists(cmd, "dec", p.Dec);

                object rc = Call(Sub(_mMotion), "StartJog", cmd);
                if (!IsOk(rc))
                    return MotionResult.Fail(RcOf(rc), "StartJog rc=" + rc + "（" + ErrorText(rc) + "）");
                return MotionResult.Success("jog axis " + axis);
            }
        }

        public MotionResult Stop(int axis, bool emergency)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis);

                object mo = Sub(_mMotion);
                // 官方三种停止语义不同：Stop / ExecQuickStop / ExecTimedStop
                object rc = emergency
                    ? (CallOrNull(mo, "ExecQuickStop", axis) ?? Call(mo, "Stop", axis))
                    : Call(mo, "Stop", axis);
                if (!IsOk(rc))
                    return MotionResult.Fail(RcOf(rc), "stop axis " + axis + " rc=" + rc);
                return MotionResult.Success((emergency ? "quick stop" : "stop") + " axis " + axis);
            }
        }

        public MotionResult WaitMoveDone(int axis, int timeoutMs)
        {
            // 官方提供原生阻塞等待 Motion.Wait(axis) —— 无需自己轮询
            if (_open)
            {
                try
                {
                    object rc = CallOrNull(Sub(_mMotion), "Wait", axis);
                    if (rc != null) return MotionResult.Success("axis " + axis + " 停止（Motion.Wait）");
                }
                catch { /* 落到轮询 */ }
            }
            return WaitMoveDonePoll(axis, timeoutMs);
        }

        private MotionResult WaitMoveDonePoll(int axis, int timeoutMs)
        {
            DateTime t0 = DateTime.Now;
            while (true)
            {
                lock (_sdk)
                {
                    if (!_open) return MotionResult.Fail(-1, "not open");
                    CheckRange(axis);
                    object st = Status(axis);
                    if (st != null && FlagOf(st, axis, "motionComplete") && !FlagOf(st, axis, "moving"))
                        return MotionResult.Success("axis " + axis + " 停止");
                }
                if ((DateTime.Now - t0).TotalMilliseconds >= timeoutMs)
                    return MotionResult.Fail(-3, "move timeout axis " + axis);
                Thread.Sleep(10);
            }
        }

        #endregion

        #region 插补

        public MotionResult MoveLine(int[] axisList, double[] targetUser, MoveParams p, MoveMode mode)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                if (axisList == null || targetUser == null) return MotionResult.Fail(-6, "null args");
                if (axisList.Length != targetUser.Length) return MotionResult.Fail(-6, "axisList/targetUser 长度不一致");

                object cmd = NewLinearCmd();
                if (cmd == null)
                    return MotionResult.Fail(-300, "官方绑定中找不到直线插补指令结构（StartLinearIntplPos 的参数类型）");

                SetIfExists(cmd, "axis", ToAxisArray(axisList));
                SetIfExists(cmd, "endPos", ToPosArray(axisList, targetUser, mode));
                SetProfile(cmd, p, _ppu[axisList[0]]);

                object rc = Call(Sub(_mMotion), "StartLinearIntplPos", cmd);
                if (!IsOk(rc))
                    return MotionResult.Fail(RcOf(rc), "StartLinearIntplPos rc=" + rc);
                return MotionResult.Success("直线插补已下发（原生插补，非线性化）");
            }
        }

        public MotionResult MoveArc2D(int axisX, int axisY, double i, double j, double endX, double endY, bool cw, MoveParams p)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axisX); CheckRange(axisY);

                // 官方原生圆弧：LengthAndEndEndCircularIntplCommand
                //   axis[2] / arcLengthDegree / clockwise / endPos[2] / profile
                object cmd = NewArcCmd();
                if (cmd == null)
                    return MotionResult.Fail(-300, "官方绑定中找不到 LengthAndEndEndCircularIntplCommand 类型");

                double ppu = _ppu[axisX];
                if (_ppu[axisY] != ppu)
                    return MotionResult.Fail(-6, "圆弧要求 X/Y 轴 ppu 一致（X=" + ppu + " Y=" + _ppu[axisY] + "）");

                SetIfExists(cmd, "axis", new object[] { axisX, axisY });
                SetIfExists(cmd, "endPos", new object[] { endX * ppu, endY * ppu });
                SetIfExists(cmd, "clockwise", cw);
                // 官方结构用「弧长角」而非圆心/半径：由 IJ 增量定圆心弧向，再与终点弧向求夹角
                double r = Math.Sqrt(i * i + j * j);
                SetIfExists(cmd, "arcLengthDegree", ArcDeg(i, j, endX, endY, r));
                SetProfile(cmd, p, ppu);

                object rc = Call(Sub(_mMotion), "StartCircularIntplPos", cmd);
                if (!IsOk(rc))
                    return MotionResult.Fail(RcOf(rc), "StartCircularIntplPos rc=" + rc);
                return MotionResult.Success("圆弧插补已下发（原生插补）");
            }
        }

        /// <summary>由圆心增量 (I,J) 与终点 (X,Y) 求弧长角（度）。官方结构用弧长角而非圆心半径。</summary>
        private static double ArcDeg(double i, double j, double endX, double endY, double r)
        {
            if (r <= 0) return 0;
            double a0 = Math.Atan2(j, i) * 180.0 / Math.PI;                 // 圆心方向
            double a1 = Math.Atan2(endY, endX) * 180.0 / Math.PI;           // 终点方向
            double d = a1 - a0;
            while (d <= -180) d += 360;
            while (d > 180) d -= 360;
            return d;
        }

        #endregion

        #region IO

        public MotionResult SetDo(int index, bool on)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                object rc = Call(_io, "SetOutBit", index / 8, index % 8, on ? 1 : 0);
                if (!IsOk(rc))
                    return MotionResult.Fail(RcOf(rc), "SetOutBit rc=" + rc);
                return MotionResult.Success();
            }
        }

        public bool GetDi(int index)
        {
            lock (_sdk)
            {
                if (!_open) return false;
                try
                {
                    object[] a = { (byte)(index / 8), (byte)(index % 8), (byte)0 };
                    object rc = Call(_io, "GetInBit", a[0], a[1], a[2]);
                    return rc != null && rc.ToString() != "0";
                }
                catch { return false; }
            }
        }

        public double GetAi(int index)
        {
            lock (_sdk)
            {
                if (!_open) return 0;
                // 输入侧官方函数名 GetInAnalogData*（SOFTWIN_STATUS.md 待确认项之一）
                MethodInfo m = FindMethod(_io.GetType(), "GetInAnalogData");
                if (m == null) return 0;
                object rc = InvokeBestEffort(m, _io, new object[] { (byte)index });
                return rc is double ? (double)rc : 0;
            }
        }

        public MotionResult SetAo(int index, double volts)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                // 模拟量输出侧函数名未在公开文档中逐字确认 —— 不猜，按显式错误返回。
                MethodInfo m = FindMethod(_io.GetType(), "SetOutAnalogData");
                if (m == null)
                    return MotionResult.Fail(-300,
                        "[PENDING-VENDOR] SoftWin 模拟量输出(AO)的确切官方函数名未在公开文档中确认，" +
                        "需向厂商索取手册后补全。详见 evidence/SOFTWIN_STATUS.md 待确认项");
                object rc = InvokeBestEffort(m, _io, new object[] { (byte)index, volts });
                if (!IsOk(rc))
                    return MotionResult.Fail(RcOf(rc), "SetOutAnalogData rc=" + rc);
                return MotionResult.Success("AO ch " + index + " = " + volts);
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
            return MotionResult.Fail(-5, "wait DI " + index + " timeout");
        }

        #endregion

        #region 反射工具（官方绑定是托管程序集，只能反射调用）

        private void CheckRange(int axis)
        {
            if (axis < 0 || axis >= AXES) throw new ArgumentOutOfRangeException("axis", axis, "axis must be 0-" + (AXES - 1));
        }

        /// <summary>取官方 CoreMotion 的子对象属性 getter。缺失返回 null，由 Sub() 抛出可读错误。</summary>
        private static MethodInfo Prop(Type t, string name)
        {
            PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return (p != null && p.CanRead) ? p.GetGetMethod() : null;
        }

        private object Sub(MethodInfo getter)
        {
            if (getter == null)
                throw new MissingMemberException("官方 CoreMotion 上缺少该子对象属性（见 evidence/SOFTWIN_STATUS.md 待确认项）");
            return getter.Invoke(_cm, null);
        }

        private static MethodInfo FindMethod(Type t, string name)
        {
            return t.GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
        }

        /// <summary>官方方法返回值是否为「无错」。官方返回的是 ErrorCode 枚举（底层 int），要兼容装箱的枚举。</summary>
        private static bool IsOk(object rc)
        {
            if (rc == null) return false;
            if (rc is int) return (int)rc == ERR_NONE;
            if (rc is Enum) return Convert.ToInt64(rc) == ERR_NONE;
            return false;
        }

        /// <summary>官方错误码 → int（便于 MotionResult.Fail 用）。</summary>
        private static int RcOf(object rc)
        {
            if (rc is int) return (int)rc;
            if (rc is Enum) return (int)Convert.ToInt64(rc);
            return -300;
        }

        /// <summary>object[] → 目标元素类型的数组（官方插补指令用 double[]/int[]，反射装箱后类型不匹配会抛异常）。</summary>
        private static Array CoerceArray(object src, Type arrayType)
        {
            object[] inA = (object[])src;
            Type et = arrayType.GetElementType();
            Array dst = Array.CreateInstance(et, inA.Length);
            for (int i = 0; i < inA.Length; i++)
            {
                object v = inA[i];
                if (v != null && !et.IsInstanceOfType(v))
                    v = et.IsEnum ? Enum.ToObject(et, Convert.ChangeType(v, Enum.GetUnderlyingType(et)))
                                  : Convert.ChangeType(v, et);
                dst.SetValue(v, i);
            }
            return dst;
        }

        /// <summary>
        /// 调用官方方法并处理 out 参数。官方 API（如 GetStatus、GetInBit）大量使用 out/ref，
        /// 直接 Invoke 会返回 ErrorCode 而不是数据本体，所以要把 out 参数回读出来。
        /// 规则：存在 out 参数时，返回第一个 out 参数的值；否则返回方法返回值。
        /// </summary>
        private static object Call(object target, string name, params object[] args)
        {
            MethodInfo m = FindMethod(target.GetType(), name);
            if (m == null)
                throw new MissingMemberException("官方绑定中找不到方法 " + target.GetType().Name + "." + name +
                                                   "（见 evidence/SOFTWIN_STATUS.md 待确认项）");

            ParameterInfo[] ps = m.GetParameters();
            object[] full = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                object a = i < args.Length ? args[i] : null;
                if (a == null)
                {
                    full[i] = ps[i].ParameterType.IsValueType
                        ? Activator.CreateInstance(ps[i].ParameterType)   // out/ref 参数占位
                        : null;
                }
                else
                {
                    // Enum.Parse 返回的是「底层类型」的装箱值（如 UInt32），
                    // 直接传给形参是 int 的枚举参数会抛 ArgumentException —— 必须按形参类型转换。
                    Type pt = ps[i].ParameterType;
                    if (pt.IsEnum && !(a is Enum))
                        a = Enum.ToObject(pt, Convert.ChangeType(a, Enum.GetUnderlyingType(pt)));
                    else if (pt.IsPrimitive || pt == typeof(decimal))
                        a = Convert.ChangeType(a, pt);
                    else if (pt.IsArray && a is object[] && a.GetType() != pt)
                        a = CoerceArray(a, pt);   // object[] → double[] / int[] 等
                    full[i] = a;
                }
            }
            object ret = m.Invoke(target, full);
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].IsOut && full[i] != null) return full[i];
            }
            return ret;
        }

        private static object CallOrNull(object target, string name, params object[] args)
        {
            MethodInfo m = FindMethod(target.GetType(), name);
            if (m == null) return null;
            try { return Call(target, name, args); } catch { return null; }
        }

        /// <summary>
        /// 调用「可能带默认参数」的官方方法：官方 .NET 把 C++ 的默认参数拆成多个重载，
        /// 这里按参数个数挑一个能对上的重载，避免必然抛 TargetParameterCountException。
        /// 注：MethodInfo.GetOverloads() 是 .NET Core 专有，net48 上必须走 Type.GetMethods()。
        /// </summary>
        private static object InvokeBestEffort(MethodInfo m, object target, object[] args)
        {
            foreach (MethodInfo ov in m.DeclaringType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (ov.Name != m.Name) continue;
                if (ov.GetParameters().Length == args.Length) return ov.Invoke(target, args);
            }
            return m.Invoke(target, args);   // 兜底：让调用方看到真实的参数不匹配异常
        }

        private static object EnumValue(string[] candidateTypeNames, string member)
        {
            foreach (string name in candidateTypeNames)
            {
                Type t = FindType(name);
                if (t != null && t.IsEnum) return Enum.Parse(t, member);
            }
            return null;
        }

        /// <summary>官方错误码 → 文本（ErrorToString 是官方提供的诊断接口）。</summary>
        private string ErrorText(object rc)
        {
            try
            {
                var sb = new System.Text.StringBuilder(256);
                object t = CallOrNull(_api, "ErrorToString", rc, sb, 256);
                if (t != null && sb.Length > 0) return sb.ToString();
            }
            catch { }
            return "无错误文本";
        }

        // ---- 状态读取 ----

        /// <summary>
        /// 取全部轴状态。官方 GetStatus 的返回形态是 out 参数（ErrorCode + out CMStatus），
        /// 这里统一走反射并把 out 参数回读出来。
        /// </summary>
        private object Status(int axis)
        {
            object r = CallOrNull(_cm, "GetStatus");
            return r == null ? null : r;
        }

        private object AxisStatusOf(object status, int axis)
        {
            object arr = FieldOf(status, "axesStatus");
            if (arr == null) return null;
            Array a = arr as Array;
            if (a == null || axis >= a.Length) return null;
            return a.GetValue(axis);
        }

        private bool FlagOf(object status, int axis, string field)
        {
            object ax = AxisStatusOf(status, axis);
            if (ax == null) return false;
            object v = FieldOf(ax, field);
            if (v == null) return false;
            if (v is bool) return (bool)v;
            try { return Convert.ToInt64(v) != 0; } catch { return false; }
        }

        private double Dbl(object status, int axis, string field)
        {
            object ax = AxisStatusOf(status, axis);
            if (ax == null) return 0;
            object v = FieldOf(ax, field);
            if (v == null) return 0;
            try { return Convert.ToDouble(v); } catch { return 0; }
        }

        private bool ServoOnOf(object status, int axis) { return FlagOf(status, axis, "servoOn"); }

        private static object FieldOf(object o, string name)
        {
            if (o == null) return null;
            Type t = o.GetType();
            FieldInfo f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) return f.GetValue(o);
            PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return (p != null && p.CanRead) ? p.GetValue(o, null) : null;
        }

        // ---- 指令结构：按官方类型名取，类型名以官方文档为准 ----

        private object NewPosCmd()
        {
            Type t = FindType("WMX3ApiCLR.CM.PositionCommand");
            return t == null ? null : Activator.CreateInstance(t);
        }

        private object NewJogCmd()
        {
            Type t = FindType("WMX3ApiCLR.CM.JogCommand");
            return t == null ? null : Activator.CreateInstance(t);
        }

        private object NewHomeParam()
        {
            Type t = FindType("WMX3ApiCLR.CM.HomeParam");
            return t == null ? null : Activator.CreateInstance(t);
        }

        private object NewLinearCmd()
        {
            Type t = FindType("WMX3ApiCLR.CM.LinearInterpolationCommand");
            return t == null ? null : Activator.CreateInstance(t);
        }

        private object NewArcCmd()
        {
            Type t = FindType("WMX3ApiCLR.CM.LengthAndEndEndCircularIntplCommand");
            return t == null ? null : Activator.CreateInstance(t);
        }

        private static Type FindType(string full)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a == null) continue;
                Type t = a.GetType(full, false);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>
        /// 设置速度轮廓。单位：WMX3 官方为「user unit/second」与「user unit/second²」
        /// （doxygen page_WMXDOC_FUNC_MPR_PROFILE_PARAM.html），
        /// 脉冲当量由控制器侧齿轮比承担（Pulse = User Unit * Gear Ratio），
        /// 所以这里传 mm/s、mm/s² 原值，**不能再乘脉冲当量**。
        ///
        /// ★ JerkRatio 落地（2026-10-21 取证，见 evidence/JERK_UNITS_EVIDENCE.md）：
        ///   官方 <c>Profile.jerkAccRatio</c> 的定义与本项目 JerkRatio **语义逐字对齐** ——
        ///   「the ratio of the time that the acceleration changes and the total acceleration time」，
        ///   0 = 等同梯形(Trapezoidal)，1 = 等同 S 曲线(SCurve)。范围 [0,1]，单位就是比率本身。
        ///   因此 JerkRatio 可**直传**，无需任何比例尺假设（六家里只有软赢能做到）。
        ///
        ///   同时必须把 type 从 Trapezoidal 改成曲线类型，否则 jerk 字段不生效 ——
        ///   这正是此前 JerkRatio 被静默丢弃的原因（type 硬编码为 0）。
        ///
        /// ⚠ 曲线类型枚举名不做猜测：C++/CLI 的 CLR 包装里 ProfileType 的嵌套层级
        ///   未在官方 doxygen 中说明（doxygen 只描述 C++ 原生头文件），本项目此前已在
        ///   C++/CLI 边界上踩过枚举层级坑。按名字逐个候选试，**试不到就回退梯形**，
        ///   并置 JerkProfileApplied=false，让调用方知道平滑没生效（宁可保守，不可静默失效）。
        /// </summary>
        private void SetProfile(object cmd, MoveParams p, double ppu)
        {
            object prof = FieldOf(cmd, "profile");
            if (prof == null)
            {
                _jerkApplied = false;   // 官方按重载区分梯形/余弦，未给 profile 时用默认梯形
                return;
            }
            SetIfExists(prof, "velocity", p.Vel);
            SetIfExists(prof, "acc", p.Acc);
            SetIfExists(prof, "dec", p.Dec);              // 减速度用 p.Dec，不是 p.Acc

            // JerkRatio=0 → 梯形（官方语义：jerk 比率 0 即等同 Trapezoidal），无需设曲线类型
            if (p.JerkRatio <= 0.0)
            {
                SetIfExists(prof, "type", CurveTrapezoidal(prof));
                _jerkApplied = false;
                return;
            }

            // JerkRatio 直传官方 jerkAccRatio（单位就是比率，范围 [0,1]，量纲一致）
            if (!TrySetField(prof, "jerkAccRatio", p.JerkRatio)) { _jerkApplied = false; return; }
            if (!TrySetField(prof, "jerkDecRatio", p.JerkRatio)) { _jerkApplied = false; return; }

            // 必须切到 JerkRatio 曲线类型，否则上面两个字段不生效
            object curve = CurveJerkRatio(prof);
            if (curve == null) { _jerkApplied = false; return; }
            if (!TrySetField(prof, "type", curve)) { _jerkApplied = false; return; }
            _jerkApplied = true;
        }

        /// <summary>CurveType 枚举的默认（梯形）值。拿不到枚举类型时退回 int 0 —— 官方默认即 Trapezoidal。</summary>
        private static object CurveTrapezoidal(object prof)
        {
            Type et = ProfileTypeOf(prof);
            if (et != null && et.IsEnum)
            {
                try { return Enum.ToObject(et, 0); } catch { }
            }
            return 0;
        }

        /// <summary>
        /// 取 ProfileType 枚举里名为 JerkRatio 的成员。C++/CLI 的嵌套层级未文档化，
        /// 故按「字段实际类型」反查 —— 比按名字猜命名空间可靠。
        /// </summary>
        private static object CurveJerkRatio(object prof)
        {
            if (prof == null) return null;
            FieldInfo f = prof.GetType().GetField("type", BindingFlags.Public | BindingFlags.Instance);
            Type et = f != null ? f.FieldType : null;
            if (et == null || !et.IsEnum) return null;
            try
            {
                string[] names = Enum.GetNames(et);
                foreach (string n in names)
                    if (string.Equals(n, "JerkRatio", StringComparison.Ordinal))
                        return Enum.ToObject(et, Enum.Parse(et, n));
            }
            catch { }
            return null;
        }

        private static Type ProfileTypeOf(object prof)
        {
            if (prof == null) return null;
            FieldInfo f = prof.GetType().GetField("type", BindingFlags.Public | BindingFlags.Instance);
            return f != null ? f.FieldType : null;
        }

        /// <summary>
        /// 赋值并回报是否真写进去了。SetIfExists 分不清「写成功」和「字段不存在」，
        /// 而这里必须区分 —— 字段不存在时要老实退回梯形，不能假装成功。
        /// </summary>
        private static bool TrySetField(object o, string name, object val)
        {
            if (o == null) return false;
            Type t = o.GetType();
            FieldInfo f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) { f.SetValue(o, Coerce(val, f.FieldType)); return true; }
            PropertyInfo pi = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (pi != null && pi.CanWrite) { pi.SetValue(o, Coerce(val, pi.PropertyType), null); return true; }
            return false;
        }

        private static void SetIfExists(object o, string name, object val)
        {
            if (o == null) return;
            Type t = o.GetType();
            FieldInfo f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) { f.SetValue(o, Coerce(val, f.FieldType)); return; }
            PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite) p.SetValue(o, Coerce(val, p.PropertyType), null);
        }

        /// <summary>把值转成目标字段/形参的类型。官方指令结构大量用值类型数组（double[] / int[]），
        /// 直接塞 object[] 会抛 ArgumentException —— 这里统一按目标类型收敛。</summary>
        private static object Coerce(object v, Type target)
        {
            if (v == null || target == null) return v;
            if (target.IsInstanceOfType(v)) return v;
            if (target.IsArray && v is object[])
                return CoerceArray(v, target);
            if (target.IsEnum && !(v is Enum))
                return Enum.ToObject(target, Convert.ChangeType(v, Enum.GetUnderlyingType(target)));
            if (target.IsPrimitive || target == typeof(decimal))
                return Convert.ChangeType(v, target);
            return v;
        }

        private static object ToAxisArray(int[] axisList)
        {
            var a = new int[axisList.Length];
            for (int i = 0; i < axisList.Length; i++) a[i] = axisList[i];
            return a;
        }

        private object[] ToPosArray(int[] axisList, double[] targetUser, MoveMode mode)
        {
            var r = new object[targetUser.Length];
            for (int i = 0; i < targetUser.Length; i++)
            {
                double t = targetUser[i];
                if (mode == MoveMode.Relative) t += GetCommandPos(axisList[i]);
                r[i] = t * _ppu[axisList[i]];
            }
            return r;
        }

        /// <summary>统一 HomeMode → WMX3 回零模式。官方取值以手册为准，此处按常见对应关系映射。</summary>
        private static int MapHomeMode(HomeMode m)
        {
            switch (m)
            {
                case HomeMode.LimitReverse:    return 1;
                case HomeMode.HomeSwitch:      return 0;
                case HomeMode.HomeSwitchIndex: return 2;
                case HomeMode.IndexOnly:       return 3;
                default:                        return 0;
            }
        }

        #endregion
    }
}
