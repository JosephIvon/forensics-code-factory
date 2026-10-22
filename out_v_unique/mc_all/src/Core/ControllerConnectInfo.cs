namespace MotionControl.Core
{
    /// <summary>连接信息：脉冲卡用卡号，网络/总线卡用 IP，按需扩展串口号。</summary>
    public class ControllerConnectInfo
    {
        public short CardNo = 0;
        public string Ip = "192.168.0.11";
        public int Port = 701;         // ACS SPiiPlus 默认 TCP 端口
        public string ConfigFile = ""; // 固高 GT_LoadConfig 配置文件（可选）

        /// <summary>
        /// 原生 DLL 所在目录（含其依赖 DLL）。留空则用程序所在目录。
        /// 必须显式指定或由 NativeLoader 加入搜索路径，否则 LoadLibrary 找依赖时
        /// 不搜 DLL 自身所在目录，会误报 Win32=126（详见 NativeLoader 类注释）。
        /// </summary>
        public string DllDir = "";
    }
}
