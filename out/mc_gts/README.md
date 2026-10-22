# MotionControlKit - 固高 GTS/GTN 系列 上位机工程包

> 生成工具: **上位机代码工厂 v1.0** | 时间: 2026-10-20 12:43:08 | 轴数: 4 | 工艺: dispensing

## 这是什么

按「分层架构 + 设备抽象层」生成的可编译 C# 工程（JD 职责2/3 的落地）：

```
src/Core/       抽象层: IMotionController 接口/配置/结果对象/工厂  <- 业务唯一依赖
src/Adapters/   适配层: 固高 GTS/GTN 系列 (P/Invoke 封装, 单位换算, 线程锁)
src/Virtual/    虚拟控制器: 无硬件全流程仿真/单元测试
samples/        工艺流程示例(换卡只改一行)
docs/           能力差异矩阵 + API 核对清单
build/          csc(零安装) 与 dotnet 两套构建脚本
```

## 30 秒跑起来（无需任何硬件/安装）

```
双击 build\build_net48.bat     # Windows 自带编译器, 编译并运行虚拟控制器 Demo
```

## 换卡 = 改一行

`samples/Program.cs`:

```csharp
const Brand CREATE_BRAND = Brand.Virtual;   // 换成 Brand.Leadshine / Brand.GoogolGts ...
```

## 上真机流程

1. 阅读 docs/ApiChecklist.md（先看头部"同源声明"）：以厂商官方手册为唯一依据逐条比对签名
2. 厂商 Demo 手动验证轴动作
3. 低速运行 GlueDemo，逐步提速
4. 按 docs/CapabilityMatrix.md 评估该卡是否支持位置比较/飞拍/力控等进阶能力

## 工程规范（JD职责10）

- 所有适配器错误码统一映射到 MotionResult
- SDK 调用统一持锁（`lock(_sdk)`）串行化
- 单位换算集中在适配层，业务层只用用户单位
- VirtualController 用于单元测试与 CI 回归
