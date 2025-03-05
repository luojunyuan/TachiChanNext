using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using R3;
using R3.ObservableEvents;
using Windows.Foundation;

namespace TouchChan.WinUI;

// TouchControl 依赖 TouchDockAnchor, AnimationTool, TouchLayerMarginConverter(Xaml), PositionCalculator

public sealed partial class TouchControl
{
    private readonly ObservableAsPropertyHelper<TouchDockAnchor> _currentDockHelper;

    public TouchDockAnchor CurrentDock => _currentDockHelper.Value;
}

public sealed partial class TouchControl : UserControl
{
    private readonly TimeSpan ReleaseToEdgeDuration = TimeSpan.FromMilliseconds(200);
    private readonly Storyboard TranslationStoryboard = new();
    private readonly DoubleAnimation TranslateXAnimation;
    private readonly DoubleAnimation TranslateYAnimation;
    private readonly TimeSpan FadeOutDuration = TimeSpan.FromMilliseconds(4000);
    private readonly Storyboard FadeInOpacityStoryboard = new();
    private readonly Storyboard FadeOutOpacityStoryboard = new();

    // WAS Shit 5: Xaml Code-Behind 中 require 或 prop init 会生成错误的代码 #8723
    public Action<Size>? ResetWindowObservable { get; set; }

    public Action<Rect>? SetWindowObservable { get; set; }

    public Action? RestoreFocus { get; set; }

    public Subject<Unit> OnWindowBound { get; private set; } = new();

    // WAS Shit 6: DPI 改变后，XamlRoot.RasterizationScale 永远是启动时候的值
    private double DpiScale => this.XamlRoot.RasterizationScale;

    public TouchControl()
    {
        this.InitializeComponent();

        FrameworkElement container = this;

        this.TouchControlSubscribe(container, out _currentDockHelper);
        this.TouchDockSubscribe(container);

        TranslateXAnimation = new DoubleAnimation { Duration = ReleaseToEdgeDuration };
        TranslateYAnimation = new DoubleAnimation { Duration = ReleaseToEdgeDuration };
        AnimationTool.BindingAnimation(TranslationStoryboard, TranslateXAnimation, TouchTransform, AnimationTool.XProperty);
        AnimationTool.BindingAnimation(TranslationStoryboard, TranslateYAnimation, TouchTransform, AnimationTool.YProperty);
        AnimationTool.BindingAnimation(FadeInOpacityStoryboard, AnimationTool.CreateFadeInOpacityAnimation, Touch, nameof(Opacity));
        AnimationTool.BindingAnimation(FadeOutOpacityStoryboard, AnimationTool.CreateFadeOutOpacityAnimation, Touch, nameof(Opacity));
    }

