using System;
namespace MotionControl.Core
{
    /// <summary>回零策略（JD任职要求5: 回零与限位策略）。适配层负责映射到各品牌回零模式字。</summary>
    public enum HomeMode { None, LimitReverse, HomeSwitch, HomeSwitchIndex, IndexOnly }

    public class HomeConfig
    {
        public HomeMode Mode = HomeMode.HomeSwitchIndex;
        public double VelFast = 20.0;   // mm/s 快找
        public double VelSlow = 2.0;    // mm/s 慢找
        public double Acc = 200.0;      // mm/s^2
        public double Offset = 0.0;     // 回零后偏置（用户单位）
        public int TimeoutMs = 30000;
    }

    /// <summary>轴配置。所有位置统一为用户单位（默认 mm），脉冲换算封装在适配层。</summary>
    public class AxisConfig
    {
        public int AxisIndex;                  // 0-based（统一约定，适配层内换算为各品牌轴号）
        public string Name = "Axis";
        public double PulsesPerUnit = 10000;   // 脉冲/mm（脉冲型卡的关键换算参数）
        public double SoftLimitNeg = -100000;  // 软限位（用户单位）
        public double SoftLimitPos = 100000;
        public HomeConfig Home = new HomeConfig();
        public override string ToString() { return Name + "[" + AxisIndex + "]"; }
    }
}
