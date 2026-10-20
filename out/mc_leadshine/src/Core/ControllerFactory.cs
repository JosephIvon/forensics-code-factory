using System;
using MotionControl.Core;
using MotionControl.Virtual;
using MotionControl.Adapters.Leadshine;

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
                case Brand.Leadshine: return new MotionControl.Adapters.Leadshine.LeadshineDmcController();
                default: throw new NotSupportedException("Brand not registered: " + brand);
            }
        }
    }
}
