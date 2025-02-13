using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

// CSWin32 的包装代码，本来应该是 CSWin32 负责生成的工作
namespace Windows.Win32
{
    partial class PInvoke
    {
        internal unsafe static uint GetWindowThreadProcessId(HWND hwnd, out uint lpdwProcessId)
        {
            fixed (uint* _lpdwProcessId = &lpdwProcessId)
                return GetWindowThreadProcessId(hwnd, _lpdwProcessId);
        }
    }
}

namespace TouchChan.Interop
{

    /// <summary>
    /// 对 CSWin32 PInvoke 调用的直接封装
    /// </summary>
    public static partial class NativeMethods
    {
        public static nint GetConsoleWindow() => PInvoke.GetConsoleWindow();

        public static Task<bool> SetParentAsync(nint child, nint parent) =>
            Task.Run(() => PInvoke.SetParent(new(child), new(parent)) != HWND.Null);

        [SupportedOSPlatform("windows10.0.14393")]
        public static uint GetDpiForWindow(nint hwnd) => PInvoke.GetDpiForWindow(new(hwnd));
    }

    public static class MessageBox
    {
        public static Task ShowAsync(string text, string caption = Constants.DisplayName) =>
            Task.FromResult(PInvoke.MessageBox(HWND.Null, text, caption, MESSAGEBOX_STYLE.MB_OK));
    }
}
