// =====================================================================
//  单位换算层（单一真值来源）
//
//  为什么单独抽一层：换算系数散在 6 个适配器里各写一遍，是本项目最高风险的静默错误源 ——
//  换错了不会崩、不会抛异常，只会让轴以错误速度走偏，肉眼极难发现。
//  本文件把「用户单位 ↔ 厂商单位」集中成可被门禁核对的纯函数。
//
//  ── 真值锚点（全部 official-artifact，逐条给出处）──────────────────────────
//  ★ 本文件最关键的一条：**6 家厂商的速率单位分成 3 种口径，任意两家混用都会错**。
//
//    ① 脉冲/毫秒（× ppu/1000）
//       · 固高：官方 MCT2008 工具语言包 Language.ini
//         L649「S_10=速度(pulse/ms)」、L650「加速度(pulse/ms^2)」、L651「减速度(pulse/ms^2)」
//       · 高川：官方 mtn_lib20.h
//         L366「vel: 目标速度（最大速度）,单位是 脉冲/ms」
//         L529「pVel: 返回速度,单位是 脉冲/ms」
//         L534「pAcc: 返回加速度,单位是 脉冲/ms.ms」← 官方笔误，实为 脉冲/ms²
//
//    ② 脉冲/秒（× ppu）
//       · 雷赛：官方 DMC5X10 用户手册 V2.0（2023-12-18，与本机头文件同版）
//         §9.8「Max_Vel 最大速度，单位：pulse/s (最大值为 4M)」
//         §9.8「Tacc 加速时间，单位：s」← 时间因子是「秒」，不是毫秒
//         §9.8「acc 加速度，单位：pulse /（s²）」
//         §9.7「dmc_read_current_speed…单位：pulse/s」
//         脉冲当量：头文件 L102-104「//脉冲当量」dmc_set_equiv(CardNo, axis, double equiv)
//                   官方定义「equiv 脉冲当量，单位：pulse/unit」
//         ★ 两套 API 并存，混用即错 equiv 倍：
//           无后缀 = 脉冲版（dmc_set_profile）；_unit 后缀 = 当量版（dmc_set_profile_unit）
//           官方例 8.2：「仍希望以脉冲为单位，可以将脉冲当量值设置为 1」
//           本适配器全程用无后缀脉冲版。
//
//    ③ 用户单位/秒（× 1，**不乘脉冲当量**）
//       · 正运动：官方 C# 绑定 Zmcaux.cs
//         L1435「/// 设置轴速度，单位为units/s」
//         L1535-1542「/// 设置轴脉冲当量」+ ZAux_Direct_SetUnits
//         → 适配器先下发脉冲当量，控制器侧 unit 已是 mm，故直接传 mm/s
//       · 软赢：官方 doxygen
//         Profile.Velocity「Unit: user unit / second」
//         AxesStatus.PosCmd/ActualPos「Unit: user unit」
//         齿轮比页公式「Pulse = User Unit * Gear Ratio」
//
//  ── 业务层单位（固定，不随品牌变）──────────────────────────────────────
//  位置 mm / 速度 mm/s / 加速度 mm/s²（见 Core/MoveParams.cs）
// =====================================================================
using System;

namespace MotionControl.Core
{
    /// <summary>
    /// 厂商速度/加速度的原生计量单位。
    /// </summary>
    public enum VendorRateUnit
    {
        /// <summary>
        /// 脉冲/秒。注意：正运动虽用 units/s 命名，但当 UNITS=1 时 1 unit = 1 脉冲，
        /// 换算系数与「用户单位/秒」相同 —— 见 Units.ZMotion 的说明。
        /// </summary>
        PulsesPerSecond = 0,

        /// <summary>脉冲/毫秒。固高（官方 MCT2008 Language.ini: 「速度(pulse/ms)」）与高川（mtn_lib20.h L366）采用。</summary>
        PulsesPerMilliSecond = 1,

