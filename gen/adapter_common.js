// adapter_common.js - 适配器骨架生成器：拼装品牌知识库(kb)为完整 C# 适配器文件
(function (root, factory) {
  if (typeof module === 'object' && module.exports) { module.exports = factory(); }
  else { root.MCAdapterCommon = factory(); }
})(typeof self !== 'undefined' ? self : this, function () {

  var TS = new Date().toISOString().replace('T', ' ').substring(0, 19);

  /**
   * kb 字段:
   *  id,label,dll,ns,cls,device,usings,api[],extraFields,ctorExtra,configureExtra,
   *  pinvoke, axDecl(轴号换算表达式, 参数名 axis),
   *  open,close,enable,state,cmdPos,fbPos,home,homeDoneExpr,moveTo,jog,stop,
   *  isMovingExpr,moveLine,setDo,getDi,getAi,setAo, buildNote
   */
  function buildAdapter(kb) {
    var s = '';
    s += '// =====================================================================\n';
    s += '//  ' + kb.label + ' 适配器（' + kb.dll + '）\n';
    s += '//  生成工具: 上位机代码工厂 v1.0 | ' + TS + '\n';
    s += '//  KB级别: ' + kb.kbLevel + ' —— ' + kb.buildNote + '\n';
    s += '//  上真机前按 docs/ApiChecklist.md 核对签名（标注 [核对] 处）\n';
    s += '// =====================================================================\n';
    s += 'using System;\n';
    s += 'using System.Runtime.InteropServices;\n';
    s += 'using System.Threading;\n';
    s += 'using MotionControl.Core;\n';
    if (kb.usings) s += kb.usings + '\n';
    s += '\nnamespace ' + kb.ns + '\n{\n';
    s += '    /// <summary>\n';
    s += '    /// ' + kb.label + ' —— IMotionController 适配器。\n';
    s += '    /// 设计要点（JD职责3）:\n';
    s += '    ///   1. P/Invoke 封装 + 签名核对标记\n';
    s += '    ///   2. 单位换算: 用户单位(mm) <-脉冲- ' + kb.dll + '\n';
    s += '    ///   3. SDK 调用统一持锁串行化（厂商库多非线程安全）\n';
    s += '    ///   4. 错误码统一映射 MotionResult\n';
    s += '    /// </summary>\n';
    s += '    public class ' + kb.cls + ' : IMotionController\n    {\n';

    s += '        private readonly object _sdk = new object();\n';
    s += '        private ControllerConnectInfo _info;\n';
    s += '        private bool _open;\n';
    s += '        private readonly bool[] _homed = new bool[8];\n';
    s += '        private readonly double[] _ppu = new double[8];         // 脉冲/用户单位\n';
    s += '        private readonly HomeMode[] _homeMode = new HomeMode[8];\n';
    s += '        private readonly double[] _homeVelFast = new double[8];\n';
    s += '        private readonly double[] _homeVelSlow = new double[8];\n';
    s += '        private readonly double[] _homeAcc = new double[8];\n';
    s += '        private readonly bool[] _homing = new bool[8];\n';
    s += '        private readonly int[] _homeStartT = new int[8];\n';
    s += '        private readonly double[] _softNeg = new double[8];\n';
    s += '        private readonly double[] _softPos = new double[8];\n';
    if (kb.extraFields) s += kb.extraFields + '\n';

    s += '\n        public ' + kb.cls + '()\n        {\n';
    s += '            for (int i = 0; i < 8; i++) { _ppu[i] = 10000; _softNeg[i] = -100000; _softPos[i] = 100000; _homeVelFast[i] = 20; _homeVelSlow[i] = 2; _homeAcc[i] = 200; }\n';
    if (kb.ctorExtra) s += kb.ctorExtra + '\n';
    s += '        }\n\n';

    s += '        public Brand Brand { get { return ' + kb.brandEnum + '; } }\n';
    s += '        public string DeviceName { get { return "' + kb.device + '"; } }\n';
    s += '        public bool IsOpen { get { return _open; } }\n';

    s += '\n        #region 厂商 API 导入（[核对] = 签名需按手册确认）\n';
    s += kb.pinvoke + '\n';
    s += '        #endregion\n\n';

    // Open
    s += '        public MotionResult Open(ControllerConnectInfo info)\n';
    s += '        {\n            lock (_sdk)\n            {\n';
    s += '                if (_open) return MotionResult.Success("already open");\n';
    s += '                _info = info;\n' + kb.open + '\n';
    if (!kb.bareOpen) {
      s += '                _open = true;\n                return MotionResult.Success("' + kb.device + ' opened");\n';
    }
    s += '            }\n        }\n\n';

    // Close
    s += '        public MotionResult Close()\n';
    s += '        {\n            lock (_sdk)\n            {\n';
    s += '                if (!_open) return MotionResult.Success();\n';
    s += kb.close + '\n';
    s += '                _open = false;\n                return MotionResult.Success();\n';
    s += '            }\n        }\n\n';
    s += '        public void Dispose() { try { Close(); } catch { } }\n\n';

    // ConfigureAxis
    s += '        public MotionResult ConfigureAxis(AxisConfig cfg)\n';
    s += '        {\n            lock (_sdk)\n            {\n';
    s += '                if (cfg == null) return MotionResult.Fail(-6, "null config");\n';
    s += '                if (cfg.AxisIndex < 0 || cfg.AxisIndex > 7) return MotionResult.Fail(-7, "axis out of range 0-7");\n';
    s += '                int i = cfg.AxisIndex;\n';
    s += '                _ppu[i] = cfg.PulsesPerUnit <= 0 ? 10000 : cfg.PulsesPerUnit;\n';
    s += '                _homeMode[i] = cfg.Home.Mode; _homeVelFast[i] = cfg.Home.VelFast;\n';
    s += '                _homeVelSlow[i] = cfg.Home.VelSlow; _homeAcc[i] = cfg.Home.Acc;\n';
    s += '                _softNeg[i] = cfg.SoftLimitNeg; _softPos[i] = cfg.SoftLimitPos;\n';
    s += '                _homed[i] = false;\n';
    if (kb.configureExtra) s += kb.configureExtra + '\n';
    s += '                return MotionResult.Success(cfg + " configured");\n';
    s += '            }\n        }\n\n';

    // Enable
    s += '        public MotionResult Enable(int axis, bool on)\n';
    s += '        {\n            lock (_sdk)\n            {\n';
    s += '                if (!_open) return MotionResult.Fail(-1, "not open");\n';
    s += '                CheckRange(axis);\n' + kb.enable + '\n';
    s += '            }\n        }\n\n';

    // GetState
    s += '        public AxisState GetState(int axis)\n';
    s += '        {\n            lock (_sdk)\n            {\n';
    s += '                if (!_open) return AxisState.None;\n';
    s += '                CheckRange(axis);\n' + kb.state + '\n';
    s += '            }\n        }\n\n';

    // positions
    s += '        public double GetCommandPos(int axis)\n';
    s += '        {\n            lock (_sdk)\n            {\n';
    s += '                if (!_open) return 0;\n';
    s += '                CheckRange(axis);\n' + kb.cmdPos + '\n';
    s += '            }\n        }\n\n';
    s += '        public double GetFeedbackPos(int axis)\n';
    s += '        {\n            lock (_sdk)\n            {\n';
    s += '                if (!_open) return 0;\n';
    s += '                CheckRange(axis);\n' + kb.fbPos + '\n';
    s += '            }\n        }\n\n';
    s += '        public double GetFollowingError(int axis)\n';
    s += '        {\n            lock (_sdk)\n            {\n';
    s += '                if (!_open) return 0;\n';
    s += '                double cmd = GetCommandPos(axis); double fb = GetFeedbackPos(axis);\n';
    s += '                return cmd - fb;\n';
    s += '            }\n        }\n\n';

    // Home
    s += '        public MotionResult Home(int axis)\n';
    s += '        {\n            lock (_sdk)\n            {\n';
    s += '                if (!_open) return MotionResult.Fail(-1, "not open");\n';
    s += '                CheckRange(axis); _homed[axis] = false; _homing[axis] = true; _homeStartT[axis] = Environment.TickCount;\n' + kb.home + '\n';
    s += '            }\n        }\n\n';
    s += '        public bool IsHomed(int axis) { lock (_sdk) { return axis >= 0 && axis < 8 && _homed[axis]; } }\n\n';
    s += '        public MotionResult WaitHomeDone(int axis, int timeoutMs)\n';
    s += '        {\n';
    s += '            DateTime t0 = DateTime.Now;\n';
    s += '            while ((DateTime.Now - t0).TotalMilliseconds < timeoutMs)\n';
    s += '            {\n';
    s += '                lock (_sdk)\n';
    s += '                {\n';
    s += '                    if (!_open) return MotionResult.Fail(-1, "not open");\n';
    s += '                    if (_homing[axis] && Environment.TickCount - _homeStartT[axis] > 200 && (' + kb.homeDoneExpr + ')) { _homing[axis] = false; _homed[axis] = true; return MotionResult.Success(); }\n';
    s += '                }\n';
    s += '                Thread.Sleep(10);\n';
    s += '            }\n';
    s += '            return MotionResult.Fail(-2, "home timeout axis " + axis);\n';
    s += '        }\n\n';

    // MoveTo
    s += '        public MotionResult MoveTo(int axis, double posUser, MoveParams p, MoveMode mode)\n';
    s += '        {\n            lock (_sdk)\n            {\n';
    s += '                if (!_open) return MotionResult.Fail(-1, "not open");\n';
    s += '                CheckRange(axis);\n';
    s += '                double from = GetCommandPos(axis);\n';
    s += '                double to = (mode == MoveMode.Absolute) ? posUser : from + posUser;\n';
    s += '                if (to < _softNeg[axis] || to > _softPos[axis])\n';
    s += '                    return MotionResult.Fail(-100, "soft limit reject: target " + to.ToString("F3"));\n';
    s += kb.moveTo + '\n';
    s += '            }\n        }\n\n';

    // MoveJog
    s += '        public MotionResult MoveJog(int axis, double velUserPerSec, MoveParams p)\n';
    s += '        {\n            lock (_sdk)\n            {\n';
    s += '                if (!_open) return MotionResult.Fail(-1, "not open");\n';
    s += '                CheckRange(axis);\n' + kb.jog + '\n';
    s += '            }\n        }\n\n';

    // Stop
    s += '        public MotionResult Stop(int axis, bool emergency)\n';
    s += '        {\n            lock (_sdk)\n            {\n';
    s += '                if (!_open) return MotionResult.Fail(-1, "not open");\n';
    s += '                CheckRange(axis);\n' + kb.stop + '\n';
    s += '            }\n        }\n\n';

    // WaitMoveDone
    s += '        public MotionResult WaitMoveDone(int axis, int timeoutMs)\n';
    s += '        {\n';
    s += '            DateTime t0 = DateTime.Now;\n';
    s += '            while ((DateTime.Now - t0).TotalMilliseconds < timeoutMs)\n';
    s += '            {\n';
    s += '                lock (_sdk)\n';
    s += '                {\n';
    s += '                    if (!_open) return MotionResult.Fail(-1, "not open");\n';
    s += '                    if (!(' + kb.isMovingExpr + ')) return MotionResult.Success();\n';
    s += '                }\n';
    s += '                Thread.Sleep(10);\n';
    s += '            }\n';
    s += '            return MotionResult.Fail(-3, "move timeout axis " + axis);\n';
    s += '        }\n\n';

    // MoveLine
    s += '        public MotionResult MoveLine(int[] axisList, double[] targetUser, MoveParams p, MoveMode mode)\n';
    s += '        {\n            lock (_sdk)\n            {\n';
    s += '                if (!_open) return MotionResult.Fail(-1, "not open");\n';
    s += '                if (axisList == null || targetUser == null || axisList.Length != targetUser.Length)\n';
    s += '                    return MotionResult.Fail(-4, "axisList/targetUser mismatch");\n';
    s += kb.moveLine + '\n';
    s += '            }\n        }\n\n';

    // MoveArc2D 通用: 圆弧线性化分段（32段），依赖 MoveLine
    s += '        public MotionResult MoveArc2D(int axisX, int axisY, double i, double j, double endX, double endY, bool cw, MoveParams p)\n';
    s += '        {\n';
    s += '            lock (_sdk)\n';
    s += '            {\n';
    s += '                if (!_open) return MotionResult.Fail(-1, "not open");\n';
    s += '                double x0 = GetCommandPos(axisX), y0 = GetCommandPos(axisY);\n';
    s += '                double cx = x0 + i, cy = y0 + j;\n';
    s += '                double r = Math.Sqrt(i * i + j * j);\n';
    s += '                if (r < 1e-9) return MotionResult.Fail(-8, "zero radius arc");\n';
    s += '                double a0 = Math.Atan2(y0 - cy, x0 - cx);\n';
    s += '                double a1 = Math.Atan2(endY - cy, endX - cx);\n';
    s += '                double sweep = a1 - a0;\n';
    s += '                if (cw && sweep >= 0) sweep -= 2 * Math.PI;\n';
    s += '                if (!cw && sweep <= 0) sweep += 2 * Math.PI;\n';
    s += '                int n = 32;\n';
    s += '                for (int k = 1; k <= n; k++)\n';
    s += '                {\n';
    s += '                    double a = a0 + sweep * k / n;\n';
    s += '                    MotionResult rseg = MoveLine(new int[] { axisX, axisY },\n';
    s += '                        new double[] { cx + r * Math.Cos(a), cy + r * Math.Sin(a) }, p, MoveMode.Absolute);\n';
    s += '                    if (!rseg.Ok) return rseg;\n';
    s += '                    MotionResult rw = WaitMoveDone(axisX, 60000);\n';
    s += '                    if (!rw.Ok) return rw;\n';
    s += '                }\n';
    s += kb.arcNote + '\n';
    s += '                return MotionResult.Success("arc done (32-seg linearization)");\n';
    s += '            }\n';
    s += '        }\n\n';

    // IO
    s += '        public MotionResult SetDo(int index, bool on)\n';
    s += '        {\n            lock (_sdk)\n            {\n';
    s += '                if (!_open) return MotionResult.Fail(-1, "not open");\n' + kb.setDo + '\n';
    s += '            }\n        }\n\n';
    s += '        public bool GetDi(int index)\n';
    s += '        {\n            lock (_sdk)\n            {\n';
    s += '                if (!_open) return false;\n' + kb.getDi + '\n';
    s += '            }\n        }\n\n';
    s += '        public double GetAi(int index)\n';
    s += '        {\n            lock (_sdk)\n            {\n';
    s += '                if (!_open) return 0;\n' + kb.getAi + '\n';
    s += '            }\n        }\n\n';
    s += '        public MotionResult SetAo(int index, double volts)\n';
    s += '        {\n            lock (_sdk)\n            {\n';
    s += '                if (!_open) return MotionResult.Fail(-1, "not open");\n' + kb.setAo + '\n';
    s += '            }\n        }\n\n';

    // WaitDi common
    s += '        public MotionResult WaitDi(int index, bool level, int timeoutMs)\n';
    s += '        {\n';
    s += '            DateTime t0 = DateTime.Now;\n';
    s += '            while ((DateTime.Now - t0).TotalMilliseconds < timeoutMs)\n';
    s += '            {\n';
    s += '                if (GetDi(index) == level) return MotionResult.Success();\n';
    s += '                Thread.Sleep(10);\n';
    s += '            }\n';
    s += '            return MotionResult.Fail(-5, "wait DI" + index + " timeout");\n';
    s += '        }\n\n';

    // helpers
    s += '        private static void CheckRange(int axis)\n';
    s += '        {\n';
    s += '            if (axis < 0 || axis > 7) throw new ArgumentOutOfRangeException("axis", axis, "supports 0-7");\n';
    s += '        }\n';
    s += (kb.helpers || '') + '\n';
    s += '    }\n}\n';
    return s;
  }

  return { buildAdapter: buildAdapter };
});
