using System;
using MotionControl.Core;
using MotionControl.Virtual;
using MotionControl.Adapters.Leadshine;
using MotionControl.Adapters.Googol;
using MotionControl.Adapters.ZMotion;
using MotionControl.Adapters.SoftWin;
using MotionControl.Adapters.GaoChuan;

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
                case Brand.GoogolGts: return new MotionControl.Adapters.Googol.GoogolGtsController();
                case Brand.ZMotion: return new MotionControl.Adapters.ZMotion.ZMotionZmcController();
                case Brand.SoftWin: return new MotionControl.Adapters.SoftWin.SoftWinController();
                case Brand.GaoChuan: return new MotionControl.Adapters.GaoChuan.GaoChuanController();
                default: throw new NotSupportedException("Brand not registered: " + brand);
            }
        }
    }
}
