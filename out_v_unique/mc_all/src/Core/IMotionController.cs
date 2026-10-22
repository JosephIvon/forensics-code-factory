using System;
namespace MotionControl.Core
{
    /// <summary>
    /// 统一运动控制接口 —— 上层业务（流程调度引擎/工艺模块）唯一依赖。
    /// JD 职责映射:
    ///   MoveTo/MoveJog/Stop      -> 职责4 高速点位
    ///   MoveLine/MoveArc2D       -> 职责4 直线/圆弧插补、耦合运动
    ///   SetAo/GetAi              -> 职责4 力控压合 / 职责5 贴装压力 / 职责6 胶量控制
    ///   Home(策略可配)            -> 任职要求5 回零与限位策略
    ///   GetFollowingError        -> 任职要求5 跟随误差补偿数据源
    ///   SetDo/GetDi/WaitDi       -> 职责5 吸嘴拾取时序 / 职责6 出胶时序
    /// 接口为"启动+查询"语义（非阻塞）；Wait* 提供阻塞等待。异步/状态机调度在业务层实现（职责2）。
    /// </summary>
    public interface IMotionController : IDisposable
    {
        Brand Brand { get; }
        string DeviceName { get; }
        bool IsOpen { get; }

        /// <summary>
        /// 本品牌是否支持 S 曲线平滑（false ⇒ <see cref="MoveParams.JerkRatio"/> 被忽略）。
        ///
        /// 存在的理由：D7 核实发现雷赛/正运动/软赢此前**静默丢弃** JerkRatio ——
        /// 调用方设了值却毫无效果，且不崩不抛、无任何告警。
        /// 「明确不支持」远比「悄悄不生效」安全：调用方可以据此降级工艺或改用梯形曲线。
        /// </summary>
        bool SupportsJerkProfile { get; }

        MotionResult Open(ControllerConnectInfo info);
        MotionResult Close();
        MotionResult ConfigureAxis(AxisConfig cfg);

        MotionResult Enable(int axis, bool on);
        AxisState GetState(int axis);
        double GetCommandPos(int axis);      // 用户单位
        double GetFeedbackPos(int axis);     // 用户单位（无编码器时返回指令位置）
        double GetFollowingError(int axis);  // 用户单位

        MotionResult Home(int axis);
        bool IsHomed(int axis);
        MotionResult WaitHomeDone(int axis, int timeoutMs);

        MotionResult MoveTo(int axis, double posUser, MoveParams p, MoveMode mode);
        MotionResult MoveJog(int axis, double velUserPerSec, MoveParams p);
        MotionResult Stop(int axis, bool emergency);
        MotionResult WaitMoveDone(int axis, int timeoutMs);

        /// <summary>多轴直线插补（各轴同时到达）。axisList 与 target 一一对应。</summary>
        MotionResult MoveLine(int[] axisList, double[] targetUser, MoveParams p, MoveMode mode);
        /// <summary>XY 平面圆弧（IJ 圆心增量格式）。cw=true 顺时针。</summary>
        MotionResult MoveArc2D(int axisX, int axisY, double i, double j, double endX, double endY, bool cw, MoveParams p);

        MotionResult SetDo(int index, bool on);
        bool GetDi(int index);
        double GetAi(int index);             // V
        MotionResult SetAo(int index, double volts);
        MotionResult WaitDi(int index, bool level, int timeoutMs);
    }
}
