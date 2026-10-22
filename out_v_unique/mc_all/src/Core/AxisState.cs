using System;
namespace MotionControl.Core
{
    /// <summary>统一轴状态位。各品牌状态寄存器位定义在适配层完成映射（JD职责9: 偶发定位偏差的统一诊断入口）。</summary>
    [Flags]
    public enum AxisState
    {
        None = 0,
        Enabled = 1,
        Moving = 2,
        InPosition = 4,
        Alarm = 8,
        Homing = 16,
        Homed = 32,
        LimitNeg = 64,
        LimitPos = 128,
        Emergency = 256,

        /// <summary>状态位映射尚未经真机标定：本返回值用的是启发式默认位，不可作为判据。
        /// 厂商适配器在首板标定前会置上此位，避免业务层把「猜的位」当成真值用。</summary>
        Uncalibrated = 512
    }
}