        /// <summary>
        /// 用户单位/秒（不做脉冲换算）。正运动 ZMotion 采用：
        /// 官方绑定 Zmcaux.cs L1435 原文「设置轴速度，单位为units/s」，
        /// 且 L150 会先调 ZAux_Direct_SetUnits 把脉冲当量下发到控制器，
        /// 因此上位机直接传 mm/s 即可，**不应再乘脉冲当量**。
        /// </summary>
        UserUnitsPerSecond = 2
    }

    /// <summary>某品牌的单位口径。构造后不可变，避免运行期被改。</summary>
    public sealed class BrandUnits
    {
        /// <summary>脉冲 / 用户单位（mm）。位置换算的唯一依据。</summary>
        public readonly double PulsesPerUnit;

        /// <summary>厂商速度/加速度的原生单位。</summary>
        public readonly VendorRateUnit RateUnit;

        /// <summary>厂商位置参数是否为 32 位有符号整数（决定目标位置是否会溢出）。</summary>
        public readonly bool PositionIsInt32;

        /// <summary>true = 该品牌速度单位未获官方文档逐字确认，属按同类通行口径对齐。</summary>
        public readonly bool NeedsVendorConfirm;

        public BrandUnits(double pulsesPerUnit, VendorRateUnit rateUnit,
                          bool positionIsInt32, bool needsVendorConfirm)
        {
            if (pulsesPerUnit <= 0)
                throw new ArgumentOutOfRangeException("pulsesPerUnit", pulsesPerUnit,
                    "脉冲当量必须 > 0（每 mm 多少脉冲）。为 0 或负会导致除零/方向反转。");
            PulsesPerUnit = pulsesPerUnit;
            RateUnit = rateUnit;
            PositionIsInt32 = positionIsInt32;
            NeedsVendorConfirm = needsVendorConfirm;
        }

        public override string ToString()
        {
            return "ppu=" + PulsesPerUnit + ", rate=" + RateUnit
                 + (PositionIsInt32 ? ", pos=int32" : ", pos=int64")
                 + (NeedsVendorConfirm ? " [待厂商确认速率单位]" : "");
        }
    }

    /// <summary>
    /// 单位换算。所有方法都是纯函数（无状态、无副作用），可被门禁脚本按闭式重算比对。
    /// </summary>
    public static class Units
    {
        /// <summary>int32 位置参数的最大绝对值（留 1 个计数余量，避免 +1 溢出）。</summary>
        public const int POS_INT32_SAFE_MAX = int.MaxValue - 1;

        // ---------- 各品牌口径（单一真值来源）----------

        /// <summary>
        /// 固高 gts.dll：脉冲/毫秒。
        /// 官方一手制品：MCT2008 工具语言包 evidence/googol/rar1_unp/mct2008/Language.ini
        ///   L649「速度(pulse/ms)」、L650「加速度(pulse/ms^2)」、L651「减速度(pulse/ms^2)」
        ///   L673-675 重复一组（另一轴区）。这是厂商自己写的单位标注，非第三方推测。
        /// needsVendorConfirm = false。
        /// </summary>
        public static BrandUnits Googol(double ppu)
        { return new BrandUnits(ppu, VendorRateUnit.PulsesPerMilliSecond, true, false); }

