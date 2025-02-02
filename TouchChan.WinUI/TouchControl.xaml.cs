using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using R3;
using Windows.Foundation;

namespace TouchChan.WinUI;

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

    // WAS Shit 6: DPI 改变后，XamlRoot.RasterizationScale 永远是启动时候的值
    private double DpiScale => this.XamlRoot.RasterizationScale;

    public TouchControl()
    {
        this.InitializeComponent();

        this.RxLoaded()
            .Select(_ => this.FindParent<Panel>() ?? throw new InvalidOperationException())
            .Subscribe(InitializeTouchControl);

        TranslateXAnimation = new DoubleAnimation { Duration = ReleaseToEdgeDuration };
        TranslateYAnimation = new DoubleAnimation { Duration = ReleaseToEdgeDuration };
        AnimationTool.BindingAnimation(TranslationStoryboard, TranslateXAnimation, TouchTransform, AnimationTool.XProperty);
        AnimationTool.BindingAnimation(TranslationStoryboard, TranslateYAnimation, TouchTransform, AnimationTool.YProperty);
        AnimationTool.BindingAnimation(FadeInOpacityStoryboard, AnimationTool.CreateFadeInOpacityAnimation, Touch, nameof(Opacity));
        AnimationTool.BindingAnimation(FadeOutOpacityStoryboard, AnimationTool.CreateFadeOutOpacityAnimation, Touch, nameof(Opacity));
    }

    private void InitializeTouchControl(Panel container)
    {
        TouchDockSubscribe(container);

        var moveAnimationEndedStream = TranslationStoryboard.RxCompleted().Share();

        var raisePointerReleasedSubject = new Subject<PointerRoutedEventArgs>();

        var pointerPressedStream = Touch.RxPointerPressed().Do(e => Touch.CapturePointer(e.Pointer)).Share();
        var pointerMovedStream = Touch.RxPointerMoved().Share();
        var pointerReleasedStream =
            Touch.RxPointerReleased()
            .Merge(raisePointerReleasedSubject)
            .Do(e => Touch.ReleasePointerCapture(e.Pointer))
            .Share();

        var dragStartedStream =
            pointerPressedStream
            .SelectMany(_ =>
                pointerMovedStream
                .Skip(1)
                .Take(1)
                .TakeUntil(pointerReleasedStream));

        // NOTE: 这里 drag end 时机是依赖按下放开时的鼠标位置决定的
        var dragEndedStream =
            pointerReleasedStream
            .WithLatestFrom(pointerPressedStream, (releaseEvent, pressedEvent) =>
            {
                var releasePosition = releaseEvent.GetPosition(container);
                var pressedPosition = pressedEvent.GetPosition(container);

                return new { DragReleased = releaseEvent, EndPoint = releasePosition, StartPoint = pressedPosition, };
            })
            .Where(x => x.EndPoint != x.StartPoint)
            .Select(x => x.DragReleased);

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
                        var distanceToOrigin = movedEvent.GetPosition(container).Warp();
                        var delta = distanceToOrigin - distanceToElement;

                        return new { Delta = delta, MovedEvent = movedEvent };
                    });
            });

        draggingStream
            .Select(item => item.Delta)
            .Subscribe(newPos =>
            {
                TouchTransform.X = newPos.X;
                TouchTransform.Y = newPos.Y;
            });

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
                var distanceToOrigin = pointer.GetPosition(container).Warp();
                var distanceToElement = pointer.GetPosition(Touch);
                var touchPos = distanceToOrigin - distanceToElement;

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
            .Select(_ => container.ActualSizeXDpi(DpiScale))
            .Subscribe(clientArea => ResetWindowObservable?.Invoke(clientArea));

        moveAnimationEndedStream
            .Do(_ => Touch.IsHitTestVisible = true)
            .Select(_ => GetTouchRect().XDpi(DpiScale))
            .Subscribe(rect => SetWindowObservable?.Invoke(rect));

        // 调整按钮透明度
        pointerPressedStream
            .Where(_ => Touch.Opacity != 1)
            .Subscribe(_ => FadeInOpacityStoryboard.Begin());

        moveAnimationEndedStream.Select(_ => Unit.Default)
            .Merge(pointerReleasedStream.Select(_ => Unit.Default))
            .Prepend(Unit.Default)
            .Select(_ =>
                Observable.Timer(FadeOutDuration)
                .TakeUntil(pointerPressedStream))
            .Switch()
            .ObserveOn(App.UISyncContext)
            .Subscribe(_ => FadeOutOpacityStoryboard.Begin());
    }

    private void TouchDockSubscribe(Panel container)
    {
        var touchRectangleShape = false;
        Touch.RxSizeChanged()
            .Select(x => x.NewSize.Width)
            .Subscribe(touchSize => Touch.CornerRadius = new(touchSize / (touchRectangleShape ? 4 : 2)));

        var defaultDock = new TouchDockAnchor(TouchCorner.Left, 0.5);

        static double TouchWidth(double windowWidth) => windowWidth < 600 ? 60 : 80;

        container.RxSizeChanged()
            .Select(windowSize =>
            {
                var window = windowSize.NewSize;
                var touchDock = PositionCalculator.GetLastTouchDockAnchor(windowSize.PreviousSize, GetTouchRect());
                var touchWidth = TouchWidth(window.Width);
                return new { WindowSize = window, TouchDock = touchDock, TouchSize = touchWidth };
            })
            .Prepend(new
            {
                WindowSize = container.ActualSize.ToSize(),
                TouchDock = defaultDock,
                TouchSize = TouchWidth(container.ActualWidth),
            })
            .Select(pair => PositionCalculator.CaculateTouchDockRect(pair.WindowSize, pair.TouchDock, pair.TouchSize))
            .Subscribe(SetTouchDockRect);
    }

    private Rect GetTouchRect() =>
        new(TouchTransform.X,
            TouchTransform.Y,
            Touch.Width,
            Touch.Height);

    private void SetTouchDockRect(Rect rect)
    {
        (TouchTransform.X, TouchTransform.Y) = (rect.X, rect.Y);
        Touch.Width = rect.Width;

        SetWindowObservable?.Invoke(rect.XDpi(DpiScale));
    }
}

class AnimationTool
{
    // 在设置 storyboard 时使用并且确保绑定对象没有在 TransformGroup 里面
    public const string XProperty = "X";
    public const string YProperty = "Y";

    /// <summary>
    /// Binding animations in storyboard
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
