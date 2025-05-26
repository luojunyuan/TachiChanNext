using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using R3;
using R3.ObservableEvents;
using Windows.Foundation;

namespace TouchChan.WinUI.Sample;

public static partial class XamlConverter
{
    public static CornerRadius CircleCorner(double value, double factor) => double.IsNaN(value) ? default : new(value * factor);

    public static Thickness TouchLayerMargin(double value, string fractionFactor)
    {
        if (value is double.NaN || TryParseFraction(fractionFactor, out double factor) == false)
            return default;

        return new(value * factor);
    }

    public static double SizeMultiply(double value, double factor) => value * factor;

    private static bool TryParseFraction(string fraction, out double factor)
    {
        string[] parts = fraction.Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], out double numerator) &&
            double.TryParse(parts[1], out double denominator))
        {
            factor = numerator / denominator;
            return true;
        }

        factor = default;
        return false;
    }
}

public sealed partial class TouchControl // State
{
    private readonly ObservableAsPropertyHelper<TouchDockAnchor> _currentDockHelper;
    private readonly ObservableAsPropertyHelper<bool> _isTouchDockedHelper;
    private readonly ObservableAsPropertyHelper<bool> _isMenuOpenedHelper;

    /// <summary>
    /// 指示触摸按钮当前的停靠位置或者即将停靠的位置。
    /// </summary>
    public TouchDockAnchor CurrentDock => _currentDockHelper.Value;

    /// <summary>
    /// 指示触摸按钮是否停靠在窗口边缘。
    /// </summary>
    public bool IsTouchDocked => _isTouchDockedHelper.Value;

    /// <summary>
    /// 指示菜单是否处于打开状态。
    /// </summary>
    public bool IsMenuOpened => _isMenuOpenedHelper.Value;
}

public sealed partial class TouchControl : UserControl
{
    public TouchControl()
    {
        InitializeComponent();

        TouchDockSubscribe();
        TouchDraggingSubscribe(out var whenDragStarted, out var whenDragEnded);
        TouchOpacitySubscribe();

        _currentDockHelper = Observable.Merge(
            whenDragEnded.Select(_ => PositionCalculator.CalculateTouchFinalDockAnchor(
                this.Size(), TouchRect)),
            this.Events().SizeChanged.Select(
                x => PositionCalculator.TouchDockCornerRedirect(x.NewSize, CurrentDock, TouchRect.Width)))
            .ToProperty(initialValue: new(TouchCorner.Left, 0.5));

        _isTouchDockedHelper = Observable.Merge(
            whenDragStarted.Select(_ => false),
            TouchReleaseStoryboard.Events().Completed.Select(_ => true))
            .ToProperty(initialValue: true);

        _isMenuOpenedHelper = MenuTransitsCompleted
            .ToProperty(initialValue: false);

        //BindingMenuTransitionAnimations();

        // TODO: 
        // 1 测试 uwp 动画执行过程中改变 To
        // 2 测试空项目double转int，int转double，分别看aot和jit，x32dbg跑起来或者IDA静态
        // * (In Progress) 单独建立 Menu，全新构建这个控件
        // 4 窗口大小改变后的 from 不对。老生话题了
    }

    /// <summary>
    /// 触摸按钮停靠在窗口边缘时，窗口大小改变时自动刷新触摸按钮位置。
    /// </summary>
    private void TouchDockSubscribe()
    {
        // QUES: 为什么是 600
        const int windowWidthThreshold = 600;
        Observable.Merge(
            this.Events().SizeChanged.Where(_ => IsTouchDocked == true).Select(x => x.NewSize),
            TouchReleaseStoryboard.Events().Completed.Select(_ => this.Size()))
            .Select(window => PositionCalculator.CalculateTouchDockRect(
                window, CurrentDock, window.Width < windowWidthThreshold ? 60 : 80))
            .Subscribe(touchRect => TouchRect = touchRect);
    }

    private void TouchDraggingSubscribe(
        out Observable<PointerRoutedEventArgs> dragStartedStream,
        out Observable<PointerRoutedEventArgs> dragEndedStream)
    {
        var raisePointerReleasedSubject = new Subject<PointerRoutedEventArgs>();

        var pointerPressedStream =
            Touch.Events().PointerPressed
            .Where(e => e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            .Do(e => Touch.CapturePointer(e.Pointer))
            .Share();
        var pointerMovedStream =
            Touch.Events().PointerMoved
            .Where(e => e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            .Share();
        var pointerReleasedStream =
            Touch.Events().PointerReleased
            .Merge(raisePointerReleasedSubject)
            .Do(e => Touch.ReleasePointerCapture(e.Pointer))
            .Share();

        dragStartedStream =
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
                .TakeUntil(pointerReleasedStream))
            .Share();

        dragEndedStream =
            dragStartedStream
            .SelectMany(_ =>
                pointerReleasedStream
                .Take(1))
            .Share();

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
            })
            .Share();

