using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using R3;
using R3.ObservableEvents;
using Windows.Foundation;

namespace TouchChan.WinUI;

public sealed partial class TouchControl
{
    private readonly ObservableAsPropertyHelper<TouchDockAnchor> _currentDockHelper;

    public TouchDockAnchor CurrentDock => _currentDockHelper.Value;
}

public sealed partial class TouchControl : UserControl
{
    private readonly static TimeSpan ReleaseToEdgeDuration = TimeSpan.FromMilliseconds(2000);
    private readonly Storyboard TranslationStoryboard = new();
    private readonly DoubleAnimation TranslateXAnimation = new() { Duration = ReleaseToEdgeDuration };
    private readonly DoubleAnimation TranslateYAnimation = new() { Duration = ReleaseToEdgeDuration };

    // WAS Shit 5: Xaml Code-Behind 中 require 或 prop init 会生成错误的代码 #8723
    public Action<Size>? ResetWindowObservable { get; set; }

    public Action<Rect>? SetWindowObservable { get; set; }

    public Action? RestoreFocus { get; set; }

    public Subject<Unit> OnWindowBounded { get; private set; } = new();

    // WAS Shit 6: DPI 改变后，XamlRoot.RasterizationScale 永远是启动时候的值
    private double DpiScale => this.XamlRoot.RasterizationScale;

    private Subject<Unit> OnMenuClosed { get; } = new();

    public TouchControl()
    {
        this.InitializeComponent();

        // NOTE: TouchControl 的大小是跟随窗口的，而 this.Touch 才是真正的控件大小
        FrameworkElement container = this;

        TouchSubscribe(container, out _currentDockHelper);
        TouchDockSubscribe(container);

        // TODO: Menu的大小
        // 以前的做法里，menu最大不能超过高度的一个间隔距离 newGameWindowHeight > EndureEdgeHeight + MaxSize
        // 如果超过了 就设置 MaxSize  MaxSize = touchSize * 5;
        //
        // 现在我感觉不太好

        Touch.Clicked()
            .Do(_ => (this.Parent as FrameworkElement)?.IsHitTestVisible = false)
            .Do(_ => RestoreFocus?.Invoke())
            .Subscribe(_ =>
            {
                Menu.Visibility = Visibility.Visible;
                Touch.Visibility = Visibility.Collapsed;

                var factor = GetTouchDockRect().Width / 300;
                ScaleXAnimation.From = factor;
                ScaleYAnimation.From = factor;
                var mc = GetTouchDockRect().Width / 2;
                //mc = 0;
                var tmp = ToCenterOrigin(new(TouchTransform.X + mc, TouchTransform.Y + mc), _window);
                (_xAnimation.From, _yAnimation.From) = (tmp.X, tmp.Y);
                ScaleStoryboard.Begin();
            });

        var whenClickBlackArea =
            this.Events().Loaded
            .SelectMany(_ => (this.Parent as FrameworkElement).Events().PointerPressed)
            .Where(e => e.OriginalSource is FrameworkElement { Name: "Root" });

        var isReversing = false;

        // 1 Menu 默认在中央
        // 此时 Translate(0, 0) 默认值应该是在窗口正中央
        // 假设小圆点处于窗口的左上角停留，小圆点的 Translate()

        // 2 重新定位 Menu To 的位置，并且考虑 scale 进行计算

        whenClickBlackArea
            .Do(_ => (this.Parent as FrameworkElement)?.IsHitTestVisible = false)
            .Subscribe(_ =>
            {
                ReverseStoryboard();
                ScaleStoryboard.Begin();
                isReversing = true;
            });

        ScaleStoryboard.Events().Completed
            .Do(_ => (this.Parent as FrameworkElement)?.IsHitTestVisible = true)
            .Where(_ => isReversing == true)
            .Subscribe(_ =>
        {
            OnMenuClosed.OnNext(Unit.Default);
            Touch.Visibility = Visibility.Visible;
            Menu.Visibility = Visibility.Collapsed;
            isReversing = false;
            (_xAnimation.To, _yAnimation.To) = (default, default);
            (ScaleXAnimation.To, ScaleYAnimation.To) = (default, default);
        });

        TranslationStoryboard.BindingAnimation(TranslateXAnimation, TouchTransform, nameof(TranslateTransform.X));
        TranslationStoryboard.BindingAnimation(TranslateYAnimation, TouchTransform, nameof(TranslateTransform.Y));

        ScaleStoryboard.BindingAnimation(ScaleXAnimation, MenuScale, "ScaleX");
        ScaleStoryboard.BindingAnimation(ScaleYAnimation, MenuScale, "ScaleY");
        ScaleStoryboard.BindingAnimation(_xAnimation, MenuTranslate, nameof(TranslateTransform.X));
        ScaleStoryboard.BindingAnimation(_yAnimation, MenuTranslate, nameof(TranslateTransform.Y));
        //ScaleStoryboard.BindingAnimation(widthAnimation, Menu, "Width");
        //ScaleStoryboard.BindingAnimation(heightAnimation, Menu, "Height");

        //ScaleStoryboard.Begin();
        //ScaleStoryboard.Stop();
    }
    // 转换坐标系，把以窗口左上角的坐标系转化为窗口中心的坐标系的点
    static Point ToCenterOrigin(Point pt, Size rect)
    {
        double centerX = 0 + rect.Width / 2;
        double centerY = 0 + rect.Height / 2;
        return new Point(pt.X - centerX, pt.Y - centerY);
    }

