using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using R3;
using System;
using System.Drawing;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using WinRT.Interop;
using ExtendedWindowStyle = WinUIEx.ExtendedWindowStyle;
using HwndExtensions = WinUIEx.HwndExtensions;
using WindowStyle = WinUIEx.WindowStyle;

namespace TouchChan
{
    public sealed partial class MainWindow : Window
    {
        private readonly nint GameWindowHandle;
        private readonly nint Hwnd;

        public double DpiScale => HwndExtensions.GetDpiForWindow(Hwnd) / 96d;

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
            // NOTE: 设置为子窗口后，this.AppWindow 不再可靠

            GameWindowHooker.ClientSizeChanged(GameWindowHandle)
               .Subscribe(ResizeClient);

            Touch.ResetWindowObservable = size => ResetWindowOriginalObservableRegion(size.Width, size.Height);
            Touch.SetWindowObservable = rect => SetWindowObservableRegion(DpiScale, rect);
            Touch.RightTapped += (s, e) => Close();
            // QUES: 启动后，获得焦点无法放在最前面？是什么原因，需要重新激活焦点。今后再检查整个程序与窗口启动方式
        }

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
        /// 替代 this.AppWindow.ResizeClient()，使用这个函数来调整 WinUI 窗口大小
        /// </summary>
        /// <remarks>
        /// NOTE: 我没有观测到 Repaint 设置为 false 带来的任何负面影响
        /// </remarks>
        private void ResizeClient(Size size) =>
            PInvoke.MoveWindow(Hwnd.ToHwnd(), 0, 0, size.Width, size.Height, false);

        /// <summary>
        /// 恢复窗口原始可观测区域
        /// </summary>
        private void ResetWindowOriginalObservableRegion(int Width, int Height) =>
            SetWindowObservableRegion(1, new(0, 0, Width, Height));

        /// <summary>
        /// 设置窗口可以被观测和点击的区域，受 DPI 影响
        /// </summary>
        private void SetWindowObservableRegion(double factor, Rectangle rect)
        {
            var r = rect.Multiply(factor);

            HRGN hRgn = PInvoke.CreateRectRgn(r.X, r.Y, r.X + r.Width, r.Y + r.Height);
            _ = PInvoke.SetWindowRgn(Hwnd.ToHwnd(), hRgn, true);
        }
    }
}

namespace TouchChan
{
    static partial class ObservableEventsExtensions
    {
        public static Observable<object> RxCompleted(this Storyboard data) =>
            Observable.FromEvent<EventHandler<object>, object>(
                h => (sender, e) => h(e),
                e => data.Completed += e,
                e => data.Completed -= e);

        public static Observable<PointerRoutedEventArgs> RxPointerPressed(this FrameworkElement data) =>
            Observable.FromEvent<PointerEventHandler, PointerRoutedEventArgs>(
                h => (sender, e) => h(e),
                e => data.PointerPressed += e,
                e => data.PointerPressed -= e);

        public static Observable<PointerRoutedEventArgs> RxPointerMoved(this FrameworkElement data) =>
            Observable.FromEvent<PointerEventHandler, PointerRoutedEventArgs>(
                h => (sender, e) => h(e),
                e => data.PointerMoved += e,
                e => data.PointerMoved -= e);

        public static Observable<PointerRoutedEventArgs> RxPointerReleased(this FrameworkElement data) =>
            Observable.FromEvent<PointerEventHandler, PointerRoutedEventArgs>(
                h => (sender, e) => h(e),
                e => data.PointerReleased += e,
                e => data.PointerReleased -= e);

        public static Observable<RoutedEventArgs> RxLoaded(this UserControl data) =>
            Observable.FromEvent<RoutedEventHandler, RoutedEventArgs>(
                h => (sender, e) => h(e),
                e => data.Loaded += e,
                e => data.Loaded -= e);
    }

    static class VisualTreeHelperExtensions
    {
        public static T? FindParent<T>(this DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null)
                return null;

            return parentObject is T parent ? parent : FindParent<T>(parentObject);
        }
    }
}
