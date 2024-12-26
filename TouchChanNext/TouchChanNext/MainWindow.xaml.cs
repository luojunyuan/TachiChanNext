using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using R3;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using WinRT.Interop;
using ExtendedWindowStyle = WinUIEx.ExtendedWindowStyle;
using HwndExtensions = WinUIEx.HwndExtensions;
using WindowStyle = WinUIEx.WindowStyle;

namespace TouchChan
{
    using TouchChan.MainWindowExtensions;

    public sealed partial class MainWindow : Window
    {
        private readonly nint GameWindowHandle;
        private readonly nint Hwnd;

        public double Dpi => HwndExtensions.GetDpiForWindow(Hwnd) / 96d;

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
               .Subscribe(Resize);

            var translationStoryboard = new Storyboard();
            var releaseToEdgeDuration = TimeSpan.FromMilliseconds(200);
            var translateXAnimation = new DoubleAnimation { Duration = releaseToEdgeDuration };
            var translateYAnimation = new DoubleAnimation { Duration = releaseToEdgeDuration };
            translateXAnimation.To = null;
            translateYAnimation.To = null;
            AnimationTool.BindingAnimation(translationStoryboard, translateXAnimation, TouchTransform, AnimationTool.XProperty);
            AnimationTool.BindingAnimation(translationStoryboard, translateYAnimation, TouchTransform, AnimationTool.YProperty);

            var smoothMoveCompletedStream = translationStoryboard.RxCompleted();

            // FIXME: 多显示器不同 DPI 下，窗口跨显示器时，内部控件的大小不会自动感知改变

            var raisePointerReleasedSubject = new Subject<PointerRoutedEventArgs>();

            var pointerPressedStream = Touch.RxPointerPressed();
            var pointerMovedStream = Touch.RxPointerMoved();
            var pointerReleasedStream = Touch.RxPointerReleased().Merge(raisePointerReleasedSubject);

            // FIXME: 连续点击两下按住，第一下的Release时间在200ms之后（也就是第二次按住后）触发了
            pointerPressedStream
                .Select(_ => Hwnd.GetClientSize())
                .Subscribe(clientArea => ResetWindowOriginalObservableRegion(Hwnd, clientArea.Width, clientArea.Height));

            smoothMoveCompletedStream
                .Select(_ => Touch.GetRect())
                .Prepend(Touch.GetRect())
                .Subscribe(rect => SetWindowObservableRegion(Hwnd, Dpi, rect));
            // FIXME: 要确定释放过程中能不能被点击抓住。

            // 增加一个 dragOutOfBoundsStream，或者还是按照原本的做法，释放一个 Release 事件，看看能不能做，我觉得都可以
            pointerReleasedStream
                .Select(pointer =>
                {
                    var distanceToOrigin = pointer.GetCurrentPoint(this.Content).Position.ToWarp();
                    var distanceToElement = pointer.GetCurrentPoint(Touch).Position;
                    var touchPos = distanceToOrigin - distanceToElement;
                    return CalculateTouchFinalPosition(this.Content.ActualSize.ToSize(), touchPos, Touch.Width);
                })
                .Subscribe(stopPos =>
                {
                    (translateXAnimation.To, translateYAnimation.To) = (stopPos.X, stopPos.Y);
                    translationStoryboard.Begin();
                });

            PointerRoutedEventArgs? moveEventInReal = null;

            var draggingStream =
                pointerPressedStream
                .Do(e => Touch.CapturePointer(e.Pointer))
                .SelectMany(pressedEvent =>
                {
                    // Origin   Element
                    // *--------*--*------
                    //             Pointer 
                    var distanceToElement = pressedEvent.GetCurrentPoint(Touch).Position;

                    // QUES: 移动防抖？释放的时候老是会动鼠标
                    return
                        pointerMovedStream
                        .TakeUntil(pointerReleasedStream
                                   .Do(e => Touch.ReleasePointerCapture(e.Pointer)))
                        .Select(movedEvent =>
                        {
                            moveEventInReal = movedEvent;
                            var distanceToOrigin = movedEvent.GetCurrentPoint(this.Content).Position.ToWarp();
                            var delta = distanceToOrigin - distanceToElement;
                            return new { Delta = delta, MovedEvent = movedEvent };
                        });
                });