    private void TouchControlSubscribe(FrameworkElement container, 
        out ObservableAsPropertyHelper<TouchDockAnchor> dockObservable)
    {
        var moveAnimationEndedStream = TranslationStoryboard.Events().Completed;

        var raisePointerReleasedSubject = new Subject<PointerRoutedEventArgs>();

        var pointerPressedStream = 
            Touch.Events().PointerPressed
            .Where(e => e.GetCurrentPoint(container).Properties.IsLeftButtonPressed)
            .Do(e => Touch.CapturePointer(e.Pointer));
        var pointerMovedStream = 
            Touch.Events().PointerMoved
            .Where(e => e.GetCurrentPoint(container).Properties.IsLeftButtonPressed);
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
                    var pressPos = pressEvent.GetPosition(container);
                    var movePos = moveEvent.GetPosition(container);
                    return pressPos != movePos;
                })
                .Take(1)
                .TakeUntil(pointerReleasedStream));

        var dragEndedStream =
            dragStartedStream
            .SelectMany(_ =>
                pointerReleasedStream
                .Take(1));

        // Time -->
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
                        var distanceToOrigin = movedEvent.GetPosition(container);
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
                var distanceToOrigin = pointer.GetPosition(container);
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

        // 回调设置容器窗口的可观察区域
        dragStartedStream
            .Select(_ => container.ActualSize.XDpi(DpiScale))
            .Subscribe(clientArea => ResetWindowObservable?.Invoke(clientArea));

        moveAnimationEndedStream
            .Do(_ => Touch.IsHitTestVisible = true)
            .Do(_ => RestoreFocus?.Invoke())
            .Select(_ => GetTouchDockRect().XDpi(DpiScale))
            .Subscribe(rect => SetWindowObservable?.Invoke(rect));

        // 调整按钮透明度
        pointerPressedStream
            .Where(_ => Touch.Opacity != 1)
            .Subscribe(_ => FadeInOpacityStoryboard.Begin());

        OnWindowBound
            .Merge(pointerReleasedStream.Select(_ => Unit.Default))
            .Merge(moveAnimationEndedStream.Select(_ => Unit.Default))
            .Select(_ =>
                Observable.Timer(FadeOutDuration)
                .TakeUntil(pointerPressedStream))
            .Switch()
            .ObserveOn(App.UISyncContext)
            .Subscribe(_ => FadeOutOpacityStoryboard.Begin());

        // 小白点停留时的位置状态
        dockObservable =
            moveAnimationEndedStream.Select(_ =>
                PositionCalculator.GetLastTouchDockAnchor(container.ActualSize.ToSize(), GetTouchDockRect()))
            .Merge(container.Events().SizeChanged.Select(_ => CurrentDock))
            .Select(dock => PositionCalculator.TouchDockTransform(dock, container.ActualSize.ToSize(), Touch.Width))
            .ToProperty(new(TouchCorner.Left, 0.5));
    }

    private void TouchDockSubscribe(FrameworkElement container)
    {
        var rectangleShape = false;
        Touch.Events().SizeChanged
            .Select(x => x.NewSize.Width)
            .Subscribe(touchSize => Touch.CornerRadius = new(touchSize / (rectangleShape ? 4 : 2)));

        container.Events().SizeChanged
            .Select(x => x.NewSize)
            .Select(window => new
            {
                WindowSize = window,
                TouchDock = CurrentDock,
                TouchSize = window.Width < 600 ? 60 : 80
            })
            .Select(pair => PositionCalculator.CalculateTouchDockRect(pair.WindowSize, pair.TouchDock, pair.TouchSize))
            .Subscribe(SetTouchDockRect);
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
    private void SetTouchDockRect(Rect rect)
    {
        (TouchTransform.X, TouchTransform.Y) = (rect.X, rect.Y);
        Touch.Width = rect.Width;

        SetWindowObservable?.Invoke(rect.XDpi(DpiScale));
    }
}

class AnimationTool
{
    // 在设置 storyboard 时使用并且确保绑定对象没有在 TransformGroup 里面 （需作为 RenderTransform 的单独元素使用）
    public const string XProperty = "X";
    public const string YProperty = "Y";

    /// <summary>
    /// 绑定动画到对象的属性
    /// </summary>
    public static void BindingAnimation(
        Storyboard storyboard,
        Timeline animation,
        DependencyObject target,
        string path)
    {
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, path);
        storyboard.Children.Add(animation);
    }

    private static readonly TimeSpan OpacityFadeInDuration = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan OpacityFadeOutDuration = TimeSpan.FromMilliseconds(400);
    private const double OpacityHalf = 0.4;
    private const double OpacityFull = 1;

    public static DoubleAnimation CreateFadeInOpacityAnimation => new()
    {
        From = OpacityHalf,
        To = OpacityFull,
        Duration = OpacityFadeInDuration,
    };

    public static DoubleAnimation CreateFadeOutOpacityAnimation => new()
    {
        From = OpacityFull,
        To = OpacityHalf,
        Duration = OpacityFadeOutDuration,
    };
}