        draggingStream
            .Select(item => item.Delta)
            .Subscribe(newPos => (TouchTransform.X, TouchTransform.Y) = (newPos.X, newPos.Y));

        // Touch 拖动边界检测
        var boundaryExceededStream =
            draggingStream
            .Where(item => PositionCalculator.IsBeyondBoundary(
                item.Delta, TouchRect.Width, this.Size()))
            .Select(item => item.MovedEvent);

        boundaryExceededStream
            .Subscribe(raisePointerReleasedSubject.OnNext);

        var moveAnimationStartedStream = dragEndedStream;

        // Touch 拖拽释放停靠到边缘的动画
        BindingDraggingReleaseAnimations();
        moveAnimationStartedStream
            .Select(_ => PositionCalculator.CalculateTouchFinalPosition(
                this.Size(), TouchRect))
            .Subscribe(StartTouchTranslateAnimation);

        //NewMethod(dragStartedStream, dragEndedStream, pointerPressedStream, pointerReleasedStream);
    }

    private void NewMethod(Observable<PointerRoutedEventArgs> dragStartedStream, Observable<PointerRoutedEventArgs> dragEndedStream, Observable<PointerRoutedEventArgs> pointerPressedStream, Observable<PointerRoutedEventArgs> pointerReleasedStream)
    {

        // 定义点击检测需要的参数
        const int holdTimeThreshold = 500; // 长按触发阈值 (ms)
        dragStartedStream.Do(_ => Debug.WriteLine("dragStartedStream")).Subscribe();
        dragEndedStream.Do(_ => Debug.WriteLine("dragEndedStream")).Subscribe();

        var dragStartedAlias = dragStartedStream;

        // 普通点击流：在hold时间内释放
        var clickStream = pointerPressedStream
            .SelectMany(pressEvent =>
            {
                // 记录按下时间
                var pressTime = DateTimeOffset.Now;

                return pointerReleasedStream
                    .Take(1)
                    .TakeUntil(dragStartedAlias) // 如果开始拖动，取消点击
                    .Where(_ => (DateTimeOffset.Now - pressTime).TotalMilliseconds < holdTimeThreshold); // 判断是否在阈值内释放
            })
            .Share();

        // 长按释放流：超过hold时间后释放
        var holdReleaseStream = pointerPressedStream
            .SelectMany(pressEvent =>
            {
                // 记录按下时间
                var pressTime = DateTimeOffset.Now;

                return pointerReleasedStream
                    .Take(1)
                    .TakeUntil(dragStartedAlias) // 如果开始拖动，取消
                    .Where(_ => (DateTimeOffset.Now - pressTime).TotalMilliseconds >= holdTimeThreshold); // 判断是否超过阈值
            })
            .Share();

        clickStream.Do(_ => Debug.WriteLine("clickStream")).Subscribe();

        BehaviorSubject<bool> _clickInProgress = new(false);
        var delayedClickStream = clickStream
            .ObserveOn(App.UISyncContext)
            // 只有当没有正在处理的点击时才允许新的点击
            .Where(_ => !_clickInProgress.Value)
            // 标记点击处理已开始
            .Do(_ => _clickInProgress.OnNext(true))
            // 延迟200毫秒
            .SelectMany(_ => Observable.Timer(TimeSpan.FromMilliseconds(200)).Select(__ => Unit.Default));

        delayedClickStream.ObserveOn(App.UISyncContext).Subscribe(_ =>
        {
            Menu.Visibility = Visibility.Visible;
            Touch.Visibility = Visibility.Collapsed;
            FakeTouchOpacityAnimation.From = OpacityFull;
            FakeTouchOpacityAnimation.To = 0;
            StartMenuTransitionAnimation();
        });

        Menu.Events().PointerPressed.Subscribe(_ =>
        {
            StartMenuTransitionAnimationReverse();
        });

        MenuTransitsCompleted
            .Do(_ => _clickInProgress.OnNext(false))
            .Do(isOpened => Debug.WriteLine($"{(isOpened ? "MenuOpened" : "MenuClosed")}"))
            .Where(isOpened => isOpened == false)
            .Subscribe(_ =>
            {
                Menu.Visibility = Visibility.Collapsed;
                Touch.Visibility = Visibility.Visible;
                //Menu.Width = TouchRect.Width * 5;
                MenuTransXAnimation.To = 0;
                MenuTransYAnimation.To = 0;
            });


        holdReleaseStream.Select(_ => Unit.Default).Do(_ => Debug.WriteLine("holdReleaseStream"))
           .Subscribe(TouchHoldReleaseSubject.OnNext);

        pointerPressedStream.Select(_ => Unit.Default).Subscribe(TouchPreviewPressedSubject.OnNext);
    }

    private readonly Subject<Unit> TouchPreviewPressedSubject = new ();
    private readonly Subject<Unit> TouchHoldReleaseSubject = new ();

    private void TouchOpacitySubscribe()
    {
        BindingOpacityAnimations(Touch);

        TouchPreviewPressedSubject.Subscribe(_ => StartTouchFadeInAnimation(Touch));

        Observable.Merge(
            MenuTransitsCompleted.Where(isOpened => isOpened == false).Select(_ => Unit.Default),
            TouchReleaseStoryboard.Events().Completed.Select(_ => Unit.Default),
            TouchHoldReleaseSubject,
            GameContext.WindowAttached)
            .Select(_ =>
                Observable.Timer(OpacityFadeDelay)
                .TakeUntil(TouchPreviewPressedSubject))
            .Switch()
            .Where(_ => IsTouchDocked == true)
            .ObserveOn(App.UISyncContext)
            .Subscribe(StartTouchFadeOutAnimation);
    }

    private Rect TouchRect
    {
        get => new(TouchTransform.X, TouchTransform.Y, Touch.Width, Touch.Width);
        set => (TouchTransform.X, TouchTransform.Y, Touch.Width) = (value.X, value.Y, value.Width);
    }
}

