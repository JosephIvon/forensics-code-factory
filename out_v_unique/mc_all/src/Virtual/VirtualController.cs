using System;
using System.Collections.Generic;
using System.Threading;
using MotionControl.Core;

namespace MotionControl.Virtual
{
    /// <summary>
    /// 虚拟运动控制器（与真实适配器同一接口），用途:
    ///   1) 无硬件开发/联调业务流程（架构先行，JD职责2）
    ///   2) 单元测试与 CI 回归（JD职责10 单元测试规范）
    ///   3) 客户现场前的工艺流程预演
    /// 运动学为匀速段仿真 + 惰性推进（不占 CPU）；固定随机种子保证可复现。
    /// </summary>
    public class VirtualController : IMotionController
    {
        private class AxisSim
        {
            public double CmdPos, FbPos;
            public double From, To, Duration;
            public DateTime Start;
            public bool Moving, Enabled, Homed, Homing;
            public DateTime HomeStart;
            public double HomeOffset;
        }

        private class Segment
        {
            public int[] Axes; public double[] Target; public double Duration;
        }

        private readonly Dictionary<int, AxisSim> _axes = new Dictionary<int, AxisSim>();
        private readonly Queue<Segment> _queue = new Queue<Segment>();
        private readonly Dictionary<int, bool> _doMap = new Dictionary<int, bool>();
        private readonly Dictionary<int, double> _aoMap = new Dictionary<int, double>();
        private readonly object _lock = new object();
        private readonly DateTime _boot = DateTime.Now;
        private readonly Random _rnd = new Random(12345);
        private bool _open;

        public Brand Brand { get { return Brand.Virtual; } }
        public string DeviceName { get { return "VirtualController-8Axis-Sim"; } }
        public bool IsOpen { get { return _open; } }

        public VirtualController()
        {
            for (int i = 0; i < 8; i++) _axes[i] = new AxisSim();
        }

        public MotionResult Open(ControllerConnectInfo info)
        {
            lock (_lock)
            {
                if (_open) return MotionResult.Success("already open");
                _open = true;
                _queue.Clear();
                return MotionResult.Success("virtual controller opened");
            }
        }

        public MotionResult Close()
        {
            lock (_lock)
            {
                _open = false;
                _queue.Clear();
                return MotionResult.Success();
            }
        }

        public void Dispose() { Close(); }

        public MotionResult ConfigureAxis(AxisConfig cfg)
        {
            lock (_lock)
            {
                if (cfg == null) return MotionResult.Fail(-6, "null config");
                MotionResult gCfg = CheckAxis(cfg.AxisIndex); if (!gCfg.Ok) return gCfg;
                if (cfg.PulsesPerUnit <= 0) return MotionResult.Fail(-9, "PulsesPerUnit must be > 0, got " + cfg.PulsesPerUnit);
                AxisSim a = GetAxis(cfg.AxisIndex);
                a.HomeOffset = cfg.Home.Offset;
                return MotionResult.Success("axis " + cfg + " configured (virtual)");
            }
        }

        public MotionResult Enable(int axis, bool on)
        {
            lock (_lock)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                MotionResult g = CheckAxis(axis); if (!g.Ok) return g;
                AxisSim a = GetAxis(axis);
                a.Enabled = on;
                return MotionResult.Success();
            }
        }

        public AxisState GetState(int axis)
        {
            lock (_lock)
            {
                AxisSim a = GetAxis(axis);
                Update();
                AxisState s = AxisState.None;
                if (a.Enabled) s |= AxisState.Enabled;
                if (a.Moving) s |= AxisState.Moving; else s |= AxisState.InPosition;
                if (a.Homed) s |= AxisState.Homed;
                if (a.Homing) s |= AxisState.Homing;
                return s;
            }
        }

        public double GetCommandPos(int axis) { lock (_lock) { Update(); return GetAxis(axis).CmdPos; } }
        public double GetFeedbackPos(int axis)
        {
            lock (_lock)
            {
                Update();
                // 模拟 2um 反馈噪声（再现 SPC 统计场景）
                return GetAxis(axis).FbPos + (_rnd.NextDouble() - 0.5) * 0.002;
            }
        }
        public double GetFollowingError(int axis) { lock (_lock) { Update(); return GetAxis(axis).CmdPos - GetAxis(axis).FbPos; } }

