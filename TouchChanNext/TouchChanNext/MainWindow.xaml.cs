using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using R3;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using WinRT.Interop;
using System.Linq;

namespace TouchChan
{
    using System;
    using TouchChan.MainWindowExtensions;
    using ExtendedWindowStyle = WinUIEx.ExtendedWindowStyle;
    using HwndExtensions = WinUIEx.HwndExtensions;
    using WindowStyle = WinUIEx.WindowStyle;

    public sealed partial class MainWindow : Window
    {
        private readonly nint GameWindowHandle;
        private readonly nint Hwnd;

        public MainWindow(nint gameWindowHandle)
        {
            GameWindowHandle = gameWindowHandle;
            Hwnd = WindowNative.GetWindowHandle(this);

            this.InitializeComponent();
            this.SystemBackdrop = new WinUIEx.TransparentTintBackdrop();
            ToggleWindowStyle(false, WindowStyle.TiledWindow);
            ToggleWindowStyle(false, WindowStyle.Popup);
            ToggleWindowStyle(true, WindowStyle.Child);
            ToggleWindowExStyle(true, ExtendedWindowStyle.Layered);
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
            this.Closed += (_, _) => PInvoke.SetParent(Hwnd.ToHwnd(), nint.Zero.ToHwnd());
        }

        private double Dpi => HwndExtensions.GetDpiForWindow(Hwnd) / 96d;

        private void ToggleWindowStyle(bool enable, WindowStyle style)
        {
            var modStyle = HwndExtensions.GetWindowStyle(Hwnd);
            modStyle = enable ? modStyle | style : modStyle & ~style;
            HwndExtensions.SetWindowStyle(Hwnd, modStyle);
        }
        private void ToggleWindowExStyle(bool enable, ExtendedWindowStyle style)
        {
            var modStyle = HwndExtensions.GetExtendedWindowStyle(Hwnd);
            modStyle = enable ? modStyle | style : modStyle & ~style;
            HwndExtensions.SetExtendedWindowStyle(Hwnd, modStyle);
        }

        /// <summary>
        /// 使用这个函数来调整 WinUI 窗口大小
        /// </summary>
        private void Resize(System.Drawing.Size size) =>
            // NOTE: 我没有观测到 Repaint 设置为 false 带来的任何负面影响
            PInvoke.MoveWindow(WindowNative.GetWindowHandle(this).ToHwnd(), 0, 0, size.Width, size.Height, false);

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
            _ = PInvoke.SetWindowRgn(hwnd.ToHwnd(), hRgn, true);
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