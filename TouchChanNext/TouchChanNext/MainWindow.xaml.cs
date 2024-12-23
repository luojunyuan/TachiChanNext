using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using R3;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using WinUIEx;

namespace TouchChan
{
    using TouchChan.MainWindowExtensions;

    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private readonly nint GameWindowHandle;
        private readonly nint Hwnd;

        public MainWindow(nint gameWindowHandle)
        {
            Hwnd = this.GetWindowHandle();
            GameWindowHandle = gameWindowHandle;

            // 设置需要的 WinUI3 窗口样式
            this.InitializeComponent();
            HwndExtensions.ToggleWindowStyle(Hwnd, false, WindowStyle.TiledWindow);
            this.SystemBackdrop = new TransparentTintBackdrop();
            this.AppWindow.IsShownInSwitchers = false;
            // 设置需要的 Win32 窗口样式
            RemovePopupAddChildStyle(Hwnd);
            PInvoke.SetParent(Hwnd.ToHwnd(), GameWindowHandle.ToHwnd());

            // 绑定窗口大小
            // TODO: Set and bind Width Height Once. 
            PInvoke.GetClientRect(GameWindowHandle.ToHwnd(), out var rectClient);
            PInvoke.SetWindowPos(Hwnd.ToHwnd(), HWND.Null, 0, 0,
                rectClient.Width, rectClient.Height, SET_WINDOW_POS_FLAGS.SWP_NOZORDER);

            // 根据触摸事件设置窗口透明度
            Touch.RxPointerPressed()
                .Select(_ => GameWindowHandle.GetClientSize())
                .Subscribe(clientArea => ResetWindowOriginalSize(Hwnd, clientArea.Width, clientArea.Height));

            Touch.RxPointerReleased()
                .Select(_ => Touch.GetMargin())
                .Prepend(Touch.GetMargin())
                .Subscribe(t => MakeWindowClickThrough(Hwnd, t.Left, t.Top, t.Width, t.Height));

            // WinUI3��Avalonia����WPF����ʵ�ֶ�ݣ�����ͨ�õĲ��֣��Լ�ҵ���UI�ķ���
            // 消灭状态，后续有别的需求，不会在原始事件函数上做修改。而是额外增加可观察流。
            var lastPosRealTime = new Point();
            bool isDragging = false;
            bool isMoving = false;
            Point relativeMousePos = default;
            Point pointWhenMouseDown = default;
            Point pointWhenMouseUp = default;

            Touch.RightTapped += (s, e) =>
            {
                PInvoke.SetParent(Hwnd.ToHwnd(), nint.Zero.ToHwnd());
                Close();
            };

            Touch.PointerPressed += (s, e) =>
            {
                if (s is not UIElement touch)
                    return;

                if (isMoving)
                    return;

                touch.CapturePointer(e.Pointer);

                isDragging = true;
                relativeMousePos = e.GetCurrentPoint(touch).Position;
                pointWhenMouseDown = e.GetCurrentPoint(this.Content).Position;
            };

            Touch.PointerMoved += (s, e) =>
            {
                if (s is not UIElement touch)
                    return;

                if (!isDragging)
                    return;

                isMoving = true;
                // Max mouse event message frequency: 125 fps, dirty react 250 (2*fps)
                //var newPos = e.GetCurrentPoint(this.Content).Position - relativeMousePos;
                var newPos = e.GetCurrentPoint(this.Content).Position.ToWarp() - relativeMousePos;

                Touch.Margin = new Thickness(newPos.X, newPos.Y, 0, 0);

                lastPosRealTime = newPos;

                // if mouse go out of the edge
                //if (newPos.X < -_oneThirdDistance || newPos.Y < -_oneThirdDistance ||
                //    newPos.X > mainWindow.ActualWidth - _twoThirdDistance ||
                //    newPos.Y > mainWindow.ActualHeight - _twoThirdDistance)
                //{
                //    RaiseMouseReleasedEventInCode(this);
                //}
            };

            Touch.PointerReleased += (s, e) =>
            {
                if (s is not Rectangle touch)
                    return;

                isDragging = false;
                pointWhenMouseUp = e.GetCurrentPoint(this.Content).Position;

                //if (isMoving && pointWhenMouseUp != pointWhenMouseDown)
                //{
                //    WhenMouseReleased(mainWindow, TouchPosTransform.X, TouchPosTransform.Y);
                //}

                isMoving = false;
                touch.ReleasePointerCapture(e.Pointer);
            };
        }

        private static void RemovePopupAddChildStyle(nint hwnd)
        {
            var style = HwndExtensions.GetWindowStyle(hwnd);
            style = style & ~WindowStyle.Popup | WindowStyle.Child;
            HwndExtensions.SetWindowStyle(hwnd, style);
        }

        /// <summary>
        /// 恢复窗口原始大小
        /// </summary>
        private static void ResetWindowOriginalSize(nint hwnd, int Width, int Height) =>
            MakeWindowClickThrough(hwnd, 0, 0, Width, Height);

        /// <summary>
        /// 设置窗口可以被观测和点击的区域
        /// </summary>
        private static void MakeWindowClickThrough(nint hwnd, int left, int top, int width, int height)
        {
            HRGN hRgn = PInvoke.CreateRectRgn(left, top, left + width, top + height);
            var result = PInvoke.SetWindowRgn(hwnd.ToHwnd(), hRgn, true);
            if (result == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}

#pragma warning disable IDE0130 // 命名空间与文件夹结构不匹配
namespace TouchChan.MainWindowExtensions
{
    static class ObservableEventsExtensions
    {
        public static Observable<PointerRoutedEventArgs> RxPointerPressed(this Rectangle data) =>
            Observable.FromEvent<PointerEventHandler, PointerRoutedEventArgs>(
                h => (sender, e) => h(e),
                e => data.PointerPressed += e,
                e => data.PointerPressed -= e);

        public static Observable<PointerRoutedEventArgs> RxPointerReleased(this Rectangle data) =>
            Observable.FromEvent<PointerEventHandler, PointerRoutedEventArgs>(
                h => (sender, e) => h(e),
                e => data.PointerReleased += e,
                e => data.PointerReleased -= e);
    }

    static class MainWindowExtensions
    {
        public static (int Left, int Top, int Width, int Height) GetMargin(this Rectangle rectangle) =>
            ((int)rectangle.Margin.Left, (int)rectangle.Margin.Top, (int)rectangle.Width, (int)rectangle.Height);

        public static (int Width, int Height) GetClientSize(this nint hwnd)
        {
            PInvoke.GetClientRect(hwnd.ToHwnd(), out var rectClient);
            return (rectClient.Width, rectClient.Height);
        }
    }
}