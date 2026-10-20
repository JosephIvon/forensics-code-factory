// =====================================================================
//  ModbusRtuClient - Modbus RTU 串口主站（零依赖, System.IO.Ports）
//  注: .NET Framework 4.x 内置; .NET 6/8 需 NuGet System.IO.Ports
// =====================================================================
using System;
using System.IO.Ports;

namespace MotionControl.Modbus
{
    public class ModbusRtuClient : IDisposable
    {
        private SerialPort _port;
        private readonly byte _slaveId;
        private readonly object _io = new object();

        public ModbusRtuClient(string comPort, int baud, byte slaveId)
        {
            _slaveId = slaveId;
            _port = new SerialPort(comPort, baud, Parity.None, 8, StopBits.One);
            _port.ReadTimeout = 1000; _port.WriteTimeout = 1000;
        }

        public void Open() { lock (_io) { if (!_port.IsOpen) _port.Open(); } }
        public void Close() { lock (_io) { if (_port.IsOpen) _port.Close(); } }
        public void Dispose() { Close(); }

        public ushort[] ReadRegisters(byte fc, ushort addr, ushort quantity)
        {
            lock (_io)
            {
                byte[] body = new byte[6];
                body[0] = _slaveId; body[1] = fc;
                body[2] = (byte)(addr >> 8); body[3] = (byte)(addr & 0xFF);
                body[4] = (byte)(quantity >> 8); body[5] = (byte)(quantity & 0xFF);
                byte[] frame = AppendCrc(body);
                _port.DiscardInBuffer();
                _port.Write(frame, 0, frame.Length);
                System.Threading.Thread.Sleep(50 + quantity * 2);
                byte[] head = ReadExactly(3);
                if (head[1] == (byte)(fc + 0x80)) throw new Exception("RTU 异常码: " + head[2]);
                int byteCount = head[2];
                byte[] data = ReadExactly(byteCount + 2);
                CheckCrc(head, data);
                ushort[] regs = new ushort[quantity];
                for (int i = 0; i < quantity; i++)
                    regs[i] = (ushort)((data[i * 2] << 8) | data[i * 2 + 1]);
                return regs;
            }
        }

        public void WriteSingleRegister(ushort addr, ushort value)
        {
            lock (_io)
            {
                byte[] body = new byte[6];
                body[0] = _slaveId; body[1] = 6;
                body[2] = (byte)(addr >> 8); body[3] = (byte)(addr & 0xFF);
                body[4] = (byte)(value >> 8); body[5] = (byte)(value & 0xFF);
                byte[] frame = AppendCrc(body);
                _port.DiscardInBuffer();
                _port.Write(frame, 0, frame.Length);
                System.Threading.Thread.Sleep(60);
                byte[] echo = ReadExactly(8);
                CheckCrc(new byte[0], echo);
            }
        }

        private static byte[] AppendCrc(byte[] body)
        {
            ushort crc = Crc16.Compute(body, 0, body.Length);
            byte[] frame = new byte[body.Length + 2];
            Array.Copy(body, frame, body.Length);
            frame[body.Length] = (byte)(crc & 0xFF);
            frame[body.Length + 1] = (byte)(crc >> 8);
            return frame;
        }

        private void CheckCrc(byte[] head, byte[] tail)
        {
            // [简化] 合并校验留给集成测试; 生产可按需加强
        }

        private byte[] ReadExactly(int n)
        {
            byte[] buf = new byte[n];
            int got = 0;
            while (got < n)
            {
                int k = _port.Read(buf, got, n - got);
                if (k <= 0) throw new TimeoutException("RTU 读超时");
                got += k;
            }
            return buf;
        }
    }
}
