// =====================================================================
//  高川 适配器（GaoChuan NMC / nmc_lib20.dll）
//  KB级别: B —— 核心 API 全部按官方头文件实现
//  真值来源: evidence/gaochuan/gen_129_unp/dll/mtn_lib20.h（官方 C 头文件，本机存证）
//            签名一致性由 evidence/verify_gaochuan_adapter.py 逐条比对门禁核对
//  单位口径: 官方 API 位置用「脉冲」、速度用「脉冲/ms」；适配层统一换算成用户单位(mm / mm/s)
//  上真机流程: 见 docs/ApiChecklist.md
// =====================================================================
using System;
using System.Runtime.InteropServices;
using System.Threading;
using MotionControl.Core;

namespace MotionControl.Adapters.GaoChuan
{
    /// <summary>
    /// 高川 NMC（nmc_lib20.dll）—— IMotionController 适配器。
    /// 所有 P/Invoke 签名与结构体字段均来自官方 mtn_lib20.h，非凭记忆。
    /// </summary>
    public class GaoChuanController : IMotionController
    {
        private const int AXES = 8;

        private readonly object _sdk = new object();
        private ControllerConnectInfo _info;
        private IntPtr _dev = IntPtr.Zero;
        private IntPtr[] _axis = new IntPtr[AXES];
        private IntPtr _crd = IntPtr.Zero;
        private bool _open;
        private bool _crdReady;

        private readonly bool[] _homed = new bool[AXES];
        private readonly bool[] _homing = new bool[AXES];
        private readonly double[] _ppu = new double[AXES];        // 脉冲 / 用户单位
        private readonly HomeMode[] _homeMode = new HomeMode[AXES];
        private readonly double[] _homeVelFast = new double[AXES];
        private readonly double[] _homeVelSlow = new double[AXES];
        private readonly double[] _homeAcc = new double[AXES];
        private readonly int[] _homeOffset = new int[AXES];        // 原点偏移（脉冲）
        private readonly double[] _softNeg = new double[AXES];
        private readonly double[] _softPos = new double[AXES];

        public GaoChuanController()
        {
            for (int i = 0; i < AXES; i++)
            {
                _ppu[i] = 10000; _softNeg[i] = -100000; _softPos[i] = 100000;
                _homeVelFast[i] = 20; _homeVelSlow[i] = 2; _homeAcc[i] = 200;
            }
        }

        public Brand Brand { get { return Brand.GaoChuan; } }
        public string DeviceName { get { return "GaoChuan-nmc_lib20"; } }
        public bool IsOpen { get { return _open; } }

        public bool SupportsJerkProfile { get { return true; } }

        #region 官方 API（nmc_lib20.dll —— 签名逐字取自 mtn_lib20.h）

        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_DevSearch(int mode, ref ushort pDevNo, IntPtr pInfoList);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_DevOpen(short devNo, ref IntPtr pDevHandle);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_DevClose(ref IntPtr pDevHandle);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_MtOpen(IntPtr devHandle, short itemNo, ref IntPtr pAxisHandle);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_MtClose(ref IntPtr pAxisHandle);

        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_MtSetSvOn(IntPtr axisHandle);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_MtSetSvOff(IntPtr axisHandle);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_MtStop(IntPtr axisHandle);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_MtAbruptStop(IntPtr axisHandle);

        // 回零：参数经 THomeSetting 结构体下发（mtn_lib20.h L1657）
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_MtSetHomePara(IntPtr axisHandle, ref THomeSetting pPara);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_MtHome(IntPtr axisHandle);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_MtGetHomeSts(IntPtr axisHandle, out short pStsWord);

        // 点位 / 点动（maxVel 单位 = 脉冲/ms，acc/dec = 脉冲/ms^2，tgtPos = 脉冲）
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_MtMovePtpAbs(IntPtr axisHandle, double acc, double dec, double startVel,
                                                    double endVel, double maxVel, short smoothCoef, int tgtPos);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_MtMoveJog(IntPtr axisHandle, double acc, double dec, double maxVel,
                                                 short smoothCoef, short clrStsFlag);

        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_MtGetPrfPos(IntPtr axisHandle, ref int pPos);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_MtGetEncPos(IntPtr axisHandle, ref int pPos);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_MtGetSts(IntPtr axisHandle, out short pStsWord);

