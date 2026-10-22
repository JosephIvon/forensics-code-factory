// =====================================================================
//  TempControllerDriver - 温控器（示例点表） 类型化驱动（JD职责8: 温控器/功率计/PLC 接口设计）
//  生成工具: 上位机代码工厂 v1.0 | 2026-10-22 12:10:22 | 点位数: 5
// =====================================================================
using System;
using MotionControl.Modbus;

namespace MotionControl.Devices
{
    /// <summary>
    /// 温控器（示例点表）。通信: Modbus TCP 192.168.0.50，从站号 1。
    /// Poll() 一次读全部点表并更新属性；RW 点用 Set* 写入。
    /// </summary>
    public class TempControllerDriver
    {
        private readonly ModbusTcpClient _tcp;
        private readonly bool _rtu;
        private readonly ModbusRtuClient _rtuClient;

        public TempControllerDriver(string ip, int port, byte slaveId)
        {
            _tcp = new ModbusTcpClient(ip, port, slaveId);
            _rtu = false;
        }

        public TempControllerDriver(ModbusRtuClient rtu)
        {
            _rtuClient = rtu; _rtu = true;
        }

        public void Open() { if (_rtu) _rtuClient.Open(); else _tcp.Open(2000); }
        public void Close() { if (_rtu) _rtuClient.Close(); else _tcp.Close(); }

        // ---------------- 点表属性 ----------------
        /// <summary>当前温度(过程值) [C]  addr=100 f32 x0.1</summary>
        public double Pv;
        /// <summary>设定温度 [C]  addr=200 f32 x0.1</summary>
        public double Sv;
        /// <summary>RUN [运行/停止]  addr=210 u16</summary>
        public double Run;
        /// <summary>ALARM [报警码]  addr=220 u16</summary>
        public double Alarm;
        /// <summary>加热功率百分比 [%]  addr=230 u16 x0.1</summary>
        public double HeatPower;

        /// <summary>轮询全部点位（RO+RW）。逐点读取便于跨地址离散点表；密集点表可优化为块读。</summary>
        public void Poll()
        {
                {
                    ushort[] r = ReadRegs(4, 100, 2);
                    Pv = ToFloat(r[0], r[1]) * 0.1;   // [核对] 字序/交换字
                }
                {
                    ushort[] r = ReadRegs(3, 200, 2);
                    Sv = ToFloat(r[0], r[1]) * 0.1;   // [核对] 字序/交换字
                }
                {
                    ushort[] r = ReadRegs(3, 210, 1);
                    Run = r[0] * 1;
                }
                {
                    ushort[] r = ReadRegs(4, 220, 1);
                    Alarm = r[0] * 1;
                }
                {
                    ushort[] r = ReadRegs(4, 230, 1);
                    HeatPower = r[0] * 0.1;
                }
        }

        private ushort[] ReadRegs(byte fc, ushort addr, ushort words)
        {
            return _rtu ? _rtuClient.ReadRegisters(fc, addr, words) : _tcp.ReadRegisters(fc, addr, words);
        }


        public void SetSv(double value)
        {
            double raw = value / 0.1;
            _tcp.WriteRegisters(200, FromF32((float)raw));   // [核对] 字序
        }

        public void SetRun(double value)
        {
            double raw = value / 1;
            _tcp.WriteSingleRegister(210, (ushort)Math.Round(raw));
        }
        // ---------------- 工具 ----------------
        private static float ToFloat(ushort hi, ushort lo)
        {
            byte[] b = new byte[4] { (byte)(hi >> 8), (byte)(hi & 0xFF), (byte)(lo >> 8), (byte)(lo & 0xFF) };
            return BitConverter.ToSingle(b, 0);   // 默认大端(高字在前); 若设备为小端字序请交换 hi/lo
        }
        private static ushort[] FromF32(float v)
        {
            byte[] b = BitConverter.GetBytes(v);
            return new ushort[2] { (ushort)((b[0] << 8) | b[1]), (ushort)((b[2] << 8) | b[3]) };
        }
    }
}
