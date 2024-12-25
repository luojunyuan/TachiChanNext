using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using R3;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace TouchChan
{
    using TouchChan.MainWindowExtensions;
    using HwndExtensions = WinUIEx.HwndExtensions;
    using WindowExtensions = WinUIEx.WindowExtensions;
    using WindowStyle = WinUIEx.WindowStyle;

    public sealed partial class MainWindow : Window
    {
        private readonly nint GameWindowHandle;
        private readonly nint Hwnd;

        public MainWindow(nint gameWindowHandle)
        {
            GameWindowHandle = gameWindowHandle;
            Hwnd = WindowExtensions.GetWindowHandle(this);

            this.InitializeComponent();
            this.SystemBackdrop = new WinUIEx.TransparentTintBackdrop();
            ToggleWindowStyle(Hwnd, false, WindowStyle.TiledWindow);
            ToggleWindowStyle(Hwnd, false, WindowStyle.Popup);
            ToggleWindowStyle(Hwnd, true, WindowStyle.Child);
            ToggleWindowStyle(Hwnd, true, WindowStyle.ClipChildren);
            PInvoke.SetParent(Hwnd.ToHwnd(), GameWindowHandle.ToHwnd());
            // Note: 设置为子窗口后，this.AppWindow 不再可靠

            GameWindowHooker.ClientSizeChanged(GameWindowHandle)
               .Subscribe(Resize);

            Touch.RxPointerPressed()
                .Select(_ => GameWindowHandle.GetClientSize())
                .Subscribe(clientArea => ResetWindowOriginalObservableRegion(Hwnd, clientArea.Width, clientArea.Height));

            Touch.RxPointerReleased()
                .Select(_ => Touch.GetRect())
                .Prepend(Touch.GetRect())
                .Subscribe(rect => SetWindowObservableRegion(Hwnd, Dpi, rect));
            // WARN: 部分游戏窗口，WinUI 透明子窗口会第一次捕获覆盖的画面，后续不会再重绘变化
            // 所以对于这个 WinUI 透明子窗口而言，ResetWindowOriginalObservableRegion 只能作为一个临时状态

            // FIXME: 多显示器不同 DPI 下，窗口跨显示器时，内部控件的大小不会自动感知改变

            var pointerPressedStream = Touch.RxPointerPressed();
            var pointerMovedStream = Touch.RxPointerMoved();
            var pointerReleasedStream = Touch.RxPointerReleased();

            var lastPosRealTime = new Point();

            var draggingStream =
                pointerPressedStream
                .Do(e => Touch.CapturePointer(e.Pointer))
                .SelectMany(pressedEvent =>
                {
                    var relativeMousePos = pressedEvent.GetCurrentPoint(Touch).Position;
                    var pointWhenMouseDown = pressedEvent.GetCurrentPoint(this.Content).Position;

                    return
                        pointerMovedStream
                        .TakeUntil(pointerReleasedStream
                                   .Do(e => Touch.ReleasePointerCapture(e.Pointer)))
                        .Select(movedEvent =>
                        {
                            var newPos = movedEvent.GetCurrentPoint(this.Content).Position.ToWarp() - relativeMousePos;
                            return (newPos, pointWhenMouseDown);
                        });
                });

            draggingStream
                .Subscribe(tuple =>
                {
                    // pointWhenMouseDown 和 lastPosRealTime 暂时不需要
                    var (newPos, pointWhenMouseDown) = tuple;
                    Touch.Margin = new Thickness(newPos.X, newPos.Y, 0, 0);
                    lastPosRealTime = newPos;
                });

            Touch.RightTapped += (s, e) => Close();
            // 避免 0xc000027b 错误
            Closed += (s, e) => PInvoke.SetParent(WindowExtensions.GetWindowHandle(this).ToHwnd(), nint.Zero.ToHwnd());
        }

        private double Dpi => HwndExtensions.GetDpiForWindow(Hwnd) / 96d;

        private static void ToggleWindowStyle(nint windowHandle, bool enable, WindowStyle style)
        {
            var modStyle = HwndExtensions.GetWindowStyle(windowHandle);
            modStyle = enable ? modStyle | style : modStyle & ~style;
            HwndExtensions.SetWindowStyle(windowHandle, modStyle);
        }

        /// <summary>
        /// 使用这个函数来调整 WinUI 窗口大小
        /// </summary>
        private void Resize(System.Drawing.Size size) =>
            // NOTE: 我没有观测到 Repaint 设置为 false 带来的任何负面影响
            PInvoke.MoveWindow(WindowExtensions.GetWindowHandle(this).ToHwnd(), 0, 0, size.Width, size.Height, false);

        /// <summary>
        /// 恢复窗口原始大小
        /// </summary>
        private static void ResetWindowOriginalObservableRegion(nint hwnd, int Width, int Height) =>
            SetWindowObservableRegion(hwnd, 1, new(0, 0, Width, Height));

        /// <summary>
        /// 设置窗口可以被观测和点击的区域，受 DPI 影响
        /// </summary>
        private static void SetWindowObservableRegion(nint hwnd, double factor, RectInt32 rect)
        {
            var r = rect.Multiply(factor);

            HRGN hRgn = PInvoke.CreateRectRgn(r.X, r.Y, r.X + r.Width, r.Y + r.Height);
            var result = PInvoke.SetWindowRgn(hwnd.ToHwnd(), hRgn, true);
            if (result == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}

#pragma warning disable IDE0130 // 命名空间与文件夹结构不匹配
namespace TouchChan.MainWindowExtensions
{
    public static class RectInt32Extensions
    {
        public static RectInt32 Multiply(this RectInt32 rect, double factor) =>
            new()
            {
                X = (int)(rect.X * factor),
                Y = (int)(rect.Y * factor),
                Width = (int)(rect.Width * factor),
                Height = (int)(rect.Height * factor)
            };
    }

    static class ObservableEventsExtensions
    {
        public static Observable<PointerRoutedEventArgs> RxPointerPressed(this Rectangle data) =>
            Observable.FromEvent<PointerEventHandler, PointerRoutedEventArgs>(
                h => (sender, e) => h(e),
                e => data.PointerPressed += e,
                e => data.PointerPressed -= e);

        public static Observable<PointerRoutedEventArgs> RxPointerMoved(this Rectangle data) =>
            Observable.FromEvent<PointerEventHandler, PointerRoutedEventArgs>(
                h => (sender, e) => h(e),
                e => data.PointerMoved += e,
                e => data.PointerMoved -= e);

        public static Observable<PointerRoutedEventArgs> RxPointerReleased(this Rectangle data) =>
            Observable.FromEvent<PointerEventHandler, PointerRoutedEventArgs>(
                h => (sender, e) => h(e),
                e => data.PointerReleased += e,
                e => data.PointerReleased -= e);

        public static Observable<WindowActivatedEventArgs> RxActivated(this Window data) =>
            Observable.FromEvent<TypedEventHandler<object, WindowActivatedEventArgs>, WindowActivatedEventArgs>(
                h => (sender, e) => h(e),
                e => data.Activated += e,
                e => data.Activated -= e);
    }

    static class MainWindowExtensions
    {
        public static RectInt32 GetRect(this Rectangle rectangle) =>
            new((int)rectangle.Margin.Left, (int)rectangle.Margin.Top, (int)rectangle.Width, (int)rectangle.Height);

        public static System.Drawing.Size GetClientSize(this nint hwnd)
        {
            PInvoke.GetClientRect(hwnd.ToHwnd(), out var rectClient);
            return rectClient.Size;
        }
    }
}


namespace Windows.Win32
{
    partial class PInvoke
    {
        internal unsafe static uint GetWindowThreadProcessId(HWND hwnd)
        {
            return GetWindowThreadProcessId(hwnd, null);
        }

        internal unsafe static uint GetWindowThreadProcessId(HWND hwnd, out uint lpdwProcessId)
        {
            fixed (uint* _lpdwProcessId = &lpdwProcessId)
                return GetWindowThreadProcessId(hwnd, _lpdwProcessId);
        }
    }
}