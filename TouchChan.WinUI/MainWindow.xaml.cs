using Microsoft.UI.Xaml;
using R3;
using R3.ObservableEvents;
using TouchChan.Interop;

namespace TouchChan.WinUI;

public sealed partial class MainWindow : Window
{
    /// <summary>
    /// 游戏窗口与子窗口完成绑定
    /// </summary>
    public Subject<Unit> OnWindowBound { get; private set; } = new();

    /// <summary>
    /// 子窗口初始化准备完成
    /// </summary>
    public ReplaySubject<Unit> Loaded { get; } = new(1);

    public MainWindow()
    {
        // WinUI 窗口的初始大小是个谜 MinSize: (136, 39)
        this.AppWindow.Move(new(-32000, -32000));
        this.AppWindow.IsShownInSwitchers = false;

        this.InitializeComponent();
        this.SystemBackdrop = new WinUIEx.TransparentTintBackdrop();
        this.ToggleWindowStyle(false, WindowStyle.TiledWindow);
        this.ToggleWindowStyle(false, WindowStyle.Popup);
        this.ToggleWindowStyle(true, WindowStyle.Child);
        this.ToggleWindowExStyle(true, ExtendedWindowStyle.Layered);
        this.Root.Events().Loaded.Subscribe(_ => Loaded.OnNext(Unit.Default));

        // NOTE: 设置为子窗口后，this.AppWindow 不再可靠

        Touch.ResetWindowObservable = this.ResetWindowOriginalObservableRegion;
        Touch.SetWindowObservable = this.SetWindowObservableRegion;
        OnWindowBound.Subscribe(Touch.OnWindowBound.OnNext);

#if DEBUG // 添加一个红色边框以确定可以点击（观测）的窗口范围
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

    public void SetFocusOnGameCallback(Action? value) => Touch.RestoreFocus = value;

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
            // Ques：不知 AppWindow.Show Hide 或者 NativeShow NativeHide 这里能不能正常使用
            throw new NotImplementedException();
        }
        return GameWindowService.ClientSizeChanged(windowHandle)
            .Select(_ => Win32.GetDpiForWindowsMonitor(windowHandle) / 96d)
            .DistinctUntilChanged()
            .Subscribe(dpiScale => SetWindowVisible(dpiScale == 1));
    }
}
