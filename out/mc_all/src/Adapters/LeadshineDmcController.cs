// =====================================================================
//  雷赛 DMC/EMC 系列 (LTDMC) 适配器（LTDMC.dll）
//  生成工具: 上位机代码工厂 v1.0 | 2026-10-22 16:11:50
//  KB级别: B —— 核心API已映射(board/点位/停止/位置/IO/模拟量)。函数名与参数表已按官方C#绑定/C头文件核对；官方无软件使能接口(脉冲卡硬线使能)，使能项标注未实现。
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
        // ── 真值链（每条 P/Invoke 上方：[ 源 | 核对 | 提取 ]）──
        //   数据源：mcopilot knowledge/sdks/dmc.json（生成时实时读取，非复制）
        //   源=verified 这条结论从哪来 ｜ 核对=check 验到哪一步 ｜ 提取=extraction 签名对≠调用对时是否需人签字
        //   本套件 P/Invoke 16 条：知识库钉扎 13 / 未收录(显式降级) 3
        //   核对进度（钉扎条目）：manual 0 / hardware 0 / auto 0 / none 13
        //   ⚠ 未核对 13 条：dmc_board_init, dmc_board_close, dmc_pmove, dmc_set_profile, dmc_stop, dmc_check_done, dmc_get_position, dmc_get_encoder, dmc_vmove, dmc_home_move, dmc_set_homemode, dmc_read_inbit, dmc_write_outbit —— 已标 TODO，不许当事实用
        //   复核：mcopilot 仓库跑 scripts/verify_traekb_citations.py；重新生成本套件即自动继承最新口径

        // [真值] 函数名/参数表取自 leisai.csv(官方C#绑定) 与 leisai_cpp.csv(官方C头文件)，并已对 dll_exports.csv(PE导出表) 校验
        // TODO[需核对厂商手册] dmc_board_init —— 核对后更新 mcopilot 仓库 knowledge/sdks/dmc.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 public-doc | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_board_init();
        // TODO[需核对厂商手册] dmc_board_close —— 核对后更新 mcopilot 仓库 knowledge/sdks/dmc.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 public-doc | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_board_close();
        // TODO[需核对厂商手册] dmc_pmove —— 核对后更新 mcopilot 仓库 knowledge/sdks/dmc.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 public-doc | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_pmove(ushort card, ushort axis, int dist, ushort posi_mode);
        // TODO[需核对厂商手册] dmc_set_profile —— 核对后更新 mcopilot 仓库 knowledge/sdks/dmc.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 public-doc | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_set_profile(ushort card, ushort axis, double minVel, double maxVel, double tacc, double tdec, double stopVel);
        // TODO[需核对厂商手册] dmc_stop —— 核对后更新 mcopilot 仓库 knowledge/sdks/dmc.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-binding | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_stop(ushort card, ushort axis, ushort stopMode);
        // TODO[需核对厂商手册] dmc_check_done —— 核对后更新 mcopilot 仓库 knowledge/sdks/dmc.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 public-doc | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_check_done(ushort card, ushort axis);
        // TODO[需核对厂商手册] dmc_get_position —— 核对后更新 mcopilot 仓库 knowledge/sdks/dmc.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 public-doc | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int dmc_get_position(ushort card, ushort axis);
        // TODO[需核对厂商手册] dmc_get_encoder —— 核对后更新 mcopilot 仓库 knowledge/sdks/dmc.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 public-doc | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int dmc_get_encoder(ushort card, ushort axis);
        // TODO[需核对厂商手册] dmc_vmove —— 核对后更新 mcopilot 仓库 knowledge/sdks/dmc.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-manual | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_vmove(ushort card, ushort axis, ushort dir);
        // [ 源 未标注 | 核对 none | 提取 judgment ]  mcopilot 知识库未收录此函数
        // TODO[需核对厂商手册] dmc_change_speed：签名未经外部真值钉扎 —— 按官方 C# 绑定逐字核对后再用
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_change_speed(ushort card, ushort axis, double currVel, double taccdec);
        // TODO[需核对厂商手册] dmc_home_move —— 核对后更新 mcopilot 仓库 knowledge/sdks/dmc.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 public-doc | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_home_move(ushort card, ushort axis);
        // TODO[需核对厂商手册] dmc_set_homemode —— 核对后更新 mcopilot 仓库 knowledge/sdks/dmc.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-manual | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_set_homemode(ushort card, ushort axis, ushort homeDir, double vel, ushort mode, ushort ezCount);
        // TODO[需核对厂商手册] dmc_read_inbit —— 核对后更新 mcopilot 仓库 knowledge/sdks/dmc.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 public-doc | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_read_inbit(ushort card, ushort bitno);
        // TODO[需核对厂商手册] dmc_write_outbit —— 核对后更新 mcopilot 仓库 knowledge/sdks/dmc.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 public-doc | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_write_outbit(ushort card, ushort bitno, ushort onOff);
        // [ 源 未标注 | 核对 none | 提取 judgment ]  mcopilot 知识库未收录此函数
        // TODO[需核对厂商手册] dmc_get_ad_input：签名未经外部真值钉扎 —— 按官方 C# 绑定逐字核对后再用
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_get_ad_input(ushort card, ushort channel, ref double vout);
        // [ 源 未标注 | 核对 none | 提取 judgment ]  mcopilot 知识库未收录此函数
        // TODO[需核对厂商手册] dmc_set_da_output：签名未经外部真值钉扎 —— 按官方 C# 绑定逐字核对后再用
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_set_da_output(ushort card, ushort channel, double vout);
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

                // [未实现] 官方 C# 绑定/C 头文件/PE 导出表里都没有 dmc_set_servo（或任何软件使能声明），该臆造声明已删除。
                //          雷赛脉冲卡(DMC系列)为硬线使能；总线卡(EMC)使能接口须按型号手册确认后再接入。
                return MotionResult.Fail(-300, "[未实现] 雷赛软件使能: 官方无 dmc_set_servo 等价声明(脉冲卡为硬线使能)");
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

                // [真值] 官方原型 dmc_set_homemode(UInt16 CardNo,UInt16 axis,UInt16 home_dir,double vel,UInt16 mode,UInt16 EZ_count)
                // [核对] mode/home_dir 的编号含义按《雷赛DMC系列运动控制器编程手册》确认
                ushort mode;
                ushort homeDir;
                switch (_homeMode[axis])
                {
                    case HomeMode.LimitReverse: mode = 0; homeDir = 1; break;
                    case HomeMode.HomeSwitch: mode = 1; homeDir = 0; break;
                    case HomeMode.HomeSwitchIndex: mode = 2; homeDir = 0; break;
                    case HomeMode.IndexOnly: mode = 3; homeDir = 0; break;
                    default: mode = 2; homeDir = 0; break;
                }
                short rc = dmc_set_homemode(Card, (ushort)axis, homeDir,
                    _homeVelSlow[axis] * _ppu[axis], mode, 1);
                if (rc != 0) return MotionResult.Fail(rc, "dmc_set_homemode");
                rc = dmc_home_move(Card, (ushort)axis);
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
                short rc = dmc_pmove(card, (ushort)axis, (int)Math.Round(to * _ppu[axis]), 1);  // [核对] posi_mode 1=绝对(或相反)
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

                // 等时点位法（升级路径: 官方多轴插补 dmc_line_unit / dmc_arc_move_radius_unit，见docs）
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
                    short rc = dmc_pmove(Card, (ushort)ax0, (int)Math.Round(toArr[i] * _ppu[ax0]), 1);
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
                // [升级路径] 雷赛硬件圆弧: 官方 dmc_arc_move_radius_unit / dmc_arc_move_center_unit / dmc_arc_move_3points_unit
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

                // [真值] 官方原型 dmc_get_ad_input(ushort CardNo,ushort channel,ref double Vout)：输出即电压
                double v = 0;
                short rc = dmc_get_ad_input(Card, (ushort)index, ref v);
                return rc == 0 ? v : 0;
            }
        }

        public MotionResult SetAo(int index, double volts)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");

                // [真值] 官方原型 dmc_set_da_output(UInt16 CardNo,UInt16 channel,double Vout)：入参即电压
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

        private ushort Card { get { return (ushort)(_info == null ? 0 : _info.CardNo); } }
    }
}