    private void ReverseStoryboard()
    {
        static void SwapAnimation(DoubleAnimation animation) =>
            (animation.To, animation.From) = (animation.From, animation.To);

        SwapAnimation(_xAnimation);
        SwapAnimation(_yAnimation);
        SwapAnimation(ScaleXAnimation);
        SwapAnimation(ScaleYAnimation);
    }

    private readonly Storyboard ScaleStoryboard = new();
    private static readonly PowerEase UnifiedPowerFunction = new() { EasingMode = EasingMode.EaseInOut };
    DoubleAnimation ScaleXAnimation = new DoubleAnimation
    {
        To = 1,
        Duration = new Duration(TimeSpan.FromMilliseconds(200)),
        EasingFunction = UnifiedPowerFunction,
    };

    DoubleAnimation ScaleYAnimation = new DoubleAnimation
    {
        To = 1,
        Duration = new Duration(TimeSpan.FromMilliseconds(200)),
        EasingFunction = UnifiedPowerFunction,
    };
    private DoubleAnimation _xAnimation = new DoubleAnimation()
    {
        Duration = new Duration(TimeSpan.FromMilliseconds(200)),
        EasingFunction = UnifiedPowerFunction,
    };
    private DoubleAnimation _yAnimation = new DoubleAnimation()
    {
        Duration = new Duration(TimeSpan.FromMilliseconds(200)),
        EasingFunction = UnifiedPowerFunction,
    };

    private DoubleAnimation widthAnimation = new DoubleAnimation()
    {
        From = 80,
        To = 300,
        Duration = new Duration(TimeSpan.FromMilliseconds(200)),
        EasingFunction = UnifiedPowerFunction,
    };
    private DoubleAnimation heightAnimation = new DoubleAnimation()
    {
        From = 80,
        To = 300,
        Duration = new Duration(TimeSpan.FromMilliseconds(200)),
        EasingFunction = UnifiedPowerFunction,
    };

