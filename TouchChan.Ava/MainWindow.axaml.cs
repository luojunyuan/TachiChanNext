using Avalonia.Controls;
using Avalonia.Interactivity;
using R3;

namespace TouchChan.Ava;

public partial class MainWindow : Window
{
    public Subject<Unit> TouchShowed { get; } = new();

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