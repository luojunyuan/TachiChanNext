using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using R3;
using R3.ObservableEvents;
using Windows.Foundation;

namespace TouchChan.WinUI.Sample;

public sealed partial class TouchControl // State
{
    private readonly ObservableAsPropertyHelper<TouchDockAnchor> _currentDockHelper;
    private readonly ObservableAsPropertyHelper<bool> _isTouchDockedHelper;

    /// <summary>
    /// 指示触摸按钮当前的停靠位置或者即将停靠的位置。
    /// </summary>
    public TouchDockAnchor CurrentDock => _currentDockHelper.Value;

    /// <summary>
    /// 指示触摸按钮是否停靠在窗口边缘。
    /// </summary>
    public bool IsTouchDocked => _isTouchDockedHelper.Value;
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
            _touchAnimationsController.TouchDockCompleted.Select(_ => true))
            .ToProperty(initialValue: true);

        // TODO: 
        // 1 测试空白 uwp 动画执行过程中改变 To 怎么做并应用到 TouchControl
        // * 单独建立 Menu，全新构建这个控件，保留 scale 和 width 两套动画，保证可替换的代码质量
        // 3 Menu 动画窗口大小改变后的 from 不对。老生话题了
        // 4 复原ScaleTransform的动画以备不需
        // * 测试触控上的 Dragging 以及各个交互的触发 log 情况
        // - 性能测试空项目 double 转 int，分别看 aot 和 jit，x32dbg跑起来或者IDA静态分析
    }

    /// <summary>
    /// 触摸按钮停靠在窗口边缘时，窗口大小改变时自动刷新触摸按钮位置。
    /// </summary>
    private void TouchDockSubscribe()
    {
        // QUES: 为什么是 600
        const int windowWidthThreshold = 600;
        Observable.Merge(
            // 小白点停靠时
            this.Events().SizeChanged.Where(_ => IsTouchDocked == true).Select(x => x.NewSize),
            // 小白点释放后
            _touchAnimationsController.TouchDockCompleted.Select(_ => this.Size()))
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
        _touchAnimationsController.BindingDraggingReleaseAnimations(TouchTransform);
        moveAnimationStartedStream
            .Select(_ => PositionCalculator.CalculateTouchFinalPosition(
                this.Size(), TouchRect))
            .Subscribe(_touchAnimationsController.StartTouchTranslateAnimation);

        pointerPressedStream.Select(_ => Unit.Default).Subscribe(_touchPreviewPressedSubject.OnNext);

        MenuSubscribe(dragStartedStream, dragEndedStream, pointerPressedStream, pointerReleasedStream);
    }

    private readonly Subject<Unit> _touchPreviewPressedSubject = new ();
    private readonly Subject<Unit> _touchHoldReleaseSubject = new ();
    private readonly TouchAnimations _touchAnimationsController = new();
    private readonly MenuAnimations _menuAnimationsController = new();

    private void TouchOpacitySubscribe()
    {
        var touchOpacityController = new TouchOpacityAnimation();    

        touchOpacityController.BindingOpacityAnimations(Touch);

        _touchPreviewPressedSubject.Subscribe(_ => touchOpacityController.StartTouchFadeInAnimation(Touch.Opacity));

        Observable.Merge(
            GameContext.WindowAttached,
            _touchAnimationsController.TouchDockCompleted,
            _menuAnimationsController.MenuClosed,
            _touchHoldReleaseSubject)
            .Select(_ =>
                Observable.Timer(TouchOpacityAnimation.OpacityFadeDelay)
                .TakeUntil(_touchPreviewPressedSubject))
            .Switch()
            .Where(_ => IsTouchDocked == true)
            .ObserveOn(App.UISyncContext)
            .Subscribe(touchOpacityController.StartTouchFadeOutAnimation);

        // System.ExecutionException
        //程序“[10356] TouchChan.WinUI.Sample.exe”已退出，返回值为 3221225477(0xc0000005) 'Access violation'。
    }

    private Rect TouchRect
    {
        get => new(TouchTransform.X, TouchTransform.Y, Touch.Width, Touch.Width);
        set => (TouchTransform.X, TouchTransform.Y, Touch.Width) = (value.X, value.Y, value.Width);
    }
}