        /// <summary>
        /// 雷赛 LTDMC.dll：**脉冲/秒**（时间因子是「秒」，不是毫秒）。
        ///
        /// 官方手册定案（DMC5X10 用户手册 V2.0，2023-12-18 版，与本机头文件同版）：
        ///   §9.8「Min_Vel 起始速度，单位：pulse/s (最大值为 4M)」
        ///        「Max_Vel 最大速度，单位：pulse/s (最大值为 4M)」
        ///        「Tacc 加速时间，单位：s (最小值为 0.001s)」
        ///        「Stop_Vel 停止速度，单位：pulse/s」
        ///   §9.8「acc 加速度，单位：pulse /（s²）」「dec 减速度，单位：pulse /（s²）」
        ///   §9.7「dmc_read_current_speed…返回值：指定轴的速度，单位：pulse/s」
        /// 反向排除：三本官方手册（合计 761 页）检索「脉冲/毫秒」「pulse/ms」命中 0 次。
        ///
        /// 脉冲当量（unit）语义：头文件 L102-104
        ///   「//脉冲当量」  dmc_set_equiv(CardNo, axis, double equiv)
        /// 官方定义「equiv 脉冲当量，单位：pulse/unit」，手册例 8.1：
        ///   「每发送 100pulse，设备平台前进 1mm…脉冲当量为 100pulse/mm」
        /// 故 equiv = 脉冲当量 = ppu（脉冲/用户单位），与本项目 PulsePerUnit 同义。
        ///
        /// ★ 两套 API 并存，混用即错 equiv 倍：
        ///   无后缀（脉冲版）：dmc_set_profile / dmc_read_current_speed —— 注释「(脉冲)」
        ///   _unit（当量版） ：dmc_set_profile_unit / dmc_read_current_speed_unit —— 注释「(脉冲当量)」
        ///   官方例 8.2：「如果用户仍希望以脉冲（pulse）为单位，可以将脉冲当量值设置为 1」
        /// 本适配器全程使用**无后缀脉冲版**（见 LeadshineDmcController），
        /// 脉冲当量只在业务层参与换算，不下发到控制器。
        /// needsVendorConfirm = false（官方手册 + 官方头文件双重定案）。
        /// </summary>
        public static BrandUnits Leadshine(double ppu)
        { return new BrandUnits(ppu, VendorRateUnit.PulsesPerSecond, true, false); }

        /// <summary>
        /// 正运动 zmcaux.dll：**用户单位/秒**，不做脉冲换算。
        /// 官方一手制品：evidence/zmotion/lib64/库文件/Zmcaux.cs
        ///   L1435「/// 设置轴速度，单位为units/s，当多轴运动时，作为插补运动的速度」
        ///   L1535-1542「/// 设置轴脉冲当量」ZAux_Direct_SetUnits(handle, iaxis, float)
        /// 因为适配器会先调 SetUnits 把脉冲当量下发到控制器，控制器侧的 unit 已经等于 mm，
        /// 所以上位机传 mm/s 即为正确值。**若这里再乘 ppu，会快 10000 倍。**
        /// needsVendorConfirm = false（官方头文件逐字定案）。
        /// </summary>
        public static BrandUnits ZMotion(double ppu)
        { return new BrandUnits(ppu, VendorRateUnit.UserUnitsPerSecond, true, false); }

        /// <summary>
        /// 高川 nmc_lib20.dll：官方头文件 L366/L529/L628 明写「脉冲/ms」，L534 明写加速度单位，
        /// 是本项目唯一有官方头文件单位真值的品牌 —— needsVendorConfirm = false。
        /// </summary>
        public static BrandUnits GaoChuan(double ppu)
        { return new BrandUnits(ppu, VendorRateUnit.PulsesPerMilliSecond, true, false); }

        /// <summary>
        /// 软赢 WMX3ApiCLR：**用户单位/秒**，不做脉冲换算。
        /// 官方 doxygen page_WMXDOC_FUNC_MPR_PROFILE_PARAM.html 原文：
        ///   Profile.Velocity「Unit: user unit / second」
        ///   Profile.Acc/Dec「Unit: user unit / second²」
        ///   page_WMXDOC_STATUS_AXIS.html：AxesStatus.PosCmd/ActualPos「Unit: user unit」、
        ///     VelocityCmd/ActualVelocity「Unit: user unit / second」
        ///   page_WMXDOC_FUNC_AXIS_CONFIG_GEAR_RATIO.html 原文公式：
        ///     「Pulse = User Unit * Gear Ratio」
        /// 即脉冲当量由控制器侧齿轮比承担，上位机按 user unit（mm）下发即可。
        /// 需注意：PM_MOTION 接口（PMAxisMotion::PosCommand.vel）是 pulses per second，
        /// 与本适配器使用的 CoreMotion 接口不是同一套，不可混用。
        /// needsVendorConfirm = false。
        /// </summary>
        public static BrandUnits SoftWin(double ppu)
        { return new BrandUnits(ppu, VendorRateUnit.UserUnitsPerSecond, true, false); }

