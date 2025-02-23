﻿using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace TouchChan.Interop;

/// <summary>
/// 操作 Windows 窗口相关的业务逻辑
/// </summary>
public static partial class Win32
{
    /// <summary>
    /// 激活窗口
    /// </summary>
    public static void ActivateWindow(nint hwnd)
    {
        PInvoke.ShowWindow(new(hwnd), SHOW_WINDOW_CMD.SW_RESTORE);
        PInvoke.SetForegroundWindow(new(hwnd));
    }

    /// <summary>
    /// 隐藏窗口
    /// </summary>
    public static bool HideWindow(IntPtr hWnd) => PInvoke.ShowWindow(new(hWnd), SHOW_WINDOW_CMD.SW_HIDE);

    /// <summary>
    /// 尝试恢复最小化的窗口
    /// </summary>
    public static Task TryRestoreWindowAsync(nint windowHandle)
    {
        if (PInvoke.IsIconic(new(windowHandle)))
            return Task.Run(() => PInvoke.ShowWindow(new(windowHandle), SHOW_WINDOW_CMD.SW_RESTORE));

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取客户区窗口大小
    /// </summary>
    public static Size GetWindowSize(nint hwnd)
    {
        PInvoke.GetClientRect(new(hwnd), out var initRect);
        return initRect.Size;
    }

    /// <summary>
    /// 调整客户区窗口大小
    /// </summary>
    /// <remarks>
    /// NOTE: 我没有观测到 Repaint 设置为 false 带来的任何负面影响
    /// </remarks>
    public static void ResizeWindow(nint hwnd, Size size) =>
        PInvoke.MoveWindow(new(hwnd), 0, 0, size.Width, size.Height, false);

    /// <summary>
    /// 设置窗口的父窗口
    /// </summary>
    public static Task<bool> SetParentWindowAsync(nint child, nint parent) =>
        Task.Run(() => PInvoke.SetParent(new(child), new(parent)) != HWND.Null);

    /// <summary>
    /// 设置窗口的 WindowStyle
    /// </summary>
    public static void ToggleWindowStyle(nint hwnd, bool enable, WindowStyle style)
    {
        var oldStyle = (WindowStyle)PInvoke.GetWindowLong(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        var newStyle = enable ? oldStyle | style : oldStyle & ~style;
        if (PInvoke.SetWindowLong(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_STYLE, (int)newStyle) != (int)oldStyle)
            Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
    }

    /// <summary>
    /// 设置窗口的 ExtendedWindowStyle
    /// </summary>
    public static void ToggleWindowExStyle(nint hwnd, bool enable, ExtendedWindowStyle style)
    {
        var oldStyle = (ExtendedWindowStyle)PInvoke.GetWindowLong(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        var newStyle = enable ? oldStyle | style : oldStyle & ~style;
        if (PInvoke.SetWindowLong(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)newStyle) != (int)oldStyle)
            Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
    }

    /// <summary>
    /// 恢复窗口原始可观测区域
    /// </summary>
    public static void ResetWindowOriginalObservableRegion(nint hwnd, Size size) =>
        SetWindowObservableRegion(hwnd, new(Point.Empty, size));

    /// <summary>
    /// 设置窗口可以被观测和点击的区域
    /// </summary>
    public static void SetWindowObservableRegion(nint hwnd, Rectangle rect)
    {
        HRGN hRgn = PInvoke.CreateRectRgn(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);
        _ = PInvoke.SetWindowRgn(new(hwnd), hRgn, true);
    }
}

public enum WindowStyle : uint
{
    ClipChildren = WINDOW_STYLE.WS_CLIPCHILDREN,
    TiledWindow = WINDOW_STYLE.WS_TILEDWINDOW,
    Popup = WINDOW_STYLE.WS_POPUP,
    Child = WINDOW_STYLE.WS_CHILD,
}

public enum ExtendedWindowStyle : uint
{
    Layered = WINDOW_EX_STYLE.WS_EX_LAYERED,
    AppWindow = WINDOW_EX_STYLE.WS_EX_APPWINDOW,
}
