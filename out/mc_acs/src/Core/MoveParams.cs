namespace MotionControl.Core
{
    public enum MoveMode { Absolute, Relative }

    /// <summary>运动参数（用户单位）。JerkRatio=0 为梯形曲线，>0 映射为各品牌 S 曲线平滑因子（JD职责4: S曲线加减速与轨迹平滑）。</summary>
    public class MoveParams
    {
        public double Vel = 50;      // mm/s
        public double Acc = 500;     // mm/s^2
        public double Dec = 500;
        public double JerkRatio = 0.2;
        public static MoveParams Default() { return new MoveParams(); }
    }
}