        // 通用 IO：DO/DI 按位；AI/AO 走 double 电压版（整型版 NMC_SetDac/GetAdc 本适配器不用）
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_SetDOBit(IntPtr devHandle, short bitIndex, short value);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_GetDIBit(IntPtr devHandle, short bitIndex, out short pBitValue);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_IOAdcGet(IntPtr devHandle, byte slv, byte chn, out double pVol);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_IODacSet(IntPtr devHandle, byte slv, byte chn, double vol);

        // 坐标系（直线 / 圆弧插补走缓冲区下发）
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_CrdOpenEx(IntPtr devHandle, short itemNo, ref IntPtr pCrdHandle);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_CrdClose(ref IntPtr pCrdHandle);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_CrdConfig(IntPtr crdHandle, ref TCrdConfig pConfig);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_CrdBufClr(IntPtr crdHandle);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_CrdBufGetUsed(IntPtr crdHandle, ref int pLen);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_CrdStartMtn(IntPtr crdHandle);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_CrdEstopMtn(IntPtr crdHandle);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_CrdLineXYZEx(IntPtr crdHandle, int segNo, short crdAxMask, int[] pTgPosArray,
                                                    double endVel, double vel, double synAcc, short lookaheadDis);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_CrdArcCenterEx(IntPtr crdHandle, int segNo, int[] pTgPosArray, int[] pCenterPosArray,
                                                      short circleDir, double endVel, double vel, double synAcc, short lookaheadDis);
        [DllImport("nmc_lib20.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short NMC_CrdGetSts(IntPtr crdHandle, out short pStsWord);

        #endregion

        #region 官方结构体与位定义（逐字取自 mtn_lib20.h）

        /// <summary>
        /// THomeSetting（mtn_lib20.h L1613）：
        /// mode,dir,offset(必须) → scan1stVel,scan2ndVel,acc → reScanEn,homeEdge,lmtEdge,zEdge →
        /// iniRetPos,retSwOffset,safeLen → usePreSetPtpPara,reserved[3],reserved2
        /// reserved2 在 HM_MODE7(DI回零) 时复用为 DI 序号。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct THomeSetting
        {
            public short mode, dir;
            public int offset;
            public double scan1stVel, scan2ndVel, acc;
            public byte reScanEn, homeEdge, lmtEdge, zEdge;
            public uint iniRetPos, retSwOffset, safeLen;
            public byte usePreSetPtpPara;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public byte[] reserved;
            public int reserved2;
        }

        /// <summary>TCrdConfig（mtn_lib20.h L819）：axCnts,reserved[3],pAxArray[4],port[4]。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct TCrdConfig
        {
            public short axCnts;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public short[] reserved;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public short[] pAxArray;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public short[] port;
        }

        // 轴状态位（mtn_lib20.h L631-644）
        private const short AX_STS_BUSY    = 0x0001; // bit0  运动
        private const short AX_STS_POSREC  = 0x0002; // bit1  位置到达
        private const short AX_STS_SVON    = 0x0008; // bit3  伺服允许
        private const short AX_STS_LMTP    = 0x0040; // bit6  正限位
        private const short AX_STS_LMTN    = 0x0080; // bit7  负限位
        private const short AX_STS_SOFTPOS = 0x0100; // bit8  正软限位
        private const short AX_STS_SOFTNEG = 0x0200; // bit9  负软限位
        private const short AX_STS_ALM     = 0x0400; // bit10 伺服报警
        private const short AX_STS_ESTP    = 0x1000; // bit12 急停

        // 坐标系状态位（mtn_lib20.h L905-911）
        private const short CRD_STS_BUSY    = 0x0001;
        private const short CRD_STS_MVERR   = 0x0002;
        private const short CRD_STS_EMPTY   = 0x0004;
        private const short CRD_STS_NODATA  = 0x0010;

        // 回零状态位（mtn_lib20.h L1660-1664）
        private const short HM_STS_BUSY = 0x0001, HM_STS_OK = 0x0002, HM_STS_FAIL = 0x0004;

        /// <summary>统一 HomeMode → 高川 THomeMode（mtn_lib20.h L1610）。
        /// HM_MODE1=0 单原点 / MODE2=1 单限位 / MODE3=2 单Z相 / MODE4=3 原点+Z /
        /// HM_MODE5=4 原点-Z / MODE6=5 限位+Z / HM_MODE7=6 DI回零 / HM_MODE8=7 DI+Index</summary>
        private static short MapHomeMode(HomeMode m)
        {
            switch (m)
            {
                case HomeMode.LimitReverse:     return 5; // HM_MODE6 限位+Z
                case HomeMode.HomeSwitch:       return 0; // HM_MODE1 单原点
                case HomeMode.HomeSwitchIndex:  return 3; // HM_MODE4 原点+Z
                case HomeMode.IndexOnly:        return 2; // HM_MODE3 单Z相
                default:                         return 0;
            }
        }

        #endregion

        #region 连接生命周期

        public MotionResult Open(ControllerConnectInfo info)
        {
            lock (_sdk)
            {
                if (_open) return MotionResult.Success("already open");
                if (info == null) return MotionResult.Fail(-6, "null connect info");
                _info = info;

                ushort devCount = 0;
                short rc = NMC_DevSearch(0, ref devCount, IntPtr.Zero);
                if (rc != 0) return MotionResult.Fail(rc, "NMC_DevSearch rc=" + rc + "（无卡或驱动未装）");

                rc = NMC_DevOpen((short)_info.CardNo, ref _dev);
                if (rc != 0) return MotionResult.Fail(rc, "NMC_DevOpen devNo=" + _info.CardNo + " rc=" + rc);

                for (int i = 0; i < AXES; i++)
                {
                    rc = NMC_MtOpen(_dev, (short)i, ref _axis[i]);
                    if (rc != 0)
                    {
                        for (int k = 0; k < i; k++) if (_axis[k] != IntPtr.Zero) NMC_MtClose(ref _axis[k]);
                        NMC_DevClose(ref _dev);
                        for (int k = 0; k < AXES; k++) _axis[k] = IntPtr.Zero;
                        return MotionResult.Fail(rc, "NMC_MtOpen axis " + i + " rc=" + rc + "（卡上轴数不足 8）");
                    }
                }
                _open = true;
                return MotionResult.Success("GaoChuan nmc_lib20 opened, devNo=" + _info.CardNo
                                          + "（枚举到 " + devCount + " 块卡）");
            }
        }

        public MotionResult Close()
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Success();
                if (_crd != IntPtr.Zero) NMC_CrdClose(ref _crd);
                for (int i = 0; i < AXES; i++) if (_axis[i] != IntPtr.Zero) NMC_MtClose(ref _axis[i]);
                if (_dev != IntPtr.Zero) NMC_DevClose(ref _dev);
                _crd = IntPtr.Zero; _crdReady = false; _open = false;
                for (int i = 0; i < AXES; i++) { _axis[i] = IntPtr.Zero; _homed[i] = false; _homing[i] = false; }
                return MotionResult.Success();
            }
        }