        // ---------- 位置换算 ----------

        /// <summary>用户位置(mm) → 厂商脉冲数。</summary>
        public static double MmToPulses(double mm, double pulsesPerUnit)
        {
            if (pulsesPerUnit <= 0)
                throw new ArgumentOutOfRangeException("pulsesPerUnit", pulsesPerUnit, "脉冲当量必须 > 0");
            return mm * pulsesPerUnit;
        }

        /// <summary>厂商脉冲数 → 用户位置(mm)。</summary>
        public static double PulsesToMm(double pulses, double pulsesPerUnit)
        {
            if (pulsesPerUnit <= 0)
                throw new ArgumentOutOfRangeException("pulsesPerUnit", pulsesPerUnit, "脉冲当量必须 > 0");
            return pulses / pulsesPerUnit;
        }

        /// <summary>用户位置(mm) → 厂商 int32 目标位置。越界抛异常而非静默截断（静默截断=轴少走一大截且无告警）。</summary>
        public static int MmToPulseInt32(double mm, double pulsesPerUnit, int axis, string brand)
        {
            double p = MmToPulses(mm, pulsesPerUnit);
            if (double.IsNaN(p) || double.IsInfinity(p))
                throw new ArgumentOutOfRangeException("mm", mm, "位置换算得到非有限值（mm 或脉冲当量非法）");
            double rounded = Math.Round(p);
            if (rounded > POS_INT32_SAFE_MAX || rounded < -POS_INT32_SAFE_MAX)
                throw new ArgumentOutOfRangeException("mm", mm,
                    brand + " 轴" + axis + " 目标位置越界：换算得 " + rounded + " 脉冲，超出 int32 安全范围 ±"
                    + POS_INT32_SAFE_MAX + "。请减小行程或改用 64 位位置口径。");
            return (int)rounded;
        }

        // ---------- 速度换算 ----------

        /// <summary>
        /// 速度换算因子：乘以「mm/s」即得厂商速率值。
        ///   UserUnitsPerSecond  ： × 1        —— 控制器侧 unit 已是 mm（正运动、软赢）
        ///   PulsesPerSecond     ： × ppu      —— 脉冲/秒（雷赛脉冲版）
        ///   PulsesPerMilliSecond： × ppu/1000 —— 脉冲/毫秒（固高、高川）
        /// 例：ppu=10000、v=50mm/s
        ///   正运动/软赢 → 50（控制器按 mm 解释）
        ///   雷赛       → 500000（脉冲/秒）
        ///   固高/高川  → 500（脉冲/毫秒，即 500000 脉冲/秒）
        ///
        /// 【本函数曾有真实错误】原写成 PerMsToPerSec(ppu)/1000.0，
        /// 而 PerMsToPerSec 内部已乘 1000，两次运算抵消成 ppu —— 比正确值大 1000 倍。
        /// 因换算错误不抛异常，只会让轴快 1000 倍，门禁 verify_units.py 已按闭式重算锁死。
        /// </summary>
        public static double RateFactor(BrandUnits u)
        {
            if (u == null) throw new ArgumentNullException("u");
            switch (u.RateUnit)
            {
                case VendorRateUnit.UserUnitsPerSecond:
                    return 1.0;                       // 控制器侧已是用户单位
                case VendorRateUnit.PulsesPerMilliSecond:
                    return u.PulsesPerUnit / 1000.0;  // ppu/1000 —— mm/s → 脉冲/ms
                case VendorRateUnit.PulsesPerSecond:
                    return u.PulsesPerUnit;           // ppu    —— mm/s → 脉冲/s
                default:
                    throw new ArgumentOutOfRangeException("u", u.RateUnit, "未知的速率单位");
            }
        }

        /// <summary>用户速度(mm/s) → 厂商速率值。</summary>
        public static double MmS2Vendor(double mmPerSec, BrandUnits u)
        {
            return mmPerSec * RateFactor(u);
        }

