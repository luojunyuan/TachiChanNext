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
        TouchDraggingSubscribe(
            out Observable<PointerRoutedEventArgs> whenDragStarted,
            out Observable<PointerRoutedEventArgs> whenDragEnded);
        TouchOpacitySubscribe();

        _currentDockHelper = Observable.Merge(
            whenDragEnded.Select(pointer => PositionCalculator.CalculateTouchFinalDockAnchor(
                this.ActualSize.ToSize(), GetCurrentTouchRectByPointer(pointer))),
            this.Events().SizeChanged.Select(
                x => PositionCalculator.TouchDockCornerRedirect(x.NewSize, CurrentDock, Touch.Width)))
            .ToProperty(initialValue: new(TouchCorner.Left, 0.5));

        _isTouchDockedHelper = Observable.Merge(
            whenDragStarted.Select(_ => false),
            TouchReleaseStoryboard.Events().Completed.Select(_ => true))
            .ToProperty(initialValue: true);

        // TODO: 目前想到的俩种思路，在拖动释放做动画的时候，窗口大小改变了，在动画结束时，重新设置 Touch 位置
        // 或者在动画进行中，改变动画的 To 值

        // NOTE: 以中心点为坐标系原点时，原点是触摸按钮的中心点，不再是触摸按钮的左上角
        //var offset = new Point((this.ActualWidth - Touch.Width) / 2, (this.ActualHeight - Touch.Width) / 2);
        // 测试用的代码
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
            TouchReleaseStoryboard.Events().Completed.Select(_ => this.ActualSize.ToSize()))
            .Select(window => PositionCalculator.CalculateTouchDockRect(
                window, CurrentDock, window.Width < windowWidthThreshold ? 60 : 80))
            .Subscribe(SetTouchDockRect);
    }

    private void TouchDraggingSubscribe(
        out Observable<PointerRoutedEventArgs> dragStartedStream,
        out Observable<PointerRoutedEventArgs> dragEndedStream)
    {
        // Note: 默认了在拖拽时窗口大小不会发生改变

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
                .TakeUntil(pointerReleasedStream));

        dragEndedStream =
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
                item.Delta, Touch.Width, this.ActualSize.ToSize()))
            .Select(item => item.MovedEvent);

        boundaryExceededStream
            .Subscribe(raisePointerReleasedSubject.OnNext);

        var moveAnimationStartedStream = dragEndedStream;

        // Touch 拖拽释放停靠到边缘的动画
        BindingDraggingReleaseAnimations();
        moveAnimationStartedStream
            .Select(pointer => PositionCalculator.CalculateTouchFinalPosition(
                this.ActualSize.ToSize(), GetCurrentTouchRectByPointer(pointer)))
            .Subscribe(StartTranslateAnimation);
    }

    private void TouchOpacitySubscribe()
    {
        BindingOpacityAnimations(Touch);

        GameContext.WindowAttached
            .Select(_ => Observable.Timer(OpacityFadeDelay))
            .Switch()
            .ObserveOn(App.UISyncContext)
            .Subscribe(StartTouchFadeOutAnimation);
    }

    private Rect GetCurrentTouchRectByPointer(PointerRoutedEventArgs pointer)
    {
        var distanceToOrigin = pointer.GetPosition();
        var distanceToElement = pointer.GetPosition(Touch);
        var touchPos = distanceToOrigin.Subtract(distanceToElement);

        return new(touchPos, Touch.ActualSize.ToSize());
    }

    /// <summary>
    /// 设置触摸按钮停留边缘时应处于的位置。
    /// </summary>
    /// <param name="touchRect">描述 Touch 的坐标位置</param>
    private void SetTouchDockRect(Rect touchRect) =>
        (TouchTransform.X, TouchTransform.Y, Touch.Width)
            = (touchRect.X, touchRect.Y, touchRect.Width);
}

public sealed partial class TouchControl // Animation Dragging Release
{
    private readonly static TimeSpan ReleaseToEdgeDuration = TimeSpan.FromMilliseconds(2000);
    private readonly Storyboard TouchReleaseStoryboard = new();
    private readonly DoubleAnimation ReleaseXAnimation = new() { Duration = ReleaseToEdgeDuration };
    private readonly DoubleAnimation ReleaseYAnimation = new() { Duration = ReleaseToEdgeDuration };

    private void BindingDraggingReleaseAnimations()
    {
        TouchReleaseStoryboard.BindingAnimation(ReleaseXAnimation, TouchTransform, nameof(TranslateTransform.X));
        TouchReleaseStoryboard.BindingAnimation(ReleaseYAnimation, TouchTransform, nameof(TranslateTransform.Y));
        TouchReleaseStoryboard.Events().Completed.Subscribe(_ => AnimationTool.InputBlocked.OnNext(false));
    }

    private void StartTranslateAnimation(Point destination)
    {
        ReleaseXAnimation.To = destination.X;
        ReleaseYAnimation.To = destination.Y;
        AnimationTool.InputBlocked.OnNext(true);
        TouchReleaseStoryboard.Begin();
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
        element.IsHitTestVisible = false;

        var currentOpacity = element.Opacity;

        var distance = OpacityFull - currentOpacity;
        if (distance <= 0) return;

        FadeInAnimation.From = currentOpacity;
        FadeInAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(
            OpacityFadeInDuration.TotalMilliseconds * (distance / (OpacityFull - OpacityHalf))
        ));

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
