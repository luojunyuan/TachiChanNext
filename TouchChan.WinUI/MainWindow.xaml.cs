using System;
using Microsoft.UI.Xaml;
using R3;
using TouchChan.Interop;

namespace TouchChan.WinUI;

public sealed partial class MainWindow : Window
{
    public static Subject<Unit> OnTouchShowed { get; private set; } = new();

    public nint Hwnd { get; }

    public MainWindow()
    {
        Hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

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

#if DEBUG
        if (this.Content is Microsoft.UI.Xaml.Controls.Grid panel)
        {
            panel.Children.Add(new Microsoft.UI.Xaml.Controls.Border()
            {
                CornerRadius = new(12),
                BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Red),
                BorderThickness = new(1),
            });
        }
#endif
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