    private void TouchSubscribe(FrameworkElement container,
        out ObservableAsPropertyHelper<TouchDockAnchor> dockObservable)
    {
        var moveAnimationEndedStream = TranslationStoryboard.Events().Completed;

        var raisePointerReleasedSubject = new Subject<PointerRoutedEventArgs>();

        var pointerPressedStream =
            Touch.Events().PointerPressed
            .Where(e => e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            .Do(e => Touch.CapturePointer(e.Pointer));
        var pointerMovedStream =
            Touch.Events().PointerMoved
            .Where(e => e.GetCurrentPoint(null).Properties.IsLeftButtonPressed);
        var pointerReleasedStream =
            Touch.Events().PointerReleased
            .Merge(raisePointerReleasedSubject)
            .Do(e => Touch.ReleasePointerCapture(e.Pointer));

        var dragStartedStream =
            pointerPressedStream
            .SelectMany(pressEvent =>
                pointerMovedStream
                .Skip(1)
                .Where(moveEvent =>
                {
                    var pressPos = pressEvent.GetPosition();
                    var movePos = moveEvent.GetPosition();
                    return pressPos != movePos;
                })
                .Take(1)
                .TakeUntil(pointerReleasedStream));

        var dragEndedStream =
            dragStartedStream
            .SelectMany(_ =>
                pointerReleasedStream
                .Take(1));

        // Timeline -->
        // |
        // |    Pressed suddenly released
        // | x -*|----->
        // |
        // |    Dragging
        // | x -*--*---------*------->
        // |       Released  Released
        // |      (by raise)
        // |                 ↓
        // |                 DragEnded
        // |                -*---------------------*|-->
        // |                 Start    Animation    End

        // Touch 的拖拽逻辑
        var draggingStream =
            dragStartedStream
            .SelectMany(pressedEvent =>
            {
                // Origin   Element
                // *--------*--*------
                //             Pointer 
                var distanceToElement = pressedEvent.GetPosition(Touch);

                return
                    pointerMovedStream
                    .TakeUntil(pointerReleasedStream)
                    .Select(movedEvent =>
                    {
                        var distanceToOrigin = movedEvent.GetPosition();
                        var delta = distanceToOrigin.Subtract(distanceToElement);

                        return new { Delta = delta, MovedEvent = movedEvent };
                    });
            });

        draggingStream
            .Select(item => item.Delta)
            .Subscribe(newPos =>
                (TouchTransform.X, TouchTransform.Y) = (newPos.X, newPos.Y));

        // Touch 拖动边界检测
        var boundaryExceededStream =
            draggingStream
            .Where(item => PositionCalculator.IsBeyondBoundary(
                item.Delta, Touch.Width, container.ActualSize.ToSize()))
            .Select(item => item.MovedEvent);

        boundaryExceededStream
            .Subscribe(raisePointerReleasedSubject.OnNext);

        var moveAnimationStartedStream = dragEndedStream;

        // Touch 边缘停靠动画
        moveAnimationStartedStream
            .Do(_ => Touch.IsHitTestVisible = false)
            .Select(pointer =>
            {
                var distanceToOrigin = pointer.GetPosition();
                var distanceToElement = pointer.GetPosition(Touch);
                var touchPos = distanceToOrigin.Subtract(distanceToElement);

                return PositionCalculator.CalculateTouchFinalPosition(
                    container.ActualSize.ToSize(), touchPos, (int)Touch.Width);
            })
            .Subscribe(stopPos =>
            {
                (TranslateXAnimation.To, TranslateYAnimation.To) = (stopPos.X, stopPos.Y);
                TranslationStoryboard.Begin();
            });
        // 苹果的小圆点不仅仅是依赖释放位置来判断动画和停靠，如果拖拽释放速度小于一个值，就是按照边缘动画恢复。
        // 如果拖拽释放速度大于一个值，还有加速度作用在控件上往速度方向飞出去

        // 回调设置容器窗口的可观察区域
        Touch.Clicked()
            .Merge(dragStartedStream.Select(_ => Unit.Default))
            .Select(_ => container.ActualSize.XDpi(DpiScale))
            .Subscribe(clientArea => ResetWindowObservable?.Invoke(clientArea));

        moveAnimationEndedStream.Select(_ => Unit.Default)
            .Do(_ => Touch.IsHitTestVisible = true)
            .Merge(OnMenuClosed)
            .Do(_ => RestoreFocus?.Invoke())
            .Select(_ => GetTouchDockRect().XDpi(DpiScale))
            .Subscribe();
            //.Subscribe(rect => SetWindowObservable?.Invoke(rect));

        // 调整按钮透明度
        AnimationTool.InitializeOpacityAnimations(Touch);
        pointerPressedStream.Select(_ => Unit.Default)
            // 打开 menu 或者 pointerReleasedStream 的时候保持透明度
            .Where(_ => !Touch.IsFullyOpaque())
            .Subscribe(_ => AnimationTool.FadeInOpacityStoryboard.Begin());

        OnWindowBounded
            .Merge(pointerReleasedStream.Select(_ => Unit.Default))
            .Merge(moveAnimationEndedStream.Select(_ => Unit.Default))
            .Merge(OnMenuClosed)
            .Select(_ =>
                Observable.Timer(OpacityFadeDelay)
                .TakeUntil(pointerPressedStream))
            .Switch()
            .ObserveOn(App.UISyncContext)
            .Subscribe(_ => AnimationTool.FadeOutOpacityStoryboard.Begin());

        // 小白点停留时的位置状态
        dockObservable =
            moveAnimationEndedStream.Select(_ =>
                PositionCalculator.GetLastTouchDockAnchor(container.ActualSize.ToSize(), GetTouchDockRect()))
            .Merge(container.Events().SizeChanged.Select(_ => CurrentDock))
            .Select(dock => PositionCalculator.TouchDockCornerRedirect(dock, container.ActualSize.ToSize(), Touch.Width))
            .ToProperty(initialValue: new(TouchCorner.Left, 0.5));
    }
    public readonly TimeSpan OpacityFadeDelay = TimeSpan.FromMilliseconds(4000);

    enum TouchOpacityState
    {
        /// <summary>
        /// 透明状态
        /// </summary>
        Stable,
        /// <summary>
        /// 激活的状态，会在之后自行转移，或者重入
        /// </summary>
        Active,
        /// <summary>
        /// 拖动或按住的状态
        /// </summary>
        Interacting,
    }

    Size _window = default;

    private void TouchDockSubscribe(FrameworkElement container)
    {
        // 这里产生 window size
        // 我可以确定 center 坐标系和 窗口坐标系的转化方法是必须需要的
        // 问题在于现在两个控件用的是不同坐标系，要不要统一成 center 坐标系
        // （先不改，留白等以后尝试修改再试试效果）
        container.Events().SizeChanged
            .Select(x => x.NewSize)
            .Do(啊 => _window = 啊)
            .Select(window => new
            {
                WindowSize = window,
                TouchDock = CurrentDock,
                TouchSize = window.Width < 600 ? 60 : 80
            })
            .Select(pair =>
            {
                return (PositionCalculator.CalculateTouchDockRect(pair.WindowSize, pair.TouchDock, pair.TouchSize), pair.WindowSize);
                //var centerPoint = ToCenterOrigin(new (rect.X, rect.Y), pair.WindowSize);
                //return new Rect(centerPoint.X, centerPoint.Y, rect.Width, rect.Height);
            })
            .Subscribe(a => SetTouchDockRect(a.Item1, a.WindowSize));
    }

    /// <summary>
    /// 获得触摸按钮停留时处于的位置
    /// </summary>
    private Rect GetTouchDockRect() =>
        new(TouchTransform.X,
            TouchTransform.Y,
            Touch.Width,
            Touch.Width);

    /// <summary>
    /// 设置触摸按钮停留时应处于的位置
    /// </summary>
    private void SetTouchDockRect(Rect rect, Size window)
    {
        //var centerPoint = ToCenterOrigin(new Point(rect.X, rect.Y), window);
        //(TouchTransform.X, TouchTransform.Y) = (centerPoint.X, centerPoint.Y);

        (TranslateXAnimation.To, TranslateYAnimation.To) = (rect.X, rect.Y);

        (TouchTransform.X, TouchTransform.Y) = (rect.X, rect.Y);
        Touch.Width = rect.Width;

        //SetWindowObservable?.Invoke(rect.XDpi(DpiScale));
    }
}

file static class AnimationTool
{
    /// <summary>
    /// 绑定动画到对象的属性
    /// </summary>
    public static void BindingAnimation(
        this Storyboard storyboard,
        Timeline animation,
        DependencyObject target,
        string path)
    {
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, path);
        storyboard.Children.Add(animation);
    }

