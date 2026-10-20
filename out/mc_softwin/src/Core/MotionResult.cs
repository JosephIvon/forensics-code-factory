using System;
namespace MotionControl.Core
{
    /// <summary>统一结果对象：错误码语义跨品牌一致，业务层不做品牌分支（JD职责9: 分析报告与预防措施的统一数据源）。</summary>
    public class MotionResult
    {
        public bool Ok;
        public int Code;
        public string Message = "";
        public DateTime Time = DateTime.Now;

        public static MotionResult Success() { return new MotionResult { Ok = true, Code = 0, Message = "OK" }; }
        public static MotionResult Success(string msg) { return new MotionResult { Ok = true, Code = 0, Message = msg }; }
        public static MotionResult Fail(int code, string msg) { return new MotionResult { Ok = false, Code = code, Message = msg }; }
        public override string ToString()
        {
            return (Ok ? "[OK] " : ("[ERR " + Code + "] ")) + Message + " @" + Time.ToString("HH:mm:ss.fff");
        }
    }
}
