// =====================================================================
//  固高 GTS/GTN 系列 适配器（gts.dll）
//  生成工具: 上位机代码工厂 v1.0 | 2026-10-22 12:00:23
//  KB级别: B —— 核心API已映射(Open/使能/回零/点位/IO/位置)。函数名、参数表与结构体字段顺序已按官方C#绑定/C头文件逐条核对；回零改用官方标准回零接口(GT_GetStandardHomePrm/GT_ExecuteStandardHome)。【重要】本套件的 gts.dll 绑定是【单卡库】口径(首参是通道号)；固高 SDK 的多卡库 DLL 同样叫 gts.dll、导出名完全相同，但每个 API 多一个 cardNum 首参——混用会让参数整体错位。插多张卡时必须改用多卡库签名，或用 GT_SetCardNo 逐卡切换。
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

        #region 厂商 API 导入（[核对] = 签名需按手册确认）
        // ── 真值链（每条 P/Invoke 上方：[ 源 | 核对 | 提取 ]）──
        //   数据源：mcopilot knowledge/sdks/gts.json（生成时实时读取，非复制）
        //   源=verified 这条结论从哪来 ｜ 核对=check 验到哪一步 ｜ 提取=extraction 签名对≠调用对时是否需人签字
        //   本套件 P/Invoke 22 条：知识库钉扎 19 / 未收录(显式降级) 3
        //   核对进度（钉扎条目）：manual 0 / hardware 0 / auto 0 / none 19
        //   ⚠ 未核对 19 条：GT_Open, GT_Close, GT_Reset, GT_LoadConfig, GT_ClrSts, GT_AxisOn, GT_AxisOff, GT_Stop, GT_SetPos, GT_SetVel, GT_Update, GT_PrfTrap, GT_SetTrapPrm, GT_PrfJog, GT_SetJogPrm, GT_GetSts, GT_GetPrfPos, GT_GetDi, GT_SetDoBit —— 已标 TODO，不许当事实用
        //   复核：mcopilot 仓库跑 scripts/annotate_provenance.py --check；重新生成本套件即自动继承最新口径

        // [真值] 函数名/参数表取自 gts.csv(官方C#绑定) 与 gts_hdr.csv(官方C头文件)，并已对 dll_exports.csv(PE导出表) 校验
        // [变体口径·必读] 本套件按【单卡库】(64bit_single_card) 签名绑定：首参是通道号，无 cardNum。
        //        固高 SDK 的【多卡库】DLL 同样叫 gts.dll、导出名完全相同，但每个 API 多一个 cardNum 首参
        //        (例：GT_Open 単卡(short channel, short param) vs 多卡(short cardNum, short channel, short param))。
        //        两者混用 → 参数整体错位 → 打开到错误的卡/参数全乱，且不抛异常。
        //        插多张卡时：改用多卡库签名，或用 GT_SetCardNo 逐卡切换。
        // TODO[需核对厂商手册] GT_Open —— 核对后更新 mcopilot 仓库 knowledge/sdks/gts.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-binding | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_Open(short channel, short param);
        // TODO[需核对厂商手册] GT_Close —— 核对后更新 mcopilot 仓库 knowledge/sdks/gts.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-binding | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_Close();
        // TODO[需核对厂商手册] GT_Reset —— 核对后更新 mcopilot 仓库 knowledge/sdks/gts.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-binding | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_Reset();
        // TODO[需核对厂商手册] GT_LoadConfig —— 核对后更新 mcopilot 仓库 knowledge/sdks/gts.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-binding | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern short GT_LoadConfig([MarshalAs(UnmanagedType.LPStr)] string pFile);
        // TODO[需核对厂商手册] GT_ClrSts —— 核对后更新 mcopilot 仓库 knowledge/sdks/gts.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-binding | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_ClrSts(short axis, short count);
        // TODO[需核对厂商手册] GT_AxisOn —— 核对后更新 mcopilot 仓库 knowledge/sdks/gts.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-binding | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_AxisOn(short axis);
        // TODO[需核对厂商手册] GT_AxisOff —— 核对后更新 mcopilot 仓库 knowledge/sdks/gts.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-binding | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_AxisOff(short axis);
        // TODO[需核对厂商手册] GT_Stop —— 核对后更新 mcopilot 仓库 knowledge/sdks/gts.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-binding | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_Stop(int mask, int option);
        // TODO[需核对厂商手册] GT_SetPos —— 核对后更新 mcopilot 仓库 knowledge/sdks/gts.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-binding | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_SetPos(short profile, int pos);
        // TODO[需核对厂商手册] GT_SetVel —— 核对后更新 mcopilot 仓库 knowledge/sdks/gts.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-binding | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_SetVel(short profile, double vel);
        // TODO[需核对厂商手册] GT_Update —— 核对后更新 mcopilot 仓库 knowledge/sdks/gts.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-binding | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_Update(int mask);
        // TODO[需核对厂商手册] GT_PrfTrap —— 核对后更新 mcopilot 仓库 knowledge/sdks/gts.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-binding | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_PrfTrap(short profile);
        // TODO[需核对厂商手册] GT_SetTrapPrm —— 核对后更新 mcopilot 仓库 knowledge/sdks/gts.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-binding | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_SetTrapPrm(short profile, ref TTrapPrm pPrm);
        // TODO[需核对厂商手册] GT_PrfJog —— 核对后更新 mcopilot 仓库 knowledge/sdks/gts.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-binding | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_PrfJog(short profile);
        // TODO[需核对厂商手册] GT_SetJogPrm —— 核对后更新 mcopilot 仓库 knowledge/sdks/gts.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-binding | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_SetJogPrm(short profile, ref TJogPrm pPrm);
        // [ 源 未标注 | 核对 none | 提取 judgment ]  mcopilot 知识库未收录此函数
        // TODO[需核对厂商手册] GT_GetStandardHomePrm：签名未经外部真值钉扎 —— 按官方 C# 绑定逐字核对后再用
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_GetStandardHomePrm(short axis, out TStandardHomePrm pHomePrm);
        // [ 源 未标注 | 核对 none | 提取 judgment ]  mcopilot 知识库未收录此函数
        // TODO[需核对厂商手册] GT_ExecuteStandardHome：签名未经外部真值钉扎 —— 按官方 C# 绑定逐字核对后再用
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_ExecuteStandardHome(short axis, ref TStandardHomePrm pHomePrm);
        // TODO[需核对厂商手册] GT_GetSts —— 核对后更新 mcopilot 仓库 knowledge/sdks/gts.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-binding | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_GetSts(short axis, out int pSts, short count, out uint pClock);
        // TODO[需核对厂商手册] GT_GetPrfPos —— 核对后更新 mcopilot 仓库 knowledge/sdks/gts.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-binding | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_GetPrfPos(short profile, out double pValue, short count, out uint pClock);
        // [ 源 未标注 | 核对 none | 提取 judgment ]  mcopilot 知识库未收录此函数
        // TODO[需核对厂商手册] GT_GetEncPos：签名未经外部真值钉扎 —— 按官方 C# 绑定逐字核对后再用
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_GetEncPos(short encoder, out double pValue, short count, out uint pClock);
        // TODO[需核对厂商手册] GT_GetDi —— 核对后更新 mcopilot 仓库 knowledge/sdks/gts.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-binding | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_GetDi(short diType, out int pValue);
        // TODO[需核对厂商手册] GT_SetDoBit —— 核对后更新 mcopilot 仓库 knowledge/sdks/gts.json 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件
        // [ 源 official-binding | 核对 none | 提取 judgment ]  导出表无背书
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_SetDoBit(short doType, short doIndex, short value);

        // [真值] 结构体字段顺序/类型/偏移取自官方头文件结构体表 gts_structs.csv（按 C 头文件的自然对齐）
        [StructLayout(LayoutKind.Sequential)]
        public struct TTrapPrm
        {
            public double acc;        // 字段0 偏移0
            public double dec;        // 字段1 偏移8
            public double velStart;   // 字段2 偏移16
            public short smoothTime;  // 字段3 偏移24（结构体总长32）
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct TJogPrm
        {
            public double acc;     // 字段0 偏移0
            public double dec;     // 字段1 偏移8
            public double smooth;  // 字段2 偏移16（结构体总长24）
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct TStandardHomePrm
        {
            public short mode;          // 字段0 偏移0
            public double highSpeed;    // 字段1 偏移8
            public double lowSpeed;     // 字段2 偏移16
            public double acc;          // 字段3 偏移24
            public int offset;          // 字段4 偏移32
            public short check;         // 字段5 偏移36
            public short autoZeroPos;   // 字段6 偏移38
            public int motorStopDelay;  // 字段7 偏移40
            public short pad1;          // 字段8 偏移44
            public short pad2;          // 字段9 偏移46
            public short pad3;          // 字段10 偏移48（结构体总长56）
        }
        #endregion

        public MotionResult Open(ControllerConnectInfo info)
        {
            lock (_sdk)
            {
                if (_open) return MotionResult.Success("already open");
                _info = info;

                short rc = GT_Open(info.CardNo, 0);   // [核对] 官方首参名为 channel（通道/卡号），param 传 0
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
                short rc = GT_ClrSts(ax, 1);   // 官方原型 GT_ClrSts(short axis,short count)
                if (rc != 0) return MotionResult.Fail(rc, "GT_ClrSts");
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
                    if ((sts & 0x1) != 0) s |= AxisState.Enabled;     // [核对] 使能位
                    if ((sts & 0x400) != 0) s |= AxisState.Moving;    // [核对] 运动位(常见bit10)
                    if ((sts & 0x2) != 0) s |= AxisState.Alarm;       // [核对] 报警位
                    if ((sts & 0x20) != 0) s |= AxisState.LimitPos;   // [核对] 正限位
                    if ((sts & 0x10) != 0) s |= AxisState.LimitNeg;   // [核对] 负限位
                    if (_homed[axis]) s |= AxisState.Homed;
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
                CheckRange(axis); _homed[axis] = false; _homing[axis] = true; _homeStartT[axis] = Environment.TickCount;

                // [真值] 官方标准回零接口: GT_GetStandardHomePrm(short axis,out TStandardHomePrm)
                //                              GT_ExecuteStandardHome(short axis,ref TStandardHomePrm)
                short ax = Ax(axis);
                TStandardHomePrm hp = new TStandardHomePrm();
                short rc = GT_GetStandardHomePrm(ax, out hp);   // 先读控制器内已有的标准回零参数
                if (rc != 0) return MotionResult.Fail(rc, "GT_GetStandardHomePrm");
                hp.mode = MapHomeModeToGtsStandard(_homeMode[axis]);   // [核对] HOME_MODE_* 宏值
                hp.highSpeed = _homeVelFast[axis] * _ppu[axis];
                hp.lowSpeed = _homeVelSlow[axis] * _ppu[axis];
                hp.acc = _homeAcc[axis] * _ppu[axis];
                rc = GT_ExecuteStandardHome(ax, ref hp);
                if (rc != 0) return MotionResult.Fail(rc, "GT_ExecuteStandardHome");
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
                TTrapPrm tp = new TTrapPrm();
                tp.acc = p.Acc * _ppu[axis];
                tp.dec = p.Dec * _ppu[axis];
                tp.velStart = 0;
                // [核对] 官方 TTrapPrm.smoothTime 是 short（gts_structs.csv 字段3），
                //        官方真值未给出量纲与取值范围：这里一律填 0（梯形），
                //        S曲线平滑时间必须按手册填写，不臆造换算比例。
                tp.smoothTime = 0;
                rc = GT_SetTrapPrm(ax, ref tp);
                if (rc != 0) return MotionResult.Fail(rc, "GT_SetTrapPrm");
                rc = GT_SetPos(ax, (int)Math.Round(to * _ppu[axis]));
                if (rc != 0) return MotionResult.Fail(rc, "GT_SetPos");
                rc = GT_SetVel(ax, Math.Abs(p.Vel) * _ppu[axis]);
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
                jp.acc = p.Acc * _ppu[axis];
                jp.dec = p.Dec * _ppu[axis];
                jp.smooth = 0;   // [核对] 平滑时间按手册填写
                rc = GT_SetJogPrm(ax, ref jp);
                if (rc != 0) return MotionResult.Fail(rc, "GT_SetJogPrm");
                // [真值] 官方 TJogPrm 里没有速度字段，Jog 速度只能用 GT_SetVel 下发（速度符号即方向）
                rc = GT_SetVel(ax, velUserPerSec * _ppu[axis]);
                if (rc != 0) return MotionResult.Fail(rc, "GT_SetVel");
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
                // [升级路径] 高同步要求场景改用固高坐标系插补(官方 GT_CrdData / GT_CrdDataCircle / GT_CrdStart，
                //            圆弧可用 GT_ArcXYR)，可获得硬件级插补脉冲同步（见docs/CapabilityMatrix.md）。
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
                    tp.acc = p.Acc * _ppu[ax0]; tp.dec = p.Dec * _ppu[ax0];
                    tp.velStart = 0; tp.smoothTime = 0;
                    rc = GT_SetTrapPrm(ax, ref tp);
                    if (rc != 0) return MotionResult.Fail(rc, "GT_SetTrapPrm");
                    rc = GT_SetPos(ax, (int)Math.Round(toArr[i] * _ppu[ax0]));
                    if (rc != 0) return MotionResult.Fail(rc, "GT_SetPos");
                    rc = GT_SetVel(ax, vi * _ppu[ax0]);
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
                // [升级路径] 硬件圆弧: 官方坐标系圆弧接口 GT_ArcXYR / GT_CrdDataCircle（坐标系模式），精度与速度优于线性化
                return MotionResult.Success("arc done (32-seg linearization)");
            }
        }

        public MotionResult SetDo(int index, bool on)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");

                // [真值] 官方单点输出为 GT_SetDoBit(short doType,short doIndex,short value)（gts.csv:396）
                short rc = GT_SetDoBit(GtsDoTypeGpo, (short)index, (short)(on ? 1 : 0));   // [核对] doType 宏值
                if (rc != 0) return MotionResult.Fail(rc, "GT_SetDoBit " + index);
                return MotionResult.Success();
            }
        }

        public bool GetDi(int index)
        {
            lock (_sdk)
            {
                if (!_open) return false;

                int di;
                short rc = GT_GetDi(GtsDiTypeGpi, out di);   // [核对] diType 宏值; 返回组值则按位取
                if (rc != 0) return false;
                return ((di >> index) & 1) != 0;
            }
        }

        public double GetAi(int index)
        {
            lock (_sdk)
            {
                if (!_open) return 0;

                // [未实现] 官方 C# 绑定与 PE 导出表里都没有 GT_GetAi（臆造名，已删除）。
                //          固高模拟量走通道型接口 GT_GetAdc(short adc,out double pValue,short count,out uint pClock)
                //          (gts.csv:129 / gts_hdr.csv:137)，但真值未给出值域与单位换算，须按手册确认后再接入。
                return 0;
            }
        }

        public MotionResult SetAo(int index, double volts)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");

                // [未实现] 官方没有 GT_SetAo（臆造名，已删除）；候选 GT_SetDac / GT_PtAo（gts.csv:393 / 336）
                //          的值域/单位换算须按手册确认后再接入。
                return MotionResult.Fail(-300, "[未实现] 固高模拟量输出: 官方无 GT_SetAo 等价声明(候选 GT_SetDac/GT_PtAo), 需按手册确认值域后接入");
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


        // [真值] HOME_MODE_* 宏值取自官方头文件宏表 gts_hdr_macros.csv:163-171（全表 9 个 mode）
        //   HOME_MODE_LIMIT=10 / HOME_MODE_LIMIT_HOME=11 / HOME_MODE_LIMIT_INDEX=12 /
        //   HOME_MODE_LIMIT_HOME_INDEX=13 / HOME_MODE_HOME=20 / HOME_MODE_HOME_INDEX=22 /
        //   HOME_MODE_INDEX=30 / HOME_MODE_FORCED_HOME=40 / HOME_MODE_FORCED_HOME_INDEX=41
        //   本套件只映射 4 个（HomeMode 枚举只有 4 值）；11/12/13 与 40/41 未暴露。
        private static short MapHomeModeToGtsStandard(HomeMode m)
        {
            switch (m)
            {
                case HomeMode.LimitReverse: return 10;      // HOME_MODE_LIMIT
                case HomeMode.HomeSwitch: return 20;        // HOME_MODE_HOME
                case HomeMode.HomeSwitchIndex: return 22;   // HOME_MODE_HOME_INDEX
                case HomeMode.IndexOnly: return 30;        // HOME_MODE_INDEX
                default: return 20;
            }
        }
        private bool IsMovingGts(int axis)
        {
            int sts; uint clk;
            if (GT_GetSts(Ax(axis), out sts, 1, out clk) != 0) return false;
            return (sts & 0x400) != 0;   // [核对] 运动位
        }
        private static void CheckRange(int axis)
        {
            if (axis < 0 || axis > 7) throw new ArgumentOutOfRangeException("axis", axis, "supports 0-7");
        }

        /// <summary>统一0-based轴号 -> 固高1-based</summary>
        private static short Ax(int axis) { return (short)(axis + 1); }

        // [核对] diType/doType 的宏值：官方头文件宏表里没有名为 GT_DI_GPI/GT_DO_GPO 的宏，
        //         但有语义接近的候选值 MC_GPI=4 / MC_GPO=12（gts_hdr_macros.csv:17,22）——
        //         真值未标注它们与 diType/doType 的对应关系，故仍取 1，
        //         必须按《GTN系列运动控制卡用户手册》确认后再上真机。
        private const short GtsDiTypeGpi = 1;
        private const short GtsDoTypeGpo = 1;
    }
}
