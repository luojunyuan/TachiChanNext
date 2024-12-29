using System;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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

    // WAS Shit 5: Xaml Code-Behind 中 require 或 prop init 会生成错误的代码 #8723
    public Action<Size>? ResetWindowObservable { get; set; }

    public Action<Rect>? SetWindowObservable { get; set; }

    private readonly GameWindowService GameService;

    public TouchControl()
    {
        GameService = ServiceLocator.GameWindowService;

        this.InitializeComponent();

        this.RxLoaded()
            .Select(_ => this.FindParent<Panel>() ?? throw new InvalidOperationException())
            .Subscribe(InitializeTouchControl);

        TouchTransform.X = 2;
        TouchTransform.Y = 2;

        TranslateXAnimation = new DoubleAnimation { Duration = ReleaseToEdgeDuration };
        TranslateYAnimation = new DoubleAnimation { Duration = ReleaseToEdgeDuration };
        AnimationTool.BindingAnimation(TranslationStoryboard, TranslateXAnimation, TouchTransform, AnimationTool.XProperty);
        AnimationTool.BindingAnimation(TranslationStoryboard, TranslateYAnimation, TouchTransform, AnimationTool.YProperty);
    }

    private void InitializeTouchControl(Panel container)
    {
        var smoothMoveCompletedStream = TranslationStoryboard.RxCompleted();

        var raisePointerReleasedSubject = new Subject<PointerRoutedEventArgs>();

        var pointerPressedStream = Touch.RxPointerPressed();
        var pointerMovedStream = Touch.RxPointerMoved();
        var pointerReleasedStream = Touch.RxPointerReleased().Merge(raisePointerReleasedSubject);

        // FIXME: 连续点击两下按住，第一下的Release时间在200ms之后（也就是第二次按住后）触发了
        // 要确定释放过程中能不能被点击抓住
        pointerPressedStream
            .Select(_ => container.ActualSizeXDpi(GameService.DpiScale))
            .Subscribe(clientArea => ResetWindowObservable?.Invoke(clientArea));

        smoothMoveCompletedStream
            .Select(_ => GetTouchRect().XDpi(GameService.DpiScale))
            .Prepend(GetTouchRect().XDpi(GameService.DpiScale))
            .Subscribe(rect => SetWindowObservable?.Invoke(rect));

        // Touch 释放时的移动动画
        pointerReleasedStream
            .Select(pointer =>
            {
                var distanceToOrigin = pointer.GetCurrentPoint(container).Position.ToWarp();
                var distanceToElement = pointer.GetCurrentPoint(Touch).Position;
                var touchPos = distanceToOrigin - distanceToElement;
                return PositionCalculator.CalculateTouchFinalPosition(container.ActualSize.ToSize(), touchPos, (int)Touch.Width);
            })
            .Subscribe(stopPos =>
            {
                (TranslateXAnimation.To, TranslateYAnimation.To) = (stopPos.X, stopPos.Y);
                TranslationStoryboard.Begin();
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
                        var distanceToOrigin = movedEvent.GetCurrentPoint(container).Position.ToWarp();
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

        // Touch 拖动边界释放检测
        var boundaryExceededStream =
        draggingStream
            .Where(item => PositionCalculator.IsBeyondBoundary(item.Delta, Touch.Width, container.ActualSize.ToSize()))
            .Select(item => item.MovedEvent);

        boundaryExceededStream
            .Subscribe(raisePointerReleasedSubject.OnNext);
    }

    private Rect GetTouchRect() =>
        new((int)((TranslateTransform)Touch.RenderTransform).X,
            (int)((TranslateTransform)Touch.RenderTransform).Y,
            (int)Touch.Width,
            (int)Touch.Height);

}