/// <summary>
/// 触摸按钮的动画逻辑，包含拖拽、释放、菜单过渡等动画。
/// </summary>
public sealed partial class TouchControl // Animation Dragging Release
{
    private static readonly TimeSpan ReleaseToEdgeDuration = TimeSpan.FromMilliseconds(200);
    private readonly Storyboard TouchReleaseStoryboard = new();
    private readonly DoubleAnimation ReleaseXAnimation = new() { Duration = ReleaseToEdgeDuration };
    private readonly DoubleAnimation ReleaseYAnimation = new() { Duration = ReleaseToEdgeDuration };

    private void BindingDraggingReleaseAnimations()
    {
        TouchReleaseStoryboard.BindingAnimation(ReleaseXAnimation, TouchTransform, nameof(TranslateTransform.X));
        TouchReleaseStoryboard.BindingAnimation(ReleaseYAnimation, TouchTransform, nameof(TranslateTransform.Y));
        TouchReleaseStoryboard.Events().Completed.Subscribe(_ => AnimationTool.InputBlocked.OnNext(false));
        // TODO
        this.Events().SizeChanged.Where(_ => IsTouchDocked == false).Subscribe(_ =>
        {
            var after = PositionCalculator.CalculateTouchFinalPosition(this.Size(), TouchRect);
            (ReleaseXAnimation.To, ReleaseYAnimation.To) = (after.X, after.Y);
        });
    }

    private void StartTouchTranslateAnimation(Point destination)
    {
        AnimationTool.InputBlocked.OnNext(true);
        (ReleaseXAnimation.To, ReleaseYAnimation.To) = (destination.X, destination.Y);
        TouchReleaseStoryboard.Begin();
    }
}

public sealed partial class TouchControl // Animation Menu Transition
{
    private readonly static TimeSpan MenuTransitsDuration = TimeSpan.FromMilliseconds(200);
    private readonly Storyboard MenuTransitionStoryboard = new();
    private readonly static PowerEase? UnifiedPowerFunction = null; // new() { EasingMode = EasingMode.EaseInOut, };
    private readonly DoubleAnimation MenuWidthAnimation = new() 
    { 
        EnableDependentAnimation = true, 
        Duration = MenuTransitsDuration,
        //EasingFunction = new PowerEase() { EasingMode = EasingMode.EaseInOut, Power = 3 },
    };
    private readonly DoubleAnimation MenuHeightAnimation = new() 
    { 
        EnableDependentAnimation = true, 
        Duration = MenuTransitsDuration, 
        //EasingFunction = new PowerEase() { EasingMode = EasingMode.EaseInOut, Power = 3 },
    };
    private readonly DoubleAnimation MenuTransXAnimation = new()
    { 
        EnableDependentAnimation = true, 
        Duration = MenuTransitsDuration, 
        //EasingFunction = new PowerEase() { EasingMode = EasingMode.EaseInOut, Power = 3 }, 
    };
    private readonly DoubleAnimation MenuTransYAnimation = new()
    {
        EnableDependentAnimation = true, 
        Duration = MenuTransitsDuration, 
        //EasingFunction = new PowerEase() { EasingMode = EasingMode.EaseInOut, Power = 3 },
    };
    private readonly DoubleAnimation FakeTouchOpacityAnimation = new()
    {
        From = OpacityFull,
        To = 0,
        Duration = MenuTransitsDuration,
    };