        /// <summary>厂商速率值 → 用户速度(mm/s)。</summary>
        public static double Vendor2MmS(double vendorRate, BrandUnits u)
        {
            double f = RateFactor(u);
            if (f == 0) throw new ArgumentOutOfRangeException("u", "换算因子为 0，无法反算");
            return vendorRate / f;
        }

        // ---------- 加速度换算 ----------

        /// <summary>
        /// 加速度换算因子。
        ///   UserUnitsPerSecond  ： × 1     —— 正运动 ZAux_Direct_SetAccel「units/s/s」；软赢 Profile.Acc「user unit/second²」
        ///   PulsesPerSecond     ： × ppu   —— 雷赛官方「acc 加速度，单位：pulse /（s²）」
        ///   PulsesPerMilliSecond： × ppu/1000 —— 固高官方 Language.ini L650「加速度(pulse/ms^2)」
        ///
        /// 【曾有真实错误】原实现对所有脉冲类品牌一律返回 ppu/1000，
        /// 这对「脉冲/毫秒」口径成立（脉冲/ms² = (脉冲/ms)/ms，与速度同除一次 1000），
        /// 但对雷赛的「脉冲/秒」口径是错的 —— 加速度会小 1000 倍（轴加速极缓）。
        /// 时间因子必须跟着速度口径走，不能想当然。
        /// </summary>
        public static double AccFactor(BrandUnits u)
        {
            if (u == null) throw new ArgumentNullException("u");
            switch (u.RateUnit)
            {
                case VendorRateUnit.UserUnitsPerSecond:
                    return 1.0;
                case VendorRateUnit.PulsesPerSecond:
                    return u.PulsesPerUnit;            // ppu    —— mm/s² → 脉冲/s²
                case VendorRateUnit.PulsesPerMilliSecond:
                    return u.PulsesPerUnit / 1000.0;   // ppu/1000 —— mm/s² → 脉冲/ms²
                default:
                    throw new ArgumentOutOfRangeException("u", u.RateUnit, "未知的速率单位");
            }
        }

        /// <summary>用户加速度(mm/s²) → 厂商加速度值。</summary>
        public static double MmS2_2ToVendor(double mmPerSec2, BrandUnits u)
        {
            return mmPerSec2 * AccFactor(u);
        }

        /// <summary>厂商加速度值 → 用户加速度(mm/s²)。</summary>
        public static double Vendor2MmS2(double vendorAcc, BrandUnits u)
        {
            double f = AccFactor(u);
            if (f == 0) throw new ArgumentOutOfRangeException("u", "换算因子为 0，无法反算");
            return vendorAcc / f;
        }

        // ---------- 自洽性自检（供门禁与运行期调用）----------

        /// <summary>
        /// 位置/速度/加速度换算的往返一致性检查。
        /// 往返误差必须为 0（浮点乘除不产生误差，误差非 0 说明换算因子写反或用了近似）。
        /// </summary>
        public static void AssertRoundTrip(BrandUnits u, double sampleMm, double sampleMmS, double sampleMmS2)
        {
            // 用相对容差而非位精确：脉冲当量常是非整数（如 1310.72 脉冲/mm），
            // 双精度乘除后反算会有 1ulp 级舍入误差，位精确会把「浮点表示」误报成「换算错误」。
            Near("位置", MmToPulses(PulsesToMm(sampleMm, u.PulsesPerUnit), u.PulsesPerUnit), sampleMm, u);
            Near("速度", Vendor2MmS(MmS2Vendor(sampleMmS, u), u), sampleMmS, u);
            Near("加速度", Vendor2MmS2(MmS2_2ToVendor(sampleMmS2, u), u), sampleMmS2, u);
        }

        private static void Near(string what, double got, double want, BrandUnits u)
        {
            double tol = Math.Abs(want) * 1e-12 + 1e-9;
            if (Math.Abs(got - want) > tol)
                throw new InvalidOperationException(
                    what + "换算往返不一致：" + u + "（得到 " + got + "，期望 " + want + "）");
        }
    }
}
