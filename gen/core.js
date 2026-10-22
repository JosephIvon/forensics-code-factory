// core.js - 核心抽象层 C# 模板（品牌无关）+ 虚拟控制器 + 工厂 + 示例 + 文档
(function (root, factory) {
  if (typeof module === 'object' && module.exports) { module.exports = factory(); }
  else { root.MCCore = factory(); }
})(typeof self !== 'undefined' ? self : this, function () {

  var TS = new Date().toISOString().replace('T', ' ').substring(0, 19);

  function brandCs(id) {
    switch (id) {
      case 'virtual': return 'Brand.Virtual';
      case 'gts': return 'Brand.GoogolGts';
      case 'leadshine': return 'Brand.Leadshine';
      case 'zmotion': return 'Brand.ZMotion';
      case 'acs': return 'Brand.Acs';
      case 'softwin': return 'Brand.SoftWin';
      case 'gaochuan': return 'Brand.GaoChuan';
    }
    return 'Brand.Virtual';
  }

  // ============ Core 源文件（与品牌无关，永远生成） ============
  function coreFiles(cfg) {
    var f = [];
    f.push({ path: 'src/Core/Brand.cs', content:
'// =====================================================================\n' +
'//  MotionControl.Core - 设备抽象层（品牌无关）\n' +
'//  生成工具: 上位机代码工厂 v1.0  |  生成时间: ' + TS + '\n' +
'//  对应 JD 职责2: "分层结构、模块边界、设备抽象层"\n' +
'//             职责3: "换卡不改上层业务代码"\n' +
'//  目标框架: .NET Framework 4.x（C#5 兼容），可无改动迁移 .NET 6/8\n' +
'// =====================================================================\n' +
'namespace MotionControl.Core\n' +
'{\n' +
'    /// <summary>运动控制品牌。新增品牌 = 加一个枚举值 + 一个适配器 + 工厂注册一行。</summary>\n' +
'    public enum Brand\n' +
'    {\n' +
'        Virtual = 0,      // 虚拟控制器（仿真/单元测试/无硬件演示）\n' +
'        GoogolGts = 1,    // 固高 GTS/GTN 系列（gts.dll）\n' +
'        Leadshine = 2,    // 雷赛 DMC/EMC 系列（LTDMC.dll）\n' +
'        ZMotion = 3,      // 正运动 ZMC 系列（zauxdll.dll）\n' +
'        Acs = 4,          // ACS SPiiPlus（ACS.SPiiPlusNET 官方.NET库）\n' +
'        SoftWin = 5,      // 软赢（骨架，见 docs/ApiChecklist.md）\n' +
'        GaoChuan = 6      // 高川（骨架，见 docs/ApiChecklist.md）\n' +
'    }\n' +
'}\n' });

    f.push({ path: 'src/Core/AxisConfig.cs', content:
'using System;\n' +
'namespace MotionControl.Core\n' +
'{\n' +
'    /// <summary>回零策略（JD任职要求5: 回零与限位策略）。适配层负责映射到各品牌回零模式字。</summary>\n' +
'    public enum HomeMode { None, LimitReverse, HomeSwitch, HomeSwitchIndex, IndexOnly }\n' +
'\n' +
'    public class HomeConfig\n' +
'    {\n' +
'        public HomeMode Mode = HomeMode.HomeSwitchIndex;\n' +
'        public double VelFast = 20.0;   // mm/s 快找\n' +
'        public double VelSlow = 2.0;    // mm/s 慢找\n' +
'        public double Acc = 200.0;      // mm/s^2\n' +
'        public double Offset = 0.0;     // 回零后偏置（用户单位）\n' +
'        public int TimeoutMs = 30000;\n' +
'    }\n' +
'\n' +
'    /// <summary>轴配置。所有位置统一为用户单位（默认 mm），脉冲换算封装在适配层。</summary>\n' +
'    public class AxisConfig\n' +
'    {\n' +
'        public int AxisIndex;                  // 0-based（统一约定，适配层内换算为各品牌轴号）\n' +
'        public string Name = "Axis";\n' +
'        public double PulsesPerUnit = 10000;   // 脉冲/mm（脉冲型卡的关键换算参数）\n' +
'        public double SoftLimitNeg = -100000;  // 软限位（用户单位）\n' +
'        public double SoftLimitPos = 100000;\n' +
'        public HomeConfig Home = new HomeConfig();\n' +
'        public override string ToString() { return Name + "[" + AxisIndex + "]"; }\n' +
'    }\n' +
'}\n' });

    f.push({ path: 'src/Core/MoveParams.cs', content:
'namespace MotionControl.Core\n' +
'{\n' +
'    public enum MoveMode { Absolute, Relative }\n' +
'\n' +
'    /// <summary>运动参数（用户单位）。JerkRatio=0 为梯形曲线，>0 映射为各品牌 S 曲线平滑因子（JD职责4: S曲线加减速与轨迹平滑）。</summary>\n' +
'    public class MoveParams\n' +
'    {\n' +
'        public double Vel = 50;      // mm/s\n' +
'        public double Acc = 500;     // mm/s^2\n' +
'        public double Dec = 500;\n' +
'        public double JerkRatio = 0.2;\n' +
'        public static MoveParams Default() { return new MoveParams(); }\n' +
'    }\n' +
'}\n' });

    f.push({ path: 'src/Core/AxisState.cs', content:
'using System;\n' +
'namespace MotionControl.Core\n' +
'{\n' +
'    /// <summary>统一轴状态位。各品牌状态寄存器位定义在适配层完成映射（JD职责9: 偶发定位偏差的统一诊断入口）。</summary>\n' +
'    [Flags]\n' +
'    public enum AxisState\n' +
'    {\n' +
'        None = 0,\n' +
'        Enabled = 1,\n' +
'        Moving = 2,\n' +
'        InPosition = 4,\n' +
'        Alarm = 8,\n' +
'        Homing = 16,\n' +
'        Homed = 32,\n' +
'        LimitNeg = 64,\n' +
'        LimitPos = 128,\n' +
'        Emergency = 256\n' +
'    }\n' +
'}\n' });

    f.push({ path: 'src/Core/MotionResult.cs', content:
'using System;\n' +
'namespace MotionControl.Core\n' +
'{\n' +
'    /// <summary>统一结果对象：错误码语义跨品牌一致，业务层不做品牌分支（JD职责9: 分析报告与预防措施的统一数据源）。</summary>\n' +
'    public class MotionResult\n' +
'    {\n' +
'        public bool Ok;\n' +
'        public int Code;\n' +
'        public string Message = "";\n' +
'        public DateTime Time = DateTime.Now;\n' +
'\n' +
'        public static MotionResult Success() { return new MotionResult { Ok = true, Code = 0, Message = "OK" }; }\n' +
'        public static MotionResult Success(string msg) { return new MotionResult { Ok = true, Code = 0, Message = msg }; }\n' +
'        public static MotionResult Fail(int code, string msg) { return new MotionResult { Ok = false, Code = code, Message = msg }; }\n' +
'        public override string ToString()\n' +
'        {\n' +
'            return (Ok ? "[OK] " : ("[ERR " + Code + "] ")) + Message + " @" + Time.ToString("HH:mm:ss.fff");\n' +
'        }\n' +
'    }\n' +
'}\n' });

    f.push({ path: 'src/Core/ControllerConnectInfo.cs', content:
'namespace MotionControl.Core\n' +
'{\n' +
'    /// <summary>连接信息：脉冲卡用卡号，网络/总线卡用 IP，按需扩展串口号。</summary>\n' +
'    public class ControllerConnectInfo\n' +
'    {\n' +
'        public short CardNo = 0;\n' +
'        public string Ip = "192.168.0.11";\n' +
'        public int Port = 701;         // ACS SPiiPlus 默认 TCP 端口\n' +
'        public string ConfigFile = ""; // 固高 GT_LoadConfig 配置文件（可选）\n' +
'    }\n' +
'}\n' });

    f.push({ path: 'src/Core/IMotionController.cs', content:
'using System;\n' +
'namespace MotionControl.Core\n' +
'{\n' +
'    /// <summary>\n' +
'    /// 统一运动控制接口 —— 上层业务（流程调度引擎/工艺模块）唯一依赖。\n' +
'    /// JD 职责映射:\n' +
'    ///   MoveTo/MoveJog/Stop      -> 职责4 高速点位\n' +
'    ///   MoveLine/MoveArc2D       -> 职责4 直线/圆弧插补、耦合运动\n' +
'    ///   SetAo/GetAi              -> 职责4 力控压合 / 职责5 贴装压力 / 职责6 胶量控制\n' +
'    ///   Home(策略可配)            -> 任职要求5 回零与限位策略\n' +
'    ///   GetFollowingError        -> 任职要求5 跟随误差补偿数据源\n' +
'    ///   SetDo/GetDi/WaitDi       -> 职责5 吸嘴拾取时序 / 职责6 出胶时序\n' +
'    /// 接口为"启动+查询"语义（非阻塞）；Wait* 提供阻塞等待。异步/状态机调度在业务层实现（职责2）。\n' +
'    /// </summary>\n' +
'    public interface IMotionController : IDisposable\n' +
'    {\n' +
'        Brand Brand { get; }\n' +
'        string DeviceName { get; }\n' +
'        bool IsOpen { get; }\n' +
'\n' +
'        MotionResult Open(ControllerConnectInfo info);\n' +
'        MotionResult Close();\n' +
'        MotionResult ConfigureAxis(AxisConfig cfg);\n' +
'\n' +
'        MotionResult Enable(int axis, bool on);\n' +
'        AxisState GetState(int axis);\n' +
'        double GetCommandPos(int axis);      // 用户单位\n' +
'        double GetFeedbackPos(int axis);     // 用户单位（无编码器时返回指令位置）\n' +
'        double GetFollowingError(int axis);  // 用户单位\n' +
'\n' +
'        MotionResult Home(int axis);\n' +
'        bool IsHomed(int axis);\n' +
'        MotionResult WaitHomeDone(int axis, int timeoutMs);\n' +
'\n' +
'        MotionResult MoveTo(int axis, double posUser, MoveParams p, MoveMode mode);\n' +
'        MotionResult MoveJog(int axis, double velUserPerSec, MoveParams p);\n' +
'        MotionResult Stop(int axis, bool emergency);\n' +
'        MotionResult WaitMoveDone(int axis, int timeoutMs);\n' +
'\n' +
'        /// <summary>多轴直线插补（各轴同时到达）。axisList 与 target 一一对应。</summary>\n' +
'        MotionResult MoveLine(int[] axisList, double[] targetUser, MoveParams p, MoveMode mode);\n' +
'        /// <summary>XY 平面圆弧（IJ 圆心增量格式）。cw=true 顺时针。</summary>\n' +
'        MotionResult MoveArc2D(int axisX, int axisY, double i, double j, double endX, double endY, bool cw, MoveParams p);\n' +
'\n' +
'        MotionResult SetDo(int index, bool on);\n' +
'        bool GetDi(int index);\n' +
'        double GetAi(int index);             // V\n' +
'        MotionResult SetAo(int index, double volts);\n' +
'        MotionResult WaitDi(int index, bool level, int timeoutMs);\n' +
'    }\n' +
'}\n' });
    return f;
  }

  // ============ 虚拟控制器 ============
  function virtualFile() {
    return { path: 'src/Virtual/VirtualController.cs', content:
'using System;\n' +
'using System.Collections.Generic;\n' +
'using System.Threading;\n' +
'using MotionControl.Core;\n' +
'\n' +
'namespace MotionControl.Virtual\n' +
'{\n' +
'    /// <summary>\n' +
'    /// 虚拟运动控制器（与真实适配器同一接口），用途:\n' +
'    ///   1) 无硬件开发/联调业务流程（架构先行，JD职责2）\n' +
'    ///   2) 单元测试与 CI 回归（JD职责10 单元测试规范）\n' +
'    ///   3) 客户现场前的工艺流程预演\n' +
'    /// 运动学为匀速段仿真 + 惰性推进（不占 CPU）；固定随机种子保证可复现。\n' +
'    /// </summary>\n' +
'    public class VirtualController : IMotionController\n' +
'    {\n' +
'        private class AxisSim\n' +
'        {\n' +
'            public double CmdPos, FbPos;\n' +
'            public double From, To, Duration;\n' +
'            public DateTime Start;\n' +
'            public bool Moving, Enabled, Homed, Homing;\n' +
'            public DateTime HomeStart;\n' +
'            public double HomeOffset;\n' +
'        }\n' +
'\n' +
'        private class Segment\n' +
'        {\n' +
'            public int[] Axes; public double[] Target; public double Duration;\n' +
'        }\n' +
'\n' +
'        private readonly Dictionary<int, AxisSim> _axes = new Dictionary<int, AxisSim>();\n' +
'        private readonly Queue<Segment> _queue = new Queue<Segment>();\n' +
'        private readonly Dictionary<int, bool> _doMap = new Dictionary<int, bool>();\n' +
'        private readonly Dictionary<int, double> _aoMap = new Dictionary<int, double>();\n' +
'        private readonly object _lock = new object();\n' +
'        private readonly DateTime _boot = DateTime.Now;\n' +
'        private readonly Random _rnd = new Random(12345);\n' +
'        private bool _open;\n' +
'\n' +
'        public Brand Brand { get { return Brand.Virtual; } }\n' +
'        public string DeviceName { get { return "VirtualController-8Axis-Sim"; } }\n' +
'        public bool IsOpen { get { return _open; } }\n' +
'\n' +
'        public VirtualController()\n' +
'        {\n' +
'            for (int i = 0; i < 8; i++) _axes[i] = new AxisSim();\n' +
'        }\n' +
'\n' +
'        public MotionResult Open(ControllerConnectInfo info)\n' +
'        {\n' +
'            lock (_lock)\n' +
'            {\n' +
'                if (_open) return MotionResult.Success("already open");\n' +
'                _open = true;\n' +
'                _queue.Clear();\n' +
'                return MotionResult.Success("virtual controller opened");\n' +
'            }\n' +
'        }\n' +
'\n' +
'        public MotionResult Close()\n' +
'        {\n' +
'            lock (_lock)\n' +
'            {\n' +
'                _open = false;\n' +
'                _queue.Clear();\n' +
'                return MotionResult.Success();\n' +
'            }\n' +
'        }\n' +
'\n' +
'        public void Dispose() { Close(); }\n' +
'\n' +
'        public MotionResult ConfigureAxis(AxisConfig cfg)\n' +
'        {\n' +
'            lock (_lock)\n' +
'            {\n' +
'                if (cfg == null) return MotionResult.Fail(-6, "null config");\n' +
'                MotionResult gCfg = CheckAxis(cfg.AxisIndex); if (!gCfg.Ok) return gCfg;\n' +
'                if (cfg.PulsesPerUnit <= 0) return MotionResult.Fail(-9, "PulsesPerUnit must be > 0, got " + cfg.PulsesPerUnit);\n' +
'                AxisSim a = GetAxis(cfg.AxisIndex);\n' +
'                a.HomeOffset = cfg.Home.Offset;\n' +
'                return MotionResult.Success("axis " + cfg + " configured (virtual)");\n' +
'            }\n' +
'        }\n' +
'\n' +
'        public MotionResult Enable(int axis, bool on)\n' +
'        {\n' +
'            lock (_lock)\n' +
'            {\n' +
'                if (!_open) return MotionResult.Fail(-1, "not open");\n' +'                MotionResult g = CheckAxis(axis); if (!g.Ok) return g;\n' +

'                AxisSim a = GetAxis(axis);\n' +
'                a.Enabled = on;\n' +
'                return MotionResult.Success();\n' +
'            }\n' +
'        }\n' +
'\n' +
'        public AxisState GetState(int axis)\n' +
'        {\n' +
'            lock (_lock)\n' +
'            {\n' +
'                AxisSim a = GetAxis(axis);\n' +
'                Update();\n' +
'                AxisState s = AxisState.None;\n' +
'                if (a.Enabled) s |= AxisState.Enabled;\n' +
'                if (a.Moving) s |= AxisState.Moving; else s |= AxisState.InPosition;\n' +
'                if (a.Homed) s |= AxisState.Homed;\n' +
'                if (a.Homing) s |= AxisState.Homing;\n' +
'                return s;\n' +
'            }\n' +
'        }\n' +
'\n' +
'        public double GetCommandPos(int axis) { lock (_lock) { Update(); return GetAxis(axis).CmdPos; } }\n' +
'        public double GetFeedbackPos(int axis)\n' +
'        {\n' +
'            lock (_lock)\n' +
'            {\n' +
'                Update();\n' +
'                // 模拟 2um 反馈噪声（再现 SPC 统计场景）\n' +
'                return GetAxis(axis).FbPos + (_rnd.NextDouble() - 0.5) * 0.002;\n' +
'            }\n' +
'        }\n' +
'        public double GetFollowingError(int axis) { lock (_lock) { Update(); return GetAxis(axis).CmdPos - GetAxis(axis).FbPos; } }\n' +
'\n' +
'        public MotionResult Home(int axis)\n' +
'        {\n' +
'            lock (_lock)\n' +
'            {\n' +
'                if (!_open) return MotionResult.Fail(-1, "not open");\n' +'                MotionResult g = CheckAxis(axis); if (!g.Ok) return g;\n' +

'                AxisSim a = GetAxis(axis);\n' +
'                a.Homing = true;\n' +
'                a.Homed = false; // [接缝3·BEH-15] 回零进行中不算已回零：不清此标志，WaitHomeDone 对已回零轴立即返回，重回零根本没等（行为断言抓到的真 bug）\n' +
'                a.HomeStart = DateTime.Now;\n' +
'                return MotionResult.Success("homing started (virtual, ~1.2s)");\n' +
'            }\n' +
'        }\n' +
'\n' +
'        public bool IsHomed(int axis) { lock (_lock) { Update(); return GetAxis(axis).Homed; } }\n' +
'\n' +
'        public MotionResult WaitHomeDone(int axis, int timeoutMs)\n' +
'        {\n' +'            MotionResult g0 = CheckAxis(axis); if (!g0.Ok) return g0;\n' +

'            DateTime t0 = DateTime.Now;\n' +
'            while ((DateTime.Now - t0).TotalMilliseconds < timeoutMs)\n' +
'            {\n' +
'                lock (_lock)\n' +
'                {\n' +
'                    Update();\n' +
'                    AxisSim a = GetAxis(axis);\n' +
'                    if (a.Homed) return MotionResult.Success();\n' +
'                }\n' +
'                Thread.Sleep(10);\n' +
'            }\n' +
'            return MotionResult.Fail(-2, "home timeout axis " + axis);\n' +
'        }\n' +
'\n' +
'        public MotionResult MoveTo(int axis, double posUser, MoveParams p, MoveMode mode)\n' +
'        {\n' +
'            lock (_lock)\n' +
'            {\n' +
'                if (!_open) return MotionResult.Fail(-1, "not open");\n' +'                MotionResult g = CheckAxis(axis); if (!g.Ok) return g;\n' +

'                double from = GetAxis(axis).CmdPos;\n' +
'                double to = (mode == MoveMode.Absolute) ? posUser : from + posUser;\n' +
'                EnqueueSegment(new int[] { axis }, new double[] { to }, p);\n' +
'                return MotionResult.Success();\n' +
'            }\n' +
'        }\n' +
'\n' +
'        public MotionResult MoveJog(int axis, double velUserPerSec, MoveParams p)\n' +
'        {\n' +
'            lock (_lock)\n' +
'            {\n' +
'                if (!_open) return MotionResult.Fail(-1, "not open");\n' +'                MotionResult g = CheckAxis(axis); if (!g.Ok) return g;\n' +

'                // 虚拟点动: 以 1 秒为目标步进的持续运动\n' +
'                double to = GetAxis(axis).CmdPos + velUserPerSec * 1.0;\n' +
'                MoveParams pj = new MoveParams();\n' +
'                pj.Vel = Math.Abs(velUserPerSec); pj.Acc = p.Acc; pj.Dec = p.Dec;\n' +
'                EnqueueSegment(new int[] { axis }, new double[] { to }, pj);\n' +
'                return MotionResult.Success();\n' +
'            }\n' +
'        }\n' +
'\n' +
'        public MotionResult Stop(int axis, bool emergency)\n' +
'        {\n' +
'            lock (_lock)\n' +
'            {\n' +'                MotionResult g = CheckAxis(axis); if (!g.Ok) return g;\n' +

'                _queue.Clear();\n' +
'                AxisSim a = GetAxis(axis);\n' +
'                a.Moving = false;\n' +
'                return MotionResult.Success(emergency ? "emergency stop" : "stop");\n' +
'            }\n' +
'        }\n' +
'\n' +
'        public MotionResult WaitMoveDone(int axis, int timeoutMs)\n' +
'        {\n' +'            MotionResult g0 = CheckAxis(axis); if (!g0.Ok) return g0;\n' +

'            DateTime t0 = DateTime.Now;\n' +
'            while ((DateTime.Now - t0).TotalMilliseconds < timeoutMs)\n' +
'            {\n' +
'                lock (_lock)\n' +
'                {\n' +
'                    Update();\n' +
'                    if (!GetAxis(axis).Moving && _queue.Count == 0) return MotionResult.Success();\n' +
'                }\n' +
'                Thread.Sleep(10);\n' +
'            }\n' +
'            return MotionResult.Fail(-3, "move timeout axis " + axis);\n' +
'        }\n' +
'\n' +
'        public MotionResult MoveLine(int[] axisList, double[] targetUser, MoveParams p, MoveMode mode)\n' +
'        {\n' +
'            lock (_lock)\n' +
'            {\n' +
'                if (!_open) return MotionResult.Fail(-1, "not open");\n' +
'                if (axisList == null || targetUser == null || axisList.Length != targetUser.Length)\n' +
'                    return MotionResult.Fail(-4, "axisList/targetUser length mismatch");\n' +
'                for (int gi = 0; gi < axisList.Length; gi++) { MotionResult gL = CheckAxis(axisList[gi]); if (!gL.Ok) return gL; }\n' +
'                double[] to = new double[axisList.Length];\n' +
'                double distSq = 0;\n' +
'                for (int i = 0; i < axisList.Length; i++)\n' +
'                {\n' +
'                    double from = GetAxis(axisList[i]).CmdPos;\n' +
'                    to[i] = (mode == MoveMode.Absolute) ? targetUser[i] : from + targetUser[i];\n' +
'                    distSq += (to[i] - from) * (to[i] - from);\n' +
'                }\n' +
'                EnqueueSegment(axisList, to, p);\n' +
'                return MotionResult.Success("line segment queued, pathLen=" + Math.Sqrt(distSq).ToString("F3"));\n' +
'            }\n' +
'        }\n' +
'\n' +
'        public MotionResult MoveArc2D(int axisX, int axisY, double i, double j, double endX, double endY, bool cw, MoveParams p)\n' +
'        {\n' +
'            lock (_lock)\n' +
'            {\n' +
'                if (!_open) return MotionResult.Fail(-1, "not open");\n' +
'                MotionResult gX = CheckAxis(axisX); if (!gX.Ok) return gX;\n' +
'                MotionResult gY = CheckAxis(axisY); if (!gY.Ok) return gY;\n' +
'                double x0 = GetAxis(axisX).CmdPos, y0 = GetAxis(axisY).CmdPos;\n' +
'                double cx = x0 + i, cy = y0 + j;\n' +
'                double r = Math.Sqrt(i * i + j * j);\n' +
'                double a0 = Math.Atan2(y0 - cy, x0 - cx);\n' +
'                double a1 = Math.Atan2(endY - cy, endX - cx);\n' +
'                double sweep = a1 - a0;\n' +
'                if (cw && sweep >= 0) sweep -= 2 * Math.PI;\n' +
'                if (!cw && sweep <= 0) sweep += 2 * Math.PI;\n' +
'                int n = 32;\n' +
'                for (int k = 1; k <= n; k++)\n' +
'                {\n' +
'                    double a = a0 + sweep * k / n;\n' +
'                    double tx = cx + r * Math.Cos(a);\n' +
'                    double ty = cy + r * Math.Sin(a);\n' +
'                    double[] prev = (k == 1)\n' +
'                        ? new double[] { x0, y0 }\n' +
'                        : new double[] { cx + r * Math.Cos(a0 + sweep * (k - 1) / n), cy + r * Math.Sin(a0 + sweep * (k - 1) / n) };\n' +
'                    double seg = Math.Sqrt((tx - prev[0]) * (tx - prev[0]) + (ty - prev[1]) * (ty - prev[1]));\n' +
'                    MoveParams ps = new MoveParams();\n' +
'                    ps.Vel = p.Vel * 32 / n; ps.Acc = p.Acc; ps.Dec = p.Dec;\n' +
'                    EnqueueSegment(new int[] { axisX, axisY }, new double[] { tx, ty }, ps);\n' +
'                }\n' +
'                EnqueueSegment(new int[] { axisX, axisY }, new double[] { endX, endY }, p);\n' +
'                return MotionResult.Success("arc queued, r=" + r.ToString("F3") + ", sweepDeg=" + (sweep * 180 / Math.PI).ToString("F1"));\n' +
'            }\n' +
'        }\n' +
'\n' +
'        public MotionResult SetDo(int index, bool on)\n' +
'        {\n' +
'            lock (_lock) { MotionResult gP = CheckPort("DO", index, DO_MAX); if (!gP.Ok) return gP; _doMap[index] = on; return MotionResult.Success("DO" + index + "=" + on); }\n' +
'        }\n' +
'\n' +
'        public bool GetDi(int index)\n' +
'        {\n' +
'            lock (_lock)\n' +
'            {\n' +
'                if (index < 0 || index > DI_MAX) throw new ArgumentOutOfRangeException("index", index, "DI 0-" + DI_MAX);\n' +
'                double t = (DateTime.Now - _boot).TotalSeconds;\n' +
'                // 周期性翻转的虚拟 DI（周期~10s/位错开），保证 WaitDi 可被满足\n' +
'                return Math.Sin(t * 0.6 + index * 1.7) > 0;\n' +
'            }\n' +
'        }\n' +
'\n' +
'        public double GetAi(int index)\n' +
'        {\n' +
'            lock (_lock)\n' +
'            {\n' +
'                if (index < 0 || index > AI_MAX) throw new ArgumentOutOfRangeException("index", index, "AI 0-" + AI_MAX);\n' +
'                double t = (DateTime.Now - _boot).TotalSeconds;\n' +
'                // 2.5V +/-0.08V 正弦 + 10mV 噪声 —— 模拟压力/胶量传感器\n' +
'                return 2.5 + 0.08 * Math.Sin(t * 2.0 + index * 0.7) + (_rnd.NextDouble() - 0.5) * 0.02;\n' +
'            }\n' +
'        }\n' +
'\n' +
'        public MotionResult SetAo(int index, double volts)\n' +
'        {\n' +
'            lock (_lock) { MotionResult gA = CheckPort("AO", index, AO_MAX); if (!gA.Ok) return gA; _aoMap[index] = volts; return MotionResult.Success("AO" + index + "=" + volts); }\n' +
'        }\n' +
'\n' +
'        public MotionResult WaitDi(int index, bool level, int timeoutMs)\n' +
'        {\n' +
'            MotionResult gW = CheckPort("DI", index, DI_MAX); if (!gW.Ok) return gW;\n' +
'            DateTime t0 = DateTime.Now;\n' +
'            while ((DateTime.Now - t0).TotalMilliseconds < timeoutMs)\n' +
'            {\n' +
'                if (GetDi(index) == level) return MotionResult.Success();\n' +
'                Thread.Sleep(10);\n' +
'            }\n' +
'            return MotionResult.Fail(-5, "wait DI" + index + "=" + level + " timeout");\n' +
'        }\n' +
'\n' +
'        // ---------------- 内部: 惰性推进仿真 ----------------\n' +
'        // ---- [接缝3·BEH-08] 参数守卫 ----\n' +
'        // 返回 MotionResult 的方法：越界必须返回 Fail（错误码风格，与本套件一致），\n' +
'        // 不许靠深处异常冒泡、更不许沉默接受。真机适配器抄这里时必须保留同语义校验——\n' +
'        // 越界轴号在真机上是非法内存访问（栈损坏级）。\n' +
'        private const int AXIS_MAX = 7, DI_MAX = 15, DO_MAX = 15, AI_MAX = 7, AO_MAX = 7;\n' +
'\n' +
'        private MotionResult CheckAxis(int axis)\n' +
'        {\n' +
'            if (axis < 0 || axis > AXIS_MAX) return MotionResult.Fail(-7, "axis " + axis + " out of range 0-" + AXIS_MAX);\n' +
'            return MotionResult.Success();\n' +
'        }\n' +
'\n' +
'        private MotionResult CheckPort(string kind, int index, int max)\n' +
'        {\n' +
'            if (index < 0 || index > max) return MotionResult.Fail(-8, kind + " index " + index + " out of range 0-" + max);\n' +
'            return MotionResult.Success();\n' +
'        }\n' +
'\n' +
'        private AxisSim GetAxis(int axis)\n' +
'        {\n' +
'            AxisSim a;\n' +
'            if (!_axes.TryGetValue(axis, out a)) throw new ArgumentOutOfRangeException("axis", axis, "virtual controller supports 0-7");\n' +
'            return a;\n' +
'        }\n' +
'\n' +
'        private void EnqueueSegment(int[] axes, double[] target, MoveParams p)\n' +
'        {\n' +
'            double maxDelta = 0;\n' +
'            for (int i = 0; i < axes.Length; i++)\n' +
'            {\n' +
'                double d = Math.Abs(target[i] - GetAxis(axes[i]).CmdPos);\n' +
'                if (d > maxDelta) maxDelta = d;\n' +
'            }\n' +
'            double vel = Math.Max(p.Vel, 0.001);\n' +
'            double duration = maxDelta / vel + vel / Math.Max(p.Acc, 1); // 匀速+加减速近似\n' +
'            Segment s = new Segment();\n' +
'            s.Axes = axes; s.Target = target; s.Duration = Math.Max(duration, 0.001);\n' +
'            _queue.Enqueue(s);\n' +
'        }\n' +
'\n' +
'        private void Update()\n' +
'        {\n' +
'            DateTime now = DateTime.Now;\n' +
'            foreach (KeyValuePair<int, AxisSim> kv in _axes)\n' +
'            {\n' +
'                AxisSim a = kv.Value;\n' +
'                if (a.Moving)\n' +
'                {\n' +
'                    double k = (now - a.Start).TotalSeconds / a.Duration;\n' +
'                    if (k >= 1.0)\n' +
'                    {\n' +
'                        a.CmdPos = a.To; a.FbPos = a.To; a.Moving = false;\n' +
'                    }\n' +
'                    else\n' +
'                    {\n' +
'                        double pos = a.From + (a.To - a.From) * k;\n' +
'                        a.CmdPos = pos; a.FbPos = pos - Math.Sin(k * Math.PI) * 0.001; // 1um 级跟随误差\n' +
'                    }\n' +
'                }\n' +
'                if (a.Homing && (now - a.HomeStart).TotalSeconds > 1.2)\n' +
'                {\n' +
'                    a.Homing = false; a.Homed = true;\n' +
'                    a.CmdPos = a.HomeOffset; a.FbPos = a.HomeOffset; a.From = a.HomeOffset; a.To = a.HomeOffset;\n' +
'                }\n' +
'            }\n' +
'            bool anyMoving = false;\n' +
'            foreach (KeyValuePair<int, AxisSim> kv in _axes) if (kv.Value.Moving) { anyMoving = true; break; }\n' +
'            if (!anyMoving && _queue.Count > 0)\n' +
'            {\n' +
'                Segment s = _queue.Dequeue();\n' +
'                for (int i = 0; i < s.Axes.Length; i++)\n' +
'                {\n' +
'                    AxisSim a = GetAxis(s.Axes[i]);\n' +
'                    a.From = a.CmdPos; a.To = s.Target[i]; a.Start = now; a.Duration = s.Duration; a.Moving = true;\n' +
'                }\n' +
'            }\n' +
'        }\n' +
'    }\n' +
'}\n' };
  }

  // ============ 工厂 ============
  function factoryFile(adapters) {
    var cases = '';
    for (var i = 0; i < adapters.length; i++) {
      var a = adapters[i];
      if (a.id === 'virtual') continue;
      cases += '                case ' + brandCs(a.id) + ': return new ' + a.ns + '.' + a.cls + '();\n';
    }
    return { path: 'src/Core/ControllerFactory.cs', content:
'using System;\n' +
'using MotionControl.Core;\n' +
'using MotionControl.Virtual;\n' +
(usingLines(adapters)) +
'\n' +
'namespace MotionControl.Core\n' +
'{\n' +
'    /// <summary>工厂：业务层唯一的品牌耦合点。换卡 = 改调用处一个枚举值（JD职责3）。</summary>\n' +
'    public static class ControllerFactory\n' +
'    {\n' +
'        public static IMotionController Create(Brand brand)\n' +
'        {\n' +
'            switch (brand)\n' +
'            {\n' +
'                case Brand.Virtual: return new VirtualController();\n' +
cases +
'                default: throw new NotSupportedException("Brand not registered: " + brand);\n' +
'            }\n' +
'        }\n' +
'    }\n' +
'}\n' };
  }

  function usingLines(adapters) {
    var seen = {}, s = '';
    for (var i = 0; i < adapters.length; i++) {
      var ns = adapters[i].ns;
      if (!seen[ns] && ns !== 'MotionControl.Virtual' && ns !== 'MotionControl.Core') {
        seen[ns] = 1; s += 'using ' + ns + ';\n';
      }
    }
    return s;
  }

  // ============ 工艺示例（点胶/贴片/耦合） ============
  function sampleFile(cfg) {
    var proc = cfg.process || 'dispensing';
    var title, flow;
    if (proc === 'diebond') {
      title = '共晶贴片工艺示例（JD职责5: 吸嘴拾取/温度曲线/贴装压力/视觉对位时序位）';
    } else if (proc === 'coupling') {
      title = '耦合对准示例（JD职责7: 多轴对准/功率采集/峰值锁定）';
    } else if (proc === 'dispensing') {
      title = '点胶工艺示例（JD职责6: 路径规划/出胶时序/缺胶溢胶判定 + 职责8: SPC统计）';
    } else {
      title = '通用运动流程示例';
    }

    return { path: 'samples/Program.cs', content:
'using System;\n' +
'using System.Collections.Generic;\n' +
'using System.Globalization;\n' +
'using System.IO;\n' +
'using System.Threading;\n' +
'using MotionControl.Core;\n' +
'\n' +
'namespace Samples\n' +
'{\n' +
'    /// <summary>\n' +
'    /// ' + title + '\n' +
'    /// 换卡演示: 只修改 CREATE_BRAND 一行，业务流程零改动（JD职责3 目标）。\n' +
'    /// 生成时间: ' + TS + '\n' +
'    /// </summary>\n' +
'    public static class Program\n' +
'    {\n' +
'        // ================== 换卡只改这一行 ==================\n' +
'        const Brand CREATE_BRAND = Brand.Virtual;\n' +
'        // Brand.Leadshine / Brand.GoogolGts / Brand.ZMotion ...\n' +
'        // [注意] 真实品牌的 P/Invoke 路径只做过编译级验证，未经运行验证；\n' +
'        //        切换前须按 docs/ApiChecklist.md 对照厂商手册核对签名。\n' +
'        //        Brand.Acs 需专用 ACS 套件 + 官方 ACS.SPiiPlusNET.dll（不在全品牌套件内）。\n' +
'        // ====================================================\n' +
'\n' +
'        static int Main(string[] args)\n' +
'        {\n' +
'            Console.WriteLine("========================================================");\n' +
'            Console.WriteLine(" 上位机工艺流程 Demo | brand=" + CREATE_BRAND);\n' +
'            Console.WriteLine("========================================================");\n' +
'\n' +
'            IMotionController c = ControllerFactory.Create(CREATE_BRAND);\n' +
'            MotionResult r = c.Open(new ControllerConnectInfo());\n' +
'            Check(r, "Open");\n' +
'\n' +
'            try\n' +
'            {\n' +
'                // 1. 使能 + 回零（任职要求5: 回零与限位策略）\n' +
'                int[] axes = new int[] { 0, 1, 2 };   // X Y Z\n' +
'                string[] names = new string[] { "X", "Y", "Z" };\n' +
'                foreach (int ax in axes)\n' +
'                {\n' +
'                    AxisConfig ac = new AxisConfig();\n' +
'                    ac.AxisIndex = ax; ac.Name = names[Array.IndexOf(axes, ax)];\n' +
'                    ac.PulsesPerUnit = 10000; ac.SoftLimitNeg = -500; ac.SoftLimitPos = 500;\n' +
'                    ac.Home.Mode = HomeMode.HomeSwitchIndex; ac.Home.Offset = 0;\n' +
'                    Check(c.ConfigureAxis(ac), "ConfigureAxis " + ac.Name);\n' +
'                }\n' +
'                foreach (int ax in axes) Check(c.Enable(ax, true), "Enable axis " + ax);\n' +
'                foreach (int ax in axes) Check(c.Home(ax), "Home axis " + ax);\n' +
'                foreach (int ax in axes) Check(c.WaitHomeDone(ax, 30000), "WaitHomeDone " + ax);\n' +
'                Console.WriteLine("[POS] X={0:F3} Y={1:F3} Z={2:F3}", c.GetCommandPos(0), c.GetCommandPos(1), c.GetCommandPos(2));\n' +
'\n' +
'                // 2. 工艺流程\n' +
'                RunProcess(c);\n' +
'\n' +
'                // 2.5 行为断言自检（接缝3 共享评测集 BEH-01/03/04/07/09/14/15）\n' +
'                BehaviorSelfCheck(c);\n' +
'\n' +
'                // 3. SPC 统计（JD职责8: 数据采集 SPC 统计）\n' +
'                SpcReport(c);\n' +
'\n' +
'                Console.WriteLine("RESULT: PASS - 流程完成，报告见 glue_report.csv");\n' +
'                return 0;\n' +
'            }\n' +
'            catch (Exception ex)\n' +
'            {\n' +
'                Console.WriteLine("RESULT: FAIL - " + ex.Message);\n' +
'                return 1;\n' +
'            }\n' +
'            finally\n' +
'            {\n' +
'                foreach (int ax in new int[] { 0, 1, 2 }) c.Stop(ax, false);\n' +
'                c.Close();\n' +
'            }\n' +
'        }\n' +
'\n' +
'        static void RunProcess(IMotionController c)\n' +
'        {\n' +
'            MoveParams fast = new MoveParams(); fast.Vel = 80; fast.Acc = 800;\n' +
'            MoveParams work = new MoveParams(); work.Vel = 25; work.Acc = 300; work.JerkRatio = 0.3;\n' +
'            const int GLUE_DO = 0;      // 出胶阀 DO\n' +
'            const double Z_SAFE = 5.0;  // 安全高度\n' +
'\n' +
'            Check(c.MoveTo(2, Z_SAFE, fast, MoveMode.Absolute), "Z -> safe");\n' +
'\n' +
'            // ---- 路径1: 矩形三点走胶（IO 时序 = 出胶时序，JD职责6）----\n' +
'            double[,] rect = new double[,] { { 20, 10 }, { 80, 10 }, { 80, 60 }, { 20, 60 } };\n' +
'            Check(c.MoveLine(new int[] { 0, 1 }, new double[] { rect[0, 0], rect[0, 1] }, fast, MoveMode.Absolute), "MoveLine to start");\n' +
'            Check(c.WaitMoveDone(0, 10000), "wait arrive");\n' +
'            Check(c.MoveTo(2, 0.5, work, MoveMode.Absolute), "Z -> glue height (点胶高度跟随)");\n' +
'            Check(c.WaitMoveDone(2, 10000), "wait Z");\n' +
'            for (int i = 1; i < rect.GetLength(0); i++)\n' +
'            {\n' +
'                Check(c.SetDo(GLUE_DO, true), "glue ON");\n' +
'                Check(c.MoveLine(new int[] { 0, 1 }, new double[] { rect[i, 0], rect[i, 1] }, work, MoveMode.Absolute), "glue path seg " + i);\n' +
'                Check(c.WaitMoveDone(0, 30000), "wait seg " + i);\n' +
'                Check(c.SetDo(GLUE_DO, false), "glue OFF");\n' +
'            }\n' +
'\n' +
'            // ---- 路径2: 圆弧转角（JD职责4: 圆弧插补）----\n' +
'            Check(c.MoveTo(2, Z_SAFE, fast, MoveMode.Absolute), "Z -> safe");\n' +
'            Check(c.MoveArc2D(0, 1, 15.0, 0.0, 50.0, 80.0, false, work), "arc");\n' +
'            Check(c.WaitMoveDone(0, 30000), "wait arc");\n' +
'\n' +
'            // ---- IO 互锁等待（吸嘴/治具到位信号，JD职责5 时序）----\n' +
'            Check(c.WaitDi(0, true, 15000), "wait fixture ready DI0");\n' +
'\n' +
'            // ---- 力控压合演示（JD职责4: 力控压合 -> 模拟量闭环粗调）----\n' +
'            Check(c.SetAo(0, 1.25), "force setpoint 1.25V");\n' +
'            double fbForce = c.GetAi(0);\n' +
'            Console.WriteLine("[FORCE] setpoint=1.25V feedback={0:F3}V", fbForce);\n' +
'            Check(c.SetAo(0, 0.0), "force release");\n' +
'\n' +
'            // ---- [接缝3·BEH-08] 参数守卫自检：越界轴号/端口必须显式拒绝，不许沉默 ----\n' +
'            MotionResult badMove = c.MoveTo(99, 1.0, fast, MoveMode.Absolute);\n' +
'            if (badMove.Ok) throw new Exception("FAIL at axis-guard: 越界轴号 MoveTo(99) 被沉默接受");\n' +
'            Console.WriteLine("[SELFCHECK] MoveTo(99) -> " + badMove);\n' +
'            bool threw = false;\n' +
'            try { c.GetCommandPos(99); } catch (ArgumentOutOfRangeException) { threw = true; }\n' +
'            if (!threw) throw new Exception("FAIL at axis-guard: 读数方法越界未抛 ArgumentOutOfRangeException");\n' +
'            MotionResult badDo = c.SetDo(999, true);\n' +
'            if (badDo.Ok) throw new Exception("FAIL at axis-guard: 越界 DO 端口 SetDo(999) 被沉默接受");\n' +
'            MotionResult badAo = c.SetAo(99, 1.0);\n' +
'            if (badAo.Ok) throw new Exception("FAIL at axis-guard: 越界 AO 端口 SetAo(99) 被沉默接受");\n' +
'            Console.WriteLine("[SELFCHECK] axis/port guard OK —— 越界轴号与端口全部显式拒绝 (BEH-08)");\n' +
'        }\n' +
'\n' +
'        // ---- [接缝3] 行为断言自检：shared_eval.json BEH-01/03/04/07/09/14/15 ----\n' +
'        static void BehaviorSelfCheck(IMotionController c)\n' +
'        {\n' +
'            MoveParams bv = new MoveParams(); bv.Vel = 50; bv.Acc = 500;\n' +
'\n' +
'            // [BEH-07] 非法脉冲当量必须显式拒绝（错误码风格）\n' +
'            AxisConfig badScale = new AxisConfig();\n' +
'            badScale.AxisIndex = 7; badScale.PulsesPerUnit = 0;\n' +
'            MotionResult rScale = c.ConfigureAxis(badScale);\n' +
'            if (rScale.Ok) throw new Exception("FAIL at BEH-07: PulsesPerUnit=0 被沉默接受");\n' +
'            Console.WriteLine("[BEH-07] PulsesPerUnit=0 -> " + rScale + " 拒绝 OK");\n' +
'\n' +
'            // [BEH-04] 单位口径：到位准确且指令/反馈同口径\n' +
'            Check(c.MoveTo(0, 10.0, bv, MoveMode.Absolute), "BEH-04 move");\n' +
'            Check(c.WaitMoveDone(0, 10000), "BEH-04 wait");\n' +
'            double cmd = c.GetCommandPos(0), fb = c.GetFeedbackPos(0);\n' +
'            if (Math.Abs(cmd - 10.0) >= 0.001) throw new Exception("FAIL at BEH-04: 指令位置应为 10.0，实际 " + cmd.ToString("F6"));\n' +
'            if (Math.Abs(cmd - fb) > 0.05) throw new Exception("FAIL at BEH-04: 指令/反馈口径不一致 cmd=" + cmd.ToString("F6") + " fb=" + fb.ToString("F6"));\n' +
'            Console.WriteLine("[BEH-04] cmd={0:F3} fb={1:F6} 口径一致 OK", cmd, fb);\n' +
'\n' +
'            // [BEH-14] 相对定位增量正确：10 + rel(5) = 15\n' +
'            Check(c.MoveTo(0, 5.0, bv, MoveMode.Relative), "BEH-14 move rel");\n' +
'            Check(c.WaitMoveDone(0, 10000), "BEH-14 wait");\n' +
'            double p15 = c.GetCommandPos(0);\n' +
'            if (Math.Abs(p15 - 15.0) >= 0.001) throw new Exception("FAIL at BEH-14: 10+5 应为 15.0，实际 " + p15.ToString("F6"));\n' +
'            Console.WriteLine("[BEH-14] 10 + rel(5) = {0:F3} OK", p15);\n' +
'\n' +
'            // [BEH-01] 跟随误差必须非零（抓假反馈）：运动中持续采样\n' +
'            Check(c.MoveTo(0, 60.0, bv, MoveMode.Absolute), "BEH-01 move far");\n' +
'            double maxErr = 0.0;\n' +
'            while ((c.GetState(0) & AxisState.Moving) == AxisState.Moving)\n' +
'            {\n' +
'                double e = Math.Abs(c.GetCommandPos(0) - c.GetFeedbackPos(0));\n' +
'                if (e > maxErr) maxErr = e;\n' +
'            }\n' +
'            if (maxErr < 0.0001) throw new Exception("FAIL at BEH-01: 运动中跟随误差恒为 0——反馈是假数据（指令副本）");\n' +
'            Console.WriteLine("[BEH-01] 运动中最大跟随误差 = {0:F6} 非零 OK", maxErr);\n' +
'            Check(c.WaitMoveDone(0, 10000), "BEH-01 settle");\n' +
'\n' +
'            // [BEH-03] 点动方向必须与速度符号一致\n' +
'            double j0 = c.GetCommandPos(0);\n' +
'            Check(c.MoveJog(0, 10.0, bv), "BEH-03 jog+");\n' +
'            Check(c.WaitMoveDone(0, 5000), "BEH-03 jog+ wait");\n' +
'            double j1 = c.GetCommandPos(0);\n' +
'            if (j1 <= j0) throw new Exception("FAIL at BEH-03: 正速度点动后位置必须增加 " + j0.ToString("F3") + " -> " + j1.ToString("F3"));\n' +
'            Check(c.MoveJog(0, -10.0, bv), "BEH-03 jog-");\n' +
'            Check(c.WaitMoveDone(0, 5000), "BEH-03 jog- wait");\n' +
'            double j2 = c.GetCommandPos(0);\n' +
'            if (j2 >= j1) throw new Exception("FAIL at BEH-03: 负速度点动后位置必须减少 " + j1.ToString("F3") + " -> " + j2.ToString("F3"));\n' +
'            Console.WriteLine("[BEH-03] jog +10 -> {0:F3}，-10 -> {1:F3} 方向一致 OK", j1, j2);\n' +
'\n' +
'            // [BEH-09] 停止后不再处于运动中\n' +
'            Check(c.MoveTo(0, 400.0, bv, MoveMode.Absolute), "BEH-09 move far");\n' +
'            Check(c.Stop(0, true), "BEH-09 emergency stop");\n' +
'            if ((c.GetState(0) & AxisState.Moving) == AxisState.Moving)\n' +
'                throw new Exception("FAIL at BEH-09: 紧急停止后仍处于运动中");\n' +
'            Console.WriteLine("[BEH-09] 紧急停止后 Moving=0 OK");\n' +
'\n' +
'            // [BEH-15] 回零后位置归零（回零是工件坐标的基准）\n' +
'            Check(c.Home(0), "BEH-15 home");\n' +
'            Check(c.WaitHomeDone(0, 30000), "BEH-15 home wait");\n' +
'            double h = c.GetCommandPos(0);\n' +
'            if (Math.Abs(h) >= 0.001) throw new Exception("FAIL at BEH-15: 回零后位置应为 0，实际 " + h.ToString("F6"));\n' +
'            Console.WriteLine("[BEH-15] 回零后位置 = {0:F6} 归零 OK", h);\n' +
'        }\n' +
'\n' +
'        static void SpcReport(IMotionController c)\n' +
'        {\n' +
'            // 连续采样压力通道，计算 Cpk（缺胶/溢胶判定 = 规格限判定，JD职责6/8）\n' +
'            List<double> x = new List<double>();\n' +
'            for (int i = 0; i < 32; i++) { x.Add(c.GetAi(0)); Thread.Sleep(25); }\n' +
'            double mean = 0; foreach (double v in x) mean += v; mean /= x.Count;\n' +
'            double var = 0; foreach (double v in x) var += (v - mean) * (v - mean); var /= (x.Count - 1);\n' +
'            double sigma = Math.Sqrt(var);\n' +
'            double usl = 3.0, lsl = 2.0;\n' +
'            double cpk = sigma > 1e-9 ? Math.Min(usl - mean, mean - lsl) / (3 * sigma) : 99.0;\n' +
'            double cp = sigma > 1e-9 ? (usl - lsl) / (6 * sigma) : 99.0;\n' +
'            Console.WriteLine("[SPC] n={0} mean={1:F4} sigma={2:F4} Cp={3:F2} Cpk={4:F2}", x.Count, mean, sigma, cp, cpk);\n' +
'            Console.WriteLine("[SPC] 判定: {0}", cpk >= 1.33 ? "合格 (Cpk>=1.33)" : "需调整工艺参数");\n' +
'\n' +
'            using (StreamWriter w = new StreamWriter("glue_report.csv"))\n' +
'            {\n' +
'                w.WriteLine("sample,pressure_V");\n' +
'                for (int i = 0; i < x.Count; i++)\n' +
'                    w.WriteLine(i.ToString() + "," + x[i].ToString("F5", CultureInfo.InvariantCulture));\n' +
'                w.WriteLine("mean," + mean.ToString("F5", CultureInfo.InvariantCulture));\n' +
'                w.WriteLine("sigma," + sigma.ToString("F5", CultureInfo.InvariantCulture));\n' +
'                w.WriteLine("cpk," + cpk.ToString("F3", CultureInfo.InvariantCulture));\n' +
'            }\n' +
'        }\n' +
'\n' +
'        static void Check(MotionResult r, string tag)\n' +
'        {\n' +
'            Console.WriteLine("  " + r + "  <" + tag + ">");\n' +
'            if (!r.Ok) throw new Exception("FAIL at " + tag + ": " + r);\n' +
'        }\n' +
'    }\n' +
'}\n' };
  }

  // ============ 差异矩阵 + 核对清单 + README + 构建脚本 ============
  function docFiles(cfg, adapters) {
    var f = [];
    f.push({ path: 'docs/CapabilityMatrix.md', content:
'# 运动控制卡能力差异矩阵（JD职责3: 沉淀各品牌能力差异与性能边界）\n' +
'\n' +
'> 生成时间: ' + TS + ' | 依据: 厂商公开手册常识整理 + 行业经验，**上真机前以官方最新手册为准**\n' +
'\n' +
'| 能力 | 固高 GTS/GTN | 雷赛 DMC/EMC | 正运动 ZMC | ACS SPiiPlus | 软赢 | 高川 |\n' +
'|---|---|---|---|---|---|---|\n' +
'| 总线形态 | 脉冲+部分EtherCAT | 脉冲+总线 | EtherCAT/脉冲 | EtherCAT | 以手册为准 | 以手册为准 |\n' +
'| 直线插补 | 坐标系 2-4 轴 | 多轴 | 多轴 | 多轴 | - | - |\n' +
'| 圆弧插补 | 2D/3D | 2D | 2D/空间 | 2D/3D | - | - |\n' +
'| S 曲线 | S 段参数 | 平滑因子 | 平滑/前瞻 | 高级(前瞻) | - | - |\n' +
'| 位置比较输出(飞拍) | 支持 | 支持 | 支持(MOVECMP) | 支持(MEP) | - | - |\n' +
'| 模拟量 AO(力控) | 部分型号 | 部分型号 | 支持 | 支持 | - | - |\n' +
'| SDK 形态 | gts.dll (C API) | LTDMC.dll (C API) | zauxdll.dll (C API) | ACS.SPiiPlusNET (.NET) | - | - |\n' +
'| .NET 调用 | P/Invoke | P/Invoke | P/Invoke | 直接引用 | - | - |\n' +
'| 调试工具 | GTN Demo | 雷赛调试软件 | ZDevelop | ACS C++/SPiiPlus MMI | - | - |\n' +
'| 本仓库适配级别 | B | B | B | B(需厂商DLL,不在全品牌套件) | C(骨架) | C(骨架) |\n' +
'\n' +
'## 适配级别说明\n' +
'- **A级**: 虚拟控制器 —— 100% 自研可运行（仿真）\n' +
'- **B级**: 核心 API 已映射（Open/使能/回零/点位/IO/位置），插补给出调用结构；**SDK 函数名与参数表已按官方真值（PE 导出表 + 官方 C# 绑定 / C 头文件）逐条核对**，仍带 [核对] 标记的项（单位/量程/IO 极性/模式号等）须按厂商手册确认。注意：全品牌套件不含 ACS（编译需官方 ACS.SPiiPlusNET.dll）；要用 ACS 请单独生成 ACS 套件并安装 ACS 软件\n' +
'- **C级**: 骨架 + 完整 TODO 清单，用于二次开发起点\n' +
'\n' +
'## 单位换算约定（适配层统一处理）\n' +
'- 用户单位: mm / 度；`AxisConfig.PulsesPerUnit` = 每毫米脉冲数\n' +
'- 总线卡（ZMC/ACS）内部支持当量标定，适配层同样走 PulsesPerUnit 归一，保证业务层语义一致\n' });

    f.push({ path: 'docs/ApiChecklist.md', content: apiChecklist(cfg, adapters) });
    return f;
  }

  function apiChecklist(cfg, adapters) {
    var s = '# P/Invoke 签名核对清单（上真机前必做）\n\n' +
      '> 生成时间: ' + TS + '\n' +
      '> **同源声明（重要）**: 本清单由生成器内置知识库派生，与适配器代码同源。\n' +
      '> 它只能索引"去哪里核对"，不能证明签名正确——签名若有错，错误会被原样列进清单。\n' +
      '> 唯一有效依据是厂商官方手册/头文件（外部信息源），须逐条比对（含函数名、参数个数/类型/方向、轴号基数、IO 极性）。\n' +
      '> 比对工时是上真机工作的主体，请按实际手册篇幅估算，不要以本清单条数推断"半天即可"。\n\n' +
      '> **核对记录（verified_against）**: "核对记录"列由工程师填写，格式 `手册章节@手册版本 结论 日期`（例: `§4.2@LTDMC-2.11 一致 2026-10-20`；不一致须在"结论"写差异并停用对应方法）。\n' +
      '> **k=0 规则**: 只要"已核对 k / 总 n"的 k < n，本套件状态为**未核对**，不得标记可用/交付/上真机。\n' +
      '> **k 的口径**: k 只统计"函数名/参数表已在厂商官方真值（PE导出表 + 官方C#绑定/C头文件）里核对到"的条目；\n' +
      '> 标注「未实现」的条目（官方真值里没有等价函数，已从代码里删除该臆造声明）计入总 n、不计入 k。\n' +
      '> **持久化警告**: 重新生成会整表覆盖（核对记录随之下丢失）——核对前先把本文件提交版本库或移出生成目录，重生成后回填。\n\n';
    var total = 0, totalOk = 0;
    for (var i = 0; i < adapters.length; i++) {
      var a = adapters[i];
      if (!a.api || a.id === 'virtual') continue;
      total += a.api.length;
      var ok = 0;
      for (var j = 0; j < a.api.length; j++) { if (a.api[j].ok === true) ok++; }
      totalOk += ok;
      s += '## ' + a.label + ' (' + a.dll + ') —— 已核对 ' + ok + ' / ' + a.api.length + '\n\n| API | 用途 | 核对要点 | 核对记录(章节@rev 结论 日期) |\n|---|---|---|---|\n';
      for (var j2 = 0; j2 < a.api.length; j2++) {
        var it = a.api[j2];
        s += '| `' + it.f + '` | ' + it.use + ' | ' + it.chk + ' | （待填） |\n';
      }
      s += '\n';
    }
    s += '## 总进度: 已核对 ' + totalOk + ' / ' + total + '（k=已对官方真值核对的条数；未实现项计入 n 不计入 k——见头部 k=0 规则）\n\n';
    s += '## 通用核对流程（每品牌）\n\n' +
      '1. 用厂商 Demo 软件手动走一遍: 使能 -> 回零 -> 低速点位 -> 停止\n' +
      '2. 按本表逐行核对签名（参数个数/类型/方向），**当场填写核对记录列**并更新 k/n\n' +
      '3. 运行本工程 `bin/GlueDemo.exe`，观察 `[OK]` 日志与实际轴动作\n' +
      '4. 低速（<10mm/s）验证方向与当量，再逐步提速\n' +
      '5. 验证急停与限位后再进入工艺联调\n';
    return s;
  }

  function buildFiles(cfg) {
    var f = [];
    var acsNote = (cfg && cfg.brandId === 'acs')
      ? 'rem [ACS] 本套件含 ACS 适配器，编译需官方 ACS.SPiiPlusNET.dll：\n' +
        'rem       安装 ACS 软件后，在下行追加   /r:ACS.SPiiPlusNET.dll   （并确保 DLL 在同目录或 GAC）\n'
      : '';
    f.push({ path: 'build/build_net48.bat', content:
'@echo off\r\n' +
'rem ===== Windows 自带 .NET Framework 编译（零安装，验证代码可编译） =====\r\n' +
acsNote +
'set CSC=%WINDIR%\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe\r\n' +
'cd /d %~dp0\r\n' +
'if not exist ..\\bin mkdir ..\\bin\r\n' +
'"%CSC%" /nologo /target:exe /out:..\\bin\\GlueDemo.exe ^\r\n' +
'  ..\\src\\Core\\*.cs ..\\src\\Virtual\\*.cs ..\\src\\Adapters\\*.cs ..\\samples\\*.cs\r\n' +
'if %errorlevel%==0 (\r\n' +
'  echo BUILD OK\r\n' +
'  ..\\bin\\GlueDemo.exe\r\n' +
') else (\r\n' +
'  echo BUILD FAILED\r\n' +
')\r\n' +
'pause\r\n' });

    f.push({ path: 'build/GlueDemo.csproj', content:
'<?xml version="1.0" encoding="utf-8"?>\n' +
'<Project Sdk="Microsoft.NET.Sdk">\n' +
'  <PropertyGroup>\n' +
'    <OutputType>Exe</OutputType>\n' +
'    <TargetFramework>net8.0</TargetFramework>\n' +
'    <RootNamespace>MotionControl</RootNamespace>\n' +
'    <AssemblyName>GlueDemo</AssemblyName>\n' +
'  </PropertyGroup>\n' +
'</Project>\n' +
'<!-- 用法: 把本文件放到工程根目录, dotnet build 即可（源码 C#5 兼容, 无改动迁移） -->\n' });
    return f;
  }

  function readme(cfg, adapters) {
    // [修复] 原来只读 cfg.brandLabel，而 index.js 传的是第二参数，导致抬头恒为 "undefined"
    var brand = (adapters && adapters.brandLabel) || cfg.brandLabel || '多品牌套件';
    var processLabel = (adapters && adapters.processLabel) || cfg.processLabel || '通用';
    return { path: 'README.md', content:
'# MotionControlKit - ' + brand + ' 上位机工程包\n\n' +
'> 生成工具: **上位机代码工厂 v1.0** | 时间: ' + TS + ' | 轴数: ' + cfg.axisCount + ' | 工艺: ' + processLabel + '\n\n' +
'## 这是什么\n\n' +
'按「分层架构 + 设备抽象层」生成的可编译 C# 工程（JD 职责2/3 的落地）：\n\n' +
'```\n' +
'src/Core/       抽象层: IMotionController 接口/配置/结果对象/工厂  <- 业务唯一依赖\n' +
'src/Adapters/   适配层: ' + brand + ' (P/Invoke 封装, 单位换算, 线程锁)\n' +
'src/Virtual/    虚拟控制器: 无硬件全流程仿真/单元测试\n' +
'samples/        工艺流程示例(换卡只改一行)\n' +
'docs/           能力差异矩阵 + API 核对清单\n' +
'build/          csc(零安装) 与 dotnet 两套构建脚本\n' +
'```\n\n' +
'## 30 秒跑起来（无需任何硬件/安装）\n\n' +
'```\n' +
'双击 build\\build_net48.bat     # Windows 自带编译器, 编译并运行虚拟控制器 Demo\n' +
'```\n\n' +
'## 换卡 = 改一行\n\n' +
'`samples/Program.cs`:\n\n' +
'```csharp\n' +
'const Brand CREATE_BRAND = Brand.Virtual;   // 换成 Brand.Leadshine / Brand.GoogolGts ...\n' +
'```\n\n' +
'## 上真机流程\n\n' +
'1. 阅读 docs/ApiChecklist.md（先看头部"同源声明"）：以厂商官方手册为唯一依据逐条比对签名\n' +
'2. 厂商 Demo 手动验证轴动作\n' +
'3. 低速运行 GlueDemo，逐步提速\n' +
'4. 按 docs/CapabilityMatrix.md 评估该卡是否支持位置比较/飞拍/力控等进阶能力\n\n' +
'## 工程规范（JD职责10）\n\n' +
'- 所有适配器错误码统一映射到 MotionResult\n' +
'- SDK 调用统一持锁（`lock(_sdk)`）串行化\n' +
'- 单位换算集中在适配层，业务层只用用户单位\n' +
'- VirtualController 用于单元测试与 CI 回归\n' };
  }

  return {
    coreFiles: coreFiles,
    virtualFile: virtualFile,
    factoryFile: factoryFile,
    sampleFile: sampleFile,
    docFiles: docFiles,
    buildFiles: buildFiles,
    readme: readme
  };
});