    private readonly Subject<bool> MenuTransitsCompleted = new();

    private void BindingMenuTransitionAnimations()
    {
        MenuTransitionStoryboard.BindingAnimation(MenuWidthAnimation, Menu, nameof(Width));
        MenuTransitionStoryboard.BindingAnimation(MenuHeightAnimation, Menu, nameof(Height));
        MenuTransitionStoryboard.BindingAnimation(MenuTransXAnimation, MenuTransform, nameof(TranslateTransform.X));
        MenuTransitionStoryboard.BindingAnimation(MenuTransYAnimation, MenuTransform, nameof(TranslateTransform.Y));
        MenuTransitionStoryboard.BindingAnimation(FakeTouchOpacityAnimation, FakeTouch, nameof(Opacity));
        MenuTransitionStoryboard.Events().Completed.Subscribe(_ =>
        {
            AnimationTool.InputBlocked.OnNext(false);
            MenuTransitsCompleted.OnNext(!IsMenuOpened);
        });
    }

    private void StartMenuTransitionAnimation()
    {
        AnimationTool.InputBlocked.OnNext(true);
        MenuWidthAnimation.To = MenuHeightAnimation.To = Menu.Width;
        MenuWidthAnimation.From = MenuHeightAnimation.From = TouchRect.Width;
        // NOTE: 以中心点为坐标系原点时，原点是触摸按钮的中心点，不再是触摸按钮的左上角
        (MenuTransXAnimation.From, MenuTransYAnimation.From) =
            (TouchRect.X - (this.Size().Width - TouchRect.Width) / 2,
            TouchRect.Y - (this.Size().Height - TouchRect.Height) / 2);
        MenuTransitionStoryboard.Begin();
    }

    private void StartMenuTransitionAnimationReverse()
    {
        AnimationTool.InputBlocked.OnNext(true);
        SwapAnimation(MenuWidthAnimation);
        SwapAnimation(MenuHeightAnimation);
        SwapAnimation(MenuTransXAnimation);
        SwapAnimation(MenuTransYAnimation);
        SwapAnimation(FakeTouchOpacityAnimation);
        MenuTransitionStoryboard.Begin();

        static void SwapAnimation(DoubleAnimation animation) =>
            (animation.To, animation.From) = (animation.From, animation.To);
    }
}

public sealed partial class TouchControl // Animation Opacity
{
    private static readonly TimeSpan OpacityFadeDelay = TimeSpan.FromMilliseconds(4000);

    private static readonly TimeSpan OpacityFadeInDuration = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan OpacityFadeOutDuration = TimeSpan.FromMilliseconds(400);
    private const double OpacityHalf = 0.4;
    private const double OpacityFull = 1;
    private readonly DoubleAnimation FadeInAnimation = new()
    {
        // From = {current}, 
        To = OpacityFull,
        Duration = OpacityFadeInDuration,
    };
    private readonly DoubleAnimation FadeOutAnimation = new()
    {
        From = OpacityFull,
        To = OpacityHalf,
        Duration = OpacityFadeOutDuration,
    };
    private readonly Storyboard FadeInOpacityStoryboard = new();
    private readonly Storyboard FadeOutOpacityStoryboard = new();

    private void BindingOpacityAnimations(UIElement element)
    {
        FadeInOpacityStoryboard.BindingAnimation(FadeInAnimation, element, nameof(Opacity));
        FadeOutOpacityStoryboard.BindingAnimation(FadeOutAnimation, element, nameof(Opacity));
        FadeInOpacityStoryboard.Events().Completed.Subscribe(_ => element.IsHitTestVisible = true);
    }

    /// <summary>
    /// 触摸按钮淡入动画，实现了动态从当前透明度开始变化。
    /// </summary>
    /// <remarks>FadeIn 动画过程中会锁定触摸按钮不可点击</remarks>
    private void StartTouchFadeInAnimation(UIElement element)
    {
        var currentOpacity = element.Opacity;

        var distance = OpacityFull - currentOpacity;
        if (distance <= 0) return;

        FadeInAnimation.From = currentOpacity;
        FadeInAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(
            OpacityFadeInDuration.TotalMilliseconds * (distance / (OpacityFull - OpacityHalf))
        ));

        element.IsHitTestVisible = false;
        FadeInOpacityStoryboard.Begin();
    }

    private void StartTouchFadeOutAnimation(Unit _) => FadeOutOpacityStoryboard.Begin();
}

public static partial class AnimationTool
{
    /// <summary>
    /// 指示是否需要阻止整个窗口的用户输入。
    /// </summary>
    public static Subject<bool> InputBlocked { get; } = new();

    /// <summary>
    /// 绑定动画到对象的属性。
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
}
