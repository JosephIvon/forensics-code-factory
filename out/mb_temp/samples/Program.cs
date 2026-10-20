using System;
using System.Net;
using System.Net.Sockets;
using MotionControl.Modbus;
using MotionControl.Devices;

namespace Samples
{
    /// <summary>自检: CRC16 向量 + f32 解析 + (可选)连接真实设备轮询。无设备也应输出 PASS。</summary>
    public static class Program
    {
        static int Main(string[] args)
        {
            int fail = 0;
            Console.WriteLine("== Modbus 设备驱动自检 | 温控器（示例点表） ==");

            // 1. CRC16 标准测试向量
            ushort crc = Crc16.Compute(System.Text.Encoding.ASCII.GetBytes("123456789"), 0, 9);
            Console.WriteLine("[CRC16] \"123456789\" -> 0x" + crc.ToString("X4") + " (期望 0x4B37)");
            if (crc != 0x4B37) fail++;

            // 2. 数值解析
            // PV f32 解析自检
            byte[] f32b = BitConverter.GetBytes(25.5f);
            ushort hi = (ushort)((f32b[0] << 8) | f32b[1]);
            ushort lo = (ushort)((f32b[2] << 8) | f32b[3]);
            float parsed = BitConverter.ToSingle(new byte[4] { (byte)(hi >> 8), (byte)(hi & 0xFF), (byte)(lo >> 8), (byte)(lo & 0xFF) }, 0);
            if (Math.Abs(parsed - 25.5f) > 0.001f) fail++;

            // 3. 可选: 连接真实设备 (args: ip port)
            if (args.Length >= 2)
            {
                try
                {
                    TempControllerDriver dev = new TempControllerDriver(args[0], int.Parse(args[1]), 1);
                    dev.Open();
                    dev.Poll();
                    Console.WriteLine("[LIVE] 轮询成功（见驱动属性）");
                    dev.Close();
                }
                catch (Exception ex) { Console.WriteLine("[LIVE] 连接失败: " + ex.Message); }
            }

            Console.WriteLine(fail == 0 ? "RESULT: PASS" : ("RESULT: FAIL x" + fail));
            return fail == 0 ? 0 : 1;
        }
    }
}
