using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;

namespace TouchChan;

/// <summary>
/// 对 Win32 Api 的业务逻辑封装
/// </summary>
public static class Win32
{
    /// <summary>
    /// 判断进程对象是否对 DPI 不感知
    /// </summary>
    [SupportedOSPlatform("windows8.1")]
    public static bool IsDpiUnaware(int pid)
    {
        var handle = Process.GetProcessById(pid).Handle;
        using var processHandle = new SafeProcessHandle(handle, true);
        var result = PInvoke.GetProcessDpiAwareness(processHandle, out var awareType);

        return result == 0 && awareType == 0;
    }
}

/// <summary>
/// 对 CSWin32 PInvoke 调用的直接封装
/// </summary>
public static class NativeMethods
{
    public static void SetParent(this nint child, nint parent) => PInvoke.SetParent(new(child), new(parent));

    [SupportedOSPlatform("windows10.0.14393")]
    public static uint GetDpiForWindow(this nint hwnd) => PInvoke.GetDpiForWindow(new(hwnd));
}
