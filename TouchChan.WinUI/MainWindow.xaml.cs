using Microsoft.UI.Xaml;
using R3;
using System;
using Windows.Foundation;
using WinRT.Interop;

namespace TouchChan.WinUI;

public sealed partial class MainWindow : Window
{
    public nint Hwnd { get; }

    private readonly GameWindowService GameWindowService;

    public MainWindow(GameWindowService? gameWindowService = null)
    {
        GameWindowService = gameWindowService ??= ServiceLocator.GameWindowService;
        Hwnd = WindowNative.GetWindowHandle(this);

        this.InitializeComponent();
        this.SystemBackdrop = new WinUIEx.TransparentTintBackdrop();
        HwndExtensions.ToggleWindowStyle(Hwnd, false, WindowStyle.TiledWindow);
        HwndExtensions.ToggleWindowStyle(Hwnd, false, WindowStyle.Popup);
        HwndExtensions.ToggleWindowStyle(Hwnd, true, WindowStyle.Child);
        HwndExtensions.ToggleWindowExStyle(Hwnd, true, ExtendedWindowStyle.Layered);

        // REFACTOR: 不再依赖 GameWindowService，初始化后再设置 SetParent ClientSizeChanged 等
        //if (GameWindowService.IsDpiUnaware)
        //{
        //    var subscription = UnawareGameWindowShowHideHack();
        //}

        //NativeMethods.SetParent(Hwnd, GameWindowService.WindowHandle);
        // NOTE: 设置为子窗口后，this.AppWindow 不再可靠

        //var gameWindowSizeSubscription =
        //GameWindowService.ClientSizeChanged()
        //    .Subscribe(size => HwndExtensions.ResizeClient(Hwnd, size));

        Touch.ResetWindowObservable = size => HwndExtensions.ResetWindowOriginalObservableRegion(Hwnd, size.ToGdiSize());
        Touch.SetWindowObservable = rect => HwndExtensions.SetWindowObservableRegion(Hwnd, rect.ToGdiRect());
    }

    /// <summary>
    /// DPI Unaware 窗口处于高 DPI 上时隐藏游戏窗口
    /// </summary>
    /// <remarks>
    /// 必须在 SetParent 之前设置，否则似乎不会感知 Unaware 下的游戏窗口大小变化
    /// </remarks>
    private IDisposable UnawareGameWindowShowHideHack()
    {
        void SetWindowVisible(bool visible)
        {
            if (visible) WinUIEx.WindowExtensions.Show(this);
            else WinUIEx.WindowExtensions.Hide(this);
        }
        return GameWindowService.ClientSizeChanged()
            .Select(_ => Win32.GetDpiForWindowsMonitor(GameWindowService.WindowHandle) / 96d)
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