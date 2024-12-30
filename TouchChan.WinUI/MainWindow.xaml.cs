using Microsoft.UI.Xaml;
using R3;
using Windows.Foundation;
using WinRT.Interop;

namespace TouchChan.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly nint Hwnd;
    private readonly GameWindowService GameWidowService;

    public MainWindow()
    {
        GameWidowService = ServiceLocator.GameWindowService;
        Hwnd = WindowNative.GetWindowHandle(this);

        this.InitializeComponent();
        this.SystemBackdrop = new WinUIEx.TransparentTintBackdrop();
        HwndExtensions.ToggleWindowStyle(Hwnd, false, WindowStyle.TiledWindow);
        HwndExtensions.ToggleWindowStyle(Hwnd, false, WindowStyle.Popup);
        HwndExtensions.ToggleWindowStyle(Hwnd, true, WindowStyle.Child);
        HwndExtensions.ToggleWindowExStyle(Hwnd, true, ExtendedWindowStyle.Layered);
        NativeMethods.SetParent(Hwnd, GameWidowService.WindowHandle);
        // NOTE: 设置为子窗口后，this.AppWindow 不再可靠

        GameWidowService.ClientSizeChanged()
            .Subscribe(size => HwndExtensions.ResizeClient(Hwnd, size));

        Touch.ResetWindowObservable = size => HwndExtensions.ResetWindowOriginalObservableRegion(Hwnd, size.ToGdiSize());
        Touch.SetWindowObservable = rect => HwndExtensions.SetWindowObservableRegion(Hwnd, rect.ToGdiRect());
        // FIXME: プログラム '[9636] TouchChan.WinUI.exe' はコード 3221225480(0xc0000008) 'An invalid handle was specified' で終了しました。
        Touch.RightTapped += (s, e) => Close();
        // QUES: 启动后，获得焦点无法放在最前面？是什么原因，需要重新激活焦点。今后再检查整个程序与窗口启动方式
    }
}

static class ControllerSizeCoverDpiExtensions
{
    public static System.Drawing.Size ToGdiSize(this Size size) => new((int)size.Width, (int)size.Height);

    public static System.Drawing.Rectangle ToGdiRect(this Rect size) => new((int)size.X, (int)size.Y, (int)size.Width, (int)size.Height);

    // XDpi 意味着将框架内部任何元素产生的点或面的值还原回真实的物理像素大小

    public static Size ActualSizeXDpi(this FrameworkElement element, double factor)
    {
        var size = element.ActualSize.ToSize();
        size.Width *= factor;
        size.Height *= factor;
        return size;
    }

    public static Rect XDpi(this Rect rect, double factor)
    {
        rect.X *= factor;
        rect.Y *= factor;
        rect.Width *= factor;
        rect.Height *= factor;
        return rect;
    }
}