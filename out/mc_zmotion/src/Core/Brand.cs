// =====================================================================
//  MotionControl.Core - 设备抽象层（品牌无关）
//  生成工具: 上位机代码工厂 v1.0  |  生成时间: 2026-10-20 08:04:23
//  对应 JD 职责2: "分层结构、模块边界、设备抽象层"
//             职责3: "换卡不改上层业务代码"
//  目标框架: .NET Framework 4.x（C#5 兼容），可无改动迁移 .NET 6/8
// =====================================================================
namespace MotionControl.Core
{
    /// <summary>运动控制品牌。新增品牌 = 加一个枚举值 + 一个适配器 + 工厂注册一行。</summary>
    public enum Brand
    {
        Virtual = 0,      // 虚拟控制器（仿真/单元测试/无硬件演示）
        GoogolGts = 1,    // 固高 GTS/GTN 系列（gts.dll）
        Leadshine = 2,    // 雷赛 DMC/EMC 系列（LTDMC.dll）
        ZMotion = 3,      // 正运动 ZMC 系列（zauxdll.dll）
        Acs = 4,          // ACS SPiiPlus（ACS.SPiiPlusNET 官方.NET库）
        SoftWin = 5,      // 软赢（骨架，见 docs/ApiChecklist.md）
        GaoChuan = 6      // 高川（骨架，见 docs/ApiChecklist.md）
    }
}
