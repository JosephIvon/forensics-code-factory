// =====================================================================
//  ModbusTcpClient - 自实现 Modbus TCP 主站（零依赖, JD职责8 设备接口）
//  生成工具: 上位机代码工厂 v1.0 | 2026-10-22 12:10:22
//  支持: FC01/02/03/04 读, FC06/16 写。同步Socket, 适配 WinForm/WPF 线程模型。
// =====================================================================
using System;
using System.Net.Sockets;

namespace MotionControl.Modbus
{
    public class ModbusTcpClient : IDisposable
    {
        private Socket _sock;
        private ushort _txnId;
        private readonly string _ip;
        private readonly int _port;
        private readonly byte _slaveId;
        private readonly object _io = new object();

        public ModbusTcpClient(string ip, int port, byte slaveId)
        {
            _ip = ip; _port = port; _slaveId = slaveId;
        }

        public bool IsOpen { get { return _sock != null && _sock.Connected; } }

        public void Open(int timeoutMs)
        {
            lock (_io)
            {
                Close();
                _sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _sock.SendTimeout = timeoutMs;
                _sock.ReceiveTimeout = timeoutMs;
                _sock.Connect(_ip, _port);
            }
        }

        public void Close()
        {
            lock (_io)
            {
                if (_sock != null) { try { _sock.Close(); } catch { } _sock = null; }
            }
        }

        public void Dispose() { Close(); }

        /// <summary>读保持/输入寄存器(FC3/FC4)。addr为寄存器地址, quantity为字数。</summary>
        public ushort[] ReadRegisters(byte fc, ushort addr, ushort quantity)
        {
            byte[] pdu = new byte[5];
            pdu[0] = fc;
            pdu[1] = (byte)(addr >> 8); pdu[2] = (byte)(addr & 0xFF);
            pdu[3] = (byte)(quantity >> 8); pdu[4] = (byte)(quantity & 0xFF);
            byte[] resp = Transaction(pdu);
            if (resp == null || resp.Length < 3 + quantity * 2) throw new Exception("Modbus 读响应长度异常");
            if (resp[1] == (byte)(fc + 0x80)) throw new Exception("Modbus 异常码: " + resp[2]);
            ushort[] regs = new ushort[quantity];
            for (int i = 0; i < quantity; i++)
                regs[i] = (ushort)((resp[3 + i * 2] << 8) | resp[4 + i * 2]);
            return regs;
        }

        /// <summary>写单寄存器(FC06)</summary>
        public void WriteSingleRegister(ushort addr, ushort value)
        {
            byte[] pdu = new byte[5];
            pdu[0] = 6;
            pdu[1] = (byte)(addr >> 8); pdu[2] = (byte)(addr & 0xFF);
            pdu[3] = (byte)(value >> 8); pdu[4] = (byte)(value & 0xFF);
            byte[] resp = Transaction(pdu);
            if (resp == null || resp.Length < 5) throw new Exception("Modbus 写响应长度异常");
            if (resp[1] == 0x80 + 6) throw new Exception("Modbus 异常码: " + resp[2]);
        }

        /// <summary>写多寄存器(FC16)</summary>
        public void WriteRegisters(ushort addr, ushort[] values)
        {
            byte[] pdu = new byte[6 + values.Length * 2];
            pdu[0] = 16;
            pdu[1] = (byte)(addr >> 8); pdu[2] = (byte)(addr & 0xFF);
            pdu[3] = (byte)(values.Length >> 8); pdu[4] = (byte)(values.Length & 0xFF);
            pdu[5] = (byte)(values.Length * 2);
            for (int i = 0; i < values.Length; i++)
            {
                pdu[6 + i * 2] = (byte)(values[i] >> 8); pdu[7 + i * 2] = (byte)(values[i] & 0xFF);
            }
            byte[] resp = Transaction(pdu);
            if (resp == null || resp.Length < 5) throw new Exception("Modbus 写响应长度异常");
            if (resp[1] == 0x80 + 16) throw new Exception("Modbus 异常码: " + resp[2]);
        }

        /// <summary>读线圈/离散量(FC01/FC02)</summary>
        public bool[] ReadBits(byte fc, ushort addr, ushort quantity)
        {
            byte[] pdu = new byte[5];
            pdu[0] = fc;
            pdu[1] = (byte)(addr >> 8); pdu[2] = (byte)(addr & 0xFF);
            pdu[3] = (byte)(quantity >> 8); pdu[4] = (byte)(quantity & 0xFF);
            byte[] resp = Transaction(pdu);
            if (resp == null || resp.Length < 3) throw new Exception("Modbus 位读响应异常");
            if (resp[1] == (byte)(fc + 0x80)) throw new Exception("Modbus 异常码: " + resp[2]);
            bool[] bits = new bool[quantity];
            for (int i = 0; i < quantity; i++)
            {
                int byi = 3 + i / 8;
                if (byi < resp.Length) bits[i] = ((resp[byi] >> (i % 8)) & 1) != 0;
            }
            return bits;
        }

        // MBAP 帧事务
        private byte[] Transaction(byte[] pdu)
        {
            lock (_io)
            {
                if (_sock == null || !_sock.Connected) throw new Exception("Modbus TCP 未连接");
                _txnId++;
                byte[] frame = new byte[7 + pdu.Length];
                frame[0] = (byte)(_txnId >> 8); frame[1] = (byte)(_txnId & 0xFF);
                frame[2] = 0; frame[3] = 0;
                frame[4] = 0; frame[5] = (byte)pdu.Length;
                frame[6] = _slaveId;
                Array.Copy(pdu, 0, frame, 7, pdu.Length);
                _sock.Send(frame);
                byte[] head = ReadExactly(7);
                int len = (head[4] << 8) | head[5];
                byte[] rest = ReadExactly(len - 1);
                byte[] respPdu = new byte[1 + rest.Length];
                respPdu[0] = head[6];
                Array.Copy(rest, 0, respPdu, 1, rest.Length);
                return respPdu;
            }
        }

        private byte[] ReadExactly(int n)
        {
            byte[] buf = new byte[n];
            int got = 0;
            while (got < n)
            {
                int k = _sock.Receive(buf, got, n - got, SocketFlags.None);
                if (k <= 0) throw new Exception("Modbus TCP 连接中断");
                got += k;
            }
            return buf;
        }
    }
}
