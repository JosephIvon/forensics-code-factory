using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using MotionControl.Core;

namespace Samples
{
    /// <summary>
    /// 点胶工艺示例（JD职责6: 路径规划/出胶时序/缺胶溢胶判定 + 职责8: SPC统计）
    /// 换卡演示: 只修改 CREATE_BRAND 一行，业务流程零改动（JD职责3 目标）。
    /// 生成时间: 2026-10-22 12:10:22
    /// </summary>
    public static class Program
    {
        // ================== 换卡只改这一行 ==================
        const Brand CREATE_BRAND = Brand.Virtual;
        // Brand.Leadshine / Brand.GoogolGts / Brand.ZMotion ...
        // [注意] 真实品牌的 P/Invoke 路径只做过编译级验证，未经运行验证；
        //        切换前须按 docs/ApiChecklist.md 对照厂商手册核对签名。
        //        Brand.Acs 需专用 ACS 套件 + 官方 ACS.SPiiPlusNET.dll（不在全品牌套件内）。
        // ====================================================

        static int Main(string[] args)
        {
            Console.WriteLine("========================================================");
            Console.WriteLine(" 上位机工艺流程 Demo | brand=" + CREATE_BRAND);
            Console.WriteLine("========================================================");

            IMotionController c = ControllerFactory.Create(CREATE_BRAND);
            MotionResult r = c.Open(new ControllerConnectInfo());
            Check(r, "Open");

            try
            {
                // 1. 使能 + 回零（任职要求5: 回零与限位策略）
                int[] axes = new int[] { 0, 1, 2 };   // X Y Z
                string[] names = new string[] { "X", "Y", "Z" };
                foreach (int ax in axes)
                {
                    AxisConfig ac = new AxisConfig();
                    ac.AxisIndex = ax; ac.Name = names[Array.IndexOf(axes, ax)];
                    ac.PulsesPerUnit = 10000; ac.SoftLimitNeg = -500; ac.SoftLimitPos = 500;
                    ac.Home.Mode = HomeMode.HomeSwitchIndex; ac.Home.Offset = 0;
                    Check(c.ConfigureAxis(ac), "ConfigureAxis " + ac.Name);
                }
                foreach (int ax in axes) Check(c.Enable(ax, true), "Enable axis " + ax);
                foreach (int ax in axes) Check(c.Home(ax), "Home axis " + ax);
                foreach (int ax in axes) Check(c.WaitHomeDone(ax, 30000), "WaitHomeDone " + ax);
                Console.WriteLine("[POS] X={0:F3} Y={1:F3} Z={2:F3}", c.GetCommandPos(0), c.GetCommandPos(1), c.GetCommandPos(2));

                // 2. 工艺流程
                RunProcess(c);

                // 2.5 行为断言自检（接缝3 共享评测集 BEH-01/03/04/07/09/14/15）
                BehaviorSelfCheck(c);

                // 3. SPC 统计（JD职责8: 数据采集 SPC 统计）
                SpcReport(c);

                Console.WriteLine("RESULT: PASS - 流程完成，报告见 glue_report.csv");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("RESULT: FAIL - " + ex.Message);
                return 1;
            }
            finally
            {
                foreach (int ax in new int[] { 0, 1, 2 }) c.Stop(ax, false);
                c.Close();
            }
        }

