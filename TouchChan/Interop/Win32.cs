using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace TouchChan.Interop;

/// <summary>
/// 对 Win32 Api 的业务逻辑封装
/// </summary>
public static class Win32
{
    /// <summary>
    /// 激活窗口
    /// </summary>
    public static void ActiveWindow(nint hwnd)
    {
        PInvoke.ShowWindow(new(hwnd), SHOW_WINDOW_CMD.SW_RESTORE);
        PInvoke.SetForegroundWindow(new(hwnd));
    }

    /// <summary>
    /// 尝试恢复最小化的窗口
    /// </summary>
    public static Task TryRestoreWindowAsync(nint windowHandle)
    {
        if (PInvoke.IsIconic(new(windowHandle)))
        {
            return Task.Run(() => PInvoke.ShowWindow(new(windowHandle), SHOW_WINDOW_CMD.SW_RESTORE));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 判断进程对象是否对 DPI 不感知
    /// </summary>
    [SupportedOSPlatform("windows8.1")]
    public static bool IsDpiUnaware(Process process)
    {
        var result = PInvoke.GetProcessDpiAwareness(process.SafeHandle, out var awareType);

        return result == 0 && (awareType == 0 || awareType == PROCESS_DPI_AWARENESS.PROCESS_SYSTEM_DPI_AWARE);
    }

    /// <summary>
    /// 获取窗口所在显示器的 DPI
    /// </summary>
    [SupportedOSPlatform("windows8.1")]
    public static uint GetDpiForWindowsMonitor(nint hwnd)
    {
        var monitorHandle = PInvoke.MonitorFromWindow(new(hwnd), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        PInvoke.GetDpiForMonitor(monitorHandle, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out _);
        return dpiX;
    }

    /// <summary>
    /// 获取客户区窗口大小
    /// </summary>
    public static Size GetWindowSize(nint hwnd)
    {
        PInvoke.GetClientRect(new(hwnd), out var initRect);
        return initRect.Size;
    }
}
