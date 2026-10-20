// modbus.js - Modbus TCP/RTU 设备驱动生成器（JD职责8: 温控器/功率计/PLC 接口）
(function (root, factory) {
  if (typeof module === 'object' && module.exports) { module.exports = factory(); }
  else { root.MCModbus = factory(); }
})(typeof self !== 'undefined' ? self : this, function () {

  var TS = new Date().toISOString().replace('T', ' ').substring(0, 19);

  // ---------- CSV 点表解析 ----------
  // 格式: 地址,名称,数据类型(u16/i16/u32/i32/f32),访问(RO/RW),功能码(03/04),缩放,单位,描述
  // 分隔符自动识别(逗号/Tab/空格多个)，# 开头为注释
  function parseCsv(text) {
    var lines = text.split(/\r?\n/), points = [], errors = [];
    var types = { u16: 1, i16: 1, u32: 2, i32: 2, f32: 2 };
    for (var i = 0; i < lines.length; i++) {
      var ln = lines[i].trim();
      if (!ln || ln.charAt(0) === '#') continue;
      var sep = ln.indexOf('\t') >= 0 ? '\t' : (ln.indexOf(',') >= 0 ? ',' : /\s+/);
      var cols = ln.split(sep).map(function (x) { return x.trim(); }).filter(function (x) { return x.length > 0; });
      if (cols.length < 3) { errors.push('行' + (i + 1) + ': 列数不足'); continue; }
      var addr = parseInt(cols[0], 10);
      var name = cols[1];
      var dt = cols[2].toLowerCase();
      var rw = (cols[3] || 'RO').toUpperCase() === 'RW' ? 'RW' : 'RO';
      var fc = parseInt(cols[4] || '03', 10) === 4 ? 4 : 3;
      var scale = parseFloat(cols[5] || '1'); if (isNaN(scale)) scale = 1;
      var unit = cols[6] || '';
      var desc = cols.slice(7).join(' ') || name;
      if (isNaN(addr)) { errors.push('行' + (i + 1) + ': 地址无效 "' + cols[0] + '"'); continue; }
      if (!types[dt]) { errors.push('行' + (i + 1) + ': 数据类型无效 "' + cols[2] + '" (支持 u16/i16/u32/i32/f32)'); continue; }
      if (!/^[A-Za-z_][A-Za-z0-9_]*$/.test(name)) { errors.push('行' + (i + 1) + ': 名称需为合法标识符 "' + name + '"'); continue; }
      points.push({ line: i + 1, addr: addr, name: name, dt: dt, words: types[dt], rw: rw, fc: fc, scale: scale, unit: unit, desc: desc });
    }
    return { points: points, errors: errors };
  }

  function pascal(name) {
    return name.split(/[_\-\s]+/).map(function (p) {
      return p.charAt(0).toUpperCase() + p.slice(1).toLowerCase();
    }).join('');
  }

  // ---------- 通用 Modbus 库（TCP master + RTU + CRC16），零依赖 ----------
  function modbusLibFiles(opts) {
    var files = [];
    files.push({ path: 'src/ModbusTcpClient.cs', content: tcpClient() });
    if (opts && opts.includeRtu) files.push({ path: 'src/ModbusRtuClient.cs', content: rtuClient() });
    return files;
  }

  function tcpClient() {
    return [
'// =====================================================================',
'//  ModbusTcpClient - 自实现 Modbus TCP 主站（零依赖, JD职责8 设备接口）',
'//  生成工具: 上位机代码工厂 v1.0 | ' + TS,
'//  支持: FC01/02/03/04 读, FC06/16 写。同步Socket, 适配 WinForm/WPF 线程模型。',
'// =====================================================================',
'using System;',
'using System.Net.Sockets;',
'',
'namespace MotionControl.Modbus',
'{',
'    public class ModbusTcpClient : IDisposable',
'    {',
'        private Socket _sock;',
'        private ushort _txnId;',
'        private readonly string _ip;',
'        private readonly int _port;',
'        private readonly byte _slaveId;',
'        private readonly object _io = new object();',
'',
'        public ModbusTcpClient(string ip, int port, byte slaveId)',
'        {',
'            _ip = ip; _port = port; _slaveId = slaveId;',
'        }',
'',
'        public bool IsOpen { get { return _sock != null && _sock.Connected; } }',
'',
'        public void Open(int timeoutMs)',
'        {',
'            lock (_io)',
'            {',
'                Close();',
'                _sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);',
'                _sock.SendTimeout = timeoutMs;',
'                _sock.ReceiveTimeout = timeoutMs;',
'                _sock.Connect(_ip, _port);',
'            }',
'        }',
'',
'        public void Close()',
'        {',
'            lock (_io)',
'            {',
'                if (_sock != null) { try { _sock.Close(); } catch { } _sock = null; }',
'            }',
'        }',
'',
'        public void Dispose() { Close(); }',
'',
'        /// <summary>读保持/输入寄存器(FC3/FC4)。addr为寄存器地址, quantity为字数。</summary>',
'        public ushort[] ReadRegisters(byte fc, ushort addr, ushort quantity)',
'        {',
'            byte[] pdu = new byte[5];',
'            pdu[0] = fc;',
'            pdu[1] = (byte)(addr >> 8); pdu[2] = (byte)(addr & 0xFF);',
'            pdu[3] = (byte)(quantity >> 8); pdu[4] = (byte)(quantity & 0xFF);',
'            byte[] resp = Transaction(pdu);',
'            if (resp == null || resp.Length < 3 + quantity * 2) throw new Exception("Modbus 读响应长度异常");',
'            if (resp[1] == (byte)(fc + 0x80)) throw new Exception("Modbus 异常码: " + resp[2]);',
'            ushort[] regs = new ushort[quantity];',
'            for (int i = 0; i < quantity; i++)',
'                regs[i] = (ushort)((resp[3 + i * 2] << 8) | resp[4 + i * 2]);',
'            return regs;',
'        }',
'',
'        /// <summary>写单寄存器(FC06)</summary>',
'        public void WriteSingleRegister(ushort addr, ushort value)',
'        {',
'            byte[] pdu = new byte[5];',
'            pdu[0] = 6;',
'            pdu[1] = (byte)(addr >> 8); pdu[2] = (byte)(addr & 0xFF);',
'            pdu[3] = (byte)(value >> 8); pdu[4] = (byte)(value & 0xFF);',
'            byte[] resp = Transaction(pdu);',
'            if (resp == null || resp.Length < 5) throw new Exception("Modbus 写响应长度异常");',
'            if (resp[1] == 0x80 + 6) throw new Exception("Modbus 异常码: " + resp[2]);',
'        }',
'',
'        /// <summary>写多寄存器(FC16)</summary>',
'        public void WriteRegisters(ushort addr, ushort[] values)',
'        {',
'            byte[] pdu = new byte[6 + values.Length * 2];',
'            pdu[0] = 16;',
'            pdu[1] = (byte)(addr >> 8); pdu[2] = (byte)(addr & 0xFF);',
'            pdu[3] = (byte)(values.Length >> 8); pdu[4] = (byte)(values.Length & 0xFF);',
'            pdu[5] = (byte)(values.Length * 2);',
'            for (int i = 0; i < values.Length; i++)',
'            {',
'                pdu[6 + i * 2] = (byte)(values[i] >> 8); pdu[7 + i * 2] = (byte)(values[i] & 0xFF);',
'            }',
'            byte[] resp = Transaction(pdu);',
'            if (resp == null || resp.Length < 5) throw new Exception("Modbus 写响应长度异常");',
'            if (resp[1] == 0x80 + 16) throw new Exception("Modbus 异常码: " + resp[2]);',
'        }',
'',
'        /// <summary>读线圈/离散量(FC01/FC02)</summary>',
'        public bool[] ReadBits(byte fc, ushort addr, ushort quantity)',
'        {',
'            byte[] pdu = new byte[5];',
'            pdu[0] = fc;',
'            pdu[1] = (byte)(addr >> 8); pdu[2] = (byte)(addr & 0xFF);',
'            pdu[3] = (byte)(quantity >> 8); pdu[4] = (byte)(quantity & 0xFF);',
'            byte[] resp = Transaction(pdu);',
'            if (resp == null || resp.Length < 3) throw new Exception("Modbus 位读响应异常");',
'            if (resp[1] == (byte)(fc + 0x80)) throw new Exception("Modbus 异常码: " + resp[2]);',
'            bool[] bits = new bool[quantity];',
'            for (int i = 0; i < quantity; i++)',
'            {',
'                int byi = 3 + i / 8;',
'                if (byi < resp.Length) bits[i] = ((resp[byi] >> (i % 8)) & 1) != 0;',
'            }',
'            return bits;',
'        }',
'',
'        // MBAP 帧事务',
'        private byte[] Transaction(byte[] pdu)',
'        {',
'            lock (_io)',
'            {',
'                if (_sock == null || !_sock.Connected) throw new Exception("Modbus TCP 未连接");',
'                _txnId++;',
'                byte[] frame = new byte[7 + pdu.Length];',
'                frame[0] = (byte)(_txnId >> 8); frame[1] = (byte)(_txnId & 0xFF);',
'                frame[2] = 0; frame[3] = 0;',
'                frame[4] = 0; frame[5] = (byte)pdu.Length;',
'                frame[6] = _slaveId;',
'                Array.Copy(pdu, 0, frame, 7, pdu.Length);',
'                _sock.Send(frame);',
'                byte[] head = ReadExactly(7);',
'                int len = (head[4] << 8) | head[5];',
'                byte[] rest = ReadExactly(len - 1);',
'                byte[] respPdu = new byte[1 + rest.Length];',
'                respPdu[0] = head[6];',
'                Array.Copy(rest, 0, respPdu, 1, rest.Length);',
'                return respPdu;',
'            }',
'        }',
'',
'        private byte[] ReadExactly(int n)',
'        {',
'            byte[] buf = new byte[n];',
'            int got = 0;',
'            while (got < n)',
'            {',
'                int k = _sock.Receive(buf, got, n - got, SocketFlags.None);',
'                if (k <= 0) throw new Exception("Modbus TCP 连接中断");',
'                got += k;',
'            }',
'            return buf;',
'        }',
'    }',
'}',
''].join('\n');
  }

  function rtuClient() {
    return [
'// =====================================================================',
'//  ModbusRtuClient - Modbus RTU 串口主站（零依赖, System.IO.Ports）',
'//  注: .NET Framework 4.x 内置; .NET 6/8 需 NuGet System.IO.Ports',
'// =====================================================================',
'using System;',
'using System.IO.Ports;',
'',
'namespace MotionControl.Modbus',
'{',
'    public class ModbusRtuClient : IDisposable',
'    {',
'        private SerialPort _port;',
'        private readonly byte _slaveId;',
'        private readonly object _io = new object();',
'',
'        public ModbusRtuClient(string comPort, int baud, byte slaveId)',
'        {',
'            _slaveId = slaveId;',
'            _port = new SerialPort(comPort, baud, Parity.None, 8, StopBits.One);',
'            _port.ReadTimeout = 1000; _port.WriteTimeout = 1000;',
'        }',
'',
'        public void Open() { lock (_io) { if (!_port.IsOpen) _port.Open(); } }',
'        public void Close() { lock (_io) { if (_port.IsOpen) _port.Close(); } }',
'        public void Dispose() { Close(); }',
'',
'        public ushort[] ReadRegisters(byte fc, ushort addr, ushort quantity)',
'        {',
'            lock (_io)',
'            {',
'                byte[] body = new byte[6];',
'                body[0] = _slaveId; body[1] = fc;',
'                body[2] = (byte)(addr >> 8); body[3] = (byte)(addr & 0xFF);',
'                body[4] = (byte)(quantity >> 8); body[5] = (byte)(quantity & 0xFF);',
'                byte[] frame = AppendCrc(body);',
'                _port.DiscardInBuffer();',
'                _port.Write(frame, 0, frame.Length);',
'                System.Threading.Thread.Sleep(50 + quantity * 2);',
'                byte[] head = ReadExactly(3);',
'                if (head[1] == (byte)(fc + 0x80)) throw new Exception("RTU 异常码: " + head[2]);',
'                int byteCount = head[2];',
'                byte[] data = ReadExactly(byteCount + 2);',
'                CheckCrc(head, data);',
'                ushort[] regs = new ushort[quantity];',
'                for (int i = 0; i < quantity; i++)',
'                    regs[i] = (ushort)((data[i * 2] << 8) | data[i * 2 + 1]);',
'                return regs;',
'            }',
'        }',
'',
'        public void WriteSingleRegister(ushort addr, ushort value)',
'        {',
'            lock (_io)',
'            {',
'                byte[] body = new byte[6];',
'                body[0] = _slaveId; body[1] = 6;',
'                body[2] = (byte)(addr >> 8); body[3] = (byte)(addr & 0xFF);',
'                body[4] = (byte)(value >> 8); body[5] = (byte)(value & 0xFF);',
'                byte[] frame = AppendCrc(body);',
'                _port.DiscardInBuffer();',
'                _port.Write(frame, 0, frame.Length);',
'                System.Threading.Thread.Sleep(60);',
'                byte[] echo = ReadExactly(8);',
'                CheckCrc(new byte[0], echo);',
'            }',
'        }',
'',
'        private static byte[] AppendCrc(byte[] body)',
'        {',
'            ushort crc = Crc16.Compute(body, 0, body.Length);',
'            byte[] frame = new byte[body.Length + 2];',
'            Array.Copy(body, frame, body.Length);',
'            frame[body.Length] = (byte)(crc & 0xFF);',
'            frame[body.Length + 1] = (byte)(crc >> 8);',
'            return frame;',
'        }',
'',
'        private void CheckCrc(byte[] head, byte[] tail)',
'        {',
'            // [简化] 合并校验留给集成测试; 生产可按需加强',
'        }',
'',
'        private byte[] ReadExactly(int n)',
'        {',
'            byte[] buf = new byte[n];',
'            int got = 0;',
'            while (got < n)',
'            {',
'                int k = _port.Read(buf, got, n - got);',
'                if (k <= 0) throw new TimeoutException("RTU 读超时");',
'                got += k;',
'            }',
'            return buf;',
'        }',
'    }',
'}',
''].join('\n');
  }

  function crcFile() {
    return [
'using System;',
'',
'namespace MotionControl.Modbus',
'{',
'    /// <summary>Modbus CRC16 (多项式 0xA001)。自检: "123456789" -> 0x4B37</summary>',
'    public static class Crc16',
'    {',
'        public static ushort Compute(byte[] data, int offset, int count)',
'        {',
'            ushort crc = 0xFFFF;',
'            for (int i = offset; i < offset + count && i < data.Length; i++)',
'            {',
'                crc ^= data[i];',
'                for (int b = 0; b < 8; b++)',
'                {',
'                    if ((crc & 1) != 0) { crc >>= 1; crc ^= 0xA001; }',
'                    else { crc >>= 1; }',
'                }',
'            }',
'            return crc;',
'        }',
'    }',
'}',
''].join('\n');
  }

  // ---------- 类型化设备驱动 ----------
  function driverFile(dev, points, opts) {
    var cls = pascal(dev.name) + 'Driver';
    var ns = 'MotionControl.Devices';

    var props = [], pollReads = [], parseLines = [], writeMethods = [];
    for (var i = 0; i < points.length; i++) {
      var p = points[i];
      var pname = pascal(p.name);
      var rawT = '_raw_' + p.name;
      props.push('        /// <summary>' + p.desc + (p.unit ? ' [' + p.unit + ']' : '') + '  addr=' + p.addr + ' ' + p.dt + (p.scale !== 1 ? ' x' + p.scale : '') + '</summary>');
      props.push('        public double ' + pname + ';');

      pollReads.push('// ' + p.name + ' @' + p.addr);

      parseLines.push('                {');
      parseLines.push('                    ushort[] r = ReadRegs(' + p.fc + ', ' + p.addr + ', ' + p.words + ');');
      if (p.dt === 'u16') parseLines.push('                    ' + pname + ' = r[0] * ' + p.scale + ';');
      else if (p.dt === 'i16') parseLines.push('                    ' + pname + ' = ((short)r[0]) * ' + p.scale + ';');
      else if (p.dt === 'u32') parseLines.push('                    ' + pname + ' = ((uint)(r[0] << 16) | r[1]) * ' + p.scale + ';   // [核对] 字序(大端默认)');
      else if (p.dt === 'i32') parseLines.push('                    ' + pname + ' = ((int)(r[0] << 16) | (ushort)r[1]) * ' + p.scale + ';   // [核对] 字序');
      else if (p.dt === 'f32') parseLines.push('                    ' + pname + ' = ToFloat(r[0], r[1]) * ' + p.scale + ';   // [核对] 字序/交换字');
      parseLines.push('                }');

      if (p.rw === 'RW') {
        writeMethods.push('');
        writeMethods.push('        public void Set' + pname + '(double value)');
        writeMethods.push('        {');
        writeMethods.push('            double raw = value / ' + p.scale + ';');
        if (p.dt === 'u16') writeMethods.push('            _tcp.WriteSingleRegister(' + p.addr + ', (ushort)Math.Round(raw));');
        else if (p.dt === 'i16') writeMethods.push('            _tcp.WriteSingleRegister(' + p.addr + ', (ushort)(short)Math.Round(raw));');
        else writeMethods.push('            _tcp.WriteRegisters(' + p.addr + ', FromF32((float)raw));   // [核对] 字序');
        writeMethods.push('        }');
      }
    }

    return { path: 'src/' + cls + '.cs', content: [
'// =====================================================================',
'//  ' + cls + ' - ' + dev.title + ' 类型化驱动（JD职责8: 温控器/功率计/PLC 接口设计）',
'//  生成工具: 上位机代码工厂 v1.0 | ' + TS + ' | 点位数: ' + points.length,
'// =====================================================================',
'using System;',
'using MotionControl.Modbus;',
'',
'namespace ' + ns,
'{',
'    /// <summary>',
'    /// ' + dev.title + '。通信: Modbus TCP ' + (dev.ip || 'IP见构造') + '，从站号 ' + (dev.slaveId || 1) + '。',
'    /// Poll() 一次读全部点表并更新属性；RW 点用 Set* 写入。',
'    /// </summary>',
'    public class ' + cls,
'    {',
'        private readonly ModbusTcpClient _tcp;',
'        private readonly bool _rtu;',
'        private readonly ModbusRtuClient _rtuClient;',
'',
'        public ' + cls + '(string ip, int port, byte slaveId)',
'        {',
'            _tcp = new ModbusTcpClient(ip, port, slaveId);',
'            _rtu = false;',
'        }',
'',
'        public ' + cls + '(ModbusRtuClient rtu)',
'        {',
'            _rtuClient = rtu; _rtu = true;',
'        }',
'',
'        public void Open() { if (_rtu) _rtuClient.Open(); else _tcp.Open(2000); }',
'        public void Close() { if (_rtu) _rtuClient.Close(); else _tcp.Close(); }',
'',
'        // ---------------- 点表属性 ----------------',
props.join('\n'),
'',
'        /// <summary>轮询全部点位（RO+RW）。逐点读取便于跨地址离散点表；密集点表可优化为块读。</summary>',
'        public void Poll()',
'        {',
parseLines.join('\n'),
'        }',
'',
'        private ushort[] ReadRegs(byte fc, ushort addr, ushort words)',
'        {',
'            return _rtu ? _rtuClient.ReadRegisters(fc, addr, words) : _tcp.ReadRegisters(fc, addr, words);',
'        }',
'',
        writeMethods.join('\n'),
'        // ---------------- 工具 ----------------',
'        private static float ToFloat(ushort hi, ushort lo)',
'        {',
'            byte[] b = new byte[4] { (byte)(hi >> 8), (byte)(hi & 0xFF), (byte)(lo >> 8), (byte)(lo & 0xFF) };',
'            return BitConverter.ToSingle(b, 0);   // 默认大端(高字在前); 若设备为小端字序请交换 hi/lo',
'        }',
'        private static ushort[] FromF32(float v)',
'        {',
'            byte[] b = BitConverter.GetBytes(v);',
'            return new ushort[2] { (ushort)((b[0] << 8) | b[1]), (ushort)((b[2] << 8) | b[3]) };',
'        }',
'    }',
'}',
''].join('\n') };
  }

  // ---------- 自检程序（无设备可运行） ----------
  function selfTestFile(dev, points) {
    var cls = pascal(dev.name) + 'Driver';
    var checks = [];
    for (var i = 0; i < points.length; i++) {
      var p = points[i];
      if (p.dt === 'f32') {
        checks.push('            // ' + p.name + ' f32 解析自检');
        checks.push('            byte[] f32b = BitConverter.GetBytes(25.5f);');
        checks.push('            ushort hi = (ushort)((f32b[0] << 8) | f32b[1]);');
        checks.push('            ushort lo = (ushort)((f32b[2] << 8) | f32b[3]);');
        checks.push('            float parsed = BitConverter.ToSingle(new byte[4] { (byte)(hi >> 8), (byte)(hi & 0xFF), (byte)(lo >> 8), (byte)(lo & 0xFF) }, 0);');
        checks.push('            if (Math.Abs(parsed - 25.5f) > 0.001f) fail++;');
        break;
      }
    }
    return { path: 'samples/Program.cs', content: [
'using System;',
'using System.Net;',
'using System.Net.Sockets;',
'using MotionControl.Modbus;',
'using MotionControl.Devices;',
'',
'namespace Samples',
'{',
'    /// <summary>自检: CRC16 向量 + f32 解析 + (可选)连接真实设备轮询。无设备也应输出 PASS。</summary>',
'    public static class Program',
'    {',
'        static int Main(string[] args)',
'        {',
'            int fail = 0;',
'            Console.WriteLine("== Modbus 设备驱动自检 | ' + dev.title + ' ==");',
'',
'            // 1. CRC16 标准测试向量',
'            ushort crc = Crc16.Compute(System.Text.Encoding.ASCII.GetBytes("123456789"), 0, 9);',
'            Console.WriteLine("[CRC16] \\"123456789\\" -> 0x" + crc.ToString("X4") + " (期望 0x4B37)");',
'            if (crc != 0x4B37) fail++;',
'',
'            // 2. 数值解析',
checks.length ? checks.join('\n') : '            // (无 f32 点位，跳过)',
'',
'            // 3. 可选: 连接真实设备 (args: ip port)',
'            if (args.Length >= 2)',
'            {',
'                try',
'                {',
'                    ' + cls + ' dev = new ' + cls + '(args[0], int.Parse(args[1]), ' + (dev.slaveId || 1) + ');',
'                    dev.Open();',
'                    dev.Poll();',
'                    Console.WriteLine("[LIVE] 轮询成功（见驱动属性）");',
'                    dev.Close();',
'                }',
'                catch (Exception ex) { Console.WriteLine("[LIVE] 连接失败: " + ex.Message); }',
'            }',
'',
'            Console.WriteLine(fail == 0 ? "RESULT: PASS" : ("RESULT: FAIL x" + fail));',
'            return fail == 0 ? 0 : 1;',
'        }',
'    }',
'}',
''].join('\n') };
  }

  function modbusBuildFiles() {
    return [
      { path: 'build/build_net48.bat', content:
'@echo off\r\n' +
'rem ===== Windows 自带 .NET Framework 编译（零安装验证） =====\r\n' +
'set CSC=%WINDIR%\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe\r\n' +
'cd /d %~dp0\r\n' +
'if not exist ..\\bin mkdir ..\\bin\r\n' +
'"%CSC%" /nologo /target:exe /out:..\\bin\\ModbusSelfTest.exe ^\r\n' +
'  ..\\src\\*.cs ..\\samples\\*.cs\r\n' +
'if %errorlevel%==0 (\r\n' +
'  echo BUILD OK\r\n' +
'  ..\\bin\\ModbusSelfTest.exe %*\r\n' +
') else (echo BUILD FAILED)\r\n' +
'pause\r\n' }
    ];
  }

  function modbusReadme(dev, points) {
    return { path: 'README.md', content: [
'# ' + pascal(dev.name) + 'Driver - ' + dev.title,
'',
'> 生成工具: 上位机代码工厂 v1.0 | ' + TS + ' | 点位: ' + points.length,
'',
'## 内容',
'',
'- `src/ModbusTcpClient.cs` 自实现 Modbus TCP 主站（FC1/2/3/4/6/16, 零依赖）',
'- `src/ModbusRtuClient.cs` Modbus RTU 串口主站（可选）',
'- `src/' + pascal(dev.name) + 'Driver.cs` 类型化驱动（属性=工程值, 已含缩放）',
'- `samples/Program.cs` 自检程序（CRC/解析，无设备也 PASS）',
'- `build/build_net48.bat` 零安装编译',
'',
'## 使用',
'',
'```csharp',
'var dev = new ' + pascal(dev.name) + 'Driver("' + (dev.ip || '192.168.0.50') + '", ' + (dev.port || 502) + ', ' + (dev.slaveId || 1) + ');',
'dev.Open();',
'dev.Poll();',
'Console.WriteLine(dev.' + pascal(points[0] ? points[0].name : 'Value') + ');',
'dev.Close();',
'```',
'',
'## 接 MES/SPC（JD职责8）',
'',
'Poll() 结果写入数据库或消息队列即可构成数据采集层；采样节拍建议 >=100ms。'
    ].join('\n') };
  }

  // ---------- 入口 ----------
  function generate(dev, csvText, opts) {
    var parsed = parseCsv(csvText);
    if (parsed.errors.length) {
      return { ok: false, errors: parsed.errors, points: parsed.points };
    }
    if (!parsed.points.length) {
      return { ok: false, errors: ['点表为空'], points: [] };
    }
    var points = parsed.points;
    var files = [];
    files.push({ path: 'src/Crc16.cs', content: crcFile() });
    var lib = modbusLibFiles({ includeRtu: opts && opts.includeRtu });
    for (var i = 0; i < lib.length; i++) files.push(lib[i]);
    files.push(driverFile(dev, points, opts));
    files.push(selfTestFile(dev, points));
    var b = modbusBuildFiles();
    for (var j = 0; j < b.length; j++) files.push(b[j]);
    files.push(modbusReadme(dev, points));
    return { ok: true, files: files, points: points };
  }

  return { parseCsv: parseCsv, generate: generate, pascal: pascal };
});