        static void RunProcess(IMotionController c)
        {
            MoveParams fast = new MoveParams(); fast.Vel = 80; fast.Acc = 800;
            MoveParams work = new MoveParams(); work.Vel = 25; work.Acc = 300; work.JerkRatio = 0.3;
            const int GLUE_DO = 0;      // 出胶阀 DO
            const double Z_SAFE = 5.0;  // 安全高度

            Check(c.MoveTo(2, Z_SAFE, fast, MoveMode.Absolute), "Z -> safe");

            // ---- 路径1: 矩形三点走胶（IO 时序 = 出胶时序，JD职责6）----
            double[,] rect = new double[,] { { 20, 10 }, { 80, 10 }, { 80, 60 }, { 20, 60 } };
            Check(c.MoveLine(new int[] { 0, 1 }, new double[] { rect[0, 0], rect[0, 1] }, fast, MoveMode.Absolute), "MoveLine to start");
            Check(c.WaitMoveDone(0, 10000), "wait arrive");
            Check(c.MoveTo(2, 0.5, work, MoveMode.Absolute), "Z -> glue height (点胶高度跟随)");
            Check(c.WaitMoveDone(2, 10000), "wait Z");
            for (int i = 1; i < rect.GetLength(0); i++)
            {
                Check(c.SetDo(GLUE_DO, true), "glue ON");
                Check(c.MoveLine(new int[] { 0, 1 }, new double[] { rect[i, 0], rect[i, 1] }, work, MoveMode.Absolute), "glue path seg " + i);
                Check(c.WaitMoveDone(0, 30000), "wait seg " + i);
                Check(c.SetDo(GLUE_DO, false), "glue OFF");
            }

            // ---- 路径2: 圆弧转角（JD职责4: 圆弧插补）----
            Check(c.MoveTo(2, Z_SAFE, fast, MoveMode.Absolute), "Z -> safe");
            Check(c.MoveArc2D(0, 1, 15.0, 0.0, 50.0, 80.0, false, work), "arc");
            Check(c.WaitMoveDone(0, 30000), "wait arc");

            // ---- IO 互锁等待（吸嘴/治具到位信号，JD职责5 时序）----
            Check(c.WaitDi(0, true, 15000), "wait fixture ready DI0");

            // ---- 力控压合演示（JD职责4: 力控压合 -> 模拟量闭环粗调）----
            Check(c.SetAo(0, 1.25), "force setpoint 1.25V");
            double fbForce = c.GetAi(0);
            Console.WriteLine("[FORCE] setpoint=1.25V feedback={0:F3}V", fbForce);
            Check(c.SetAo(0, 0.0), "force release");

            // ---- [接缝3·BEH-08] 参数守卫自检：越界轴号/端口必须显式拒绝，不许沉默 ----
            MotionResult badMove = c.MoveTo(99, 1.0, fast, MoveMode.Absolute);
            if (badMove.Ok) throw new Exception("FAIL at axis-guard: 越界轴号 MoveTo(99) 被沉默接受");
            Console.WriteLine("[SELFCHECK] MoveTo(99) -> " + badMove);
            bool threw = false;
            try { c.GetCommandPos(99); } catch (ArgumentOutOfRangeException) { threw = true; }
            if (!threw) throw new Exception("FAIL at axis-guard: 读数方法越界未抛 ArgumentOutOfRangeException");
            MotionResult badDo = c.SetDo(999, true);
            if (badDo.Ok) throw new Exception("FAIL at axis-guard: 越界 DO 端口 SetDo(999) 被沉默接受");
            MotionResult badAo = c.SetAo(99, 1.0);
            if (badAo.Ok) throw new Exception("FAIL at axis-guard: 越界 AO 端口 SetAo(99) 被沉默接受");
            Console.WriteLine("[SELFCHECK] axis/port guard OK —— 越界轴号与端口全部显式拒绝 (BEH-08)");
        }

