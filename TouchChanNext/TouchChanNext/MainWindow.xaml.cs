using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using R3;
using System;
using System.Linq;
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
    using System.Diagnostics.Contracts;
    using System.Numerics;
    using System.Xml.Schema;
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

            var pointerPressedStream = Touch.RxPointerPressed();
            var pointerMovedStream = Touch.RxPointerMoved();
            var pointerReleasedStream = Touch.RxPointerReleased();

            pointerPressedStream
                .Select(_ => Hwnd.GetClientSize())
                .Subscribe(clientArea => ResetWindowOriginalObservableRegion(Hwnd, clientArea.Width, clientArea.Height));

            smoothMoveCompletedStream
                .Select(_ => Touch.GetRect())
                .Prepend(Touch.GetRect())
                .Subscribe(rect => SetWindowObservableRegion(Hwnd, Dpi, rect));
            // FIXME: 要确定释放过程中能不能被点击抓住。

            pointerReleasedStream.Subscribe(pointer =>
            {
                // 要计算释放时候的位置，按照规则确定是竖向移动还是横向移动，移动多远设置到 Animation.To
                // 有三种情况，横向移动，竖向移动，边角移动。确定一种移动方式后，就可以确定设置哪个动画或者两者都需要设置
                var distanceToOrigin = pointer.GetCurrentPoint(this.Content).Position.ToWarp();
                var distanceToElement = pointer.GetCurrentPoint(Touch).Position;
                var touchPos = distanceToOrigin - distanceToElement;


                [Pure]
                static Point CalculateTouchFinalPosition(Size container, Point initPos, double touchSize)
                {
                    const double TouchSpace = 2;

                    var horizontalCenterLine = container.Width / 2;
                    var right = container.Width - initPos.X - touchSize;
                    var bottom = container.Height - initPos.Y - touchSize;

                    var halfTouch = touchSize / 2;
                    var twoThirdTouch = touchSize / 3 * 2;

                    var centerX = initPos.X + halfTouch;

                    bool VCloseTo(double distance) => distance < twoThirdTouch;
                    bool HCloseTo(double distance) => distance < halfTouch;

                    // 1 假设在中线上
                    // 2 假设在边角上
                    // 3 假设在边缘上
                    // 4 假设超出边缘
                    // TODO 把这个想清楚，保证覆盖所有情况，测试看看编译器是否会提示

                    // 这个确定是可以覆盖完的

                    var value1 = 123d;
                    var value2 = 321d;
                    var test6 = (value1, value2) switch
                    {
                        (0, 0) => 1,
                        (>0, 0) => 2,
                        (<0, 0) => 3,
                        (0, >0) => 5,
                        (>0, >0) => 6,
                        (<0, >0) => 7,
                        (0, <0) => 9,
                        (>0, <0) => 10,
                        (<0, <0) => 11,
                        (double.NaN, 0) => 4,
                        (0, double.NaN) => 13,
                        (>0, double.NaN) => 14,
                        (<0, double.NaN) => 15,
                        (double.NaN, >0) => 8,
                        (double.NaN, <0) => 12,
                        (double.NaN, double.NaN) => 16,
                    };

                    var _ = (value1, value2) switch
                    {
                        (0, 0) => 1,
                        (>0, 0) => 2,
                        (<0, 0) => 3,
                        (0, >0) => 5,
                        (>0, >0) => 6,
                        (<0, >0) => 7,
                        (0, <0) => 9,
                        (>0, <0) => 10,
                        (<0, <0) => 11,
                        (double.NaN, _) => 4,
                        (_, double.NaN) => 13,
                    };

                    _ = (value1, value2) switch
                    {
                        (>0, 0) => 2,
                        (<=0, 0) => 3,
                        (0, >0) => 4,
                        (0, <=0) => 5,
                        (>0, >0) => 6,
                        (>0, <=0) => 7,
                        (<=0, >0) => 8,
                        (<=0, <=0) => 9,
                        var (x, y) when double.IsNaN(x) || double.IsNaN(y) => new(),

                        //(double.NaN, _) => 10,
                        //(_, double.NaN) => 11,
                    };

                    // >= or <
                    // < or >=
                    var h = (int)halfTouch;

                    var result = ((int)initPos.X, (int)initPos.Y) switch
                    {
                        (>=h, >0) => "Both are greater than zero",
                        (>0, <=0) => "First is greater than zero, second is less than or equal to zero",
                        (<=0, >0) => "First is less than or equal to zero, second is greater than zero",
                        (<=0, <=0) => "Both are less than or equal to zero",
                    };

                    var a = ((int)initPos.X, (int)initPos.Y) switch
                    {
                        // 我应该在前面就匹配
                        var (x, y) when HCloseTo(x)     && VCloseTo(y) => new Point(TouchSpace, TouchSpace), // 左上
                        var (x, y) when HCloseTo(right) && VCloseTo(y) => new Point(container.Width - touchSize - TouchSpace, TouchSpace), // 右上
                        var (x, y) when HCloseTo(x)     && VCloseTo(bottom) => new Point(TouchSpace, container.Height - touchSize - TouchSpace), // 左下
                        var (x, y) when HCloseTo(right) && VCloseTo(bottom) => new Point(container.Width - touchSize - TouchSpace, container.Height - touchSize - TouchSpace), // 右下
                        var (_, y) when VCloseTo(y) => new Point(initPos.X, TouchSpace), // 上
                        var (x, _) when centerX < horizontalCenterLine => new Point(TouchSpace, initPos.Y), // 左
                        var (x, _) when centerX >= horizontalCenterLine => new Point(container.Width - touchSize - TouchSpace, initPos.Y), // 右
                        var (_, y) when VCloseTo(bottom) => new Point(initPos.X, container.Height - touchSize - TouchSpace), // 下
                    };

                    return (initPos.X, initPos.Y) switch
                    {
                        // 我应该在前面就匹配
                        var (x, y) when HCloseTo(x)     && VCloseTo(y) => new Point(TouchSpace, TouchSpace), // 左上
                        var (x, y) when HCloseTo(right) && VCloseTo(y) => new Point(container.Width - touchSize - TouchSpace, TouchSpace), // 右上
                        var (x, y) when HCloseTo(x)     && VCloseTo(bottom) => new Point(TouchSpace, container.Height - touchSize - TouchSpace), // 左下
                        var (x, y) when HCloseTo(right) && VCloseTo(bottom) => new Point(container.Width - touchSize - TouchSpace, container.Height - touchSize - TouchSpace), // 右下
                        var (_, y) when VCloseTo(y) => new Point(initPos.X, TouchSpace), // 上
                        var (x, _) when centerX < horizontalCenterLine => new Point(TouchSpace, initPos.Y), // 左
                        var (x, _) when centerX >= horizontalCenterLine => new Point(container.Width - touchSize - TouchSpace, initPos.Y), // 右
                        var (_, y) when VCloseTo(bottom) => new Point(initPos.X, container.Height - touchSize - TouchSpace), // 下

                        // 这个不可能发生，应该先使用 int 匹配保证所有可能性
                        var (_, _) => new(initPos.X, initPos.Y),
                    };
                }

                //var stopPos = CalculateTouchFinalPosition(this.Content.ActualSize.ToSize(), touchPos, Touch.Width);
                //(translateXAnimation.To, translateYAnimation.To) = (stopPos.X, stopPos.Y);
                translationStoryboard.Begin();

                //Touch.TranslationTransition = new Vector3Transition
                //{
                //    Duration = System.TimeSpan.FromMilliseconds(400),
                //    //Components = Vector3TransitionComponents.X,
                //};
                // 1. 创建一个 Vector3Animation 作用在 Touch.Translation 上
                // 2. 创建俩个 DoubleAnimation 作用在 Touch.RenderTransform.TranslateTransform 上
                //var translationAnime = new Vector3Animation();
                //translationAnime.Duration = TouchReleaseToEdgeDuration;
                //translationAnime.To = "100, 100, 0";
                // https://stackoverflow.com/questions/74330352/how-do-i-animate-a-uwp-control-xy-position-in-code

                //Touch.RenderTransform = new Microsoft.UI.Xaml.Media.TranslateTransform { X = newPos.X, Y = newPos.Y };
                //System.Diagnostics.Debug.WriteLine(TouchTransform.X);
            });

            var draggingStream =
                pointerPressedStream
                .Do(e => Touch.CapturePointer(e.Pointer))
                .SelectMany(pressedEvent =>
                {
                    // Origin   Element
                    // *--------*--*------
                    //             Pointer 
                    var distanceToElement = pressedEvent.GetCurrentPoint(Touch).Position;

                    return
                        pointerMovedStream
                        .TakeUntil(pointerReleasedStream
                                   .Do(e => Touch.ReleasePointerCapture(e.Pointer)))
                        .Select(movedEvent =>
                        {
                            var distanceToOrigin = movedEvent.GetCurrentPoint(this.Content).Position.ToWarp();
                            return distanceToOrigin - distanceToElement;
                        });
                });

            draggingStream
                // 在拖拽过程中检查边界？是副作用吗该如何理解？
                .Subscribe(newPos =>
                {
                    TouchTransform.X = newPos.X;
                    TouchTransform.Y = newPos.Y;
                });

            Touch.RightTapped += (s, e) => Close();

            // QUES: 启动后，获得焦点无法放在最前面？是什么原因，需要重新激活焦点。今后再检查整个程序与窗口启动方式
        }

        private static readonly TimeSpan TouchReleaseToEdgeDuration = TimeSpan.FromMilliseconds(300);

        private static readonly Storyboard TranslateTouchStoryboard = new();

        private static readonly DoubleAnimation TranslateXAnimation = new() { Duration = TouchReleaseToEdgeDuration };
        private static readonly DoubleAnimation TranslateYAnimation = new() { Duration = TouchReleaseToEdgeDuration };

        private static void SmoothMoveAnimation(double left, double top)
        {
            if (TranslateXAnimation.To == left)
            {
                TranslateYAnimation.To = top;
            }
            else if (TranslateYAnimation.To == top)
            {
                TranslateXAnimation.To = left;
            }
            else
            {
                TranslateXAnimation.To = left;
                TranslateYAnimation.To = top;
            }
            TranslateTouchStoryboard.Begin();
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
