using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using R3;
using System.Runtime.Versioning;

namespace TouchChan.Ava;

public partial class MainWindow : Window
{
    public nint Hwnd { get; }

    public MainWindow()
    {
        Hwnd = GetTopLevel(this)?.TryGetPlatformHandle()?.Handle ?? throw new InvalidOperationException();

        InitializeComponent();

        this.RxLoaded()
            .Subscribe(_ =>
            {
                HwndExtensions.ToggleWindowStyle(Hwnd, false, WindowStyle.Popup);
                HwndExtensions.ToggleWindowStyle(Hwnd, true, WindowStyle.Child);
                HwndExtensions.ToggleWindowExStyle(Hwnd, true, ExtendedWindowStyle.Layered);
            });

        Touch.ResetWindowObservable = size => HwndExtensions.ResetWindowOriginalObservableRegion(Hwnd, size.ToGdiSize());
        Touch.SetWindowObservable = rect => HwndExtensions.SetWindowObservableRegion(Hwnd, rect.ToGdiRect());

        // DEBUG
        Touch.RxPointerPressed()
            .Where(e => e.GetCurrentPoint(Touch).Properties.PointerUpdateKind == Avalonia.Input.PointerUpdateKind.RightButtonPressed)
            .Subscribe(_ => Close());
        // プログラム '[10828] TouchChan.Ava.exe' はコード 3221225480 (0xc0000008) 'An invalid handle was specified' で終了しました。
    }
}

static partial class MainWindowEventsExtensions
{
    public static System.Drawing.Size ToGdiSize(this Avalonia.Size size) => new((int)size.Width, (int)size.Height);

    public static System.Drawing.Rectangle ToGdiRect(this Avalonia.Rect size) => new((int)size.X, (int)size.Y, (int)size.Width, (int)size.Height);

    public static Observable<RoutedEventArgs> RxLoaded(this Window data) =>
        Observable.FromEvent<EventHandler<RoutedEventArgs>, RoutedEventArgs>(
            h => (sender, e) => h(e),
            e => data.Loaded += e,
            e => data.Loaded -= e);
}