public sealed partial class TouchControl // Menu Definition
{
    private void MenuSubscribe(Observable<PointerRoutedEventArgs> dragStartedStream, Observable<PointerRoutedEventArgs> dragEndedStream, Observable<PointerRoutedEventArgs> pointerPressedStream, Observable<PointerRoutedEventArgs> pointerReleasedStream)
    {
        _menuAnimationsController.BindingMenuTransitionAnimations(Menu, MenuTransform, FakeTouch);

        const int holdTimeThreshold = 500;

        var pressedHoldTimeStream =
            pointerPressedStream
            .SelectMany(pressEvent =>
            {
                var pressTime = DateTimeOffset.Now;

                return pointerReleasedStream
                    .Take(1)
                    .TakeUntil(dragStartedStream)
                    .Select(_ => (DateTimeOffset.Now - pressTime).TotalMilliseconds);
            })
            .Share();

        var clickStream =
            pressedHoldTimeStream
            .Where(holdTime => holdTime < holdTimeThreshold)
            .Share();

        var holdReleaseStream =
            pressedHoldTimeStream
            .Where(holdTime => holdTime >= holdTimeThreshold)
            .Share();

        holdReleaseStream.Select(_ => Unit.Default)
            .Subscribe(_touchHoldReleaseSubject.OnNext);

        clickStream.ObserveOn(App.UISyncContext).Subscribe(_ =>
        {
            Touch.Visibility = Visibility.Collapsed;
            _menuAnimationsController.StartMenuTransitionAnimation(TouchRect, this.Size());
        });

        Menu.Events().PointerPressed.Subscribe(_ => 
            _menuAnimationsController.StartMenuTransitionAnimation());

        _menuAnimationsController.MenuClosed
            .Subscribe(_ => Touch.Visibility = Visibility.Visible);
    }
}

/// <summary>
/// 触摸按钮的动画逻辑，包含拖拽、释放等动画。
/// </summary>
public class TouchAnimations
{
    public Observable<Unit> TouchDockCompleted => _touchReleaseStoryboard.Events().Completed
        .Select(_ => Unit.Default)
        .Share();

    private static readonly TimeSpan ReleaseToEdgeDuration = TimeSpan.FromMilliseconds(2000);
    private readonly Storyboard _touchReleaseStoryboard = new();
    private readonly DoubleAnimation _releaseXAnimation = new() { Duration = ReleaseToEdgeDuration };
    private readonly DoubleAnimation _releaseYAnimation = new() { Duration = ReleaseToEdgeDuration };

    public void BindingDraggingReleaseAnimations(TranslateTransform transform)
    {
        _touchReleaseStoryboard.BindingAnimation(_releaseXAnimation, transform, nameof(TranslateTransform.X));
        _touchReleaseStoryboard.BindingAnimation(_releaseYAnimation, transform, nameof(TranslateTransform.Y));
        _touchReleaseStoryboard.Events().Completed.Subscribe(_ => AnimationTool.InputBlocked.OnNext(false));
        //this.Events().SizeChanged.Where(_ => IsTouchDocked == false).Subscribe(_ =>
        //{
        //    var after = PositionCalculator.CalculateTouchFinalPosition(this.Size(), TouchRect);
        //    (_releaseXAnimation.To, _releaseYAnimation.To) = (after.X, after.Y);
        //});
    }

    public void StartTouchTranslateAnimation(Point destination)
    {
        AnimationTool.InputBlocked.OnNext(true);
        (_releaseXAnimation.To, _releaseYAnimation.To) = (destination.X, destination.Y);
        _touchReleaseStoryboard.Begin();
    }
}

public class MenuAnimations
{
    public Observable<Unit> MenuOpened => _menuOpendSubject
        .Where(isOpened => isOpened == true)
        .Select(_ => Unit.Default)
        .Share();

    public Observable<Unit> MenuClosed => _menuOpendSubject
        .Where(isOpened => isOpened == false)
        .Select(_ => Unit.Default)
        .Share();

    // NOTE: 因为 EnableDependentAnimation 导致菜单大小变化可能无法和位移动画完全同步

    private readonly static TimeSpan MenuTransitsDuration = TimeSpan.FromMilliseconds(200);
    private readonly Storyboard _menuTransitionStoryboard = new();
    private readonly DoubleAnimation _menuWidthAnimation = new() 
    { 
        EnableDependentAnimation = true, 
        Duration = MenuTransitsDuration,
    };
    private readonly DoubleAnimation _menuHeightAnimation = new() 
    { 
        EnableDependentAnimation = true, 
        Duration = MenuTransitsDuration, 
    };
    private readonly DoubleAnimation _menuTransXAnimation = new() { Duration = MenuTransitsDuration, };
    private readonly DoubleAnimation _menuTransYAnimation = new() { Duration = MenuTransitsDuration, };
    private readonly DoubleAnimation _fakeTouchOpacityAnimation = new()
    {
        From = 1,
        To = 0,
        Duration = MenuTransitsDuration,
    };

