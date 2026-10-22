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
        Emergency = 256
    }
}