        public void Dispose() { try { Close(); } catch { } }

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
                _homeVelFast[i] = cfg.Home.VelFast;
                _homeVelSlow[i] = cfg.Home.VelSlow;
                _homeAcc[i] = cfg.Home.Acc;
                // 回零偏置是**位置**量（用户单位 → 脉冲），走 Units 保证 int32 越界抛异常而非静默截断。
                _homeOffset[i] = Units.MmToPulseInt32(cfg.Home.Offset, _ppu[i], i, "GaoChuan");
                _softNeg[i] = cfg.SoftLimitNeg; _softPos[i] = cfg.SoftLimitPos;
                _homed[i] = false;
                return MotionResult.Success(cfg + " configured (ppu=" + _ppu[i] + ", homeMode=" + cfg.Home.Mode + ")");
            }
        }

        #region 轴状态 / 位置

        public MotionResult Enable(int axis, bool on)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis);

                // 官方注释（L398）：SetSvOn/Off 仅在轴静止时执行
                short sts = 0;
                if (NMC_MtGetSts(_axis[axis], out sts) == 0 && (sts & AX_STS_BUSY) != 0)
                    return MotionResult.Fail(-8, "axis " + axis + " 运动中不允许切换伺服");

                short rc = on ? NMC_MtSetSvOn(_axis[axis]) : NMC_MtSetSvOff(_axis[axis]);
                if (rc != 0) return MotionResult.Fail(rc, (on ? "NMC_MtSetSvOn" : "NMC_MtSetSvOff") + " axis " + axis + " rc=" + rc);
                return MotionResult.Success("servo " + (on ? "ON" : "OFF") + " axis " + axis);
            }
        }

        public AxisState GetState(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return AxisState.None;
                CheckRange(axis);

                short sts = 0;
                if (NMC_MtGetSts(_axis[axis], out sts) != 0) return AxisState.None;

                AxisState s = AxisState.None;
                if ((sts & AX_STS_BUSY) != 0)    s |= AxisState.Moving;
                if ((sts & AX_STS_POSREC) != 0)  s |= AxisState.InPosition;
                if ((sts & AX_STS_SVON) != 0)    s |= AxisState.Enabled;
                if ((sts & AX_STS_ALM) != 0)     s |= AxisState.Alarm;
                if ((sts & AX_STS_ESTP) != 0)    s |= AxisState.Emergency;
                if ((sts & AX_STS_LMTP) != 0)    s |= AxisState.LimitPos;
                if ((sts & AX_STS_LMTN) != 0)    s |= AxisState.LimitNeg;
                if ((sts & AX_STS_SOFTPOS) != 0) s |= AxisState.LimitPos;
                if ((sts & AX_STS_SOFTNEG) != 0) s |= AxisState.LimitNeg;
                if (_homing[axis]) s |= AxisState.Homing;
                return s;
            }
        }

        public double GetCommandPos(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return 0;
                CheckRange(axis);
                int p = 0;
                return NMC_MtGetPrfPos(_axis[axis], ref p) == 0 ? p / _ppu[axis] : 0;
            }
        }

        public double GetFeedbackPos(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return 0;
                CheckRange(axis);
                int p = 0;
                return NMC_MtGetEncPos(_axis[axis], ref p) == 0 ? p / _ppu[axis] : 0;
            }
        }

        public double GetFollowingError(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return 0;
                CheckRange(axis);
                int c = 0, f = 0;
                if (NMC_MtGetPrfPos(_axis[axis], ref c) != 0) return 0;
                if (NMC_MtGetEncPos(_axis[axis], ref f) != 0) return 0;
                return Math.Abs((c - f) / _ppu[axis]);   // 用户单位
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

                THomeSetting hs = new THomeSetting();
                BrandUnits bu = Units.GaoChuan(_ppu[axis]);
                hs.mode = MapHomeMode(_homeMode[axis]);
                hs.dir = 0;                                                     // 0=负向搜寻
                hs.offset = _homeOffset[axis];
                // THomeSetting 的 scan1stVel/scan2ndVel/acc 未单独标注单位，
                // 但同库 NMC_SetRefSpeed(L366) 与 NMC_GetVel(L529) 已定案速度为脉冲/ms，
                // 故 acc 按脉冲/ms² 处理（见 Units.cs 顶部真值锚点）。
                hs.scan1stVel = Units.MmS2Vendor(_homeVelFast[axis], bu);
                hs.scan2ndVel = Units.MmS2Vendor(_homeVelSlow[axis], bu);
                hs.acc = Units.MmS2_2ToVendor(_homeAcc[axis], bu);
                hs.reScanEn = 0; hs.homeEdge = 0; hs.lmtEdge = 0; hs.zEdge = 0;
                hs.iniRetPos = 0; hs.retSwOffset = 0; hs.safeLen = 0;
                hs.usePreSetPtpPara = 0;    // 0 → 回零运动参数默认取 acc
                hs.reserved = new byte[3];
                hs.reserved2 = 0;

                short rc = NMC_MtSetHomePara(_axis[axis], ref hs);
                if (rc != 0) return MotionResult.Fail(rc, "NMC_MtSetHomePara axis " + axis + " rc=" + rc);

                rc = NMC_MtHome(_axis[axis]);
                if (rc != 0) { _homing[axis] = false; return MotionResult.Fail(rc, "NMC_MtHome axis " + axis + " rc=" + rc); }

                _homing[axis] = true; _homed[axis] = false;
                return MotionResult.Success("homing started axis " + axis
                    + "（mode=" + hs.mode + " scan1stVel=" + Round3(hs.scan1stVel) + " 脉冲/ms dir=负向 offset="
                    + hs.offset + " 脉冲）");
            }
        }

        public bool IsHomed(int axis)
        {
            lock (_sdk)
            {
                if (!_open) return false;
                CheckRange(axis);
                PollHomeSts(axis);
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
                    if (PollHomeSts(axis)) return MotionResult.Success("homed axis " + axis);
                }
                if ((DateTime.Now - t0).TotalMilliseconds >= timeoutMs)
                    return MotionResult.Fail(-3, "home timeout axis " + axis + "（未收到 BIT_AXHOME_OK）");
                Thread.Sleep(20);
            }
        }

        /// <summary>回读回零状态字并更新 _homing/_homed；返回 true 表示本次回零已成功。</summary>
        private bool PollHomeSts(int axis)
        {
            if (!_homing[axis]) return _homed[axis];
            short h = 0;
            if (NMC_MtGetHomeSts(_axis[axis], out h) != 0) return false;
            if ((h & HM_STS_OK) != 0) { _homing[axis] = false; _homed[axis] = true; return true; }
            if ((h & HM_STS_FAIL) != 0) { _homing[axis] = false; _homed[axis] = false; }
            return false;
        }

        #endregion

        #region 单轴运动

        public MotionResult MoveTo(int axis, double posUser, MoveParams p, MoveMode mode)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis);
                if (mode == MoveMode.Relative) posUser += GetCommandPos(axis);

                BrandUnits bu = Units.GaoChuan(_ppu[axis]);
                double ppms = Units.MmS2Vendor(p.Vel, bu);        // mm/s → 脉冲/ms
                double ppms2 = Units.MmS2_2ToVendor(p.Acc, bu);    // mm/s² → 脉冲/ms²
                int tgt = Units.MmToPulseInt32(posUser, _ppu[axis], axis, "GaoChuan");

                // 签名逐字对齐 mtn_lib20.h L415：
                //   NMC_MtMovePtpAbs(axis, acc, dec, startVel, endVel, maxVel, smoothCoef, tgtPos)
                short rc = NMC_MtMovePtpAbs(_axis[axis], ppms2, ppms2, 0, 0, ppms,
                                            Smooth(p.JerkRatio), tgt);
                if (rc != 0) return MotionResult.Fail(rc, "NMC_MtMovePtpAbs axis " + axis + " rc=" + rc);
                return MotionResult.Success("ptp axis " + axis + " → " + tgt + " 脉冲 @ " + Round3(ppms) + " 脉冲/ms");
            }
        }

        public MotionResult MoveJog(int axis, double velUserPerSec, MoveParams p)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis);

                // 官方注释（L421）：Jog 模式下 maxVel 正负号标识运动方向
                BrandUnits bu = Units.GaoChuan(_ppu[axis]);
                double ppms = Units.MmS2Vendor(velUserPerSec, bu);
                double ppms2 = Units.MmS2_2ToVendor(p.Acc, bu);
                short rc = NMC_MtMoveJog(_axis[axis], ppms2, ppms2, ppms, Smooth(p.JerkRatio), 1);
                if (rc != 0) return MotionResult.Fail(rc, "NMC_MtMoveJog axis " + axis + " rc=" + rc);
                return MotionResult.Success("jog axis " + axis + " @ " + Round3(ppms) + " 脉冲/ms");
            }
        }

        public MotionResult Stop(int axis, bool emergency)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axis);
                short rc = emergency ? NMC_MtAbruptStop(_axis[axis]) : NMC_MtStop(_axis[axis]);
                if (rc != 0) return MotionResult.Fail(rc, (emergency ? "NMC_MtAbruptStop" : "NMC_MtStop") + " axis " + axis + " rc=" + rc);
                return MotionResult.Success((emergency ? "abrupt stop" : "decel stop") + " axis " + axis);
            }
        }

        public MotionResult WaitMoveDone(int axis, int timeoutMs)
        {
            DateTime t0 = DateTime.Now;
            while (true)
            {
                lock (_sdk)
                {
                    if (!_open) return MotionResult.Fail(-1, "not open");
                    CheckRange(axis);
                    short sts = 0;
                    if (NMC_MtGetSts(_axis[axis], out sts) == 0 && (sts & AX_STS_BUSY) == 0)
                        return MotionResult.Success("axis " + axis + " 停止");
                }
                if ((DateTime.Now - t0).TotalMilliseconds >= timeoutMs)
                    return MotionResult.Fail(-3, "move timeout axis " + axis);
                Thread.Sleep(10);
            }
        }

        #endregion

        #region 插补（坐标系缓冲区）

        public MotionResult MoveLine(int[] axisList, double[] targetUser, MoveParams p, MoveMode mode)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                if (axisList == null || targetUser == null) return MotionResult.Fail(-6, "null args");
                if (axisList.Length != targetUser.Length) return MotionResult.Fail(-6, "axisList/targetUser 长度不一致");
                if (axisList.Length < 2 || axisList.Length > 3) return MotionResult.Fail(-6, "本适配器坐标系固定 3 轴，直线插补接受 2~3 轴");

                double ppu = 0;
                for (int i = 0; i < axisList.Length; i++)
                {
                    CheckRange(axisList[i]);
                    if (i == 0) ppu = _ppu[axisList[i]];
                    else if (_ppu[axisList[i]] != ppu) return MotionResult.Fail(-6, "插补各轴 ppu 必须一致，当前 axis " + axisList[i]);
                }

                BrandUnits bu = Units.GaoChuan(ppu);
                int[] tgPos = new int[3];
                short mask = 0;
                for (int i = 0; i < axisList.Length; i++)
                {
                    double tgtUser = targetUser[i];
                    if (mode == MoveMode.Relative) tgtUser += GetCommandPos(axisList[i]);
                    tgPos[i] = Units.MmToPulseInt32(tgtUser, ppu, axisList[i], "GaoChuan");
                    mask |= (short)(1 << i);
                }

                return CrdSeg("直线插补", delegate(int segNo)
                {
                    return NMC_CrdLineXYZEx(_crd, segNo, mask, tgPos,
                                            0, Units.MmS2Vendor(p.Vel, bu), Units.MmS2_2ToVendor(p.Acc, bu), 0);
                }, "axis=" + mask + " target=" + string.Join("/", new int[] { tgPos[0], tgPos[1], tgPos[2] }));
            }
        }

        public MotionResult MoveArc2D(int axisX, int axisY, double i, double j, double endX, double endY, bool cw, MoveParams p)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                CheckRange(axisX); CheckRange(axisY);

                double ppu = _ppu[axisX];
                if (_ppu[axisY] != ppu)
                    return MotionResult.Fail(-6, "圆弧插补要求 X/Y 轴 ppu 一致（X=" + ppu + " Y=" + _ppu[axisY] + "）");

                BrandUnits bu = Units.GaoChuan(ppu);
                // 官方 NMC_CrdArcCenterEx：pTgPosArray = 终点，pCenterPosArray = 圆心（相对起点增量）
                // 与 IMotionController 的 IJ 增量格式一致
                int[] tgPos = new int[2];
                tgPos[0] = Units.MmToPulseInt32(endX, ppu, axisX, "GaoChuan");
                tgPos[1] = Units.MmToPulseInt32(endY, ppu, axisY, "GaoChuan");
                int[] center = new int[2];
                center[0] = Units.MmToPulseInt32(i, ppu, axisX, "GaoChuan");
                center[1] = Units.MmToPulseInt32(j, ppu, axisY, "GaoChuan");

                return CrdSeg("圆弧插补", delegate(int segNo)
                {
                    // circleDir：官方注释 0=顺时针 1=逆时针（mtn_lib20.h L1282）
                    return NMC_CrdArcCenterEx(_crd, segNo, tgPos, center,
                                              (short)(cw ? 0 : 1), 0, Units.MmS2Vendor(p.Vel, bu), Units.MmS2_2ToVendor(p.Acc, bu), 0);
                }, "XY=(" + tgPos[0] + "," + tgPos[1] + ") IJ=(" + center[0] + "," + center[1] + ") " + (cw ? "CW" : "CCW"));
            }
        }

        /// <summary>向坐标系缓冲区压入一段并启动；segNo 取已用段数。</summary>
        private MotionResult CrdSeg(string what, Func<int, short> emit, string detail)
        {
            if (!EnsureCrd()) return MotionResult.Fail(-300, "坐标系创建失败");

            short sts = 0;
            if (NMC_CrdGetSts(_crd, out sts) == 0 && (sts & CRD_STS_MVERR) != 0)
                return MotionResult.Fail(-10, "坐标系处于运动错误态，请先 Close/Open 复位");

            int used = 0;
            NMC_CrdBufGetUsed(_crd, ref used);

            short rc = emit(used);
            if (rc != 0) return MotionResult.Fail(rc, what + " 下发失败 rc=" + rc);

            rc = NMC_CrdStartMtn(_crd);
            if (rc != 0) return MotionResult.Fail(rc, what + " NMC_CrdStartMtn rc=" + rc);

            return MotionResult.Success(what + " 已下发 segNo=" + used + "（" + detail + "）");
        }

        private bool EnsureCrd()
        {
            if (_crdReady && _crd != IntPtr.Zero) return true;

            short rc = NMC_CrdOpenEx(_dev, 0, ref _crd);
            if (rc != 0) { _crd = IntPtr.Zero; return false; }

            TCrdConfig cfg = new TCrdConfig();
            cfg.axCnts = 3;
            cfg.reserved = new short[3];
            cfg.pAxArray = new short[] { 0, 1, 2 };
            cfg.port = new short[] { 0, 0, 0, 0 };

            if (NMC_CrdConfig(_crd, ref cfg) != 0)
            {
                NMC_CrdClose(ref _crd);
                _crd = IntPtr.Zero;
                return false;
            }
            _crdReady = true;
            return true;
        }

        #endregion

        #region IO

        public MotionResult SetDo(int index, bool on)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                short rc = NMC_SetDOBit(_dev, (short)index, (short)(on ? 1 : 0));
                if (rc != 0) return MotionResult.Fail(rc, "NMC_SetDOBit " + index + " rc=" + rc);
                return MotionResult.Success();
            }
        }

        public bool GetDi(int index)
        {
            lock (_sdk)
            {
                if (!_open) return false;
                short v = 0;
                return NMC_GetDIBit(_dev, (short)index, out v) == 0 && v != 0;
            }
        }

        public double GetAi(int index)
        {
            lock (_sdk)
            {
                if (!_open) return 0;
                double v = 0;
                return NMC_IOAdcGet(_dev, 0, (byte)index, out v) == 0 ? v : 0;
            }
        }

        public MotionResult SetAo(int index, double volts)
        {
            lock (_sdk)
            {
                if (!_open) return MotionResult.Fail(-1, "not open");
                short rc = NMC_IODacSet(_dev, 0, (byte)index, volts);
                if (rc != 0) return MotionResult.Fail(rc, "NMC_IODacSet ch=" + index + " rc=" + rc);
                return MotionResult.Success("AO ch " + index + " = " + volts + " V");
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

        #region 工具

        /// <summary>
        ///
        /// 官方锚点（一手）：本地 mtn_lib20.h L310「Int16 smoothCoef; // 平滑系数,取值范围[0,199]」
        /// 与 L351「double smoothCoef; // 平滑系数,取值范围[0,199],数值越大则速度变化越平滑」。
        ///
        /// 区间映射（0~1 → 0~199）有官方区间支撑，**方向性也官方明写「数值越大越平滑」**，
        /// 故映射本身成立。
        ///
        /// ★ 仍存疑（NeedsVendorConfirm）：官方只给了区间的上下界，**没有定义 1 个单位对应多少时间**，
        ///   要标定需真机或官方编程手册正文。这属于「已知正确的量纲 + 未标定的比例尺」，
        ///   与固高 smoothTime 那类「量纲就错」是不同性质的问题。
        /// </summary>
        private static short Smooth(double jerkRatio) { return (short)Math.Round(Math.Max(0, Math.Min(199, jerkRatio * 199))); }

        private static string Round3(double v) { return Math.Round(v, 4).ToString(); }

        private void CheckRange(int axis)
        {
            if (axis < 0 || axis >= AXES) throw new ArgumentOutOfRangeException("axis", axis, "axis must be 0-" + (AXES - 1));
        }

        #endregion
    }
}