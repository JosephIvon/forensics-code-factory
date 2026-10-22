using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace MotionControl.Core
{
    /// <summary>
    /// 原生 DLL 预加载器 —— 解决 P/Invoke 的「依赖搜索路径」陷阱。
    ///
    /// 【为什么必须有这个类】
    /// LoadLibrary("<绝对路径>\ACSCL_x64.dll") 会按标准 DLL 搜索顺序找它的依赖，
    /// 而该顺序<b>默认不包含这个 DLL 自己所在的目录</b>。
    /// 于是：ACSCL_x64.dll 与 WibuCm64.dll 明明躺在同一个文件夹，
    ///       装载依然报 Win32=126 (ERROR_MOD_NOT_FOUND)。
    /// 本项目就实际踩过这个坑：据此误判「ACS 原生库缺授权运行时、装不上」。
    ///
    /// 【正确做法】
    /// 1) AddDllDirectory(dir) 把目录加入搜索路径（Win8+ 正式 API，推荐）
    /// 2) 或 SetDllDirectory("") 让当前目录进入搜索路径（有安全副作用，不推荐）
    /// 3) 或 LoadLibraryEx(..., LOAD_WITH_ALTERED_SEARCH_PATH) —— 只对本次装载生效，
    ///    且要求传相对路径名，对「A 依赖 B 且 A、B 同目录」最干净
    ///
    /// 【何时调用】
    /// 在第一次触碰任何 P/Invoke 之前调用 <see cref="Ensure"/>，通常是 Open() 之前。
    /// </summary>
    public static class NativeLoader
    {
        private const string KERNEL32 = "kernel32.dll";

        [DllImport(KERNEL32, SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool AddDllDirectory(string lpPathName);

        [DllImport(KERNEL32, SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport(KERNEL32, SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryExW(string lpLibFileName, IntPtr hFile, uint dwFlags);

        [DllImport(KERNEL32, SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetModuleHandleW(string lpModuleName);

        [DllImport(KERNEL32, SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern uint GetLastError();

        private const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008;

        private static readonly object Gate = new object();
        private static readonly HashSet<string> Done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 把若干目录加入本进程的 DLL 搜索路径（幂等）。
        /// </summary>
        /// <param name="dirs">目录列表；不存在或为空名的会被跳过</param>
        /// <returns>成功加入的目录名</returns>
        public static IReadOnlyList<string> Ensure(params string[] dirs)
        {
            var added = new List<string>();
            lock (Gate)
            {
                foreach (var raw in dirs)
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    string dir;
                    try { dir = Path.GetFullPath(raw.Trim()); }
                    catch { continue; }

                    if (!Directory.Exists(dir)) continue;
                    if (!Done.Add(dir)) continue;

                    // 先试 AddDllDirectory（干净、无副作用）
                    if (AddDllDirectory(dir))
                    {
                        added.Add(dir);
                        continue;
                    }

                    // 退化：SetDllDirectory("") 把当前目录纳入搜索
                    // 注意这会改变全进程行为，故只在 AddDllDirectory 失败时用
                    string cur = Environment.CurrentDirectory;
                    try
                    {
                        Environment.CurrentDirectory = dir;
                        if (SetDllDirectory(string.Empty))
                        {
                            added.Add(dir + "  (via SetDllDirectory)");
                        }
                    }
                    finally
                    {
                        try { Environment.CurrentDirectory = cur; } catch { /* 忽略 */ }
                    }
                }
            }
            return added;
        }

        /// <summary>
        /// 预加载某个原生 DLL 及其同目录依赖，并确保其目录进入搜索路径。
        /// 适合在 Open() 里先调一次，把「装载失败」与「连接失败」两类错误区分开。
        /// </summary>
        /// <returns>true=库已在本进程加载；false=失败（看 <paramref name="win32Error"/>）</returns>
        public static bool Preload(string dllFileName, out uint win32Error)
        {
            win32Error = 0;
            if (string.IsNullOrWhiteSpace(dllFileName)) return false;

            string full;
            try { full = Path.GetFullPath(dllFileName); }
            catch { return false; }

            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir)) Ensure(dir);

            // 已加载则直接成功
            if (GetModuleHandleW(Path.GetFileName(full)) != IntPtr.Zero) return true;

            IntPtr h = LoadLibraryExW(full, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH);
            if (h == IntPtr.Zero)
            {
                win32Error = GetLastError();
                return false;
            }
            return true;
        }

        /// <summary>把 Win32 装载错误码翻译成人话，用于日志/异常信息。</summary>
        public static string DescribeLoadError(uint err)
        {
            switch (err)
            {
                case 0: return "无错误码";
                case 2: return "ERROR_FILE_NOT_FOUND 找不到文件";
                case 126: return "ERROR_MOD_NOT_FOUND 缺依赖（常见：依赖 DLL 不在搜索路径，见本类说明）";
                case 193: return "ERROR_BAD_EXE_FORMAT 位数不匹配（x86/x64 混用）";
                case 1114: return "ERROR_DLL_INIT_FAILED 初始化失败（DllMain 抛错）";
                case 145: return "ERROR_DLL_NOT_FOUND DLL 未找到";
                default: return "Win32=" + err;
            }
        }
    }
}