        public MotionResult Home(int axis)
        {
            lock (_lock)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                MotionResult g = CheckAxis(axis); if (!g.Ok) return g;
                AxisSim a = GetAxis(axis);
                a.Homing = true;
                a.Homed = false; // [接缝3·BEH-15] 回零进行中不算已回零：不清此标志，WaitHomeDone 对已回零轴立即返回，重回零根本没等（行为断言抓到的真 bug）
                a.HomeStart = DateTime.Now;
                return MotionResult.Success("homing started (virtual, ~1.2s)");
            }
        }

        public bool IsHomed(int axis) { lock (_lock) { Update(); return GetAxis(axis).Homed; } }

        public MotionResult WaitHomeDone(int axis, int timeoutMs)
        {
            MotionResult g0 = CheckAxis(axis); if (!g0.Ok) return g0;
            DateTime t0 = DateTime.Now;
            while ((DateTime.Now - t0).TotalMilliseconds < timeoutMs)
            {
                lock (_lock)
                {
                    Update();
                    AxisSim a = GetAxis(axis);
                    if (a.Homed) return MotionResult.Success();
                }
                Thread.Sleep(10);
            }
            return MotionResult.Fail(-2, "home timeout axis " + axis);
        }

        public MotionResult MoveTo(int axis, double posUser, MoveParams p, MoveMode mode)
        {
            lock (_lock)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                MotionResult g = CheckAxis(axis); if (!g.Ok) return g;
                double from = GetAxis(axis).CmdPos;
                double to = (mode == MoveMode.Absolute) ? posUser : from + posUser;
                EnqueueSegment(new int[] { axis }, new double[] { to }, p);
                return MotionResult.Success();
            }
        }

        public MotionResult MoveJog(int axis, double velUserPerSec, MoveParams p)
        {
            lock (_lock)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                MotionResult g = CheckAxis(axis); if (!g.Ok) return g;
                // 虚拟点动: 以 1 秒为目标步进的持续运动
                double to = GetAxis(axis).CmdPos + velUserPerSec * 1.0;
                MoveParams pj = new MoveParams();
                pj.Vel = Math.Abs(velUserPerSec); pj.Acc = p.Acc; pj.Dec = p.Dec;
                EnqueueSegment(new int[] { axis }, new double[] { to }, pj);
                return MotionResult.Success();
            }
        }

        public MotionResult Stop(int axis, bool emergency)
        {
            lock (_lock)
            {
                MotionResult g = CheckAxis(axis); if (!g.Ok) return g;
                _queue.Clear();
                AxisSim a = GetAxis(axis);
                a.Moving = false;
                return MotionResult.Success(emergency ? "emergency stop" : "stop");
            }
        }

        public MotionResult WaitMoveDone(int axis, int timeoutMs)
        {
            MotionResult g0 = CheckAxis(axis); if (!g0.Ok) return g0;
            DateTime t0 = DateTime.Now;
            while ((DateTime.Now - t0).TotalMilliseconds < timeoutMs)
            {
                lock (_lock)
                {
                    Update();
                    if (!GetAxis(axis).Moving && _queue.Count == 0) return MotionResult.Success();
                }
                Thread.Sleep(10);
            }
            return MotionResult.Fail(-3, "move timeout axis " + axis);
        }

        public MotionResult MoveLine(int[] axisList, double[] targetUser, MoveParams p, MoveMode mode)
        {
            lock (_lock)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                if (axisList == null || targetUser == null || axisList.Length != targetUser.Length)
                    return MotionResult.Fail(-4, "axisList/targetUser length mismatch");
                for (int gi = 0; gi < axisList.Length; gi++) { MotionResult gL = CheckAxis(axisList[gi]); if (!gL.Ok) return gL; }
                double[] to = new double[axisList.Length];
                double distSq = 0;
                for (int i = 0; i < axisList.Length; i++)
                {
                    double from = GetAxis(axisList[i]).CmdPos;
                    to[i] = (mode == MoveMode.Absolute) ? targetUser[i] : from + targetUser[i];
                    distSq += (to[i] - from) * (to[i] - from);
                }
                EnqueueSegment(axisList, to, p);
                return MotionResult.Success("line segment queued, pathLen=" + Math.Sqrt(distSq).ToString("F3"));
            }
        }

        public MotionResult MoveArc2D(int axisX, int axisY, double i, double j, double endX, double endY, bool cw, MoveParams p)
        {
            lock (_lock)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                MotionResult gX = CheckAxis(axisX); if (!gX.Ok) return gX;
                MotionResult gY = CheckAxis(axisY); if (!gY.Ok) return gY;
                double x0 = GetAxis(axisX).CmdPos, y0 = GetAxis(axisY).CmdPos;
                double cx = x0 + i, cy = y0 + j;
                double r = Math.Sqrt(i * i + j * j);
                double a0 = Math.Atan2(y0 - cy, x0 - cx);
                double a1 = Math.Atan2(endY - cy, endX - cx);
                double sweep = a1 - a0;
                if (cw && sweep >= 0) sweep -= 2 * Math.PI;
                if (!cw && sweep <= 0) sweep += 2 * Math.PI;
                int n = 32;
                for (int k = 1; k <= n; k++)
                {
                    double a = a0 + sweep * k / n;
                    double tx = cx + r * Math.Cos(a);
                    double ty = cy + r * Math.Sin(a);
                    double[] prev = (k == 1)
                        ? new double[] { x0, y0 }
                        : new double[] { cx + r * Math.Cos(a0 + sweep * (k - 1) / n), cy + r * Math.Sin(a0 + sweep * (k - 1) / n) };
                    double seg = Math.Sqrt((tx - prev[0]) * (tx - prev[0]) + (ty - prev[1]) * (ty - prev[1]));
                    MoveParams ps = new MoveParams();
                    ps.Vel = p.Vel * 32 / n; ps.Acc = p.Acc; ps.Dec = p.Dec;
                    EnqueueSegment(new int[] { axisX, axisY }, new double[] { tx, ty }, ps);
                }
                EnqueueSegment(new int[] { axisX, axisY }, new double[] { endX, endY }, p);
                return MotionResult.Success("arc queued, r=" + r.ToString("F3") + ", sweepDeg=" + (sweep * 180 / Math.PI).ToString("F1"));
            }
        }

        public MotionResult SetDo(int index, bool on)
        {
            lock (_lock) { MotionResult gP = CheckPort("DO", index, DO_MAX); if (!gP.Ok) return gP; _doMap[index] = on; return MotionResult.Success("DO" + index + "=" + on); }
        }

        public bool GetDi(int index)
        {
            lock (_lock)
            {
                if (index < 0 || index > DI_MAX) throw new ArgumentOutOfRangeException("index", index, "DI 0-" + DI_MAX);
                double t = (DateTime.Now - _boot).TotalSeconds;
                // 周期性翻转的虚拟 DI（周期~10s/位错开），保证 WaitDi 可被满足
                return Math.Sin(t * 0.6 + index * 1.7) > 0;
            }
        }

        public double GetAi(int index)
        {
            lock (_lock)
            {
                if (index < 0 || index > AI_MAX) throw new ArgumentOutOfRangeException("index", index, "AI 0-" + AI_MAX);
                double t = (DateTime.Now - _boot).TotalSeconds;
                // 2.5V +/-0.08V 正弦 + 10mV 噪声 —— 模拟压力/胶量传感器
                return 2.5 + 0.08 * Math.Sin(t * 2.0 + index * 0.7) + (_rnd.NextDouble() - 0.5) * 0.02;
            }
        }

        public MotionResult SetAo(int index, double volts)
        {
            lock (_lock) { MotionResult gA = CheckPort("AO", index, AO_MAX); if (!gA.Ok) return gA; _aoMap[index] = volts; return MotionResult.Success("AO" + index + "=" + volts); }
        }

        public MotionResult WaitDi(int index, bool level, int timeoutMs)
        {
            MotionResult gW = CheckPort("DI", index, DI_MAX); if (!gW.Ok) return gW;
            DateTime t0 = DateTime.Now;
            while ((DateTime.Now - t0).TotalMilliseconds < timeoutMs)
            {
                if (GetDi(index) == level) return MotionResult.Success();
                Thread.Sleep(10);
            }
            return MotionResult.Fail(-5, "wait DI" + index + "=" + level + " timeout");
        }

        // ---------------- 内部: 惰性推进仿真 ----------------
        // ---- [接缝3·BEH-08] 参数守卫 ----
        // 返回 MotionResult 的方法：越界必须返回 Fail（错误码风格，与本套件一致），
        // 不许靠深处异常冒泡、更不许沉默接受。真机适配器抄这里时必须保留同语义校验——
        // 越界轴号在真机上是非法内存访问（栈损坏级）。
        private const int AXIS_MAX = 7, DI_MAX = 15, DO_MAX = 15, AI_MAX = 7, AO_MAX = 7;

        private MotionResult CheckAxis(int axis)
        {
            if (axis < 0 || axis > AXIS_MAX) return MotionResult.Fail(-7, "axis " + axis + " out of range 0-" + AXIS_MAX);
            return MotionResult.Success();
        }

        private MotionResult CheckPort(string kind, int index, int max)
        {
            if (index < 0 || index > max) return MotionResult.Fail(-8, kind + " index " + index + " out of range 0-" + max);
            return MotionResult.Success();
        }

        private AxisSim GetAxis(int axis)
        {
            AxisSim a;
            if (!_axes.TryGetValue(axis, out a)) throw new ArgumentOutOfRangeException("axis", axis, "virtual controller supports 0-7");
            return a;
        }

        private void EnqueueSegment(int[] axes, double[] target, MoveParams p)
        {
            double maxDelta = 0;
            for (int i = 0; i < axes.Length; i++)
            {
                double d = Math.Abs(target[i] - GetAxis(axes[i]).CmdPos);
                if (d > maxDelta) maxDelta = d;
            }
            double vel = Math.Max(p.Vel, 0.001);
            double duration = maxDelta / vel + vel / Math.Max(p.Acc, 1); // 匀速+加减速近似
            Segment s = new Segment();
            s.Axes = axes; s.Target = target; s.Duration = Math.Max(duration, 0.001);
            _queue.Enqueue(s);
        }

        private void Update()
        {
            DateTime now = DateTime.Now;
            foreach (KeyValuePair<int, AxisSim> kv in _axes)
            {
                AxisSim a = kv.Value;
                if (a.Moving)
                {
                    double k = (now - a.Start).TotalSeconds / a.Duration;
                    if (k >= 1.0)
                    {
                        a.CmdPos = a.To; a.FbPos = a.To; a.Moving = false;
                    }
                    else
                    {
                        double pos = a.From + (a.To - a.From) * k;
                        a.CmdPos = pos; a.FbPos = pos - Math.Sin(k * Math.PI) * 0.001; // 1um 级跟随误差
                    }
                }
                if (a.Homing && (now - a.HomeStart).TotalSeconds > 1.2)
                {
                    a.Homing = false; a.Homed = true;
                    a.CmdPos = a.HomeOffset; a.FbPos = a.HomeOffset; a.From = a.HomeOffset; a.To = a.HomeOffset;
                }
            }
            bool anyMoving = false;
            foreach (KeyValuePair<int, AxisSim> kv in _axes) if (kv.Value.Moving) { anyMoving = true; break; }
            if (!anyMoving && _queue.Count > 0)
            {
                Segment s = _queue.Dequeue();
                for (int i = 0; i < s.Axes.Length; i++)
                {
                    AxisSim a = GetAxis(s.Axes[i]);
                    a.From = a.CmdPos; a.To = s.Target[i]; a.Start = now; a.Duration = s.Duration; a.Moving = true;
                }
            }
        }
    }
}
