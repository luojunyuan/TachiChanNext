using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using R3;
using TouchChan;

namespace AvaFramelessChildWindow;

public partial class MainWindow : Window
{
    private readonly GameWindowService GameWindowService;
    private readonly nint Hwnd;

    public MainWindow()
    {
        GameWindowService = ServiceLocator.GameWindowService;
        Hwnd = GetTopLevel(this)?.TryGetPlatformHandle()?.Handle ?? throw new InvalidOperationException();

        InitializeComponent();

        this.RxLoaded()
            .Subscribe(_ =>
            {
                HwndExtensions.ToggleWindowStyle(Hwnd, false, WindowStyle.Popup);
                HwndExtensions.ToggleWindowStyle(Hwnd, true, WindowStyle.Child);
                HwndExtensions.ToggleWindowExStyle(Hwnd, true, ExtendedWindowStyle.Layered);
                NativeMethods.SetParent(Hwnd, GameWindowService.WindowHandle);
            });

        // QUES: 这个应该需要开发者显式 Dispose 吧？一个很明显的特征是，这个服务由 ServiceLocator 管理，所以应该在外部释放它
        GameWindowService.ClientSizeChanged()
            .Subscribe(size => Hwnd.ResizeClient(size));

        Touch.ResetWindowObservable = size => HwndExtensions.ResetWindowOriginalObservableRegion(Hwnd, size);
        Touch.SetWindowObservable = rect => HwndExtensions.SetWindowObservableRegion(Hwnd, rect);
        Touch.RxPointerPressed()
            .Where(e => e.GetCurrentPoint(Touch).Properties.PointerUpdateKind == Avalonia.Input.PointerUpdateKind.RightButtonPressed)
            .Subscribe(_ => Close());
        // プログラム '[10828] AvaFramelessChildWindow.exe' はコード 3221225480 (0xc0000008) 'An invalid handle was specified' で終了しました。
    }
}

static partial class ObservableEventsExtensions
{
    public static Observable<RoutedEventArgs> RxLoaded(this Window data) =>
        Observable.FromEvent<EventHandler<RoutedEventArgs>, RoutedEventArgs>(
            h => (sender, e) => h(e),
            e => data.Loaded += e,
            e => data.Loaded -= e);
}