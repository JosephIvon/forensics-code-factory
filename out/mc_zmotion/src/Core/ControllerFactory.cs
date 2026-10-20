using System;
using MotionControl.Core;
using MotionControl.Virtual;
using MotionControl.Adapters.ZMotion;

namespace MotionControl.Core
{
    /// <summary>工厂：业务层唯一的品牌耦合点。换卡 = 改调用处一个枚举值（JD职责3）。</summary>
    public static class ControllerFactory
    {
        public static IMotionController Create(Brand brand)
        {
            switch (brand)
            {
                case Brand.Virtual: return new VirtualController();
                case Brand.ZMotion: return new MotionControl.Adapters.ZMotion.ZMotionZmcController();
                default: throw new NotSupportedException("Brand not registered: " + brand);
            }
        }
    }
}
