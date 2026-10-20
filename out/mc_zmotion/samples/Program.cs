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
    /// 生成时间: 2026-10-20 08:04:23
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

                // 3. SPC 统计（JD职责8: 数据采集 SPC 统计）
                SpcReport(c);

                Console.WriteLine("RESULT: PASS - 流程完成，报告见 glue_report.csv");
                return 0;
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
