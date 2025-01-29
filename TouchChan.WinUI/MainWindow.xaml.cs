using System;
using Microsoft.UI.Xaml;
using R3;
using Windows.Foundation;
using WinRT.Interop;

namespace TouchChan.WinUI;

public sealed partial class MainWindow : Window
{
    public nint Hwnd { get; }

    public MainWindow()
    {
        Hwnd = WindowNative.GetWindowHandle(this);

        this.AppWindow.MoveAndResize(new(-32000, -320000, 0, 0));
        this.AppWindow.IsShownInSwitchers = false;

        this.InitializeComponent();
        this.SystemBackdrop = new WinUIEx.TransparentTintBackdrop();
        Hwnd.ToggleWindowStyle(false, WindowStyle.TiledWindow);
        Hwnd.ToggleWindowStyle(false, WindowStyle.Popup);
        Hwnd.ToggleWindowStyle(true, WindowStyle.Child);
        Hwnd.ToggleWindowExStyle(true, ExtendedWindowStyle.Layered);

        // NOTE: 设置为子窗口后，this.AppWindow 不再可靠

        Touch.ResetWindowObservable = size => Hwnd.ResetWindowOriginalObservableRegion(size.ToGdiSize());
        Touch.SetWindowObservable = rect => Hwnd.SetWindowObservableRegion(rect.ToGdiRect());
    }

    /// <summary>
    /// DPI Unaware 窗口处于高 DPI 上时隐藏游戏窗口
    /// </summary>
    /// <remarks>
    /// 必须在 SetParent 之前设置，否则似乎不会感知 Unaware 下的游戏窗口大小变化
    /// </remarks>
    public IDisposable UnawareGameWindowShowHideHack(nint windowHandle)
    {
        void SetWindowVisible(bool visible)
        {
            if (visible) WinUIEx.WindowExtensions.Show(this);
            else WinUIEx.WindowExtensions.Hide(this);
        }
        return GameWindowService.ClientSizeChanged(windowHandle)
            .Select(_ => Win32.GetDpiForWindowsMonitor(windowHandle) / 96d)
            .DistinctUntilChanged()
            .Subscribe(dpiScale => SetWindowVisible(dpiScale == 1));
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