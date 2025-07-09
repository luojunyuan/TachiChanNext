// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Windows.Win32;

internal static class Libraries
{
    public const string User32 = "user32.dll";
}

internal static partial class PInvokeCore
{
    [DllImport(Libraries.User32, SetLastError = true)]
    private static extern nint SetWindowLongW(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex, nint dwNewLong);

    [DllImport(Libraries.User32, SetLastError = true)]
    private static extern nint SetWindowLongPtrW(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex, nint dwNewLong);

    public static nint SetWindowLong(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex, nint newValue)
    {
        var result = Environment.Is64BitProcess
            ? SetWindowLongPtrW(hWnd, nIndex, newValue)
            : SetWindowLongW(hWnd, nIndex, (int)newValue);
        GC.KeepAlive(hWnd);
        return result;
    }
}
