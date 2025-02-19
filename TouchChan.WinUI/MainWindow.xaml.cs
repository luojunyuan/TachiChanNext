using Microsoft.UI.Xaml;
using R3;
using R3.ObservableEvents;
using TouchChan.Interop;
using Windows.System;

namespace TouchChan.WinUI;

public sealed partial class MainWindow : Window
{
    public static Subject<Unit> OnTouchShowed { get; private set; } = new();

    public ReplaySubject<Unit> Loaded { get; } = new(1);

    public nint Hwnd { get; }

    public MainWindow()
    {
        Hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // WinUI 窗口的初始大小是个谜 MinSize: (136, 39)
        this.AppWindow.Move(new(-32000, -320000));
        this.AppWindow.IsShownInSwitchers = false;

        this.InitializeComponent();
        this.SystemBackdrop = new WinUIEx.TransparentTintBackdrop();
        Win32.ToggleWindowStyle(Hwnd, false, WindowStyle.TiledWindow);
        Win32.ToggleWindowStyle(Hwnd, false, WindowStyle.Popup);
        Win32.ToggleWindowStyle(Hwnd, true, WindowStyle.Child);
        Win32.ToggleWindowExStyle(Hwnd, true, ExtendedWindowStyle.Layered);
        ((FrameworkElement)this.Content).Events().Loaded.Subscribe(_ => Loaded.OnNext(Unit.Default));

        // NOTE: 设置为子窗口后，this.AppWindow 不再可靠

        Touch.ResetWindowObservable = size => Win32.ResetWindowOriginalObservableRegion(Hwnd, size.ToGdiSize());
        Touch.SetWindowObservable = rect => Win32.SetWindowObservableRegion(Hwnd, rect.ToGdiRect());

        // FIXME: 仅对部分游戏有效，没有改变抢占父窗口焦点的本质
        this.Content.Events().ProcessKeyboardAccelerators
            .Select(x => x.args)
            .Where(args => args.Modifiers == VirtualKeyModifiers.Menu &&
                args.Key == VirtualKey.Enter)
            .Subscribe(_ => Win32.SendAltEnter(_gameHandleAltEnter));

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

    private nint _gameHandleAltEnter;
    public void UpdateAltEnterHandle(nint handle) => _gameHandleAltEnter = handle;

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