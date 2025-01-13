using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace TouchChan.SplashScreenGdiPlus;

public class SplashScreen
{
    public unsafe SplashScreen Show(Stream imageStream)
    {
        using var image = Image.FromStream(imageStream); // d:17ms

        fixed (char* lpClassName = "SplashScreen")
        {
            var wndClass = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = WndProc,
                hInstance = default,
                lpszClassName = new(lpClassName),
            };
            PInvoke.RegisterClassEx(in wndClass);
        }

        _hwnd = PInvoke.CreateWindowEx(
            0,
            "SplashScreen",
            "Splash Screen",
            WINDOW_STYLE.WS_POPUP | WINDOW_STYLE.WS_VISIBLE,
            100, 100, image.Width, image.Height,
            default, null, default, null); // d:29ms

        _hdc = PInvoke.GetDC(_hwnd);

        using Graphics g = Graphics.FromHdc(_hdc);
        g.DrawImage(image, 0, 0, image.Width, image.Height); // d:9ms

        return this;
    }

    private HWND _hwnd;
    private HDC _hdc;

    public void Hide()
    {
        PInvoke.ReleaseDC(_hwnd, _hdc);
    }

    private static LRESULT WndProc(HWND hwnd, uint uMsg, WPARAM wParam, LPARAM lParam)
    {
        if (uMsg == PInvoke.WM_DESTROY)
        {
            PInvoke.PostQuitMessage(0);
            return new LRESULT(0);
        }

        return PInvoke.DefWindowProc(hwnd, uMsg, wParam, lParam);
    }
}
