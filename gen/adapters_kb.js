// adapters_kb.js - 品牌知识库: 固高GTS / 雷赛LTDMC / 正运动ZMC / ACS / 骨架(软赢,高川)
(function (root, factory) {
  if (typeof module === 'object' && module.exports) { module.exports = factory(require('./adapter_common.js')); }
  else { root.MCKb = factory(root.MCAdapterCommon); }
})(typeof self !== 'undefined' ? self : this, function (common) {

  var B = common.buildAdapter;

  // ================= 固高 GTS/GTN =================
  // 真值依据（只认这三层外部真值，函数名/参数表/结构体字段顺序全部照抄，不凭记忆）：
  //   dll_exports.csv    PE 导出表        —— 函数是否存在、是否真的导出
  //   gts.csv            官方 C# 绑定     —— 函数原型
  //   gts_hdr.csv        官方 C 头文件    —— 返回类型与参数类型
  //   gts_structs.csv    官方头文件结构体布局 —— 结构体字段顺序/类型/偏移
  //   gts_hdr_macros.csv 官方头文件宏表   —— 回零模式号等常量
  var gts = {
    id: 'gts', label: '固高 GTS/GTN 系列', dll: 'gts.dll',
    ns: 'MotionControl.Adapters.Googol', cls: 'GoogolGtsController',
    device: 'GoogolGTS', brandEnum: 'Brand.GoogolGts', kbLevel: 'B',
    buildNote: '核心API已映射(Open/使能/回零/点位/IO/位置)。函数名、参数表与结构体字段顺序已按官方C#绑定/C头文件逐条核对；回零改用官方标准回零接口(GT_GetStandardHomePrm/GT_ExecuteStandardHome)。【重要】本套件的 gts.dll 绑定是【单卡库】口径(首参是通道号)；固高 SDK 的多卡库 DLL 同样叫 gts.dll、导出名完全相同，但每个 API 多一个 cardNum 首参——混用会让参数整体错位。插多张卡时必须改用多卡库签名，或用 GT_SetCardNo 逐卡切换。',
    file: 'src/Adapters/GoogolGtsController.cs',
    api: [
      { f: 'GT_Open(channel, param)', use: '打开卡', chk: '官方原型 GT_Open(short channel,short param)（gts.csv:326 / gts_hdr.csv:367）；第一参是通道(卡)号，第二参 param 按卡型取值。变体口径：这是【单卡库】签名；多卡库 gts.dll 的 GT_Open 是 (short cardNum, short channel, short param)——DLL 同名、导出名相同，混用会参数错位', ok: true },
      { f: 'GT_Close()', use: '关闭卡', chk: '官方原型 GT_Close()（gts.csv:79 / gts_hdr.csv:85）', ok: true },
      { f: 'GT_Reset()', use: '复位控制器', chk: '官方原型 GT_Reset()（gts.csv:356 / gts_hdr.csv:398）；打开后必须复位一次', ok: true },
      { f: 'GT_LoadConfig(pFile)', use: '加载GTC文件配置', chk: '官方原型 GT_LoadConfig(string pFile)（gts.csv:322 / gts_hdr.csv:360）；配置文件由固高配置工具生成', ok: true },
      { f: 'GT_AxisOn(axis) / GT_AxisOff(axis)', use: '使能/下使能', chk: '官方原型 GT_AxisOn(short axis) / GT_AxisOff(short axis)（gts.csv:42 / gts_hdr.csv:45）；轴号1-based', ok: true },
      { f: 'GT_ClrSts(axis, count)', use: '清报警', chk: '官方原型 GT_ClrSts(short axis,short count)（gts.csv:81 / gts_hdr.csv:87）；官方是两参，count 传 1', ok: true },
      { f: 'GT_PrfTrap + GT_SetTrapPrm', use: '梯形/S曲线点位', chk: '官方原型 GT_SetTrapPrm(short profile,ref TTrapPrm pPrm)（gts.csv:454 / gts_hdr.csv:513）；结构体 TTrapPrm 字段序 acc/dec/velStart/smoothTime(short)（gts_structs.csv）', ok: true },
      { f: 'GT_SetPos/GT_SetVel + GT_Update(mask)', use: '设目标/速度并触发', chk: '官方原型 GT_SetPos(short profile,int pos) / GT_SetVel(short profile,double vel) / GT_Update(int mask)（gts.csv:439/459/470）；Update掩码bit=轴号-1', ok: true },
      { f: 'GT_PrfJog + GT_SetJogPrm', use: 'Jog', chk: '官方原型 GT_SetJogPrm(short profile,ref TJogPrm pPrm)（gts.csv:422 / gts_hdr.csv:470）；TJogPrm 官方只有 acc/dec/smooth 三个字段，Jog 速度必须走 GT_SetVel（速度符号即方向）', ok: true },
      { f: 'GT_Stop(mask, option)', use: '停止', chk: '官方原型 GT_Stop(int mask,int option)（gts.csv:465 / gts_hdr.csv:527）；option 0=减速 1=急停', ok: true },
      { f: 'GT_GetStandardHomePrm + GT_ExecuteStandardHome', use: '回零', chk: '官方原型 GT_GetStandardHomePrm(short axis,out TStandardHomePrm) / GT_ExecuteStandardHome(short axis,ref TStandardHomePrm)（gts.csv:241/119 / gts_hdr.csv:264/128）；官方 mode 宏全表在 gts_hdr_macros.csv:163-171：HOME_MODE_LIMIT=10 / HOME_MODE_LIMIT_HOME=11 / HOME_MODE_LIMIT_INDEX=12 / HOME_MODE_LIMIT_HOME_INDEX=13 / HOME_MODE_HOME=20 / HOME_MODE_HOME_INDEX=22 / HOME_MODE_INDEX=30 / HOME_MODE_FORCED_HOME=40 / HOME_MODE_FORCED_HOME_INDEX=41。本套件只映射其中 4 个（HomeMode 枚举只有 4 值）—— 需限位+原点+索引组合回零（11/12/13）或强制回零（40/41）时，官方有这个 mode，本套件没暴露，要自己扩 HomeMode 枚举再加 case', ok: true },
      { f: 'GT_GetSts(axis,...)', use: '轴状态', chk: '官方原型 GT_GetSts(short axis,out int pSts,short count,out uint pClock)（gts.csv:244 / gts_hdr.csv:267）；使能/运动/报警/限位位定义按手册bit表核对', ok: true },
      { f: 'GT_GetPrfPos/GT_GetEncPos', use: '指令/反馈位置', chk: '官方原型 GT_GetPrfPos(short profile,out double pValue,short count,out uint pClock) / GT_GetEncPos(short encoder,...)（gts.csv:230/186）；单位脉冲', ok: true },
      { f: 'GT_GetDi(diType) / GT_SetDoBit(doType,index,value)', use: 'IO', chk: '官方原型 GT_GetDi(short diType,out int pValue) / GT_SetDoBit(short doType,short doIndex,short value)（gts.csv:180/396）；官方宏表里有候选值 MC_GPI=4 / MC_GPO=12（gts_hdr_macros.csv:17,22），但真值未标注其与 diType/doType 的对应关系，故代码保留[核对]标记取值 1，待手册确认', ok: true },
      { f: '(未实现) 模拟量 AI/AO', use: '力控模拟量', chk: '官方 C# 绑定与 PE 导出表均无 GT_GetAi/GT_SetAo，已删除该臆造声明；固高模拟量是通道型接口 GT_GetAdc / GT_SetDac / GT_PtAo（gts.csv:129/393/336），值域与单位换算须按手册确认后再接入', ok: false }
    ],
    pinvoke: `
        // [真值] 函数名/参数表取自 gts.csv(官方C#绑定) 与 gts_hdr.csv(官方C头文件)，并已对 dll_exports.csv(PE导出表) 校验
        // [变体口径·必读] 本套件按【单卡库】(64bit_single_card) 签名绑定：首参是通道号，无 cardNum。
        //        固高 SDK 的【多卡库】DLL 同样叫 gts.dll、导出名完全相同，但每个 API 多一个 cardNum 首参
        //        (例：GT_Open 単卡(short channel, short param) vs 多卡(short cardNum, short channel, short param))。
        //        两者混用 → 参数整体错位 → 打开到错误的卡/参数全乱，且不抛异常。
        //        插多张卡时：改用多卡库签名，或用 GT_SetCardNo 逐卡切换。
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_Open(short channel, short param);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_Close();
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_Reset();
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern short GT_LoadConfig([MarshalAs(UnmanagedType.LPStr)] string pFile);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_ClrSts(short axis, short count);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_AxisOn(short axis);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_AxisOff(short axis);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_Stop(int mask, int option);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_SetPos(short profile, int pos);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_SetVel(short profile, double vel);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_Update(int mask);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_PrfTrap(short profile);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_SetTrapPrm(short profile, ref TTrapPrm pPrm);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_PrfJog(short profile);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_SetJogPrm(short profile, ref TJogPrm pPrm);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_GetStandardHomePrm(short axis, out TStandardHomePrm pHomePrm);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_ExecuteStandardHome(short axis, ref TStandardHomePrm pHomePrm);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_GetSts(short axis, out int pSts, short count, out uint pClock);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_GetPrfPos(short profile, out double pValue, short count, out uint pClock);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_GetEncPos(short encoder, out double pValue, short count, out uint pClock);
        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_GetDi(short diType, out int pValue);
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
        }`,
    helpers: `
        /// <summary>统一0-based轴号 -> 固高1-based</summary>
        private static short Ax(int axis) { return (short)(axis + 1); }

        // [核对] diType/doType 的宏值：官方头文件宏表里没有名为 GT_DI_GPI/GT_DO_GPO 的宏，
        //         但有语义接近的候选值 MC_GPI=4 / MC_GPO=12（gts_hdr_macros.csv:17,22）——
        //         真值未标注它们与 diType/doType 的对应关系，故仍取 1，
        //         必须按《GTN系列运动控制卡用户手册》确认后再上真机。
        private const short GtsDiTypeGpi = 1;
        private const short GtsDoTypeGpo = 1;`,
    open: `
                short rc = GT_Open(info.CardNo, 0);   // [核对] 官方首参名为 channel（通道/卡号），param 传 0
                if (rc != 0) return MotionResult.Fail(rc, "GT_Open card=" + info.CardNo);
                GT_Reset();
                if (!string.IsNullOrEmpty(info.ConfigFile))
                {
                    rc = GT_LoadConfig(info.ConfigFile);
                    if (rc != 0) { GT_Close(); return MotionResult.Fail(rc, "GT_LoadConfig " + info.ConfigFile); }
                }`,
    close: `
                GT_Reset();
                GT_Close();`,
    enable: `
                short ax = Ax(axis);
                short rc = GT_ClrSts(ax, 1);   // 官方原型 GT_ClrSts(short axis,short count)
                if (rc != 0) return MotionResult.Fail(rc, "GT_ClrSts");
                rc = on ? GT_AxisOn(ax) : GT_AxisOff(ax);
                if (rc != 0) return MotionResult.Fail(rc, "GT_Axis" + (on ? "On" : "Off") + " axis " + axis);
                return MotionResult.Success();`,
    state: `
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
                return s;`,
    cmdPos: `
                double pos; uint clk;
                short rc = GT_GetPrfPos(Ax(axis), out pos, 1, out clk);
                return rc == 0 ? pos / _ppu[axis] : 0;`,
    fbPos: `
                double pos; uint clk;
                short rc = GT_GetEncPos(Ax(axis), out pos, 1, out clk);
                return rc == 0 ? pos / _ppu[axis] : GetCommandPos(axis);`,
    home: `
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
                return MotionResult.Success("homing started");`,
    homeDoneExpr: '!IsMovingGts(axis)',
    moveTo: `
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
                return MotionResult.Success();`,
    jog: `
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
                return MotionResult.Success("jog vel=" + velUserPerSec.ToString("F2"));`,
    stop: `
                int mask = 1 << axis;
                short rc = GT_Stop(mask, emergency ? 1 : 0);
                if (rc != 0) return MotionResult.Fail(rc, "GT_Stop");
                return MotionResult.Success(emergency ? "emergency stop" : "stop");`,
    isMovingExpr: 'IsMovingGts(axis)',
    moveLine: `
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
                return MotionResult.Success("equal-time line, axes=" + axisList.Length);`,
    arcNote: '                // [升级路径] 硬件圆弧: 官方坐标系圆弧接口 GT_ArcXYR / GT_CrdDataCircle（坐标系模式），精度与速度优于线性化',
    setDo: `
                // [真值] 官方单点输出为 GT_SetDoBit(short doType,short doIndex,short value)（gts.csv:396）
                short rc = GT_SetDoBit(GtsDoTypeGpo, (short)index, (short)(on ? 1 : 0));   // [核对] doType 宏值
                if (rc != 0) return MotionResult.Fail(rc, "GT_SetDoBit " + index);
                return MotionResult.Success();`,
    getDi: `
                int di;
                short rc = GT_GetDi(GtsDiTypeGpi, out di);   // [核对] diType 宏值; 返回组值则按位取
                if (rc != 0) return false;
                return ((di >> index) & 1) != 0;`,
    getAi: `
                // [未实现] 官方 C# 绑定与 PE 导出表里都没有 GT_GetAi（臆造名，已删除）。
                //          固高模拟量走通道型接口 GT_GetAdc(short adc,out double pValue,short count,out uint pClock)
                //          (gts.csv:129 / gts_hdr.csv:137)，但真值未给出值域与单位换算，须按手册确认后再接入。
                return 0;`,
    setAo: `
                // [未实现] 官方没有 GT_SetAo（臆造名，已删除）；候选 GT_SetDac / GT_PtAo（gts.csv:393 / 336）
                //          的值域/单位换算须按手册确认后再接入。
                return MotionResult.Fail(-300, "[未实现] 固高模拟量输出: 官方无 GT_SetAo 等价声明(候选 GT_SetDac/GT_PtAo), 需按手册确认值域后接入");`,
    extraMethods: `
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
        }`
  };

  // ================= 雷赛 LTDMC =================
  // 真值依据：dll_exports.csv(PE导出表) + leisai.csv(官方C#绑定) + leisai_cpp.csv(官方C头文件)
  var leadshine = {
    id: 'leadshine', label: '雷赛 DMC/EMC 系列 (LTDMC)', dll: 'LTDMC.dll',
    ns: 'MotionControl.Adapters.Leadshine', cls: 'LeadshineDmcController',
    device: 'LeadshineDMC', brandEnum: 'Brand.Leadshine', kbLevel: 'B',
    buildNote: '核心API已映射(board/点位/停止/位置/IO/模拟量)。函数名与参数表已按官方C#绑定/C头文件核对；官方无软件使能接口(脉冲卡硬线使能)，使能项标注未实现。',
    file: 'src/Adapters/LeadshineDmcController.cs',
    api: [
      { f: 'dmc_board_init()', use: '初始化库并枚举卡', chk: '官方原型 dmc_board_init()（leisai.csv:4 / leisai_cpp.csv:32）；返回卡数，卡信息用官方 dmc_get_CardInfList(leisai.csv:10)', ok: true },
      { f: 'dmc_board_close()', use: '关闭库', chk: '官方原型 dmc_board_close()（leisai.csv:6 / leisai_cpp.csv:30）', ok: true },
      { f: 'dmc_pmove(card,axis,dist,posi_mode)', use: '点位运动', chk: '官方原型 dmc_pmove(UInt16 CardNo,UInt16 axis,Int32 Dist,UInt16 posi_mode)（leisai.csv:71 / leisai_cpp.csv:816）；posi_mode 0/1 绝对相对定义、脉冲单位', ok: true },
      { f: 'dmc_set_profile(card,axis,...)', use: '速度曲线', chk: '官方原型 dmc_set_profile(UInt16 CardNo,UInt16 axis,double Min_Vel,double Max_Vel,double Tacc,double Tdec,double stop_vel)（leisai.csv:61 / leisai_cpp.csv:1110）；参数序(min,max,tacc,tdec,stop)', ok: true },
      { f: 'dmc_stop(card,axis,stop_mode)', use: '停止', chk: '官方原型 dmc_stop(UInt16 CardNo,UInt16 axis,UInt16 stop_mode)（leisai.csv:327 / leisai_cpp.csv:1236）；0=减速 1=立即', ok: true },
      { f: 'dmc_check_done(card,axis)', use: '运动完成判断', chk: '官方原型 dmc_check_done(UInt16 CardNo,UInt16 axis)（leisai.csv:321 / leisai_cpp.csv:61）；返回1=停止', ok: true },
      { f: 'dmc_get_position(card,axis)', use: '指令位置', chk: '官方C头文件 long dmc_get_position(WORD CardNo,WORD axis)（leisai_cpp.csv:552）；官方C#绑定返回 Int32（EMC系列开发头文件\\C#\\LTDMC.cs:891）——Windows LLP64 下 C 的 long 是 32 位，故 C# 侧必须用 int 而不是 long（用 long 会读到 RAX 未定义的高 32 位）；参数表 leisai.csv:314；单位脉冲', ok: true },
      { f: 'dmc_get_encoder(card,axis)', use: '反馈位置', chk: '官方C头文件 long dmc_get_encoder(WORD CardNo,WORD axis)（leisai_cpp.csv:406）；官方C#绑定返回 Int32（EMC系列开发头文件\\C#\\LTDMC.cs:859）——Windows LLP64 下 C 的 long 是 32 位，故 C# 侧必须用 int 而不是 long；参数表 leisai.csv:302；单位脉冲', ok: true },
      { f: 'dmc_vmove(card,axis,dir) / dmc_change_speed(card,axis,vel,taccdec)', use: 'Jog/变速', chk: '官方原型 dmc_vmove(UInt16 CardNo,UInt16 axis,UInt16 dir)（leisai.csv:81）/ dmc_change_speed(UInt16 CardNo,UInt16 axis,double Curr_Vel,double Taccdec)（leisai.csv:76）；change_speed 官方是四参', ok: true },
      { f: 'dmc_set_homemode(...) + dmc_home_move(card,axis)', use: '触发回零', chk: '官方原型 dmc_set_homemode(UInt16 CardNo,UInt16 axis,UInt16 home_dir,double vel,UInt16 mode,UInt16 EZ_count)（leisai.csv:115 / leisai_cpp.csv:1041）+ dmc_home_move(UInt16 CardNo,UInt16 axis)（leisai.csv:119）；mode/home_dir 编号按手册', ok: true },
      { f: 'dmc_read_inbit(card,bitno) / dmc_write_outbit(card,bitno,on_off)', use: '读DI/写DO', chk: '官方原型 dmc_read_inbit(UInt16 CardNo,UInt16 bitno)（leisai.csv:259）/ dmc_write_outbit(UInt16 CardNo,UInt16 bitno,UInt16 on_off)（leisai.csv:260）；DI/DO 低有效极性按手册核对', ok: true },
      { f: 'dmc_get_ad_input(card,ch) / dmc_set_da_output(card,ch,vout)', use: 'AI/AO', chk: '官方原型 dmc_get_ad_input(ushort CardNo,ushort channel,ref double Vout)（leisai.csv:442 / leisai_cpp.csv:319）/ dmc_set_da_output(UInt16 CardNo,UInt16 channel,double Vout)（leisai.csv:440 / leisai_cpp.csv:981）；输出参数官方即电压(Vout)，不再做码值换算', ok: true },
      { f: '(未实现) 总线卡软件使能', use: '使能', chk: '官方 C# 绑定/C 头文件/PE 导出表均无 dmc_set_servo 或任何软件使能声明，已删除该臆造声明；雷赛脉冲卡为硬线使能，总线卡使能接口须按型号手册确认后再接入', ok: false }
    ],
    pinvoke: `
        // [真值] 函数名/参数表取自 leisai.csv(官方C#绑定) 与 leisai_cpp.csv(官方C头文件)，并已对 dll_exports.csv(PE导出表) 校验
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_board_init();
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_board_close();
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_pmove(ushort card, ushort axis, int dist, ushort posi_mode);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_set_profile(ushort card, ushort axis, double minVel, double maxVel, double tacc, double tdec, double stopVel);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_stop(ushort card, ushort axis, ushort stopMode);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_check_done(ushort card, ushort axis);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int dmc_get_position(ushort card, ushort axis);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int dmc_get_encoder(ushort card, ushort axis);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_vmove(ushort card, ushort axis, ushort dir);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_change_speed(ushort card, ushort axis, double currVel, double taccdec);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_home_move(ushort card, ushort axis);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_set_homemode(ushort card, ushort axis, ushort homeDir, double vel, ushort mode, ushort ezCount);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_read_inbit(ushort card, ushort bitno);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_write_outbit(ushort card, ushort bitno, ushort onOff);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_get_ad_input(ushort card, ushort channel, ref double vout);
        [DllImport("LTDMC.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short dmc_set_da_output(ushort card, ushort channel, double vout);`,
    helpers: `
        private ushort Card { get { return (ushort)(_info == null ? 0 : _info.CardNo); } }`,
    open: `
                short n = dmc_board_init();
                if (n <= 0) return MotionResult.Fail(n, "dmc_board_init: no card found");
                if (info.CardNo >= n) return MotionResult.Fail(-9, "card " + info.CardNo + " not present, cards=" + n);`,
    close: `
                dmc_board_close();`,
    enable: `
                // [未实现] 官方 C# 绑定/C 头文件/PE 导出表里都没有 dmc_set_servo（或任何软件使能声明），该臆造声明已删除。
                //          雷赛脉冲卡(DMC系列)为硬线使能；总线卡(EMC)使能接口须按型号手册确认后再接入。
                return MotionResult.Fail(-300, "[未实现] 雷赛软件使能: 官方无 dmc_set_servo 等价声明(脉冲卡为硬线使能)");`,
    state: `
                AxisState s = AxisState.None;
                short done = dmc_check_done(Card, (ushort)axis);
                if (done == 0) s |= AxisState.Moving; else s |= AxisState.InPosition;
                if (_homed[axis]) s |= AxisState.Homed;
                // [核对] 报警/限位状态需按型号读取状态寄存器(如 dmc_get_axis_state)
                return s;`,
    cmdPos: `
                return dmc_get_position(Card, (ushort)axis) / _ppu[axis];`,
    fbPos: `
                return dmc_get_encoder(Card, (ushort)axis) / _ppu[axis];`,
    home: `
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
                return MotionResult.Success("homing started");`,
    homeDoneExpr: 'dmc_check_done(Card, (ushort)axis) == 1',
    moveTo: `
                ushort card = Card;
                dmc_set_profile(card, (ushort)axis, 0, Math.Abs(p.Vel) * _ppu[axis],
                    1000.0 / Math.Max(p.Acc, 1), 1000.0 / Math.Max(p.Dec, 1), 0);   // [核对] tacc/tdec单位ms
                short rc = dmc_pmove(card, (ushort)axis, (int)Math.Round(to * _ppu[axis]), 1);  // [核对] posi_mode 1=绝对(或相反)
                if (rc != 0) return MotionResult.Fail(rc, "dmc_pmove");
                return MotionResult.Success();`,
    jog: `
                ushort dir = velUserPerSec >= 0 ? (ushort)0 : (ushort)1;   // [核对] 方向定义
                dmc_set_profile(Card, (ushort)axis, 0, Math.Abs(velUserPerSec) * _ppu[axis], 50, 50, 0);
                short rc = dmc_vmove(Card, (ushort)axis, dir);
                if (rc != 0) return MotionResult.Fail(rc, "dmc_vmove");
                return MotionResult.Success("jog dir=" + dir);`,
    stop: `
                short rc = dmc_stop(Card, (ushort)axis, (ushort)(emergency ? 1 : 0));
                if (rc != 0) return MotionResult.Fail(rc, "dmc_stop");
                return MotionResult.Success(emergency ? "emergency" : "stop");`,
    isMovingExpr: 'dmc_check_done(Card, (ushort)axis) == 0',
    moveLine: `
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
                return MotionResult.Success("equal-time line, axes=" + axisList.Length);`,
    arcNote: '                // [升级路径] 雷赛硬件圆弧: 官方 dmc_arc_move_radius_unit / dmc_arc_move_center_unit / dmc_arc_move_3points_unit',
    setDo: `
                // [注意] 雷赛DO低有效: 0=输出导通
                ushort v = on ? (ushort)0 : (ushort)1;
                short rc = dmc_write_outbit(Card, (ushort)index, v);
                if (rc != 0) return MotionResult.Fail(rc, "dmc_write_outbit " + index);
                return MotionResult.Success();`,
    getDi: `
                short v = dmc_read_inbit(Card, (ushort)index);
                return v == 0;   // [注意] 雷赛DI低有效: 0=导通`,
    getAi: `
                // [真值] 官方原型 dmc_get_ad_input(ushort CardNo,ushort channel,ref double Vout)：输出即电压
                double v = 0;
                short rc = dmc_get_ad_input(Card, (ushort)index, ref v);
                return rc == 0 ? v : 0;`,
    setAo: `
                // [真值] 官方原型 dmc_set_da_output(UInt16 CardNo,UInt16 channel,double Vout)：入参即电压
                short rc = dmc_set_da_output(Card, (ushort)index, volts);
                if (rc != 0) return MotionResult.Fail(rc, "dmc_set_da_output " + index);
                return MotionResult.Success();`
  };

  // ================= 正运动 ZMC =================
  // 真值依据：dll_exports.csv(PE导出表) + zmotion.csv(官方C#绑定) + zmotion_cpp.csv(官方C头文件)
  // 注意：官方 DLL 名是 zauxdll.dll（PE 导出表里根本没有 zmcaux.dll）
  var zmotion = {
    id: 'zmotion', label: '正运动 ZMC 系列', dll: 'zauxdll.dll',
    ns: 'MotionControl.Adapters.ZMotion', cls: 'ZMotionZmcController',
    device: 'ZMotionZMC', brandEnum: 'Brand.ZMotion', kbLevel: 'B',
    buildNote: '核心API已映射(EtherCAT网络连接/当量/点位/回零/IO/模拟量)。DLL名已按PE导出表改为 zauxdll.dll，ZAux_Direct 系列函数名与参数表已按官方C#绑定/C头文件逐条核对。',
    file: 'src/Adapters/ZMotionZmcController.cs',
    api: [
      { f: 'ZAux_OpenEth(ip,&h) / ZAux_Close(h)', use: '网口连接/断开', chk: '官方原型 ZAux_OpenEth(string ipaddr,out IntPtr phandle)（zmotion.csv:374 / zmotion_cpp.csv:394）/ ZAux_Close(IntPtr handle)（zmotion.csv:35）；控制器默认IP 192.168.0.11', ok: true },
      { f: 'ZAux_Direct_SetUnits(h,axis,v)', use: '当量(脉冲/unit)', chk: '官方原型 ZAux_Direct_SetUnits(IntPtr handle,int iaxis,float fValue)（zmotion.csv:314 / zmotion_cpp.csv:324）', ok: true },
      { f: 'ZAux_Direct_SetSpeed/SetAccel/SetDecel', use: '速度/加减速', chk: '官方原型 ZAux_Direct_SetSpeed(IntPtr,int,float)（zmotion.csv:308）/SetAccel（:254）/SetDecel（:268）；用户单位/s', ok: true },
      { f: 'ZAux_Direct_Single_MoveAbs / ZAux_Direct_Single_Move', use: '单轴绝对/相对点位', chk: '官方原型 ZAux_Direct_Single_MoveAbs(IntPtr,int,float fdistance)（zmotion.csv:328）/ ZAux_Direct_Single_Move(IntPtr,int,float fdistance)（zmotion.csv:327）；相对移动用 Single_Move', ok: true },
      { f: 'ZAux_Direct_MoveAbs(h,n,axislist[],dpos[])', use: '多轴直线插补', chk: '官方原型 ZAux_Direct_MoveAbs(IntPtr handle,int imaxaxises,int[] piAxislist,float[] pfDposlist)（zmotion.csv:194 / zmotion_cpp.csv:201）', ok: true },
      { f: 'ZAux_Direct_Single_Vmove(h,axis,dir)', use: 'Jog', chk: '官方原型 ZAux_Direct_Single_Vmove(IntPtr handle,int iaxis,int idir)（zmotion.csv:329 / zmotion_cpp.csv:341）', ok: true },
      { f: 'ZAux_Direct_Single_Datum(h,axis,imode)', use: '回零', chk: '官方原型 ZAux_Direct_Single_Datum(IntPtr handle,int iaxis,int imode)（zmotion.csv:326 / zmotion_cpp.csv:336）；imode 按手册DATUM模式表', ok: true },
      { f: 'ZAux_Direct_Single_Cancel(h,axis,imode)', use: '停止', chk: '官方原型 ZAux_Direct_Single_Cancel(IntPtr handle,int iaxis,int imode)（zmotion.csv:325 / zmotion_cpp.csv:335）；imode 2=急停', ok: true },
      { f: 'ZAux_Direct_GetMpos / ZAux_Direct_GetDpos', use: '指令/反馈位置', chk: '官方原型 ZAux_Direct_GetMpos(IntPtr,int,ref float pfValue)（zmotion.csv:124）/ ZAux_Direct_GetDpos(IntPtr,int,ref float pfValue)（zmotion.csv:82）；单位受UNITS影响', ok: true },
      { f: 'ZAux_Direct_GetIfIdle(h,axis,&v)', use: '运动空闲判断', chk: '官方原型 ZAux_Direct_GetIfIdle(IntPtr handle,int iaxis,ref int piValue)（zmotion.csv:106 / zmotion_cpp.csv:110）；0=运动中', ok: true },
      { f: 'ZAux_Direct_GetIn / ZAux_Direct_SetOp', use: 'IN/OUT', chk: '官方原型 ZAux_Direct_GetIn(IntPtr,int ionum,ref UInt32 piValue)（zmotion.csv:108）/ ZAux_Direct_SetOp(IntPtr,int ionum,UInt32 iValue)（zmotion.csv:297）；编号0起', ok: true },
      { f: 'ZAux_Direct_GetAD / ZAux_Direct_SetDA', use: 'ADC/DAC', chk: '官方原型 ZAux_Direct_GetAD(IntPtr,int ionum,ref float pfValue)（zmotion.csv:59 / zmotion_cpp.csv:63）/ ZAux_Direct_SetDA(IntPtr,int ionum,float fValue)（zmotion.csv:265 / zmotion_cpp.csv:275）；量程换算按手册', ok: true },
      { f: 'ZAux_Direct_SetAxisEnable(h,axis,iValue)', use: '使能', chk: '官方原型 ZAux_Direct_SetAxisEnable(IntPtr handle,int iaxis,int iValue)（zmotion.csv:258 / zmotion_cpp.csv:268）', ok: true }
    ],
    pinvoke: `
        // [真值] DLL 名取自 dll_exports.csv(PE导出表)：官方是 zauxdll.dll，不存在 zmcaux.dll
        // [真值] 函数名/参数表取自 zmotion.csv(官方C#绑定) 与 zmotion_cpp.csv(官方C头文件)
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern int ZAux_OpenEth([MarshalAs(UnmanagedType.LPStr)] string ipaddr, out IntPtr phandle);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Close(IntPtr handle);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetUnits(IntPtr h, int iaxis, float value);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetSpeed(IntPtr h, int iaxis, float value);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetAccel(IntPtr h, int iaxis, float value);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetDecel(IntPtr h, int iaxis, float value);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_Single_MoveAbs(IntPtr h, int iaxis, float fdistance);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_Single_Move(IntPtr h, int iaxis, float fdistance);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_MoveAbs(IntPtr h, int imaxaxises, int[] piAxislist, float[] pfDposlist);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_Single_Vmove(IntPtr h, int iaxis, int idir);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_Single_Cancel(IntPtr h, int iaxis, int imode);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_Single_Datum(IntPtr h, int iaxis, int imode);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_GetMpos(IntPtr h, int iaxis, out float pfValue);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_GetDpos(IntPtr h, int iaxis, out float pfValue);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_GetIfIdle(IntPtr h, int iaxis, out int piValue);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_GetIn(IntPtr h, int ionum, out uint piValue);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetOp(IntPtr h, int ionum, uint iValue);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_GetAD(IntPtr h, int ionum, out float pfValue);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetDA(IntPtr h, int ionum, float fValue);
        [DllImport("zauxdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZAux_Direct_SetAxisEnable(IntPtr h, int iaxis, int iValue);`,
    extraFields: `
        private IntPtr _h = IntPtr.Zero;`,
    close: `
                if (_h != IntPtr.Zero) { ZAux_Close(_h); _h = IntPtr.Zero; }`,
    open: `
                IntPtr h;
                int rc = ZAux_OpenEth(info.Ip, out h);
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_OpenEth " + info.Ip);
                _h = h;`,
    enable: `
                // [真值] 官方使能接口 ZAux_Direct_SetAxisEnable(IntPtr handle,int iaxis,int iValue)
                int rc = ZAux_Direct_SetAxisEnable(_h, axis, on ? 1 : 0);
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_Direct_SetAxisEnable");
                return MotionResult.Success();`,
    state: `
                int idle;
                ZAux_Direct_GetIfIdle(_h, axis, out idle);
                AxisState s = AxisState.None;
                if (idle == 0) s |= AxisState.Moving; else s |= AxisState.InPosition;
                if (_homed[axis]) s |= AxisState.Homed;
                return s;`,
    cmdPos: `
                float pos;
                ZAux_Direct_GetMpos(_h, axis, out pos);
                return pos;`,
    fbPos: `
                // [真值] 反馈位置用官方 ZAux_Direct_GetDpos（指令位置是 GetMpos）
                float pos;
                ZAux_Direct_GetDpos(_h, axis, out pos);
                return pos;`,
    home: `
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
                return MotionResult.Success("homing started mode=" + mode);`,
    homeDoneExpr: 'ZmotionIdle(axis)',
    moveTo: `
                ZAux_Direct_SetSpeed(_h, axis, (float)Math.Abs(p.Vel));
                ZAux_Direct_SetAccel(_h, axis, (float)p.Acc);
                ZAux_Direct_SetDecel(_h, axis, (float)p.Dec);
                int rc = (mode == MoveMode.Absolute)
                    ? ZAux_Direct_Single_MoveAbs(_h, axis, (float)to)
                    : ZAux_Direct_Single_Move(_h, axis, (float)(to - from));
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_Direct_Single_Move");
                return MotionResult.Success();`,
    jog: `
                ZAux_Direct_SetSpeed(_h, axis, (float)Math.Abs(velUserPerSec));
                ZAux_Direct_SetAccel(_h, axis, (float)p.Acc);
                int dir = velUserPerSec >= 0 ? 1 : -1;
                int rc = ZAux_Direct_Single_Vmove(_h, axis, dir);
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_Direct_Single_Vmove");
                return MotionResult.Success("jog");`,
    stop: `
                // [真值] 官方停止接口 ZAux_Direct_Single_Cancel(IntPtr handle,int iaxis,int imode)
                int rc = ZAux_Direct_Single_Cancel(_h, axis, emergency ? 2 : 0);   // [核对] imode 2=急停
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_Direct_Single_Cancel");
                return MotionResult.Success();`,
    isMovingExpr: '!ZmotionIdle(axis)',
    moveLine: `
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
                return MotionResult.Success("hw linear interp, axes=" + axes.Length);`,
    arcNote: '                // [升级路径] 硬件圆弧: 官方 ZAux_Direct_MoveCircAbs / ZAux_Direct_MoveCirc（zmotion.csv:203/198）',
    setDo: `
                int rc = ZAux_Direct_SetOp(_h, index, on ? 1u : 0u);
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_Direct_SetOp " + index);
                return MotionResult.Success();`,
    getDi: `
                uint v;
                ZAux_Direct_GetIn(_h, index, out v);
                return v != 0;`,
    getAi: `
                // [真值] 官方 ZAux_Direct_GetAD(IntPtr,int ionum,ref float pfValue)
                float code;
                ZAux_Direct_GetAD(_h, index, out code);
                return code * 10.0 / 4096.0;   // [核对] 量程换算：真值未给出AD量程，按手册确认`,
    setAo: `
                // [真值] 官方 ZAux_Direct_SetDA(IntPtr,int ionum,float fValue)
                int code = (int)Math.Round(volts / 10.0 * 4095);   // [核对] 量程换算：真值未给出DA量程，按手册确认
                int rc = ZAux_Direct_SetDA(_h, index, code);
                if (rc != 0) return MotionResult.Fail(rc, "ZAux_Direct_SetDA " + index);
                return MotionResult.Success();`,
    configureExtra: `
                if (_h != IntPtr.Zero) ZAux_Direct_SetUnits(_h, i, (float)_ppu[i]);   // 当量同步到控制器`,
    extraMethods: `
        private bool ZmotionIdle(int axis)
        {
            int idle;
            ZAux_Direct_GetIfIdle(_h, axis, out idle);
            return idle != 0;
        }`
  };

  // ================= ACS SPiiPlus =================
  var acs = {
    id: 'acs', label: 'ACS SPiiPlus (SPiiPlusNET)', dll: 'ACS.SPiiPlusNET.dll',
    ns: 'MotionControl.Adapters.Acs', cls: 'AcsSpiiPlusController',
    device: 'ACS-SPiiPlus', brandEnum: 'Brand.Acs', kbLevel: 'B',
    buildNote: '基于官方.NET库(安装ACS软件后引用ACS.SPiiPlusNET.dll)，为高级EtherCAT平台(前瞻/力控强)。',
    file: 'src/Adapters/AcsSpiiPlusController.cs',
    usings: 'using ACS.SPiiPlusNET;',
    api: [
      { f: 'CommCtrl.OpenCommEthernetTCPSocket(ip,port)', use: 'TCP连接', chk: '默认端口701' },
      { f: 'Axis.Enable()/Disable()', use: '使能', chk: 'GetAxis((short)n)' },
      { f: 'Axis.MoveAbs(pos)', use: '绝对点位', chk: '单位counts; 用MUnits换算' },
      { f: 'Axis.Kill()', use: '急停', chk: '-' },
      { f: 'Axis.Halt()', use: '减速停', chk: '-' },
      { f: 'Axis.GoHome()/SetHomeMethod', use: '回零', chk: 'HOME方法编号按手册' },
      { f: 'Axis.Position / FError', use: '位置/跟随误差', chk: 'counts' },
      { f: 'Axis.MotorStatus', use: '状态位', chk: '位掩码核对' },
      { f: 'CommCtrl.GetBit/SetBit', use: 'IO', chk: 'MPI/用户位' }
    ],
    pinvoke: `
        // ACS 使用官方 .NET 托管库，无需 P/Invoke。
        // [部署] 从 ACS 软件安装目录引用 ACS.SPiiPlusNET.dll，
        //        build_net48.bat 需追加: /r:ACS.SPiiPlusNET.dll
        private CommCtrl _ctrl;
        private readonly Axis[] _axesArr = new Axis[8];`,
    extraFields: `
        private readonly double[] _homeMethod = new double[8];`,
    ctorExtra: `
            for (int i = 0; i < 8; i++) _homeMethod[i] = 21;   // [核对] HOME方法编号`,
    open: `
                _ctrl = new CommCtrl();
                _ctrl.OpenCommEthernetTCPSocket(info.Ip, (ushort)info.Port);   // [核对] 默认701
                for (int i = 0; i < 8; i++) _axesArr[i] = _ctrl.GetAxis((short)i);`,
    close: `
                if (_ctrl != null) { _ctrl.CloseComm(); _ctrl = null; }`,
    enable: `
                try
                {
                    if (on) _axesArr[axis].Enable(); else _axesArr[axis].Disable();
                    return MotionResult.Success();
                }
                catch (Exception ex) { return MotionResult.Fail(-200, ex.Message); }`,
    state: `
                AxisState s = AxisState.None;
                try
                {
                    MotorStatus ms = _axesArr[axis].MotorStatus;
                    if ((ms & MotorStatus.Enable) != 0) s |= AxisState.Enabled;    // [核对] 位定义
                    if ((ms & MotorStatus.InMotion) != 0) s |= AxisState.Moving;   // [核对]
                    if (_homed[axis]) s |= AxisState.Homed;
                }
                catch { }
                return s;`,
    cmdPos: `
                try { return _axesArr[axis].Position / _ppu[axis]; }
                catch { return 0; }`,
    fbPos: `
                try { return _axesArr[axis].Position / _ppu[axis]; }
                catch { return 0; }`,
    home: `
                try
                {
                    _axesArr[axis].SetHomeMethod(_homeMethod[axis]);   // [核对] 方法编号
                    _axesArr[axis].GoHome();
                    return MotionResult.Success("homing started");
                }
                catch (Exception ex) { return MotionResult.Fail(-200, ex.Message); }`,
    homeDoneExpr: '(_axesArr[axis].MotorStatus & MotorStatus.HomeComplete) != 0',
    moveTo: `
                try
                {
                    _axesArr[axis].VelHigh = Math.Abs(p.Vel) * _ppu[axis];   // [核对] 属性名
                    _axesArr[axis].AccHigh = p.Acc * _ppu[axis];             // [核对]
                    _axesArr[axis].MoveAbs(to * _ppu[axis]);
                    return MotionResult.Success();
                }
                catch (Exception ex) { return MotionResult.Fail(-200, ex.Message); }`,
    jog: `
                try
                {
                    _axesArr[axis].VelHigh = Math.Abs(velUserPerSec) * _ppu[axis];
                    _axesArr[axis].Jog(velUserPerSec >= 0 ? 1 : -1);   // [核对] Jog重载
                    return MotionResult.Success("jog");
                }
                catch (Exception ex) { return MotionResult.Fail(-200, ex.Message); }`,
    stop: `
                try
                {
                    if (emergency) _axesArr[axis].Kill(); else _axesArr[axis].Halt();
                    return MotionResult.Success(emergency ? "kill" : "halt");
                }
                catch (Exception ex) { return MotionResult.Fail(-200, ex.Message); }`,
    isMovingExpr: '(_axesArr[axis].MotorStatus & MotorStatus.InMotion) != 0',
    moveLine: `
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
                catch (Exception ex) { return MotionResult.Fail(-200, ex.Message); }`,
    arcNote: '                // [升级路径] ACS 前瞻圆弧: 圆弧MSeg缓冲区',
    setDo: `
                try { _ctrl.SetBit((ushort)index, on ? 1 : 0); return MotionResult.Success(); }
                catch (Exception ex) { return MotionResult.Fail(-200, ex.Message); }`,
    getDi: `
                try { return _ctrl.GetBit((ushort)index) != 0; }
                catch { return false; }`,
    getAi: `
                // [TODO] ACS 模拟量按型号补全（CommCtrl模拟量对象/属性）
                return 0;`,
    setAo: `
                return MotionResult.Fail(-300, "[TODO] ACS AO 按型号补全(见ApiChecklist)");`
  };

  // ================= 骨架（软赢 / 高川） =================
  function skeletonKb(id, label, dll, ns, cls, device, brandEnum, cName) {
    return {
      id: id, label: label, dll: dll, ns: ns, cls: cls, device: device,
      brandEnum: brandEnum, kbLevel: 'C',
      buildNote: '骨架级——接口/单位换算/线程模型已就绪，SDK调用点以 [TODO] 标注并给出手册核对路径。',
      file: 'src/Adapters/' + cls + '.cs',
      api: [
        { f: '(厂商手册)', use: '全部API', chk: '获取厂商《函数库手册》后按 [TODO] 标注逐个补全' }
      ],
      pinvoke: `
        // [TODO] 引入 ' + dll + ' 后按手册补全 P/Invoke 声明（参考同目录其他适配器的写法）
        private readonly bool[] _skelMoving = new bool[8];
        private readonly bool[] _skelHomeDone = new bool[8];`,
      helpers: '',
      bareOpen: true,
      open: `
                return MotionResult.Fail(-300, "[TODO] 按《' + label + '手册》实现连接初始化");`,
      close: '',
      enable: `
                return MotionResult.Fail(-300, "[TODO]");`,
      state: `
                return AxisState.None;`,
      cmdPos: `
                return 0;`,
      fbPos: `
                return 0;`,
      home: `
                return MotionResult.Fail(-300, "[TODO]");`,
      homeDoneExpr: '_skelHomeDone[axis]',
      moveTo: `
                return MotionResult.Fail(-300, "[TODO]");`,
      jog: `
                return MotionResult.Fail(-300, "[TODO]");`,
      stop: `
                return MotionResult.Fail(-300, "[TODO]");`,
      isMovingExpr: '_skelMoving[axis]',
      moveLine: `
                return MotionResult.Fail(-300, "[TODO]");`,
      arcNote: '                // [TODO]',
      setDo: `
                return MotionResult.Fail(-300, "[TODO]");`,
      getDi: `
                return false;`,
      getAi: `
                return 0;`,
      setAo: `
                return MotionResult.Fail(-300, "[TODO]");`
    };
  }

  var softwin = skeletonKb('softwin', '软赢 (SoftWin)', 'softwin.dll(以实际为准)',
    'MotionControl.Adapters.SoftWin', 'SoftWinController', 'SoftWin', 'Brand.SoftWin');
  var gaochuan = skeletonKb('gaochuan', '高川', 'gaochuan.dll(以实际为准)',
    'MotionControl.Adapters.GaoChuan', 'GaoChuanController', 'GaoChuan', 'Brand.GaoChuan');

  var virtualKb = {
    id: 'virtual', label: '虚拟控制器', dll: '(无)',
    ns: 'MotionControl.Virtual', cls: 'VirtualController',
    device: 'VirtualController', brandEnum: 'Brand.Virtual', kbLevel: 'A'
  };

  var all = { gts: gts, leadshine: leadshine, zmotion: zmotion, acs: acs, softwin: softwin, gaochuan: gaochuan, virtual: virtualKb };

  function buildFile(id) {
    var kb = all[id];
    var content = B(kb);
    // 附加品牌自定义辅助方法（插入到 CheckRange 之前，保持类内位置）
    if (kb.extraMethods) {
      content = content.replace('        private static void CheckRange(int axis)',
        kb.extraMethods + '\n        private static void CheckRange(int axis)');
    }
    return { path: kb.file, content: content };
  }

  return { kb: all, buildFile: buildFile };
});
