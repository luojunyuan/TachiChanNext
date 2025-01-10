using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace TouchChan
{
    /// <summary>
    /// Windows Handle 相关的 Win32 封装方法
    /// </summary>
    public static class HwndExtensions
    {
        public static void ToggleWindowStyle(this nint hwnd, bool enable, WindowStyle style)
        {
            var oldStyle = (WindowStyle)PInvoke.GetWindowLong(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_STYLE);
            var newStyle = enable ? oldStyle | style : oldStyle & ~style;
            if (PInvoke.SetWindowLong(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_STYLE, (int)newStyle) != (int)oldStyle)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
            }
        }

        public static void ToggleWindowExStyle(this nint hwnd, bool enable, ExtendedWindowStyle style)
        {
            var oldStyle = (ExtendedWindowStyle)PInvoke.GetWindowLong(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            var newStyle = enable ? oldStyle | style : oldStyle & ~style;
            if (PInvoke.SetWindowLong(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)newStyle) != (int)oldStyle)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
            }
        }

        /// <summary>
        /// 调整窗口客户区大小
        /// </summary>
        /// <remarks>
        /// NOTE: 我没有观测到 Repaint 设置为 false 带来的任何负面影响
        /// </remarks>
        public static void ResizeClient(this nint hwnd, Size size) =>
            PInvoke.MoveWindow(new(hwnd), 0, 0, size.Width, size.Height, false);

        /// <summary>
        /// 恢复窗口原始可观测区域
        /// </summary>
        public static void ResetWindowOriginalObservableRegion(this nint hwnd, Size size) =>
            SetWindowObservableRegion(hwnd, new(Point.Empty, size));

        /// <summary>
        /// 设置窗口可以被观测和点击的区域
        /// </summary>
        public static void SetWindowObservableRegion(this nint hwnd, Rectangle rect)
        {
            HRGN hRgn = PInvoke.CreateRectRgn(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);
            _ = PInvoke.SetWindowRgn(new(hwnd), hRgn, true);
        }
    }

    public enum WindowStyle
    {
        TiledWindow = 0xCF0000,
        Popup = int.MinValue,
        Child = 0x40000000,
    }

    public enum ExtendedWindowStyle
    {
        Layered = 0x80000,
    }
}