            draggingStream
                .Subscribe(item =>
                {
                    var newPos = item.Delta;
                    TouchTransform.X = newPos.X;
                    TouchTransform.Y = newPos.Y;
                });


            static bool IsBeyondBoundary(Point newPos, double touchSize, System.Drawing.Size container)
            {
                // TODO: 明确一下窗口中用的坐标和dpi关系
                // win32设置时候的 size 与dpi关系，窗口中所有坐标计算，该用原始的还是应用过dpi后的？
                // dpi 计算后的，大小数值缩小了一半，应该是用原始的才对。需要从控件开始计算起。
                var oneThirdDistance = touchSize / 3;
                var twoThirdDistance = oneThirdDistance * 2;

                if (newPos.X < -oneThirdDistance ||
                    newPos.Y < -oneThirdDistance ||
                    newPos.X > container.Width - twoThirdDistance ||
                    newPos.Y > container.Height - twoThirdDistance)
                {
                    return true;
                }
                return false;
            }

            var boundaryExceededStream =
                draggingStream
                .Where(item => IsBeyondBoundary(item.Delta, Touch.Width / Dpi, Hwnd.GetClientSize()))
                .Select(item => item.MovedEvent);

            boundaryExceededStream
                .Subscribe(raisePointerReleasedSubject.OnNext);

            Touch.RightTapped += (s, e) => Close();
            // QUES: 启动后，获得焦点无法放在最前面？是什么原因，需要重新激活焦点。今后再检查整个程序与窗口启动方式
        }

        [Pure]
        private static Point CalculateTouchFinalPosition(Size container, Point initPos, double touchSize)
        {
            const double TouchSpace = 2;

            var xMidline = container.Width / 2;
            var right = container.Width - initPos.X - touchSize;
            var bottom = container.Height - initPos.Y - touchSize;

            var hSnapLimit = touchSize / 2;
            var vSnapLimit = touchSize / 3 * 2;

            var centerToLeft = initPos.X + hSnapLimit;

            bool VCloseTo(double distance) => distance < vSnapLimit;
            bool HCloseTo(double distance) => distance < hSnapLimit;

            double AlignToRight() => container.Width - touchSize - TouchSpace;
            double AlignToBottom() => container.Height - touchSize - TouchSpace;

            var left = initPos.X;
            var top = initPos.Y;

            return
                HCloseTo(left)  && VCloseTo(top)    ? new Point(TouchSpace, TouchSpace) :
                HCloseTo(right) && VCloseTo(top)    ? new Point(AlignToRight(), TouchSpace) :
                HCloseTo(left)  && VCloseTo(bottom) ? new Point(TouchSpace, AlignToBottom()) :
                HCloseTo(right) && VCloseTo(bottom) ? new Point(AlignToRight(), AlignToBottom()) :
                                   VCloseTo(top)    ? new Point(left, TouchSpace) :
                                   VCloseTo(bottom) ? new Point(left, AlignToBottom()) :
                centerToLeft < xMidline             ? new Point(TouchSpace, top) :
             /* centerToLeft >= xMidline */           new Point(AlignToRight(), top);
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

    record TouchDockAnchor(TouchCorner Corner, double Scale, Point Position);

    public enum TouchCorner
    {
        Left,
        Top,
        Right,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
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
        public static Observable<object> RxCompleted(this Storyboard data) =>
            Observable.FromEvent<EventHandler<object>, object>(
                h => (sender, e) => h(e),
                e => data.Completed += e,
                e => data.Completed -= e);

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

    // 带有业务逻辑的代码
    static class MainWindowExtensions
    {
        public static RectInt32 GetRect(this Rectangle rectangle) =>
            new((int)((TranslateTransform)rectangle.RenderTransform).X, (int)((TranslateTransform)rectangle.RenderTransform).Y, (int)rectangle.Width, (int)rectangle.Height);

        public static System.Drawing.Size GetClientSize(this nint hwnd)
        {
            PInvoke.GetClientRect(hwnd.ToHwnd(), out var rectClient);
            return rectClient.Size;
        }
    }
}
