namespace MotionControl.Core
{
    /// <summary>连接信息：脉冲卡用卡号，网络/总线卡用 IP，按需扩展串口号。</summary>
    public class ControllerConnectInfo
    {
        public short CardNo = 0;
        public string Ip = "192.168.0.11";
        public int Port = 701;         // ACS SPiiPlus 默认 TCP 端口
        public string ConfigFile = ""; // 固高 GT_LoadConfig 配置文件（可选）
    }
}
