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
    // 在TouchControl类中添加此方法
    public double Multiply(double value, double factor) => value * factor;

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

        BindingMenuTransitionAnimations();
        MenuBackground.Events().PointerPressed.Subscribe(_ =>
        {
            StartMenuTransitionAnimation();
        });

        // TODO: 
        // 1 测试uwp动画执行过程中改变 To
        // 2 测试空项目double转int，int转double，分别看aot和jit，x32dbg跑起来或者IDA静态
        // 3 单独建立 Menu，全新构建这个控件

        //.Do(_ => _isAnimating.OnNext(true))
        //Touch.Events().PointerPressed.Where(p => p.GetCurrentPoint(this).Properties.IsLeftButtonPressed).Subscribe(_ => StartTouchFadeInAnimation(Touch));
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
            .Subscribe(StartTranslateAnimation);
    }

    private void TouchOpacitySubscribe()
    {
        BindingOpacityAnimations(Touch);

        Observable.Merge(
            TouchReleaseStoryboard.Events().Completed.Select(_ => Unit.Default),
            GameContext.WindowAttached)
            .Select(_ => Observable.Timer(OpacityFadeDelay))
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

public sealed partial class TouchControl // Animation Dragging Release
{
    private readonly static TimeSpan ReleaseToEdgeDuration = TimeSpan.FromMilliseconds(200);
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

    private void StartTranslateAnimation(Point destination)
    {
        ReleaseXAnimation.To = destination.X;
        ReleaseYAnimation.To = destination.Y;
        AnimationTool.InputBlocked.OnNext(true);
        TouchReleaseStoryboard.Begin();
    }
}

public sealed partial class TouchControl // Animation Menu Transition
{
    private readonly static TimeSpan MenuTransistDuration = TimeSpan.FromMilliseconds(20000);
    private readonly Storyboard MenuTransitionStoryboard = new();
    private readonly static PowerEase UnifiedPowerFunction = new() { EasingMode = EasingMode.EaseInOut };
    private readonly DoubleAnimation ScaleXAnimation = new() { From = 0.2, To = 1, Duration = MenuTransistDuration,/* EasingFunction = UnifiedPowerFunction*/ };
    private readonly DoubleAnimation ScaleYAnimation = new() { From = 0.2, To = 1, Duration = MenuTransistDuration,/* EasingFunction = UnifiedPowerFunction*/ };
    private readonly DoubleAnimation MenuTransXAnimation = new() { Duration = MenuTransistDuration, /*EasingFunction = UnifiedPowerFunction*/ };
    private readonly DoubleAnimation MenuTransYAnimation = new() { Duration = MenuTransistDuration, /*EasingFunction = UnifiedPowerFunction*/ };

    private void BindingMenuTransitionAnimations()
    {
        MenuTransitionStoryboard.BindingAnimation(ScaleXAnimation, ScaleTransform, nameof(ScaleTransform.ScaleX));
        MenuTransitionStoryboard.BindingAnimation(ScaleYAnimation, ScaleTransform, nameof(ScaleTransform.ScaleY));
        MenuTransitionStoryboard.BindingAnimation(MenuTransXAnimation, MenuTranslate, nameof(TranslateTransform.X));
        MenuTransitionStoryboard.BindingAnimation(MenuTransYAnimation, MenuTranslate, nameof(TranslateTransform.Y));

        // 400 -> 40
        // 80 -> 40
        // TODO: WARNIGN: 与scale动画的对抗本质上是要算出当前size的时候， corner被放大到了多少，我们要减回来
        // init: 400 -> 200
        var cornerRadiusAnimation = CreateAntiCornerScaleAnimation();
        MenuTransitionStoryboard.BindingAnimation(cornerRadiusAnimation, MenuBackground, nameof(Border.CornerRadius));
    }

    private void StartMenuTransitionAnimation()
    {
        // NOTE: 以中心点为坐标系原点时，原点是触摸按钮的中心点，不再是触摸按钮的左上角
        (MenuTransXAnimation.From, MenuTransYAnimation.From) =
            (TouchRect.X - (this.Size().Width - TouchRect.Width) / 2,
            TouchRect.Y - (this.Size().Height - TouchRect.Height) / 2);
        MenuTransitionStoryboard.Begin();

        Task.Run(async () =>
        {
            while(true)
            {
                await Task.Delay(1000);
                Observable.Return(Unit.Default)
                    .ObserveOn(App.UISyncContext)
                    .Do(_ => Debug.WriteLine($"{Menu.ActualHeight} {MenuBackground.ActualHeight}"))
                    .Subscribe();
                //Debug.WriteLine($"{Menu.ActualHeight} {MenuBackground.ActualHeight}");
            }
        });
    }

    private ObjectAnimationUsingKeyFrames CreateAntiCornerScaleAnimation()
    {
        // TODO: Definitions
        double startRadius = 200;    // 初始角度
        double endRadius = 40;     // 结束角度
        const int fps = 60;
        var totalDuration = MenuTransistDuration.TotalMilliseconds;

        var cornerRadiusAnimation = new ObjectAnimationUsingKeyFrames
        {
            Duration = MenuTransistDuration,
        };
        var frames = totalDuration / (1.0 / fps * 1000);
        var millisecondPerFrame = totalDuration / frames;

        for (var i = 0; i < frames; i++)
        {
            var progress = i / (frames - 1);
            //var frameTimeMillseconds = easeFunction.Ease(progress) * totalDuration;
            var frameTimeMillseconds = i * millisecondPerFrame;
            var currentRadius = startRadius + (endRadius - startRadius) * progress;

            var keyFrame = new DiscreteObjectKeyFrame
            {
                KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(frameTimeMillseconds)),
                Value = new CornerRadius(currentRadius, currentRadius, currentRadius, currentRadius)
            };

            cornerRadiusAnimation.KeyFrames.Add(keyFrame);
        }

        return cornerRadiusAnimation;
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