    #region Touch Opacity Animations

    private static readonly TimeSpan OpacityFadeInDuration = TimeSpan.FromMilliseconds(1000);
    private static readonly TimeSpan OpacityFadeOutDuration = TimeSpan.FromMilliseconds(4000);
    private const double OpacityHalf = 0.4;
    private const double OpacityFull = 1;
    private static readonly DoubleAnimation FadeInAnimation = new()
    {
        From = OpacityHalf,
        To = OpacityFull,
        Duration = OpacityFadeInDuration,
    };
    private static readonly DoubleAnimation FadeOutAnimation = new()
    {
        From = OpacityFull,
        To = OpacityHalf,
        Duration = OpacityFadeOutDuration,
    };
    public static readonly Storyboard FadeInOpacityStoryboard = new();
    public static readonly Storyboard FadeOutOpacityStoryboard = new();

    public static readonly TimeSpan OpacityFadeDelay = TimeSpan.FromMilliseconds(4000);

    public static void InitializeOpacityAnimations(FrameworkElement touch)
    {
        FadeInOpacityStoryboard.BindingAnimation(FadeInAnimation, touch, nameof(FrameworkElement.Opacity));
        FadeOutOpacityStoryboard.BindingAnimation(FadeOutAnimation, touch, nameof(FrameworkElement.Opacity));
    }

    public static bool IsFullyOpaque(this UIElement element, double tolerance = 0.001)
        => Math.Abs(element.Opacity - OpacityFull) < tolerance;

    #endregion Touch Opacity Animations
}
