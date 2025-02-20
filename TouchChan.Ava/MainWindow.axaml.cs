using Avalonia.Controls;
using Avalonia.Interactivity;
using R3;
using TouchChan.Interop;

namespace TouchChan.Ava;

public partial class MainWindow : Window
{
    public Subject<Unit> TouchShowed { get; } = new();

    public nint Hwnd { get; }

    public MainWindow()
    {
        Hwnd = GetTopLevel(this)?.TryGetPlatformHandle()?.Handle ?? throw new InvalidOperationException();

        InitializeComponent();
        Position = new(-32000, -32000);

        // Child 样式需要 this.Loaded 触发后才能正确被设置
        this.RxLoaded()
            .Subscribe(_ =>
            {
                Hwnd.ToggleWindowStyle(false, WindowStyle.ClipChildren);
                Hwnd.ToggleWindowStyle(false, WindowStyle.TiledWindow);
                Hwnd.ToggleWindowStyle(false, WindowStyle.Popup);
                Hwnd.ToggleWindowStyle(true, WindowStyle.Child);
                Hwnd.ToggleWindowExStyle(false, ExtendedWindowStyle.AppWindow);
                Hwnd.ToggleWindowExStyle(true, ExtendedWindowStyle.Layered);
            });

        Touch.ResetWindowObservable = size => HwndExtensions.ResetWindowOriginalObservableRegion(Hwnd, (size * DesktopScaling).ToGdiSize());
        Touch.SetWindowObservable = rect => HwndExtensions.SetWindowObservableRegion(Hwnd, (rect * DesktopScaling).ToGdiRect());
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