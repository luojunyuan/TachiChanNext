using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace TouchChan.SplashScreenGdiPlus;

public class SplashScreen
{
    public static async Task<T> WithShowAndExecuteAsync<T>(Stream resource, Func<Task<T>> action)
    {

        using var image = Image.FromStream(resource);
        var splash = InternalShow(image);
        try
        {
            return await Task.Run(() => action()).ConfigureAwait(false);
        }
        finally
        {
            // NOTE: finally 是重要的，他保证了无论 Action 结果如何 splash 都将被清理
            splash.CleanUp();
        }
    }

    private static SplashScreen InternalShow(Image image)
    {
        var splash = new SplashScreen();
        splash.DisplaySplash(image);
        return splash;
    }

    private HWND _hWndSplash;
    private HDC _hdc;

    private unsafe void DisplaySplash(Image image)
    {
        const string className = "SplashScreen";
        const string windowTitle = "Splash Screen";
        fixed (char* lpClassName = className)
        {
            var wndClass = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = WndProc,
                lpszClassName = new(lpClassName),
                hInstance = default,
            };
            PInvoke.RegisterClassEx(in wndClass);
        }

        // Hires image scaling
        const double scaleFactor = 2.0;
        const double scaleFactorMid = 1.5;
        const int dpiFactor = 200;
        const int dpiFactorMid = 150;
        var dpi = (int)(GetDpiScale() * 100);

        var (width, height) = dpi switch
        {
            < dpiFactorMid => ((int)(image.Width / scaleFactor), (int)(image.Height / scaleFactor)),
            < dpiFactor => ((int)(image.Width / scaleFactorMid), (int)(image.Height / scaleFactorMid)),
            >= dpiFactor => (image.Width, image.Height),
        };

        var (x, y) = CenterToPrimaryScreen(width, height);

        _hWndSplash = PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_TOOLWINDOW |
            WINDOW_EX_STYLE.WS_EX_TRANSPARENT |
            WINDOW_EX_STYLE.WS_EX_TOPMOST |
            WINDOW_EX_STYLE.WS_EX_NOACTIVATE,
            className,
            windowTitle,
            WINDOW_STYLE.WS_POPUP | WINDOW_STYLE.WS_VISIBLE,
            x, y, width, height,
            HWND.Null, null, default, null);

        _hdc = PInvoke.GetDC(_hWndSplash);

        using var g = Graphics.FromHdc(_hdc);
        g.DrawImage(image, 0, 0, width, height);

        PInvoke.SetWindowLong(_hWndSplash, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
            PInvoke.GetWindowLong(_hWndSplash, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE) | (int)WINDOW_EX_STYLE.WS_EX_LAYERED);

        PInvoke.SetLayeredWindowAttributes(_hWndSplash, new COLORREF((uint)Color.Green.ToArgb()), 0, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_COLORKEY);
    }

    /// <summary>
    /// Clean up resources
    /// </summary>
    /// <remarks>Must be executed on the same thread that called CreateWindowEx to be effective</remarks>
    private void CleanUp()
    {
        PInvoke.ReleaseDC(_hWndSplash, _hdc);
        PInvoke.DestroyWindow(_hWndSplash);
    }

    private static unsafe (int X, int Y) CenterToPrimaryScreen(int width, int height)
    {
        var rcWorkArea = new Rectangle();
        PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETWORKAREA, 0, &rcWorkArea, 0);
        int nX = Convert.ToInt32((rcWorkArea.Left + rcWorkArea.Right) / (double)2 - width / (double)2);
        int nY = Convert.ToInt32((rcWorkArea.Top + rcWorkArea.Bottom) / (double)2 - height / (double)2);
        return (nX, nY);
    }

    private static unsafe double GetDpiScale()
    {
        var monitor = PInvoke.MonitorFromPoint(new Point(0, 0), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);

        if (monitor != nint.Zero && OperatingSystem.IsWindowsVersionAtLeast(8, 1))
        {
            PInvoke.GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpi, out _);

            return dpi == 0 ? 1 : dpi / 96d;
        }

        return 1;
    }

    private static LRESULT WndProc(HWND hwnd, uint uMsg, WPARAM wParam, LPARAM lParam)
        => PInvoke.DefWindowProc(hwnd, uMsg, wParam, lParam);
}