        // ---- [接缝3] 行为断言自检：shared_eval.json BEH-01/03/04/07/09/14/15 ----
        static void BehaviorSelfCheck(IMotionController c)
        {
            MoveParams bv = new MoveParams(); bv.Vel = 50; bv.Acc = 500;

            // [BEH-07] 非法脉冲当量必须显式拒绝（错误码风格）
            AxisConfig badScale = new AxisConfig();
            badScale.AxisIndex = 7; badScale.PulsesPerUnit = 0;
            MotionResult rScale = c.ConfigureAxis(badScale);
            if (rScale.Ok) throw new Exception("FAIL at BEH-07: PulsesPerUnit=0 被沉默接受");
            Console.WriteLine("[BEH-07] PulsesPerUnit=0 -> " + rScale + " 拒绝 OK");

            // [BEH-04] 单位口径：到位准确且指令/反馈同口径
            Check(c.MoveTo(0, 10.0, bv, MoveMode.Absolute), "BEH-04 move");
            Check(c.WaitMoveDone(0, 10000), "BEH-04 wait");
            double cmd = c.GetCommandPos(0), fb = c.GetFeedbackPos(0);
            if (Math.Abs(cmd - 10.0) >= 0.001) throw new Exception("FAIL at BEH-04: 指令位置应为 10.0，实际 " + cmd.ToString("F6"));
            if (Math.Abs(cmd - fb) > 0.05) throw new Exception("FAIL at BEH-04: 指令/反馈口径不一致 cmd=" + cmd.ToString("F6") + " fb=" + fb.ToString("F6"));
            Console.WriteLine("[BEH-04] cmd={0:F3} fb={1:F6} 口径一致 OK", cmd, fb);

            // [BEH-14] 相对定位增量正确：10 + rel(5) = 15
            Check(c.MoveTo(0, 5.0, bv, MoveMode.Relative), "BEH-14 move rel");
            Check(c.WaitMoveDone(0, 10000), "BEH-14 wait");
            double p15 = c.GetCommandPos(0);
            if (Math.Abs(p15 - 15.0) >= 0.001) throw new Exception("FAIL at BEH-14: 10+5 应为 15.0，实际 " + p15.ToString("F6"));
            Console.WriteLine("[BEH-14] 10 + rel(5) = {0:F3} OK", p15);

            // [BEH-01] 跟随误差必须非零（抓假反馈）：运动中持续采样
            Check(c.MoveTo(0, 60.0, bv, MoveMode.Absolute), "BEH-01 move far");
            double maxErr = 0.0;
            while ((c.GetState(0) & AxisState.Moving) == AxisState.Moving)
            {
                double e = Math.Abs(c.GetCommandPos(0) - c.GetFeedbackPos(0));
                if (e > maxErr) maxErr = e;
            }
            if (maxErr < 0.0001) throw new Exception("FAIL at BEH-01: 运动中跟随误差恒为 0——反馈是假数据（指令副本）");
            Console.WriteLine("[BEH-01] 运动中最大跟随误差 = {0:F6} 非零 OK", maxErr);
            Check(c.WaitMoveDone(0, 10000), "BEH-01 settle");

            // [BEH-03] 点动方向必须与速度符号一致
            double j0 = c.GetCommandPos(0);
            Check(c.MoveJog(0, 10.0, bv), "BEH-03 jog+");
            Check(c.WaitMoveDone(0, 5000), "BEH-03 jog+ wait");
            double j1 = c.GetCommandPos(0);
            if (j1 <= j0) throw new Exception("FAIL at BEH-03: 正速度点动后位置必须增加 " + j0.ToString("F3") + " -> " + j1.ToString("F3"));
            Check(c.MoveJog(0, -10.0, bv), "BEH-03 jog-");
            Check(c.WaitMoveDone(0, 5000), "BEH-03 jog- wait");
            double j2 = c.GetCommandPos(0);
            if (j2 >= j1) throw new Exception("FAIL at BEH-03: 负速度点动后位置必须减少 " + j1.ToString("F3") + " -> " + j2.ToString("F3"));
            Console.WriteLine("[BEH-03] jog +10 -> {0:F3}，-10 -> {1:F3} 方向一致 OK", j1, j2);

            // [BEH-09] 停止后不再处于运动中
            Check(c.MoveTo(0, 400.0, bv, MoveMode.Absolute), "BEH-09 move far");
            Check(c.Stop(0, true), "BEH-09 emergency stop");
            if ((c.GetState(0) & AxisState.Moving) == AxisState.Moving)
                throw new Exception("FAIL at BEH-09: 紧急停止后仍处于运动中");
            Console.WriteLine("[BEH-09] 紧急停止后 Moving=0 OK");

            // [BEH-15] 回零后位置归零（回零是工件坐标的基准）
            Check(c.Home(0), "BEH-15 home");
            Check(c.WaitHomeDone(0, 30000), "BEH-15 home wait");
            double h = c.GetCommandPos(0);
            if (Math.Abs(h) >= 0.001) throw new Exception("FAIL at BEH-15: 回零后位置应为 0，实际 " + h.ToString("F6"));
            Console.WriteLine("[BEH-15] 回零后位置 = {0:F6} 归零 OK", h);
        }

        static void SpcReport(IMotionController c)
        {
            // 连续采样压力通道，计算 Cpk（缺胶/溢胶判定 = 规格限判定，JD职责6/8）
            List<double> x = new List<double>();
            for (int i = 0; i < 32; i++) { x.Add(c.GetAi(0)); Thread.Sleep(25); }
            double mean = 0; foreach (double v in x) mean += v; mean /= x.Count;
            double var = 0; foreach (double v in x) var += (v - mean) * (v - mean); var /= (x.Count - 1);
            double sigma = Math.Sqrt(var);
            double usl = 3.0, lsl = 2.0;
            double cpk = sigma > 1e-9 ? Math.Min(usl - mean, mean - lsl) / (3 * sigma) : 99.0;
            double cp = sigma > 1e-9 ? (usl - lsl) / (6 * sigma) : 99.0;
            Console.WriteLine("[SPC] n={0} mean={1:F4} sigma={2:F4} Cp={3:F2} Cpk={4:F2}", x.Count, mean, sigma, cp, cpk);
            Console.WriteLine("[SPC] 判定: {0}", cpk >= 1.33 ? "合格 (Cpk>=1.33)" : "需调整工艺参数");

            using (StreamWriter w = new StreamWriter("glue_report.csv"))
            {
                w.WriteLine("sample,pressure_V");
                for (int i = 0; i < x.Count; i++)
                    w.WriteLine(i.ToString() + "," + x[i].ToString("F5", CultureInfo.InvariantCulture));
                w.WriteLine("mean," + mean.ToString("F5", CultureInfo.InvariantCulture));
                w.WriteLine("sigma," + sigma.ToString("F5", CultureInfo.InvariantCulture));
                w.WriteLine("cpk," + cpk.ToString("F3", CultureInfo.InvariantCulture));
            }
        }

        static void Check(MotionResult r, string tag)
        {
            Console.WriteLine("  " + r + "  <" + tag + ">");
            if (!r.Ok) throw new Exception("FAIL at " + tag + ": " + r);
        }
    }
}