    private readonly BehaviorSubject<bool> _menuOpendSubject = new(false);

    public void BindingMenuTransitionAnimations(FrameworkElement menu, TranslateTransform transform, UIElement fakeTouch)
    {
        _menuTransitionStoryboard.BindingAnimation(_menuWidthAnimation, menu, nameof(FrameworkElement.Width));
        _menuTransitionStoryboard.BindingAnimation(_menuHeightAnimation, menu, nameof(FrameworkElement.Height));
        _menuTransitionStoryboard.BindingAnimation(_menuTransXAnimation, transform, nameof(TranslateTransform.X));
        _menuTransitionStoryboard.BindingAnimation(_menuTransYAnimation, transform, nameof(TranslateTransform.Y));
        _menuTransitionStoryboard.BindingAnimation(_fakeTouchOpacityAnimation, fakeTouch, nameof(UIElement.Opacity));
        _menuTransitionStoryboard.Events().Completed.Subscribe(_ =>
        {
            AnimationTool.InputBlocked.OnNext(false);
            _menuOpendSubject.OnNext(!_menuOpendSubject.Value);

            SwapAnimation(_menuWidthAnimation);
            SwapAnimation(_menuHeightAnimation);
            SwapAnimation(_menuTransXAnimation);
            SwapAnimation(_menuTransYAnimation);
            SwapAnimation(_fakeTouchOpacityAnimation);

            static void SwapAnimation(DoubleAnimation animation) =>
                (animation.To, animation.From) = (animation.From, animation.To);
        });
    }

    public void StartMenuTransitionAnimation(Rect touch, Size container)
    {
        AnimationTool.InputBlocked.OnNext(true);
        _menuWidthAnimation.To = _menuHeightAnimation.To = touch.Width * 5;
        _menuWidthAnimation.From = _menuHeightAnimation.From = touch.Width;
        // NOTE: 以中心点为坐标系原点时，原点是触摸按钮的中心点，不再是触摸按钮的左上角
        (_menuTransXAnimation.From, _menuTransYAnimation.From) =
            (touch.X - (container.Width - touch.Width) / 2,
            touch.Y - (container.Height - touch.Height) / 2);
        _menuTransitionStoryboard.Begin();
    }

    public void StartMenuTransitionAnimation()
    {
        AnimationTool.InputBlocked.OnNext(true);
        _menuTransitionStoryboard.Begin();
    }
}

public class TouchOpacityAnimation
{
    public static readonly TimeSpan OpacityFadeDelay = TimeSpan.FromMilliseconds(4000);

    private static readonly TimeSpan OpacityFadeInDuration = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan OpacityFadeOutDuration = TimeSpan.FromMilliseconds(400);
    private const double OpacityHalf = 0.4;
    private const double OpacityFull = 1;
    private readonly DoubleAnimation _fadeInAnimation = new()
    {
        // From = {current}, 
        To = OpacityFull,
        Duration = OpacityFadeInDuration,
    };
    private readonly DoubleAnimation _fadeOutAnimation = new()
    {
        From = OpacityFull,
        To = OpacityHalf,
        Duration = OpacityFadeOutDuration,
    };
    private readonly Storyboard _fadeInOpacityStoryboard = new();
    private readonly Storyboard _fadeOutOpacityStoryboard = new();

    public void BindingOpacityAnimations(UIElement element)
    {
        _fadeInOpacityStoryboard.BindingAnimation(_fadeInAnimation, element, nameof(UIElement.Opacity));
        _fadeOutOpacityStoryboard.BindingAnimation(_fadeOutAnimation, element, nameof(UIElement.Opacity));
    }

    /// <summary>
    /// 触摸按钮淡入动画，实现了动态从当前透明度开始变化。
    /// </summary>
    /// <remarks>FadeIn 动画过程中会锁定触摸按钮不可点击</remarks>
    public void StartTouchFadeInAnimation(double currentOpacity)
    {
        var distance = OpacityFull - currentOpacity;
        if (distance <= 0) return;

        _fadeInAnimation.From = currentOpacity;
        _fadeInAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(
            OpacityFadeInDuration.TotalMilliseconds * (distance / (OpacityFull - OpacityHalf))
        ));

        _fadeInOpacityStoryboard.Begin();
    }

    public void StartTouchFadeOutAnimation(Unit _) => _fadeOutOpacityStoryboard.Begin();
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

public static partial class XamlConverter
{
    public static Visibility VisibleInverse(Visibility value) => 
        value == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    public static CornerRadius CircleCorner(double value, double factor) => 
        double.IsNaN(value) ? default : new(value * factor);

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
